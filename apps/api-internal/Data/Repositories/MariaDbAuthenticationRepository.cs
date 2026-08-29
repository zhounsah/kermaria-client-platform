using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbAuthenticationRepository : IAuthenticationRepository
{
    private readonly string _connectionString;

    public MariaDbAuthenticationRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public async Task<PortalUserCredential?> FindUserByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                u.id,
                u.customer_id,
                c.external_reference,
                u.email,
                u.display_name,
                u.status,
                u.role,
                u.password_hash,
                u.last_login_at,
                u.failed_login_count,
                u.last_failed_login_at,
                u.locked_until
            FROM portal_users u
            INNER JOIN customers c ON c.id = u.customer_id
            WHERE LOWER(u.email) = @email
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@email", normalizedEmail);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PortalUserCredential(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            reader.GetString("external_reference"),
            reader.GetString("email"),
            reader.GetString("display_name"),
            reader.GetString("status"),
            reader.GetString("role"),
            reader.IsDBNull(reader.GetOrdinal("password_hash"))
                ? null
                : reader.GetString("password_hash"),
            ReadNullableUtc(reader, "last_login_at"),
            reader.GetInt32("failed_login_count"),
            ReadNullableUtc(reader, "last_failed_login_at"),
            ReadNullableUtc(reader, "locked_until"));
    }

    public async Task CreateSessionAsync(
        string id,
        string userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? sourceAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO portal_sessions (
                id,
                user_id,
                session_token_hash,
                created_at,
                expires_at,
                revoked_at,
                last_seen_at,
                ip_address,
                user_agent
            ) VALUES (
                @id,
                @user_id,
                @session_token_hash,
                @created_at,
                @expires_at,
                NULL,
                @last_seen_at,
                @ip_address,
                @user_agent
            );
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue("@session_token_hash", tokenHash);
        command.Parameters.AddWithValue("@created_at", createdAtUtc);
        command.Parameters.AddWithValue("@expires_at", expiresAtUtc);
        command.Parameters.AddWithValue("@last_seen_at", createdAtUtc);
        command.Parameters.AddWithValue(
            "@ip_address",
            DbValue(sourceAddress));
        command.Parameters.AddWithValue("@user_agent", DbValue(userAgent));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PortalSessionRecord?> FindSessionAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                s.id,
                s.user_id,
                u.customer_id,
                c.external_reference,
                u.email,
                u.display_name,
                u.status,
                u.role,
                u.last_login_at,
                s.expires_at,
                s.revoked_at,
                s.last_seen_at
            FROM portal_sessions s
            INNER JOIN portal_users u ON u.id = s.user_id
            INNER JOIN customers c ON c.id = u.customer_id
            WHERE s.session_token_hash = @session_token_hash
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@session_token_hash", tokenHash);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PortalSessionRecord(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "user_id"),
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            reader.GetString("external_reference"),
            reader.GetString("email"),
            reader.GetString("display_name"),
            reader.GetString("status"),
            reader.GetString("role"),
            ReadNullableUtc(reader, "last_login_at"),
            AsUtc(reader.GetDateTime("expires_at")),
            reader.IsDBNull(reader.GetOrdinal("revoked_at"))
                ? null
                : AsUtc(reader.GetDateTime("revoked_at")),
            reader.IsDBNull(reader.GetOrdinal("last_seen_at"))
                ? null
                : AsUtc(reader.GetDateTime("last_seen_at")));
    }

    public async Task TouchSessionAsync(
        string sessionId,
        DateTime seenAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE portal_sessions
            SET last_seen_at = @last_seen_at
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@last_seen_at", seenAtUtc);
        command.Parameters.AddWithValue("@id", sessionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RevokeSessionAsync(
        string sessionId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE portal_sessions
            SET revoked_at = COALESCE(revoked_at, @revoked_at)
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@revoked_at", revokedAtUtc);
        command.Parameters.AddWithValue("@id", sessionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> RevokeOtherSessionsAsync(
        string userId,
        string currentSessionId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE portal_sessions
            SET revoked_at = @revoked_at
            WHERE user_id = @user_id
              AND id <> @current_session_id
              AND revoked_at IS NULL
              AND expires_at > @revoked_at;
            """;
        command.Parameters.AddWithValue("@revoked_at", revokedAtUtc);
        command.Parameters.AddWithValue("@user_id", userId);
        command.Parameters.AddWithValue(
            "@current_session_id",
            currentSessionId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LoginFailureState> RecordFailedLoginAsync(
        string userId,
        DateTime failedAtUtc,
        DateTime failureWindowStartUtc,
        int maximumFailures,
        DateTime lockedUntilUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);
        var failedLoginCount = 0;
        DateTime? lastFailedAtUtc = null;

        await using (var readCommand = connection.CreateCommand())
        {
            readCommand.Transaction = transaction;
            readCommand.CommandText =
                """
                SELECT failed_login_count, last_failed_login_at
                FROM portal_users
                WHERE id = @id
                FOR UPDATE;
                """;
            readCommand.Parameters.AddWithValue("@id", userId);
            await using var reader = await readCommand.ExecuteReaderAsync(
                cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Portal user is unavailable.");
            }

            failedLoginCount = reader.GetInt32("failed_login_count");
            lastFailedAtUtc = ReadNullableUtc(reader, "last_failed_login_at");
        }

        var nextCount = lastFailedAtUtc is null
            || lastFailedAtUtc < failureWindowStartUtc
                ? 1
                : failedLoginCount + 1;
        DateTime? nextLockedUntil = nextCount >= maximumFailures
            ? lockedUntilUtc
            : null;

        await using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                UPDATE portal_users
                SET failed_login_count = @failed_login_count,
                    last_failed_login_at = @last_failed_login_at,
                    locked_until = @locked_until,
                    updated_at = @updated_at
                WHERE id = @id;
                """;
            updateCommand.Parameters.AddWithValue(
                "@failed_login_count",
                nextCount);
            updateCommand.Parameters.AddWithValue(
                "@last_failed_login_at",
                failedAtUtc);
            updateCommand.Parameters.AddWithValue(
                "@locked_until",
                nextLockedUntil is null
                    ? DBNull.Value
                    : nextLockedUntil.Value);
            updateCommand.Parameters.AddWithValue("@updated_at", failedAtUtc);
            updateCommand.Parameters.AddWithValue("@id", userId);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new LoginFailureState(nextCount, nextLockedUntil);
    }

    public async Task ResetLoginFailuresAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE portal_users
            SET failed_login_count = 0,
                last_failed_login_at = NULL,
                locked_until = NULL,
                updated_at = @updated_at
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@updated_at", DateTime.UtcNow);
        command.Parameters.AddWithValue("@id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateLastLoginAsync(
        string userId,
        DateTime loggedInAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE portal_users
            SET last_login_at = @last_login_at,
                updated_at = @updated_at
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@last_login_at", loggedInAtUtc);
        command.Parameters.AddWithValue("@updated_at", loggedInAtUtc);
        command.Parameters.AddWithValue("@id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdatePasswordHashAsync(
        string userId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE portal_users
            SET password_hash = @password_hash,
                updated_at = @updated_at
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@password_hash", passwordHash);
        command.Parameters.AddWithValue("@updated_at", DateTime.UtcNow);
        command.Parameters.AddWithValue("@id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> TryChangePasswordWithKoxoHandoffAsync(
        string userId,
        string passwordHash,
        PortalPasswordSecret? koxoSecret,
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        await using (var passwordCommand = connection.CreateCommand())
        {
            passwordCommand.Transaction = transaction;
            passwordCommand.CommandText =
                """
                UPDATE portal_users
                SET password_hash = @password_hash,
                    updated_at = @updated_at
                WHERE id = @id;
                """;
            passwordCommand.Parameters.AddWithValue("@password_hash", passwordHash);
            passwordCommand.Parameters.AddWithValue("@updated_at", atUtc);
            passwordCommand.Parameters.AddWithValue("@id", userId);
            if (await passwordCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                // L'utilisateur portail n'existe plus. Deposer quand meme le
                // secret ferait appliquer par KoXo un mot de passe qui n'a
                // aucun compte portail en face.
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        }

        if (koxoSecret is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        await using (var secretCommand = connection.CreateCommand())
        {
            secretCommand.Transaction = transaction;
            // Le compteur de relectures repart de zero : c'est un nouveau
            // secret, pas la suite du precedent.
            secretCommand.CommandText =
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
            secretCommand.Parameters.AddWithValue("@portal_user_id", userId);
            secretCommand.Parameters.AddWithValue("@ciphertext", koxoSecret.Ciphertext);
            secretCommand.Parameters.AddWithValue("@key_id", koxoSecret.KeyId);
            secretCommand.Parameters.AddWithValue("@expires_at", koxoSecret.ExpiresAtUtc);
            await secretCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var syncCommand = connection.CreateCommand())
        {
            syncCommand.Transaction = transaction;
            // L'etat de synchronisation fait partie de la meme ecriture :
            // c'est le depot du secret qui rend la synchronisation due, et un
            // etat annonce sans secret derriere serait faux.
            syncCommand.CommandText =
                """
                UPDATE customer_ad_links
                SET last_password_sync_at = @changed_at,
                    last_password_sync_status = 'pending'
                WHERE portal_user_id = @portal_user_id
                  AND object_type = 'user';
                """;
            syncCommand.Parameters.AddWithValue("@changed_at", atUtc);
            syncCommand.Parameters.AddWithValue("@portal_user_id", userId);
            await syncCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
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

    private static DateTime AsUtc(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? ReadNullableUtc(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : AsUtc(reader.GetDateTime(columnName));
}
