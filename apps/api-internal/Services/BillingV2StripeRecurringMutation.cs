namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2StripeRecurringMutationRequest(
    string ProviderSubscriptionId,
    string ChangeId,
    long AmountCents,
    string Currency,
    int Quantity,
    string IdempotencyKey);

public sealed record BillingV2StripeRecurringMutationResult(
    bool Succeeded,
    string ReasonCode,
    string? ProviderSubscriptionId,
    bool Retryable)
{
    public static BillingV2StripeRecurringMutationResult Disabled { get; } =
        new(false, "BILLING_V2_STRIPE_RECURRING_MUTATION_DISABLED", null, false);
}
