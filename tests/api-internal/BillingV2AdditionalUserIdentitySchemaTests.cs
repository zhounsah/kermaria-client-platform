using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.Provisioning;
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

            await VerifyRealHandoffCommitsAtomicallyAsync(
                connection,
                fixture,
                connectionString);
            await VerifyRealHandoffRollsBackEntirelyAsync(
                connection,
                fixture,
                connectionString);
            await VerifyRealAssignmentIsSerializedAsync(
                connection,
                fixture,
                connectionString);

            await VerifyRealProductReadingIsAdministrableOnlyAsync(
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
        // condensat â€” KoXo a besoin du mot de passe reel.
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
                $"Un cycle de vie Â« {status} Â» n'autorise aucune creation.");
        }
    }

    /// <summary>
    /// Fenetre de crash : objet annuaire resolu, lien pas encore ecrit.
    /// </summary>
    /// <remarks>
    /// C'est l'etat que le service persiste <b>avant</b> d'ecrire le lien. Une
    /// interruption a cet instant â€” redemarrage, panne reseau, arret du
    /// service â€” laisse une ligne <c>directory_ready</c> sans
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
            "Un cycle de vie Â« directory_ready Â» sans lien AD reste exporte : "
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
            "Un cycle de vie Â« ready Â» sans lien AD n'est pas exporte par "
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

    // ==================================================================
    // Depots reels : ce que seule une vraie transaction peut prouver
    // ==================================================================

    /// <summary>
    /// Le relais du mot de passe s'engage d'un seul bloc, par les vrais depots.
    /// </summary>
    /// <remarks>
    /// Le scenario passe par
    /// <see cref="MariaDbBillingV2AdditionalUserIdentityRepository"/> puis
    /// <see cref="MariaDbPortalPasswordSetupRepository"/>, puis relit tout sur
    /// une connexion neuve : les quatre ecritures â€” condensat, consommation du
    /// jeton, secret scelle, transition du cycle â€” doivent etre visibles
    /// ensemble ou pas du tout.
    /// </remarks>
    private static async Task VerifyRealHandoffCommitsAtomicallyAsync(
        MySqlConnection connection,
        Fixture fixture,
        string connectionString)
    {
        const string password = "MotDePasseAtomique!1";
        const string expectedHash = "$argon2id$test$atomique";

        var sql = SqlFor(connectionString);
        var identities =
            new MariaDbBillingV2AdditionalUserIdentityRepository(sql);
        var setups = new MariaDbPortalPasswordSetupRepository(sql);
        var store = NewStore(sql, TestProtector(11), connectionString);

        var slotId = await fixture.CreateSlotAsync(
            connection,
            isPrimary: false,
            withEntitlement: true);
        var token = $"jeton-atomique-{Guid.NewGuid():N}";
        var command = BuildAssignment(fixture, slotId, "atomique", token);

        var assignment = await identities.AssignAsync(
            command,
            RealPolicyFor(fixture),
            CancellationToken.None);
        Ensure(
            assignment.Succeeded,
            "L'attribution reelle doit reussir sur une place libre "
            + $"(refus obtenu : {assignment.RejectionCode}).");

        var secret = store.Seal(command.PortalUserId, password);
        Ensure(secret is not null, "Le scellement doit produire un secret.");

        var consumption = await setups.ConsumeAndSetPasswordAsync(
            PortalSetupToken.Hash(token),
            BillingV2AdditionalUserIdentityConventions.PasswordSetupPurpose,
            _ => expectedHash,
            new PortalPasswordHandoff(
                command.PortalUserId,
                command.LifecycleId,
                DateTime.UtcNow,
                secret),
            CancellationToken.None);
        Ensure(
            consumption.Succeeded
            && consumption.PortalUserId == command.PortalUserId,
            "La consommation reelle doit reussir "
            + $"(code obtenu : {consumption.Code}).");

        // Connexion neuve : on ne lit rien depuis la transaction qui a ecrit.
        await using var verification = new MySqlConnection(connectionString);
        await verification.OpenAsync();

        Ensure(
            await ScalarStringAsync(
                verification,
                "SELECT password_hash FROM portal_users WHERE id = @id;",
                ("@id", command.PortalUserId)) == expectedHash,
            "Le condensat doit etre pose en base apres COMMIT.");
        Ensure(
            await ScalarLongAsync(
                verification,
                """
                SELECT COUNT(*)
                FROM portal_user_password_setups
                WHERE id = @id AND consumed_at IS NOT NULL;
                """,
                ("@id", command.PasswordSetupId)) == 1,
            "Le jeton doit etre marque consomme.");
        Ensure(
            await ScalarLongAsync(
                verification,
                """
                SELECT COUNT(*)
                FROM koxo_pending_directory_passwords
                WHERE portal_user_id = @id;
                """,
                ("@id", command.PortalUserId)) == 1,
            "Le secret scelle doit etre persiste par la meme transaction.");
        Ensure(
            await ScalarStringAsync(
                verification,
                """
                SELECT status
                FROM billing_v2_user_identity_provisioning
                WHERE id = @id;
                """,
                ("@id", command.LifecycleId)) == "koxo_pending",
            "Le cycle de vie doit avoir bascule en koxo_pending.");

        // Le secret persiste bien le mot de passe REEL : un chiffre illisible
        // passerait toutes les assertions de comptage ci-dessus.
        Ensure(
            await NewStore(sql, TestProtector(11), connectionString)
                .PeekAsync(command.PortalUserId, CancellationToken.None)
                == password,
            "Un processus neuf doit relire le mot de passe exact.");
    }

    /// <summary>
    /// Un echec tardif annule tout, condensat compris.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deux declenchements, tous deux posterieurs au debut des ecritures :
    /// un chiffre plus long que <c>VARCHAR(1024)</c>, qui leve une erreur SQL
    /// remontant jusqu'a l'appelant ; puis un <c>lifecycle_id</c> inexistant,
    /// qui produit un refus metier sans exception. Le premier prouve que
    /// l'annulation vient bien de la transaction et non d'un chemin d'erreur
    /// ecrit a la main ; le second ne depend d'aucun <c>sql_mode</c>.
    /// </para>
    /// <para>
    /// Ce test mord si la transaction disparait : le jeton resterait consomme
    /// et le nouveau condensat pose, alors que le secret reversible â€” qui
    /// n'existe en clair qu'a cet instant â€” n'aurait jamais ete ecrit.
    /// </para>
    /// </remarks>
    private static async Task VerifyRealHandoffRollsBackEntirelyAsync(
        MySqlConnection connection,
        Fixture fixture,
        string connectionString)
    {
        const string previousHash = "$argon2id$test$precedent";
        var sql = SqlFor(connectionString);
        var identities =
            new MariaDbBillingV2AdditionalUserIdentityRepository(sql);
        var setups = new MariaDbPortalPasswordSetupRepository(sql);
        var store = NewStore(sql, TestProtector(11), connectionString);

        var slotId = await fixture.CreateSlotAsync(
            connection,
            isPrimary: false,
            withEntitlement: true);
        var token = $"jeton-rollback-{Guid.NewGuid():N}";
        var command = BuildAssignment(fixture, slotId, "rollback", token);
        var assignment = await identities.AssignAsync(
            command,
            RealPolicyFor(fixture),
            CancellationToken.None);
        Ensure(
            assignment.Succeeded,
            "L'attribution preparatoire doit reussir "
            + $"(refus obtenu : {assignment.RejectionCode}).");

        // Un condensat anterieur connu : sans lui, Â« intact Â» ne voudrait rien
        // dire, NULL etant aussi l'etat d'un compte jamais active.
        await ExecuteAsync(
            connection,
            "UPDATE portal_users SET password_hash = @h WHERE id = @id;",
            ("@h", previousHash),
            ("@id", command.PortalUserId));

        var tokenHash = PortalSetupToken.Hash(token);
        var secret = store.Seal(command.PortalUserId, "MotDePasseAnnule!1")!;

        // 1. Erreur SQL tardive : le chiffre depasse la colonne.
        MySqlException? failure = null;
        try
        {
            await setups.ConsumeAndSetPasswordAsync(
                tokenHash,
                BillingV2AdditionalUserIdentityConventions.PasswordSetupPurpose,
                _ => "$argon2id$test$jamais-pose",
                new PortalPasswordHandoff(
                    command.PortalUserId,
                    command.LifecycleId,
                    DateTime.UtcNow,
                    new PortalPasswordSecret(
                        new string('A', 2048),
                        secret.KeyId,
                        secret.ExpiresAtUtc)),
                CancellationToken.None);
        }
        catch (MySqlException exception)
        {
            failure = exception;
        }

        Ensure(
            failure is not null,
            "Un chiffre de 2048 caracteres doit etre refuse par "
            + "koxo_pending_directory_passwords.ciphertext (VARCHAR(1024)). "
            + "Sans erreur, la base tronque silencieusement et ce scenario ne "
            + "prouverait plus rien : verifier sql_mode.");
        await AssertHandoffLeftNoTraceAsync(
            connectionString,
            command,
            previousHash,
            "apres une erreur SQL tardive");

        // 2. Refus metier tardif : le cycle vise n'existe pas. Meme exigence,
        //    sans dependre du mode strict du serveur.
        var refused = await setups.ConsumeAndSetPasswordAsync(
            tokenHash,
            BillingV2AdditionalUserIdentityConventions.PasswordSetupPurpose,
            _ => "$argon2id$test$jamais-pose-non-plus",
            new PortalPasswordHandoff(
                command.PortalUserId,
                Guid.NewGuid().ToString("D"),
                DateTime.UtcNow,
                secret),
            CancellationToken.None);
        Ensure(
            refused.Code == PortalPasswordSetupCodes.HandoffFailed,
            "Un cycle de vie introuvable doit produire PASSWORD_HANDOFF_FAILED "
            + $"(code obtenu : {refused.Code}).");
        await AssertHandoffLeftNoTraceAsync(
            connectionString,
            command,
            previousHash,
            "apres un refus metier tardif");

        // Le jeton doit rester utilisable : c'est l'unique lien dont dispose la
        // personne, et rien n'a abouti.
        var target = await setups.FindByTokenHashAsync(
            tokenHash,
            CancellationToken.None);
        Ensure(
            target is not null && target.IsUsable(DateTime.UtcNow),
            "Le jeton doit rester utilisable apres une annulation.");
    }

    private static async Task AssertHandoffLeftNoTraceAsync(
        string connectionString,
        BillingV2AdditionalUserAssignmentCommand command,
        string previousHash,
        string context)
    {
        await using var verification = new MySqlConnection(connectionString);
        await verification.OpenAsync();

        Ensure(
            await ScalarStringAsync(
                verification,
                "SELECT password_hash FROM portal_users WHERE id = @id;",
                ("@id", command.PortalUserId)) == previousHash,
            $"Le condensat anterieur doit etre intact {context}.");
        Ensure(
            await ScalarLongAsync(
                verification,
                """
                SELECT COUNT(*)
                FROM portal_user_password_setups
                WHERE id = @id AND consumed_at IS NULL;
                """,
                ("@id", command.PasswordSetupId)) == 1,
            $"Le jeton ne doit pas etre consomme {context}.");
        Ensure(
            await ScalarLongAsync(
                verification,
                """
                SELECT COUNT(*)
                FROM koxo_pending_directory_passwords
                WHERE portal_user_id = @id;
                """,
                ("@id", command.PortalUserId)) == 0,
            $"Aucun secret ne doit subsister {context}.");
        Ensure(
            await ScalarStringAsync(
                verification,
                """
                SELECT status
                FROM billing_v2_user_identity_provisioning
                WHERE id = @id;
                """,
                ("@id", command.LifecycleId)) == "awaiting_password",
            $"Le cycle de vie doit rester awaiting_password {context}.");
    }

    /// <summary>
    /// Deux attributions concurrentes sur la meme place : une seule aboutit.
    /// </summary>
    /// <remarks>
    /// Les deux e-mails sont distincts, donc ce n'est pas l'index unique de
    /// <c>portal_users.email</c> qui tranche : seuls le <c>FOR UPDATE</c> de la
    /// place et le <c>UPDATE</c> conditionne a
    /// <c>identity_reference IS NULL</c> peuvent le faire.
    /// </remarks>
    private static async Task VerifyRealAssignmentIsSerializedAsync(
        MySqlConnection connection,
        Fixture fixture,
        string connectionString)
    {
        var identities = new MariaDbBillingV2AdditionalUserIdentityRepository(
            SqlFor(connectionString));
        var policy = RealPolicyFor(fixture);

        // Ce que chaque tentative a REELLEMENT vu sous verrou : sans cette
        // capture, un refus juste pourrait venir d'une relecture fausse.
        var seen = new System.Collections.Concurrent.ConcurrentDictionary<
            string,
            BillingV2AdditionalUserSlotSnapshot>();

        Func<string, Func<BillingV2AdditionalUserSlotSnapshot, string?>> observe =
            key => snapshot =>
            {
                seen[key] = snapshot;
                return policy(snapshot);
            };

        var slotId = await fixture.CreateSlotAsync(
            connection,
            isPrimary: false,
            withEntitlement: true);
        var first = BuildAssignment(
            fixture,
            slotId,
            "concurrent-a",
            $"jeton-concurrent-a-{Guid.NewGuid():N}");
        var second = BuildAssignment(
            fixture,
            slotId,
            "concurrent-b",
            $"jeton-concurrent-b-{Guid.NewGuid():N}");

        var results = await Task.WhenAll(
            identities.AssignAsync(
                first,
                observe("a"),
                CancellationToken.None),
            identities.AssignAsync(
                second,
                observe("b"),
                CancellationToken.None));

        var winners = results.Where(result => result.Succeeded).ToList();
        Ensure(
            winners.Count == 1,
            "Exactement une des deux tentatives doit aboutir "
            + $"({winners.Count} obtenues).");
        var loser = results.Single(result => !result.Succeeded);
        Ensure(
            loser.RejectionCode
                == BillingV2AdditionalUserRejectionCodes.SlotAlreadyAssigned,
            "La tentative perdante doit etre refusee comme conflit de place, "
            + "pas comme anomalie de cycle de vie "
            + $"(code obtenu : {loser.RejectionCode}).");

        var winnerPortalUserId = winners[0].Created!.PortalUserId;
        var loserKey = winnerPortalUserId == first.PortalUserId ? "b" : "a";

        // La lecture verrouillee du perdant doit avoir eu lieu APRES le commit
        // du gagnant : si elle voyait encore la place libre, le refus obtenu
        // serait juste par accident.
        Ensure(
            seen.TryGetValue(loserKey, out var loserSnapshot)
            && loserSnapshot.IdentityReference is not null,
            "Le perdant doit relire la place deja attribuee : c'est le "
            + "FOR UPDATE qui le garantit, pas l'index unique.");

        await using var verification = new MySqlConnection(connectionString);
        await verification.OpenAsync();

        Ensure(
            await ScalarLongAsync(
                verification,
                """
                SELECT COUNT(*)
                FROM portal_users
                WHERE id IN (@a, @b);
                """,
                ("@a", first.PortalUserId),
                ("@b", second.PortalUserId)) == 1,
            "Un seul utilisateur portail doit exister : l'autre insertion doit "
            + "avoir ete annulee, pas laissee orpheline.");
        Ensure(
            await ScalarLongAsync(
                verification,
                """
                SELECT COUNT(*)
                FROM billing_v2_user_identity_provisioning
                WHERE subscription_user_id = @slot;
                """,
                ("@slot", slotId)) == 1,
            "La place ne doit porter qu'un seul cycle de vie.");
        Ensure(
            await ScalarLongAsync(
                verification,
                """
                SELECT COUNT(*)
                FROM portal_user_password_setups
                WHERE id IN (@a, @b);
                """,
                ("@a", first.PasswordSetupId),
                ("@b", second.PasswordSetupId)) == 1,
            "Un seul jeton de mot de passe doit avoir ete emis.");
        var assignedIdentity = await ScalarStringAsync(
            verification,
            """
            SELECT identity_reference
            FROM billing_v2_subscription_users
            WHERE id = @slot;
            """,
            ("@slot", slotId));
        Ensure(
            string.Equals(
                assignedIdentity,
                winnerPortalUserId,
                StringComparison.OrdinalIgnoreCase),
            "La place doit designer l'attribution qui a abouti.");
    }

    /// <summary>
    /// La <b>vraie</b> politique, appliquee a l'instantane verrouille.
    /// </summary>
    /// <remarks>
    /// Un predicat ecrit pour le test ne prouverait rien de la decision reelle,
    /// et un ordre de controles different du sien produirait un refus different
    /// pour le meme etat : c'est exactement ce qui faisait rendre
    /// <c>LIFECYCLE_ALREADY_EXISTS</c> a un perdant de course, alors que la
    /// place etait deja attribuee.
    /// </remarks>
    private static Func<BillingV2AdditionalUserSlotSnapshot, string?>
        RealPolicyFor(Fixture fixture)
        => snapshot => BillingV2AdditionalUserAssignmentPolicy.Validate(
            snapshot,
            fixture.CustomerId,
            fixture.SubscriptionId);

    private static BillingV2AdditionalUserAssignmentCommand BuildAssignment(
        Fixture fixture,
        string slotId,
        string label,
        string token)
        => new(
            fixture.CustomerId,
            fixture.SubscriptionId,
            slotId,
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            PortalSetupToken.Hash(token),
            DateTime.UtcNow.AddHours(2),
            BillingV2AdditionalUserIdentityConventions.PasswordSetupPurpose,
            $"{label}-{fixture.Marker}@example.invalid",
            $"Utilisateur {label}",
            "monsieur",
            "Paul",
            "Durand",
            new DateOnly(1988, 3, 4),
            "PD",
            "+33123456789",
            "tests");

    private static SqlRuntimeConfiguration SqlFor(string connectionString)
        => new(
            PortalPersistenceMode.MariaDb,
            "mariadb",
            connectionString,
            "TEST",
            ConfigurationValid: true);

    private static KoxoPendingPasswordProtector? TestProtector(byte filler)
        => KoxoPendingPasswordProtector.TryCreate(
            Convert.ToBase64String(
                Enumerable.Repeat(filler, 32).ToArray()));

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
    /// interne, inaccessible depuis les tests â€” d'ou cette relecture locale.
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

    // ==================================================================
    // Lecture produit et compteurs, en base reelle
    // ==================================================================

    /// <summary>
    /// La lecture produit et les compteurs ne montrent que ce qui est
    /// reellement administrable, et ne touchent pas aux montants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rien de tout cela n'est verifiable en mock : la clause de lecture, la
    /// jointure vers <c>portal_users</c> et les deux sous-requetes de
    /// comptage ne sont que des chaines tant qu'un vrai serveur ne les a pas
    /// executees. Le mock, lui, ne fait qu'imiter leur intention.
    /// </para>
    /// <para>
    /// Le scenario est deroule dans l'ordre parce que chaque etape s'appuie
    /// sur l'etat pose par la precedente : ajouter des places sans ligne
    /// financiere, puis une place facturee, puis la desactiver, puis
    /// desactiver l'abonnement. C'est aussi ce qui rend l'invariant des
    /// montants observable : quatre places de plus ne doivent rien changer
    /// aux euros, la ou une jointure les aurait multiplies par cinq.
    /// </para>
    /// </remarks>
    private static async Task VerifyRealProductReadingIsAdministrableOnlyAsync(
        MySqlConnection connection,
        Fixture fixture,
        string connectionString)
    {
        var identities = new MariaDbBillingV2AdditionalUserIdentityRepository(
            SqlFor(connectionString));
        var projection = new BillingV2PortalSubscriptionProjection(
            SqlFor(connectionString));
        var scenarioMarker = $"bv2-product-{Guid.NewGuid():N}"[..24];
        var scenarioSubscriptionId = Guid.NewGuid().ToString("D");
        var scenarioKoxoIdentifier = ($"CLI-TEST-{Guid.NewGuid():N}")[..32];
        string? scenarioPortalUserId = null;
        try
        {
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
                ("@id", scenarioSubscriptionId),
                ("@customer_id", fixture.CustomerId));

            var scenarioEmail = $"ua-{scenarioMarker}@example.invalid";
            scenarioPortalUserId = await fixture.CreatePortalUserAsync(
                connection,
                scenarioEmail,
                scenarioKoxoIdentifier);
            var scenarioSlotId = await fixture.CreateSlotAsync(
                connection,
                scenarioSubscriptionId,
                isPrimary: false,
                withEntitlement: true);
            await ExecuteAsync(
                connection,
                """
                UPDATE billing_v2_subscription_users
                SET identity_reference = @portal_user_id,
                    email = @email,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @slot_id;
                """,
                ("@portal_user_id", scenarioPortalUserId),
                ("@email", scenarioEmail),
                ("@slot_id", scenarioSlotId));
            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_user_identity_provisioning (
                    id, subscription_user_id, subscription_id, customer_id,
                    portal_user_id, koxo_unique_identifier, status,
                    created_at, updated_at
                ) VALUES (
                    @id, @slot_id, @subscription_id, @customer_id,
                    @portal_user_id, @koxo, 'awaiting_password',
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", Guid.NewGuid().ToString("D")),
                ("@slot_id", scenarioSlotId),
                ("@subscription_id", scenarioSubscriptionId),
                ("@customer_id", fixture.CustomerId),
                ("@portal_user_id", scenarioPortalUserId),
                ("@koxo", scenarioKoxoIdentifier));

            // La projection portail exige une demande de checkout : sans elle, la
            // souscription n'existe pas pour l'espace client.
            await fixture.EnsureCheckoutRequestAsync(connection, scenarioSubscriptionId, scenarioMarker);

            // --- Etat initial : une place vendue, attribuee. -------------------
            var listed = await identities.ListAdditionalUserSlotsAsync(
                fixture.CustomerId,
                scenarioSubscriptionId,
                CancellationToken.None);
            Ensure(
                listed.Count == 1
                && listed[0].SubscriptionUserId == scenarioSlotId,
                "La place vendue et attribuee est la seule listee "
                + $"({listed.Count} obtenue(s)).");
            Ensure(
                listed[0].IsAssigned,
                "Elle est annoncee attribuee : identity_reference est pose.");
            // COALESCE(portal_user.display_name, slot.display_name) : la place
            // porte Â« Utilisateur additionnel 1 Â», l'utilisateur portail
            // Â« Utilisateur additionnel Â». Lire le second prouve que la jointure
            // a bien resolu l'identite, et pas seulement recopie la place.
            Ensure(
                listed[0].DisplayName == "Utilisateur additionnel",
                "Le nom affiche vient de l'utilisateur portail joint, pas du "
                + $"libelle de planification (Â« {listed[0].DisplayName} Â»).");
            Ensure(
                listed[0].Email == $"ua-{scenarioMarker}@example.invalid",
                $"L'adresse est celle de la personne (Â« {listed[0].Email} Â»).");
            Ensure(
                listed[0].LifecycleStatus == "awaiting_password",
                "Le cycle de vie remonte tel quel : c'est lui qui donne l'etat "
                + $"produit (Â« {listed[0].LifecycleStatus} Â»).");

            var baseline = await ReadSummaryAsync(projection, fixture.CustomerId, scenarioSubscriptionId);
            Ensure(
                baseline.AdditionalUserSlotsCount == 1
                && baseline.AssignedAdditionalUsersCount == 1,
                "Les compteurs partent de 1 place vendue, 1 pourvue "
                + $"({baseline.AdditionalUserSlotsCount}/"
                + $"{baseline.AssignedAdditionalUsersCount}).");

            // --- Quatre places de plus, aucune ligne financiere. ---------------
            // Sans droit contractuel elles ne sont pas des places USER-ADDITIONAL
            // et ne doivent rien changer : ni a la liste, ni aux compteurs, ni
            // surtout aux montants. Une jointure sur les places multiplierait ici
            // les lignes d'items par cinq.
            await fixture.CreateSlotAsync(connection, scenarioSubscriptionId, isPrimary: false);
            await fixture.CreateSlotAsync(connection, scenarioSubscriptionId, isPrimary: false);
            await fixture.CreateSlotAsync(connection, scenarioSubscriptionId, isPrimary: false);
            await fixture.CreateSlotAsync(
                connection,
                scenarioSubscriptionId,
                isPrimary: true,
                withEntitlement: false);

            var noise = await ReadSummaryAsync(projection, fixture.CustomerId, scenarioSubscriptionId);
            Ensure(
                noise.AdditionalUserSlotsCount == 1
                && noise.AssignedAdditionalUsersCount == 1,
                "Des places sans droit contractuel ne sont pas comptees "
                + $"({noise.AdditionalUserSlotsCount}/"
                + $"{noise.AssignedAdditionalUsersCount}).");
            Ensure(
                noise.PriceAmountCents == baseline.PriceAmountCents
                && noise.SetupFeeAmountCents == baseline.SetupFeeAmountCents,
                "Les montants sont STRICTEMENT identiques apres l'ajout de quatre "
                + $"places ({noise.PriceAmountCents} contre "
                + $"{baseline.PriceAmountCents} centimes).");
            Ensure(
                (await identities.ListAdditionalUserSlotsAsync(
                    fixture.CustomerId,
                    scenarioSubscriptionId,
                    CancellationToken.None)).Count == 1,
                "La liste non plus ne bouge pas : ni la place principale, ni une "
                + "place sans droit ne sont administrables.");

            // --- Une vraie place vendue et vide. -------------------------------
            var emptySlotId = await fixture.CreateSlotAsync(
                connection,
                scenarioSubscriptionId,
                isPrimary: false,
                withEntitlement: true);

            listed = await identities.ListAdditionalUserSlotsAsync(
                fixture.CustomerId,
                scenarioSubscriptionId,
                CancellationToken.None);
            var empty = listed.SingleOrDefault(
                slot => slot.SubscriptionUserId == emptySlotId);
            Ensure(
                listed.Count == 2 && empty is not null,
                $"La place vendue et vide est annoncee ({listed.Count} listee(s)).");
            Ensure(
                !empty!.IsAssigned
                && empty.DisplayName is null
                && empty.Email is null
                && empty.LifecycleStatus is null,
                "Une place vide ne porte personne : la jointure vers portal_users "
                + "ne ramene rien et le libelle de planification n'est pas "
                + $"presente comme un occupant (Â« {empty.DisplayName} Â»).");

            var sold = await ReadSummaryAsync(projection, fixture.CustomerId, scenarioSubscriptionId);
            Ensure(
                sold.AdditionalUserSlotsCount == 2
                && sold.AssignedAdditionalUsersCount == 1,
                "Deux places vendues, une seule pourvue "
                + $"({sold.AdditionalUserSlotsCount}/"
                + $"{sold.AssignedAdditionalUsersCount}).");
            Ensure(
                sold.PriceAmountCents == baseline.PriceAmountCents + 1000,
                "Le montant augmente d'exactement une ligne d'item, pas d'un "
                + $"multiple ({sold.PriceAmountCents} contre "
                + $"{baseline.PriceAmountCents + 1000} centimes attendus).");

            // --- Place resiliee : plus administrable. --------------------------
            await fixture.SetSlotStatusAsync(connection, emptySlotId, "cancelled");

            listed = await identities.ListAdditionalUserSlotsAsync(
                fixture.CustomerId,
                scenarioSubscriptionId,
                CancellationToken.None);
            Ensure(
                listed.Count == 1
                && listed[0].SubscriptionUserId == scenarioSlotId,
                "Une place resiliee disparait de la liste : la politique "
                + $"d'attribution la refuserait ({listed.Count} listee(s)).");

            var cancelledSlot = await ReadSummaryAsync(projection, fixture.CustomerId, scenarioSubscriptionId);
            Ensure(
                cancelledSlot.AdditionalUserSlotsCount == 1
                && cancelledSlot.AssignedAdditionalUsersCount == 1,
                "Elle ne compte plus non plus "
                + $"({cancelledSlot.AdditionalUserSlotsCount}/"
                + $"{cancelledSlot.AssignedAdditionalUsersCount}).");
            // Le compteur produit et la facturation sont deux axes distincts :
            // l'item reste actif, donc le montant ne bouge pas. C'est constate
            // ici, pas corrige : ce test ne decide pas de la politique de
            // facturation d'une place resiliee.
            Ensure(
                cancelledSlot.PriceAmountCents == sold.PriceAmountCents,
                "Desactiver une place ne touche pas au montant facture "
                + $"({cancelledSlot.PriceAmountCents} contre "
                + $"{sold.PriceAmountCents} centimes).");

            await fixture.SetSlotStatusAsync(connection, emptySlotId, "active");

            // --- Abonnement non actif : plus rien a administrer. ---------------
            await fixture.SetSubscriptionStatusAsync(connection, scenarioSubscriptionId, "cancelled");
            try
            {
                Ensure(
                    (await identities.ListAdditionalUserSlotsAsync(
                        fixture.CustomerId,
                        scenarioSubscriptionId,
                        CancellationToken.None)).Count == 0,
                    "Un abonnement non actif ne presente aucune place : "
                    + "SUBSCRIPTION_NOT_PROVISIONABLE refuserait chaque "
                    + "attribution.");

                var inactive = await ReadSummaryAsync(projection, fixture.CustomerId, scenarioSubscriptionId);
                Ensure(
                    inactive.AdditionalUserSlotsCount == 0
                    && inactive.AssignedAdditionalUsersCount == 0,
                    "Les compteurs tombent a zero avec lui "
                    + $"({inactive.AdditionalUserSlotsCount}/"
                    + $"{inactive.AssignedAdditionalUsersCount}).");
            }
            finally
            {
                await fixture.SetSubscriptionStatusAsync(connection, scenarioSubscriptionId, "active");
            }

            // --- Cloisonnement : Â« pas a vous Â» se lit Â« rien Â». ---------------
            Ensure(
                (await identities.ListAdditionalUserSlotsAsync(
                    fixture.OtherCustomerId,
                    scenarioSubscriptionId,
                    CancellationToken.None)).Count == 0,
                "L'abonnement d'un autre client ne se lit pas.");
            Ensure(
                (await identities.ListAdditionalUserSlotsAsync(
                    fixture.CustomerId,
                    Guid.NewGuid().ToString("D"),
                    CancellationToken.None)).Count == 0,
                "Un abonnement inconnu se lit exactement pareil : aucune "
                + "difference observable entre Â« pas a vous Â» et Â« inexistant Â».");
            Ensure(
                (await projection.GetClientSubscriptionsAsync(
                    fixture.OtherCustomerId,
                    CancellationToken.None))
                    .All(summary => summary.Id != scenarioSubscriptionId),
                "La projection portail ne rend pas la souscription a l'autre "
                + "client.");
        }
        finally
        {
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_user_identity_provisioning WHERE subscription_id = @id;",
                ("@id", scenarioSubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_subscription_items WHERE subscription_id = @id;",
                ("@id", scenarioSubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_subscription_users WHERE subscription_id = @id;",
                ("@id", scenarioSubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_authoritative_checkout_requests WHERE subscription_id = @id;",
                ("@id", scenarioSubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_subscriptions WHERE id = @id;",
                ("@id", scenarioSubscriptionId));
            if (scenarioPortalUserId is not null)
            {
                await ExecuteAsync(
                    connection,
                    "DELETE FROM portal_users WHERE id = @id;",
                    ("@id", scenarioPortalUserId));
            }
        }
    }

    /// <summary>
    /// Lit la souscription de la fixture par la <b>vraie</b> projection.
    /// </summary>
    private static Task<SubscriptionSummary> ReadSummaryAsync(
        BillingV2PortalSubscriptionProjection projection,
        Fixture fixture)
        => ReadSummaryAsync(
            projection,
            fixture.CustomerId,
            fixture.SubscriptionId);

    private static async Task<SubscriptionSummary> ReadSummaryAsync(
        BillingV2PortalSubscriptionProjection projection,
        string customerId,
        string subscriptionId)
    {
        var summaries = await projection.GetClientSubscriptionsAsync(
            customerId,
            CancellationToken.None);
        var summary = summaries.SingleOrDefault(
            candidate => candidate.Id == subscriptionId);
        Ensure(
            summary is not null,
            "La souscription de test doit etre visible dans la projection "
            + "portail, sinon rien de ce qui suit ne mesure quoi que ce soit.");
        return summary!;
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

        /// <param name="withEntitlement">
        /// Adosse un item actif de perimetre <c>user</c> a la place, sans quoi
        /// la vraie politique refuse tout de suite en
        /// <c>SLOT_ENTITLEMENT_MISSING</c>.
        /// </param>
        public Task<string> CreateSlotAsync(
            MySqlConnection connection,
            bool isPrimary,
            bool withEntitlement = false)
            => CreateSlotAsync(
                connection,
                SubscriptionId,
                isPrimary,
                withEntitlement);

        public async Task<string> CreateSlotAsync(
            MySqlConnection connection,
            string subscriptionId,
            bool isPrimary,
            bool withEntitlement = false)
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
                ("@subscription_id", subscriptionId),
                ("@is_primary", isPrimary ? 1 : 0));
            ExtraSlotIds.Add(id);

            if (withEntitlement)
            {
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
                    ("@id", Guid.NewGuid().ToString("D")),
                    ("@subscription_id", subscriptionId),
                    ("@slot_id", id),
                    ("@service_id", ServiceId),
                    ("@tier_id", TierId),
                    ("@price_id", ServicePriceId));
            }

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

        public Task SetSlotStatusAsync(
            MySqlConnection connection,
            string slotId,
            string status)
            => ExecuteAsync(
                connection,
                """
                UPDATE billing_v2_subscription_users
                SET status = @s, updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id;
                """,
                ("@s", status),
                ("@id", slotId));

        /// <summary>
        /// Pose la demande de checkout exigee par la projection portail.
        /// </summary>
        /// <remarks>
        /// La projection joint cette table en <c>INNER JOIN</c> : sans une
        /// ligne, la souscription n'apparait pas du tout, et un test de
        /// compteurs lirait zero pour la mauvaise raison.
        /// </remarks>
        public Task EnsureCheckoutRequestAsync(MySqlConnection connection)
            => EnsureCheckoutRequestAsync(connection, SubscriptionId, Marker);

        public Task EnsureCheckoutRequestAsync(
            MySqlConnection connection,
            string subscriptionId,
            string marker)
            => ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_authoritative_checkout_requests (
                    id, customer_id, idempotency_key,
                    request_fingerprint_hash, legacy_offer_id,
                    selection_fingerprint, provider,
                    environment, subscription_id, status,
                    created_at, updated_at
                ) VALUES (
                    @id, @customer_id, @key,
                    SHA2(@key, 256), @offer_id,
                    SHA2(CONCAT('billing_v2.legacy_offer|', @offer_id), 256), 'stripe',
                    'test', @subscription_id, 'succeeded',
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                )
                ON DUPLICATE KEY UPDATE updated_at = UTC_TIMESTAMP(6);
                """,
                ("@id", Guid.NewGuid().ToString("D")),
                ("@customer_id", CustomerId),
                ("@key", $"checkout-{marker}"),
                ("@offer_id", Guid.NewGuid().ToString("D")),
                ("@subscription_id", subscriptionId));

        public Task SetSubscriptionStatusAsync(
            MySqlConnection connection,
            string status)
            => SetSubscriptionStatusAsync(connection, SubscriptionId, status);

        public Task SetSubscriptionStatusAsync(
            MySqlConnection connection,
            string subscriptionId,
            string status)
            => ExecuteAsync(
                connection,
                "UPDATE billing_v2_subscriptions SET status = @s WHERE id = @id;",
                ("@s", status),
                ("@id", subscriptionId));

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
            // Le vrai depot journalise chaque attribution ; l'audit ne porte
            // pas de cle etrangere, donc rien ne l'emporterait autrement.
            await ExecuteAsync(
                connection,
                """
                DELETE FROM billing_v2_audit_log
                WHERE entity_type = 'billing_v2_subscription_user'
                  AND details_text LIKE CONCAT('%subscription_id=', @id, '%');
                """,
                ("@id", SubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_subscription_items WHERE subscription_id = @id;",
                ("@id", SubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_subscription_users WHERE subscription_id = @id;",
                ("@id", SubscriptionId));
            // La demande de checkout porte une cle etrangere ON DELETE
            // RESTRICT vers la souscription : elle part d'abord.
            await ExecuteAsync(
                connection,
                """
                DELETE FROM billing_v2_authoritative_checkout_requests
                WHERE subscription_id = @id;
                """,
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
