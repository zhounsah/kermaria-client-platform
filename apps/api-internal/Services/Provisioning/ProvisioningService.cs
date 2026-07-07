using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record ProvisioningOperationResult(
    string GroupSamAccountName,
    string UserSamAccountName,
    string Operation,
    string Code,
    bool Changed);

public sealed record ProvisioningExecutionResult(
    bool Succeeded,
    bool Changed,
    string ResultCode,
    IReadOnlyList<ProvisioningOperationResult> Operations);

public sealed record ProvisioningExecutionRequest(
    IReadOnlyList<CustomerAdLinkSummary> TargetUsers,
    IReadOnlyList<string> DesiredGroupSamAccountNames,
    IReadOnlyList<string> ManagedGroupSamAccountNames,
    IReadOnlyDictionary<string, string?> GroupDistinguishedNamesBySamAccountName);

public interface IProvisioningService
{
    Task<ProvisioningExecutionResult> ReconcileAsync(
        ProvisioningExecutionRequest request,
        CancellationToken cancellationToken);
}

public sealed class ProvisioningService : IProvisioningService
{
    private static readonly StringComparer GroupComparer =
        StringComparer.OrdinalIgnoreCase;

    private readonly IAdGroupProvisioner _groupProvisioner;
    private readonly SubscriptionProvisioningRuntimeConfiguration _configuration;

    public ProvisioningService(
        IAdGroupProvisioner groupProvisioner,
        SubscriptionProvisioningRuntimeConfiguration configuration)
    {
        _groupProvisioner = groupProvisioner;
        _configuration = configuration;
    }

    public async Task<ProvisioningExecutionResult> ReconcileAsync(
        ProvisioningExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ManagedGroupSamAccountNames.Count == 0)
        {
            return new ProvisioningExecutionResult(
                true,
                false,
                "PROVISIONING_MAPPING_EMPTY",
                Array.Empty<ProvisioningOperationResult>());
        }

        if (_groupProvisioner.RequiresConfiguredGroupDistinguishedNames)
        {
            var missingGroup = request.ManagedGroupSamAccountNames
                .FirstOrDefault(group =>
                    !request.GroupDistinguishedNamesBySamAccountName.TryGetValue(
                        group,
                        out var distinguishedName)
                    || string.IsNullOrWhiteSpace(distinguishedName));
            if (missingGroup is not null)
            {
                return new ProvisioningExecutionResult(
                    false,
                    false,
                    "PROVISIONING_GROUP_NOT_CONFIGURED",
                    Array.Empty<ProvisioningOperationResult>());
            }
        }

        var desiredGroups = new HashSet<string>(
            request.DesiredGroupSamAccountNames,
            GroupComparer);
        var operations = new List<ProvisioningOperationResult>();
        var changed = false;

        foreach (var groupSamAccountName in request.ManagedGroupSamAccountNames
            .Distinct(GroupComparer)
            .OrderBy(group => group, GroupComparer))
        {
            request.GroupDistinguishedNamesBySamAccountName.TryGetValue(
                groupSamAccountName,
                out var groupDistinguishedName);

            foreach (var user in request.TargetUsers
                .OrderBy(target => target.SamAccountName, StringComparer.OrdinalIgnoreCase))
            {
                var shouldAdd = desiredGroups.Contains(groupSamAccountName);
                var operation = shouldAdd ? "add" : "remove";
                var result = await ExecuteWithRetryAsync(
                    user,
                    groupSamAccountName,
                    groupDistinguishedName,
                    shouldAdd,
                    cancellationToken);

                operations.Add(new ProvisioningOperationResult(
                    groupSamAccountName,
                    user.SamAccountName,
                    operation,
                    result.Code,
                    result.Changed));
                changed |= result.Changed;

                if (result.StatusCode >= 400)
                {
                    return new ProvisioningExecutionResult(
                        false,
                        changed,
                        result.Code,
                        operations);
                }
            }
        }

        return new ProvisioningExecutionResult(
            true,
            changed,
            changed
                ? "PROVISIONING_APPLIED"
                : "PROVISIONING_UNCHANGED",
            operations);
    }

    private async Task<AdGroupProvisionerResult> ExecuteWithRetryAsync(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        string? groupDistinguishedName,
        bool shouldAdd,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = shouldAdd
                ? await _groupProvisioner.AddUserToGroupAsync(
                    user,
                    groupSamAccountName,
                    groupDistinguishedName,
                    cancellationToken)
                : await _groupProvisioner.RemoveUserFromGroupAsync(
                    user,
                    groupSamAccountName,
                    groupDistinguishedName,
                    cancellationToken);

            attempt++;
            if (attempt >= _configuration.MaxAttempts
                || !string.Equals(
                    result.Code,
                    "AD_UNAVAILABLE",
                    StringComparison.Ordinal))
            {
                return result;
            }

            if (_configuration.RetryDelayMs > 0)
            {
                await Task.Delay(
                    _configuration.RetryDelayMs * attempt,
                    cancellationToken);
            }
        }
    }
}
