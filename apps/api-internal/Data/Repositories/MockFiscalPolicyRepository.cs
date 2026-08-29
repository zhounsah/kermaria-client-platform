namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockFiscalPolicyRepository : IFiscalPolicyRepository
{
    private static readonly List<StoredFiscalMention> Items = [];
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
            var storedVersion = Items.Count(item =>
                string.Equals(item.Regime, mention.Regime, StringComparison.Ordinal));
            if (storedVersion != expectedVersion)
            {
                return Task.FromResult(FiscalMentionAddOutcome.VersionConflict);
            }

            if (Items.Any(item =>
                string.Equals(item.Regime, mention.Regime, StringComparison.Ordinal)
                && item.EffectiveFromUtc == mention.EffectiveFromUtc))
            {
                return Task.FromResult(FiscalMentionAddOutcome.EffectiveDateTaken);
            }

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
            Items.RemoveAt(index);
            return Task.FromResult(true);
        }
    }

    public static void Clear()
    {
        lock (Gate) Items.Clear();
    }
}
