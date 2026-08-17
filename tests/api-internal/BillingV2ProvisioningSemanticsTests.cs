using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Verrouille la semantique des regles de provisioning Billing V2.
/// </summary>
/// <remarks>
/// <para>
/// Les 12 briques du catalogue ne se provisionnent pas de la meme facon. Trois
/// familles coexistent et doivent rester distinctes :
/// </para>
/// <list type="bullet">
/// <item>
/// une action reelle sur une ressource (groupe Active Directory, quota de
/// stockage) ;
/// </item>
/// <item>
/// un droit reconnu qui n'a aucun objet a creer (socle, support, mise en
/// service, supervision, couverture de sauvegarde heritee) ;
/// </item>
/// <item>
/// une intention comprise mais non executable (identite absente, provider
/// dormant).
/// </item>
/// </list>
/// <para>
/// Confondre la deuxieme famille avec la troisieme bloquerait tout abonnement
/// contenant un socle. Confondre la deuxieme avec un simple noop ferait
/// disparaitre les lacunes de catalogue. C'est pourquoi seule une regle
/// EXPLICITE peut etre reconnue sans ecriture : l'absence de regle reste une
/// anomalie.
/// </para>
/// </remarks>
public static class BillingV2ProvisioningSemanticsTests
{
    private const string SubscriptionId = "11111111-1111-1111-1111-111111111111";
    private const string CustomerId = "22222222-2222-2222-2222-222222222222";

    public static void Run()
    {
        VerifyExplicitBaseServiceRuleIsAcknowledgedWithoutOperation();
        VerifyBaseServiceWithoutRuleStaysUnresolved();
        VerifySupportPlusIsAcknowledgedWithoutOperation();
        VerifyMonitoringIsAcknowledgedWithoutOperation();
        VerifyInitServiceIsAcknowledgedWithoutOperation();
        VerifyPersonalBackupIsInheritedCoverageWithoutOperation();
        VerifySharedBackupIsSubscriptionCoverageWithoutOperation();
        VerifyPersonalStorageStaysWithItsOwner();
        VerifyTwoUsersKeepTwoDistinctStoragePlans();
        VerifySharedStorageNeverReachesAnyUser();
        VerifyDormantStorageProviderBlocksTheWholePlan();
        VerifyUnassignedUserSlotNeverBlocksTheSubscription();
        VerifyResourceAttachedToUnassignedSlotStaysBlocked();
        VerifyUserSlotIsNeverASecondIdentityCreationPath();
        VerifyDownstreamAccessRequiresPersonalStorage();
        VerifyPersonalStorageAloneIsAValidEnvironment();
        VerifyPersonalStorageNeedsNoResolvedAdIdentity();
        VerifyVpnStillDemandsAResolvedAdIdentity();
        VerifyTwoPersonalStoragePlansForOneUserAreRefused();
        VerifyUnknownTargetTypeStaysUnresolved();
        VerifyUnknownRuleTypeStaysUnresolved();
        VerifyUserScopedRuleWithoutUserStaysUnresolved();
        VerifySubscriptionScopedRuleCarryingAUserStaysUnresolved();
        VerifyStorageTierInUnexpectedUnitIsRefused();
        VerifyEntitlementsAloneNeverBlockTheStoragelessPlan();

        Console.WriteLine(
            "Tests semantique des regles de provisioning Billing V2 reussis.");
    }

    // ------------------------------------------------------------------
    // CAS A : BASE-SERVICE avec regle explicite.
    // ------------------------------------------------------------------
    private static void VerifyExplicitBaseServiceRuleIsAcknowledgedWithoutOperation()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                SubscriptionRule(
                    "item-base",
                    "BASE-SERVICE",
                    "platform_entitlement",
                    "platform",
                    "ZACHARY-IT-BASE")
            ]);

        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.SubscriptionResources.Entitlements.Count == 1
            && plan.SubscriptionResources.Entitlements[0].TargetReference
                == "ZACHARY-IT-BASE",
            "CAS A : le socle porte par une regle explicite doit etre reconnu comme resolu.");
        EnsureNoExternalOperation(
            plan,
            "CAS A : reconnaitre le socle ne doit produire aucune action d'infrastructure.");
    }

    // ------------------------------------------------------------------
    // CAS B : BASE-SERVICE sans regle.
    // ------------------------------------------------------------------
    private static void VerifyBaseServiceWithoutRuleStaysUnresolved()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                new BillingV2ProvisioningRuleProjection(
                    SubscriptionId,
                    "item-base",
                    "BASE-SERVICE",
                    TierCode: null,
                    RuleType: string.Empty,
                    TargetType: string.Empty,
                    TargetReference: null,
                    ValueSource: string.Empty,
                    StaticValue: null,
                    TierNumericValue: null,
                    TierUnit: null,
                    Quantity: 1,
                    ScopeType: "subscription",
                    SubscriptionUserId: null,
                    IdentityReference: null,
                    SubscriptionUserIsPrimary: false,
                    SubscriptionUserStatus: null)
            ]);

        // Une brique vendue dont le catalogue ne dit pas quoi faire est une
        // lacune. La reconnaitre implicitement comme "rien a faire" masquerait
        // exactement le cas ou un droit paye n'est jamais applique.
        Ensure(
            plan.Blockers.Count == 1
            && plan.Blockers[0].ReasonCode
                == BillingV2ProvisioningBlockerReasons.RuleMissing
            && plan.SubscriptionResources.Entitlements.Count == 0,
            "CAS B : l'absence de regle ne doit jamais devenir un noop implicite.");
    }

    // ------------------------------------------------------------------
    // CAS C : SUPPORT-PLUS.
    // ------------------------------------------------------------------
    private static void VerifySupportPlusIsAcknowledgedWithoutOperation()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                SubscriptionRule(
                    "item-support",
                    "SUPPORT-PLUS",
                    "contractual_entitlement",
                    "support_level",
                    "PLUS")
            ]);

        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.SubscriptionResources.Entitlements.Count == 1
            && plan.SubscriptionResources.Entitlements[0].TargetType
                == "support_level",
            "CAS C : un niveau de support contractuel doit etre reconnu.");
        EnsureNoExternalOperation(
            plan,
            "CAS C : un niveau de support ne cree aucun objet technique.");
    }

    // ------------------------------------------------------------------
    // CAS D : MONITORING-INTERNAL.
    // ------------------------------------------------------------------
    private static void VerifyMonitoringIsAcknowledgedWithoutOperation()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                SubscriptionRule(
                    "item-monitoring",
                    "MONITORING-INTERNAL",
                    "platform_entitlement",
                    "monitoring",
                    "ZACHARY-IT-INFRA")
            ]);

        // La supervision est globale : il n'existe pas d'objet de supervision
        // par abonnement a creer.
        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.SubscriptionResources.Entitlements.Count == 1,
            "CAS D : la supervision de plateforme doit etre reconnue sans objet dedie.");
        EnsureNoExternalOperation(
            plan,
            "CAS D : la supervision ne doit declencher aucune action par abonnement.");
    }

    // ------------------------------------------------------------------
    // CAS E : INIT-SERVICE.
    // ------------------------------------------------------------------
    private static void VerifyInitServiceIsAcknowledgedWithoutOperation()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                SubscriptionRule(
                    "item-init",
                    "INIT-SERVICE",
                    "service_delivery",
                    "onboarding",
                    "ZACHARY-IT-INIT")
            ]);

        // Une prestation humaine connue ne doit pas bloquer les ressources
        // techniques de l'abonnement.
        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.SubscriptionResources.Entitlements.Count == 1,
            "CAS E : une prestation de mise en service doit etre reconnue sans bloquer le reste.");
        EnsureNoExternalOperation(
            plan,
            "CAS E : une prestation humaine ne declenche aucune action d'infrastructure.");
    }

    // ------------------------------------------------------------------
    // CAS F : BACKUP-PERSONAL 64.
    // ------------------------------------------------------------------
    private static void VerifyPersonalBackupIsInheritedCoverageWithoutOperation()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                BackupRule(
                    "item-backup-a",
                    "BACKUP-PERSONAL",
                    "64",
                    64,
                    "user",
                    "user-a",
                    "identity-a")
            ]);

        // Le dossier personnel est deja dans le perimetre sauvegarde par la
        // politique globale : aucun objet de sauvegarde par abonnement.
        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.Users.Count == 1
            && plan.Users[0].UserInheritedCoverages.Count == 1
            && plan.Users[0].UserInheritedCoverages[0].TargetReference
                == "VEEAM-KOXODATA"
            && plan.Users[0].DesiredAdGroups.Count == 0
            && plan.Users[0].UserStoragePlans.Count == 0,
            "CAS F : une sauvegarde personnelle doit etre reconnue comme couverture heritee, rattachee a son utilisateur.");
        EnsureNoExternalOperation(
            plan,
            "CAS F : une couverture heritee ne doit produire aucune operation.");
    }

    // ------------------------------------------------------------------
    // CAS G : BACKUP-SHARED 128.
    // ------------------------------------------------------------------
    private static void VerifySharedBackupIsSubscriptionCoverageWithoutOperation()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                BackupRule(
                    "item-backup-partage",
                    "BACKUP-SHARED",
                    "128",
                    128,
                    "subscription",
                    subscriptionUserId: null,
                    identityReference: null)
            ]);

        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.Users.Count == 0
            && plan.SubscriptionResources.InheritedCoverages.Count == 1
            && plan.SubscriptionResources.InheritedCoverages[0].TargetReference
                == "VEEAM-KOXODATA",
            "CAS G : une sauvegarde partagee doit rester au niveau abonnement.");
        EnsureNoExternalOperation(
            plan,
            "CAS G : une couverture partagee ne doit produire aucune operation.");
    }

    // ------------------------------------------------------------------
    // CAS H : STORAGE-PERSONAL de A ne doit pas atteindre B.
    // ------------------------------------------------------------------
    private static void VerifyPersonalStorageStaysWithItsOwner()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserStorageRule("item-stockage-a", 64, "user-a", "identity-a"),
                UserStorageRule("item-stockage-b", 16, "user-b", "identity-b"),
                AdGroupRule("item-vpn-b", "VPN-ACCESS", "GG_VPN", "user-b", "identity-b")
            ]);

        var owner = plan.Users.Single(user => user.SubscriptionUserId == "user-a");
        var other = plan.Users.Single(user => user.SubscriptionUserId == "user-b");

        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && owner.PersonalStorage is not null
            && owner.PersonalStorage.QuotaValue == 64
            && owner.PersonalStorage.IdentityReference == "identity-a"
            && other.PersonalStorage is not null
            && other.PersonalStorage.QuotaValue == 16,
            "CAS H : le stockage personnel de A ne doit jamais apparaitre dans l'etat desire de B.");
    }

    // ------------------------------------------------------------------
    // CAS I : A = 64, B = 32.
    // ------------------------------------------------------------------
    private static void VerifyTwoUsersKeepTwoDistinctStoragePlans()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserStorageRule("item-stockage-a", 64, "user-a", "identity-a"),
                UserStorageRule("item-stockage-b", 32, "user-b", "identity-b")
            ]);

        // Le quota est individuel : deux utilisateurs du meme client peuvent
        // porter deux tiers differents, et rien ne doit les fusionner.
        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.StorageQuotaPlans.Count == 2
            && plan.Users.Single(user => user.SubscriptionUserId == "user-a")
                .PersonalStorage!.QuotaValue == 64
            && plan.Users.Single(user => user.SubscriptionUserId == "user-b")
                .PersonalStorage!.QuotaValue == 32,
            "CAS I : deux quotas personnels distincts doivent rester distincts.");
    }

    // ------------------------------------------------------------------
    // CAS J : STORAGE-SHARED 128.
    // ------------------------------------------------------------------
    private static void VerifySharedStorageNeverReachesAnyUser()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                SharedStorageRule("item-stockage-partage", 128),
                UserStorageRule("item-stockage-a", 64, "user-a", "identity-a"),
                AdGroupRule("item-vpn-a", "VPN-ACCESS", "GG_VPN", "user-a", "identity-a")
            ]);

        // Le stockage partage appartient au groupe secondaire du client, pas a
        // un utilisateur : le distribuer serait la meme faute que distribuer un
        // groupe AD achete au niveau abonnement.
        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.SubscriptionResources.SharedStoragePlans.Count == 1
            && plan.SubscriptionResources.SharedStoragePlans[0].TargetType
                == "koxo_secondary_group_storage"
            && plan.SubscriptionResources.SharedStoragePlans[0].SubscriptionUserId
                is null
            && plan.SubscriptionResources.SharedStoragePlans[0].IdentityReference
                is null
            && plan.Users.All(user => user.PersonalStorage?.TargetType
                != "koxo_secondary_group_storage"),
            "CAS J : un stockage partage ne doit jamais etre recopie dans l'etat desire d'un utilisateur.");
    }

    // ------------------------------------------------------------------
    // CAS K : provider de stockage dormant.
    // ------------------------------------------------------------------
    private static void VerifyDormantStorageProviderBlocksTheWholePlan()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserStorageRule("item-stockage-a", 64, "user-a", "identity-a"),
                AdGroupRule("item-vpn-a", "VPN-ACCESS", "GG_VPN", "user-a", "identity-a"),
                AdGroupRule("item-rds-a", "RDS-ACCESS", "GG_RDS", "user-a", "identity-a")
            ]);

        var readiness = DormantBillingV2KoxoStorageProvider.Instance
            .CheckReadiness(plan.StorageQuotaPlans);

        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.StorageQuotaPlans.Count == 1
            && plan.AllDesiredAdGroups.Count == 2,
            "CAS K : le plan doit bien contenir a la fois un quota et des droits AD.");

        // Le plan est coherent et pourtant inexecutable : tant que le quota ne
        // peut pas etre applique, accorder VPN et RDS reviendrait a declarer
        // provisionne un abonnement qui ne l'est qu'a moitie.
        Ensure(
            !readiness.CanApplyQuotas
            && readiness.ReasonCode
                == "BILLING_V2_KOXO_STORAGE_PROVIDER_NOT_CONFIGURED",
            "CAS K : un plan de stockage non applicable doit bloquer avant toute execution.");
    }

    // ------------------------------------------------------------------
    // CAS L : USER-ADDITIONAL sans identite.
    // ------------------------------------------------------------------
    private static void VerifyUnassignedUserSlotNeverBlocksTheSubscription()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserSlotRule("item-user-additionnel", "user-additionnel", identityReference: null),
                UserStorageRule("item-stockage-a", 64, "user-a", "identity-a"),
                AdGroupRule("item-vpn-a", "VPN-ACCESS", "GG_VPN", "user-a", "identity-a")
            ]);

        // Une place payee mais pas encore attribuee est un etat normal du
        // cycle de vie commercial : le client peut acheter le poste avant de
        // savoir qui l'occupera. Elle ne demande aucune ecriture, donc elle ne
        // doit pas immobiliser le provisioning du reste de l'abonnement.
        Ensure(
            plan.Blockers.Count == 0
            && plan.UnresolvedRuleReferences.Count == 0,
            "CAS L : une place d'utilisateur non attribuee ne doit jamais bloquer tout l'abonnement.");
        Ensure(
            plan.SubscriptionResources.UnassignedUserSlots.Count == 1
            && plan.SubscriptionResources.UnassignedUserSlots[0].TargetType
                == "user_slot"
            && plan.SubscriptionResources.UnassignedUserSlots[0].SubscriptionUserId
                == "user-additionnel",
            "CAS L : la place non attribuee doit rester visible au niveau abonnement, pas disparaitre.");

        // Elle ne doit surtout pas se replier sur l'utilisateur existant.
        Ensure(
            plan.Users.Count == 1
            && plan.Users[0].SubscriptionUserId == "user-a"
            && plan.Users[0].UserEntitlements.Count == 0,
            "CAS L : une place non attribuee ne doit jamais se replier sur un autre utilisateur du client.");
    }

    // ------------------------------------------------------------------
    // La tolerance s'arrete des qu'une ressource reelle vise la place vide.
    // ------------------------------------------------------------------
    private static void VerifyResourceAttachedToUnassignedSlotStaysBlocked()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserSlotRule("item-user-additionnel", "user-additionnel", identityReference: null),
                UserStorageRule("item-stockage-vide", 64, "user-additionnel", identityReference: null),
                AdGroupRule("item-vpn-vide", "VPN-ACCESS", "GG_VPN", "user-additionnel", identityReference: null)
            ]);

        // Acheter la place est sans consequence ; y rattacher un stockage ou un
        // acces ne l'est pas. A ce moment precis, l'absence d'identite de
        // facturation redevient une anomalie, car il n'existe aucun titulaire a
        // qui appliquer la ressource.
        Ensure(
            plan.Blockers.Count == 2
            && plan.Blockers.All(blocker => blocker.ReasonCode
                == BillingV2ProvisioningBlockerReasons.IdentityRequired),
            "Une ressource rattachee a une place non attribuee doit bloquer, elle.");
        Ensure(
            plan.Users.Count == 0
            && plan.SubscriptionResources.UnassignedUserSlots.Count == 1,
            "Une place non attribuee ne doit jamais devenir un utilisateur du plan.");
    }

    // ------------------------------------------------------------------
    // Le droit a un utilisateur supplementaire ne cree aucune identite.
    // ------------------------------------------------------------------
    private static void VerifyUserSlotIsNeverASecondIdentityCreationPath()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserSlotRule("item-user-additionnel", "user-b", "identity-b"),
                UserStorageRule("item-stockage-b", 32, "user-b", "identity-b")
            ]);

        // Il ne doit exister qu'un seul proprietaire technique de la creation
        // d'identite, et c'est la chaine KoXo. Le slot commercial se contente
        // d'autoriser l'utilisateur ; c'est le stockage personnel qui l'equipe.
        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.Users.Single().UserEntitlements.Count == 1
            && plan.Users.Single().UserEntitlements[0].TargetType == "user_slot"
            && plan.Users.Single().PersonalStorage is not null
            && plan.Users.Single().PersonalStorage!.QuotaValue == 32,
            "Le droit a un utilisateur supplementaire doit rester un entitlement, adosse au stockage personnel qui equipe reellement l'utilisateur.");
    }

    // ------------------------------------------------------------------
    // CAS M : target_type inconnu.
    // ------------------------------------------------------------------
    private static void VerifyUnknownTargetTypeStaysUnresolved()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                SubscriptionRule(
                    "item-inconnu",
                    "BASE-SERVICE",
                    "platform_entitlement",
                    "plateforme_inconnue",
                    "ZACHARY-IT-BASE")
            ]);

        Ensure(
            plan.Blockers.Count == 1
            && plan.Blockers[0].ReasonCode
                == BillingV2ProvisioningBlockerReasons.TargetTypeUnknown
            && plan.SubscriptionResources.Entitlements.Count == 0,
            "CAS M : un target_type inconnu ne doit recevoir aucune interpretation par defaut.");
    }

    // ------------------------------------------------------------------
    // CAS N : rule_type inconnu.
    // ------------------------------------------------------------------
    private static void VerifyUnknownRuleTypeStaysUnresolved()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                SubscriptionRule(
                    "item-inconnu",
                    "BASE-SERVICE",
                    "regle_inconnue",
                    "platform",
                    "ZACHARY-IT-BASE")
            ]);

        Ensure(
            plan.Blockers.Count == 1
            && plan.Blockers[0].ReasonCode
                == BillingV2ProvisioningBlockerReasons.RuleTypeUnknown
            && plan.SubscriptionResources.Entitlements.Count == 0,
            "CAS N : un rule_type inconnu doit rester non resolu.");
    }

    // ------------------------------------------------------------------
    // CAS O : scope utilisateur incoherent.
    // ------------------------------------------------------------------
    private static void VerifyUserScopedRuleWithoutUserStaysUnresolved()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserStorageRule(
                    "item-stockage-orphelin",
                    64,
                    subscriptionUserId: null,
                    identityReference: null)
            ]);

        Ensure(
            plan.Blockers.Count == 1
            && plan.Blockers[0].ReasonCode
                == BillingV2ProvisioningBlockerReasons.ScopeIncoherent
            && plan.StorageQuotaPlans.Count == 0,
            "CAS O : un quota personnel sans titulaire ne doit jamais etre attribue par defaut.");
    }

    // ------------------------------------------------------------------
    // CAS P : scope abonnement incoherent.
    // ------------------------------------------------------------------
    private static void VerifySubscriptionScopedRuleCarryingAUserStaysUnresolved()
    {
        var carriedByAUser = BillingV2ProvisioningPlanner.Plan(
            [
                SharedStorageRule(
                    "item-stockage-partage",
                    128,
                    scopeType: "subscription",
                    subscriptionUserId: "user-a")
            ]);

        Ensure(
            carriedByAUser.Blockers.Count == 1
            && carriedByAUser.Blockers[0].ReasonCode
                == BillingV2ProvisioningBlockerReasons.ScopeIncoherent
            && carriedByAUser.SubscriptionResources.SharedStoragePlans.Count == 0,
            "CAS P : un item de scope abonnement portant un utilisateur est une anomalie.");

        var boughtAtUserScope = BillingV2ProvisioningPlanner.Plan(
            [
                SharedStorageRule(
                    "item-stockage-partage",
                    128,
                    scopeType: "user",
                    subscriptionUserId: "user-a",
                    identityReference: "identity-a")
            ]);

        // Le scope impose par la regle prime : un stockage de groupe secondaire
        // achete au scope utilisateur ne doit pas devenir un quota personnel.
        Ensure(
            boughtAtUserScope.Blockers.Count == 1
            && boughtAtUserScope.Blockers[0].ReasonCode
                == BillingV2ProvisioningBlockerReasons.ScopeIncoherent
            && boughtAtUserScope.StorageQuotaPlans.Count == 0,
            "CAS P : un stockage de groupe secondaire ne doit jamais devenir un quota personnel.");
    }

    // ------------------------------------------------------------------
    // Unite du catalogue.
    // ------------------------------------------------------------------
    private static void VerifyStorageTierInUnexpectedUnitIsRefused()
    {
        var withoutUnit = BillingV2ProvisioningPlanner.Plan(
            [
                UserStorageRule(
                    "item-stockage-a",
                    64,
                    "user-a",
                    "identity-a",
                    tierUnit: null)
            ]);

        var withAnotherUnit = BillingV2ProvisioningPlanner.Plan(
            [
                UserStorageRule(
                    "item-stockage-a",
                    64,
                    "user-a",
                    "identity-a",
                    tierUnit: "Mbps")
            ]);

        // Un tier ne porte qu'un nombre : sans unite verifiee, 64 pourrait
        // aussi bien valoir 64 Mio que 64 Tio.
        Ensure(
            withoutUnit.Blockers.Count == 1
            && withoutUnit.Blockers[0].ReasonCode
                == BillingV2ProvisioningBlockerReasons.UnitUnexpected
            && withoutUnit.StorageQuotaPlans.Count == 0,
            "Un tier de stockage sans unite ne doit pas recevoir une unite par defaut.");
        Ensure(
            withAnotherUnit.Blockers.Count == 1
            && withAnotherUnit.Blockers[0].ReasonCode
                == BillingV2ProvisioningBlockerReasons.UnitUnexpected
            && withAnotherUnit.StorageQuotaPlans.Count == 0,
            "Un tier de stockage exprime dans une autre unite doit etre refuse.");
    }

    // ------------------------------------------------------------------
    // Le socle technique conditionne les acces aval.
    // ------------------------------------------------------------------
    private static void VerifyDownstreamAccessRequiresPersonalStorage()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                AdGroupRule("item-vpn-b", "VPN-ACCESS", "GG_VPN", "user-b", "identity-b"),
                AdGroupRule("item-rds-b", "RDS-ACCESS", "GG_RDS", "user-b", "identity-b"),
                UserStorageRule("item-stockage-a", 64, "user-a", "identity-a"),
                AdGroupRule("item-vpn-a", "VPN-ACCESS", "GG_VPN", "user-a", "identity-a")
            ]);

        // L'environnement utilisateur est produit par le provisioning du
        // stockage personnel. Un acces VPN ou RDS accorde sans lui ouvrirait
        // une porte vers un poste de travail qui n'existe pas.
        Ensure(
            plan.Blockers.Count == 2
            && plan.Blockers.All(blocker => blocker.ReasonCode
                == BillingV2ProvisioningBlockerReasons.PersonalStorageRequired)
            && plan.UnresolvedRuleReferences.Count == 2,
            "Un acces VPN ou RDS sans stockage personnel doit bloquer, avec une raison nommant le socle manquant.");

        // Le blocage est nominatif : l'utilisateur correctement equipe n'est
        // pas puni pour son voisin.
        Ensure(
            plan.UnresolvedRuleReferences.All(reference =>
                reference.EndsWith("item-vpn-b", StringComparison.Ordinal)
                || reference.EndsWith("item-rds-b", StringComparison.Ordinal)),
            "Seuls les acces de l'utilisateur sans socle doivent etre bloques.");
    }

    private static void VerifyPersonalStorageAloneIsAValidEnvironment()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserStorageRule("item-stockage-a", 64, "user-a", "identity-a")
            ]);

        // L'inverse n'est pas vrai : un utilisateur peut n'avoir qu'un
        // environnement, sans aucun acces optionnel.
        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.Users.Single().PersonalStorage is not null
            && plan.Users.Single().DesiredAdGroups.Count == 0,
            "Un stockage personnel sans acces optionnel doit rester un plan valide.");
    }

    /// <summary>
    /// Le stockage personnel ne suppose aucune identite annuaire deja resolue.
    /// </summary>
    /// <remarks>
    /// Au stade du plan, aucune ecriture annuaire n'est demandee par un simple
    /// quota : exiger un <c>customer_ad_links</c> ici bloquerait l'essai de
    /// demonstration, dont le compte n'est cree qu'ensuite par KoXo. La
    /// verification stricte de l'identite existe bien, mais au moment de viser
    /// la fiche KoXo — voir
    /// <c>BillingV2KoxoStorageTargetResolver</c>.
    /// </remarks>
    private static void VerifyPersonalStorageNeedsNoResolvedAdIdentity()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserStorageRule("item-stockage-a", 64, "user-a", "identity-a")
            ]);

        // L'identite de facturation suffit a planifier : identity-a existe cote
        // portail, aucun lien annuaire n'existe encore.
        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.Users.Single().PersonalStorage is not null
            && plan.Users.Single().IdentityReference == "identity-a",
            "Un stockage personnel doit se planifier sur la seule identite de facturation.");

        // Et il ne reclame aucune resolution annuaire.
        Ensure(
            plan.UsersRequiringAdIdentity.Count == 0,
            "Un utilisateur sans acces AD ne doit pas etre soumis a la resolution d'identite annuaire.");

        // Preuve directe : la resolution appliquee au sous-ensemble reellement
        // concerne reussit meme sans aucun lien annuaire pour ce client.
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            plan.UsersRequiringAdIdentity,
            new Dictionary<string, IReadOnlyList<Kermaria.ApiInternal.Data.Repositories.PortalUserAdLinkRecord>>(
                StringComparer.Ordinal),
            []);
        Ensure(
            resolution.Resolved && resolution.Targets.Count == 0,
            "L'absence de lien annuaire ne doit pas produire d'echec quand aucun acces AD n'est demande.");

        // Le seul frein restant doit etre le provider KoXo, pas l'identite.
        var readiness = DormantBillingV2KoxoStorageProvider.Instance
            .CheckReadiness(plan.StorageQuotaPlans);
        Ensure(
            !readiness.CanApplyQuotas
            && readiness.ReasonCode
                == "BILLING_V2_KOXO_STORAGE_PROVIDER_NOT_CONFIGURED",
            "Le blocage doit venir du provider KoXo dormant, jamais d'un defaut d'identite annuaire.");
    }

    /// <summary>
    /// Le VPN, lui, reste adosse a une identite annuaire reellement resolue.
    /// </summary>
    private static void VerifyVpnStillDemandsAResolvedAdIdentity()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserStorageRule("item-stockage-a", 64, "user-a", "identity-a"),
                AdGroupRule("item-vpn-a", "VPN-ACCESS", "GG_VPN", "user-a", "identity-a")
            ]);

        // Le socle reste planifiable dans la meme passe.
        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.Users.Single().PersonalStorage is not null
            && plan.UsersRequiringAdIdentity.Count == 1,
            "Le stockage doit rester planifiable, et seul l'acces VPN doit reclamer l'annuaire.");

        // Mais tant que le compte n'existe pas dans l'annuaire, l'acces ne peut
        // pas etre applique — et surtout pas a quelqu'un d'autre.
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            plan.UsersRequiringAdIdentity,
            new Dictionary<string, IReadOnlyList<Kermaria.ApiInternal.Data.Repositories.PortalUserAdLinkRecord>>(
                StringComparer.Ordinal),
            [AdLink("identity-tierce")]);
        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == "BILLING_V2_PROVISIONING_IDENTITY_NOT_LINKED"
            && resolution.Targets.Count == 0,
            "Un acces VPN sans identite annuaire resolue doit echouer, sans repli sur un autre compte du client.");
    }

    private static void VerifyTwoPersonalStoragePlansForOneUserAreRefused()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserStorageRule("item-stockage-a1", 64, "user-a", "identity-a"),
                UserStorageRule("item-stockage-a2", 128, "user-a", "identity-a")
            ]);

        // Deux quotas contradictoires pour un meme environnement ne se
        // departagent pas silencieusement.
        Ensure(
            plan.Blockers.Count == 1
            && plan.Blockers[0].ReasonCode
                == BillingV2ProvisioningBlockerReasons.PersonalStorageConflict,
            "Deux stockages personnels pour un meme utilisateur doivent bloquer au lieu d'en choisir un.");
    }

    // ------------------------------------------------------------------
    // Les droits reconnus ne sont pas des bloqueurs.
    // ------------------------------------------------------------------
    private static void VerifyEntitlementsAloneNeverBlockTheStoragelessPlan()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                SubscriptionRule("item-base", "BASE-SERVICE", "platform_entitlement", "platform", "ZACHARY-IT-BASE"),
                SubscriptionRule("item-support", "SUPPORT-STANDARD", "contractual_entitlement", "support_level", "STANDARD"),
                SubscriptionRule("item-init", "INIT-SERVICE", "service_delivery", "onboarding", "ZACHARY-IT-INIT"),
                SubscriptionRule("item-monitoring", "MONITORING-INTERNAL", "platform_entitlement", "monitoring", "ZACHARY-IT-INFRA"),
                UserStorageRule("item-stockage-a", 64, "user-a", "identity-a"),
                BackupRule("item-backup-a", "BACKUP-PERSONAL", "64", 64, "user", "user-a", "identity-a"),
                AdGroupRule("item-vpn-a", "VPN-ACCESS", "GG_VPN", "user-a", "identity-a")
            ]);

        // Un abonnement realiste melange droits contractuels et droits reels.
        // Les premiers ne doivent jamais empecher les seconds : seul le
        // stockage KoXo, dont le provider est dormant, bloque encore.
        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.SubscriptionResources.Entitlements.Count == 4
            && plan.SubscriptionResources.InheritedCoverages.Count == 0
            && plan.Users.Single().UserInheritedCoverages.Count == 1
            && plan.AllDesiredAdGroups.SequenceEqual(["GG_VPN"]),
            "Les droits contractuels ne doivent jamais empecher les droits techniques d'etre resolus.");

        var requests = BillingV2ProvisioningExecutionPlanner.BuildPerUserRequests(
            BillingV2ProvisioningGateDecision.Allow(addOnlyMode: true),
            [
                new BillingV2ResolvedProvisioningTarget(
                    plan.Users.Single(),
                    AdLink("identity-a"))
            ],
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["GG_VPN"] = "CN=GG_VPN,OU=Groupes_TEST,DC=clients,DC=home,DC=bzh"
            });

        Ensure(
            requests.Count == 1
            && requests[0].DesiredGroupSamAccountNames.SequenceEqual(["GG_VPN"]),
            "Seul le droit AD reellement achete doit produire une requete d'execution.");
    }

    // ------------------------------------------------------------------
    // Assertions communes.
    // ------------------------------------------------------------------

    /// <summary>
    /// Verifie qu'un plan reconnu ne produit aucune ecriture externe.
    /// </summary>
    /// <remarks>
    /// Un droit reconnu ne doit produire ni requete Active Directory, ni plan
    /// de quota, ni identite a creer. La construction des requetes est refaite
    /// ici plutot que deduite : c'est elle, et non le classement, qui decide de
    /// ce qui part vers l'annuaire.
    /// </remarks>
    private static void EnsureNoExternalOperation(
        BillingV2ProvisioningPlan plan,
        string message)
    {
        var targets = plan.Users
            .Select(user => new BillingV2ResolvedProvisioningTarget(
                user,
                AdLink(user.IdentityReference)))
            .ToArray();
        var requests = BillingV2ProvisioningExecutionPlanner.BuildPerUserRequests(
            BillingV2ProvisioningGateDecision.Allow(addOnlyMode: true),
            targets,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));

        Ensure(
            requests.Count == 0
            && plan.StorageQuotaPlans.Count == 0
            && plan.AllDesiredAdGroups.Count == 0
,
            message);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    // ------------------------------------------------------------------
    // Fabriques de projections.
    // ------------------------------------------------------------------

    private static BillingV2ProvisioningRuleProjection SubscriptionRule(
        string subscriptionItemId,
        string serviceCode,
        string ruleType,
        string targetType,
        string targetReference)
        => new(
            SubscriptionId,
            subscriptionItemId,
            serviceCode,
            TierCode: null,
            ruleType,
            targetType,
            targetReference,
            "none",
            StaticValue: null,
            TierNumericValue: null,
            TierUnit: null,
            Quantity: 1,
            ScopeType: "subscription",
            SubscriptionUserId: null,
            IdentityReference: null,
            SubscriptionUserIsPrimary: false,
            SubscriptionUserStatus: null);

    private static BillingV2ProvisioningRuleProjection BackupRule(
        string subscriptionItemId,
        string serviceCode,
        string tierCode,
        long numericValue,
        string scopeType,
        string? subscriptionUserId,
        string? identityReference)
        => new(
            SubscriptionId,
            subscriptionItemId,
            serviceCode,
            tierCode,
            "inherited_coverage",
            "backup_policy",
            "VEEAM-KOXODATA",
            "tier_numeric_value",
            StaticValue: null,
            numericValue,
            TierUnit: "GiB",
            Quantity: 1,
            scopeType,
            subscriptionUserId,
            identityReference,
            SubscriptionUserIsPrimary: false,
            SubscriptionUserStatus: subscriptionUserId is null ? null : "active");

    private static BillingV2ProvisioningRuleProjection UserStorageRule(
        string subscriptionItemId,
        long numericValue,
        string? subscriptionUserId,
        string? identityReference,
        string? tierUnit = "GiB")
        => new(
            SubscriptionId,
            subscriptionItemId,
            "STORAGE-PERSONAL",
            $"{numericValue}",
            "infrastructure_action",
            "koxo_user_storage",
            "KOXO-USER-STORAGE",
            "tier_numeric_value",
            StaticValue: null,
            numericValue,
            tierUnit,
            Quantity: 1,
            ScopeType: "user",
            subscriptionUserId,
            identityReference,
            SubscriptionUserIsPrimary: false,
            SubscriptionUserStatus: subscriptionUserId is null ? null : "active");

    private static BillingV2ProvisioningRuleProjection SharedStorageRule(
        string subscriptionItemId,
        long numericValue,
        string scopeType = "subscription",
        string? subscriptionUserId = null,
        string? identityReference = null)
        => new(
            SubscriptionId,
            subscriptionItemId,
            "STORAGE-SHARED",
            $"{numericValue}",
            "infrastructure_action",
            "koxo_secondary_group_storage",
            "KOXO-SECONDARY-GROUP-STORAGE",
            "tier_numeric_value",
            StaticValue: null,
            numericValue,
            TierUnit: "GiB",
            Quantity: 1,
            scopeType,
            subscriptionUserId,
            identityReference,
            SubscriptionUserIsPrimary: false,
            SubscriptionUserStatus: subscriptionUserId is null ? null : "active");

    private static BillingV2ProvisioningRuleProjection UserSlotRule(
        string subscriptionItemId,
        string subscriptionUserId,
        string? identityReference)
        => new(
            SubscriptionId,
            subscriptionItemId,
            "USER-ADDITIONAL",
            TierCode: null,
            "contractual_entitlement",
            "user_slot",
            "ADDITIONAL",
            "none",
            StaticValue: null,
            TierNumericValue: null,
            TierUnit: null,
            Quantity: 1,
            ScopeType: "user",
            subscriptionUserId,
            identityReference,
            SubscriptionUserIsPrimary: false,
            SubscriptionUserStatus: "active");

    private static BillingV2ProvisioningRuleProjection AdGroupRule(
        string subscriptionItemId,
        string serviceCode,
        string groupSamAccountName,
        string subscriptionUserId,
        string? identityReference)
        => new(
            SubscriptionId,
            subscriptionItemId,
            serviceCode,
            TierCode: null,
            "ad_group_membership",
            "ad_group",
            groupSamAccountName,
            "none",
            StaticValue: null,
            TierNumericValue: null,
            TierUnit: null,
            Quantity: 1,
            ScopeType: "user",
            subscriptionUserId,
            identityReference,
            SubscriptionUserIsPrimary: true,
            SubscriptionUserStatus: "active");

    private static CustomerAdLinkSummary AdLink(string identityReference)
        => new(
            Id: $"link-{identityReference}",
            CustomerReference: "22222222-2222-2222-2222-222222222222",
            ObjectGuid: $"00000000-0000-0000-0000-{identityReference.GetHashCode():x12}",
            ObjectSid: $"S-1-5-21-1-1-1-{Math.Abs(identityReference.GetHashCode()) % 100000}",
            ObjectType: "user",
            SamAccountName: $"svc.{identityReference}",
            UserPrincipalName: null,
            DisplayName: identityReference,
            DistinguishedName:
                $"CN={identityReference},OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
            LinkedAt: "1970-01-01T00:00:00Z",
            LinkedBy: null);
}
