using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record SubscriptionCreatePayload(
    string? OfferId,
    [property: JsonPropertyName("paypalSubscriptionId")] string? PayPalSubscriptionId);

public sealed record SubscriptionSummary(
    string Id,
    string CustomerId,
    string CustomerReference,
    string CustomerName,
    string CommercialOfferId,
    string OfferName,
    [property: JsonPropertyName("paypalPlanId")] string PayPalPlanId,
    [property: JsonPropertyName("paypalSubscriptionId")] string? PayPalSubscriptionId,
    string Status,
    int PriceAmountCents,
    string Currency,
    string? StartedAt,
    string? NextBillingAt,
    string? CancelledAt,
    string CreatedAt,
    string UpdatedAt);

public sealed record AdminSubscriptionDetail(
    SubscriptionSummary Subscription,
    IReadOnlyList<CommercialDocumentSummary> Documents);
