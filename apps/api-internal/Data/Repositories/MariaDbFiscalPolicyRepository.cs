using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbFiscalPolicyRepository : IFiscalPolicyRepository
{
    private readonly SqlRuntimeConfiguration _configuration;

    public MariaDbFiscalPolicyRepository(SqlRuntimeConfiguration configuration)
        => _configuration = configuration;

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<StoredFiscalMention>> ListAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = ListSql;

        var items = new List<StoredFiscalMention>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadMention(reader));
        }

        return items;
    }

    private const string ListSql =
        """
        SELECT id, regime, mention, effective_from, created_at, created_by_user_id
        FROM fiscal_policy_mentions
        ORDER BY regime, effective_from;
        """;

    private const string VersionsSql =
        """
        SELECT regime, version
        FROM fiscal_policy_regime_versions;
        """;

    private static StoredFiscalMention ReadMention(MySqlDataReader reader)
        => new(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("regime"),
            reader.GetString("mention"),
            DateTime.SpecifyKind(reader.GetDateTime("effective_from"), DateTimeKind.Utc),
            DateTime.SpecifyKind(reader.GetDateTime("created_at"), DateTimeKind.Utc),
            MariaDbIdentifierReader.ReadNullable(reader, "created_by_user_id"));

    /// <remarks>
    /// Transaction de lecture courte, explicitement en <c>REPEATABLE READ</c> :
    /// les deux requetes voient le meme instantane, quelle que soit l'isolation
    /// par defaut du serveur. En <c>READ COMMITTED</c>, chaque requete prendrait
    /// son propre instantane et une mutation concurrente pourrait se glisser
    /// entre les deux. Aucune ligne n'est verrouillee : ce sont deux lectures.
    /// </remarks>
    public async Task<FiscalPolicyAdminSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead,
            cancellationToken);

        var mentions = new List<StoredFiscalMention>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = ListSql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                mentions.Add(ReadMention(reader));
            }
        }

        var versions = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = VersionsSql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                versions[reader.GetString("regime")] = Convert.ToInt32(reader.GetValue(1));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new FiscalPolicyAdminSnapshot(mentions, versions);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetRegimeVersionsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = VersionsSql;

        var versions = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            versions[reader.GetString("regime")] = Convert.ToInt32(reader.GetValue(1));
        }

        return versions;
    }

    /// <summary>
    /// Verification de version et insertion dans la meme transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le verrou porte sur la <b>ligne de version</b> du regime, pas sur les
    /// mentions. Le decompte precedent s'appuyait sur un verrou d'intervalle,
    /// qui n'existe qu'en REPEATABLE READ : en READ COMMITTED, deux ajouts
    /// concurrents sur un regime vide comptaient tous les deux zero et
    /// passaient tous les deux. Une ligne presente se verrouille dans les deux
    /// isolations.
    /// </para>
    /// <para>
    /// L'insertion reste un <c>INSERT IGNORE</c> : l'unicite
    /// <c>(regime, effective_from)</c> reste la garantie de dernier recours,
    /// meme si le verrou l'a deja rendue improbable.
    /// </para>
    /// </remarks>
    public async Task<FiscalMentionAddOutcome> TryAddAsync(
        StoredFiscalMention mention,
        int expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var storedVersion = await LockRegimeVersionAsync(
            connection,
            transaction,
            mention.Regime,
            cancellationToken);

        if (storedVersion != expectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return FiscalMentionAddOutcome.VersionConflict;
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT IGNORE INTO fiscal_policy_mentions (
                    id, regime, mention, effective_from,
                    created_at, created_by_user_id, correlation_id)
                VALUES (
                    @id, @regime, @mention, @effective_from,
                    @created_at, @created_by, @correlation_id);
                """;
            command.Parameters.AddWithValue("@id", mention.Id);
            command.Parameters.AddWithValue("@regime", mention.Regime);
            command.Parameters.AddWithValue("@mention", mention.Mention);
            command.Parameters.AddWithValue("@effective_from", mention.EffectiveFromUtc);
            command.Parameters.AddWithValue("@created_at", mention.CreatedAtUtc);
            command.Parameters.AddWithValue(
                "@created_by",
                (object?)mention.CreatedByUserId ?? DBNull.Value);
            command.Parameters.AddWithValue("@correlation_id", correlationId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return FiscalMentionAddOutcome.EffectiveDateTaken;
            }
        }

        await BumpRegimeVersionAsync(
            connection,
            transaction,
            mention.Regime,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FiscalMentionAddOutcome.Added;
    }

    /// <remarks>
    /// Transactionnelle, et elle incremente la version : une suppression est une
    /// modification comme une autre. Sans cela le numero redescendrait et un
    /// <c>expectedVersion</c> perime redeviendrait acceptable.
    /// </remarks>
    public async Task<bool> TryDeleteScheduledAsync(
        string id,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Lecture simple, et volontairement avant le verrou : le regime d'une
        // mention ne change jamais, et le DELETE revalide id et date d'effet
        // sous le verrou. Prendre le verrou de version en premier garde le meme
        // ordre d'acquisition que l'ajout, donc aucun interblocage entre les
        // deux chemins.
        string regime;
        await using (var lookup = connection.CreateCommand())
        {
            lookup.Transaction = transaction;
            lookup.CommandText =
                """
                SELECT regime
                FROM fiscal_policy_mentions
                WHERE id = @id AND effective_from > @now;
                """;
            lookup.Parameters.AddWithValue("@id", id);
            lookup.Parameters.AddWithValue("@now", nowUtc);
            var scalar = await lookup.ExecuteScalarAsync(cancellationToken);
            if (scalar is null or DBNull)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            regime = (string)scalar;
        }

        await LockRegimeVersionAsync(connection, transaction, regime, cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            // La condition sur `effective_from` fait partie de la requete : une
            // version deja en vigueur ne doit pas pouvoir disparaitre, meme si
            // l'appelant s'est trompe.
            command.CommandText =
                """
                DELETE FROM fiscal_policy_mentions
                WHERE id = @id AND effective_from > @now;
                """;
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@now", nowUtc);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        }

        await BumpRegimeVersionAsync(connection, transaction, regime, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Verrouille la ligne de version du regime et la retourne, en l'amorcant
    /// si elle n'existe pas encore.
    /// </summary>
    /// <remarks>
    /// L'amorce reprend le decompte courant des mentions : un ecran ouvert
    /// avant la bascule reste ainsi coherent. Elle est ecrite en deux temps
    /// plutot qu'en <c>INSERT ... SELECT</c>, dont les verrous sur la table
    /// source dependent du format de journalisation binaire.
    /// </remarks>
    private static async Task<int> LockRegimeVersionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string regime,
        CancellationToken cancellationToken)
    {
        int seed;
        await using (var count = connection.CreateCommand())
        {
            count.Transaction = transaction;
            count.CommandText =
                """
                SELECT COUNT(*) FROM fiscal_policy_mentions WHERE regime = @regime;
                """;
            count.Parameters.AddWithValue("@regime", regime);
            var scalar = await count.ExecuteScalarAsync(cancellationToken);
            seed = scalar is null or DBNull ? 0 : Convert.ToInt32(scalar);
        }

        await using (var ensure = connection.CreateCommand())
        {
            ensure.Transaction = transaction;
            ensure.CommandText =
                """
                INSERT IGNORE INTO fiscal_policy_regime_versions (
                    regime, version, updated_at)
                VALUES (@regime, @version, UTC_TIMESTAMP(6));
                """;
            ensure.Parameters.AddWithValue("@regime", regime);
            ensure.Parameters.AddWithValue("@version", seed);
            await ensure.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var read = connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText =
            """
            SELECT version
            FROM fiscal_policy_regime_versions
            WHERE regime = @regime
            FOR UPDATE;
            """;
        read.Parameters.AddWithValue("@regime", regime);
        var current = await read.ExecuteScalarAsync(cancellationToken);
        return current is null or DBNull ? seed : Convert.ToInt32(current);
    }

    private static async Task BumpRegimeVersionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string regime,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE fiscal_policy_regime_versions
            SET version = version + 1,
                updated_at = UTC_TIMESTAMP(6)
            WHERE regime = @regime;
            """;
        command.Parameters.AddWithValue("@regime", regime);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
