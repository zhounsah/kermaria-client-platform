using System.Security.Cryptography;
using System.Text;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Politiques pures du hardening Phase 2.5 : reconciliation Stripe, cycles de
/// renouvellement, et idempotence d'emission documentaire BPCE.
///
/// Aucune ne touche la base ni le reseau : elles sont la specification
/// executable de ce que les services ont le droit de faire.
/// </summary>

// ---------------------------------------------------------------------------
// 3. RECONCILIATION STRIPE
// ---------------------------------------------------------------------------

public static class BillingV2ReconciliationStatuses
{
    public const string ReconciliationRequired = "reconciliation_required";
}

public sealed record BillingV2ReconciliationCandidate(
    string AttemptId,
    string BillingEventId,
    string Status,
    string? ProviderSessionId,
    int ReconciliationAttempts,
    // Phase 3 : un renouvellement n'a pas de session checkout. Il se relit par
    // l'invoice, d'ou ces identifiants persistes - la relecture reste bornee.
    string? ProviderInvoiceId = null,
    string? ProviderSubscriptionId = null);

public sealed record BillingV2ReconciliationDecision(
    bool ShouldRefetch,
    string ReasonCode,
    int NextDelaySeconds,
    bool GiveUp);

/// <summary>
/// Selection et cadence du reconciliateur.
///
/// Principe : le webhook reste un signal ; le reconciliateur est le filet qui
/// garantit la convergence quand ce signal n'arrive jamais. Il ne cree jamais
/// de checkout, il ne fait que relire.
/// </summary>
public static class BillingV2ReconciliationPolicy
{
    /// <summary>
    /// Au-dela, on cesse de repoller et on demande une revue humaine plutot
    /// que de boucler indefiniment sur un objet qui ne convergera pas seul.
    /// </summary>
    public const int MaxAttempts = 12;

    public const string RefetchReasonCode =
        "BILLING_V2_RECONCILIATION_REFETCH";

    /// <summary>
    /// Etats sur lesquels une reconciliation a du sens : la tentative est
    /// partie chez Stripe mais n'a pas atteint d'etat terminal local.
    /// </summary>
    public static readonly IReadOnlySet<string> ReconcilableStatuses =
        new HashSet<string>(StringComparer.Ordinal)
        {
            BillingV2PaymentAttemptStatuses.Created,
            BillingV2PaymentAttemptStatuses.InFlight
        };

    public static BillingV2ReconciliationDecision Evaluate(
        BillingV2ReconciliationCandidate candidate)
    {
        if (!ReconcilableStatuses.Contains(candidate.Status))
        {
            return new BillingV2ReconciliationDecision(
                false,
                "BILLING_V2_RECONCILIATION_STATUS_TERMINAL",
                0,
                GiveUp: false);
        }

        // Phase 3 : un renouvellement n'a pas de session checkout, il se relit
        // par l'invoice ou l'abonnement provider. La question n'est donc plus
        // "y a-t-il une session ?" mais "a-t-on un identifiant persiste a
        // relire ?". Sans aucun, il n'y a rien a lire - et surtout rien a
        // recreer : le dispatch normal reprend la main.
        var lookup = BillingV2StripeSessionLookupPolicy.Plan(
            new BillingV2StripeSessionLocator(
                candidate.ProviderSessionId,
                candidate.ProviderInvoiceId,
                candidate.ProviderSubscriptionId,
                candidate.AttemptId));
        if (!lookup.CanLookup)
        {
            return new BillingV2ReconciliationDecision(
                false,
                "BILLING_V2_RECONCILIATION_NO_PROVIDER_SESSION",
                0,
                GiveUp: false);
        }

        if (candidate.ReconciliationAttempts >= MaxAttempts)
        {
            return new BillingV2ReconciliationDecision(
                false,
                "BILLING_V2_RECONCILIATION_EXHAUSTED",
                0,
                GiveUp: true);
        }

        return new BillingV2ReconciliationDecision(
            true,
            RefetchReasonCode,
            NextDelaySeconds(candidate.ReconciliationAttempts),
            GiveUp: false);
    }

    /// <summary>
    /// Backoff exponentiel plafonne : 1 min, 2, 4, 8, 16, puis 30 min.
    /// </summary>
    public static int NextDelaySeconds(int attempts)
    {
        var normalized = Math.Max(0, Math.Min(attempts, 10));
        var seconds = 60L << Math.Min(normalized, 5);
        return (int)Math.Min(seconds, 1800L);
    }
}

// ---------------------------------------------------------------------------
// 4. MODELE DE RENOUVELLEMENT
// ---------------------------------------------------------------------------

public sealed record BillingV2RenewalCycle(
    string SubscriptionId,
    int CycleSequence,
    BillingV2ContractPeriod Period,
    string IdempotencyKeyCanonical);

/// <summary>
/// Un renouvellement est identifie par (subscription_id, cycle_sequence).
///
/// Jamais par l'heure courante : c'est ce qui empeche deux workers, ou un
/// rattrapage manuel, de facturer deux fois le meme cycle. Deux tentatives sur
/// le cycle 17 produisent la meme cle d'idempotence et entrent donc en
/// collision sur l'unicite en base, au lieu de creer deux evenements.
/// </summary>
public static class BillingV2RenewalPolicy
{
    public const string Prefix = "billing_v2.billing_event|renewal_charge";

    /// <summary>Cycle de la charge initiale.</summary>
    public const int InitialCycleSequence = 1;

    public static BillingV2RenewalCycle ResolveCycle(
        string subscriptionId,
        DateTime anchorUtc,
        int monthsPerCycle,
        int cycleSequence)
    {
        if (cycleSequence <= InitialCycleSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cycleSequence),
                "Le cycle 1 est la charge initiale, pas un renouvellement.");
        }

        var period = BillingV2BillingCalendar.ResolveCyclePeriod(
            anchorUtc,
            monthsPerCycle,
            cycleSequence);
        return new BillingV2RenewalCycle(
            subscriptionId,
            cycleSequence,
            period,
            Canonical(subscriptionId, cycleSequence));
    }

    public static string Canonical(string subscriptionId, int cycleSequence)
        => $"{Prefix}|{subscriptionId}|{cycleSequence}";

    public static string Hash(string canonical)
        => Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();

    /// <summary>
    /// Rang du cycle couvrant un instant donne, derive de l'ancre contractuelle.
    /// Sert au planificateur : il calcule quel cycle est du, il ne devine pas
    /// une periode a partir de l'heure courante.
    /// </summary>
    public static int CycleSequenceAt(
        DateTime anchorUtc,
        int monthsPerCycle,
        DateTime instantUtc)
    {
        if (monthsPerCycle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthsPerCycle));
        }

        var anchor = BillingV2BillingCalendar.CivilDate(anchorUtc);
        var instant = BillingV2BillingCalendar.CivilDate(instantUtc);
        if (instant < anchor)
        {
            return InitialCycleSequence;
        }

        var months = ((instant.Year - anchor.Year) * 12)
            + instant.Month - anchor.Month;
        if (instant.Day < anchor.Day)
        {
            months -= 1;
        }

        return Math.Max(InitialCycleSequence, (months / monthsPerCycle) + 1);
    }
}

// ---------------------------------------------------------------------------
// 5. IDEMPOTENCE D'EMISSION DOCUMENTAIRE (BPCE)
// ---------------------------------------------------------------------------

public static class BillingV2DocumentIssuanceStatuses
{
    public const string Created = "created";
    public const string InFlight = "in_flight";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string ReconciliationRequired = "reconciliation_required";
}

public sealed record BillingV2DocumentIssuanceAttempt(
    string Id,
    string CommercialDocumentId,
    string ExternalReference,
    string Status,
    string? ProviderInvoiceId,
    int AttemptCount);

public sealed record BillingV2DocumentIssuanceDecision(
    bool CanCallProvider,
    string ReasonCode,
    bool RequiresManualReview,
    string? Diagnostic = null);

/// <summary>
/// Ferme la fenetre du double numero fiscal.
///
/// L'intention d'emission est ecrite AVANT l'appel BPCE, avec une reference
/// externe stable derivee du document. Apres un retour indetermine, la reprise
/// n'a le droit de recreer QUE si elle a pu prouver, par recherche sur cette
/// reference, qu'aucune facture n'existe deja. Si l'API BPCE ne permet pas
/// cette recherche, on echoue en ferme vers une revue humaine : une facture
/// manquante se rattrape, un second numero fiscal ne se reprend pas.
/// </summary>
public static class BillingV2DocumentIssuancePolicy
{
    public const int MaxAttempts = 5;

    /// <summary>
    /// Ce que l'API BPCE permet reellement aujourd'hui, constate dans
    /// <c>LiveBpceInvoicingService</c> / <c>IBpceInvoicingService</c> :
    ///
    /// - CLIENTS : recherche par identifiant externe disponible
    ///   (<c>GetCustomerByExternalIdAsync</c>), utilisee pour rendre
    ///   <c>UpsertCustomerAsync</c> idempotent ;
    /// - FACTURES : AUCUNE methode de recherche ou de liste. Le contrat expose
    ///   uniquement create draft, validate, get PDF, mark as paid.
    ///
    /// Consequence : apres un appel de creation au sort indetermine, il est
    /// impossible de prouver par l'API si le brouillon existe deja. On ne
    /// recree donc pas, on passe la main a une revue humaine.
    ///
    /// Note sur la numerotation : le numero fiscal est alloue par BPCE au
    /// moment de <c>ValidateInvoiceAsync</c>, pas a la creation du brouillon.
    /// Un brouillon orphelin ne consomme donc pas de numero ; c'est la
    /// validation d'un SECOND brouillon qui creerait un second numero. Le
    /// garde-fou porte donc sur les deux etapes.
    ///
    /// Passer ce drapeau a true exige d'abord d'ajouter une vraie recherche
    /// facture a <c>IBpceInvoicingService</c> et de documenter son comportement.
    /// </summary>
    public const bool InvoiceLookupByExternalReferenceSupported = false;

    /// <summary>
    /// Reference stable, deterministe, portee a BPCE comme reference externe.
    /// Deux tentatives sur le meme document produisent la meme valeur.
    /// </summary>
    public static string BuildExternalReference(string commercialDocumentId)
        => $"BV2-DOC-{commercialDocumentId}";

    public static BillingV2DocumentIssuanceDecision Evaluate(
        BillingV2DocumentIssuanceAttempt? attempt)
    {
        if (attempt is null)
        {
            // Aucune intention persistee : interdiction d'appeler BPCE. C'est
            // exactement le trou qui produisait la seconde facture.
            return new BillingV2DocumentIssuanceDecision(
                false,
                "BILLING_V2_DOCUMENT_ISSUANCE_NOT_PERSISTED",
                RequiresManualReview: false);
        }

        return attempt.Status switch
        {
            BillingV2DocumentIssuanceStatuses.Succeeded =>
                new BillingV2DocumentIssuanceDecision(
                    false,
                    "BILLING_V2_DOCUMENT_ISSUANCE_ALREADY_SUCCEEDED",
                    RequiresManualReview: false),
            BillingV2DocumentIssuanceStatuses.ReconciliationRequired =>
                new BillingV2DocumentIssuanceDecision(
                    false,
                    "BILLING_V2_DOCUMENT_ISSUANCE_RECONCILIATION_REQUIRED",
                    RequiresManualReview: true),
            _ when attempt.AttemptCount >= MaxAttempts =>
                new BillingV2DocumentIssuanceDecision(
                    false,
                    "BILLING_V2_DOCUMENT_ISSUANCE_EXHAUSTED",
                    RequiresManualReview: true),
            _ => new BillingV2DocumentIssuanceDecision(
                true,
                "BILLING_V2_DOCUMENT_ISSUANCE_MAY_CALL_PROVIDER",
                RequiresManualReview: false)
        };
    }

    /// <summary>
    /// Que faire apres un appel dont on ignore le sort (timeout, coupure).
    ///
    /// <paramref name="lookupSupported"/> dit si l'API BPCE courante permet de
    /// rechercher une facture par reference externe. Tant que ce n'est pas le
    /// cas, la seule issue sure est la revue humaine.
    /// </summary>
    public static BillingV2DocumentIssuanceDecision ResolveIndeterminate(
        bool lookupSupported,
        bool lookupFoundExistingInvoice)
    {
        if (!lookupSupported)
        {
            return new BillingV2DocumentIssuanceDecision(
                false,
                "BILLING_V2_DOCUMENT_ISSUANCE_INDETERMINATE_NO_LOOKUP",
                RequiresManualReview: true,
                "L'API BPCE ne permet pas de rechercher par reference externe : "
                + "recreer risquerait un second numero fiscal.");
        }

        return lookupFoundExistingInvoice
            ? new BillingV2DocumentIssuanceDecision(
                false,
                "BILLING_V2_DOCUMENT_ISSUANCE_RECOVERED",
                RequiresManualReview: false,
                "Facture deja creee cote BPCE, rattachee sans nouvel appel.")
            : new BillingV2DocumentIssuanceDecision(
                true,
                "BILLING_V2_DOCUMENT_ISSUANCE_SAFE_TO_RETRY",
                RequiresManualReview: false,
                "Recherche par reference externe concluante : aucune facture.");
    }
}
