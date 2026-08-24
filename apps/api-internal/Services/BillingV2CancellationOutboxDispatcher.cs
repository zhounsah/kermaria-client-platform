using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface IBillingV2CancellationOutboxDispatcher
{
    Task<BillingV2ProviderOutboxDispatchResult> DispatchPendingAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Convergence fournisseur des resiliations Billing V2.
/// </summary>
/// <remarks>
/// <para>
/// C'est ici, et seulement ici, qu'un abonnement passe a <c>cancelled</c>
/// lorsqu'un fournisseur est implique — apres que celui-ci a accepte. Tant que
/// l'appel echoue, l'abonnement reste en <c>pending_cancellation</c> : un etat
/// visible et rattrapable, jamais un mensonge rassurant.
/// </para>
/// <para>
/// Une demande a FIN DE TERME reussie ne clot rien non plus : le fournisseur a
/// seulement promis de ne pas renouveler. Cote Stripe, le passage a
/// <c>cancelled</c> viendra du signal fournisseur au terme, via la
/// reconciliation — le webhook est un signal, le refetch est la convergence.
/// Cote PayPal, qui ne sait pas tenir cette promesse, le terme est atteint par
/// un SECOND evenement d'outbox dormant jusqu'a <c>current_period_ends_at</c> :
/// le filtre <c>available_at &lt;= UTC_TIMESTAMP(6)</c> ci-dessous est ce qui le
/// tient endormi, et le fait qu'il soit en base est ce qui le fait survivre a
/// un redemarrage.
/// </para>
/// </remarks>
public sealed class BillingV2CancellationOutboxDispatcher
    : IBillingV2CancellationOutboxDispatcher
{
    private const int BatchSize = 10;

    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _configuration;
    private readonly IBillingV2ProviderCancellationExecutor _executor;
    private readonly ILogger<BillingV2CancellationOutboxDispatcher> _logger;

    public BillingV2CancellationOutboxDispatcher(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration configuration,
        IBillingV2ProviderCancellationExecutor executor,
        ILogger<BillingV2CancellationOutboxDispatcher> logger)
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
            _sql.IsPersistent
                && !string.IsNullOrWhiteSpace(_sql.ConnectionString),
            providerExecutorConfigured: _executor.CanExecute);
        if (!readiness.CanDispatch)
        {
            _logger.LogWarning(
                "Billing V2 cancellation dispatch blocked: {ReasonCode}. No provider cancellation was executed; affected subscriptions stay pending_cancellation.",
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
            if (!await TryClaimAsync(
                    connection,
                    outboxEvent.Id,
                    cancellationToken))
            {
                _logger.LogInformation(
                    "Billing V2 cancellation event {OutboxEventId} was already claimed elsewhere. No provider call was made by this worker.",
                    outboxEvent.Id);
                continue;
            }

            BillingV2CancellationOutboxPayload payload;
            try
            {
                payload = BillingV2CancellationOutbox.Parse(
                    outboxEvent.PayloadText);
            }
            catch (Exception error)
                when (error is InvalidOperationException or JsonException)
            {
                // Une charge illisible ne se repare pas par un retry, et on ne
                // devine pas le geste voulu : l'evenement est mis en echec et
                // l'abonnement reste visible en pending_cancellation.
                await using var poison = await connection
                    .BeginTransactionAsync(cancellationToken);
                await UpdateOutboxEventAsync(
                    connection,
                    poison,
                    outboxEvent.Id,
                    new BillingV2ProviderOutboxUpdate(
                        "failed",
                        0,
                        error.Message),
                    cancellationToken);
                await poison.CommitAsync(cancellationToken);
                _logger.LogError(
                    error,
                    "Billing V2 cancellation event {OutboxEventId} carries an unusable payload. No provider call was made; the subscription stays pending_cancellation.",
                    outboxEvent.Id);
                continue;
            }

            var result = await _executor.CancelAsync(
                new BillingV2ProviderCancellationRequest(
                    payload.Provider,
                    payload.Environment,
                    payload.ProviderSubscriptionId,
                    payload.Operation,
                    payload.Reason),
                cancellationToken);

            var update = BillingV2CancellationDispatchPolicy.Resolve(
                result,
                outboxEvent.RetryCount);

            await using var transaction = await connection.BeginTransactionAsync(
                cancellationToken);

            // Seuls les gestes qui rendent l'abonnement definitivement non
            // facturable autorisent le statut terminal : `cancel_immediate` et
            // `cancel_at_term`. Une promesse de non-renouvellement
            // (`cancel_at_period_end`) ou une suspension
            // (`suspend_pending_term_end`) ne clot rien — la premiere laisse la
            // periode courir, la seconde se leve.
            if (result.Succeeded
                && BillingV2CancellationOperations.ClosesLocalSubscription(
                    payload.Operation))
            {
                await MarkCancelledAsync(
                    connection,
                    transaction,
                    payload.SubscriptionId,
                    cancellationToken);
            }

            await UpdateOutboxEventAsync(
                connection,
                transaction,
                outboxEvent.Id,
                update,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogError(
                    "Billing V2 provider cancellation failed for subscription {SubscriptionId}: {Code}. Local status stays pending_cancellation; the provider may still bill.",
                    payload.SubscriptionId,
                    result.Code);
            }

            dispatched++;
        }

        return new BillingV2ProviderOutboxDispatchResult(
            dispatched,
            dispatched == 0
                ? "BILLING_V2_CANCELLATION_OUTBOX_NO_PENDING_EVENTS"
                : "BILLING_V2_CANCELLATION_OUTBOX_DISPATCHED");
    }

    private static async Task<IReadOnlyList<BillingV2ProviderOutboxEvent>>
        ReadPendingEventsAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
    {
        var events = new List<BillingV2ProviderOutboxEvent>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT id, idempotency_key_hash, payload_text, retry_count
             FROM billing_v2_outbox_events
             WHERE event_type = '{BillingV2CancellationOutbox.EventType}'
               AND available_at <= UTC_TIMESTAMP(6)
               AND status IN ('pending', 'processing')
             ORDER BY available_at, created_at
             LIMIT {BatchSize};
             """;
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new BillingV2ProviderOutboxEvent(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("idempotency_key_hash"),
                reader.IsDBNull(reader.GetOrdinal("payload_text"))
                    ? string.Empty
                    : reader.GetString("payload_text"),
                reader.GetInt32("retry_count")));
        }

        return events;
    }

    /// <remarks>
    /// La revendication deplace <c>available_at</c> dans le futur : deux
    /// instances de l'API ne peuvent pas annuler deux fois le meme abonnement.
    /// </remarks>
    private static async Task<bool> TryClaimAsync(
        MySqlConnection connection,
        string eventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             UPDATE billing_v2_outbox_events
             SET status = 'processing',
                 available_at = DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 5 MINUTE),
                 last_error = NULL
             WHERE id = @id
               AND event_type = '{BillingV2CancellationOutbox.EventType}'
               AND available_at <= UTC_TIMESTAMP(6)
               AND status IN ('pending', 'processing');
             """;
        command.Parameters.AddWithValue("@id", eventId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task MarkCancelledAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_subscriptions
            SET status = 'cancelled',
                cancel_at_period_end = 0,
                cancellation_requested_at =
                    COALESCE(cancellation_requested_at, UTC_TIMESTAMP(6)),
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND status NOT IN ('cancelled', 'expired');
            """;
        command.Parameters.AddWithValue("@id", subscriptionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
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

/// <summary>
/// Relance periodique du dispatcher de resiliation.
/// </summary>
public sealed class BillingV2CancellationOutboxWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingV2CancellationOutboxWorker> _logger;

    public BillingV2CancellationOutboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BillingV2CancellationOutboxWorker> logger)
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
                    .GetRequiredService<IBillingV2CancellationOutboxDispatcher>();
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                // Le worker ne doit pas mourir : une resiliation non convergee
                // est un abonnement encore facturable.
                _logger.LogError(
                    error,
                    "Billing V2 cancellation outbox dispatch failed. Pending cancellations remain queued.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
