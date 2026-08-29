using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbEditorialRepository : IEditorialRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web);

    private readonly string _connectionString;

    public MariaDbEditorialRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<bool> HasAdminPermissionAsync(
        string userId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                SUM(CASE WHEN user_id = @user_id THEN 1 ELSE 0 END) AS user_grants,
                COUNT(*) AS total_grants
            FROM admin_permission_grants
            WHERE permission_code = @permission_code;
            """;
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@permission_code", permissionCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return true;
        }

        var total = IsDbNull(reader, "total_grants")
            ? 0
            : Convert.ToInt32(reader["total_grants"]);
        var user = IsDbNull(reader, "user_grants")
            ? 0
            : Convert.ToInt32(reader["user_grants"]);

        // Bootstrap compatibility: once explicit grants exist for a permission,
        // only granted admins keep access. Before that, existing internal_admin
        // users can still initialize the editorial module.
        return total == 0 || user > 0;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetAdminPermissionGrantCountsAsync(
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (permissionCodes.Count == 0)
        {
            return counts;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var parameters = new List<string>(permissionCodes.Count);
        for (var index = 0; index < permissionCodes.Count; index++)
        {
            var name = $"@code{index}";
            parameters.Add(name);
            command.Parameters.AddWithValue(name, permissionCodes[index]);
        }

        command.CommandText =
            $"""
            SELECT permission_code, COUNT(*) AS grant_count
            FROM admin_permission_grants
            WHERE permission_code IN ({string.Join(", ", parameters)})
            GROUP BY permission_code;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts[reader.GetString("permission_code")] =
                Convert.ToInt32(reader["grant_count"]);
        }

        return counts;
    }

    public async Task<IReadOnlyList<EditorialCategory>> GetCategoriesAsync(
        string? contentType,
        CancellationToken cancellationToken)
    {
        var categories = new List<EditorialCategory>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id, content_type, name, slug, description, sort_order,
                created_at, updated_at
            FROM editorial_categories
            WHERE (@content_type IS NULL OR content_type = @content_type)
            ORDER BY content_type, sort_order, name;
            """;
        command.Parameters.AddWithValue("@content_type", DbValue(contentType));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(ReadCategory(reader));
        }

        return categories;
    }

    public async Task<EditorialCategory> UpsertCategoryAsync(
        EditorialCategory category,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO editorial_categories (
                id, content_type, name, slug, description, sort_order,
                created_at, updated_at
            ) VALUES (
                @id, @content_type, @name, @slug, @description, @sort_order,
                @created_at, @updated_at
            )
            ON DUPLICATE KEY UPDATE
                content_type = VALUES(content_type),
                name = VALUES(name),
                slug = VALUES(slug),
                description = VALUES(description),
                sort_order = VALUES(sort_order),
                updated_at = VALUES(updated_at);
            """;
        AddCategoryParameters(command, category);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return category;
    }

    public async Task<IReadOnlyList<EditorialContentSummary>> GetContentListAsync(
        string? contentType,
        string? status,
        string? query,
        CancellationToken cancellationToken)
    {
        var contents = new List<EditorialContentSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                c.id, c.content_type, c.title, c.slug, c.summary,
                c.body_markdown, c.category_id, cat.name AS category_name, cat.sort_order AS category_sort_order,
                c.status, c.seo_title, c.seo_description, c.canonical_url,
                c.no_index, c.sort_order, c.published_at, c.created_at,
                c.updated_at, c.created_by_user_id, c.updated_by_user_id,
                GROUP_CONCAT(scope.scope_key ORDER BY scope.sort_order, scope.scope_key SEPARATOR ',')
                    AS faq_scopes
            FROM editorial_contents c
            LEFT JOIN editorial_categories cat ON cat.id = c.category_id
            LEFT JOIN editorial_faq_scope_links scope ON scope.content_id = c.id
            WHERE (@content_type IS NULL OR c.content_type = @content_type)
                AND (@status IS NULL OR c.status = @status)
                AND (
                    @query IS NULL
                    OR c.title LIKE @query_like
                    OR c.summary LIKE @query_like
                    OR c.body_markdown LIKE @query_like
                )
            GROUP BY
                c.id, c.content_type, c.title, c.slug, c.summary,
                c.body_markdown, c.category_id, cat.name, cat.sort_order, c.status,
                c.seo_title, c.seo_description, c.canonical_url, c.no_index,
                c.sort_order, c.published_at, c.created_at, c.updated_at,
                c.created_by_user_id, c.updated_by_user_id
            ORDER BY c.sort_order, c.updated_at DESC;
            """;
        AddListParameters(command, contentType, status, query);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            contents.Add(ReadContent(reader));
        }

        return contents.Select(content => (EditorialContentSummary)content).ToArray();
    }

    public async Task<EditorialContentDetail?> GetContentAsync(
        string id,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ContentSelectSql + " WHERE c.id = @id GROUP BY "
            + ContentGroupBySql
            + " LIMIT 1;";
        command.Parameters.AddWithValue("@id", id);
        return await ReadSingleContentAsync(command, cancellationToken);
    }

    public async Task<EditorialContentDetail?> GetContentBySlugAsync(
        string contentType,
        string slug,
        bool publicOnly,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ContentSelectSql
            + """
             WHERE c.content_type = @content_type
                AND c.slug = @slug
            """
            + (publicOnly
                ? " AND c.status = 'published' AND c.published_at IS NOT NULL"
                : "")
            + " GROUP BY " + ContentGroupBySql + " LIMIT 1;";
        command.Parameters.AddWithValue("@content_type", contentType);
        command.Parameters.AddWithValue("@slug", slug);
        return await ReadSingleContentAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<EditorialContentDetail>> GetFaqByScopeAsync(
        string scope,
        CancellationToken cancellationToken)
    {
        var contents = new List<EditorialContentDetail>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ContentSelectSql
            + """
             WHERE c.content_type = 'faq'
                AND c.status = 'published'
                AND c.published_at IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM editorial_faq_scope_links l
                    WHERE l.content_id = c.id AND l.scope_key = @scope
                )
            GROUP BY
            """
            + ContentGroupBySql
            + " ORDER BY c.sort_order, c.title;";
        command.Parameters.AddWithValue("@scope", scope);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            contents.Add(ReadContent(reader));
        }

        return contents;
    }

    public async Task<EditorialRedirect?> GetRedirectAsync(
        string oldPath,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, content_type, old_path, new_path, created_at
            FROM editorial_redirects
            WHERE old_path = @old_path
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@old_path", oldPath);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new EditorialRedirect(
                ReadString(reader, "id"),
                ReadString(reader, "content_type"),
                ReadString(reader, "old_path"),
                ReadString(reader, "new_path"),
                ToUtcIso(ReadDateTime(reader, "created_at")))
            : null;
    }

    public async Task<EditorialContentDetail> UpsertContentAsync(
        EditorialContentDetail content,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO editorial_contents (
                    id, content_type, title, slug, summary, body_markdown,
                    category_id, status, seo_title, seo_description,
                    canonical_url, no_index, sort_order, published_at,
                    created_by_user_id, updated_by_user_id, created_at, updated_at
                ) VALUES (
                    @id, @content_type, @title, @slug, @summary, @body_markdown,
                    @category_id, @status, @seo_title, @seo_description,
                    @canonical_url, @no_index, @sort_order, @published_at,
                    @created_by_user_id, @updated_by_user_id, @created_at, @updated_at
                )
                ON DUPLICATE KEY UPDATE
                    title = VALUES(title),
                    slug = VALUES(slug),
                    summary = VALUES(summary),
                    body_markdown = VALUES(body_markdown),
                    category_id = VALUES(category_id),
                    status = VALUES(status),
                    seo_title = VALUES(seo_title),
                    seo_description = VALUES(seo_description),
                    canonical_url = VALUES(canonical_url),
                    no_index = VALUES(no_index),
                    sort_order = VALUES(sort_order),
                    published_at = VALUES(published_at),
                    updated_by_user_id = VALUES(updated_by_user_id),
                    updated_at = VALUES(updated_at);
                """;
            AddContentParameters(command, content);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText =
                "DELETE FROM editorial_faq_scope_links WHERE content_id = @content_id;";
            deleteCommand.Parameters.AddWithValue("@content_id", content.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var scope in content.FaqScopes.Distinct(StringComparer.Ordinal))
        {
            await using var scopeCommand = connection.CreateCommand();
            scopeCommand.Transaction = transaction;
            scopeCommand.CommandText =
                """
                INSERT IGNORE INTO editorial_faq_scopes (
                    scope_key, label, sort_order, created_at, updated_at
                ) VALUES (
                    @scope_key, @label, 100, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                INSERT INTO editorial_faq_scope_links (
                    content_id, scope_key, sort_order
                ) VALUES (
                    @content_id, @scope_key, @sort_order
                );
                """;
            scopeCommand.Parameters.AddWithValue("@content_id", content.Id);
            scopeCommand.Parameters.AddWithValue("@scope_key", scope);
            scopeCommand.Parameters.AddWithValue("@label", scope);
            scopeCommand.Parameters.AddWithValue("@sort_order", content.SortOrder);
            await scopeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return content;
    }

    public async Task<IReadOnlyList<EditorialRevisionSummary>> GetRevisionsAsync(
        string contentId,
        CancellationToken cancellationToken)
    {
        var revisions = new List<EditorialRevisionSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, content_id, version_number, action, created_at, created_by_user_id
            FROM editorial_content_revisions
            WHERE content_id = @content_id
            ORDER BY version_number DESC;
            """;
        command.Parameters.AddWithValue("@content_id", contentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            revisions.Add(ReadRevisionSummary(reader));
        }

        return revisions;
    }

    public async Task<EditorialRevisionDetail?> GetRevisionAsync(
        string revisionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id, content_id, version_number, action, created_at,
                created_by_user_id, snapshot_json
            FROM editorial_content_revisions
            WHERE id = @id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", revisionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<EditorialContentDetail>(
            ReadString(reader, "snapshot_json"),
            JsonOptions) ?? throw new InvalidOperationException(
                "Editorial revision snapshot is invalid.");

        return new EditorialRevisionDetail(
            ReadString(reader, "id"),
            ReadString(reader, "content_id"),
            ReadInt32(reader, "version_number"),
            ReadString(reader, "action"),
            ToUtcIso(ReadDateTime(reader, "created_at")),
            IsDbNull(reader, "created_by_user_id")
                ? null
                : ReadString(reader, "created_by_user_id"),
            snapshot);
    }

    public async Task AddRevisionAsync(
        string contentId,
        string action,
        EditorialContentDetail snapshot,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO editorial_content_revisions (
                id, content_id, version_number, action, snapshot_json,
                created_at, created_by_user_id
            ) VALUES (
                @id,
                @content_id,
                COALESCE((
                    SELECT MAX(existing.version_number) + 1
                    FROM editorial_content_revisions existing
                    WHERE existing.content_id = @content_id
                ), 1),
                @action,
                @snapshot_json,
                @created_at,
                @created_by_user_id
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@content_id", contentId);
        command.Parameters.AddWithValue("@action", action);
        command.Parameters.AddWithValue(
            "@snapshot_json",
            JsonSerializer.Serialize(snapshot, JsonOptions));
        command.Parameters.AddWithValue("@created_at", DateTime.UtcNow);
        command.Parameters.AddWithValue(
            "@created_by_user_id",
            DbValue(actorUserId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddRedirectAsync(
        string contentId,
        string contentType,
        string oldPath,
        string newPath,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        if (oldPath == newPath)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT IGNORE INTO editorial_redirects (
                id, content_id, content_type, old_path, new_path,
                created_at, created_by_user_id
            ) VALUES (
                @id, @content_id, @content_type, @old_path, @new_path,
                @created_at, @created_by_user_id
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@content_id", contentId);
        command.Parameters.AddWithValue("@content_type", contentType);
        command.Parameters.AddWithValue("@old_path", oldPath);
        command.Parameters.AddWithValue("@new_path", newPath);
        command.Parameters.AddWithValue("@created_at", DateTime.UtcNow);
        command.Parameters.AddWithValue("@created_by_user_id", DbValue(actorUserId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string ContentSelectSql =
        """
        SELECT
            c.id, c.content_type, c.title, c.slug, c.summary,
            c.body_markdown, c.category_id, cat.name AS category_name, cat.sort_order AS category_sort_order,
            c.status, c.seo_title, c.seo_description, c.canonical_url,
            c.no_index, c.sort_order, c.published_at, c.created_at,
            c.updated_at, c.created_by_user_id, c.updated_by_user_id,
            GROUP_CONCAT(scope.scope_key ORDER BY scope.sort_order, scope.scope_key SEPARATOR ',')
                AS faq_scopes
        FROM editorial_contents c
        LEFT JOIN editorial_categories cat ON cat.id = c.category_id
        LEFT JOIN editorial_faq_scope_links scope ON scope.content_id = c.id
        """;

    private const string ContentGroupBySql =
        """
        c.id, c.content_type, c.title, c.slug, c.summary,
        c.body_markdown, c.category_id, cat.name, cat.sort_order, c.status,
        c.seo_title, c.seo_description, c.canonical_url, c.no_index,
        c.sort_order, c.published_at, c.created_at, c.updated_at,
        c.created_by_user_id, c.updated_by_user_id
        """;

    private async Task<EditorialContentDetail?> ReadSingleContentAsync(
        MySqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadContent(reader)
            : null;
    }

    private static EditorialCategory ReadCategory(MySqlDataReader reader)
        => new(
            ReadString(reader, "id"),
            ReadString(reader, "content_type"),
            ReadString(reader, "name"),
            ReadString(reader, "slug"),
            IsDbNull(reader, "description") ? null : ReadString(reader, "description"),
            ReadInt32(reader, "sort_order"),
            ToUtcIso(ReadDateTime(reader, "created_at")),
            ToUtcIso(ReadDateTime(reader, "updated_at")));

    private static EditorialContentDetail ReadContent(MySqlDataReader reader)
    {
        var contentType = ReadString(reader, "content_type");
        var slug = ReadString(reader, "slug");
        return new EditorialContentDetail(
            ReadString(reader, "id"),
            contentType,
            ReadString(reader, "title"),
            slug,
            IsDbNull(reader, "summary") ? null : ReadString(reader, "summary"),
            ReadString(reader, "body_markdown"),
            IsDbNull(reader, "category_id") ? null : ReadString(reader, "category_id"),
            IsDbNull(reader, "category_name") ? null : ReadString(reader, "category_name"),
            IsDbNull(reader, "category_sort_order") ? null : ReadInt32(reader, "category_sort_order"),
            ReadString(reader, "status"),
            IsDbNull(reader, "seo_title") ? null : ReadString(reader, "seo_title"),
            IsDbNull(reader, "seo_description")
                ? null
                : ReadString(reader, "seo_description"),
            IsDbNull(reader, "canonical_url")
                ? null
                : ReadString(reader, "canonical_url"),
            ReadBoolean(reader, "no_index"),
            ReadInt32(reader, "sort_order"),
            ReadScopes(reader),
            IsDbNull(reader, "published_at")
                ? null
                : ToUtcIso(ReadDateTime(reader, "published_at")),
            ToUtcIso(ReadDateTime(reader, "created_at")),
            ToUtcIso(ReadDateTime(reader, "updated_at")),
            IsDbNull(reader, "created_by_user_id")
                ? null
                : ReadString(reader, "created_by_user_id"),
            IsDbNull(reader, "updated_by_user_id")
                ? null
                : ReadString(reader, "updated_by_user_id"),
            BuildPublicPath(contentType, slug));
    }

    private static IReadOnlyList<string> ReadScopes(MySqlDataReader reader)
    {
        if (IsDbNull(reader, "faq_scopes"))
        {
            return [];
        }

        return ReadString(reader, "faq_scopes")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static EditorialRevisionSummary ReadRevisionSummary(
        MySqlDataReader reader)
        => new(
            ReadString(reader, "id"),
            ReadString(reader, "content_id"),
            ReadInt32(reader, "version_number"),
            ReadString(reader, "action"),
            ToUtcIso(ReadDateTime(reader, "created_at")),
            IsDbNull(reader, "created_by_user_id")
                ? null
                : ReadString(reader, "created_by_user_id"));

    private static bool IsDbNull(MySqlDataReader reader, string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName));

    private static string ReadString(MySqlDataReader reader, string columnName)
    {
        var value = reader.GetValue(reader.GetOrdinal(columnName));
        return value switch
        {
            string text => text,
            Guid guid => guid.ToString("D"),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static int ReadInt32(MySqlDataReader reader, string columnName)
        => reader.GetInt32(reader.GetOrdinal(columnName));

    private static bool ReadBoolean(MySqlDataReader reader, string columnName)
        => reader.GetBoolean(reader.GetOrdinal(columnName));

    private static DateTime ReadDateTime(MySqlDataReader reader, string columnName)
        => reader.GetDateTime(reader.GetOrdinal(columnName));

    private static void AddCategoryParameters(
        MySqlCommand command,
        EditorialCategory category)
    {
        command.Parameters.AddWithValue("@id", category.Id);
        command.Parameters.AddWithValue("@content_type", category.ContentType);
        command.Parameters.AddWithValue("@name", category.Name);
        command.Parameters.AddWithValue("@slug", category.Slug);
        command.Parameters.AddWithValue("@description", DbValue(category.Description));
        command.Parameters.AddWithValue("@sort_order", category.SortOrder);
        command.Parameters.AddWithValue("@created_at", ParseUtc(category.CreatedAt));
        command.Parameters.AddWithValue("@updated_at", ParseUtc(category.UpdatedAt));
    }

    private static void AddContentParameters(
        MySqlCommand command,
        EditorialContentDetail content)
    {
        command.Parameters.AddWithValue("@id", content.Id);
        command.Parameters.AddWithValue("@content_type", content.ContentType);
        command.Parameters.AddWithValue("@title", content.Title);
        command.Parameters.AddWithValue("@slug", content.Slug);
        command.Parameters.AddWithValue("@summary", DbValue(content.Summary));
        command.Parameters.AddWithValue("@body_markdown", content.BodyMarkdown);
        command.Parameters.AddWithValue("@category_id", DbValue(content.CategoryId));
        command.Parameters.AddWithValue("@status", content.Status);
        command.Parameters.AddWithValue("@seo_title", DbValue(content.SeoTitle));
        command.Parameters.AddWithValue(
            "@seo_description",
            DbValue(content.SeoDescription));
        command.Parameters.AddWithValue("@canonical_url", DbValue(content.CanonicalUrl));
        command.Parameters.AddWithValue("@no_index", content.NoIndex);
        command.Parameters.AddWithValue("@sort_order", content.SortOrder);
        command.Parameters.AddWithValue(
            "@published_at",
            content.PublishedAt is null ? DBNull.Value : ParseUtc(content.PublishedAt));
        command.Parameters.AddWithValue(
            "@created_by_user_id",
            DbValue(content.CreatedByUserId));
        command.Parameters.AddWithValue(
            "@updated_by_user_id",
            DbValue(content.UpdatedByUserId));
        command.Parameters.AddWithValue("@created_at", ParseUtc(content.CreatedAt));
        command.Parameters.AddWithValue("@updated_at", ParseUtc(content.UpdatedAt));
    }

    private static void AddListParameters(
        MySqlCommand command,
        string? contentType,
        string? status,
        string? query)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        command.Parameters.AddWithValue("@content_type", DbValue(contentType));
        command.Parameters.AddWithValue("@status", DbValue(status));
        command.Parameters.AddWithValue("@query", DbValue(normalizedQuery));
        command.Parameters.AddWithValue(
            "@query_like",
            normalizedQuery is null ? DBNull.Value : $"%{normalizedQuery}%");
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;

    private static DateTime ParseUtc(string value)
        => DateTime.Parse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal
                | System.Globalization.DateTimeStyles.AdjustToUniversal);

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

    private static string? BuildPublicPath(string contentType, string slug)
        => contentType switch
        {
            EditorialContentTypes.WikiArticle => $"/article/{slug}",
            EditorialContentTypes.SeoPage => $"/{slug}",
            _ => null
        };
}

