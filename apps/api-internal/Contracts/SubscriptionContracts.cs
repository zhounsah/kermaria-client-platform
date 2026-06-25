namespace Kermaria.ApiInternal.Contracts;

public sealed record SubscriptionCreatePayload(
    string? OfferId,
    string? PayPalSubscriptionId);

public sealed record SubscriptionSummary(
    string Id,
    string CustomerId,
    string CustomerReference,
    string CustomerName,
    string CommercialOfferId,
    string OfferName,
    string PayPalPlanId,
    string? PayPalSubscriptionId,
    string Status,
    int PriceAmountCents,
    string Currency,
    string? StartedAt,
    string? NextBillingAt,
    string? CancelledAt,
    string CreatedAt,
    string UpdatedAt);
