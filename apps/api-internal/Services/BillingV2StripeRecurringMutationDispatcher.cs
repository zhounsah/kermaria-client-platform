using System.Text.Json;
using System.Text.Json.Serialization;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Consomme exclusivement l'outbox de realignement MRR. La mutation est
/// refusee hors Stripe Test : l'activation Live exige une passe dediee.
/// </summary>
public sealed class BillingV2StripeRecurringMutationDispatcher
{
    private const string EventType = "billing_v2.stripe.recurring_mutation_requested";
    private const int MaximumRetries = 5;
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly IBillingV2StripeGateway _gateway;

    public BillingV2StripeRecurringMutationDispatcher(SqlRuntimeConfiguration sql, BillingV2RuntimeConfiguration runtime, StripeRuntimeConfiguration stripe, IBillingV2StripeGateway gateway)
        => (_sql, _runtime, _stripe, _gateway) = (sql, runtime, stripe, gateway);

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        if (!_runtime.StripeRecurringMutationEnabled || _stripe.Mode != StripeMode.Test || !_gateway.CanExecute || !_sql.IsPersistent)
            return 0;
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var events = new List<(string Id, string Hash, string Payload, int RetryCount)>();
        await using (var read = connection.CreateCommand())
        {
            read.CommandText = "SELECT id,idempotency_key_hash,payload_text,retry_count FROM billing_v2_outbox_events WHERE event_type=@type AND ((status='pending' AND available_at<=UTC_TIMESTAMP(6)) OR (status='processing' AND available_at<=UTC_TIMESTAMP(6))) ORDER BY created_at LIMIT 10";
            read.Parameters.AddWithValue("@type", EventType);
            await using var rows = await read.ExecuteReaderAsync(cancellationToken);
            while (await rows.ReadAsync(cancellationToken)) events.Add((Convert.ToString(rows.GetValue(0))!, rows.GetString(1), rows.GetString(2), rows.GetInt32(3)));
        }
        var dispatched = 0;
        foreach (var item in events)
        {
            await using var claim = connection.CreateCommand();
            claim.CommandText = "UPDATE billing_v2_outbox_events SET status='processing',available_at=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 5 MINUTE) WHERE id=@id AND event_type=@type AND ((status='pending' AND available_at<=UTC_TIMESTAMP(6)) OR (status='processing' AND available_at<=UTC_TIMESTAMP(6)))";
            claim.Parameters.AddWithValue("@id", item.Id); claim.Parameters.AddWithValue("@type", EventType);
            if (await claim.ExecuteNonQueryAsync(cancellationToken) != 1) continue;
            var payload = JsonSerializer.Deserialize<Payload>(item.Payload) ?? throw new InvalidOperationException("BILLING_V2_STRIPE_RECURRING_PAYLOAD_INVALID");
            var providerSubscriptionId = await ReadProviderSubscriptionIdAsync(connection, payload.SubscriptionId, cancellationToken);
            var result = providerSubscriptionId is null
                ? new BillingV2StripeRecurringMutationResult(false, "BILLING_V2_STRIPE_RECURRING_PROVIDER_SUBSCRIPTION_MISSING", null, false)
                : await _gateway.UpdateRecurringAmountAsync(new BillingV2StripeRecurringMutationRequest(providerSubscriptionId, payload.ChangeId, payload.AmountCents, payload.Currency, payload.Quantity, item.Hash), cancellationToken);
            var deterministicMismatch = result.ReasonCode == "BILLING_V2_STRIPE_RECURRING_MUTATION_REFETCH_MISMATCH";
            var retry = result.Retryable && !deterministicMismatch && item.RetryCount + 1 < MaximumRetries;
            var status = result.Succeeded ? "processed" : retry ? "pending" : "failed";
            var delayMinutes = Math.Min(60, 1 << Math.Min(item.RetryCount, 5));
            var error = !result.Succeeded && !retry && result.Retryable
                ? $"{result.ReasonCode}_MANUAL_REVIEW_REQUIRED"
                : result.ReasonCode;
            await using var update = connection.CreateCommand();
            update.CommandText = "UPDATE billing_v2_outbox_events SET status=@status,processed_at=CASE WHEN @status='processed' THEN UTC_TIMESTAMP(6) ELSE NULL END,last_error=@error,available_at=CASE WHEN @status='pending' THEN DATE_ADD(UTC_TIMESTAMP(6),INTERVAL @delay MINUTE) ELSE available_at END,retry_count=retry_count+CASE WHEN @status='pending' THEN 1 ELSE 0 END WHERE id=@id";
            update.Parameters.AddWithValue("@id", item.Id); update.Parameters.AddWithValue("@status", result.Succeeded ? "processed" : result.Retryable ? "pending" : "failed"); update.Parameters.AddWithValue("@error", result.Succeeded ? DBNull.Value : result.ReasonCode);
            update.Parameters["@status"].Value = status; update.Parameters["@error"].Value = result.Succeeded ? DBNull.Value : error; update.Parameters.AddWithValue("@delay", delayMinutes);
            await update.ExecuteNonQueryAsync(cancellationToken);
            if (result.Succeeded) dispatched++;
        }
        return dispatched;
    }

    // Meme resolution que la resiliation et le renouvellement : cette ligne ne
    // lisait que les sessions de checkout et manquait donc l'ancre d'un
    // abonnement converge par reconciliation.
    private static Task<string?> ReadProviderSubscriptionIdAsync(MySqlConnection connection,string subscriptionId,CancellationToken ct)
        => BillingV2ProviderAnchorReader.ReadStripeSubscriptionIdAsync(connection,subscriptionId,ct);
    private sealed record Payload(
        [property: JsonPropertyName("change_id")] string ChangeId,
        [property: JsonPropertyName("subscription_id")] string SubscriptionId,
        [property: JsonPropertyName("successor_item_id")] string SuccessorItemId,
        [property: JsonPropertyName("amount_cents")] long AmountCents,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("quantity")] int Quantity);
}

public sealed class BillingV2StripeRecurringMutationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BillingV2StripeRecurringMutationWorker> _logger;
    public BillingV2StripeRecurringMutationWorker(IServiceScopeFactory scopes, ILogger<BillingV2StripeRecurringMutationWorker> logger)
        => (_scopes, _logger) = (scopes, logger);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = _scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<BillingV2StripeRecurringMutationDispatcher>().DispatchPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { _logger.LogError(error, "Billing V2 Stripe recurring mutation worker failed."); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
