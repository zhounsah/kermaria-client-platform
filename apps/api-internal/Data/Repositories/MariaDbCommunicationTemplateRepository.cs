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

    public Task<bool> TrySaveEmailTemplateAsync(
        StoredEmailTemplate template,
        string displayName,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
        => SaveAsync(
            "email_templates",
            "template_key",
            template.Key,
            expectedVersion,
            outcome,
            correlationId,
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
            INSERT INTO email_templates
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
            },
            revision:
            """
            INSERT INTO email_template_revisions
                (id, template_key, version, subject_template, body_template,
                 enabled, actor_user_id, correlation_id, outcome, created_at)
            VALUES (@id, @key, @version, @subject, @body, @enabled, @actor,
                    @correlation, @outcome, UTC_TIMESTAMP(6));
            """,
            bindRevision: command =>
            {
                command.Parameters.AddWithValue("@key", template.Key);
                command.Parameters.AddWithValue("@version", template.Version);
                command.Parameters.AddWithValue("@subject", template.Subject);
                command.Parameters.AddWithValue("@body", template.Body);
                command.Parameters.AddWithValue("@enabled", template.Enabled);
                command.Parameters.AddWithValue(
                    "@actor",
                    (object?)template.UpdatedByUserId ?? DBNull.Value);
            });

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

    public Task<bool> TrySaveNotificationTemplateAsync(
        StoredNotificationTemplate template,
        string displayName,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
        => SaveAsync(
            "notification_templates",
            "template_key",
            template.Key,
            expectedVersion,
            outcome,
            correlationId,
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
            INSERT INTO notification_templates
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
            },
            revision:
            """
            INSERT INTO notification_template_revisions
                (id, template_key, version, title_template, message_template,
                 enabled, actor_user_id, correlation_id, outcome, created_at)
            VALUES (@id, @key, @version, @title, @message, @enabled, @actor,
                    @correlation, @outcome, UTC_TIMESTAMP(6));
            """,
            bindRevision: command =>
            {
                command.Parameters.AddWithValue("@key", template.Key);
                command.Parameters.AddWithValue("@version", template.Version);
                command.Parameters.AddWithValue("@title", template.Title);
                command.Parameters.AddWithValue("@message", template.Message);
                command.Parameters.AddWithValue("@enabled", template.Enabled);
                command.Parameters.AddWithValue(
                    "@actor",
                    (object?)template.UpdatedByUserId ?? DBNull.Value);
            });

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

    public Task<bool> TrySaveSnippetAsync(
        StoredSystemSnippet snippet,
        string displayName,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
        => SaveAsync(
            "system_snippets",
            "snippet_key",
            snippet.Key,
            expectedVersion,
            outcome,
            correlationId,
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
            INSERT INTO system_snippets
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
            },
            revision:
            """
            INSERT INTO system_snippet_revisions
                (id, snippet_key, version, body_text, actor_user_id,
                 correlation_id, outcome, created_at)
            VALUES (@id, @key, @version, @body, @actor, @correlation, @outcome,
                    UTC_TIMESTAMP(6));
            """,
            bindRevision: command =>
            {
                command.Parameters.AddWithValue("@key", snippet.Key);
                command.Parameters.AddWithValue("@version", snippet.Version);
                command.Parameters.AddWithValue("@body", snippet.Body);
                command.Parameters.AddWithValue(
                    "@actor",
                    (object?)snippet.UpdatedByUserId ?? DBNull.Value);
            });

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
    /// Enregistrement et revision dans une seule transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le <c>SELECT ... FOR UPDATE</c> remplace le couple UPDATE-puis-INSERT :
    /// il verrouille la ligne existante, ou l'intervalle quand elle n'existe
    /// pas encore, ce qui serialise deux creations concurrentes de la meme cle.
    /// La comparaison de version se fait alors sur l'etat verrouille, pas sur
    /// un etat lu avant.
    /// </para>
    /// <para>
    /// La revision est insérée dans la meme transaction. Un modele enregistre
    /// sans trace laisserait croire qu'un message parti a de vrais clients n'a
    /// jamais ete modifie.
    /// </para>
    /// <para>
    /// Les noms de table et de colonne proviennent exclusivement d'appels
    /// internes a cette classe, jamais d'une entree utilisateur.
    /// </para>
    /// </remarks>
    private async Task<bool> SaveAsync(
        string tableName,
        string keyColumn,
        string keyValue,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken,
        string update,
        string insert,
        Action<MySqlCommand> bind,
        string revision,
        Action<MySqlCommand> bindRevision)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        int storedVersion;
        await using (var check = connection.CreateCommand())
        {
            check.Transaction = transaction;
            check.CommandText =
                $"SELECT version FROM {tableName} WHERE {keyColumn} = @lock_key FOR UPDATE;";
            check.Parameters.AddWithValue("@lock_key", keyValue);
            var scalar = await check.ExecuteScalarAsync(cancellationToken);
            storedVersion = scalar is null or DBNull ? 0 : Convert.ToInt32(scalar);
        }

        if (storedVersion != expectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await using (var write = connection.CreateCommand())
        {
            write.Transaction = transaction;
            write.CommandText = storedVersion == 0 ? insert : update;
            bind(write);
            if (storedVersion != 0)
            {
                write.Parameters.AddWithValue("@expected_version", expectedVersion);
            }

            await write.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var history = connection.CreateCommand())
        {
            history.Transaction = transaction;
            history.CommandText = revision;
            bindRevision(history);
            history.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            history.Parameters.AddWithValue("@correlation", correlationId);
            history.Parameters.AddWithValue("@outcome", outcome);
            await history.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
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
