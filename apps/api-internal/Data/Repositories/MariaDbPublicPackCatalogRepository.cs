using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbPublicPackCatalogRepository
    : IPublicPackCatalogRepository
{
    private const string ContentKey = "public-pack-catalog";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _connectionString;

    public MariaDbPublicPackCatalogRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<PublicPackCatalogContent?> GetAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT content_json, updated_at
            FROM public_pack_catalog_content
            WHERE content_key = @content_key
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@content_key", ContentKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var payload = DeserializePayload(reader.GetString("content_json"));
        if (payload is null)
        {
            return null;
        }

        return new PublicPackCatalogContent(
            payload.PageEyebrow!,
            payload.PageTitle!,
            payload.PageDescription!,
            payload.ComparisonColumnLabel!,
            payload.FootnotePrimary!,
            payload.FootnoteSecondary!,
            payload.Packs ?? [],
            payload.ComparisonRows ?? [],
            ReadDateTime(reader, "updated_at"));
    }

    public async Task<PublicPackCatalogMutationResponse> UpsertAsync(
        ValidatedPublicPackCatalogContent content,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var payload = new PublicPackCatalogContentPayload(
            content.PageEyebrow,
            content.PageTitle,
            content.PageDescription,
            content.ComparisonColumnLabel,
            content.FootnotePrimary,
            content.FootnoteSecondary,
            content.Packs,
            content.ComparisonRows);
        var serialized = JsonSerializer.Serialize(payload, JsonOptions);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        string? currentSerialized = null;
        await using (var readCommand = connection.CreateCommand())
        {
            readCommand.Transaction = transaction;
            readCommand.CommandText =
                """
                SELECT content_json
                FROM public_pack_catalog_content
                WHERE content_key = @content_key
                LIMIT 1;
                """;
            readCommand.Parameters.AddWithValue("@content_key", ContentKey);

            await using var reader = await readCommand.ExecuteReaderAsync(
                cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                currentSerialized = reader.GetString("content_json");
            }
        }

        var now = DateTime.UtcNow;
        await using (var writeCommand = connection.CreateCommand())
        {
            writeCommand.Transaction = transaction;
            writeCommand.CommandText =
                """
                INSERT INTO public_pack_catalog_content (
                    content_key,
                    content_json,
                    created_at,
                    updated_at
                ) VALUES (
                    @content_key,
                    @content_json,
                    @created_at,
                    @updated_at
                )
                ON DUPLICATE KEY UPDATE
                    content_json = VALUES(content_json),
                    updated_at = VALUES(updated_at);
                """;
            writeCommand.Parameters.AddWithValue("@content_key", ContentKey);
            writeCommand.Parameters.AddWithValue("@content_json", serialized);
            writeCommand.Parameters.AddWithValue("@created_at", now);
            writeCommand.Parameters.AddWithValue("@updated_at", now);
            await writeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new PublicPackCatalogMutationResponse(
            !string.Equals(currentSerialized, serialized, StringComparison.Ordinal),
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

    private static PublicPackCatalogContentPayload? DeserializePayload(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<PublicPackCatalogContentPayload>(
                value,
                JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadDateTime(MySqlDataReader reader, string columnName)
        => reader.GetDateTime(columnName).ToString("O");
}
