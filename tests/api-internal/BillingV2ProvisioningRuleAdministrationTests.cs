using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Contrat local, sans MariaDB ni annuaire : le backoffice n'accepte que le
/// couple de regle effectivement interprete par le planner et ce dernier
/// conserve une isolation par identite.
/// </summary>
public static class BillingV2ProvisioningRuleAdministrationTests
{
    public static void Run()
    {
        VerifyAdGroupRuleIsClassifiedAsUserScoped();
        VerifyUnknownAndIncompatibleRulesAreRejected();
        VerifyCreateAndUpdateAdGroupEnvironmentIsolation();
        VerifyPlannerConsumesTheAuthorizedRule();
        Console.WriteLine("Tests administration des regles de provisioning Billing V2 reussis.");
    }

    private static void VerifyAdGroupRuleIsClassifiedAsUserScoped()
    {
        Ensure(BillingV2ProvisioningRuleSemantics.IsKnownRuleType("ad_group_membership"),
            "La regle administree doit faire partie du vocabulaire ferme.");
        Ensure(BillingV2ProvisioningRuleSemantics.TryClassify(
                "ad_group_membership", "ad_group", out var kind, out var scope)
            && kind == BillingV2ProvisioningRuleKind.AdGroupMembership
            && scope == BillingV2ProvisioningRuleScope.User,
            "Le groupe AD doit rester une ressource utilisateur.");
    }

    private static void VerifyUnknownAndIncompatibleRulesAreRejected()
    {
        Ensure(!BillingV2ProvisioningRuleSemantics.IsKnownRuleType("unknown_rule"),
            "Un type inconnu ne doit jamais recevoir une interpretation implicite.");
        Ensure(!BillingV2ProvisioningRuleSemantics.TryClassify(
                "ad_group_membership", "platform", out _, out _),
            "Une cible incompatible ne doit pas etre acceptee pour un groupe AD.");
        Ensure(BillingV2ProvisioningRuleSemantics.TryClassify(
                "ad_group_membership", "ad_group", out _, out var scope)
            && scope != BillingV2ProvisioningRuleScope.Subscription,
            "La portee abonnement est interdite pour une appartenance AD.");
    }

    private static void VerifyCreateAndUpdateAdGroupEnvironmentIsolation()
    {
        Ensure(DeploymentEnvironmentGuard.TryValidateAdGroupTarget(
                DeploymentEnvironment.Development, "GG_SERVICE_E2E_DEV", out _),
            "CREATE DEV doit accepter le groupe E2E DEV.");
        Ensure(!DeploymentEnvironmentGuard.TryValidateAdGroupTarget(
                DeploymentEnvironment.Development, "GG_VPN", out var devVpnReason)
            && devVpnReason == "AD_GROUP_TARGET_OUTSIDE_DEVELOPMENT",
            "CREATE DEV doit refuser GG_VPN, groupe hors perimetre DEV.");
        Ensure(!DeploymentEnvironmentGuard.TryValidateAdGroupTarget(
                DeploymentEnvironment.Development, "GG_RDS", out var devRdsReason)
            && devRdsReason == "AD_GROUP_TARGET_OUTSIDE_DEVELOPMENT",
            "UPDATE DEV doit refuser GG_RDS, groupe hors perimetre DEV.");
        Ensure(!DeploymentEnvironmentGuard.TryValidateAdGroupTarget(
                DeploymentEnvironment.Production, "GG_SERVICE_E2E_DEV", out var productionReason)
            && productionReason == "AD_GROUP_TARGET_DEVELOPMENT_FORBIDDEN",
            "CREATE/UPDATE PROD doit refuser une cible _DEV.");
        Ensure(DeploymentEnvironmentGuard.TryValidateAdGroupTarget(
                DeploymentEnvironment.Production, "GG_VPN", out _),
            "La PROD doit conserver ses groupes non DEV.");
    }

    private static void VerifyPlannerConsumesTheAuthorizedRule()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                new BillingV2ProvisioningRuleProjection(
                    "11111111-1111-1111-1111-111111111111",
                    "item-vpn",
                    "VPN-ACCESS",
                    TierCode: null,
                    RuleType: "ad_group_membership",
                    TargetType: "ad_group",
                    TargetReference: "GG_VPN",
                    ValueSource: "none",
                    StaticValue: null,
                    TierNumericValue: null,
                    TierUnit: null,
                    Quantity: 1,
                    ScopeType: "user",
                    SubscriptionUserId: "user-a",
                    IdentityReference: "CLI-000001",
                    SubscriptionUserIsPrimary: true,
                    SubscriptionUserStatus: "active")
            ]);

        Ensure(plan.Users.Count == 1
            && plan.Users[0].DesiredAdGroups.Single() == "GG_VPN",
            "Le planner doit prendre en compte la regle administree sans configuration parallele.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
