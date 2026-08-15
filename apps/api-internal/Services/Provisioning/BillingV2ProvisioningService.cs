using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record BillingV2ProvisioningReadinessState(
    bool GlobalFlagEnabled,
    bool ClientReady,
    bool AddOnlyMode,
    bool CompleteMaterialization,
    bool RequiredRulesResolved,
    bool ShadowSucceeded,
    bool ShadowMatchesLegacy,
    bool HasUnresolvedMismatch,
    bool TargetGroupsResolved);

public sealed record BillingV2ProvisioningGateDecision(
    bool Authorized,
    bool AddOnlyMode,
    string ReasonCode)
{
    public static BillingV2ProvisioningGateDecision Allow(bool addOnlyMode)
        => new(true, addOnlyMode, "BILLING_V2_PROVISIONING_READY");

    public static BillingV2ProvisioningGateDecision Deny(string reasonCode)
        => new(false, AddOnlyMode: true, reasonCode);
}

public sealed record BillingV2ProvisioningRuleProjection(
    string SubscriptionId,
    string SubscriptionItemId,
    string ServiceCode,
    string? TierCode,
    string RuleType,
    string TargetType,
    string? TargetReference,
    string ValueSource,
    string? StaticValue,
    long? TierNumericValue,
    string? TierUnit,
    int Quantity);

public sealed record BillingV2ProvisioningPlan(
    IReadOnlyList<string> DesiredAdGroups,
    IReadOnlyList<BillingV2NextcloudQuotaPlan> NextcloudQuotas,
    IReadOnlyList<string> UnresolvedRuleReferences);

public sealed record BillingV2NextcloudQuotaPlan(
    string SubscriptionItemId,
    string TargetType,
    string? IdentityReference,
    long QuotaValue,
    string Unit);

public sealed record BillingV2NextcloudQuotaReadiness(
    bool CanApplyQuotas,
    string ReasonCode);

public interface IBillingV2NextcloudQuotaProvider
{
    BillingV2NextcloudQuotaReadiness CheckReadiness(
        IReadOnlyList<BillingV2NextcloudQuotaPlan> quotas);
}

public sealed class DormantBillingV2NextcloudQuotaProvider
    : IBillingV2NextcloudQuotaProvider
{
    public static DormantBillingV2NextcloudQuotaProvider Instance { get; }
        = new();

    private DormantBillingV2NextcloudQuotaProvider()
    {
    }

    public BillingV2NextcloudQuotaReadiness CheckReadiness(
        IReadOnlyList<BillingV2NextcloudQuotaPlan> quotas)
        => quotas.Count == 0
            ? new BillingV2NextcloudQuotaReadiness(
                CanApplyQuotas: true,
                "BILLING_V2_NEXTCLOUD_QUOTA_NOOP")
            : new BillingV2NextcloudQuotaReadiness(
                CanApplyQuotas: false,
                "BILLING_V2_NEXTCLOUD_QUOTA_PROVIDER_NOT_CONFIGURED");
}

public interface IBillingV2ProvisioningService
{
    Task<ProvisioningExecutionResult?> TryReconcileAsync(
        SubscriptionProvisioningContext context,
        CancellationToken cancellationToken);

    Task<ProvisioningExecutionResult?> TryReconcileActivatedSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2ProvisioningService
    : IBillingV2ProvisioningService
{
    public static NoOpBillingV2ProvisioningService Instance { get; } = new();

    private NoOpBillingV2ProvisioningService()
    {
    }

    public Task<ProvisioningExecutionResult?> TryReconcileAsync(
        SubscriptionProvisioningContext context,
        CancellationToken cancellationToken)
        => Task.FromResult<ProvisioningExecutionResult?>(null);

    public Task<ProvisioningExecutionResult?> TryReconcileActivatedSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
        => Task.FromResult<ProvisioningExecutionResult?>(null);
}

public sealed class BillingV2ProvisioningService : IBillingV2ProvisioningService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _billingV2;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IActiveDirectoryLinkRepository _activeDirectoryLinks;
    private readonly IProvisioningService _provisioningService;
    private readonly IBillingV2NextcloudQuotaProvider _nextcloudQuotaProvider;
    private readonly SubscriptionProvisioningRuntimeConfiguration
        _provisioningConfiguration;
    private readonly ILogger<BillingV2ProvisioningService> _logger;

    public BillingV2ProvisioningService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration billingV2,
        ISubscriptionRepository subscriptions,
        IActiveDirectoryLinkRepository activeDirectoryLinks,
        IProvisioningService provisioningService,
        IBillingV2NextcloudQuotaProvider nextcloudQuotaProvider,
        SubscriptionProvisioningRuntimeConfiguration provisioningConfiguration,
        ILogger<BillingV2ProvisioningService> logger)
    {
        _sql = sql;
        _billingV2 = billingV2;
        _subscriptions = subscriptions;
        _activeDirectoryLinks = activeDirectoryLinks;
        _provisioningService = provisioningService;
        _nextcloudQuotaProvider = nextcloudQuotaProvider;
        _provisioningConfiguration = provisioningConfiguration;
        _logger = logger;
    }

    public async Task<ProvisioningExecutionResult?> TryReconcileAsync(
        SubscriptionProvisioningContext context,
        CancellationToken cancellationToken)
    {
        if (!_billingV2.ProvisioningEnabled
            || !_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return null;
        }

        var activeLegacySubscriptions =
            (await _subscriptions.GetByCustomerAsync(
                context.Subscription.CustomerId,
                cancellationToken))
            .Where(subscription => string.Equals(
                subscription.Status,
                "active",
                StringComparison.Ordinal))
            .ToArray();
        if (activeLegacySubscriptions.Length == 0)
        {
            return null;
        }

        var readiness = await LoadReadinessAsync(
            context.Subscription.CustomerId,
            cancellationToken);
        var plan = await LoadProvisioningPlanAsync(
            context.Subscription.CustomerId,
            activeLegacySubscriptions.Select(subscription => subscription.Id).ToArray(),
            cancellationToken);
        var materializedIds = await LoadMaterializedActiveSubscriptionIdsAsync(
            context.Subscription.CustomerId,
            cancellationToken);
        var missingSubscriptions = activeLegacySubscriptions
            .Select(subscription => subscription.Id)
            .Except(materializedIds, StringComparer.Ordinal)
            .ToArray();
        var targetGroupsResolved = plan.DesiredAdGroups.All(group =>
            context.GroupDistinguishedNamesBySamAccountName.TryGetValue(
                group,
                out var distinguishedName)
            && !string.IsNullOrWhiteSpace(distinguishedName));
        var sameAdRights = plan.DesiredAdGroups.SequenceEqual(
            context.ReconciledGroups,
            StringComparer.OrdinalIgnoreCase);
        var decision = BillingV2ProvisioningReadinessGate.Evaluate(
            new BillingV2ProvisioningReadinessState(
                GlobalFlagEnabled: _billingV2.ProvisioningEnabled,
                ClientReady: readiness.ClientReady,
                AddOnlyMode: readiness.AddOnlyMode,
                CompleteMaterialization: missingSubscriptions.Length == 0
                    && plan.UnresolvedRuleReferences.Count == 0,
                RequiredRulesResolved: plan.UnresolvedRuleReferences.Count == 0,
                ShadowSucceeded: readiness.ShadowSucceeded,
                ShadowMatchesLegacy: readiness.ShadowMatchesLegacy
                    && sameAdRights,
                HasUnresolvedMismatch: readiness.HasUnresolvedMismatch,
                TargetGroupsResolved: targetGroupsResolved));
        if (!decision.Authorized)
        {
            _logger.LogWarning(
                "Billing V2 provisioning gate denied for customer {CustomerId} subscription {SubscriptionId}: {ReasonCode}. Legacy provisioning remains authoritative.",
                context.Subscription.CustomerId,
                context.Subscription.Id,
                decision.ReasonCode);
            return null;
        }

        if (plan.NextcloudQuotas.Count > 0)
        {
            var nextcloudReadiness =
                _nextcloudQuotaProvider.CheckReadiness(plan.NextcloudQuotas);
            if (nextcloudReadiness.CanApplyQuotas)
            {
                _logger.LogWarning(
                    "Billing V2 Nextcloud quota provider unexpectedly reported ready for customer {CustomerId}, but quota execution is not wired in this release. Legacy provisioning remains authoritative.",
                    context.Subscription.CustomerId);
                return null;
            }

            _logger.LogWarning(
                "Billing V2 provisioning gate denied for customer {CustomerId}: Nextcloud quota plans exist but no trusted runtime quota provider is configured ({ReasonCode}). Legacy provisioning remains authoritative.",
                context.Subscription.CustomerId,
                nextcloudReadiness.ReasonCode);
            return null;
        }

        var managedGroups = BillingV2ProvisioningExecutionPolicy
            .ResolveManagedGroupsForExecution(
                decision,
                plan.DesiredAdGroups,
                context.ManagedGroups);
        return await _provisioningService.ReconcileAsync(
            new ProvisioningExecutionRequest(
                context.TargetUsers,
                plan.DesiredAdGroups,
                managedGroups,
                context.GroupDistinguishedNamesBySamAccountName),
            cancellationToken);
    }

    public async Task<ProvisioningExecutionResult?> TryReconcileActivatedSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        if (!_billingV2.ProvisioningEnabled
            || !_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return null;
        }

        var customerId = await LoadActiveSubscriptionCustomerIdAsync(
            subscriptionId,
            cancellationToken);
        if (customerId is null)
        {
            return null;
        }

        var activeV2SubscriptionIds =
            await LoadMaterializedActiveSubscriptionIdsAsync(
                customerId,
                cancellationToken);
        if (!activeV2SubscriptionIds.Contains(subscriptionId))
        {
            return null;
        }

        var readiness = await LoadReadinessAsync(
            customerId,
            cancellationToken);
        var plan = await LoadProvisioningPlanAsync(
            customerId,
            activeV2SubscriptionIds.ToArray(),
            cancellationToken);
        var targetUsers = await _activeDirectoryLinks.GetCustomerUserLinksAsync(
            customerId,
            cancellationToken);
        if (targetUsers.Count == 0)
        {
            _logger.LogWarning(
                "Billing V2 provisioning skipped for subscription {SubscriptionId}: no Active Directory user link is available. No external action was executed.",
                subscriptionId);
            return null;
        }

        var targetGroupsResolved = plan.DesiredAdGroups.All(group =>
            _provisioningConfiguration.GroupDistinguishedNamesBySamAccountName
                .TryGetValue(group, out var distinguishedName)
            && !string.IsNullOrWhiteSpace(distinguishedName));
        var decision = BillingV2ProvisioningReadinessGate.Evaluate(
            new BillingV2ProvisioningReadinessState(
                GlobalFlagEnabled: _billingV2.ProvisioningEnabled,
                ClientReady: readiness.ClientReady,
                AddOnlyMode: readiness.AddOnlyMode,
                CompleteMaterialization: activeV2SubscriptionIds.Count > 0
                    && plan.UnresolvedRuleReferences.Count == 0,
                RequiredRulesResolved: plan.UnresolvedRuleReferences.Count == 0,
                ShadowSucceeded: readiness.ShadowSucceeded,
                ShadowMatchesLegacy: readiness.ShadowMatchesLegacy,
                HasUnresolvedMismatch: readiness.HasUnresolvedMismatch,
                TargetGroupsResolved: targetGroupsResolved));
        if (!decision.Authorized)
        {
            _logger.LogWarning(
                "Billing V2 provisioning gate denied after provider activation for subscription {SubscriptionId}: {ReasonCode}. No external action was executed.",
                subscriptionId,
                decision.ReasonCode);
            return null;
        }

        if (!decision.AddOnlyMode)
        {
            _logger.LogWarning(
                "Billing V2 provisioning after provider activation is limited to add-only mode for subscription {SubscriptionId}. No external action was executed.",
                subscriptionId);
            return null;
        }

        if (plan.NextcloudQuotas.Count > 0)
        {
            var nextcloudReadiness =
                _nextcloudQuotaProvider.CheckReadiness(plan.NextcloudQuotas);
            _logger.LogWarning(
                "Billing V2 provisioning gate denied for subscription {SubscriptionId}: Nextcloud quota plans exist but no trusted runtime quota provider is configured ({ReasonCode}). No external action was executed.",
                subscriptionId,
                nextcloudReadiness.ReasonCode);
            return null;
        }

        var managedGroups = BillingV2ProvisioningExecutionPolicy
            .ResolveManagedGroupsForExecution(
                decision,
                plan.DesiredAdGroups,
                plan.DesiredAdGroups);
        return await _provisioningService.ReconcileAsync(
            new ProvisioningExecutionRequest(
                targetUsers,
                plan.DesiredAdGroups,
                managedGroups,
                _provisioningConfiguration
                    .GroupDistinguishedNamesBySamAccountName),
            cancellationToken);
    }

    private async Task<string?> LoadActiveSubscriptionCustomerIdAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT customer_id
            FROM billing_v2_subscriptions
            WHERE id = @subscription_id
              AND status = 'active'
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string customerId
            && !string.IsNullOrWhiteSpace(customerId)
                ? customerId
                : null;
    }

    private async Task<BillingV2ProvisioningDbReadiness> LoadReadinessAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                ready_for_v2_provisioning,
                add_only_mode,
                last_shadow_status,
                last_shadow_matches_legacy,
                unresolved_mismatch_count
            FROM billing_v2_provisioning_client_readiness
            WHERE customer_id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return BillingV2ProvisioningDbReadiness.NotReady;
        }

        var status = reader.IsDBNull(reader.GetOrdinal("last_shadow_status"))
            ? null
            : reader.GetString("last_shadow_status");
        return new BillingV2ProvisioningDbReadiness(
            ClientReady: reader.GetBoolean("ready_for_v2_provisioning"),
            AddOnlyMode: reader.GetBoolean("add_only_mode"),
            ShadowSucceeded: string.Equals(
                status,
                "success",
                StringComparison.OrdinalIgnoreCase),
            ShadowMatchesLegacy:
                !reader.IsDBNull(reader.GetOrdinal("last_shadow_matches_legacy"))
                && reader.GetBoolean("last_shadow_matches_legacy"),
            HasUnresolvedMismatch:
                reader.GetInt32("unresolved_mismatch_count") > 0);
    }

    private async Task<IReadOnlySet<string>> LoadMaterializedActiveSubscriptionIdsAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id
            FROM billing_v2_subscriptions
            WHERE customer_id = @customer_id
              AND status = 'active';
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString("id"));
        }

        return ids;
    }

    private async Task<BillingV2ProvisioningPlan> LoadProvisioningPlanAsync(
        string customerId,
        IReadOnlyList<string> activeLegacySubscriptionIds,
        CancellationToken cancellationToken)
    {
        var rules = new List<BillingV2ProvisioningRuleProjection>();
        var legacyIdSet = activeLegacySubscriptionIds
            .ToHashSet(StringComparer.Ordinal);
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                sub.id AS subscription_id,
                item.id AS subscription_item_id,
                service.code AS service_code,
                tier.code AS tier_code,
                rule.rule_type,
                rule.target_type,
                rule.target_reference,
                rule.value_source,
                rule.static_value,
                tier.numeric_value,
                tier.unit,
                provisioned.provisioned_quantity,
                provisioned.subscription_item_id AS provisioning_item_id
            FROM billing_v2_subscriptions sub
            INNER JOIN billing_v2_subscription_items item
                ON item.subscription_id = sub.id
               AND item.status = 'active'
               AND item.effective_from <= UTC_TIMESTAMP(6)
               AND (item.effective_until IS NULL
                    OR item.effective_until > UTC_TIMESTAMP(6))
            LEFT JOIN billing_v2_subscription_item_provisioning provisioned
                ON provisioned.subscription_item_id = item.id
            INNER JOIN billing_v2_services service
                ON service.id = item.service_id
               AND service.status = 'active'
            LEFT JOIN billing_v2_service_tiers tier
                ON tier.id = COALESCE(provisioned.provisioned_tier_id, item.tier_id)
               AND tier.status = 'active'
            LEFT JOIN billing_v2_provisioning_rules rule
                ON rule.service_id = service.id
               AND rule.status = 'active'
               AND (rule.tier_id IS NULL OR rule.tier_id = tier.id)
            WHERE sub.customer_id = @customer_id
              AND sub.status = 'active'
            ORDER BY sub.id, item.id, rule.display_order;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var subscriptionId = reader.GetString("subscription_id");
            if (!legacyIdSet.Contains(subscriptionId))
            {
                continue;
            }

            var provisioningItemId = reader.IsDBNull(
                    reader.GetOrdinal("provisioning_item_id"))
                ? null
                : reader.GetString("provisioning_item_id");
            if (string.IsNullOrWhiteSpace(provisioningItemId))
            {
                rules.Add(new BillingV2ProvisioningRuleProjection(
                    subscriptionId,
                    reader.GetString("subscription_item_id"),
                    reader.GetString("service_code"),
                    reader.IsDBNull(reader.GetOrdinal("tier_code"))
                        ? null
                        : reader.GetString("tier_code"),
                    RuleType: string.Empty,
                    TargetType: string.Empty,
                    TargetReference: null,
                    ValueSource: string.Empty,
                    StaticValue: null,
                    TierNumericValue: null,
                    TierUnit: null,
                    Quantity: 0));
                continue;
            }

            rules.Add(new BillingV2ProvisioningRuleProjection(
                subscriptionId,
                reader.GetString("subscription_item_id"),
                reader.GetString("service_code"),
                reader.IsDBNull(reader.GetOrdinal("tier_code"))
                    ? null
                    : reader.GetString("tier_code"),
                reader.IsDBNull(reader.GetOrdinal("rule_type"))
                    ? string.Empty
                    : reader.GetString("rule_type"),
                reader.IsDBNull(reader.GetOrdinal("target_type"))
                    ? string.Empty
                    : reader.GetString("target_type"),
                reader.IsDBNull(reader.GetOrdinal("target_reference"))
                    ? null
                    : reader.GetString("target_reference"),
                reader.IsDBNull(reader.GetOrdinal("value_source"))
                    ? string.Empty
                    : reader.GetString("value_source"),
                reader.IsDBNull(reader.GetOrdinal("static_value"))
                    ? null
                    : reader.GetString("static_value"),
                reader.IsDBNull(reader.GetOrdinal("numeric_value"))
                    ? null
                    : reader.GetInt64("numeric_value"),
                reader.IsDBNull(reader.GetOrdinal("unit"))
                    ? null
                    : reader.GetString("unit"),
                reader.GetInt32("provisioned_quantity")));
        }

        return BillingV2ProvisioningPlanner.Plan(rules);
    }

    private sealed record BillingV2ProvisioningDbReadiness(
        bool ClientReady,
        bool AddOnlyMode,
        bool ShadowSucceeded,
        bool ShadowMatchesLegacy,
        bool HasUnresolvedMismatch)
    {
        public static BillingV2ProvisioningDbReadiness NotReady { get; }
            = new(
                ClientReady: false,
                AddOnlyMode: true,
                ShadowSucceeded: false,
                ShadowMatchesLegacy: false,
                HasUnresolvedMismatch: false);
    }
}

public static class BillingV2ProvisioningReadinessGate
{
    public static BillingV2ProvisioningGateDecision Evaluate(
        BillingV2ProvisioningReadinessState state)
    {
        if (!state.GlobalFlagEnabled)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_FLAG_OFF");
        }

        if (!state.CompleteMaterialization)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_INCOMPLETE_MATERIALIZATION");
        }

        if (!state.RequiredRulesResolved)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_RULES_UNRESOLVED");
        }

        if (!state.ShadowSucceeded || !state.ShadowMatchesLegacy)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_SHADOW_NOT_MATCHING");
        }

        if (state.HasUnresolvedMismatch)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_UNRESOLVED_MISMATCH");
        }

        if (!state.TargetGroupsResolved)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_TARGETS_UNRESOLVED");
        }

        if (!state.ClientReady)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_CLIENT_NOT_READY");
        }

        return BillingV2ProvisioningGateDecision.Allow(state.AddOnlyMode);
    }
}

public static class BillingV2ProvisioningExecutionPolicy
{
    public static IReadOnlyList<string> ResolveManagedGroupsForExecution(
        BillingV2ProvisioningGateDecision decision,
        IReadOnlyList<string> desiredGroups,
        IReadOnlyList<string> managedGroups)
        => decision.AddOnlyMode
            ? desiredGroups
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : managedGroups
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToArray();
}

public static class BillingV2ProvisioningPlanner
{
    public static BillingV2ProvisioningPlan Plan(
        IReadOnlyList<BillingV2ProvisioningRuleProjection> rules)
    {
        var adGroups = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var quotas = new List<BillingV2NextcloudQuotaPlan>();
        var unresolved = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleType)
                || string.IsNullOrWhiteSpace(rule.TargetType))
            {
                unresolved.Add(CreateRuleReference(rule));
                continue;
            }

            if (string.Equals(
                    rule.TargetType,
                    "ad_group",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(rule.TargetReference))
                {
                    unresolved.Add(CreateRuleReference(rule));
                    continue;
                }

                adGroups.Add(rule.TargetReference.Trim());
                continue;
            }

            if (rule.TargetType.StartsWith(
                    "nextcloud_",
                    StringComparison.OrdinalIgnoreCase))
            {
                var value = ResolveValue(rule);
                if (value is null)
                {
                    unresolved.Add(CreateRuleReference(rule));
                    continue;
                }

                quotas.Add(new BillingV2NextcloudQuotaPlan(
                    rule.SubscriptionItemId,
                    rule.TargetType,
                    null,
                    value.Value,
                    rule.TierUnit ?? "GiB"));
                continue;
            }

            unresolved.Add(CreateRuleReference(rule));
        }

        return new BillingV2ProvisioningPlan(
            adGroups.ToArray(),
            quotas,
            unresolved.ToArray());
    }

    private static long? ResolveValue(BillingV2ProvisioningRuleProjection rule)
    {
        if (string.Equals(
                rule.ValueSource,
                "tier_numeric_value",
                StringComparison.OrdinalIgnoreCase))
        {
            return rule.TierNumericValue;
        }

        if (string.Equals(
                rule.ValueSource,
                "static",
                StringComparison.OrdinalIgnoreCase)
            && long.TryParse(rule.StaticValue, out var staticValue))
        {
            return staticValue;
        }

        return null;
    }

    private static string CreateRuleReference(
        BillingV2ProvisioningRuleProjection rule)
        => $"{rule.ServiceCode}:{rule.TierCode ?? "no-tier"}:{rule.SubscriptionItemId}";
}
