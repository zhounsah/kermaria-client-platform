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
}
