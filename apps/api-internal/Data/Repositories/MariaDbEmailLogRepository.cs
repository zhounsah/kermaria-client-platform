using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbEmailLogRepository : IEmailLogRepository
{
    private readonly SqlRuntimeConfiguration _configuration;

    public MariaDbEmailLogRepository(SqlRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsPersistent => true;

    public async Task<string> RecordAsync(
        string template,
        string recipient,
        string subject,
        string body,
        string status,
        string? errorMessage,
        string? relatedDocumentId,
        string correlationId,
        bool delivered,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("D");
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO email_messages
                (id, template, recipient, subject, body, status,
                 error_message, related_document_id, correlation_id,
                 created_at, sent_at)
            VALUES
                (@id, @template, @recipient, @subject, @body, @status,
                 @errorMessage, @relatedDocumentId, @correlationId,
                 NOW(6), @sentAt)
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("template", template);
        cmd.Parameters.AddWithValue("recipient", recipient);
        cmd.Parameters.AddWithValue("subject", Truncate(subject, 255));
        cmd.Parameters.AddWithValue("body", body);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue(
            "errorMessage",
            (object?)errorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "relatedDocumentId",
            (object?)relatedDocumentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("correlationId", correlationId);
        cmd.Parameters.AddWithValue(
            "sentAt",
            delivered ? DateTime.UtcNow : (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    public async Task<IReadOnlyList<EmailLogEntry>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var capped = Math.Clamp(limit, 1, 500);
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT
                id, template, recipient, subject, status,
                error_message, related_document_id, correlation_id,
                created_at, sent_at
            FROM email_messages
            ORDER BY created_at DESC
            LIMIT {capped}
            """;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var entries = new List<EmailLogEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new EmailLogEntry(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("template"),
                reader.GetString("recipient"),
                reader.GetString("subject"),
                reader.GetString("status"),
                reader.IsDBNull(reader.GetOrdinal("error_message"))
                    ? null
                    : reader.GetString("error_message"),
                reader.IsDBNull(reader.GetOrdinal("related_document_id"))
                    ? null
                    : MariaDbIdentifierReader.ReadRequired(reader, "related_document_id"),
                reader.GetString("correlation_id"),
                reader.GetDateTime("created_at")
                    .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                reader.IsDBNull(reader.GetOrdinal("sent_at"))
                    ? null
                    : reader.GetDateTime("sent_at")
                        .ToString("yyyy-MM-ddTHH:mm:ss.fffZ")));
        }
        return entries;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
