namespace Kermaria.ApiInternal.Data.Repositories;

// Etat panier en memoire pour le mode non persistant (dev / mock). Le store
// est enregistre en singleton pour survivre entre les requetes.
public sealed class MockCartStore
{
    public object SyncRoot { get; } = new();

    public List<MockCartItem> Items { get; } = new();
}

public sealed class MockCartItem
{
    public required string CustomerId { get; set; }
    public required string OfferId { get; set; }
    public int Quantity { get; set; }
}

public sealed class MockCartRepository : ICartRepository
{
    private readonly MockCartStore _store;

    public MockCartRepository(MockCartStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<IReadOnlyList<CartItemRecord>> GetItemsAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<CartItemRecord>>(
                _store.Items
                    .Where(item => item.CustomerId == customerId)
                    .Select(item => new CartItemRecord(item.OfferId, item.Quantity))
                    .ToArray());
        }
    }

    public Task UpsertItemAsync(
        string customerId,
        string offerId,
        int quantity,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var existing = _store.Items.FirstOrDefault(
                item => item.CustomerId == customerId && item.OfferId == offerId);
            if (existing is null)
            {
                _store.Items.Add(new MockCartItem
                {
                    CustomerId = customerId,
                    OfferId = offerId,
                    Quantity = quantity,
                });
            }
            else
            {
                existing.Quantity = quantity;
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
            _store.Items.RemoveAll(
                item => item.CustomerId == customerId && item.OfferId == offerId);
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
