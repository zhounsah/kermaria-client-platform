using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Verrouille l'isolation par utilisateur du provisioning Billing V2.
/// </summary>
/// <remarks>
/// <para>
/// Avant cette suite, un droit user-scoped achete par un utilisateur pouvait
/// etre applique a tous les utilisateurs Active Directory du client : la
/// projection SQL ne lisait pas <c>subscription_user_id</c>, le plan agregeait
/// les groupes au niveau client, et <see cref="ProvisioningService"/> applique
/// chaque groupe gere a chaque <c>TargetUsers</c> recu.
/// </para>
/// <para>
/// Les tests s'appuient sur le vrai <see cref="ProvisioningService"/> et un
/// provisionneur enregistreur, et non sur un faux moteur : c'est la seule
/// facon de prouver l'isolation reellement obtenue plutot que celle qu'on
/// croit avoir codee.
/// </para>
/// </remarks>
public static class BillingV2ProvisioningScopeTests
{
    private const string CustomerId = "customer-kermaria";
    private const string OtherCustomerId = "customer-autre";
    private const string SubscriptionId = "subscription-v2";

    // Deux objets annuaire homonymes : meme sAMAccountName, domaines
    // differents, donc objectGUID et objectSid differents.
    private const string HomonymGuidA = "0f1b6f0a-9d54-4a1e-9b3f-6d1c2f8a4b71";
    private const string HomonymSidA = "S-1-5-21-1004336348-1177238915-682003330-1131";
    private const string HomonymGuidB = "7c4e2d18-3b6a-4f52-8e10-a5d9c3b70e24";
    private const string HomonymSidB = "S-1-5-21-2110995556-1274434293-847283962-2417";

    // Valeur non vide et non parsable : elle ne doit jamais servir de cle.
    private const string OpaqueGuid = "guid-svc.jdupont";

    public static async Task RunAsync()
    {
        await VerifyUserScopedRightsNeverLeakToAnotherUser();
        await VerifyUserWithoutUserScopedServiceGetsNoOperation();
        VerifyUserScopedItemWithoutSubscriptionUserFailsClosed();
        VerifyUserWithoutIdentityReferenceFailsClosed();
        VerifyIdentityWithoutAdLinkFailsClosed();
        VerifyAmbiguousAdLinkFailsClosed();
        VerifyAdLinkOfAnotherCustomerFailsClosed();
        VerifyTwoSubscriptionUsersSharingOneIdentityFailClosed();
        VerifyPersonalQuotaWithoutIdentityFailsClosed();
        VerifyPersonalQuotaKeepsUserScopeAndStaysBlocked();
        VerifyActiveServiceWithoutRuleStaysUnresolved();
        VerifySubscriptionScopedAdGroupFailsClosed();
        await VerifyRetryIsIdempotent();
        await VerifyAddOnlyModeNeverRemoves();
        VerifyEveryExecutionRequestCarriesExactlyOneUser();
        VerifySameSamAccountNameIsDisambiguatedByObjectGuid();
        VerifyMissingObjectGuidInCustomerReferentialFailsClosed();
        VerifyIncoherentSnapshotsFailClosed();
        VerifySameSamAccountNameAcrossDomainsResolvesTheRightObject();
        VerifyRefreshedSnapshotsAfterSidChangeResolve();
        VerifyMalformedPortalObjectGuidFailsClosed();
        VerifyMalformedCustomerObjectGuidNeverMatches();
        VerifyObjectGuidWritingFormIsCanonicalized();
    }

    // ------------------------------------------------------------------
    // CAS 1 : A = VPN + RDS, B = VPN.
    // ------------------------------------------------------------------
    private static async Task VerifyUserScopedRightsNeverLeakToAnotherUser()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                StorageRule("item-a-stockage", "user-a", "identity-a"),
                StorageRule("item-b-stockage", "user-b", "identity-b"),
                UserRule("item-a-vpn", "user-a", "identity-a", "VPN-ACCESS", "GG_VPN"),
                UserRule("item-a-rds", "user-a", "identity-a", "RDS-ACCESS", "GG_RDS"),
                UserRule("item-b-vpn", "user-b", "identity-b", "VPN-ACCESS", "GG_VPN")
            ]);

        Ensure(
            plan.UnresolvedRuleReferences.Count == 0,
            "CAS 1 : un plan complet et coherent ne doit rien laisser non resolu.");
        Ensure(
            plan.Users.Count == 2,
            "CAS 1 : le plan doit porter un etat desire par utilisateur d'abonnement.");
        Ensure(
            GroupsOf(plan, "user-a").SequenceEqual(
                ["GG_RDS", "GG_VPN"],
                StringComparer.OrdinalIgnoreCase),
            "CAS 1 : A doit desirer GG_VPN et GG_RDS.");
        Ensure(
            GroupsOf(plan, "user-b").SequenceEqual(
                ["GG_VPN"],
                StringComparer.OrdinalIgnoreCase),
            "CAS 1 : B doit desirer GG_VPN et rien d'autre.");

        var execution = await ExecuteAsync(plan, ["identity-a", "identity-b"]);

        Ensure(
            execution.Result.Succeeded
            && execution.Result.ResultCode == "PROVISIONING_APPLIED",
            "CAS 1 : l'execution par utilisateur doit reussir.");
        Ensure(
            AppliedGroups(execution, "svc.identity-a").SequenceEqual(
                ["GG_RDS", "GG_VPN"],
                StringComparer.OrdinalIgnoreCase),
            "CAS 1 : A doit recevoir GG_VPN et GG_RDS.");
        Ensure(
            AppliedGroups(execution, "svc.identity-b").SequenceEqual(
                ["GG_VPN"],
                StringComparer.OrdinalIgnoreCase),
            "CAS 1 : B ne doit recevoir que GG_VPN.");
        Ensure(
            !execution.Provisioner.Added.Any(operation =>
                operation.UserSamAccountName == "svc.identity-b"
                && operation.GroupSamAccountName == "GG_RDS"),
            "CAS 1 : le droit RDS de A ne doit jamais atteindre B.");
    }

    // ------------------------------------------------------------------
    // CAS 2 : A = VPN, B n'a aucun service user-scoped.
    // ------------------------------------------------------------------
    private static async Task VerifyUserWithoutUserScopedServiceGetsNoOperation()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                StorageRule("item-a-stockage", "user-a", "identity-a"),
                UserRule("item-a-vpn", "user-a", "identity-a", "VPN-ACCESS", "GG_VPN")
            ]);

        Ensure(
            plan.Users.Count == 1,
            "CAS 2 : un utilisateur sans item user-scoped ne doit pas apparaitre dans le plan.");

        // B existe bien comme identite AD du client : c'est la seule facon de
        // prouver qu'il est ignore par decision et non par absence.
        var execution = await ExecuteAsync(plan, ["identity-a", "identity-b"]);

        Ensure(
            execution.Requests.Count == 1,
            "CAS 2 : une seule requete de reconciliation doit etre produite.");
        Ensure(
            execution.Result.Operations.All(operation =>
                operation.UserSamAccountName == "svc.identity-a"),
            "CAS 2 : aucune operation ne doit nommer B.");
        Ensure(
            execution.Provisioner.Added.Count == 1
            && execution.Provisioner.Removed.Count == 0,
            "CAS 2 : B ne doit declencher aucun appel annuaire.");
    }

    // ------------------------------------------------------------------
    // CAS 3 : item scope=user sans subscription_user_id.
    // ------------------------------------------------------------------
    private static void VerifyUserScopedItemWithoutSubscriptionUserFailsClosed()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserRule(
                    "item-orphelin",
                    subscriptionUserId: null,
                    "identity-a",
                    "VPN-ACCESS",
                    "GG_VPN")
            ]);

        Ensure(
            plan.Users.Count == 0
            && plan.AllDesiredAdGroups.Count == 0
            && plan.UnresolvedRuleReferences.SequenceEqual(
                ["VPN-ACCESS:LEGACY:item-orphelin"],
                StringComparer.OrdinalIgnoreCase),
            "CAS 3 : un item de scope utilisateur sans utilisateur doit rester non resolu, jamais retomber sur les utilisateurs du client.");
    }

    // ------------------------------------------------------------------
    // CAS 4 : subscription_user sans identity_reference.
    // ------------------------------------------------------------------
    private static void VerifyUserWithoutIdentityReferenceFailsClosed()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                UserRule(
                    "item-additionnel",
                    "user-additionnel",
                    identityReference: null,
                    "VPN-ACCESS",
                    "GG_VPN")
            ]);

        Ensure(
            plan.Users.Count == 0
            && plan.UnresolvedRuleReferences.Count == 1,
            "CAS 4 : un utilisateur d'abonnement sans identite doit rester non resolu, ce qui est le cas de tout utilisateur supplementaire aujourd'hui.");
    }

    // ------------------------------------------------------------------
    // CAS 5 : identity_reference sans lien AD.
    // ------------------------------------------------------------------
    private static void VerifyIdentityWithoutAdLinkFailsClosed()
    {
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal),
            [AdLink("identity-a")]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == "BILLING_V2_PROVISIONING_IDENTITY_NOT_LINKED"
            && resolution.Targets.Count == 0,
            "CAS 5 : une identite sans lien Active Directory doit bloquer le provisioning.");
    }

    // ------------------------------------------------------------------
    // CAS 6 : plusieurs liens AD pour la meme identity_reference.
    // ------------------------------------------------------------------
    private static void VerifyAmbiguousAdLinkFailsClosed()
    {
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLink("identity-a", CustomerId, "svc.identity-a"),
                    PortalLink("identity-a", CustomerId, "svc.identity-a-bis")
                ]
            },
            [AdLink("identity-a"), AdLink("identity-a-bis")]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == "BILLING_V2_PROVISIONING_IDENTITY_AMBIGUOUS",
            "CAS 6 : deux liens Active Directory pour une meme identite ne doivent jamais etre tranches en silence.");
    }

    // ------------------------------------------------------------------
    // CAS 7 : lien AD appartenant a un autre customer_id.
    // ------------------------------------------------------------------
    private static void VerifyAdLinkOfAnotherCustomerFailsClosed()
    {
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLink("identity-a", OtherCustomerId, "svc.identity-a")
                ]
            },
            [AdLink("identity-a")]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == "BILLING_V2_PROVISIONING_IDENTITY_CUSTOMER_MISMATCH",
            "CAS 7 : un lien Active Directory rattache a un autre client doit bloquer le provisioning.");
    }

    private static void VerifyTwoSubscriptionUsersSharingOneIdentityFailClosed()
    {
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [
                DesiredState("user-a", "identity-a", ["GG_VPN"]),
                DesiredState("user-b", "identity-b", ["GG_RDS"])
            ],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLink("identity-a", CustomerId, "svc.identity-a")
                ],
                ["identity-b"] =
                [
                    PortalLink("identity-b", CustomerId, "svc.identity-a")
                ]
            },
            [AdLink("identity-a")]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == "BILLING_V2_PROVISIONING_IDENTITY_AMBIGUOUS",
            "Deux utilisateurs d'abonnement resolus vers le meme compte Active Directory cumuleraient leurs droits : cela doit bloquer.");
    }

    // ------------------------------------------------------------------
    // CAS 8 : quota personnel sans identite.
    // ------------------------------------------------------------------
    private static void VerifyPersonalQuotaWithoutIdentityFailsClosed()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                QuotaRule(
                    "item-stockage",
                    "user-additionnel",
                    identityReference: null,
                    "koxo_user_storage",
                    scopeType: "user")
            ]);

        Ensure(
            plan.Users.Count == 0
            && plan.StorageQuotaPlans.Count == 0
            && plan.UnresolvedRuleReferences.Count == 1
            && plan.Blockers.Count == 1
            && plan.Blockers[0].ReasonCode
                == BillingV2ProvisioningBlockerReasons.IdentityRequired,
            "CAS 8 : un quota personnel sans identite ne doit produire aucun plan de quota.");
    }

    private static void VerifyPersonalQuotaKeepsUserScopeAndStaysBlocked()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                QuotaRule(
                    "item-stockage",
                    "user-a",
                    "identity-a",
                    "koxo_user_storage",
                    scopeType: "user")
            ]);

        Ensure(
            plan.UnresolvedRuleReferences.Count == 0
            && plan.StorageQuotaPlans.Count == 1
            && plan.StorageQuotaPlans[0].SubscriptionUserId == "user-a"
            && plan.StorageQuotaPlans[0].IdentityReference == "identity-a"
            && plan.AllDesiredAdGroups.Count == 0,
            "Un quota de stockage personnel doit rester rattache a son utilisateur et ne jamais devenir un groupe AD.");

        // Le chemin reel du quota passe par KoXo puis le systeme de fichiers :
        // disposer de l'identite ne rend rien applicable pour autant.
        var readiness = DormantBillingV2KoxoStorageProvider.Instance
            .CheckReadiness(plan.StorageQuotaPlans);

        Ensure(
            !readiness.CanApplyQuotas
            && readiness.ReasonCode
                == "BILLING_V2_KOXO_STORAGE_PROVIDER_NOT_CONFIGURED",
            "Connaitre l'identite d'un quota ne doit pas lever le blocage : aucun provider de stockage n'existe.");
    }

    // ------------------------------------------------------------------
    // CAS 9 : service actif sans provisioning rule.
    // ------------------------------------------------------------------
    private static void VerifyActiveServiceWithoutRuleStaysUnresolved()
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

        Ensure(
            plan.UnresolvedRuleReferences.SequenceEqual(
                ["BASE-SERVICE:no-tier:item-base"],
                StringComparer.OrdinalIgnoreCase),
            "CAS 9 : un service actif sans regle de provisioning doit rester non resolu, y compris BASE-SERVICE.");
    }

    // ------------------------------------------------------------------
    // CAS 10 : scope abonnement portant une action AD.
    // ------------------------------------------------------------------
    private static void VerifySubscriptionScopedAdGroupFailsClosed()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                new BillingV2ProvisioningRuleProjection(
                    SubscriptionId,
                    "item-partage",
                    "SERVICE-PARTAGE",
                    "LEGACY",
                    "ad_group_membership",
                    "ad_group",
                    "GG_PARTAGE",
                    "static",
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

        Ensure(
            plan.Users.Count == 0
            && plan.AllDesiredAdGroups.Count == 0
            && plan.UnresolvedRuleReferences.SequenceEqual(
                ["SERVICE-PARTAGE:LEGACY:item-partage"],
                StringComparer.OrdinalIgnoreCase),
            "CAS 10 : un droit AD de scope abonnement n'a pas de titulaire defini et ne doit pas etre distribue a tous les utilisateurs du client.");
    }

    // ------------------------------------------------------------------
    // CAS 11 : retry du meme provisioning.
    // ------------------------------------------------------------------
    private static async Task VerifyRetryIsIdempotent()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                StorageRule("item-a-stockage", "user-a", "identity-a"),
                StorageRule("item-b-stockage", "user-b", "identity-b"),
                UserRule("item-a-vpn", "user-a", "identity-a", "VPN-ACCESS", "GG_VPN"),
                UserRule("item-b-rds", "user-b", "identity-b", "RDS-ACCESS", "GG_RDS")
            ]);

        var provisioner = new RecordingAdGroupProvisioner();
        var first = await ExecuteAsync(plan, ["identity-a", "identity-b"], provisioner);
        var retry = await ExecuteAsync(plan, ["identity-a", "identity-b"], provisioner);

        Ensure(
            first.Result.Changed
            && first.Result.ResultCode == "PROVISIONING_APPLIED",
            "CAS 11 : la premiere execution doit appliquer les droits.");
        Ensure(
            !retry.Result.Changed
            && retry.Result.ResultCode == "PROVISIONING_UNCHANGED",
            "CAS 11 : un retry a l'identique doit etre idempotent.");
        Ensure(
            provisioner.Memberships.Count == 2,
            "CAS 11 : un retry ne doit creer aucune appartenance supplementaire.");
        Ensure(
            retry.Result.Operations.All(operation =>
                operation.Code == "AD_GROUP_MEMBER_ALREADY_PRESENT"),
            "CAS 11 : le retry doit constater des appartenances deja presentes.");
    }

    // ------------------------------------------------------------------
    // CAS 12 : mode add-only.
    // ------------------------------------------------------------------
    private static async Task VerifyAddOnlyModeNeverRemoves()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                StorageRule("item-a-stockage", "user-a", "identity-a"),
                UserRule("item-a-vpn", "user-a", "identity-a", "VPN-ACCESS", "GG_VPN")
            ]);

        var execution = await ExecuteAsync(plan, ["identity-a"]);

        Ensure(
            execution.Result.Operations.All(operation =>
                operation.Operation == "add"),
            "CAS 12 : le mode add-only ne doit produire aucune operation de retrait.");
        Ensure(
            execution.Provisioner.Removed.Count == 0,
            "CAS 12 : aucun retrait ne doit atteindre l'annuaire.");
        Ensure(
            execution.Requests.All(request =>
                request.ManagedGroupSamAccountNames.All(group =>
                    request.DesiredGroupSamAccountNames.Contains(
                        group,
                        StringComparer.OrdinalIgnoreCase))),
            "CAS 12 : les groupes geres doivent rester bornes aux groupes desires du meme utilisateur, ce qui rend le retrait structurellement impossible.");
    }

    private static void VerifyEveryExecutionRequestCarriesExactlyOneUser()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                StorageRule("item-a-stockage", "user-a", "identity-a"),
                StorageRule("item-b-stockage", "user-b", "identity-b"),
                StorageRule("item-c-stockage", "user-c", "identity-c"),
                UserRule("item-a-vpn", "user-a", "identity-a", "VPN-ACCESS", "GG_VPN"),
                UserRule("item-a-rds", "user-a", "identity-a", "RDS-ACCESS", "GG_RDS"),
                UserRule("item-b-vpn", "user-b", "identity-b", "VPN-ACCESS", "GG_VPN"),
                UserRule("item-c-rds", "user-c", "identity-c", "RDS-ACCESS", "GG_RDS")
            ]);
        var resolution = Resolve(plan, ["identity-a", "identity-b", "identity-c"]);
        var requests = BillingV2ProvisioningExecutionPlanner.BuildPerUserRequests(
            BillingV2ProvisioningGateDecision.Allow(addOnlyMode: true),
            resolution.Targets,
            GroupDistinguishedNames);

        Ensure(
            requests.Count == 3
            && requests.All(request => request.TargetUsers.Count == 1),
            "Chaque requete de reconciliation V2 ne doit porter qu'un seul utilisateur : c'est l'invariant qui empeche le produit cartesien du moteur AD de croiser les droits.");
        Ensure(
            plan.AllDesiredAdGroups.SequenceEqual(
                ["GG_RDS", "GG_VPN"],
                StringComparer.OrdinalIgnoreCase),
            "L'enveloppe client doit bien contenir les deux groupes achetes par le client.");
        Ensure(
            DesiredGroupsFor(requests, "svc.identity-b").SequenceEqual(
                ["GG_VPN"],
                StringComparer.OrdinalIgnoreCase)
            && DesiredGroupsFor(requests, "svc.identity-c").SequenceEqual(
                ["GG_RDS"],
                StringComparer.OrdinalIgnoreCase),
            "L'union des groupes du client ne doit jamais servir de droit desire a un utilisateur qui ne les a pas tous achetes.");
    }

    // ------------------------------------------------------------------
    // Outillage
    // ------------------------------------------------------------------
    private static IReadOnlyDictionary<string, string?> GroupDistinguishedNames =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GG_VPN"] = "CN=GG_VPN,OU=Groupes_TEST,DC=clients,DC=home,DC=bzh",
            ["GG_RDS"] = "CN=GG_RDS,OU=Groupes_TEST,DC=clients,DC=home,DC=bzh"
        };

    private static BillingV2ProvisioningRuleProjection UserRule(
        string subscriptionItemId,
        string? subscriptionUserId,
        string? identityReference,
        string serviceCode,
        string groupSamAccountName)
        => new(
            SubscriptionId,
            subscriptionItemId,
            serviceCode,
            "LEGACY",
            "ad_group_membership",
            "ad_group",
            groupSamAccountName,
            "static",
            StaticValue: null,
            TierNumericValue: null,
            TierUnit: null,
            Quantity: 1,
            ScopeType: "user",
            subscriptionUserId,
            identityReference,
            SubscriptionUserIsPrimary: true,
            SubscriptionUserStatus: subscriptionUserId is null ? null : "active");

    /// <summary>
    /// Socle technique d'un utilisateur : stockage personnel.
    /// </summary>
    /// <remarks>
    /// Depuis la phase 2B, un acces VPN ou RDS suppose que l'environnement
    /// utilisateur, donc le compte annuaire, soit provisionne. Les scenarios
    /// qui attendent un plan resolu doivent donc fournir ce socle, comme le
    /// ferait un vrai abonnement.
    /// </remarks>
    private static BillingV2ProvisioningRuleProjection StorageRule(
        string subscriptionItemId,
        string subscriptionUserId,
        string identityReference)
        => QuotaRule(
            subscriptionItemId,
            subscriptionUserId,
            identityReference,
            "koxo_user_storage",
            scopeType: "user");

    private static BillingV2ProvisioningRuleProjection QuotaRule(
        string subscriptionItemId,
        string? subscriptionUserId,
        string? identityReference,
        string targetType,
        string scopeType,
        long numericValue = 128,
        string? tierUnit = "GiB")
        => new(
            SubscriptionId,
            subscriptionItemId,
            targetType == "koxo_secondary_group_storage"
                ? "STORAGE-SHARED"
                : "STORAGE-PERSONAL",
            $"{numericValue}",
            "infrastructure_action",
            targetType,
            TargetReference: null,
            "tier_numeric_value",
            StaticValue: null,
            TierNumericValue: numericValue,
            tierUnit,
            Quantity: 1,
            scopeType,
            subscriptionUserId,
            identityReference,
            SubscriptionUserIsPrimary: true,
            SubscriptionUserStatus: subscriptionUserId is null ? null : "active");

    private static BillingV2UserDesiredState DesiredState(
        string subscriptionUserId,
        string identityReference,
        IReadOnlyList<string> desiredAdGroups)
        => new(
            subscriptionUserId,
            identityReference,
            desiredAdGroups,
            PersonalStorage: null,
            Array.Empty<BillingV2AcknowledgedEntitlement>(),
            Array.Empty<BillingV2AcknowledgedEntitlement>());

    // ------------------------------------------------------------------
    // CAS A : meme sAMAccountName, objectGUID differents.
    // ------------------------------------------------------------------
    private static void VerifySameSamAccountNameIsDisambiguatedByObjectGuid()
    {
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLinkWith(
                        "identity-a",
                        CustomerId,
                        "jdupont",
                        HomonymGuidA,
                        HomonymSidA)
                ]
            },
            [
                AdLinkWith("jdupont", HomonymGuidB, HomonymSidB),
                AdLinkWith("jdupont", HomonymGuidA, HomonymSidA)
            ]);

        Ensure(
            resolution.Resolved && resolution.Targets.Count == 1,
            "CAS A : deux homonymes ne doivent pas rendre la resolution ambigue quand l'objectGUID les separe.");
        Ensure(
            resolution.Targets[0].AdLink.ObjectGuid == HomonymGuidA,
            "CAS A : seule l'identite dont l'objectGUID correspond a identity_reference doit etre ciblee.");
    }

    // ------------------------------------------------------------------
    // CAS B : objectGUID attendu absent du referentiel client.
    // ------------------------------------------------------------------
    private static void VerifyMissingObjectGuidInCustomerReferentialFailsClosed()
    {
        // Le referentiel client contient un homonyme parfait : sous une
        // correlation par sAMAccountName, ce compte aurait ete cible a tort.
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLinkWith(
                        "identity-a",
                        CustomerId,
                        "jdupont",
                        HomonymGuidA,
                        HomonymSidA)
                ]
            },
            [AdLinkWith("jdupont", HomonymGuidB, HomonymSidB)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == "BILLING_V2_PROVISIONING_IDENTITY_NOT_LINKED"
            && resolution.Targets.Count == 0,
            "CAS B : un objectGUID absent du referentiel du client doit bloquer, jamais retomber sur un homonyme.");
    }

    // ------------------------------------------------------------------
    // CAS C : deux instantanes SQL incoherents pour le meme objectGUID.
    // ------------------------------------------------------------------
    private static void VerifyIncoherentSnapshotsFailClosed()
    {
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLinkWith(
                        "identity-a",
                        CustomerId,
                        "jdupont",
                        HomonymGuidA,
                        HomonymSidA)
                ]
            },
            [AdLinkWith("jdupont", HomonymGuidA, HomonymSidB)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == "BILLING_V2_PROVISIONING_IDENTITY_SID_MISMATCH",
            "CAS C : deux lectures divergentes du meme objet decrivent un etat dont on ignore lequel est a jour, donc le cycle doit s'arreter.");
    }

    // ------------------------------------------------------------------
    // CAS H : deplacement inter-domaines, les deux sources rafraichies.
    // ------------------------------------------------------------------
    private static void VerifyRefreshedSnapshotsAfterSidChangeResolve()
    {
        // Un deplacement entre deux domaines d'une meme foret change
        // legitimement l'objectSid et conserve l'objectGUID. Une fois les deux
        // lectures rafraichies, la resolution doit repartir : le refus du CAS C
        // est temporaire, jamais definitif.
        const string sidApresDeplacement =
            "S-1-5-21-2110995556-1274434293-847283962-5402";

        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLinkWith(
                        "identity-a",
                        CustomerId,
                        "jdupont",
                        HomonymGuidA,
                        sidApresDeplacement)
                ]
            },
            [AdLinkWith("jdupont", HomonymGuidA, sidApresDeplacement)]);

        Ensure(
            resolution.Resolved
            && resolution.Targets.Count == 1
            && resolution.Targets[0].AdLink.ObjectGuid == HomonymGuidA,
            "CAS H : un objectSid change mais coherent dans les deux sources ne doit pas bloquer, sinon un utilisateur deplace de domaine reste bloque a vie.");
    }

    // ------------------------------------------------------------------
    // CAS E : objectGUID malforme cote lien portail.
    // ------------------------------------------------------------------
    private static void VerifyMalformedPortalObjectGuidFailsClosed()
    {
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLinkWith(
                        "identity-a",
                        CustomerId,
                        "jdupont",
                        OpaqueGuid,
                        HomonymSidA)
                ]
            },
            // Le referentiel porte la meme chaine opaque : une comparaison
            // textuelle aurait donc conclu a une correspondance.
            [AdLinkWith("jdupont", OpaqueGuid, HomonymSidA)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == "BILLING_V2_PROVISIONING_IDENTITY_GUID_INVALID"
            && resolution.Targets.Count == 0,
            "CAS E : une chaine opaque ne doit jamais servir de cle d'identite, meme presente a l'identique des deux cotes.");
    }

    // ------------------------------------------------------------------
    // CAS F : objectGUID malforme cote referentiel client.
    // ------------------------------------------------------------------
    private static void VerifyMalformedCustomerObjectGuidNeverMatches()
    {
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLinkWith(
                        "identity-a",
                        CustomerId,
                        "jdupont",
                        HomonymGuidA,
                        HomonymSidA)
                ]
            },
            [AdLinkWith("jdupont", OpaqueGuid, HomonymSidA)]);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == "BILLING_V2_PROVISIONING_IDENTITY_NOT_LINKED"
            && resolution.Targets.Count == 0,
            "CAS F : un candidat sans objectGUID exploitable ne doit jamais etre selectionne, meme homonyme du compte attendu.");
    }

    // ------------------------------------------------------------------
    // CAS G : meme GUID ecrit avec accolades et en majuscules.
    // ------------------------------------------------------------------
    private static void VerifyObjectGuidWritingFormIsCanonicalized()
    {
        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLinkWith(
                        "identity-a",
                        CustomerId,
                        "jdupont",
                        $"{{{HomonymGuidA.ToUpperInvariant()}}}",
                        HomonymSidA)
                ]
            },
            [AdLinkWith("jdupont", HomonymGuidA, HomonymSidA)]);

        Ensure(
            resolution.Resolved
            && resolution.Targets.Count == 1
            && resolution.Targets[0].AdLink.ObjectGuid == HomonymGuidA,
            "CAS G : la forme d'ecriture d'un objectGUID valide ne doit pas changer l'identite designee.");
    }

    // ------------------------------------------------------------------
    // CAS D : meme sAMAccountName dans deux domaines distincts.
    // ------------------------------------------------------------------
    private static void VerifySameSamAccountNameAcrossDomainsResolvesTheRightObject()
    {
        const string childDomainDn =
            "CN=jdupont,OU=KoXoAdm,DC=clients,DC=home,DC=bzh";
        const string rootDomainDn = "CN=jdupont,OU=Utilisateurs,DC=home,DC=bzh";

        var resolution = BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            [DesiredState("user-a", "identity-a", ["GG_VPN"])],
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal)
            {
                ["identity-a"] =
                [
                    PortalLinkWith(
                        "identity-a",
                        CustomerId,
                        "jdupont",
                        HomonymGuidB,
                        HomonymSidB,
                        rootDomainDn)
                ]
            },
            [
                AdLinkWith(
                    "jdupont",
                    HomonymGuidA,
                    HomonymSidA,
                    childDomainDn),
                AdLinkWith("jdupont", HomonymGuidB, HomonymSidB, rootDomainDn)
            ]);

        Ensure(
            resolution.Resolved && resolution.Targets.Count == 1,
            "CAS D : un sAMAccountName present dans deux domaines ne doit produire aucune ambiguite.");
        Ensure(
            resolution.Targets[0].AdLink.DistinguishedName == rootDomainDn
            && resolution.Targets[0].AdLink.ObjectGuid == HomonymGuidB,
            "CAS D : c'est l'objet du bon domaine, designe par son objectGUID, qui doit etre cible.");
    }

    private static PortalUserAdLinkRecord PortalLinkWith(
        string identityReference,
        string customerId,
        string samAccountName,
        string objectGuid,
        string objectSid,
        string? distinguishedName = null)
        => new(
            $"link-{objectGuid}",
            customerId,
            "CLI-000001",
            identityReference,
            objectGuid,
            objectSid,
            samAccountName,
            $"{samAccountName}@clients.home.bzh",
            samAccountName,
            distinguishedName
                ?? $"CN={samAccountName},OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
            "clients.home.bzh",
            "provisioned",
            AdProvisionedAtUtc: null,
            LastPasswordSyncAtUtc: null,
            LastPasswordSyncStatus: null,
            KoxoExportStatus: null);

    private static CustomerAdLinkSummary AdLinkWith(
        string samAccountName,
        string objectGuid,
        string objectSid,
        string? distinguishedName = null)
        => new(
            $"link-{objectGuid}",
            "CLI-000001",
            objectGuid,
            objectSid,
            "user",
            samAccountName,
            $"{samAccountName}@clients.home.bzh",
            samAccountName,
            distinguishedName
                ?? $"CN={samAccountName},OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
            "2026-08-17T00:00:00Z",
            LinkedBy: null);

    /// <summary>
    /// Fabrique un objectGUID valide et stable a partir d'une graine de test.
    /// </summary>
    /// <remarks>
    /// La resolution rejette desormais toute valeur non parsable : les
    /// identifiants de confort du type <c>guid-svc.identity-a</c> ne sont plus
    /// des cles d'identite recevables, et les fixtures doivent donc porter de
    /// vrais GUID. La derivation reste deterministe pour que deux graines
    /// distinctes donnent deux identites distinctes, reproductibles d'une
    /// execution a l'autre.
    /// </remarks>
    private static string TestObjectGuid(string seed)
    {
        var bytes = new byte[16];
        for (var position = 0; position < bytes.Length; position++)
        {
            var hash = 0xcbf29ce484222325UL;
            foreach (var character in $"{seed}#{position}")
            {
                hash ^= character;
                hash *= 0x100000001b3UL;
            }

            bytes[position] = (byte)(hash & 0xFF);
        }

        return new Guid(bytes).ToString("D");
    }

    private static PortalUserAdLinkRecord PortalLink(
        string identityReference,
        string customerId,
        string samAccountName)
        => new(
            $"link-{samAccountName}",
            customerId,
            "CLI-000001",
            identityReference,
            TestObjectGuid(samAccountName),
            $"sid-{samAccountName}",
            samAccountName,
            $"{samAccountName}@clients.home.bzh",
            samAccountName,
            $"CN={samAccountName},OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
            "clients.home.bzh",
            "provisioned",
            AdProvisionedAtUtc: null,
            LastPasswordSyncAtUtc: null,
            LastPasswordSyncStatus: null,
            KoxoExportStatus: null);

    private static CustomerAdLinkSummary AdLink(string identityReference)
        => new(
            $"link-svc.{identityReference}",
            "CLI-000001",
            TestObjectGuid($"svc.{identityReference}"),
            $"sid-svc.{identityReference}",
            "user",
            $"svc.{identityReference}",
            $"svc.{identityReference}@clients.home.bzh",
            $"svc.{identityReference}",
            $"CN=svc.{identityReference},OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
            "2026-08-17T00:00:00Z",
            LinkedBy: null);

    private static BillingV2ProvisioningTargetResolution Resolve(
        BillingV2ProvisioningPlan plan,
        IReadOnlyList<string> customerIdentityReferences)
    {
        var linksByIdentityReference =
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal);
        foreach (var identityReference in customerIdentityReferences)
        {
            linksByIdentityReference[identityReference] =
            [
                PortalLink(
                    identityReference,
                    CustomerId,
                    $"svc.{identityReference}")
            ];
        }

        return BillingV2ProvisioningIdentityResolver.Resolve(
            CustomerId,
            plan.Users,
            linksByIdentityReference,
            customerIdentityReferences.Select(AdLink).ToArray());
    }

    private static async Task<ExecutionOutcome> ExecuteAsync(
        BillingV2ProvisioningPlan plan,
        IReadOnlyList<string> customerIdentityReferences,
        RecordingAdGroupProvisioner? provisioner = null)
    {
        var resolution = Resolve(plan, customerIdentityReferences);
        Ensure(
            resolution.Resolved,
            $"L'outillage de test attend une resolution d'identite reussie, obtenu {resolution.ReasonCode}.");

        var decision = BillingV2ProvisioningGateDecision.Allow(addOnlyMode: true);
        var requests = BillingV2ProvisioningExecutionPlanner.BuildPerUserRequests(
            decision,
            resolution.Targets,
            GroupDistinguishedNames);

        var effectiveProvisioner = provisioner ?? new RecordingAdGroupProvisioner();
        var provisioningService = new ProvisioningService(
            effectiveProvisioner,
            ProvisioningConfiguration());

        var results = new List<ProvisioningExecutionResult>(requests.Count);
        foreach (var request in requests)
        {
            var result = await provisioningService.ReconcileAsync(
                request,
                CancellationToken.None);
            results.Add(result);
            if (!result.Succeeded)
            {
                break;
            }
        }

        return new ExecutionOutcome(
            requests,
            BillingV2ProvisioningResultAggregator.Combine(results),
            effectiveProvisioner);
    }

    private static SubscriptionProvisioningRuntimeConfiguration
        ProvisioningConfiguration()
        => new(
            new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            GroupDistinguishedNames.ToDictionary(
                entry => entry.Key,
                entry => entry.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase),
            MaxAttempts: 3,
            RetryDelayMs: 0);

    private static IReadOnlyList<string> GroupsOf(
        BillingV2ProvisioningPlan plan,
        string subscriptionUserId)
        => plan.Users
            .Single(user => user.SubscriptionUserId == subscriptionUserId)
            .DesiredAdGroups;

    private static IReadOnlyList<string> DesiredGroupsFor(
        IReadOnlyList<ProvisioningExecutionRequest> requests,
        string samAccountName)
        => requests
            .Single(request =>
                request.TargetUsers[0].SamAccountName == samAccountName)
            .DesiredGroupSamAccountNames;

    private static IReadOnlyList<string> AppliedGroups(
        ExecutionOutcome execution,
        string samAccountName)
        => execution.Provisioner.Added
            .Where(operation => operation.UserSamAccountName == samAccountName)
            .Select(operation => operation.GroupSamAccountName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record ExecutionOutcome(
        IReadOnlyList<ProvisioningExecutionRequest> Requests,
        ProvisioningExecutionResult Result,
        RecordingAdGroupProvisioner Provisioner);

    private sealed record RecordedGroupOperation(
        string UserSamAccountName,
        string GroupSamAccountName);

    /// <summary>
    /// Provisionneur d'annuaire enregistreur, idempotent comme le vrai.
    /// </summary>
    private sealed class RecordingAdGroupProvisioner : IAdGroupProvisioner
    {
        public List<RecordedGroupOperation> Added { get; } = [];

        public List<RecordedGroupOperation> Removed { get; } = [];

        public HashSet<string> Memberships { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public string ModeName => "recording";

        public bool RequiresConfiguredGroupDistinguishedNames => true;

        public Task<AdGroupProvisionerResult> AddUserToGroupAsync(
            CustomerAdLinkSummary user,
            string groupSamAccountName,
            string? groupDistinguishedName,
            CancellationToken cancellationToken)
        {
            Added.Add(new RecordedGroupOperation(
                user.SamAccountName,
                groupSamAccountName));
            var added = Memberships.Add(
                $"{user.SamAccountName}|{groupSamAccountName}");
            return Task.FromResult(new AdGroupProvisionerResult(
                200,
                added
                    ? "AD_GROUP_MEMBER_ADDED"
                    : "AD_GROUP_MEMBER_ALREADY_PRESENT",
                "OK",
                Changed: added));
        }

        public Task<AdGroupProvisionerResult> RemoveUserFromGroupAsync(
            CustomerAdLinkSummary user,
            string groupSamAccountName,
            string? groupDistinguishedName,
            CancellationToken cancellationToken)
        {
            Removed.Add(new RecordedGroupOperation(
                user.SamAccountName,
                groupSamAccountName));
            var removed = Memberships.Remove(
                $"{user.SamAccountName}|{groupSamAccountName}");
            return Task.FromResult(new AdGroupProvisionerResult(
                200,
                "AD_GROUP_MEMBER_REMOVED",
                "OK",
                Changed: removed));
        }

        public Task<AdDirectoryObjectSummary?> ResolveUserByEmployeeNumberAsync(
            string employeeNumber,
            CancellationToken cancellationToken)
            => Task.FromResult<AdDirectoryObjectSummary?>(null);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
