using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface ISubscriptionRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<SubscriptionSummary>> GetByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionSummary>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<SubscriptionSummary?> GetByIdAsync(
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary?> GetByExternalIdAsync(
        string rail,
        string externalId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> CreatePendingAsync(
        string customerId,
        string commercialOfferId,
        string rail,
        string? paypalPlanId,
        string? paypalSubscriptionId,
        string? stripePriceId,
        string? stripeSubscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> UpdateStatusAsync(
        string subscriptionId,
        string newStatus,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> ActivateAsync(
        string subscriptionId,
        DateTime startedAtUtc,
        DateTime nextBillingAtUtc,
        CancellationToken cancellationToken);
}
