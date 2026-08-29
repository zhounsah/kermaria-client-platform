namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockFiscalPolicyRepository : IFiscalPolicyRepository
{
    private static readonly List<StoredFiscalMention> Items = [];

    // Version monotone par regime, comme la table `fiscal_policy_regime_versions`.
    // Le decompte des mentions ne peut pas jouer ce role : une suppression le
    // fait redescendre, et un `expectedVersion` perime redevient valide.
    private static readonly Dictionary<string, int> Versions =
        new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    public bool IsPersistent => false;

    public Task<IReadOnlyList<StoredFiscalMention>> ListAsync(
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            return Task.FromResult<IReadOnlyList<StoredFiscalMention>>(
                Items
                    .OrderBy(item => item.Regime, StringComparer.Ordinal)
                    .ThenBy(item => item.EffectiveFromUtc)
                    .ToArray());
        }
    }

    public Task<IReadOnlyDictionary<string, int>> GetRegimeVersionsAsync(
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            return Task.FromResult<IReadOnlyDictionary<string, int>>(
                new Dictionary<string, int>(Versions, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// Verification de version et ajout sous le meme verrou : deux ajouts
    /// concurrents ne peuvent pas partir de la meme version.
    /// </summary>
    public Task<FiscalMentionAddOutcome> TryAddAsync(
        StoredFiscalMention mention,
        int expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (CurrentVersion(mention.Regime) != expectedVersion)
            {
                return Task.FromResult(FiscalMentionAddOutcome.VersionConflict);
            }

            if (Items.Any(item =>
                string.Equals(item.Regime, mention.Regime, StringComparison.Ordinal)
                && item.EffectiveFromUtc == mention.EffectiveFromUtc))
            {
                return Task.FromResult(FiscalMentionAddOutcome.EffectiveDateTaken);
            }

            // Increment avant la mutation : la version courante se lit sur
            // l'etat d'avant, comme l'amorce SQL qui compte les mentions.
            Bump(mention.Regime);
            Items.Add(mention);
            return Task.FromResult(FiscalMentionAddOutcome.Added);
        }
    }

    public Task<bool> TryDeleteScheduledAsync(
        string id,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            var index = Items.FindIndex(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal)
                && item.EffectiveFromUtc > nowUtc);
            if (index < 0) return Task.FromResult(false);

            var regime = Items[index].Regime;
            // La suppression compte : sans cela le numero redescendrait.
            Bump(regime);
            Items.RemoveAt(index);
            return Task.FromResult(true);
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Items.Clear();
            Versions.Clear();
        }
    }

    /// <remarks>
    /// Un regime jamais versionne vaut le nombre de mentions qu'il porte, comme
    /// l'amorce de la migration.
    /// </remarks>
    private static int CurrentVersion(string regime)
        => Versions.TryGetValue(regime, out var version)
            ? version
            : Items.Count(item =>
                string.Equals(item.Regime, regime, StringComparison.Ordinal));

    private static void Bump(string regime)
        => Versions[regime] = CurrentVersion(regime) + 1;
}
