namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Coeur financier Billing V2 - Phase 1.
///
/// Ce fichier ne contient que des politiques PURES, sans acces base ni appel
/// reseau. Elles portent les invariants que MariaDB ne peut pas exprimer en
/// CHECK (contraintes inter-lignes et inter-tables).
///
/// Le fait qu'un invariant soit applicatif et non DB n'en fait pas une
/// recommandation : tout chemin d'ecriture du coeur financier doit passer par
/// ces politiques.
///
/// Specification : docs/billing-v2/FINANCIAL-CORE.md
/// </summary>
public static class BillingV2BillingEventTypes
{
    public const string InitialCharge = "initial_charge";
    public const string RenewalCharge = "renewal_charge";
    public const string UpgradeCharge = "upgrade_charge";
    public const string PrepaidUpgradeCharge = "prepaid_upgrade_charge";
    public const string DowngradeCredit = "downgrade_credit";
    public const string OneTimeCharge = "one_time_charge";
    public const string Adjustment = "adjustment";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal)
        {
            InitialCharge,
            RenewalCharge,
            UpgradeCharge,
            PrepaidUpgradeCharge,
            DowngradeCredit,
            OneTimeCharge,
            Adjustment
        };
}

public static class BillingV2BillingEventDirections
{
    public const string Debit = "debit";
    public const string Credit = "credit";
}

public static class BillingV2FinancialStatuses
{
    public const string Draft = "draft";
    public const string Finalized = "finalized";
    public const string Void = "void";
}

public static class BillingV2SettlementStatuses
{
    public const string None = "none";
    public const string Pending = "pending";
    public const string Settled = "settled";
    public const string PartiallySettled = "partially_settled";
    public const string Failed = "failed";
    public const string AmountMismatch = "amount_mismatch";
    public const string Refunded = "refunded";

    /// <summary>
    /// Etats representant de l'argent effectivement recu. Un evenement dans un
    /// de ces etats ne peut plus etre annule (APP-5).
    /// </summary>
    public static readonly IReadOnlySet<string> Successful =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Settled,
            PartiallySettled,
            Refunded
        };
}

public static class BillingV2EventDocumentStatuses
{
    public const string None = "none";
    public const string Pending = "pending";
    public const string Issued = "issued";
    public const string Failed = "failed";
}

public static class BillingV2PaymentAttemptStatuses
{
    public const string Created = "created";
    public const string InFlight = "in_flight";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Abandoned = "abandoned";
    public const string AmountMismatch = "amount_mismatch";

    /// <summary>
    /// Etats terminaux : aucun nouvel appel provider ne doit repartir dessus.
    /// </summary>
    public static readonly IReadOnlySet<string> Terminal =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Succeeded,
            Abandoned
        };
}

public sealed record BillingV2FinancialDecision(
    bool IsValid,
    string ReasonCode,
    string? Diagnostic = null)
{
    public static BillingV2FinancialDecision Ok(string reasonCode)
        => new(true, reasonCode);

    public static BillingV2FinancialDecision Refused(
        string reasonCode,
        string? diagnostic = null)
        => new(false, reasonCode, diagnostic);
}

public sealed record BillingV2BillingEventLineDraft(
    int DisplayOrder,
    string ServiceCode,
    string? TierCode,
    string Description,
    int Quantity,
    long UnitAmountCents,
    long GrossAmountCents,
    long DiscountAllocatedAmountCents,
    long NetAmountCents,
    long TaxAmountCents,
    long TotalAmountCents,
    string Currency);

public sealed record BillingV2BillingEventDraft(
    string EventType,
    string Direction,
    string Currency,
    long GrossAmountCents,
    long DiscountAmountCents,
    long NetAmountCents,
    long TaxAmountCents,
    long TotalAmountCents,
    string PricingEngineVersion,
    string IdempotencyKeyCanonical,
    IReadOnlyList<BillingV2BillingEventLineDraft> Lines);

/// <summary>
/// APP-1 a APP-4 : coherence arithmetique entre un evenement et ses lignes.
/// La base garantit deja l'arithmetique intra-ligne et intra-evenement ; ce
/// qu'elle ne peut pas garantir, c'est que la SOMME des lignes egale les
/// totaux de l'evenement.
/// </summary>
public static class BillingV2BillingEventPolicy
{
    public const string ValidReasonCode = "BILLING_V2_EVENT_VALID";

    public static BillingV2FinancialDecision ValidateForFinalization(
        BillingV2BillingEventDraft draft)
    {
        if (!BillingV2BillingEventTypes.All.Contains(draft.EventType))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_TYPE_UNSUPPORTED",
                draft.EventType);
        }

        if (draft.Direction is not (BillingV2BillingEventDirections.Debit
            or BillingV2BillingEventDirections.Credit))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_DIRECTION_UNSUPPORTED",
                draft.Direction);
        }

        if (!IsValidCurrency(draft.Currency))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_CURRENCY_INVALID");
        }

        if (string.IsNullOrWhiteSpace(draft.PricingEngineVersion))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_PRICING_ENGINE_VERSION_MISSING");
        }

        if (string.IsNullOrWhiteSpace(draft.IdempotencyKeyCanonical))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_IDEMPOTENCY_KEY_MISSING");
        }

        // APP-1 : un evenement finalise porte au moins une ligne.
        if (draft.Lines.Count == 0)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_HAS_NO_LINES");
        }

        if (draft.GrossAmountCents < 0
            || draft.DiscountAmountCents < 0
            || draft.NetAmountCents < 0
            || draft.TaxAmountCents < 0
            || draft.TotalAmountCents < 0)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_AMOUNT_NEGATIVE");
        }

        if (draft.DiscountAmountCents > draft.GrossAmountCents)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_DISCOUNT_EXCEEDS_GROSS");
        }

        if (draft.NetAmountCents
            != draft.GrossAmountCents - draft.DiscountAmountCents)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_NET_MISMATCH");
        }

        if (draft.TotalAmountCents
            != draft.NetAmountCents + draft.TaxAmountCents)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_TOTAL_MISMATCH");
        }

        var seenOrders = new HashSet<int>();
        foreach (var line in draft.Lines)
        {
            var lineDecision = ValidateLine(draft, line);
            if (!lineDecision.IsValid)
            {
                return lineDecision;
            }

            if (!seenOrders.Add(line.DisplayOrder))
            {
                return BillingV2FinancialDecision.Refused(
                    "BILLING_V2_EVENT_LINE_ORDER_DUPLICATED",
                    line.DisplayOrder.ToString());
            }
        }

        // APP-2 : la somme des lignes doit egaler les totaux de l'evenement.
        // Sans cela, la ventilation de remise pourrait perdre ou inventer des
        // centimes sans qu'aucune contrainte DB ne le voie.
        return ValidateAggregates(draft);
    }

    private static BillingV2FinancialDecision ValidateLine(
        BillingV2BillingEventDraft draft,
        BillingV2BillingEventLineDraft line)
    {
        // APP-3 : une ligne ne peut pas etre dans une autre devise que
        // l'evenement qui la porte.
        if (!string.Equals(line.Currency, draft.Currency, StringComparison.Ordinal))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINE_CURRENCY_MISMATCH",
                line.ServiceCode);
        }

        if (string.IsNullOrWhiteSpace(line.Description))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINE_DESCRIPTION_MISSING",
                line.ServiceCode);
        }

        if (line.Quantity <= 0)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINE_QUANTITY_INVALID",
                line.ServiceCode);
        }

        // APP-4 : aucune ligne negative dans un evenement debit.
        if (string.Equals(
                draft.Direction,
                BillingV2BillingEventDirections.Debit,
                StringComparison.Ordinal)
            && (line.UnitAmountCents < 0
                || line.GrossAmountCents < 0
                || line.DiscountAllocatedAmountCents < 0
                || line.NetAmountCents < 0
                || line.TaxAmountCents < 0
                || line.TotalAmountCents < 0))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINE_NEGATIVE_IN_DEBIT",
                line.ServiceCode);
        }

        if (line.GrossAmountCents != line.UnitAmountCents * line.Quantity)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINE_GROSS_MISMATCH",
                line.ServiceCode);
        }

        if (line.DiscountAllocatedAmountCents > line.GrossAmountCents)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINE_DISCOUNT_EXCEEDS_GROSS",
                line.ServiceCode);
        }

        if (line.NetAmountCents
            != line.GrossAmountCents - line.DiscountAllocatedAmountCents)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINE_NET_MISMATCH",
                line.ServiceCode);
        }

        return line.TotalAmountCents
            != line.NetAmountCents + line.TaxAmountCents
            ? BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINE_TOTAL_MISMATCH",
                line.ServiceCode)
            : BillingV2FinancialDecision.Ok(ValidReasonCode);
    }

    private static BillingV2FinancialDecision ValidateAggregates(
        BillingV2BillingEventDraft draft)
    {
        long gross = 0;
        long discount = 0;
        long net = 0;
        long tax = 0;
        long total = 0;
        foreach (var line in draft.Lines)
        {
            gross = checked(gross + line.GrossAmountCents);
            discount = checked(discount + line.DiscountAllocatedAmountCents);
            net = checked(net + line.NetAmountCents);
            tax = checked(tax + line.TaxAmountCents);
            total = checked(total + line.TotalAmountCents);
        }

        if (gross != draft.GrossAmountCents)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINES_GROSS_SUM_MISMATCH",
                $"lines={gross} event={draft.GrossAmountCents}");
        }

        if (discount != draft.DiscountAmountCents)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINES_DISCOUNT_SUM_MISMATCH",
                $"lines={discount} event={draft.DiscountAmountCents}");
        }

        if (net != draft.NetAmountCents)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINES_NET_SUM_MISMATCH",
                $"lines={net} event={draft.NetAmountCents}");
        }

        if (tax != draft.TaxAmountCents)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINES_TAX_SUM_MISMATCH",
                $"lines={tax} event={draft.TaxAmountCents}");
        }

        return total != draft.TotalAmountCents
            ? BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_LINES_TOTAL_SUM_MISMATCH",
                $"lines={total} event={draft.TotalAmountCents}")
            : BillingV2FinancialDecision.Ok(ValidReasonCode);
    }

    private static bool IsValidCurrency(string? currency)
        => !string.IsNullOrWhiteSpace(currency) && currency.Trim().Length == 3;
}

public sealed record BillingV2BillingEventStateSnapshot(
    string FinancialStatus,
    string SettlementStatus,
    string DocumentStatus);

/// <summary>
/// APP-5 a APP-9 : transitions financieres autorisees.
///
/// Une correction n'est jamais une mutation : c'est un nouvel evenement
/// `adjustment`. Une cle d'idempotence n'est jamais reutilisee, meme apres un
/// void - c'est ce qui empeche un retry concurrent de ressusciter un evenement
/// annule.
/// </summary>
public static class BillingV2BillingEventStateMachine
{
    public const string TransitionAllowedReasonCode =
        "BILLING_V2_EVENT_TRANSITION_ALLOWED";

    public static BillingV2FinancialDecision CanTransition(
        BillingV2BillingEventStateSnapshot snapshot,
        string targetFinancialStatus)
    {
        if (targetFinancialStatus is not (BillingV2FinancialStatuses.Draft
            or BillingV2FinancialStatuses.Finalized
            or BillingV2FinancialStatuses.Void))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_TRANSITION_TARGET_UNSUPPORTED",
                targetFinancialStatus);
        }

        if (string.Equals(
                snapshot.FinancialStatus,
                targetFinancialStatus,
                StringComparison.Ordinal))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_TRANSITION_ALREADY_IN_STATE",
                targetFinancialStatus);
        }

        // APP-8 : void est terminal.
        if (string.Equals(
                snapshot.FinancialStatus,
                BillingV2FinancialStatuses.Void,
                StringComparison.Ordinal))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_TRANSITION_FROM_VOID_FORBIDDEN");
        }

        // APP-7 : un evenement finalise ne redevient jamais un brouillon.
        if (string.Equals(
                targetFinancialStatus,
                BillingV2FinancialStatuses.Draft,
                StringComparison.Ordinal))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_TRANSITION_TO_DRAFT_FORBIDDEN");
        }

        if (string.Equals(
                targetFinancialStatus,
                BillingV2FinancialStatuses.Finalized,
                StringComparison.Ordinal))
        {
            return string.Equals(
                    snapshot.FinancialStatus,
                    BillingV2FinancialStatuses.Draft,
                    StringComparison.Ordinal)
                ? BillingV2FinancialDecision.Ok(TransitionAllowedReasonCode)
                : BillingV2FinancialDecision.Refused(
                    "BILLING_V2_EVENT_TRANSITION_FINALIZE_FORBIDDEN",
                    snapshot.FinancialStatus);
        }

        // APP-5 : void interdit si de l'argent a ete effectivement recu.
        if (BillingV2SettlementStatuses.Successful.Contains(
                snapshot.SettlementStatus))
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_VOID_FORBIDDEN_SETTLED",
                snapshot.SettlementStatus);
        }

        // APP-6 : void interdit si un document legal a ete emis.
        return string.Equals(
                snapshot.DocumentStatus,
                BillingV2EventDocumentStatuses.Issued,
                StringComparison.Ordinal)
            ? BillingV2FinancialDecision.Refused(
                "BILLING_V2_EVENT_VOID_FORBIDDEN_DOCUMENT_ISSUED")
            : BillingV2FinancialDecision.Ok(TransitionAllowedReasonCode);
    }

    /// <summary>
    /// APP-9. Rend explicite et testable le fait qu'une cle d'idempotence
    /// consommee l'est definitivement. La contrainte UNIQUE en base est le
    /// filet ; cette politique est la regle.
    /// </summary>
    public static BillingV2FinancialDecision CanReuseIdempotencyKey(
        BillingV2BillingEventStateSnapshot existingEvent)
        => BillingV2FinancialDecision.Refused(
            "BILLING_V2_EVENT_IDEMPOTENCY_KEY_ALREADY_CONSUMED",
            existingEvent.FinancialStatus);
}

public sealed record BillingV2SettlementObservation(
    long ExpectedAmountCents,
    string ExpectedCurrency,
    long? SettledAmountCents,
    string? SettledCurrency);

public sealed record BillingV2SettlementDecision(
    string SettlementStatus,
    string ReasonCode,
    string? Diagnostic = null);

/// <summary>
/// APP-10 / APP-11 : montant attendu vs montant reellement constate.
///
/// Le montant attendu vient du Pricing Engine et fait foi. Le montant settled
/// vient du provider et ne fait que constater. Aucun ecart n'est arrondi,
/// ignore, ni traite comme un succes.
/// </summary>
public static class BillingV2SettlementPolicy
{
    public static BillingV2SettlementDecision Evaluate(
        BillingV2SettlementObservation observation)
    {
        if (observation.ExpectedAmountCents < 0)
        {
            return new BillingV2SettlementDecision(
                BillingV2SettlementStatuses.Failed,
                "BILLING_V2_SETTLEMENT_EXPECTED_AMOUNT_INVALID");
        }

        if (observation.SettledAmountCents is null
            || string.IsNullOrWhiteSpace(observation.SettledCurrency))
        {
            // Rien de constate : on reste en attente, on ne conclut jamais a
            // un succes par defaut.
            return new BillingV2SettlementDecision(
                BillingV2SettlementStatuses.Pending,
                "BILLING_V2_SETTLEMENT_NOT_OBSERVED");
        }

        if (!string.Equals(
                observation.SettledCurrency,
                observation.ExpectedCurrency,
                StringComparison.Ordinal))
        {
            return new BillingV2SettlementDecision(
                BillingV2SettlementStatuses.AmountMismatch,
                "BILLING_V2_SETTLEMENT_CURRENCY_MISMATCH",
                $"expected={observation.ExpectedCurrency} settled={observation.SettledCurrency}");
        }

        if (observation.SettledAmountCents.Value
            != observation.ExpectedAmountCents)
        {
            return new BillingV2SettlementDecision(
                BillingV2SettlementStatuses.AmountMismatch,
                "BILLING_V2_SETTLEMENT_AMOUNT_MISMATCH",
                $"expected={observation.ExpectedAmountCents} settled={observation.SettledAmountCents.Value}");
        }

        return new BillingV2SettlementDecision(
            BillingV2SettlementStatuses.Settled,
            "BILLING_V2_SETTLEMENT_CONFIRMED");
    }
}

public sealed record BillingV2PaymentAttemptSnapshot(
    string Id,
    string Provider,
    string Environment,
    string ProviderRequestKey,
    string Status);

public sealed record BillingV2PaymentAttemptRetryDecision(
    bool CanCall,
    string ReasonCode,
    string? ProviderRequestKey,
    string? Diagnostic = null);

/// <summary>
/// APP-12 / APP-13.
///
/// Une PaymentAttempt est persistee AVANT tout appel provider, et un retry
/// reutilise la meme ligne et la meme cle. Generer une nouvelle cle a chaque
/// tentative est exactement ce qui produit un double debit quand un appel part
/// en timeout.
/// </summary>
public static class BillingV2PaymentAttemptPolicy
{
    public static BillingV2PaymentAttemptRetryDecision EvaluateProviderCall(
        BillingV2PaymentAttemptSnapshot? persistedAttempt,
        string provider,
        string environment)
    {
        // APP-12 : pas de ligne persistee, pas d'appel provider.
        if (persistedAttempt is null)
        {
            return new BillingV2PaymentAttemptRetryDecision(
                false,
                "BILLING_V2_PAYMENT_ATTEMPT_NOT_PERSISTED",
                ProviderRequestKey: null);
        }

        if (string.IsNullOrWhiteSpace(persistedAttempt.ProviderRequestKey))
        {
            return new BillingV2PaymentAttemptRetryDecision(
                false,
                "BILLING_V2_PAYMENT_ATTEMPT_REQUEST_KEY_MISSING",
                ProviderRequestKey: null);
        }

        if (!string.Equals(
                persistedAttempt.Provider,
                provider,
                StringComparison.Ordinal)
            || !string.Equals(
                persistedAttempt.Environment,
                environment,
                StringComparison.Ordinal))
        {
            return new BillingV2PaymentAttemptRetryDecision(
                false,
                "BILLING_V2_PAYMENT_ATTEMPT_CONTEXT_MISMATCH",
                ProviderRequestKey: null,
                $"persisted={persistedAttempt.Provider}/{persistedAttempt.Environment} requested={provider}/{environment}");
        }

        if (BillingV2PaymentAttemptStatuses.Terminal.Contains(
                persistedAttempt.Status))
        {
            return new BillingV2PaymentAttemptRetryDecision(
                false,
                "BILLING_V2_PAYMENT_ATTEMPT_ALREADY_TERMINAL",
                persistedAttempt.ProviderRequestKey,
                persistedAttempt.Status);
        }

        // APP-13 : le retry reutilise la cle persistee, il n'en invente pas.
        return new BillingV2PaymentAttemptRetryDecision(
            true,
            "BILLING_V2_PAYMENT_ATTEMPT_REUSABLE",
            persistedAttempt.ProviderRequestKey);
    }
}

/// <summary>
/// APP-14 : compare-and-swap sur billing_v2_subscriptions.version.
/// Un conflit remonte en echec explicite, jamais en no-op silencieux.
/// </summary>
public static class BillingV2SubscriptionVersionPolicy
{
    public const string InitialVersion = "1";

    public static long NextVersion(long currentVersion)
        => currentVersion < 1
            ? throw new ArgumentOutOfRangeException(nameof(currentVersion))
            : checked(currentVersion + 1);

    public static BillingV2FinancialDecision EvaluateCompareAndSwap(
        int affectedRows)
        => affectedRows switch
        {
            1 => BillingV2FinancialDecision.Ok(
                "BILLING_V2_SUBSCRIPTION_VERSION_ADVANCED"),
            0 => BillingV2FinancialDecision.Refused(
                "BILLING_V2_SUBSCRIPTION_VERSION_CONFLICT",
                "Expected version no longer current."),
            _ => BillingV2FinancialDecision.Refused(
                "BILLING_V2_SUBSCRIPTION_VERSION_AMBIGUOUS",
                affectedRows.ToString())
        };
}

public sealed record BillingV2ServicePriceCandidate(
    string ServicePriceId,
    string PriceCode,
    int PriceVersion,
    long AmountCents,
    string Currency,
    string BillingCadence,
    DateTime ValidFromUtc);

public sealed record BillingV2ServicePriceResolution(
    bool Resolved,
    string ReasonCode,
    BillingV2ServicePriceCandidate? Price,
    string? Diagnostic = null);

/// <summary>
/// APP-15 : resolution d'un prix applicable ambigu.
///
/// Deux versions d'un meme prix ne sont JAMAIS sommees comme deux services.
/// Si une regle versionnee explicite permet de trancher (une version
/// strictement superieure aux autres), elle s'applique. Sinon, fail closed.
/// </summary>
public static class BillingV2ServicePriceResolutionPolicy
{
    public const string ResolvedSingleReasonCode =
        "BILLING_V2_SERVICE_PRICE_RESOLVED_SINGLE";
    public const string ResolvedByVersionReasonCode =
        "BILLING_V2_SERVICE_PRICE_RESOLVED_BY_VERSION";
    public const string NotFoundReasonCode =
        "BILLING_V2_SERVICE_PRICE_NOT_FOUND";
    public const string AmbiguousReasonCode =
        "BILLING_V2_SERVICE_PRICE_AMBIGUOUS";

    public static BillingV2ServicePriceResolution Resolve(
        IReadOnlyList<BillingV2ServicePriceCandidate> candidates,
        string serviceCode,
        string? tierCode)
    {
        var scope = tierCode is null
            ? serviceCode
            : $"{serviceCode}/{tierCode}";

        if (candidates.Count == 0)
        {
            return new BillingV2ServicePriceResolution(
                false,
                NotFoundReasonCode,
                Price: null,
                scope);
        }

        if (candidates.Count == 1)
        {
            return new BillingV2ServicePriceResolution(
                true,
                ResolvedSingleReasonCode,
                candidates[0]);
        }

        var highestVersion = candidates.Max(candidate => candidate.PriceVersion);
        var winners = candidates
            .Where(candidate => candidate.PriceVersion == highestVersion)
            .ToArray();

        // Regle versionnee explicite : la version la plus haute gagne, mais
        // seulement si elle est unique. A egalite de version, aucun depart
        // deterministe n'existe et un tri arbitraire (id, date d'insertion)
        // ne serait qu'un choix silencieux : on echoue en ferme.
        return winners.Length == 1
            ? new BillingV2ServicePriceResolution(
                true,
                ResolvedByVersionReasonCode,
                winners[0],
                $"{scope}: {candidates.Count} prix actifs, version {highestVersion} retenue")
            : new BillingV2ServicePriceResolution(
                false,
                AmbiguousReasonCode,
                Price: null,
                $"{scope}: {winners.Length} prix actifs a egalite sur la version {highestVersion}");
    }
}

/// <summary>
/// Plancher d'engagement mensuel, partage par tous les chemins de creation
/// d'abonnement pour qu'aucun ne diverge.
///
/// Le plancher vaut 45 % du MRR initial APRES remise, conformement a
/// docs/billing-v2/PRICING-RULES.md, et ne s'applique qu'aux abonnements
/// mensuels reellement engages.
/// </summary>
public static class BillingV2CommitmentFloorPolicy
{
    public static long? Resolve(
        IBillingV2PricingEngine pricing,
        string paymentMode,
        int commitmentMonths,
        long discountedRecurringAmountCents)
        => string.Equals(
               paymentMode,
               BillingV2PaymentModes.Monthly,
               StringComparison.Ordinal)
           && commitmentMonths > 1
            ? pricing.CalculateMinimumCommitmentAmount(
                discountedRecurringAmountCents)
            : null;
}
