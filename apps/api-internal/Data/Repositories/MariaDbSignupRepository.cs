using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbSignupRepository : ISignupRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SqlRuntimeConfiguration _configuration;

    public MariaDbSignupRepository(SqlRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsPersistent => true;

    public async Task<bool> HasRecentSignupOrUserAsync(
        string normalizedEmail,
        DateTime windowStartUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM portal_users
                 WHERE LOWER(email) = @email)
              + (SELECT COUNT(*) FROM signup_pending
                 WHERE email = @email
                   AND (status IN ('email_pending', 'email_verified', 'approved')
                        OR created_at >= @windowStart))
            """;
        cmd.Parameters.AddWithValue("email", normalizedEmail);
        cmd.Parameters.AddWithValue("windowStart", windowStartUtc);
        var count = Convert.ToInt64(
            await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task InsertPendingAsync(
        SignupInsert insert,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO signup_pending
                (id, status, company_name, contact_name, email, phone,
                 message, pack_selection_snapshot_json, verification_token_hash,
                 verification_token_expires_at, source_address, user_agent,
                 created_at, updated_at)
            VALUES
                (@id, 'email_pending', @companyName, @contactName, @email,
                 @phone, @message, @packSelectionSnapshotJson, @verificationHash,
                 @verificationExpires, @sourceAddress, @userAgent, UTC_TIMESTAMP(6),
                 UTC_TIMESTAMP(6))
            """;
        cmd.Parameters.AddWithValue("id", insert.Id);
        cmd.Parameters.AddWithValue("companyName", insert.CompanyName);
        cmd.Parameters.AddWithValue("contactName", insert.ContactName);
        cmd.Parameters.AddWithValue("email", insert.Email);
        cmd.Parameters.AddWithValue(
            "phone", (object?)insert.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "message", (object?)insert.Message ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "packSelectionSnapshotJson",
            SerializeSnapshot(insert.PackSelection));
        cmd.Parameters.AddWithValue(
            "verificationHash", insert.VerificationTokenHash);
        cmd.Parameters.AddWithValue(
            "verificationExpires", insert.VerificationTokenExpiresAtUtc);
        cmd.Parameters.AddWithValue(
            "sourceAddress", (object?)insert.SourceAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue(
            "userAgent", (object?)insert.UserAgent ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SignupVerificationTarget?>
        FindPendingByVerificationHashAsync(
            string verificationTokenHash,
            CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, status, verification_token_expires_at
            FROM signup_pending
            WHERE verification_token_hash = @hash
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("hash", verificationTokenHash);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SignupVerificationTarget(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("status"),
            reader.IsDBNull(reader.GetOrdinal("verification_token_expires_at"))
                ? null
                : reader.GetDateTime("verification_token_expires_at"));
    }

    public async Task MarkEmailVerifiedAsync(
        string id,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE signup_pending
            SET status = 'email_verified',
                verification_token_hash = NULL,
                verification_token_expires_at = NULL,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id AND status = 'email_pending'
            """;
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SignupPendingRecord>> ListAsync(
        string? statusFilter,
        int limit,
        CancellationToken cancellationToken)
    {
        var capped = Math.Clamp(limit, 1, 200);
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, status, company_name, contact_name, email, phone,
                   message, pack_selection_snapshot_json, source_address,
                   verification_token_expires_at, approved_user_id,
                   approved_customer_id, approved_at, rejected_at,
                   rejected_reason, created_at, updated_at
            FROM signup_pending
            {(string.IsNullOrWhiteSpace(statusFilter)
                ? string.Empty
                : "WHERE status = @status")}
            ORDER BY created_at DESC
            LIMIT {capped}
            """;
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            cmd.Parameters.AddWithValue("status", statusFilter);
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var entries = new List<SignupPendingRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(ReadRecord(reader));
        }

        return entries;
    }

    public async Task<SignupPendingRecord?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, status, company_name, contact_name, email, phone,
                   message, pack_selection_snapshot_json, source_address,
                   verification_token_expires_at, approved_user_id,
                   approved_customer_id, approved_at, rejected_at,
                   rejected_reason, created_at, updated_at
            FROM signup_pending
            WHERE id = @id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadRecord(reader);
    }

    public async Task<SignupPendingRecord?> GetLatestApprovedByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, status, company_name, contact_name, email, phone,
                   message, pack_selection_snapshot_json, source_address,
                   verification_token_expires_at, approved_user_id,
                   approved_customer_id, approved_at, rejected_at,
                   rejected_reason, created_at, updated_at
            FROM signup_pending
            WHERE approved_customer_id = @customerId
              AND status = 'approved'
              AND pack_selection_snapshot_json IS NOT NULL
            ORDER BY approved_at DESC, created_at DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("customerId", customerId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadRecord(reader);
    }

    public async Task<SignupApprovalResult?> ApproveAsync(
        SignupApprovalRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        await using (var guard = connection.CreateCommand())
        {
            guard.Transaction = transaction;
            guard.CommandText = """
                SELECT status FROM signup_pending WHERE id = @id FOR UPDATE
                """;
            guard.Parameters.AddWithValue("id", request.SignupId);
            var status = await guard.ExecuteScalarAsync(cancellationToken)
                as string;
            if (!string.Equals(status, "email_verified", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
        }

        await using (var customerCmd = connection.CreateCommand())
        {
            customerCmd.Transaction = transaction;
            customerCmd.CommandText = """
                INSERT INTO customers
                    (id, external_reference, display_name, status,
                     billing_email, phone, created_at, updated_at)
                VALUES
                    (@id, @reference, @displayName, 'active', @billingEmail,
                     @phone, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
                """;
            customerCmd.Parameters.AddWithValue("id", request.CustomerId);
            customerCmd.Parameters.AddWithValue(
                "reference", request.CustomerReference);
            customerCmd.Parameters.AddWithValue(
                "displayName", request.CompanyName);
            customerCmd.Parameters.AddWithValue(
                "billingEmail", request.BillingEmail);
            customerCmd.Parameters.AddWithValue(
                "phone", (object?)request.Phone ?? DBNull.Value);
            await customerCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var userCmd = connection.CreateCommand())
        {
            userCmd.Transaction = transaction;
            userCmd.CommandText = """
                INSERT INTO portal_users
                    (id, customer_id, identity_provider_subject, email,
                     password_hash, display_name, status, role,
                     created_at, updated_at)
                VALUES
                    (@id, @customerId, @subject, @email, NULL, @displayName,
                     'active', @role, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
                """;
            userCmd.Parameters.AddWithValue("id", request.UserId);
            userCmd.Parameters.AddWithValue("customerId", request.CustomerId);
            userCmd.Parameters.AddWithValue(
                "subject", $"signup-{request.UserId}");
            userCmd.Parameters.AddWithValue("email", request.BillingEmail);
            userCmd.Parameters.AddWithValue(
                "displayName", request.ContactName);
            userCmd.Parameters.AddWithValue("role", PortalRoles.ClientUser);
            await userCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var signupCmd = connection.CreateCommand())
        {
            signupCmd.Transaction = transaction;
            signupCmd.CommandText = """
                UPDATE signup_pending
                SET status = 'approved',
                    approved_user_id = @userId,
                    approved_customer_id = @customerId,
                    approved_at = UTC_TIMESTAMP(6),
                    password_setup_token_hash = @passwordHash,
                    password_setup_expires_at = @passwordExpires,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id
                """;
            signupCmd.Parameters.AddWithValue("id", request.SignupId);
            signupCmd.Parameters.AddWithValue("userId", request.UserId);
            signupCmd.Parameters.AddWithValue(
                "customerId", request.CustomerId);
            signupCmd.Parameters.AddWithValue(
                "passwordHash", request.PasswordSetupTokenHash);
            signupCmd.Parameters.AddWithValue(
                "passwordExpires", request.PasswordSetupExpiresAtUtc);
            await signupCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new SignupApprovalResult(
            request.SignupId,
            request.CustomerId,
            request.CustomerReference,
            request.UserId,
            request.BillingEmail,
            request.ContactName);
    }

    public async Task<bool> RejectAsync(
        string id,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE signup_pending
            SET status = 'rejected',
                rejected_at = UTC_TIMESTAMP(6),
                rejected_reason = @reason,
                verification_token_hash = NULL,
                verification_token_expires_at = NULL,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id AND status IN ('email_pending', 'email_verified')
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<SignupPasswordTarget?> FindApprovedByPasswordHashAsync(
        string passwordSetupTokenHash,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, approved_user_id, password_setup_expires_at
            FROM signup_pending
            WHERE password_setup_token_hash = @hash
              AND status = 'approved'
              AND approved_user_id IS NOT NULL
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("hash", passwordSetupTokenHash);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SignupPasswordTarget(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "approved_user_id"),
            reader.IsDBNull(reader.GetOrdinal("password_setup_expires_at"))
                ? null
                : reader.GetDateTime("password_setup_expires_at"));
    }

    public async Task SetPasswordAsync(
        string signupId,
        string portalUserId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        await using (var userCmd = connection.CreateCommand())
        {
            userCmd.Transaction = transaction;
            userCmd.CommandText = """
                UPDATE portal_users
                SET password_hash = @passwordHash,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id
                """;
            userCmd.Parameters.AddWithValue("passwordHash", passwordHash);
            userCmd.Parameters.AddWithValue("id", portalUserId);
            await userCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var signupCmd = connection.CreateCommand())
        {
            signupCmd.Transaction = transaction;
            signupCmd.CommandText = """
                UPDATE signup_pending
                SET password_setup_token_hash = NULL,
                    password_setup_expires_at = NULL,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id
                """;
            signupCmd.Parameters.AddWithValue("id", signupId);
            await signupCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static SignupPendingRecord ReadRecord(MySqlDataReader reader)
    {
        return new SignupPendingRecord(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("status"),
            reader.GetString("company_name"),
            reader.GetString("contact_name"),
            reader.GetString("email"),
            reader.IsDBNull(reader.GetOrdinal("phone"))
                ? null
                : reader.GetString("phone"),
            reader.IsDBNull(reader.GetOrdinal("message"))
                ? null
                : reader.GetString("message"),
            DeserializeSnapshot(reader, "pack_selection_snapshot_json"),
            reader.IsDBNull(reader.GetOrdinal("source_address"))
                ? null
                : reader.GetString("source_address"),
            reader.IsDBNull(reader.GetOrdinal("verification_token_expires_at"))
                ? null
                : reader.GetDateTime("verification_token_expires_at"),
            reader.IsDBNull(reader.GetOrdinal("approved_user_id"))
                ? null
                : MariaDbIdentifierReader.ReadRequired(reader, "approved_user_id"),
            reader.IsDBNull(reader.GetOrdinal("approved_customer_id"))
                ? null
                : MariaDbIdentifierReader.ReadRequired(reader, "approved_customer_id"),
            reader.IsDBNull(reader.GetOrdinal("approved_at"))
                ? null
                : reader.GetDateTime("approved_at"),
            reader.IsDBNull(reader.GetOrdinal("rejected_at"))
                ? null
                : reader.GetDateTime("rejected_at"),
            reader.IsDBNull(reader.GetOrdinal("rejected_reason"))
                ? null
                : reader.GetString("rejected_reason"),
            reader.GetDateTime("created_at"),
            reader.GetDateTime("updated_at"));
    }

    private static object SerializeSnapshot(SignupPackSelectionSnapshot? snapshot)
        => snapshot is null
            ? DBNull.Value
            : JsonSerializer.Serialize(snapshot, JsonOptions);

    private static SignupPackSelectionSnapshot? DeserializeSnapshot(
        MySqlDataReader reader,
        string columnName)
    {
        if (reader.IsDBNull(reader.GetOrdinal(columnName)))
        {
            return null;
        }

        var raw = reader.GetString(columnName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SignupPackSelectionSnapshot>(
                raw,
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
