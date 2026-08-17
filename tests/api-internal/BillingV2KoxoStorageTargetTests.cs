using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Verrouille la traduction d'un plan de quota Billing V2 vers la cible KoXo
/// reelle, avant toute execution.
/// </summary>
/// <remarks>
/// <para>
/// Rien n'est applique dans cette version : ces tests portent sur une resolution
/// pure, sans annuaire, sans base, sans SRV-21. Ce qui est verrouille ici est
/// exactement ce qui rendra l'execution sure plus tard : viser la bonne OU,
/// viser la bonne fiche utilisateur, convertir l'unite sans se tromper de
/// facteur, et refuser plutot que deviner.
/// </para>
/// <para>
/// La topologie n'est pas recopiee dans ces tests : ils appellent
/// <see cref="KoxoDirectoryTopology"/>, ce qui garantit que la cible du quota et
/// la cible de l'export KoXo restent le meme objet d'annuaire.
/// </para>
/// </remarks>
public static class BillingV2KoxoStorageTargetTests
{
    private const string CustomerId = "22222222-2222-2222-2222-222222222222";
    private const string OtherCustomerId = "33333333-3333-3333-3333-333333333333";
    private const string IdentityReference = "44444444-4444-4444-4444-444444444444";
    private const string OtherIdentityReference =
        "99999999-9999-9999-9999-999999999999";
    private const string EmployeeNumber = "CLI-000123";
    private const string ObjectGuid = "55555555-5555-5555-5555-555555555555";
    private const string ObjectSid = "S-1-5-21-1-2-3-1104";
    private const string SamAccountName = "zachary.hounsahou";

    public static void Run()
    {
        VerifyTopologyStaysSharedWithTheKoxoExport();
        VerifyUserQuotaIsConvertedToMebibytes();
        VerifyQuotaOverflowIsRefusedInsteadOfWrapping();
        VerifyNonPositiveQuotaIsRefused();
        VerifyCatalogUnitIsTheOnlyAcceptedUnit();
        VerifyUnknownTargetTypeIsRefused();
        VerifyUnmaterializedUserBlocksInsteadOfBeingCreated();
        VerifyMissingEmployeeNumberBlocks();
        VerifyDuplicateAdLinkBlocksInsteadOfChoosing();
        VerifyLinkOfAnotherCustomerIsRefused();
        VerifySnapshotFiledUnderTheWrongKeyIsRefused();
        VerifyLinkOfAnotherPortalUserIsRefused();
        VerifyDirectoryLookupFailureBlocks();
        VerifyDirectoryMismatchBlocksOnEachAttribute();
        VerifyNoNameBasedFallbackExists();
        VerifySecondaryGroupTargetFollowsTheExportTopology();
        VerifyDemoSecondaryGroupKeepsItsOwnOu();
        VerifySecondaryGroupWithoutCustomerContextIsRefused();
        VerifySecondaryGroupOfAnotherCustomerIsRefused();
        VerifySameOuNameUnderTwoPrimaryGroupsIsNotACollision();
        VerifyScopeIsCheckedOnBothSides();
        VerifyTwoQuotasOnTheSameObjectAreRefused();
        VerifyOneBadPlanRefusesTheWholeResolution();
        VerifyQuotaDecreaseIsClassifiedAsNonApplicable();

        Console.WriteLine(
            "Tests cibles de stockage KoXo Billing V2 reussis.");
    }

    // ------------------------------------------------------------------
    // La topologie est partagee, pas redupliquee.
    // ------------------------------------------------------------------
    private static void VerifyTopologyStaysSharedWithTheKoxoExport()
    {
        // Un client payant ordinaire : l'OU porte sa reference.
        Ensure(
            KoxoDirectoryTopology.ResolveSecondaryGroup(
                isDemo: false,
                koxoGroupReference: null,
                customerReference: "CLI-000042") == "CLI-000042"
            && KoxoDirectoryTopology.ResolvePrimaryGroup(isDemo: false)
                == "CLIENTS",
            "Un client payant ordinaire doit viser l'OU nommee d'apres sa reference, sous CLIENTS.");

        // Un essai : OU prefixee, sous le groupe primaire de demonstration. Le
        // prefixe n'est pas cosmetique — un meme nom des deux cotes fait perdre
        // son groupe a l'identite migree.
        Ensure(
            KoxoDirectoryTopology.ResolveSecondaryGroup(
                isDemo: true,
                koxoGroupReference: "CLI-000042",
                customerReference: "CLI-000042") == "DEMO-CLI-000042"
            && KoxoDirectoryTopology.ResolveSecondaryGroup(
                isDemo: true,
                koxoGroupReference: null,
                customerReference: "CLI-000042") == "DEMO-CLI-DEMO",
            "Un essai doit viser une OU prefixee, avec repli sur l'OU commune historique.");

        // Le groupe primaire de demonstration doit rester identique AU BIT PRES
        // a la graphie saisie dans l'IHM KoXo. L'attendu est construit par code
        // de caractere : un fichier de test relu dans un autre encodage ne doit
        // pas pouvoir rendre cette comparaison vraie par accident.
        Ensure(
            KoxoDirectoryTopology.PrimaryGroupDemo
                == "CLIENTS D" + (char)0x00C9 + "MO",
            "La graphie du groupe primaire de demonstration ne doit pas deriver.");

        // Et l'export KoXo doit continuer a designer exactement les memes noms.
        Ensure(
            KoxoExportService.PrimaryGroupClients
                == KoxoDirectoryTopology.PrimaryGroupClients
            && KoxoExportService.PrimaryGroupDemo
                == KoxoDirectoryTopology.PrimaryGroupDemo
            && KoxoExportService.DemoGroupPrefix
                == KoxoDirectoryTopology.DemoGroupPrefix
            && KoxoExportService.DemoGroupReference
                == KoxoDirectoryTopology.DemoGroupReference,
            "L'export KoXo et le provisioning Billing V2 doivent partager une seule topologie.");
    }

    // ------------------------------------------------------------------
    // Conversion d'unite.
    // ------------------------------------------------------------------
    private static void VerifyUserQuotaIsConvertedToMebibytes()
    {
        var resolution = ResolveUser(UserQuota(64));

        // Le plan reste en GiB, la cible KoXo est en MiB : c'est ici, et nulle
        // part ailleurs, que le facteur 1024 est applique.
        Ensure(
            resolution.Resolved
            && resolution.Targets.Count == 1
            && resolution.Targets[0].Kind
                == BillingV2KoxoStorageTargetKind.User
            && resolution.Targets[0].QuotaMebibytes == 65536
            && resolution.Targets[0].EmployeeNumber == EmployeeNumber
            && resolution.Targets[0].AdLink?.SamAccountName == SamAccountName,
            "Un quota de 64 GiB doit devenir 65536 MiB sur la fiche utilisateur resolue.");
    }

    private static void VerifyQuotaOverflowIsRefusedInsteadOfWrapping()
    {
        // Sans checked, la multiplication reboucle et un quota gigantesque
        // deviendrait un quota minuscule, donc un blocage utilisateur immediat.
        var resolution = ResolveUser(UserQuota(long.MaxValue / 512));

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.QuotaOverflow
            && resolution.Targets.Count == 0,
            "Un depassement de conversion doit refuser, jamais reboucler silencieusement.");
    }

    private static void VerifyNonPositiveQuotaIsRefused()
    {
        foreach (var value in new long[] { 0, -1 })
        {
            var resolution = ResolveUser(UserQuota(value));
            Ensure(
                !resolution.Resolved
                && resolution.ReasonCode
                    == BillingV2KoxoStorageTargetReasons.QuotaInvalid,
                "Un quota nul ou negatif n'a aucune traduction KoXo acceptable.");
        }
    }

    private static void VerifyCatalogUnitIsTheOnlyAcceptedUnit()
    {
        var resolution = ResolveUser(UserQuota(64) with { Unit = "MiB" });

        // Accepter une autre unite ferait appliquer 64 MiB comme 65536 MiB.
        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.UnitUnexpected,
            "Une unite differente de celle du catalogue doit refuser la conversion.");
    }

    private static void VerifyUnknownTargetTypeIsRefused()
    {
        var resolution = ResolveUser(
            UserQuota(64) with { TargetType = "koxo_stockage_inconnu" });

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.TargetTypeUnknown,
            "Un type de cible inconnu ne doit recevoir aucune interpretation par defaut.");
    }

    // ------------------------------------------------------------------
    // Identite : materialisee, unique, verifiee.
    // ------------------------------------------------------------------
    private static void VerifyUnmaterializedUserBlocksInsteadOfBeingCreated()
    {
        var resolution = BillingV2KoxoStorageTargetResolver.Resolve(
            CustomerId,
            [UserQuota(64)],
            new Dictionary<string, BillingV2KoxoUserIdentitySnapshot>(
                StringComparer.Ordinal),
            secondaryGroup: null);

        // Le fournisseur de quota n'est pas un chemin de creation d'identite :
        // deux proprietaires de la creation feraient diverger l'annuaire de la
        // base KoXo.
        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.IdentityNotMaterialized
            && resolution.Targets.Count == 0,
            "Un utilisateur non materialise doit bloquer, jamais etre cree implicitement par le quota.");
    }

    private static void VerifyMissingEmployeeNumberBlocks()
    {
        foreach (var identifier in new string?[]
            { null, "", "CLI-42", "ZZZ-000123" })
        {
            var resolution = ResolveUser(
                UserQuota(64),
                Snapshot() with { KoxoUniqueIdentifier = identifier });

            // employeeNumber est la seule cle de rattachement fiable : le nom
            // est translittere et le sAMAccountName derive par KoXo.
            Ensure(
                !resolution.Resolved
                && resolution.ReasonCode
                    == BillingV2KoxoStorageTargetReasons.EmployeeNumberInvalid,
                "Un identifiant unique absent ou mal forme doit bloquer la resolution.");
        }
    }

    private static void VerifyDuplicateAdLinkBlocksInsteadOfChoosing()
    {
        var second = Link() with
        {
            Id = "link-2",
            ObjectGuid = "66666666-6666-6666-6666-666666666666"
        };
        var resolution = ResolveUser(
            UserQuota(64),
            Snapshot() with { PortalUserLinks = [Link(), second] });

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.IdentityAmbiguous,
            "Deux liens annuaire pour un meme utilisateur doivent bloquer, pas etre departages au hasard.");

        var none = ResolveUser(
            UserQuota(64),
            Snapshot() with { PortalUserLinks = [] });
        Ensure(
            !none.Resolved
            && none.ReasonCode
                == BillingV2KoxoStorageTargetReasons.IdentityNotLinked,
            "Aucun lien annuaire doit bloquer explicitement.");
    }

    private static void VerifyLinkOfAnotherCustomerIsRefused()
    {
        var resolution = ResolveUser(
            UserQuota(64),
            Snapshot() with
            {
                PortalUserLinks = [Link() with { CustomerId = OtherCustomerId }]
            });

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.IdentityCustomerMismatch,
            "Un lien appartenant a un autre client ne doit jamais porter le quota.");
    }

    /// <summary>
    /// Cle du dictionnaire A, instantane decrivant B.
    /// </summary>
    /// <remarks>
    /// La cle est choisie par l'appelant : elle ne prouve rien. Un instantane
    /// range sous la mauvaise cle est parfaitement coherent avec lui-meme, donc
    /// les controles GUID/SID/sAMAccountName reussiraient tous et le quota de A
    /// serait pose sur l'identite de B.
    /// </remarks>
    private static void VerifySnapshotFiledUnderTheWrongKeyIsRefused()
    {
        var resolution = ResolveUser(
            UserQuota(64),
            Snapshot() with { IdentityReference = OtherIdentityReference });

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.IdentitySnapshotMismatch
            && resolution.Targets.Count == 0,
            "Un instantane range sous la cle d'un autre utilisateur doit refuser, pas etre cru sur parole.");
    }

    /// <summary>
    /// Quota de A, lien annuaire de B, meme client.
    /// </summary>
    /// <remarks>
    /// Le triplet prouve que le lien et l'objet d'annuaire decrivent le meme
    /// compte, pas que ce compte appartient au <c>portal_users.id</c> qui a paye
    /// le quota. Sans ce controle, deux utilisateurs d'un meme client sont
    /// interchangeables.
    /// </remarks>
    private static void VerifyLinkOfAnotherPortalUserIsRefused()
    {
        var resolution = ResolveUser(
            UserQuota(64),
            Snapshot() with
            {
                PortalUserLinks =
                    [Link() with { PortalUserId = OtherIdentityReference }]
            });

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.IdentityPortalUserMismatch
            && resolution.Targets.Count == 0,
            "Un lien annuaire rattache a un autre utilisateur portail ne doit jamais porter le quota.");
    }

    private static void VerifyDirectoryLookupFailureBlocks()
    {
        var resolution = ResolveUser(
            UserQuota(64),
            Snapshot() with { DirectoryObjectByEmployeeNumber = null });

        // Null couvre aussi le cas de plusieurs correspondances : la recherche
        // par employeeNumber traite le multiple comme une absence.
        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.DirectoryObjectNotFound,
            "Une recherche par employeeNumber sans resultat unique doit bloquer.");
    }

    private static void VerifyDirectoryMismatchBlocksOnEachAttribute()
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

        // Le lien enregistre et l'objet retrouve doivent decrire le meme compte
        // sur les trois attributs. Un seul ecart signale un lien perime ou un
        // employeeNumber recycle.
        foreach (var divergent in divergences)
        {
            var resolution = ResolveUser(
                UserQuota(64),
                Snapshot() with { DirectoryObjectByEmployeeNumber = divergent });

            Ensure(
                !resolution.Resolved
                && resolution.ReasonCode
                    == BillingV2KoxoStorageTargetReasons.DirectoryObjectMismatch,
                "Toute divergence GUID/SID/sAMAccountName doit refuser la cible.");
        }
    }

    private static void VerifyNoNameBasedFallbackExists()
    {
        // Nom identique, GUID different : c'est exactement le piege que KoXo
        // fabrique en translitterant les noms. Aucun rapprochement par nom ne
        // doit sauver ce cas.
        var homonym = DirectoryObject() with
        {
            ObjectGuid = "88888888-8888-8888-8888-888888888888",
            DisplayName = "LAUMAILLE Marie",
            ObjectSid = "S-1-5-21-1-2-3-7777"
        };
        var resolution = ResolveUser(
            UserQuota(64),
            Snapshot() with { DirectoryObjectByEmployeeNumber = homonym });

        Ensure(
            !resolution.Resolved
            && resolution.Targets.Count == 0,
            "Un homonyme ne doit jamais devenir la cible du quota.");
    }

    // ------------------------------------------------------------------
    // Groupe secondaire.
    // ------------------------------------------------------------------
    private static void VerifySecondaryGroupTargetFollowsTheExportTopology()
    {
        var resolution = BillingV2KoxoStorageTargetResolver.Resolve(
            CustomerId,
            [SharedQuota(128)],
            Identities(),
            new BillingV2KoxoSecondaryGroupSnapshot(
                CustomerId,
                IsDemo: false,
                KoxoGroupReference: null,
                CustomerReference: "CLI-000042"));

        Ensure(
            resolution.Resolved
            && resolution.Targets.Count == 1
            && resolution.Targets[0].Kind
                == BillingV2KoxoStorageTargetKind.SecondaryGroup
            && resolution.Targets[0].SecondaryGroup == "CLI-000042"
            && resolution.Targets[0].PrimaryGroup
                == KoxoDirectoryTopology.PrimaryGroupClients
            && resolution.Targets[0].QuotaMebibytes == 131072
            && resolution.Targets[0].AdLink is null
            && resolution.Targets[0].EmployeeNumber is null,
            "Un stockage partage doit viser l'OU du client, sans jamais nommer d'utilisateur.");
    }

    private static void VerifyDemoSecondaryGroupKeepsItsOwnOu()
    {
        var resolution = BillingV2KoxoStorageTargetResolver.Resolve(
            CustomerId,
            [SharedQuota(16)],
            Identities(),
            new BillingV2KoxoSecondaryGroupSnapshot(
                CustomerId,
                IsDemo: true,
                KoxoGroupReference: "CLI-000042",
                CustomerReference: "CLI-000042"));

        // L'essai et le compte converti ne doivent jamais partager le nom de
        // leur OU, sinon KoXo croit le groupe deja present et l'identite migree
        // perd son groupe definitivement.
        Ensure(
            resolution.Resolved
            && resolution.Targets[0].SecondaryGroup == "DEMO-CLI-000042"
            && resolution.Targets[0].PrimaryGroup
                == KoxoDirectoryTopology.PrimaryGroupDemo,
            "Un essai doit viser son OU prefixee, sous le groupe primaire de demonstration.");
    }

    private static void VerifySecondaryGroupWithoutCustomerContextIsRefused()
    {
        var resolution = BillingV2KoxoStorageTargetResolver.Resolve(
            CustomerId,
            [SharedQuota(128)],
            Identities(),
            secondaryGroup: null);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.SecondaryGroupUnknown,
            "Sans contexte client, aucune OU ne doit etre devinee.");
    }

    /// <summary>
    /// Abonnement du client A, instantane de groupe du client B.
    /// </summary>
    /// <remarks>
    /// Une OU partagee n'a aucune identite pour la contredire : rien, dans
    /// <c>CLI-000042</c>, ne rappelle a quel abonnement elle appartient. C'est
    /// le seul endroit ou une erreur d'alimentation ne serait rattrapee par
    /// aucun autre controle.
    /// </remarks>
    private static void VerifySecondaryGroupOfAnotherCustomerIsRefused()
    {
        var resolution = BillingV2KoxoStorageTargetResolver.Resolve(
            CustomerId,
            [SharedQuota(128)],
            Identities(),
            new BillingV2KoxoSecondaryGroupSnapshot(
                OtherCustomerId,
                IsDemo: false,
                KoxoGroupReference: null,
                CustomerReference: "CLI-000099"));

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons
                    .SecondaryGroupCustomerMismatch
            && resolution.Targets.Count == 0,
            "Le dossier partage d'un autre client ne doit jamais recevoir ce quota.");

        // Et le meme instantane, presente a son vrai client, reste resolvable :
        // le refus vient bien du rattachement, pas du contenu.
        var sameSnapshotForItsOwner =
            BillingV2KoxoStorageTargetResolver.Resolve(
                OtherCustomerId,
                [SharedQuota(128)],
                Identities(),
                new BillingV2KoxoSecondaryGroupSnapshot(
                    OtherCustomerId,
                    IsDemo: false,
                    KoxoGroupReference: null,
                    CustomerReference: "CLI-000099"));

        Ensure(
            sameSnapshotForItsOwner.Resolved
            && sameSnapshotForItsOwner.Targets.Count == 1
            && sameSnapshotForItsOwner.Targets[0].SecondaryGroup
                == "CLI-000099",
            "Le meme instantane doit rester valide pour le client auquel il appartient.");
    }

    /// <summary>
    /// Deux OU de meme nom sous deux groupes primaires differents.
    /// </summary>
    /// <remarks>
    /// <c>CLIENTS/X</c> et <c>CLIENTS DEMO/X</c> sont deux objets distincts,
    /// dans les deux branches que la separation des groupes primaires cloisonne.
    /// Les confondre declarerait une collision inexistante et refuserait deux
    /// quotas legitimes.
    /// </remarks>
    private static void VerifySameOuNameUnderTwoPrimaryGroupsIsNotACollision()
    {
        var paying = BillingV2ResolvedKoxoStorageTarget.ForSecondaryGroup(
            "item-1",
            131072,
            KoxoDirectoryTopology.PrimaryGroupClients,
            "CLI-000042");
        var demo = BillingV2ResolvedKoxoStorageTarget.ForSecondaryGroup(
            "item-2",
            16384,
            KoxoDirectoryTopology.PrimaryGroupDemo,
            "CLI-000042");

        Ensure(
            paying.TargetKey != demo.TargetKey,
            "Deux OU de meme nom sous deux groupes primaires differents ne sont pas le meme objet.");

        // Le meme couple, lui, doit bien collisionner : c'est ce sur quoi
        // repose le refus de deux quotas concurrents.
        var duplicate = BillingV2ResolvedKoxoStorageTarget.ForSecondaryGroup(
            "item-3",
            65536,
            KoxoDirectoryTopology.PrimaryGroupClients,
            "CLI-000042");
        Ensure(
            paying.TargetKey == duplicate.TargetKey,
            "Deux quotas sur la meme OU doivent rester detectes comme un conflit.");

        // Les espaces de noms restent separes : une OU nommee comme un GUID ne
        // doit pas pouvoir se confondre avec une fiche utilisateur.
        Ensure(
            paying.TargetKey.StartsWith("group:", StringComparison.Ordinal)
            && ResolveUser(UserQuota(64)).Targets[0].TargetKey
                .StartsWith("user:", StringComparison.Ordinal),
            "Les espaces de noms user: et group: doivent rester distincts.");
    }

    // ------------------------------------------------------------------
    // Scope et globalite du refus.
    // ------------------------------------------------------------------
    private static void VerifyScopeIsCheckedOnBothSides()
    {
        var userPlanWithoutOwner = ResolveUser(
            UserQuota(64) with { SubscriptionUserId = null });
        Ensure(
            !userPlanWithoutOwner.Resolved
            && userPlanWithoutOwner.ReasonCode
                == BillingV2KoxoStorageTargetReasons.ScopeIncoherent,
            "Un quota personnel sans titulaire ne doit pas etre resolu.");

        var sharedPlanCarryingAUser = BillingV2KoxoStorageTargetResolver.Resolve(
            CustomerId,
            [
                SharedQuota(128) with
                {
                    SubscriptionUserId = "user-a",
                    IdentityReference = IdentityReference
                }
            ],
            Identities(),
            new BillingV2KoxoSecondaryGroupSnapshot(
                CustomerId,
                IsDemo: false,
                KoxoGroupReference: null,
                CustomerReference: "CLI-000042"));

        // Un stockage partage rattache a une personne est une anomalie : il
        // appartient au client.
        Ensure(
            !sharedPlanCarryingAUser.Resolved
            && sharedPlanCarryingAUser.ReasonCode
                == BillingV2KoxoStorageTargetReasons.ScopeIncoherent,
            "Un stockage partage portant un utilisateur doit etre refuse.");
    }

    private static void VerifyTwoQuotasOnTheSameObjectAreRefused()
    {
        // Deux utilisateurs d'abonnement pointant la meme identite portail : le
        // dernier ecrit gagnerait, donc le resultat dependrait de l'ordre de
        // lecture.
        var resolution = BillingV2KoxoStorageTargetResolver.Resolve(
            CustomerId,
            [
                UserQuota(64) with { SubscriptionItemId = "item-1" },
                UserQuota(32) with
                {
                    SubscriptionItemId = "item-2",
                    SubscriptionUserId = "user-b"
                }
            ],
            Identities(),
            secondaryGroup: null);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.IdentityAmbiguous,
            "Deux quotas visant la meme fiche annuaire doivent bloquer.");
    }

    private static void VerifyOneBadPlanRefusesTheWholeResolution()
    {
        // Fermeture par defaut : un plan sain ne doit pas etre applique aux
        // cotes d'un plan douteux, sinon l'abonnement serait a moitie
        // provisionne sans que rien ne le signale.
        var resolution = BillingV2KoxoStorageTargetResolver.Resolve(
            CustomerId,
            [
                UserQuota(64),
                SharedQuota(128) with { Unit = "TiB" }
            ],
            Identities(),
            new BillingV2KoxoSecondaryGroupSnapshot(
                CustomerId,
                IsDemo: false,
                KoxoGroupReference: null,
                CustomerReference: "CLI-000042"));

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2KoxoStorageTargetReasons.UnitUnexpected
            && resolution.Targets.Count == 0,
            "Une seule ligne douteuse doit refuser la resolution entiere.");
    }

    // ------------------------------------------------------------------
    // Reduction de quota : classee, pas encore appliquee.
    // ------------------------------------------------------------------
    private static void VerifyQuotaDecreaseIsClassifiedAsNonApplicable()
    {
        // Abaisser un quota sous l'occupation reelle bloque immediatement
        // l'utilisateur, sans qu'aucune donnee n'ait ete liberee.
        Ensure(
            BillingV2KoxoQuotaPolicy.Classify(65536, 32768)
                == BillingV2KoxoQuotaTransition.Decrease
            && !BillingV2KoxoQuotaPolicy.IsApplicable(
                BillingV2KoxoQuotaTransition.Decrease),
            "Une reduction de quota doit etre reconnue et declaree non applicable.");

        Ensure(
            BillingV2KoxoQuotaPolicy.Classify(32768, 65536)
                == BillingV2KoxoQuotaTransition.Increase
            && BillingV2KoxoQuotaPolicy.Classify(65536, 65536)
                == BillingV2KoxoQuotaTransition.Unchanged
            && BillingV2KoxoQuotaPolicy.IsApplicable(
                BillingV2KoxoQuotaTransition.Increase)
            && BillingV2KoxoQuotaPolicy.IsApplicable(
                BillingV2KoxoQuotaTransition.Unchanged),
            "Augmentation et statu quo doivent rester applicables.");
    }

    // ------------------------------------------------------------------
    // Fabriques.
    // ------------------------------------------------------------------

    private static BillingV2KoxoStorageTargetResolution ResolveUser(
        BillingV2StorageQuotaPlan quota,
        BillingV2KoxoUserIdentitySnapshot? snapshot = null)
        => BillingV2KoxoStorageTargetResolver.Resolve(
            CustomerId,
            [quota],
            Identities(snapshot),
            secondaryGroup: null);

    private static IReadOnlyDictionary<string, BillingV2KoxoUserIdentitySnapshot>
        Identities(BillingV2KoxoUserIdentitySnapshot? snapshot = null)
        => new Dictionary<string, BillingV2KoxoUserIdentitySnapshot>(
            StringComparer.Ordinal)
        {
            [IdentityReference] = snapshot ?? Snapshot(),
        };

    private static BillingV2KoxoUserIdentitySnapshot Snapshot()
        => new(
            IdentityReference,
            EmployeeNumber,
            [Link()],
            DirectoryObject());

    private static BillingV2StorageQuotaPlan UserQuota(long gibibytes)
        => new(
            SubscriptionItemId: "item-stockage-a",
            SubscriptionUserId: "user-a",
            TargetType: "koxo_user_storage",
            IdentityReference,
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
            CustomerReference: "CLI-000042",
            PortalUserId: IdentityReference,
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
            CustomerReference: "CLI-000042",
            IsDisabled: false);

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
