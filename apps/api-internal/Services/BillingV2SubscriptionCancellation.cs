using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kermaria.ApiInternal.Services;

// ---------------------------------------------------------------------------
// A. DECISION — quelle resiliation, et faut-il seulement parler au fournisseur
// ---------------------------------------------------------------------------

/// <summary>
/// Ancre fournisseur d'un abonnement Billing V2 : le triplet qui designe
/// l'objet a resilier chez l'operateur.
/// </summary>
/// <remarks>
/// Elle est resolue par <see cref="BillingV2ProviderAnchorPolicy"/> sur les
/// trois sources autoritaires. Son absence n'est PAS la preuve d'un achat
/// ponctuel : voir <see cref="BillingV2CancellationPolicy"/>.
/// </remarks>
public sealed record BillingV2ProviderAnchor(
    string Provider,
    string Environment,
    string? ProviderSubscriptionId);

/// <summary>
/// Gestes fournisseur possibles d'une resiliation.
/// </summary>
/// <remarks>
/// Le geste, et non un simple booleen « immediat », parce que la fin de terme
/// PayPal en demande DEUX : suspendre maintenant pour qu'aucun renouvellement
/// ne parte, puis resilier au terme. Seuls <see cref="CancelImmediate"/> et
/// <see cref="CancelAtTerm"/> autorisent la cloture locale : ce sont les deux
/// seuls apres lesquels le fournisseur ne peut plus rien prelever.
/// </remarks>
public static class BillingV2CancellationOperations
{
    public const string CancelImmediate = "cancel_immediate";
    public const string CancelAtPeriodEnd = "cancel_at_period_end";
    public const string SuspendPendingTermEnd = "suspend_pending_term_end";
    public const string CancelAtTerm = "cancel_at_term";

    /// <summary>
    /// Ce geste, une fois accepte, prouve-t-il que plus rien ne sera facture ?
    /// </summary>
    /// <remarks>
    /// <c>cancel_at_period_end</c> ne le prouve pas : Stripe a seulement promis
    /// de ne pas renouveler, la periode en cours court toujours.
    /// <c>suspend_pending_term_end</c> encore moins : une suspension PayPal se
    /// leve.
    /// </remarks>
    public static bool ClosesLocalSubscription(string operation)
        => operation is CancelImmediate or CancelAtTerm;

    public static bool IsKnown(string operation)
        => operation is CancelImmediate
            or CancelAtPeriodEnd
            or SuspendPendingTermEnd
            or CancelAtTerm;
}

public enum BillingV2CancellationMode
{
    /// <summary>Rien a resilier chez le fournisseur : achat purement ponctuel.</summary>
    NoProviderSubscription,

    /// <summary>Le fournisseur cesse de facturer a la fin de la periode payee.</summary>
    AtPeriodEnd,

    /// <summary>Le fournisseur resilie tout de suite.</summary>
    Immediate,

    /// <summary>
    /// Impossible de conclure sans risque financier. Aucun appel, aucune
    /// cloture locale : l'abonnement est laisse en l'etat pour reprise humaine.
    /// </summary>
    ManualReviewRequired
}

/// <summary>
/// Un geste fournisseur a mettre en file.
/// </summary>
/// <param name="AvailableAtUtc">
/// <c>null</c> = executable immediatement. Une date future rend l'evenement
/// dormant jusqu'a cet instant : c'est ainsi que la resiliation PayPal au terme
/// survit a un redemarrage, sans aucun minuteur en memoire.
/// </param>
public sealed record BillingV2CancellationAction(
    string Operation,
    DateTime? AvailableAtUtc);

/// <summary>
/// Etat contractuel lu au moment de la demande.
/// </summary>
/// <param name="HasRecurringComponent">
/// Vrai si le snapshot de composantes effectif porte au moins une cadence
/// <c>monthly</c>. C'est la seule preuve acceptable qu'un contrat n'a
/// legitimement aucun abonnement fournisseur.
/// </param>
public sealed record BillingV2CancellationContext(
    string Status,
    bool HasRecurringComponent,
    DateTime? StartedAtUtc,
    DateTime? CurrentPeriodEndsAtUtc,
    DateTime? RenewsAtUtc);

/// <summary>
/// Plan de resiliation : ce qu'on ecrit localement MAINTENANT, et les gestes
/// qu'on demande au fournisseur.
/// </summary>
/// <param name="LocalStatus">
/// Statut local pose immediatement. Il ne vaut <c>cancelled</c> que dans le
/// seul cas ou plus rien ne peut etre facture : aucune composante recurrente,
/// donc aucun abonnement fournisseur a resilier. Partout ailleurs il vaut
/// <c>pending_cancellation</c>, ou reste inchange si on refuse de conclure.
/// </param>
public sealed record BillingV2CancellationPlan(
    BillingV2CancellationMode Mode,
    string LocalStatus,
    bool CancelAtPeriodEnd,
    IReadOnlyList<BillingV2CancellationAction> ProviderActions,
    string ReasonCode)
{
    public bool RequiresProviderCall => ProviderActions.Count > 0;

    /// <summary>
    /// Vrai quand on refuse de conclure : ni appel fournisseur, ni ecriture de
    /// statut. L'abonnement doit rester exactement tel qu'il etait.
    /// </summary>
    public bool RequiresManualReview =>
        Mode is BillingV2CancellationMode.ManualReviewRequired;
}

/// <summary>
/// Regle de decision de la resiliation Billing V2.
/// </summary>
/// <remarks>
/// <para>
/// Trois invariants portes ici et nulle part ailleurs.
/// </para>
/// <para>
/// <b>1. Un statut local <c>cancelled</c> affirme que plus rien ne sera
/// facture.</b> Tant qu'un abonnement fournisseur existe, seul le fournisseur
/// peut rendre cette affirmation vraie. On pose donc <c>pending_cancellation</c>
/// et on attend sa confirmation. Poser <c>cancelled</c> des la demande
/// donnerait au client une page « resilie » pendant que le fournisseur
/// continue de prelever.
/// </para>
/// <para>
/// <b>2. L'absence d'ancre fournisseur ne prouve pas un achat ponctuel.</b>
/// C'est l'inference qui coute le plus cher : une ecriture manquee, une
/// reconciliation partielle ou un rail muet produisent exactement la meme
/// absence qu'un vrai one-shot. Seule l'absence de composante <c>monthly</c>
/// dans le snapshot effectif autorise a conclure qu'il n'y a rien a resilier.
/// Une composante recurrente sans ancre est un ECHEC FERME : on ne resilie pas
/// localement un contrat dont on a perdu la trace chez l'operateur.
/// </para>
/// <para>
/// <b>3. Une resiliation immediate n'est pas la valeur par defaut.</b> Une
/// periode deja payee a ete reglee : elle doit etre servie jusqu'a son terme.
/// Couper immediatement reviendrait a garder l'argent sans rendre le service.
/// Ce que « deja payee » veut dire se lit dans les DATES du contrat, pas dans
/// le libelle du statut : un abonnement <c>suspended</c> a pu etre actif, avoir
/// un abonnement fournisseur et une periode encore courante.
/// L'administration peut passer outre — c'est une decision humaine, tracee.
/// </para>
/// </remarks>
public static class BillingV2CancellationPolicy
{
    public const string AlreadyTerminalReasonCode =
        "BILLING_V2_CANCELLATION_ALREADY_TERMINAL";

    public const string NoProviderReasonCode =
        "BILLING_V2_CANCELLATION_NO_PROVIDER_SUBSCRIPTION";

    public const string AnchorMissingReasonCode =
        BillingV2ProviderAnchorPolicy.MissingReasonCode;

    public const string AnchorConflictReasonCode =
        BillingV2ProviderAnchorPolicy.ConflictReasonCode;

    public const string AtPeriodEndReasonCode =
        "BILLING_V2_CANCELLATION_SCHEDULED_AT_PERIOD_END";

    public const string ImmediateReasonCode =
        "BILLING_V2_CANCELLATION_IMMEDIATE_REQUESTED";

    private static readonly string[] TerminalStatuses =
        ["cancelled", "expired"];

    /// <summary>
    /// Statuts qui prouvent qu'aucune periode n'a jamais commence a etre
    /// servie. Ils ne sont qu'un garde-fou : la decision reelle se prend sur
    /// les dates.
    /// </summary>
    private static readonly string[] NeverActivatedStatuses =
    [
        "draft",
        "pending",
        "pending_approval",
        "pending_payment",
        "pending_activation"
    ];

    public static bool IsTerminal(string? status)
        => status is not null
           && TerminalStatuses.Contains(status, StringComparer.Ordinal);

    public static bool IsNeverActivated(string status)
        => NeverActivatedStatuses.Contains(status, StringComparer.Ordinal);

    /// <summary>
    /// Une periode payee est-elle encore en cours de service ?
    /// </summary>
    /// <remarks>
    /// Test purement temporel, volontairement independant du statut : c'est ce
    /// qui empeche de traiter un abonnement <c>suspended</c> comme « jamais
    /// active » et de lui couper une periode qu'il a reglee.
    /// </remarks>
    public static bool HasRunningPaidPeriod(
        BillingV2CancellationContext context,
        DateTime nowUtc)
        => context.StartedAtUtc is not null
           && context.CurrentPeriodEndsAtUtc is not null
           && context.CurrentPeriodEndsAtUtc > nowUtc;

    /// <param name="forceImmediate">
    /// Reserve a l'administration. Le client ne peut jamais l'exiger : il
    /// perdrait une periode qu'il a payee.
    /// </param>
    public static BillingV2CancellationPlan Resolve(
        BillingV2CancellationContext context,
        BillingV2ProviderAnchorResolution anchor,
        bool forceImmediate,
        DateTime nowUtc)
    {
        if (IsTerminal(context.Status))
        {
            return Inert(
                BillingV2CancellationMode.NoProviderSubscription,
                context.Status,
                AlreadyTerminalReasonCode);
        }

        // Les sources autoritaires se contredisent. Choisir l'une d'elles
        // reviendrait a agir sur un objet fournisseur possiblement etranger au
        // contrat. On ne touche a rien.
        if (anchor.Outcome is BillingV2ProviderAnchorOutcome.Conflict)
        {
            return Inert(
                BillingV2CancellationMode.ManualReviewRequired,
                context.Status,
                AnchorConflictReasonCode);
        }

        if (!anchor.IsResolved)
        {
            // Une composante mensuelle sans ancre : le fournisseur preleve
            // peut-etre encore, et on ne sait pas quoi lui demander. Fermer
            // localement fabriquerait un « resilie » mensonger.
            if (context.HasRecurringComponent)
            {
                return Inert(
                    BillingV2CancellationMode.ManualReviewRequired,
                    context.Status,
                    AnchorMissingReasonCode);
            }

            // Aucune cadence recurrente au snapshot effectif : le fournisseur a
            // encaisse un paiement, il n'a jamais cree d'abonnement. Inventer
            // un appel d'annulation fabriquerait un echec permanent sur un
            // objet qui n'a jamais existe.
            return Inert(
                BillingV2CancellationMode.NoProviderSubscription,
                "cancelled",
                NoProviderReasonCode);
        }

        var provider = anchor.Anchor!.Provider;
        var immediate = forceImmediate
            || IsNeverActivated(context.Status)
            || !HasRunningPaidPeriod(context, nowUtc);

        if (immediate)
        {
            return new BillingV2CancellationPlan(
                BillingV2CancellationMode.Immediate,
                "pending_cancellation",
                CancelAtPeriodEnd: false,
                [
                    new BillingV2CancellationAction(
                        BillingV2CancellationOperations.CancelImmediate,
                        AvailableAtUtc: null)
                ],
                ImmediateReasonCode);
        }

        return new BillingV2CancellationPlan(
            BillingV2CancellationMode.AtPeriodEnd,
            "pending_cancellation",
            CancelAtPeriodEnd: true,
            TermEndActions(provider, context.CurrentPeriodEndsAtUtc!.Value),
            AtPeriodEndReasonCode);
    }

    /// <remarks>
    /// <para>
    /// Stripe sait tenir la promesse tout seul : <c>cancel_at_period_end</c>
    /// est un etat cote fournisseur, il survit a nos redemarrages.
    /// </para>
    /// <para>
    /// PayPal n'a pas d'equivalent. On suspend donc immediatement — un
    /// abonnement suspendu ne se renouvelle pas — puis on met en file un SECOND
    /// geste, dormant jusqu'au terme, qui appellera reellement <c>/cancel</c>.
    /// Cet evenement est persiste dans l'outbox : il ne depend d'aucun minuteur
    /// en memoire, et un redemarrage entre la demande et le terme ne le perd
    /// pas.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<BillingV2CancellationAction> TermEndActions(
        string provider,
        DateTime periodEndsAtUtc)
        => string.Equals(provider, "paypal", StringComparison.Ordinal)
            ?
            [
                new BillingV2CancellationAction(
                    BillingV2CancellationOperations.SuspendPendingTermEnd,
                    AvailableAtUtc: null),
                new BillingV2CancellationAction(
                    BillingV2CancellationOperations.CancelAtTerm,
                    periodEndsAtUtc)
            ]
            :
            [
                new BillingV2CancellationAction(
                    BillingV2CancellationOperations.CancelAtPeriodEnd,
                    AvailableAtUtc: null)
            ];

    private static BillingV2CancellationPlan Inert(
        BillingV2CancellationMode mode,
        string localStatus,
        string reasonCode)
        => new(mode, localStatus, CancelAtPeriodEnd: false, [], reasonCode);
}

// ---------------------------------------------------------------------------
// B. CHARGE UTILE ET CLE D'IDEMPOTENCE DE L'EVENEMENT OUTBOX
// ---------------------------------------------------------------------------

public sealed record BillingV2CancellationOutboxPayload(
    string SubscriptionId,
    string Provider,
    string Environment,
    string ProviderSubscriptionId,
    string Operation,
    string Reason);

public static class BillingV2CancellationOutbox
{
    public const string EventType =
        "billing_v2.provider_subscription.cancel_requested";

    public const string AggregateType = "billing_v2_subscription";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static string Serialize(BillingV2CancellationOutboxPayload payload)
        => JsonSerializer.Serialize(payload, JsonOptions);

    public static BillingV2CancellationOutboxPayload Parse(string payloadText)
    {
        var payload =
            JsonSerializer.Deserialize<BillingV2CancellationOutboxPayload>(
                payloadText,
                JsonOptions)
            ?? throw new InvalidOperationException(
                "BILLING_V2_CANCELLATION_PAYLOAD_INVALID");

        // Un geste inconnu ne doit jamais atteindre l'executeur : il y serait
        // traduit par defaut, et un defaut sur une resiliation se paie.
        if (!BillingV2CancellationOperations.IsKnown(payload.Operation))
        {
            throw new InvalidOperationException(
                "BILLING_V2_CANCELLATION_OPERATION_UNKNOWN");
        }

        return payload;
    }

    /// <summary>
    /// Cle d'idempotence de la demande de resiliation.
    /// </summary>
    /// <remarks>
    /// Le geste fait partie de la cle. Deux clics sur « resilier » produisent
    /// la meme cle, donc un seul appel fournisseur. Mais la suspension immediate
    /// et la resiliation au terme d'une meme demande PayPal sont deux gestes
    /// distincts : ils doivent coexister dans l'outbox, pas s'annuler.
    /// </remarks>
    public static string ComputeIdempotencyHash(
        BillingV2CancellationOutboxPayload payload)
    {
        var raw = string.Join(
            "|",
            "billing-v2-provider-cancellation",
            payload.Provider.Trim().ToLowerInvariant(),
            payload.Environment.Trim().ToLowerInvariant(),
            payload.ProviderSubscriptionId.Trim(),
            payload.Operation.Trim().ToLowerInvariant());
        return Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
            .ToLowerInvariant();
    }
}

// ---------------------------------------------------------------------------
// C. APPEL FOURNISSEUR
// ---------------------------------------------------------------------------

public sealed record BillingV2ProviderCancellationRequest(
    string Provider,
    string Environment,
    string ProviderSubscriptionId,
    string Operation,
    string Reason);

public sealed record BillingV2ProviderCancellationResult(
    bool Succeeded,
    string Code,
    string? ErrorMessage,
    bool Retryable);

/// <summary>
/// Verification que l'environnement PERSISTE est bien celui avec lequel le
/// processus va reellement appeler.
/// </summary>
/// <remarks>
/// <para>
/// Verifier que le couple <c>(provider, environment)</c> est theoriquement
/// possible ne suffit pas. Un abonnement persiste en <c>stripe/live</c> appele
/// avec une cle Stripe TEST recoit un <c>404</c> parfaitement legitime — l'objet
/// n'existe pas dans cet environnement — que la lecture naive du code HTTP
/// interpreterait comme « deja absent, convergence atteinte ». On cloturerait
/// alors un abonnement LIVE toujours preleve.
/// </para>
/// <para>
/// C'est pour cela que le controle se fait AVANT tout appel : il n'existe aucun
/// moyen de rattraper ensuite l'ambiguite d'un 404. Un desaccord n'est pas
/// retryable — la configuration du processus ne changera pas d'elle-meme — et
/// laisse l'abonnement en revue manuelle.
/// </para>
/// </remarks>
public static class BillingV2ProviderRuntimeEnvironmentPolicy
{
    public const string MismatchCode =
        "BILLING_V2_PROVIDER_RUNTIME_ENVIRONMENT_MISMATCH";

    /// <returns>
    /// <c>null</c> si l'appel peut partir ; sinon le refus a renvoyer tel quel.
    /// </returns>
    public static BillingV2ProviderCancellationResult? Check(
        string provider,
        string persistedEnvironment,
        string? runtimeEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(runtimeEnvironment)
            && string.Equals(
                persistedEnvironment.Trim(),
                runtimeEnvironment.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new BillingV2ProviderCancellationResult(
            false,
            MismatchCode,
            $"Subscription is persisted on '{provider}/{persistedEnvironment}' "
                + $"but this process runs '{provider}/"
                + $"{runtimeEnvironment ?? "unconfigured"}'. No provider call "
                + "was made.",
            Retryable: false);
    }
}

public interface IBillingV2ProviderCancellationExecutor
{
    bool CanExecute { get; }

    Task<BillingV2ProviderCancellationResult> CancelAsync(
        BillingV2ProviderCancellationRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Executeur inerte. Il ECHOUE, il ne reussit pas silencieusement : sans acces
/// fournisseur configure, la resiliation reste due et l'evenement outbox
/// repartira.
/// </summary>
public sealed class DisabledBillingV2ProviderCancellationExecutor
    : IBillingV2ProviderCancellationExecutor
{
    public static DisabledBillingV2ProviderCancellationExecutor Instance { get; }
        = new();

    private DisabledBillingV2ProviderCancellationExecutor()
    {
    }

    public bool CanExecute => false;

    public Task<BillingV2ProviderCancellationResult> CancelAsync(
        BillingV2ProviderCancellationRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(new BillingV2ProviderCancellationResult(
            false,
            "BILLING_V2_PROVIDER_CANCELLATION_EXECUTOR_DISABLED",
            "Billing V2 provider cancellation executor is disabled.",
            Retryable: true));
}

/// <summary>
/// Traduction de la reponse fournisseur en decision d'outbox.
/// </summary>
/// <remarks>
/// Un echec RETRYABLE remet l'evenement en file : le fournisseur sera
/// rappele. Un echec NON retryable le met en echec definitif et laisse
/// l'abonnement en <c>pending_cancellation</c> — visible, donc traitable.
/// Dans aucun des deux cas l'etat local ne devient <c>cancelled</c>.
/// </remarks>
public static class BillingV2CancellationDispatchPolicy
{
    public const int BaseRetryDelayMinutes = 5;
    public const int MaxRetryCount = 8;

    public static BillingV2ProviderOutboxUpdate Resolve(
        BillingV2ProviderCancellationResult result,
        int retryCount)
    {
        if (result.Succeeded)
        {
            return new BillingV2ProviderOutboxUpdate(
                "processed",
                0,
                null);
        }

        if (!result.Retryable || retryCount >= MaxRetryCount)
        {
            return new BillingV2ProviderOutboxUpdate(
                "failed",
                0,
                result.ErrorMessage ?? result.Code);
        }

        return new BillingV2ProviderOutboxUpdate(
            "pending",
            BaseRetryDelayMinutes * (retryCount + 1),
            result.ErrorMessage ?? result.Code);
    }
}
