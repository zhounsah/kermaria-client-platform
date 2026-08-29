using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbDemoContentTemplateRepository : IDemoContentTemplateRepository
{
    private readonly SqlRuntimeConfiguration _configuration;

    public MariaDbDemoContentTemplateRepository(SqlRuntimeConfiguration configuration)
        => _configuration = configuration;

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<StoredDemoContentTemplate>> ListAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Un seul lecteur ouvert a la fois sur une connexion MySqlConnector :
        // les services sont donc lus en entier avant d'etre regroupes, pas en
        // requete imbriquee par modele.
        var services = new Dictionary<string, List<StoredDemoTemplateService>>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT template_key, service_type, name, description, scope, display_order
                FROM demo_content_template_services
                ORDER BY template_key, display_order, name;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = reader.GetString("template_key");
                if (!services.TryGetValue(key, out var list))
                {
                    list = [];
                    services[key] = list;
                }

                list.Add(new StoredDemoTemplateService(
                    reader.GetString("service_type"),
                    reader.GetString("name"),
                    reader.GetString("description"),
                    reader.GetString("scope"),
                    reader.GetInt32("display_order")));
            }
        }

        var templates = new List<StoredDemoContentTemplate>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT template_key, label, description, enabled, display_order,
                       version, updated_at, updated_by_user_id
                FROM demo_content_templates
                ORDER BY display_order, template_key;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = reader.GetString("template_key");
                templates.Add(new StoredDemoContentTemplate(
                    key,
                    reader.GetString("label"),
                    reader.GetString("description"),
                    reader.GetBoolean("enabled"),
                    reader.GetInt32("display_order"),
                    reader.GetInt32("version"),
                    DateTime.SpecifyKind(reader.GetDateTime("updated_at"), DateTimeKind.Utc),
                    MariaDbIdentifierReader.ReadNullable(reader, "updated_by_user_id"),
                    services.TryGetValue(key, out var list) ? list : []));
            }
        }

        return templates;
    }

    public async Task<bool> TrySaveAsync(
        StoredDemoContentTemplate template,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        int storedVersion;
        await using (var check = connection.CreateCommand())
        {
            check.Transaction = transaction;
            check.CommandText =
                "SELECT version FROM demo_content_templates WHERE template_key = @key FOR UPDATE;";
            check.Parameters.AddWithValue("@key", template.TemplateKey);
            var scalar = await check.ExecuteScalarAsync(cancellationToken);
            storedVersion = scalar is null or DBNull ? 0 : Convert.ToInt32(scalar);
        }

        if (storedVersion != expectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var now = DateTime.UtcNow;
        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText =
                """
                INSERT INTO demo_content_templates (
                    template_key, label, description, enabled, display_order,
                    version, updated_by_user_id, created_at, updated_at)
                VALUES (
                    @key, @label, @description, @enabled, @display_order,
                    @version, @updated_by, @now, @now)
                ON DUPLICATE KEY UPDATE
                    label = VALUES(label),
                    description = VALUES(description),
                    enabled = VALUES(enabled),
                    display_order = VALUES(display_order),
                    version = VALUES(version),
                    updated_by_user_id = VALUES(updated_by_user_id),
                    updated_at = VALUES(updated_at);
                """;
            upsert.Parameters.AddWithValue("@key", template.TemplateKey);
            upsert.Parameters.AddWithValue("@label", template.Label);
            upsert.Parameters.AddWithValue("@description", template.Description);
            upsert.Parameters.AddWithValue("@enabled", template.Enabled);
            upsert.Parameters.AddWithValue("@display_order", template.DisplayOrder);
            upsert.Parameters.AddWithValue("@version", expectedVersion + 1);
            upsert.Parameters.AddWithValue(
                "@updated_by",
                (object?)template.UpdatedByUserId ?? DBNull.Value);
            upsert.Parameters.AddWithValue("@now", now);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        // Les services sont remplaces en bloc : l'ordre et la composition sont
        // decrits par la charge envoyee, pas reconstruits par differences.
        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText =
                "DELETE FROM demo_content_template_services WHERE template_key = @key;";
            clear.Parameters.AddWithValue("@key", template.TemplateKey);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        var order = 0;
        foreach (var service in template.Services)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO demo_content_template_services (
                    id, template_key, service_type, name, description, scope, display_order)
                VALUES (@id, @key, @service_type, @name, @description, @scope, @display_order);
                """;
            insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            insert.Parameters.AddWithValue("@key", template.TemplateKey);
            insert.Parameters.AddWithValue("@service_type", service.ServiceType);
            insert.Parameters.AddWithValue("@name", service.Name);
            insert.Parameters.AddWithValue("@description", service.Description);
            insert.Parameters.AddWithValue("@scope", service.Scope);
            insert.Parameters.AddWithValue("@display_order", (order += 10));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryDeleteAsync(
        string templateKey,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM demo_content_templates
            WHERE template_key = @key AND version = @version;
            """;
        command.Parameters.AddWithValue("@key", templateKey);
        command.Parameters.AddWithValue("@version", expectedVersion);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task AddRevisionAsync(
        string templateKey,
        int version,
        string payloadJson,
        string? actorUserId,
        string correlationId,
        string outcome,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO demo_content_template_revisions (
                id, template_key, version, payload_json,
                actor_user_id, correlation_id, outcome, created_at)
            VALUES (
                @id, @key, @version, @payload,
                @actor, @correlation_id, @outcome, @created_at);
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@key", templateKey);
        command.Parameters.AddWithValue("@version", version);
        command.Parameters.AddWithValue("@payload", payloadJson);
        command.Parameters.AddWithValue("@actor", (object?)actorUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("@correlation_id", correlationId);
        command.Parameters.AddWithValue("@outcome", outcome);
        command.Parameters.AddWithValue("@created_at", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredTemplateRevision>> GetRevisionsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT template_key, version, outcome, actor_user_id, correlation_id, created_at
            FROM demo_content_template_revisions
            ORDER BY created_at DESC
            LIMIT 100;
            """;

        var items = new List<StoredTemplateRevision>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new StoredTemplateRevision(
                reader.GetString("template_key"),
                reader.GetInt32("version"),
                reader.GetString("outcome"),
                MariaDbIdentifierReader.ReadNullable(reader, "actor_user_id"),
                reader.GetString("correlation_id"),
                DateTime.SpecifyKind(reader.GetDateTime("created_at"), DateTimeKind.Utc)));
        }

        return items;
    }
}
