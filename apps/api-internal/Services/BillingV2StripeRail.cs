using System.Security.Cryptography;
using System.Text;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Rail Stripe Billing V2 - Phase 2, couche PURE.
///
/// Tout ce qui decide d'un montant, d'une cle d'idempotence ou d'une transition
/// financiere vit ici, sans acces base ni reseau, pour etre testable seul.
///
/// Regle structurante : le montant envoye a Stripe provient TOUJOURS d'un
/// BillingEvent finalise. Aucun `price_id` externe ne determine plus le total
/// contractuel.
///
/// Specification : docs/billing-v2/STRIPE-RAIL.md
/// </summary>
public static class BillingV2LineCadences
{
    public const string Monthly = "monthly";
    public const string UpfrontTerm = "upfront_term";
    public const string OneTime = "one_time";
}

public static class BillingV2SubscriptionChangeStatuses
{
    public const string Pending = "pending";
    public const string Applied = "applied";
    public const string Expired = "expired";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class BillingV2StripeModes
{
    public const string Subscription = "subscription";
    public const string Payment = "payment";
}

// ---------------------------------------------------------------------------
// A. ANCRE D'IDEMPOTENCE : L'INTENTION SERVEUR
// ---------------------------------------------------------------------------

public sealed record BillingV2SubscriptionIntentRequest(
    string CustomerId,
    string ClientRequestId,
    string LegacyOfferId,
    string Provider,
    string Environment);

/// <summary>
/// L'ancre d'idempotence metier n'est plus un `useRef` cote navigateur : c'est
/// une intention persistee. Le navigateur ne fournit qu'un `client_request_id`
/// stable ; la cle canonique, elle, est derivee cote serveur et inclut tout ce
/// qui change la nature de l'intention.
///
/// Consequence voulue : un retry (meme client_request_id, meme choix) retrouve
/// la meme intention ; un choix volontairement different (autre offre, autre
/// rail) produit une intention distincte.
/// </summary>
public static class BillingV2SubscriptionIntentKey
{
    public const string Prefix = "billing_v2.subscription_change";

    public static string Canonical(BillingV2SubscriptionIntentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientRequestId))
        {
            throw new InvalidOperationException(
                "BILLING_V2_INTENT_CLIENT_REQUEST_ID_REQUIRED");
        }

        return string.Join(
            "|",
            Prefix,
            request.CustomerId.Trim(),
            request.Provider.Trim().ToLowerInvariant(),
            request.Environment.Trim().ToLowerInvariant(),
            request.LegacyOfferId.Trim(),
            request.ClientRequestId.Trim());
    }

    public static string Hash(string canonical)
        => Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
}

// ---------------------------------------------------------------------------
// B. CONSTRUCTION DU BILLING EVENT
// ---------------------------------------------------------------------------

public sealed record BillingV2BillingEventBuildRequest(
    string PaymentMode,
    int CommitmentMonths,
    int DiscountBasisPoints,
    string Currency,
    IReadOnlyList<BillingV2NewSubscriptionPresetItem> Items,
    BillingV2PricingResult Pricing,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    string IdempotencyKeyCanonical);

public sealed record BillingV2BillingEventBuildResult(
    BillingV2BillingEventDraft Draft,
    long RecurringAmountCents,
    long OneTimeAmountCents,
    IReadOnlyList<BillingV2BillingEventLineSource> LineSources);

/// <summary>
/// Construit un BillingEvent et ses lignes a partir du resultat du Pricing
/// Engine. La remise globale est ventilee de facon deterministe (plus grands
/// restes, tri stable), puis la coherence est re-verifiee contre le moteur :
/// tout ecart echoue en ferme plutot que de produire une facture fausse.
/// </summary>
public static class BillingV2BillingEventFactory
{
    public const string PricingEngineVersion = "billing-v2-pricing-engine-1";

    public static BillingV2BillingEventBuildResult BuildInitialCharge(
        BillingV2BillingEventBuildRequest request)
    {
        var upfront = string.Equals(
            request.PaymentMode,
            BillingV2PaymentModes.Upfront,
            StringComparison.Ordinal);

        // Le plancher d'engagement est une regle de RENOUVELLEMENT. S'il mordait
        // sur la charge initiale, le total ne serait plus egal a la somme des
        // lignes : on refuse plutot que de bricoler un ecart.
        if (!upfront
            && request.Pricing.PayableRecurringAmountCents
                != request.Pricing.DiscountedRecurringAmountCents)
        {
            throw new InvalidOperationException(
                "BILLING_V2_EVENT_INITIAL_CHARGE_FLOOR_UNEXPECTED");
        }

        var termMultiplier = upfront ? Math.Max(1, request.CommitmentMonths) : 1;
        var recurringCadence = upfront
            ? BillingV2LineCadences.UpfrontTerm
            : BillingV2LineCadences.Monthly;

        var seeds = new List<LineSeed>();
        var order = 0;
        foreach (var item in request.Items)
        {
            var oneTime = string.Equals(
                item.BillingCadence,
                BillingV2BillingCadences.OneTime,
                StringComparison.Ordinal);
            var multiplier = oneTime ? 1 : termMultiplier;
            var unit = checked(item.AmountCents * multiplier);
            seeds.Add(new LineSeed(
                order++,
                item,
                oneTime ? BillingV2LineCadences.OneTime : recurringCadence,
                unit,
                checked(unit * item.Quantity),
                // Les prestations ponctuelles ne recoivent pas la remise
                // d'engagement (BILLING-INVARIANTS #19).
                !oneTime && item.DiscountEligible));
        }

        var discountTotal = ResolveDiscountTotal(request, seeds, upfront);
        var weights = seeds
            .Select(seed => seed.DiscountEligible ? seed.GrossAmountCents : 0L)
            .ToArray();
        var allocation = BillingV2DiscountAllocator.Allocate(
            discountTotal,
            weights);

        var lines = new List<BillingV2BillingEventLineDraft>();
        long gross = 0;
        long discount = 0;
        long net = 0;
        long recurring = 0;
        long oneTimeTotal = 0;
        for (var index = 0; index < seeds.Count; index++)
        {
            var seed = seeds[index];
            var allocated = allocation[index];
            var lineNet = checked(seed.GrossAmountCents - allocated);
            lines.Add(new BillingV2BillingEventLineDraft(
                seed.DisplayOrder,
                seed.Item.ServiceCode,
                seed.Item.TierCode,
                BuildDescription(seed, request.CommitmentMonths, upfront),
                seed.Item.Quantity,
                seed.UnitAmountCents,
                seed.GrossAmountCents,
                allocated,
                lineNet,
                // Franchise en base de TVA : aucune taxe sur le rail V2.
                TaxAmountCents: 0,
                lineNet,
                request.Currency));

            gross = checked(gross + seed.GrossAmountCents);
            discount = checked(discount + allocated);
            net = checked(net + lineNet);
            if (string.Equals(
                    seed.Cadence,
                    BillingV2LineCadences.OneTime,
                    StringComparison.Ordinal))
            {
                oneTimeTotal = checked(oneTimeTotal + lineNet);
            }
            else
            {
                recurring = checked(recurring + lineNet);
            }
        }

        var draft = new BillingV2BillingEventDraft(
            BillingV2BillingEventTypes.InitialCharge,
            BillingV2BillingEventDirections.Debit,
            request.Currency,
            gross,
            discount,
            net,
            TaxAmountCents: 0,
            net,
            PricingEngineVersion,
            request.IdempotencyKeyCanonical,
            lines);

        // Garde-fou : la somme des lignes doit retomber exactement sur le total
        // annonce par le Pricing Engine. Sans ce controle, une divergence de
        // ventilation passerait inapercue jusqu'a la facture.
        if (net != request.Pricing.TotalDueNowCents)
        {
            throw new InvalidOperationException(
                "BILLING_V2_EVENT_TOTAL_DOES_NOT_MATCH_PRICING_ENGINE:"
                + $" lines={net} engine={request.Pricing.TotalDueNowCents}");
        }

        return new BillingV2BillingEventBuildResult(
            draft,
            recurring,
            oneTimeTotal,
            seeds
                .Select(seed => new BillingV2BillingEventLineSource(
                    seed.Item.ServiceId,
                    seed.Item.TierId,
                    seed.Item.ServicePriceId,
                    seed.Cadence))
                .ToArray());
    }

    private static long ResolveDiscountTotal(
        BillingV2BillingEventBuildRequest request,
        IReadOnlyList<LineSeed> seeds,
        bool upfront)
    {
        var eligibleGross = seeds
            .Where(seed => seed.DiscountEligible)
            .Aggregate(0L, (total, seed) => checked(total + seed.GrossAmountCents));
        if (eligibleGross == 0)
        {
            return 0;
        }

        if (!upfront)
        {
            return request.Pricing.RecurringDiscountCents;
        }

        // Meme arithmetique entiere que le moteur, appliquee au montant du terme.
        var discounted = checked(
            (eligibleGross * (10000L - request.DiscountBasisPoints) + 5000L)
            / 10000L);
        return checked(eligibleGross - discounted);
    }

    private static string BuildDescription(
        LineSeed seed,
        int commitmentMonths,
        bool upfront)
    {
        var label = seed.Item.TierCode is null
            ? seed.Item.ServiceCode
            : $"{seed.Item.ServiceCode} {seed.Item.TierCode}";
        return seed.Cadence switch
        {
            BillingV2LineCadences.UpfrontTerm =>
                $"{label} ({commitmentMonths} mois prepayes)",
            BillingV2LineCadences.OneTime => $"{label} (prestation ponctuelle)",
            _ => upfront ? label : $"{label} (mensuel)"
        };
    }

    private sealed record LineSeed(
        int DisplayOrder,
        BillingV2NewSubscriptionPresetItem Item,
        string Cadence,
        long UnitAmountCents,
        long GrossAmountCents,
        bool DiscountEligible);
}

/// <summary>
/// Ventilation deterministe par la methode des plus grands restes.
/// Deux executions sur les memes entrees produisent exactement la meme
/// repartition, sinon les documents ne seraient pas reproductibles.
/// </summary>
public static class BillingV2DiscountAllocator
{
    public static IReadOnlyList<long> Allocate(
        long totalToAllocate,
        IReadOnlyList<long> weights)
    {
        var allocation = new long[weights.Count];
        if (totalToAllocate <= 0)
        {
            return allocation;
        }

        var weightSum = weights.Aggregate(0L, (total, w) => checked(total + w));
        if (weightSum <= 0)
        {
            throw new InvalidOperationException(
                "BILLING_V2_DISCOUNT_ALLOCATION_WITHOUT_ELIGIBLE_LINE");
        }

        if (totalToAllocate > weightSum)
        {
            throw new InvalidOperationException(
                "BILLING_V2_DISCOUNT_ALLOCATION_EXCEEDS_ELIGIBLE_GROSS");
        }

        var remainders = new (int Index, long Remainder)[weights.Count];
        var distributed = 0L;
        for (var index = 0; index < weights.Count; index++)
        {
            var numerator = checked(totalToAllocate * weights[index]);
            var share = numerator / weightSum;
            allocation[index] = share;
            distributed = checked(distributed + share);
            remainders[index] = (index, numerator % weightSum);
        }

        var leftover = totalToAllocate - distributed;
        if (leftover == 0)
        {
            return allocation;
        }

        // Tri stable : reste decroissant, puis index croissant. L'index vient de
        // display_order, lui-meme unique par evenement (contrainte DB).
        var ranked = remainders
            .Where(entry => weights[entry.Index] > 0)
            .OrderByDescending(entry => entry.Remainder)
            .ThenBy(entry => entry.Index)
            .ToArray();
        for (var i = 0; i < leftover && i < ranked.Length; i++)
        {
            allocation[ranked[i].Index] = checked(
                allocation[ranked[i].Index] + 1);
        }

        return allocation;
    }
}

// ---------------------------------------------------------------------------
// D. STRIPE : LE MONTANT VIENT DU BILLING EVENT
// ---------------------------------------------------------------------------

public sealed record BillingV2StripeLineRequest(
    string Description,
    long UnitAmountCents,
    bool Recurring);

public sealed record BillingV2StripeCheckoutRequest(
    string Mode,
    string Currency,
    long ExpectedAmountCents,
    IReadOnlyList<BillingV2StripeLineRequest> Lines,
    string CustomerEmail,
    string SuccessUrl,
    string CancelUrl,
    IReadOnlyDictionary<string, string> Metadata,
    string IdempotencyKey,
    /// <summary>
    /// Identifiant client Stripe DEJA connu localement. Quand il est renseigne,
    /// il remplace <c>customer_email</c> : Stripe rattache alors la session au
    /// client existant au lieu d'en creer un implicitement.
    ///
    /// Introduit pour le scenario Test Clock : une horloge de test ne peut
    /// s'attacher qu'a un client cree a l'avance, ce que `customer_email`
    /// interdit. Aucun client n'est cree par ce chemin - on ne fait que
    /// reutiliser un identifiant persiste.
    /// </summary>
    string? ProviderCustomerId = null);

public sealed record BillingV2FinalizedBillingEvent(
    string Id,
    string SubscriptionId,
    string CustomerId,
    string FinancialStatus,
    string SettlementStatus,
    string Currency,
    string PaymentModeSnapshot,
    int CommitmentMonthsSnapshot,
    long TotalAmountCents,
    long RecurringAmountCents,
    long OneTimeAmountCents,
    long TaxAmountCents,
    int LineCount);

/// <summary>
/// B. Aucun checkout provider V2 ne part sans un BillingEvent finalise et
/// coherent. Cette garde est la frontiere : au-dela, plus aucune relecture du
/// catalogue n'est autorisee.
/// </summary>
public static class BillingV2StripeDispatchGuard
{
    public const string AuthorizedReasonCode =
        "BILLING_V2_STRIPE_DISPATCH_AUTHORIZED";

    public static BillingV2FinancialDecision Evaluate(
        BillingV2FinalizedBillingEvent? billingEvent)
    {
        if (billingEvent is null)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_STRIPE_DISPATCH_WITHOUT_BILLING_EVENT");
        }

        if (!string.Equals(
                billingEvent.FinancialStatus,
                BillingV2FinancialStatuses.Finalized,
                StringComparison.Ordinal))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_STRIPE_DISPATCH_EVENT_NOT_FINALIZED",
                billingEvent.FinancialStatus);
        }

        if (billingEvent.LineCount == 0)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_STRIPE_DISPATCH_EVENT_HAS_NO_LINES");
        }

        if (string.IsNullOrWhiteSpace(billingEvent.Currency)
            || billingEvent.Currency.Trim().Length != 3)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_STRIPE_DISPATCH_CURRENCY_INVALID");
        }

        if (billingEvent.TotalAmountCents <= 0)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_STRIPE_DISPATCH_TOTAL_INVALID",
                billingEvent.TotalAmountCents.ToString());
        }

        if (billingEvent.RecurringAmountCents + billingEvent.OneTimeAmountCents
            != billingEvent.TotalAmountCents)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_STRIPE_DISPATCH_AMOUNT_DECOMPOSITION_MISMATCH");
        }

        // Franchise en base de TVA. Une TVA non nulle exigerait de declarer la
        // fiscalite a Stripe : hors perimetre Phase 2, donc fail closed.
        if (billingEvent.TaxAmountCents != 0)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_STRIPE_DISPATCH_TAX_NOT_SUPPORTED");
        }

        return string.Equals(
                billingEvent.SettlementStatus,
                BillingV2SettlementStatuses.Settled,
                StringComparison.Ordinal)
            ? BillingV2FinancialDecision.Refused(
                "BILLING_V2_STRIPE_DISPATCH_ALREADY_SETTLED")
            : BillingV2FinancialDecision.Ok(AuthorizedReasonCode);
    }
}

/// <summary>
/// Construit la requete Stripe a partir du BillingEvent finalise.
///
/// Representation choisie : `price_data` inline, jamais un `price_id` externe.
/// C'est ce qui garantit que le montant preleve est exactement le montant local.
///
/// - mensuel : mode=subscription, une ligne recurrente mensuelle au MRR
///   contractuel (remise deja integree), plus une ligne one-shot separee pour
///   les frais de mise en service ;
/// - comptant 6/12 mois : mode=payment, un paiement unique du montant upfront
///   exact, et AUCUNE subscription Stripe mensuelle.
/// </summary>
public static class BillingV2StripeCheckoutRequestFactory
{
    public static BillingV2StripeCheckoutRequest Build(
        BillingV2FinalizedBillingEvent billingEvent,
        string paymentAttemptId,
        string providerRequestKey,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        string? providerCustomerId = null)
    {
        var guard = BillingV2StripeDispatchGuard.Evaluate(billingEvent);
        if (!guard.IsValid)
        {
            throw new InvalidOperationException(guard.ReasonCode);
        }

        var upfront = string.Equals(
            billingEvent.PaymentModeSnapshot,
            BillingV2PaymentModes.Upfront,
            StringComparison.Ordinal);
        var lines = new List<BillingV2StripeLineRequest>();

        if (billingEvent.RecurringAmountCents > 0)
        {
            lines.Add(new BillingV2StripeLineRequest(
                upfront
                    ? $"Abonnement prepaye {billingEvent.CommitmentMonthsSnapshot} mois"
                    : "Abonnement mensuel",
                billingEvent.RecurringAmountCents,
                // En comptant, la part recurrente est encaissee en une fois :
                // aucune recurrence Stripe ne doit etre creee.
                Recurring: !upfront));
        }

        if (billingEvent.OneTimeAmountCents > 0)
        {
            lines.Add(new BillingV2StripeLineRequest(
                "Frais de mise en service",
                billingEvent.OneTimeAmountCents,
                Recurring: false));
        }

        if (lines.Count == 0)
        {
            throw new InvalidOperationException(
                "BILLING_V2_STRIPE_DISPATCH_TOTAL_INVALID");
        }

        var mode = upfront
            ? BillingV2StripeModes.Payment
            : BillingV2StripeModes.Subscription;

        return new BillingV2StripeCheckoutRequest(
            mode,
            billingEvent.Currency.Trim().ToLowerInvariant(),
            billingEvent.TotalAmountCents,
            lines,
            customerEmail,
            successUrl,
            cancelUrl,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["billing_v2_billing_event_id"] = billingEvent.Id,
                ["billing_v2_subscription_id"] = billingEvent.SubscriptionId,
                ["billing_v2_payment_attempt_id"] = paymentAttemptId,
                ["customer_id"] = billingEvent.CustomerId
            },
            providerRequestKey,
            string.IsNullOrWhiteSpace(providerCustomerId)
                ? null
                : providerCustomerId.Trim());
    }

    /// <summary>
    /// Encode la requete au format form-urlencoded attendu par l'API Stripe.
    /// Isole ici pour etre verifiable sans appel reseau.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ToFormParameters(
        BillingV2StripeCheckoutRequest request)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = request.Mode,
            ["success_url"] = request.SuccessUrl,
            ["cancel_url"] = request.CancelUrl
        };

        // Stripe refuse `customer` et `customer_email` ensemble. Un client
        // deja connu prime donc, et rien d'autre ne change ; sans client
        // connu, le comportement est celui d'avant, a l'identique.
        if (request.ProviderCustomerId is { Length: > 0 } providerCustomerId)
        {
            parameters["customer"] = providerCustomerId;
        }
        else
        {
            parameters["customer_email"] = request.CustomerEmail;
        }

        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];
            var prefix = $"line_items[{index}]";
            parameters[$"{prefix}[quantity]"] = "1";
            parameters[$"{prefix}[price_data][currency]"] = request.Currency;
            parameters[$"{prefix}[price_data][unit_amount]"] =
                line.UnitAmountCents.ToString();
            parameters[$"{prefix}[price_data][product_data][name]"] =
                line.Description;
            if (line.Recurring)
            {
                parameters[$"{prefix}[price_data][recurring][interval]"] =
                    "month";
            }
        }

        foreach (var (key, value) in request.Metadata)
        {
            parameters[$"metadata[{key}]"] = value;
            if (string.Equals(
                    request.Mode,
                    BillingV2StripeModes.Subscription,
                    StringComparison.Ordinal))
            {
                parameters[$"subscription_data[metadata][{key}]"] = value;
            }
            else
            {
                parameters[$"payment_intent_data[metadata][{key}]"] = value;
            }
        }

        return parameters;
    }
}

// ---------------------------------------------------------------------------
// E. SETTLEMENT VERIFIE
// ---------------------------------------------------------------------------

public sealed record BillingV2StripeSessionSnapshot(
    string SessionId,
    string? PaymentIntentId,
    string? SubscriptionId,
    string Mode,
    string? Currency,
    long? AmountTotalCents,
    string? PaymentStatus,
    string? SessionStatus,
    string? CustomerEmail,
    IReadOnlyDictionary<string, string> Metadata,
    string? ApprovalUrl = null);

public sealed record BillingV2StripeApprovalUrlRecovery(
    string? ApprovalUrl,
    string ReasonCode,
    bool RequiresManualReview)
{
    public bool Recovered => ApprovalUrl is { Length: > 0 };
}

/// <summary>
/// Reprise de l'URL d'approbation d'une session checkout deja creee.
///
/// Une reprise ne doit JAMAIS creer une seconde session pour retrouver une
/// URL : ce serait un second encaissement possible. On rend donc l'URL deja
/// connue - persistee localement en priorite, sinon celle de la session
/// relue - et si aucune n'est disponible on echoue en ferme vers revue
/// manuelle plutot que de laisser l'abonnement sans moyen de paiement.
/// </summary>
public static class BillingV2StripeApprovalUrlRecoveryPolicy
{
    public static BillingV2StripeApprovalUrlRecovery Resolve(
        string? persistedApprovalUrl,
        string? refetchedApprovalUrl)
    {
        if (Normalize(persistedApprovalUrl) is { } persisted)
        {
            return new BillingV2StripeApprovalUrlRecovery(
                persisted,
                "BILLING_V2_STRIPE_APPROVAL_URL_PERSISTED",
                RequiresManualReview: false);
        }

        if (Normalize(refetchedApprovalUrl) is { } refetched)
        {
            return new BillingV2StripeApprovalUrlRecovery(
                refetched,
                "BILLING_V2_STRIPE_APPROVAL_URL_REFETCHED",
                RequiresManualReview: false);
        }

        return new BillingV2StripeApprovalUrlRecovery(
            null,
            "BILLING_V2_STRIPE_APPROVAL_URL_UNRECOVERABLE",
            RequiresManualReview: true);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record BillingV2StripeVerificationExpectation(
    string BillingEventId,
    string SubscriptionId,
    string PaymentAttemptId,
    string ExpectedCurrency,
    long ExpectedAmountCents,
    string ExpectedMode,
    string? ExpectedCustomerEmail);

public sealed record BillingV2StripeVerificationResult(
    bool Settled,
    string AttemptStatus,
    string SettlementStatus,
    string ReasonCode,
    long? SettledAmountCents,
    string? SettledCurrency,
    string? Diagnostic = null);

/// <summary>
/// E / F. Un webhook ou un retour navigateur n'est qu'un signal. Cette
/// politique s'applique sur une RELECTURE de l'objet chez Stripe.
///
/// Elle ne conclut a un encaissement que si, simultanement :
/// l'objet correspond a l'attente, la devise correspond, le montant
/// reellement paye correspond, et l'etat Stripe est reellement final.
///
/// Tout ecart de montant ou de devise donne `amount_mismatch` : jamais
/// d'activation, jamais de `paid` par defaut.
/// </summary>
public static class BillingV2StripeSettlementVerifier
{
    public static BillingV2StripeVerificationResult Verify(
        BillingV2StripeSessionSnapshot? snapshot,
        BillingV2StripeVerificationExpectation expectation)
    {
        if (snapshot is null)
        {
            return Pending("BILLING_V2_STRIPE_SESSION_NOT_FOUND");
        }

        // L'objet relu doit etre celui que nous avons cree.
        if (!MetadataMatches(
                snapshot.Metadata,
                "billing_v2_billing_event_id",
                expectation.BillingEventId))
        {
            return Reconcile(
                "BILLING_V2_STRIPE_BILLING_EVENT_MISMATCH",
                snapshot.Metadata.GetValueOrDefault(
                    "billing_v2_billing_event_id"));
        }

        if (!MetadataMatches(
                snapshot.Metadata,
                "billing_v2_subscription_id",
                expectation.SubscriptionId))
        {
            return Reconcile("BILLING_V2_STRIPE_SUBSCRIPTION_MISMATCH");
        }

        if (!MetadataMatches(
                snapshot.Metadata,
                "billing_v2_payment_attempt_id",
                expectation.PaymentAttemptId))
        {
            return Reconcile("BILLING_V2_STRIPE_PAYMENT_ATTEMPT_MISMATCH");
        }

        if (!string.Equals(
                snapshot.Mode,
                expectation.ExpectedMode,
                StringComparison.Ordinal))
        {
            return Reconcile(
                "BILLING_V2_STRIPE_MODE_MISMATCH",
                snapshot.Mode);
        }

        if (expectation.ExpectedCustomerEmail is not null
            && snapshot.CustomerEmail is not null
            && !string.Equals(
                snapshot.CustomerEmail,
                expectation.ExpectedCustomerEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            return Reconcile(
                "BILLING_V2_STRIPE_CUSTOMER_MISMATCH",
                snapshot.CustomerEmail);
        }

        // Etat reellement final chez Stripe. `complete` sans `paid` ne prouve
        // rien : c'est exactement le piege que la Phase 1 avait identifie.
        if (!string.Equals(
                snapshot.PaymentStatus,
                "paid",
                StringComparison.Ordinal))
        {
            return Pending(
                "BILLING_V2_STRIPE_PAYMENT_NOT_CONFIRMED",
                snapshot.PaymentStatus);
        }

        if (snapshot.AmountTotalCents is null
            || string.IsNullOrWhiteSpace(snapshot.Currency))
        {
            return Pending("BILLING_V2_STRIPE_AMOUNT_NOT_OBSERVED");
        }

        var observation = new BillingV2SettlementObservation(
            expectation.ExpectedAmountCents,
            expectation.ExpectedCurrency.Trim().ToUpperInvariant(),
            snapshot.AmountTotalCents,
            snapshot.Currency.Trim().ToUpperInvariant());
        var settlement = BillingV2SettlementPolicy.Evaluate(observation);

        if (!string.Equals(
                settlement.SettlementStatus,
                BillingV2SettlementStatuses.Settled,
                StringComparison.Ordinal))
        {
            return new BillingV2StripeVerificationResult(
                false,
                BillingV2PaymentAttemptStatuses.AmountMismatch,
                BillingV2SettlementStatuses.AmountMismatch,
                settlement.ReasonCode,
                snapshot.AmountTotalCents,
                snapshot.Currency.Trim().ToUpperInvariant(),
                settlement.Diagnostic);
        }

        return new BillingV2StripeVerificationResult(
            true,
            BillingV2PaymentAttemptStatuses.Succeeded,
            BillingV2SettlementStatuses.Settled,
            "BILLING_V2_STRIPE_SETTLEMENT_CONFIRMED",
            snapshot.AmountTotalCents,
            snapshot.Currency.Trim().ToUpperInvariant());
    }

    private static bool MetadataMatches(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        string expected)
        => metadata.TryGetValue(key, out var value)
           && string.Equals(value, expected, StringComparison.Ordinal);

    private static BillingV2StripeVerificationResult Pending(
        string reasonCode,
        string? diagnostic = null)
        => new(
            false,
            BillingV2PaymentAttemptStatuses.InFlight,
            BillingV2SettlementStatuses.Pending,
            reasonCode,
            SettledAmountCents: null,
            SettledCurrency: null,
            diagnostic);

    private static BillingV2StripeVerificationResult Reconcile(
        string reasonCode,
        string? diagnostic = null)
        => new(
            false,
            BillingV2PaymentAttemptStatuses.AmountMismatch,
            BillingV2SettlementStatuses.AmountMismatch,
            reasonCode,
            SettledAmountCents: null,
            SettledCurrency: null,
            diagnostic);
}
