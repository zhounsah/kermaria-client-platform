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
                u.password_hash
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
            reader.IsDBNull(reader.GetOrdinal("password_hash"))
                ? null
                : reader.GetString("password_hash"));
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
}
