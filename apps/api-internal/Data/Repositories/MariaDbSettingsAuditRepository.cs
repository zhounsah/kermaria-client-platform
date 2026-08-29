using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Lecture filtree du journal d'audit, restreinte aux actions du Centre de
/// configuration.
///
/// La restriction est faite en SQL et non apres coup : le journal general
/// contient toute l'activite du portail, et une limite appliquee avant le
/// filtre ramenerait surtout des evenements sans rapport, en laissant croire
/// que la configuration n'a pas ete touchee.
/// </summary>
public sealed class MariaDbSettingsAuditRepository : ISettingsAuditRepository
{
    private const int MaximumLimit = 200;
    private readonly string _connectionString;

    public MariaDbSettingsAuditRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<SettingsAuditEntry>> SearchAsync(
        SettingsAuditQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Actions.Count == 0)
        {
            return [];
        }

        var entries = new List<SettingsAuditEntry>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var actionParameters = new List<string>(query.Actions.Count);
        for (var index = 0; index < query.Actions.Count; index++)
        {
            var name = $"@action{index}";
            actionParameters.Add(name);
            command.Parameters.AddWithValue(name, query.Actions[index]);
        }

        var filters = new List<string>
        {
            $"a.action IN ({string.Join(", ", actionParameters)})"
        };

        if (query.FromUtc is not null)
        {
            filters.Add("a.occurred_at >= @from_utc");
            command.Parameters.AddWithValue("@from_utc", query.FromUtc.Value);
        }

        if (query.ToUtc is not null)
        {
            filters.Add("a.occurred_at <= @to_utc");
            command.Parameters.AddWithValue("@to_utc", query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            filters.Add("a.outcome = @outcome");
            command.Parameters.AddWithValue("@outcome", query.Outcome);
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            filters.Add("a.correlation_id = @correlation_id");
            command.Parameters.AddWithValue(
                "@correlation_id",
                query.CorrelationId);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetReference))
        {
            filters.Add("a.target_reference LIKE @target_reference");
            command.Parameters.AddWithValue(
                "@target_reference",
                $"%{Escape(query.TargetReference!)}%");
        }

        if (!string.IsNullOrWhiteSpace(query.Actor))
        {
            filters.Add(
                "(u.display_name LIKE @actor OR a.actor_service LIKE @actor)");
            command.Parameters.AddWithValue(
                "@actor",
                $"%{Escape(query.Actor!)}%");
        }

        command.CommandText =
            $"""
            SELECT
                a.occurred_at,
                COALESCE(u.display_name, a.actor_service) AS actor_name,
                a.action,
                a.outcome,
                a.reason_code,
                a.target_type,
                a.target_reference,
                a.correlation_id,
                a.source_address
            FROM audit_logs a
            LEFT JOIN portal_users u ON u.id = a.actor_user_id
            WHERE {string.Join(" AND ", filters)}
            ORDER BY a.occurred_at DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue(
            "@limit",
            Math.Clamp(query.Limit, 1, MaximumLimit));

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new SettingsAuditEntry(
                ToUtcIso(reader.GetDateTime("occurred_at")),
                reader.GetString("actor_name"),
                reader.GetString("action"),
                reader.GetString("outcome"),
                ReadNullableString(reader, "reason_code"),
                ReadNullableString(reader, "target_type"),
                ReadNullableString(reader, "target_reference"),
                reader.GetString("correlation_id"),
                MariaDbAddressMask.Apply(
                    ReadNullableString(reader, "source_address"))));
        }

        return entries;
    }

    /// <summary>
    /// Neutralise les jokers d'un LIKE. Sans cela, un acteur saisi comme
    /// « % » ramenerait tout le journal filtre, ce qui n'est pas la recherche
    /// demandee.
    /// </summary>
    private static string Escape(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}
