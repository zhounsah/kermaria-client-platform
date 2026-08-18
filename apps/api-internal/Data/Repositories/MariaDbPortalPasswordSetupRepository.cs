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
        string expectedPurpose,
        Func<string, string> hashPasswordForUser,
        PortalPasswordHandoff? handoff,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        string portalUserId;
        string purpose;
        bool consumed;
        bool superseded;
        DateTime expiresAtUtc;

        await using (var lookupCommand = connection.CreateCommand())
        {
            lookupCommand.Transaction = transaction;
            // FOR UPDATE : la lecture et la consommation doivent etre
            // indissociables, sinon deux requetes concurrentes liraient toutes
            // les deux un jeton libre. Le `purpose` est lu ici, sous ce meme
            // verrou, et non par un appel prealable : verifier l'usage d'un
            // jeton en dehors de la transaction qui le consomme laisse
            // exactement la fenetre qu'on pretend fermer.
            lookupCommand.CommandText =
                """
                SELECT
                    portal_user_id,
                    purpose,
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
            purpose = reader.GetString("purpose");
            expiresAtUtc = DateTime.SpecifyKind(
                reader.GetDateTime("expires_at"),
                DateTimeKind.Utc);
            consumed = !reader.IsDBNull(reader.GetOrdinal("consumed_at"));
            superseded = !reader.IsDBNull(reader.GetOrdinal("superseded_at"));
        }

        // Meme reponse qu'un jeton inconnu : distinguer un jeton d'un autre
        // parcours d'un jeton inexistant dirait a l'appelant qu'il tient un
        // secret valide ailleurs. Le jeton n'est pas consomme.
        if (!string.Equals(purpose, expectedPurpose, StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PortalPasswordSetupConsumption(
                PortalPasswordSetupCodes.TokenInvalid,
                null);
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

        // Le relais a ete prepare a partir d'une lecture anterieure, hors
        // verrou. Si le jeton designe finalement quelqu'un d'autre, ecrire ce
        // secret attribuerait le mot de passe d'une personne a une autre.
        if (handoff is not null
            && !string.Equals(
                handoff.PortalUserId,
                portalUserId,
                StringComparison.OrdinalIgnoreCase))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PortalPasswordSetupConsumption(
                PortalPasswordSetupCodes.TokenInvalid,
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
                  AND purpose = @purpose
                  AND consumed_at IS NULL
                  AND superseded_at IS NULL
                  AND expires_at > UTC_TIMESTAMP(6);
                """;
            consumeCommand.Parameters.AddWithValue("@token_hash", tokenHash);
            consumeCommand.Parameters.AddWithValue("@purpose", expectedPurpose);
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

        if (handoff is not null)
        {
            // Meme transaction, volontairement. Une seconde transaction — ce
            // qu'on faisait — laissait une fenetre ou le jeton etait deja
            // consomme alors que le secret n'existait nulle part : la personne
            // se connectait au portail sans jamais obtenir ses acces, et le
            // mot de passe en clair n'existait plus pour recommencer.
            if (handoff.Secret is not null
                && !await WriteSecretAsync(
                    connection,
                    transaction,
                    portalUserId,
                    handoff.Secret,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.HandoffFailed,
                    null);
            }

            if (!await MarkKoxoPendingAsync(
                    connection,
                    transaction,
                    handoff,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PortalPasswordSetupConsumption(
                    PortalPasswordSetupCodes.HandoffFailed,
                    null);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new PortalPasswordSetupConsumption(
            PortalPasswordSetupCodes.Consumed,
            portalUserId);
    }

    private static async Task<bool> WriteSecretAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string portalUserId,
        PortalPasswordSecret secret,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Le compteur de relectures repart de zero : c'est un nouveau secret,
        // pas la suite du precedent.
        command.CommandText =
            """
            INSERT INTO koxo_pending_directory_passwords (
                portal_user_id, ciphertext, key_id, expires_at,
                published_count, created_at, updated_at
            ) VALUES (
                @portal_user_id, @ciphertext, @key_id, @expires_at,
                0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            )
            ON DUPLICATE KEY UPDATE
                ciphertext = VALUES(ciphertext),
                key_id = VALUES(key_id),
                expires_at = VALUES(expires_at),
                published_count = 0,
                last_published_at = NULL,
                updated_at = UTC_TIMESTAMP(6);
            """;
        command.Parameters.AddWithValue("@portal_user_id", portalUserId);
        command.Parameters.AddWithValue("@ciphertext", secret.Ciphertext);
        command.Parameters.AddWithValue("@key_id", secret.KeyId);
        command.Parameters.AddWithValue("@expires_at", secret.ExpiresAtUtc);
        return await command.ExecuteNonQueryAsync(cancellationToken) >= 1;
    }

    /// <summary>
    /// Fait passer le cycle de vie en <c>koxo_pending</c>, sous la meme
    /// transaction.
    /// </summary>
    /// <remarks>
    /// Meme clause d'entree que
    /// <see cref="MariaDbBillingV2AdditionalUserIdentityRepository"/>, plus la
    /// verification que le cycle vise bien cet utilisateur portail : le
    /// <c>lifecycle_id</c> vient d'une lecture faite avant le verrou.
    /// </remarks>
    private static async Task<bool> MarkKoxoPendingAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        PortalPasswordHandoff handoff,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_user_identity_provisioning
            SET status = 'koxo_pending',
                password_set_at = COALESCE(password_set_at, @at),
                koxo_triggered_at = @at,
                failure_code = NULL,
                failure_detail = NULL,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND portal_user_id = @portal_user_id
              AND status IN ('awaiting_password', 'koxo_pending', 'failed');
            """;
        command.Parameters.AddWithValue("@id", handoff.LifecycleId);
        command.Parameters.AddWithValue(
            "@portal_user_id",
            handoff.PortalUserId);
        command.Parameters.AddWithValue("@at", handoff.AtUtc);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
