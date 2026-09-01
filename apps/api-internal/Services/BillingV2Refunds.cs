using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Noyau de remboursement Billing V2. Cette primitive ne connait ni produit,
/// ni VPS : un appelant serveur lui fournit seulement l'evenement financier et
/// le motif de compensation. Le montant est toujours relu depuis l'evenement
/// settled et n'est jamais une entree d'API.
/// </summary>
public static class BillingV2RefundStatuses
{
    public const string Requested = "requested";
    public const string PendingProvider = "pending_provider";
    public const string Confirmed = "confirmed";
    public const string Failed = "failed";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Requested, PendingProvider, Confirmed, Failed
        };
}

public sealed record BillingV2RefundSourceSnapshot(
    string BillingEventId,
    string SubscriptionId,
    string SettlementStatus,
    string DocumentStatus,
    long TotalAmountCents,
    string Currency,
    string? PaymentAttemptId,
    string? Provider,
    string? Environment,
    string? ProviderPaymentId,
    bool HasRecurringComponent = false,
    string? ProviderSubscriptionId = null);

public sealed record BillingV2RefundRequestDecision(
    bool IsValid,
    string ReasonCode,
    long AmountCents,
    string? Currency,
    string? Diagnostic = null)
{
    public static BillingV2RefundRequestDecision Refused(
        string reasonCode,
        string? diagnostic = null)
        => new(false, reasonCode, 0, null, diagnostic);
}

/// <summary>
/// APP-R1 a APP-R6. Une demande V1 couvre exclusivement la charge totale
/// deja settlementee. Une facture deja emise exige un avoir canonique : tant
/// que ce flux documentaire n'existe pas, le refus est intentionnel.
/// </summary>
public static class BillingV2RefundPolicy
{
    public static BillingV2RefundRequestDecision EvaluateFullRequest(
        BillingV2RefundSourceSnapshot? source)
    {
        if (source is null)
        {
            return BillingV2RefundRequestDecision.Refused(
                "BILLING_V2_REFUND_BILLING_EVENT_NOT_FOUND");
        }

        if (!string.Equals(
                source.SettlementStatus,
                BillingV2SettlementStatuses.Settled,
                StringComparison.Ordinal))
        {
            return BillingV2RefundRequestDecision.Refused(
                "BILLING_V2_REFUND_PAYMENT_NOT_SETTLED",
                source.SettlementStatus);
        }

        if (!string.Equals(
                source.DocumentStatus,
                BillingV2EventDocumentStatuses.None,
                StringComparison.Ordinal))
        {
            // FIN-REF-1 : il est interdit de rembourser une charge documentee
            // OU en cours de documentation sans avoir. Ce n'est pas un
            // probleme que Stripe puisse resoudre ; refuser aussi pending
            // evite une course avec l'emission BPCE.
            return BillingV2RefundRequestDecision.Refused(
                string.Equals(
                    source.DocumentStatus,
                    BillingV2EventDocumentStatuses.Issued,
                    StringComparison.Ordinal)
                    ? "BILLING_V2_REFUND_CREDIT_NOTE_REQUIRED"
                    : "BILLING_V2_REFUND_DOCUMENT_IN_PROGRESS",
                source.DocumentStatus);
        }

        if (source.TotalAmountCents <= 0
            || string.IsNullOrWhiteSpace(source.Currency))
        {
            return BillingV2RefundRequestDecision.Refused(
                "BILLING_V2_REFUND_SOURCE_AMOUNT_INVALID");
        }

        if (string.IsNullOrWhiteSpace(source.PaymentAttemptId)
            || !string.Equals(source.Provider, "stripe", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(source.Environment)
            || string.IsNullOrWhiteSpace(source.ProviderPaymentId))
        {
            return BillingV2RefundRequestDecision.Refused(
                "BILLING_V2_REFUND_PROVIDER_PAYMENT_UNRESOLVED");
        }

        if (source.HasRecurringComponent
            && string.IsNullOrWhiteSpace(source.ProviderSubscriptionId))
        {
            // Un remboursement ne doit jamais laisser un abonnement Stripe
            // renouvelable parce que son ancre locale a ete perdue.
            return BillingV2RefundRequestDecision.Refused(
                "BILLING_V2_REFUND_RECURRING_SUBSCRIPTION_UNRESOLVED");
        }

        return new BillingV2RefundRequestDecision(
            true,
            "BILLING_V2_REFUND_FULL_REQUEST_AUTHORIZED",
            source.TotalAmountCents,
            source.Currency.Trim().ToUpperInvariant());
    }
}

public sealed record BillingV2RefundProviderObservation(
    string? ProviderRefundId,
    string? Status,
    long? AmountCents,
    string? Currency,
    string? ProviderPaymentId);

public sealed record BillingV2RefundConfirmationDecision(
    bool IsConfirmed,
    bool IsFailed,
    string ReasonCode,
    string? Diagnostic = null);

/// <summary>
/// La seule politique qui autorise le passage de l'evenement a <c>refunded</c>.
/// Un POST Stripe reussi, un webhook ou un statut local pending ne sont jamais
/// une preuve suffisante : on relit un refund identifie chez le provider.
/// </summary>
public static class BillingV2RefundConfirmationPolicy
{
    public static BillingV2RefundConfirmationDecision Evaluate(
        BillingV2RefundSourceSnapshot source,
        BillingV2RefundProviderObservation? observation)
    {
        if (!string.Equals(
                source.SettlementStatus,
                BillingV2SettlementStatuses.Settled,
                StringComparison.Ordinal))
        {
            return Refused("BILLING_V2_REFUND_SOURCE_NO_LONGER_SETTLED");
        }

        if (observation is null
            || string.IsNullOrWhiteSpace(observation.ProviderRefundId))
        {
            return Refused("BILLING_V2_REFUND_PROVIDER_NOT_OBSERVED");
        }

        if (string.Equals(observation.Status, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(observation.Status, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            return new BillingV2RefundConfirmationDecision(
                false,
                true,
                "BILLING_V2_REFUND_PROVIDER_FAILED");
        }

        if (!string.Equals(observation.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return Refused("BILLING_V2_REFUND_PROVIDER_PENDING", observation.Status);
        }

        if (!string.Equals(
                observation.ProviderPaymentId,
                source.ProviderPaymentId,
                StringComparison.Ordinal))
        {
            return Refused("BILLING_V2_REFUND_PROVIDER_PAYMENT_MISMATCH");
        }

        if (observation.AmountCents != source.TotalAmountCents)
        {
            return Refused("BILLING_V2_REFUND_AMOUNT_MISMATCH");
        }

        if (!string.Equals(
                observation.Currency,
                source.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            return Refused("BILLING_V2_REFUND_CURRENCY_MISMATCH");
        }

        return new BillingV2RefundConfirmationDecision(
            true,
            false,
            "BILLING_V2_REFUND_PROVIDER_CONFIRMED");
    }

    private static BillingV2RefundConfirmationDecision Refused(
        string reasonCode,
        string? diagnostic = null)
        => new(false, false, reasonCode, diagnostic);
}

/// <summary>
/// Compensation contractuelle appliquée avec la confirmation du refund. Elle
/// ne dépend jamais d'un droit client de résiliation : un produit intégralement
/// remboursé ne doit plus renouveler, même si le libre-service est fermé.
/// </summary>
public sealed record BillingV2RefundSubscriptionCompensationDecision(
    bool IsValid,
    bool BlockLocalRenewal,
    bool QueueProviderCancellation,
    string ReasonCode);

public static class BillingV2RefundSubscriptionCompensationPolicy
{
    public static BillingV2RefundSubscriptionCompensationDecision Evaluate(
        BillingV2RefundSourceSnapshot source)
    {
        if (string.IsNullOrWhiteSpace(source.SubscriptionId))
        {
            return new(false, false, false,
                "BILLING_V2_REFUND_SUBSCRIPTION_UNRESOLVED");
        }

        if (source.HasRecurringComponent
            && (string.IsNullOrWhiteSpace(source.Provider)
                || string.IsNullOrWhiteSpace(source.Environment)
                || string.IsNullOrWhiteSpace(source.ProviderSubscriptionId)))
        {
            return new(false, false, false,
                "BILLING_V2_REFUND_RECURRING_SUBSCRIPTION_UNRESOLVED");
        }

        return new(
            true,
            BlockLocalRenewal: true,
            QueueProviderCancellation: source.HasRecurringComponent,
            "BILLING_V2_REFUND_RENEWAL_BLOCK_REQUIRED");
    }
}

public sealed record BillingV2RefundOutboxPayload(
    string RefundId,
    string BillingEventId,
    string Provider,
    string Environment,
    string ProviderPaymentId);

public static class BillingV2RefundOutbox
{
    public const string AggregateType = "billing_v2_refund";
    public const string EventType = "billing_v2.provider_refund.create_requested";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static string Serialize(BillingV2RefundOutboxPayload payload)
        => JsonSerializer.Serialize(payload, JsonOptions);

    public static BillingV2RefundOutboxPayload Parse(string payloadText)
        => JsonSerializer.Deserialize<BillingV2RefundOutboxPayload>(
               payloadText,
               JsonOptions)
           ?? throw new InvalidOperationException(
               "BILLING_V2_REFUND_OUTBOX_PAYLOAD_INVALID");

    public static string CanonicalIdempotencyKey(string billingEventId)
        => $"billing-v2-refund|full|{billingEventId}";

    public static string ComputeIdempotencyHash(string billingEventId)
        => Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(CanonicalIdempotencyKey(billingEventId))))
            .ToLowerInvariant();
}

/// <summary>
/// Garde d'activation. Le flag ne donne aucun droit client : il autorise
/// uniquement l'execution serveur des demandes deja persistees et auditees.
/// </summary>
public static class BillingV2RefundExecutionGate
{
    public static BillingV2FinancialDecision Evaluate(
        BillingV2RuntimeConfiguration configuration,
        bool persistentSqlAvailable,
        bool stripeGatewayAvailable)
    {
        if (!configuration.RefundsEnabled)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_REFUND_FLAG_OFF");
        }

        if (!configuration.ProviderOutboxEnabled)
        {
            // La demande et son outbox sont atomiques. Accepter le refund sans
            // worker executable produirait une intention financiere bloquee
            // indefiniment, ce qui n'est pas une capacite operationnelle.
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_REFUND_PROVIDER_OUTBOX_OFF");
        }

        if (!persistentSqlAvailable)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_REFUND_NO_PERSISTENT_SQL");
        }

        if (!stripeGatewayAvailable)
        {
            return BillingV2FinancialDecision.Refused(
                "BILLING_V2_REFUND_STRIPE_GATEWAY_UNAVAILABLE");
        }

        return BillingV2FinancialDecision.Ok("BILLING_V2_REFUND_READY");
    }
}
