using System.Collections.Concurrent;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Registre de profils en memoire pour le mode non persistant. Suffisant pour
/// le graphe DI et les tests ; le mode mock ne materialise pas de vrais comptes.
/// </summary>
public sealed class MockDemoProfileRepository : IDemoProfileRepository
{
    private readonly ConcurrentDictionary<string, DemoProfile> _profiles =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsPersistent => false;

    public Task<IReadOnlyList<DemoProfile>> ListAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DemoProfile>>(
            _profiles.Values
                .OrderBy(profile => profile.Key, StringComparer.Ordinal)
                .ToArray());

    public Task<DemoProfile?> GetByKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
        => Task.FromResult(
            _profiles.TryGetValue(key, out var profile) ? profile : null);

    public Task<DemoProfile> UpsertAsync(
        DemoProfile profile,
        CancellationToken cancellationToken = default)
    {
        var stored = _profiles.TryGetValue(profile.Key, out var existing)
            ? profile with { Id = existing.Id }
            : profile with { Id = Guid.NewGuid().ToString("D") };
        _profiles[profile.Key] = stored;
        return Task.FromResult(stored);
    }

    public Task<bool> DeleteByKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_profiles.TryRemove(key, out _));
}
