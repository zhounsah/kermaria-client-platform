namespace Kermaria.ApiInternal.Data.Repositories;

// Stockage du panier a la carte, rattache au client (V0.35). Le repository
// ne connait que le couple (offre, quantite) ; la jointure avec le catalogue
// et les regles metier (offre active, cadence one-shot) sont portees par
// CartService.
public interface ICartRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<CartItemRecord>> GetItemsAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task UpsertItemAsync(
        string customerId,
        string offerId,
        int quantity,
        CancellationToken cancellationToken);

    Task RemoveItemAsync(
        string customerId,
        string offerId,
        CancellationToken cancellationToken);

    Task ClearAsync(
        string customerId,
        CancellationToken cancellationToken);
}

public sealed record CartItemRecord(string OfferId, int Quantity);
