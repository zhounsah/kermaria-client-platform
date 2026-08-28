using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbApplicationSettingsRepository : IApplicationSettingsRepository
{
    private readonly string _connectionString;
    public MariaDbApplicationSettingsRepository(SqlRuntimeConfiguration configuration) => _connectionString = configuration.ConnectionString ?? throw new InvalidOperationException("MariaDB connection configuration is unavailable.");
    public bool IsPersistent => true;

    public async Task<IReadOnlyList<StoredApplicationSetting>> GetAllAsync(CancellationToken cancellationToken)
    {
        var values = new List<StoredApplicationSetting>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT setting_key, category, value_json, value_type, version, updated_at FROM application_settings ORDER BY setting_key;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) values.Add(new(reader.GetString("setting_key"), reader.GetString("category"), reader.GetString("value_json"), reader.GetString("value_type"), reader.GetInt32("version"), reader.GetDateTime("updated_at")));
        return values;
    }

    public async Task<StoredApplicationSetting?> GetAsync(string key, CancellationToken cancellationToken)
        => (await GetAllAsync(cancellationToken)).FirstOrDefault(item => item.Key == key);

    public async Task<bool> TryUpsertAsync(StoredApplicationSetting setting, int expectedVersion, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE application_settings SET category=@category, value_json=@value_json, value_type=@value_type, version=@next_version, updated_by_user_id=@updated_by_user_id, updated_at=@updated_at WHERE setting_key=@key AND version=@expected_version;";
        command.Parameters.AddWithValue("@category", setting.Category); command.Parameters.AddWithValue("@value_json", setting.ValueJson); command.Parameters.AddWithValue("@value_type", setting.ValueType); command.Parameters.AddWithValue("@next_version", setting.Version); command.Parameters.AddWithValue("@updated_by_user_id", setting.UpdatedByUserId is null ? DBNull.Value : setting.UpdatedByUserId); command.Parameters.AddWithValue("@updated_at", setting.UpdatedAtUtc); command.Parameters.AddWithValue("@key", setting.Key); command.Parameters.AddWithValue("@expected_version", expectedVersion);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 1) return true;
        if (expectedVersion != 0) return false;
        command.Parameters.Clear();
        command.CommandText = "INSERT IGNORE INTO application_settings (setting_key, category, value_json, value_type, version, updated_by_user_id, created_at, updated_at) VALUES (@key, @category, @value_json, @value_type, @version, @updated_by_user_id, @created_at, @updated_at);";
        command.Parameters.AddWithValue("@key", setting.Key); command.Parameters.AddWithValue("@category", setting.Category); command.Parameters.AddWithValue("@value_json", setting.ValueJson); command.Parameters.AddWithValue("@value_type", setting.ValueType); command.Parameters.AddWithValue("@version", setting.Version); command.Parameters.AddWithValue("@updated_by_user_id", setting.UpdatedByUserId is null ? DBNull.Value : setting.UpdatedByUserId); command.Parameters.AddWithValue("@created_at", setting.UpdatedAtUtc); command.Parameters.AddWithValue("@updated_at", setting.UpdatedAtUtc);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task AddRevisionAsync(string key, int version, string? oldValueJson, string newValueJson, string? actorUserId, string correlationId, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO application_setting_revisions (id, setting_key, version, old_value_json, new_value_json, actor_user_id, correlation_id, outcome, created_at) VALUES (@id, @key, @version, @old, @new, @actor, @correlation, 'success', UTC_TIMESTAMP(6));";
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("@key", key); command.Parameters.AddWithValue("@version", version); command.Parameters.AddWithValue("@old", oldValueJson is null ? DBNull.Value : oldValueJson); command.Parameters.AddWithValue("@new", newValueJson); command.Parameters.AddWithValue("@actor", actorUserId is null ? DBNull.Value : actorUserId); command.Parameters.AddWithValue("@correlation", correlationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
