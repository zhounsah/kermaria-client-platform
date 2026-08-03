using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbDemoProfileRepository : IDemoProfileRepository
{
    private readonly string _connectionString;

    public MariaDbDemoProfileRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<DemoProfile>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = new List<DemoProfile>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"{SelectColumns} FROM demo_profiles ORDER BY profile_key;";
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            profiles.Add(Map(reader));
        }

        return profiles;
    }

    public async Task<DemoProfile?> GetByKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"{SelectColumns} FROM demo_profiles WHERE profile_key = @key LIMIT 1;";
        command.Parameters.AddWithValue("@key", key);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<DemoProfile> UpsertAsync(
        DemoProfile profile,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var existing = await GetIdByKeyAsync(
            connection,
            profile.Key,
            cancellationToken);
        var id = existing ?? Guid.NewGuid().ToString("D");
        var adGroupsJson = JsonSerializer.Serialize(profile.AdGroups);

        await using var command = connection.CreateCommand();
        command.CommandText = existing is null
            ? """
              INSERT INTO demo_profiles (
                  id, profile_key, label, kind, content_template_key,
                  email_mode, bpce_mode, payment_mode, ad_provisioning_mode,
                  ad_groups_json, storage_quota_go, rds_session_mode,
                  lifetime_days, status, created_at, updated_at
              ) VALUES (
                  @id, @key, @label, @kind, @content_template_key,
                  @email_mode, @bpce_mode, @payment_mode, @ad_provisioning_mode,
                  @ad_groups_json, @storage_quota_go, @rds_session_mode,
                  @lifetime_days, @status, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
              );
              """
            : """
              UPDATE demo_profiles SET
                  label = @label,
                  kind = @kind,
                  content_template_key = @content_template_key,
                  email_mode = @email_mode,
                  bpce_mode = @bpce_mode,
                  payment_mode = @payment_mode,
                  ad_provisioning_mode = @ad_provisioning_mode,
                  ad_groups_json = @ad_groups_json,
                  storage_quota_go = @storage_quota_go,
                  rds_session_mode = @rds_session_mode,
                  lifetime_days = @lifetime_days,
                  status = @status,
                  updated_at = UTC_TIMESTAMP(6)
              WHERE id = @id;
              """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@key", profile.Key);
        command.Parameters.AddWithValue("@label", profile.Label);
        command.Parameters.AddWithValue("@kind", profile.Kind);
        command.Parameters.AddWithValue(
            "@content_template_key",
            (object?)profile.ContentTemplateKey ?? DBNull.Value);
        command.Parameters.AddWithValue("@email_mode", profile.EmailMode);
        command.Parameters.AddWithValue("@bpce_mode", profile.BpceMode);
        command.Parameters.AddWithValue("@payment_mode", profile.PaymentMode);
        command.Parameters.AddWithValue(
            "@ad_provisioning_mode",
            profile.AdProvisioningMode);
        command.Parameters.AddWithValue("@ad_groups_json", adGroupsJson);
        command.Parameters.AddWithValue(
            "@storage_quota_go",
            (object?)profile.StorageQuotaGo ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@rds_session_mode",
            profile.RdsSessionMode);
        command.Parameters.AddWithValue("@lifetime_days", profile.LifetimeDays);
        command.Parameters.AddWithValue("@status", profile.Status);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return profile with { Id = id };
    }

    public async Task<bool> DeleteByKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM demo_profiles WHERE profile_key = @key;";
        command.Parameters.AddWithValue("@key", key);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    private static async Task<string?> GetIdByKeyAsync(
        MySqlConnection connection,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id FROM demo_profiles WHERE profile_key = @key LIMIT 1;";
        command.Parameters.AddWithValue("@key", key);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value?.ToString();
    }

    private const string SelectColumns =
        """
        SELECT
            id, profile_key, label, kind, content_template_key,
            email_mode, bpce_mode, payment_mode, ad_provisioning_mode,
            ad_groups_json, storage_quota_go, rds_session_mode,
            lifetime_days, status
        """;

    private static DemoProfile Map(MySqlDataReader reader)
    {
        var adGroupsOrdinal = reader.GetOrdinal("ad_groups_json");
        var adGroups = reader.IsDBNull(adGroupsOrdinal)
            ? Array.Empty<string>()
            : DeserializeGroups(reader.GetString(adGroupsOrdinal));
        var contentOrdinal = reader.GetOrdinal("content_template_key");
        var quotaOrdinal = reader.GetOrdinal("storage_quota_go");

        return new DemoProfile(
            reader.GetString("id"),
            reader.GetString("profile_key"),
            reader.GetString("label"),
            reader.GetString("kind"),
            reader.IsDBNull(contentOrdinal)
                ? null
                : reader.GetString(contentOrdinal),
            reader.GetString("email_mode"),
            reader.GetString("bpce_mode"),
            reader.GetString("payment_mode"),
            reader.GetString("ad_provisioning_mode"),
            adGroups,
            reader.IsDBNull(quotaOrdinal) ? null : reader.GetInt32(quotaOrdinal),
            reader.GetString("rds_session_mode"),
            reader.GetInt32("lifetime_days"),
            reader.GetString("status"));
    }

    private static IReadOnlyList<string> DeserializeGroups(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json)
                ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
