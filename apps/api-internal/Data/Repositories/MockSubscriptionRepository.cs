using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockSubscriptionStore
{
    public object SyncRoot { get; } = new();

    public List<SubscriptionSummary> Subscriptions { get; } = new();
}

public sealed class MockSubscriptionRepository : ISubscriptionRepository
{
    private readonly MockSubscriptionStore _store;

    public MockSubscriptionRepository(MockSubscriptionStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<IReadOnlyList<SubscriptionSummary>> GetByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<SubscriptionSummary>>(
                _store.Subscriptions
                    .Where(subscription => subscription.CustomerId == customerId)
                    .OrderByDescending(subscription => subscription.UpdatedAt)
                    .ToArray());
        }
    }

    public Task<IReadOnlyList<SubscriptionSummary>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<SubscriptionSummary>>(
                _store.Subscriptions
                    .OrderByDescending(subscription => subscription.UpdatedAt)
                    .ToArray());
        }
    }

    public Task<SubscriptionSummary?> GetByIdAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Subscriptions.FirstOrDefault(
                    subscription => subscription.Id == subscriptionId));
        }
    }

    public Task<SubscriptionSummary?> GetByExternalIdAsync(
        string rail,
        string externalId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Subscriptions.FirstOrDefault(
                    subscription => rail == "stripe"
                        ? subscription.StripeSubscriptionId == externalId
                        : subscription.PayPalSubscriptionId == externalId));
        }
    }

    public Task<SubscriptionSummary> CreatePendingAsync(
        string customerId,
        string commercialOfferId,
        string rail,
        string? paypalPlanId,
        string? paypalSubscriptionId,
        string? stripePriceId,
        string? stripeSubscriptionId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var now = DateTime.UtcNow.ToString("O");
            var summary = new SubscriptionSummary(
                Guid.NewGuid().ToString("D"),
                customerId,
                customerId,
                customerId,
                commercialOfferId,
                commercialOfferId,
                rail,
                paypalPlanId,
                paypalSubscriptionId,
                stripePriceId,
                stripeSubscriptionId,
                "pending_approval",
                0,
                "EUR",
                null,
                null,
                null,
                now,
                now);
            _store.Subscriptions.Add(summary);
            return Task.FromResult(summary);
        }
    }

    public Task<SubscriptionSummary> UpdateStatusAsync(
        string subscriptionId,
        string newStatus,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var index = _store.Subscriptions.FindIndex(
                subscription => subscription.Id == subscriptionId);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Subscription {subscriptionId} not found.");
            }

            var current = _store.Subscriptions[index];
            var now = DateTime.UtcNow.ToString("O");
            var cancelledAt = newStatus == "cancelled"
                ? current.CancelledAt ?? now
                : current.CancelledAt;
            var updated = current with
            {
                Status = newStatus,
                UpdatedAt = now,
                CancelledAt = cancelledAt
            };
            _store.Subscriptions[index] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<SubscriptionSummary> ActivateAsync(
        string subscriptionId,
        DateTime startedAtUtc,
        DateTime nextBillingAtUtc,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var index = _store.Subscriptions.FindIndex(
                subscription => subscription.Id == subscriptionId);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Subscription {subscriptionId} not found.");
            }

            var current = _store.Subscriptions[index];
            var now = DateTime.UtcNow.ToString("O");
            var updated = current with
            {
                Status = "active",
                StartedAt = current.StartedAt ?? startedAtUtc.ToString("O"),
                NextBillingAt = nextBillingAtUtc.ToString("O"),
                UpdatedAt = now
            };
            _store.Subscriptions[index] = updated;
            return Task.FromResult(updated);
        }
    }
}
