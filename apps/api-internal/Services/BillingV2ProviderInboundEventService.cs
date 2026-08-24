using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Provisioning;
using MySqlConnector;
using System.Data;
using System.Text.Json;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2ProviderInboundEventRequest(
    string Provider,
    string Environment,
    string ProviderEventId,
    string EventType,
    string? ProviderCheckoutId,
    string? ProviderSubscriptionId,
    string? PayloadText,
    string? ExpectedCustomerId = null,
    string? LocalSubscriptionId = null);

public sealed record BillingV2ProviderInboundEventResult(
    bool Applied,
    string ReasonCode,
    string? SubscriptionId,
    string? CheckoutSessionId);

public sealed record BillingV2ProviderLocalState(
    string? CheckoutSessionId,
    string SubscriptionId,
    string Provider,
    string Environment,
    string? ProviderCheckoutId,
    string? ProviderSubscriptionId,
    string CheckoutStatus,
    string? AgreementStatus,
    string SubscriptionStatus);

public sealed record BillingV2ProviderInboundEventPlan(
    bool CanApply,
    bool AlreadyApplied,
    string ReasonCode,
    string? CheckoutStatus,
    string? AgreementStatus,
    string? SubscriptionStatus,
    string? ProviderSubscriptionId);

public interface IBillingV2ProviderInboundEventService
{
    Task<BillingV2ProviderInboundEventResult> ProcessAsync(
        BillingV2ProviderInboundEventRequest request,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2ProviderInboundEventService
    : IBillingV2ProviderInboundEventService
{
    public static NoOpBillingV2ProviderInboundEventService Instance { get; }
        = new();

    private NoOpBillingV2ProviderInboundEventService()
    {
    }

    public Task<BillingV2ProviderInboundEventResult> ProcessAsync(
        BillingV2ProviderInboundEventRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(new BillingV2ProviderInboundEventResult(
            Applied: false,
            "BILLING_V2_PROVIDER_INBOUND_DISABLED",
            SubscriptionId: null,
            CheckoutSessionId: null));
}

public sealed class BillingV2ProviderInboundEventService
    : IBillingV2ProviderInboundEventService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _configuration;
    private readonly IBillingV2DocumentIssuerService _documents;
    private readonly IBillingV2ProvisioningService _provisioning;
    private readonly IBillingV2StripeRailService _stripeRail;
    private readonly IBillingV2RenewalService _renewals;
    private readonly ILogger<BillingV2ProviderInboundEventService> _logger;

    public BillingV2ProviderInboundEventService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration configuration,
        IBillingV2DocumentIssuerService documents,
        IBillingV2ProvisioningService provisioning,
        IBillingV2StripeRailService stripeRail,
        IBillingV2RenewalService renewals,
        ILogger<BillingV2ProviderInboundEventService> logger)
    {
        _sql = sql;
        _configuration = configuration;
        _documents = documents;
        _provisioning = provisioning;
        _stripeRail = stripeRail;
        _renewals = renewals;
        _logger = logger;
    }

    public async Task<BillingV2ProviderInboundEventResult> ProcessAsync(
        BillingV2ProviderInboundEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!_configuration.AuthoritativeCheckoutEnabled
            || !_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            _logger.LogWarning(
                "Billing V2 provider inbound event {ProviderEventId} blocked: checkout V2 is not authoritative or SQL is not persistent. No local subscription was changed.",
                request.ProviderEventId);
            return new BillingV2ProviderInboundEventResult(
                Applied: false,
                "BILLING_V2_PROVIDER_INBOUND_GATE_CLOSED",
                SubscriptionId: null,
                CheckoutSessionId: null);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var existingEvent = await ReadOrCreateProviderEventAsync(
            connection,
            transaction,
            request,
            cancellationToken);
        if (string.Equals(
                existingEvent.Status,
                "processed",
                StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            var result = new BillingV2ProviderInboundEventResult(
                Applied: false,
                "BILLING_V2_PROVIDER_EVENT_ALREADY_PROCESSED",
                existingEvent.SubscriptionId,
                existingEvent.CheckoutSessionId);
            if (BillingV2ProviderInboundProvisioningPolicy
                .ShouldAttemptProcessedReplay(existingEvent.ReasonCode))
            {
                await TryIssueDocumentAsync(
                    result.SubscriptionId,
                    cancellationToken);
                await TryTriggerProvisioningAsync(
                    result.SubscriptionId,
                    existingEvent.ReasonCode
                        ?? result.ReasonCode,
                    cancellationToken);
            }

            return result;
        }

        try
        {
            var state = await ResolveLocalStateAsync(
                connection,
                transaction,
                request,
                cancellationToken);
            if (state.Count != 1)
            {
                var reason = state.Count == 0
                    ? "BILLING_V2_PROVIDER_LOCAL_SESSION_NOT_FOUND"
                    : "BILLING_V2_PROVIDER_LOCAL_SESSION_AMBIGUOUS";
                await MarkProviderEventAsync(
                    connection,
                    transaction,
                    request.ProviderEventId,
                    request.Provider,
                    request.Environment,
                    status: state.Count == 0 ? "skipped" : "failed",
                    reason,
                    lastError: null,
                    subscriptionId: null,
                    checkoutSessionId: null,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new BillingV2ProviderInboundEventResult(
                    Applied: false,
                    reason,
                    SubscriptionId: null,
                    CheckoutSessionId: null);
            }

            var localState = state[0];
            var plan = BillingV2ProviderInboundEventPlanner.Plan(
                request,
                localState);
            if (!plan.CanApply)
            {
                await MarkProviderEventAsync(
                    connection,
                    transaction,
                    request.ProviderEventId,
                    request.Provider,
                    request.Environment,
                    "failed",
                    plan.ReasonCode,
                    lastError: null,
                    localState.SubscriptionId,
                    localState.CheckoutSessionId,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new BillingV2ProviderInboundEventResult(
                    Applied: false,
                    plan.ReasonCode,
                    localState.SubscriptionId,
                    localState.CheckoutSessionId);
            }

            if (!plan.AlreadyApplied)
            {
                await ApplyPlanAsync(
                    connection,
                    transaction,
                    request,
                    localState,
                    plan,
                    cancellationToken);
            }

            await MarkProviderEventAsync(
                connection,
                transaction,
                request.ProviderEventId,
                request.Provider,
                request.Environment,
                "processed",
                plan.ReasonCode,
                lastError: null,
                localState.SubscriptionId,
                localState.CheckoutSessionId,
                cancellationToken);
            await InsertAuditAsync(
                connection,
                transaction,
                request,
                localState,
                plan,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var result = new BillingV2ProviderInboundEventResult(
                Applied: !plan.AlreadyApplied,
                plan.AlreadyApplied
                    ? "BILLING_V2_PROVIDER_EVENT_IDEMPOTENT_NOOP"
                    : plan.ReasonCode,
                localState.SubscriptionId,
                localState.CheckoutSessionId);
            // Phase 2. Le signal ne fait que declencher une RELECTURE Stripe.
            // Document et provisioning ne suivent que si cette relecture a
            // confirme l'encaissement du montant attendu, dans la bonne devise.
            var settledByVerification = false;

            // Phase 3. Signal de cycle : on relit l'invoice chez Stripe, on en
            // deduit le cycle, on facture ce cycle s'il ne l'est pas deja, puis
            // on verifie. Rien ne decoule du payload.
            if (BillingV2ProviderInboundRenewalPolicy.IsRenewalSignal(
                    request.Provider,
                    plan.ReasonCode))
            {
                var renewal = await _renewals.HandleProviderSignalAsync(
                    localState.SubscriptionId,
                    cancellationToken);
                if (renewal.Settled)
                {
                    await TryIssueRenewalDocumentAsync(
                        localState.SubscriptionId,
                        renewal.BillingEventId!,
                        renewal.CycleSequence,
                        cancellationToken);
                }
                else
                {
                    _logger.LogWarning(
                        "Billing V2 renewal signal for subscription {SubscriptionId} did not settle: {ReasonCode}. No document, no provisioning change.",
                        localState.SubscriptionId,
                        renewal.ReasonCode);
                }

                return result with { ReasonCode = renewal.ReasonCode };
            }

            // Signal d'abonnement : controle de sante uniquement. Il ne peut
            // que degrader l'etat local, jamais activer ni encaisser.
            if (BillingV2ProviderInboundRenewalPolicy.IsSubscriptionHealthSignal(
                    request.Provider,
                    plan.ReasonCode)
                && !string.IsNullOrWhiteSpace(plan.ProviderSubscriptionId))
            {
                var health = await _stripeRail.EvaluateSubscriptionHealthAsync(
                    localState.SubscriptionId,
                    plan.ProviderSubscriptionId!,
                    cancellationToken);
                return result with { ReasonCode = health.ReasonCode };
            }

            if (BillingV2ProviderInboundProvisioningPolicy.ShouldVerifySettlement(
                    request.Provider,
                    plan.ReasonCode))
            {
                var settlement = await _stripeRail.VerifyAndSettleAsync(
                    localState.SubscriptionId,
                    cancellationToken);
                settledByVerification = settlement.Settled;
                if (!settlement.Settled)
                {
                    _logger.LogWarning(
                        "Billing V2 Stripe settlement not confirmed for subscription {SubscriptionId}: {ReasonCode}. No document and no provisioning.",
                        localState.SubscriptionId,
                        settlement.ReasonCode);
                    return result with { ReasonCode = settlement.ReasonCode };
                }
            }

            if (settledByVerification
                || (!string.Equals(
                        request.Provider,
                        "stripe",
                        StringComparison.OrdinalIgnoreCase)
                    && BillingV2ProviderInboundProvisioningPolicy.ShouldAttempt(
                        plan,
                        localState)))
            {
                await TryIssueDocumentAsync(
                    localState.SubscriptionId,
                    cancellationToken);
                await TryTriggerProvisioningAsync(
                    localState.SubscriptionId,
                    plan.ReasonCode,
                    cancellationToken);
            }

            return result;
        }
        catch (Exception exception)
        {
            await MarkProviderEventAsync(
                connection,
                transaction,
                request.ProviderEventId,
                request.Provider,
                request.Environment,
                "failed",
                "BILLING_V2_PROVIDER_EVENT_FAILED",
                exception.Message,
                subscriptionId: null,
                checkoutSessionId: null,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<BillingV2ProviderEventRecord>
        ReadOrCreateProviderEventAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            BillingV2ProviderInboundEventRequest request,
            CancellationToken cancellationToken)
    {
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO billing_v2_provider_events (
                    id,
                    provider,
                    environment,
                    provider_event_id,
                    event_type,
                    provider_checkout_id,
                    provider_subscription_id,
                    payload_text,
                    status,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @provider,
                    @environment,
                    @provider_event_id,
                    @event_type,
                    @provider_checkout_id,
                    @provider_subscription_id,
                    @payload_text,
                    'processing',
                    UTC_TIMESTAMP(6),
                    UTC_TIMESTAMP(6)
                )
                ON DUPLICATE KEY UPDATE
                    event_type = IF(
                        status = 'processed',
                        event_type,
                        VALUES(event_type)),
                    provider_checkout_id = IF(
                        status = 'processed',
                        provider_checkout_id,
                        VALUES(provider_checkout_id)),
                    provider_subscription_id = IF(
                        status = 'processed',
                        provider_subscription_id,
                        VALUES(provider_subscription_id)),
                    payload_text = IF(
                        status = 'processed',
                        payload_text,
                        VALUES(payload_text)),
                    status = IF(
                        status = 'processed',
                        status,
                        'processing'),
                    reason_code = IF(
                        status = 'processed',
                        reason_code,
                        NULL),
                    last_error = IF(
                        status = 'processed',
                        last_error,
                        NULL),
                    updated_at = IF(
                        status = 'processed',
                        updated_at,
                        UTC_TIMESTAMP(6));
                """;
            insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            AddCommonEventParameters(insert, request);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText =
            """
            SELECT status, reason_code, subscription_id, checkout_session_id
            FROM billing_v2_provider_events
            WHERE provider = @provider
              AND environment = @environment
              AND provider_event_id = @provider_event_id
            FOR UPDATE;
            """;
        select.Parameters.AddWithValue("@provider", request.Provider);
        select.Parameters.AddWithValue("@environment", request.Environment);
        select.Parameters.AddWithValue(
            "@provider_event_id",
            request.ProviderEventId);
        await using var reader = await select.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Billing V2 provider event insert was not readable.");
        }

        return new BillingV2ProviderEventRecord(
            reader.GetString("status"),
            reader.IsDBNull(reader.GetOrdinal("reason_code"))
                ? null
                : reader.GetString("reason_code"),
            MariaDbIdentifierReader.ReadNullable(reader, "subscription_id"),
            MariaDbIdentifierReader.ReadNullable(
                reader,
                "checkout_session_id"));
    }

    private static async Task<IReadOnlyList<BillingV2ProviderLocalState>>
        ResolveLocalStateAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            BillingV2ProviderInboundEventRequest request,
            CancellationToken cancellationToken)
    {
        var states = new List<BillingV2ProviderLocalState>();
        await using var sessionCommand = connection.CreateCommand();
        sessionCommand.Transaction = transaction;
        sessionCommand.CommandText =
            """
            SELECT
                session.id AS checkout_session_id,
                session.subscription_id,
                session.provider,
                session.environment,
                session.provider_checkout_id,
                session.provider_subscription_id,
                session.status AS checkout_status,
                agreement.status AS agreement_status,
                subscription.status AS subscription_status
            FROM billing_v2_provider_checkout_sessions session
            INNER JOIN billing_v2_subscriptions subscription
                ON subscription.id = session.subscription_id
            LEFT JOIN billing_v2_payment_agreements agreement
                ON agreement.subscription_id = session.subscription_id
               AND agreement.provider = session.provider
               AND agreement.environment = session.environment
            WHERE session.provider = @provider
              AND session.environment = @environment
              AND (
                    @expected_customer_id IS NULL
                 OR subscription.customer_id = @expected_customer_id
              )
              AND (
                    @local_subscription_id IS NULL
                 OR subscription.id = @local_subscription_id
              )
              AND (
                    (@provider_checkout_id IS NOT NULL
                     AND session.provider_checkout_id = @provider_checkout_id)
                 OR (@provider_subscription_id IS NOT NULL
                     AND session.provider_subscription_id = @provider_subscription_id)
              )
            LIMIT 2;
            """;
        sessionCommand.Parameters.AddWithValue("@provider", request.Provider);
        sessionCommand.Parameters.AddWithValue(
            "@environment",
            request.Environment);
        sessionCommand.Parameters.AddWithValue(
            "@provider_checkout_id",
            DbNullable(request.ProviderCheckoutId));
        sessionCommand.Parameters.AddWithValue(
            "@provider_subscription_id",
            DbNullable(request.ProviderSubscriptionId));
        sessionCommand.Parameters.AddWithValue(
            "@expected_customer_id",
            DbNullable(request.ExpectedCustomerId));
        sessionCommand.Parameters.AddWithValue(
            "@local_subscription_id",
            DbNullable(request.LocalSubscriptionId));
        await using (var reader = await sessionCommand.ExecuteReaderAsync(
                         cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                states.Add(ReadLocalState(reader));
            }
        }

        if (states.Count > 0
            || string.IsNullOrWhiteSpace(request.ProviderSubscriptionId))
        {
            return states;
        }

        await using var agreementCommand = connection.CreateCommand();
        agreementCommand.Transaction = transaction;
        agreementCommand.CommandText =
            """
            SELECT
                NULL AS checkout_session_id,
                agreement.subscription_id,
                agreement.provider,
                agreement.environment,
                NULL AS provider_checkout_id,
                agreement.provider_subscription_id,
                'no_checkout_session' AS checkout_status,
                agreement.status AS agreement_status,
                subscription.status AS subscription_status
            FROM billing_v2_payment_agreements agreement
            INNER JOIN billing_v2_subscriptions subscription
                ON subscription.id = agreement.subscription_id
            WHERE agreement.provider = @provider
              AND agreement.environment = @environment
              AND (
                    @expected_customer_id IS NULL
                 OR subscription.customer_id = @expected_customer_id
              )
              AND (
                    @local_subscription_id IS NULL
                 OR subscription.id = @local_subscription_id
              )
              AND agreement.provider_subscription_id = @provider_subscription_id
            LIMIT 2;
            """;
        agreementCommand.Parameters.AddWithValue("@provider", request.Provider);
        agreementCommand.Parameters.AddWithValue(
            "@environment",
            request.Environment);
        agreementCommand.Parameters.AddWithValue(
            "@provider_subscription_id",
            request.ProviderSubscriptionId);
        agreementCommand.Parameters.AddWithValue(
            "@expected_customer_id",
            DbNullable(request.ExpectedCustomerId));
        agreementCommand.Parameters.AddWithValue(
            "@local_subscription_id",
            DbNullable(request.LocalSubscriptionId));
        // Le lecteur est referme AVANT la requete suivante : MySqlConnector
        // n'autorise qu'un lecteur ouvert par connexion, et un `await using`
        // porte jusqu'a la fin de methode gelait le chemin renouvellement
        // avec "This MySqlConnection is already in use".
        await using (var agreementReader =
            await agreementCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await agreementReader.ReadAsync(cancellationToken))
            {
                states.Add(ReadLocalState(agreementReader));
            }
        }

        if (states.Count > 0
            || string.IsNullOrWhiteSpace(request.LocalSubscriptionId))
        {
            return states;
        }

        await using var subscriptionCommand = connection.CreateCommand();
        subscriptionCommand.Transaction = transaction;
        subscriptionCommand.CommandText =
            """
            SELECT
                NULL AS checkout_session_id,
                subscription.id AS subscription_id,
                @provider AS provider,
                @environment AS environment,
                NULL AS provider_checkout_id,
                @provider_subscription_id AS provider_subscription_id,
                'no_checkout_session' AS checkout_status,
                NULL AS agreement_status,
                subscription.status AS subscription_status
            FROM billing_v2_subscriptions subscription
            WHERE subscription.id = @local_subscription_id
              AND (
                    @expected_customer_id IS NULL
                 OR subscription.customer_id = @expected_customer_id
              )
            LIMIT 2;
            """;
        subscriptionCommand.Parameters.AddWithValue("@provider", request.Provider);
        subscriptionCommand.Parameters.AddWithValue(
            "@environment",
            request.Environment);
        subscriptionCommand.Parameters.AddWithValue(
            "@provider_subscription_id",
            DbNullable(request.ProviderSubscriptionId));
        subscriptionCommand.Parameters.AddWithValue(
            "@local_subscription_id",
            request.LocalSubscriptionId);
        subscriptionCommand.Parameters.AddWithValue(
            "@expected_customer_id",
            DbNullable(request.ExpectedCustomerId));
        await using var subscriptionReader =
            await subscriptionCommand.ExecuteReaderAsync(cancellationToken);
        while (await subscriptionReader.ReadAsync(cancellationToken))
        {
            states.Add(ReadLocalState(subscriptionReader));
        }

        return states;
    }

    private static BillingV2ProviderLocalState ReadLocalState(
        MySqlDataReader reader)
        => new(
            MariaDbIdentifierReader.ReadNullable(
                reader,
                "checkout_session_id"),
            MariaDbIdentifierReader.ReadRequired(reader, "subscription_id"),
            reader.GetString("provider"),
            reader.GetString("environment"),
            reader.IsDBNull(reader.GetOrdinal("provider_checkout_id"))
                ? null
                : reader.GetString("provider_checkout_id"),
            reader.IsDBNull(reader.GetOrdinal("provider_subscription_id"))
                ? null
                : reader.GetString("provider_subscription_id"),
            reader.GetString("checkout_status"),
            reader.IsDBNull(reader.GetOrdinal("agreement_status"))
                ? null
                : reader.GetString("agreement_status"),
            reader.GetString("subscription_status"));

    private static async Task ApplyPlanAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2ProviderInboundEventRequest request,
        BillingV2ProviderLocalState state,
        BillingV2ProviderInboundEventPlan plan,
        CancellationToken cancellationToken)
    {
        if (state.CheckoutSessionId is not null && plan.CheckoutStatus is not null)
        {
            await using var checkoutCommand = connection.CreateCommand();
            checkoutCommand.Transaction = transaction;
            checkoutCommand.CommandText =
                """
                UPDATE billing_v2_provider_checkout_sessions
                SET status = @status,
                    provider_subscription_id = COALESCE(
                        provider_subscription_id,
                        @provider_subscription_id),
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id;
                """;
            checkoutCommand.Parameters.AddWithValue(
                "@id",
                state.CheckoutSessionId);
            checkoutCommand.Parameters.AddWithValue("@status", plan.CheckoutStatus);
            checkoutCommand.Parameters.AddWithValue(
                "@provider_subscription_id",
                DbNullable(plan.ProviderSubscriptionId));
            await checkoutCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (plan.AgreementStatus is not null
            && !string.IsNullOrWhiteSpace(plan.ProviderSubscriptionId))
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
                    @status,
                    UTC_TIMESTAMP(6),
                    UTC_TIMESTAMP(6)
                )
                ON DUPLICATE KEY UPDATE
                    status = @status,
                    provider_subscription_id = COALESCE(
                        provider_subscription_id,
                        @provider_subscription_id),
                    updated_at = UTC_TIMESTAMP(6);
                """;
            agreementCommand.Parameters.AddWithValue(
                "@id",
                Guid.NewGuid().ToString("D"));
            agreementCommand.Parameters.AddWithValue(
                "@subscription_id",
                state.SubscriptionId);
            agreementCommand.Parameters.AddWithValue("@provider", state.Provider);
            agreementCommand.Parameters.AddWithValue(
                "@environment",
                state.Environment);
            agreementCommand.Parameters.AddWithValue(
                "@provider_subscription_id",
                plan.ProviderSubscriptionId);
            agreementCommand.Parameters.AddWithValue(
                "@status",
                plan.AgreementStatus);
            await agreementCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (plan.SubscriptionStatus is not null)
        {
            await using var subscriptionCommand = connection.CreateCommand();
            subscriptionCommand.Transaction = transaction;
            subscriptionCommand.CommandText =
                """
                UPDATE billing_v2_subscriptions
                SET status = @status,
                    started_at = CASE
                        WHEN @status = 'active' THEN COALESCE(
                            started_at,
                            UTC_TIMESTAMP(6))
                        ELSE started_at
                    END,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @subscription_id;
                """;
            subscriptionCommand.Parameters.AddWithValue(
                "@subscription_id",
                state.SubscriptionId);
            subscriptionCommand.Parameters.AddWithValue(
                "@status",
                plan.SubscriptionStatus);
            await subscriptionCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task MarkProviderEventAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string providerEventId,
        string provider,
        string environment,
        string status,
        string reason,
        string? lastError,
        string? subscriptionId,
        string? checkoutSessionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_provider_events
            SET status = @status,
                reason_code = @reason_code,
                last_error = @last_error,
                subscription_id = @subscription_id,
                checkout_session_id = @checkout_session_id,
                processed_at = CASE
                    WHEN @status IN ('processed', 'skipped')
                        THEN UTC_TIMESTAMP(6)
                    ELSE processed_at
                END,
                updated_at = UTC_TIMESTAMP(6)
            WHERE provider = @provider
              AND environment = @environment
              AND provider_event_id = @provider_event_id;
            """;
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@environment", environment);
        command.Parameters.AddWithValue(
            "@provider_event_id",
            providerEventId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@reason_code", reason);
        command.Parameters.AddWithValue(
            "@last_error",
            lastError is null ? DBNull.Value : lastError);
        command.Parameters.AddWithValue(
            "@subscription_id",
            DbNullable(subscriptionId));
        command.Parameters.AddWithValue(
            "@checkout_session_id",
            DbNullable(checkoutSessionId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2ProviderInboundEventRequest request,
        BillingV2ProviderLocalState state,
        BillingV2ProviderInboundEventPlan plan,
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
                'billing_v2_subscription',
                @entity_id,
                'provider_inbound_event_processed',
                'provider:webhook',
                @details_text,
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@entity_id", state.SubscriptionId);
        command.Parameters.AddWithValue(
            "@details_text",
            JsonSerializer.Serialize(new
            {
                request.Provider,
                request.Environment,
                request.ProviderEventId,
                request.EventType,
                request.ProviderCheckoutId,
                ProviderSubscriptionId = plan.ProviderSubscriptionId,
                plan.ReasonCode,
                plan.AlreadyApplied
            }));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddCommonEventParameters(
        MySqlCommand command,
        BillingV2ProviderInboundEventRequest request)
    {
        command.Parameters.AddWithValue("@provider", request.Provider);
        command.Parameters.AddWithValue("@environment", request.Environment);
        command.Parameters.AddWithValue(
            "@provider_event_id",
            request.ProviderEventId);
        command.Parameters.AddWithValue("@event_type", request.EventType);
        command.Parameters.AddWithValue(
            "@provider_checkout_id",
            DbNullable(request.ProviderCheckoutId));
        command.Parameters.AddWithValue(
            "@provider_subscription_id",
            DbNullable(request.ProviderSubscriptionId));
        command.Parameters.AddWithValue(
            "@payload_text",
            DbNullable(request.PayloadText));
    }

    private static object DbNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private async Task TryTriggerProvisioningAsync(
        string? subscriptionId,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return;
        }

        try
        {
            var result = await _provisioning.TryReconcileActivatedSubscriptionAsync(
                subscriptionId,
                cancellationToken);
            if (result is not null)
            {
                _logger.LogInformation(
                    "Billing V2 provisioning attempted after provider inbound event for subscription {SubscriptionId}: {ResultCode}.",
                    subscriptionId,
                    result.ResultCode);
            }
        }
        catch (Exception exception) when (
            BillingV2ProviderInboundProvisioningFailurePolicy
                .ShouldKeepProviderEventProcessed(exception))
        {
            _logger.LogWarning(
                exception,
                "Billing V2 provisioning trigger failed after provider inbound event for subscription {SubscriptionId} ({ReasonCode}). Provider event remains processed and provisioning can be retried idempotently.",
                subscriptionId,
                reasonCode);
        }
    }

    private async Task TryIssueRenewalDocumentAsync(
        string subscriptionId,
        string billingEventId,
        int cycleSequence,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _documents.EnsureCycleInvoiceAsync(
                subscriptionId,
                billingEventId,
                cycleSequence,
                $"billing-v2-renewal-{subscriptionId}-{cycleSequence}",
                cancellationToken);
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Billing V2 renewal document for subscription {SubscriptionId} cycle {Cycle} returned {ReasonCode}. It can be retried idempotently.",
                    subscriptionId,
                    cycleSequence,
                    result.ReasonCode);
            }
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Billing V2 renewal document issuing failed for subscription {SubscriptionId} cycle {Cycle}. It can be retried idempotently.",
                subscriptionId,
                cycleSequence);
        }
    }

    private async Task TryIssueDocumentAsync(
        string? subscriptionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return;
        }

        try
        {
            var result = await _documents.EnsureInitialInvoiceAsync(
                subscriptionId,
                $"billing-v2-document-{subscriptionId}",
                cancellationToken);
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Billing V2 document issuing after provider activation for subscription {SubscriptionId} returned {ReasonCode}. It can be retried idempotently.",
                    subscriptionId,
                    result.ReasonCode);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Billing V2 document issuing failed after provider activation for subscription {SubscriptionId}. Provider event remains processed and document issuing can be retried idempotently.",
                subscriptionId);
        }
    }

    private sealed record BillingV2ProviderEventRecord(
        string Status,
        string? ReasonCode,
        string? SubscriptionId,
        string? CheckoutSessionId);
}

public static class BillingV2ProviderInboundProvisioningFailurePolicy
{
    public static bool ShouldKeepProviderEventProcessed(Exception exception)
        => exception is not OperationCanceledException;
}

/// <summary>
/// Aiguillage des signaux de cycle (Phase 3).
///
/// Aucun de ces signaux ne porte de decision : ils designent seulement l'objet
/// Stripe a relire. La separation entre "signal de cycle" et "signal de sante"
/// est ce qui garantit qu'un <c>customer.subscription.updated</c> ne puisse
/// jamais servir de preuve de paiement.
/// </summary>
public static class BillingV2ProviderInboundRenewalPolicy
{
    public const string SubscriptionSignalOnlyReasonCode =
        "BILLING_V2_PROVIDER_SUBSCRIPTION_SIGNAL_ONLY";

    public static bool IsRenewalSignal(string provider, string? reasonCode)
        => IsStripe(provider)
           && string.Equals(
               reasonCode,
               BillingV2ProviderInboundEventPlanner.RenewalSignalReasonCode,
               StringComparison.Ordinal);

    public static bool IsSubscriptionHealthSignal(
        string provider,
        string? reasonCode)
        => IsStripe(provider)
           && string.Equals(
               reasonCode,
               SubscriptionSignalOnlyReasonCode,
               StringComparison.Ordinal);

    private static bool IsStripe(string provider)
        => string.Equals(provider, "stripe", StringComparison.OrdinalIgnoreCase);
}

public static class BillingV2ProviderInboundProvisioningPolicy
{
    private const string SubscriptionActivatedReasonCode =
        "BILLING_V2_PROVIDER_SUBSCRIPTION_ACTIVATED";

    public static bool ShouldAttempt(
        BillingV2ProviderInboundEventPlan plan,
        BillingV2ProviderLocalState state)
        => plan.CanApply
            && string.Equals(
                plan.SubscriptionStatus,
                "active",
                StringComparison.Ordinal);

    public static bool ShouldAttemptProcessedReplay(string? reasonCode)
        => string.Equals(
            reasonCode,
            SubscriptionActivatedReasonCode,
            StringComparison.Ordinal);

    public const string CheckoutCompletedSignalReasonCode =
        "BILLING_V2_PROVIDER_CHECKOUT_COMPLETED_SIGNAL";

    /// <summary>
    /// Signaux Stripe qui justifient une RELECTURE de l'objet chez Stripe.
    ///
    /// Ils n'autorisent rien par eux-memes : ils declenchent la verification,
    /// qui seule peut conclure a un encaissement. Un signal inerte de Phase 1
    /// (`customer.subscription.created` / `updated`) reste volontairement hors
    /// de cette liste.
    /// </summary>
    public static bool ShouldVerifySettlement(
        string provider,
        string? reasonCode)
        => string.Equals(provider, "stripe", StringComparison.OrdinalIgnoreCase)
           && (string.Equals(
                   reasonCode,
                   CheckoutCompletedSignalReasonCode,
                   StringComparison.Ordinal)
               || string.Equals(
                   reasonCode,
                   SubscriptionActivatedReasonCode,
                   StringComparison.Ordinal)
               || string.Equals(
                   reasonCode,
                   "BILLING_V2_PROVIDER_CHECKOUT_RETURN_RECORDED",
                   StringComparison.Ordinal));
}

public static class BillingV2ProviderInboundEventPlanner
{
    /// <summary>
    /// Signal de cycle : declenche une relecture, n'autorise rien.
    /// </summary>
    public const string RenewalSignalReasonCode =
        "BILLING_V2_PROVIDER_RENEWAL_SIGNAL";

    public static BillingV2ProviderInboundEventPlan Plan(
        BillingV2ProviderInboundEventRequest request,
        BillingV2ProviderLocalState state)
    {
        if (!string.Equals(
                request.Provider,
                state.Provider,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                request.Environment,
                state.Environment,
                StringComparison.OrdinalIgnoreCase))
        {
            return Blocked("BILLING_V2_PROVIDER_EVENT_CONTEXT_MISMATCH");
        }

        if (BothPresentAndDifferent(
                request.ProviderCheckoutId,
                state.ProviderCheckoutId))
        {
            return Blocked("BILLING_V2_PROVIDER_CHECKOUT_ID_CONFLICT");
        }

        if (BothPresentAndDifferent(
                request.ProviderSubscriptionId,
                state.ProviderSubscriptionId))
        {
            return Blocked("BILLING_V2_PROVIDER_SUBSCRIPTION_ID_CONFLICT");
        }

        var providerSubscriptionId = FirstNonBlank(
            request.ProviderSubscriptionId,
            state.ProviderSubscriptionId);
        var eventKind = ResolveEventKind(request.EventType, state);
        if (eventKind is null)
        {
            return Blocked("BILLING_V2_PROVIDER_EVENT_UNSUPPORTED");
        }

        if (eventKind.RequiresProviderSubscription
            && string.IsNullOrWhiteSpace(providerSubscriptionId))
        {
            return Blocked("BILLING_V2_PROVIDER_SUBSCRIPTION_ID_MISSING");
        }

        var checkoutStatus = MaxStatus(
            state.CheckoutStatus,
            eventKind.CheckoutStatus,
            CheckoutStatusRank);
        var agreementStatus = eventKind.AgreementStatus is null
            ? null
            : MaxStatus(
                state.AgreementStatus,
                eventKind.AgreementStatus,
                AgreementStatusRank);
        var subscriptionStatus = eventKind.SubscriptionStatus is null
            ? null
            : MaxStatus(
                state.SubscriptionStatus,
                eventKind.SubscriptionStatus,
                SubscriptionStatusRank);

        var alreadyApplied =
            string.Equals(
                state.ProviderSubscriptionId,
                providerSubscriptionId,
                StringComparison.Ordinal)
            && (checkoutStatus is null
                || string.Equals(
                    state.CheckoutStatus,
                    checkoutStatus,
                    StringComparison.Ordinal))
            && (agreementStatus is null
                || string.Equals(
                    state.AgreementStatus,
                    agreementStatus,
                    StringComparison.Ordinal))
            && (subscriptionStatus is null
                || string.Equals(
                    state.SubscriptionStatus,
                    subscriptionStatus,
                    StringComparison.Ordinal));

        return new BillingV2ProviderInboundEventPlan(
            CanApply: true,
            alreadyApplied,
            eventKind.ReasonCode,
            checkoutStatus,
            agreementStatus,
            subscriptionStatus,
            providerSubscriptionId);
    }

    /// <param name="state">
    /// L'etat local participe a la lecture d'un evenement : le meme
    /// `billing.subscription.suspended` ne veut pas dire la meme chose selon
    /// qu'une resiliation est en cours ou non. Voir
    /// <see cref="IsExpectedSuspension"/>.
    /// </param>
    private static BillingV2ProviderEventKind? ResolveEventKind(
        string eventType,
        BillingV2ProviderLocalState state)
    {
        var normalized = eventType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "billing_v2.checkout_returned"
                or "checkout.returned" => new BillingV2ProviderEventKind(
                    "BILLING_V2_PROVIDER_CHECKOUT_RETURN_RECORDED",
                    CheckoutStatus: "approved",
                    AgreementStatus: "pending",
                    SubscriptionStatus: null,
                    RequiresProviderSubscription: false),
            "billing_v2.subscription_activated"
                or "billing.subscription.activated" =>
                new BillingV2ProviderEventKind(
                    "BILLING_V2_PROVIDER_SUBSCRIPTION_ACTIVATED",
                    CheckoutStatus: "completed",
                    AgreementStatus: "active",
                    SubscriptionStatus: "active",
                    RequiresProviderSubscription: true),
            // Phase 2 : `checkout.session.completed` n'active plus rien par
            // lui-meme. La session peut etre "complete" alors que le paiement
            // n'est pas encaisse (3DS en attente, `payment_status=unpaid`).
            // L'evenement marque donc seulement la session comme terminee et
            // sert de DECLENCHEUR a une relecture Stripe ; c'est cette
            // relecture qui pourra, elle, conclure a un encaissement.
            "checkout.session.completed" =>
                new BillingV2ProviderEventKind(
                    "BILLING_V2_PROVIDER_CHECKOUT_COMPLETED_SIGNAL",
                    CheckoutStatus: "completed",
                    AgreementStatus: null,
                    SubscriptionStatus: null,
                    RequiresProviderSubscription: false),
            // Signal inerte (Phase 1, safety fix).
            //
            // `customer.subscription.created` et `customer.subscription.updated`
            // ne prouvent RIEN sur le paiement : Stripe cree l'objet en statut
            // `incomplete` tant qu'une authentification 3DS est en attente, et
            // emet `updated` a chaque changement, y compris vers `past_due`.
            //
            // Ces deux evenements sont donc enregistres et rattaches, mais ne
            // portent aucun statut : ils ne peuvent provoquer ni activation V2,
            // ni emission documentaire, ni passage a `paid`, ni provisioning.
            // Le systeme reste fail-closed en attendant la verification de
            // settlement de la Phase 2.
            "customer.subscription.created"
                or "customer.subscription.updated" =>
                new BillingV2ProviderEventKind(
                    "BILLING_V2_PROVIDER_SUBSCRIPTION_SIGNAL_ONLY",
                    CheckoutStatus: null,
                    AgreementStatus: null,
                    SubscriptionStatus: null,
                    RequiresProviderSubscription: true),
            // Phase 3. Les evenements d'invoice Stripe ne portent AUCUNE
            // transition : ils disent seulement quel cycle relire. La preuve
            // financiere vient de la relecture de l'invoice chez Stripe, avec
            // verification du montant et de la devise attendus.
            "invoice.paid"
                or "invoice.payment_succeeded"
                or "invoice.payment_failed"
                or "invoice.marked_uncollectible" =>
                new BillingV2ProviderEventKind(
                    RenewalSignalReasonCode,
                    CheckoutStatus: null,
                    AgreementStatus: null,
                    SubscriptionStatus: null,
                    RequiresProviderSubscription: false),
            // Suspension ATTENDUE : c'est notre propre `/suspend` PayPal qui
            // nous revient. Une resiliation a fin de terme suspend l'abonnement
            // pour qu'aucun renouvellement ne parte, puis le resiliera au terme.
            // Le lire comme un impaye ecraserait `pending_cancellation` en
            // `past_due` et afficherait au client un incident de paiement que
            // nous avons nous-memes provoque.
            //
            // L'evenement reste enregistre et rattache : il n'est pas ignore,
            // il est seulement prive de transition.
            "billing.subscription.suspended"
                when IsExpectedSuspension(state) =>
                new BillingV2ProviderEventKind(
                    "BILLING_V2_PROVIDER_SUBSCRIPTION_SUSPENSION_EXPECTED",
                    CheckoutStatus: null,
                    AgreementStatus: null,
                    SubscriptionStatus: null,
                    RequiresProviderSubscription: true),
            // Suspension INATTENDUE, ou echec de paiement declare : incident
            // reel, il doit rester visible en `past_due`.
            "billing_v2.subscription_payment_failed"
                or "billing.subscription.suspended" =>
                new BillingV2ProviderEventKind(
                    "BILLING_V2_PROVIDER_SUBSCRIPTION_PAYMENT_FAILED",
                    CheckoutStatus: null,
                    AgreementStatus: "past_due",
                    SubscriptionStatus: "past_due",
                    RequiresProviderSubscription: true),
            "billing_v2.subscription_cancelled"
                or "customer.subscription.deleted"
                or "billing.subscription.cancelled" =>
                new BillingV2ProviderEventKind(
                    "BILLING_V2_PROVIDER_SUBSCRIPTION_CANCELLED",
                    CheckoutStatus: null,
                    AgreementStatus: "cancelled",
                    SubscriptionStatus: "cancelled",
                    RequiresProviderSubscription: true),
            _ => null
        };
    }

    /// <summary>
    /// La suspension observee est-elle celle que nous avons demandee ?
    /// </summary>
    /// <remarks>
    /// <c>pending_cancellation</c> n'est jamais ecrit par un webhook : seul
    /// <see cref="BillingV2SubscriptionCancellationService"/> le pose, en meme
    /// temps qu'il met le <c>/suspend</c> en file. C'est donc un marqueur
    /// d'intention fiable, et le seul signal disponible ici pour distinguer
    /// notre propre geste d'un vrai incident de paiement.
    /// </remarks>
    private static bool IsExpectedSuspension(
        BillingV2ProviderLocalState state)
        => string.Equals(
            state.SubscriptionStatus,
            "pending_cancellation",
            StringComparison.Ordinal);

    private static BillingV2ProviderInboundEventPlan Blocked(string reason)
        => new(
            CanApply: false,
            AlreadyApplied: false,
            reason,
            CheckoutStatus: null,
            AgreementStatus: null,
            SubscriptionStatus: null,
            ProviderSubscriptionId: null);

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool BothPresentAndDifferent(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
           && !string.IsNullOrWhiteSpace(right)
           && !string.Equals(left, right, StringComparison.Ordinal);

    private static string? MaxStatus(
        string? current,
        string? target,
        IReadOnlyDictionary<string, int> rank)
    {
        if (target is null)
        {
            return null;
        }

        if (current is null)
        {
            return target;
        }

        return rank.TryGetValue(current, out var currentRank)
            && rank.TryGetValue(target, out var targetRank)
            && currentRank > targetRank
            ? current
            : target;
    }

    private static readonly IReadOnlyDictionary<string, int> CheckoutStatusRank =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["pending_approval"] = 0,
            ["approved"] = 1,
            ["completed"] = 2,
            ["failed"] = 3
        };

    private static readonly IReadOnlyDictionary<string, int> AgreementStatusRank =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["pending"] = 0,
            ["active"] = 1,
            ["past_due"] = 2,
            ["cancelled"] = 3,
            ["failed"] = 3
        };

    private static readonly IReadOnlyDictionary<string, int> SubscriptionStatusRank =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["draft"] = 0,
            ["pending"] = 1,
            ["active"] = 2,
            ["past_due"] = 3,
            // Au meme rang que `past_due`, et au-dessus de `active`. Un
            // evenement d'activation ne peut donc pas ressusciter un abonnement
            // en cours de resiliation, tandis qu'un impaye REEL garde le droit
            // de s'afficher : a rang egal, la cible l'emporte.
            ["pending_cancellation"] = 3,
            ["cancelled"] = 4
        };

    private sealed record BillingV2ProviderEventKind(
        string ReasonCode,
        string? CheckoutStatus,
        string? AgreementStatus,
        string? SubscriptionStatus,
        bool RequiresProviderSubscription);
}

public static class BillingV2ProviderInboundEventExtractor
{
    public static BillingV2ProviderInboundEventRequest CreateProviderReturn(
        string provider,
        string environment,
        string? providerCheckoutId,
        string? providerSubscriptionId,
        string? payloadText,
        string? expectedCustomerId)
    {
        var normalizedProvider = NormalizeProvider(provider);
        var normalizedEnvironment = NormalizeEnvironment(environment);
        var checkoutId = Normalize(providerCheckoutId);
        var subscriptionId = Normalize(providerSubscriptionId);
        if (checkoutId is null && subscriptionId is null)
        {
            throw new InvalidOperationException(
                "BILLING_V2_PROVIDER_RETURN_ID_MISSING");
        }

        return new BillingV2ProviderInboundEventRequest(
            normalizedProvider,
            normalizedEnvironment,
            $"return:{normalizedProvider}:{normalizedEnvironment}:{checkoutId ?? subscriptionId}",
            "billing_v2.checkout_returned",
            checkoutId,
            subscriptionId,
            payloadText,
            Normalize(expectedCustomerId),
            LocalSubscriptionId: null);
    }

    public static BillingV2ProviderInboundEventRequest?
        TryCreateStripeWebhook(
            StripeWebhookEventPayload payload,
            string environment)
    {
        var eventId = Normalize(payload.EventId);
        var eventType = Normalize(payload.EventType);
        if (eventId is null || eventType is null)
        {
            return null;
        }

        using var document = TryParse(payload.RawPayload);
        var root = document?.RootElement;
        var dataObject = TryReadDataObject(root);
        var metadataSubscriptionId = TryReadStripeSubscriptionMetadata(
            dataObject,
            "billing_v2_subscription_id");
        var providerCheckoutId = IsStripeCheckoutSessionEvent(eventType)
            ? Normalize(payload.ResourceId) ?? TryReadString(dataObject, "id")
            : null;
        var providerSubscriptionId =
            TryReadStripeSubscriptionId(dataObject, eventType);
        if (metadataSubscriptionId is null)
        {
            return null;
        }

        return new BillingV2ProviderInboundEventRequest(
            "stripe",
            NormalizeEnvironment(environment),
            eventId,
            eventType,
            providerCheckoutId,
            providerSubscriptionId,
            payload.RawPayload,
            ExpectedCustomerId: null,
            LocalSubscriptionId: metadataSubscriptionId);
    }

    public static BillingV2ProviderInboundEventRequest?
        TryCreatePayPalWebhook(
            PayPalWebhookEventPayload payload,
            string environment)
    {
        var eventId = Normalize(payload.EventId);
        var eventType = Normalize(payload.EventType);
        if (eventId is null || eventType is null)
        {
            return null;
        }

        using var document = TryParse(payload.RawPayload);
        var root = document?.RootElement;
        var resource = root.HasValue
            && root.Value.TryGetProperty("resource", out var resourceElement)
                ? resourceElement
                : default;
        var customId = resource.ValueKind == JsonValueKind.Object
            ? TryReadString(resource, "custom_id")
            : null;
        var providerSubscriptionId =
            Normalize(payload.ResourceId)
            ?? (resource.ValueKind == JsonValueKind.Object
                ? TryReadString(resource, "id")
                    ?? TryReadString(resource, "billing_agreement_id")
                : null);
        if (customId is null)
        {
            return null;
        }

        return new BillingV2ProviderInboundEventRequest(
            "paypal",
            NormalizeEnvironment(environment),
            eventId,
            eventType,
            null,
            providerSubscriptionId,
            payload.RawPayload,
            ExpectedCustomerId: null,
            LocalSubscriptionId: customId);
    }

    private static string NormalizeProvider(string provider)
    {
        var normalized = provider.Trim().ToLowerInvariant();
        return normalized is "stripe" or "paypal"
            ? normalized
            : throw new InvalidOperationException(
                "BILLING_V2_PROVIDER_UNSUPPORTED");
    }

    private static string NormalizeEnvironment(string environment)
        => string.IsNullOrWhiteSpace(environment)
            ? "disabled"
            : environment.Trim().ToLowerInvariant();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static JsonDocument? TryParse(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(rawPayload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? TryReadDataObject(JsonElement? root)
        => root.HasValue
        && root.Value.TryGetProperty("data", out var data)
        && data.TryGetProperty("object", out var dataObject)
            ? dataObject
            : null;

    private static string? TryReadString(JsonElement? element, string property)
        => element.HasValue
        && element.Value.ValueKind == JsonValueKind.Object
        && element.Value.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? Normalize(value.GetString())
            : null;

    private static string? TryReadMetadata(
        JsonElement? dataObject,
        string metadataKey)
        => dataObject.HasValue
        && dataObject.Value.TryGetProperty("metadata", out var metadata)
        && metadata.ValueKind == JsonValueKind.Object
        && metadata.TryGetProperty(metadataKey, out var value)
        && value.ValueKind == JsonValueKind.String
            ? Normalize(value.GetString())
            : null;

    private static JsonElement? TryReadObject(
        JsonElement? element,
        string property)
        => element.HasValue
        && element.Value.ValueKind == JsonValueKind.Object
        && element.Value.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    /// <summary>
    /// Lit une metadata d'abonnement Billing V2 sur un objet Stripe.
    /// Une facture ne porte pas la metadata de son abonnement : Stripe
    /// l'expose sous `parent.subscription_details.metadata` (et sous
    /// `subscription_details.metadata` sur les versions d'API anterieures).
    /// Sans ce repli, `invoice.paid` retombe sur le chemin legacy et aucun
    /// renouvellement n'est facture.
    /// </summary>
    private static string? TryReadStripeSubscriptionMetadata(
        JsonElement? dataObject,
        string metadataKey)
        => TryReadMetadata(dataObject, metadataKey)
        ?? TryReadMetadata(
            TryReadObject(
                TryReadObject(dataObject, "parent"),
                "subscription_details"),
            metadataKey)
        ?? TryReadMetadata(
            TryReadObject(dataObject, "subscription_details"),
            metadataKey)
        ?? TryReadStripeLineMetadata(dataObject, metadataKey);

    private static string? TryReadStripeLineMetadata(
        JsonElement? dataObject,
        string metadataKey)
    {
        var lines = TryReadObject(dataObject, "lines");
        if (lines is null
            || !lines.Value.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var line in data.EnumerateArray())
        {
            var found = TryReadMetadata(line, metadataKey)
                ?? TryReadMetadata(
                    TryReadObject(line, "subscription_details"),
                    metadataKey);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsStripeCheckoutSessionEvent(string eventType)
        => string.Equals(
            eventType,
            "checkout.session.completed",
            StringComparison.OrdinalIgnoreCase);

    private static string? TryReadStripeSubscriptionId(
        JsonElement? dataObject,
        string eventType)
    {
        if (dataObject is null)
        {
            return null;
        }

        if (eventType.StartsWith(
                "customer.subscription.",
                StringComparison.OrdinalIgnoreCase))
        {
            return TryReadString(dataObject, "id");
        }

        var direct = TryReadString(dataObject, "subscription");
        if (direct is not null)
        {
            return direct;
        }

        if (dataObject.Value.TryGetProperty("parent", out var parent)
            && parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(
                "subscription_details",
                out var subscriptionDetails)
            && subscriptionDetails.ValueKind == JsonValueKind.Object
            && subscriptionDetails.TryGetProperty(
                "subscription",
                out var nestedSubscription)
            && nestedSubscription.ValueKind == JsonValueKind.String)
        {
            return Normalize(nestedSubscription.GetString());
        }

        return null;
    }
}
