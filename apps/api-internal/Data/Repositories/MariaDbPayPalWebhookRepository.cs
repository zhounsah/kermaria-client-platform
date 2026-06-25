using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbPayPalWebhookRepository : IPayPalWebhookRepository
{
    private readonly string _connectionString;

    public MariaDbPayPalWebhookRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public async Task<PayPalWebhookEventRecord?> GetByEventIdAsync(
        string eventId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, event_id, event_type, resource_id, status
            FROM paypal_webhook_events
            WHERE event_id = @eventId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("eventId", eventId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PayPalWebhookEventRecord(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("event_id"),
            reader.GetString("event_type"),
            reader.IsDBNull(reader.GetOrdinal("resource_id"))
                ? null
                : reader.GetString("resource_id"),
            reader.GetString("status"));
    }

    public async Task<string> InsertReceivedAsync(
        string eventId,
        string eventType,
        string? resourceId,
        string rawPayload,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("D");
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO paypal_webhook_events (
                id,
                event_id,
                event_type,
                resource_id,
                received_at,
                processed_at,
                status,
                error_message,
                raw_payload
            ) VALUES (
                @id,
                @eventId,
                @eventType,
                @resourceId,
                UTC_TIMESTAMP(6),
                NULL,
                'received',
                NULL,
                @rawPayload
            );
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("eventId", eventId);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue(
            "resourceId",
            (object?)resourceId ?? DBNull.Value);
        command.Parameters.AddWithValue("rawPayload", rawPayload);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    public Task MarkProcessedAsync(
        string eventId,
        CancellationToken cancellationToken)
        => UpdateStatusAsync(eventId, "processed", null, cancellationToken);

    public Task MarkFailedAsync(
        string eventId,
        string errorMessage,
        CancellationToken cancellationToken)
        => UpdateStatusAsync(eventId, "failed", errorMessage, cancellationToken);

    public Task MarkIgnoredAsync(
        string eventId,
        CancellationToken cancellationToken)
        => UpdateStatusAsync(eventId, "ignored", null, cancellationToken);

    private async Task UpdateStatusAsync(
        string eventId,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE paypal_webhook_events
            SET status = @status,
                processed_at = UTC_TIMESTAMP(6),
                error_message = @errorMessage
            WHERE event_id = @eventId;
            """;
        command.Parameters.AddWithValue("eventId", eventId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue(
            "errorMessage",
            (object?)errorMessage ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<MySqlConnection> OpenAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
