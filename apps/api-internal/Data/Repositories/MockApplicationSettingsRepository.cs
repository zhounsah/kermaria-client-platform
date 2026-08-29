namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockApplicationSettingsRepository : IApplicationSettingsRepository
{
    private sealed record Revision(
        string Key,
        int Version,
        string? OldValueJson,
        string NewValueJson,
        string? ActorUserId,
        string CorrelationId,
        DateTime CreatedAtUtc);

    private static readonly Dictionary<string, StoredApplicationSetting> Values = new(StringComparer.Ordinal);
    private static readonly List<Revision> Revisions = [];
    public bool IsPersistent => false;

    public Task<IReadOnlyList<StoredApplicationSetting>> GetAllAsync(CancellationToken cancellationToken)
    {
        lock (Values) return Task.FromResult<IReadOnlyList<StoredApplicationSetting>>(Values.Values.ToArray());
    }

    public Task<StoredApplicationSetting?> GetAsync(string key, CancellationToken cancellationToken)
    {
        lock (Values) return Task.FromResult(Values.GetValueOrDefault(key));
    }

    /// <summary>
    /// Valeur et revision sous le meme verrou : soit les deux sont posees, soit
    /// aucune. La panne simulee est levee apres la preparation et avant la
    /// publication, la ou une transaction MariaDB ferait un <c>ROLLBACK</c>.
    /// </summary>
    public Task<bool> TryApplyAsync(
        StoredApplicationSetting setting,
        int expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Values)
        {
            string? previousValueJson = null;
            if (Values.TryGetValue(setting.Key, out var current))
            {
                if (current.Version != expectedVersion) return Task.FromResult(false);
                previousValueJson = current.ValueJson;
            }
            else if (expectedVersion != 0) return Task.FromResult(false);

            MockRevisionFailureSwitch.ThrowIfArmed();

            Values[setting.Key] = setting;
            Revisions.Add(new Revision(
                setting.Key,
                setting.Version,
                previousValueJson,
                setting.ValueJson,
                setting.UpdatedByUserId,
                correlationId,
                DateTime.UtcNow));
            return Task.FromResult(true);
        }
    }

    /// <summary>Nombre de revisions enregistrees pour une cle (tests).</summary>
    public static int RevisionCount(string key)
    {
        lock (Values) return Revisions.Count(item => string.Equals(item.Key, key, StringComparison.Ordinal));
    }

    public static void Clear()
    {
        lock (Values)
        {
            Values.Clear();
            Revisions.Clear();
        }
    }
}
