using MySqlConnector;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record BillingV2ProvisioningReadinessReviewInputs(
    bool PersistentSqlAvailable,
    bool CustomerExists,
    bool CustomerIsDemo,
    int ActiveV2SubscriptionCount,
    int ActiveLegacySubscriptionCount,
    int UnresolvedRuleCount,
    bool TargetGroupsResolved,
    bool StorageProviderReady,
    bool StorageTargetsResolved,
    bool AdTargetsResolved);

public sealed record BillingV2ProvisioningReadinessReviewDecision(
    bool Ready,
    bool AddOnlyMode,
    string ShadowStatus,
    bool ShadowMatchesLegacy,
    int UnresolvedMismatchCount,
    IReadOnlyList<string> ReasonCodes)
{
    public string ReasonCode => Ready
        ? BillingV2ProvisioningReadinessReviewReasons.Ready
        : ReasonCodes.FirstOrDefault()
            ?? BillingV2ProvisioningReadinessReviewReasons.ReviewFailed;
}

public sealed record BillingV2ProvisioningReadinessReviewResult(
    bool Ready,
    bool AddOnlyMode,
    string ShadowStatus,
    bool ShadowMatchesLegacy,
    int UnresolvedMismatchCount,
    IReadOnlyList<string> ReasonCodes,
    int ActiveV2SubscriptionCount,
    int ActiveLegacySubscriptionCount,
    int DesiredAdGroupCount,
    int StorageTargetCount,
    bool Persisted)
{
    public string ReasonCode => Ready
        ? BillingV2ProvisioningReadinessReviewReasons.Ready
        : ReasonCodes.FirstOrDefault()
            ?? BillingV2ProvisioningReadinessReviewReasons.ReviewFailed;

    public static BillingV2ProvisioningReadinessReviewResult PersistenceUnavailable { get; }
        = new(
            Ready: false,
            AddOnlyMode: true,
            ShadowStatus: "failed",
            ShadowMatchesLegacy: false,
            UnresolvedMismatchCount: 1,
            [BillingV2ProvisioningReadinessReviewReasons.PersistentSqlUnavailable],
            ActiveV2SubscriptionCount: 0,
            ActiveLegacySubscriptionCount: 0,
            DesiredAdGroupCount: 0,
            StorageTargetCount: 0,
            Persisted: false);
}
public static class BillingV2ProvisioningReadinessReviewReasons
{
    public const string Ready = "BILLING_V2_PROVISIONING_READINESS_REVIEW_READY";
    public const string ReviewFailed = "BILLING_V2_PROVISIONING_READINESS_REVIEW_FAILED";
    public const string PersistentSqlUnavailable = "BILLING_V2_PROVISIONING_READINESS_SQL_UNAVAILABLE";
    public const string CustomerNotFound = "BILLING_V2_PROVISIONING_READINESS_CUSTOMER_NOT_FOUND";
    public const string DemoCustomer = "BILLING_V2_PROVISIONING_READINESS_DEMO_CUSTOMER";
    public const string NoActiveV2Subscription = "BILLING_V2_PROVISIONING_READINESS_NO_ACTIVE_V2_SUBSCRIPTION";
    public const string LegacyOverlap = "BILLING_V2_PROVISIONING_READINESS_LEGACY_OVERLAP";
    public const string RulesUnresolved = "BILLING_V2_PROVISIONING_READINESS_RULES_UNRESOLVED";
    public const string TargetGroupsUnresolved = "BILLING_V2_PROVISIONING_READINESS_TARGET_GROUPS_UNRESOLVED";
    public const string StorageProviderNotReady = "BILLING_V2_PROVISIONING_READINESS_STORAGE_PROVIDER_NOT_READY";
    public const string StorageTargetsUnresolved = "BILLING_V2_PROVISIONING_READINESS_STORAGE_TARGETS_UNRESOLVED";
    public const string AdTargetsUnresolved = "BILLING_V2_PROVISIONING_READINESS_AD_TARGETS_UNRESOLVED";
}

public static class BillingV2ProvisioningReadinessReviewPolicy
{
    public static BillingV2ProvisioningReadinessReviewDecision Evaluate(
        BillingV2ProvisioningReadinessReviewInputs inputs)
    {
        var reasons = new List<string>();
        var mismatchCount = Math.Max(0, inputs.UnresolvedRuleCount);

        void Reject(bool condition, string reasonCode)
        {
            if (!condition)
            {
                return;
            }

            reasons.Add(reasonCode);
            if (reasonCode != BillingV2ProvisioningReadinessReviewReasons.RulesUnresolved)
            {
                mismatchCount++;
            }
        }
        Reject(!inputs.PersistentSqlAvailable, BillingV2ProvisioningReadinessReviewReasons.PersistentSqlUnavailable);
        Reject(!inputs.CustomerExists, BillingV2ProvisioningReadinessReviewReasons.CustomerNotFound);
        Reject(inputs.CustomerIsDemo, BillingV2ProvisioningReadinessReviewReasons.DemoCustomer);
        Reject(inputs.ActiveV2SubscriptionCount <= 0, BillingV2ProvisioningReadinessReviewReasons.NoActiveV2Subscription);
        Reject(inputs.ActiveLegacySubscriptionCount > 0, BillingV2ProvisioningReadinessReviewReasons.LegacyOverlap);
        Reject(inputs.UnresolvedRuleCount > 0, BillingV2ProvisioningReadinessReviewReasons.RulesUnresolved);
        Reject(!inputs.TargetGroupsResolved, BillingV2ProvisioningReadinessReviewReasons.TargetGroupsUnresolved);
        Reject(!inputs.StorageProviderReady, BillingV2ProvisioningReadinessReviewReasons.StorageProviderNotReady);
        Reject(!inputs.StorageTargetsResolved, BillingV2ProvisioningReadinessReviewReasons.StorageTargetsUnresolved);
        Reject(!inputs.AdTargetsResolved, BillingV2ProvisioningReadinessReviewReasons.AdTargetsUnresolved);

        var ready = reasons.Count == 0;
        return new BillingV2ProvisioningReadinessReviewDecision(
            ready,
            AddOnlyMode: true,
            ShadowStatus: ready ? "success" : "failed",
            ShadowMatchesLegacy: inputs.ActiveLegacySubscriptionCount == 0,
            mismatchCount,
            reasons);
    }
}

public sealed partial class BillingV2ProvisioningService
{
    public async Task<BillingV2ProvisioningReadinessReviewResult>
        ReviewClientReadinessAsync(
            string customerId,
            string reviewedByReference,
            CancellationToken cancellationToken)
    {
        var persistentSqlAvailable =
            _sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString);
        if (!persistentSqlAvailable)
        {
            return BillingV2ProvisioningReadinessReviewResult.PersistenceUnavailable;
        }

        if (string.IsNullOrWhiteSpace(customerId)
            || string.IsNullOrWhiteSpace(reviewedByReference))
        {
            throw new ArgumentException(
                "Customer and reviewer references are required for Billing V2 provisioning readiness review.");
        }

        var subject = await LoadReadinessReviewSubjectAsync(
            customerId,
            cancellationToken);
        var activeV2SubscriptionIds = subject.Exists
            ? await LoadMaterializedActiveSubscriptionIdsAsync(
                customerId,
                cancellationToken)
            : new HashSet<string>(StringComparer.Ordinal);
        var activeLegacySubscriptions = subject.Exists
            ? (await _subscriptions.GetByCustomerAsync(
                    customerId,
                    cancellationToken))
                .Where(subscription => string.Equals(
                    subscription.Status,
                    "active",
                    StringComparison.Ordinal))
                .ToArray()
            : [];
        var plan = activeV2SubscriptionIds.Count > 0
            ? await LoadProvisioningPlanAsync(
                customerId,
                activeV2SubscriptionIds.ToArray(),
                cancellationToken)
            : BillingV2ProvisioningPlan.Empty;

        var targetGroupsResolved = plan.AllDesiredAdGroups.All(group =>
            _provisioningConfiguration.GroupDistinguishedNamesBySamAccountName
                .TryGetValue(group, out var distinguishedName)
            && !string.IsNullOrWhiteSpace(distinguishedName));
        var storageProviderReady = _koxoStorageProvider
            .CheckReadiness(plan.StorageQuotaPlans)
            .CanApplyQuotas;

        var storageTargetsResolved = plan.StorageQuotaPlans.Count == 0;
        if (subject.Exists
            && activeV2SubscriptionIds.Count > 0
            && plan.UnresolvedRuleReferences.Count == 0
            && storageProviderReady
            && plan.StorageQuotaPlans.Count > 0)
        {
            var storageResolution = await _koxoStorageTargets.ResolveAsync(
                customerId,
                plan.StorageQuotaPlans,
                cancellationToken);
            storageTargetsResolved = storageResolution.Resolved;
        }

        var adTargetsResolved = plan.UsersRequiringAdIdentity.Count == 0;
        if (subject.Exists
            && activeV2SubscriptionIds.Count > 0
            && plan.UnresolvedRuleReferences.Count == 0
            && plan.UsersRequiringAdIdentity.Count > 0)
        {
            var customerUserLinks =
                await _activeDirectoryLinks.GetCustomerUserLinksAsync(
                    customerId,
                    cancellationToken);
            var adResolution = await ResolveTargetsAsync(
                customerId,
                plan.UsersRequiringAdIdentity,
                customerUserLinks,
                cancellationToken);
            adTargetsResolved = adResolution.Resolved;
        }

        var decision = BillingV2ProvisioningReadinessReviewPolicy.Evaluate(
            new BillingV2ProvisioningReadinessReviewInputs(
                PersistentSqlAvailable: true,
                CustomerExists: subject.Exists,
                CustomerIsDemo: subject.IsDemo,
                ActiveV2SubscriptionCount: activeV2SubscriptionIds.Count,
                ActiveLegacySubscriptionCount: activeLegacySubscriptions.Length,
                UnresolvedRuleCount: plan.UnresolvedRuleReferences.Count,
                targetGroupsResolved,
                storageProviderReady,
                storageTargetsResolved,
                adTargetsResolved));

        var persisted = false;
        if (subject.Exists)
        {
            await PersistReadinessReviewAsync(
                customerId,
                reviewedByReference,
                decision,
                cancellationToken);
            persisted = true;
        }

        return new BillingV2ProvisioningReadinessReviewResult(
            decision.Ready,
            decision.AddOnlyMode,
            decision.ShadowStatus,
            decision.ShadowMatchesLegacy,
            decision.UnresolvedMismatchCount,
            decision.ReasonCodes,
            activeV2SubscriptionIds.Count,
            activeLegacySubscriptions.Length,
            plan.AllDesiredAdGroups.Count,
            plan.StorageQuotaPlans.Count,
            persisted);
    }

    private async Task<BillingV2ProvisioningReadinessReviewSubject>
        LoadReadinessReviewSubjectAsync(
            string customerId,
            CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT is_demo
            FROM customers
            WHERE id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull
            ? BillingV2ProvisioningReadinessReviewSubject.NotFound
            : new BillingV2ProvisioningReadinessReviewSubject(
                Exists: true,
                IsDemo: Convert.ToBoolean(value));
    }

    private async Task PersistReadinessReviewAsync(
        string customerId,
        string reviewedByReference,
        BillingV2ProvisioningReadinessReviewDecision decision,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO billing_v2_provisioning_client_readiness (
                customer_id,
                ready_for_v2_provisioning,
                add_only_mode,
                last_shadow_status,
                last_shadow_matches_legacy,
                unresolved_mismatch_count,
                reviewed_by_reference,
                reviewed_at,
                notes,
                created_at,
                updated_at)
            VALUES (
                @customer_id,
                @ready,
                1,
                @shadow_status,
                @shadow_matches_legacy,
                @unresolved_mismatch_count,
                @reviewed_by_reference,
                UTC_TIMESTAMP(6),
                @notes,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE
                ready_for_v2_provisioning = VALUES(ready_for_v2_provisioning),
                add_only_mode = 1,
                last_shadow_status = VALUES(last_shadow_status),
                last_shadow_matches_legacy = VALUES(last_shadow_matches_legacy),
                unresolved_mismatch_count = VALUES(unresolved_mismatch_count),
                reviewed_by_reference = VALUES(reviewed_by_reference),
                reviewed_at = VALUES(reviewed_at),
                notes = VALUES(notes),
                updated_at = UTC_TIMESTAMP(6);
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        command.Parameters.AddWithValue("@ready", decision.Ready ? 1 : 0);
        command.Parameters.AddWithValue("@shadow_status", decision.ShadowStatus);
        command.Parameters.AddWithValue(
            "@shadow_matches_legacy",
            decision.ShadowMatchesLegacy ? 1 : 0);
        command.Parameters.AddWithValue(
            "@unresolved_mismatch_count",
            decision.UnresolvedMismatchCount);
        command.Parameters.AddWithValue(
            "@reviewed_by_reference",
            reviewedByReference.Trim());
        command.Parameters.AddWithValue(
            "@notes",
            "review_reason_codes="
                + (decision.ReasonCodes.Count == 0
                    ? "none"
                    : string.Join(",", decision.ReasonCodes)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record BillingV2ProvisioningReadinessReviewSubject(
        bool Exists,
        bool IsDemo)
    {
        public static BillingV2ProvisioningReadinessReviewSubject NotFound { get; }
            = new(Exists: false, IsDemo: false);
    }
}
