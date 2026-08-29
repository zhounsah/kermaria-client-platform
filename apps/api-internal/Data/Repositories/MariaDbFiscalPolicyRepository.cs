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
        command.CommandText =
            """
            SELECT id, regime, mention, effective_from, created_at, created_by_user_id
            FROM fiscal_policy_mentions
            ORDER BY regime, effective_from;
            """;

        var items = new List<StoredFiscalMention>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new StoredFiscalMention(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("regime"),
                reader.GetString("mention"),
                DateTime.SpecifyKind(reader.GetDateTime("effective_from"), DateTimeKind.Utc),
                DateTime.SpecifyKind(reader.GetDateTime("created_at"), DateTimeKind.Utc),
                MariaDbIdentifierReader.ReadNullable(reader, "created_by_user_id")));
        }

        return items;
    }

    public async Task<bool> TryAddAsync(
        StoredFiscalMention mention,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
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
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> TryDeleteScheduledAsync(
        string id,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
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
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
