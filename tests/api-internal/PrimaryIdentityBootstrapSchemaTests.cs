using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Invariants base reelle de l'amorcage de l'identite du compte principal
/// (migration 096).
/// </summary>
/// <remarks>
/// <para>
/// Exige une MariaDB <b>JETABLE</b> portant les migrations 001 a 096, fournie
/// par <c>BILLING_V2_TEST_MARIADB_CONNECTION</c>. Sans elle, la suite echoue en
/// le disant : elle n'est jamais verte par absence de base. Ne JAMAIS la
/// pointer vers une base de recette ou de production.
/// </para>
/// <para>
/// Les lignes sont creees par le code de production
/// (<see cref="MariaDbSignupRepository"/>), puis la requete d'export reelle
/// est executee : c'est la seule preuve que la branche SQL du compte principal
/// retient exactement ce que <see cref="PrimaryIdentityBootstrapPolicy"/>
/// decrit.
/// </para>
/// </remarks>
public static class PrimaryIdentityBootstrapSchemaTests
{
    private const string ConnectionVariable = "BILLING_V2_TEST_MARIADB_CONNECTION";

    public static async Task RunAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} n'est pas defini. Cette suite exige une "
                + "MariaDB jetable portant les migrations 001 a 096. Elle ne "
                + "peut pas etre consideree comme passee sans base.");
        }

        var sql = new SqlRuntimeConfiguration(
            PortalPersistenceMode.MariaDb,
            "mariadb",
            connectionString,
            "TEST",
            ConfigurationValid: true);
        var signups = new MariaDbSignupRepository(sql);
        var koxo = new MariaDbKoxoRepository(sql);
        var links = new MariaDbActiveDirectoryLinkRepository(sql);

        // Cart non verifie, secret depose : exclu.
        var cart = await CreateSelfServiceAccountAsync(signups, "cart", withSecret: true);
        var cartBirth = await signups.GetPrimaryIdentityBootstrapAsync(cart.UserId, CancellationToken.None);
        Ensure(
            cartBirth?.Status == PrimaryIdentityBootstrapStatuses.KoxoPending
            && cartBirth.Origin == PrimaryIdentityBootstrapOrigins.SelfServiceCart
            && cartBirth.EmailVerificationRequired
            && !cartBirth.EmailVerified
            && cartBirth.SecretAvailable
            && !cartBirth.HasUserLink
            && cartBirth.KoxoUniqueIdentifier == cartBirth.PortalUserKoxoUniqueIdentifier,
            "ApproveAsync Cart : amorcage koxo_pending + secret, sans lien, dans la transaction du compte.");
        await AssertExportedAsync(koxo, cart.UserId, false, "Principal self-service non verifie : exclu.");

        // Verification : inclus, et KoXo doit creer (mot de passe exige).
        await signups.MarkEmailVerifiedAsync(cart.SignupId, CancellationToken.None);
        var included = await FindCandidateAsync(koxo, cart.UserId);
        Ensure(included is not null, "Principal pending + verifie + secret : inclus.");
        Ensure(included!.RequiresPendingPassword, "La ligne exige le mot de passe de la colonne 14.");

        // Standard approuve : awaiting_password, exclu ; set-password : inclus.
        var standard = await CreateStandardAccountAsync(signups, "standard");
        var standardBirth = await signups.GetPrimaryIdentityBootstrapAsync(standard.UserId, CancellationToken.None);
        Ensure(
            standardBirth?.Status == PrimaryIdentityBootstrapStatuses.AwaitingPassword
            && standardBirth.Origin == PrimaryIdentityBootstrapOrigins.Signup
            && !standardBirth.EmailVerificationRequired
            && !standardBirth.SecretAvailable
            && !standardBirth.HasUserLink,
            "ApproveAsync standard : amorcage awaiting_password, sans secret ni lien.");
        await AssertExportedAsync(koxo, standard.UserId, false, "Principal sans secret : exclu.");
        await signups.SetPasswordForPrimaryIdentityBootstrapAsync(
            standard.SignupId,
            standard.UserId,
            "hash-standard",
            NewSecret(),
            DateTime.UtcNow,
            CancellationToken.None);
        await AssertExportedAsync(koxo, standard.UserId, true, "Standard avec secret, sans lien : inclus.");
        var standardRecord = await signups.GetPrimaryIdentityBootstrapAsync(standard.UserId, CancellationToken.None);
        Ensure(
            standardRecord?.Status == PrimaryIdentityBootstrapStatuses.KoxoPending
            && standardRecord.SecretAvailable
            && !standardRecord.HasUserLink,
            "Le secret est depose sans lien AD, dans la meme transaction que l'amorcage.");

        // Principal sans amorcage : exclu, quoi qu'il ait par ailleurs.
        var orphan = await CreateSelfServiceAccountAsync(signups, "orphan", withSecret: true);
        await signups.MarkEmailVerifiedAsync(orphan.SignupId, CancellationToken.None);
        await ExecuteAsync(
            connectionString,
            "DELETE FROM portal_user_identity_bootstrap WHERE portal_user_id = @id;",
            orphan.UserId);
        await AssertExportedAsync(koxo, orphan.UserId, false, "Principal sans amorcage explicite : exclu.");

        // Secret expire : exclu.
        var expired = await CreateSelfServiceAccountAsync(signups, "expired", withSecret: true);
        await signups.MarkEmailVerifiedAsync(expired.SignupId, CancellationToken.None);
        await ExecuteAsync(
            connectionString,
            "UPDATE koxo_pending_directory_passwords SET expires_at = UTC_TIMESTAMP(6) - INTERVAL 1 MINUTE WHERE portal_user_id = @id;",
            expired.UserId);
        await AssertExportedAsync(koxo, expired.UserId, false, "Secret expire : exclu.");

        // Lien pose : la branche ordinaire le porte, sans exiger de secret.
        var directoryObject = new AdDirectoryObjectSummary(
            Guid.NewGuid().ToString("D"),
            "S-1-5-21-1000000001-1000000002-1000000003-7001",
            "user",
            "cart.schema",
            "cart.schema@clients.home.bzh",
            "CART SCHEMA",
            $"CN=cart.schema,OU={cart.CustomerReference},OU=CLIENTS,OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
            string.Empty,
            false);
        var cartRecord = await signups.GetPrimaryIdentityBootstrapAsync(cart.UserId, CancellationToken.None)
            ?? throw new InvalidOperationException("Amorcage Cart introuvable.");
        Ensure(
            !await signups.MarkPrimaryIdentityCompletedAsync(cartRecord.Id, DateTime.UtcNow, CancellationToken.None),
            "Jamais completed avant le lien.");
        Ensure(
            await signups.MarkPrimaryIdentityDirectoryResolvedAsync(cartRecord.Id, directoryObject.ObjectGuid, DateTime.UtcNow, CancellationToken.None),
            "koxo_pending -> directory_ready.");
        Ensure(
            !await signups.MarkPrimaryIdentityDirectoryResolvedAsync(cartRecord.Id, Guid.NewGuid().ToString("D"), DateTime.UtcNow, CancellationToken.None),
            "Un autre objectGUID est refuse.");
        await AssertExportedAsync(koxo, cart.UserId, true, "directory_ready sans lien : reste exporte.");
        Ensure(
            !await signups.MarkPrimaryIdentityCompletedAsync(cartRecord.Id, DateTime.UtcNow, CancellationToken.None),
            "directory_ready sans lien : toujours pas completed.");
        await links.UpsertPortalUserLinkAsync(
            cart.CustomerReference,
            cart.UserId,
            null,
            directoryObject,
            "clients.home.bzh",
            "succeeded",
            DateTime.UtcNow,
            "succeeded",
            DateTime.UtcNow,
            "koxo_pending",
            CancellationToken.None);
        var linked = await FindCandidateAsync(koxo, cart.UserId);
        Ensure(linked is not null && !linked.RequiresPendingPassword, "Lie : exporte par la branche ordinaire.");
        Ensure(
            await signups.MarkPrimaryIdentityCompletedAsync(cartRecord.Id, DateTime.UtcNow, CancellationToken.None),
            "Lien persiste : completed accepte.");

        // Contraintes de schema.
        await ExpectFailureAsync(
            connectionString,
            "UPDATE portal_user_identity_bootstrap SET status = 'completed', directory_object_guid = NULL WHERE portal_user_id = @id;",
            standard.UserId,
            "La base refuse completed sans objectGUID.");
        await ExpectFailureAsync(
            connectionString,
            "UPDATE portal_user_identity_bootstrap SET status = 'ready' WHERE portal_user_id = @id;",
            standard.UserId,
            "La base refuse un etat inconnu.");
        await ExpectFailureAsync(
            connectionString,
            """
            INSERT INTO portal_user_identity_bootstrap
                (id, portal_user_id, customer_id, signup_id, koxo_unique_identifier, origin, status)
            SELECT UUID(), portal_user_id, customer_id, signup_id, koxo_unique_identifier, origin, status
            FROM portal_user_identity_bootstrap WHERE portal_user_id = @id;
            """,
            standard.UserId,
            "Un seul amorcage par compte.");

        // Reprise : un compte sans amorcage recoit un cycle et un jeton.
        var recovery = await signups.RequestPrimaryIdentityRecoveryAsync(
            orphan.SignupId,
            new string('a', 64),
            DateTime.UtcNow.AddHours(24),
            DateTime.UtcNow,
            CancellationToken.None);
        Ensure(recovery.Succeeded, "La reprise d'un compte sans amorcage aboutit.");
        Ensure(
            (await signups.RequestPrimaryIdentityRecoveryAsync(
                Guid.NewGuid().ToString("D"),
                new string('d', 64),
                DateTime.UtcNow.AddHours(24),
                DateTime.UtcNow,
                CancellationToken.None)).Code == PrimaryIdentityRecoveryCodes.SignupNotFound,
            "Reprise d'une demande inconnue : SIGNUP_NOT_FOUND, sans exception.");
        Ensure(
            (await signups.GetPrimaryIdentityBootstrapAsync(orphan.UserId, CancellationToken.None))?.Status
                == PrimaryIdentityBootstrapStatuses.AwaitingPassword,
            "Le compte repris attend un nouveau mot de passe.");

        await RunRollbackChecksAsync(signups, connectionString);
        await RunConcurrencyChecksAsync(signups, connectionString);

        Console.WriteLine("Tests schema amorcage identite AD du compte principal reussis.");
    }

    /// <summary>
    /// Une unite de travail qui echoue APRES avoir ecrit ne laisse rien : ni
    /// compte sans cycle, ni cycle sans compte, ni secret orphelin.
    /// </summary>
    private static async Task RunRollbackChecksAsync(
        MariaDbSignupRepository signups,
        string connectionString)
    {
        // ApproveAsync : le depot du secret, posterieur a l'insertion du client,
        // du compte et de l'amorcage, est refuse par la base (key_id trop long).
        var (signupId, customer, user) = await InsertPendingAsync(signups, "rollback-approve", "cart");
        var customerId = Guid.NewGuid().ToString("D");
        var userId = Guid.NewGuid().ToString("D");
        var failed = false;
        try
        {
            await signups.ApproveAsync(
                SelfServiceApproval(
                    signupId,
                    customerId,
                    userId,
                    customer,
                    user,
                    new PortalPasswordSecret("opaque", new string('k', 200), DateTime.UtcNow.AddHours(1))),
                CancellationToken.None);
        }
        catch (MySqlException)
        {
            failed = true;
        }

        Ensure(failed, "Le depot du secret echoue apres l'insertion de l'amorcage.");
        Ensure(
            await CountAsync(connectionString, "SELECT COUNT(*) FROM customers WHERE id = @id;", customerId) == 0,
            "Rollback ApproveAsync : aucun client.");
        Ensure(
            await CountAsync(connectionString, "SELECT COUNT(*) FROM portal_users WHERE id = @id;", userId) == 0,
            "Rollback ApproveAsync : aucun compte.");
        Ensure(
            await CountAsync(connectionString, "SELECT COUNT(*) FROM portal_user_identity_bootstrap WHERE signup_id = @id;", signupId) == 0,
            "Rollback ApproveAsync : aucun amorcage.");
        Ensure(
            await CountAsync(connectionString, "SELECT COUNT(*) FROM koxo_pending_directory_passwords WHERE portal_user_id = @id;", userId) == 0,
            "Rollback ApproveAsync : aucun secret.");
        Ensure(
            await CountAsync(
                connectionString,
                "SELECT COUNT(*) FROM signup_pending WHERE id = @id AND status = 'email_pending' AND approved_user_id IS NULL;",
                signupId) == 1,
            "Rollback ApproveAsync : la demande reste en attente.");

        // Rejouable apres l'echec : aucune ligne ni verrou residuel.
        var retryUserId = Guid.NewGuid().ToString("D");
        Ensure(
            await signups.ApproveAsync(
                SelfServiceApproval(signupId, Guid.NewGuid().ToString("D"), retryUserId, customer, user, NewSecret()),
                CancellationToken.None) is not null,
            "ApproveAsync rejoue apres rollback.");
        Ensure(
            (await signups.GetPrimaryIdentityBootstrapAsync(retryUserId, CancellationToken.None))?.Status
                == PrimaryIdentityBootstrapStatuses.KoxoPending,
            "Le rejeu cree l'amorcage attendu.");

        // Set-password : condensat et jeton ecrits, puis refus sur un amorcage
        // `completed` sans lien. Rien ne doit subsister.
        var standard = await CreateStandardAccountAsync(signups, "rollback-set");
        await ExecuteAsync(
            connectionString,
            """
            UPDATE portal_user_identity_bootstrap
            SET status = 'completed',
                directory_object_guid = UUID(),
                directory_linked_at = UTC_TIMESTAMP(6)
            WHERE portal_user_id = @id;
            """,
            standard.UserId);
        var hashBefore = await ScalarStringAsync(
            connectionString,
            "SELECT password_hash FROM portal_users WHERE id = @id;",
            standard.UserId);
        var tokenBefore = await ScalarStringAsync(
            connectionString,
            "SELECT password_setup_token_hash FROM signup_pending WHERE approved_user_id = @id;",
            standard.UserId);
        Ensure(tokenBefore is not null, "Le jeton de definition existe avant la tentative.");
        var refused = false;
        try
        {
            await signups.SetPasswordForPrimaryIdentityBootstrapAsync(
                standard.SignupId,
                standard.UserId,
                "hash-rollback-set",
                NewSecret(),
                DateTime.UtcNow,
                CancellationToken.None);
        }
        catch (InvalidOperationException exception)
            when (exception.Message == "PRIMARY_IDENTITY_COMPLETED_WITHOUT_LINK")
        {
            refused = true;
        }

        Ensure(refused, "Set-password refuse un amorcage completed sans lien.");
        Ensure(
            await ScalarStringAsync(connectionString, "SELECT password_hash FROM portal_users WHERE id = @id;", standard.UserId)
                == hashBefore,
            "Rollback set-password : condensat inchange.");
        Ensure(
            await ScalarStringAsync(
                connectionString,
                "SELECT password_setup_token_hash FROM signup_pending WHERE approved_user_id = @id;",
                standard.UserId) == tokenBefore,
            "Rollback set-password : jeton inchange.");
        Ensure(
            await CountAsync(connectionString, "SELECT COUNT(*) FROM koxo_pending_directory_passwords WHERE portal_user_id = @id;", standard.UserId) == 0,
            "Rollback set-password : aucun secret.");

        Console.WriteLine("  rollback ApproveAsync et set-password : OK");
    }

    /// <summary>
    /// Courses reelles sous InnoDB : les verrous et l'index unique gardent un
    /// seul cycle par compte.
    /// </summary>
    private static async Task RunConcurrencyChecksAsync(
        MariaDbSignupRepository signups,
        string connectionString)
    {
        // Deux validations humaines simultanees de la meme demande.
        var (signupId, customer, user) = await InsertPendingAsync(signups, "race-approve", null);
        await signups.MarkEmailVerifiedAsync(signupId, CancellationToken.None);
        var outcomes = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(_ => TryAsync(() => signups.ApproveAsync(
                StandardApproval(signupId, customer, user, Guid.NewGuid().ToString("D")),
                CancellationToken.None))));
        Console.WriteLine($"  approbations concurrentes : {string.Join(", ", outcomes.Select(o => o.Describe()))}");
        Ensure(outcomes.Count(o => o.Value is not null) == 1, "Deux approbations concurrentes : une seule aboutit.");
        Ensure(
            outcomes.All(o => o.Error is null),
            "Deux approbations concurrentes : le perdant est refuse proprement (null), sans exception.");
        Ensure(
            await signups.ApproveAsync(
                StandardApproval(signupId, customer, user, Guid.NewGuid().ToString("D")),
                CancellationToken.None) is null,
            "Une demande deja approuvee est refusee proprement (null).");
        Ensure(
            await signups.ApproveAsync(
                StandardApproval(Guid.NewGuid().ToString("D"), customer, user, Guid.NewGuid().ToString("D")),
                CancellationToken.None) is null,
            "Une demande inconnue est refusee proprement (null).");
        Ensure(
            await CountAsync(connectionString, "SELECT COUNT(*) FROM portal_user_identity_bootstrap WHERE signup_id = @id;", signupId) == 1,
            "Deux approbations concurrentes : un seul amorcage.");
        Ensure(
            await CountAsync(
                connectionString,
                """
                SELECT COUNT(*) FROM signup_pending s
                JOIN portal_user_identity_bootstrap b
                  ON b.signup_id = s.id AND b.portal_user_id = s.approved_user_id
                WHERE s.id = @id;
                """,
                signupId) == 1,
            "L'amorcage survivant designe le compte approuve.");

        // Deux set-password simultanes d'un compte anterieur a 096.
        var legacy = await CreateStandardAccountAsync(signups, "race-set");
        await ExecuteAsync(
            connectionString,
            "DELETE FROM portal_user_identity_bootstrap WHERE portal_user_id = @id;",
            legacy.UserId);
        var setOutcomes = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(index => TryAsync(async () =>
            {
                await signups.SetPasswordForPrimaryIdentityBootstrapAsync(
                    legacy.SignupId,
                    legacy.UserId,
                    "hash-race-" + index,
                    NewSecret(),
                    DateTime.UtcNow,
                    CancellationToken.None);
                return (object?)true;
            })));
        Console.WriteLine($"  set-password concurrents : {string.Join(", ", setOutcomes.Select(o => o.Describe()))}");
        Ensure(setOutcomes.Any(o => o.Value is not null), "Au moins un set-password aboutit.");
        Ensure(
            await CountAsync(connectionString, "SELECT COUNT(*) FROM portal_user_identity_bootstrap WHERE portal_user_id = @id;", legacy.UserId) == 1,
            "Set-password concurrents : un seul amorcage.");
        Ensure(
            (await signups.GetPrimaryIdentityBootstrapAsync(legacy.UserId, CancellationToken.None)) is
            {
                Status: PrimaryIdentityBootstrapStatuses.KoxoPending,
                SecretAvailable: true,
                HasUserLink: false
            },
            "Set-password concurrents : koxo_pending avec secret.");

        // Deux reprises simultanees d'un compte sans amorcage.
        var orphan = await CreateSelfServiceAccountAsync(signups, "race-recovery", withSecret: false);
        await signups.MarkEmailVerifiedAsync(orphan.SignupId, CancellationToken.None);
        await ExecuteAsync(
            connectionString,
            "DELETE FROM portal_user_identity_bootstrap WHERE portal_user_id = @id;",
            orphan.UserId);
        var recoveryOutcomes = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(index => TryAsync(async () =>
                (object?)await signups.RequestPrimaryIdentityRecoveryAsync(
                    orphan.SignupId,
                    new string((char)('c' + index), 64),
                    DateTime.UtcNow.AddHours(24),
                    DateTime.UtcNow,
                    CancellationToken.None))));
        Console.WriteLine($"  reprises concurrentes : {string.Join(", ", recoveryOutcomes.Select(o => o.Describe()))}");
        Ensure(
            recoveryOutcomes.Any(o => o.Value is PrimaryIdentityRecoveryTarget { Succeeded: true }),
            "Au moins une reprise aboutit.");
        Ensure(
            await CountAsync(connectionString, "SELECT COUNT(*) FROM portal_user_identity_bootstrap WHERE portal_user_id = @id;", orphan.UserId) == 1,
            "Reprises concurrentes : un seul amorcage.");
        Ensure(
            (await signups.GetPrimaryIdentityBootstrapAsync(orphan.UserId, CancellationToken.None))?.Status
                == PrimaryIdentityBootstrapStatuses.AwaitingPassword,
            "Reprises concurrentes : le compte attend un nouveau mot de passe.");
    }

    private sealed record Outcome(object? Value, Exception? Error)
    {
        public string Describe()
            => Error switch
            {
                null => Value is null ? "null" : Value is PrimaryIdentityRecoveryTarget target ? target.Code : "ok",
                MySqlException sql => $"MySqlException({sql.ErrorCode})",
                _ => Error.GetType().Name + "(" + Error.Message + ")"
            };
    }

    private static async Task<Outcome> TryAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return new Outcome(await action(), null);
        }
        catch (Exception exception) when (exception is MySqlException or InvalidOperationException)
        {
            return new Outcome(null, exception);
        }
    }

    private sealed record Account(string SignupId, string UserId, string CustomerReference);

    private static PortalPasswordSecret NewSecret()
        => new("opaque-ciphertext-for-schema-test", "schema-key", DateTime.UtcNow.AddHours(1));

    private static SignupApprovalRequest SelfServiceApproval(
        string signupId,
        string customerId,
        string userId,
        SignupCustomerData customer,
        SignupUserData user,
        PortalPasswordSecret? secret)
        => new(
            signupId,
            customerId,
            CustomerReferenceGenerator.Generate(),
            customer,
            user,
            userId,
            null,
            null,
            InitialPasswordHash: "hash-" + userId,
            EmailVerified: false,
            SelfServiceFlow: "cart",
            InitialKoxoSecret: secret);

    private static SignupApprovalRequest StandardApproval(
        string signupId,
        SignupCustomerData customer,
        SignupUserData user,
        string userId)
        => new(
            signupId,
            Guid.NewGuid().ToString("D"),
            CustomerReferenceGenerator.Generate(),
            customer,
            user,
            userId,
            new string('b', 64),
            DateTime.UtcNow.AddHours(24));

    private static async Task<Account> CreateSelfServiceAccountAsync(
        MariaDbSignupRepository signups,
        string label,
        bool withSecret)
    {
        var (signupId, customer, user) = await InsertPendingAsync(signups, label, "cart");
        var userId = Guid.NewGuid().ToString("D");
        var request = SelfServiceApproval(
            signupId,
            Guid.NewGuid().ToString("D"),
            userId,
            customer,
            user,
            withSecret ? NewSecret() : null);
        var approval = await signups.ApproveAsync(request, CancellationToken.None);
        Ensure(approval is not null, "Compte self-service cree.");
        return new Account(signupId, userId, request.CustomerReference);
    }

    private static async Task<Account> CreateStandardAccountAsync(
        MariaDbSignupRepository signups,
        string label)
    {
        var (signupId, customer, user) = await InsertPendingAsync(signups, label, null);
        await signups.MarkEmailVerifiedAsync(signupId, CancellationToken.None);
        var userId = Guid.NewGuid().ToString("D");
        var request = StandardApproval(signupId, customer, user, userId);
        var approval = await signups.ApproveAsync(request, CancellationToken.None);
        Ensure(approval is not null, "Compte standard approuve.");
        return new Account(signupId, userId, request.CustomerReference);
    }

    private static async Task<(string SignupId, SignupCustomerData Customer, SignupUserData User)>
        InsertPendingAsync(MariaDbSignupRepository signups, string label, string? flow)
    {
        var email = $"bootstrap.schema.{label}.{Guid.NewGuid():N}@example.invalid";
        var customer = new SignupCustomerData(
            "professional", "Societe Schema", email, "0102030405",
            "1 rue du Test", null, "29000", "Quimper", "FR");
        var user = new SignupUserData(
            "madame", "Alice", "Schema", "1990-01-02", null,
            "Alice Schema", email, "0102030405", true);
        var signupId = Guid.NewGuid().ToString("D");
        await signups.InsertPendingAsync(
            new SignupInsert(
                signupId,
                "Societe Schema",
                "Alice Schema",
                email,
                "0102030405",
                null,
                customer,
                user,
                Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                DateTime.UtcNow.AddHours(24),
                "127.0.0.1",
                "schema-tests",
                SelfServiceFlow: flow),
            CancellationToken.None);
        return (signupId, customer, user);
    }

    private static async Task<KoxoExportCandidate?> FindCandidateAsync(
        MariaDbKoxoRepository koxo,
        string portalUserId)
        => (await koxo.ListExportCandidatesAsync(CancellationToken.None))
            .SingleOrDefault(candidate => candidate.PortalUserId == portalUserId);

    private static async Task AssertExportedAsync(
        MariaDbKoxoRepository koxo,
        string portalUserId,
        bool expected,
        string message)
        => Ensure((await FindCandidateAsync(koxo, portalUserId) is not null) == expected, message);

    private static async Task ExecuteAsync(string connectionString, string sql, string portalUserId)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", portalUserId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(string connectionString, string sql, string id)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", id);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }

    private static async Task<long> CountAsync(string connectionString, string sql, string id)
        => Convert.ToInt64(await ScalarAsync(connectionString, sql, id));

    private static async Task<string?> ScalarStringAsync(string connectionString, string sql, string id)
        => await ScalarAsync(connectionString, sql, id) is { } value ? Convert.ToString(value) : null;

    private static async Task ExpectFailureAsync(
        string connectionString,
        string sql,
        string portalUserId,
        string message)
    {
        try
        {
            await ExecuteAsync(connectionString, sql, portalUserId);
        }
        catch (MySqlException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
