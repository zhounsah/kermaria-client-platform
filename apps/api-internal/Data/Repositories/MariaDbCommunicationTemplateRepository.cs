using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Acces MariaDB aux tables specialisees de la migration 074. Aucune DDL n'est
/// executee ici : le compte applicatif n'en a pas le droit et une absence de
/// table doit remonter comme indisponibilite, pas comme migration implicite.
/// </summary>
public sealed class MariaDbCommunicationTemplateRepository
    : ICommunicationTemplateRepository
{
    private readonly string _connectionString;

    public MariaDbCommunicationTemplateRepository(
        SqlRuntimeConfiguration configuration)
        => _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<StoredEmailTemplate>> GetEmailTemplatesAsync(
        CancellationToken cancellationToken)
    {
        var items = new List<StoredEmailTemplate>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT template_key, subject_template, body_template, enabled,
                   version, updated_at
            FROM email_templates
            ORDER BY template_key;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new StoredEmailTemplate(
                reader.GetString("template_key"),
                reader.GetString("subject_template"),
                reader.GetString("body_template"),
                reader.GetBoolean("enabled"),
                reader.GetInt32("version"),
                reader.GetDateTime("updated_at")));
        }

        return items;
    }

    public Task<bool> TryUpsertEmailTemplateAsync(
        StoredEmailTemplate template,
        string displayName,
        int expectedVersion,
        CancellationToken cancellationToken)
        => UpsertAsync(
            expectedVersion,
            cancellationToken,
            update:
            """
            UPDATE email_templates
            SET display_name=@display_name, subject_template=@subject,
                body_template=@body, enabled=@enabled, version=@next_version,
                updated_by_user_id=@actor, updated_at=@updated_at
            WHERE template_key=@key AND version=@expected_version;
            """,
            insert:
            """
            INSERT IGNORE INTO email_templates
                (template_key, display_name, subject_template, body_template,
                 enabled, version, updated_by_user_id, created_at, updated_at)
            VALUES (@key, @display_name, @subject, @body, @enabled, @next_version,
                    @actor, @updated_at, @updated_at);
            """,
            bind: command =>
            {
                command.Parameters.AddWithValue("@key", template.Key);
                command.Parameters.AddWithValue("@display_name", displayName);
                command.Parameters.AddWithValue("@subject", template.Subject);
                command.Parameters.AddWithValue("@body", template.Body);
                command.Parameters.AddWithValue("@enabled", template.Enabled);
                command.Parameters.AddWithValue("@next_version", template.Version);
                command.Parameters.AddWithValue(
                    "@actor",
                    (object?)template.UpdatedByUserId ?? DBNull.Value);
                command.Parameters.AddWithValue("@updated_at", template.UpdatedAtUtc);
            });

    public Task AddEmailRevisionAsync(
        StoredEmailTemplate template,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
        => AddRevisionAsync(
            """
            INSERT INTO email_template_revisions
                (id, template_key, version, subject_template, body_template,
                 enabled, actor_user_id, correlation_id, outcome, created_at)
            VALUES (@id, @key, @version, @subject, @body, @enabled, @actor,
                    @correlation, @outcome, UTC_TIMESTAMP(6));
            """,
            command =>
            {
                command.Parameters.AddWithValue("@key", template.Key);
                command.Parameters.AddWithValue("@version", template.Version);
                command.Parameters.AddWithValue("@subject", template.Subject);
                command.Parameters.AddWithValue("@body", template.Body);
                command.Parameters.AddWithValue("@enabled", template.Enabled);
                command.Parameters.AddWithValue(
                    "@actor",
                    (object?)template.UpdatedByUserId ?? DBNull.Value);
            },
            outcome,
            correlationId,
            cancellationToken);

    public Task<IReadOnlyList<StoredTemplateRevision>> GetEmailRevisionsAsync(
        string templateKey,
        int limit,
        CancellationToken cancellationToken)
        => GetRevisionsAsync(
            "email_template_revisions",
            "template_key",
            templateKey,
            limit,
            cancellationToken);

    public async Task<IReadOnlyList<StoredNotificationTemplate>>
        GetNotificationTemplatesAsync(CancellationToken cancellationToken)
    {
        var items = new List<StoredNotificationTemplate>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT template_key, title_template, message_template, enabled,
                   version, updated_at
            FROM notification_templates
            ORDER BY template_key;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new StoredNotificationTemplate(
                reader.GetString("template_key"),
                reader.GetString("title_template"),
                reader.GetString("message_template"),
                reader.GetBoolean("enabled"),
                reader.GetInt32("version"),
                reader.GetDateTime("updated_at")));
        }

        return items;
    }

    public Task<bool> TryUpsertNotificationTemplateAsync(
        StoredNotificationTemplate template,
        string displayName,
        int expectedVersion,
        CancellationToken cancellationToken)
        => UpsertAsync(
            expectedVersion,
            cancellationToken,
            update:
            """
            UPDATE notification_templates
            SET display_name=@display_name, title_template=@title,
                message_template=@message, enabled=@enabled,
                version=@next_version, updated_by_user_id=@actor,
                updated_at=@updated_at
            WHERE template_key=@key AND version=@expected_version;
            """,
            insert:
            """
            INSERT IGNORE INTO notification_templates
                (template_key, display_name, title_template, message_template,
                 enabled, version, updated_by_user_id, created_at, updated_at)
            VALUES (@key, @display_name, @title, @message, @enabled,
                    @next_version, @actor, @updated_at, @updated_at);
            """,
            bind: command =>
            {
                command.Parameters.AddWithValue("@key", template.Key);
                command.Parameters.AddWithValue("@display_name", displayName);
                command.Parameters.AddWithValue("@title", template.Title);
                command.Parameters.AddWithValue("@message", template.Message);
                command.Parameters.AddWithValue("@enabled", template.Enabled);
                command.Parameters.AddWithValue("@next_version", template.Version);
                command.Parameters.AddWithValue(
                    "@actor",
                    (object?)template.UpdatedByUserId ?? DBNull.Value);
                command.Parameters.AddWithValue("@updated_at", template.UpdatedAtUtc);
            });

    public Task AddNotificationRevisionAsync(
        StoredNotificationTemplate template,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
        => AddRevisionAsync(
            """
            INSERT INTO notification_template_revisions
                (id, template_key, version, title_template, message_template,
                 enabled, actor_user_id, correlation_id, outcome, created_at)
            VALUES (@id, @key, @version, @title, @message, @enabled, @actor,
                    @correlation, @outcome, UTC_TIMESTAMP(6));
            """,
            command =>
            {
                command.Parameters.AddWithValue("@key", template.Key);
                command.Parameters.AddWithValue("@version", template.Version);
                command.Parameters.AddWithValue("@title", template.Title);
                command.Parameters.AddWithValue("@message", template.Message);
                command.Parameters.AddWithValue("@enabled", template.Enabled);
                command.Parameters.AddWithValue(
                    "@actor",
                    (object?)template.UpdatedByUserId ?? DBNull.Value);
            },
            outcome,
            correlationId,
            cancellationToken);

    public Task<IReadOnlyList<StoredTemplateRevision>> GetNotificationRevisionsAsync(
        string templateKey,
        int limit,
        CancellationToken cancellationToken)
        => GetRevisionsAsync(
            "notification_template_revisions",
            "template_key",
            templateKey,
            limit,
            cancellationToken);

    public async Task<IReadOnlyList<StoredSystemSnippet>> GetSnippetsAsync(
        CancellationToken cancellationToken)
    {
        var items = new List<StoredSystemSnippet>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT snippet_key, body_text, version, updated_at
            FROM system_snippets
            ORDER BY snippet_key;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new StoredSystemSnippet(
                reader.GetString("snippet_key"),
                reader.GetString("body_text"),
                reader.GetInt32("version"),
                reader.GetDateTime("updated_at")));
        }

        return items;
    }

    public Task<bool> TryUpsertSnippetAsync(
        StoredSystemSnippet snippet,
        string displayName,
        int expectedVersion,
        CancellationToken cancellationToken)
        => UpsertAsync(
            expectedVersion,
            cancellationToken,
            update:
            """
            UPDATE system_snippets
            SET display_name=@display_name, body_text=@body,
                version=@next_version, updated_by_user_id=@actor,
                updated_at=@updated_at
            WHERE snippet_key=@key AND version=@expected_version;
            """,
            insert:
            """
            INSERT IGNORE INTO system_snippets
                (snippet_key, display_name, body_text, version,
                 updated_by_user_id, created_at, updated_at)
            VALUES (@key, @display_name, @body, @next_version, @actor,
                    @updated_at, @updated_at);
            """,
            bind: command =>
            {
                command.Parameters.AddWithValue("@key", snippet.Key);
                command.Parameters.AddWithValue("@display_name", displayName);
                command.Parameters.AddWithValue("@body", snippet.Body);
                command.Parameters.AddWithValue("@next_version", snippet.Version);
                command.Parameters.AddWithValue(
                    "@actor",
                    (object?)snippet.UpdatedByUserId ?? DBNull.Value);
                command.Parameters.AddWithValue("@updated_at", snippet.UpdatedAtUtc);
            });

    public Task AddSnippetRevisionAsync(
        StoredSystemSnippet snippet,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
        => AddRevisionAsync(
            """
            INSERT INTO system_snippet_revisions
                (id, snippet_key, version, body_text, actor_user_id,
                 correlation_id, outcome, created_at)
            VALUES (@id, @key, @version, @body, @actor, @correlation, @outcome,
                    UTC_TIMESTAMP(6));
            """,
            command =>
            {
                command.Parameters.AddWithValue("@key", snippet.Key);
                command.Parameters.AddWithValue("@version", snippet.Version);
                command.Parameters.AddWithValue("@body", snippet.Body);
                command.Parameters.AddWithValue(
                    "@actor",
                    (object?)snippet.UpdatedByUserId ?? DBNull.Value);
            },
            outcome,
            correlationId,
            cancellationToken);

    public Task<IReadOnlyList<StoredTemplateRevision>> GetSnippetRevisionsAsync(
        string snippetKey,
        int limit,
        CancellationToken cancellationToken)
        => GetRevisionsAsync(
            "system_snippet_revisions",
            "snippet_key",
            snippetKey,
            limit,
            cancellationToken);

    private async Task<MySqlConnection> OpenAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    /// Concurrence optimiste : la version attendue borne l'UPDATE. La version
    /// attendue 0 signifie « aucune ligne persistee », donc un INSERT IGNORE
    /// qui echoue si un autre administrateur a cree la ligne entre-temps.
    /// </summary>
    private async Task<bool> UpsertAsync(
        int expectedVersion,
        CancellationToken cancellationToken,
        string update,
        string insert,
        Action<MySqlCommand> bind)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = update;
        bind(command);
        command.Parameters.AddWithValue("@expected_version", expectedVersion);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 1)
        {
            return true;
        }

        if (expectedVersion != 0)
        {
            return false;
        }

        command.Parameters.Clear();
        command.CommandText = insert;
        bind(command);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private async Task AddRevisionAsync(
        string commandText,
        Action<MySqlCommand> bind,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        bind(command);
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@correlation", correlationId);
        command.Parameters.AddWithValue("@outcome", outcome);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<StoredTemplateRevision>> GetRevisionsAsync(
        string tableName,
        string keyColumn,
        string keyValue,
        int limit,
        CancellationToken cancellationToken)
    {
        var items = new List<StoredTemplateRevision>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Les noms de table et de colonne proviennent exclusivement d'appels
        // internes a cette classe, jamais d'une entree utilisateur.
        command.CommandText =
            $"""
            SELECT {keyColumn} AS entry_key, version, outcome, actor_user_id,
                   correlation_id, created_at
            FROM {tableName}
            WHERE {keyColumn} = @key
            ORDER BY created_at DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("@key", keyValue);
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 100));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new StoredTemplateRevision(
                reader.GetString("entry_key"),
                reader.GetInt32("version"),
                reader.GetString("outcome"),
                // actor_user_id est un CHAR(36) : MySqlConnector le materialise
                // en Guid, jamais en string. GetString y leverait.
                MariaDbIdentifierReader.ReadNullable(reader, "actor_user_id"),
                reader.GetString("correlation_id"),
                reader.GetDateTime("created_at")));
        }

        return items;
    }
}
