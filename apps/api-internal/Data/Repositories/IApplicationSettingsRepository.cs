namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record StoredApplicationSetting(
    string Key,
    string Category,
    string ValueJson,
    string ValueType,
    int Version,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId = null);

public interface IApplicationSettingsRepository
{
    bool IsPersistent { get; }
    Task<IReadOnlyList<StoredApplicationSetting>> GetAllAsync(CancellationToken cancellationToken);
    Task<StoredApplicationSetting?> GetAsync(string key, CancellationToken cancellationToken);
    Task<bool> TryUpsertAsync(StoredApplicationSetting setting, int expectedVersion, CancellationToken cancellationToken);
    Task AddRevisionAsync(string key, int version, string? oldValueJson, string newValueJson, string? actorUserId, string correlationId, CancellationToken cancellationToken);
}
