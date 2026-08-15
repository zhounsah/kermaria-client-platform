using System.Data;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2StripeDispatchResult(
    bool Succeeded,
    string ReasonCode,
    string? SessionId,
    string? ApprovalUrl,
    bool Retryable);

public sealed record BillingV2StripeSettlementResult(
    bool Settled,
    string ReasonCode,
    string? SubscriptionId,
    bool ReconciliationRequired);

public interface IBillingV2StripeRailService
{
    Task<BillingV2StripeDispatchResult> DispatchCheckoutAsync(
        string subscriptionId,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken);

    Task<BillingV2StripeSettlementResult> VerifyAndSettleAsync(
        string subscriptionId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Rail Stripe Billing V2 - orchestration (Phase 2).
///
/// Chaine appliquee, sans raccourci possible :
///
///   BillingEvent finalized -> PaymentAttempt persistee -> Stripe
///     -> refetch Stripe -> settlement verifie -> activation locale
///
/// Deux proprietes portees ici et nulle part ailleurs :
///
/// 1. le montant envoye a Stripe vient du BillingEvent finalise, jamais du
///    catalogue ni d'un price_id externe ;
/// 2. aucune transition financiere ne decoule d'un signal brut : elle decoule
///    toujours d'une relecture de l'objet chez Stripe.
/// </summary>
public sealed class BillingV2StripeRailService : IBillingV2StripeRailService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly IBillingV2StripeGateway _gateway;
    private readonly ILogger<BillingV2StripeRailService> _logger;

    public BillingV2StripeRailService(
        SqlRuntimeConfiguration sql,
        StripeRuntimeConfiguration stripe,
        IBillingV2StripeGateway gateway,
        ILogger<BillingV2StripeRailService> logger)
    {
        _sql = sql;
        _stripe = stripe;
        _gateway = gateway;
        _logger = logger;
    }

    private const string Provider = "stripe";

    // -----------------------------------------------------------------
    // C + D : tentative persistee, puis appel Stripe au montant local
    // -----------------------------------------------------------------

    public async Task<BillingV2StripeDispatchResult> DispatchCheckoutAsync(
        string subscriptionId,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return Failed("BILLING_V2_STRIPE_RAIL_NO_PERSISTENT_SQL", false);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var billingEventId = await ReadBillingEventIdAsync(
            connection,
            subscriptionId,
            cancellationToken);
        var billingEvent = billingEventId is null
            ? null
            : await BillingV2FinancialCoreStore.ReadBillingEventAsync(
                connection,
                transaction: null,
                billingEventId,
                cancellationToken);

        // B. Sans BillingEvent finalise et coherent, rien ne part chez Stripe.
        var guard = BillingV2StripeDispatchGuard.Evaluate(billingEvent);
        if (!guard.IsValid)
        {
            _logger.LogWarning(
                "Billing V2 Stripe dispatch refused for subscription {SubscriptionId}: {ReasonCode}.",
                subscriptionId,
                guard.ReasonCode);
            return Failed(guard.ReasonCode, false);
        }

        var environment = _stripe.ModeName;
        BillingV2PaymentAttemptRecord attempt;
        await using (var transaction = await connection.BeginTransactionAsync(
                         IsolationLevel.ReadCommitted,
                         cancellationToken))
        {
            // C. La tentative est ecrite AVANT le moindre appel reseau.
            attempt = await BillingV2FinancialCoreStore
                .ResolveOrCreateAttemptAsync(
                    connection,
                    transaction,
                    billingEvent!.Id,
                    Provider,
                    environment,
                    billingEvent.TotalAmountCents,
                    billingEvent.Currency,
                    DateTime.UtcNow,
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var reuse = BillingV2PaymentAttemptPolicy.EvaluateProviderCall(
            new BillingV2PaymentAttemptSnapshot(
                attempt.Id,
                attempt.Provider,
                attempt.Environment,
                attempt.ProviderRequestKey,
                attempt.Status),
            Provider,
            environment);
        if (!reuse.CanCall)
        {
            return Failed(reuse.ReasonCode, false);
        }

        // Reprise apres interruption : si un identifiant de session a deja ete
        // persiste, on RELIT au lieu de recreer. Une seconde session voudrait
        // dire un second encaissement possible.
        if (!string.IsNullOrWhiteSpace(attempt.ProviderSessionId))
        {
            var existing = await _gateway.GetCheckoutSessionAsync(
                attempt.ProviderSessionId!,
                cancellationToken);
            if (existing is not null)
            {
                return new BillingV2StripeDispatchResult(
                    true,
                    "BILLING_V2_STRIPE_CHECKOUT_ALREADY_CREATED",
                    existing.SessionId,
                    ApprovalUrl: null,
                    Retryable: false);
            }
        }

        var stripeRequest = BillingV2StripeCheckoutRequestFactory.Build(
            billingEvent,
            attempt.Id,
            attempt.ProviderRequestKey,
            customerEmail,
            successUrl,
            cancelUrl);
        var created = await _gateway.CreateCheckoutSessionAsync(
            stripeRequest,
            cancellationToken);

        if (!created.Succeeded)
        {
            // Timeout reseau : l'appel a peut-etre abouti. On ne cree surtout
            // pas une nouvelle tentative ; on interroge Stripe avec la cle
            // persistee avant tout nouvel essai.
            if (string.Equals(
                    created.Code,
                    "BILLING_V2_STRIPE_CALL_INDETERMINATE",
                    StringComparison.Ordinal))
            {
                var recovered = await _gateway
                    .FindCheckoutSessionByRequestKeyAsync(
                        attempt.ProviderRequestKey,
                        cancellationToken);
                if (recovered is not null)
                {
                    await PersistSessionAsync(
                        connection,
                        attempt,
                        billingEvent,
                        recovered.SessionId,
                        recovered.SubscriptionId,
                        stripeRequest.Mode,
                        cancellationToken);
                    return new BillingV2StripeDispatchResult(
                        true,
                        "BILLING_V2_STRIPE_CHECKOUT_RECOVERED",
                        recovered.SessionId,
                        ApprovalUrl: null,
                        Retryable: false);
                }
            }

            await BillingV2FinancialCoreStore.UpdateAttemptAsync(
                connection,
                transaction: null,
                attempt.Id,
                created.Retryable
                    ? BillingV2PaymentAttemptStatuses.InFlight
                    : BillingV2PaymentAttemptStatuses.Failed,
                providerSessionId: null,
                providerPaymentId: null,
                stripeRequest.Mode,
                providerPaymentStatus: null,
                settledAmountCents: null,
                settledCurrency: null,
                verificationReasonCode: null,
                DateTime.UtcNow,
                cancellationToken);
            return Failed(created.Code, created.Retryable);
        }

        await PersistSessionAsync(
            connection,
            attempt,
            billingEvent,
            created.SessionId!,
            created.SubscriptionId,
            stripeRequest.Mode,
            cancellationToken);

        return new BillingV2StripeDispatchResult(
            true,
            "BILLING_V2_STRIPE_CHECKOUT_CREATED",
            created.SessionId,
            created.ApprovalUrl,
            Retryable: false);
    }

    // -----------------------------------------------------------------
    // E + F + G : refetch, verification, settlement, activation
    // -----------------------------------------------------------------

    public async Task<BillingV2StripeSettlementResult> VerifyAndSettleAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return new BillingV2StripeSettlementResult(
                false,
                "BILLING_V2_STRIPE_RAIL_NO_PERSISTENT_SQL",
                subscriptionId,
                false);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var billingEventId = await ReadBillingEventIdAsync(
            connection,
            subscriptionId,
            cancellationToken);
        if (billingEventId is null)
        {
            return new BillingV2StripeSettlementResult(
                false,
                "BILLING_V2_STRIPE_SETTLEMENT_WITHOUT_BILLING_EVENT",
                subscriptionId,
                true);
        }

        var billingEvent = await BillingV2FinancialCoreStore
            .ReadBillingEventAsync(
                connection,
                transaction: null,
                billingEventId,
                cancellationToken);
        var attempt = await BillingV2FinancialCoreStore
            .ReadAttemptByRequestKeyAsync(
                connection,
                transaction: null,
                Provider,
                _stripe.ModeName,
                BillingV2FinancialCoreStore.BuildProviderRequestKey(
                    billingEventId),
                cancellationToken);
        if (billingEvent is null || attempt is null)
        {
            return new BillingV2StripeSettlementResult(
                false,
                "BILLING_V2_STRIPE_SETTLEMENT_WITHOUT_ATTEMPT",
                subscriptionId,
                true);
        }

        if (string.IsNullOrWhiteSpace(attempt.ProviderSessionId))
        {
            return new BillingV2StripeSettlementResult(
                false,
                "BILLING_V2_STRIPE_SETTLEMENT_WITHOUT_SESSION",
                subscriptionId,
                false);
        }

        // E. LE point de verite : on relit l'objet chez Stripe. Le webhook qui
        // nous a amenes ici n'a servi qu'a declencher cette lecture.
        var snapshot = await _gateway.GetCheckoutSessionAsync(
            attempt.ProviderSessionId!,
            cancellationToken);
        var expectedMode = string.Equals(
                billingEvent.PaymentModeSnapshot,
                BillingV2PaymentModes.Upfront,
                StringComparison.Ordinal)
            ? BillingV2StripeModes.Payment
            : BillingV2StripeModes.Subscription;
        var verification = BillingV2StripeSettlementVerifier.Verify(
            snapshot,
            new BillingV2StripeVerificationExpectation(
                billingEvent.Id,
                billingEvent.SubscriptionId,
                attempt.Id,
                attempt.ExpectedCurrency,
                attempt.ExpectedAmountCents,
                expectedMode,
                ExpectedCustomerEmail: null));

        await BillingV2FinancialCoreStore.UpdateAttemptAsync(
            connection,
            transaction: null,
            attempt.Id,
            verification.AttemptStatus,
            snapshot?.SessionId,
            snapshot?.PaymentIntentId,
            snapshot?.Mode,
            snapshot?.PaymentStatus,
            verification.SettledAmountCents,
            verification.SettledCurrency,
            verification.ReasonCode,
            DateTime.UtcNow,
            cancellationToken);

        if (!verification.Settled)
        {
            var reconciliation = string.Equals(
                verification.SettlementStatus,
                BillingV2SettlementStatuses.AmountMismatch,
                StringComparison.Ordinal);
            if (reconciliation)
            {
                await using var mismatchTransaction =
                    await connection.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);
                await BillingV2FinancialCoreStore.ApplySettlementAsync(
                    connection,
                    mismatchTransaction,
                    billingEvent.Id,
                    BillingV2SettlementStatuses.AmountMismatch,
                    verification.ReasonCode,
                    DateTime.UtcNow,
                    cancellationToken);
                await mismatchTransaction.CommitAsync(cancellationToken);
                _logger.LogWarning(
                    "Billing V2 Stripe settlement mismatch on subscription {SubscriptionId}: {ReasonCode}. No activation performed.",
                    subscriptionId,
                    verification.ReasonCode);
            }

            return new BillingV2StripeSettlementResult(
                false,
                verification.ReasonCode,
                subscriptionId,
                reconciliation);
        }

        // G. Verrou puis compare-and-swap : deux evenements Stripe concurrents
        // sur le meme abonnement sont serialises, et le perdant ne peut pas
        // ecraser silencieusement le gagnant.
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var version = await BillingV2FinancialCoreStore.LockSubscriptionAsync(
            connection,
            transaction,
            subscriptionId,
            cancellationToken);
        if (version is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new BillingV2StripeSettlementResult(
                false,
                "BILLING_V2_STRIPE_SETTLEMENT_SUBSCRIPTION_NOT_FOUND",
                subscriptionId,
                true);
        }

        var alreadySettled = await IsAlreadySettledAsync(
            connection,
            transaction,
            billingEvent.Id,
            cancellationToken);
        if (alreadySettled)
        {
            // Rejeu : le settlement logique est deja acquis, on ne recree rien.
            await transaction.CommitAsync(cancellationToken);
            return new BillingV2StripeSettlementResult(
                true,
                "BILLING_V2_STRIPE_SETTLEMENT_ALREADY_APPLIED",
                subscriptionId,
                false);
        }

        await BillingV2FinancialCoreStore.ApplySettlementAsync(
            connection,
            transaction,
            billingEvent.Id,
            BillingV2SettlementStatuses.Settled,
            verification.ReasonCode,
            DateTime.UtcNow,
            cancellationToken);

        var swap = await BillingV2FinancialCoreStore
            .TryAdvanceSubscriptionAsync(
                connection,
                transaction,
                subscriptionId,
                version.Value,
                "active",
                DateTime.UtcNow,
                cancellationToken);
        if (!swap.IsValid)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(
                "Billing V2 subscription {SubscriptionId} version conflict during settlement: {ReasonCode}.",
                subscriptionId,
                swap.ReasonCode);
            return new BillingV2StripeSettlementResult(
                false,
                swap.ReasonCode,
                subscriptionId,
                true);
        }

        await MarkIntentAppliedAsync(
            connection,
            transaction,
            billingEvent.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BillingV2StripeSettlementResult(
            true,
            "BILLING_V2_STRIPE_SETTLEMENT_CONFIRMED",
            subscriptionId,
            false);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static BillingV2StripeDispatchResult Failed(
        string reasonCode,
        bool retryable)
        => new(false, reasonCode, null, null, retryable);

    private static async Task<string?> ReadBillingEventIdAsync(
        MySqlConnection connection,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT billing_event_id
            FROM billing_v2_authoritative_checkout_requests
            WHERE subscription_id = @subscription_id
              AND billing_event_id IS NOT NULL
            ORDER BY created_at ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static async Task<bool> IsAlreadySettledAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string billingEventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT settlement_status
            FROM billing_v2_billing_events
            WHERE id = @id
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("@id", billingEventId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return string.Equals(
            Convert.ToString(value),
            BillingV2SettlementStatuses.Settled,
            StringComparison.Ordinal);
    }

    private static async Task MarkIntentAppliedAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string billingEventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_subscription_changes change_row
            INNER JOIN billing_v2_billing_events event_row
                ON event_row.subscription_change_id = change_row.id
            SET change_row.status = 'applied',
                change_row.applied_at = COALESCE(
                    change_row.applied_at,
                    UTC_TIMESTAMP(6))
            WHERE event_row.id = @billing_event_id
              AND change_row.status = 'pending';
            """;
        command.Parameters.AddWithValue("@billing_event_id", billingEventId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task PersistSessionAsync(
        MySqlConnection connection,
        BillingV2PaymentAttemptRecord attempt,
        BillingV2FinalizedBillingEvent billingEvent,
        string sessionId,
        string? providerSubscriptionId,
        string mode,
        CancellationToken cancellationToken)
    {
        await BillingV2FinancialCoreStore.UpdateAttemptAsync(
            connection,
            transaction: null,
            attempt.Id,
            BillingV2PaymentAttemptStatuses.InFlight,
            sessionId,
            providerPaymentId: null,
            mode,
            providerPaymentStatus: null,
            settledAmountCents: null,
            settledCurrency: null,
            verificationReasonCode: null,
            DateTime.UtcNow,
            cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE billing_v2_provider_checkout_sessions
            SET billing_event_id = COALESCE(billing_event_id, @billing_event_id),
                payment_attempt_id = COALESCE(payment_attempt_id, @attempt_id),
                provider_checkout_id = COALESCE(provider_checkout_id, @session_id),
                provider_subscription_id = COALESCE(
                    provider_subscription_id,
                    @provider_subscription_id),
                updated_at = UTC_TIMESTAMP(6)
            WHERE subscription_id = @subscription_id
              AND provider = 'stripe';
            """;
        command.Parameters.AddWithValue("@billing_event_id", billingEvent.Id);
        command.Parameters.AddWithValue("@attempt_id", attempt.Id);
        command.Parameters.AddWithValue("@session_id", sessionId);
        command.Parameters.AddWithValue(
            "@provider_subscription_id",
            providerSubscriptionId is null
                ? DBNull.Value
                : providerSubscriptionId);
        command.Parameters.AddWithValue(
            "@subscription_id",
            billingEvent.SubscriptionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
