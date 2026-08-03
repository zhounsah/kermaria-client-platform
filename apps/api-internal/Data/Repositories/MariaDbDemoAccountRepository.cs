using System.Globalization;
using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbDemoAccountRepository : IDemoAccountRepository
{
    private readonly string _connectionString;

    public MariaDbDemoAccountRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public async Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT 1 FROM portal_users WHERE LOWER(email) = @email LIMIT 1;";
        command.Parameters.AddWithValue("@email", email.ToLowerInvariant());
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null;
    }

    public async Task CreateDemoAccountAsync(
        DemoAccountCreationSpec spec,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await InsertCustomerAsync(
                connection,
                transaction,
                spec,
                cancellationToken);
            await InsertPortalUserAsync(
                connection,
                transaction,
                spec,
                cancellationToken);
            foreach (var service in spec.Services)
            {
                await InsertServiceAsync(
                    connection,
                    transaction,
                    spec.CustomerId,
                    service,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<DemoAccountSummary>> ListDemoAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var accounts = new List<DemoAccountSummary>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                c.external_reference,
                c.display_name,
                c.demo_kind,
                c.demo_expires_at,
                c.demo_revoked_at,
                c.created_at,
                dp.profile_key,
                (
                    SELECT COUNT(*)
                    FROM customer_services s
                    WHERE s.customer_id = c.id
                ) AS service_count
            FROM customers c
            LEFT JOIN demo_profiles dp ON dp.id = c.demo_profile_id
            WHERE c.is_demo = TRUE
            ORDER BY c.created_at DESC
            LIMIT 200;
            """;
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        var kindOrdinal = reader.GetOrdinal("demo_kind");
        var expiresOrdinal = reader.GetOrdinal("demo_expires_at");
        var revokedOrdinal = reader.GetOrdinal("demo_revoked_at");
        var profileOrdinal = reader.GetOrdinal("profile_key");
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new DemoAccountSummary(
                reader.GetString("external_reference"),
                reader.GetString("display_name"),
                reader.IsDBNull(kindOrdinal)
                    ? DemoKinds.Showcase
                    : reader.GetString(kindOrdinal),
                reader.IsDBNull(profileOrdinal)
                    ? null
                    : reader.GetString(profileOrdinal),
                reader.GetInt32("service_count"),
                ToUtcIso(reader.GetDateTime("created_at")),
                reader.IsDBNull(expiresOrdinal)
                    ? null
                    : ToUtcIso(reader.GetDateTime(expiresOrdinal)),
                reader.IsDBNull(revokedOrdinal)
                    ? null
                    : ToUtcIso(reader.GetDateTime(revokedOrdinal))));
        }

        return accounts;
    }

    public async Task<IReadOnlyList<DemoExpiredTrial>> ListExpiredTrialsToRevokeAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DemoExpiredTrial>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                c.id,
                c.external_reference,
                dp.ad_groups_json,
                (
                    SELECT pu.id
                    FROM portal_users pu
                    WHERE pu.customer_id = c.id
                    ORDER BY pu.created_at
                    LIMIT 1
                ) AS portal_user_id
            FROM customers c
            INNER JOIN demo_profiles dp ON dp.id = c.demo_profile_id
            WHERE c.is_demo = TRUE
              AND c.demo_kind = 'trial'
              AND c.demo_revoked_at IS NULL
              AND c.demo_expires_at IS NOT NULL
              AND c.demo_expires_at < @now
              AND dp.ad_provisioning_mode = 'real_scoped'
            ORDER BY c.demo_expires_at;
            """;
        command.Parameters.AddWithValue("@now", nowUtc);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        var groupsOrdinal = reader.GetOrdinal("ad_groups_json");
        var userOrdinal = reader.GetOrdinal("portal_user_id");
        while (await reader.ReadAsync(cancellationToken))
        {
            // Un trial provisionne porte toujours une identite portail ; on
            // ignore une ligne sans portal_user (etat incoherent) plutot que
            // de tenter une revocation sans cible.
            if (reader.IsDBNull(userOrdinal))
            {
                continue;
            }

            results.Add(new DemoExpiredTrial(
                ReadIdentifier(reader, "id"),
                reader.GetString("external_reference"),
                ReadIdentifier(reader, userOrdinal),
                ParseAdGroups(
                    reader.IsDBNull(groupsOrdinal)
                        ? null
                        : reader.GetString(groupsOrdinal))));
        }

        return results;
    }

    public async Task<IReadOnlyList<DemoTrialProvisioningTarget>>
        ListTrialsForProvisioningRetryAsync(
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
    {
        var results = new List<DemoTrialProvisioningTarget>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Essais encore vivants : ni revoques, ni echus. Ceux qui n'ont pas
        // encore d'identite AD sont simplement ignores plus haut.
        command.CommandText =
            """
            SELECT c.id,
                   c.external_reference,
                   dp.ad_groups_json,
                   (
                       SELECT pu.id FROM portal_users pu
                       WHERE pu.customer_id = c.id
                       ORDER BY pu.created_at
                       LIMIT 1
                   ) AS portal_user_id
            FROM customers c
            INNER JOIN demo_profiles dp ON dp.id = c.demo_profile_id
            WHERE c.is_demo = TRUE
              AND c.demo_kind = 'trial'
              AND c.demo_revoked_at IS NULL
              AND dp.ad_provisioning_mode = 'real_scoped'
              AND (c.demo_expires_at IS NULL OR c.demo_expires_at >= @now)
            ORDER BY c.demo_expires_at;
            """;
        command.Parameters.AddWithValue("@now", nowUtc);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        var groupsOrdinal = reader.GetOrdinal("ad_groups_json");
        var userOrdinal = reader.GetOrdinal("portal_user_id");
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(userOrdinal))
            {
                continue;
            }

            results.Add(new DemoTrialProvisioningTarget(
                ReadIdentifier(reader, "id"),
                reader.GetString("external_reference"),
                ReadIdentifier(reader, userOrdinal),
                ParseAdGroups(
                    reader.IsDBNull(groupsOrdinal)
                        ? null
                        : reader.GetString(groupsOrdinal))));
        }

        return results;
    }

    public async Task MarkTrialProvisionedAsync(
        string customerId,
        DateTime provisionedAtUtc,
        CancellationToken cancellationToken = default)
        => await MarkTrialTimestampAsync(
            "demo_provisioned_at",
            customerId,
            provisionedAtUtc,
            cancellationToken);

    public async Task MarkTrialRevokedAsync(
        string customerId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
        => await MarkTrialTimestampAsync(
            "demo_revoked_at",
            customerId,
            revokedAtUtc,
            cancellationToken);

    public async Task<DemoConversionCandidate?> FindConversionCandidateAsync(
        string customerReference,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // LEFT JOIN : un compte deja converti n'a plus de demo_profile_id, il
        // doit malgre tout etre retrouve pour repondre « deja converti ».
        command.CommandText =
            """
            SELECT c.id,
                   c.external_reference,
                   c.demo_kind,
                   c.demo_converted_at,
                   dp.profile_key,
                   dp.ad_groups_json,
                   (
                       SELECT pu.id FROM portal_users pu
                       WHERE pu.customer_id = c.id
                       ORDER BY pu.created_at
                       LIMIT 1
                   ) AS portal_user_id
            FROM customers c
            LEFT JOIN demo_profiles dp ON dp.id = c.demo_profile_id
            WHERE c.external_reference = @reference
              AND (c.is_demo = TRUE OR c.demo_converted_at IS NOT NULL)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@reference", customerReference);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var convertedOrdinal = reader.GetOrdinal("demo_converted_at");
        var kindOrdinal = reader.GetOrdinal("demo_kind");
        var profileOrdinal = reader.GetOrdinal("profile_key");
        var groupsOrdinal = reader.GetOrdinal("ad_groups_json");
        var userOrdinal = reader.GetOrdinal("portal_user_id");

        return new DemoConversionCandidate(
            ReadIdentifier(reader, "id"),
            reader.GetString("external_reference"),
            reader.IsDBNull(userOrdinal)
                ? string.Empty
                : ReadIdentifier(reader, userOrdinal),
            reader.IsDBNull(kindOrdinal) ? string.Empty : reader.GetString(kindOrdinal),
            reader.IsDBNull(profileOrdinal) ? null : reader.GetString(profileOrdinal),
            ParseAdGroups(
                reader.IsDBNull(groupsOrdinal)
                    ? null
                    : reader.GetString(groupsOrdinal)),
            !reader.IsDBNull(convertedOrdinal));
    }

    public async Task MarkConvertedAsync(
        string customerId,
        DateTime convertedAtUtc,
        string? actorUserId,
        string? sourceProfileKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Bascule sur place : on leve les marqueurs de demo (ce qui sort le
        // compte du balayage d'expiration et de la purge) en conservant la
        // provenance. Le contenu et l'historique ne sont pas touches.
        command.CommandText =
            """
            UPDATE customers
            SET is_demo = FALSE,
                demo_source_profile_key = @source_profile_key,
                demo_profile_id = NULL,
                demo_kind = NULL,
                demo_expires_at = NULL,
                demo_converted_at = @converted_at,
                demo_converted_by_user_id = @actor
            WHERE id = @id
              AND demo_converted_at IS NULL;
            """;
        command.Parameters.AddWithValue("@source_profile_key", sourceProfileKey);
        command.Parameters.AddWithValue("@converted_at", convertedAtUtc);
        command.Parameters.AddWithValue("@actor", actorUserId);
        command.Parameters.AddWithValue("@id", customerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DemoAccountDeletionOutcome> DeleteDemoAccountAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Meme garde-fou que la purge a l'echeance : plutot conserver un compte
        // porteur de contenu metier que lever une erreur de cle etrangere ou le
        // supprimer a moitie.
        if (await HasUncoveredBusinessContentAsync(
                connection,
                customerId,
                cancellationToken))
        {
            return new DemoAccountDeletionOutcome(false, true);
        }

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await DeleteDemoCustomerAsync(
                connection,
                transaction,
                customerId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new DemoAccountDeletionOutcome(true, false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> CustomerReferenceTakenAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Les deux colonnes partagent le meme espace de noms : un code reserve
        // devient la reference d'OU du client a la conversion.
        command.CommandText =
            """
            SELECT EXISTS (
                SELECT 1
                FROM customers
                WHERE external_reference = @reference
                   OR koxo_group_reference = @reference
            );
            """;
        command.Parameters.AddWithValue("@reference", reference);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToBoolean(result, CultureInfo.InvariantCulture);
    }

    public async Task SetKoxoGroupReferenceAsync(
        string customerId,
        string groupReference,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Rattrapage seulement : on n'ecrase jamais un code deja reserve, sous
        // peine de deplacer une OU deja creee par KoXo.
        command.CommandText =
            """
            UPDATE customers
            SET koxo_group_reference = @group_reference
            WHERE id = @id
              AND koxo_group_reference IS NULL;
            """;
        command.Parameters.AddWithValue("@group_reference", groupReference);
        command.Parameters.AddWithValue("@id", customerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task MarkTrialTimestampAsync(
        string column,
        string customerId,
        DateTime valueUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Nom de colonne issu d'un litteral controle (jamais d'entree externe).
        command.CommandText =
            $"UPDATE customers SET {column} = @value WHERE id = @id AND is_demo = TRUE;";
        command.Parameters.AddWithValue("@value", valueUtc);
        command.Parameters.AddWithValue("@id", customerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Lit une colonne d'identifiant <c>CHAR(36)</c>.
    /// </summary>
    /// <remarks>
    /// MySqlConnector materialise ces colonnes en <see cref="Guid"/> et non en
    /// <see cref="string"/> : un <c>GetString</c> direct leve
    /// <see cref="InvalidCastException"/>. Meme convention que
    /// <c>MariaDbDownloadRepository</c>.
    /// </remarks>
    private static string ReadIdentifier(
        MySqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            throw new InvalidOperationException(
                $"The identifier column '{columnName}' cannot be null.");
        }

        return ReadIdentifier(reader, ordinal);
    }

    private static string ReadIdentifier(MySqlDataReader reader, int ordinal)
        => reader.GetValue(ordinal) switch
        {
            Guid guid => guid.ToString("D"),
            byte[] bytes when bytes.Length == 16 => new Guid(bytes).ToString("D"),
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            var raw => Convert.ToString(
                    raw,
                    System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty
        };

    private static IReadOnlyList<string> ParseAdGroups(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(json);
            if (parsed is null)
            {
                return Array.Empty<string>();
            }

            return parsed
                .Where(group => !string.IsNullOrWhiteSpace(group))
                .Select(group => group.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    public async Task<DemoPurgeResult> PurgeExpiredDemoCustomersAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var expired = await ListExpiredDemoCustomersAsync(
            connection,
            nowUtc,
            cancellationToken);

        var purgedCount = 0;
        var skipped = new List<string>();

        foreach (var (customerId, externalReference) in expired)
        {
            if (await HasUncoveredBusinessContentAsync(
                    connection,
                    customerId,
                    cancellationToken))
            {
                // Garde-fou : ne jamais lever une erreur FK ni supprimer a
                // moitie un compte qui porte du contenu hors cascade actuelle.
                skipped.Add(externalReference);
                continue;
            }

            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await DeleteDemoCustomerAsync(
                    connection,
                    transaction,
                    customerId,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                purgedCount++;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return new DemoPurgeResult(purgedCount, skipped);
    }

    private static async Task InsertCustomerAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        DemoAccountCreationSpec spec,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO customers (
                id, external_reference, display_name, status, customer_type,
                is_demo, demo_profile_id, demo_kind, demo_expires_at,
                demo_created_by_user_id, koxo_group_reference,
                created_at, updated_at
            ) VALUES (
                @id, @external_reference, @display_name, 'active', @customer_type,
                TRUE, @demo_profile_id, @demo_kind, @demo_expires_at,
                @demo_created_by_user_id, @koxo_group_reference,
                UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", spec.CustomerId);
        command.Parameters.AddWithValue(
            "@external_reference",
            spec.ExternalReference);
        command.Parameters.AddWithValue("@display_name", spec.DisplayName);
        command.Parameters.AddWithValue("@customer_type", spec.CustomerType);
        command.Parameters.AddWithValue("@demo_profile_id", spec.DemoProfileId);
        command.Parameters.AddWithValue("@demo_kind", spec.DemoKind);
        command.Parameters.AddWithValue(
            "@demo_expires_at",
            (object?)spec.DemoExpiresAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@demo_created_by_user_id",
            (object?)spec.DemoCreatedByUserId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@koxo_group_reference",
            (object?)spec.KoxoGroupReference ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPortalUserAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        DemoAccountCreationSpec spec,
        CancellationToken cancellationToken)
    {
        // Identifiant KoXo alloue comme a l'inscription : sans lui, le compte est
        // rejete par la validation de l'export et bloque la synchronisation
        // globale — il ne pourrait donc jamais recevoir d'identite AD.
        var koxoUniqueIdentifier = await KoxoIdentifierAllocator.AllocateAsync(
            connection,
            transaction,
            cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO portal_users (
                id, customer_id, identity_provider_subject, email,
                password_hash, display_name, status, role,
                personal_title, given_name, surname, birth_date,
                koxo_unique_identifier, created_at, updated_at
            ) VALUES (
                @id, @customer_id, @subject, @email,
                @password_hash, @display_name, 'active', @role,
                @personal_title, @given_name, @surname, @birth_date,
                @koxo_unique_identifier, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", spec.PortalUserId);
        command.Parameters.AddWithValue("@customer_id", spec.CustomerId);
        command.Parameters.AddWithValue(
            "@subject",
            $"demo-{spec.PortalUserId}");
        command.Parameters.AddWithValue("@email", spec.Email);
        command.Parameters.AddWithValue("@password_hash", spec.PasswordHash);
        command.Parameters.AddWithValue("@display_name", spec.UserDisplayName);
        command.Parameters.AddWithValue("@role", PortalRoles.ClientUser);
        command.Parameters.AddWithValue(
            "@personal_title",
            (object?)spec.PersonalTitle ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@given_name",
            (object?)spec.GivenName ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@surname",
            (object?)spec.Surname ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@birth_date",
            spec.BirthDate.HasValue
                ? spec.BirthDate.Value.ToDateTime(TimeOnly.MinValue)
                : (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "@koxo_unique_identifier",
            koxoUniqueIdentifier);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertServiceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string customerId,
        DemoServiceSeed service,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO customer_services (
                id, customer_id, external_reference, service_type, name,
                status, description, started_at, scope, commercial_terms,
                created_at, updated_at
            ) VALUES (
                @id, @customer_id, @external_reference, @service_type, @name,
                'active', @description, UTC_TIMESTAMP(6), @scope,
                @commercial_terms, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@customer_id", customerId);
        command.Parameters.AddWithValue(
            "@external_reference",
            $"DEMO-{Guid.NewGuid():N}"[..24]);
        command.Parameters.AddWithValue("@service_type", service.ServiceType);
        command.Parameters.AddWithValue("@name", service.Name);
        command.Parameters.AddWithValue("@description", service.Description);
        command.Parameters.AddWithValue("@scope", service.Scope);
        command.Parameters.AddWithValue(
            "@commercial_terms",
            service.CommercialTerms);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<(string CustomerId, string ExternalReference)>>
        ListExpiredDemoCustomersAsync(
            MySqlConnection connection,
            DateTime nowUtc,
            CancellationToken cancellationToken)
    {
        var results = new List<(string, string)>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, external_reference
            FROM customers
            WHERE is_demo = TRUE
              AND demo_expires_at IS NOT NULL
              AND demo_expires_at < @now
            ORDER BY demo_expires_at;
            """;
        command.Parameters.AddWithValue("@now", nowUtc);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add((
                ReadIdentifier(reader, "id"),
                reader.GetString("external_reference")));
        }

        return results;
    }

    private static async Task<bool> HasUncoveredBusinessContentAsync(
        MySqlConnection connection,
        string customerId,
        CancellationToken cancellationToken)
    {
        // customer_services est desormais gere par la cascade ; on ne compte
        // donc que le contenu metier NON encore couvert. Toute valeur > 0
        // -> on saute ce compte plutot que de risquer une erreur FK.
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                (SELECT COUNT(*) FROM invoices WHERE customer_id = @id)
              + (SELECT COUNT(*) FROM support_requests WHERE customer_id = @id)
              + (SELECT COUNT(*) FROM service_requests WHERE customer_id = @id)
              + (SELECT COUNT(*) FROM commercial_documents WHERE customer_id = @id)
              + (SELECT COUNT(*) FROM subscriptions WHERE customer_id = @id)
              + (SELECT COUNT(*) FROM ad_actions WHERE customer_id = @id)
              + (SELECT COUNT(*) FROM bpce_customers WHERE customer_id = @id)
              + (SELECT COUNT(*) FROM cart_items WHERE customer_id = @id)
              + (SELECT COUNT(*) FROM recurring_checkout WHERE customer_id = @id)
              + (SELECT COUNT(*) FROM portal_notifications WHERE customer_id = @id)
                AS content_count;
            """;
        command.Parameters.AddWithValue("@id", customerId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value) > 0;
    }

    private static async Task DeleteDemoCustomerAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string customerId,
        CancellationToken cancellationToken)
    {
        // Ordre FK-safe : sessions -> services -> liens AD -> users -> client.
        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE ps
            FROM portal_sessions ps
            INNER JOIN portal_users pu ON pu.id = ps.user_id
            WHERE pu.customer_id = @id;
            """,
            customerId,
            cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM customer_services WHERE customer_id = @id;",
            customerId,
            cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM customer_ad_links WHERE customer_id = @id;",
            customerId,
            cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM portal_users WHERE customer_id = @id;",
            customerId,
            cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM customers WHERE id = @id;",
            customerId,
            cancellationToken);
    }

    private static async Task ExecuteAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string sql,
        string customerId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", customerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            .ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture);
}
