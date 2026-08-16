using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2ProviderCheckoutCommandRequest(
    string SubscriptionId,
    string CustomerId,
    string CustomerEmail,
    string SuccessUrl,
    string CancelUrl,
    BillingV2CheckoutPlan CheckoutPlan,
    BillingV2CheckoutReadinessDecision Readiness,
    string CorrelationId,
    string? ActorReference);

public sealed record BillingV2ProviderCheckoutCommandPlan(
    string EventType,
    string AggregateType,
    string AggregateId,
    string IdempotencyKeyHash,
    string PayloadText);

public sealed record BillingV2ProviderCheckoutCommandResult(
    bool Created,
    string OutboxEventId,
    string IdempotencyKeyHash);

public interface IBillingV2ProviderCheckoutCommandService
{
    Task<BillingV2ProviderCheckoutCommandResult> QueueCreateCheckoutAsync(
        BillingV2ProviderCheckoutCommandRequest request,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2ProviderCheckoutCommandService
    : IBillingV2ProviderCheckoutCommandService
{
    public static NoOpBillingV2ProviderCheckoutCommandService Instance { get; }
        = new();

    private NoOpBillingV2ProviderCheckoutCommandService()
    {
    }

    public Task<BillingV2ProviderCheckoutCommandResult> QueueCreateCheckoutAsync(
        BillingV2ProviderCheckoutCommandRequest request,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException(
            "Billing V2 provider checkout command service is disabled.");
}

public sealed class BillingV2ProviderCheckoutCommandService
    : IBillingV2ProviderCheckoutCommandService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _configuration;

    public BillingV2ProviderCheckoutCommandService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration configuration)
    {
        _sql = sql;
        _configuration = configuration;
    }

    public async Task<BillingV2ProviderCheckoutCommandResult>
        QueueCreateCheckoutAsync(
            BillingV2ProviderCheckoutCommandRequest request,
            CancellationToken cancellationToken)
    {
        if (!_configuration.AuthoritativeCheckoutEnabled)
        {
            throw new InvalidOperationException(
                "Billing V2 authoritative checkout is disabled.");
        }

        if (!request.Readiness.Authorized)
        {
            throw new InvalidOperationException(
                $"Billing V2 checkout is not ready: {request.Readiness.ReasonCode}.");
        }

        if (!_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            throw new InvalidOperationException(
                "Billing V2 provider checkout requires persistent SQL.");
        }

        var plan = BillingV2ProviderCheckoutCommandPlanner.Plan(request);
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        var requestedEventId = Guid.NewGuid().ToString("D");
        await InsertOutboxEventAsync(
            connection,
            transaction,
            requestedEventId,
            plan,
            cancellationToken);
        var outboxEventId = await ReadOutboxEventIdByIdempotencyAsync(
            connection,
            transaction,
            plan.IdempotencyKeyHash,
            cancellationToken);
        var created = string.Equals(
            outboxEventId,
            requestedEventId,
            StringComparison.Ordinal);
        if (created)
        {
            await InsertAuditAsync(
                connection,
                transaction,
                request,
                plan,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new BillingV2ProviderCheckoutCommandResult(
            created,
            outboxEventId,
            plan.IdempotencyKeyHash);
    }

    private static async Task InsertOutboxEventAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string eventId,
        BillingV2ProviderCheckoutCommandPlan plan,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_outbox_events (
                id,
                aggregate_type,
                aggregate_id,
                event_type,
                payload_text,
                idempotency_key_hash,
                status,
                retry_count,
                available_at,
                created_at
            ) VALUES (
                @id,
                @aggregate_type,
                @aggregate_id,
                @event_type,
                @payload_text,
                @idempotency_key_hash,
                'pending',
                0,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            )
            ON DUPLICATE KEY UPDATE
                id = id;
            """;
        command.Parameters.AddWithValue("@id", eventId);
        command.Parameters.AddWithValue("@aggregate_type", plan.AggregateType);
        command.Parameters.AddWithValue("@aggregate_id", plan.AggregateId);
        command.Parameters.AddWithValue("@event_type", plan.EventType);
        command.Parameters.AddWithValue("@payload_text", plan.PayloadText);
        command.Parameters.AddWithValue(
            "@idempotency_key_hash",
            plan.IdempotencyKeyHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> ReadOutboxEventIdByIdempotencyAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string idempotencyKeyHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id
            FROM billing_v2_outbox_events
            WHERE idempotency_key_hash = @idempotency_key_hash
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(
            "@idempotency_key_hash",
            idempotencyKeyHash);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToString(result)
            ?? throw new InvalidOperationException(
                "Billing V2 provider checkout outbox event was not persisted.");
    }

    private static async Task InsertAuditAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2ProviderCheckoutCommandRequest request,
        BillingV2ProviderCheckoutCommandPlan plan,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_audit_log (
                id,
                entity_type,
                entity_id,
                action,
                actor_reference,
                details_text,
                created_at
            ) VALUES (
                @id,
                @entity_type,
                @entity_id,
                @action,
                @actor_reference,
                @details_text,
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@entity_type", plan.AggregateType);
        command.Parameters.AddWithValue("@entity_id", plan.AggregateId);
        command.Parameters.AddWithValue("@action", plan.EventType);
        command.Parameters.AddWithValue(
            "@actor_reference",
            string.IsNullOrWhiteSpace(request.ActorReference)
                ? DBNull.Value
                : request.ActorReference);
        command.Parameters.AddWithValue("@details_text", plan.PayloadText);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public static class BillingV2ProviderCheckoutCommandPlanner
{
    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web);

    public static BillingV2ProviderCheckoutCommandPlan Plan(
        BillingV2ProviderCheckoutCommandRequest request)
    {
        if (!request.Readiness.Authorized)
        {
            throw new InvalidOperationException(
                $"Billing V2 checkout is not ready: {request.Readiness.ReasonCode}.");
        }

        var payload = new ProviderCheckoutPayload(
            request.SubscriptionId,
            request.CustomerId,
            request.CustomerEmail,
            request.CheckoutPlan.Provider,
            request.CheckoutPlan.Environment,
            request.CheckoutPlan.Currency,
            request.CheckoutPlan.RecurringAmountCents,
            request.CheckoutPlan.OneTimeAmountCents,
            request.CheckoutPlan.TotalDueNowCents,
            request.SuccessUrl,
            request.CancelUrl,
            request.CorrelationId,
            request.CheckoutPlan.ProviderLines
                .Select(line => new ProviderCheckoutLinePayload(
                    line.ServicePriceId,
                    line.ProviderExternalId,
                    line.Quantity,
                    line.AmountCents))
                .ToArray());

        return new BillingV2ProviderCheckoutCommandPlan(
            "billing_v2.provider_checkout.create_requested",
            "billing_v2_subscription",
            request.SubscriptionId,
            ComputeIdempotencyHash(request),
            JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static string ComputeIdempotencyHash(
        BillingV2ProviderCheckoutCommandRequest request)
    {
        var raw = string.Join(
            "|",
            "billing-v2-provider-checkout",
            request.CheckoutPlan.Provider,
            request.CheckoutPlan.Environment,
            request.SubscriptionId);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record ProviderCheckoutPayload(
        string SubscriptionId,
        string CustomerId,
        string CustomerEmail,
        string Provider,
        string Environment,
        string Currency,
        long RecurringAmountCents,
        long OneTimeAmountCents,
        long TotalDueNowCents,
        string SuccessUrl,
        string CancelUrl,
        string CorrelationId,
        IReadOnlyList<ProviderCheckoutLinePayload> Lines);

    private sealed record ProviderCheckoutLinePayload(
        string ServicePriceId,
        string ProviderExternalId,
        int Quantity,
        long AmountCents);
}

