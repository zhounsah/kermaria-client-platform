using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbDirectoryAuditRepository : IDirectoryAuditRepository
{
    private const int MaximumLimit = 100;
    private readonly string _connectionString;

    public MariaDbDirectoryAuditRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<DirectoryWriteEntry>> GetRecentWritesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var entries = new List<DirectoryWriteEntry>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                a.requested_at,
                a.completed_at,
                a.action_type,
                a.target_reference,
                a.status,
                a.result_code,
                a.changed,
                a.correlation_id,
                a.subscription_id,
                u.display_name AS actor_name,
                c.external_reference AS customer_reference
            FROM ad_actions a
            LEFT JOIN portal_users u ON u.id = a.requested_by_user_id
            LEFT JOIN customers c ON c.id = a.customer_id
            ORDER BY a.requested_at DESC, a.id DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue(
            "@limit",
            Math.Clamp(limit, 1, MaximumLimit));

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            // L'horodatage retenu est celui de l'achevement quand il existe :
            // c'est le moment ou l'annuaire a reellement change. La demande
            // seule ne prouve rien.
            var occurredAt = ReadNullableUtc(reader, "completed_at")
                ?? reader.GetDateTime("requested_at");

            entries.Add(new DirectoryWriteEntry(
                ToUtcIso(occurredAt),
                reader.GetString("action_type"),
                "api_internal",
                ReadNullableString(reader, "actor_name"),
                reader.IsDBNull(reader.GetOrdinal("subscription_id"))
                    ? "Administration directe"
                    : "Provisioning d'abonnement",
                ReadNullableString(reader, "customer_reference"),
                reader.GetString("target_reference"),
                reader.GetString("status"),
                ReadNullableString(reader, "result_code"),
                reader.IsDBNull(reader.GetOrdinal("changed"))
                    ? null
                    : reader.GetBoolean("changed"),
                reader.GetString("correlation_id")));
        }

        return entries;
    }

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);

    private static DateTime? ReadNullableUtc(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetDateTime(columnName);

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}
