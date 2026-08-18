using System.Collections.Concurrent;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Jetons de mot de passe en memoire.
/// </summary>
/// <remarks>
/// Reproduit les invariants de la table, y compris la consommation exclusive :
/// un test de concurrence qui s'appuierait sur une implementation permissive ne
/// prouverait rien. Le verrou global remplace ici le <c>FOR UPDATE</c>.
/// </remarks>
public sealed class MockPortalPasswordSetupRepository
    : IPortalPasswordSetupRepository
{
    private sealed class Entry
    {
        public required string Id { get; init; }
        public required string PortalUserId { get; init; }
        public required string Purpose { get; init; }
        public required DateTime ExpiresAtUtc { get; set; }
        public DateTime? ConsumedAtUtc { get; set; }
        public DateTime? SupersededAtUtc { get; set; }
    }

    private readonly ConcurrentDictionary<string, Entry> _byTokenHash =
        new(StringComparer.Ordinal);
    private readonly MockPortalUserStore _portalUsers;
    private readonly object _gate = new();

    public MockPortalPasswordSetupRepository(MockPortalUserStore portalUsers)
    {
        _portalUsers = portalUsers;
    }

    public bool IsPersistent => false;

    public int Count => _byTokenHash.Count;

    /// <summary>
    /// Vrai si un jeton en clair a fuite dans le magasin.
    /// </summary>
    /// <remarks>
    /// Le depot n'indexe que des condensats. Cette sonde permet a un test
    /// d'affirmer que le clair n'a jamais ete persiste, plutot que de le
    /// supposer.
    /// </remarks>
    public bool ContainsRawToken(string rawToken)
        => _byTokenHash.Keys.Any(key => string.Equals(
            key,
            rawToken,
            StringComparison.Ordinal));

    public Task IssueAsync(
        PortalPasswordSetupIssue issue,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            foreach (var entry in _byTokenHash.Values)
            {
                if (string.Equals(
                        entry.PortalUserId,
                        issue.PortalUserId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        entry.Purpose,
                        issue.Purpose,
                        StringComparison.Ordinal)
                    && entry.ConsumedAtUtc is null
                    && entry.SupersededAtUtc is null)
                {
                    entry.SupersededAtUtc = DateTime.UtcNow;
                }
            }

            _byTokenHash[issue.TokenHash] = new Entry
            {
                Id = issue.Id,
                PortalUserId = issue.PortalUserId,
                Purpose = issue.Purpose,
                ExpiresAtUtc = issue.ExpiresAtUtc
            };
        }

        return Task.CompletedTask;
    }

    public Task<PortalPasswordSetupTarget?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_byTokenHash.TryGetValue(tokenHash, out var entry))
            {
                return Task.FromResult<PortalPasswordSetupTarget?>(null);
            }

            return Task.FromResult<PortalPasswordSetupTarget?>(
                new PortalPasswordSetupTarget(
                    entry.Id,
                    entry.PortalUserId,
                    entry.Purpose,
                    entry.ExpiresAtUtc,
                    entry.ConsumedAtUtc is not null,
                    entry.SupersededAtUtc is not null));
        }
    }

    public Task<PortalPasswordSetupConsumption> ConsumeAndSetPasswordAsync(
        string tokenHash,
        Func<string, string> hashPasswordForUser,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_byTokenHash.TryGetValue(tokenHash, out var entry))
            {
                return Task.FromResult(new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.TokenInvalid,
                    null));
            }

            if (entry.ConsumedAtUtc is not null)
            {
                return Task.FromResult(new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.TokenAlreadyUsed,
                    null));
            }

            if (entry.SupersededAtUtc is not null)
            {
                return Task.FromResult(new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.TokenInvalid,
                    null));
            }

            if (entry.ExpiresAtUtc <= DateTime.UtcNow)
            {
                return Task.FromResult(new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.TokenExpired,
                    null));
            }

            if (!_portalUsers.TrySetPasswordHash(
                    entry.PortalUserId,
                    hashPasswordForUser(entry.PortalUserId)))
            {
                return Task.FromResult(new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.TokenInvalid,
                    null));
            }

            entry.ConsumedAtUtc = DateTime.UtcNow;
            return Task.FromResult(new PortalPasswordSetupConsumption(
                PortalPasswordSetupCodes.Consumed,
                entry.PortalUserId));
        }
    }

    /// <summary>Force l'expiration d'un jeton, pour les tests.</summary>
    public void ExpireForTests(string tokenHash)
    {
        lock (_gate)
        {
            if (_byTokenHash.TryGetValue(tokenHash, out var entry))
            {
                entry.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            }
        }
    }
}
