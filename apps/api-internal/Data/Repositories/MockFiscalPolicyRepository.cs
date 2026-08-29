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

    public Task<bool> TryAddAsync(
        StoredFiscalMention mention,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (Items.Any(item =>
                string.Equals(item.Regime, mention.Regime, StringComparison.Ordinal)
                && item.EffectiveFromUtc == mention.EffectiveFromUtc))
            {
                return Task.FromResult(false);
            }

            Items.Add(mention);
            return Task.FromResult(true);
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
