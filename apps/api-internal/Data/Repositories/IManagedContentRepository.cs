using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IManagedContentRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<StoredManagedContentEntry>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<StoredManagedContentEntry?> GetAsync(
        string key,
        CancellationToken cancellationToken);

    Task SeedMissingAsync(
        IReadOnlyList<ValidatedManagedContentEntry> entries,
        CancellationToken cancellationToken);

    Task<ManagedContentMutationResponse> UpsertAsync(
        ValidatedManagedContentEntry entry,
        string correlationId,
        CancellationToken cancellationToken);
}
