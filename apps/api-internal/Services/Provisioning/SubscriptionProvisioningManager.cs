using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services.Provisioning;

public interface ISubscriptionProvisioningManager
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

public sealed class SubscriptionProvisioningManager
    : ISubscriptionProvisioningManager
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IActiveDirectoryLinkRepository _links;
    private readonly ISubscriptionProvisioningActionRepository _actions;
    private readonly IProvisioningService _provisioningService;
    private readonly ICommercialOfferTopologyService _topologyService;
    private readonly SubscriptionProvisioningRuntimeConfiguration _configuration;
    private readonly IAdGroupProvisioner _groupProvisioner;
    private readonly ILogger<SubscriptionProvisioningManager> _logger;

    public SubscriptionProvisioningManager(
        ISubscriptionRepository subscriptions,
        IActiveDirectoryLinkRepository links,
        ISubscriptionProvisioningActionRepository actions,
        IProvisioningService provisioningService,
        ICommercialOfferTopologyService topologyService,
        SubscriptionProvisioningRuntimeConfiguration configuration,
        IAdGroupProvisioner groupProvisioner,
        ILogger<SubscriptionProvisioningManager> logger)
    {
        _subscriptions = subscriptions;
        _links = links;
        _actions = actions;
        _provisioningService = provisioningService;
        _topologyService = topologyService;
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
        var actionId = await _actions.CreateRequestedAsync(
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
        await _actions.MarkStartedAsync(actionId, cancellationToken);

        try
        {
            var executionResult = await ExecuteAsync(
                context,
                cancellationToken);
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
                "Subscription provisioning {ActionType} completed for subscription {SubscriptionId} customer {CustomerReference} status={Status} code={Code} changed={Changed}",
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
                "Subscription provisioning {ActionType} crashed for subscription {SubscriptionId}",
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

    private async Task<SubscriptionProvisioningContext> BuildContextAsync(
        SubscriptionSummary subscription,
        IReadOnlyList<string>? targetUserSamAccountNames,
        CancellationToken cancellationToken)
    {
        var customerSubscriptions = await _subscriptions.GetByCustomerAsync(
            subscription.CustomerId,
            cancellationToken);
        var activeSubscriptions = customerSubscriptions
            .Where(candidate => string.Equals(
                candidate.Status,
                "active",
                StringComparison.Ordinal))
            .ToArray();
        var mappedGroups = await _topologyService.ResolveMappedGroupsAsync(
            subscription,
            cancellationToken);
        var reconciledGroups = new SortedSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in activeSubscriptions)
        {
            foreach (var group in await _topologyService.ResolveMappedGroupsAsync(
                         candidate,
                         cancellationToken))
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

        return new SubscriptionProvisioningContext(
            subscription,
            mappedGroups,
            reconciledGroups.ToArray(),
            managedGroups,
            targetUsers,
            groupDns);
    }

    private async Task<ProvisioningExecutionResult> ExecuteAsync(
        SubscriptionProvisioningContext context,
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
        SubscriptionProvisioningContext context,
        IReadOnlyList<SubscriptionProvisioningActionSummary> recentActions)
    {
        var lastAction = recentActions.FirstOrDefault();
        var status = ResolveSummaryStatus(context, lastAction);
        var canRetry = status is not "not_required" and not "not_configured"
            && (context.MappedGroups.Count > 0 || context.ReconciledGroups.Count > 0);

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
            lastAction?.ResultCode,
            recentActions);
    }

    private string ResolveSummaryStatus(
        SubscriptionProvisioningContext context,
        SubscriptionProvisioningActionSummary? lastAction)
    {
        if (string.IsNullOrWhiteSpace(
                context.Subscription.OfferExternalReference))
        {
            return "not_configured";
        }

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

        return lastAction?.Status switch
        {
            "failed" => "failed",
            "succeeded" or "unchanged" => "succeeded",
            _ => "ready"
        };
    }

    private static string ComputeIdempotencyKeyHash(
        SubscriptionProvisioningContext context)
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
        SubscriptionProvisioningContext context,
        ProvisioningExecutionResult? executionResult)
    {
        return JsonSerializer.Serialize(new
        {
            subscriptionId = context.Subscription.Id,
            offerExternalReference = context.Subscription.OfferExternalReference,
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
    }

    private static string ResolveTargetReference(
        string customerReference,
        IReadOnlyList<CustomerAdLinkSummary> targetUsers)
    {
        if (targetUsers.Count == 0)
        {
            return customerReference;
        }

        if (targetUsers.Count == 1)
        {
            return targetUsers[0].SamAccountName;
        }

        return customerReference;
    }
}

internal sealed record SubscriptionProvisioningContext(
    SubscriptionSummary Subscription,
    IReadOnlyList<string> MappedGroups,
    IReadOnlyList<string> ReconciledGroups,
    IReadOnlyList<string> ManagedGroups,
    IReadOnlyList<CustomerAdLinkSummary> TargetUsers,
    IReadOnlyDictionary<string, string?> GroupDistinguishedNamesBySamAccountName);
