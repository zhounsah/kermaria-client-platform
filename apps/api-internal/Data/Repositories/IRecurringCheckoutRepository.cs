namespace Kermaria.ApiInternal.Data.Repositories;

public interface IRecurringCheckoutRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<RecurringCheckoutItemRecord>> GetItemsAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task UpsertItemAsync(
        string customerId,
        string offerId,
        int commitmentMonths,
        string paymentMode,
        CancellationToken cancellationToken);

    Task RemoveItemAsync(
        string customerId,
        string offerId,
        CancellationToken cancellationToken);

    Task ClearAsync(
        string customerId,
        CancellationToken cancellationToken);
}

public sealed record RecurringCheckoutItemRecord(
    string OfferId,
    int CommitmentMonths,
    string PaymentMode);
