using MySqlConnector;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record BillingV2ProvisioningReadinessReviewInputs(
    bool PersistentSqlAvailable,
    bool CustomerExists,
    bool CustomerIsDemo,
    int ActiveV2SubscriptionCount,
    int UnresolvedRuleCount,
    bool TargetGroupsResolved,
    bool StorageProviderReady,
    bool StorageTargetsResolved,
    bool AdTargetsResolved);

public sealed record BillingV2ProvisioningReadinessReviewDecision(
    bool Ready,
    bool AddOnlyMode,
    string ReviewStatus,
    int UnresolvedMismatchCount,
    IReadOnlyList<string> ReasonCodes)
{
    public string ReasonCode => Ready
        ? BillingV2ProvisioningReadinessReviewReasons.Ready
        : ReasonCodes.FirstOrDefault()
            ?? BillingV2ProvisioningReadinessReviewReasons.ReviewFailed;
}

/// <summary>
/// Etat de résolution d'une cible nécessaire au provisioning, construit sans
/// appeler le provider et sans écrire dans la persistance.
/// </summary>
public sealed record BillingV2ProvisioningReadinessTargetStatus(
    bool Required,
    bool Evaluated,
    bool Resolved,
    string? ReasonCode);

public sealed record BillingV2ProvisioningReadinessGroupStatus(
    string GroupSamAccountName,
    bool Resolved);

/// <summary>
/// Verdict non persistant de la readiness customer. Les valeurs qui servent à
/// la review persistante sont calculées une seule fois ici ; une preview et une
/// review ne peuvent donc pas diverger par une policy différente.
/// </summary>
public sealed record BillingV2ProvisioningClientReadinessEvaluation(
    BillingV2ProvisioningReadinessReviewDecision Decision,
    bool PersistentSqlAvailable,
    bool CustomerExists,
    bool CustomerIsDemo,
    int ActiveV2SubscriptionCount,
    IReadOnlyList<string> UnresolvedRuleReferences,
    IReadOnlyList<BillingV2ProvisioningReadinessGroupStatus> DesiredAdGroups,
    int StorageTargetCount,
    BillingV2KoxoStorageReadiness StorageProviderReadiness,
    BillingV2ProvisioningReadinessTargetStatus StorageTargets,
    BillingV2ProvisioningReadinessTargetStatus AdTargets)
{
    public bool Ready => Decision.Ready;
    public bool AddOnlyMode => Decision.AddOnlyMode;
    public string ReviewStatus => Decision.ReviewStatus;
    public int UnresolvedMismatchCount => Decision.UnresolvedMismatchCount;
    public IReadOnlyList<string> ReasonCodes => Decision.ReasonCodes;
    public int DesiredAdGroupCount => DesiredAdGroups.Count;
    public BillingV2ProvisioningReadinessReviewResult ToReviewResult(bool persisted)
        => new(
            Ready,
            AddOnlyMode,
            ReviewStatus,
            UnresolvedMismatchCount,
            ReasonCodes,
            ActiveV2SubscriptionCount,
            DesiredAdGroupCount,
            StorageTargetCount,
            persisted);

    public static BillingV2ProvisioningClientReadinessEvaluation PersistenceUnavailable { get; }
        = new(
            new(
                Ready: false,
                AddOnlyMode: true,
                ReviewStatus: "failed",
                UnresolvedMismatchCount: 1,
                [BillingV2ProvisioningReadinessReviewReasons.PersistentSqlUnavailable]),
            PersistentSqlAvailable: false,
            CustomerExists: false,
            CustomerIsDemo: false,
            ActiveV2SubscriptionCount: 0,
            [],
            [],
            StorageTargetCount: 0,
            new(false, BillingV2KoxoStorageApplyReasons.ProviderNotConfigured),
            new(Required: false, Evaluated: false, Resolved: false,
                ReasonCode: BillingV2ProvisioningReadinessReviewReasons.PersistentSqlUnavailable),
            new(Required: false, Evaluated: false, Resolved: false,
                ReasonCode: BillingV2ProvisioningReadinessReviewReasons.PersistentSqlUnavailable));
}

public sealed record BillingV2ProvisioningReadinessReviewResult(
    bool Ready,
    bool AddOnlyMode,
    string ReviewStatus,
    int UnresolvedMismatchCount,
    IReadOnlyList<string> ReasonCodes,
    int ActiveV2SubscriptionCount,
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
            ReviewStatus: "failed",
            UnresolvedMismatchCount: 1,
            [BillingV2ProvisioningReadinessReviewReasons.PersistentSqlUnavailable],
            ActiveV2SubscriptionCount: 0,
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
        Reject(inputs.UnresolvedRuleCount > 0, BillingV2ProvisioningReadinessReviewReasons.RulesUnresolved);
        Reject(!inputs.TargetGroupsResolved, BillingV2ProvisioningReadinessReviewReasons.TargetGroupsUnresolved);
        Reject(!inputs.StorageProviderReady, BillingV2ProvisioningReadinessReviewReasons.StorageProviderNotReady);
        Reject(!inputs.StorageTargetsResolved, BillingV2ProvisioningReadinessReviewReasons.StorageTargetsUnresolved);
        Reject(!inputs.AdTargetsResolved, BillingV2ProvisioningReadinessReviewReasons.AdTargetsUnresolved);

        var ready = reasons.Count == 0;
        return new BillingV2ProvisioningReadinessReviewDecision(
            ready,
            AddOnlyMode: true,
            ReviewStatus: ready ? "success" : "failed",
            mismatchCount,
            reasons);
    }
}

public sealed partial class BillingV2ProvisioningService
{
    /// <summary>
    /// Evalue toutes les preconditions customer-scoped sans persister de
    /// readiness et sans appeler AD, KoXo ou le provisioning. Les resolveurs
    /// de cibles restent des lectures/probes pures.
    /// </summary>
    public async Task<BillingV2ProvisioningClientReadinessEvaluation>
        EvaluateClientReadinessAsync(
            string customerId,
            CancellationToken cancellationToken)
    {
        var persistentSqlAvailable =
            _sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString);
        if (!persistentSqlAvailable)
        {
            return BillingV2ProvisioningClientReadinessEvaluation.PersistenceUnavailable;
        }

        var subject = await LoadReadinessReviewSubjectAsync(
            customerId,
            cancellationToken);
        var activeV2SubscriptionIds = subject.Exists
            ? await LoadMaterializedActiveSubscriptionIdsAsync(
                customerId,
                cancellationToken)
            : new HashSet<string>(StringComparer.Ordinal);
        var plan = activeV2SubscriptionIds.Count > 0
            ? await LoadProvisioningPlanAsync(
                customerId,
                activeV2SubscriptionIds.ToArray(),
                cancellationToken)
            : BillingV2ProvisioningPlan.Empty;

        var desiredAdGroups = plan.AllDesiredAdGroups
            .Select(group => new BillingV2ProvisioningReadinessGroupStatus(
                group,
                _provisioningConfiguration.GroupDistinguishedNamesBySamAccountName
                    .TryGetValue(group, out var distinguishedName)
                && !string.IsNullOrWhiteSpace(distinguishedName)))
            .ToArray();
        var targetGroupsResolved = desiredAdGroups.All(group => group.Resolved);
        var storageProviderReadiness = _koxoStorageProvider
            .CheckReadiness(plan.StorageQuotaPlans);

        var storageTargets = await EvaluateStorageTargetsAsync(
            customerId,
            subject.Exists,
            activeV2SubscriptionIds.Count,
            plan,
            storageProviderReadiness,
            cancellationToken);
        var adTargets = await EvaluateAdTargetsAsync(
            customerId,
            subject.Exists,
            activeV2SubscriptionIds.Count,
            plan,
            cancellationToken);

        var decision = BillingV2ProvisioningReadinessReviewPolicy.Evaluate(
            new BillingV2ProvisioningReadinessReviewInputs(
                PersistentSqlAvailable: true,
                CustomerExists: subject.Exists,
                CustomerIsDemo: subject.IsDemo,
                ActiveV2SubscriptionCount: activeV2SubscriptionIds.Count,
                UnresolvedRuleCount: plan.UnresolvedRuleReferences.Count,
                targetGroupsResolved,
                storageProviderReadiness.CanApplyQuotas,
                storageTargets.Resolved,
                adTargets.Resolved));

        return new BillingV2ProvisioningClientReadinessEvaluation(
            decision,
            PersistentSqlAvailable: true,
            CustomerExists: subject.Exists,
            CustomerIsDemo: subject.IsDemo,
            ActiveV2SubscriptionCount: activeV2SubscriptionIds.Count,
            plan.UnresolvedRuleReferences,
            desiredAdGroups,
            plan.StorageQuotaPlans.Count,
            storageProviderReadiness,
            storageTargets,
            adTargets);
    }

    public async Task<BillingV2ProvisioningReadinessReviewResult>
        ReviewClientReadinessAsync(
            string customerId,
            string reviewedByReference,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reviewedByReference))
        {
            throw new ArgumentException("Reviewer reference is required for Billing V2 provisioning readiness review.");
        }
        var evaluation = await EvaluateClientReadinessAsync(customerId, cancellationToken);
        var persisted = false;
        if (evaluation.PersistentSqlAvailable && evaluation.CustomerExists)
        {
            await PersistReadinessReviewAsync(
                customerId,
                reviewedByReference,
                evaluation,
                cancellationToken);
            persisted = true;
        }
        return evaluation.ToReviewResult(persisted);
    }

    private async Task<BillingV2ProvisioningReadinessTargetStatus>
        EvaluateStorageTargetsAsync(
            string customerId,
            bool customerExists,
            int activeV2SubscriptionCount,
            BillingV2ProvisioningPlan plan,
            BillingV2KoxoStorageReadiness storageProviderReadiness,
            CancellationToken cancellationToken)
    {
        if (plan.StorageQuotaPlans.Count == 0)
        {
            return new(Required: false, Evaluated: true, Resolved: true,
                ReasonCode: BillingV2KoxoStorageApplyReasons.Noop);
        }
        if (!customerExists || activeV2SubscriptionCount <= 0
            || plan.UnresolvedRuleReferences.Count > 0
            || !storageProviderReadiness.CanApplyQuotas)
        {
            return new(Required: true, Evaluated: false, Resolved: false,
                ReasonCode: storageProviderReadiness.CanApplyQuotas
                    ? null
                    : storageProviderReadiness.ReasonCode);
        }
        var resolution = await _koxoStorageTargets.ResolveAsync(
            customerId,
            plan.StorageQuotaPlans,
            cancellationToken);
        return new(Required: true, Evaluated: true, resolution.Resolved,
            resolution.ReasonCode);
    }

    private async Task<BillingV2ProvisioningReadinessTargetStatus>
        EvaluateAdTargetsAsync(
            string customerId,
            bool customerExists,
            int activeV2SubscriptionCount,
            BillingV2ProvisioningPlan plan,
            CancellationToken cancellationToken)
    {
        if (plan.UsersRequiringAdIdentity.Count == 0)
        {
            return new(Required: false, Evaluated: true, Resolved: true,
                ReasonCode: "not_required");
        }
        if (!customerExists || activeV2SubscriptionCount <= 0
            || plan.UnresolvedRuleReferences.Count > 0)
        {
            return new(Required: true, Evaluated: false, Resolved: false,
                ReasonCode: null);
        }
        var customerUserLinks = await _activeDirectoryLinks.GetCustomerUserLinksAsync(
            customerId,
            cancellationToken);
        var resolution = await ResolveTargetsAsync(
            customerId,
            plan.UsersRequiringAdIdentity,
            customerUserLinks,
            cancellationToken);
        return new(Required: true, Evaluated: true, resolution.Resolved,
            resolution.ReasonCode);
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
        BillingV2ProvisioningClientReadinessEvaluation evaluation,
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
                last_review_status,
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
                @review_status,
                @unresolved_mismatch_count,
                @reviewed_by_reference,
                UTC_TIMESTAMP(6),
                @notes,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE
                ready_for_v2_provisioning = VALUES(ready_for_v2_provisioning),
                add_only_mode = 1,
                last_review_status = VALUES(last_review_status),
                unresolved_mismatch_count = VALUES(unresolved_mismatch_count),
                reviewed_by_reference = VALUES(reviewed_by_reference),
                reviewed_at = VALUES(reviewed_at),
                notes = VALUES(notes),
                updated_at = UTC_TIMESTAMP(6);
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        command.Parameters.AddWithValue("@ready", evaluation.Ready ? 1 : 0);
        command.Parameters.AddWithValue("@review_status", evaluation.ReviewStatus);
        command.Parameters.AddWithValue(
            "@unresolved_mismatch_count",
            evaluation.UnresolvedMismatchCount);
        command.Parameters.AddWithValue(
            "@reviewed_by_reference",
            reviewedByReference.Trim());
        command.Parameters.AddWithValue(
            "@notes",
            "review_reason_codes="
                + (evaluation.ReasonCodes.Count == 0
                    ? "none"
                    : string.Join(",", evaluation.ReasonCodes)));
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
