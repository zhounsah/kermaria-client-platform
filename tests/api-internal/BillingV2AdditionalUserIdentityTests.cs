using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Email;
using Kermaria.ApiInternal.Services.Provisioning;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
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
        await VerifyInvisibleSlotAnswersLikeAnAbsentOne();
        await VerifyResendHonoursTheSubscriptionInTheUrl();
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
        await VerifyRuntimeConvergenceClosesKoxoPendingGap();
        VerifyDedicatedGateConfigurationIsNarrow();
        await VerifyDedicatedGateDoesNotOpenGlobalProvisioning();
        await VerifyIdentityIsAdoptedByExactEmployeeNumber();
        await VerifyForeignEmployeeNumberIsNeverAdopted();
        await VerifyRetryAfterMaterializationIsIdempotent();
        await VerifyMaterializationBeforePasswordIsRefused();
        await VerifyDisabledLifecycleStopsMaterializing();
        await VerifyDirectoryObjectConflictFailsClosed();
        await VerifyPendingPasswordSurvivesUntilTheLinkIsProven();
        await VerifyGateOffRefusesEveryMutation();
        await VerifyGateOffStillValidatesATokenReadOnly();
        await VerifyUnavailableHandoffNeverConsumesTheToken();
        VerifyProtectorRoundTripsWithoutLeakingPlaintext();
        await VerifyTokenAndSecretCommitTogether();
        await VerifyFailedHandoffRollsEverythingBack();
        await VerifyExportRefusesALineWithoutItsSecret();
        await VerifyExportRefusesAnUnreadableSecret();
        await VerifyLinkedUserNeedsNoPendingSecret();
        await VerifyAcknowledgeAndReadyAreIdempotent();
        VerifyExportQueryKeepsEveryMandatoryCondition();
        VerifyAssignmentPolicyRefusesEveryIncoherentSnapshot();
        VerifyComposedSlotQueriesAreWellFormed();
        await VerifyListingShowsEveryRealEmptySlot();
        await VerifyListingHidesWhatIsNotAnAdditionalUserSlot();
        await VerifyInactiveSlotIsNotAdministrable();
        await VerifyInactiveSubscriptionHasNoAdministrableSlot();
        await VerifyCrossCustomerListingIsEmpty();
        await VerifyEveryLifecycleMapsToAProductState();
        await VerifyGateOffClosesEveryOfferedAction();
        await VerifyForeignPurposeTokenChangesNothing();
        VerifySlotSummaryCarriesNoTechnicalData();
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
            !result.Succeeded,
            "Une place d'un autre client est refusee : l'attribuer creerait "
            + $"une identite dans le mauvais perimetre annuaire ({result.Code}).");
        Assert(
            harness.PortalUsers.Entries.Count == 0,
            "Un refus ne laisse aucun utilisateur derriere lui.");
    }

    /// <summary>
    /// Une place invisible repond <b>exactement</b> comme une place absente.
    /// </summary>
    /// <remarks>
    /// La comparaison est faite entre les deux reponses reelles, pas contre
    /// une constante : c'est l'egalite qui est le contrat. Le code et le
    /// message sont tous deux verifies parce que l'espace client affiche le
    /// message tel quel — n'unifier que le statut HTTP laisserait la phrase
    /// « cette place n'appartient pas a votre organisation » confirmer a un
    /// inconnu qu'une place existe ailleurs.
    /// </remarks>
    private static async Task VerifyInvisibleSlotAnswersLikeAnAbsentOne()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-etranger", customerId: OtherCustomerId);
        harness.RegisterSlot(
            "slot-autre-abonnement",
            subscriptionId: OtherSubscriptionId);

        var unknown = await harness.AssignAsync(
            "slot-inexistant",
            "a@example.invalid");
        var foreignCustomer = await harness.AssignAsync(
            "slot-etranger",
            "b@example.invalid");
        var foreignSubscription = await harness.AssignAsync(
            "slot-autre-abonnement",
            "c@example.invalid");

        foreach (var (label, result) in new[]
        {
            ("place d'un autre client", foreignCustomer),
            ("place d'un autre abonnement", foreignSubscription)
        })
        {
            Assert(
                !result.Succeeded && !unknown.Succeeded,
                $"La {label} est refusee, comme la place inexistante.");
            Assert(
                string.Equals(result.Code, unknown.Code, StringComparison.Ordinal),
                $"La {label} rend le meme code qu'une place inexistante "
                + $"(« {result.Code} » contre « {unknown.Code} »).");
            Assert(
                string.Equals(
                    result.Message,
                    unknown.Message,
                    StringComparison.Ordinal),
                $"La {label} rend le meme message qu'une place inexistante "
                + $"(« {result.Message} » contre « {unknown.Message} »).");
            Assert(
                result.PortalUserId is null && result.LifecycleStatus is null,
                $"La {label} ne renvoie aucune donnee de la place visee.");
        }

        Assert(
            harness.PortalUsers.Entries.Count == 0,
            "Aucun de ces refus ne cree quoi que ce soit.");
    }

    /// <summary>
    /// Le renvoi d'invitation honore l'abonnement porte par l'URL.
    /// </summary>
    /// <remarks>
    /// Meme client, mais la place appartient a un autre abonnement : l'adresse
    /// appelee ne la designe pas. Le refus est celui d'une place absente, sinon
    /// l'URL d'un abonnement deviendrait un revelateur des places des autres.
    /// </remarks>
    private static async Task VerifyResendHonoursTheSubscriptionInTheUrl()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        await harness.AssignAsync("slot-1", "alice@example.invalid");
        var issued = harness.Emails.LastToken!;

        var mismatched = await harness.Service.ResendInvitationAsync(
            "slot-1",
            OtherSubscriptionId,
            CustomerId,
            "correlation",
            CancellationToken.None);
        var unknown = await harness.Service.ResendInvitationAsync(
            "slot-inexistant",
            OtherSubscriptionId,
            CustomerId,
            "correlation",
            CancellationToken.None);

        Assert(
            !mismatched.Succeeded,
            "Une place appelee depuis l'URL d'un autre abonnement est refusee "
            + $"({mismatched.Code}).");
        Assert(
            string.Equals(mismatched.Code, unknown.Code, StringComparison.Ordinal)
            && string.Equals(
                mismatched.Message,
                unknown.Message,
                StringComparison.Ordinal),
            "Le refus est indistinguable de celui d'une place inexistante "
            + $"(« {mismatched.Code} » contre « {unknown.Code} »).");
        Assert(
            string.Equals(
                harness.Emails.LastToken,
                issued,
                StringComparison.Ordinal),
            "Aucun nouveau jeton n'est emis : un refus ne doit pas invalider "
            + "le lien deja envoye a la personne.");

        var accepted = await harness.Service.ResendInvitationAsync(
            "slot-1",
            SubscriptionId,
            CustomerId,
            "correlation",
            CancellationToken.None);
        Assert(
            accepted.Succeeded,
            $"L'abonnement reel reste servi ({accepted.Code}).");
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

        // Le code exact du refus est celui d'une place introuvable : la
        // distinction reste interne a la politique, verifiee par
        // VerifyAssignmentPolicyRefusesEveryIncoherentSnapshot.
        Assert(
            !result.Succeeded,
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
        // Meme semantique que sur MariaDB : les perdants apprennent que la
        // place est prise, jamais que « un cycle de vie existe deja » — ce
        // dernier code doit rester reserve a un etat incoherent.
        Assert(
            results
                .Where(result => !result.Succeeded)
                .All(result => result.Code
                    == BillingV2AdditionalUserRejectionCodes
                        .SlotAlreadyAssigned),
            "Un perdant de course est un conflit de place : "
            + string.Join(
                ", ",
                results
                    .Where(result => !result.Succeeded)
                    .Select(result => result.Code)
                    .Distinct()));
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
            SubscriptionId,
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
            await harness.PendingPasswords.PeekAsync(
                assignment.PortalUserId!,
                CancellationToken.None) == Password,
            "Le mot de passe est publie pour la colonne 14 du CSV, seul chemin "
            + "par lequel KoXo l'appliquera a l'annuaire.");
        Assert(
            await harness.PendingPasswords.PeekAsync(
                assignment.PortalUserId!,
                CancellationToken.None) == Password,
            "Une relecture ne consomme pas le secret : tant que l'identite "
            + "n'est pas confirmee, l'instantane suivant doit encore le "
            + "porter.");
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

    private static async Task VerifyRuntimeConvergenceClosesKoxoPendingGap()
    {
        var harness = Harness.Create(
            provisioningEnabled: false,
            additionalUserProvisioningEnabled: true);
        harness.RegisterSlot("slot-convergence");
        var assignment = await harness.AssignAsync(
            "slot-convergence",
            "convergence@example.invalid");

        await harness.Service.SetPasswordAsync(
            harness.Emails.LastToken,
            Password,
            CancellationToken.None);
        Assert(
            harness.Repository.StatusOf(harness.LifecycleIdOf(
                assignment.PortalUserId!))
                == BillingV2UserIdentityStatuses.KoxoPending,
            "Sans objet annuaire, le handoff reste en koxo_pending.");

        harness.Directory.Publish(
            harness.KoxoIdentifierOf(assignment.PortalUserId!),
            "8f47c9e1-6ab2-4d35-91fe-20c7a8b3d654");
        var completed = await harness.Service.ConvergePendingAsync(
            50,
            CancellationToken.None);

        Assert(completed == 1, "La passe de convergence ferme exactement un cycle.");
        Assert(
            harness.Repository.StatusOf(harness.LifecycleIdOf(
                assignment.PortalUserId!))
                == BillingV2UserIdentityStatuses.Ready,
            "Le cycle passe de koxo_pending a ready sans TryMaterializeAsync manuel.");
        Assert(
            await harness.Links.FindUserLinkByPortalUserIdAsync(
                assignment.PortalUserId!,
                CancellationToken.None) is not null,
            "La convergence prouve et persiste le lien annuaire.");
        Assert(
            await harness.PendingPasswords.PeekAsync(
                assignment.PortalUserId!,
                CancellationToken.None) is null,
            "Le secret KoXo est retire seulement apres la convergence prouvee.");
    }

    private static void VerifyDedicatedGateConfigurationIsNarrow()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BILLING_V2_PROVISIONING_ENABLED"] = "false",
                ["BILLING_V2_ADDITIONAL_USER_PROVISIONING_ENABLED"] = "true"
            })
            .Build();
        var runtime = BillingV2RuntimeConfiguration.Resolve(configuration);

        Assert(
            !runtime.ProvisioningEnabled,
            "Le gate dedie ne doit jamais ouvrir le provisioning Billing V2 global.");
        Assert(
            runtime.AdditionalUserProvisioningEnabled
            && runtime.AdditionalUserMutationsEnabled,
            "Le gate dedie doit ouvrir uniquement les mutations USER-ADDITIONAL.");
    }

    private static async Task VerifyDedicatedGateDoesNotOpenGlobalProvisioning()
    {
        var harness = Harness.Create(
            provisioningEnabled: false,
            additionalUserProvisioningEnabled: true);
        harness.RegisterSlot("slot-dedicated-gate");

        var assignment = await harness.AssignAsync(
            "slot-dedicated-gate",
            "dedicated-gate@example.invalid");

        Assert(
            assignment.Succeeded,
            $"Le gate USER-ADDITIONAL autorise son propre parcours ({assignment.Code}).");
        Assert(
            harness.PortalUsers.Entries.Count == 1,
            "Le gate dedie ouvre uniquement la mutation USER-ADDITIONAL testee.");
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

    // ==================================================================
    // Reprise apres crash et verrou de provisioning
    // ==================================================================

    /// <summary>
    /// Le secret destine a KoXo survit a un instantane qui n'aboutit pas.
    /// </summary>
    /// <remarks>
    /// C'est le seul secret reversible du systeme : le portail n'en garde
    /// qu'un condensat, et KoXo a besoin du mot de passe reel pour la colonne
    /// 14 du CSV. Le retirer au premier instantane le perdait des que l'export
    /// echouait ensuite, ou que l'API redemarrait entre les deux — et la
    /// personne perdait VPN, RDS et stockage sans aucune erreur visible.
    /// </remarks>
    private static async Task VerifyPendingPasswordSurvivesUntilTheLinkIsProven()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "reprise@example.invalid");

        await harness.Service.SetPasswordAsync(
            harness.Emails.LastToken,
            Password,
            CancellationToken.None);
        var portalUserId = assignment.PortalUserId!;

        // Trois relectures d'affilee : l'identite n'est pas encore confirmee,
        // le secret doit encore etre la a chaque fois.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert(
                await harness.PendingPasswords.PeekAsync(
                    portalUserId,
                    CancellationToken.None) == Password,
                "Tant que le lien annuaire n'est pas prouve, chaque instantane "
                + "doit pouvoir reporter le meme mot de passe.");
        }

        // KoXo cree enfin l'objet : la materialisation aboutit, le lien est
        // ecrit puis relu, et seulement la le secret disparait.
        harness.Directory.Publish(
            harness.KoxoIdentifierOf(portalUserId),
            "6a0b1c2d-3e4f-5061-7283-94a5b6c7d8e9");
        var materialized = await harness.Service.TryMaterializeAsync(
            portalUserId,
            CancellationToken.None);

        Assert(
            materialized.Succeeded,
            $"La materialisation doit aboutir ({materialized.Code}).");
        Assert(
            await harness.PendingPasswords.PeekAsync(
                portalUserId,
                CancellationToken.None) is null,
            "Le secret n'est efface qu'apres la preuve durable du lien AD.");
    }

    /// <summary>
    /// Provisioning desactive : aucune operation a effet reel ne passe.
    /// </summary>
    /// <remarks>
    /// Le refus tombe <b>avant</b> tout point de non-retour. Un refus tardif
    /// laisserait un jeton a usage unique consomme et un compte sans identite
    /// annuaire, etat dont on ne revient pas sans intervention.
    /// </remarks>
    private static async Task VerifyGateOffRefusesEveryMutation()
    {
        var enabled = Harness.Create();
        enabled.RegisterSlot("slot-1");
        var assignment = await enabled.AssignAsync(
            "slot-1",
            "gate@example.invalid");
        var token = enabled.Emails.LastToken;

        var harness = Harness.Create(provisioningEnabled: false);
        harness.RegisterSlot("slot-1");

        var assign = await harness.AssignAsync(
            "slot-1",
            "gate-off@example.invalid");
        Assert(
            !assign.Succeeded
            && assign.Code == BillingV2AdditionalUserMaterializationCodes
                .ProvisioningDisabled,
            $"L'attribution est refusee par le drapeau ({assign.Code}).");
        Assert(
            harness.PortalUsers.Entries.Count == 0
            && harness.Repository.AllocatedKoxoIdentifiers.Count == 0,
            "Aucun utilisateur portail, aucun CLI-NNNNNN : le refus precede "
            + "la premiere ecriture.");
        Assert(
            harness.Emails.LastToken is null,
            "Aucune invitation n'est envoyee.");

        foreach (var (label, result) in new[]
        {
            ("definition du mot de passe", await harness.Service
                .SetPasswordAsync(token, Password, CancellationToken.None)),
            ("materialisation", await harness.Service.TryMaterializeAsync(
                assignment.PortalUserId!,
                CancellationToken.None)),
            ("renvoi d'invitation", await harness.Service
                .ResendInvitationAsync(
                    "slot-1",
                    SubscriptionId,
                    CustomerId,
                    "correlation-test",
                    CancellationToken.None)),
            ("desactivation", await harness.Service.DisableAsync(
                "slot-1",
                CustomerId,
                CancellationToken.None))
        })
        {
            Assert(
                !result.Succeeded
                && result.Code == BillingV2AdditionalUserMaterializationCodes
                    .ProvisioningDisabled,
                $"Le drapeau refuse aussi la {label} ({result.Code}).");
        }

        Assert(
            await IsTokenStillUsable(enabled, token),
            "Le jeton n'a pas ete consomme : le refus tombe avant la "
            + "consommation, qui est a usage unique.");
        Assert(
            harness.Koxo.Triggers.Count == 0
            && harness.ActiveDirectory.CreateUserCalls == 0
            && harness.ActiveDirectory.SetPasswordCalls == 0,
            "Aucun appel KoXo ni AD n'est emis drapeau ferme.");
    }

    /// <summary>
    /// La validation en lecture d'un jeton reste autorisee.
    /// </summary>
    /// <remarks>
    /// Elle ne mute rien : la refuser afficherait « lien invalide » a une
    /// personne dont le lien est parfaitement valable, ce qui la pousserait a
    /// en demander un autre.
    /// </remarks>
    private static async Task VerifyGateOffStillValidatesATokenReadOnly()
    {
        var harness = Harness.Create(provisioningEnabled: false);
        var portalUserId = Guid.NewGuid().ToString("D");
        harness.PortalUsers.TryAdd(
            new MockPortalUserStore.Entry(
                portalUserId,
                CustomerId,
                "lecture@example.invalid",
                "Lecture Seule",
                "CLI-000999",
                PasswordHash: null));
        var token = PortalSetupToken.Generate();
        await harness.PasswordSetups.IssueAsync(
            new PortalPasswordSetupIssue(
                Guid.NewGuid().ToString("D"),
                portalUserId,
                BillingV2AdditionalUserIdentityConventions
                    .PasswordSetupPurpose,
                PortalSetupToken.Hash(token),
                DateTime.UtcNow.AddHours(24)),
            CancellationToken.None);

        var validation = await harness.Service.ValidateInvitationTokenAsync(
            token,
            CancellationToken.None);

        Assert(
            validation.Succeeded && validation.Code == "TOKEN_VALID",
            $"La validation en lecture reste possible ({validation.Code}).");
        Assert(
            await IsTokenStillUsable(harness, token),
            "Elle ne consomme rien.");
    }

    /// <summary>
    /// Relais de mot de passe indisponible : le jeton n'est pas consomme.
    /// </summary>
    /// <remarks>
    /// Consommer d'abord et decouvrir ensuite que le secret n'atteindra jamais
    /// l'annuaire laisserait la personne sans second lien pour recommencer.
    /// </remarks>
    private static async Task VerifyUnavailableHandoffNeverConsumesTheToken()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        await harness.AssignAsync("slot-1", "relais@example.invalid");
        var token = harness.Emails.LastToken;
        harness.PendingPasswords.Operational = false;

        var result = await harness.Service.SetPasswordAsync(
            token,
            Password,
            CancellationToken.None);

        Assert(
            !result.Succeeded
            && result.Code == BillingV2AdditionalUserMaterializationCodes
                .PasswordHandoffUnavailable,
            $"Le relais indisponible fait echouer fermement ({result.Code}).");
        Assert(
            await IsTokenStillUsable(harness, token),
            "Le jeton reste utilisable : le refus precede sa consommation.");
        Assert(
            harness.Koxo.Triggers.Count == 0,
            "Aucune synchronisation n'est declenchee : KoXo creerait le compte "
            + "avec un mot de passe que personne ne connait.");
    }

    /// <summary>
    /// Le chiffrement du secret est reversible, lie a sa ligne, et ne laisse
    /// pas fuir le clair.
    /// </summary>
    private static void VerifyProtectorRoundTripsWithoutLeakingPlaintext()
    {
        var key = Convert.ToBase64String(new byte[32]);
        var protector = KoxoPendingPasswordProtector.TryCreate(key);
        Assert(protector is not null, "Une cle de 32 octets est acceptee.");

        var envelope = protector!.Protect(Password, "portal-user-1");
        Assert(
            !envelope.Contains(Password, StringComparison.Ordinal),
            "Le chiffre ne contient jamais le clair.");
        Assert(
            protector.Unprotect(envelope, "portal-user-1") == Password,
            "Le secret est relisible autant de fois que necessaire.");
        Assert(
            protector.Unprotect(envelope, "portal-user-1") == Password,
            "La relecture n'est pas destructive.");

        // Lie a sa ligne : un chiffre deplace d'une personne a une autre ne
        // doit pas se dechiffrer, sinon on attribuerait le mot de passe de
        // quelqu'un a quelqu'un d'autre.
        Assert(
            protector.Unprotect(envelope, "portal-user-2") is null,
            "Un chiffre deplace vers un autre utilisateur est illisible.");

        // Rotation de cle : ne jamais deviner, sinon un mot de passe faux
        // serait applique a un compte reel.
        var other = KoxoPendingPasswordProtector.TryCreate(
            Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()))!;
        Assert(
            other.KeyId != protector.KeyId,
            "Deux cles distinctes portent deux empreintes distinctes.");
        Assert(
            other.Unprotect(envelope, "portal-user-1") is null,
            "Une autre cle ne dechiffre pas.");

        // Fail-closed sur configuration absente ou aberrante.
        foreach (var invalid in new[]
        {
            null,
            "",
            "   ",
            "pas-du-base64!",
            Convert.ToBase64String(new byte[16])
        })
        {
            Assert(
                KoxoPendingPasswordProtector.TryCreate(invalid) is null,
                "Une cle absente ou de mauvaise taille est refusee, jamais "
                + "remplacee par une cle improvisee.");
        }
    }

    // ==================================================================
    // Atomicite du relais, fail-closed de l'export, nettoyage
    // ==================================================================

    /// <summary>
    /// Il n'existe aucun instant ou le jeton est consomme sans que le secret
    /// existe.
    /// </summary>
    /// <remarks>
    /// Ce n'est pas une question de sequencement mais de construction : les
    /// deux ecritures sont dans la meme unite de travail, donc il n'y a pas
    /// d'entre-deux ou s'arreter. Le test le constate par l'observable qui
    /// compte — apres l'appel, jeton consomme ET secret present, ou ni l'un ni
    /// l'autre.
    /// </remarks>
    private static async Task VerifyTokenAndSecretCommitTogether()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "atomique@example.invalid");
        var token = harness.Emails.LastToken;
        var portalUserId = assignment.PortalUserId!;

        // Avant : le jeton est utilisable et aucun secret n'existe.
        Assert(
            await IsTokenStillUsable(harness, token)
            && await harness.PendingPasswords.PeekAsync(
                portalUserId,
                CancellationToken.None) is null,
            "Avant la saisie, jeton libre et aucun secret.");

        var result = await harness.Service.SetPasswordAsync(
            token,
            Password,
            CancellationToken.None);

        Assert(result.Succeeded, $"La saisie doit reussir ({result.Code}).");
        Assert(
            !await IsTokenStillUsable(harness, token)
            && await harness.PendingPasswords.PeekAsync(
                portalUserId,
                CancellationToken.None) == Password,
            "Apres la saisie, jeton consomme ET secret present : les deux "
            + "ecritures sont indissociables.");
        Assert(
            harness.Repository.StatusOf(harness.LifecycleIdOf(portalUserId))
                == BillingV2UserIdentityStatuses.KoxoPending,
            "La transition du cycle de vie fait partie de la meme unite de "
            + "travail.");
    }

    /// <summary>
    /// Une ecriture du relais qui echoue annule tout.
    /// </summary>
    /// <remarks>
    /// Sans ce comportement, le jeton — a usage unique — serait perdu et le
    /// mot de passe, qui n'existe en clair qu'a cet instant, avec lui. La
    /// personne n'aurait plus aucun moyen de reprendre le parcours.
    /// </remarks>
    private static async Task VerifyFailedHandoffRollsEverythingBack()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "rollback@example.invalid");
        var token = harness.Emails.LastToken;
        var portalUserId = assignment.PortalUserId!;
        harness.PendingPasswords.FailAttach = true;

        var result = await harness.Service.SetPasswordAsync(
            token,
            Password,
            CancellationToken.None);

        Assert(
            !result.Succeeded
            && result.Code == BillingV2AdditionalUserMaterializationCodes
                .PasswordHandoffUnavailable,
            $"L'echec du relais fait echouer l'ensemble ({result.Code}).");
        Assert(
            await IsTokenStillUsable(harness, token),
            "Le jeton reste utilisable : rien n'a ete consomme.");
        Assert(
            harness.PortalUsers.Find(portalUserId)?.PasswordHash is null,
            "Le condensat pose est repris : la transaction est annulee en "
            + "entier, pas a moitie.");
        Assert(
            await harness.PendingPasswords.PeekAsync(
                portalUserId,
                CancellationToken.None) is null,
            "Aucun secret n'a survecu a l'annulation.");
        Assert(
            harness.Repository.StatusOf(harness.LifecycleIdOf(portalUserId))
                == BillingV2UserIdentityStatuses.AwaitingPassword,
            "Le cycle de vie n'a pas avance.");
        Assert(
            harness.Koxo.Triggers.Count == 0,
            "Aucune synchronisation KoXo n'est declenchee.");

        // Le parcours reste reprenable : c'est tout l'interet de l'annulation.
        harness.PendingPasswords.FailAttach = false;
        var retry = await harness.Service.SetPasswordAsync(
            token,
            Password,
            CancellationToken.None);
        Assert(
            retry.Succeeded,
            $"Le meme lien doit encore fonctionner ({retry.Code}).");
    }

    /// <summary>
    /// Une ligne creee par l'exception Billing V2, sans secret, refuse
    /// l'export.
    /// </summary>
    /// <remarks>
    /// KoXo va creer l'objet annuaire a partir de cette ligne. Sans mot de
    /// passe en colonne 14, le compte naitrait avec un secret que personne ne
    /// connait, et aucune synchronisation ulterieure ne le rattraperait :
    /// l'objet existerait deja.
    /// </remarks>
    private static async Task VerifyExportRefusesALineWithoutItsSecret()
    {
        var service = NewExportService(
            out var store,
            new KoxoExportCandidate(
                "portal-user-1",
                "CLI-A",
                "CLI-000001",
                "madame",
                "Martin",
                "Alice",
                "1990-04-12",
                "alice@example.invalid",
                IsDemo: false,
                KoxoGroupReference: null,
                RequiresPendingPassword: true));

        var refused = await ExportFailsAsync(service);
        Assert(
            refused is not null
            && refused.Any(invalid =>
                invalid.PortalUserId == "portal-user-1"
                && invalid.Fields.Contains("motDePasse")),
            "Sans secret en attente, la ligne est invalide et l'export "
            + "n'aboutit pas.");

        // Avec le secret, la meme ligne passe.
        await store.PublishAsync(
            "portal-user-1",
            Password,
            CancellationToken.None);
        var exported = await service.ExportAsync(
            "api",
            "phase4-handoff",
            "127.0.0.1",
            CancellationToken.None);
        Assert(
            exported.Users.Single().MotDePasse == Password,
            "Avec son secret, la ligne part normalement.");
    }

    /// <summary>
    /// Un secret illisible vaut un secret absent.
    /// </summary>
    /// <remarks>
    /// Chiffre altere, cle tournee, entree expiree : tous aboutissent a un
    /// <c>null</c>. Devinez a partir de la serait pire que refuser — un mot de
    /// passe faux applique a un compte reel.
    /// </remarks>
    private static async Task VerifyExportRefusesAnUnreadableSecret()
    {
        var service = NewExportService(
            out var store,
            new KoxoExportCandidate(
                "portal-user-1",
                "CLI-A",
                "CLI-000001",
                "madame",
                "Martin",
                "Alice",
                "1990-04-12",
                "alice@example.invalid",
                IsDemo: false,
                KoxoGroupReference: null,
                RequiresPendingPassword: true));
        store.ReturnUnreadable = true;
        await store.PublishAsync(
            "portal-user-1",
            Password,
            CancellationToken.None);

        var refused = await ExportFailsAsync(service);
        Assert(
            refused is not null
            && refused.Any(invalid =>
                invalid.Fields.Contains("motDePasse")),
            "Un secret illisible — cle tournee, chiffre altere — est traite "
            + "comme absent : l'export refuse au lieu de deviner.");
    }

    /// <summary>
    /// Un utilisateur deja lie a l'annuaire n'a besoin d'aucun secret.
    /// </summary>
    /// <remarks>
    /// Son objet existe : KoXo ne le cree pas, il le met a jour. Exiger un
    /// secret ici bloquerait l'export GLOBAL — un seul invalide suffit — et
    /// desactiverait donc tous les comptes du CSV.
    /// </remarks>
    private static async Task VerifyLinkedUserNeedsNoPendingSecret()
    {
        var service = NewExportService(
            out _,
            new KoxoExportCandidate(
                "portal-user-1",
                "CLI-A",
                "CLI-000001",
                "madame",
                "Martin",
                "Alice",
                "1990-04-12",
                "alice@example.invalid",
                IsDemo: false,
                KoxoGroupReference: null,
                RequiresPendingPassword: false));

        var exported = await service.ExportAsync(
            "api",
            "phase4-linked",
            "127.0.0.1",
            CancellationToken.None);

        Assert(
            exported.Users.Single().MotDePasse is null,
            "Un utilisateur deja adopte part sans mot de passe, et c'est "
            + "normal : son objet annuaire existe deja.");
    }

    /// <summary>
    /// La conclusion et l'acquittement se rejouent sans dommage.
    /// </summary>
    private static async Task VerifyAcknowledgeAndReadyAreIdempotent()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "idempotent@example.invalid");
        var portalUserId = assignment.PortalUserId!;

        await harness.Service.SetPasswordAsync(
            harness.Emails.LastToken,
            Password,
            CancellationToken.None);
        harness.Directory.Publish(
            harness.KoxoIdentifierOf(portalUserId),
            "7a0b1c2d-3e4f-5061-7283-94a5b6c7d8e9");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await harness.Service.TryMaterializeAsync(
                portalUserId,
                CancellationToken.None);
            Assert(
                result.Succeeded
                && result.LifecycleStatus
                    == BillingV2UserIdentityStatuses.Ready,
                $"La materialisation rejouee reste stable ({result.Code}).");
            Assert(
                await harness.PendingPasswords.PeekAsync(
                    portalUserId,
                    CancellationToken.None) is null,
                "Un cycle conclu ne conserve aucun secret, y compris apres "
                + "rejeu.");
        }

        var link = await harness.Links.FindUserLinkByPortalUserIdAsync(
            portalUserId,
            CancellationToken.None);
        Assert(
            link is not null
            && string.Equals(
                link.ObjectGuid,
                "7a0b1c2d-3e4f-5061-7283-94a5b6c7d8e9",
                StringComparison.OrdinalIgnoreCase),
            "Le rejeu n'a pas fait basculer l'identite sur un autre objet.");
    }

    // ------------------------------------------------------------------
    // Outils des tests d'export
    // ------------------------------------------------------------------

    private static KoxoExportService NewExportService(
        out ExportPendingPasswordStore store,
        params KoxoExportCandidate[] candidates)
    {
        store = new ExportPendingPasswordStore();
        return new KoxoExportService(
            new StubKoxoRepository(candidates),
            store);
    }

    private static async Task<IReadOnlyList<KoxoInvalidUser>?> ExportFailsAsync(
        KoxoExportService service)
    {
        try
        {
            await service.ExportAsync(
                "api",
                "phase4-fail-closed",
                "127.0.0.1",
                CancellationToken.None);
        }
        catch (KoxoValidationException exception)
        {
            return exception.InvalidUsers;
        }

        return null;
    }

    private sealed class StubKoxoRepository : IKoxoRepository
    {
        private readonly IReadOnlyList<KoxoExportCandidate> _candidates;

        public StubKoxoRepository(IReadOnlyList<KoxoExportCandidate> candidates)
            => _candidates = candidates;

        public bool IsPersistent => false;

        public Task<IReadOnlyList<KoxoExportCandidate>>
            ListExportCandidatesAsync(CancellationToken cancellationToken)
            => Task.FromResult(_candidates);

        public Task InsertRunAsync(
            KoxoRunInsert run,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<KoxoRunSummary?> GetLatestRunAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<KoxoRunSummary?>(null);

        public Task<KoxoRunSummary?> GetLatestRunBySourceAsync(
            string source,
            CancellationToken cancellationToken)
            => Task.FromResult<KoxoRunSummary?>(null);
    }

    /// <summary>
    /// Magasin d'export capable de rendre un secret illisible.
    /// </summary>
    /// <remarks>
    /// Cle tournee, chiffre altere, entree expiree : cote appelant, les trois
    /// se presentent de la meme facon — un <c>null</c>. C'est ce que ce double
    /// reproduit.
    /// </remarks>
    private sealed class ExportPendingPasswordStore : IKoxoPendingPasswordStore
    {
        private readonly KoxoPendingPasswordStore _inner =
            new(NullLogger<KoxoPendingPasswordStore>.Instance);

        public bool ReturnUnreadable { get; set; }

        public bool IsOperational => true;

        public PortalPasswordSecret? Seal(string portalUserId, string password)
            => _inner.Seal(portalUserId, password);

        public Task<bool> PublishAsync(
            string portalUserId,
            string password,
            CancellationToken cancellationToken)
            => _inner.PublishAsync(portalUserId, password, cancellationToken);

        public Task<string?> PeekAsync(
            string portalUserId,
            CancellationToken cancellationToken)
            => ReturnUnreadable
                ? Task.FromResult<string?>(null)
                : _inner.PeekAsync(portalUserId, cancellationToken);

        public Task AcknowledgeAsync(
            string portalUserId,
            CancellationToken cancellationToken)
            => _inner.AcknowledgeAsync(portalUserId, cancellationToken);

        public Task<IReadOnlyList<string>> DrainExpiredAsync(
            CancellationToken cancellationToken)
            => _inner.DrainExpiredAsync(cancellationToken);
    }

    /// <summary>
    /// Vrai si le jeton n'a ete ni consomme ni remplace.
    /// </summary>
    /// <remarks>
    /// A ne pas confondre avec <c>ContainsRawToken</c>, qui verifie l'inverse :
    /// que le jeton en clair n'est nulle part en magasin.
    /// </remarks>
    private static async Task<bool> IsTokenStillUsable(
        Harness harness,
        string? token)
    {
        var target = await harness.PasswordSetups.FindByTokenHashAsync(
            PortalSetupToken.Hash(token!),
            CancellationToken.None);
        return target is not null && target.IsUsable(DateTime.UtcNow);
    }

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

        // Les deux etats sans lien AD, et eux seuls. Le cycle passe par
        // directory_ready AVANT d'ecrire le lien : s'arreter a koxo_pending
        // laissait une interruption a cet instant sortir l'identite du CSV,
        // donc la DESACTIVER, sans aucun retour possible.
        Assert(
            collapsed.Contains(
                "AND lifecycle.status IN ( 'koxo_pending', 'directory_ready')",
                StringComparison.Ordinal),
            "L'exception couvre exactement les deux etats ou le lien AD "
            + "n'existe pas encore.");

        // L'export doit savoir, ligne par ligne, si KoXo va CREER l'objet :
        // c'est ce qui lui permet d'exiger le mot de passe et de refuser
        // plutot que de creer un compte au secret inconnu.
        Assert(
            collapsed.Contains(
                "(ad_link.portal_user_id IS NULL AND customer.is_demo = FALSE)"
                + " AS requires_pending_password",
                StringComparison.Ordinal),
            "La requete expose si la ligne exige un mot de passe en attente.");
        foreach (var forbidden in new[]
        {
            "'awaiting_password'",
            "'failed'",
            "'disabled'",
            "'ready'"
        })
        {
            Assert(
                !collapsed.Contains(forbidden, StringComparison.Ordinal),
                $"L'etat {forbidden} n'acquiert jamais cette exception.");
        }
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
            // Etat exact d'un perdant de course : la place ET son cycle de vie
            // existent. La place tranche — c'est le fait, le cycle n'en est que
            // la consequence. L'ordre inverse rendrait un refus different selon
            // qui gagne la course.
            (valid with
                {
                    IdentityReference = "portal-user-existant",
                    HasExistingLifecycle = true
                },
                BillingV2AdditionalUserRejectionCodes.SlotAlreadyAssigned),
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
    // Forme des requetes composees
    // ==================================================================

    /// <summary>
    /// Les requetes assemblees a partir des fragments partages sont
    /// syntaxiquement soudables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un litteral brut C# ne conserve pas le saut de ligne qui precede son
    /// delimiteur fermant. Concatener un fragment commencant par
    /// <c>FROM</c> juste apres <c>SELECT 1</c> produit donc
    /// <c>SELECT 1FROM</c>, que le serveur lit comme un identifiant et
    /// refuse. Le defaut est invisible partout ailleurs : les suites mock
    /// n'executent aucun SQL, et la relecture humaine voit deux lignes la ou
    /// la chaine n'en a qu'une.
    /// </para>
    /// <para>
    /// La verification passe par les constantes reellement compilees, pas par
    /// une relecture du fichier source : c'est la chaine envoyee au pilote qui
    /// compte.
    /// </para>
    /// </remarks>
    private static void VerifyComposedSlotQueriesAreWellFormed()
    {
        (string Label, string Sql)[] queries =
        [
            (
                "lecture des places",
                ReadSqlConstant(
                    typeof(MariaDbBillingV2AdditionalUserIdentityRepository),
                    "ListAdditionalUserSlotsSql")
            ),
            (
                "projection portail client",
                ReadSqlConstant(
                    typeof(BillingV2PortalSubscriptionProjection),
                    "SelectSql")
            ),
            (
                "projection portail admin",
                ReadSqlConstant(
                    typeof(BillingV2PortalSubscriptionProjection),
                    "AdminSelectSql")
            )
        ];

        foreach (var (label, sql) in queries)
        {
            Assert(
                !sql.Contains("1FROM", StringComparison.Ordinal),
                $"La requete « {label} » soude deux mots-cles : le fragment "
                + "partage doit commencer par un saut de ligne.");
            Assert(
                sql.Contains("SELECT 1\nFROM billing_v2_subscription_items",
                    StringComparison.Ordinal),
                $"La requete « {label} » doit ouvrir son EXISTS sur une "
                + "clause FROM detachee.");
            if (label.StartsWith("projection portail", StringComparison.Ordinal))
            {
                var normalizedSql = sql.Replace("\r\n", "\n", StringComparison.Ordinal);
                Assert(
                    !normalizedSql.Contains("updated_atFROM", StringComparison.Ordinal),
                    $"La requete {label} soude subscription.updated_at et FROM.");
                Assert(
                    normalizedSql.Contains(
                        "subscription.updated_at\nFROM billing_v2_subscriptions subscription",
                        StringComparison.Ordinal),
                    $"La requete {label} doit separer subscription.updated_at "
                    + "du FROM principal par un saut de ligne explicite.");
            }
            // Ce que la lecture montre doit etre ce que l'attribution
            // accepte : ces deux predicats sont la difference entre une place
            // administrable et une place seulement vendue un jour.
            foreach (var predicate in new[]
            {
                "AND slot.is_primary = 0",
                "AND slot.status = 'active'",
                "AND subscription.status = 'active'"
            })
            {
                Assert(
                    sql.Contains(predicate, StringComparison.Ordinal),
                    $"La requete « {label} » doit imposer « {predicate} ».");
            }
        }
    }

    /// <summary>
    /// Lit une constante SQL compilee, quelle que soit sa visibilite.
    /// </summary>
    private static string ReadSqlConstant(Type type, string fieldName)
    {
        var field = type.GetField(
            fieldName,
            System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Static);
        Assert(
            field is not null,
            $"La constante {type.Name}.{fieldName} doit exister : sans elle, "
            + "la requete reellement envoyee n'est plus verifiable.");
        return (string)field!.GetRawConstantValue()!;
    }

    // ==================================================================
    // Lecture produit des places
    // ==================================================================

    /// <summary>
    /// Une place contractuelle vide est annoncee, pas omise.
    /// </summary>
    /// <remarks>
    /// C'est le cas qui justifie l'ecran : sans lui, le client paie des places
    /// qu'aucune interface ne lui montre, et il n'a aucun moyen de savoir
    /// qu'il peut les attribuer.
    /// </remarks>
    private static async Task VerifyListingShowsEveryRealEmptySlot()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        harness.RegisterSlot("slot-2");

        var slots = await harness.Service.ListSlotsAsync(
            CustomerId,
            SubscriptionId,
            CancellationToken.None);

        Assert(
            slots.Count == 2,
            $"Les deux places contractuelles sont listees ({slots.Count}).");
        foreach (var slot in slots)
        {
            Assert(
                string.Equals(
                    slot.Status,
                    BillingV2AdditionalUserSlotStatuses.Available,
                    StringComparison.Ordinal),
                $"Une place vide est annoncee a attribuer ({slot.Status}).");
            Assert(
                slot.CanAssign && !slot.CanResendInvitation,
                "Une place vide propose l'attribution, et elle seule.");
            Assert(
                slot.DisplayName is null && slot.Email is null,
                "Une place vide ne porte le nom de personne.");
        }
    }

    /// <summary>
    /// La place primaire et les places sans droit contractuel restent hors
    /// de la lecture.
    /// </summary>
    /// <remarks>
    /// <c>is_primary = 0</c> ne suffit pas a designer une place utilisateur
    /// supplementaire : une ligne d'abonnement peut exister sans regle
    /// d'attribution active. Annoncer une telle place ferait proposer une
    /// attribution que la transaction refuserait par
    /// <c>SLOT_ENTITLEMENT_MISSING</c>, sans que le client comprenne pourquoi.
    /// </remarks>
    private static async Task VerifyListingHidesWhatIsNotAnAdditionalUserSlot()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        harness.RegisterSlot("slot-primaire", isPrimary: true);
        var withoutEntitlement = harness.RegisterSlot("slot-sans-droit");
        withoutEntitlement.HasActiveUserSlotEntitlement = false;
        harness.RegisterSlot(
            "slot-autre-souscription",
            subscriptionId: OtherSubscriptionId);

        var slots = await harness.Service.ListSlotsAsync(
            CustomerId,
            SubscriptionId,
            CancellationToken.None);

        Assert(
            slots.Count == 1
            && string.Equals(slots[0].Id, "slot-1", StringComparison.Ordinal),
            "Seule la place utilisateur supplementaire reelle de la "
            + $"souscription demandee est listee ({slots.Count}).");
    }

    /// <summary>
    /// Une place non active n'est pas administrable, donc pas listee.
    /// </summary>
    /// <remarks>
    /// La politique d'attribution la refuse en <c>SLOT_NOT_ACTIVE</c>. La
    /// lister l'annoncerait « a attribuer », avec un bouton dont l'appel
    /// echouerait a chaque fois : le client lirait une panne la ou il y a une
    /// place resiliee.
    /// </remarks>
    private static async Task VerifyInactiveSlotIsNotAdministrable()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var cancelled = harness.RegisterSlot("slot-resilie");
        cancelled.Status = "cancelled";

        var slots = await harness.Service.ListSlotsAsync(
            CustomerId,
            SubscriptionId,
            CancellationToken.None);

        Assert(
            slots.Count == 1
            && string.Equals(slots[0].Id, "slot-1", StringComparison.Ordinal),
            $"Seule la place active est administrable ({slots.Count}).");

        var refused = await harness.AssignAsync(
            "slot-resilie",
            "alice@example.invalid");
        Assert(
            !refused.Succeeded,
            "Ce que la lecture cache, la transaction le refuse : les deux "
            + $"disent la meme chose ({refused.Code}).");
    }

    /// <summary>
    /// Un abonnement non actif n'ouvre aucune place administrable.
    /// </summary>
    /// <remarks>
    /// Meme raison : <c>SUBSCRIPTION_NOT_PROVISIONABLE</c> refuserait toute
    /// attribution. Un abonnement resilie ne doit donc pas continuer a
    /// presenter ses places comme attribuables.
    /// </remarks>
    private static async Task VerifyInactiveSubscriptionHasNoAdministrableSlot()
    {
        var harness = Harness.Create();
        var slot = harness.RegisterSlot("slot-1");
        slot.SubscriptionStatus = "cancelled";

        var slots = await harness.Service.ListSlotsAsync(
            CustomerId,
            SubscriptionId,
            CancellationToken.None);

        Assert(
            slots.Count == 0,
            "Un abonnement non actif ne presente aucune place a administrer "
            + $"({slots.Count}).");

        var refused = await harness.AssignAsync("slot-1", "alice@example.invalid");
        Assert(
            !refused.Succeeded,
            $"L'attribution reste refusee, comme avant ({refused.Code}).");
    }

    /// <summary>
    /// La souscription d'une autre organisation n'existe pas.
    /// </summary>
    /// <remarks>
    /// Aucune distinction n'est offerte entre « pas a vous » et « inconnue » :
    /// la difference renseignerait sur l'existence d'une souscription tierce.
    /// </remarks>
    private static async Task VerifyCrossCustomerListingIsEmpty()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");

        var foreign = await harness.Service.ListSlotsAsync(
            OtherCustomerId,
            SubscriptionId,
            CancellationToken.None);
        var unknown = await harness.Service.ListSlotsAsync(
            OtherCustomerId,
            "subscription-inconnue",
            CancellationToken.None);

        Assert(
            foreign.Count == 0,
            "La souscription d'un autre client ne se lit pas.");
        Assert(
            unknown.Count == 0,
            "Une souscription inconnue se lit exactement comme une "
            + "souscription etrangere.");
    }

    /// <summary>
    /// Chaque etat interne a une, et une seule, traduction produit.
    /// </summary>
    /// <remarks>
    /// La traduction est le contrat de l'ecran : un etat interne ajoute plus
    /// tard sans traduction tomberait dans « a finaliser » plutot que d'etre
    /// presente comme un succes, mais un etat existant mal traduit ferait
    /// croire a un acces disponible qui ne l'est pas.
    /// </remarks>
    private static async Task VerifyEveryLifecycleMapsToAProductState()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-0-vide");

        var invited = await AssignedLifecycleId(harness, "slot-1-invite", "a");
        var koxoPending = await AssignedLifecycleId(harness, "slot-2-koxo", "b");
        var directoryReady =
            await AssignedLifecycleId(harness, "slot-3-annuaire", "c");
        var ready = await AssignedLifecycleId(harness, "slot-4-pret", "d");
        var failed = await AssignedLifecycleId(harness, "slot-5-echec", "e");
        var disabled = await AssignedLifecycleId(harness, "slot-6-desactive", "f");

        var now = DateTime.UtcNow;
        await harness.Repository.MarkKoxoPendingAsync(
            koxoPending, now, now, CancellationToken.None);
        await harness.Repository.MarkKoxoPendingAsync(
            directoryReady, now, now, CancellationToken.None);
        await harness.Repository.MarkDirectoryResolvedAsync(
            directoryReady,
            Guid.NewGuid().ToString("D"),
            now,
            CancellationToken.None);
        await harness.Repository.MarkKoxoPendingAsync(
            ready, now, now, CancellationToken.None);
        await harness.Repository.MarkDirectoryResolvedAsync(
            ready,
            Guid.NewGuid().ToString("D"),
            now,
            CancellationToken.None);
        await harness.Repository.MarkReadyAsync(ready, now, CancellationToken.None);
        await harness.Repository.MarkFailedAsync(
            failed,
            "AD_ACCESS_DENIED",
            "detail technique",
            CancellationToken.None);
        await harness.Repository.MarkDisabledAsync(
            disabled, now, CancellationToken.None);

        var byId = (await harness.Service.ListSlotsAsync(
                CustomerId,
                SubscriptionId,
                CancellationToken.None))
            .ToDictionary(slot => slot.Id, StringComparer.Ordinal);

        (string Slot, string Expected)[] expectations =
        [
            ("slot-0-vide", BillingV2AdditionalUserSlotStatuses.Available),
            ("slot-1-invite", BillingV2AdditionalUserSlotStatuses.Invited),
            ("slot-2-koxo", BillingV2AdditionalUserSlotStatuses.Activating),
            ("slot-3-annuaire", BillingV2AdditionalUserSlotStatuses.Activating),
            ("slot-4-pret", BillingV2AdditionalUserSlotStatuses.Active),
            ("slot-5-echec", BillingV2AdditionalUserSlotStatuses.Attention),
            ("slot-6-desactive", BillingV2AdditionalUserSlotStatuses.Disabled)
        ];

        foreach (var (slotId, expected) in expectations)
        {
            Assert(
                byId.TryGetValue(slotId, out var slot)
                && string.Equals(slot.Status, expected, StringComparison.Ordinal),
                $"La place {slotId} est presentee « {expected} », obtenu "
                + $"« {(byId.TryGetValue(slotId, out var actual) ? actual.Status : "absente")} ».");
        }

        // Seule la place invitee peut recevoir un nouveau lien : renvoyer une
        // invitation a une identite deja active reinitialiserait un acces qui
        // fonctionne.
        foreach (var slot in byId.Values)
        {
            var isInvited = string.Equals(
                slot.Status,
                BillingV2AdditionalUserSlotStatuses.Invited,
                StringComparison.Ordinal);
            var isAvailable = string.Equals(
                slot.Status,
                BillingV2AdditionalUserSlotStatuses.Available,
                StringComparison.Ordinal);
            Assert(
                slot.CanResendInvitation == isInvited,
                $"Le renvoi d'invitation ne concerne que l'etat invite ({slot.Status}).");
            Assert(
                slot.CanAssign == isAvailable,
                $"L'attribution ne concerne qu'une place libre ({slot.Status}).");
            Assert(
                isAvailable || slot.Email is not null,
                "Une place occupee montre l'utilisateur qui l'occupe.");
        }
    }

    /// <summary>
    /// Drapeau ferme : l'ecran n'offre plus aucune action.
    /// </summary>
    /// <remarks>
    /// Laisser les boutons visibles derriere un drapeau ferme produirait un
    /// refus systematique, que le client lirait comme une panne alors que
    /// c'est une decision d'exploitation.
    /// </remarks>
    private static async Task VerifyGateOffClosesEveryOfferedAction()
    {
        var enabled = Harness.Create();
        enabled.RegisterSlot("slot-1");
        await enabled.AssignAsync("slot-1", "alice@example.invalid");
        enabled.RegisterSlot("slot-2");

        var disabled = Harness.Create(provisioningEnabled: false);
        disabled.RegisterSlot("slot-1");
        disabled.RegisterSlot("slot-2");

        var open = await enabled.Service.ListSlotsAsync(
            CustomerId,
            SubscriptionId,
            CancellationToken.None);
        var closed = await disabled.Service.ListSlotsAsync(
            CustomerId,
            SubscriptionId,
            CancellationToken.None);

        Assert(
            open.Any(slot => slot.CanAssign)
            && open.Any(slot => slot.CanResendInvitation),
            "Drapeau ouvert : les actions restent proposees.");
        Assert(
            closed.Count == 2,
            $"La lecture reste possible drapeau ferme ({closed.Count}).");
        Assert(
            closed.All(slot => !slot.CanAssign && !slot.CanResendInvitation),
            "Drapeau ferme : plus aucune action n'est proposee.");
    }

    /// <summary>
    /// Un jeton emis pour un autre usage ne fait rien, et reste intact.
    /// </summary>
    /// <remarks>
    /// Le refus doit etre indistinguable d'un jeton inconnu, et surtout ne
    /// rien consommer : consommer le jeton d'un autre parcours le detruirait
    /// pour son parcours legitime, en plus de ne rien accomplir ici.
    /// </remarks>
    private static async Task VerifyForeignPurposeTokenChangesNothing()
    {
        var harness = Harness.Create();
        harness.RegisterSlot("slot-1");
        var assignment = await harness.AssignAsync(
            "slot-1",
            "alice@example.invalid");
        var portalUserId = assignment.PortalUserId!;
        var lifecycleId = harness.LifecycleIdOf(portalUserId);

        // Jeton du parcours d'inscription, emis sur la meme identite : c'est
        // exactement la confusion que le `purpose` doit empecher.
        var foreignToken = PortalSetupToken.Generate();
        await harness.PasswordSetups.IssueAsync(
            new PortalPasswordSetupIssue(
                Guid.NewGuid().ToString("D"),
                portalUserId,
                "signup_pending",
                PortalSetupToken.Hash(foreignToken),
                DateTime.UtcNow.AddHours(24)),
            CancellationToken.None);

        var validation = await harness.Service.ValidateInvitationTokenAsync(
            foreignToken,
            CancellationToken.None);
        var applied = await harness.Service.SetPasswordAsync(
            foreignToken,
            Password,
            CancellationToken.None);

        Assert(
            !validation.Succeeded
            && validation.Code == PortalPasswordSetupCodes.TokenInvalid,
            "Un jeton d'un autre usage est refuse a la validation, et refuse "
            + $"comme un jeton inconnu ({validation.Code}).");
        Assert(
            !applied.Succeeded
            && applied.Code == PortalPasswordSetupCodes.TokenInvalid,
            $"Un jeton d'un autre usage ne pose aucun mot de passe ({applied.Code}).");
        Assert(
            harness.PortalUsers.Entries.Single().PasswordHash is null,
            "Le condensat de mot de passe reste inchange.");
        Assert(
            string.Equals(
                (await harness.Repository.FindBySubscriptionUserIdAsync(
                    "slot-1",
                    CancellationToken.None))!.Status,
                BillingV2UserIdentityStatuses.AwaitingPassword,
                StringComparison.Ordinal),
            "Le cycle de vie n'a pas bouge.");
        Assert(
            await IsTokenStillUsable(harness, foreignToken),
            "Le jeton de l'autre parcours n'est pas consomme : il reste "
            + "utilisable la ou il a un sens.");
        Assert(
            lifecycleId.Length > 0,
            "Le cycle de vie existe toujours.");
    }

    /// <summary>
    /// La projection produit ne transporte aucune donnee technique.
    /// </summary>
    /// <remarks>
    /// Le controle porte sur la forme du type, pas sur une instance : ajouter
    /// un identifiant KoXo, un GUID annuaire ou un code d'echec « pour le
    /// support » les enverrait au navigateur de chaque client.
    /// </remarks>
    private static void VerifySlotSummaryCarriesNoTechnicalData()
    {
        var actual = typeof(BillingV2AdditionalUserSlotSummary)
            .GetProperties()
            .Select(property => property.Name)
            .Where(name => !string.Equals(
                name,
                "EqualityContract",
                StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        string[] expected =
        [
            "CanAssign",
            "CanResendInvitation",
            "DisplayName",
            "Email",
            "Id",
            "Status"
        ];

        Assert(
            actual.SequenceEqual(expected, StringComparer.Ordinal),
            "La projection produit expose exactement l'identifiant de place, "
            + "le nom, l'adresse, l'etat et les deux actions : obtenu "
            + $"« {string.Join(", ", actual)} ».");
    }

    private static async Task<string> AssignedLifecycleId(
        Harness harness,
        string slotId,
        string emailPrefix)
    {
        harness.RegisterSlot(slotId);
        var result = await harness.AssignAsync(
            slotId,
            $"{emailPrefix}@example.invalid");
        Assert(
            result.Succeeded,
            $"L'attribution de {slotId} doit reussir ({result.Code}).");
        return harness.LifecycleIdOf(result.PortalUserId!);
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
        public required ControllablePendingPasswordStore PendingPasswords
        { get; init; }
        public required IPortalPasswordService PasswordService { get; init; }
        public required BillingV2AdditionalUserIdentityService Service
        { get; init; }

        public static Harness Create(
            bool provisioningEnabled = true,
            bool additionalUserProvisioningEnabled = false)
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
            var pendingPasswords = new ControllablePendingPasswordStore();
            // Le depot de jetons joue la transaction : il n'attache le secret
            // qu'au moment ou l'unite de travail aboutit.
            passwordSetups.SealSink = pendingPasswords;
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
                    new BillingV2RuntimeConfiguration(
                        CatalogShadowModeEnabled: false,
                        ProvisioningShadowModeEnabled: false,
                        NewSubscriptionsEnabled: false,
                        AuthoritativeCheckoutEnabled: false,
                        FirstRealSubscriptionApproved: false,
                        ProviderOutboxEnabled: false,
                        ProviderExecutorEnabled: false,
                        ProvisioningEnabled: provisioningEnabled,
                        AdditionalUserProvisioningEnabled:
                            additionalUserProvisioningEnabled),
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
    /// <summary>
    /// Magasin de mots de passe en attente, dont on peut couper la
    /// disponibilite.
    /// </summary>
    /// <remarks>
    /// Reproduit le seul cas ou la version persistante refuse de retenir un
    /// secret : cle de chiffrement absente ou inutilisable. Sans ce levier, le
    /// chemin fail-closed le plus important du lot ne serait jamais parcouru.
    /// </remarks>
    internal sealed class ControllablePendingPasswordStore
        : IKoxoPendingPasswordStore, IKoxoPendingPasswordSealSink
    {
        private readonly KoxoPendingPasswordStore _inner =
            new(NullLogger<KoxoPendingPasswordStore>.Instance);

        public bool Operational { get; set; } = true;

        /// <summary>
        /// Fait echouer l'attache du scelle, comme une insertion refusee par
        /// la base au milieu de la transaction.
        /// </summary>
        public bool FailAttach { get; set; }

        public bool IsOperational => Operational;

        public PortalPasswordSecret? Seal(string portalUserId, string password)
            => Operational ? _inner.Seal(portalUserId, password) : null;

        public void AttachSealed(
            string portalUserId,
            PortalPasswordSecret secret)
        {
            if (FailAttach)
            {
                throw new InvalidOperationException(
                    "Echec simule de l'ecriture du relais.");
            }

            _inner.AttachSealed(portalUserId, secret);
        }

        public Task<bool> PublishAsync(
            string portalUserId,
            string password,
            CancellationToken cancellationToken)
            => Operational
                ? _inner.PublishAsync(portalUserId, password, cancellationToken)
                : Task.FromResult(false);

        public Task<string?> PeekAsync(
            string portalUserId,
            CancellationToken cancellationToken)
            => _inner.PeekAsync(portalUserId, cancellationToken);

        public Task AcknowledgeAsync(
            string portalUserId,
            CancellationToken cancellationToken)
            => _inner.AcknowledgeAsync(portalUserId, cancellationToken);

        public Task<IReadOnlyList<string>> DrainExpiredAsync(
            CancellationToken cancellationToken)
            => _inner.DrainExpiredAsync(cancellationToken);
    }

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
