namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockRecurringCheckoutStore
{
    public object SyncRoot { get; } = new();

    public List<MockRecurringCheckoutItem> Items { get; } = [];
}

public sealed class MockRecurringCheckoutItem
{
    public required string CustomerId { get; set; }
    public required string OfferId { get; set; }
    public required int CommitmentMonths { get; set; }
    public required string PaymentMode { get; set; }
}

public sealed class MockRecurringCheckoutRepository
    : IRecurringCheckoutRepository
{
    private readonly MockRecurringCheckoutStore _store;

    public MockRecurringCheckoutRepository(MockRecurringCheckoutStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<IReadOnlyList<RecurringCheckoutItemRecord>> GetItemsAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<RecurringCheckoutItemRecord>>(
                _store.Items
                    .Where(item => item.CustomerId == customerId)
                    .Select(item => new RecurringCheckoutItemRecord(
                        item.OfferId,
                        item.CommitmentMonths,
                        item.PaymentMode))
                    .ToArray());
        }
    }

    public Task UpsertItemAsync(
        string customerId,
        string offerId,
        int commitmentMonths,
        string paymentMode,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var existing = _store.Items.FirstOrDefault(item =>
                item.CustomerId == customerId
                && item.OfferId == offerId);
            if (existing is null)
            {
                _store.Items.Add(new MockRecurringCheckoutItem
                {
                    CustomerId = customerId,
                    OfferId = offerId,
                    CommitmentMonths = commitmentMonths,
                    PaymentMode = paymentMode,
                });
            }
            else
            {
                existing.CommitmentMonths = commitmentMonths;
                existing.PaymentMode = paymentMode;
            }
        }

        return Task.CompletedTask;
    }

    public Task RemoveItemAsync(
        string customerId,
        string offerId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Items.RemoveAll(item =>
                item.CustomerId == customerId
                && item.OfferId == offerId);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Items.RemoveAll(item => item.CustomerId == customerId);
        }

        return Task.CompletedTask;
    }
}
