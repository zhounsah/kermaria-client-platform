using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record SubscriptionCreatePayload(
    string? OfferId,
    string? Rail,
    [property: JsonPropertyName("paypalSubscriptionId")] string? PayPalSubscriptionId,
    [property: JsonPropertyName("stripeSubscriptionId")] string? StripeSubscriptionId);

public sealed record SubscriptionSummary(
    string Id,
    string CustomerId,
    string CustomerReference,
    string CustomerName,
    string CommercialOfferId,
    string OfferName,
    [property: JsonPropertyName("offerExternalReference")] string? OfferExternalReference,
    [property: JsonPropertyName("publicPackCode")] string? PublicPackCode,
    string Rail,
    [property: JsonPropertyName("paypalPlanId")] string? PayPalPlanId,
    [property: JsonPropertyName("paypalSubscriptionId")] string? PayPalSubscriptionId,
    [property: JsonPropertyName("stripePriceId")] string? StripePriceId,
    [property: JsonPropertyName("stripeSubscriptionId")] string? StripeSubscriptionId,
    string Status,
    int PriceAmountCents,
    [property: JsonPropertyName("setupFeeAmountCents")] int SetupFeeAmountCents,
    [property: JsonPropertyName("billingIntervalMonths")] int BillingIntervalMonths,
    [property: JsonPropertyName("commitmentMonths")] int CommitmentMonths,
    [property: JsonPropertyName("paymentMode")] string PaymentMode,
    [property: JsonPropertyName("paidCyclesCount")] int PaidCyclesCount,
    [property: JsonPropertyName("commitmentEndsAt")] string? CommitmentEndsAt,
    [property: JsonPropertyName("cancelRequestedAt")] string? CancelRequestedAt,
    [property: JsonPropertyName("cancelAtTermEnd")] bool CancelAtTermEnd,
    string Currency,
    string? StartedAt,
    string? NextBillingAt,
    string? CancelledAt,
    string CreatedAt,
    string UpdatedAt);

public sealed record SubscriptionProvisioningTargetUserSummary(
    string SamAccountName,
    string DisplayName,
    string? UserPrincipalName);

public sealed record SubscriptionProvisioningReconcileRequest(
    IReadOnlyList<string>? TargetUserSamAccountNames);

public sealed record SubscriptionProvisioningActionSummary(
    string Id,
    string ActionType,
    string Status,
    string? ResultCode,
    bool Changed,
    string CorrelationId,
    string TargetReference,
    string RequestedAt,
    string? StartedAt,
    string? CompletedAt);

public sealed record SubscriptionProvisioningSummary(
    string Status,
    IReadOnlyList<string> MappedGroups,
    IReadOnlyList<string> ReconciledGroups,
    IReadOnlyList<SubscriptionProvisioningTargetUserSummary> TargetUsers,
    bool CanRetry,
    string? LastResultCode,
    IReadOnlyList<SubscriptionProvisioningActionSummary> RecentActions);

public sealed record AdminSubscriptionDetail(
    SubscriptionSummary Subscription,
    IReadOnlyList<CommercialDocumentSummary> Documents,
    SubscriptionProvisioningSummary Provisioning);
