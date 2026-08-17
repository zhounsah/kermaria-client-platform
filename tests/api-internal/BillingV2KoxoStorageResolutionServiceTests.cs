using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Verrouille l'alimentation reelle des cibles de stockage KoXo : lectures
/// ciblees, instantanes, puis resolver pur.
/// </summary>
/// <remarks>
/// <para>
/// Les doublures utilisees ici n'exposent que des lectures. Toute tentative
/// d'ecriture — creation d'identite, upsert de lien, appel KoXo — leve, donc un
/// test qui passerait en ecrivant echouerait bruyamment. C'est la garantie
/// principale de ce lot : la resolution observe, elle n'agit pas.
/// </para>
/// <para>
/// Ces tests portent sur l'orchestration. Les invariants du resolver lui-meme
/// restent verrouilles par <see cref="BillingV2KoxoStorageTargetTests"/>.
/// </para>
/// </remarks>
public static class BillingV2KoxoStorageResolutionServiceTests
{
    private const string CustomerId = "22222222-2222-2222-2222-222222222222";
    private const string OtherCustomerId = "33333333-3333-3333-3333-333333333333";
    private const string PortalUserId = "44444444-4444-4444-4444-444444444444";
    private const string OtherPortalUserId = "99999999-9999-9999-9999-999999999999";
    private const string EmployeeNumber = "CLI-000123";
    private const string ObjectGuid = "55555555-5555-5555-5555-555555555555";
    private const string ObjectSid = "S-1-5-21-1-2-3-1104";
    private const string SamAccountName = "zachary.hounsahou";
    private const string CustomerReference = "CLI-000042";

    public static async Task RunAsync()
    {
        await VerifyPersonalStorageHappyPathAsync();
        await VerifySharedStorageHappyPathAsync();
        await VerifyMixedPersonalAndSharedResolveTogetherAsync();
        await VerifyRepeatedIdentityIsReadOnlyOnceAsync();
        await VerifyUnknownPortalUserIsRefusedAsync();
        await VerifyPortalUserOfAnotherCustomerIsRefusedAsync();
        await VerifyRepositoryLeakingAnotherCustomerIsRefusedAsync();
        await VerifyDivergentCustomerReferenceIsRefusedAsync();
        await VerifyMissingEmployeeNumberIsRefusedAsync();
        await VerifyDuplicateLinksAreRefusedAsync();
        await VerifyLinkOfAnotherPortalUserIsRefusedAsync();
        await VerifyEmployeeNumberWithoutDirectoryMatchIsRefusedAsync();
        await VerifyDirectoryMismatchesAreRefusedAsync();
        await VerifyMissingCustomerIsRefusedAsync();
        await VerifyOneAnomalyFailsTheWholeBatchAsync();
        await VerifyEmptyPlanNeedsNoReadAsync();

        Console.WriteLine(
            "Tests alimentation des cibles de stockage KoXo Billing V2 reussis.");
    }

    // ------------------------------------------------------------------
    // Chemins nominaux.
    // ------------------------------------------------------------------
    private static async Task VerifyPersonalStorageHappyPathAsync()
    {
        var world = new FakeWorld();
        var resolution = await ResolveAsync(world, [UserQuota(64)]);

        Ensure(
            resolution.Resolved
            && resolution.Targets.Count == 1
            && resolution.Targets[0].Kind
                == BillingV2KoxoStorageTargetKind.User
            && resolution.Targets[0].QuotaMebibytes == 65536
            && resolution.Targets[0].EmployeeNumber == EmployeeNumber
            // Le sAMAccountName vient de l'annuaire, il n'est jamais predit.
            && resolution.Targets[0].AdLink?.SamAccountName == SamAccountName,
            "Un stockage personnel alimente par des lectures reelles doit resoudre sa fiche KoXo.");

        // L'emplacement de la fiche vient de l'etat du client LU en base, pas
        // d'une valeur composee par cette couche d'alimentation.
        Ensure(
            resolution.Targets[0].PrimaryGroup
                == KoxoDirectoryTopology.PrimaryGroupClients
            && resolution.Targets[0].SecondaryGroup == CustomerReference
            && resolution.Targets[0].UserId == SamAccountName,
            "L'emplacement KoXo de la fiche doit decouler des lectures, pas d'une convention locale.");
    }

    private static async Task VerifySharedStorageHappyPathAsync()
    {
        var world = new FakeWorld();
        var resolution = await ResolveAsync(world, [SharedQuota(128)]);

        Ensure(
            resolution.Resolved
            && resolution.Targets.Count == 1
            && resolution.Targets[0].Kind
                == BillingV2KoxoStorageTargetKind.SecondaryGroup
            && resolution.Targets[0].SecondaryGroup == CustomerReference
            && resolution.Targets[0].QuotaMebibytes == 131072,
            "Un stockage partage doit resoudre l'OU du client lue en base.");

        // Une cible partagee ne doit provoquer aucune lecture d'identite.
        Ensure(
            world.PortalUserReads.Count == 0
            && world.LinkReads.Count == 0
            && world.DirectoryReads.Count == 0
            && world.CustomerReads.Count == 1,
            "Un stockage partage ne doit interroger ni les utilisateurs, ni l'annuaire.");
    }

    private static async Task VerifyMixedPersonalAndSharedResolveTogetherAsync()
    {
        var world = new FakeWorld();
        var resolution = await ResolveAsync(
            world,
            [UserQuota(64), SharedQuota(128)]);

        Ensure(
            resolution.Resolved
            && resolution.Targets.Count == 2
            && resolution.Targets.Count(target => target.Kind
                == BillingV2KoxoStorageTargetKind.User) == 1
            && resolution.Targets.Count(target => target.Kind
                == BillingV2KoxoStorageTargetKind.SecondaryGroup) == 1,
            "Un abonnement melangeant personnel et partage doit resoudre les deux cibles.");
    }

    /// <summary>
    /// Deux plans du meme utilisateur : une seule lecture de chaque source.
    /// </summary>
    /// <remarks>
    /// La deduplication n'est pas qu'une economie : une recherche LDAP par
    /// <c>employeeNumber</c> balaye les racines autorisees, et la repeter par
    /// ligne de facturation ferait grimper le cout au rythme du catalogue.
    /// </remarks>
    private static async Task VerifyRepeatedIdentityIsReadOnlyOnceAsync()
    {
        var world = new FakeWorld();
        var resolution = await ResolveAsync(
            world,
            [
                UserQuota(64) with { SubscriptionItemId = "item-1" },
                UserQuota(64) with { SubscriptionItemId = "item-2" }
            ]);

        Ensure(
            world.PortalUserReads.Count == 1
            && world.LinkReads.Count == 1
            && world.DirectoryReads.Count == 1,
            "Une meme identite ne doit produire qu'une lecture base et une lecture annuaire.");

        // Le refus vient ensuite du resolver : deux quotas visent la meme
        // fiche, donc ils ne se departagent pas.
        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.IdentityAmbiguous,
            "Deux quotas sur la meme fiche doivent rester un conflit.");
    }

    // ------------------------------------------------------------------
    // Isolation client.
    // ------------------------------------------------------------------
    private static async Task VerifyUnknownPortalUserIsRefusedAsync()
    {
        var world = new FakeWorld { PortalUser = null };
        var resolution = await ResolveAsync(world, [UserQuota(64)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.PortalUserNotFound
            && resolution.Targets.Count == 0,
            "Un utilisateur portail inexistant doit refuser sans rien creer.");

        // Et surtout : rien n'a ete demande a l'annuaire pour un fantome.
        Ensure(
            world.DirectoryReads.Count == 0,
            "Une identite absente ne doit declencher aucune recherche annuaire.");
    }

    /// <summary>
    /// Utilisateur reel, mais rattache a un autre client.
    /// </summary>
    /// <remarks>
    /// La lecture etant bornee par le couple exact, elle ne rend rien : le
    /// service ne peut donc pas distinguer ce cas d'une absence, et ne doit pas
    /// le faire. Confirmer qu'une ligne existe chez le voisin serait deja une
    /// fuite.
    /// </remarks>
    private static async Task VerifyPortalUserOfAnotherCustomerIsRefusedAsync()
    {
        var world = new FakeWorld
        {
            PortalUser = PortalUser() with { CustomerId = OtherCustomerId }
        };
        var resolution = await ResolveAsync(world, [UserQuota(64)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.PortalUserNotFound
            && resolution.Targets.Count == 0,
            "Un utilisateur d'un autre client ne doit jamais etre atteint par ce quota.");
    }

    /// <summary>
    /// Depot fautif qui relache le bornage par client.
    /// </summary>
    private static async Task VerifyRepositoryLeakingAnotherCustomerIsRefusedAsync()
    {
        var world = new FakeWorld
        {
            PortalUser = PortalUser() with { CustomerId = OtherCustomerId },
            EnforceCustomerScope = false
        };
        var resolution = await ResolveAsync(world, [UserQuota(64)]);

        // La requete reelle est bornee, mais le service ne s'y fie pas : si une
        // implementation relachait ce bornage, le controle explicite l'arrete.
        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.PortalUserCustomerMismatch
            && resolution.Targets.Count == 0,
            "Un depot qui rendrait l'utilisateur d'un autre client doit etre arrete par le service.");
    }

    private static async Task VerifyDivergentCustomerReferenceIsRefusedAsync()
    {
        var world = new FakeWorld
        {
            Links = [Link() with { CustomerReference = "CLI-000099" }]
        };
        var resolution = await ResolveAsync(world, [UserQuota(64)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.CustomerReferenceMismatch
            && resolution.Targets.Count == 0,
            "Un lien portant la reference d'un autre client doit refuser.");

        // Une reference absente cote annuaire n'est pas une contradiction :
        // la recherche LDAP par employeeNumber ne la renseigne pas.
        var withoutDirectoryReference = new FakeWorld
        {
            DirectoryObject = DirectoryObject() with
            {
                CustomerReference = string.Empty
            }
        };
        var tolerated = await ResolveAsync(
            withoutDirectoryReference,
            [UserQuota(64)]);
        Ensure(
            tolerated.Resolved,
            "Une reference client non renseignee ne doit pas etre traitee comme une contradiction.");

        // Renseignee mais differente, en revanche, elle contredit.
        var contradicting = new FakeWorld
        {
            DirectoryObject = DirectoryObject() with
            {
                CustomerReference = "CLI-000099"
            }
        };
        var refused = await ResolveAsync(contradicting, [UserQuota(64)]);
        Ensure(
            !refused.Resolved
            && refused.ReasonCode
                == BillingV2KoxoStorageTargetReasons.CustomerReferenceMismatch,
            "Une reference annuaire renseignee et divergente doit refuser.");
    }

    // ------------------------------------------------------------------
    // Chaine d'identite.
    // ------------------------------------------------------------------
    private static async Task VerifyMissingEmployeeNumberIsRefusedAsync()
    {
        foreach (var identifier in new string?[] { null, "", "CLI-42" })
        {
            var world = new FakeWorld
            {
                PortalUser = PortalUser() with
                {
                    KoxoUniqueIdentifier = identifier
                }
            };
            var resolution = await ResolveAsync(world, [UserQuota(64)]);

            Ensure(
                !resolution.Resolved
                && resolution.ReasonCode
                    == BillingV2KoxoStorageTargetReasons.EmployeeNumberInvalid,
                "Sans identifiant unique valide, il n'y a rien a chercher et rien a inventer.");
            Ensure(
                world.DirectoryReads.Count == 0,
                "Aucune recherche annuaire ne doit partir sans employeeNumber valide.");
        }
    }

    private static async Task VerifyDuplicateLinksAreRefusedAsync()
    {
        var world = new FakeWorld
        {
            Links =
            [
                Link(),
                Link() with
                {
                    Id = "link-2",
                    ObjectGuid = "66666666-6666-6666-6666-666666666666"
                }
            ]
        };
        var resolution = await ResolveAsync(world, [UserQuota(64)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.IdentityAmbiguous,
            "Deux liens annuaire doivent rester visibles et bloquants.");
    }

    private static async Task VerifyLinkOfAnotherPortalUserIsRefusedAsync()
    {
        var world = new FakeWorld
        {
            Links = [Link() with { PortalUserId = OtherPortalUserId }]
        };
        var resolution = await ResolveAsync(world, [UserQuota(64)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.IdentityPortalUserMismatch,
            "Un lien rattache a un autre utilisateur portail ne doit pas porter le quota.");
    }

    private static async Task VerifyEmployeeNumberWithoutDirectoryMatchIsRefusedAsync()
    {
        // null couvre aussi la correspondance multiple : la recherche par
        // employeeNumber traite le multiple comme une absence.
        var world = new FakeWorld { DirectoryObject = null };
        var resolution = await ResolveAsync(world, [UserQuota(64)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.DirectoryObjectNotFound,
            "Un employeeNumber sans resultat unique doit refuser.");
        Ensure(
            world.DirectoryReads.Count == 1
            && world.DirectoryReads[0] == EmployeeNumber,
            "La recherche annuaire doit porter exactement sur l'employeeNumber lu en base.");
    }

    private static async Task VerifyDirectoryMismatchesAreRefusedAsync()
    {
        var divergences = new[]
        {
            DirectoryObject() with
            {
                ObjectGuid = "77777777-7777-7777-7777-777777777777"
            },
            DirectoryObject() with { ObjectSid = "S-1-5-21-1-2-3-9999" },
            DirectoryObject() with { SamAccountName = "un.autre.compte" },
        };

        foreach (var divergent in divergences)
        {
            var world = new FakeWorld { DirectoryObject = divergent };
            var resolution = await ResolveAsync(world, [UserQuota(64)]);

            Ensure(
                !resolution.Resolved
                && resolution.ReasonCode
                    == BillingV2KoxoStorageTargetReasons.DirectoryObjectMismatch,
                "Toute divergence GUID/SID/sAMAccountName doit refuser la cible.");
        }
    }

    private static async Task VerifyMissingCustomerIsRefusedAsync()
    {
        var world = new FakeWorld { Customer = null };
        var resolution = await ResolveAsync(world, [SharedQuota(128)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.CustomerNotFound
            && resolution.Targets.Count == 0,
            "Sans client lisible, aucune OU partagee ne doit etre devinee.");
    }

    // ------------------------------------------------------------------
    // Globalite du refus.
    // ------------------------------------------------------------------
    private static async Task VerifyOneAnomalyFailsTheWholeBatchAsync()
    {
        // Le stockage partage est parfaitement resolvable ; l'identite, non.
        var world = new FakeWorld { DirectoryObject = null };
        var resolution = await ResolveAsync(
            world,
            [SharedQuota(128), UserQuota(64)]);

        Ensure(
            !resolution.Resolved
            && resolution.Targets.Count == 0,
            "Une seule anomalie doit refuser tout le lot, sans rendre le sous-ensemble compris.");
    }

    private static async Task VerifyEmptyPlanNeedsNoReadAsync()
    {
        var world = new FakeWorld();
        var resolution = await ResolveAsync(world, []);

        Ensure(
            resolution.Resolved
            && resolution.Targets.Count == 0
            && world.PortalUserReads.Count == 0
            && world.CustomerReads.Count == 0
            && world.LinkReads.Count == 0
            && world.DirectoryReads.Count == 0,
            "Un abonnement sans quota ne doit provoquer aucune lecture.");
    }

    // ------------------------------------------------------------------
    // Doublures strictement en lecture.
    // ------------------------------------------------------------------

    private static Task<BillingV2KoxoStorageTargetResolution> ResolveAsync(
        FakeWorld world,
        IReadOnlyList<BillingV2StorageQuotaPlan> quotas)
        => new BillingV2KoxoStorageTargetResolutionService(
                new FakeTargetingRepository(world),
                new FakeLinkRepository(world),
                new FakeDirectory(world))
            .ResolveAsync(CustomerId, quotas, CancellationToken.None);

    /// <summary>
    /// Etat lu par les doublures, et journal des lectures effectuees.
    /// </summary>
    private sealed class FakeWorld
    {
        public BillingV2KoxoPortalUserRecord? PortalUser { get; init; }
            = BillingV2KoxoStorageResolutionServiceTests.PortalUser();

        public BillingV2KoxoCustomerRecord? Customer { get; init; }
            = new(
                CustomerId,
                CustomerReference,
                IsDemo: false,
                KoxoGroupReference: null);

        public IReadOnlyList<PortalUserAdLinkRecord> Links { get; init; }
            = [Link()];

        public AdDirectoryObjectSummary? DirectoryObject { get; init; }
            = BillingV2KoxoStorageResolutionServiceTests.DirectoryObject();

        /// <summary>
        /// Faux pour simuler un depot qui aurait perdu son bornage par client.
        /// </summary>
        public bool EnforceCustomerScope { get; init; } = true;

        public List<string> PortalUserReads { get; } = [];
        public List<string> CustomerReads { get; } = [];
        public List<string> LinkReads { get; } = [];
        public List<string> DirectoryReads { get; } = [];
    }

    private sealed class FakeTargetingRepository
        : IBillingV2KoxoTargetingRepository
    {
        private readonly FakeWorld _world;

        public FakeTargetingRepository(FakeWorld world) => _world = world;

        public bool IsPersistent => true;

        public Task<BillingV2KoxoPortalUserRecord?> FindPortalUserAsync(
            string customerId,
            string portalUserId,
            CancellationToken cancellationToken)
        {
            _world.PortalUserReads.Add($"{customerId}|{portalUserId}");
            var record = _world.PortalUser;
            if (record is null)
            {
                return Task.FromResult<BillingV2KoxoPortalUserRecord?>(null);
            }

            // Reproduit le bornage de la requete reelle : les deux
            // identifiants sont exacts, donc rien ne remonte pour un autre
            // client. Le monde de test peut relacher ce bornage pour verifier
            // le garde-fou du service.
            var matches = string.Equals(
                    record.PortalUserId,
                    portalUserId,
                    StringComparison.Ordinal)
                && (!_world.EnforceCustomerScope
                    || string.Equals(
                        record.CustomerId,
                        customerId,
                        StringComparison.Ordinal));
            return Task.FromResult(matches ? record : null);
        }

        public Task<BillingV2KoxoCustomerRecord?> FindCustomerAsync(
            string customerId,
            CancellationToken cancellationToken)
        {
            _world.CustomerReads.Add(customerId);
            var record = _world.Customer;
            return Task.FromResult(
                record is not null
                && string.Equals(
                    record.CustomerId,
                    customerId,
                    StringComparison.Ordinal)
                    ? record
                    : null);
        }
    }

    /// <summary>
    /// Depot de liens en lecture seule : toute ecriture leve.
    /// </summary>
    private sealed class FakeLinkRepository : IActiveDirectoryLinkRepository
    {
        private readonly FakeWorld _world;

        public FakeLinkRepository(FakeWorld world) => _world = world;

        public bool IsPersistent => true;

        public Task<IReadOnlyList<PortalUserAdLinkRecord>>
            GetUserLinksByPortalUserIdAsync(
                string portalUserId,
                CancellationToken cancellationToken)
        {
            _world.LinkReads.Add(portalUserId);
            return Task.FromResult(_world.Links);
        }

        public Task<AdCustomerContext?> GetCustomerContextAsync(
            string customerReference,
            CancellationToken cancellationToken)
            => throw Forbidden(nameof(GetCustomerContextAsync));

        public Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerLinksAsync(
            string customerReference,
            CancellationToken cancellationToken)
            => throw Forbidden(nameof(GetCustomerLinksAsync));

        public Task<IReadOnlyList<CustomerAdLinkSummary>>
            GetCustomerUserLinksAsync(
                string customerReference,
                CancellationToken cancellationToken)
            => throw Forbidden(nameof(GetCustomerUserLinksAsync));

        public Task<CustomerAdLinkUpsertResult> UpsertCustomerLinkAsync(
            string customerReference,
            string? actorUserId,
            AdDirectoryObjectSummary directoryObject,
            CancellationToken cancellationToken)
            => throw Forbidden(nameof(UpsertCustomerLinkAsync));

        public Task<CustomerAdLinkUpsertResult> UpsertPortalUserLinkAsync(
            string customerReference,
            string portalUserId,
            string? actorUserId,
            AdDirectoryObjectSummary directoryObject,
            string? adDomain,
            string? adProvisioningStatus,
            DateTime? adProvisionedAtUtc,
            string? lastPasswordSyncStatus,
            DateTime? lastPasswordSyncAtUtc,
            string? koxoExportStatus,
            CancellationToken cancellationToken)
            => throw Forbidden(nameof(UpsertPortalUserLinkAsync));

        public Task<bool> UpdateUserPasswordSyncStatusAsync(
            string portalUserId,
            string status,
            DateTime changedAtUtc,
            CancellationToken cancellationToken)
            => throw Forbidden(nameof(UpdateUserPasswordSyncStatusAsync));

        public Task<bool> DeleteCustomerLinkAsync(
            string customerReference,
            string linkId,
            CancellationToken cancellationToken)
            => throw Forbidden(nameof(DeleteCustomerLinkAsync));

        public Task<bool> RefreshCustomerLinkAsync(
            string targetCustomerReference,
            AdDirectoryObjectSummary directoryObject,
            CancellationToken cancellationToken)
            => throw Forbidden(nameof(RefreshCustomerLinkAsync));

        public Task<CustomerAdLinkSummary?> FindUserLinkByEmailAsync(
            string customerReference,
            string email,
            CancellationToken cancellationToken)
            => throw Forbidden(nameof(FindUserLinkByEmailAsync));

        public Task<PortalUserAdLinkRecord?> FindUserLinkByPortalUserIdAsync(
            string portalUserId,
            CancellationToken cancellationToken)
            // Masquerait un doublon : la resolution doit passer par
            // GetUserLinksByPortalUserIdAsync.
            => throw Forbidden(nameof(FindUserLinkByPortalUserIdAsync));
    }

    /// <summary>
    /// Annuaire en lecture seule : seule la recherche par employeeNumber est
    /// autorisee, toute mutation de groupe leve.
    /// </summary>
    private sealed class FakeDirectory : IAdGroupProvisioner
    {
        private readonly FakeWorld _world;

        public FakeDirectory(FakeWorld world) => _world = world;

        public string ModeName => "fake-read-only";

        public bool RequiresConfiguredGroupDistinguishedNames => false;

        public Task<AdDirectoryObjectSummary?> ResolveUserByEmployeeNumberAsync(
            string employeeNumber,
            CancellationToken cancellationToken)
        {
            _world.DirectoryReads.Add(employeeNumber);
            return Task.FromResult(_world.DirectoryObject);
        }

        public Task<AdGroupProvisionerResult> AddUserToGroupAsync(
            CustomerAdLinkSummary user,
            string groupSamAccountName,
            string? groupDistinguishedName,
            CancellationToken cancellationToken)
            => throw Forbidden(nameof(AddUserToGroupAsync));

        public Task<AdGroupProvisionerResult> RemoveUserFromGroupAsync(
            CustomerAdLinkSummary user,
            string groupSamAccountName,
            string? groupDistinguishedName,
            CancellationToken cancellationToken)
            => throw Forbidden(nameof(RemoveUserFromGroupAsync));
    }

    private static InvalidOperationException Forbidden(string member)
        => new(
            $"La resolution de cibles KoXo ne doit appeler que des lectures : {member} est interdit.");

    // ------------------------------------------------------------------
    // Fabriques.
    // ------------------------------------------------------------------

    private static BillingV2KoxoPortalUserRecord PortalUser()
        => new(
            PortalUserId,
            CustomerId,
            CustomerReference,
            EmployeeNumber,
            IsDemo: false,
            KoxoGroupReference: null);

    private static BillingV2StorageQuotaPlan UserQuota(long gibibytes)
        => new(
            SubscriptionItemId: "item-stockage-a",
            SubscriptionUserId: "user-a",
            TargetType: "koxo_user_storage",
            IdentityReference: PortalUserId,
            gibibytes,
            Unit: "GiB",
            ScopeType: "user");

    private static BillingV2StorageQuotaPlan SharedQuota(long gibibytes)
        => new(
            SubscriptionItemId: "item-stockage-partage",
            SubscriptionUserId: null,
            TargetType: "koxo_secondary_group_storage",
            IdentityReference: null,
            gibibytes,
            Unit: "GiB",
            ScopeType: "subscription");

    private static PortalUserAdLinkRecord Link()
        => new(
            Id: "link-1",
            CustomerId,
            CustomerReference,
            PortalUserId,
            ObjectGuid,
            ObjectSid,
            SamAccountName,
            UserPrincipalName: null,
            DisplayName: "LAUMAILLE Zachary",
            DistinguishedName:
                "CN=Zachary,OU=CLI-000042,OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
            AdDomain: "clients.home.bzh",
            AdProvisioningStatus: "provisioned",
            AdProvisionedAtUtc: null,
            LastPasswordSyncAtUtc: null,
            LastPasswordSyncStatus: null,
            KoxoExportStatus: "exported");

    private static AdDirectoryObjectSummary DirectoryObject()
        => new(
            ObjectGuid,
            ObjectSid,
            ObjectType: "user",
            SamAccountName,
            UserPrincipalName: null,
            DisplayName: "LAUMAILLE Zachary",
            DistinguishedName:
                "CN=Zachary,OU=CLI-000042,OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
            // Laissee vide comme le fait la recherche LDAP par employeeNumber.
            CustomerReference: "",
            IsDisabled: false);

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
