using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Acces MariaDB aux tables de la migration 075. Aucune DDL n'est executee
/// ici : le compte applicatif n'en a pas le droit et une table absente doit
/// remonter comme indisponibilite, pas comme migration implicite.
/// </summary>
public sealed class MariaDbDiagnosticConfigurationRepository
    : IDiagnosticConfigurationRepository
{
    private readonly string _connectionString;

    public MariaDbDiagnosticConfigurationRepository(
        SqlRuntimeConfiguration configuration)
        => _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");

    public bool IsPersistent => true;

    public async Task<StoredDiagnosticConfiguration?> GetAsync(
        string state,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT state, payload_json, version, updated_at
            FROM diagnostic_configurations
            WHERE state = @state;
            """;
        command.Parameters.AddWithValue("@state", state);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StoredDiagnosticConfiguration(
                reader.GetString("state"),
                reader.GetString("payload_json"),
                reader.GetInt32("version"),
                reader.GetDateTime("updated_at"))
            : null;
    }

    public async Task<bool> TrySaveDraftAsync(
        StoredDiagnosticConfiguration draft,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        if (!await UpsertAsync(
                connection,
                transaction,
                draft,
                expectedVersion,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await AddRevisionAsync(
            connection,
            transaction,
            draft,
            outcome,
            correlationId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryPublishAsync(
        StoredDiagnosticConfiguration published,
        int expectedPublishedVersion,
        int expectedDraftVersion,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        // Le brouillon est verrouille pendant la publication : un enregistrement
        // concurrent ne peut pas glisser une autre version entre la lecture et
        // l'ecriture publique.
        int draftVersion;
        await using (var check = connection.CreateCommand())
        {
            check.Transaction = transaction;
            check.CommandText =
                """
                SELECT version FROM diagnostic_configurations
                WHERE state = 'draft'
                FOR UPDATE;
                """;
            var scalar = await check.ExecuteScalarAsync(cancellationToken);
            draftVersion = scalar is null or DBNull ? 0 : Convert.ToInt32(scalar);
        }

        if (draftVersion != expectedDraftVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (!await UpsertAsync(
                connection,
                transaction,
                published,
                expectedPublishedVersion,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await AddRevisionAsync(
            connection,
            transaction,
            published,
            "published",
            correlationId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<StoredTemplateRevision>> GetRevisionsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var items = new List<StoredTemplateRevision>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT state, version, outcome, actor_user_id, correlation_id,
                   created_at
            FROM diagnostic_configuration_revisions
            ORDER BY created_at DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 100));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new StoredTemplateRevision(
                reader.GetString("state"),
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

    private async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    /// Concurrence optimiste : la version attendue borne l'UPDATE. Une version
    /// attendue de 0 signifie « aucune ligne persistee », donc un INSERT qui
    /// echoue si un autre administrateur a cree la ligne entre-temps.
    /// </summary>
    private static async Task<bool> UpsertAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        StoredDiagnosticConfiguration entry,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE diagnostic_configurations
            SET payload_json = @payload, version = @next_version,
                updated_by_user_id = @actor, updated_at = @updated_at
            WHERE state = @state AND version = @expected_version;
            """;
        Bind(command, entry);
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
        command.CommandText =
            """
            INSERT IGNORE INTO diagnostic_configurations
                (state, payload_json, version, updated_by_user_id, created_at,
                 updated_at)
            VALUES (@state, @payload, @next_version, @actor, @updated_at,
                    @updated_at);
            """;
        Bind(command, entry);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task AddRevisionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        StoredDiagnosticConfiguration entry,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO diagnostic_configuration_revisions
                (id, state, version, payload_json, actor_user_id,
                 correlation_id, outcome, created_at)
            VALUES (@id, @state, @version, @payload, @actor, @correlation,
                    @outcome, UTC_TIMESTAMP(6));
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@state", entry.State);
        command.Parameters.AddWithValue("@version", entry.Version);
        command.Parameters.AddWithValue("@payload", entry.PayloadJson);
        command.Parameters.AddWithValue(
            "@actor",
            (object?)entry.UpdatedByUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("@correlation", correlationId);
        command.Parameters.AddWithValue("@outcome", outcome);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Bind(MySqlCommand command, StoredDiagnosticConfiguration entry)
    {
        command.Parameters.AddWithValue("@state", entry.State);
        command.Parameters.AddWithValue("@payload", entry.PayloadJson);
        command.Parameters.AddWithValue("@next_version", entry.Version);
        command.Parameters.AddWithValue(
            "@actor",
            (object?)entry.UpdatedByUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("@updated_at", entry.UpdatedAtUtc);
    }
}
