using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbPortalNotificationRepository
    : IPortalNotificationRepository
{
    private readonly string _connectionString;

    public MariaDbPortalNotificationRepository(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<PortalNotificationSummary>>
        GetNotificationsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
    {
        var notifications = new List<PortalNotificationSummary>();
        await using var connection = await OpenConnectionAsync(
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                notification_type,
                title,
                message,
                link_url,
                read_at,
                created_at
            FROM portal_notifications
            WHERE customer_id = @customer_id
            ORDER BY created_at DESC, id DESC
            LIMIT 100;
            """;
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var readAtOrdinal = reader.GetOrdinal("read_at");
            notifications.Add(new PortalNotificationSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("notification_type"),
                reader.GetString("title"),
                reader.GetString("message"),
                ReadNullableString(reader, "link_url"),
                !reader.IsDBNull(readAtOrdinal),
                reader.IsDBNull(readAtOrdinal)
                    ? null
                    : ToUtcIso(reader.GetDateTime(readAtOrdinal)),
                ToUtcIso(reader.GetDateTime("created_at"))));
        }

        return notifications;
    }

    public async Task<int> MarkAsReadAsync(
        PortalSessionContext session,
        string notificationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE portal_notifications
            SET read_at = @read_at
            WHERE id = @notification_id
              AND customer_id = @customer_id
              AND read_at IS NULL;
            """;
        command.Parameters.AddWithValue("@read_at", DateTime.UtcNow);
        command.Parameters.AddWithValue(
            "@notification_id",
            notificationId);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);
        var updated = await command.ExecuteNonQueryAsync(cancellationToken);
        if (updated > 0)
        {
            return updated;
        }

        await using var lookup = connection.CreateCommand();
        lookup.CommandText =
            """
            SELECT COUNT(*)
            FROM portal_notifications
            WHERE id = @notification_id
              AND customer_id = @customer_id;
            """;
        lookup.Parameters.AddWithValue(
            "@notification_id",
            notificationId);
        lookup.Parameters.AddWithValue("@customer_id", session.CustomerId);
        var exists = Convert.ToInt32(
            await lookup.ExecuteScalarAsync(cancellationToken)) > 0;
        return exists ? 0 : throw new PortalDataNotFoundException();
    }

    public async Task<int> MarkAllAsReadAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE portal_notifications
            SET read_at = @read_at
            WHERE customer_id = @customer_id
              AND read_at IS NULL;
            """;
        command.Parameters.AddWithValue("@read_at", DateTime.UtcNow);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}
