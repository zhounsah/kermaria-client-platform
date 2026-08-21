using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2FulfillmentDispatchResult(
    string SubscriptionItemId,
    string ResultCode,
    bool Changed);

/// <summary>
/// Dispatcher local du fulfillment V2.1. Il ne touche jamais aux droits ni
/// aux lignes financieres : un settlement cree au plus un travail humain ou
/// technique. Le backend MANUAL reste volontairement pending.
/// </summary>
public sealed class BillingV2FulfillmentDispatcher
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _configuration;

    public BillingV2FulfillmentDispatcher(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration configuration)
    {
        _sql = sql;
        _configuration = configuration;
    }

    public async Task<BillingV2FulfillmentDispatchResult> DispatchAsync(
        string subscriptionItemId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!_configuration.ServiceFulfillmentEnabled)
        {
            return new BillingV2FulfillmentDispatchResult(
                subscriptionItemId, "BILLING_V2_FULFILLMENT_DISABLED", false);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var read = connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText =
            """
            SELECT backend, fulfillment_status
            FROM billing_v2_subscription_item_fulfillment
            WHERE subscription_item_id = @item_id
            FOR UPDATE;
            """;
        read.Parameters.AddWithValue("@item_id", subscriptionItemId);
        await using var reader = await read.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new BillingV2FulfillmentDispatchResult(
                subscriptionItemId, "BILLING_V2_FULFILLMENT_NOT_FOUND", false);
        }

        var backend = reader.GetString("backend");
        var current = reader.GetString("fulfillment_status");
        await reader.CloseAsync();
        if (!string.Equals(current, BillingV2FulfillmentPolicy.Pending, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return new BillingV2FulfillmentDispatchResult(
                subscriptionItemId, "BILLING_V2_FULFILLMENT_ALREADY_DISPATCHED", false);
        }
        if (string.Equals(backend, "MANUAL", StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken);
            return new BillingV2FulfillmentDispatchResult(
                subscriptionItemId, "BILLING_V2_FULFILLMENT_MANUAL_PENDING", false);
        }

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText =
            """
            UPDATE billing_v2_subscription_item_fulfillment
            SET fulfillment_status = 'in_progress', started_at = @now,
                updated_at = @now, last_error = NULL
            WHERE subscription_item_id = @item_id
              AND fulfillment_status = 'pending';
            """;
        update.Parameters.AddWithValue("@item_id", subscriptionItemId);
        update.Parameters.AddWithValue("@now", nowUtc);
        var changed = await update.ExecuteNonQueryAsync(cancellationToken) == 1;
        await transaction.CommitAsync(cancellationToken);
        return new BillingV2FulfillmentDispatchResult(
            subscriptionItemId,
            changed ? "BILLING_V2_FULFILLMENT_DISPATCHED" : "BILLING_V2_FULFILLMENT_RACE_LOST",
            changed);
    }
}
