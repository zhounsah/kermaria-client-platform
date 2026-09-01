using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Consommateur generique de la table <c>billing_v2_outbox_events</c> pour les
/// remboursements. Ce n'est pas un worker VPS : il ne connait que RefundId et
/// delegue toute autorite financiere a <see cref="BillingV2RefundService"/>.
/// </summary>
public interface IBillingV2RefundOutboxDispatcher
{
    Task<BillingV2ProviderOutboxDispatchResult> DispatchPendingAsync(
        CancellationToken cancellationToken);
}

public sealed class BillingV2RefundOutboxDispatcher
    : IBillingV2RefundOutboxDispatcher
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly IBillingV2StripeGateway _stripe;
    private readonly IBillingV2RefundService _refunds;
    private readonly ILogger<BillingV2RefundOutboxDispatcher> _logger;

    public BillingV2RefundOutboxDispatcher(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration runtime,
        IBillingV2StripeGateway stripe,
        IBillingV2RefundService refunds,
        ILogger<BillingV2RefundOutboxDispatcher> logger)
    {
        _sql = sql;
        _runtime = runtime;
        _stripe = stripe;
        _refunds = refunds;
        _logger = logger;
    }

    public async Task<BillingV2ProviderOutboxDispatchResult> DispatchPendingAsync(
        CancellationToken cancellationToken)
    {
        var gate = BillingV2RefundExecutionGate.Evaluate(_runtime,
            _sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString),
            _stripe.CanExecute);
        if (!gate.IsValid)
        {
            return new BillingV2ProviderOutboxDispatchResult(0, gate.ReasonCode);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var events = await ReadAsync(connection, cancellationToken);
        var dispatched = 0;
        foreach (var item in events)
        {
            if (!await ClaimAsync(connection, item.Id, cancellationToken)) continue;
            BillingV2RefundOutboxPayload payload;
            try { payload = BillingV2RefundOutbox.Parse(item.PayloadText); }
            catch (Exception error) when (error is JsonException or InvalidOperationException)
            {
                await UpdateAsync(connection, item.Id, "failed", 0,
                    "BILLING_V2_REFUND_OUTBOX_PAYLOAD_INVALID", cancellationToken);
                _logger.LogError(error, "Billing V2 refund outbox payload {OutboxEventId} is invalid.", item.Id);
                continue;
            }

            BillingV2RefundDispatchResult result;
            try
            {
                result = await _refunds.DispatchAsync(
                    payload.RefundId,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                // Une lecture Stripe peut elle aussi expirer. L'evenement reste
                // durable et sera repris apres le bail ; aucun second POST ne
                // part sans que DispatchAsync ait d'abord relu le provider.
                _logger.LogWarning(
                    error,
                    "Billing V2 refund outbox dispatch {OutboxEventId} is indeterminate and will be retried.",
                    item.Id);
                await UpdateAsync(
                    connection,
                    item.Id,
                    "pending",
                    Math.Min(60, Math.Max(1, item.RetryCount + 1) * 5),
                    "BILLING_V2_REFUND_DISPATCH_INDETERMINATE",
                    cancellationToken);
                continue;
            }
            await UpdateAsync(connection, item.Id,
                result.Completed ? "processed" : result.Retryable ? "pending" : "failed",
                result.Retryable ? Math.Min(60, Math.Max(1, item.RetryCount + 1) * 5) : 0,
                result.Completed ? null : result.ReasonCode,
                cancellationToken);
            dispatched++;
        }
        return new BillingV2ProviderOutboxDispatchResult(dispatched,
            dispatched == 0 ? "BILLING_V2_REFUND_OUTBOX_NO_PENDING_EVENTS" : "BILLING_V2_REFUND_OUTBOX_DISPATCHED");
    }

    private sealed record OutboxItem(string Id, string PayloadText, int RetryCount);

    private static async Task<IReadOnlyList<OutboxItem>> ReadAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<OutboxItem>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT id,payload_text,retry_count FROM billing_v2_outbox_events
            WHERE event_type='{BillingV2RefundOutbox.EventType}'
              AND available_at <= UTC_TIMESTAMP(6) AND status IN ('pending','processing')
            ORDER BY available_at,created_at LIMIT 10;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            list.Add(new OutboxItem(MariaDbIdentifierReader.ReadRequired(reader,"id"),
                reader.IsDBNull(reader.GetOrdinal("payload_text")) ? string.Empty : reader.GetString("payload_text"),
                reader.GetInt32("retry_count")));
        return list;
    }

    private static async Task<bool> ClaimAsync(MySqlConnection connection, string id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE billing_v2_outbox_events SET status='processing', available_at=DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 5 MINUTE),last_error=NULL
            WHERE id=@id AND event_type='{BillingV2RefundOutbox.EventType}' AND available_at<=UTC_TIMESTAMP(6) AND status IN ('pending','processing');
            """;
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task UpdateAsync(MySqlConnection connection, string id, string status, int delay, string? error, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE billing_v2_outbox_events SET status=@status,
                retry_count=CASE WHEN @status='pending' THEN retry_count+1 ELSE retry_count END,
                available_at=CASE WHEN @status='pending' THEN DATE_ADD(UTC_TIMESTAMP(6), INTERVAL @delay MINUTE) ELSE available_at END,
                processed_at=CASE WHEN @status='processed' THEN UTC_TIMESTAMP(6) ELSE processed_at END,
                last_error=@error WHERE id=@id;
            """;
        command.Parameters.AddWithValue("@id", id); command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@delay", delay); command.Parameters.AddWithValue("@error", error is null ? DBNull.Value : error);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class BillingV2RefundOutboxWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BillingV2RefundOutboxWorker> _logger;
    public BillingV2RefundOutboxWorker(IServiceScopeFactory scopes, ILogger<BillingV2RefundOutboxWorker> logger)
        => (_scopes, _logger) = (scopes, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IBillingV2RefundOutboxDispatcher>()
                    .DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { _logger.LogError(error, "Billing V2 refund outbox worker failed."); }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
