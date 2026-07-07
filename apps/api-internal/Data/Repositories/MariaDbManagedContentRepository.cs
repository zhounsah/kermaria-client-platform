using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbManagedContentRepository : IManagedContentRepository
{
    private readonly string _connectionString;

    public MariaDbManagedContentRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<StoredManagedContentEntry>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var entries = new List<StoredManagedContentEntry>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                content_key,
                content_type,
                title,
                public_path,
                body_markdown,
                version_label,
                created_at,
                updated_at
            FROM managed_content_entries
            ORDER BY content_key;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    public async Task<StoredManagedContentEntry?> GetAsync(
        string key,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                content_key,
                content_type,
                title,
                public_path,
                body_markdown,
                version_label,
                created_at,
                updated_at
            FROM managed_content_entries
            WHERE content_key = @content_key
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@content_key", key);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadEntry(reader)
            : null;
    }

    public async Task SeedMissingAsync(
        IReadOnlyList<ValidatedManagedContentEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        foreach (var entry in entries)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT IGNORE INTO managed_content_entries (
                    content_key,
                    content_type,
                    title,
                    public_path,
                    body_markdown,
                    version_label,
                    created_at,
                    updated_at
                ) VALUES (
                    @content_key,
                    @content_type,
                    @title,
                    @public_path,
                    @body_markdown,
                    @version_label,
                    @created_at,
                    @updated_at
                );
                """;
            var now = DateTime.UtcNow;
            command.Parameters.AddWithValue("@content_key", entry.Key);
            command.Parameters.AddWithValue("@content_type", entry.ContentType);
            command.Parameters.AddWithValue("@title", entry.Title);
            command.Parameters.AddWithValue("@public_path", entry.PublicPath);
            command.Parameters.AddWithValue("@body_markdown", entry.BodyMarkdown);
            command.Parameters.AddWithValue(
                "@version_label",
                DbValue(entry.VersionLabel));
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ManagedContentMutationResponse> UpsertAsync(
        ValidatedManagedContentEntry entry,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        StoredManagedContentEntry? current = null;
        await using (var readCommand = connection.CreateCommand())
        {
            readCommand.Transaction = transaction;
            readCommand.CommandText =
                """
                SELECT
                    content_key,
                    content_type,
                    title,
                    public_path,
                    body_markdown,
                    version_label,
                    created_at,
                    updated_at
                FROM managed_content_entries
                WHERE content_key = @content_key
                LIMIT 1;
                """;
            readCommand.Parameters.AddWithValue("@content_key", entry.Key);

            await using var reader = await readCommand.ExecuteReaderAsync(
                cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                current = ReadEntry(reader);
            }
        }

        var now = DateTime.UtcNow;
        var createdAt = current?.CreatedAt is { Length: > 0 }
            ? DateTime.Parse(
                current.CreatedAt,
                provider: null,
                System.Globalization.DateTimeStyles.RoundtripKind)
            : now;
        await using (var writeCommand = connection.CreateCommand())
        {
            writeCommand.Transaction = transaction;
            writeCommand.CommandText =
                """
                INSERT INTO managed_content_entries (
                    content_key,
                    content_type,
                    title,
                    public_path,
                    body_markdown,
                    version_label,
                    created_at,
                    updated_at
                ) VALUES (
                    @content_key,
                    @content_type,
                    @title,
                    @public_path,
                    @body_markdown,
                    @version_label,
                    @created_at,
                    @updated_at
                )
                ON DUPLICATE KEY UPDATE
                    content_type = VALUES(content_type),
                    title = VALUES(title),
                    public_path = VALUES(public_path),
                    body_markdown = VALUES(body_markdown),
                    version_label = VALUES(version_label),
                    updated_at = VALUES(updated_at);
                """;
            writeCommand.Parameters.AddWithValue("@content_key", entry.Key);
            writeCommand.Parameters.AddWithValue("@content_type", entry.ContentType);
            writeCommand.Parameters.AddWithValue("@title", entry.Title);
            writeCommand.Parameters.AddWithValue("@public_path", entry.PublicPath);
            writeCommand.Parameters.AddWithValue(
                "@body_markdown",
                entry.BodyMarkdown);
            writeCommand.Parameters.AddWithValue(
                "@version_label",
                DbValue(entry.VersionLabel));
            writeCommand.Parameters.AddWithValue("@created_at", createdAt);
            writeCommand.Parameters.AddWithValue("@updated_at", now);
            await writeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var changed = current is null
            || current.ContentType != entry.ContentType
            || current.Title != entry.Title
            || current.PublicPath != entry.PublicPath
            || current.BodyMarkdown != entry.BodyMarkdown
            || current.VersionLabel != entry.VersionLabel;

        return new ManagedContentMutationResponse(
            entry.Key,
            changed,
            now.ToString("O"),
            correlationId);
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static StoredManagedContentEntry ReadEntry(MySqlDataReader reader)
        => new(
            reader.GetString("content_key"),
            reader.GetString("content_type"),
            reader.GetString("title"),
            reader.GetString("public_path"),
            reader.GetString("body_markdown"),
            ReadNullableString(reader, "version_label"),
            reader.GetDateTime("created_at").ToString("O"),
            reader.GetDateTime("updated_at").ToString("O"));

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);
}
