using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

public static class BillingV2ProvisioningShadowTests
{
    public static Task RunAsync()
    {
        VerifyTwentyLegacyPacksResolveExpectedAdGroups();
        VerifyMissingV2RuleWouldBeDetected();
        VerifyClientServiceCatalogShadowCoversLegacyReferences();
        VerifyClientServiceCatalogShadowDetectsMissingMapping();
        VerifyClientServiceCatalogV2EntitlementUsesLegacyReferenceWhenMapped();
        VerifyProvisioningReadinessAllowsOnlyCompleteReadyMatch();
        VerifyProvisioningReadinessDeniesMismatch();
        VerifyProvisioningReadinessDeniesIncompleteMaterialization();
        VerifyProvisioningReadinessDeniesUnknownRuleOrGroup();
        VerifyProvisioningReadinessDeniesFlagOff();
        VerifyFirstActivationIsAddOnly();
        VerifyProvisioningRetryKeepsSameGateDecision();
        VerifyProvisioningPlannerFlagsMissingItemProvisioning();
        VerifyNextcloudQuotaRulesAreCalculatedButNotAdGroups();
        VerifyDormantNextcloudQuotaProviderBlocksExecution();
        return Task.CompletedTask;
    }

    private static void VerifyTwentyLegacyPacksResolveExpectedAdGroups()
    {
        var store = new MockCommercialStore();
        var packs = store.Offers
            .Where(offer => offer.ExternalReference?.StartsWith(
                "PACK-",
                StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(offer => offer.ExternalReference, StringComparer.Ordinal)
            .ToArray();

        Ensure(packs.Length == 20, "Le shadow provisioning doit couvrir les 20 PACK-* legacy.");

        foreach (var pack in packs)
        {
            var actual = BillingV2ProvisioningShadowCalculator.ResolveAdGroups(
                pack.TechnicalServiceReferences,
                V2AdRules);
            var expected = ExpectedGroups(pack.ExternalReference);
            Ensure(
                actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase),
                $"{pack.ExternalReference}: le shadow V2 doit produire les memes groupes AD que le legacy.");
        }
    }

    private static void VerifyMissingV2RuleWouldBeDetected()
    {
        var actual = BillingV2ProvisioningShadowCalculator.ResolveAdGroups(
            ["ACCES-VPN", "ACCES-RDS"],
            [V2AdRules.Single(rule => rule.LegacyServiceReference == "ACCES-VPN")]);

        var missing = new[] { "GG_RDS", "GG_VPN" }
            .Except(actual, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Ensure(
            missing.SequenceEqual(["GG_RDS"], StringComparer.OrdinalIgnoreCase),
            "Le shadow provisioning doit detecter une regle V2 manquante.");
    }

    private static void VerifyClientServiceCatalogShadowCoversLegacyReferences()
    {
        var comparison =
            BillingV2ClientServiceCatalogShadowCalculator.Compare(
                ["ACCES-VPN", "ACCES-RDS", "DOC-TECH"],
                V2ServiceRules);

        Ensure(
            comparison.Enabled
            && comparison.Succeeded
            && comparison.IsCovered
            && comparison.MappedV2ServiceCodes.SequenceEqual(
                ["RDS-ACCESS", "VPN-ACCESS"],
                StringComparer.OrdinalIgnoreCase)
            && comparison.IgnoredLegacyEntitlementReferences.SequenceEqual(
                ["DOC-TECH"],
                StringComparer.OrdinalIgnoreCase),
            "Le shadow catalogue services doit couvrir les droits legacy et isoler les droits ponctuels sans item recurrent V2.");
    }

    private static void VerifyClientServiceCatalogShadowDetectsMissingMapping()
    {
        var comparison =
            BillingV2ClientServiceCatalogShadowCalculator.Compare(
                ["ACCES-VPN", "SERVICE-INCONNU"],
                V2ServiceRules);

        Ensure(
            !comparison.IsCovered
            && comparison.UnsupportedLegacyServiceReferences.SequenceEqual(
                ["SERVICE-INCONNU"],
                StringComparer.OrdinalIgnoreCase),
            "Le shadow catalogue services doit signaler une reference legacy sans mapping V2.");
    }

    private static void
        VerifyClientServiceCatalogV2EntitlementUsesLegacyReferenceWhenMapped()
    {
        Ensure(
            BillingV2ClientServiceEntitlementPolicy
                .ResolveTechnicalServiceReference("ACCES-VPN", "VPN-ACCESS")
                == "ACCES-VPN"
            && BillingV2ClientServiceEntitlementPolicy
                .ResolveTechnicalServiceReference(null, "VPN-ACCESS")
                == "VPN-ACCESS",
            "La projection catalogue client V2 doit preferer la reference technique legacy mappee et fallback sur le code service V2.");
    }

    private static void VerifyProvisioningReadinessAllowsOnlyCompleteReadyMatch()
    {
        var decision = BillingV2ProvisioningReadinessGate.Evaluate(
            ReadyState());

        Ensure(
            decision.Authorized
            && decision.AddOnlyMode
            && decision.ReasonCode == "BILLING_V2_PROVISIONING_READY",
            "V2 complet, shadow success/match et client ready doivent autoriser une action V2 add-only.");
    }

    private static void VerifyProvisioningReadinessDeniesMismatch()
    {
        var decision = BillingV2ProvisioningReadinessGate.Evaluate(
            ReadyState(shadowMatchesLegacy: false));

        Ensure(
            !decision.Authorized
            && decision.ReasonCode == "BILLING_V2_PROVISIONING_SHADOW_NOT_MATCHING",
            "Un mismatch shadow doit interdire toute action V2.");
    }

    private static void VerifyProvisioningReadinessDeniesIncompleteMaterialization()
    {
        var decision = BillingV2ProvisioningReadinessGate.Evaluate(
            ReadyState(completeMaterialization: false));

        Ensure(
            !decision.Authorized
            && decision.ReasonCode == "BILLING_V2_PROVISIONING_INCOMPLETE_MATERIALIZATION",
            "Un abonnement legacy actif non materialise en V2 doit laisser le legacy autoritaire.");
    }

    private static void VerifyProvisioningReadinessDeniesUnknownRuleOrGroup()
    {
        var missingRule = BillingV2ProvisioningReadinessGate.Evaluate(
            ReadyState(requiredRulesResolved: false));
        var missingGroup = BillingV2ProvisioningReadinessGate.Evaluate(
            ReadyState(targetGroupsResolved: false));

        Ensure(
            !missingRule.Authorized
            && missingRule.ReasonCode == "BILLING_V2_PROVISIONING_RULES_UNRESOLVED"
            && !missingGroup.Authorized
            && missingGroup.ReasonCode == "BILLING_V2_PROVISIONING_TARGETS_UNRESOLVED",
            "Une regle ou un groupe cible inconnu doit bloquer le provisioning V2.");
    }

    private static void VerifyProvisioningReadinessDeniesFlagOff()
    {
        var decision = BillingV2ProvisioningReadinessGate.Evaluate(
            ReadyState(globalFlagEnabled: false));

        Ensure(
            !decision.Authorized
            && decision.ReasonCode == "BILLING_V2_PROVISIONING_FLAG_OFF",
            "Le flag global off doit interdire toute action V2 meme si le client est ready.");
    }

    private static void VerifyFirstActivationIsAddOnly()
    {
        var managedGroups =
            BillingV2ProvisioningExecutionPolicy.ResolveManagedGroupsForExecution(
                BillingV2ProvisioningGateDecision.Allow(addOnlyMode: true),
                ["GG_VPN"],
                ["GG_RDS", "GG_VPN"]);

        Ensure(
            managedGroups.SequenceEqual(["GG_VPN"], StringComparer.OrdinalIgnoreCase),
            "La premiere activation V2 add-only ne doit pas gerer les groupes absents du droit V2 et donc ne doit rien retirer.");
    }

    private static void VerifyProvisioningRetryKeepsSameGateDecision()
    {
        var first = BillingV2ProvisioningReadinessGate.Evaluate(ReadyState());
        var retry = BillingV2ProvisioningReadinessGate.Evaluate(ReadyState());

        Ensure(
            first == retry,
            "Un retry avec les memes preuves de readiness doit conserver la meme decision idempotente.");
    }

    private static void VerifyProvisioningPlannerFlagsMissingItemProvisioning()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                new BillingV2ProvisioningRuleProjection(
                    "subscription-v2-active",
                    "item-v2-unprovisioned",
                    "VPN-ACCESS",
                    TierCode: null,
                    RuleType: string.Empty,
                    TargetType: string.Empty,
                    TargetReference: null,
                    ValueSource: string.Empty,
                    StaticValue: null,
                    TierNumericValue: null,
                    TierUnit: null,
                    Quantity: 0)
            ]);

        Ensure(
            plan.DesiredAdGroups.Count == 0
            && plan.UnresolvedRuleReferences.SequenceEqual(
                ["VPN-ACCESS:no-tier:item-v2-unprovisioned"],
                StringComparer.OrdinalIgnoreCase),
            "Un item V2 actif sans etat subscription_item_provisioning doit bloquer le provisioning au lieu d'etre ignore.");
    }

    private static void VerifyNextcloudQuotaRulesAreCalculatedButNotAdGroups()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                new BillingV2ProvisioningRuleProjection(
                    "subscription-v2-storage",
                    "item-v2-storage",
                    "STORAGE-PERSONAL",
                    "128",
                    "nextcloud_quota",
                    "nextcloud_user_quota",
                    TargetReference: null,
                    "tier_numeric_value",
                    StaticValue: null,
                    TierNumericValue: 128,
                    TierUnit: "GiB",
                    Quantity: 1)
            ]);

        Ensure(
            plan.DesiredAdGroups.Count == 0
            && plan.UnresolvedRuleReferences.Count == 0
            && plan.NextcloudQuotas.Count == 1
            && plan.NextcloudQuotas[0].QuotaValue == 128
            && plan.NextcloudQuotas[0].Unit == "GiB"
            && plan.NextcloudQuotas[0].TargetType == "nextcloud_user_quota",
            "Les regles Nextcloud V2 doivent calculer un quota explicite sans le confondre avec un groupe AD.");
    }

    private static void VerifyDormantNextcloudQuotaProviderBlocksExecution()
    {
        var readiness = DormantBillingV2NextcloudQuotaProvider.Instance
            .CheckReadiness(
                [
                    new BillingV2NextcloudQuotaPlan(
                        "item-v2-storage",
                        "nextcloud_user_quota",
                        IdentityReference: null,
                        QuotaValue: 128,
                        Unit: "GiB")
                ]);

        Ensure(
            !readiness.CanApplyQuotas
            && readiness.ReasonCode
                == "BILLING_V2_NEXTCLOUD_QUOTA_PROVIDER_NOT_CONFIGURED",
            "Le provider Nextcloud dormant doit bloquer toute application reelle de quota.");
    }

    private static IReadOnlyList<BillingV2ProvisioningShadowRule> V2AdRules =>
        [
            new(
                "ACCES-VPN",
                "direct",
                "VPN-ACCESS",
                "LEGACY",
                "ad_group",
                "GG_VPN"),
            new(
                "ACCES-RDS",
                "direct",
                "RDS-ACCESS",
                null,
                "ad_group",
                "GG_RDS")
        ];

    private static IReadOnlyList<BillingV2ProvisioningShadowRule> V2ServiceRules =>
        [
            new(
                "ACCES-VPN",
                "direct",
                "VPN-ACCESS",
                "LEGACY",
                string.Empty,
                null),
            new(
                "ACCES-RDS",
                "direct",
                "RDS-ACCESS",
                null,
                string.Empty,
                null),
            new(
                "DOC-TECH",
                "legacy_one_time_entitlement",
                null,
                null,
                string.Empty,
                null)
        ];

    private static BillingV2ProvisioningReadinessState ReadyState(
        bool globalFlagEnabled = true,
        bool clientReady = true,
        bool addOnlyMode = true,
        bool completeMaterialization = true,
        bool requiredRulesResolved = true,
        bool shadowSucceeded = true,
        bool shadowMatchesLegacy = true,
        bool hasUnresolvedMismatch = false,
        bool targetGroupsResolved = true)
        => new(
            globalFlagEnabled,
            clientReady,
            addOnlyMode,
            completeMaterialization,
            requiredRulesResolved,
            shadowSucceeded,
            shadowMatchesLegacy,
            hasUnresolvedMismatch,
            targetGroupsResolved);

    private static IReadOnlyList<string> ExpectedGroups(string? externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return Array.Empty<string>();
        }

        if (externalReference.StartsWith(
                "PACK-BUREAU-",
                StringComparison.OrdinalIgnoreCase))
        {
            return ["GG_RDS", "GG_VPN"];
        }

        if (externalReference.StartsWith(
                "PACK-ACCES-",
                StringComparison.OrdinalIgnoreCase)
            || externalReference.StartsWith(
                "PACK-PRO-",
                StringComparison.OrdinalIgnoreCase))
        {
            return ["GG_VPN"];
        }

        return Array.Empty<string>();
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
