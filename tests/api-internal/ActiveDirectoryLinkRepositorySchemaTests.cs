using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Adoption d'une identite Active Directory recreee, sur MariaDB reelle.
///
/// Cette suite exige une MariaDB JETABLE, fournie explicitement par
/// API_INTERNAL_TEST_MARIADB_CONNECTION (a defaut
/// BILLING_V2_TEST_MARIADB_CONNECTION). Sans base, elle echoue en le disant :
/// elle n'est jamais silencieusement "verte" par absence de base.
///
/// Elle ne peut pas tourner en persistance mock : le defaut couvert ici est
/// l'omission d'une colonne dans un UPDATE SQL, et le depot mock reconstruit
/// l'enregistrement entier a chaque ecriture. Cette classe de bug lui est donc
/// structurellement invisible.
///
/// Ne JAMAIS pointer ces variables vers une base de recette ou de production.
/// </summary>
public static class ActiveDirectoryLinkRepositorySchemaTests
{
    private const string ConnectionVariable =
        "API_INTERNAL_TEST_MARIADB_CONNECTION";
    private const string FallbackConnectionVariable =
        "BILLING_V2_TEST_MARIADB_CONNECTION";

    public static async Task RunAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable(
                FallbackConnectionVariable);
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Ni {ConnectionVariable} ni {FallbackConnectionVariable} n'est "
                + "defini. Cette suite exige une MariaDB jetable portant les "
                + "migrations 001 a 063. Elle ne peut pas etre consideree comme "
                + "passee sans base.");
        }

        var repository = new MariaDbActiveDirectoryLinkRepository(
            new SqlRuntimeConfiguration(
                PortalPersistenceMode.MariaDb,
                "mariadb",
                connectionString,
                "TEST",
                ConfigurationValid: true));

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        var fixture = await LinkFixture.CreateAsync(connection);
        try
        {
            await VerifyRecreatedIdentityIsAdoptedInPlaceAsync(
                connection, repository, fixture);
            await VerifyConflictingAdoptionIsRefusedAsync(
                connection, repository, fixture);
        }
        finally
        {
            await fixture.CleanupAsync(connection);
        }
    }

    /// <summary>
    /// P est deja lie a OLD. On l'adopte sur NEW : meme ligne, GUID repris,
    /// SID/SAM/DN a jour, aucun second lien.
    /// </summary>
    private static async Task VerifyRecreatedIdentityIsAdoptedInPlaceAsync(
        MySqlConnection connection,
        MariaDbActiveDirectoryLinkRepository repository,
        LinkFixture fixture)
    {
        var oldGuid = Guid.NewGuid().ToString("D");
        var newGuid = Guid.NewGuid().ToString("D");

        var created = await repository.UpsertPortalUserLinkAsync(
            fixture.CustomerReference,
            fixture.PortalUserId,
            actorUserId: null,
            DirectoryObject(oldGuid, "ancien", fixture.CustomerReference),
            adDomain: "clients.home.bzh",
            adProvisioningStatus: "provisioned",
            adProvisionedAtUtc: DateTime.UtcNow,
            lastPasswordSyncStatus: null,
            lastPasswordSyncAtUtc: null,
            koxoExportStatus: "exported",
            CancellationToken.None);

        var adopted = await repository.UpsertPortalUserLinkAsync(
            fixture.CustomerReference,
            fixture.PortalUserId,
            actorUserId: null,
            DirectoryObject(newGuid, "nouveau", fixture.CustomerReference),
            adDomain: "clients.home.bzh",
            adProvisioningStatus: "provisioned",
            adProvisionedAtUtc: DateTime.UtcNow,
            lastPasswordSyncStatus: null,
            lastPasswordSyncAtUtc: null,
            koxoExportStatus: "exported",
            CancellationToken.None);

        Ensure(
            string.Equals(created.Id, adopted.Id, StringComparison.Ordinal),
            "L'adoption doit reconduire la meme ligne customer_ad_links.");

        Ensure(
            await ScalarLongAsync(
                connection,
                "SELECT COUNT(*) FROM customer_ad_links "
                + "WHERE portal_user_id = @p",
                fixture.PortalUserId) == 1,
            "L'adoption ne doit jamais creer un second lien pour P.");

        var stored = await ReadLinkAsync(connection, adopted.Id);

        // Le defaut corrige : le GUID restait celui de l'objet supprime pendant
        // que tout le reste devenait celui du nouvel objet, laissant un lien
        // qui designe une identite annuaire disparue.
        Ensure(
            string.Equals(stored.ObjectGuid, newGuid, StringComparison.OrdinalIgnoreCase),
            "Le lien reconduit doit porter le nouvel objectGUID.");
        Ensure(
            string.Equals(stored.ObjectSid, "S-1-5-21-nouveau", StringComparison.Ordinal),
            "Le SID doit etre celui de la nouvelle identite.");
        Ensure(
            string.Equals(stored.SamAccountName, "nouveau", StringComparison.Ordinal),
            "Le sAMAccountName doit etre celui de la nouvelle identite.");
        Ensure(
            stored.DistinguishedName.Contains("nouveau", StringComparison.Ordinal),
            "Le DN doit etre celui de la nouvelle identite.");

        // L'ancien GUID ne doit plus etre porte par aucun lien : sinon le
        // triplet GUID/SID/SAM ne designerait plus un objet unique.
        Ensure(
            await ScalarLongAsync(
                connection,
                "SELECT COUNT(*) FROM customer_ad_links WHERE object_guid = @p",
                oldGuid) == 0,
            "L'ancien objectGUID ne doit plus subsister.");
    }

    /// <summary>
    /// P porte OLD, une autre ligne porte deja NEW : la fusion est refusee et
    /// rien n'est ecrit.
    /// </summary>
    private static async Task VerifyConflictingAdoptionIsRefusedAsync(
        MySqlConnection connection,
        MariaDbActiveDirectoryLinkRepository repository,
        LinkFixture fixture)
    {
        var contestedGuid = Guid.NewGuid().ToString("D");

        // La ligne concurrente est posee sur l'autre utilisateur portail du
        // meme client : le refus ne doit rien devoir a une frontiere client,
        // qui est deja couverte par PortalAccessDeniedException.
        var other = await repository.UpsertPortalUserLinkAsync(
            fixture.CustomerReference,
            fixture.OtherPortalUserId,
            actorUserId: null,
            DirectoryObject(contestedGuid, "convoite", fixture.CustomerReference),
            adDomain: null,
            adProvisioningStatus: null,
            adProvisionedAtUtc: null,
            lastPasswordSyncStatus: null,
            lastPasswordSyncAtUtc: null,
            koxoExportStatus: null,
            CancellationToken.None);

        var mine = await ReadLinkByPortalUserAsync(connection, fixture.PortalUserId);

        var refused = false;
        try
        {
            await repository.UpsertPortalUserLinkAsync(
                fixture.CustomerReference,
                fixture.PortalUserId,
                actorUserId: null,
                DirectoryObject(contestedGuid, "convoite", fixture.CustomerReference),
                adDomain: null,
                adProvisioningStatus: null,
                adProvisionedAtUtc: null,
                lastPasswordSyncStatus: null,
                lastPasswordSyncAtUtc: null,
                koxoExportStatus: null,
                CancellationToken.None);
        }
        catch (AmbiguousAdLinkException exception)
        {
            refused = true;
            Ensure(
                string.Equals(
                    exception.PortalUserLinkId, mine.Id, StringComparison.Ordinal)
                && string.Equals(
                    exception.ObjectGuidLinkId, other.Id, StringComparison.Ordinal),
                "Le refus doit nommer les deux liens en conflit.");
        }

        Ensure(
            refused,
            "Deux liens distincts revendiquant la meme adoption doivent etre "
            + "refuses, pas fusionnes ni laisses a une violation d'unicite.");

        // Rollback : les deux liens doivent etre exactement dans leur etat
        // d'avant la tentative.
        var mineAfter = await ReadLinkByPortalUserAsync(
            connection, fixture.PortalUserId);
        Ensure(
            string.Equals(mineAfter.Id, mine.Id, StringComparison.Ordinal)
            && string.Equals(
                mineAfter.ObjectGuid, mine.ObjectGuid, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                mineAfter.SamAccountName, mine.SamAccountName, StringComparison.Ordinal),
            "Le lien de P doit rester intact apres un refus.");

        var otherAfter = await ReadLinkAsync(connection, other.Id);
        Ensure(
            string.Equals(
                otherAfter.ObjectGuid, contestedGuid, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                otherAfter.PortalUserId,
                fixture.OtherPortalUserId,
                StringComparison.Ordinal),
            "Le lien concurrent ne doit pas avoir ete repris.");

        Ensure(
            await ScalarLongAsync(
                connection,
                "SELECT COUNT(*) FROM customer_ad_links WHERE object_guid = @p",
                contestedGuid) == 1,
            "L'objet convoite ne doit rester porte que par un seul lien.");
    }

    private static AdDirectoryObjectSummary DirectoryObject(
        string objectGuid,
        string samAccountName,
        string customerReference)
        => new(
            objectGuid,
            $"S-1-5-21-{samAccountName}",
            "user",
            samAccountName,
            $"{samAccountName}@clients.home.bzh",
            samAccountName,
            $"CN={samAccountName},OU={customerReference},OU=KoXoAdm,"
            + "DC=clients,DC=home,DC=bzh",
            customerReference,
            IsDisabled: false);

    private sealed record StoredLink(
        string Id,
        string ObjectGuid,
        string ObjectSid,
        string SamAccountName,
        string DistinguishedName,
        string PortalUserId);

    private static async Task<StoredLink> ReadLinkAsync(
        MySqlConnection connection,
        string linkId)
        => await ReadLinkCoreAsync(
            connection,
            "SELECT id, object_guid, object_sid, sam_account_name, "
            + "distinguished_name, portal_user_id FROM customer_ad_links "
            + "WHERE id = @p",
            linkId);

    private static async Task<StoredLink> ReadLinkByPortalUserAsync(
        MySqlConnection connection,
        string portalUserId)
        => await ReadLinkCoreAsync(
            connection,
            "SELECT id, object_guid, object_sid, sam_account_name, "
            + "distinguished_name, portal_user_id FROM customer_ad_links "
            + "WHERE portal_user_id = @p AND object_type = 'user'",
            portalUserId);

    private static async Task<StoredLink> ReadLinkCoreAsync(
        MySqlConnection connection,
        string sql,
        string parameter)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@p", parameter);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException(
                "Le lien attendu est introuvable.");
        }

        // MySqlConnector materialise les colonnes CHAR(36) en Guid : passer par
        // reader.GetString leverait.
        return new StoredLink(
            ReadIdentifier(reader, "id")!,
            ReadIdentifier(reader, "object_guid")!,
            reader.GetString("object_sid"),
            reader.GetString("sam_account_name"),
            reader.GetString("distinguished_name"),
            ReadIdentifier(reader, "portal_user_id")!);
    }

    private static string? ReadIdentifier(MySqlDataReader reader, string column)
    {
        var value = reader.GetValue(reader.GetOrdinal(column));
        return value switch
        {
            null or DBNull => null,
            Guid guid => guid.ToString("D"),
            _ => value.ToString()
        };
    }

    private sealed record LinkFixture(
        string CustomerId,
        string CustomerReference,
        string PortalUserId,
        string OtherPortalUserId)
    {
        public static async Task<LinkFixture> CreateAsync(
            MySqlConnection connection)
        {
            var marker = "ADLINK-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            var customerId = Guid.NewGuid().ToString("D");
            var portalUserId = Guid.NewGuid().ToString("D");
            var otherPortalUserId = Guid.NewGuid().ToString("D");

            await ExecuteAsync(
                connection,
                "INSERT INTO customers (id, external_reference, display_name, "
                + "status, created_at, updated_at) VALUES (@id, @ref, "
                + "'Client adoption AD', 'active', UTC_TIMESTAMP(6), "
                + "UTC_TIMESTAMP(6))",
                ("@id", customerId),
                ("@ref", marker));

            await InsertPortalUserAsync(
                connection, portalUserId, customerId, marker, "a");
            await InsertPortalUserAsync(
                connection, otherPortalUserId, customerId, marker, "b");

            return new LinkFixture(
                customerId, marker, portalUserId, otherPortalUserId);
        }

        private static Task InsertPortalUserAsync(
            MySqlConnection connection,
            string portalUserId,
            string customerId,
            string marker,
            string suffix)
            => ExecuteAsync(
                connection,
                "INSERT INTO portal_users (id, customer_id, "
                + "identity_provider_subject, email, display_name, status, "
                + "role, created_at, updated_at) VALUES (@id, @cust, @subject, "
                + "@email, 'Utilisateur adoption', 'active', 'customer_admin', "
                + "UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", portalUserId),
                ("@cust", customerId),
                ("@subject", $"adlink-{marker}-{suffix}"),
                ("@email", $"{marker}-{suffix}@example.invalid".ToLowerInvariant()));

        public async Task CleanupAsync(MySqlConnection connection)
        {
            await ExecuteAsync(
                connection,
                "DELETE FROM customer_ad_links WHERE customer_id = @id",
                ("@id", CustomerId));
            await ExecuteAsync(
                connection,
                "DELETE FROM portal_users WHERE customer_id = @id",
                ("@id", CustomerId));
            await ExecuteAsync(
                connection,
                "DELETE FROM customers WHERE id = @id",
                ("@id", CustomerId));
        }
    }

    private static async Task ExecuteAsync(
        MySqlConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarLongAsync(
        MySqlConnection connection,
        string sql,
        string parameter)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@p", parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
