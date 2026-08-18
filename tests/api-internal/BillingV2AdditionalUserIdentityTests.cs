using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Email;
using Kermaria.ApiInternal.Services.Provisioning;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Cycle de vie d'identite des utilisateurs additionnels Billing V2 (Phase 4).
/// </summary>
/// <remarks>
/// <para>
/// La suite exerce le <b>vrai</b> service contre des depots mock qui
/// reproduisent les gardes structurelles de MariaDB (exclusion mutuelle,
/// unicite de la place, unicite de l'adresse). Un mock permissif rendrait les
/// scenarios de concurrence purement decoratifs.
/// </para>
/// <para>
/// Ce que cette suite ne peut <b>pas</b> prouver, et qui reste du ressort de
/// <see cref="BillingV2AdditionalUserIdentitySchemaTests"/> sur une vraie
/// MariaDB : le comportement de la clause d'export KoXo, la serialisation par
/// <c>FOR UPDATE</c>, l'atomicite du ROLLBACK et les index uniques. Les
/// verifications de forme sur le SQL faites ici sont un garde-fou contre une
/// relaxation accidentelle, pas une preuve d'execution.
/// </para>
/// </remarks>
public static class BillingV2AdditionalUserIdentityTests
{
    private const string CustomerId = "customer-kermaria";
    private const string CustomerReference = "CLI-KERMARIA";
    private const string OtherCustomerId = "customer-autre";
    private const string OtherCustomerReference = "CLI-AUTRE";
    private const string SubscriptionId = "subscription-v2";
    private const string OtherSubscriptionId = "subscription-autre";
    private const string Password = "MotDePasseAssezLong!";

    public static async Task RunAsync()
    {
        await VerifyUnassignedSlotStaysInert();
        await VerifyAssignmentCreatesExactlyOnePortalUser();
        await VerifyIdentityReferencePointsAtTheCreatedUser();
        await VerifyKoxoIdentifierIsAllocated();
        await VerifyCrossCustomerAssignmentIsRefused();
        await VerifyPrimarySlotIsRefused();
        await VerifyForeignSubscriptionIsRefused();
        await VerifyAlreadyAssignedSlotIsRefused();
        await VerifyConcurrentAssignmentFailsClosed();
        await VerifyDuplicateEmailIsRefused();
        await VerifySlotWithoutEntitlementIsRefused();
        await VerifyInactiveSubscriptionIsRefused();
        await VerifyRawTokenIsNeverPersisted();
        await VerifyExpiredTokenIsRefused();
        await VerifyConsumedTokenCannotBeReused();
        await VerifyRenewedInvitationInvalidatesThePreviousLink();
        await VerifyPortalPasswordHashIsUsable();
        await VerifyKoxoOwnedDirectoryNeverCreatesNorSetsPassword();
        await VerifyPasswordIsPublishedForTheKoxoExport();
        await VerifyIdentityIsAdoptedByExactEmployeeNumber();
        await VerifyForeignEmployeeNumberIsNeverAdopted();
        await VerifyRetryAfterMaterializationIsIdempotent();
        await VerifyMaterializationBeforePasswordIsRefused();
        await VerifyDisabledLifecycleStopsMaterializing();
        await VerifyDirectoryObjectConflictFailsClosed();
        VerifyExportQueryKeepsEveryMandatoryCondition();
        VerifyAssignmentPolicyRefusesEveryIncoherentSnapshot();
    }

    // ==================================================================
    // 1. La place non attribuee est inerte
    // ==================================================================

    private static async Task VerifyUnassignedSlotStaysInert()
    {
        var harness = Harness.Create();
        var slot = harness.RegisterSlot("slot-1");

        Assert(
            slot.IdentityReference is null,
            "Une place fraichement planifiee ne porte aucune identite.");
        Assert(
            await harness.Repository.FindBySubscriptionUserIdAsync(
                slot.Id,
                CancellationToken.None) is null,
            "Une place non attribuee n'a aucun cycle de vie : elle ne demande "
            + "aucune ecriture annuaire et ne doit rien declencher.");
        Assert(
            harness.PortalUsers.Entries.Count == 0,
            "Aucun utilisateur portail n'existe tant que rien n'est attribue.");
    }

    // ==================================================================
    // 2 a 4. Attribution nominale
    // ==================================================================

    private static async Task VerifyAssignmentCreatesExactlyOnePortalUser()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");

        var result = await harness.AssignAsync("slot-1", "alice@example.invalid");

        Assert(result.Succeeded, $"L'attribution doit reussir ({result.Code}).");
        Assert(
            harness.PortalUsers.Entries.Count == 1,
            "L'attribution cree exactement un utilisateur portail.");
        Assert(
            harness.PortalUsers.Find(result.PortalUserId!)?.PasswordHash is null,
            "L'utilisateur est cree sans mot de passe : c'est lui qui le "
            + "choisira, et un compte actif sans condensat n'est pas "
            + "connectable.");
        Assert(
            result.LifecycleStatus
                == BillingV2UserIdentityStatuses.AwaitingPassword,
            "Le cycle de vie demarre en awaiting_password, jamais en "
            + "koxo_pending : rien ne doit partir vers KoXo avant le mot de "
            + "passe.");
    }

    private static async Task VerifyIdentityReferencePointsAtTheCreatedUser()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");

        var result = await harness.AssignAsync("slot-1", "alice@example.invalid");
        var slot = harness.Repository.FindSlot("slot-1")!;
        var lifecycle = await harness.Repository.FindBySubscriptionUserIdAsync(
            "slot-1",
            CancellationToken.None);

        Assert(
            slot.IdentityReference == result.PortalUserId,
            "identity_reference designe exactement l'utilisateur cree.");
        Assert(
            lifecycle?.PortalUserId == result.PortalUserId,
            "Le cycle de vie designe le meme utilisateur que la place.");
        Assert(
            lifecycle?.SubscriptionUserId == "slot-1",
            "Le cycle de vie est bien rattache a la place attribuee.");
    }

    private static async Task VerifyKoxoIdentifierIsAllocated()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        harness.RegisterSlot("slot-2");

        await harness.AssignAsync("slot-1", "alice@example.invalid");
        await harness.AssignAsync("slot-2", "bob@example.invalid");

        var identifiers = harness.Repository.AllocatedKoxoIdentifiers;
        Assert(
            identifiers.Count == 2,
            "Chaque attribution alloue son propre identifiant KoXo.");
        Assert(
            identifiers.All(value =>
                KoxoDirectoryTopology.IsValidUniqueIdentifier(value)),
            "Les identifiants alloues respectent la forme CLI-NNNNNN attendue "
            + "dans employeeNumber.");
        Assert(
            identifiers.Distinct(StringComparer.Ordinal).Count() == 2,
            "Deux places ne partagent jamais un identifiant : la colonne est "
            + "sous index unique et c'est la seule cle de rattachement AD.");
    }

    // ==================================================================
    // 5 a 12. Refus
    // ==================================================================

    private static async Task VerifyCrossCustomerAssignmentIsRefused()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-etranger", customerId: OtherCustomerId);

        var result = await harness.AssignAsync(
            "slot-etranger",
            "intrus@example.invalid");

        Assert(
            !result.Succeeded
            && result.Code
                == BillingV2AdditionalUserRejectionCodes.SlotCustomerMismatch,
            "Une place d'un autre client est refusee : l'attribuer creerait "
            + $"une identite dans le mauvais perimetre annuaire ({result.Code}).");
        Assert(
            harness.PortalUsers.Entries.Count == 0,
            "Un refus ne laisse aucun utilisateur derriere lui.");
    }

    private static async Task VerifyPrimarySlotIsRefused()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-principal", isPrimary: true);

        var result = await harness.AssignAsync(
            "slot-principal",
            "alice@example.invalid");

        Assert(
            !result.Succeeded
            && result.Code == BillingV2AdditionalUserRejectionCodes.SlotIsPrimary,
            $"La place du contact principal n'est jamais reattribuee ({result.Code}).");
    }

    private static async Task VerifyForeignSubscriptionIsRefused()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1", subscriptionId: OtherSubscriptionId);

        var result = await harness.AssignAsync("slot-1", "alice@example.invalid");

        Assert(
            !result.Succeeded
            && result.Code == BillingV2AdditionalUserRejectionCodes
                .SlotSubscriptionMismatch,
            $"La place doit appartenir a l'abonnement vise ({result.Code}).");
    }

    private static async Task VerifyAlreadyAssignedSlotIsRefused()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");

        var first = await harness.AssignAsync("slot-1", "alice@example.invalid");
        var second = await harness.AssignAsync("slot-1", "bob@example.invalid");

        Assert(first.Succeeded, "La premiere attribution reussit.");
        Assert(
            !second.Succeeded
            && second.Code
                == BillingV2AdditionalUserRejectionCodes.SlotAlreadyAssigned,
            "Une seconde attribution est un conflit explicite, pas un succes "
            + $"silencieux ({second.Code}).");
        Assert(
            harness.PortalUsers.Entries.Count == 1,
            "Le refus n'a cree aucun second utilisateur.");
    }

    private static async Task VerifyConcurrentAssignmentFailsClosed()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(index => Task.Run(() =>
                harness.AssignAsync(
                    "slot-1",
                    $"concurrent{index}@example.invalid"))));

        Assert(
            results.Count(result => result.Succeeded) == 1,
            "Une seule attribution concurrente aboutit : la place est vendue "
            + "une fois, elle ne peut pas produire deux identites.");
        Assert(
            harness.PortalUsers.Entries.Count == 1,
            "Aucun utilisateur orphelin n'est cree par les tentatives "
            + "perdantes.");
        Assert(
            harness.Repository.AllocatedKoxoIdentifiers.Count == 1,
            "Aucun CLI-NNNNNN n'est consomme par une tentative refusee.");
    }

    private static async Task VerifyDuplicateEmailIsRefused()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        harness.RegisterSlot("slot-2");

        await harness.AssignAsync("slot-1", "alice@example.invalid");
        // Casse differente : l'adresse est normalisee avant comparaison, sinon
        // deux comptes distincts porteraient la meme identite de connexion.
        var duplicate = await harness.AssignAsync(
            "slot-2",
            "ALICE@Example.Invalid");

        Assert(
            !duplicate.Succeeded
            && duplicate.Code
                == BillingV2AdditionalUserRejectionCodes.EmailAlreadyUsed,
            $"Une adresse deja prise est refusee ({duplicate.Code}).");
        Assert(
            harness.PortalUsers.Entries.Count == 1,
            "Le refus n'a cree aucun second utilisateur.");
    }

    private static async Task VerifySlotWithoutEntitlementIsRefused()
    {
        var harness = Harness.Create();
        var slot = harness.RegisterSlot("slot-1");
        slot.HasActiveUserSlotEntitlement = false;

        var result = await harness.AssignAsync("slot-1", "alice@example.invalid");

        Assert(
            !result.Succeeded
            && result.Code == BillingV2AdditionalUserRejectionCodes
                .SlotEntitlementMissing,
            "Sans droit USER-ADDITIONAL actif, la place n'est pas un point "
            + $"d'entree pour creer une identite reelle ({result.Code}).");
    }

    private static async Task VerifyInactiveSubscriptionIsRefused()
    {
        var harness = Harness.Create();
        var slot = harness.RegisterSlot("slot-1");
        slot.SubscriptionStatus = "past_due";

        var result = await harness.AssignAsync("slot-1", "alice@example.invalid");

        Assert(
            !result.Succeeded
            && result.Code == BillingV2AdditionalUserRejectionCodes
                .SubscriptionNotProvisionable,
            "Un abonnement hors etat provisionnable ne permet pas d'equiper un "
            + $"utilisateur ({result.Code}).");
    }

    // ==================================================================
    // 11 a 14. Jeton et mot de passe
    // ==================================================================

    private static async Task VerifyRawTokenIsNeverPersisted()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");

        await harness.AssignAsync("slot-1", "alice@example.invalid");
        var token = harness.Emails.LastToken!;

        Assert(
            !string.IsNullOrWhiteSpace(token),
            "L'invitation transporte bien un jeton.");
        Assert(
            !harness.PasswordSetups.ContainsRawToken(token),
            "Le jeton en clair n'est jamais persiste : une base compromise ne "
            + "doit pas rendre les liens rejouables.");
        Assert(
            await harness.PasswordSetups.FindByTokenHashAsync(
                PortalSetupToken.Hash(token),
                CancellationToken.None) is not null,
            "Seul le condensat SHA-256 permet de retrouver le jeton.");
        Assert(
            PortalSetupToken.Hash(token).Length == 64,
            "Le condensat stocke est bien un SHA-256 hexadecimal.");
    }

    private static async Task VerifyExpiredTokenIsRefused()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        await harness.AssignAsync("slot-1", "alice@example.invalid");

        var token = harness.Emails.LastToken!;
        harness.PasswordSetups.ExpireForTests(PortalSetupToken.Hash(token));

        var validation = await harness.Service.ValidateInvitationTokenAsync(
            token,
            CancellationToken.None);
        var applied = await harness.Service.SetPasswordAsync(
            token,
            Password,
            CancellationToken.None);

        Assert(
            !validation.Succeeded
            && validation.Code == PortalPasswordSetupCodes.TokenExpired,
            $"Un lien expire est refuse a la validation ({validation.Code}).");
        Assert(
            !applied.Succeeded
            && applied.Code == PortalPasswordSetupCodes.TokenExpired,
            $"Un lien expire ne pose aucun mot de passe ({applied.Code}).");
        Assert(
            harness.PortalUsers.Entries.Single().PasswordHash is null,
            "Aucun condensat n'a ete ecrit.");
    }

    private static async Task VerifyConsumedTokenCannotBeReused()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        await harness.AssignAsync("slot-1", "alice@example.invalid");
        var token = harness.Emails.LastToken!;

        var first = await harness.Service.SetPasswordAsync(
            token,
            Password,
            CancellationToken.None);
        var second = await harness.Service.SetPasswordAsync(
            token,
            "UnAutreMotDePasse!",
            CancellationToken.None);

        Assert(first.Succeeded, $"Le premier usage reussit ({first.Code}).");
        Assert(
            !second.Succeeded
            && second.Code == PortalPasswordSetupCodes.TokenAlreadyUsed,
            $"Le jeton est a usage unique ({second.Code}).");

        var hash = harness.PortalUsers.Entries.Single().PasswordHash!;
        Assert(
            harness.PasswordService.Verify(
                    first.PortalUserId!,
                    hash,
                    Password)
                == PasswordVerificationResult.Success,
            "Le second appel n'a pas ecrase le mot de passe choisi.");
    }

    private static async Task VerifyRenewedInvitationInvalidatesThePreviousLink()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        await harness.AssignAsync("slot-1", "alice@example.invalid");
        var firstToken = harness.Emails.LastToken!;

        await harness.Service.ResendInvitationAsync(
            "slot-1",
            CustomerId,
            "correlation",
            CancellationToken.None);
        var secondToken = harness.Emails.LastToken!;

        Assert(
            !string.Equals(firstToken, secondToken, StringComparison.Ordinal),
            "Le renouvellement emet un nouveau jeton.");

        var stale = await harness.Service.SetPasswordAsync(
            firstToken,
            Password,
            CancellationToken.None);
        Assert(
            !stale.Succeeded,
            "Le lien precedent cesse immediatement d'etre valable : laisser "
            + $"deux liens ouverts multiplierait les fenetres d'usage ({stale.Code}).");

        var fresh = await harness.Service.SetPasswordAsync(
            secondToken,
            Password,
            CancellationToken.None);
        Assert(fresh.Succeeded, $"Le nouveau lien fonctionne ({fresh.Code}).");
    }

    private static async Task VerifyPortalPasswordHashIsUsable()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "alice@example.invalid");

        await harness.Service.SetPasswordAsync(
            harness.Emails.LastToken,
            Password,
            CancellationToken.None);

        var hash = harness.PortalUsers.Find(assignment.PortalUserId!)!
            .PasswordHash;
        Assert(hash is not null, "Le condensat est ecrit.");
        Assert(
            harness.PasswordService.Verify(
                    assignment.PortalUserId!,
                    hash!,
                    Password)
                == PasswordVerificationResult.Success,
            "Le condensat est celui d'IPortalPasswordService, lie a "
            + "l'identifiant de l'utilisateur.");
        Assert(
            harness.PasswordService.Verify(
                    assignment.PortalUserId!,
                    hash!,
                    "MauvaisMotDePasse!")
                != PasswordVerificationResult.Success,
            "Un autre mot de passe ne passe pas.");
    }

    // ==================================================================
    // 17 a 19. Chaine KoXo / annuaire
    // ==================================================================

    private static async Task VerifyKoxoOwnedDirectoryNeverCreatesNorSetsPassword()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "alice@example.invalid");
        harness.Directory.Publish(
            harness.KoxoIdentifierOf(assignment.PortalUserId!),
            "1a0b6f0a-9d54-4a1e-9b3f-6d1c2f8a4b71");

        await harness.Service.SetPasswordAsync(
            harness.Emails.LastToken,
            Password,
            CancellationToken.None);

        Assert(
            harness.ActiveDirectory.CreateUserCalls == 0,
            "Aucun CreateUserAsync quand KoXo est maitre : le sAMAccountName "
            + "derive ici differerait de celui derive par KoXo et produirait un "
            + "doublon.");
        Assert(
            harness.ActiveDirectory.SetPasswordCalls == 0,
            "Aucun SetUserPassword LDAP : ForcePasswords=1 ecraserait "
            + "l'ecriture au passage suivant, et le client perdrait ses acces "
            + "sans aucune erreur visible.");
    }

    private static async Task VerifyPasswordIsPublishedForTheKoxoExport()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "alice@example.invalid");

        await harness.Service.SetPasswordAsync(
            harness.Emails.LastToken,
            Password,
            CancellationToken.None);

        Assert(
            harness.PendingPasswords.Consume(assignment.PortalUserId!)
                == Password,
            "Le mot de passe est publie pour la colonne 14 du CSV, seul chemin "
            + "par lequel KoXo l'appliquera a l'annuaire.");
        Assert(
            harness.Koxo.Triggers.Count > 0,
            "Un declenchement de synchronisation KoXo est emis.");
        Assert(
            harness.Repository.StatusOf(harness.LifecycleIdOf(
                assignment.PortalUserId!))
                == BillingV2UserIdentityStatuses.KoxoPending,
            "Faute d'objet annuaire, le cycle reste en koxo_pending : c'est "
            + "l'etat qui le maintient dans l'export.");
    }

    private static async Task VerifyIdentityIsAdoptedByExactEmployeeNumber()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "alice@example.invalid");
        var koxoIdentifier = harness.KoxoIdentifierOf(assignment.PortalUserId!);
        const string objectGuid = "2a0b6f0a-9d54-4a1e-9b3f-6d1c2f8a4b71";

        await harness.Service.SetPasswordAsync(
            harness.Emails.LastToken,
            Password,
            CancellationToken.None);

        // KoXo cree l'objet apres coup : c'est le cas nominal, la
        // synchronisation etant asynchrone.
        harness.Directory.Publish(koxoIdentifier, objectGuid);
        var materialized = await harness.Service.TryMaterializeAsync(
            assignment.PortalUserId!,
            CancellationToken.None);

        Assert(
            materialized.Succeeded
            && materialized.Code
                == BillingV2AdditionalUserMaterializationCodes.Ready,
            $"L'identite creee par KoXo est adoptee ({materialized.Code}).");
        Assert(
            harness.Directory.LookedUpEmployeeNumbers.Contains(koxoIdentifier),
            "La resolution passe par employeeNumber, seule cle fiable : le nom "
            + "est translittere et le sAMAccountName derive par KoXo.");
        var link = await harness.Links.FindUserLinkByPortalUserIdAsync(
            assignment.PortalUserId!,
            CancellationToken.None);
        Assert(
            link is not null
            && string.Equals(
                link.ObjectGuid,
                objectGuid,
                StringComparison.OrdinalIgnoreCase),
            "Le lien customer_ad_links porte exactement l'objet resolu.");
    }

    private static async Task VerifyForeignEmployeeNumberIsNeverAdopted()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "alice@example.invalid");

        await harness.Service.SetPasswordAsync(
            harness.Emails.LastToken,
            Password,
            CancellationToken.None);

        // Un objet annuaire existe, mais sous un AUTRE employeeNumber.
        harness.Directory.Publish(
            "CLI-999999",
            "bb0b6f0a-9d54-4a1e-9b3f-6d1c2f8a4b71");
        var materialized = await harness.Service.TryMaterializeAsync(
            assignment.PortalUserId!,
            CancellationToken.None);

        Assert(
            !materialized.Succeeded
            && materialized.Code == BillingV2AdditionalUserMaterializationCodes
                .DirectoryNotReady,
            "Un employeeNumber different n'est jamais adopte par ressemblance "
            + $"({materialized.Code}).");
        Assert(
            await harness.Links.FindUserLinkByPortalUserIdAsync(
                assignment.PortalUserId!,
                CancellationToken.None) is null,
            "Aucun lien n'est cree vers une identite qui n'est pas la sienne.");
    }

    private static async Task VerifyRetryAfterMaterializationIsIdempotent()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "alice@example.invalid");
        harness.Directory.Publish(
            harness.KoxoIdentifierOf(assignment.PortalUserId!),
            "3a0b6f0a-9d54-4a1e-9b3f-6d1c2f8a4b71");

        await harness.Service.SetPasswordAsync(
            harness.Emails.LastToken,
            Password,
            CancellationToken.None);
        var lookupsAfterFirst = harness.Directory.LookedUpEmployeeNumbers.Count;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var retry = await harness.Service.TryMaterializeAsync(
                assignment.PortalUserId!,
                CancellationToken.None);
            Assert(
                retry.Succeeded
                && retry.Code
                    == BillingV2AdditionalUserMaterializationCodes.Ready,
                $"Une reprise sur un cycle deja pret est un noop reussi ({retry.Code}).");
        }

        Assert(
            harness.PortalUsers.Entries.Count == 1,
            "Aucun second utilisateur portail.");
        Assert(
            harness.Repository.AllocatedKoxoIdentifiers.Count == 1,
            "Aucun second CLI-NNNNNN.");
        Assert(
            (await harness.Links.GetUserLinksByPortalUserIdAsync(
                assignment.PortalUserId!,
                CancellationToken.None)).Count == 1,
            "Aucun second lien Active Directory.");
        Assert(
            harness.Directory.LookedUpEmployeeNumbers.Count == lookupsAfterFirst,
            "Le lien deja present prime sur toute nouvelle resolution : "
            + "reinterroger l'annuaire risquerait d'adopter un autre objet.");
    }

    private static async Task VerifyMaterializationBeforePasswordIsRefused()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "alice@example.invalid");
        harness.Directory.Publish(
            harness.KoxoIdentifierOf(assignment.PortalUserId!),
            "4a0b6f0a-9d54-4a1e-9b3f-6d1c2f8a4b71");

        var materialized = await harness.Service.TryMaterializeAsync(
            assignment.PortalUserId!,
            CancellationToken.None);

        Assert(
            !materialized.Succeeded
            && materialized.Code == BillingV2AdditionalUserMaterializationCodes
                .AwaitingPassword,
            "Tant que le mot de passe n'est pas choisi, rien n'avance : le "
            + "compte annuaire naitrait sans mot de passe maitrise "
            + $"({materialized.Code}).");
        Assert(
            await harness.Links.FindUserLinkByPortalUserIdAsync(
                assignment.PortalUserId!,
                CancellationToken.None) is null,
            "Aucun lien n'est cree avant le mot de passe.");
    }

    private static async Task VerifyDisabledLifecycleStopsMaterializing()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "alice@example.invalid");
        await harness.Service.SetPasswordAsync(
            harness.Emails.LastToken,
            Password,
            CancellationToken.None);

        await harness.Service.DisableAsync(
            "slot-1",
            CustomerId,
            CancellationToken.None);
        harness.Directory.Publish(
            harness.KoxoIdentifierOf(assignment.PortalUserId!),
            "5a0b6f0a-9d54-4a1e-9b3f-6d1c2f8a4b71");

        var materialized = await harness.Service.TryMaterializeAsync(
            assignment.PortalUserId!,
            CancellationToken.None);

        Assert(
            !materialized.Succeeded
            && materialized.Code == BillingV2AdditionalUserMaterializationCodes
                .LifecycleDisabled,
            $"Une place desactivee n'avance plus ({materialized.Code}).");
        Assert(
            await harness.Links.FindUserLinkByPortalUserIdAsync(
                assignment.PortalUserId!,
                CancellationToken.None) is null,
            "La desactivation n'ecrit rien dans l'annuaire — et n'en efface "
            + "rien non plus : ce lot ne construit aucune suppression.");
    }

    private static async Task VerifyDirectoryObjectConflictFailsClosed()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        harness.RegisterSlot("slot-2");
        var first = await harness.AssignAsync("slot-1", "alice@example.invalid");
        var second = await harness.AssignAsync("slot-2", "bob@example.invalid");
        const string sharedGuid = "cc0b6f0a-9d54-4a1e-9b3f-6d1c2f8a4b71";

        // Les deux identifiants KoXo pointent le MEME objet annuaire :
        // situation anormale, mais c'est exactement celle ou une adoption
        // permissive transfererait l'identite d'une personne a l'autre.
        harness.Directory.Publish(
            harness.KoxoIdentifierOf(first.PortalUserId!),
            sharedGuid);
        harness.Directory.Publish(
            harness.KoxoIdentifierOf(second.PortalUserId!),
            sharedGuid);

        await harness.Service.SetPasswordAsync(
            harness.Emails.TokenFor("alice@example.invalid"),
            Password,
            CancellationToken.None);
        await harness.Service.SetPasswordAsync(
            harness.Emails.TokenFor("bob@example.invalid"),
            Password,
            CancellationToken.None);

        var firstLink = await harness.Links.FindUserLinkByPortalUserIdAsync(
            first.PortalUserId!,
            CancellationToken.None);
        var secondLink = await harness.Links.FindUserLinkByPortalUserIdAsync(
            second.PortalUserId!,
            CancellationToken.None);

        Assert(
            firstLink is not null && secondLink is null,
            "Le second n'adopte pas l'objet deja rattache au premier : "
            + "poursuivre transfererait des droits reels au mauvais compte.");
        Assert(
            harness.Repository.StatusOf(
                harness.LifecycleIdOf(second.PortalUserId!))
                == BillingV2UserIdentityStatuses.Failed,
            "Le conflit est enregistre comme echec explicite, pas ignore.");
    }

    // ==================================================================
    // 15. Forme de la clause d'export KoXo
    // ==================================================================

    /// <summary>
    /// Verrouille la forme de la clause d'export.
    /// </summary>
    /// <remarks>
    /// Ce n'est pas une preuve d'execution — <see
    /// cref="BillingV2AdditionalUserIdentitySchemaTests"/> s'en charge sur une
    /// vraie MariaDB. C'est un garde-fou : cette requete decide seule quelles
    /// identites reelles KoXo cree, modifie ou desactive, et une condition
    /// retiree par megarde n'a aucune autre chance d'etre remarquee.
    /// </remarks>
    private static void VerifyExportQueryKeepsEveryMandatoryCondition()
    {
        var sql = KoxoExportCandidateQuery.Sql;
        string[] mandatory =
        [
            "lifecycle.status = 'koxo_pending'",
            "lifecycle.portal_user_id = portal_user.id",
            "lifecycle.customer_id = portal_user.customer_id",
            "sub.customer_id = portal_user.customer_id",
            "slot.subscription_id = lifecycle.subscription_id",
            "slot.identity_reference = portal_user.id",
            "slot.status = 'active'",
            "slot.is_primary = 0",
            "sub.status = 'active'",
            "rule.rule_type = 'contractual_entitlement'",
            "rule.target_type = 'user_slot'",
            "rule.status = 'active'",
            "service.status = 'active'",
            "item.status = 'active'",
            "item.scope_type = 'user'",
            "lifecycle.koxo_unique_identifier =",
            "portal_user.status = 'active'",
            "customer.status = 'active'",
            "customer.demo_kind = 'trial'",
            "NOT (customer.is_demo = TRUE AND customer.demo_kind = 'showcase')"
        ];

        foreach (var condition in mandatory)
        {
            Assert(
                sql.Contains(condition, StringComparison.Ordinal),
                $"La clause d'export KoXo doit conserver la condition « {condition} ».");
        }

        Assert(
            !sql.Contains("password_hash", StringComparison.OrdinalIgnoreCase),
            "L'absence de mot de passe portail n'est jamais un critere "
            + "d'export : elle ne designe aucune identite que KoXo doit creer.");
        // Comparaison sur une forme normalisee : l'assertion porte sur la
        // structure de la clause, pas sur son indentation.
        var collapsed = string.Join(
            ' ',
            sql.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries));
        Assert(
            collapsed.Contains(
                "AND EXISTS ( SELECT 1 FROM billing_v2_user_identity_provisioning lifecycle",
                StringComparison.Ordinal),
            "L'exception passe par une ligne de cycle de vie explicitement "
            + "designante, pas par une caracteristique de l'utilisateur.");
    }

    // ==================================================================
    // Regle d'attribution, cas par cas
    // ==================================================================

    private static void VerifyAssignmentPolicyRefusesEveryIncoherentSnapshot()
    {
        var valid = new BillingV2AdditionalUserSlotSnapshot(
            "slot-1",
            SubscriptionId,
            CustomerId,
            "active",
            IsPrimary: false,
            "active",
            IdentityReference: null,
            CustomerReference,
            HasActiveUserSlotEntitlement: true,
            IncompatibleScopedItemCount: 0,
            HasExistingLifecycle: false,
            EmailAlreadyUsed: false);

        Assert(
            BillingV2AdditionalUserAssignmentPolicy.Validate(
                valid,
                CustomerId,
                SubscriptionId) is null,
            "Un instantane coherent est accepte.");

        (BillingV2AdditionalUserSlotSnapshot Snapshot, string Expected)[] cases =
        [
            (valid with { SubscriptionId = OtherSubscriptionId },
                BillingV2AdditionalUserRejectionCodes.SlotSubscriptionMismatch),
            (valid with { SubscriptionCustomerId = OtherCustomerId },
                BillingV2AdditionalUserRejectionCodes.SlotCustomerMismatch),
            (valid with { CustomerReference = null },
                BillingV2AdditionalUserRejectionCodes.CustomerNotFound),
            (valid with { IsPrimary = true },
                BillingV2AdditionalUserRejectionCodes.SlotIsPrimary),
            (valid with { SlotStatus = "cancelled" },
                BillingV2AdditionalUserRejectionCodes.SlotNotActive),
            (valid with { SubscriptionStatus = "draft" },
                BillingV2AdditionalUserRejectionCodes
                    .SubscriptionNotProvisionable),
            (valid with { IdentityReference = "portal-user-existant" },
                BillingV2AdditionalUserRejectionCodes.SlotAlreadyAssigned),
            (valid with { HasExistingLifecycle = true },
                BillingV2AdditionalUserRejectionCodes.LifecycleAlreadyExists),
            (valid with { HasActiveUserSlotEntitlement = false },
                BillingV2AdditionalUserRejectionCodes.SlotEntitlementMissing),
            (valid with { IncompatibleScopedItemCount = 1 },
                BillingV2AdditionalUserRejectionCodes.SlotScopeIncoherent),
            (valid with { EmailAlreadyUsed = true },
                BillingV2AdditionalUserRejectionCodes.EmailAlreadyUsed)
        ];

        foreach (var (snapshot, expected) in cases)
        {
            var actual = BillingV2AdditionalUserAssignmentPolicy.Validate(
                snapshot,
                CustomerId,
                SubscriptionId);
            Assert(
                string.Equals(actual, expected, StringComparison.Ordinal),
                $"Refus attendu « {expected} », obtenu « {actual ?? "aucun"} ».");
        }
    }

    // ==================================================================
    // Banc d'essai
    // ==================================================================

    private sealed class Harness
    {
        public required MockPortalUserStore PortalUsers { get; init; }
        public required MockPortalPasswordSetupRepository PasswordSetups
        { get; init; }
        public required MockBillingV2AdditionalUserIdentityRepository Repository
        { get; init; }
        public required MockActiveDirectoryLinkRepository Links { get; init; }
        public required RecordingEmailDispatch Emails { get; init; }
        public required RecordingKoxoTrigger Koxo { get; init; }
        public required PublishedDirectory Directory { get; init; }
        public required CountingActiveDirectoryService ActiveDirectory
        { get; init; }
        public required IKoxoPendingPasswordStore PendingPasswords
        { get; init; }
        public required IPortalPasswordService PasswordService { get; init; }
        public required BillingV2AdditionalUserIdentityService Service
        { get; init; }

        public static Harness Create()
        {
            var portalUsers = new MockPortalUserStore();
            var passwordSetups =
                new MockPortalPasswordSetupRepository(portalUsers);
            var repository = new MockBillingV2AdditionalUserIdentityRepository(
                portalUsers,
                passwordSetups);
            repository.RegisterCustomer(CustomerId, CustomerReference);
            repository.RegisterCustomer(OtherCustomerId, OtherCustomerReference);

            var links = new MockActiveDirectoryLinkRepository();
            var emails = new RecordingEmailDispatch();
            var koxo = new RecordingKoxoTrigger();
            var directory = new PublishedDirectory();
            var activeDirectory = new CountingActiveDirectoryService();
            var pendingPasswords = new KoxoPendingPasswordStore(
                NullLogger<KoxoPendingPasswordStore>.Instance);
            var passwordService = new PortalPasswordService();

            // controlled_write : c'est le mode de production, celui ou KoXo
            // fait autorite. Le tester en mock validerait le mauvais chemin.
            var adConfiguration = new AdRuntimeConfiguration(
                AdIntegrationMode.ControlledWrite,
                "clients.home.bzh",
                "OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
                "DC=clients,DC=home,DC=bzh",
                ["OU=KoXoAdm,DC=clients,DC=home,DC=bzh"],
                UseCurrentWindowsCredentials: false,
                "svc-kermaria",
                "NOT_A_REAL_PASSWORD",
                ConnectTimeoutMs: 100,
                QueryTimeoutMs: 100,
                MaxResults: 10,
                ConfigurationValid: true);

            return new Harness
            {
                PortalUsers = portalUsers,
                PasswordSetups = passwordSetups,
                Repository = repository,
                Links = links,
                Emails = emails,
                Koxo = koxo,
                Directory = directory,
                ActiveDirectory = activeDirectory,
                PendingPasswords = pendingPasswords,
                PasswordService = passwordService,
                Service = new BillingV2AdditionalUserIdentityService(
                    repository,
                    passwordSetups,
                    passwordService,
                    activeDirectory,
                    links,
                    directory,
                    pendingPasswords,
                    koxo,
                    emails,
                    new SignupRuntimeConfiguration(
                        Enabled: true,
                        RateLimitPerIpPerHour: 10,
                        RateLimitPerEmailPer24h: 10,
                        VerificationTokenTtlHours: 24,
                        PasswordSetupTokenTtlHours: 24,
                        AutoApprove: false),
                    new EmailRuntimeConfiguration(
                        EmailIntegrationMode.Mock,
                        SmtpHost: null,
                        SmtpPort: 587,
                        SmtpUseStartTls: true,
                        SmtpUsername: null,
                        SmtpPassword: null,
                        FromAddress: null,
                        FromDisplayName: "Kermaria",
                        PortalPublicUrl: "https://portail.example.invalid",
                        ContactFormRecipient: null,
                        RequestTimeoutMs: 1000,
                        LiveAllowlistOnly: true,
                        LiveAllowlist: [],
                        ConfigurationValid: true),
                    adConfiguration,
                    NullLogger<BillingV2AdditionalUserIdentityService>.Instance)
            };
        }

        public MockBillingV2AdditionalUserIdentityRepository.Slot RegisterSlot(
            string id,
            string? customerId = null,
            string? subscriptionId = null,
            bool isPrimary = false)
            => Repository.RegisterSlot(
                new MockBillingV2AdditionalUserIdentityRepository.Slot
                {
                    Id = id,
                    SubscriptionId = subscriptionId ?? SubscriptionId,
                    SubscriptionCustomerId = customerId ?? CustomerId,
                    SubscriptionStatus = "active",
                    IsPrimary = isPrimary,
                    Status = "active"
                });

        public Task<BillingV2AdditionalUserOperationResult> AssignAsync(
            string subscriptionUserId,
            string email)
            => Service.AssignAsync(
                new BillingV2AdditionalUserAssignment(
                    CustomerId,
                    SubscriptionId,
                    subscriptionUserId,
                    email,
                    "Utilisateur Test",
                    "madame",
                    "Alice",
                    "Martin",
                    new DateOnly(1990, 4, 12),
                    "AM",
                    "+33100000000",
                    "admin-test"),
                "correlation-test",
                CancellationToken.None);

        public string KoxoIdentifierOf(string portalUserId)
            => Repository
                .FindByPortalUserIdAsync(portalUserId, CancellationToken.None)
                .GetAwaiter()
                .GetResult()!
                .KoxoUniqueIdentifier;

        public string LifecycleIdOf(string portalUserId)
            => Repository
                .FindByPortalUserIdAsync(portalUserId, CancellationToken.None)
                .GetAwaiter()
                .GetResult()!
                .Id;
    }

    /// <summary>
    /// Annuaire simule interroge <b>uniquement</b> par employeeNumber.
    /// </summary>
    /// <remarks>
    /// Volontairement incapable de repondre a autre chose : c'est le seul mode
    /// de recherche legitime quand KoXo fait autorite, et un faux plus
    /// complaisant laisserait passer une adoption par ressemblance de nom.
    /// </remarks>
    private sealed class PublishedDirectory : IAdGroupProvisioner
    {
        private readonly Dictionary<string, AdDirectoryObjectSummary> _objects =
            new(StringComparer.Ordinal);

        public List<string> LookedUpEmployeeNumbers { get; } = [];

        public string ModeName => "test";
        public bool RequiresConfiguredGroupDistinguishedNames => false;

        public void Publish(string employeeNumber, string objectGuid)
            => _objects[employeeNumber] = new AdDirectoryObjectSummary(
                objectGuid,
                $"S-1-5-21-1004336348-1177238915-682003330-{_objects.Count + 1100}",
                "user",
                employeeNumber.Replace("-", string.Empty).ToLowerInvariant(),
                null,
                "Utilisateur Test",
                $"CN={employeeNumber},OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
                CustomerReference,
                IsDisabled: false);

        public Task<AdGroupProvisionerResult> AddUserToGroupAsync(
            CustomerAdLinkSummary user,
            string groupSamAccountName,
            string? groupDistinguishedName,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AdGroupProvisionerResult> RemoveUserFromGroupAsync(
            CustomerAdLinkSummary user,
            string groupSamAccountName,
            string? groupDistinguishedName,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AdDirectoryObjectSummary?> ResolveUserByEmployeeNumberAsync(
            string employeeNumber,
            CancellationToken cancellationToken)
        {
            LookedUpEmployeeNumbers.Add(employeeNumber);
            return Task.FromResult(
                _objects.TryGetValue(employeeNumber, out var found)
                    ? found
                    : null);
        }
    }

    /// <summary>
    /// Service annuaire qui compte les ecritures au lieu de les executer.
    /// </summary>
    /// <remarks>
    /// Toute ecriture est aussi un echec du test qui l'a provoquee : en mode
    /// controlled_write, ni la creation ni la pose de mot de passe LDAP ne sont
    /// autorisees.
    /// </remarks>
    private sealed class CountingActiveDirectoryService : IActiveDirectoryService
    {
        public int CreateUserCalls { get; private set; }
        public int SetPasswordCalls { get; private set; }

        public string ModeName => "test";

        public Task<AdServiceResult<AdDirectoryObjectSummary>> CreateUserAsync(
            string customerReference,
            CreateAdUserRequest? request,
            CancellationToken cancellationToken)
        {
            CreateUserCalls++;
            return Task.FromResult(
                new AdServiceResult<AdDirectoryObjectSummary>(
                    500,
                    "AD_UNEXPECTED_CREATE",
                    "Creation interdite dans ce test.",
                    null));
        }

        public Task<AdServiceResult<AdDirectoryObjectSummary>>
            SetUserPasswordAsync(
                string customerReference,
                string? samAccountName,
                string? newPassword,
                CancellationToken cancellationToken)
        {
            SetPasswordCalls++;
            return Task.FromResult(
                new AdServiceResult<AdDirectoryObjectSummary>(
                    500,
                    "AD_UNEXPECTED_PASSWORD",
                    "Ecriture de mot de passe interdite dans ce test.",
                    null));
        }

        public Task<AdStatusResponse> GetStatusAsync(
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchUsersAsync(string? query, string? customerReference, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchGroupsAsync(string? query, string? customerReference, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> ResolveObjectForLinkAsync(string customerReference, string? distinguishedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> CreateGroupAsync(string customerReference, CreateAdGroupRequest? request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> AddGroupMemberAsync(string customerReference, string? groupSamAccountName, string? userSamAccountName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> RemoveGroupMemberAsync(string customerReference, string? groupSamAccountName, string? userSamAccountName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> DisableUserAsync(string customerReference, string? samAccountName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserToDisabledAsync(string customerReference, string? samAccountName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> GetUserEffectiveGroupsAsync(string customerReference, string? samAccountName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> RenameUserAsync(string customerReference, string? currentSamAccountName, RenameAdUserRequest? request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserAsync(string customerReference, string? samAccountName, MoveAdUserRequest? request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> ChangeUserPasswordAsync(string customerReference, string? samAccountName, string? currentPassword, string? newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingKoxoTrigger : IKoxoSyncWebhookTriggerService
    {
        public List<KoxoSyncWebhookTriggerRequest> Triggers { get; } = [];

        public Task TriggerAsync(
            KoxoSyncWebhookTriggerRequest request,
            CancellationToken cancellationToken)
        {
            Triggers.Add(request);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Capture les liens d'invitation pour en extraire le jeton en clair.
    /// </summary>
    /// <remarks>
    /// C'est le seul endroit ou le jeton existe encore en clair : le tester
    /// depuis la base serait impossible, et c'est precisement ce qu'on veut
    /// prouver.
    /// </remarks>
    private sealed class RecordingEmailDispatch : IEmailDispatchService
    {
        private readonly Dictionary<string, string> _tokensByEmail =
            new(StringComparer.OrdinalIgnoreCase);

        public string? LastToken { get; private set; }

        public string? TokenFor(string email)
            => _tokensByEmail.TryGetValue(email, out var token) ? token : null;

        public Task<EmailDispatchResult> SendAccountApprovedAsync(
            string email,
            string contactName,
            string setPasswordUrl,
            string correlationId,
            CancellationToken cancellationToken)
        {
            var marker = "token=";
            var index = setPasswordUrl.IndexOf(marker, StringComparison.Ordinal);
            var token = index < 0
                ? null
                : Uri.UnescapeDataString(
                    setPasswordUrl[(index + marker.Length)..]);
            LastToken = token;
            if (token is not null)
            {
                _tokensByEmail[email] = token;
            }

            return Task.FromResult(
                new EmailDispatchResult(true, "mock", string.Empty));
        }

        public Task<EmailDispatchResult> SendInvoiceIssuedAsync(string documentId, string correlationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmailDispatchResult> SendPaymentReminderAsync(string documentId, string correlationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmailDispatchResult> SendPaymentConfirmedAsync(string documentId, string correlationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmailDispatchResult> SendContactFormAsync(ContactFormSubmission submission, string correlationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmailDispatchResult> SendSignupVerificationAsync(string email, string contactName, string verificationUrl, string correlationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmailDispatchResult> SendAccountRejectedAsync(string email, string contactName, string? reason, string correlationId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
