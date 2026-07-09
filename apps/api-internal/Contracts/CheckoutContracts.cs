using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record RecurringCheckoutItemResponse(
    string OfferId,
    string Name,
    string Description,
    string Category,
    string UnitLabel,
    [property: JsonPropertyName("publicPackCode")] string? PublicPackCode,
    [property: JsonPropertyName("priceAmountCents")] int PriceAmountCents,
    [property: JsonPropertyName("setupFeeAmountCents")] int SetupFeeAmountCents,
    [property: JsonPropertyName("firstChargeAmountCents")] int FirstChargeAmountCents,
    [property: JsonPropertyName("billingIntervalMonths")] int BillingIntervalMonths,
    [property: JsonPropertyName("commitmentMonths")] int CommitmentMonths,
    [property: JsonPropertyName("paymentMode")] string PaymentMode,
    string Currency);

public sealed record CheckoutBucketResponse<TItem>(
    IReadOnlyList<TItem> Items,
    int ItemCount,
    int SubtotalCents,
    string Currency);

public sealed record CheckoutSummaryResponse(
    CartSummaryResponse Cart,
    CheckoutBucketResponse<RecurringCheckoutItemResponse> Recurring,
    int TotalItemCount,
    bool HasMixedCheckout);

public sealed record CheckoutRecurringAddRequest(string? OfferId);

public sealed record CheckoutRecurringMutationResponse(
    CheckoutBucketResponse<RecurringCheckoutItemResponse> Recurring,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

public sealed record CheckoutRecurringConfirmResponse(
    string DocumentId,
    int ItemCount,
    int TotalAmountCents,
    IReadOnlyList<string> SubscriptionIds,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
