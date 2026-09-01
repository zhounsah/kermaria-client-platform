using System.Data;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2RefundRequestResult(
    bool Accepted,
    string ReasonCode,
    string? RefundId,
    string? Status);

public sealed record BillingV2RefundDispatchResult(
    bool Completed,
    bool Retryable,
    string ReasonCode,
    string? RefundId);

public interface IBillingV2RefundService
{
    /// <summary>
    /// Usage exclusivement serveur : le montant est derive de l'evenement
    /// settled. Il n'existe volontairement aucun endpoint portal refund(...).
    /// </summary>
    Task<BillingV2RefundRequestResult> RequestFullRefundAsync(
        string billingEventId,
        string reasonCode,
        string actorReference,
        string correlationId,
        CancellationToken cancellationToken);

    Task<BillingV2RefundDispatchResult> DispatchAsync(
        string refundId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Orchestrateur server-side du remboursement integral Billing V2. Les appels
/// Stripe sont toujours hors transaction SQL ; l'intention et l'outbox sont
/// d'abord commits, et tout resultat externe est relu avant settlement refunded.
/// </summary>
public sealed class BillingV2RefundService : IBillingV2RefundService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly IBillingV2StripeGateway _stripe;
    private readonly IBillingV2Clock _clock;
    private readonly ILogger<BillingV2RefundService> _logger;

    public BillingV2RefundService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration runtime,
        IBillingV2StripeGateway stripe,
        IBillingV2Clock clock,
        ILogger<BillingV2RefundService> logger)
    {
        _sql = sql;
        _runtime = runtime;
        _stripe = stripe;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BillingV2RefundRequestResult> RequestFullRefundAsync(
        string billingEventId,
        string reasonCode,
        string actorReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(billingEventId)
            || string.IsNullOrWhiteSpace(reasonCode)
            || string.IsNullOrWhiteSpace(actorReference)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            return new(false, "BILLING_V2_REFUND_REQUEST_INVALID", null, null);
        }

        var gate = BillingV2RefundExecutionGate.Evaluate(
            _runtime,
            _sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString),
            _stripe.CanExecute);
        if (!gate.IsValid)
        {
            return new(false, gate.ReasonCode, null, null);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var source = await ReadSourceAsync(
            connection, transaction, billingEventId, forUpdate: true,
            cancellationToken);
        if (source is not null)
        {
            // L'emission initiale verrouille la souscription, alors que les
            // cycles verrouillent le BillingEvent. Prendre les deux verrous
            // ordonnes rend atomique la decision "documenter ou rembourser".
            // Sans ce verrou commun, une facture pouvait etre creee entre la
            // lecture du refund et son intention/outbox.
            await LockSubscriptionAsync(
                connection,
                transaction,
                source.SubscriptionId,
                cancellationToken);
        }
        var decision = BillingV2RefundPolicy.EvaluateFullRequest(source);
        if (!decision.IsValid)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, decision.ReasonCode, null, null);
        }

        var idempotencyHash = BillingV2RefundOutbox.ComputeIdempotencyHash(
            source!.BillingEventId);
        var existing = await ReadRefundByEventAsync(
            connection, transaction, source.BillingEventId, forUpdate: true,
            cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(true, "BILLING_V2_REFUND_ALREADY_REQUESTED", existing.Id, existing.Status);
        }

        var refundId = Guid.NewGuid().ToString("D");
        await InsertRefundAsync(
            connection, transaction, refundId, source, decision, reasonCode,
            idempotencyHash, correlationId, cancellationToken);
        await EnqueueAsync(connection, transaction, new BillingV2RefundOutboxPayload(
            refundId, source.BillingEventId, source.Provider!, source.Environment!,
            source.ProviderPaymentId!), idempotencyHash, cancellationToken);
        await InsertAuditAsync(connection, transaction, refundId,
            "billing_v2.refund.requested", actorReference, correlationId,
            $"billing_event_id={source.BillingEventId};reason={reasonCode}",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, "BILLING_V2_REFUND_REQUESTED", refundId,
            BillingV2RefundStatuses.Requested);
    }

    public async Task<BillingV2RefundDispatchResult> DispatchAsync(
        string refundId,
        CancellationToken cancellationToken)
    {
        var gate = BillingV2RefundExecutionGate.Evaluate(
            _runtime,
            _sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString),
            _stripe.CanExecute);
        if (!gate.IsValid)
        {
            return new(false, false, gate.ReasonCode, refundId);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var refund = await ReadRefundAsync(connection, transaction: null, refundId,
            forUpdate: false, cancellationToken);
        if (refund is null)
        {
            return new(false, false, "BILLING_V2_REFUND_NOT_FOUND", refundId);
        }

        if (string.Equals(refund.Status, BillingV2RefundStatuses.Confirmed,
            StringComparison.Ordinal))
        {
            return new(true, false, "BILLING_V2_REFUND_ALREADY_CONFIRMED", refundId);
        }
        if (string.Equals(refund.Status, BillingV2RefundStatuses.Failed,
            StringComparison.Ordinal))
        {
            return new(false, false, "BILLING_V2_REFUND_ALREADY_FAILED", refundId);
        }

        // Une demande peut attendre dans l'outbox. Le dossier financier est
        // donc relu AVANT le moindre appel externe : settlement retire, source
        // devenue documentaire ou ancre provider perdue doivent echouer en
        // ferme, pas creer un refund Stripe difficile a corriger comptablement.
        var dispatchSource = await ReadSourceAsync(connection, transaction: null,
            refund.BillingEventId, forUpdate: false, cancellationToken);
        var dispatchDecision = BillingV2RefundPolicy.EvaluateFullRequest(
            dispatchSource);
        if (!dispatchDecision.IsValid)
        {
            await MarkFailedAsync(
                connection,
                refund.Id,
                dispatchDecision.ReasonCode,
                _clock.UtcNow,
                cancellationToken);
            return new(false, false, dispatchDecision.ReasonCode, refund.Id);
        }

        // Toujours chercher AVANT de creer : timeout/crash apres POST retrouve
        // le refund Stripe cible par provider id ou metadata stable.
        var observed = await _stripe.FindRefundAsync(
            new BillingV2StripeRefundLocator(
                refund.ProviderRefundId,
                refund.ProviderPaymentId,
                refund.Id),
            cancellationToken);
        if (observed is null)
        {
            var created = await _stripe.CreateRefundAsync(
                new BillingV2StripeRefundCreateRequest(
                    refund.ProviderPaymentId, refund.Currency, refund.AmountCents,
                    refund.IdempotencyKeyCanonical, refund.Id),
                cancellationToken);
            if (!created.Succeeded)
            {
                return new(false, created.Retryable, created.ReasonCode, refund.Id);
            }

            await PersistProviderRefundAsync(
                connection, refund.Id, created.Refund!, _clock.UtcNow,
                cancellationToken);
            // Le POST Stripe n'est pas une preuve. Une relecture explicite est
            // imposee au prochain passage (ou maintenant si l'id est connu).
            observed = await _stripe.FindRefundAsync(
                new BillingV2StripeRefundLocator(
                    created.Refund!.RefundId, refund.ProviderPaymentId, refund.Id),
                cancellationToken);
            if (observed is null)
            {
                return new(false, true,
                    "BILLING_V2_REFUND_PROVIDER_CONFIRMATION_PENDING", refund.Id);
            }
        }

        var source = await ReadSourceAsync(connection, transaction: null,
            refund.BillingEventId, forUpdate: false, cancellationToken);
        if (source is null)
        {
            return new(false, false, "BILLING_V2_REFUND_BILLING_EVENT_NOT_FOUND", refund.Id);
        }
        var confirmation = BillingV2RefundConfirmationPolicy.Evaluate(source,
            new BillingV2RefundProviderObservation(observed.RefundId,
                observed.Status, observed.AmountCents, observed.Currency,
                observed.PaymentIntentId));
        if (confirmation.IsFailed)
        {
            await MarkFailedAsync(connection, refund.Id, confirmation.ReasonCode,
                _clock.UtcNow, cancellationToken);
            return new(false, false, confirmation.ReasonCode, refund.Id);
        }
        if (!confirmation.IsConfirmed)
        {
            await PersistProviderRefundAsync(connection, refund.Id, observed,
                _clock.UtcNow, cancellationToken);
            return new(false, true, confirmation.ReasonCode, refund.Id);
        }

        await ConfirmAsync(connection, refund, source, observed, cancellationToken);
        return new(true, false, confirmation.ReasonCode, refund.Id);
    }

    private async Task ConfirmAsync(
        MySqlConnection connection,
        RefundRecord refund,
        BillingV2RefundSourceSnapshot source,
        BillingV2StripeRefundSnapshot observed,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var lockedSource = await ReadSourceAsync(connection, transaction,
            source.BillingEventId, forUpdate: true, cancellationToken);
        var lockedRefund = await ReadRefundAsync(connection, transaction, refund.Id,
            forUpdate: true, cancellationToken);
        if (lockedSource is null || lockedRefund is null
            || string.Equals(lockedRefund.Status, BillingV2RefundStatuses.Confirmed,
                StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var finalDecision = BillingV2RefundConfirmationPolicy.Evaluate(lockedSource,
            new BillingV2RefundProviderObservation(observed.RefundId,
                observed.Status, observed.AmountCents, observed.Currency,
                observed.PaymentIntentId));
        if (!finalDecision.IsConfirmed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        // Compensation interne atomique avec `refunded`: le moteur de renewal
        // ne peut plus produire une charge, et la resiliation provider est en
        // outbox avant que le statut financier ne confirme le remboursement.
        await BlockRenewalAndQueueCancellationAsync(
            connection, transaction, lockedSource, cancellationToken);

        var now = _clock.UtcNow;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE billing_v2_refunds
                SET status = 'confirmed',
                    provider_refund_id = @provider_refund_id,
                    provider_confirmed_at = @now,
                    failure_code = NULL,
                    last_error = NULL,
                    updated_at = @now
                WHERE id = @id AND status IN ('requested', 'pending_provider');
                """;
            command.Parameters.AddWithValue("@id", lockedRefund.Id);
            command.Parameters.AddWithValue("@provider_refund_id", observed.RefundId);
            command.Parameters.AddWithValue("@now", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE billing_v2_billing_events
                SET settlement_status = 'refunded',
                    refunded_at = @now,
                    refund_reason_code = @reason_code,
                    settlement_reason_code = 'BILLING_V2_REFUND_PROVIDER_CONFIRMED'
                WHERE id = @id AND settlement_status = 'settled';
                """;
            command.Parameters.AddWithValue("@id", lockedSource.BillingEventId);
            command.Parameters.AddWithValue("@now", now);
            command.Parameters.AddWithValue("@reason_code", lockedRefund.ReasonCode);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException(
                    "BILLING_V2_REFUND_SETTLEMENT_TRANSITION_CONFLICT");
            }
        }
        await InsertAuditAsync(connection, transaction, lockedRefund.Id,
            "billing_v2.refund.confirmed", "system:refund-worker",
            lockedRefund.CorrelationId ?? "unknown",
            $"provider_refund_id={observed.RefundId}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task BlockRenewalAndQueueCancellationAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2RefundSourceSnapshot source,
        CancellationToken cancellationToken)
    {
        var compensation = BillingV2RefundSubscriptionCompensationPolicy
            .Evaluate(source);
        if (!compensation.IsValid)
        {
            throw new InvalidOperationException(compensation.ReasonCode);
        }

        await using (var block = connection.CreateCommand())
        {
            block.Transaction = transaction;
            block.CommandText =
                """
                UPDATE billing_v2_subscriptions
                SET status = CASE WHEN status IN ('cancelled', 'expired') THEN status ELSE 'pending_cancellation' END,
                    renews_at = NULL,
                    cancel_at_period_end = 0,
                    renewal_blocked_at = COALESCE(renewal_blocked_at, UTC_TIMESTAMP(6)),
                    renewal_block_reason_code = 'BILLING_V2_REFUND_CONFIRMED',
                    cancellation_requested_at = COALESCE(cancellation_requested_at, UTC_TIMESTAMP(6)),
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @subscription_id;
                """;
            block.Parameters.AddWithValue("@subscription_id", source.SubscriptionId);
            if (await block.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException(
                    "BILLING_V2_REFUND_SUBSCRIPTION_NOT_FOUND");
            }
        }

        if (!compensation.QueueProviderCancellation)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(source.ProviderSubscriptionId))
        {
            throw new InvalidOperationException(
                "BILLING_V2_REFUND_RECURRING_SUBSCRIPTION_UNRESOLVED");
        }

        var payload = new BillingV2CancellationOutboxPayload(
            source.SubscriptionId, source.Provider!, source.Environment!,
            source.ProviderSubscriptionId!,
            BillingV2CancellationOperations.CancelImmediate,
            "billing_v2_refund_confirmed");
        await using var enqueue = connection.CreateCommand();
        enqueue.Transaction = transaction;
        enqueue.CommandText =
            """
            INSERT INTO billing_v2_outbox_events (
                id, aggregate_type, aggregate_id, event_type, payload_text,
                idempotency_key_hash, status, retry_count, available_at, created_at
            ) VALUES (
                @id, @aggregate_type, @aggregate_id, @event_type, @payload_text,
                @idempotency_key_hash, 'pending', 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            ) ON DUPLICATE KEY UPDATE id = id;
            """;
        enqueue.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        enqueue.Parameters.AddWithValue("@aggregate_type", BillingV2CancellationOutbox.AggregateType);
        enqueue.Parameters.AddWithValue("@aggregate_id", source.SubscriptionId);
        enqueue.Parameters.AddWithValue("@event_type", BillingV2CancellationOutbox.EventType);
        enqueue.Parameters.AddWithValue("@payload_text", BillingV2CancellationOutbox.Serialize(payload));
        enqueue.Parameters.AddWithValue("@idempotency_key_hash", BillingV2CancellationOutbox.ComputeIdempotencyHash(payload));
        await enqueue.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<BillingV2RefundSourceSnapshot?> ReadSourceAsync(
        MySqlConnection connection, MySqlTransaction? transaction,
        string billingEventId, bool forUpdate, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT event_row.id, event_row.subscription_id, event_row.settlement_status,
                   CASE
                     WHEN event_row.document_status <> 'none'
                       THEN event_row.document_status
                     WHEN EXISTS (
                       SELECT 1 FROM billing_v2_subscription_documents document_row
                       WHERE document_row.billing_event_id = event_row.id)
                       THEN 'pending'
                     ELSE 'none'
                   END AS document_status,
                   event_row.total_amount_cents, event_row.currency,
                   attempt.id AS payment_attempt_id, attempt.provider, attempt.environment,
                   attempt.provider_payment_id,
                   EXISTS(
                       SELECT 1 FROM billing_v2_subscription_items item
                       INNER JOIN billing_v2_subscription_item_effective_price_components component
                           ON component.subscription_item_id = item.id
                       WHERE item.subscription_id = event_row.subscription_id
                         AND item.status = 'active' AND component.status = 'active'
                         AND component.billing_cadence = 'monthly') AS has_recurring_component,
                   COALESCE(attempt.provider_subscription_id,
                       (SELECT agreement.provider_subscription_id
                          FROM billing_v2_payment_agreements agreement
                         WHERE agreement.subscription_id = event_row.subscription_id
                           AND agreement.provider = attempt.provider
                         ORDER BY agreement.created_at ASC LIMIT 1)) AS provider_subscription_id
            FROM billing_v2_billing_events event_row
            LEFT JOIN billing_v2_payment_attempts attempt
              ON attempt.id = (
                  SELECT candidate.id FROM billing_v2_payment_attempts candidate
                  WHERE candidate.billing_event_id = event_row.id
                    AND candidate.status = 'succeeded'
                    AND candidate.provider_payment_id IS NOT NULL
                  ORDER BY candidate.verified_at DESC, candidate.created_at DESC LIMIT 1)
            WHERE event_row.id = @id
            {(forUpdate ? "FOR UPDATE" : string.Empty)};
            """;
        command.Parameters.AddWithValue("@id", billingEventId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        string? Nullable(string name) => reader.IsDBNull(reader.GetOrdinal(name))
            ? null : reader.GetString(name);
        return new BillingV2RefundSourceSnapshot(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "subscription_id"),
            reader.GetString("settlement_status"), reader.GetString("document_status"),
            reader.GetInt64("total_amount_cents"), reader.GetString("currency"),
            Nullable("payment_attempt_id"), Nullable("provider"), Nullable("environment"),
            Nullable("provider_payment_id"), reader.GetBoolean("has_recurring_component"),
            Nullable("provider_subscription_id"));
    }

    private static async Task LockSubscriptionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id
            FROM billing_v2_subscriptions
            WHERE id = @subscription_id
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        var found = await command.ExecuteScalarAsync(cancellationToken);
        if (found is null or DBNull)
        {
            throw new InvalidOperationException(
                "BILLING_V2_REFUND_SUBSCRIPTION_NOT_FOUND");
        }
    }

    private sealed record RefundRecord(string Id, string BillingEventId,
        string PaymentAttemptId, string Provider, string Environment,
        string ProviderPaymentId, string? ProviderRefundId, long AmountCents,
        string Currency, string ReasonCode, string Status,
        string IdempotencyKeyCanonical, string? CorrelationId);

    private static async Task<RefundRecord?> ReadRefundByEventAsync(
        MySqlConnection connection, MySqlTransaction transaction, string eventId,
        bool forUpdate, CancellationToken cancellationToken)
        => await ReadRefundCoreAsync(connection, transaction,
            "billing_event_id = @value", eventId, forUpdate, cancellationToken);

    private static async Task<RefundRecord?> ReadRefundAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string refundId,
        bool forUpdate, CancellationToken cancellationToken)
        => await ReadRefundCoreAsync(connection, transaction,
            "id = @value", refundId, forUpdate, cancellationToken);

    private static async Task<RefundRecord?> ReadRefundCoreAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string where,
        string value, bool forUpdate, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = $"""
            SELECT id, billing_event_id, payment_attempt_id, provider, environment,
                   provider_payment_id, provider_refund_id, amount_cents, currency,
                   reason_code, status, idempotency_key_canonical, correlation_id
            FROM billing_v2_refunds WHERE {where}
            {(forUpdate ? "FOR UPDATE" : string.Empty)};
            """;
        command.Parameters.AddWithValue("@value", value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        string? Nullable(string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetString(name);
        return new RefundRecord(MariaDbIdentifierReader.ReadRequired(reader,"id"),
            MariaDbIdentifierReader.ReadRequired(reader,"billing_event_id"),
            MariaDbIdentifierReader.ReadRequired(reader,"payment_attempt_id"),
            reader.GetString("provider"), reader.GetString("environment"),
            reader.GetString("provider_payment_id"), Nullable("provider_refund_id"),
            reader.GetInt64("amount_cents"), reader.GetString("currency"),
            reader.GetString("reason_code"), reader.GetString("status"),
            reader.GetString("idempotency_key_canonical"), Nullable("correlation_id"));
    }

    private static async Task InsertRefundAsync(MySqlConnection connection,
        MySqlTransaction transaction, string refundId, BillingV2RefundSourceSnapshot source,
        BillingV2RefundRequestDecision decision, string reasonCode, string hash,
        string correlationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO billing_v2_refunds (
                id, billing_event_id, payment_attempt_id, provider, environment,
                provider_payment_id, amount_cents, currency, reason_code, status,
                idempotency_key_canonical, idempotency_key_hash, correlation_id,
                requested_at, updated_at)
            VALUES (@id,@event,@attempt,@provider,@environment,@payment,@amount,@currency,
                @reason,'requested',@canonical,@hash,@correlation,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
            """;
        command.Parameters.AddWithValue("@id", refundId); command.Parameters.AddWithValue("@event", source.BillingEventId);
        command.Parameters.AddWithValue("@attempt", source.PaymentAttemptId!); command.Parameters.AddWithValue("@provider", source.Provider!);
        command.Parameters.AddWithValue("@environment", source.Environment!); command.Parameters.AddWithValue("@payment", source.ProviderPaymentId!);
        command.Parameters.AddWithValue("@amount", decision.AmountCents); command.Parameters.AddWithValue("@currency", decision.Currency!);
        command.Parameters.AddWithValue("@reason", reasonCode.Trim()); command.Parameters.AddWithValue("@canonical", BillingV2RefundOutbox.CanonicalIdempotencyKey(source.BillingEventId));
        command.Parameters.AddWithValue("@hash", hash); command.Parameters.AddWithValue("@correlation", correlationId.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnqueueAsync(MySqlConnection connection, MySqlTransaction transaction,
        BillingV2RefundOutboxPayload payload, string hash, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO billing_v2_outbox_events (
                id,aggregate_type,aggregate_id,event_type,payload_text,idempotency_key_hash,
                status,retry_count,available_at,created_at)
            VALUES (@id,@type,@aggregate,@event,@payload,@hash,'pending',0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE id=id;
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("@type", BillingV2RefundOutbox.AggregateType);
        command.Parameters.AddWithValue("@aggregate", payload.RefundId); command.Parameters.AddWithValue("@event", BillingV2RefundOutbox.EventType);
        command.Parameters.AddWithValue("@payload", BillingV2RefundOutbox.Serialize(payload)); command.Parameters.AddWithValue("@hash", hash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task PersistProviderRefundAsync(MySqlConnection connection, string refundId,
        BillingV2StripeRefundSnapshot refund, DateTime now, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE billing_v2_refunds SET status='pending_provider', provider_refund_id=COALESCE(provider_refund_id,@provider_refund_id),
                updated_at=@now WHERE id=@id AND status IN ('requested','pending_provider');
            """;
        command.Parameters.AddWithValue("@id", refundId); command.Parameters.AddWithValue("@provider_refund_id", refund.RefundId); command.Parameters.AddWithValue("@now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkFailedAsync(MySqlConnection connection, string refundId,
        string code, DateTime now, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE billing_v2_refunds SET status='failed', failed_at=@now, failure_code=@code,
                updated_at=@now WHERE id=@id AND status IN ('requested','pending_provider');
            """;
        command.Parameters.AddWithValue("@id", refundId); command.Parameters.AddWithValue("@now", now); command.Parameters.AddWithValue("@code", code);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(MySqlConnection connection, MySqlTransaction transaction,
        string refundId, string action, string actor, string correlation, string details,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO billing_v2_audit_log (id,entity_type,entity_id,action,actor_reference,details_text,created_at)
            VALUES (@id,'billing_v2_refund',@entity_id,@action,@actor,@details,UTC_TIMESTAMP(6));
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("@entity_id", refundId);
        command.Parameters.AddWithValue("@action", action); command.Parameters.AddWithValue("@actor", actor);
        command.Parameters.AddWithValue("@details", $"correlation_id={correlation};{details}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
