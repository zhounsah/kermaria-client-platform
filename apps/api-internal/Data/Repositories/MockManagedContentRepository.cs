using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockManagedContentStore
{
    public object SyncRoot { get; } = new();

    public Dictionary<string, StoredManagedContentEntry> Entries { get; } =
        new(StringComparer.Ordinal);
}

public sealed class MockManagedContentRepository : IManagedContentRepository
{
    private readonly MockManagedContentStore _store;

    public MockManagedContentRepository(MockManagedContentStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<IReadOnlyList<StoredManagedContentEntry>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<StoredManagedContentEntry>>(
                _store.Entries.Values
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    public Task<StoredManagedContentEntry?> GetAsync(
        string key,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Entries.TryGetValue(key, out var entry)
                    ? entry
                    : null);
        }
    }

    public Task SeedMissingAsync(
        IReadOnlyList<ValidatedManagedContentEntry> entries,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            foreach (var entry in entries)
            {
                if (_store.Entries.ContainsKey(entry.Key))
                {
                    continue;
                }

                var now = DateTime.UtcNow.ToString("O");
                _store.Entries[entry.Key] = new StoredManagedContentEntry(
                    entry.Key,
                    entry.ContentType,
                    entry.Title,
                    entry.PublicPath,
                    entry.BodyMarkdown,
                    entry.VersionLabel,
                    now,
                    now);
            }
        }

        return Task.CompletedTask;
    }

    public Task<ManagedContentMutationResponse> UpsertAsync(
        ValidatedManagedContentEntry entry,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Entries.TryGetValue(entry.Key, out var current);
            var createdAt = current?.CreatedAt ?? DateTime.UtcNow.ToString("O");
            var updatedAt = DateTime.UtcNow.ToString("O");
            var next = new StoredManagedContentEntry(
                entry.Key,
                entry.ContentType,
                entry.Title,
                entry.PublicPath,
                entry.BodyMarkdown,
                entry.VersionLabel,
                createdAt,
                updatedAt);
            var changed = current is null || !Equals(current with
            {
                UpdatedAt = updatedAt
            }, next);
            _store.Entries[entry.Key] = next;

            return Task.FromResult(
                new ManagedContentMutationResponse(
                    entry.Key,
                    changed,
                    updatedAt,
                    correlationId));
        }
    }
}
