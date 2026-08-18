using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbPortalPasswordSetupRepository
    : IPortalPasswordSetupRepository
{
    private readonly string _connectionString;

    public MariaDbPortalPasswordSetupRepository(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task IssueAsync(
        PortalPasswordSetupIssue issue,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        await using (var supersedeCommand = connection.CreateCommand())
        {
            supersedeCommand.Transaction = transaction;
            supersedeCommand.CommandText =
                """
                UPDATE portal_user_password_setups
                SET superseded_at = UTC_TIMESTAMP(6),
                    updated_at = UTC_TIMESTAMP(6)
                WHERE portal_user_id = @portal_user_id
                  AND purpose = @purpose
                  AND consumed_at IS NULL
                  AND superseded_at IS NULL;
                """;
            supersedeCommand.Parameters.AddWithValue(
                "@portal_user_id",
                issue.PortalUserId);
            supersedeCommand.Parameters.AddWithValue("@purpose", issue.Purpose);
            await supersedeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO portal_user_password_setups (
                    id,
                    portal_user_id,
                    purpose,
                    token_hash,
                    expires_at,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @portal_user_id,
                    @purpose,
                    @token_hash,
                    @expires_at,
                    UTC_TIMESTAMP(6),
                    UTC_TIMESTAMP(6)
                );
                """;
            insertCommand.Parameters.AddWithValue("@id", issue.Id);
            insertCommand.Parameters.AddWithValue(
                "@portal_user_id",
                issue.PortalUserId);
            insertCommand.Parameters.AddWithValue("@purpose", issue.Purpose);
            insertCommand.Parameters.AddWithValue(
                "@token_hash",
                issue.TokenHash);
            insertCommand.Parameters.AddWithValue(
                "@expires_at",
                issue.ExpiresAtUtc);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PortalPasswordSetupTarget?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                portal_user_id,
                purpose,
                expires_at,
                consumed_at,
                superseded_at
            FROM portal_user_password_setups
            WHERE token_hash = @token_hash
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@token_hash", tokenHash);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PortalPasswordSetupTarget(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "portal_user_id"),
            reader.GetString("purpose"),
            DateTime.SpecifyKind(
                reader.GetDateTime("expires_at"),
                DateTimeKind.Utc),
            !reader.IsDBNull(reader.GetOrdinal("consumed_at")),
            !reader.IsDBNull(reader.GetOrdinal("superseded_at")));
    }

    public async Task<PortalPasswordSetupConsumption> ConsumeAndSetPasswordAsync(
        string tokenHash,
        Func<string, string> hashPasswordForUser,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        string portalUserId;
        bool consumed;
        bool superseded;
        DateTime expiresAtUtc;

        await using (var lookupCommand = connection.CreateCommand())
        {
            lookupCommand.Transaction = transaction;
            // FOR UPDATE : la lecture et la consommation doivent etre
            // indissociables, sinon deux requetes concurrentes liraient toutes
            // les deux un jeton libre.
            lookupCommand.CommandText =
                """
                SELECT
                    portal_user_id,
                    expires_at,
                    consumed_at,
                    superseded_at
                FROM portal_user_password_setups
                WHERE token_hash = @token_hash
                LIMIT 1
                FOR UPDATE;
                """;
            lookupCommand.Parameters.AddWithValue("@token_hash", tokenHash);

            await using var reader = await lookupCommand.ExecuteReaderAsync(
                cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.TokenInvalid,
                    null);
            }

            portalUserId = MariaDbIdentifierReader.ReadRequired(
                reader,
                "portal_user_id");
            expiresAtUtc = DateTime.SpecifyKind(
                reader.GetDateTime("expires_at"),
                DateTimeKind.Utc);
            consumed = !reader.IsDBNull(reader.GetOrdinal("consumed_at"));
            superseded = !reader.IsDBNull(reader.GetOrdinal("superseded_at"));
        }

        if (consumed || superseded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PortalPasswordSetupConsumption(
                consumed
                    ? PortalPasswordSetupCodes.TokenAlreadyUsed
                    : PortalPasswordSetupCodes.TokenInvalid,
                null);
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PortalPasswordSetupConsumption(
                PortalPasswordSetupCodes.TokenExpired,
                null);
        }

        // La condition complete est repetee dans le UPDATE : le FOR UPDATE
        // protege de la concurrence, cette clause protege d'une relecture
        // devenue fausse et rend le nombre de lignes affectees decisif.
        int affected;
        await using (var consumeCommand = connection.CreateCommand())
        {
            consumeCommand.Transaction = transaction;
            consumeCommand.CommandText =
                """
                UPDATE portal_user_password_setups
                SET consumed_at = UTC_TIMESTAMP(6),
                    updated_at = UTC_TIMESTAMP(6)
                WHERE token_hash = @token_hash
                  AND consumed_at IS NULL
                  AND superseded_at IS NULL
                  AND expires_at > UTC_TIMESTAMP(6);
                """;
            consumeCommand.Parameters.AddWithValue("@token_hash", tokenHash);
            affected = await consumeCommand.ExecuteNonQueryAsync(
                cancellationToken);
        }

        if (affected != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PortalPasswordSetupConsumption(
                PortalPasswordSetupCodes.TokenInvalid,
                null);
        }

        await using (var passwordCommand = connection.CreateCommand())
        {
            passwordCommand.Transaction = transaction;
            passwordCommand.CommandText =
                """
                UPDATE portal_users
                SET password_hash = @password_hash,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id;
                """;
            passwordCommand.Parameters.AddWithValue(
                "@password_hash",
                hashPasswordForUser(portalUserId));
            passwordCommand.Parameters.AddWithValue("@id", portalUserId);
            var passwordAffected = await passwordCommand.ExecuteNonQueryAsync(
                cancellationToken);
            if (passwordAffected != 1)
            {
                // L'utilisateur portail vise n'existe plus : consommer le jeton
                // sans poser le mot de passe laisserait un compte inaccessible
                // et un lien mort.
                await transaction.RollbackAsync(cancellationToken);
                return new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.TokenInvalid,
                    null);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new PortalPasswordSetupConsumption(
            PortalPasswordSetupCodes.Consumed,
            portalUserId);
    }
}
