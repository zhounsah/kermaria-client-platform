using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbDownloadRepository : IDownloadRepository
{
    private readonly string _connectionString;

    public MariaDbDownloadRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task SeedDefaultCategoriesAsync(
        IReadOnlyList<ValidatedDownloadCategory> categories,
        CancellationToken cancellationToken)
    {
        if (categories.Count == 0)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        foreach (var category in categories)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT IGNORE INTO download_categories (
                    id,
                    slug,
                    title,
                    description,
                    status,
                    display_order,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @slug,
                    @title,
                    @description,
                    @status,
                    @display_order,
                    @created_at,
                    @updated_at
                );
                """;
            var now = DateTime.UtcNow;
            command.Parameters.AddWithValue("@id", category.Id);
            command.Parameters.AddWithValue("@slug", category.Slug);
            command.Parameters.AddWithValue("@title", category.Title);
            command.Parameters.AddWithValue(
                "@description",
                DbValue(category.Description));
            command.Parameters.AddWithValue("@status", category.Status);
            command.Parameters.AddWithValue(
                "@display_order",
                category.DisplayOrder);
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredDownloadCategory>> GetCategoriesAsync(
        CancellationToken cancellationToken)
    {
        var categories = new List<StoredDownloadCategory>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                slug,
                title,
                description,
                status,
                display_order,
                created_at,
                updated_at
            FROM download_categories
            ORDER BY display_order, title, id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(ReadCategory(reader));
        }

        return categories;
    }

    public async Task<IReadOnlyList<StoredDownloadResource>> GetResourcesAsync(
        CancellationToken cancellationToken)
    {
        var resources = new List<StoredDownloadResource>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                category_id,
                title,
                short_description,
                resource_type,
                source_kind,
                visibility_mode,
                status,
                external_url,
                version_label,
                installation_instructions,
                display_order,
                internal_file_storage_key,
                internal_file_original_name,
                internal_file_content_type,
                internal_file_size_bytes,
                internal_file_extension,
                created_at,
                updated_at
            FROM download_resources
            ORDER BY display_order, title, id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            resources.Add(ReadResource(reader));
        }

        return resources;
    }

    public async Task<IReadOnlyList<StoredDownloadVisibilityRule>> GetVisibilityRulesAsync(
        CancellationToken cancellationToken)
    {
        var rules = new List<StoredDownloadVisibilityRule>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                resource_id,
                target_type,
                target_value
            FROM download_resource_visibility_rules
            ORDER BY resource_id, target_type, target_value, id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(new StoredDownloadVisibilityRule(
                ReadIdentifier(reader, "id"),
                ReadIdentifier(reader, "resource_id"),
                reader.GetString("target_type"),
                reader.GetString("target_value")));
        }

        return rules;
    }

    public async Task<DownloadCategoryMutationResponse> CreateCategoryAsync(
        ValidatedDownloadCategory category,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO download_categories (
                id,
                slug,
                title,
                description,
                status,
                display_order,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @slug,
                @title,
                @description,
                @status,
                @display_order,
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue("@id", category.Id);
        command.Parameters.AddWithValue("@slug", category.Slug);
        command.Parameters.AddWithValue("@title", category.Title);
        command.Parameters.AddWithValue("@description", DbValue(category.Description));
        command.Parameters.AddWithValue("@status", category.Status);
        command.Parameters.AddWithValue("@display_order", category.DisplayOrder);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new DownloadCategoryMutationResponse(
            category.Id,
            Changed: true,
            now.ToString("O"),
            correlationId);
    }

    public async Task<DownloadCategoryMutationResponse> UpdateCategoryAsync(
        ValidatedDownloadCategory category,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        StoredDownloadCategory? current;
        await using (var readCommand = connection.CreateCommand())
        {
            readCommand.Transaction = transaction;
            readCommand.CommandText =
                """
                SELECT
                    id,
                    slug,
                    title,
                    description,
                    status,
                    display_order,
                    created_at,
                    updated_at
                FROM download_categories
                WHERE id = @id
                LIMIT 1;
                """;
            readCommand.Parameters.AddWithValue("@id", category.Id);
            await using var reader = await readCommand.ExecuteReaderAsync(
                cancellationToken);
            current = await reader.ReadAsync(cancellationToken)
                ? ReadCategory(reader)
                : null;
        }

        var now = DateTime.UtcNow;
        await using (var writeCommand = connection.CreateCommand())
        {
            writeCommand.Transaction = transaction;
            writeCommand.CommandText =
                """
                UPDATE download_categories
                SET
                    slug = @slug,
                    title = @title,
                    description = @description,
                    status = @status,
                    display_order = @display_order,
                    updated_at = @updated_at
                WHERE id = @id;
                """;
            writeCommand.Parameters.AddWithValue("@id", category.Id);
            writeCommand.Parameters.AddWithValue("@slug", category.Slug);
            writeCommand.Parameters.AddWithValue("@title", category.Title);
            writeCommand.Parameters.AddWithValue(
                "@description",
                DbValue(category.Description));
            writeCommand.Parameters.AddWithValue("@status", category.Status);
            writeCommand.Parameters.AddWithValue(
                "@display_order",
                category.DisplayOrder);
            writeCommand.Parameters.AddWithValue("@updated_at", now);
            await writeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var changed = current is null
            || current.Slug != category.Slug
            || current.Title != category.Title
            || current.Description != category.Description
            || current.Status != category.Status
            || current.DisplayOrder != category.DisplayOrder;

        return new DownloadCategoryMutationResponse(
            category.Id,
            changed,
            now.ToString("O"),
            correlationId);
    }

    public async Task<DownloadCategoryMutationResponse> DeleteCategoryAsync(
        string categoryId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM download_categories
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", categoryId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new DownloadCategoryMutationResponse(
            categoryId,
            Changed: true,
            DateTime.UtcNow.ToString("O"),
            correlationId);
    }

    public async Task<DownloadResourceMutationResponse> CreateResourceAsync(
        ValidatedDownloadResource resource,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        await InsertOrUpdateResourceAsync(
            connection,
            transaction,
            resource,
            createdAt: now,
            updatedAt: now,
            isCreate: true,
            cancellationToken);
        await ReplaceRulesAsync(
            connection,
            transaction,
            resource,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new DownloadResourceMutationResponse(
            resource.Id,
            Changed: true,
            now.ToString("O"),
            correlationId);
    }

    public async Task<DownloadResourceMutationResponse> UpdateResourceAsync(
        ValidatedDownloadResource resource,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        StoredDownloadResource? current;
        IReadOnlyList<StoredDownloadVisibilityRule> currentRules;
        await using (var readCommand = connection.CreateCommand())
        {
            readCommand.Transaction = transaction;
            readCommand.CommandText =
                """
                SELECT
                    id,
                    category_id,
                    title,
                    short_description,
                    resource_type,
                    source_kind,
                    visibility_mode,
                    status,
                    external_url,
                    version_label,
                    installation_instructions,
                    display_order,
                    internal_file_storage_key,
                    internal_file_original_name,
                    internal_file_content_type,
                    internal_file_size_bytes,
                    internal_file_extension,
                    created_at,
                    updated_at
                FROM download_resources
                WHERE id = @id
                LIMIT 1;
                """;
            readCommand.Parameters.AddWithValue("@id", resource.Id);
            await using var reader = await readCommand.ExecuteReaderAsync(
                cancellationToken);
            current = await reader.ReadAsync(cancellationToken)
                ? ReadResource(reader)
                : null;
        }

        currentRules = await ReadResourceRulesAsync(
            connection,
            transaction,
            resource.Id,
            cancellationToken);
        var now = DateTime.UtcNow;
        await InsertOrUpdateResourceAsync(
            connection,
            transaction,
            resource,
            createdAt: ParseCreatedAt(current?.CreatedAt, now),
            updatedAt: now,
            isCreate: false,
            cancellationToken);
        await ReplaceRulesAsync(
            connection,
            transaction,
            resource,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var changed = current is null
            || current.CategoryId != resource.CategoryId
            || current.Title != resource.Title
            || current.ShortDescription != resource.ShortDescription
            || current.ResourceType != resource.ResourceType
            || current.SourceKind != resource.SourceKind
            || current.VisibilityMode != resource.VisibilityMode
            || current.Status != resource.Status
            || current.ExternalUrl != resource.ExternalUrl
            || current.VersionLabel != resource.VersionLabel
            || current.InstallationInstructions != resource.InstallationInstructions
            || current.DisplayOrder != resource.DisplayOrder
            || !FileMetadataEquals(current.InternalFile, resource.InternalFile)
            || !RulesEquivalent(currentRules, resource.Rules);

        return new DownloadResourceMutationResponse(
            resource.Id,
            changed,
            now.ToString("O"),
            correlationId);
    }

    public async Task<DownloadResourceMutationResponse> DeleteResourceAsync(
        string resourceId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        await using (var deleteRules = connection.CreateCommand())
        {
            deleteRules.Transaction = transaction;
            deleteRules.CommandText =
                """
                DELETE FROM download_resource_visibility_rules
                WHERE resource_id = @resource_id;
                """;
            deleteRules.Parameters.AddWithValue("@resource_id", resourceId);
            await deleteRules.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteResource = connection.CreateCommand())
        {
            deleteResource.Transaction = transaction;
            deleteResource.CommandText =
                """
                DELETE FROM download_resources
                WHERE id = @id;
                """;
            deleteResource.Parameters.AddWithValue("@id", resourceId);
            await deleteResource.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new DownloadResourceMutationResponse(
            resourceId,
            Changed: true,
            DateTime.UtcNow.ToString("O"),
            correlationId);
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task InsertOrUpdateResourceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ValidatedDownloadResource resource,
        DateTime createdAt,
        DateTime updatedAt,
        bool isCreate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = isCreate
            ? """
              INSERT INTO download_resources (
                  id,
                  category_id,
                  title,
                  short_description,
                  resource_type,
                  source_kind,
                  visibility_mode,
                  status,
                  external_url,
                  version_label,
                  installation_instructions,
                  display_order,
                  internal_file_storage_key,
                  internal_file_original_name,
                  internal_file_content_type,
                  internal_file_size_bytes,
                  internal_file_extension,
                  created_at,
                  updated_at
              ) VALUES (
                  @id,
                  @category_id,
                  @title,
                  @short_description,
                  @resource_type,
                  @source_kind,
                  @visibility_mode,
                  @status,
                  @external_url,
                  @version_label,
                  @installation_instructions,
                  @display_order,
                  @internal_file_storage_key,
                  @internal_file_original_name,
                  @internal_file_content_type,
                  @internal_file_size_bytes,
                  @internal_file_extension,
                  @created_at,
                  @updated_at
              );
              """
            : """
              UPDATE download_resources
              SET
                  category_id = @category_id,
                  title = @title,
                  short_description = @short_description,
                  resource_type = @resource_type,
                  source_kind = @source_kind,
                  visibility_mode = @visibility_mode,
                  status = @status,
                  external_url = @external_url,
                  version_label = @version_label,
                  installation_instructions = @installation_instructions,
                  display_order = @display_order,
                  internal_file_storage_key = @internal_file_storage_key,
                  internal_file_original_name = @internal_file_original_name,
                  internal_file_content_type = @internal_file_content_type,
                  internal_file_size_bytes = @internal_file_size_bytes,
                  internal_file_extension = @internal_file_extension,
                  updated_at = @updated_at
              WHERE id = @id;
              """;

        command.Parameters.AddWithValue("@id", resource.Id);
        command.Parameters.AddWithValue("@category_id", resource.CategoryId);
        command.Parameters.AddWithValue("@title", resource.Title);
        command.Parameters.AddWithValue(
            "@short_description",
            resource.ShortDescription);
        command.Parameters.AddWithValue("@resource_type", resource.ResourceType);
        command.Parameters.AddWithValue("@source_kind", resource.SourceKind);
        command.Parameters.AddWithValue(
            "@visibility_mode",
            resource.VisibilityMode);
        command.Parameters.AddWithValue("@status", resource.Status);
        command.Parameters.AddWithValue("@external_url", DbValue(resource.ExternalUrl));
        command.Parameters.AddWithValue(
            "@version_label",
            DbValue(resource.VersionLabel));
        command.Parameters.AddWithValue(
            "@installation_instructions",
            DbValue(resource.InstallationInstructions));
        command.Parameters.AddWithValue("@display_order", resource.DisplayOrder);
        command.Parameters.AddWithValue(
            "@internal_file_storage_key",
            DbValue(resource.InternalFile?.StorageKey));
        command.Parameters.AddWithValue(
            "@internal_file_original_name",
            DbValue(resource.InternalFile?.OriginalName));
        command.Parameters.AddWithValue(
            "@internal_file_content_type",
            DbValue(resource.InternalFile?.ContentType));
        command.Parameters.AddWithValue(
            "@internal_file_size_bytes",
            DbValue(resource.InternalFile?.SizeBytes));
        command.Parameters.AddWithValue(
            "@internal_file_extension",
            DbValue(resource.InternalFile?.Extension));
        command.Parameters.AddWithValue("@created_at", createdAt);
        command.Parameters.AddWithValue("@updated_at", updatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReplaceRulesAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ValidatedDownloadResource resource,
        CancellationToken cancellationToken)
    {
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText =
                """
                DELETE FROM download_resource_visibility_rules
                WHERE resource_id = @resource_id;
                """;
            deleteCommand.Parameters.AddWithValue("@resource_id", resource.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var rule in resource.Rules)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            var now = DateTime.UtcNow;
            insertCommand.CommandText =
                """
                INSERT INTO download_resource_visibility_rules (
                    id,
                    resource_id,
                    target_type,
                    target_value,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @resource_id,
                    @target_type,
                    @target_value,
                    @created_at,
                    @updated_at
                );
                """;
            insertCommand.Parameters.AddWithValue("@id", rule.Id);
            insertCommand.Parameters.AddWithValue(
                "@resource_id",
                rule.ResourceId);
            insertCommand.Parameters.AddWithValue(
                "@target_type",
                rule.TargetType);
            insertCommand.Parameters.AddWithValue(
                "@target_value",
                rule.TargetValue);
            insertCommand.Parameters.AddWithValue("@created_at", now);
            insertCommand.Parameters.AddWithValue("@updated_at", now);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<StoredDownloadVisibilityRule>>
        ReadResourceRulesAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string resourceId,
            CancellationToken cancellationToken)
    {
        var rules = new List<StoredDownloadVisibilityRule>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                id,
                resource_id,
                target_type,
                target_value
            FROM download_resource_visibility_rules
            WHERE resource_id = @resource_id
            ORDER BY target_type, target_value, id;
            """;
        command.Parameters.AddWithValue("@resource_id", resourceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(new StoredDownloadVisibilityRule(
                ReadIdentifier(reader, "id"),
                ReadIdentifier(reader, "resource_id"),
                reader.GetString("target_type"),
                reader.GetString("target_value")));
        }

        return rules;
    }

    private static StoredDownloadCategory ReadCategory(MySqlDataReader reader)
        => new(
            ReadIdentifier(reader, "id"),
            reader.GetString("slug"),
            reader.GetString("title"),
            ReadNullableString(reader, "description"),
            reader.GetString("status"),
            reader.GetInt32("display_order"),
            ToUtcIso(reader.GetDateTime("created_at")),
            ToUtcIso(reader.GetDateTime("updated_at")));

    private static StoredDownloadResource ReadResource(MySqlDataReader reader)
        => new(
            ReadIdentifier(reader, "id"),
            ReadIdentifier(reader, "category_id"),
            reader.GetString("title"),
            reader.GetString("short_description"),
            reader.GetString("resource_type"),
            reader.GetString("source_kind"),
            reader.GetString("visibility_mode"),
            reader.GetString("status"),
            ReadNullableString(reader, "external_url"),
            ReadNullableString(reader, "version_label"),
            ReadNullableString(reader, "installation_instructions"),
            reader.GetInt32("display_order"),
            ReadFileMetadata(reader),
            ToUtcIso(reader.GetDateTime("created_at")),
            ToUtcIso(reader.GetDateTime("updated_at")));

    private static StoredDownloadFileMetadata? ReadFileMetadata(MySqlDataReader reader)
    {
        var storageKey = ReadNullableString(reader, "internal_file_storage_key");
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        return new StoredDownloadFileMetadata(
            storageKey,
            ReadNullableString(reader, "internal_file_original_name")
                ?? "telechargement.bin",
            ReadNullableString(reader, "internal_file_content_type")
                ?? "application/octet-stream",
            reader.IsDBNull(reader.GetOrdinal("internal_file_size_bytes"))
                ? 0
                : reader.GetInt64("internal_file_size_bytes"),
            ReadNullableString(reader, "internal_file_extension"));
    }

    private static bool RulesEquivalent(
        IReadOnlyList<StoredDownloadVisibilityRule> currentRules,
        IReadOnlyList<ValidatedDownloadVisibilityRule> nextRules)
    {
        var current = currentRules
            .Select(rule => $"{rule.TargetType}:{rule.TargetValue}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var next = nextRules
            .Select(rule => $"{rule.TargetType}:{rule.TargetValue}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return current.SequenceEqual(next, StringComparer.Ordinal);
    }

    private static bool FileMetadataEquals(
        StoredDownloadFileMetadata? left,
        StoredDownloadFileMetadata? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.StorageKey == right.StorageKey
            && left.OriginalName == right.OriginalName
            && left.ContentType == right.ContentType
            && left.SizeBytes == right.SizeBytes
            && left.Extension == right.Extension;
    }

    private static DateTime ParseCreatedAt(string? value, DateTime fallback)
        => value is { Length: > 0 }
            ? DateTime.Parse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal
                | System.Globalization.DateTimeStyles.AdjustToUniversal)
            : fallback;

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
