using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.ActiveDirectory;

namespace Kermaria.ApiInternal.Services.Provisioning;

/// <summary>
/// Reconciliation Active Directory d'un abonnement Billing V2, declenchee
/// manuellement depuis le back-office.
/// </summary>
/// <remarks>
/// <para>
/// A distinguer de <see cref="IBillingV2ProvisioningService"/>, qui reconcilie
/// automatiquement apres activation fournisseur et applique les regles par
/// utilisateur. Ce manager-ci sert l'action d'exploitation : un administrateur
/// demande explicitement la remise en conformite d'un abonnement, et veut voir
/// le detail de ce qui a change.
/// </para>
/// <para>
/// L'ensemble de groupes reconcilie est celui de <b>tous</b> les abonnements
/// actifs du client, pas seulement de celui qu'on regarde : retirer un
/// utilisateur d'un groupe parce qu'un abonnement ne le porte pas lui
/// retirerait un droit qu'un autre abonnement paie.
/// </para>
/// </remarks>
public interface IBillingV2SubscriptionProvisioningManager
{
    Task<SubscriptionProvisioningSummary> GetSummaryAsync(
        SubscriptionSummary subscription,
        CancellationToken cancellationToken);

    Task<SubscriptionProvisioningSummary> ReconcileAsync(
        SubscriptionSummary subscription,
        string actionType,
        string correlationId,
        string? requestedByUserId,
        IReadOnlyList<string>? targetUserSamAccountNames,
        CancellationToken cancellationToken);
}

public sealed class BillingV2SubscriptionProvisioningManager
    : IBillingV2SubscriptionProvisioningManager
{
    private readonly IBillingV2PortalSubscriptionProjection _subscriptions;
    private readonly IBillingV2SubscriptionAdGroupProjection _subscriptionGroups;
    private readonly IActiveDirectoryLinkRepository _links;
    private readonly ISubscriptionProvisioningActionRepository _actions;
    private readonly IProvisioningService _provisioningService;
    private readonly IServiceTopologyService _topologyService;
    private readonly IActiveDirectoryService _activeDirectory;
    private readonly SubscriptionProvisioningRuntimeConfiguration _configuration;
    private readonly IAdGroupProvisioner _groupProvisioner;
    private readonly ILogger<BillingV2SubscriptionProvisioningManager> _logger;

    public BillingV2SubscriptionProvisioningManager(
        IBillingV2PortalSubscriptionProjection subscriptions,
        IBillingV2SubscriptionAdGroupProjection subscriptionGroups,
        IActiveDirectoryLinkRepository links,
        ISubscriptionProvisioningActionRepository actions,
        IProvisioningService provisioningService,
        IServiceTopologyService topologyService,
        IActiveDirectoryService activeDirectory,
        SubscriptionProvisioningRuntimeConfiguration configuration,
        IAdGroupProvisioner groupProvisioner,
        ILogger<BillingV2SubscriptionProvisioningManager> logger)
    {
        _subscriptions = subscriptions;
        _subscriptionGroups = subscriptionGroups;
        _links = links;
        _actions = actions;
        _provisioningService = provisioningService;
        _topologyService = topologyService;
        _activeDirectory = activeDirectory;
        _configuration = configuration;
        _groupProvisioner = groupProvisioner;
        _logger = logger;
    }

    public async Task<SubscriptionProvisioningSummary> GetSummaryAsync(
        SubscriptionSummary subscription,
        CancellationToken cancellationToken)
    {
        var context = await BuildContextAsync(
            subscription,
            targetUserSamAccountNames: null,
            cancellationToken);
        var recentActions = await _actions.GetRecentBySubscriptionAsync(
            subscription.Id,
            limit: 10,
            cancellationToken);
        return BuildSummary(context, recentActions);
    }

    public async Task<SubscriptionProvisioningSummary> ReconcileAsync(
        SubscriptionSummary subscription,
        string actionType,
        string correlationId,
        string? requestedByUserId,
        IReadOnlyList<string>? targetUserSamAccountNames,
        CancellationToken cancellationToken)
    {
        var context = await BuildContextAsync(
            subscription,
            targetUserSamAccountNames,
            cancellationToken);
        var actionCreate = await _actions.CreateRequestedAsync(
            new SubscriptionProvisioningActionCreateRequest(
                subscription.Id,
                subscription.CustomerId,
                requestedByUserId,
                actionType,
                ResolveTargetReference(
                    subscription.CustomerReference,
                    context.TargetUsers),
                correlationId,
                ComputeIdempotencyKeyHash(context),
                SerializeDetails(context, null)),
            cancellationToken);
        var actionId = actionCreate.ActionId;
        if (!actionCreate.Created)
        {
            _logger.LogInformation(
                "Billing V2 subscription provisioning {ActionType} skipped duplicate active action {ActionId} for subscription {SubscriptionId}",
                actionType,
                actionId,
                subscription.Id);
            var recentDuplicateActions = await _actions.GetRecentBySubscriptionAsync(
                subscription.Id,
                limit: 10,
                cancellationToken);
            return BuildSummary(context, recentDuplicateActions);
        }

        await _actions.MarkStartedAsync(actionId, cancellationToken);

        try
        {
            var executionResult = await ExecuteAsync(context, cancellationToken);
            await _actions.MarkCompletedAsync(
                actionId,
                executionResult.Succeeded
                    ? executionResult.Changed
                        ? "succeeded"
                        : "unchanged"
                    : "failed",
                executionResult.ResultCode,
                executionResult.Changed,
                SerializeDetails(context, executionResult),
                cancellationToken);

            _logger.LogInformation(
                "Billing V2 subscription provisioning {ActionType} completed for subscription {SubscriptionId} customer {CustomerReference} status={Status} code={Code} changed={Changed}",
                actionType,
                subscription.Id,
                subscription.CustomerReference,
                executionResult.Succeeded
                    ? executionResult.Changed
                        ? "succeeded"
                        : "unchanged"
                    : "failed",
                executionResult.ResultCode,
                executionResult.Changed);

            var recentActions = await _actions.GetRecentBySubscriptionAsync(
                subscription.Id,
                limit: 10,
                cancellationToken);
            return BuildSummary(context, recentActions);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Billing V2 subscription provisioning {ActionType} crashed for subscription {SubscriptionId}",
                actionType,
                subscription.Id);
            await _actions.MarkCompletedAsync(
                actionId,
                "failed",
                "PROVISIONING_INTERNAL_ERROR",
                false,
                SerializeDetails(
                    context,
                    new ProvisioningExecutionResult(
                        false,
                        false,
                        "PROVISIONING_INTERNAL_ERROR",
                        Array.Empty<ProvisioningOperationResult>())),
                cancellationToken);

            var recentActions = await _actions.GetRecentBySubscriptionAsync(
                subscription.Id,
                limit: 10,
                cancellationToken);
            return BuildSummary(context, recentActions);
        }
    }

    private async Task<BillingV2SubscriptionProvisioningContext> BuildContextAsync(
        SubscriptionSummary subscription,
        IReadOnlyList<string>? targetUserSamAccountNames,
        CancellationToken cancellationToken)
    {
        var groupsBySubscription =
            await _subscriptionGroups.GetGroupsBySubscriptionAsync(
                subscription.CustomerId,
                cancellationToken);
        var customerSubscriptions = await _subscriptions.GetClientSubscriptionsAsync(
            subscription.CustomerId,
            cancellationToken);
        var mappedGroups = groupsBySubscription.TryGetValue(
            subscription.Id,
            out var ownGroups)
            ? ownGroups
            : Array.Empty<string>();
        var reconciledGroups = new SortedSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in customerSubscriptions.Where(candidate =>
                     string.Equals(candidate.Status, "active", StringComparison.Ordinal)))
        {
            if (!groupsBySubscription.TryGetValue(candidate.Id, out var groups))
            {
                continue;
            }

            foreach (var group in groups)
            {
                reconciledGroups.Add(group);
            }
        }

        var targetUsers = await _links.GetCustomerUserLinksAsync(
            subscription.CustomerId,
            cancellationToken);
        var targetUserFilter = targetUserSamAccountNames?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (targetUserFilter is not null && targetUserFilter.Count > 0)
        {
            targetUsers = targetUsers
                .Where(user => targetUserFilter.Contains(user.SamAccountName))
                .ToArray();
        }

        var managedGroups = await _topologyService.GetManagedGroupSamAccountNamesAsync(
            cancellationToken);
        var groupDns = managedGroups.ToDictionary(
            group => group,
            group =>
            {
                _configuration.TryGetGroupDistinguishedName(group, out var dn);
                return (string?)dn;
            },
            StringComparer.OrdinalIgnoreCase);
        var effectiveGroups = await LoadEffectiveGroupsByUserSamAsync(
            subscription.CustomerReference,
            targetUsers,
            cancellationToken);

        return new BillingV2SubscriptionProvisioningContext(
            subscription,
            mappedGroups,
            reconciledGroups.ToArray(),
            managedGroups,
            targetUsers,
            groupDns,
            effectiveGroups.GroupsByUserSam,
            effectiveGroups.IsComplete);
    }

    private async Task<ProvisioningExecutionResult> ExecuteAsync(
        BillingV2SubscriptionProvisioningContext context,
        CancellationToken cancellationToken)
    {
        if (context.ManagedGroups.Count == 0)
        {
            return new ProvisioningExecutionResult(
                true,
                false,
                "PROVISIONING_MAPPING_EMPTY",
                Array.Empty<ProvisioningOperationResult>());
        }

        if (context.TargetUsers.Count == 0
            && (context.MappedGroups.Count > 0
                || context.ReconciledGroups.Count > 0))
        {
            return new ProvisioningExecutionResult(
                false,
                false,
                "PROVISIONING_NO_TARGET_USERS",
                Array.Empty<ProvisioningOperationResult>());
        }

        if (context.TargetUsers.Count == 0)
        {
            return new ProvisioningExecutionResult(
                true,
                false,
                "PROVISIONING_NOT_REQUIRED",
                Array.Empty<ProvisioningOperationResult>());
        }

        return await _provisioningService.ReconcileAsync(
            new ProvisioningExecutionRequest(
                context.TargetUsers,
                context.ReconciledGroups,
                context.ManagedGroups,
                context.GroupDistinguishedNamesBySamAccountName),
            cancellationToken);
    }

    private SubscriptionProvisioningSummary BuildSummary(
        BillingV2SubscriptionProvisioningContext context,
        IReadOnlyList<SubscriptionProvisioningActionSummary> recentActions)
    {
        var lastAction = recentActions.FirstOrDefault();
        var status = ResolveSummaryStatus(context, lastAction);
        var canRetry = status is not "not_required" and not "not_configured"
            && (context.MappedGroups.Count > 0 || context.ReconciledGroups.Count > 0);
        var displayResultCode = ResolveDisplayResultCode(
            context,
            status,
            lastAction);

        return new SubscriptionProvisioningSummary(
            status,
            context.MappedGroups,
            context.ReconciledGroups,
            context.TargetUsers
                .Select(user => new SubscriptionProvisioningTargetUserSummary(
                    user.SamAccountName,
                    user.DisplayName,
                    user.UserPrincipalName))
                .ToArray(),
            canRetry,
            displayResultCode,
            recentActions);
    }

    private string ResolveSummaryStatus(
        BillingV2SubscriptionProvisioningContext context,
        SubscriptionProvisioningActionSummary? lastAction)
    {
        if (context.MappedGroups.Count == 0
            && context.ReconciledGroups.Count == 0)
        {
            return "not_required";
        }

        if (_groupProvisioner.RequiresConfiguredGroupDistinguishedNames
            && context.MappedGroups.Any(group =>
                !context.GroupDistinguishedNamesBySamAccountName.TryGetValue(
                    group,
                    out var distinguishedName)
                || string.IsNullOrWhiteSpace(distinguishedName)))
        {
            return "not_configured";
        }

        if (IsCurrentlySynchronized(context))
        {
            return "succeeded";
        }

        return lastAction?.Status switch
        {
            "failed" => "failed",
            "succeeded" or "unchanged" => "succeeded",
            _ => "ready"
        };
    }

    private async Task<BillingV2EffectiveGroupSnapshot>
        LoadEffectiveGroupsByUserSamAsync(
            string customerReference,
            IReadOnlyList<CustomerAdLinkSummary> targetUsers,
            CancellationToken cancellationToken)
    {
        if (targetUsers.Count == 0)
        {
            return new BillingV2EffectiveGroupSnapshot(
                new Dictionary<string, HashSet<string>>(
                    StringComparer.OrdinalIgnoreCase),
                IsComplete: true);
        }

        var adStatus = await _activeDirectory.GetStatusAsync(cancellationToken);
        if (!adStatus.ReadsEnabled)
        {
            return new BillingV2EffectiveGroupSnapshot(
                new Dictionary<string, HashSet<string>>(
                    StringComparer.OrdinalIgnoreCase),
                IsComplete: false);
        }

        var groupsByUserSam =
            new Dictionary<string, HashSet<string>>(
                StringComparer.OrdinalIgnoreCase);
        foreach (var user in targetUsers)
        {
            var result = await _activeDirectory.GetUserEffectiveGroupsAsync(
                customerReference,
                user.SamAccountName,
                cancellationToken);
            if (result.StatusCode >= 400 || result.Value is null)
            {
                return new BillingV2EffectiveGroupSnapshot(groupsByUserSam, false);
            }

            groupsByUserSam[user.SamAccountName] = result.Value
                .Select(group => group.SamAccountName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return new BillingV2EffectiveGroupSnapshot(groupsByUserSam, true);
    }

    private static bool IsCurrentlySynchronized(
        BillingV2SubscriptionProvisioningContext context)
    {
        if (!context.EffectiveGroupsComplete)
        {
            return false;
        }

        if (context.TargetUsers.Count == 0 || context.ReconciledGroups.Count == 0)
        {
            return false;
        }

        return context.TargetUsers.All(user =>
            context.EffectiveGroupsByUserSam.TryGetValue(
                user.SamAccountName,
                out var groups)
            && context.ReconciledGroups.All(groups.Contains));
    }

    private static string? ResolveDisplayResultCode(
        BillingV2SubscriptionProvisioningContext context,
        string status,
        SubscriptionProvisioningActionSummary? lastAction)
    {
        if (status == "succeeded"
            && IsCurrentlySynchronized(context)
            && lastAction?.Status == "failed")
        {
            return "PROVISIONING_SYNCHRONIZED";
        }

        return lastAction?.ResultCode;
    }

    private static string ComputeIdempotencyKeyHash(
        BillingV2SubscriptionProvisioningContext context)
    {
        using var sha256 = SHA256.Create();
        var payload = JsonSerializer.Serialize(new
        {
            subscriptionId = context.Subscription.Id,
            customerId = context.Subscription.CustomerId,
            reconciledGroups = context.ReconciledGroups,
            targetUsers = context.TargetUsers
                .Select(user => user.SamAccountName)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        });
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string SerializeDetails(
        BillingV2SubscriptionProvisioningContext context,
        ProvisioningExecutionResult? executionResult)
        => JsonSerializer.Serialize(new
        {
            subscriptionId = context.Subscription.Id,
            presetCode = context.Subscription.PresetCode,
            mappedGroups = context.MappedGroups,
            reconciledGroups = context.ReconciledGroups,
            targetUsers = context.TargetUsers.Select(user => new
            {
                user.SamAccountName,
                user.DisplayName,
                user.UserPrincipalName
            }),
            result = executionResult is null
                ? null
                : new
                {
                    executionResult.Succeeded,
                    executionResult.Changed,
                    executionResult.ResultCode,
                    operations = executionResult.Operations
                }
        });

    private static string ResolveTargetReference(
        string customerReference,
        IReadOnlyList<CustomerAdLinkSummary> targetUsers)
        => targetUsers.Count == 1
            ? targetUsers[0].SamAccountName
            : customerReference;
}

public sealed record BillingV2SubscriptionProvisioningContext(
    SubscriptionSummary Subscription,
    IReadOnlyList<string> MappedGroups,
    IReadOnlyList<string> ReconciledGroups,
    IReadOnlyList<string> ManagedGroups,
    IReadOnlyList<CustomerAdLinkSummary> TargetUsers,
    IReadOnlyDictionary<string, string?> GroupDistinguishedNamesBySamAccountName,
    IReadOnlyDictionary<string, HashSet<string>> EffectiveGroupsByUserSam,
    bool EffectiveGroupsComplete);

internal sealed record BillingV2EffectiveGroupSnapshot(
    IReadOnlyDictionary<string, HashSet<string>> GroupsByUserSam,
    bool IsComplete);
