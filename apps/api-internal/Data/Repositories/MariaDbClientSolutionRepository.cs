using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbClientSolutionRepository : IClientSolutionRepository
{
    private const string SettingsKey = "default";

    private const string SolutionColumns =
        """
        id,
        slug,
        title,
        tagline,
        target_url,
        opens_in_new_tab,
        status,
        display_order,
        logo_content_type,
        logo_original_name,
        logo_size_bytes,
        logo_updated_at,
        created_at,
        updated_at
        """;

    private readonly string _connectionString;

    public MariaDbClientSolutionRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<StoredClientSolutionPortalSettings?> GetSettingsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                eyebrow,
                title,
                description,
                footer_note,
                updated_at
            FROM client_solution_portal_settings
            WHERE settings_key = @settings_key
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@settings_key", SettingsKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StoredClientSolutionPortalSettings(
                ReadNullableString(reader, "eyebrow"),
                reader.GetString("title"),
                ReadNullableString(reader, "description"),
                ReadNullableString(reader, "footer_note"),
                ToUtcIso(reader.GetDateTime("updated_at")))
            : null;
    }

    public async Task<ClientSolutionPortalMutationResponse> UpsertSettingsAsync(
        ValidatedClientSolutionPortalSettings settings,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var current = await GetSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO client_solution_portal_settings (
                settings_key,
                eyebrow,
                title,
                description,
                footer_note,
                created_at,
                updated_at
            ) VALUES (
                @settings_key,
                @eyebrow,
                @title,
                @description,
                @footer_note,
                @now,
                @now
            )
            ON DUPLICATE KEY UPDATE
                eyebrow = VALUES(eyebrow),
                title = VALUES(title),
                description = VALUES(description),
                footer_note = VALUES(footer_note),
                updated_at = VALUES(updated_at);
            """;
        command.Parameters.AddWithValue("@settings_key", SettingsKey);
        command.Parameters.AddWithValue("@eyebrow", DbValue(settings.Eyebrow));
        command.Parameters.AddWithValue("@title", settings.Title);
        command.Parameters.AddWithValue(
            "@description",
            DbValue(settings.Description));
        command.Parameters.AddWithValue(
            "@footer_note",
            DbValue(settings.FooterNote));
        command.Parameters.AddWithValue("@now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var changed = current is null
            || current.Eyebrow != settings.Eyebrow
            || current.Title != settings.Title
            || current.Description != settings.Description
            || current.FooterNote != settings.FooterNote;

        return new ClientSolutionPortalMutationResponse(
            changed,
            now.ToString("O"),
            correlationId);
    }

    public async Task<IReadOnlyList<StoredClientSolution>> GetSolutionsAsync(
        CancellationToken cancellationToken)
    {
        var solutions = new List<StoredClientSolution>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                {SolutionColumns}
            FROM client_solutions
            ORDER BY display_order, title, id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            solutions.Add(ReadSolution(reader));
        }

        return solutions;
    }

    public async Task<StoredClientSolution?> GetSolutionAsync(
        string id,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                {SolutionColumns}
            FROM client_solutions
            WHERE id = @id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadSolution(reader)
            : null;
    }

    public async Task<bool> SlugExistsAsync(
        string slug,
        string? excludedId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM client_solutions
            WHERE slug = @slug
              AND (@excluded_id IS NULL OR id <> @excluded_id);
            """;
        command.Parameters.AddWithValue("@slug", slug);
        command.Parameters.AddWithValue("@excluded_id", DbValue(excludedId));

        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(
            count,
            System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    public async Task<ClientSolutionMutationResponse> CreateSolutionAsync(
        ValidatedClientSolution solution,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO client_solutions (
                id,
                slug,
                title,
                tagline,
                target_url,
                opens_in_new_tab,
                status,
                display_order,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @slug,
                @title,
                @tagline,
                @target_url,
                @opens_in_new_tab,
                @status,
                @display_order,
                @now,
                @now
            );
            """;
        AddSolutionParameters(command, solution);
        command.Parameters.AddWithValue("@now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new ClientSolutionMutationResponse(
            solution.Id,
            Changed: true,
            now.ToString("O"),
            correlationId);
    }

    public async Task<ClientSolutionMutationResponse> UpdateSolutionAsync(
        ValidatedClientSolution solution,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var current = await GetSolutionAsync(solution.Id, cancellationToken);
        var now = DateTime.UtcNow;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE client_solutions
            SET
                slug = @slug,
                title = @title,
                tagline = @tagline,
                target_url = @target_url,
                opens_in_new_tab = @opens_in_new_tab,
                status = @status,
                display_order = @display_order,
                updated_at = @now
            WHERE id = @id;
            """;
        AddSolutionParameters(command, solution);
        command.Parameters.AddWithValue("@now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var changed = current is null
            || current.Slug != solution.Slug
            || current.Title != solution.Title
            || current.Tagline != solution.Tagline
            || current.TargetUrl != solution.TargetUrl
            || current.OpensInNewTab != solution.OpensInNewTab
            || current.Status != solution.Status
            || current.DisplayOrder != solution.DisplayOrder;

        return new ClientSolutionMutationResponse(
            solution.Id,
            changed,
            now.ToString("O"),
            correlationId);
    }

    public async Task<ClientSolutionMutationResponse> DeleteSolutionAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM client_solutions
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", id);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);

        return new ClientSolutionMutationResponse(
            id,
            affected > 0,
            DateTime.UtcNow.ToString("O"),
            correlationId);
    }

    public async Task<StoredClientSolutionLogoContent?> GetLogoAsync(
        string id,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                logo_bytes,
                logo_content_type,
                logo_original_name,
                logo_updated_at
            FROM client_solutions
            WHERE id = @id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)
            || reader.IsDBNull(reader.GetOrdinal("logo_bytes")))
        {
            return null;
        }

        return new StoredClientSolutionLogoContent(
            (byte[])reader.GetValue(reader.GetOrdinal("logo_bytes")),
            ReadNullableString(reader, "logo_content_type")
                ?? "application/octet-stream",
            ReadNullableString(reader, "logo_original_name") ?? "logo",
            reader.IsDBNull(reader.GetOrdinal("logo_updated_at"))
                ? DateTime.UnixEpoch.ToString("O")
                : ToUtcIso(reader.GetDateTime("logo_updated_at")));
    }

    public async Task<ClientSolutionMutationResponse> SaveLogoAsync(
        string id,
        StoredClientSolutionLogoContent logo,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE client_solutions
            SET
                logo_bytes = @logo_bytes,
                logo_content_type = @logo_content_type,
                logo_original_name = @logo_original_name,
                logo_size_bytes = @logo_size_bytes,
                logo_updated_at = @now,
                updated_at = @now
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@logo_bytes", logo.Bytes);
        command.Parameters.AddWithValue("@logo_content_type", logo.ContentType);
        command.Parameters.AddWithValue("@logo_original_name", logo.OriginalName);
        command.Parameters.AddWithValue("@logo_size_bytes", logo.Bytes.Length);
        command.Parameters.AddWithValue("@now", now);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);

        return new ClientSolutionMutationResponse(
            id,
            affected > 0,
            now.ToString("O"),
            correlationId);
    }

    public async Task<ClientSolutionMutationResponse> DeleteLogoAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE client_solutions
            SET
                logo_bytes = NULL,
                logo_content_type = NULL,
                logo_original_name = NULL,
                logo_size_bytes = NULL,
                logo_updated_at = NULL,
                updated_at = @now
            WHERE id = @id
              AND logo_bytes IS NOT NULL;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@now", now);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);

        return new ClientSolutionMutationResponse(
            id,
            affected > 0,
            now.ToString("O"),
            correlationId);
    }

    private static void AddSolutionParameters(
        MySqlCommand command,
        ValidatedClientSolution solution)
    {
        command.Parameters.AddWithValue("@id", solution.Id);
        command.Parameters.AddWithValue("@slug", solution.Slug);
        command.Parameters.AddWithValue("@title", solution.Title);
        command.Parameters.AddWithValue("@tagline", DbValue(solution.Tagline));
        command.Parameters.AddWithValue("@target_url", solution.TargetUrl);
        command.Parameters.AddWithValue(
            "@opens_in_new_tab",
            solution.OpensInNewTab ? 1 : 0);
        command.Parameters.AddWithValue("@status", solution.Status);
        command.Parameters.AddWithValue("@display_order", solution.DisplayOrder);
    }

    private static StoredClientSolution ReadSolution(MySqlDataReader reader)
        => new(
            ReadIdentifier(reader, "id"),
            reader.GetString("slug"),
            reader.GetString("title"),
            ReadNullableString(reader, "tagline"),
            reader.GetString("target_url"),
            reader.GetBoolean("opens_in_new_tab"),
            reader.GetString("status"),
            reader.GetInt32("display_order"),
            ReadLogoMetadata(reader),
            ToUtcIso(reader.GetDateTime("created_at")),
            ToUtcIso(reader.GetDateTime("updated_at")));

    private static StoredClientSolutionLogo? ReadLogoMetadata(
        MySqlDataReader reader)
    {
        var contentType = ReadNullableString(reader, "logo_content_type");
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        return new StoredClientSolutionLogo(
            contentType,
            ReadNullableString(reader, "logo_original_name") ?? "logo",
            reader.IsDBNull(reader.GetOrdinal("logo_size_bytes"))
                ? 0
                : reader.GetInt32("logo_size_bytes"),
            reader.IsDBNull(reader.GetOrdinal("logo_updated_at"))
                ? DateTime.UnixEpoch.ToString("O")
                : ToUtcIso(reader.GetDateTime("logo_updated_at")));
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

    private static object DbValue(object? value)
        => value ?? DBNull.Value;

    private static string ReadIdentifier(
        MySqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            throw new InvalidOperationException(
                $"The identifier column '{columnName}' cannot be null.");
        }

        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            Guid guid => guid.ToString("D"),
            byte[] bytes when bytes.Length == 16 => new Guid(bytes).ToString("D"),
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            _ => Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture)
                ?? throw new InvalidOperationException(
                    $"The identifier column '{columnName}' cannot be converted to string.")
        };
    }

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);
}
