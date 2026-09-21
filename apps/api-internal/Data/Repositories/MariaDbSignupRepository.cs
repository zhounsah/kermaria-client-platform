using System.Globalization;
using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbSignupRepository : ISignupRepository
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly SqlRuntimeConfiguration _configuration;

    public MariaDbSignupRepository(SqlRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsPersistent => true;

    public async Task<bool> HasBlockingSignupOrUserAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                (SELECT COUNT(*) FROM portal_users
                 WHERE LOWER(email) = @email)
              + (SELECT COUNT(*) FROM signup_pending
                 WHERE email = @email
                   AND status IN ('email_pending', 'email_verified', 'approved'));
            """;
        command.Parameters.AddWithValue("@email", normalizedEmail);
        var count = Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task<bool> HasExistingCustomerEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM customers WHERE LOWER(billing_email) = @email;";
        command.Parameters.AddWithValue("@email", normalizedEmail);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task<PortalEmailVerificationState> GetPortalUserEmailVerificationStateAsync(
        string portalUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                u.email_verified_at,
                EXISTS(
                    SELECT 1
                    FROM signup_pending s
                    WHERE s.approved_user_id = u.id
                      AND s.self_service_flow IN ('cart', 'vps')
                ) AS verification_required
            FROM portal_users u
            WHERE u.id = @portal_user_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@portal_user_id", portalUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            // Une session ne devrait jamais referencer une identite absente.
            // La policy reste fail-closed si cette invariant est brise.
            return new PortalEmailVerificationState(false, true);
        }

        return new PortalEmailVerificationState(
            !reader.IsDBNull(reader.GetOrdinal("email_verified_at")),
            reader.GetBoolean(reader.GetOrdinal("verification_required")));
    }

    public async Task<ManualCustomerCreateResult> CreateManualCustomerAsync(
        ManualCustomerCreateRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var displayName = request.Customer.DisplayName
            ?? throw new InvalidOperationException("Customer display name is required.");
        var billingEmail = request.Customer.BillingEmail
            ?? throw new InvalidOperationException("Customer billing email is required.");

        await using var duplicateGuard = connection.CreateCommand();
        duplicateGuard.Transaction = transaction;
        duplicateGuard.CommandText =
            """
            SELECT
                (SELECT COUNT(*) FROM portal_users WHERE LOWER(email) = @email)
              + (SELECT COUNT(*) FROM customers WHERE LOWER(billing_email) = @email)
              + (SELECT COUNT(*) FROM signup_pending
                 WHERE email = @email
                   AND status IN ('email_pending', 'email_verified', 'approved'))
            ;
            """;
        duplicateGuard.Parameters.AddWithValue("@email", billingEmail);
        var existing = Convert.ToInt64(
            await duplicateGuard.ExecuteScalarAsync(cancellationToken));
        if (existing > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("CUSTOMER_EMAIL_ALREADY_USED");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO customers (
                id, external_reference, display_name, status, customer_type,
                billing_email, phone, address, address_line_1, address_line_2,
                postal_code, city, country, created_at, updated_at
            ) VALUES (
                @id, @reference, @display_name, 'active', @customer_type,
                @billing_email, @phone, @address, @address_line_1, @address_line_2,
                @postal_code, @city, @country, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", request.CustomerId);
        command.Parameters.AddWithValue("@reference", request.CustomerReference);
        command.Parameters.AddWithValue("@display_name", displayName);
        command.Parameters.AddWithValue("@customer_type", DbValue(request.Customer.CustomerType));
        command.Parameters.AddWithValue("@billing_email", billingEmail);
        command.Parameters.AddWithValue("@phone", DbValue(request.Customer.Phone));
        command.Parameters.AddWithValue("@address", DbValue(BuildLegacyAddress(request.Customer)));
        command.Parameters.AddWithValue("@address_line_1", DbValue(request.Customer.AddressLine1));
        command.Parameters.AddWithValue("@address_line_2", DbValue(request.Customer.AddressLine2));
        command.Parameters.AddWithValue("@postal_code", DbValue(request.Customer.PostalCode));
        command.Parameters.AddWithValue("@city", DbValue(request.Customer.City));
        command.Parameters.AddWithValue("@country", DbValue(request.Customer.Country));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ManualCustomerCreateResult(
            request.CustomerReference,
            displayName,
            billingEmail,
            "active");
    }

    public Task<int> CountRecentSignupsByEmailAsync(
        string normalizedEmail,
        DateTime windowStartUtc,
        CancellationToken cancellationToken)
        => CountRecentAsync(
            "email = @value",
            normalizedEmail,
            windowStartUtc,
            cancellationToken);

    public Task<int> CountRecentSignupsBySourceAddressAsync(
        string sourceAddress,
        DateTime windowStartUtc,
        CancellationToken cancellationToken)
        => CountRecentAsync(
            "source_address = @value",
            sourceAddress,
            windowStartUtc,
            cancellationToken);

    private async Task<int> CountRecentAsync(
        string predicate,
        string value,
        DateTime windowStartUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // `predicate` n'est jamais construit a partir d'une entree utilisateur :
        // les deux seules formes possibles sont litterales ci-dessus, et la
        // valeur comparee reste un parametre.
        command.CommandText =
            $"""
            SELECT COUNT(*) FROM signup_pending
            WHERE {predicate}
              AND created_at >= @window_start;
            """;
        command.Parameters.AddWithValue("@value", value);
        command.Parameters.AddWithValue("@window_start", windowStartUtc);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task InsertPendingAsync(
        SignupInsert insert,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO signup_pending (
                id,
                status,
                company_name,
                contact_name,
                email,
                phone,
                message,
                customer_type,
                address_line_1,
                address_line_2,
                postal_code,
                city_structured,
                country_structured,
                personal_title,
                given_name,
                surname,
                birth_date,
                initials,
                is_primary_contact,
                catalog_configuration_snapshot_json,
                verification_token_hash,
                verification_token_expires_at,
                source_address,
                user_agent,
                self_service_flow,
                created_at,
                updated_at
            ) VALUES (
                @id,
                'email_pending',
                @company_name,
                @contact_name,
                @email,
                @phone,
                @message,
                @customer_type,
                @address_line_1,
                @address_line_2,
                @postal_code,
                @city_structured,
                @country_structured,
                @personal_title,
                @given_name,
                @surname,
                @birth_date,
                @initials,
                @is_primary_contact,
                @catalog_configuration_snapshot_json,
                @verification_token_hash,
                @verification_token_expires_at,
                @source_address,
                @user_agent,
                @self_service_flow,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", insert.Id);
        command.Parameters.AddWithValue("@company_name", insert.CompanyName);
        command.Parameters.AddWithValue("@contact_name", insert.ContactName);
        command.Parameters.AddWithValue("@email", insert.Email);
        command.Parameters.AddWithValue("@phone", DbValue(insert.Phone));
        command.Parameters.AddWithValue("@message", DbValue(insert.Message));
        command.Parameters.AddWithValue(
            "@customer_type",
            DbValue(insert.Customer.CustomerType));
        command.Parameters.AddWithValue(
            "@address_line_1",
            DbValue(insert.Customer.AddressLine1));
        command.Parameters.AddWithValue(
            "@address_line_2",
            DbValue(insert.Customer.AddressLine2));
        command.Parameters.AddWithValue(
            "@postal_code",
            DbValue(insert.Customer.PostalCode));
        command.Parameters.AddWithValue(
            "@city_structured",
            DbValue(insert.Customer.City));
        command.Parameters.AddWithValue(
            "@country_structured",
            DbValue(insert.Customer.Country));
        command.Parameters.AddWithValue(
            "@personal_title",
            DbValue(insert.PrimaryUser.PersonalTitle));
        command.Parameters.AddWithValue(
            "@given_name",
            DbValue(insert.PrimaryUser.GivenName));
        command.Parameters.AddWithValue(
            "@surname",
            DbValue(insert.PrimaryUser.Surname));
        command.Parameters.AddWithValue(
            "@birth_date",
            DbDateValue(insert.PrimaryUser.BirthDate));
        command.Parameters.AddWithValue(
            "@initials",
            DbValue(insert.PrimaryUser.Initials));
        command.Parameters.AddWithValue(
            "@is_primary_contact",
            insert.PrimaryUser.IsPrimaryContact ?? true);
        command.Parameters.AddWithValue(
            "@catalog_configuration_snapshot_json",
            SerializeCatalogContext(insert.BillingV2Selection));
        command.Parameters.AddWithValue(
            "@verification_token_hash",
            insert.VerificationTokenHash);
        command.Parameters.AddWithValue(
            "@verification_token_expires_at",
            insert.VerificationTokenExpiresAtUtc);
        command.Parameters.AddWithValue(
            "@source_address",
            DbValue(insert.SourceAddress));
        command.Parameters.AddWithValue(
            "@user_agent",
            DbValue(insert.UserAgent));
        command.Parameters.AddWithValue("@self_service_flow", DbValue(insert.SelfServiceFlow));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SignupVerificationTarget?> FindPendingByVerificationHashAsync(
        string verificationTokenHash,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, status, verification_token_expires_at, approved_user_id, self_service_flow
            FROM signup_pending
            WHERE verification_token_hash = @hash
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@hash", verificationTokenHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SignupVerificationTarget(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("status"),
            ReadNullableUtc(reader, "verification_token_expires_at"),
            ReadNullableIdentifier(reader, "approved_user_id"),
            ReadNullableString(reader, "self_service_flow"));
    }

    public async Task MarkEmailVerifiedAsync(
        string id,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE signup_pending
            SET status = CASE WHEN approved_user_id IS NULL THEN 'email_verified' ELSE 'approved' END,
                email_verified_at = UTC_TIMESTAMP(6), updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id AND status = 'email_pending';
            """;
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var userCommand = connection.CreateCommand();
        userCommand.Transaction = transaction;
        userCommand.CommandText =
            """
            UPDATE portal_users u JOIN signup_pending s ON s.approved_user_id = u.id
            SET u.email_verified_at = s.email_verified_at, u.updated_at = UTC_TIMESTAMP(6)
            WHERE s.id = @id AND s.email_verified_at IS NOT NULL;
            """;
        userCommand.Parameters.AddWithValue("@id", id);
        await userCommand.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<SignupVerificationResendTarget?> RotateSelfServiceVerificationTokenAsync(
        string portalUserId,
        string verificationTokenHash,
        DateTime verificationTokenExpiresAtUtc,
        DateTime resendAllowedBeforeUtc,
        CancellationToken cancellationToken)
        => await RotateSelfServiceVerificationTokenCoreAsync(
            "approved_user_id = @identity",
            portalUserId,
            verificationTokenHash,
            verificationTokenExpiresAtUtc,
            resendAllowedBeforeUtc,
            cancellationToken);

    public async Task<SignupVerificationResendTarget?> RotateSelfServiceVerificationTokenByEmailAsync(
        string normalizedEmail,
        string verificationTokenHash,
        DateTime verificationTokenExpiresAtUtc,
        DateTime resendAllowedBeforeUtc,
        CancellationToken cancellationToken)
        => await RotateSelfServiceVerificationTokenCoreAsync(
            "LOWER(email) = @identity",
            normalizedEmail,
            verificationTokenHash,
            verificationTokenExpiresAtUtc,
            resendAllowedBeforeUtc,
            cancellationToken);

    private async Task<SignupVerificationResendTarget?> RotateSelfServiceVerificationTokenCoreAsync(
        string identityPredicate,
        string identity,
        string verificationTokenHash,
        DateTime verificationTokenExpiresAtUtc,
        DateTime resendAllowedBeforeUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        string? signupId = null;
        string? email = null;
        string? contactName = null;
        string? flow = null;
        DateTime? expiresAtUtc = null;
        DateTime? updatedAtUtc = null;
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText =
                $"""
                SELECT id, email, contact_name, self_service_flow,
                       verification_token_expires_at, updated_at
                FROM signup_pending
                WHERE {identityPredicate}
                  AND status = 'email_pending'
                  AND self_service_flow IN ('cart', 'vps')
                  AND email_verified_at IS NULL
                ORDER BY updated_at DESC, id DESC
                LIMIT 1
                FOR UPDATE;
                """;
            read.Parameters.AddWithValue("@identity", identity);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                signupId = MariaDbIdentifierReader.ReadRequired(reader, "id");
                email = reader.GetString("email");
                contactName = reader.GetString("contact_name");
                flow = reader.GetString("self_service_flow");
                expiresAtUtc = ReadNullableUtc(reader, "verification_token_expires_at");
                updatedAtUtc = ReadNullableUtc(reader, "updated_at");
            }
        }

        if (signupId is null || email is null || contactName is null || flow is null
            || (expiresAtUtc is { } expiry && expiry > DateTime.UtcNow)
            && updatedAtUtc is { } updated && updated > resendAllowedBeforeUtc)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText =
            """
            UPDATE signup_pending
            SET verification_token_hash = @hash,
                verification_token_expires_at = @expires_at,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND status = 'email_pending'
              AND email_verified_at IS NULL;
            """;
        update.Parameters.AddWithValue("@hash", verificationTokenHash);
        update.Parameters.AddWithValue("@expires_at", verificationTokenExpiresAtUtc);
        update.Parameters.AddWithValue("@id", signupId);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return new SignupVerificationResendTarget(email, contactName, flow);
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

        await using var command = connection.CreateCommand();
        command.CommandText =
            BuildRecordSelectSql(
                whereClause: string.IsNullOrWhiteSpace(statusFilter)
                    ? null
                    : "WHERE signup_pending.status = @status",
                orderByClause: "ORDER BY signup_pending.created_at DESC",
                limitClause: $"LIMIT {capped}");
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            command.Parameters.AddWithValue("@status", statusFilter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<SignupPendingRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadRecord(reader));
        }

        return records;
    }

    public async Task<SignupPendingRecord?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildRecordSelectSql(
            "WHERE signup_pending.id = @id",
            limitClause: "LIMIT 1");
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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

        await using var command = connection.CreateCommand();
        command.CommandText = BuildRecordSelectSql(
            """
            WHERE signup_pending.approved_customer_id = @customer_id
              AND signup_pending.status = 'approved'
              AND (
                    signup_pending.catalog_configuration_snapshot_json IS NOT NULL
                  )
            """,
            "ORDER BY signup_pending.approved_at DESC, signup_pending.created_at DESC",
            "LIMIT 1");
        command.Parameters.AddWithValue("@customer_id", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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
        var koxoUniqueIdentifier = await AllocateKoxoUniqueIdentifierAsync(
            connection,
            transaction,
            cancellationToken);

        await using (var guard = connection.CreateCommand())
        {
            guard.Transaction = transaction;
            guard.CommandText =
                """
                SELECT status, self_service_flow
                FROM signup_pending
                WHERE id = @id
                FOR UPDATE;
                """;
            guard.Parameters.AddWithValue("@id", request.SignupId);
            await using var reader = await guard.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
            var status = reader.GetString("status");
            var flow = ReadNullableString(reader, "self_service_flow");
            var allowed = request.EmailVerified
                ? string.Equals(status, "email_verified", StringComparison.Ordinal)
                : string.Equals(status, "email_pending", StringComparison.Ordinal)
                  && string.Equals(flow, request.SelfServiceFlow, StringComparison.Ordinal)
                  && flow is "cart" or "vps";
            if (!allowed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
        }

        var customerDisplayName = request.Customer.DisplayName
            ?? throw new InvalidOperationException("Customer display name is required.");
        var billingEmail = request.Customer.BillingEmail
            ?? request.PrimaryUser.Email
            ?? throw new InvalidOperationException("Signup email is required.");
        var portalEmail = request.PrimaryUser.Email
            ?? request.Customer.BillingEmail
            ?? throw new InvalidOperationException("Portal user email is required.");
        var portalDisplayName = request.PrimaryUser.DisplayName
            ?? throw new InvalidOperationException("Portal user display name is required.");
        var legacyAddress = BuildLegacyAddress(request.Customer);

        await using (var customerCommand = connection.CreateCommand())
        {
            customerCommand.Transaction = transaction;
            customerCommand.CommandText =
                """
                INSERT INTO customers (
                    id,
                    external_reference,
                    display_name,
                    status,
                    customer_type,
                    billing_email,
                    phone,
                    address,
                    address_line_1,
                    address_line_2,
                    postal_code,
                    city,
                    country,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @reference,
                    @display_name,
                    'active',
                    @customer_type,
                    @billing_email,
                    @phone,
                    @address,
                    @address_line_1,
                    @address_line_2,
                    @postal_code,
                    @city,
                    @country,
                    UTC_TIMESTAMP(6),
                    UTC_TIMESTAMP(6)
                );
                """;
            customerCommand.Parameters.AddWithValue("@id", request.CustomerId);
            customerCommand.Parameters.AddWithValue("@reference", request.CustomerReference);
            customerCommand.Parameters.AddWithValue("@display_name", customerDisplayName);
            customerCommand.Parameters.AddWithValue(
                "@customer_type",
                DbValue(request.Customer.CustomerType));
            customerCommand.Parameters.AddWithValue("@billing_email", billingEmail);
            customerCommand.Parameters.AddWithValue(
                "@phone",
                DbValue(request.Customer.Phone ?? request.PrimaryUser.Phone));
            customerCommand.Parameters.AddWithValue("@address", DbValue(legacyAddress));
            customerCommand.Parameters.AddWithValue(
                "@address_line_1",
                DbValue(request.Customer.AddressLine1));
            customerCommand.Parameters.AddWithValue(
                "@address_line_2",
                DbValue(request.Customer.AddressLine2));
            customerCommand.Parameters.AddWithValue(
                "@postal_code",
                DbValue(request.Customer.PostalCode));
            customerCommand.Parameters.AddWithValue(
                "@city",
                DbValue(request.Customer.City));
            customerCommand.Parameters.AddWithValue(
                "@country",
                DbValue(request.Customer.Country));
            await customerCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var userCommand = connection.CreateCommand())
        {
            userCommand.Transaction = transaction;
            userCommand.CommandText =
                """
                INSERT INTO portal_users (
                    id,
                    customer_id,
                    identity_provider_subject,
                    email, email_verified_at,
                    password_hash,
                    display_name,
                    status,
                    role,
                    personal_title,
                    given_name,
                    surname,
                    birth_date,
                    koxo_unique_identifier,
                    initials,
                    phone,
                    is_primary_contact,
                    last_login_at,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @customer_id,
                    @subject,
                    @email, @email_verified_at,
                    @password_hash,
                    @display_name,
                    'active',
                    @role,
                    @personal_title,
                    @given_name,
                    @surname,
                    @birth_date,
                    @koxo_unique_identifier,
                    @initials,
                    @phone,
                    @is_primary_contact,
                    NULL,
                    UTC_TIMESTAMP(6),
                    UTC_TIMESTAMP(6)
                );
                """;
            userCommand.Parameters.AddWithValue("@id", request.UserId);
            userCommand.Parameters.AddWithValue("@customer_id", request.CustomerId);
            userCommand.Parameters.AddWithValue("@subject", $"signup-{request.UserId}");
            userCommand.Parameters.AddWithValue("@email", portalEmail);
            userCommand.Parameters.AddWithValue("@email_verified_at", request.EmailVerified ? DateTime.UtcNow : DBNull.Value);
            userCommand.Parameters.AddWithValue(
                "@password_hash",
                DbValue(request.InitialPasswordHash));
            userCommand.Parameters.AddWithValue("@display_name", portalDisplayName);
            userCommand.Parameters.AddWithValue("@role", PortalRoles.ClientUser);
            userCommand.Parameters.AddWithValue(
                "@personal_title",
                DbValue(request.PrimaryUser.PersonalTitle));
            userCommand.Parameters.AddWithValue(
                "@given_name",
                DbValue(request.PrimaryUser.GivenName));
            userCommand.Parameters.AddWithValue(
                "@surname",
                DbValue(request.PrimaryUser.Surname));
            userCommand.Parameters.AddWithValue(
                "@birth_date",
                DbDateValue(request.PrimaryUser.BirthDate));
            userCommand.Parameters.AddWithValue(
                "@koxo_unique_identifier",
                koxoUniqueIdentifier);
            userCommand.Parameters.AddWithValue(
                "@initials",
                DbValue(request.PrimaryUser.Initials));
            userCommand.Parameters.AddWithValue(
                "@phone",
                DbValue(request.PrimaryUser.Phone));
            userCommand.Parameters.AddWithValue(
                "@is_primary_contact",
                request.PrimaryUser.IsPrimaryContact ?? true);
            await userCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var signupCommand = connection.CreateCommand())
        {
            signupCommand.Transaction = transaction;
            signupCommand.CommandText =
                """
                UPDATE signup_pending
                SET status = CASE WHEN @email_verified THEN 'approved' ELSE 'email_pending' END,
                    approved_user_id = @user_id,
                    approved_customer_id = @customer_id,
                    approved_at = UTC_TIMESTAMP(6),
                    password_setup_token_hash = @password_hash,
                    password_setup_expires_at = @password_expires_at,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id;
                """;
            signupCommand.Parameters.AddWithValue("@id", request.SignupId);
            signupCommand.Parameters.AddWithValue("@email_verified", request.EmailVerified);
            signupCommand.Parameters.AddWithValue("@user_id", request.UserId);
            signupCommand.Parameters.AddWithValue("@customer_id", request.CustomerId);
            signupCommand.Parameters.AddWithValue(
                "@password_hash",
                DbValue(request.PasswordSetupTokenHash));
            signupCommand.Parameters.AddWithValue(
                "@password_expires_at",
                request.PasswordSetupExpiresAtUtc is { } passwordSetupExpiresAtUtc
                    ? passwordSetupExpiresAtUtc
                    : DBNull.Value);
            await signupCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new SignupApprovalResult(
            request.SignupId,
            request.CustomerId,
            request.CustomerReference,
            request.UserId,
            koxoUniqueIdentifier,
            billingEmail,
            portalDisplayName);
    }

    public async Task<bool> RejectAsync(
        string id,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE signup_pending
            SET status = 'rejected',
                rejected_at = UTC_TIMESTAMP(6),
                rejected_reason = @reason,
                verification_token_hash = NULL,
                verification_token_expires_at = NULL,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND status IN ('email_pending', 'email_verified');
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@reason", DbValue(reason));
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<SignupPasswordTarget?> FindApprovedByPasswordHashAsync(
        string passwordSetupTokenHash,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, approved_user_id, password_setup_expires_at
            FROM signup_pending
            WHERE password_setup_token_hash = @hash
              AND status = 'approved'
              AND approved_user_id IS NOT NULL
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@hash", passwordSetupTokenHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SignupPasswordTarget(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "approved_user_id"),
            ReadNullableUtc(reader, "password_setup_expires_at"));
    }

    public async Task RefreshPasswordSetupTokenAsync(
        string signupId,
        string passwordSetupTokenHash,
        DateTime passwordSetupExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE signup_pending
            SET password_setup_token_hash = @password_hash,
                password_setup_expires_at = @password_expires_at,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND status = 'approved'
              AND approved_user_id IS NOT NULL;
            """;
        command.Parameters.AddWithValue("@id", signupId);
        command.Parameters.AddWithValue("@password_hash", passwordSetupTokenHash);
        command.Parameters.AddWithValue(
            "@password_expires_at",
            passwordSetupExpiresAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetPasswordAsync(
        string signupId,
        string portalUserId,
        string passwordHash,
        PortalPasswordSecret? koxoSecret,
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        await using (var userCommand = connection.CreateCommand())
        {
            userCommand.Transaction = transaction;
            userCommand.CommandText =
                """
                UPDATE portal_users
                SET password_hash = @password_hash,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id;
                """;
            userCommand.Parameters.AddWithValue("@password_hash", passwordHash);
            userCommand.Parameters.AddWithValue("@id", portalUserId);
            await userCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var signupCommand = connection.CreateCommand())
        {
            signupCommand.Transaction = transaction;
            signupCommand.CommandText =
                """
                UPDATE signup_pending
                SET password_setup_token_hash = NULL,
                    password_setup_expires_at = NULL,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id;
                """;
            signupCommand.Parameters.AddWithValue("@id", signupId);
            await signupCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (koxoSecret is not null)
        {
            // Meme transaction que le condensat, volontairement : le secret
            // destine a KoXo et le mot de passe du portail decrivent le meme
            // fait. Publie separement, il survivait a l'echec de l'autre.
            await using (var secretCommand = connection.CreateCommand())
            {
                secretCommand.Transaction = transaction;
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
                secretCommand.Parameters.AddWithValue("@portal_user_id", portalUserId);
                secretCommand.Parameters.AddWithValue("@ciphertext", koxoSecret.Ciphertext);
                secretCommand.Parameters.AddWithValue("@key_id", koxoSecret.KeyId);
                secretCommand.Parameters.AddWithValue("@expires_at", koxoSecret.ExpiresAtUtc);
                await secretCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var syncCommand = connection.CreateCommand())
            {
                syncCommand.Transaction = transaction;
                syncCommand.CommandText =
                    """
                    UPDATE customer_ad_links
                    SET last_password_sync_at = @changed_at,
                        last_password_sync_status = 'pending'
                    WHERE portal_user_id = @portal_user_id
                      AND object_type = 'user';
                    """;
                syncCommand.Parameters.AddWithValue("@changed_at", atUtc);
                syncCommand.Parameters.AddWithValue("@portal_user_id", portalUserId);
                // Exactement une ligne : sans lien annuaire touche, le secret
                // partirait a KoXo sans etat de synchronisation en face.
                if (await syncCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException(
                        "L'etat de synchronisation KoXo n'a pas pu etre pose sur exactement un lien annuaire.");
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<string?> GetKoxoUniqueIdentifierAsync(
        string portalUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT koxo_unique_identifier
            FROM portal_users
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", portalUserId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value
            ? null
            : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static SignupPendingRecord ReadRecord(MySqlDataReader reader)
        => new(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("status"),
            reader.GetString("company_name"),
            reader.GetString("contact_name"),
            reader.GetString("email"),
            ReadNullableString(reader, "phone"),
            ReadNullableString(reader, "message"),
            new SignupCustomerData(
                ReadNullableString(reader, "customer_type"),
                reader.GetString("company_name"),
                reader.GetString("email"),
                ReadNullableString(reader, "phone"),
                ReadNullableString(reader, "address_line_1"),
                ReadNullableString(reader, "address_line_2"),
                ReadNullableString(reader, "postal_code"),
                ReadNullableString(reader, "city_structured"),
                ReadNullableString(reader, "country_structured")),
            new SignupUserData(
                ReadNullableString(reader, "personal_title"),
                ReadNullableString(reader, "given_name"),
                ReadNullableString(reader, "surname"),
                ReadNullableDate(reader, "birth_date"),
                ReadNullableString(reader, "initials"),
                reader.GetString("contact_name"),
                reader.GetString("email"),
                ReadNullableString(reader, "phone"),
                reader.GetBoolean("is_primary_contact")),

            ReadNullableString(reader, "source_address"),
            ReadNullableUtc(reader, "verification_token_expires_at"),
            ReadNullableIdentifier(reader, "approved_user_id"),
            ReadNullableIdentifier(reader, "approved_customer_id"),
            ReadNullableString(reader, "approved_customer_reference"),
            ReadNullableUtc(reader, "approved_at"),
            ReadNullableUtc(reader, "password_setup_expires_at"),
            reader.GetInt32("approved_user_has_password") > 0,
            ReadNullableString(reader, "ad_provisioning_status"),
            ReadNullableString(reader, "last_password_sync_status"),
            ReadNullableString(reader, "koxo_export_status"),
            ReadNullableString(reader, "approved_user_sam_account_name"),
            ReadNullableString(reader, "approved_user_principal_name"),
            ReadNullableUtc(reader, "rejected_at"),
            ReadNullableString(reader, "rejected_reason"),
            reader.GetDateTime("created_at"),
            reader.GetDateTime("updated_at"),
            BillingV2Selection: DeserializeBillingV2Selection(
                reader,
                "catalog_configuration_snapshot_json"),
            EmailVerifiedAtUtc: ReadNullableUtc(reader, "email_verified_at"),
            SelfServiceFlow: ReadNullableString(reader, "self_service_flow"));

    private static string BuildRecordSelectSql(
        string? whereClause = null,
        string? orderByClause = null,
        string? limitClause = null)
        => $"""
            SELECT
                signup_pending.id AS id,
                signup_pending.status AS status,
                signup_pending.company_name AS company_name,
                signup_pending.contact_name AS contact_name,
                signup_pending.email AS email,
                signup_pending.phone AS phone,
                signup_pending.message AS message,
                signup_pending.customer_type AS customer_type,
                signup_pending.address_line_1 AS address_line_1,
                signup_pending.address_line_2 AS address_line_2,
                signup_pending.postal_code AS postal_code,
                signup_pending.city_structured AS city_structured,
                signup_pending.country_structured AS country_structured,
                signup_pending.personal_title AS personal_title,
                signup_pending.given_name AS given_name,
                signup_pending.surname AS surname,
                signup_pending.birth_date AS birth_date,
                signup_pending.initials AS initials,
                signup_pending.is_primary_contact AS is_primary_contact,
                signup_pending.catalog_configuration_snapshot_json AS catalog_configuration_snapshot_json,
                signup_pending.source_address AS source_address,
                signup_pending.verification_token_expires_at AS verification_token_expires_at,
                signup_pending.email_verified_at AS email_verified_at,
                signup_pending.self_service_flow AS self_service_flow,
                signup_pending.approved_user_id AS approved_user_id,
                signup_pending.approved_customer_id AS approved_customer_id,
                approved_customer.external_reference AS approved_customer_reference,
                signup_pending.approved_at AS approved_at,
                signup_pending.password_setup_expires_at AS password_setup_expires_at,
                CASE
                    WHEN approved_user.password_hash IS NULL THEN 0
                    ELSE 1
                END AS approved_user_has_password,
                ad_link.ad_provisioning_status AS ad_provisioning_status,
                ad_link.last_password_sync_status AS last_password_sync_status,
                ad_link.koxo_export_status AS koxo_export_status,
                ad_link.sam_account_name AS approved_user_sam_account_name,
                ad_link.user_principal_name AS approved_user_principal_name,
                signup_pending.rejected_at AS rejected_at,
                signup_pending.rejected_reason AS rejected_reason,
                signup_pending.created_at AS created_at,
                signup_pending.updated_at AS updated_at
            FROM signup_pending
            LEFT JOIN customers approved_customer
                ON approved_customer.id = signup_pending.approved_customer_id
            LEFT JOIN portal_users approved_user
                ON approved_user.id = signup_pending.approved_user_id
            LEFT JOIN customer_ad_links ad_link
                ON ad_link.portal_user_id = signup_pending.approved_user_id
               AND ad_link.object_type = 'user'
            {whereClause ?? string.Empty}
            {orderByClause ?? string.Empty}
            {limitClause ?? string.Empty}
            """;

    private static object SerializeCatalogContext(
        BillingV2PublicSelection? billingV2Selection)
        => billingV2Selection is null
            ? DBNull.Value
            : JsonSerializer.Serialize(
                new SignupCatalogContextEnvelope("billing_v2", billingV2Selection),
                JsonOptions);

    private static Task<string> AllocateKoxoUniqueIdentifierAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CancellationToken cancellationToken)
        => KoxoIdentifierAllocator.AllocateAsync(
            connection,
            transaction,
            cancellationToken);

    private static BillingV2PublicSelection? DeserializeBillingV2Selection(
        MySqlDataReader reader,
        string columnName)
    {
        if (reader.IsDBNull(reader.GetOrdinal(columnName)))
        {
            return null;
        }

        var raw = reader.GetString(columnName);
        return string.IsNullOrWhiteSpace(raw)
            ? null
            : TryDeserializeBillingV2Selection(raw);
    }

    private static BillingV2PublicSelection? TryDeserializeBillingV2Selection(string raw)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<SignupCatalogContextEnvelope>(
                raw,
                JsonOptions);
            return envelope is not null
                && string.Equals(
                    envelope.Kind,
                    "billing_v2",
                    StringComparison.Ordinal)
                ? envelope.Selection
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record SignupCatalogContextEnvelope(
        string? Kind,
        BillingV2PublicSelection? Selection);

    private static string? BuildLegacyAddress(SignupCustomerData customer)
    {
        if (string.IsNullOrWhiteSpace(customer.AddressLine1))
        {
            return customer.AddressLine2;
        }

        if (string.IsNullOrWhiteSpace(customer.AddressLine2))
        {
            return customer.AddressLine1;
        }

        return $"{customer.AddressLine1}, {customer.AddressLine2}";
    }

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);

    private static string? ReadNullableIdentifier(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : MariaDbIdentifierReader.ReadRequired(reader, columnName);

    private static string? ReadNullableDate(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetDateTime(columnName)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime? ReadNullableUtc(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetDateTime(columnName);

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;

    private static object DbDateValue(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? DBNull.Value
            : DateTime.ParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
}
