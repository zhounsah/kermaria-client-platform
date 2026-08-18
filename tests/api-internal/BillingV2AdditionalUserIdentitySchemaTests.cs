using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Invariants base reelle du cycle de vie des utilisateurs additionnels
/// Billing V2 (Phase 4).
/// </summary>
/// <remarks>
/// <para>
/// Cette suite exige une MariaDB <b>JETABLE</b>, fournie par
/// <c>BILLING_V2_TEST_MARIADB_CONNECTION</c>, portant les migrations 001 a 065.
/// Sans elle, la suite ne s'execute pas et le dit : elle n'est jamais
/// silencieusement verte par absence de base. Ne JAMAIS la pointer vers une
/// base de recette ou de production.
/// </para>
/// <para>
/// Ce qui ne peut se prouver qu'ici : le comportement reel de la clause
/// d'export KoXo, l'unicite portee par les index, la serialisation de
/// l'attribution et l'annulation complete en cas d'echec. Les suites mock
/// n'ont ni index unique, ni <c>FOR UPDATE</c>, ni ROLLBACK.
/// </para>
/// </remarks>
public static class BillingV2AdditionalUserIdentitySchemaTests
{
    private const string ConnectionVariable =
        "BILLING_V2_TEST_MARIADB_CONNECTION";

    public static async Task RunAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} n'est pas defini. Cette suite exige une "
                + "MariaDB jetable portant les migrations 001 a 065. Elle ne "
                + "peut pas etre consideree comme passee sans base.");
        }

        await RunAsync(connectionString);
    }

    public static async Task RunAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        await VerifySchemaShapeAsync(connection);

        var fixture = await Fixture.CreateAsync(connection);
        try
        {
            await VerifyUnknownLifecycleStatusIsRejectedAsync(connection, fixture);
            await VerifyOneLifecyclePerSlotAsync(connection, fixture);
            await VerifyOneLifecyclePerPortalUserAsync(connection, fixture);
            await VerifyTokenHashIsUniqueAsync(connection, fixture);

            await VerifyPayingUserWithoutLinkNorLifecycleIsExcludedAsync(
                connection,
                fixture);
            await VerifyKoxoPendingLifecycleIsIncludedAsync(connection, fixture);
            await VerifyAwaitingPasswordIsExcludedAsync(connection, fixture);
            await VerifyFailedAndDisabledAreExcludedAsync(connection, fixture);
            await VerifyDirectoryReadyWithoutLinkIsIncludedAsync(
                connection,
                fixture);
            await VerifyReadyWithoutLinkIsExcludedAsync(connection, fixture);
            await VerifyLifecycleOfAnotherUserGrantsNothingAsync(
                connection,
                fixture);
            await VerifyForeignCustomerLifecycleIsExcludedAsync(
                connection,
                fixture);
            await VerifyInactiveSubscriptionIsExcludedAsync(connection, fixture);
            await VerifyPrimarySlotIsExcludedAsync(connection, fixture);
            await VerifyMissingEntitlementRuleExcludesAsync(connection, fixture);
            await VerifyIncompleteCivilStatusIsExcludedAsync(connection, fixture);
            await VerifyLinkedUserIsIncludedThroughTheNormalBranchAsync(
                connection,
                fixture);
            await VerifyPendingPasswordIsPersistedRereadableAndAckedAsync(
                connection,
                fixture,
                connectionString);
        }
        finally
        {
            await fixture.CleanupAsync(connection);
        }
    }

    // ==================================================================
    // Forme du schema
    // ==================================================================

    private static async Task VerifySchemaShapeAsync(MySqlConnection connection)
    {
        foreach (var (table, column) in new[]
        {
            ("billing_v2_user_identity_provisioning", "subscription_user_id"),
            ("billing_v2_user_identity_provisioning", "subscription_id"),
            ("billing_v2_user_identity_provisioning", "customer_id"),
            ("billing_v2_user_identity_provisioning", "portal_user_id"),
            ("billing_v2_user_identity_provisioning", "koxo_unique_identifier"),
            ("billing_v2_user_identity_provisioning", "status"),
            ("billing_v2_user_identity_provisioning", "directory_object_guid"),
            ("portal_user_password_setups", "portal_user_id"),
            ("portal_user_password_setups", "token_hash"),
            ("portal_user_password_setups", "expires_at"),
            ("portal_user_password_setups", "consumed_at"),
            ("portal_user_password_setups", "superseded_at"),
            ("koxo_pending_directory_passwords", "ciphertext"),
            ("koxo_pending_directory_passwords", "key_id"),
            ("koxo_pending_directory_passwords", "expires_at"),
            ("koxo_pending_directory_passwords", "published_count")
        })
        {
            var present = await ScalarLongAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_schema = DATABASE()
                  AND table_name = @table
                  AND column_name = @column;
                """,
                ("@table", table),
                ("@column", column));
            Ensure(
                present == 1,
                $"La migration 065 doit avoir cree {table}.{column}.");
        }

        // Le jeton en clair ne doit avoir aucune colonne ou vivre.
        var plaintextColumns = await ScalarLongAsync(
            connection,
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'portal_user_password_setups'
              AND column_name IN ('token', 'raw_token', 'token_plain');
            """);
        Ensure(
            plaintextColumns == 0,
            "Aucune colonne ne doit pouvoir accueillir un jeton en clair.");

        // Meme regle pour le relais de mot de passe : la seule colonne de
        // contenu est un chiffre authentifie, jamais un clair ni un simple
        // condensat — KoXo a besoin du mot de passe reel.
        var passwordColumns = await ScalarLongAsync(
            connection,
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'koxo_pending_directory_passwords'
              AND column_name IN ('password', 'plaintext', 'password_hash');
            """);
        Ensure(
            passwordColumns == 0,
            "Le relais de mot de passe ne stocke ni clair ni condensat.");
    }

    // ==================================================================
    // Unicites structurelles
    // ==================================================================

    private static async Task VerifyUnknownLifecycleStatusIsRejectedAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        await ExpectRejectionAsync(
            connection,
            $"""
            INSERT INTO billing_v2_user_identity_provisioning (
                id, subscription_user_id, subscription_id, customer_id,
                portal_user_id, koxo_unique_identifier, status
            ) VALUES (
                '{Guid.NewGuid():D}', @slot, @subscription, @customer,
                @portal_user, 'CLI-900001', 'inconnu'
            );
            """,
            "un statut de cycle de vie hors vocabulaire",
            ("@slot", fixture.SlotId),
            ("@subscription", fixture.SubscriptionId),
            ("@customer", fixture.CustomerId),
            ("@portal_user", fixture.PortalUserId));
    }

    private static async Task VerifyOneLifecyclePerSlotAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        // Le cycle de vie de reference existe deja (cree par le fixture) :
        // une seconde ligne sur la meme place est un doublon d'attribution.
        var otherPortalUserId = await fixture.CreatePortalUserAsync(
            connection,
            "doublon-place@example.invalid",
            "CLI-900010");
        await ExpectRejectionAsync(
            connection,
            $"""
            INSERT INTO billing_v2_user_identity_provisioning (
                id, subscription_user_id, subscription_id, customer_id,
                portal_user_id, koxo_unique_identifier, status
            ) VALUES (
                '{Guid.NewGuid():D}', @slot, @subscription, @customer,
                @portal_user, 'CLI-900010', 'awaiting_password'
            );
            """,
            "deux cycles de vie sur une meme place",
            ("@slot", fixture.SlotId),
            ("@subscription", fixture.SubscriptionId),
            ("@customer", fixture.CustomerId),
            ("@portal_user", otherPortalUserId));
    }

    private static async Task VerifyOneLifecyclePerPortalUserAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        var otherSlotId = await fixture.CreateSlotAsync(
            connection,
            isPrimary: false);
        await ExpectRejectionAsync(
            connection,
            $"""
            INSERT INTO billing_v2_user_identity_provisioning (
                id, subscription_user_id, subscription_id, customer_id,
                portal_user_id, koxo_unique_identifier, status
            ) VALUES (
                '{Guid.NewGuid():D}', @slot, @subscription, @customer,
                @portal_user, 'CLI-900011', 'awaiting_password'
            );
            """,
            "deux places revendiquant le meme utilisateur portail",
            ("@slot", otherSlotId),
            ("@subscription", fixture.SubscriptionId),
            ("@customer", fixture.CustomerId),
            ("@portal_user", fixture.PortalUserId));
    }

    private static async Task VerifyTokenHashIsUniqueAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        var hash = new string('a', 64);
        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO portal_user_password_setups (
                id, portal_user_id, purpose, token_hash, expires_at
            ) VALUES (
                '{Guid.NewGuid():D}', @portal_user, 'test', @hash,
                UTC_TIMESTAMP(6) + INTERVAL 1 DAY
            );
            """,
            ("@portal_user", fixture.PortalUserId),
            ("@hash", hash));

        await ExpectRejectionAsync(
            connection,
            $"""
            INSERT INTO portal_user_password_setups (
                id, portal_user_id, purpose, token_hash, expires_at
            ) VALUES (
                '{Guid.NewGuid():D}', @portal_user, 'test', @hash,
                UTC_TIMESTAMP(6) + INTERVAL 1 DAY
            );
            """,
            "deux jetons portant le meme condensat",
            ("@portal_user", fixture.PortalUserId),
            ("@hash", hash));
    }

    // ==================================================================
    // Regle d'export KoXo
    // ==================================================================

    private static async Task
        VerifyPayingUserWithoutLinkNorLifecycleIsExcludedAsync(
            MySqlConnection connection,
            Fixture fixture)
    {
        await fixture.SetLifecycleStatusAsync(connection, "awaiting_password");
        var orphan = await fixture.CreatePortalUserAsync(
            connection,
            "sans-cycle@example.invalid",
            "CLI-900020");

        Ensure(
            !await IsExportCandidateAsync(connection, orphan),
            "Un client payant sans lien AD ni cycle de vie reste exclu : c'est "
            + "la regle fail-closed de base, et la relacher exporterait tout "
            + "compte dont le provisioning a echoue.");
    }

    private static async Task VerifyKoxoPendingLifecycleIsIncludedAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        await fixture.SetLifecycleStatusAsync(connection, "koxo_pending");

        Ensure(
            await IsExportCandidateAsync(connection, fixture.PortalUserId),
            "Un utilisateur additionnel en koxo_pending est exporte : c'est la "
            + "seule facon de sortir de la circularite, KoXo devant creer "
            + "l'objet avant que customer_ad_links puisse exister.");
    }

    private static async Task VerifyAwaitingPasswordIsExcludedAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        await fixture.SetLifecycleStatusAsync(connection, "awaiting_password");

        Ensure(
            !await IsExportCandidateAsync(connection, fixture.PortalUserId),
            "Avant le mot de passe, rien ne part : le CSV le porte en colonne "
            + "14, et un compte cree sans lui echapperait a l'application.");
    }

    private static async Task VerifyFailedAndDisabledAreExcludedAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        foreach (var status in new[] { "failed", "disabled" })
        {
            await fixture.SetLifecycleStatusAsync(connection, status);
            Ensure(
                !await IsExportCandidateAsync(connection, fixture.PortalUserId),
                $"Un cycle de vie « {status} » n'autorise aucune creation.");
        }
    }

    /// <summary>
    /// Fenetre de crash : objet annuaire resolu, lien pas encore ecrit.
    /// </summary>
    /// <remarks>
    /// C'est l'etat que le service persiste <b>avant</b> d'ecrire le lien. Une
    /// interruption a cet instant — redemarrage, panne reseau, arret du
    /// service — laisse une ligne <c>directory_ready</c> sans
    /// <c>customer_ad_links</c>. Si l'export l'excluait, l'identite sortirait
    /// du CSV, ce qui <b>desactive</b> le compte AD correspondant, et rien ne
    /// permettrait d'y revenir : le compte est desactive, donc jamais relie,
    /// donc jamais reexporte.
    /// </remarks>
    private static async Task VerifyDirectoryReadyWithoutLinkIsIncludedAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        await fixture.SetLifecycleStatusAsync(connection, "directory_ready");

        Ensure(
            await IsExportCandidateAsync(connection, fixture.PortalUserId),
            "Un cycle de vie « directory_ready » sans lien AD reste exporte : "
            + "c'est la fenetre de crash entre la resolution de l'objet et "
            + "l'ecriture du lien, et l'en exclure desactiverait le compte "
            + "sans retour possible.");
    }

    private static async Task VerifyReadyWithoutLinkIsExcludedAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        // ready sans lien est un etat incoherent : ready signifie precisement
        // que le lien a ete relu et confirme. L'inclure ici reviendrait a
        // exporter sur la foi d'un statut au lieu d'un fait.
        await fixture.SetLifecycleStatusAsync(connection, "ready");

        Ensure(
            !await IsExportCandidateAsync(connection, fixture.PortalUserId),
            "Un cycle de vie « ready » sans lien AD n'est pas exporte par "
            + "l'exception : a ce stade, c'est le lien qui fait foi.");
    }

    private static async Task VerifyLifecycleOfAnotherUserGrantsNothingAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        await fixture.SetLifecycleStatusAsync(connection, "koxo_pending");
        var stranger = await fixture.CreatePortalUserAsync(
            connection,
            "voisin@example.invalid",
            "CLI-900021");

        Ensure(
            !await IsExportCandidateAsync(connection, stranger),
            "Le cycle de vie d'une autre personne n'autorise rien : "
            + "l'exception designe un portal_user, elle ne couvre pas ses "
            + "voisins.");
    }

    private static async Task VerifyForeignCustomerLifecycleIsExcludedAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        await fixture.SetLifecycleStatusAsync(connection, "koxo_pending");
        await ExecuteAsync(
            connection,
            """
            UPDATE billing_v2_user_identity_provisioning
            SET customer_id = @other
            WHERE subscription_user_id = @slot;
            """,
            ("@other", fixture.OtherCustomerId),
            ("@slot", fixture.SlotId));

        var included = await IsExportCandidateAsync(
            connection,
            fixture.PortalUserId);

        await ExecuteAsync(
            connection,
            """
            UPDATE billing_v2_user_identity_provisioning
            SET customer_id = @customer
            WHERE subscription_user_id = @slot;
            """,
            ("@customer", fixture.CustomerId),
            ("@slot", fixture.SlotId));

        Ensure(
            !included,
            "Un cycle de vie rattache a un autre client n'autorise rien : le "
            + "customer_id doit etre identique aux trois niveaux.");
    }

    private static async Task VerifyInactiveSubscriptionIsExcludedAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        await fixture.SetLifecycleStatusAsync(connection, "koxo_pending");
        await fixture.SetSubscriptionStatusAsync(connection, "past_due");

        var included = await IsExportCandidateAsync(
            connection,
            fixture.PortalUserId);
        await fixture.SetSubscriptionStatusAsync(connection, "active");

        Ensure(
            !included,
            "Un abonnement hors etat provisionnable n'ouvre aucune creation "
            + "d'identite.");
    }

    private static async Task VerifyPrimarySlotIsExcludedAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        await fixture.SetLifecycleStatusAsync(connection, "koxo_pending");
        await fixture.SetSlotPrimaryAsync(connection, isPrimary: true);

        var included = await IsExportCandidateAsync(
            connection,
            fixture.PortalUserId);
        await fixture.SetSlotPrimaryAsync(connection, isPrimary: false);

        Ensure(
            !included,
            "Une place primaire ne passe jamais par cette exception : elle "
            + "designe le contact principal, dont l'identite suit le parcours "
            + "d'inscription.");
    }

    private static async Task VerifyMissingEntitlementRuleExcludesAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        await fixture.SetLifecycleStatusAsync(connection, "koxo_pending");
        await fixture.SetEntitlementRuleStatusAsync(connection, "inactive");

        var included = await IsExportCandidateAsync(
            connection,
            fixture.PortalUserId);
        await fixture.SetEntitlementRuleStatusAsync(connection, "active");

        Ensure(
            !included,
            "Sans regle USER-ADDITIONAL active, la place n'est adossee a aucun "
            + "droit et n'ouvre aucune creation d'identite.");
    }

    private static async Task VerifyIncompleteCivilStatusIsExcludedAsync(
        MySqlConnection connection,
        Fixture fixture)
    {
        await fixture.SetLifecycleStatusAsync(connection, "koxo_pending");
        await ExecuteAsync(
            connection,
            "UPDATE portal_users SET birth_date = NULL WHERE id = @id;",
            ("@id", fixture.PortalUserId));

        var included = await IsExportCandidateAsync(
            connection,
            fixture.PortalUserId);
        await ExecuteAsync(
            connection,
            "UPDATE portal_users SET birth_date = '1990-04-12' WHERE id = @id;",
            ("@id", fixture.PortalUserId));

        Ensure(
            !included,
            "Un etat civil incomplet reste dehors : il serait rejete par la "
            + "validation, et un seul invalide bloque l'export GLOBAL.");
    }

    private static async Task
        VerifyLinkedUserIsIncludedThroughTheNormalBranchAsync(
            MySqlConnection connection,
            Fixture fixture)
    {
        // Une fois le lien pose, l'inclusion ne depend plus du cycle de vie :
        // c'est ce qui garantit la continuite du CSV apres adoption. Une
        // rupture ici DESACTIVERAIT le compte, le CSV faisant autorite.
        await fixture.SetLifecycleStatusAsync(connection, "ready");
        await fixture.CreateAdLinkAsync(connection);

        Ensure(
            await IsExportCandidateAsync(connection, fixture.PortalUserId),
            "Apres adoption, l'utilisateur reste exporte par la branche "
            + "normale : disparaitre du CSV desactiverait son compte AD.");
    }

    // ==================================================================
    // Relais de mot de passe vers KoXo
    // ==================================================================

    /// <summary>
    /// Le secret survit au processus, se relit sans se consommer, et ne
    /// disparait qu'a l'acquittement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Trois choses ne se prouvent qu'ici : que le secret est bien ecrit en
    /// base (donc qu'il survit a un redemarrage de l'API), qu'il n'y figure
    /// jamais en clair, et qu'une relecture ne le detruit pas. Un magasin en
    /// memoire ne demontre aucune des trois.
    /// </para>
    /// <para>
    /// Un second magasin est construit pour la relecture : c'est la
    /// simulation la plus proche d'un redemarrage, puisqu'il ne partage aucun
    /// etat avec le premier.
    /// </para>
    /// </remarks>
    private static async Task
        VerifyPendingPasswordIsPersistedRereadableAndAckedAsync(
            MySqlConnection connection,
            Fixture fixture,
            string connectionString)
    {
        const string secret = "MotDePasseAssezLong!";
        var sql = new SqlRuntimeConfiguration(
            PortalPersistenceMode.MariaDb,
            "mariadb",
            connectionString,
            "TEST",
            ConfigurationValid: true);
        var protector = KoxoPendingPasswordProtector.TryCreate(
            Convert.ToBase64String(Enumerable.Repeat((byte)11, 32).ToArray()));
        Ensure(
            protector is not null,
            "La cle de test doit etre acceptee.");

        var store = NewStore(sql, protector, connectionString);
        Ensure(store.IsOperational, "Avec une cle valide, le magasin opere.");
        Ensure(
            await store.PublishAsync(
                fixture.PortalUserId,
                secret,
                CancellationToken.None),
            "La publication doit reussir.");

        // Le clair ne doit exister nulle part dans la ligne.
        var stored = await ScalarStringAsync(
            connection,
            """
            SELECT ciphertext
            FROM koxo_pending_directory_passwords
            WHERE portal_user_id = @id;
            """,
            ("@id", fixture.PortalUserId));
        Ensure(
            stored is not null
            && !stored.Contains(secret, StringComparison.Ordinal),
            "La colonne ne porte jamais le mot de passe en clair.");

        // Redemarrage simule : un magasin neuf relit la meme entree.
        var afterRestart = NewStore(sql, protector, connectionString);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Ensure(
                await afterRestart.PeekAsync(
                    fixture.PortalUserId,
                    CancellationToken.None) == secret,
                "Chaque instantane doit pouvoir relire le secret tant qu'il "
                + "n'est pas acquitte : c'est ce qui rend un crash avant ou "
                + "apres export reprenable.");
        }

        var reads = await ScalarLongAsync(
            connection,
            """
            SELECT published_count
            FROM koxo_pending_directory_passwords
            WHERE portal_user_id = @id;
            """,
            ("@id", fixture.PortalUserId));
        Ensure(
            reads == 3,
            "Les relectures sont comptees : un compteur qui grimpe signale un "
            + "cycle KoXo qui n'aboutit pas.");

        // Rotation de cle : ne jamais deviner. Un mot de passe faux applique a
        // un compte reel serait pire que pas de mot de passe du tout.
        var rotated = NewStore(
            sql,
            KoxoPendingPasswordProtector.TryCreate(
                Convert.ToBase64String(
                    Enumerable.Repeat((byte)12, 32).ToArray())),
            connectionString);
        Ensure(
            await rotated.PeekAsync(
                fixture.PortalUserId,
                CancellationToken.None) is null,
            "Une ligne scellee sous une autre cle est ignoree, jamais devinee.");

        // Fail-closed : sans cle, rien n'est retenu et rien n'est relu.
        var keyless = NewStore(sql, protector: null, connectionString);
        Ensure(
            !keyless.IsOperational
            && !await keyless.PublishAsync(
                fixture.PortalUserId,
                secret,
                CancellationToken.None)
            && await keyless.PeekAsync(
                fixture.PortalUserId,
                CancellationToken.None) is null,
            "Sans cle exploitable, le magasin refuse au lieu de retomber en "
            + "clair ou en memoire.");

        await afterRestart.AcknowledgeAsync(
            fixture.PortalUserId,
            CancellationToken.None);
        Ensure(
            await afterRestart.PeekAsync(
                fixture.PortalUserId,
                CancellationToken.None) is null
            && await ScalarLongAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM koxo_pending_directory_passwords
                WHERE portal_user_id = @id;
                """,
                ("@id", fixture.PortalUserId)) == 0,
            "L'acquittement efface l'entree.");
    }

    private static MariaDbKoxoPendingPasswordStore NewStore(
        SqlRuntimeConfiguration sql,
        KoxoPendingPasswordProtector? protector,
        string connectionString)
        => new(
            sql,
            protector,
            TimeSpan.FromMinutes(
                MariaDbKoxoPendingPasswordStore.DefaultLifetimeMinutes),
            NullLogger<MariaDbKoxoPendingPasswordStore>.Instance);

    // ==================================================================
    // Outils
    // ==================================================================

    /// <summary>
    /// Execute la <b>vraie</b> requete d'export et cherche l'utilisateur.
    /// </summary>
    /// <remarks>
    /// Aucune reecriture : c'est litteralement la constante consommee par
    /// <see cref="MariaDbKoxoRepository"/>. Une variante ecrite pour le test ne
    /// prouverait rien de la requete reellement executee.
    /// </remarks>
    private static async Task<bool> IsExportCandidateAsync(
        MySqlConnection connection,
        string portalUserId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = KoxoExportCandidateQuery.Sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(
                    ReadIdentifier(reader, "portal_user_id"),
                    portalUserId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Lit une colonne <c>CHAR(36)</c> sans presumer de son type CLR.
    /// </summary>
    /// <remarks>
    /// MySqlConnector materialise ces colonnes en <see cref="Guid"/> et non en
    /// chaine : <c>GetString</c> y leve. Le depot resout cela avec un helper
    /// interne, inaccessible depuis les tests — d'ou cette relecture locale.
    /// </remarks>
    private static string ReadIdentifier(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        var value = reader.GetValue(ordinal);
        return value switch
        {
            Guid guid => guid.ToString("D"),
            string text => text,
            _ => Convert.ToString(value) ?? string.Empty
        };
    }

    private static async Task ExpectRejectionAsync(
        MySqlConnection connection,
        string sql,
        string message,
        params (string Name, object Value)[] parameters)
    {
        try
        {
            await ExecuteAsync(connection, sql, parameters);
        }
        catch (MySqlException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"La base aurait du rejeter : {message}.");
    }

    private static async Task ExecuteAsync(
        MySqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarLongAsync(
        MySqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var value2 = await command.ExecuteScalarAsync();
        return value2 is null or DBNull ? 0 : Convert.ToInt64(value2);
    }

    private static async Task<string?> ScalarStringAsync(
        MySqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var value2 = await command.ExecuteScalarAsync();
        return value2 is null or DBNull ? null : Convert.ToString(value2);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record Fixture(
        string Marker,
        string CustomerId,
        string OtherCustomerId,
        string SubscriptionId,
        string ServiceId,
        string ServicePriceId,
        string TierId,
        string RuleId,
        string SlotId,
        string ItemId,
        string PortalUserId,
        string LifecycleId,
        List<string> ExtraPortalUserIds,
        List<string> ExtraSlotIds)
    {
        public static async Task<Fixture> CreateAsync(
            MySqlConnection connection)
        {
            var marker = $"bv2-au-{Guid.NewGuid():N}"[..24];
            var customerId = Guid.NewGuid().ToString("D");
            var otherCustomerId = Guid.NewGuid().ToString("D");
            var subscriptionId = Guid.NewGuid().ToString("D");
            var serviceId = Guid.NewGuid().ToString("D");
            var priceId = Guid.NewGuid().ToString("D");
            var tierId = Guid.NewGuid().ToString("D");
            var ruleId = Guid.NewGuid().ToString("D");
            var slotId = Guid.NewGuid().ToString("D");
            var itemId = Guid.NewGuid().ToString("D");
            var portalUserId = Guid.NewGuid().ToString("D");
            var lifecycleId = Guid.NewGuid().ToString("D");

            foreach (var (id, reference) in new[]
            {
                (customerId, marker),
                (otherCustomerId, $"{marker}-B")
            })
            {
                await ExecuteAsync(
                    connection,
                    """
                    INSERT INTO customers (
                        id, external_reference, display_name, status,
                        is_demo, created_at, updated_at
                    ) VALUES (
                        @id, @reference, 'Client utilisateurs additionnels',
                        'active', FALSE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                    );
                    """,
                    ("@id", id),
                    ("@reference", reference));
            }

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_services (
                    id, code, name, billing_type, default_scope_type,
                    discount_eligible, status, created_at, updated_at
                ) VALUES (
                    @id, @code, 'Utilisateur additionnel', 'recurring', 'user',
                    1, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", serviceId),
                ("@code", $"UA-{marker[..16]}"));

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_service_tiers (
                    id, service_id, code, name, numeric_value, unit,
                    display_order, status, created_at, updated_at
                ) VALUES (
                    @id, @service_id, @code, 'Palier unique', 1, 'user',
                    0, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", tierId),
                ("@service_id", serviceId),
                ("@code", $"UA-T-{marker[..16]}"));

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_service_prices (
                    id, service_id, price_code, price_version,
                    amount_cents, currency, billing_cadence,
                    valid_from, status, created_at
                ) VALUES (
                    @id, @service_id, @code, 1,
                    1000, 'EUR', 'monthly',
                    '2026-01-01 00:00:00', 'active', UTC_TIMESTAMP(6)
                );
                """,
                ("@id", priceId),
                ("@service_id", serviceId),
                ("@code", $"UA-P-{marker[..16]}"));

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_provisioning_rules (
                    id, service_id, tier_id, rule_type, target_type,
                    value_source, status, display_order,
                    created_at, updated_at
                ) VALUES (
                    @id, @service_id, NULL,
                    'contractual_entitlement', 'user_slot',
                    'none', 'active', 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", ruleId),
                ("@service_id", serviceId));

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_subscriptions (
                    id, customer_id, status, payment_mode, currency,
                    billing_model, created_at, updated_at
                ) VALUES (
                    @id, @customer_id, 'active', 'monthly', 'EUR',
                    'v2', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", subscriptionId),
                ("@customer_id", customerId));

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_subscription_users (
                    id, subscription_id, identity_reference, display_name,
                    email, is_primary, status, created_at, updated_at
                ) VALUES (
                    @id, @subscription_id, @identity, 'Utilisateur additionnel 1',
                    @email, 0, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", slotId),
                ("@subscription_id", subscriptionId),
                ("@identity", portalUserId),
                ("@email", $"ua-{marker}@example.invalid"));

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_subscription_items (
                    id, subscription_id, subscription_user_id, service_id,
                    tier_id, service_price_id, scope_type, quantity,
                    amount_cents_snapshot, currency, source,
                    effective_from, status, created_at, updated_at
                ) VALUES (
                    @id, @subscription_id, @slot_id, @service_id,
                    @tier_id, @price_id, 'user', 1,
                    1000, 'EUR', 'preset',
                    '2026-01-01 00:00:00', 'active',
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", itemId),
                ("@subscription_id", subscriptionId),
                ("@slot_id", slotId),
                ("@service_id", serviceId),
                ("@tier_id", tierId),
                ("@price_id", priceId));

            var fixture = new Fixture(
                marker,
                customerId,
                otherCustomerId,
                subscriptionId,
                serviceId,
                priceId,
                tierId,
                ruleId,
                slotId,
                itemId,
                portalUserId,
                lifecycleId,
                [],
                []);

            await fixture.InsertPortalUserAsync(
                connection,
                portalUserId,
                $"ua-{marker}@example.invalid",
                "CLI-900000");

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_user_identity_provisioning (
                    id, subscription_user_id, subscription_id, customer_id,
                    portal_user_id, koxo_unique_identifier, status,
                    created_at, updated_at
                ) VALUES (
                    @id, @slot_id, @subscription_id, @customer_id,
                    @portal_user_id, 'CLI-900000', 'awaiting_password',
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", lifecycleId),
                ("@slot_id", slotId),
                ("@subscription_id", subscriptionId),
                ("@customer_id", customerId),
                ("@portal_user_id", portalUserId));

            return fixture;
        }

        public async Task<string> CreatePortalUserAsync(
            MySqlConnection connection,
            string email,
            string koxoIdentifier)
        {
            var id = Guid.NewGuid().ToString("D");
            await InsertPortalUserAsync(connection, id, email, koxoIdentifier);
            ExtraPortalUserIds.Add(id);
            return id;
        }

        public async Task<string> CreateSlotAsync(
            MySqlConnection connection,
            bool isPrimary)
        {
            var id = Guid.NewGuid().ToString("D");
            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_subscription_users (
                    id, subscription_id, display_name, is_primary, status,
                    created_at, updated_at
                ) VALUES (
                    @id, @subscription_id, 'Place supplementaire', @is_primary,
                    'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", id),
                ("@subscription_id", SubscriptionId),
                ("@is_primary", isPrimary ? 1 : 0));
            ExtraSlotIds.Add(id);
            return id;
        }

        private async Task InsertPortalUserAsync(
            MySqlConnection connection,
            string id,
            string email,
            string koxoIdentifier)
            => await ExecuteAsync(
                connection,
                """
                INSERT INTO portal_users (
                    id, customer_id, identity_provider_subject, email,
                    display_name, status, role, personal_title, given_name,
                    surname, birth_date, koxo_unique_identifier,
                    is_primary_contact, created_at, updated_at
                ) VALUES (
                    @id, @customer_id, @subject, @email,
                    'Utilisateur additionnel', 'active', 'client_user',
                    'madame', 'Alice', 'Martin', '1990-04-12',
                    @koxo, 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", id),
                ("@customer_id", CustomerId),
                ("@subject", $"billing-v2-user-{id}"),
                ("@email", email),
                ("@koxo", koxoIdentifier));

        public Task SetLifecycleStatusAsync(
            MySqlConnection connection,
            string status)
            => ExecuteAsync(
                connection,
                """
                UPDATE billing_v2_user_identity_provisioning
                SET status = @status, updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id;
                """,
                ("@status", status),
                ("@id", LifecycleId));

        public Task SetSubscriptionStatusAsync(
            MySqlConnection connection,
            string status)
            => ExecuteAsync(
                connection,
                "UPDATE billing_v2_subscriptions SET status = @s WHERE id = @id;",
                ("@s", status),
                ("@id", SubscriptionId));

        public Task SetSlotPrimaryAsync(
            MySqlConnection connection,
            bool isPrimary)
            => ExecuteAsync(
                connection,
                """
                UPDATE billing_v2_subscription_users
                SET is_primary = @p
                WHERE id = @id;
                """,
                ("@p", isPrimary ? 1 : 0),
                ("@id", SlotId));

        public Task SetEntitlementRuleStatusAsync(
            MySqlConnection connection,
            string status)
            => ExecuteAsync(
                connection,
                """
                UPDATE billing_v2_provisioning_rules
                SET status = @s
                WHERE id = @id;
                """,
                ("@s", status),
                ("@id", RuleId));

        public Task CreateAdLinkAsync(MySqlConnection connection)
            => ExecuteAsync(
                connection,
                $"""
                INSERT INTO customer_ad_links (
                    id, customer_id, portal_user_id, object_type, object_guid,
                    object_sid, sam_account_name, display_name,
                    distinguished_name, linked_at
                ) VALUES (
                    '{Guid.NewGuid():D}', @customer_id, @portal_user_id, 'user',
                    @object_guid,
                    'S-1-5-21-1004336348-1177238915-682003330-4242',
                    @sam, 'Utilisateur additionnel',
                    @dn, UTC_TIMESTAMP(6)
                );
                """,
                ("@customer_id", CustomerId),
                ("@portal_user_id", PortalUserId),
                ("@object_guid", Guid.NewGuid().ToString("D")),
                ("@sam", $"ua{Marker[..12]}"),
                ("@dn", $"CN=UA,OU=KoXoAdm,DC=clients,DC=home,DC=bzh"));

        public async Task CleanupAsync(MySqlConnection connection)
        {
            await ExecuteAsync(
                connection,
                "DELETE FROM customer_ad_links WHERE customer_id IN (@a, @b);",
                ("@a", CustomerId),
                ("@b", OtherCustomerId));
            await ExecuteAsync(
                connection,
                """
                DELETE pending FROM koxo_pending_directory_passwords pending
                INNER JOIN portal_users portal_user
                    ON portal_user.id = pending.portal_user_id
                WHERE portal_user.customer_id IN (@a, @b);
                """,
                ("@a", CustomerId),
                ("@b", OtherCustomerId));
            await ExecuteAsync(
                connection,
                """
                DELETE FROM billing_v2_user_identity_provisioning
                WHERE customer_id IN (@a, @b);
                """,
                ("@a", CustomerId),
                ("@b", OtherCustomerId));
            await ExecuteAsync(
                connection,
                """
                DELETE setup FROM portal_user_password_setups setup
                INNER JOIN portal_users portal_user
                    ON portal_user.id = setup.portal_user_id
                WHERE portal_user.customer_id IN (@a, @b);
                """,
                ("@a", CustomerId),
                ("@b", OtherCustomerId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_subscription_items WHERE subscription_id = @id;",
                ("@id", SubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_subscription_users WHERE subscription_id = @id;",
                ("@id", SubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_subscriptions WHERE id = @id;",
                ("@id", SubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_provisioning_rules WHERE id = @id;",
                ("@id", RuleId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_service_prices WHERE id = @id;",
                ("@id", ServicePriceId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_service_tiers WHERE id = @id;",
                ("@id", TierId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_services WHERE id = @id;",
                ("@id", ServiceId));
            await ExecuteAsync(
                connection,
                "DELETE FROM portal_users WHERE customer_id IN (@a, @b);",
                ("@a", CustomerId),
                ("@b", OtherCustomerId));
            await ExecuteAsync(
                connection,
                "DELETE FROM customers WHERE id IN (@a, @b);",
                ("@a", CustomerId),
                ("@b", OtherCustomerId));
        }
    }
}
