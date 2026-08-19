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
        VerifyReadinessReviewApprovesNativeV2WithoutLegacyOverlap();
        VerifyReadinessReviewDeniesLegacyOverlap();
        VerifyReadinessReviewDeniesStorageTargetGap();
        VerifyFirstActivationIsAddOnly();
        VerifyProvisioningRetryKeepsSameGateDecision();
        VerifyProvisioningPlannerFlagsMissingItemProvisioning();
        VerifyStorageQuotaRulesAreCalculatedButNotAdGroups();
        VerifyProvisioningItemStatusPolicy();
        VerifyDormantKoxoStorageProviderBlocksExecution();
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

    private static void VerifyReadinessReviewApprovesNativeV2WithoutLegacyOverlap()
    {
        var decision = BillingV2ProvisioningReadinessReviewPolicy.Evaluate(
            ReviewInputs());

        Ensure(
            decision.Ready
            && decision.AddOnlyMode
            && decision.ShadowStatus == "success"
            && decision.ShadowMatchesLegacy
            && decision.UnresolvedMismatchCount == 0
            && decision.ReasonCode
                == BillingV2ProvisioningReadinessReviewReasons.Ready,
            "Un client reel V2 natif sans chevauchement legacy et sans blocker doit etre approuvable uniquement en add-only.");
    }

    private static void VerifyReadinessReviewDeniesLegacyOverlap()
    {
        var decision = BillingV2ProvisioningReadinessReviewPolicy.Evaluate(
            ReviewInputs(activeLegacySubscriptionCount: 1));

        Ensure(
            !decision.Ready
            && !decision.ShadowMatchesLegacy
            && decision.ReasonCodes.Contains(
                BillingV2ProvisioningReadinessReviewReasons.LegacyOverlap)
            && decision.UnresolvedMismatchCount > 0,
            "Un abonnement legacy actif concurrent doit fermer la revue readiness.");
    }

    private static void VerifyReadinessReviewDeniesStorageTargetGap()
    {
        var decision = BillingV2ProvisioningReadinessReviewPolicy.Evaluate(
            ReviewInputs(storageTargetsResolved: false));

        Ensure(
            !decision.Ready
            && decision.ReasonCodes.Contains(
                BillingV2ProvisioningReadinessReviewReasons.StorageTargetsUnresolved)
            && decision.UnresolvedMismatchCount == 1,
            "Une cible KoXo non resolue doit laisser la readiness rouge avant toute action externe.");
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
                    Quantity: 0,
                    ScopeType: "user",
                    SubscriptionUserId: "subscription-user-v2",
                    IdentityReference: "portal-user-v2",
                    SubscriptionUserIsPrimary: true,
                    SubscriptionUserStatus: "active")
            ]);

        Ensure(
            plan.AllDesiredAdGroups.Count == 0
            && plan.Users.Count == 0
            && plan.UnresolvedRuleReferences.SequenceEqual(
                ["VPN-ACCESS:no-tier:item-v2-unprovisioned"],
                StringComparer.OrdinalIgnoreCase),
            "Un item V2 actif sans etat subscription_item_provisioning doit bloquer le provisioning au lieu d'etre ignore.");
    }

    private static void VerifyStorageQuotaRulesAreCalculatedButNotAdGroups()
    {
        var plan = BillingV2ProvisioningPlanner.Plan(
            [
                new BillingV2ProvisioningRuleProjection(
                    "subscription-v2-storage",
                    "item-v2-storage",
                    "STORAGE-PERSONAL",
                    "128",
                    "infrastructure_action",
                    "koxo_user_storage",
                    TargetReference: "KOXO-USER-STORAGE",
                    "tier_numeric_value",
                    StaticValue: null,
                    TierNumericValue: 128,
                    TierUnit: "GiB",
                    Quantity: 1,
                    ScopeType: "user",
                    SubscriptionUserId: "subscription-user-v2",
                    IdentityReference: "portal-user-v2",
                    SubscriptionUserIsPrimary: true,
                    SubscriptionUserStatus: "active")
            ]);

        Ensure(
            plan.AllDesiredAdGroups.Count == 0
            && plan.UnresolvedRuleReferences.Count == 0
            && plan.StorageQuotaPlans.Count == 1
            && plan.StorageQuotaPlans[0].QuotaValue == 128
            && plan.StorageQuotaPlans[0].Unit == "GiB"
            && plan.StorageQuotaPlans[0].TargetType == "koxo_user_storage"
            && plan.StorageQuotaPlans[0].SubscriptionUserId
                == "subscription-user-v2"
            && plan.StorageQuotaPlans[0].IdentityReference == "portal-user-v2",
            "Les regles de stockage V2 doivent calculer un quota explicite, rattache a son utilisateur, sans le confondre avec un groupe AD.");
    }

    private static void VerifyProvisioningItemStatusPolicy()
    {
        var entitlement = new BillingV2AcknowledgedEntitlement(
            "item-entitlement", null, "SUPPORT-PLUS",
            "contractual_entitlement", "support_level", "PLUS", "subscription");
        var slot = new BillingV2AcknowledgedEntitlement(
            "item-slot", null, "USER-ADDITIONAL",
            "platform_entitlement", "platform", "additional_user_slot", "subscription");
        var plan = new BillingV2ProvisioningPlan(
            Array.Empty<BillingV2UserDesiredState>(),
            new BillingV2SubscriptionDesiredState(
                Array.Empty<BillingV2StorageQuotaPlan>(),
                Array.Empty<BillingV2AcknowledgedEntitlement>(),
                [entitlement],
                [slot]),
            Array.Empty<BillingV2ProvisioningBlocker>());

        var acknowledged = BillingV2ProvisioningItemStatusPolicy.Acknowledged(plan);
        Ensure(
            acknowledged.Select(update => update.SubscriptionItemId)
                .SequenceEqual(["item-entitlement", "item-slot"], StringComparer.Ordinal)
            && acknowledged.All(update => update.Status == "provisioned" && update.SetProvisionedAt),
            "Les entitlements sans mutation externe et les slots doivent etre marques provisioned des que le contrat actif est autorise.");

        var storage = BillingV2ProvisioningItemStatusPolicy.Storage(
            new BillingV2KoxoStorageApplyResult(
                "mixed",
                [
                    new BillingV2KoxoStorageTargetResult(
                        "item-storage-ok", "user:u", BillingV2KoxoStorageOutcome.Noop,
                        "NOOP", BillingV2KoxoStorageVerification.XmlVerified),
                    new BillingV2KoxoStorageTargetResult(
                        "item-storage-ko", "group:g", BillingV2KoxoStorageOutcome.Failed,
                        "KOXO_FAILED", BillingV2KoxoStorageVerification.None)
                ]));
        Ensure(
            storage.Single(update => update.SubscriptionItemId == "item-storage-ok").Status == "provisioned"
            && storage.Single(update => update.SubscriptionItemId == "item-storage-ko").Status == "failed"
            && storage.Single(update => update.SubscriptionItemId == "item-storage-ko").LastError == "KOXO_FAILED",
            "Le statut stockage doit suivre la preuve KoXo cible par cible.");

        var ad = BillingV2ProvisioningItemStatusPolicy.ActiveDirectory(
            ["item-vpn"],
            new ProvisioningExecutionResult(true, false, "PROVISIONING_ALREADY_COMPLIANT",
                Array.Empty<ProvisioningOperationResult>()));
        Ensure(
            ad.Count == 1 && ad[0].Status == "provisioned" && ad[0].SetProvisionedAt,
            "Un droit AD deja conforme doit etre provisioned meme sans changement LDAP.");
    }

    private static void VerifyDormantKoxoStorageProviderBlocksExecution()
    {
        var readiness = DormantBillingV2KoxoStorageProvider.Instance
            .CheckReadiness(
                [
                    new BillingV2StorageQuotaPlan(
                        "item-v2-storage",
                        SubscriptionUserId: "subscription-user-v2",
                        "koxo_user_storage",
                        IdentityReference: "portal-user-v2",
                        QuotaValue: 128,
                        Unit: "GiB",
                        ScopeType: "user")
                ]);

        Ensure(
            !readiness.CanApplyQuotas
            && readiness.ReasonCode
                == "BILLING_V2_KOXO_STORAGE_PROVIDER_NOT_CONFIGURED",
            "Le provider de stockage KoXo dormant doit bloquer toute application reelle de quota.");
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

    private static BillingV2ProvisioningReadinessReviewInputs ReviewInputs(
        bool persistentSqlAvailable = true,
        bool customerExists = true,
        bool customerIsDemo = false,
        int activeV2SubscriptionCount = 1,
        int activeLegacySubscriptionCount = 0,
        int unresolvedRuleCount = 0,
        bool targetGroupsResolved = true,
        bool storageProviderReady = true,
        bool storageTargetsResolved = true,
        bool adTargetsResolved = true)
        => new(
            persistentSqlAvailable,
            customerExists,
            customerIsDemo,
            activeV2SubscriptionCount,
            activeLegacySubscriptionCount,
            unresolvedRuleCount,
            targetGroupsResolved,
            storageProviderReady,
            storageTargetsResolved,
            adTargetsResolved);

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
