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

    /// <summary>
    /// Valeur et revision dans la meme transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le <c>SELECT ... FOR UPDATE</c> sert deux fois : il verrouille la ligne
    /// (ou l'intervalle, quand elle n'existe pas encore, ce qui serialise deux
    /// creations concurrentes de la meme cle) et il fournit la valeur remplacee
    /// telle qu'elle est au moment de l'ecriture, pas telle qu'un appelant l'a
    /// lue plus tot.
    /// </para>
    /// <para>
    /// Toute erreur remonte apres <c>ROLLBACK</c> : ni la valeur ni la revision
    /// ne subsistent. Un reglage applique sans trace serait pire qu'un reglage
    /// refuse.
    /// </para>
    /// </remarks>
    public async Task<bool> TryApplyAsync(
        StoredApplicationSetting setting,
        int expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        int storedVersion;
        string? previousValueJson;
        await using (var check = connection.CreateCommand())
        {
            check.Transaction = transaction;
            check.CommandText = "SELECT version, value_json FROM application_settings WHERE setting_key = @key FOR UPDATE;";
            check.Parameters.AddWithValue("@key", setting.Key);
            await using var reader = await check.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                storedVersion = reader.GetInt32("version");
                previousValueJson = reader.GetString("value_json");
            }
            else
            {
                storedVersion = 0;
                previousValueJson = null;
            }
        }

        if (storedVersion != expectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText =
                """
                INSERT INTO application_settings (
                    setting_key, category, value_json, value_type, version,
                    updated_by_user_id, created_at, updated_at)
                VALUES (
                    @key, @category, @value_json, @value_type, @version,
                    @updated_by_user_id, @updated_at, @updated_at)
                ON DUPLICATE KEY UPDATE
                    category = VALUES(category),
                    value_json = VALUES(value_json),
                    value_type = VALUES(value_type),
                    version = VALUES(version),
                    updated_by_user_id = VALUES(updated_by_user_id),
                    updated_at = VALUES(updated_at);
                """;
            upsert.Parameters.AddWithValue("@key", setting.Key);
            upsert.Parameters.AddWithValue("@category", setting.Category);
            upsert.Parameters.AddWithValue("@value_json", setting.ValueJson);
            upsert.Parameters.AddWithValue("@value_type", setting.ValueType);
            upsert.Parameters.AddWithValue("@version", setting.Version);
            upsert.Parameters.AddWithValue("@updated_by_user_id", setting.UpdatedByUserId is null ? DBNull.Value : setting.UpdatedByUserId);
            upsert.Parameters.AddWithValue("@updated_at", setting.UpdatedAtUtc);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var revision = connection.CreateCommand())
        {
            revision.Transaction = transaction;
            revision.CommandText = "INSERT INTO application_setting_revisions (id, setting_key, version, old_value_json, new_value_json, actor_user_id, correlation_id, outcome, created_at) VALUES (@id, @key, @version, @old, @new, @actor, @correlation, 'success', UTC_TIMESTAMP(6));";
            revision.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            revision.Parameters.AddWithValue("@key", setting.Key);
            revision.Parameters.AddWithValue("@version", setting.Version);
            revision.Parameters.AddWithValue("@old", previousValueJson is null ? DBNull.Value : previousValueJson);
            revision.Parameters.AddWithValue("@new", setting.ValueJson);
            revision.Parameters.AddWithValue("@actor", setting.UpdatedByUserId is null ? DBNull.Value : setting.UpdatedByUserId);
            revision.Parameters.AddWithValue("@correlation", correlationId);
            await revision.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
