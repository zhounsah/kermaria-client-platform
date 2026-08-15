using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2ProviderOutboxReadiness(
    bool CanDispatch,
    string ReasonCode);

public sealed record BillingV2ProviderOutboxDispatchResult(
    int DispatchedCount,
    string ReasonCode);

public sealed record BillingV2ProviderOutboxEvent(
    string Id,
    string IdempotencyKeyHash,
    string PayloadText,
    int RetryCount);

public sealed record BillingV2ProviderOutboxUpdate(
    string Status,
    int RetryDelayMinutes,
    string? LastError);

public sealed record BillingV2ProviderCheckoutSessionSnapshot(
    string SubscriptionId,
    string Provider,
    string Environment,
    string? ProviderCheckoutId,
    string? ProviderSubscriptionId,
    string? ApprovalUrl);

public sealed record BillingV2ProviderCheckoutSessionConsistency(
    bool IsConsistent,
    string? ReasonCode,
    string? Diagnostic);

public interface IBillingV2ProviderOutboxDispatcher
{
    Task<BillingV2ProviderOutboxDispatchResult> DispatchPendingAsync(
        CancellationToken cancellationToken);
}

public sealed class BillingV2ProviderOutboxDispatcher
    : IBillingV2ProviderOutboxDispatcher
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _configuration;
    private readonly IBillingV2ProviderCheckoutExecutor _executor;
    private readonly ILogger<BillingV2ProviderOutboxDispatcher> _logger;

    public BillingV2ProviderOutboxDispatcher(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration configuration,
        IBillingV2ProviderCheckoutExecutor executor,
        ILogger<BillingV2ProviderOutboxDispatcher> logger)
    {
        _sql = sql;
        _configuration = configuration;
        _executor = executor;
        _logger = logger;
    }

    public async Task<BillingV2ProviderOutboxDispatchResult> DispatchPendingAsync(
        CancellationToken cancellationToken)
    {
        var readiness = BillingV2ProviderOutboxGate.Evaluate(
            _configuration,
            _sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString),
            providerExecutorConfigured: _executor.CanExecute);
        if (!readiness.CanDispatch)
        {
            _logger.LogWarning(
                "Billing V2 provider outbox dispatch blocked: {ReasonCode}. No Stripe/PayPal action was executed.",
                readiness.ReasonCode);
            return new BillingV2ProviderOutboxDispatchResult(
                0,
                readiness.ReasonCode);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var events = await ReadPendingEventsAsync(connection, cancellationToken);
        var dispatched = 0;
        foreach (var outboxEvent in events)
        {
            var claimed = await TryClaimOutboxEventAsync(
                connection,
                outboxEvent.Id,
                cancellationToken);
            if (!claimed)
            {
                _logger.LogInformation(
                    "Billing V2 provider outbox event {OutboxEventId} was already claimed by another dispatcher. No provider action was executed by this worker.",
                    outboxEvent.Id);
                continue;
            }

            var result = await _executor.ExecuteAsync(
                new BillingV2ProviderCheckoutExecutionRequest(
                    outboxEvent.Id,
                    outboxEvent.IdempotencyKeyHash,
                    outboxEvent.PayloadText),
                cancellationToken);
            var update = BillingV2ProviderOutboxDispatchPolicy.Resolve(
                result,
                outboxEvent.RetryCount);
            await using var transaction = await connection.BeginTransactionAsync(
                cancellationToken);
            if (result.Succeeded)
            {
                var payload = BillingV2ProviderCheckoutPayload.Parse(
                    outboxEvent.PayloadText);
                var conflictUpdate = await RecordProviderCheckoutResultAsync(
                    connection,
                    transaction,
                    outboxEvent,
                    payload,
                    result,
                    cancellationToken);
                if (conflictUpdate is not null)
                {
                    update = conflictUpdate;
                }
            }

            await UpdateOutboxEventAsync(
                connection,
                transaction,
                outboxEvent.Id,
                update,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dispatched++;
        }

        return new BillingV2ProviderOutboxDispatchResult(
            dispatched,
            dispatched == 0
                ? "BILLING_V2_PROVIDER_OUTBOX_NO_PENDING_EVENTS"
                : "BILLING_V2_PROVIDER_OUTBOX_DISPATCHED");
    }

    private static async Task<BillingV2ProviderOutboxUpdate?>
        RecordProviderCheckoutResultAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2ProviderOutboxEvent outboxEvent,
        BillingV2ProviderCheckoutPayload payload,
        BillingV2ProviderCheckoutExecutionResult result,
        CancellationToken cancellationToken)
    {
        await using var checkoutCommand = connection.CreateCommand();
        checkoutCommand.Transaction = transaction;
        checkoutCommand.CommandText =
            """
            INSERT INTO billing_v2_provider_checkout_sessions (
                id,
                subscription_id,
                provider,
                environment,
                provider_checkout_id,
                provider_subscription_id,
                approval_url,
                status,
                idempotency_key_hash,
                outbox_event_id,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @subscription_id,
                @provider,
                @environment,
                @provider_checkout_id,
                @provider_subscription_id,
                @approval_url,
                'pending_approval',
                @idempotency_key_hash,
                @outbox_event_id,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            )
            ON DUPLICATE KEY UPDATE
                updated_at = updated_at;
            """;
        checkoutCommand.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        checkoutCommand.Parameters.AddWithValue("@subscription_id", payload.SubscriptionId);
        checkoutCommand.Parameters.AddWithValue("@provider", payload.Provider);
        checkoutCommand.Parameters.AddWithValue("@environment", payload.Environment);
        checkoutCommand.Parameters.AddWithValue(
            "@provider_checkout_id",
            string.IsNullOrWhiteSpace(result.ProviderCheckoutId)
                ? DBNull.Value
                : result.ProviderCheckoutId);
        checkoutCommand.Parameters.AddWithValue(
            "@provider_subscription_id",
            string.IsNullOrWhiteSpace(result.ProviderSubscriptionId)
                ? DBNull.Value
                : result.ProviderSubscriptionId);
        checkoutCommand.Parameters.AddWithValue(
            "@approval_url",
            string.IsNullOrWhiteSpace(result.ApprovalUrl)
                ? DBNull.Value
                : result.ApprovalUrl);
        checkoutCommand.Parameters.AddWithValue(
            "@idempotency_key_hash",
            outboxEvent.IdempotencyKeyHash);
        checkoutCommand.Parameters.AddWithValue("@outbox_event_id", outboxEvent.Id);
        await checkoutCommand.ExecuteNonQueryAsync(cancellationToken);

        var persisted = await ReadProviderCheckoutSessionAsync(
            connection,
            transaction,
            outboxEvent.IdempotencyKeyHash,
            cancellationToken);
        if (persisted is null)
        {
            return BillingV2ProviderOutboxDispatchPolicy.FailClosed(
                "BILLING_V2_PROVIDER_CHECKOUT_SESSION_NOT_RECORDED",
                "No local provider checkout session was readable after insert.");
        }

        var consistency =
            BillingV2ProviderCheckoutSessionPolicy.Evaluate(
                persisted,
                payload,
                result);
        if (!consistency.IsConsistent)
        {
            return BillingV2ProviderOutboxDispatchPolicy.FailClosed(
                consistency.ReasonCode
                    ?? BillingV2ProviderCheckoutSessionPolicy.ConflictReasonCode,
                consistency.Diagnostic);
        }

        if (!string.IsNullOrWhiteSpace(result.ProviderSubscriptionId))
        {
            await RecordProviderAgreementAsync(
                connection,
                transaction,
                outboxEvent,
                payload,
                result.ProviderSubscriptionId,
                cancellationToken);
        }

        return null;
    }

    private static async Task<BillingV2ProviderCheckoutSessionSnapshot?>
        ReadProviderCheckoutSessionAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string idempotencyKeyHash,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                subscription_id,
                provider,
                environment,
                provider_checkout_id,
                provider_subscription_id,
                approval_url
            FROM billing_v2_provider_checkout_sessions
            WHERE idempotency_key_hash = @idempotency_key_hash
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue(
            "@idempotency_key_hash",
            idempotencyKeyHash);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2ProviderCheckoutSessionSnapshot(
            reader.GetString(reader.GetOrdinal("subscription_id")),
            reader.GetString(reader.GetOrdinal("provider")),
            reader.GetString(reader.GetOrdinal("environment")),
            ReadNullableString(reader, "provider_checkout_id"),
            ReadNullableString(reader, "provider_subscription_id"),
            ReadNullableString(reader, "approval_url"));
    }

    private static string? ReadNullableString(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static async Task RecordProviderAgreementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2ProviderOutboxEvent outboxEvent,
        BillingV2ProviderCheckoutPayload payload,
        string providerSubscriptionId,
        CancellationToken cancellationToken)
    {
        await using var agreementCommand = connection.CreateCommand();
        agreementCommand.Transaction = transaction;
        agreementCommand.CommandText =
            """
            INSERT INTO billing_v2_payment_agreements (
                id,
                subscription_id,
                provider,
                environment,
                provider_subscription_id,
                status,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @subscription_id,
                @provider,
                @environment,
                @provider_subscription_id,
                'pending',
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            )
            ON DUPLICATE KEY UPDATE
                provider_subscription_id = provider_subscription_id,
                updated_at = updated_at;
            """;
        agreementCommand.Parameters.AddWithValue(
            "@id",
            Guid.NewGuid().ToString("D"));
        agreementCommand.Parameters.AddWithValue(
            "@subscription_id",
            payload.SubscriptionId);
        agreementCommand.Parameters.AddWithValue("@provider", payload.Provider);
        agreementCommand.Parameters.AddWithValue(
            "@environment",
            payload.Environment);
        agreementCommand.Parameters.AddWithValue(
            "@provider_subscription_id",
            providerSubscriptionId);
        await agreementCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<BillingV2ProviderOutboxEvent>>
        ReadPendingEventsAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
    {
        var events = new List<BillingV2ProviderOutboxEvent>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, idempotency_key_hash, payload_text, retry_count
            FROM billing_v2_outbox_events
            WHERE event_type = 'billing_v2.provider_checkout.create_requested'
              AND available_at <= UTC_TIMESTAMP(6)
              AND status IN ('pending', 'processing')
            ORDER BY available_at, created_at
            LIMIT 10;
            """;
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new BillingV2ProviderOutboxEvent(
                reader.GetString("id"),
                reader.GetString("idempotency_key_hash"),
                reader.IsDBNull(reader.GetOrdinal("payload_text"))
                    ? string.Empty
                    : reader.GetString("payload_text"),
                reader.GetInt32("retry_count")));
        }

        return events;
    }

    private static async Task<bool> TryClaimOutboxEventAsync(
        MySqlConnection connection,
        string eventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE billing_v2_outbox_events
            SET status = 'processing',
                available_at = DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 5 MINUTE),
                last_error = NULL
            WHERE id = @id
              AND event_type = 'billing_v2.provider_checkout.create_requested'
              AND available_at <= UTC_TIMESTAMP(6)
              AND status IN ('pending', 'processing');
            """;
        command.Parameters.AddWithValue("@id", eventId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task UpdateOutboxEventAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string eventId,
        BillingV2ProviderOutboxUpdate update,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_outbox_events
            SET status = @status,
                retry_count = CASE
                    WHEN @status = 'pending' THEN retry_count + 1
                    ELSE retry_count
                END,
                available_at = CASE
                    WHEN @status = 'pending'
                        THEN DATE_ADD(
                            UTC_TIMESTAMP(6),
                            INTERVAL @retry_delay_minutes MINUTE)
                    ELSE available_at
                END,
                processed_at = CASE
                    WHEN @status = 'processed' THEN UTC_TIMESTAMP(6)
                    ELSE processed_at
                END,
                last_error = @last_error
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", eventId);
        command.Parameters.AddWithValue("@status", update.Status);
        command.Parameters.AddWithValue(
            "@retry_delay_minutes",
            update.RetryDelayMinutes);
        command.Parameters.AddWithValue(
            "@last_error",
            update.LastError is null ? DBNull.Value : update.LastError);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class BillingV2ProviderOutboxWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingV2ProviderOutboxWorker> _logger;

    public BillingV2ProviderOutboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BillingV2ProviderOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider
                    .GetRequiredService<IBillingV2ProviderOutboxDispatcher>();
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Billing V2 provider outbox worker failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}

public static class BillingV2ProviderOutboxDispatchPolicy
{
    public static BillingV2ProviderOutboxUpdate Resolve(
        BillingV2ProviderCheckoutExecutionResult result,
        int currentRetryCount)
    {
        if (result.Succeeded)
        {
            return new BillingV2ProviderOutboxUpdate(
                "processed",
                RetryDelayMinutes: 0,
                LastError: null);
        }

        var retryDelay = Math.Min(
            60,
            Math.Max(1, currentRetryCount + 1) * 5);
        return new BillingV2ProviderOutboxUpdate(
            "pending",
            retryDelay,
            string.Join(
                ": ",
                new[] { result.Code, result.ErrorMessage }
                    .Where(value => !string.IsNullOrWhiteSpace(value))));
    }

    public static BillingV2ProviderOutboxUpdate FailClosed(
        string reasonCode,
        string? diagnostic)
        => new(
            "failed",
            RetryDelayMinutes: 0,
            string.Join(
                ": ",
                new[] { reasonCode, diagnostic }
                    .Where(value => !string.IsNullOrWhiteSpace(value))));
}

public static class BillingV2ProviderCheckoutSessionPolicy
{
    public const string ConflictReasonCode =
        "BILLING_V2_PROVIDER_CHECKOUT_SESSION_CONFLICT";

    public static BillingV2ProviderCheckoutSessionConsistency Evaluate(
        BillingV2ProviderCheckoutSessionSnapshot persisted,
        BillingV2ProviderCheckoutPayload expectedPayload,
        BillingV2ProviderCheckoutExecutionResult providerResult)
    {
        if (!Same(persisted.SubscriptionId, expectedPayload.SubscriptionId))
        {
            return Conflict("subscription_id");
        }

        if (!Same(persisted.Provider, expectedPayload.Provider))
        {
            return Conflict("provider");
        }

        if (!Same(persisted.Environment, expectedPayload.Environment))
        {
            return Conflict("environment");
        }

        if (!Same(
                persisted.ProviderCheckoutId,
                providerResult.ProviderCheckoutId))
        {
            return Conflict("provider_checkout_id");
        }

        if (!Same(
                persisted.ProviderSubscriptionId,
                providerResult.ProviderSubscriptionId))
        {
            return Conflict("provider_subscription_id");
        }

        if (!Same(persisted.ApprovalUrl, providerResult.ApprovalUrl))
        {
            return Conflict("approval_url");
        }

        return new BillingV2ProviderCheckoutSessionConsistency(
            true,
            ReasonCode: null,
            Diagnostic: null);
    }

    private static BillingV2ProviderCheckoutSessionConsistency Conflict(
        string field)
        => new(
            false,
            ConflictReasonCode,
            $"Provider checkout idempotency replay changed {field}.");

    private static bool Same(string? left, string? right)
        => string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}

public static class BillingV2ProviderOutboxClaimPolicy
{
    public const string ProcessingStatus = "processing";

    public static bool CanClaim(
        string status,
        DateTime availableAtUtc,
        DateTime nowUtc)
        => status is "pending" or ProcessingStatus
           && availableAtUtc <= nowUtc;
}

public static class BillingV2ProviderOutboxGate
{
    public static BillingV2ProviderOutboxReadiness Evaluate(
        BillingV2RuntimeConfiguration configuration,
        bool persistentSqlAvailable,
        bool providerExecutorConfigured)
    {
        if (!configuration.ProviderOutboxEnabled)
        {
            return Blocked("BILLING_V2_PROVIDER_OUTBOX_FLAG_OFF");
        }

        if (!persistentSqlAvailable)
        {
            return Blocked("BILLING_V2_PROVIDER_OUTBOX_NO_PERSISTENT_SQL");
        }

        if (!providerExecutorConfigured)
        {
            return Blocked(
                "BILLING_V2_PROVIDER_OUTBOX_EXECUTOR_NOT_CONFIGURED");
        }

        return new BillingV2ProviderOutboxReadiness(
            true,
            "BILLING_V2_PROVIDER_OUTBOX_READY");
    }

    private static BillingV2ProviderOutboxReadiness Blocked(string reasonCode)
        => new(false, reasonCode);
}
