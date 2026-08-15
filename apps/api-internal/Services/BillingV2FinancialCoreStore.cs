using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2IntentRecord(
    string Id,
    string SubscriptionId,
    string Status,
    string? BillingEventId,
    long? BaseSubscriptionVersion);

public sealed record BillingV2PaymentAttemptRecord(
    string Id,
    string BillingEventId,
    string Provider,
    string Environment,
    string ProviderRequestKey,
    string Status,
    long ExpectedAmountCents,
    string ExpectedCurrency,
    string? ProviderSessionId,
    string? ProviderPaymentId);

/// <summary>
/// Persistance du coeur financier Billing V2.
///
/// Regroupe les seules ecritures autorisees sur l'intention, l'evenement
/// financier et la tentative de paiement, pour qu'aucun autre service ne
/// puisse contourner les politiques de <see cref="BillingV2BillingEventPolicy"/>
/// et <see cref="BillingV2PaymentAttemptPolicy"/>.
/// </summary>
public static class BillingV2FinancialCoreStore
{
    // -----------------------------------------------------------------
    // A. INTENTION
    // -----------------------------------------------------------------

    public static async Task<BillingV2IntentRecord?> FindIntentByHashAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string idempotencyKeyHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                change_row.id,
                change_row.subscription_id,
                change_row.status,
                change_row.base_subscription_version,
                (SELECT event_row.id
                   FROM billing_v2_billing_events event_row
                  WHERE event_row.subscription_change_id = change_row.id
                  ORDER BY event_row.created_at ASC
                  LIMIT 1) AS billing_event_id
            FROM billing_v2_subscription_changes change_row
            WHERE change_row.idempotency_key_hash = @hash
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@hash", idempotencyKeyHash);
        return await ReadIntentAsync(command, cancellationToken);
    }

    /// <summary>
    /// Retrouve une intention encore ouverte pour la MEME selection metier.
    ///
    /// C'est ce qui permet a un rafraichissement de navigateur - qui fabrique
    /// forcement un nouveau client_request_id - de retomber sur l'intention
    /// existante au lieu d'en ouvrir une seconde. Un choix volontairement
    /// different (autre offre, autre rail) ne matche pas et cree bien une
    /// nouvelle intention.
    /// </summary>
    public static async Task<BillingV2IntentRecord?> FindOpenIntentForSelectionAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string customerId,
        string legacyOfferId,
        string provider,
        string environment,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                change_row.id,
                change_row.subscription_id,
                change_row.status,
                change_row.base_subscription_version,
                request_row.billing_event_id
            FROM billing_v2_authoritative_checkout_requests request_row
            INNER JOIN billing_v2_subscription_changes change_row
                ON change_row.id = request_row.subscription_change_id
            WHERE request_row.customer_id = @customer_id
              AND request_row.legacy_offer_id = @legacy_offer_id
              AND request_row.provider = @provider
              AND request_row.environment = @environment
              AND change_row.status = 'pending'
              AND (change_row.expires_at IS NULL
                   OR change_row.expires_at > @now)
            ORDER BY change_row.requested_at ASC, change_row.id ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        command.Parameters.AddWithValue("@legacy_offer_id", legacyOfferId);
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@environment", environment);
        command.Parameters.AddWithValue("@now", nowUtc);
        return await ReadIntentAsync(command, cancellationToken);
    }

    private static async Task<BillingV2IntentRecord?> ReadIntentAsync(
        MySqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2IntentRecord(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "subscription_id"),
            reader.GetString("status"),
            MariaDbIdentifierReader.ReadNullable(reader, "billing_event_id"),
            reader.IsDBNull(reader.GetOrdinal("base_subscription_version"))
                ? null
                : reader.GetInt64("base_subscription_version"));
    }

    /// <summary>
    /// Insere l'intention. INSERT IGNORE + relecture : sous concurrence, le
    /// perdant recupere l'intention du gagnant au lieu d'en creer une seconde.
    /// </summary>
    public static async Task<bool> TryInsertIntentAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string changeId,
        string subscriptionId,
        string clientRequestId,
        string canonical,
        string hash,
        long baseSubscriptionVersion,
        DateTime nowUtc,
        DateTime expiresAtUtc,
        string? actorReference,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT IGNORE INTO billing_v2_subscription_changes (
                id, subscription_id,
                client_request_id, idempotency_key_canonical,
                idempotency_key_hash, base_subscription_version,
                change_kind, billing_effect,
                requested_at, expires_at, effective_at,
                status, requested_by_reference, created_at
            ) VALUES (
                @id, @subscription_id,
                @client_request_id, @canonical,
                @hash, @base_version,
                'new_subscription', 'initial_charge',
                @now, @expires_at, @now,
                'pending', @actor, @now
            );
            """;
        command.Parameters.AddWithValue("@id", changeId);
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue("@client_request_id", clientRequestId);
        command.Parameters.AddWithValue("@canonical", canonical);
        command.Parameters.AddWithValue("@hash", hash);
        command.Parameters.AddWithValue("@base_version", baseSubscriptionVersion);
        command.Parameters.AddWithValue("@now", nowUtc);
        command.Parameters.AddWithValue("@expires_at", expiresAtUtc);
        command.Parameters.AddWithValue(
            "@actor",
            actorReference is null ? DBNull.Value : actorReference);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public static async Task MarkIntentAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string changeId,
        string status,
        string? failureReasonCode,
        string? reconciliationReasonCode,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_subscription_changes
            SET status = @status,
                applied_at = CASE
                    WHEN @status = 'applied' THEN COALESCE(applied_at, @now)
                    ELSE applied_at
                END,
                failure_reason_code = COALESCE(@failure, failure_reason_code),
                reconciliation_reason_code =
                    COALESCE(@reconciliation, reconciliation_reason_code)
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", changeId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@now", nowUtc);
        command.Parameters.AddWithValue(
            "@failure",
            failureReasonCode is null ? DBNull.Value : failureReasonCode);
        command.Parameters.AddWithValue(
            "@reconciliation",
            reconciliationReasonCode is null
                ? DBNull.Value
                : reconciliationReasonCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // -----------------------------------------------------------------
    // B. BILLING EVENT
    // -----------------------------------------------------------------

    /// <summary>
    /// Ecrit un BillingEvent DEJA finalise avec ses lignes. La validation
    /// applicative est jouee avant l'ecriture : un evenement incoherent n'est
    /// jamais persiste, meme en brouillon.
    /// </summary>
    public static async Task<string> InsertFinalizedBillingEventAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string billingEventId,
        string customerId,
        string subscriptionId,
        string subscriptionChangeId,
        BillingV2BillingEventDraft draft,
        string paymentModeSnapshot,
        int commitmentMonthsSnapshot,
        int discountBasisPointsSnapshot,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateTime nowUtc,
        DateTime settlementDeadlineUtc,
        IReadOnlyList<BillingV2BillingEventLineSource> lineSources,
        CancellationToken cancellationToken)
    {
        var validation = BillingV2BillingEventPolicy.ValidateForFinalization(
            draft);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"{validation.ReasonCode}: {validation.Diagnostic}");
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO billing_v2_billing_events (
                    id, customer_id, subscription_id, subscription_change_id,
                    event_type, direction,
                    financial_status, settlement_status, document_status,
                    currency, period_start, period_end,
                    payment_mode_snapshot, commitment_months_snapshot,
                    discount_basis_points_snapshot,
                    gross_amount_cents, discount_amount_cents,
                    net_amount_cents, tax_amount_cents, total_amount_cents,
                    pricing_engine_version,
                    idempotency_key_canonical, idempotency_key_hash,
                    settlement_deadline_at,
                    created_at, finalized_at
                ) VALUES (
                    @id, @customer_id, @subscription_id, @change_id,
                    @event_type, @direction,
                    'finalized', 'none', 'none',
                    @currency, @period_start, @period_end,
                    @payment_mode, @commitment_months, @discount_bps,
                    @gross, @discount, @net, @tax, @total,
                    @engine_version,
                    @canonical, SHA2(@canonical, 256),
                    @settlement_deadline,
                    @now, @now
                );
                """;
            command.Parameters.AddWithValue("@id", billingEventId);
            command.Parameters.AddWithValue("@customer_id", customerId);
            command.Parameters.AddWithValue("@subscription_id", subscriptionId);
            command.Parameters.AddWithValue("@change_id", subscriptionChangeId);
            command.Parameters.AddWithValue("@event_type", draft.EventType);
            command.Parameters.AddWithValue("@direction", draft.Direction);
            command.Parameters.AddWithValue("@currency", draft.Currency);
            command.Parameters.AddWithValue("@period_start", periodStartUtc);
            command.Parameters.AddWithValue("@period_end", periodEndUtc);
            command.Parameters.AddWithValue("@payment_mode", paymentModeSnapshot);
            command.Parameters.AddWithValue(
                "@commitment_months",
                commitmentMonthsSnapshot);
            command.Parameters.AddWithValue(
                "@discount_bps",
                discountBasisPointsSnapshot);
            command.Parameters.AddWithValue("@gross", draft.GrossAmountCents);
            command.Parameters.AddWithValue("@discount", draft.DiscountAmountCents);
            command.Parameters.AddWithValue("@net", draft.NetAmountCents);
            command.Parameters.AddWithValue("@tax", draft.TaxAmountCents);
            command.Parameters.AddWithValue("@total", draft.TotalAmountCents);
            command.Parameters.AddWithValue(
                "@engine_version",
                draft.PricingEngineVersion);
            command.Parameters.AddWithValue(
                "@canonical",
                draft.IdempotencyKeyCanonical);
            command.Parameters.AddWithValue(
                "@settlement_deadline",
                settlementDeadlineUtc);
            command.Parameters.AddWithValue("@now", nowUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            var source = lineSources[index];
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO billing_v2_billing_event_lines (
                    id, billing_event_id,
                    service_id, tier_id, service_price_id,
                    service_code, tier_code, description, billing_cadence,
                    quantity, unit_amount_cents, gross_amount_cents,
                    discount_allocated_amount_cents, net_amount_cents,
                    tax_amount_cents, total_amount_cents, currency,
                    period_start, period_end, display_order, created_at
                ) VALUES (
                    @id, @billing_event_id,
                    @service_id, @tier_id, @service_price_id,
                    @service_code, @tier_code, @description, @cadence,
                    @quantity, @unit, @gross,
                    @discount, @net,
                    @tax, @total, @currency,
                    @period_start, @period_end, @display_order, @now
                );
                """;
            command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue("@billing_event_id", billingEventId);
            command.Parameters.AddWithValue("@service_id", source.ServiceId);
            command.Parameters.AddWithValue(
                "@tier_id",
                source.TierId is null ? DBNull.Value : source.TierId);
            command.Parameters.AddWithValue(
                "@service_price_id",
                source.ServicePriceId);
            command.Parameters.AddWithValue("@service_code", line.ServiceCode);
            command.Parameters.AddWithValue(
                "@tier_code",
                line.TierCode is null ? DBNull.Value : line.TierCode);
            command.Parameters.AddWithValue("@description", line.Description);
            command.Parameters.AddWithValue("@cadence", source.Cadence);
            command.Parameters.AddWithValue("@quantity", line.Quantity);
            command.Parameters.AddWithValue("@unit", line.UnitAmountCents);
            command.Parameters.AddWithValue("@gross", line.GrossAmountCents);
            command.Parameters.AddWithValue(
                "@discount",
                line.DiscountAllocatedAmountCents);
            command.Parameters.AddWithValue("@net", line.NetAmountCents);
            command.Parameters.AddWithValue("@tax", line.TaxAmountCents);
            command.Parameters.AddWithValue("@total", line.TotalAmountCents);
            command.Parameters.AddWithValue("@currency", line.Currency);
            command.Parameters.AddWithValue("@period_start", periodStartUtc);
            command.Parameters.AddWithValue("@period_end", periodEndUtc);
            command.Parameters.AddWithValue("@display_order", line.DisplayOrder);
            command.Parameters.AddWithValue("@now", nowUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return billingEventId;
    }

    public static async Task<BillingV2FinalizedBillingEvent?>
        ReadBillingEventAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string billingEventId,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                event_row.id,
                event_row.subscription_id,
                event_row.customer_id,
                event_row.financial_status,
                event_row.settlement_status,
                event_row.currency,
                event_row.payment_mode_snapshot,
                event_row.commitment_months_snapshot,
                event_row.total_amount_cents,
                event_row.tax_amount_cents,
                COALESCE(SUM(CASE WHEN line.billing_cadence = 'one_time'
                                  THEN line.total_amount_cents ELSE 0 END), 0)
                    AS one_time_amount_cents,
                COALESCE(SUM(CASE WHEN line.billing_cadence <> 'one_time'
                                  THEN line.total_amount_cents ELSE 0 END), 0)
                    AS recurring_amount_cents,
                COUNT(line.id) AS line_count
            FROM billing_v2_billing_events event_row
            LEFT JOIN billing_v2_billing_event_lines line
                ON line.billing_event_id = event_row.id
            WHERE event_row.id = @id
            GROUP BY event_row.id;
            """;
        command.Parameters.AddWithValue("@id", billingEventId);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2FinalizedBillingEvent(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "subscription_id"),
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            reader.GetString("financial_status"),
            reader.GetString("settlement_status"),
            reader.GetString("currency"),
            reader.GetString("payment_mode_snapshot"),
            reader.GetInt32("commitment_months_snapshot"),
            reader.GetInt64("total_amount_cents"),
            reader.GetInt64("recurring_amount_cents"),
            reader.GetInt64("one_time_amount_cents"),
            reader.GetInt64("tax_amount_cents"),
            reader.GetInt32("line_count"));
    }

    public static async Task ApplySettlementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string billingEventId,
        string settlementStatus,
        string reasonCode,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_billing_events
            SET settlement_status = @settlement_status,
                settlement_reason_code = @reason_code,
                settled_at = CASE
                    WHEN @settlement_status = 'settled'
                        THEN COALESCE(settled_at, @now)
                    ELSE settled_at
                END
            WHERE id = @id
              AND financial_status = 'finalized'
              AND settlement_status <> 'settled';
            """;
        command.Parameters.AddWithValue("@id", billingEventId);
        command.Parameters.AddWithValue("@settlement_status", settlementStatus);
        command.Parameters.AddWithValue("@reason_code", reasonCode);
        command.Parameters.AddWithValue("@now", nowUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // -----------------------------------------------------------------
    // C. PAYMENT ATTEMPT
    // -----------------------------------------------------------------

    /// <summary>
    /// Resout la tentative de paiement AVANT tout appel provider.
    ///
    /// La cle provider est derivee de facon deterministe de l'evenement
    /// financier : un retry retombe donc exactement sur la meme cle, ce qui
    /// fait renvoyer par Stripe la session deja creee au lieu d'en creer une
    /// seconde.
    /// </summary>
    public static async Task<BillingV2PaymentAttemptRecord>
        ResolveOrCreateAttemptAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string billingEventId,
            string provider,
            string environment,
            long expectedAmountCents,
            string expectedCurrency,
            DateTime nowUtc,
            CancellationToken cancellationToken)
    {
        var requestKey = BuildProviderRequestKey(billingEventId);
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT IGNORE INTO billing_v2_payment_attempts (
                    id, billing_event_id, provider, environment,
                    provider_request_key,
                    expected_amount_cents, expected_currency,
                    status, attempted_at, created_at, updated_at
                ) VALUES (
                    @id, @billing_event_id, @provider, @environment,
                    @request_key,
                    @expected_amount, @expected_currency,
                    'created', @now, @now, @now
                );
                """;
            insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            insert.Parameters.AddWithValue("@billing_event_id", billingEventId);
            insert.Parameters.AddWithValue("@provider", provider);
            insert.Parameters.AddWithValue("@environment", environment);
            insert.Parameters.AddWithValue("@request_key", requestKey);
            insert.Parameters.AddWithValue(
                "@expected_amount",
                expectedAmountCents);
            insert.Parameters.AddWithValue(
                "@expected_currency",
                expectedCurrency);
            insert.Parameters.AddWithValue("@now", nowUtc);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        return await ReadAttemptByRequestKeyAsync(
                   connection,
                   transaction,
                   provider,
                   environment,
                   requestKey,
                   cancellationToken)
               ?? throw new InvalidOperationException(
                   "BILLING_V2_PAYMENT_ATTEMPT_NOT_PERSISTED");
    }

    public static string BuildProviderRequestKey(string billingEventId)
        => $"bv2-evt-{billingEventId}";

    public static async Task<BillingV2PaymentAttemptRecord?>
        ReadAttemptByRequestKeyAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string provider,
            string environment,
            string requestKey,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, billing_event_id, provider, environment,
                   provider_request_key, status,
                   expected_amount_cents, expected_currency,
                   provider_session_id, provider_payment_id
            FROM billing_v2_payment_attempts
            WHERE provider = @provider
              AND environment = @environment
              AND provider_request_key = @request_key
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@environment", environment);
        command.Parameters.AddWithValue("@request_key", requestKey);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2PaymentAttemptRecord(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "billing_event_id"),
            reader.GetString("provider"),
            reader.GetString("environment"),
            reader.GetString("provider_request_key"),
            reader.GetString("status"),
            reader.GetInt64("expected_amount_cents"),
            reader.GetString("expected_currency"),
            reader.IsDBNull(reader.GetOrdinal("provider_session_id"))
                ? null
                : reader.GetString("provider_session_id"),
            reader.IsDBNull(reader.GetOrdinal("provider_payment_id"))
                ? null
                : reader.GetString("provider_payment_id"));
    }

    public static async Task UpdateAttemptAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string attemptId,
        string status,
        string? providerSessionId,
        string? providerPaymentId,
        string? providerMode,
        string? providerPaymentStatus,
        long? settledAmountCents,
        string? settledCurrency,
        string? verificationReasonCode,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_payment_attempts
            SET status = @status,
                provider_session_id =
                    COALESCE(@session_id, provider_session_id),
                provider_payment_id =
                    COALESCE(@payment_id, provider_payment_id),
                provider_mode = COALESCE(@mode, provider_mode),
                provider_payment_status =
                    COALESCE(@payment_status, provider_payment_status),
                settled_amount_cents =
                    COALESCE(@settled_amount, settled_amount_cents),
                settled_currency =
                    COALESCE(@settled_currency, settled_currency),
                verification_reason_code =
                    COALESCE(@verification_reason, verification_reason_code),
                responded_at = COALESCE(responded_at, @now),
                verified_at = CASE
                    WHEN @verification_reason IS NULL THEN verified_at
                    ELSE @now
                END,
                updated_at = @now
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", attemptId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue(
            "@session_id",
            providerSessionId is null ? DBNull.Value : providerSessionId);
        command.Parameters.AddWithValue(
            "@payment_id",
            providerPaymentId is null ? DBNull.Value : providerPaymentId);
        command.Parameters.AddWithValue(
            "@mode",
            providerMode is null ? DBNull.Value : providerMode);
        command.Parameters.AddWithValue(
            "@payment_status",
            providerPaymentStatus is null
                ? DBNull.Value
                : providerPaymentStatus);
        command.Parameters.AddWithValue(
            "@settled_amount",
            settledAmountCents.HasValue
                ? settledAmountCents.Value
                : DBNull.Value);
        command.Parameters.AddWithValue(
            "@settled_currency",
            settledCurrency is null ? DBNull.Value : settledCurrency);
        command.Parameters.AddWithValue(
            "@verification_reason",
            verificationReasonCode is null
                ? DBNull.Value
                : verificationReasonCode);
        command.Parameters.AddWithValue("@now", nowUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // -----------------------------------------------------------------
    // G. CONCURRENCE SUR L'ABONNEMENT
    // -----------------------------------------------------------------

    /// <summary>
    /// Verrouille l'abonnement pour la duree de la transaction et renvoie sa
    /// version courante. Deux evenements Stripe concurrents sur le meme
    /// abonnement sont ainsi serialises au lieu de se marcher dessus.
    /// </summary>
    public static async Task<long?> LockSubscriptionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT version
            FROM billing_v2_subscriptions
            WHERE id = @id
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("@id", subscriptionId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToInt64(value);
    }

    /// <summary>
    /// Compare-and-swap sur la version. Zero ligne affectee = conflit, qui doit
    /// remonter en echec explicite : jamais de lost update silencieux.
    /// </summary>
    public static async Task<BillingV2FinancialDecision>
        TryAdvanceSubscriptionAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string subscriptionId,
            long expectedVersion,
            string newStatus,
            DateTime nowUtc,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_subscriptions
            SET status = @status,
                version = version + 1,
                started_at = CASE
                    WHEN @status = 'active' THEN COALESCE(started_at, @now)
                    ELSE started_at
                END,
                updated_at = @now
            WHERE id = @id
              AND version = @expected_version;
            """;
        command.Parameters.AddWithValue("@id", subscriptionId);
        command.Parameters.AddWithValue("@status", newStatus);
        command.Parameters.AddWithValue("@expected_version", expectedVersion);
        command.Parameters.AddWithValue("@now", nowUtc);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return BillingV2SubscriptionVersionPolicy.EvaluateCompareAndSwap(
            affected);
    }
}

public sealed record BillingV2BillingEventLineSource(
    string ServiceId,
    string? TierId,
    string ServicePriceId,
    string Cadence);
