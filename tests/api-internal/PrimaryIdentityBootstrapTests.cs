using System.Security.Cryptography;
using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Email;
using Kermaria.ApiInternal.Services.Provisioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Amorcage de l'identite AD du compte client principal (migration 096).
/// </summary>
/// <remarks>
/// <para>
/// Invariant : tout compte client principal possede une identite AD. Les
/// parcours signup standard, Cart et VPS convergent vers le meme cycle :
/// compte portail -&gt; CLI-NNNNNN -&gt; secret KoXo chiffre AVANT tout lien
/// -&gt; amorcage explicite -&gt; export KoXo -&gt; adoption par employeeNumber
/// -&gt; customer_ad_links(user) -&gt; completed.
/// </para>
/// <para>
/// Persistance <b>mock</b> : la regle SQL d'export n'est pas executee ici. Sa
/// forme est verrouillee par <see cref="VerifyExportQueryPrimaryBranch"/> et
/// sa transcription C# (<see cref="PrimaryIdentityBootstrapPolicy"/>) est
/// exercee par la table de verite. L'execution reelle reste a prouver sur une
/// MariaDB jetable.
/// </para>
/// </remarks>
public static class PrimaryIdentityBootstrapTests
{
    private const string SignupPassword = "NOT_A_REAL_PASSWORD_BOOTSTRAP_1";
    private const string RecoveryPassword = "NOT_A_REAL_PASSWORD_BOOTSTRAP_2";
    private const string KoxoRoot = "OU=CLIENTS,OU=KoXoAdm,DC=clients,DC=home,DC=bzh";

    private static int _sequence;

    public static async Task RunAsync()
    {
        VerifyExportQueryPrimaryBranch();
        VerifyExportPolicyTruthTable();
        VerifyDirectoryObjectValidation();
        VerifySecretIsSealedNeverPlaintext();

        await VerifyCartBootstrapUnderKoxoAuthorityAsync();
        await VerifyStandardSignupBreaksTheDeadlockAsync();
        await VerifyVpsAccountGetsItsIdentityAsync();
        await VerifyDisabledDirectoryKeepsExplicitPendingStateAsync();
        await VerifyAdoptionFailsClosedAsync();
        await VerifyRetryAfterKoxoCreationBeforeLinkAsync();
        await VerifyExistingLinkIsNoopAsync();
        await VerifyConcurrentConvergenceYieldsOneIdentityAsync();
        await VerifyIdentityIsNeverTransferredAsync();
        await VerifyDoubleSignupCreatesOneBootstrapAsync();
        await VerifyKoxoTriggerIsRateLimitedAsync();
        await VerifyLegacyAccountRecoveryAsync();
        await VerifyUnverifiedLegacyAccountRecoversThroughVerificationAsync();
        await VerifyExpiredSecretFallsBackToRecoveryAsync();
        await VerifyCartEndToEndResolvesProvisioningIdentityAsync();

        Console.WriteLine("Tests amorcage identite AD du compte principal reussis.");
    }

    // ==================================================================
    // E. Regle d'export : forme SQL et transcription C#
    // ==================================================================

    private static void VerifyExportQueryPrimaryBranch()
    {
        var collapsed = Collapse(KoxoExportCandidateQuery.Sql);
        const string branchStart =
            "OR ( customer.is_demo = FALSE AND ad_link.portal_user_id IS NULL";
        const string branchEnd =
            "AND NOT (customer.is_demo = TRUE AND customer.demo_kind = 'showcase')";
        var start = collapsed.IndexOf(branchStart, StringComparison.Ordinal);
        Assert(start >= 0, "La branche du compte principal existe et exige l'absence de lien.");
        var end = collapsed.IndexOf(branchEnd, start, StringComparison.Ordinal);
        Assert(end > start, "La branche du compte principal reste soumise a l'exclusion des vitrines.");
        var branch = collapsed[start..end];

        foreach (var mandatory in new[]
        {
            "portal_user.personal_title IS NOT NULL",
            "portal_user.given_name IS NOT NULL",
            "portal_user.surname IS NOT NULL",
            "portal_user.birth_date IS NOT NULL",
            "portal_user.koxo_unique_identifier IS NOT NULL",
            "FROM portal_user_identity_bootstrap bootstrap WHERE bootstrap.portal_user_id = portal_user.id",
            "AND bootstrap.customer_id = portal_user.customer_id",
            "AND bootstrap.koxo_unique_identifier = portal_user.koxo_unique_identifier",
            "AND bootstrap.status IN ( 'koxo_pending', 'directory_ready')",
            "AND (bootstrap.email_verification_required = FALSE OR portal_user.email_verified_at IS NOT NULL)",
            "FROM koxo_pending_directory_passwords pending_secret WHERE pending_secret.portal_user_id = portal_user.id AND pending_secret.expires_at > UTC_TIMESTAMP(6)",
            "AND NOT EXISTS ( SELECT 1 FROM billing_v2_user_identity_provisioning other_lifecycle WHERE other_lifecycle.portal_user_id = portal_user.id )"
        })
        {
            Assert(
                branch.Contains(mandatory, StringComparison.Ordinal),
                $"La branche du compte principal doit exiger « {mandatory} ».");
        }

        foreach (var forbidden in new[] { "'awaiting_password'", "'completed'", "'failed'", "password_hash" })
        {
            Assert(
                !branch.Contains(forbidden, StringComparison.Ordinal),
                $"La branche du compte principal ne doit jamais retenir « {forbidden} ».");
        }

        // Jamais « tout portal_user sans lien AD » : l'absence de lien
        // n'apparait que dans l'expression requires_pending_password et dans
        // cette branche, ou elle est adossee a un amorcage explicite.
        Assert(
            CountOccurrences(collapsed, "ad_link.portal_user_id IS NULL") == 2,
            "L'absence de lien AD ne doit jamais suffire seule a rendre un compte exportable.");

        // Les branches historiques sont intactes.
        foreach (var historical in new[]
        {
            "ad_link.portal_user_id IS NOT NULL",
            "customer.is_demo = TRUE AND customer.demo_kind = 'trial'",
            "AND slot.is_primary = 0",
            "(ad_link.portal_user_id IS NULL AND customer.is_demo = FALSE) AS requires_pending_password",
            branchEnd
        })
        {
            Assert(
                collapsed.Contains(historical, StringComparison.Ordinal),
                $"La regle historique « {historical} » doit etre conservee.");
        }
    }

    private static void VerifyExportPolicyTruthTable()
    {
        var exportable = new PrimaryIdentityBootstrapRecord(
            "bootstrap-1",
            "portal-user-1",
            "customer-1",
            "CLI20260925ABCD",
            KoxoGroupReference: null,
            "signup-1",
            "CLI-000123",
            "CLI-000123",
            PrimaryIdentityBootstrapOrigins.SelfServiceCart,
            EmailVerificationRequired: true,
            EmailVerified: true,
            PrimaryIdentityBootstrapStatuses.KoxoPending,
            FailureCode: null,
            DirectoryObjectGuid: null,
            KoxoTriggeredAtUtc: null,
            PortalUserActive: true,
            CustomerActive: true,
            IsDemo: false,
            DemoKind: null,
            IdentityComplete: true,
            HasUserLink: false,
            SecretAvailable: true,
            HasAdditionalUserLifecycle: false);

        Assert(
            PrimaryIdentityBootstrapPolicy.GetExportBlocker(exportable) is null,
            "Principal koxo_pending, verifie, avec secret et sans lien : exportable.");
        Assert(
            PrimaryIdentityBootstrapPolicy.GetExportBlocker(
                exportable with { Status = PrimaryIdentityBootstrapStatuses.DirectoryReady }) is null,
            "directory_ready reste exportable : sortir du CSV desactiverait l'objet.");
        Assert(
            PrimaryIdentityBootstrapPolicy.GetExportBlocker(
                exportable with { EmailVerificationRequired = false, EmailVerified = false }) is null,
            "Inscription standard : pas d'exigence de verification apres approbation.");

        (PrimaryIdentityBootstrapRecord Record, string Expected, string Reason)[] blocked =
        [
            (exportable with { EmailVerified = false },
                PrimaryIdentityBootstrapCodes.EmailVerificationRequired,
                "Principal self-service non verifie : exclu."),
            (exportable with { Status = PrimaryIdentityBootstrapStatuses.AwaitingPassword },
                PrimaryIdentityBootstrapCodes.AwaitingPassword,
                "Sans secret depose : exclu."),
            (exportable with { Status = PrimaryIdentityBootstrapStatuses.Failed },
                PrimaryIdentityBootstrapCodes.Failed,
                "Conflit : exclu."),
            (exportable with { Status = PrimaryIdentityBootstrapStatuses.Completed },
                PrimaryIdentityBootstrapCodes.Completed,
                "Termine : c'est la branche du lien qui le porte."),
            (exportable with { HasUserLink = true },
                PrimaryIdentityBootstrapCodes.Completed,
                "Lien deja pose : pas d'exception."),
            (exportable with { SecretAvailable = false },
                PrimaryIdentityBootstrapCodes.SecretMissing,
                "Secret absent : la ligne serait invalide et bloquerait l'export global."),
            (exportable with { IsDemo = true, DemoKind = "showcase" },
                PrimaryIdentityBootstrapCodes.NotEligible,
                "Vitrine : toujours exclue."),
            (exportable with { IsDemo = true, DemoKind = "trial" },
                PrimaryIdentityBootstrapCodes.NotEligible,
                "Essai : son pipeline propre, pas celui du principal."),
            (exportable with { HasAdditionalUserLifecycle = true },
                PrimaryIdentityBootstrapCodes.NotEligible,
                "Utilisateur additionnel : son cycle propre."),
            (exportable with { PortalUserKoxoUniqueIdentifier = "CLI-000124" },
                PrimaryIdentityBootstrapCodes.NotEligible,
                "Identifiant KoXo divergent : exclu."),
            (exportable with { KoxoUniqueIdentifier = "CLI-12", PortalUserKoxoUniqueIdentifier = "CLI-12" },
                PrimaryIdentityBootstrapCodes.NotEligible,
                "Identifiant KoXo invalide : exclu."),
            (exportable with { IdentityComplete = false },
                PrimaryIdentityBootstrapCodes.NotEligible,
                "Etat civil incomplet : exclu."),
            (exportable with { CustomerActive = false },
                PrimaryIdentityBootstrapCodes.NotEligible,
                "Client inactif : exclu."),
            (exportable with { PortalUserActive = false },
                PrimaryIdentityBootstrapCodes.NotEligible,
                "Compte portail inactif : exclu.")
        ];
        foreach (var (record, expected, reason) in blocked)
        {
            Assert(
                PrimaryIdentityBootstrapPolicy.GetExportBlocker(record) == expected,
                reason);
        }
    }

    // ==================================================================
    // F. Verification stricte de l'objet adopte
    // ==================================================================

    private static void VerifyDirectoryObjectValidation()
    {
        var record = new PrimaryIdentityBootstrapRecord(
            "bootstrap-2", "portal-user-2", "customer-2", "CLI20260925WXYZ", null,
            "signup-2", "CLI-000200", "CLI-000200", PrimaryIdentityBootstrapOrigins.Signup,
            false, true, PrimaryIdentityBootstrapStatuses.KoxoPending, null, null, null,
            true, true, false, null, true, false, true, false);
        var valid = new AdDirectoryObjectSummary(
            Guid.NewGuid().ToString("D"),
            "S-1-5-21-1111111111-2222222222-3333333333-1105",
            "user",
            "alice.martin",
            "alice.martin@clients.home.bzh",
            "Alice MARTIN",
            $"CN=Alice MARTIN,OU=CLI20260925WXYZ,{KoxoRoot}",
            string.Empty,
            false);

        Assert(
            PrimaryIdentityBootstrapPolicy.ValidateDirectoryObject(valid, record) is null,
            "Un objet complet dans l'OU du client est adoptable.");

        (AdDirectoryObjectSummary Candidate, string Expected, string Reason)[] refused =
        [
            (valid with { DistinguishedName = $"CN=Alice MARTIN,OU=CLI20260925ZZZZ,{KoxoRoot}" },
                PrimaryIdentityBootstrapCodes.CustomerMismatch,
                "Objet dans l'OU d'un autre client : refuse."),
            (valid with { DistinguishedName = $"CN=Alice MARTIN,OU=CLI20260925WXYZ-OLD,{KoxoRoot}" },
                PrimaryIdentityBootstrapCodes.CustomerMismatch,
                "Rapprochement par prefixe d'OU : refuse."),
            (valid with { CustomerReference = "CLI20260925ZZZZ" },
                PrimaryIdentityBootstrapCodes.CustomerMismatch,
                "Reference client explicite divergente : refusee."),
            (valid with { ObjectGuid = "pas-un-guid" },
                PrimaryIdentityBootstrapCodes.InvalidDirectoryObject,
                "objectGUID invalide : refuse."),
            (valid with { ObjectGuid = Guid.Empty.ToString("D") },
                PrimaryIdentityBootstrapCodes.InvalidDirectoryObject,
                "objectGUID nul : refuse."),
            (valid with { ObjectSid = "S-1-5-32-544" },
                PrimaryIdentityBootstrapCodes.InvalidDirectoryObject,
                "SID hors domaine : refuse."),
            (valid with { SamAccountName = "un nom avec espaces" },
                PrimaryIdentityBootstrapCodes.InvalidDirectoryObject,
                "sAMAccountName invalide : refuse."),
            (valid with { SamAccountName = new string('a', 21) },
                PrimaryIdentityBootstrapCodes.InvalidDirectoryObject,
                "sAMAccountName trop long : refuse."),
            (valid with { DistinguishedName = " " },
                PrimaryIdentityBootstrapCodes.InvalidDirectoryObject,
                "DN vide : refuse."),
            (valid with { ObjectType = "group" },
                PrimaryIdentityBootstrapCodes.InvalidDirectoryObject,
                "Objet non utilisateur : refuse.")
        ];
        foreach (var (candidate, expected, reason) in refused)
        {
            Assert(
                PrimaryIdentityBootstrapPolicy.ValidateDirectoryObject(candidate, record) == expected,
                reason);
        }
    }

    // ==================================================================
    // D. Secret : chiffre, lie au compte, jamais en clair
    // ==================================================================

    private static void VerifySecretIsSealedNeverPlaintext()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var protector = KoxoPendingPasswordProtector.TryCreate(key)
            ?? throw new InvalidOperationException("Protecteur de test indisponible.");
        var store = new MariaDbKoxoPendingPasswordStore(
            new SqlRuntimeConfiguration(
                PortalPersistenceMode.MariaDb,
                "mariadb",
                "Server=127.0.0.1;Database=unused_seal_only;",
                "test",
                true),
            protector,
            TimeSpan.FromMinutes(60),
            NullLogger<MariaDbKoxoPendingPasswordStore>.Instance);

        var secret = store.Seal("portal-user-sealed", SignupPassword)
            ?? throw new InvalidOperationException("Le scellement doit aboutir avec une cle.");
        Assert(
            !secret.Ciphertext.Contains(SignupPassword, StringComparison.Ordinal),
            "Le secret depose ne contient jamais le mot de passe en clair.");
        Assert(
            protector.Unprotect(secret.Ciphertext, "portal-user-sealed") == SignupPassword,
            "Seul le protecteur, pour ce compte, restitue le mot de passe destine a KoXo.");
        Assert(
            protector.Unprotect(secret.Ciphertext, "portal-user-other") is null,
            "Le chiffre deplace sur un autre compte est indechiffrable.");

        // L'approbation ne transporte le secret que scelle : aucune propriete
        // texte ne peut y porter un mot de passe clair.
        var textProperties = typeof(SignupApprovalRequest).GetProperties()
            .Where(property => property.PropertyType == typeof(string)
                && property.Name.Contains("Password", StringComparison.Ordinal))
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert(
            textProperties.SequenceEqual(["InitialPasswordHash", "PasswordSetupTokenHash"]),
            "La demande d'approbation ne porte que des condensats, jamais de clair.");
        Assert(
            typeof(SignupApprovalRequest).GetProperty("InitialKoxoSecret")?.PropertyType
                == typeof(PortalPasswordSecret),
            "Le secret initial voyage deja scelle.");
    }

    // ==================================================================
    // A. Cart sous autorite KoXo
    // ==================================================================

    private static async Task VerifyCartBootstrapUnderKoxoAuthorityAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var email = fixture.NextEmail("cart");
        var signup = await fixture.Service.CompleteSelfServiceCartAsync(
            fixture.Payload(email, cart: true),
            "corr-cart",
            CancellationToken.None);
        Assert(
            signup.Succeeded && signup.Session is not null,
            "Le signup Cart cree le compte et ouvre la session.");

        var userId = fixture.PortalUserIdFor(email);
        var record = await fixture.RecordAsync(userId);
        Assert(record.Status == PrimaryIdentityBootstrapStatuses.KoxoPending, "Amorcage cree en koxo_pending.");
        Assert(record.Origin == PrimaryIdentityBootstrapOrigins.SelfServiceCart, "Origine Cart tracee.");
        Assert(record.EmailVerificationRequired && !record.EmailVerified, "E-mail pas encore prouve.");
        Assert(
            KoxoDirectoryTopology.IsValidUniqueIdentifier(record.KoxoUniqueIdentifier)
            && record.KoxoUniqueIdentifier == record.PortalUserKoxoUniqueIdentifier,
            "koxo_unique_identifier durable et identique des deux cotes.");
        Assert(
            record.SecretAvailable && !record.HasUserLink,
            "D : le secret KoXo est disponible AVANT tout lien AD.");
        Assert(
            JsonSerializer.Serialize(record).Contains(SignupPassword, StringComparison.Ordinal) is false,
            "D : l'amorcage relu ne porte jamais le mot de passe.");

        // Avant verification : ni export, ni creation, ni adoption.
        Assert(
            PrimaryIdentityBootstrapPolicy.GetExportBlocker(record)
                == PrimaryIdentityBootstrapCodes.EmailVerificationRequired,
            "Avant verification : non exportable.");
        var early = await fixture.Service.ConvergePrimaryIdentityAsync(userId, CancellationToken.None);
        Assert(
            early.Code == PrimaryIdentityBootstrapCodes.EmailVerificationRequired,
            "Avant verification : aucune action annuaire.");
        await fixture.Service.ConvergePendingPrimaryIdentitiesAsync(50, CancellationToken.None);
        Assert(fixture.Koxo.ResolveCalls.Count == 0, "Aucune resolution annuaire avant verification.");
        Assert(!await fixture.SimulateKoxoSyncAsync(userId), "KoXo ne cree rien pour un compte non exportable.");

        // Verification e-mail : le compte devient exportable, la synchro part.
        var verified = await fixture.Service.VerifyEmailAsync(
            fixture.Email.LastToken(email, EmailKind.Verification),
            CancellationToken.None);
        Assert(verified.Succeeded && verified.Code == "EMAIL_VERIFIED", "Verification acceptee.");
        record = await fixture.RecordAsync(userId);
        Assert(PrimaryIdentityBootstrapPolicy.GetExportBlocker(record) is null, "Apres verification : exportable.");
        Assert(
            fixture.Trigger.Requests.Any(request =>
                request.PortalUserId == userId && request.Trigger == "primary_identity_email_verified"),
            "La verification relance la synchronisation KoXo.");

        // KoXo cree l'identite ; l'adoption pose le lien.
        Assert(await fixture.SimulateKoxoSyncAsync(userId), "KoXo cree l'identite du compte exportable.");
        var converged = await fixture.Service.ConvergePrimaryIdentityAsync(userId, CancellationToken.None);
        Assert(converged.Succeeded && converged.Code == PrimaryIdentityBootstrapCodes.Completed, "Identite adoptee.");

        record = await fixture.RecordAsync(userId);
        var link = await fixture.Links.FindUserLinkByPortalUserIdAsync(userId, CancellationToken.None);
        Assert(link is not null, "customer_ad_links(user) persiste.");
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.Completed
            && string.Equals(link!.ObjectGuid, fixture.Koxo.Find(record.KoxoUniqueIdentifier)!.ObjectGuid, StringComparison.OrdinalIgnoreCase),
            "Amorcage conclu sur l'objet cree par KoXo.");
        Assert(!record.SecretAvailable, "Le secret est acquitte une fois le lien prouve.");
        Assert(fixture.Directory.LifecycleWriteCount == 0, "Sous autorite KoXo, aucune ecriture de cycle de vie.");
        Assert(
            fixture.Koxo.ResolveCalls.All(call => call == record.KoxoUniqueIdentifier),
            "F : resolution par l'employeeNumber exact seulement.");

        var gate = await fixture.Repository.GetPortalUserEmailVerificationStateAsync(userId, CancellationToken.None);
        Assert(gate.EmailVerified && gate.VerificationRequired, "Le checkout continue d'exiger l'e-mail verifie.");
    }

    // ==================================================================
    // B. Signup standard : plus de boucle lien / export / secret
    // ==================================================================

    private static async Task VerifyStandardSignupBreaksTheDeadlockAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var email = fixture.NextEmail("standard");
        var submitted = await fixture.Service.SubmitAsync(
            fixture.Payload(email, cart: false, password: null),
            "corr-standard",
            CancellationToken.None);
        Assert(submitted.Succeeded, "Soumission standard acceptee.");
        await fixture.Service.VerifyEmailAsync(
            fixture.Email.LastToken(email, EmailKind.Verification),
            CancellationToken.None);

        var signupId = fixture.SignupIdFor(email);
        var approved = await fixture.Service.ApproveAsync(signupId, "corr-approve", CancellationToken.None);
        Assert(approved.Succeeded, "La validation humaine reste le passage oblige.");

        var userId = fixture.PortalUserIdFor(email);
        var record = await fixture.RecordAsync(userId);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.AwaitingPassword
            && record.Origin == PrimaryIdentityBootstrapOrigins.Signup
            && !record.EmailVerificationRequired,
            "Approbation : amorcage explicite en attente du mot de passe.");
        Assert(
            PrimaryIdentityBootstrapPolicy.GetExportBlocker(record) == PrimaryIdentityBootstrapCodes.AwaitingPassword,
            "Sans mot de passe : non exportable.");

        // Avant : AD_IDENTITY_NOT_READY pour toujours. Desormais le mot de
        // passe est pose, le secret depose sans lien, le compte exportable.
        var set = await fixture.Service.SetPasswordAsync(
            fixture.Email.LastToken(email, EmailKind.SetPassword),
            SignupPassword,
            CancellationToken.None);
        Assert(set.Succeeded && set.Code == "PASSWORD_SET", "Le set-password standard aboutit sans identite prealable.");
        record = await fixture.RecordAsync(userId);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.KoxoPending
            && record.SecretAvailable
            && !record.HasUserLink,
            "Secret depose sans lien AD prealable.");
        Assert(PrimaryIdentityBootstrapPolicy.GetExportBlocker(record) is null, "Compte standard exportable.");
        Assert(
            await fixture.Pending.PeekAsync(userId, CancellationToken.None) == SignupPassword,
            "KoXo recevra exactement le mot de passe choisi.");

        Assert(await fixture.SimulateKoxoSyncAsync(userId), "KoXo cree l'identite.");
        var completed = await fixture.Service.ConvergePendingPrimaryIdentitiesAsync(50, CancellationToken.None);
        Assert(completed >= 1, "Le worker conclut l'amorcage.");
        record = await fixture.RecordAsync(userId);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.Completed
            && await fixture.Links.FindUserLinkByPortalUserIdAsync(userId, CancellationToken.None) is not null,
            "Identite standard liee.");
    }

    // ==================================================================
    // C. VPS : meme amorcage (mode mock, l'application cree l'objet)
    // ==================================================================

    private static async Task VerifyVpsAccountGetsItsIdentityAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.Mock);
        var email = fixture.NextEmail("vps");
        var signup = await fixture.Service.CompleteSelfServiceVpsAsync(
            fixture.Payload(email, vps: true),
            "corr-vps",
            CancellationToken.None);
        Assert(signup.Succeeded, "Le signup VPS cree le compte.");
        var userId = fixture.PortalUserIdFor(email);
        var record = await fixture.RecordAsync(userId);
        Assert(
            record.Origin == PrimaryIdentityBootstrapOrigins.SelfServiceVps
            && record.Status == PrimaryIdentityBootstrapStatuses.KoxoPending,
            "Le compte VPS n'est plus exempte : il entre dans l'amorcage.");
        Assert(
            await fixture.Links.FindUserLinkByPortalUserIdAsync(userId, CancellationToken.None) is null,
            "Aucune identite avant la preuve de l'adresse.");

        await fixture.Service.VerifyEmailAsync(
            fixture.Email.LastToken(email, EmailKind.Verification),
            CancellationToken.None);
        record = await fixture.RecordAsync(userId);
        var link = await fixture.Links.FindUserLinkByPortalUserIdAsync(userId, CancellationToken.None);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.Completed && link is not null,
            "Le compte VPS possede son identite AD apres verification.");
        Assert(
            PrimaryIdentityBootstrapPolicy.DistinguishedNameContainsOu(link!.DistinguishedName, record.CustomerReference),
            "L'identite vit dans l'OU du client.");
    }

    private static async Task VerifyDisabledDirectoryKeepsExplicitPendingStateAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.Disabled);
        var email = fixture.NextEmail("disabled");
        await fixture.Service.CompleteSelfServiceCartAsync(
            fixture.Payload(email, cart: true),
            "corr-disabled",
            CancellationToken.None);
        var userId = fixture.PortalUserIdFor(email);
        var record = await fixture.RecordAsync(userId);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.AwaitingPassword && !record.SecretAvailable,
            "Annuaire desactive : l'amorcage existe, visible, sans secret inutile.");
        Assert(
            (await fixture.Service.ConvergePrimaryIdentityAsync(userId, CancellationToken.None)).Code
                == PrimaryIdentityBootstrapCodes.AwaitingPassword,
            "Aucune action annuaire quand l'annuaire est desactive.");
    }

    // ==================================================================
    // F. Adoption : fail-closed
    // ==================================================================

    private static async Task VerifyAdoptionFailsClosedAsync()
    {
        // Mauvais client : l'objet porte le bon employeeNumber mais vit dans
        // l'OU d'un autre client.
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var (userId, record) = await fixture.CreateVerifiedCartAccountAsync("wrong-customer");
        fixture.Koxo.CreateIdentity(record.KoxoUniqueIdentifier, "CLI20991231ZZZZ", "intrus.client");
        var result = await fixture.Service.ConvergePrimaryIdentityAsync(userId, CancellationToken.None);
        Assert(result.Code == PrimaryIdentityBootstrapCodes.CustomerMismatch, "Objet d'un autre client : refuse.");
        record = await fixture.RecordAsync(userId);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.Failed
            && await fixture.Links.FindUserLinkByPortalUserIdAsync(userId, CancellationToken.None) is null,
            "Echec explicite, aucun lien.");

        // Voisin : un employeeNumber proche n'est jamais adopte.
        var (neighbourId, neighbour) = await fixture.CreateVerifiedCartAccountAsync("neighbour");
        var digits = int.Parse(neighbour.KoxoUniqueIdentifier[4..], System.Globalization.CultureInfo.InvariantCulture);
        fixture.Koxo.CreateIdentity($"CLI-{digits + 500:D6}", neighbour.CustomerReference, "voisin.proche");
        var neighbourResult = await fixture.Service.ConvergePrimaryIdentityAsync(neighbourId, CancellationToken.None);
        Assert(neighbourResult.Code == PrimaryIdentityBootstrapCodes.DirectoryNotReady, "Aucun rapprochement approchant.");
        Assert(
            await fixture.Links.FindUserLinkByPortalUserIdAsync(neighbourId, CancellationToken.None) is null,
            "Aucun lien sur une identite voisine.");

        // Objet malforme (SID hors domaine) : refuse.
        var (malformedId, malformed) = await fixture.CreateVerifiedCartAccountAsync("malformed");
        fixture.Koxo.CreateIdentity(malformed.KoxoUniqueIdentifier, malformed.CustomerReference, "sid.invalide", sid: "S-1-5-32-544");
        var malformedResult = await fixture.Service.ConvergePrimaryIdentityAsync(malformedId, CancellationToken.None);
        Assert(malformedResult.Code == PrimaryIdentityBootstrapCodes.InvalidDirectoryObject, "Objet malforme : refuse.");

        // Objet desactive : attente, pas d'adoption.
        var (disabledId, disabledRecord) = await fixture.CreateVerifiedCartAccountAsync("disabled-object");
        fixture.Koxo.CreateIdentity(disabledRecord.KoxoUniqueIdentifier, disabledRecord.CustomerReference, "compte.desactive", disabled: true);
        var disabledResult = await fixture.Service.ConvergePrimaryIdentityAsync(disabledId, CancellationToken.None);
        Assert(
            disabledResult.Code == PrimaryIdentityBootstrapCodes.DirectoryNotReady
            && (await fixture.RecordAsync(disabledId)).Status == PrimaryIdentityBootstrapStatuses.KoxoPending,
            "Objet desactive : on attend que KoXo le reactive.");
    }

    // ==================================================================
    // G. Reprise, idempotence, concurrence
    // ==================================================================

    private static async Task VerifyRetryAfterKoxoCreationBeforeLinkAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite, flakyLinks: true);
        var (userId, record) = await fixture.CreateVerifiedCartAccountAsync("retry");
        await fixture.SimulateKoxoSyncAsync(userId);
        var created = fixture.Koxo.Find(record.KoxoUniqueIdentifier)!;

        fixture.FlakyLinks!.FailNextUpsert = true;
        try
        {
            await fixture.Service.ConvergePrimaryIdentityAsync(userId, CancellationToken.None);
            Assert(false, "L'ecriture du lien devait echouer une fois.");
        }
        catch (InvalidOperationException exception) when (exception.Message == FlakyLinkRepository.FailureMessage)
        {
        }

        record = await fixture.RecordAsync(userId);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.DirectoryReady
            && string.Equals(record.DirectoryObjectGuid, created.ObjectGuid, StringComparison.OrdinalIgnoreCase)
            && await fixture.Links.FindUserLinkByPortalUserIdAsync(userId, CancellationToken.None) is null,
            "Arret entre resolution et lien : objet fige, jamais conclu sans lien.");
        Assert(PrimaryIdentityBootstrapPolicy.GetExportBlocker(record) is null, "directory_ready reste dans l'export.");

        var retried = await fixture.Service.ConvergePendingPrimaryIdentitiesAsync(50, CancellationToken.None);
        Assert(retried >= 1, "Le rejeu conclut.");
        var link = await fixture.Links.FindUserLinkByPortalUserIdAsync(userId, CancellationToken.None);
        Assert(
            link is not null && string.Equals(link.ObjectGuid, created.ObjectGuid, StringComparison.OrdinalIgnoreCase),
            "Le rejeu retrouve le meme objet KoXo.");
    }

    private static async Task VerifyExistingLinkIsNoopAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var (userId, _) = await fixture.CreateVerifiedCartAccountAsync("noop");
        await fixture.SimulateKoxoSyncAsync(userId);
        await fixture.Service.ConvergePrimaryIdentityAsync(userId, CancellationToken.None);
        var resolutions = fixture.Koxo.ResolveCalls.Count;

        var again = await fixture.Service.ConvergePrimaryIdentityAsync(userId, CancellationToken.None);
        Assert(again.Succeeded && again.Code == PrimaryIdentityBootstrapCodes.Completed, "Lien existant : noop reussi.");
        Assert(fixture.Koxo.ResolveCalls.Count == resolutions, "Aucune nouvelle resolution annuaire.");
        Assert(
            (await fixture.Links.GetUserLinksByPortalUserIdAsync(userId, CancellationToken.None)).Count == 1,
            "Toujours un seul lien.");
    }

    private static async Task VerifyConcurrentConvergenceYieldsOneIdentityAsync()
    {
        var koxo = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var (koxoUserId, _) = await koxo.CreateVerifiedCartAccountAsync("concurrent-koxo");
        await koxo.SimulateKoxoSyncAsync(koxoUserId);
        var koxoResults = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(() => koxo.Service.ConvergePrimaryIdentityAsync(koxoUserId, CancellationToken.None))));
        Assert(koxoResults.Any(result => result.Succeeded), "Au moins un appel concurrent conclut.");
        Assert(
            (await koxo.Links.GetUserLinksByPortalUserIdAsync(koxoUserId, CancellationToken.None)).Count == 1
            && (await koxo.RecordAsync(koxoUserId)).Status == PrimaryIdentityBootstrapStatuses.Completed,
            "Appels concurrents sous KoXo : un lien, un amorcage conclu.");

        // Mode mock : l'application cree l'objet elle-meme. Le nom stable
        // interdit un second objet.
        var mock = Fixture.Create(AdIntegrationMode.Mock);
        var email = mock.NextEmail("concurrent-mock");
        await mock.Service.CompleteSelfServiceCartAsync(mock.Payload(email, cart: true), "corr", CancellationToken.None);
        var mockUserId = mock.PortalUserIdFor(email);
        await mock.Repository.MarkEmailVerifiedAsync(mock.SignupIdFor(email), CancellationToken.None);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(() => mock.Service.ConvergePrimaryIdentityAsync(mockUserId, CancellationToken.None))));
        var record = await mock.RecordAsync(mockUserId);
        var users = await mock.AdService.SearchUsersAsync(null, record.CustomerReference, CancellationToken.None);
        Assert(
            users.Value?.Count(user => user.ObjectType == "user") == 1,
            "Appels concurrents en mock : un seul objet annuaire.");
        Assert(
            (await mock.Links.GetUserLinksByPortalUserIdAsync(mockUserId, CancellationToken.None)).Count == 1
            && record.Status == PrimaryIdentityBootstrapStatuses.Completed,
            "Appels concurrents en mock : un lien, un amorcage conclu.");
    }

    private static async Task VerifyIdentityIsNeverTransferredAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var (userId, record) = await fixture.CreateVerifiedCartAccountAsync("transfer");
        await fixture.SimulateKoxoSyncAsync(userId);
        var created = fixture.Koxo.Find(record.KoxoUniqueIdentifier)!;

        // Le meme objet est deja lie a un autre utilisateur portail.
        var otherUserId = $"portal-user-owner-{Guid.NewGuid():N}";
        await fixture.Links.UpsertPortalUserLinkAsync(
            record.CustomerReference,
            otherUserId,
            null,
            created,
            "clients.home.bzh",
            "succeeded",
            DateTime.UtcNow,
            "succeeded",
            DateTime.UtcNow,
            "koxo_pending",
            CancellationToken.None);

        var result = await fixture.Service.ConvergePrimaryIdentityAsync(userId, CancellationToken.None);
        Assert(result.Code == PrimaryIdentityBootstrapCodes.Conflict, "Transfert d'identite refuse.");
        Assert(
            (await fixture.RecordAsync(userId)).Status == PrimaryIdentityBootstrapStatuses.Failed
            && await fixture.Links.FindUserLinkByPortalUserIdAsync(userId, CancellationToken.None) is null
            && (await fixture.Links.FindUserLinkByPortalUserIdAsync(otherUserId, CancellationToken.None))?.ObjectGuid
                == created.ObjectGuid,
            "Le lien existant reste a son titulaire, aucun lien pour le demandeur.");
    }

    private static async Task VerifyDoubleSignupCreatesOneBootstrapAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var email = fixture.NextEmail("double");
        await fixture.Service.CompleteSelfServiceCartAsync(fixture.Payload(email, cart: true), "corr-1", CancellationToken.None);
        var second = await fixture.Service.CompleteSelfServiceCartAsync(fixture.Payload(email, cart: true), "corr-2", CancellationToken.None);
        Assert(!second.Succeeded && second.Code == "ACCOUNT_ALREADY_EXISTS", "Le second signup est refuse.");
        Assert(
            fixture.Store.Rows.Values.Count(row => row.Email == email && row.ApprovedUserId is not null) == 1
            && fixture.Store.PrimaryIdentityBootstraps.Values.Count(row =>
                row.PortalUserId == fixture.PortalUserIdFor(email)) == 1,
            "Un seul compte, un seul amorcage.");
    }

    private static async Task VerifyKoxoTriggerIsRateLimitedAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var (userId, _) = await fixture.CreateVerifiedCartAccountAsync("trigger");
        var before = fixture.Trigger.Requests.Count(request => request.Trigger == "primary_identity_missing");
        await fixture.Service.ConvergePrimaryIdentityAsync(userId, CancellationToken.None);
        await fixture.Service.ConvergePrimaryIdentityAsync(userId, CancellationToken.None);
        await fixture.Service.ConvergePendingPrimaryIdentitiesAsync(50, CancellationToken.None);
        var after = fixture.Trigger.Requests.Count(request => request.Trigger == "primary_identity_missing");
        Assert(
            after - before <= 1,
            "Une synchronisation KoXo (globale) n'est pas redemandee a chaque passage.");
    }

    // ==================================================================
    // H. Reprise des comptes existants sans lien AD
    // ==================================================================

    private static async Task VerifyLegacyAccountRecoveryAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var (userId, signupId) = await fixture.CreateLegacyCartAccountAsync("legacy", verified: true);

        var first = await fixture.Service.RequestPrimaryIdentityRecoveryAsync(signupId, "corr-recovery-1", CancellationToken.None);
        Assert(first.Succeeded && first.Code == PrimaryIdentityRecoveryCodes.Issued, "La reprise emet un lien.");
        var firstToken = fixture.Email.LastToken(fixture.EmailFor(signupId), EmailKind.SetPassword);
        var record = await fixture.RecordAsync(userId);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.AwaitingPassword
            && record.Origin == PrimaryIdentityBootstrapOrigins.SelfServiceCart
            && !record.SecretAvailable,
            "Le compte ancien entre dans l'amorcage, sans secret invente.");

        var second = await fixture.Service.RequestPrimaryIdentityRecoveryAsync(signupId, "corr-recovery-2", CancellationToken.None);
        Assert(second.Succeeded, "La reprise est rejouable.");
        var secondToken = fixture.Email.LastToken(fixture.EmailFor(signupId), EmailKind.SetPassword);
        Assert(
            fixture.Store.PrimaryIdentityBootstraps.Values.Count(row => row.PortalUserId == userId) == 1,
            "Rejouer ne cree pas de second amorcage.");
        Assert(
            (await fixture.Service.SetPasswordAsync(firstToken, RecoveryPassword, CancellationToken.None)).Code == "TOKEN_INVALID",
            "Le lien precedent est invalide.");

        var set = await fixture.Service.SetPasswordAsync(secondToken, RecoveryPassword, CancellationToken.None);
        Assert(set.Succeeded, "Le titulaire choisit un nouveau mot de passe.");
        record = await fixture.RecordAsync(userId);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.KoxoPending
            && PrimaryIdentityBootstrapPolicy.GetExportBlocker(record) is null,
            "Le compte ancien devient exportable.");
        Assert(
            await fixture.Pending.PeekAsync(userId, CancellationToken.None) == RecoveryPassword,
            "Seul le nouveau mot de passe part vers KoXo, jamais une reconstitution de l'ancien.");
        Assert(
            (await fixture.Service.RequestPrimaryIdentityRecoveryAsync(signupId, "corr-recovery-3", CancellationToken.None)).Code
                == PrimaryIdentityRecoveryCodes.InProgress,
            "Pendant la creation KoXo, une nouvelle reprise est refusee.");

        await fixture.SimulateKoxoSyncAsync(userId);
        await fixture.Service.ConvergePendingPrimaryIdentitiesAsync(50, CancellationToken.None);
        Assert(
            (await fixture.RecordAsync(userId)).Status == PrimaryIdentityBootstrapStatuses.Completed
            && await fixture.Links.FindUserLinkByPortalUserIdAsync(userId, CancellationToken.None) is not null,
            "Le compte ancien possede son identite liee.");
        Assert(
            (await fixture.Service.RequestPrimaryIdentityRecoveryAsync(signupId, "corr-recovery-4", CancellationToken.None)).Code
                == PrimaryIdentityRecoveryCodes.AlreadyLinked,
            "Apres liaison, la reprise est sans objet.");
    }

    private static async Task VerifyUnverifiedLegacyAccountRecoversThroughVerificationAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var (userId, signupId) = await fixture.CreateLegacyCartAccountAsync("legacy-unverified", verified: false);
        Assert(
            (await fixture.Service.RequestPrimaryIdentityRecoveryAsync(signupId, "corr", CancellationToken.None)).Code
                == PrimaryIdentityRecoveryCodes.EmailVerificationRequired,
            "Adresse non prouvee : la reprise passe par la verification.");

        // Le titulaire suit le lien de verification recu a l'inscription.
        var email = fixture.EmailFor(signupId);
        await fixture.Service.VerifyEmailAsync(fixture.Email.LastToken(email, EmailKind.Verification), CancellationToken.None);

        var record = await fixture.RecordAsync(userId);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.AwaitingPassword,
            "La verification cree l'amorcage du compte ancien.");
        var token = fixture.Email.LastToken(email, EmailKind.SetPassword);
        Assert(
            (await fixture.Service.SetPasswordAsync(token, RecoveryPassword, CancellationToken.None)).Succeeded,
            "Le lien de reprise envoye a la verification fonctionne.");
        Assert(
            PrimaryIdentityBootstrapPolicy.GetExportBlocker(await fixture.RecordAsync(userId)) is null,
            "Le compte ancien verifie est exportable.");
    }

    private static async Task VerifyExpiredSecretFallsBackToRecoveryAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.ControlledWrite);
        var email = fixture.NextEmail("expired");
        await fixture.Service.CompleteSelfServiceCartAsync(fixture.Payload(email, cart: true), "corr", CancellationToken.None);
        var userId = fixture.PortalUserIdFor(email);
        // Le secret expire avant la verification (ici : retire).
        await fixture.Pending.AcknowledgeAsync(userId, CancellationToken.None);

        await fixture.Service.VerifyEmailAsync(fixture.Email.LastToken(email, EmailKind.Verification), CancellationToken.None);
        var record = await fixture.RecordAsync(userId);
        Assert(
            record.Status == PrimaryIdentityBootstrapStatuses.AwaitingPassword,
            "Secret perdu : l'amorcage attend un nouveau mot de passe.");
        Assert(
            fixture.Email.Sent.Any(sent => sent.Kind == EmailKind.SetPassword && sent.Email == email),
            "Un lien de definition est envoye automatiquement au titulaire.");
    }

    // ==================================================================
    // I. De bout en bout : la resolution d'identite du provisioning
    // ==================================================================

    private static async Task VerifyCartEndToEndResolvesProvisioningIdentityAsync()
    {
        var fixture = Fixture.Create(AdIntegrationMode.Mock);
        var email = fixture.NextEmail("e2e");
        await fixture.Service.CompleteSelfServiceCartAsync(fixture.Payload(email, cart: true), "corr", CancellationToken.None);
        var userId = fixture.PortalUserIdFor(email);
        var customerReference = (await fixture.RecordAsync(userId)).CustomerReference;

        var before = await ResolveProvisioningIdentityAsync(fixture, userId, customerReference);
        Assert(
            !before.Resolved && before.ReasonCode == "BILLING_V2_PROVISIONING_IDENTITY_NOT_LINKED",
            "Avant verification, le provisioning echoue comme observe.");

        await fixture.Service.VerifyEmailAsync(fixture.Email.LastToken(email, EmailKind.Verification), CancellationToken.None);
        var after = await ResolveProvisioningIdentityAsync(fixture, userId, customerReference);
        Assert(
            after.Resolved && after.Targets.Count == 1,
            "Apres verification et amorcage, l'identite du compte principal est resolue.");
    }

    private static async Task<BillingV2ProvisioningTargetResolution> ResolveProvisioningIdentityAsync(
        Fixture fixture,
        string portalUserId,
        string customerReference)
    {
        var portalLinks = await fixture.Links.GetUserLinksByPortalUserIdAsync(portalUserId, CancellationToken.None);
        var customerUserLinks = (await fixture.Links.GetCustomerLinksAsync(customerReference, CancellationToken.None))
            .Where(link => link.ObjectType == "user")
            .ToArray();
        // Le depot de liens mock derive l'identifiant client de la reference :
        // on reprend celui qu'il a ecrit, apres avoir verifie que le lien
        // designe bien la reference du client inscrit.
        var customerId = portalLinks.FirstOrDefault()?.CustomerId ?? "customer-without-link";
        Assert(
            portalLinks.All(link => link.CustomerReference == customerReference),
            "Le lien designe le client inscrit.");
        return BillingV2ProvisioningIdentityResolver.Resolve(
            customerId,
            [new BillingV2UserDesiredState("slot-primary", portalUserId, [], null, [], [])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(StringComparer.Ordinal)
            {
                [portalUserId] = portalLinks
            },
            customerUserLinks);
    }

    // ==================================================================
    // Outillage
    // ==================================================================

    private static string Collapse(string sql)
        => string.Join(
            ' ',
            sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private enum EmailKind
    {
        Verification,
        SetPassword
    }

    private sealed record SentEmail(EmailKind Kind, string Email, string Url);

    private sealed class RecordingEmailDispatch : IEmailDispatchService
    {
        private readonly object _sync = new();
        public List<SentEmail> Sent { get; } = [];

        public string LastToken(string email, EmailKind kind)
        {
            SentEmail? sent;
            lock (_sync)
            {
                sent = Sent.LastOrDefault(candidate => candidate.Email == email && candidate.Kind == kind);
            }

            if (sent is null)
            {
                throw new InvalidOperationException($"Aucun e-mail {kind} pour {email}.");
            }

            var start = sent.Url.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
            var end = sent.Url.IndexOf('&', start);
            return Uri.UnescapeDataString(end < 0 ? sent.Url[start..] : sent.Url[start..end]);
        }

        private Task<EmailDispatchResult> Record(EmailKind kind, string email, string url)
        {
            lock (_sync)
            {
                Sent.Add(new SentEmail(kind, email, url));
            }

            return Task.FromResult(new EmailDispatchResult(true, "recorded", string.Empty));
        }

        public Task<EmailDispatchResult> SendInvoiceIssuedAsync(string documentId, string correlationId, CancellationToken cancellationToken)
            => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));

        public Task<EmailDispatchResult> SendPaymentReminderAsync(string documentId, string correlationId, CancellationToken cancellationToken)
            => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));

        public Task<EmailDispatchResult> SendPaymentConfirmedAsync(string documentId, string correlationId, CancellationToken cancellationToken)
            => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));

        public Task<EmailDispatchResult> SendContactFormAsync(ContactFormSubmission submission, string correlationId, CancellationToken cancellationToken)
            => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));

        public Task<EmailDispatchResult> SendSignupVerificationAsync(string email, string contactName, string verificationUrl, string correlationId, CancellationToken cancellationToken)
            => Record(EmailKind.Verification, email, verificationUrl);

        public Task<EmailDispatchResult> SendAccountApprovedAsync(string email, string contactName, string setPasswordUrl, string correlationId, CancellationToken cancellationToken)
            => Record(EmailKind.SetPassword, email, setPasswordUrl);

        public Task<EmailDispatchResult> SendAccountRejectedAsync(string email, string contactName, string? reason, string correlationId, CancellationToken cancellationToken)
            => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));
    }

    /// <summary>
    /// Annuaire tel que KoXo le laisse : des identites creees a partir du CSV,
    /// retrouvables uniquement par leur employeeNumber exact.
    /// </summary>
    private sealed class SimulatedKoxoDirectory : IAdGroupProvisioner
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, AdDirectoryObjectSummary> _byEmployeeNumber =
            new(StringComparer.Ordinal);
        private readonly List<string> _resolveCalls = [];

        public string ModeName => "controlled_write";
        public bool RequiresConfiguredGroupDistinguishedNames => false;

        public IReadOnlyList<string> ResolveCalls
        {
            get
            {
                lock (_sync)
                {
                    return _resolveCalls.ToArray();
                }
            }
        }

        public AdDirectoryObjectSummary? Find(string employeeNumber)
        {
            lock (_sync)
            {
                return _byEmployeeNumber.TryGetValue(employeeNumber, out var found) ? found : null;
            }
        }

        public AdDirectoryObjectSummary CreateIdentity(
            string employeeNumber,
            string secondaryGroup,
            string samAccountName,
            string? sid = null,
            bool disabled = false)
        {
            var created = new AdDirectoryObjectSummary(
                Guid.NewGuid().ToString("D"),
                sid ?? $"S-1-5-21-1000000001-1000000002-1000000003-{Interlocked.Increment(ref _sequence) + 5000}",
                "user",
                samAccountName,
                $"{samAccountName}@clients.home.bzh",
                samAccountName.ToUpperInvariant(),
                $"CN={samAccountName},OU={secondaryGroup},{KoxoRoot}",
                // Le resolveur LDAP reel ne renseigne pas la reference client.
                string.Empty,
                disabled);
            lock (_sync)
            {
                _byEmployeeNumber[employeeNumber] = created;
            }

            return created;
        }

        public Task<AdDirectoryObjectSummary?> ResolveUserByEmployeeNumberAsync(
            string employeeNumber,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                _resolveCalls.Add(employeeNumber);
                return Task.FromResult(
                    _byEmployeeNumber.TryGetValue(employeeNumber, out var found) ? found : null);
            }
        }

        public Task<AdGroupProvisionerResult> AddUserToGroupAsync(
            CustomerAdLinkSummary user, string groupSamAccountName, string? groupDistinguishedName, CancellationToken cancellationToken)
            => Task.FromResult(new AdGroupProvisionerResult(200, "NOOP", "noop", false));

        public Task<AdGroupProvisionerResult> RemoveUserFromGroupAsync(
            CustomerAdLinkSummary user, string groupSamAccountName, string? groupDistinguishedName, CancellationToken cancellationToken)
            => Task.FromResult(new AdGroupProvisionerResult(200, "NOOP", "noop", false));
    }

    /// <summary>Depot de liens dont la prochaine ecriture echoue, une fois.</summary>
    private sealed class FlakyLinkRepository : IActiveDirectoryLinkRepository
    {
        public const string FailureMessage = "Ecriture du lien interrompue (test).";
        private readonly IActiveDirectoryLinkRepository _inner;

        public FlakyLinkRepository(IActiveDirectoryLinkRepository inner) => _inner = inner;

        public bool FailNextUpsert { get; set; }
        public bool IsPersistent => _inner.IsPersistent;

        public Task<AdCustomerContext?> GetCustomerContextAsync(string customerReference, CancellationToken cancellationToken)
            => _inner.GetCustomerContextAsync(customerReference, cancellationToken);

        public Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerLinksAsync(string customerReference, CancellationToken cancellationToken)
            => _inner.GetCustomerLinksAsync(customerReference, cancellationToken);

        public Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerUserLinksAsync(string customerId, CancellationToken cancellationToken)
            => _inner.GetCustomerUserLinksAsync(customerId, cancellationToken);

        public Task<CustomerAdLinkUpsertResult> UpsertCustomerLinkAsync(string customerReference, string? actorUserId, AdDirectoryObjectSummary directoryObject, CancellationToken cancellationToken)
            => _inner.UpsertCustomerLinkAsync(customerReference, actorUserId, directoryObject, cancellationToken);

        public Task<CustomerAdLinkUpsertResult> UpsertPortalUserLinkAsync(
            string customerReference, string portalUserId, string? actorUserId, AdDirectoryObjectSummary directoryObject,
            string? adDomain, string? adProvisioningStatus, DateTime? adProvisionedAtUtc, string? lastPasswordSyncStatus,
            DateTime? lastPasswordSyncAtUtc, string? koxoExportStatus, CancellationToken cancellationToken)
        {
            if (FailNextUpsert)
            {
                FailNextUpsert = false;
                throw new InvalidOperationException(FailureMessage);
            }

            return _inner.UpsertPortalUserLinkAsync(
                customerReference, portalUserId, actorUserId, directoryObject, adDomain, adProvisioningStatus,
                adProvisionedAtUtc, lastPasswordSyncStatus, lastPasswordSyncAtUtc, koxoExportStatus, cancellationToken);
        }

        public Task<bool> UpdateUserPasswordSyncStatusAsync(string portalUserId, string status, DateTime changedAtUtc, CancellationToken cancellationToken)
            => _inner.UpdateUserPasswordSyncStatusAsync(portalUserId, status, changedAtUtc, cancellationToken);

        public Task<bool> DeleteCustomerLinkAsync(string customerReference, string linkId, CancellationToken cancellationToken)
            => _inner.DeleteCustomerLinkAsync(customerReference, linkId, cancellationToken);

        public Task<bool> RefreshCustomerLinkAsync(string targetCustomerReference, AdDirectoryObjectSummary directoryObject, CancellationToken cancellationToken)
            => _inner.RefreshCustomerLinkAsync(targetCustomerReference, directoryObject, cancellationToken);

        public Task<CustomerAdLinkSummary?> FindUserLinkByEmailAsync(string customerReference, string email, CancellationToken cancellationToken)
            => _inner.FindUserLinkByEmailAsync(customerReference, email, cancellationToken);

        public Task<PortalUserAdLinkRecord?> FindUserLinkByPortalUserIdAsync(string portalUserId, CancellationToken cancellationToken)
            => _inner.FindUserLinkByPortalUserIdAsync(portalUserId, cancellationToken);

        public Task<IReadOnlyList<PortalUserAdLinkRecord>> GetUserLinksByPortalUserIdAsync(string portalUserId, CancellationToken cancellationToken)
            => _inner.GetUserLinksByPortalUserIdAsync(portalUserId, cancellationToken);
    }

    private sealed class Fixture
    {
        public required MockSignupStore Store { get; init; }
        public required MockSignupRepository Repository { get; init; }
        public required KoxoPendingPasswordStore Pending { get; init; }
        public required IActiveDirectoryLinkRepository Links { get; init; }
        public FlakyLinkRepository? FlakyLinks { get; init; }
        public required SimulatedKoxoDirectory Koxo { get; init; }
        public required IActiveDirectoryService AdService { get; init; }
        public required RecordingActiveDirectoryService Directory { get; init; }
        public required RecordingKoxoSyncWebhookTriggerService Trigger { get; init; }
        public required RecordingEmailDispatch Email { get; init; }
        public required SignupService Service { get; init; }

        public static Fixture Create(AdIntegrationMode mode, bool flakyLinks = false)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DEMO_PORTAL_EMAIL"] = "portal.bootstrap@example.invalid",
                    ["DEMO_PORTAL_PASSWORD"] = "NOT_A_REAL_PASSWORD_BOOTSTRAP_DEMO",
                    ["DEMO_PORTAL_STATUS"] = "active",
                    ["DEMO_INTERNAL_ADMIN_EMAIL"] = "admin.bootstrap@example.invalid",
                    ["DEMO_INTERNAL_ADMIN_PASSWORD"] = "NOT_A_REAL_PASSWORD_BOOTSTRAP_ADMIN"
                })
                .Build();
            var passwordService = new PortalPasswordService();
            var authStore = new MockAuthenticationStore(configuration, passwordService);
            var settings = new ApplicationSettingsService(
                new MockApplicationSettingsRepository(),
                NullLogger<ApplicationSettingsService>.Instance);
            var ad = new AdRuntimeConfiguration(
                mode,
                mode == AdIntegrationMode.Disabled ? null : "clients.home.bzh",
                "OU=Clients,DC=clients,DC=home,DC=bzh",
                "OU=Clients,DC=clients,DC=home,DC=bzh",
                ["OU=Clients,DC=clients,DC=home,DC=bzh", "OU=KoXoAdm,DC=clients,DC=home,DC=bzh"],
                false,
                null,
                null,
                3000,
                5000,
                25,
                true);
            var membership = new MockAdGroupMembershipStore();
            var adService = new MockActiveDirectoryService(ad, membership);
            var directory = new RecordingActiveDirectoryService(adService);
            var pending = new KoxoPendingPasswordStore(
                NullLogger<KoxoPendingPasswordStore>.Instance,
                TimeSpan.FromHours(24));
            IActiveDirectoryLinkRepository links = new MockActiveDirectoryLinkRepository();
            FlakyLinkRepository? flaky = null;
            if (flakyLinks)
            {
                flaky = new FlakyLinkRepository(links);
                links = flaky;
            }

            var store = new MockSignupStore();
            var repository = new MockSignupRepository(store, authStore)
            {
                SealSink = pending,
                LinkRepository = links
            };
            var koxo = new SimulatedKoxoDirectory();
            var trigger = new RecordingKoxoSyncWebhookTriggerService();
            var email = new RecordingEmailDispatch();
            IAdGroupProvisioner provisioner = mode == AdIntegrationMode.ControlledWrite
                ? koxo
                : new MockAdGroupProvisioner(membership);
            var service = new SignupService(
                repository,
                email,
                passwordService,
                new AuthenticationService(
                    new MockAuthenticationRepository(authStore),
                    passwordService,
                    new SessionTokenService(),
                    new NoopAuditService(),
                    new AuthRuntimeConfiguration(TimeSpan.FromMinutes(60), 5, TimeSpan.FromMinutes(10)),
                    settings),
                directory,
                links,
                provisioner,
                pending,
                trigger,
                new SignupRuntimeConfiguration(true, 1000, 1000, 24, 24, false),
                settings,
                new EmailRuntimeConfiguration(
                    EmailIntegrationMode.Mock,
                    "smtp.example.invalid",
                    25,
                    false,
                    null,
                    null,
                    "noreply@example.invalid",
                    "Kermaria",
                    "https://portal.example.invalid",
                    "contact@example.invalid",
                    10000,
                    false,
                    [],
                    true),
                ad,
                NullLogger<SignupService>.Instance);

            return new Fixture
            {
                Store = store,
                Repository = repository,
                Pending = pending,
                Links = links,
                FlakyLinks = flaky,
                Koxo = koxo,
                AdService = adService,
                Directory = directory,
                Trigger = trigger,
                Email = email,
                Service = service
            };
        }

        public string NextEmail(string label)
            => $"bootstrap.{label}.{Interlocked.Increment(ref _sequence)}@example.invalid";

        public SignupSubmitPayload Payload(
            string email,
            bool cart = false,
            bool vps = false,
            string? password = SignupPassword)
            => new(
                "Societe Amorcage",
                "Alice Martin",
                email,
                "0102030405",
                "Amorcage de l'identite.",
                new SignupCustomerData(
                    "professional",
                    "Societe Amorcage",
                    email,
                    "0102030405",
                    "1 rue du Test",
                    null,
                    "29000",
                    "Quimper",
                    "FR"),
                new SignupUserData(
                    "madame",
                    "Alice",
                    "Martin",
                    "1990-01-02",
                    null,
                    "Alice Martin",
                    email,
                    "0102030405",
                    true),
                $"10.96.{Interlocked.Increment(ref _sequence) % 250}.1",
                "bootstrap-tests",
                SelfServiceVpsIntent: vps ? new SignupSelfServiceVpsIntent("VPS", "VPS-S") : null,
                SelfServiceCartIntent: cart ? new SignupSelfServiceCartIntent(Guid.NewGuid().ToString("D")) : null,
                Password: cart || vps ? password : null);

        public string SignupIdFor(string email)
            => Store.Rows.Values.Single(row => row.Email == email).Id;

        public string EmailFor(string signupId)
            => Store.Rows[signupId].Email;

        public string PortalUserIdFor(string email)
            => Store.Rows.Values.Single(row => row.Email == email && row.ApprovedUserId is not null).ApprovedUserId!;

        public async Task<PrimaryIdentityBootstrapRecord> RecordAsync(string portalUserId)
            => await Repository.GetPrimaryIdentityBootstrapAsync(portalUserId, CancellationToken.None)
                ?? throw new InvalidOperationException($"Aucun amorcage pour {portalUserId}.");

        /// <summary>
        /// Passage KoXo simule : il ne cree que ce que la regle d'export
        /// retient, et lit le mot de passe comme la colonne 14.
        /// </summary>
        public async Task<bool> SimulateKoxoSyncAsync(string portalUserId)
        {
            var record = await RecordAsync(portalUserId);
            if (PrimaryIdentityBootstrapPolicy.GetExportBlocker(record) is not null)
            {
                return false;
            }

            var columnFourteen = await Pending.PeekAsync(portalUserId, CancellationToken.None);
            Assert(!string.IsNullOrEmpty(columnFourteen), "Un compte exporte porte son mot de passe (colonne 14).");
            Koxo.CreateIdentity(
                record.KoxoUniqueIdentifier,
                PrimaryIdentityBootstrapPolicy.ResolveExpectedSecondaryGroup(record),
                $"alice.martin{record.KoxoUniqueIdentifier[4..]}");
            return true;
        }

        public async Task<(string UserId, PrimaryIdentityBootstrapRecord Record)> CreateVerifiedCartAccountAsync(string label)
        {
            var email = NextEmail(label);
            var signup = await Service.CompleteSelfServiceCartAsync(Payload(email, cart: true), "corr", CancellationToken.None);
            Assert(signup.Succeeded, "Compte Cart cree.");
            await Service.VerifyEmailAsync(Email.LastToken(email, EmailKind.Verification), CancellationToken.None);
            var userId = PortalUserIdFor(email);
            return (userId, await RecordAsync(userId));
        }

        /// <summary>
        /// Compte Cart anterieur a la migration 096 : aucun amorcage, aucun
        /// secret, mot de passe clair perdu.
        /// </summary>
        public async Task<(string UserId, string SignupId)> CreateLegacyCartAccountAsync(string label, bool verified)
        {
            var email = NextEmail(label);
            await Service.CompleteSelfServiceCartAsync(Payload(email, cart: true), "corr", CancellationToken.None);
            var userId = PortalUserIdFor(email);
            var signupId = SignupIdFor(email);
            Store.PrimaryIdentityBootstraps.TryRemove(userId, out _);
            await Pending.AcknowledgeAsync(userId, CancellationToken.None);
            if (verified)
            {
                // Verification anterieure au deploiement : directement en base.
                await Repository.MarkEmailVerifiedAsync(signupId, CancellationToken.None);
            }

            Assert(
                await Links.FindUserLinkByPortalUserIdAsync(userId, CancellationToken.None) is null,
                "Le compte ancien n'a pas de lien AD.");
            return (userId, signupId);
        }
    }
}
