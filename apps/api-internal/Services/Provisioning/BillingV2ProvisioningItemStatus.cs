using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record BillingV2ProvisioningItemStatusUpdate(
    string SubscriptionItemId,
    string Status,
    string? LastError,
    bool SetProvisionedAt);

public static class BillingV2ProvisioningItemStatusPolicy
{
    public const string Provisioned = "provisioned";
    public const string Failed = "failed";

    public static IReadOnlyList<BillingV2ProvisioningItemStatusUpdate> Acknowledged(
        BillingV2ProvisioningPlan plan)
        => plan.Users
            .SelectMany(user => user.UserInheritedCoverages.Concat(user.UserEntitlements))
            .Concat(plan.SubscriptionResources.InheritedCoverages)
            .Concat(plan.SubscriptionResources.Entitlements)
            .Concat(plan.SubscriptionResources.UnassignedUserSlots)
            .Select(entitlement => entitlement.SubscriptionItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new BillingV2ProvisioningItemStatusUpdate(
                id, Provisioned, LastError: null, SetProvisionedAt: true))
            .ToArray();

    public static IReadOnlyList<BillingV2ProvisioningItemStatusUpdate> Storage(
        BillingV2KoxoStorageApplyResult result)
        => result.Results
            .Where(item => !string.IsNullOrWhiteSpace(item.SubscriptionItemId))
            .Select(item => new BillingV2ProvisioningItemStatusUpdate(
                item.SubscriptionItemId,
                item.Succeeded ? Provisioned : Failed,
                item.Succeeded ? null : item.ReasonCode,
                SetProvisionedAt: item.Succeeded))
            .ToArray();

    public static IReadOnlyList<BillingV2ProvisioningItemStatusUpdate> ActiveDirectory(
        IReadOnlyList<string> subscriptionItemIds,
        ProvisioningExecutionResult result)
        => subscriptionItemIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new BillingV2ProvisioningItemStatusUpdate(
                id,
                result.Succeeded ? Provisioned : Failed,
                result.Succeeded ? null : result.ResultCode,
                SetProvisionedAt: result.Succeeded))
            .ToArray();
}

public sealed partial class BillingV2ProvisioningService
{
    private Task MarkAcknowledgedEntitlementsAsync(
        BillingV2ProvisioningPlan plan,
        CancellationToken cancellationToken)
        => PersistItemStatusesAsync(
            BillingV2ProvisioningItemStatusPolicy.Acknowledged(plan),
            cancellationToken);

    private Task PersistStorageStatusesAsync(
        BillingV2KoxoStorageApplyResult result,
        CancellationToken cancellationToken)
        => PersistItemStatusesAsync(
            BillingV2ProvisioningItemStatusPolicy.Storage(result),
            cancellationToken);

    private async Task<IReadOnlyList<string>> LoadActiveAdGroupItemIdsAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT item.id
            FROM billing_v2_subscription_items item
            INNER JOIN billing_v2_subscriptions sub
                ON sub.id = item.subscription_id
               AND sub.status = 'active'
            INNER JOIN billing_v2_services service
                ON service.id = item.service_id
               AND service.status = 'active'
            INNER JOIN billing_v2_provisioning_rules rule
                ON rule.service_id = service.id
               AND rule.status = 'active'
               AND (rule.tier_id IS NULL OR rule.tier_id = item.tier_id)
            WHERE sub.customer_id = @customer_id
              AND item.status = 'active'
              AND item.effective_from <= UTC_TIMESTAMP(6)
              AND (item.effective_until IS NULL OR item.effective_until > UTC_TIMESTAMP(6))
              AND rule.rule_type = 'ad_group_membership'
            ORDER BY item.id;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(MariaDbIdentifierReader.ReadRequired(reader, "id"));
        }
        return ids;
    }

    private async Task PersistAdStatusesAsync(
        string customerId,
        ProvisioningExecutionResult result,
        CancellationToken cancellationToken)
    {
        var itemIds = await LoadActiveAdGroupItemIdsAsync(customerId, cancellationToken);
        await PersistItemStatusesAsync(
            BillingV2ProvisioningItemStatusPolicy.ActiveDirectory(itemIds, result),
            cancellationToken);
    }

    private async Task PersistItemStatusesAsync(
        IReadOnlyList<BillingV2ProvisioningItemStatusUpdate> updates,
        CancellationToken cancellationToken)
    {
        if (updates.Count == 0)
        {
            return;
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var update in updates)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE billing_v2_subscription_item_provisioning
                SET provisioning_status = @status,
                    last_provisioned_at = CASE
                        WHEN @set_provisioned_at = 1 THEN UTC_TIMESTAMP(6)
                        ELSE last_provisioned_at
                    END,
                    last_error = @last_error,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE subscription_item_id = @subscription_item_id;
                """;
            command.Parameters.AddWithValue("@status", update.Status);
            command.Parameters.AddWithValue("@set_provisioned_at", update.SetProvisionedAt ? 1 : 0);
            command.Parameters.AddWithValue("@last_error", (object?)update.LastError ?? DBNull.Value);
            command.Parameters.AddWithValue("@subscription_item_id", update.SubscriptionItemId);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Billing V2 provisioning item status update expected one row for {update.SubscriptionItemId}, got {affected}.");
            }
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
