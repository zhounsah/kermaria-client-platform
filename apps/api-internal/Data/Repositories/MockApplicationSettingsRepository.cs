namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockApplicationSettingsRepository : IApplicationSettingsRepository
{
    private static readonly Dictionary<string, StoredApplicationSetting> Values = new(StringComparer.Ordinal);
    public bool IsPersistent => false;

    public Task<IReadOnlyList<StoredApplicationSetting>> GetAllAsync(CancellationToken cancellationToken)
    {
        lock (Values) return Task.FromResult<IReadOnlyList<StoredApplicationSetting>>(Values.Values.ToArray());
    }

    public Task<StoredApplicationSetting?> GetAsync(string key, CancellationToken cancellationToken)
    {
        lock (Values) return Task.FromResult(Values.GetValueOrDefault(key));
    }

    public Task<bool> TryUpsertAsync(StoredApplicationSetting setting, int expectedVersion, CancellationToken cancellationToken)
    {
        lock (Values)
        {
            if (Values.TryGetValue(setting.Key, out var current))
            {
                if (current.Version != expectedVersion) return Task.FromResult(false);
            }
            else if (expectedVersion != 0) return Task.FromResult(false);
            Values[setting.Key] = setting;
            return Task.FromResult(true);
        }
    }
    public Task AddRevisionAsync(string key, int version, string? oldValueJson, string newValueJson, string? actorUserId, string correlationId, CancellationToken cancellationToken) => Task.CompletedTask;
}
