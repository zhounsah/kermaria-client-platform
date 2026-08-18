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

    /// <summary>
    /// Recoit le secret scelle quand la transaction simulee aboutit.
    /// </summary>
    /// <remarks>
    /// Renseigne apres construction : le magasin de secrets et ce depot se
    /// connaissent mutuellement, et l'un des deux doit donc etre branche
    /// ensuite.
    /// </remarks>
    public IKoxoPendingPasswordSealSink? SealSink { get; set; }

    /// <summary>Fait avancer le cycle de vie dans la meme unite de travail.</summary>
    public IBillingV2UserIdentityTransitionSink? LifecycleSink { get; set; }

    public Task<PortalPasswordSetupConsumption> ConsumeAndSetPasswordAsync(
        string tokenHash,
        Func<string, string> hashPasswordForUser,
        PortalPasswordHandoff? handoff,
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

            if (handoff is not null
                && !string.Equals(
                    handoff.PortalUserId,
                    entry.PortalUserId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.TokenInvalid,
                    null));
            }

            var previousHash = _portalUsers.Find(entry.PortalUserId)
                ?.PasswordHash;
            if (!_portalUsers.TrySetPasswordHash(
                    entry.PortalUserId,
                    hashPasswordForUser(entry.PortalUserId)))
            {
                return Task.FromResult(new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.TokenInvalid,
                    null));
            }

            // Tout ou rien, comme la transaction reelle : si le relais ou la
            // transition echoue, le mot de passe pose est repris et le jeton
            // n'est pas consomme. Sans cela le mock validerait un chemin que
            // la base refuse, et la classe de bug qu'on corrige resterait
            // invisible hors MariaDB.
            if (handoff is not null && !TryApplyHandoff(handoff))
            {
                _portalUsers.TrySetPasswordHash(
                    entry.PortalUserId,
                    previousHash);
                return Task.FromResult(new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.HandoffFailed,
                    null));
            }

            entry.ConsumedAtUtc = DateTime.UtcNow;
            return Task.FromResult(new PortalPasswordSetupConsumption(
                PortalPasswordSetupCodes.Consumed,
                entry.PortalUserId));
        }
    }

    private bool TryApplyHandoff(PortalPasswordHandoff handoff)
    {
        try
        {
            if (handoff.Secret is not null)
            {
                if (SealSink is null)
                {
                    return false;
                }

                SealSink.AttachSealed(handoff.PortalUserId, handoff.Secret);
            }

            return LifecycleSink is not null
                && LifecycleSink.TryMarkKoxoPending(
                    handoff.LifecycleId,
                    handoff.PortalUserId,
                    handoff.AtUtc);
        }
        catch (InvalidOperationException)
        {
            // Panne simulee du relais : traitee comme un echec de transaction,
            // pas comme une exception qui remonterait a l'appelant.
            return false;
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
