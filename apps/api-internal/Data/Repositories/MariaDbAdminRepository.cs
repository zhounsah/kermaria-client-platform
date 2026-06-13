using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbAdminRepository : IAdminRepository
{
    private const int DefaultLimit = 100;
    private readonly string _connectionString;

    public MariaDbAdminRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<AdminOverview> GetOverviewAsync(
        string adMode,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                (SELECT COUNT(*) FROM customers) AS customer_count,
                (
                    SELECT COUNT(*)
                    FROM portal_users
                    WHERE status = 'active'
                ) AS active_user_count,
                (
                    SELECT COUNT(*)
                    FROM portal_sessions
                    WHERE revoked_at IS NULL
                      AND expires_at > UTC_TIMESTAMP(6)
                ) AS active_session_count,
                (
                    SELECT COUNT(*)
                    FROM support_requests
                    WHERE status <> 'closed'
                ) AS open_support_count,
                (
                    SELECT COUNT(*)
                    FROM service_requests
                    WHERE created_at >= UTC_TIMESTAMP(6) - INTERVAL 30 DAY
                ) AS recent_service_request_count;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Admin overview is unavailable.");
        }

        var customerCount = reader.GetInt32("customer_count");
        var activeUserCount = reader.GetInt32("active_user_count");
        var activeSessionCount = reader.GetInt32("active_session_count");
        var openSupportCount = reader.GetInt32("open_support_count");
        var recentServiceRequestCount =
            reader.GetInt32("recent_service_request_count");
        await reader.DisposeAsync();

        return new AdminOverview(
            customerCount,
            activeUserCount,
            activeSessionCount,
            openSupportCount,
            recentServiceRequestCount,
            await GetAuditLogsAsync(10, cancellationToken),
            adMode,
            false);
    }

    public async Task<IReadOnlyList<AdminCustomerSummary>> GetCustomersAsync(
        CancellationToken cancellationToken)
    {
        var customers = new List<AdminCustomerSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                c.external_reference,
                c.display_name,
                c.status,
                (
                    SELECT COUNT(*)
                    FROM customer_services s
                    WHERE s.customer_id = c.id
                ) AS service_count,
                (
                    SELECT COUNT(*)
                    FROM support_requests sr
                    WHERE sr.customer_id = c.id
                      AND sr.status <> 'closed'
                ) AS open_support_count,
                c.created_at,
                GREATEST(
                    c.updated_at,
                    COALESCE((
                        SELECT MAX(sr.updated_at)
                        FROM support_requests sr
                        WHERE sr.customer_id = c.id
                    ), c.updated_at),
                    COALESCE((
                        SELECT MAX(r.created_at)
                        FROM service_requests r
                        WHERE r.customer_id = c.id
                    ), c.updated_at)
                ) AS last_activity_at
            FROM customers c
            ORDER BY c.display_name
            LIMIT 100;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(new AdminCustomerSummary(
                reader.GetString("external_reference"),
                reader.GetString("display_name"),
                reader.GetString("status"),
                reader.GetInt32("service_count"),
                reader.GetInt32("open_support_count"),
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("last_activity_at"))));
        }

        return customers;
    }

    public async Task<IReadOnlyList<AdminSupportRequestSummary>>
        GetSupportRequestsAsync(CancellationToken cancellationToken)
    {
        var requests = new List<AdminSupportRequestSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                sr.reference,
                c.external_reference AS customer_reference,
                c.display_name AS customer_name,
                COALESCE(s.name, 'Compte client') AS service_name,
                sr.priority,
                sr.status,
                sr.subject,
                sr.created_at,
                sr.updated_at
            FROM support_requests sr
            INNER JOIN customers c ON c.id = sr.customer_id
            LEFT JOIN customer_services s
                ON s.id = sr.service_id
                AND s.customer_id = sr.customer_id
            ORDER BY sr.created_at DESC
            LIMIT 100;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            requests.Add(new AdminSupportRequestSummary(
                reader.GetString("reference"),
                reader.GetString("customer_reference"),
                reader.GetString("customer_name"),
                reader.GetString("service_name"),
                reader.GetString("priority"),
                reader.GetString("status"),
                reader.GetString("subject"),
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("updated_at"))));
        }

        return requests;
    }

    public async Task<IReadOnlyList<AdminServiceRequestSummary>>
        GetServiceRequestsAsync(CancellationToken cancellationToken)
    {
        var requests = new List<AdminServiceRequestSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                r.reference,
                c.external_reference AS customer_reference,
                c.display_name AS customer_name,
                catalog.name AS catalog_item_name,
                r.context,
                r.status,
                r.created_at
            FROM service_requests r
            INNER JOIN customers c ON c.id = r.customer_id
            INNER JOIN service_catalog catalog
                ON catalog.id = r.catalog_item_id
            ORDER BY r.created_at DESC
            LIMIT 100;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var context = reader.GetString("context");
            requests.Add(new AdminServiceRequestSummary(
                reader.GetString("reference"),
                reader.GetString("customer_reference"),
                reader.GetString("customer_name"),
                reader.GetString("catalog_item_name"),
                ExtractSubject(context),
                Truncate(ExtractDescription(context), 240) ?? string.Empty,
                reader.GetString("status"),
                true,
                ToUtcIso(reader.GetDateTime("created_at"))));
        }

        return requests;
    }

    public async Task<IReadOnlyList<AdminSessionSummary>> GetSessionsAsync(
        CancellationToken cancellationToken)
    {
        var sessions = new List<AdminSessionSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                u.display_name,
                u.email,
                u.role,
                c.external_reference AS customer_reference,
                s.created_at,
                s.expires_at,
                s.revoked_at,
                s.last_seen_at,
                s.ip_address,
                s.user_agent
            FROM portal_sessions s
            INNER JOIN portal_users u ON u.id = s.user_id
            LEFT JOIN customers c ON c.id = u.customer_id
            ORDER BY s.created_at DESC
            LIMIT 100;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        var now = DateTime.UtcNow;
        while (await reader.ReadAsync(cancellationToken))
        {
            var expiresAt = AsUtc(reader.GetDateTime("expires_at"));
            var revokedAt = ReadNullableUtc(reader, "revoked_at");
            var role = reader.GetString("role");
            sessions.Add(new AdminSessionSummary(
                reader.GetString("display_name"),
                reader.GetString("email"),
                role,
                role == PortalRoles.ClientUser
                    ? reader.GetString("customer_reference")
                    : null,
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(expiresAt),
                ToUtcIso(ReadNullableUtc(reader, "last_seen_at")),
                MaskAddress(ReadNullableString(reader, "ip_address")),
                Truncate(ReadNullableString(reader, "user_agent"), 120),
                revokedAt is not null
                    ? "revoked"
                    : expiresAt <= now
                        ? "expired"
                        : "active"));
        }

        return sessions;
    }

    public async Task<IReadOnlyList<AdminAuditLogEntry>> GetAuditLogsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var audits = new List<AdminAuditLogEntry>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                a.occurred_at,
                COALESCE(u.display_name, a.actor_service) AS actor_name,
                a.action,
                a.outcome,
                a.reason_code,
                c.external_reference AS customer_reference,
                a.correlation_id,
                a.source_address
            FROM audit_logs a
            LEFT JOIN portal_users u ON u.id = a.actor_user_id
            LEFT JOIN customers c ON c.id = a.customer_id
            ORDER BY a.occurred_at DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue(
            "@limit",
            Math.Clamp(limit, 1, DefaultLimit));

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            audits.Add(new AdminAuditLogEntry(
                ToUtcIso(reader.GetDateTime("occurred_at")),
                reader.GetString("actor_name"),
                reader.GetString("action"),
                reader.GetString("outcome"),
                ReadNullableString(reader, "reason_code"),
                ReadNullableString(reader, "customer_reference"),
                reader.GetString("correlation_id"),
                MaskAddress(ReadNullableString(reader, "source_address"))));
        }

        return audits;
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string ExtractSubject(string context)
    {
        var firstLine = context
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .FirstOrDefault()
            ?? "Demande de service";
        return firstLine.StartsWith("Sujet :", StringComparison.OrdinalIgnoreCase)
            ? firstLine["Sujet :".Length..].Trim()
            : Truncate(firstLine.Trim(), 160) ?? "Demande de service";
    }

    private static string ExtractDescription(string context)
    {
        var separatorIndex = context.IndexOf(
            $"{Environment.NewLine}{Environment.NewLine}",
            StringComparison.Ordinal);
        if (separatorIndex >= 0)
        {
            return context[
                (separatorIndex + (Environment.NewLine.Length * 2))..].Trim();
        }

        var lines = context.Split(["\r\n", "\n"], StringSplitOptions.None);
        return string.Join(" ", lines.Skip(1)).Trim();
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
            : AsUtc(reader.GetDateTime(columnName));

    private static DateTime AsUtc(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string ToUtcIso(DateTime value)
        => AsUtc(value).ToString("O");

    private static string? ToUtcIso(DateTime? value)
        => value is null ? null : ToUtcIso(value.Value);

    private static string? Truncate(string? value, int maximumLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, maximumLength)];

    private static string? MaskAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (System.Net.IPAddress.TryParse(value, out var address))
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4)
            {
                bytes[3] = 0;
                return new System.Net.IPAddress(bytes).ToString();
            }

            for (var index = 8; index < bytes.Length; index++)
            {
                bytes[index] = 0;
            }

            return new System.Net.IPAddress(bytes).ToString();
        }

        return "masquée";
    }
}
