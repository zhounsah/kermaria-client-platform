namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Cycle de vie du renouvellement Stripe - Phase 3, couche PURE.
///
/// Tout ce qui decide d'un montant de renouvellement, d'un rang de cycle, d'un
/// etat d'impaye ou d'une strategie de relecture vit ici, sans base ni reseau.
///
/// Deux regles structurantes :
///
/// 1. un renouvellement se derive du CONTRAT (items et prix verrouilles a la
///    souscription) et du rang du cycle, jamais du catalogue courant ni de
///    l'heure courante ;
/// 2. un impaye produit un etat local visible, jamais un retrait automatique
///    d'acces.
///
/// Specification : docs/billing-v2/RENEWAL-LIFECYCLE.md
/// </summary>

// ---------------------------------------------------------------------------
// 7. ETAT DE PAIEMENT LOCAL (POLITIQUE DE GRACE V2.0)
// ---------------------------------------------------------------------------

public static class BillingV2SubscriptionPaymentStates
{
    /// <summary>Aucun incident de paiement connu.</summary>
    public const string Current = "current";

    /// <summary>
    /// Un encaissement de renouvellement a echoue ou n'a pas pu etre prouve.
    /// L'acces reste en place ; un humain tranche.
    /// </summary>
    public const string PaymentAttention = "payment_attention";

    /// <summary>
    /// Incoherence financiere (montant, devise, objet provider inattendu).
    /// Aucun automatisme ne doit plus toucher a cet abonnement.
    /// </summary>
    public const string ManualReview = "manual_review";
}

public sealed record BillingV2RenewalOutcomeDecision(
    string PaymentState,
    string ReasonCode,
    bool KeepsProvisioning,
    bool AllowsAutomaticRetry);

/// <summary>
/// Politique de grace temporaire V2.0.
///
/// Le choix est volontairement conservateur : en V2.0, AUCUN echec de
/// renouvellement ne retire de groupe AD, ne reduit de quota, ne supprime de
/// donnee et ne resilie. Le systeme se contente de rendre l'impaye visible.
///
/// Raison : une coupure d'acces est irreversible du point de vue du client
/// (session perdue, partage casse, sauvegarde interrompue) alors qu'un impaye
/// se rattrape. Tant que la detection n'a pas fait ses preuves en exploitation,
/// le cout d'un faux positif est bien superieur au cout d'un jour de grace.
///
/// Cette politique est un choix produit assume, pas une limite technique :
/// elle est destinee a etre resserree une fois le rail eprouve.
/// </summary>
public static class BillingV2RenewalGracePolicy
{
    /// <summary>
    /// Invariant de phase : rien dans ce chemin ne doit deprovisionner.
    /// Le test le verifie sur toutes les issues possibles.
    /// </summary>
    public const bool AutomaticDeprovisioningEnabled = false;

    public static BillingV2RenewalOutcomeDecision Resolve(
        string renewalOutcome)
        => renewalOutcome switch
        {
            BillingV2RenewalOutcomes.Paid => new BillingV2RenewalOutcomeDecision(
                BillingV2SubscriptionPaymentStates.Current,
                "BILLING_V2_RENEWAL_SETTLED",
                KeepsProvisioning: true,
                AllowsAutomaticRetry: false),
            BillingV2RenewalOutcomes.Pending =>
                new BillingV2RenewalOutcomeDecision(
                    BillingV2SubscriptionPaymentStates.Current,
                    "BILLING_V2_RENEWAL_PENDING",
                    KeepsProvisioning: true,
                    AllowsAutomaticRetry: true),
            BillingV2RenewalOutcomes.Failed =>
                new BillingV2RenewalOutcomeDecision(
                    BillingV2SubscriptionPaymentStates.PaymentAttention,
                    "BILLING_V2_RENEWAL_PAYMENT_FAILED",
                    KeepsProvisioning: true,
                    AllowsAutomaticRetry: true),
            BillingV2RenewalOutcomes.PastDue =>
                new BillingV2RenewalOutcomeDecision(
                    BillingV2SubscriptionPaymentStates.PaymentAttention,
                    "BILLING_V2_RENEWAL_PAST_DUE",
                    KeepsProvisioning: true,
                    AllowsAutomaticRetry: true),
            BillingV2RenewalOutcomes.AmountMismatch =>
                new BillingV2RenewalOutcomeDecision(
                    BillingV2SubscriptionPaymentStates.ManualReview,
                    "BILLING_V2_RENEWAL_AMOUNT_MISMATCH",
                    KeepsProvisioning: true,
                    AllowsAutomaticRetry: false),
            BillingV2RenewalOutcomes.Unpaid
                or BillingV2RenewalOutcomes.Cancelled =>
                new BillingV2RenewalOutcomeDecision(
                    BillingV2SubscriptionPaymentStates.ManualReview,
                    "BILLING_V2_RENEWAL_REQUIRES_HUMAN_DECISION",
                    // Meme ici : la resiliation V2 n'est pas automatisee.
                    KeepsProvisioning: true,
                    AllowsAutomaticRetry: false),
            _ => new BillingV2RenewalOutcomeDecision(
                BillingV2SubscriptionPaymentStates.ManualReview,
                "BILLING_V2_RENEWAL_OUTCOME_UNKNOWN",
                KeepsProvisioning: true,
                AllowsAutomaticRetry: false)
        };
}

public static class BillingV2RenewalOutcomes
{
    public const string Paid = "paid";
    public const string Pending = "pending";
    public const string Failed = "failed";
    public const string PastDue = "past_due";
    public const string Unpaid = "unpaid";
    public const string Cancelled = "cancelled";
    public const string AmountMismatch = "amount_mismatch";
}

// ---------------------------------------------------------------------------
// 3. BILLING EVENT DE RENOUVELLEMENT
// ---------------------------------------------------------------------------

/// <summary>
/// Une ligne contractuelle telle qu'elle a ete FIGEE a la souscription.
/// <paramref name="UnitAmountCents"/> vient de
/// <c>billing_v2_subscription_items.amount_cents_snapshot</c> et
/// <paramref name="ServicePriceId"/> de la version de prix verrouillee : le
/// catalogue courant n'intervient jamais.
/// </summary>
public sealed record BillingV2RenewalContractItem(
    string ServiceId,
    string? TierId,
    string ServicePriceId,
    string ServiceCode,
    string? TierCode,
    string BillingCadence,
    int Quantity,
    long UnitAmountCents,
    bool DiscountEligible,
    string? SubscriptionItemId = null,
    string? SubscriptionItemPriceComponentId = null);

public sealed record BillingV2RenewalChargeRequest(
    string SubscriptionId,
    int CycleSequence,
    string PaymentMode,
    int CommitmentMonths,
    int DiscountBasisPoints,
    string Currency,
    long? MinimumCommitmentAmountCents,
    IReadOnlyList<BillingV2RenewalContractItem> Items,
    BillingV2ContractPeriod Period);

public sealed record BillingV2RenewalChargeResult(
    BillingV2BillingEventDraft Draft,
    int CycleSequence,
    BillingV2ContractPeriod Period,
    IReadOnlyList<BillingV2BillingEventLineSource> LineSources);

/// <summary>
/// Construit le BillingEvent d'un cycle de renouvellement.
///
/// Ce qui est snapshotte : les items contractuels applicables, la version de
/// prix verrouillee, l'engagement, la remise, le plancher eventuel, la periode,
/// le montant attendu et la devise. Rien n'est relu dans le catalogue, donc une
/// hausse tarifaire posterieure ne peut pas repricer un contrat en cours.
/// </summary>
public static class BillingV2RenewalChargeFactory
{
    public const string PricingEngineVersion =
        BillingV2BillingEventFactory.PricingEngineVersion;

    /// <summary>
    /// Ligne technique materialisant le plancher d'engagement. Elle existe pour
    /// que le total reste EXACTEMENT la somme des lignes : un plancher applique
    /// en silence sur le total casserait l'invariant arithmetique DB.
    /// </summary>
    public const string CommitmentFloorServiceCode = "COMMITMENT_FLOOR";

    public static BillingV2RenewalChargeResult Build(
        BillingV2RenewalChargeRequest request)
    {
        if (request.CycleSequence <= BillingV2RenewalPolicy.InitialCycleSequence)
        {
            throw new InvalidOperationException(
                "BILLING_V2_RENEWAL_CYCLE_IS_INITIAL_CHARGE");
        }

        // Comptant : le terme est deja encaisse, il n'y a pas de renouvellement
        // mensuel a produire pendant sa duree. Le renouvellement d'un terme
        // prepaye est un sujet distinct, hors perimetre Phase 3.
        if (string.Equals(
                request.PaymentMode,
                BillingV2PaymentModes.Upfront,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "BILLING_V2_RENEWAL_UPFRONT_NOT_SUPPORTED");
        }

        if (string.IsNullOrWhiteSpace(request.Currency)
            || request.Currency.Trim().Length != 3)
        {
            throw new InvalidOperationException(
                "BILLING_V2_RENEWAL_CURRENCY_INVALID");
        }

        // Un renouvellement ne refacture QUE le recurrent : les prestations
        // ponctuelles ont ete encaissees a la charge initiale.
        var recurring = request.Items
            .Where(item => !string.Equals(
                item.BillingCadence,
                BillingV2BillingCadences.OneTime,
                StringComparison.Ordinal))
            .ToArray();
        if (recurring.Length == 0)
        {
            throw new InvalidOperationException(
                "BILLING_V2_RENEWAL_WITHOUT_RECURRING_ITEM");
        }

        var grossAmounts = recurring
            .Select(item => checked(item.UnitAmountCents * item.Quantity))
            .ToArray();
        var eligibleGross = recurring
            .Select((item, index) => item.DiscountEligible ? grossAmounts[index] : 0L)
            .Aggregate(0L, (total, value) => checked(total + value));
        var discountTotal = ResolveDiscount(
            eligibleGross,
            request.DiscountBasisPoints);
        var allocation = BillingV2DiscountAllocator.Allocate(
            discountTotal,
            recurring
                .Select((item, index) =>
                    item.DiscountEligible ? grossAmounts[index] : 0L)
                .ToArray());

        var lines = new List<BillingV2BillingEventLineDraft>();
        var sources = new List<BillingV2BillingEventLineSource>();
        long gross = 0;
        long discount = 0;
        long net = 0;
        for (var index = 0; index < recurring.Length; index++)
        {
            var item = recurring[index];
            var lineGross = grossAmounts[index];
            var allocated = allocation[index];
            var lineNet = checked(lineGross - allocated);
            var label = item.TierCode is null
                ? item.ServiceCode
                : $"{item.ServiceCode} {item.TierCode}";
            lines.Add(new BillingV2BillingEventLineDraft(
                index,
                item.ServiceCode,
                item.TierCode,
                $"{label} (renouvellement cycle {request.CycleSequence})",
                item.Quantity,
                item.UnitAmountCents,
                lineGross,
                allocated,
                lineNet,
                // Franchise en base de TVA : aucune taxe sur le rail V2.
                TaxAmountCents: 0,
                lineNet,
                request.Currency));
            sources.Add(new BillingV2BillingEventLineSource(
                item.ServiceId,
                item.TierId,
                item.ServicePriceId,
                BillingV2LineCadences.Monthly,
                item.SubscriptionItemId,
                item.SubscriptionItemPriceComponentId));

            gross = checked(gross + lineGross);
            discount = checked(discount + allocated);
            net = checked(net + lineNet);
        }

        // Plancher d'engagement : c'est une regle de RENOUVELLEMENT, donc c'est
        // bien ici qu'elle s'applique - et nulle part sur la charge initiale.
        var floor = request.MinimumCommitmentAmountCents;
        if (floor.HasValue && floor.Value > net)
        {
            var complement = checked(floor.Value - net);
            lines.Add(new BillingV2BillingEventLineDraft(
                lines.Count,
                CommitmentFloorServiceCode,
                null,
                $"Complement de plancher d'engagement ({request.CommitmentMonths} mois)",
                1,
                complement,
                complement,
                0,
                complement,
                TaxAmountCents: 0,
                complement,
                request.Currency));
            sources.Add(new BillingV2BillingEventLineSource(
                recurring[0].ServiceId,
                recurring[0].TierId,
                recurring[0].ServicePriceId,
                BillingV2LineCadences.Monthly,
                recurring[0].SubscriptionItemId,
                recurring[0].SubscriptionItemPriceComponentId));
            gross = checked(gross + complement);
            net = checked(net + complement);
        }

        var draft = new BillingV2BillingEventDraft(
            BillingV2BillingEventTypes.RenewalCharge,
            BillingV2BillingEventDirections.Debit,
            request.Currency,
            gross,
            discount,
            net,
            TaxAmountCents: 0,
            net,
            PricingEngineVersion,
            BillingV2RenewalPolicy.Canonical(
                request.SubscriptionId,
                request.CycleSequence),
            lines);

        var validation = BillingV2BillingEventPolicy.ValidateForFinalization(
            draft);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"{validation.ReasonCode}: {validation.Diagnostic}");
        }

        return new BillingV2RenewalChargeResult(
            draft,
            request.CycleSequence,
            request.Period,
            sources);
    }

    private static long ResolveDiscount(long eligibleGross, int basisPoints)
    {
        if (eligibleGross <= 0 || basisPoints <= 0)
        {
            return 0;
        }

        // Meme arithmetique entiere que le moteur de prix : arrondi au demi
        // superieur, sur des entiers, sans jamais passer par un flottant.
        var discounted = checked(
            (eligibleGross * (10000L - basisPoints) + 5000L) / 10000L);
        return checked(eligibleGross - discounted);
    }
}

// ---------------------------------------------------------------------------
// 4. SIGNAUX STRIPE DE RENOUVELLEMENT
// ---------------------------------------------------------------------------

public sealed record BillingV2RenewalSignal(
    bool Recognized,
    string ReasonCode,
    bool RequiresInvoiceRefetch,
    bool RequiresSubscriptionRefetch,
    bool CanProveSettlement);

/// <summary>
/// Classification des evenements Stripe de renouvellement.
///
/// Aucun d'eux ne porte de transition : ils disent seulement QUEL objet
/// relire. La preuve financiere vient ensuite de la relecture, jamais du
/// payload. <c>customer.subscription.created</c> et
/// <c>customer.subscription.updated</c> restent incapables de prouver un
/// paiement - au mieux ils declenchent un controle de sante qui ne peut que
/// DEGRADER l'etat local.
/// </summary>
public static class BillingV2RenewalSignalClassifier
{
    public static BillingV2RenewalSignal Classify(string eventType)
    {
        var normalized = (eventType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "invoice.paid" or "invoice.payment_succeeded" =>
                new BillingV2RenewalSignal(
                    true,
                    "BILLING_V2_RENEWAL_INVOICE_SIGNAL",
                    RequiresInvoiceRefetch: true,
                    RequiresSubscriptionRefetch: true,
                    CanProveSettlement: true),
            "invoice.payment_failed" or "invoice.marked_uncollectible" =>
                new BillingV2RenewalSignal(
                    true,
                    "BILLING_V2_RENEWAL_INVOICE_FAILURE_SIGNAL",
                    RequiresInvoiceRefetch: true,
                    RequiresSubscriptionRefetch: true,
                    CanProveSettlement: false),
            // Controle de sante uniquement : peut faire passer en
            // payment_attention, jamais activer ni encaisser.
            "customer.subscription.updated"
                or "customer.subscription.created" =>
                new BillingV2RenewalSignal(
                    true,
                    "BILLING_V2_RENEWAL_SUBSCRIPTION_HEALTH_SIGNAL",
                    RequiresInvoiceRefetch: false,
                    RequiresSubscriptionRefetch: true,
                    CanProveSettlement: false),
            _ => new BillingV2RenewalSignal(
                false,
                "BILLING_V2_RENEWAL_SIGNAL_UNSUPPORTED",
                RequiresInvoiceRefetch: false,
                RequiresSubscriptionRefetch: false,
                CanProveSettlement: false)
        };
    }
}

// ---------------------------------------------------------------------------
// 2 + 5. VERIFICATION DE L'ETAT REEL COTE STRIPE
// ---------------------------------------------------------------------------

public sealed record BillingV2StripeSubscriptionSnapshot(
    string SubscriptionId,
    string Status,
    string? CustomerId,
    string? LatestInvoiceId,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<BillingV2StripeSubscriptionItemSnapshot>? Items = null);

public sealed record BillingV2StripeSubscriptionItemSnapshot(
    string ItemId,
    string? ProductId,
    bool IsRecurring,
    long? UnitAmountCents = null,
    string? Currency = null,
    int? Quantity = null);

public sealed record BillingV2StripeInvoiceSnapshot(
    string InvoiceId,
    string? SubscriptionId,
    string? CustomerId,
    string Status,
    string? Currency,
    long? AmountPaidCents,
    long? AmountDueCents,
    string? PaymentIntentId,
    string? BillingReason,
    IReadOnlyDictionary<string, string> Metadata,
    /// <summary>
    /// Debut de la periode facturee par Stripe. Sert UNIQUEMENT a savoir de
    /// quel cycle on parle ; le montant, lui, vient toujours du contrat local.
    /// </summary>
    DateTime? PeriodStartUtc = null);

public sealed record BillingV2RenewalCycleResolution(
    bool Resolved,
    int CycleSequence,
    string ReasonCode);

/// <summary>
/// Determine DE QUEL cycle parle un objet Stripe.
///
/// Le rang est calcule a partir de l'ancre contractuelle locale ; Stripe ne
/// fournit que la periode concernee. Ni l'heure courante ni l'horodatage du
/// webhook n'entrent dans ce calcul : sans periode exploitable, on echoue en
/// ferme plutot que de facturer "le cycle du moment".
/// </summary>
public static class BillingV2RenewalCycleResolver
{
    public static BillingV2RenewalCycleResolution Resolve(
        DateTime anchorUtc,
        int monthsPerCycle,
        DateTime? providerPeriodStartUtc)
    {
        if (providerPeriodStartUtc is null)
        {
            return new BillingV2RenewalCycleResolution(
                false,
                0,
                "BILLING_V2_RENEWAL_CYCLE_PERIOD_UNKNOWN");
        }

        var cycle = BillingV2RenewalPolicy.CycleSequenceAt(
            anchorUtc,
            monthsPerCycle,
            providerPeriodStartUtc.Value);
        return cycle <= BillingV2RenewalPolicy.InitialCycleSequence
            ? new BillingV2RenewalCycleResolution(
                false,
                cycle,
                "BILLING_V2_RENEWAL_CYCLE_IS_INITIAL_CHARGE")
            : new BillingV2RenewalCycleResolution(
                true,
                cycle,
                "BILLING_V2_RENEWAL_CYCLE_RESOLVED");
    }
}

public sealed record BillingV2StripeLifecycleExpectation(
    string BillingEventId,
    string SubscriptionId,
    string PaymentAttemptId,
    string ExpectedCurrency,
    long ExpectedAmountCents,
    string? ExpectedProviderSubscriptionId,
    string? ExpectedProviderCustomerId);

public sealed record BillingV2StripeLifecycleVerification(
    bool Settled,
    string Outcome,
    string ReasonCode,
    long? SettledAmountCents,
    string? SettledCurrency,
    string? Diagnostic = null);

/// <summary>
/// 2. Pour <c>mode=subscription</c>, <c>payment_status=paid</c> sur la session
/// ne suffit plus.
///
/// Une session peut etre payee puis l'abonnement basculer <c>past_due</c>
/// quelques heures plus tard ; et un renouvellement, lui, n'a pas de session du
/// tout. La preuve financiere est donc fondee sur l'INVOICE reellement payee,
/// rattachee au bon client, au bon abonnement provider, pour le bon montant et
/// la bonne devise.
/// </summary>
public static class BillingV2StripeLifecycleVerifier
{
    /// <summary>Etats Stripe ou l'abonnement est sainement en cours.</summary>
    public static readonly IReadOnlySet<string> HealthySubscriptionStatuses =
        new HashSet<string>(StringComparer.Ordinal) { "active", "trialing" };

    /// <summary>
    /// Verifie l'invoice d'un cycle (initial ou renouvellement).
    /// </summary>
    public static BillingV2StripeLifecycleVerification VerifyInvoice(
        BillingV2StripeInvoiceSnapshot? invoice,
        BillingV2StripeSubscriptionSnapshot? subscription,
        BillingV2StripeLifecycleExpectation expectation)
    {
        if (invoice is null)
        {
            return Pending(
                BillingV2RenewalOutcomes.Pending,
                "BILLING_V2_RENEWAL_INVOICE_NOT_FOUND");
        }

        // L'objet relu doit etre celui de NOTRE abonnement provider.
        if (expectation.ExpectedProviderSubscriptionId is not null
            && invoice.SubscriptionId is not null
            && !string.Equals(
                invoice.SubscriptionId,
                expectation.ExpectedProviderSubscriptionId,
                StringComparison.Ordinal))
        {
            return Mismatch(
                "BILLING_V2_RENEWAL_INVOICE_SUBSCRIPTION_MISMATCH",
                invoice.SubscriptionId);
        }

        if (expectation.ExpectedProviderCustomerId is not null
            && invoice.CustomerId is not null
            && !string.Equals(
                invoice.CustomerId,
                expectation.ExpectedProviderCustomerId,
                StringComparison.Ordinal))
        {
            return Mismatch(
                "BILLING_V2_RENEWAL_INVOICE_CUSTOMER_MISMATCH",
                invoice.CustomerId);
        }

        var subscriptionOutcome = EvaluateSubscription(subscription);
        if (subscriptionOutcome is not null)
        {
            return subscriptionOutcome;
        }

        if (string.Equals(invoice.Status, "paid", StringComparison.Ordinal))
        {
            return VerifyPaidAmount(invoice, expectation);
        }

        return invoice.Status switch
        {
            "open" or "draft" => Pending(
                BillingV2RenewalOutcomes.Pending,
                "BILLING_V2_RENEWAL_INVOICE_NOT_PAID",
                invoice.Status),
            "uncollectible" => Pending(
                BillingV2RenewalOutcomes.Failed,
                "BILLING_V2_RENEWAL_INVOICE_UNCOLLECTIBLE",
                invoice.Status),
            "void" => Pending(
                BillingV2RenewalOutcomes.Cancelled,
                "BILLING_V2_RENEWAL_INVOICE_VOID",
                invoice.Status),
            _ => Pending(
                BillingV2RenewalOutcomes.Pending,
                "BILLING_V2_RENEWAL_INVOICE_STATUS_UNKNOWN",
                invoice.Status)
        };
    }

    /// <summary>
    /// Controle de sante declenche par un signal d'abonnement. Il ne peut
    /// JAMAIS conclure a un encaissement : au mieux il constate que tout va
    /// bien, au pire il degrade l'etat local.
    /// </summary>
    public static BillingV2StripeLifecycleVerification VerifySubscriptionHealth(
        BillingV2StripeSubscriptionSnapshot? subscription)
    {
        if (subscription is null)
        {
            return Pending(
                BillingV2RenewalOutcomes.Pending,
                "BILLING_V2_RENEWAL_SUBSCRIPTION_NOT_FOUND");
        }

        var degraded = EvaluateSubscription(subscription);
        return degraded ?? Pending(
            BillingV2RenewalOutcomes.Pending,
            "BILLING_V2_RENEWAL_SUBSCRIPTION_HEALTHY",
            subscription.Status);
    }

    private static BillingV2StripeLifecycleVerification? EvaluateSubscription(
        BillingV2StripeSubscriptionSnapshot? subscription)
    {
        if (subscription is null)
        {
            return null;
        }

        if (HealthySubscriptionStatuses.Contains(subscription.Status))
        {
            return null;
        }

        return subscription.Status switch
        {
            "past_due" => Pending(
                BillingV2RenewalOutcomes.PastDue,
                "BILLING_V2_RENEWAL_SUBSCRIPTION_PAST_DUE",
                subscription.Status),
            "unpaid" => Pending(
                BillingV2RenewalOutcomes.Unpaid,
                "BILLING_V2_RENEWAL_SUBSCRIPTION_UNPAID",
                subscription.Status),
            "canceled" or "cancelled" => Pending(
                BillingV2RenewalOutcomes.Cancelled,
                "BILLING_V2_RENEWAL_SUBSCRIPTION_CANCELLED",
                subscription.Status),
            // `incomplete`, `incomplete_expired`, `paused` : rien n'est prouve.
            _ => Pending(
                BillingV2RenewalOutcomes.Pending,
                "BILLING_V2_RENEWAL_SUBSCRIPTION_NOT_CONFIRMED",
                subscription.Status)
        };
    }

    private static BillingV2StripeLifecycleVerification VerifyPaidAmount(
        BillingV2StripeInvoiceSnapshot invoice,
        BillingV2StripeLifecycleExpectation expectation)
    {
        if (invoice.AmountPaidCents is null
            || string.IsNullOrWhiteSpace(invoice.Currency))
        {
            return Pending(
                BillingV2RenewalOutcomes.Pending,
                "BILLING_V2_RENEWAL_AMOUNT_NOT_OBSERVED");
        }

        var settlement = BillingV2SettlementPolicy.Evaluate(
            new BillingV2SettlementObservation(
                expectation.ExpectedAmountCents,
                expectation.ExpectedCurrency.Trim().ToUpperInvariant(),
                invoice.AmountPaidCents,
                invoice.Currency.Trim().ToUpperInvariant()));

        if (!string.Equals(
                settlement.SettlementStatus,
                BillingV2SettlementStatuses.Settled,
                StringComparison.Ordinal))
        {
            return new BillingV2StripeLifecycleVerification(
                false,
                BillingV2RenewalOutcomes.AmountMismatch,
                settlement.ReasonCode,
                invoice.AmountPaidCents,
                invoice.Currency.Trim().ToUpperInvariant(),
                settlement.Diagnostic);
        }

        return new BillingV2StripeLifecycleVerification(
            true,
            BillingV2RenewalOutcomes.Paid,
            "BILLING_V2_RENEWAL_SETTLEMENT_CONFIRMED",
            invoice.AmountPaidCents,
            invoice.Currency.Trim().ToUpperInvariant());
    }

    private static BillingV2StripeLifecycleVerification Pending(
        string outcome,
        string reasonCode,
        string? diagnostic = null)
        => new(false, outcome, reasonCode, null, null, diagnostic);

    private static BillingV2StripeLifecycleVerification Mismatch(
        string reasonCode,
        string? diagnostic)
        => new(
            false,
            BillingV2RenewalOutcomes.AmountMismatch,
            reasonCode,
            null,
            null,
            diagnostic);
}

// ---------------------------------------------------------------------------
// 8. RECHERCHE BORNEE D'UNE SESSION CHECKOUT
// ---------------------------------------------------------------------------

public sealed record BillingV2StripeSessionLocator(
    string? ProviderSessionId,
    string? ProviderPaymentIntentId,
    string? ProviderSubscriptionId,
    string ProviderRequestKey);

public sealed record BillingV2StripeLookupPlan(
    bool CanLookup,
    string Method,
    string? Target,
    string ReasonCode);

/// <summary>
/// Remplace le balayage non borne du compte Stripe.
///
/// L'ancienne implementation listait les sessions du compte et cherchait la
/// bonne dans la page renvoyee : correct sur un compte de test vide, faux et
/// couteux des le premier millier de sessions - et silencieusement faux, ce
/// qui est pire.
///
/// La regle est desormais : on ne relit que ce qu'on a persiste. Si aucun
/// identifiant n'a pu l'etre, on echoue en ferme. Le retry normal repartira
/// avec la MEME cle d'idempotence, que Stripe deduplique : c'est la reprise
/// sure, et elle ne demande aucun scan.
/// </summary>
public static class BillingV2StripeSessionLookupPolicy
{
    public const string MethodSession = "checkout_session";
    public const string MethodPaymentIntent = "payment_intent";
    public const string MethodSubscription = "subscription";
    public const string MethodNone = "none";

    public static BillingV2StripeLookupPlan Plan(
        BillingV2StripeSessionLocator locator)
    {
        if (!string.IsNullOrWhiteSpace(locator.ProviderSessionId))
        {
            return new BillingV2StripeLookupPlan(
                true,
                MethodSession,
                locator.ProviderSessionId,
                "BILLING_V2_STRIPE_LOOKUP_BY_SESSION_ID");
        }

        if (!string.IsNullOrWhiteSpace(locator.ProviderPaymentIntentId))
        {
            return new BillingV2StripeLookupPlan(
                true,
                MethodPaymentIntent,
                locator.ProviderPaymentIntentId,
                "BILLING_V2_STRIPE_LOOKUP_BY_PAYMENT_INTENT");
        }

        if (!string.IsNullOrWhiteSpace(locator.ProviderSubscriptionId))
        {
            return new BillingV2StripeLookupPlan(
                true,
                MethodSubscription,
                locator.ProviderSubscriptionId,
                "BILLING_V2_STRIPE_LOOKUP_BY_SUBSCRIPTION");
        }

        return new BillingV2StripeLookupPlan(
            false,
            MethodNone,
            null,
            "BILLING_V2_STRIPE_LOOKUP_NO_PERSISTED_IDENTIFIER");
    }
}
