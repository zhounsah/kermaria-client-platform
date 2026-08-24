using System.Data;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2RenewalContractSnapshot(
    string SubscriptionId,
    string CustomerId,
    string Status,
    string PaymentMode,
    string Currency,
    int CommitmentMonths,
    int DiscountBasisPoints,
    long? MinimumCommitmentAmountCents,
    DateTime BillingAnchorUtc,
    IReadOnlyList<BillingV2RenewalContractItem> Items);

public sealed record BillingV2RenewalEnsureResult(
    bool Created,
    string ReasonCode,
    string? BillingEventId,
    int CycleSequence,
    long ExpectedAmountCents,
    string? ExpectedCurrency);

public interface IBillingV2RenewalService
{
    /// <summary>
    /// Cree - ou retrouve - le BillingEvent du cycle demande. Idempotent par
    /// (subscription_id, cycle_sequence).
    /// </summary>
    Task<BillingV2RenewalEnsureResult> EnsureRenewalChargeAsync(
        string subscriptionId,
        int cycleSequence,
        CancellationToken cancellationToken);

    /// <summary>
    /// Traite un signal Stripe de cycle : relit l'invoice, en deduit DE QUEL
    /// cycle il s'agit, facture ce cycle si besoin, puis verifie et regle.
    ///
    /// Le signal ne decide de rien : il ne fait que designer l'objet a relire.
    /// </summary>
    Task<BillingV2RenewalSignalResult> HandleProviderSignalAsync(
        string subscriptionId,
        CancellationToken cancellationToken);
}

public sealed record BillingV2RenewalSignalResult(
    bool Settled,
    string ReasonCode,
    int CycleSequence,
    string? BillingEventId);

public sealed class NoOpBillingV2RenewalService : IBillingV2RenewalService
{
    public static NoOpBillingV2RenewalService Instance { get; } = new();

    private NoOpBillingV2RenewalService()
    {
    }

    public Task<BillingV2RenewalEnsureResult> EnsureRenewalChargeAsync(
        string subscriptionId,
        int cycleSequence,
        CancellationToken cancellationToken)
        => Task.FromResult(new BillingV2RenewalEnsureResult(
            false,
            "BILLING_V2_RENEWAL_DISABLED",
            null,
            cycleSequence,
            0,
            null));

    public Task<BillingV2RenewalSignalResult> HandleProviderSignalAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
        => Task.FromResult(new BillingV2RenewalSignalResult(
            false,
            "BILLING_V2_RENEWAL_DISABLED",
            0,
            null));
}

/// <summary>
/// Facturation d'un cycle de renouvellement (Phase 3).
///
/// Identite metier : <c>subscription_id + cycle_sequence</c>. Jamais l'heure
/// courante, jamais la date du webhook. Deux workers qui visent le cycle 17
/// produisent la meme cle et entrent en collision sur
/// <c>uq_billing_v2_billing_events_cycle</c> : le perdant retrouve
/// l'evenement du gagnant au lieu d'en creer un second.
///
/// Le montant est snapshotte depuis le CONTRAT (items et versions de prix
/// verrouilles a la souscription). Une hausse tarifaire posterieure ne peut
/// donc pas repricer un abonnement en cours.
/// </summary>
public sealed class BillingV2RenewalService : IBillingV2RenewalService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly IBillingV2Clock _clock;
    private readonly IBillingV2StripeGateway _gateway;
    private readonly IBillingV2StripeRailService _rail;
    private readonly ILogger<BillingV2RenewalService> _logger;

    public BillingV2RenewalService(
        SqlRuntimeConfiguration sql,
        IBillingV2Clock clock,
        IBillingV2StripeGateway gateway,
        IBillingV2StripeRailService rail,
        ILogger<BillingV2RenewalService> logger)
    {
        _sql = sql;
        _clock = clock;
        _gateway = gateway;
        _rail = rail;
        _logger = logger;
    }

    public async Task<BillingV2RenewalSignalResult> HandleProviderSignalAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return new BillingV2RenewalSignalResult(
                false,
                "BILLING_V2_RENEWAL_NO_PERSISTENT_SQL",
                0,
                null);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var snapshot = await ReadContractSnapshotAsync(
            connection,
            subscriptionId,
            _clock.UtcNow,
            cancellationToken);
        if (snapshot is null)
        {
            return new BillingV2RenewalSignalResult(
                false,
                "BILLING_V2_RENEWAL_SUBSCRIPTION_NOT_FOUND",
                0,
                null);
        }

        var providerSubscriptionId = await ReadProviderSubscriptionIdAsync(
            connection,
            subscriptionId,
            cancellationToken);
        if (providerSubscriptionId is null)
        {
            return new BillingV2RenewalSignalResult(
                false,
                "BILLING_V2_RENEWAL_NO_PROVIDER_SUBSCRIPTION",
                0,
                null);
        }

        // Relecture BORNEE : un objet, cible par un identifiant persiste.
        var invoice = await _gateway.GetLatestInvoiceForSubscriptionAsync(
            providerSubscriptionId,
            cancellationToken);
        var resolution = BillingV2RenewalCycleResolver.Resolve(
            snapshot.BillingAnchorUtc,
            monthsPerCycle: 1,
            invoice?.PeriodStartUtc);
        if (!resolution.Resolved)
        {
            // Cycle indeterminable : on ne facture rien plutot que de deviner.
            return new BillingV2RenewalSignalResult(
                false,
                resolution.ReasonCode,
                resolution.CycleSequence,
                null);
        }

        var ensured = await EnsureRenewalChargeAsync(
            subscriptionId,
            resolution.CycleSequence,
            cancellationToken);
        if (ensured.BillingEventId is null)
        {
            return new BillingV2RenewalSignalResult(
                false,
                ensured.ReasonCode,
                resolution.CycleSequence,
                null);
        }

        // On persiste l'identifiant d'invoice AVANT la verification, pour que
        // le reconciliateur sache quoi relire meme si la suite echoue.
        await BillingV2FinancialCoreStore.LinkAttemptProviderObjectsAsync(
            connection,
            transaction: null,
            await EnsureAttemptIdAsync(
                connection,
                subscriptionId,
                ensured.BillingEventId,
                cancellationToken)
            ?? string.Empty,
            invoice?.InvoiceId,
            providerSubscriptionId,
            _clock.UtcNow,
            cancellationToken);

        var settlement = await _rail.VerifyAndSettleRenewalAsync(
            ensured.BillingEventId,
            cancellationToken);
        return new BillingV2RenewalSignalResult(
            settlement.Settled,
            settlement.ReasonCode,
            resolution.CycleSequence,
            ensured.BillingEventId);
    }

    /// <summary>
    /// La tentative du cycle doit exister AVANT qu'on y attache les
    /// identifiants provider. Sans elle, le lien partait dans le vide et la
    /// verification echouait ensuite en `NO_PROVIDER_OBJECT`, faute d'objet
    /// Stripe a relire : le renouvellement n'etait jamais regle.
    ///
    /// La cle de requete est deterministe et identique a celle que le rail
    /// resout, donc aucune seconde tentative ne peut naitre ici.
    /// </summary>
    private static async Task<string?> EnsureAttemptIdAsync(
        MySqlConnection connection,
        string subscriptionId,
        string billingEventId,
        CancellationToken cancellationToken)
    {
        var existing = await ResolveAttemptIdAsync(
            connection,
            billingEventId,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT IGNORE INTO billing_v2_payment_attempts (
                id,
                billing_event_id,
                provider,
                environment,
                provider_request_key,
                expected_amount_cents,
                expected_currency,
                status
            )
            SELECT
                UUID(),
                event_row.id,
                'stripe',
                COALESCE((
                    SELECT prior.environment
                    FROM billing_v2_payment_attempts prior
                    INNER JOIN billing_v2_billing_events prior_event
                        ON prior_event.id = prior.billing_event_id
                    WHERE prior_event.subscription_id = @subscription_id
                      AND prior.provider = 'stripe'
                    ORDER BY prior.created_at DESC
                    LIMIT 1
                ), 'test'),
                CONCAT('bv2-evt-', event_row.id),
                event_row.total_amount_cents,
                event_row.currency,
                'created'
            FROM billing_v2_billing_events event_row
            WHERE event_row.id = @billing_event_id;
            """;
        command.Parameters.AddWithValue("@billing_event_id", billingEventId);
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await ResolveAttemptIdAsync(
            connection,
            billingEventId,
            cancellationToken);
    }

    private static async Task<string?> ResolveAttemptIdAsync(
        MySqlConnection connection,
        string billingEventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id FROM billing_v2_payment_attempts
            WHERE billing_event_id = @billing_event_id
            ORDER BY created_at ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@billing_event_id", billingEventId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    /// <remarks>
    /// Delegue au resolveur partage : les trois sources autoritaires y sont
    /// lues au meme endroit que pour la resiliation et la mutation recurrente.
    /// Trois requetes divergentes finiraient par ne plus repondre la meme chose
    /// sur le meme contrat.
    /// </remarks>
    private static Task<string?> ReadProviderSubscriptionIdAsync(
        MySqlConnection connection,
        string subscriptionId,
        CancellationToken cancellationToken)
        => BillingV2ProviderAnchorReader.ReadStripeSubscriptionIdAsync(
            connection,
            subscriptionId,
            cancellationToken);

    public async Task<BillingV2RenewalEnsureResult> EnsureRenewalChargeAsync(
        string subscriptionId,
        int cycleSequence,
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return Refused(
                "BILLING_V2_RENEWAL_NO_PERSISTENT_SQL",
                cycleSequence);
        }

        if (cycleSequence <= BillingV2RenewalPolicy.InitialCycleSequence)
        {
            return Refused(
                "BILLING_V2_RENEWAL_CYCLE_IS_INITIAL_CHARGE",
                cycleSequence);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Chemin rapide : le cycle est deja facture. On sort avant toute
        // reconstruction, donc avant toute chance de diverger.
        var existing = await ReadCycleEventAsync(
            connection,
            transaction: null,
            subscriptionId,
            cycleSequence,
            cancellationToken);
        if (existing is not null)
        {
            return new BillingV2RenewalEnsureResult(
                false,
                "BILLING_V2_RENEWAL_ALREADY_BILLED",
                existing.Id,
                cycleSequence,
                existing.TotalAmountCents,
                existing.Currency);
        }

        var snapshot = await ReadContractSnapshotAsync(
            connection,
            subscriptionId,
            _clock.UtcNow,
            cancellationToken);
        if (snapshot is null)
        {
            return Refused(
                "BILLING_V2_RENEWAL_SUBSCRIPTION_NOT_FOUND",
                cycleSequence);
        }

        if (!string.Equals(snapshot.Status, "active", StringComparison.Ordinal))
        {
            return Refused(
                "BILLING_V2_RENEWAL_SUBSCRIPTION_NOT_ACTIVE",
                cycleSequence);
        }

        var cyclePeriod = BillingV2BillingCalendar.ResolveCyclePeriod(
            snapshot.BillingAnchorUtc,
            monthsPerCycle: 1,
            cycleSequence);
        snapshot = await ReadContractSnapshotAsync(
            connection,
            subscriptionId,
            cyclePeriod.StartUtc,
            cancellationToken);
        if (snapshot is null)
        {
            return Refused("BILLING_V2_RENEWAL_SUBSCRIPTION_NOT_FOUND", cycleSequence);
        }

        BillingV2RenewalChargeResult charge;
        try
        {
            charge = BillingV2RenewalChargeFactory.Build(
                new BillingV2RenewalChargeRequest(
                    snapshot.SubscriptionId,
                    cycleSequence,
                    snapshot.PaymentMode,
                    snapshot.CommitmentMonths,
                    snapshot.DiscountBasisPoints,
                    snapshot.Currency,
                    snapshot.MinimumCommitmentAmountCents,
                    snapshot.Items,
                    // La periode vient de l'ancre contractuelle et du rang du
                    // cycle : elle est reproductible a l'identique.
                    cyclePeriod));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 renewal charge refused for subscription {SubscriptionId} cycle {Cycle}.",
                subscriptionId,
                cycleSequence);
            return Refused(exception.Message, cycleSequence);
        }

        var now = _clock.UtcNow;
        var billingEventId = Guid.NewGuid().ToString("D");
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        try
        {
            await BillingV2FinancialCoreStore
                .InsertFinalizedRenewalEventAsync(
                    connection,
                    transaction,
                    billingEventId,
                    snapshot.CustomerId,
                    snapshot.SubscriptionId,
                    cycleSequence,
                    charge.Draft,
                    snapshot.PaymentMode,
                    snapshot.CommitmentMonths,
                    snapshot.DiscountBasisPoints,
                    charge.Period.StartUtc,
                    charge.Period.EndUtc,
                    now,
                    now.AddDays(7),
                    charge.LineSources,
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (MySqlException exception)
            when (exception.Number == 1062)
        {
            // Course perdue : un autre worker a facture ce cycle entre notre
            // lecture et notre ecriture. C'est exactement ce que l'index
            // unique doit produire - pas une seconde facturation.
            await transaction.RollbackAsync(cancellationToken);
            var winner = await ReadCycleEventAsync(
                connection,
                transaction: null,
                subscriptionId,
                cycleSequence,
                cancellationToken);
            return new BillingV2RenewalEnsureResult(
                false,
                "BILLING_V2_RENEWAL_ALREADY_BILLED",
                winner?.Id,
                cycleSequence,
                winner?.TotalAmountCents ?? 0,
                winner?.Currency);
        }

        return new BillingV2RenewalEnsureResult(
            true,
            "BILLING_V2_RENEWAL_BILLING_EVENT_CREATED",
            billingEventId,
            cycleSequence,
            charge.Draft.TotalAmountCents,
            charge.Draft.Currency);
    }

    private static BillingV2RenewalEnsureResult Refused(
        string reasonCode,
        int cycleSequence)
        => new(false, reasonCode, null, cycleSequence, 0, null);

    private sealed record CycleEvent(
        string Id,
        long TotalAmountCents,
        string Currency);

    private static async Task<CycleEvent?> ReadCycleEventAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string subscriptionId,
        int cycleSequence,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, total_amount_cents, currency
            FROM billing_v2_billing_events
            WHERE subscription_id = @subscription_id
              AND event_type = 'renewal_charge'
              AND cycle_sequence = @cycle_sequence
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue("@cycle_sequence", cycleSequence);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CycleEvent(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetInt64("total_amount_cents"),
            reader.GetString("currency"));
    }

    /// <summary>
    /// Lit le CONTRAT, pas le catalogue.
    ///
    /// Chaque montant vient de <c>amount_cents_snapshot</c>, fige a la
    /// souscription, et chaque ligne pointe la version de prix verrouillee.
    /// C'est ce qui garantit qu'une modification tarifaire posterieure ne
    /// change pas le renouvellement d'un contrat en cours.
    /// </summary>
    public static async Task<BillingV2RenewalContractSnapshot?>
        ReadContractSnapshotAsync(
            MySqlConnection connection,
            string subscriptionId,
            DateTime cyclePeriodStartUtc,
            CancellationToken cancellationToken)
    {
        BillingV2RenewalContractSnapshot? snapshot;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT
                    subscription.id,
                    subscription.customer_id,
                    subscription.status,
                    subscription.payment_mode,
                    subscription.currency,
                    subscription.discount_basis_points_snapshot,
                    subscription.minimum_commitment_amount_cents,
                    COALESCE(
                        subscription.billing_anchor_at,
                        subscription.started_at,
                        subscription.created_at) AS billing_anchor_at,
                    term.commitment_months
                FROM billing_v2_subscriptions subscription
                INNER JOIN billing_v2_commitment_terms term
                    ON term.id = subscription.commitment_term_id
                WHERE subscription.id = @subscription_id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@subscription_id", subscriptionId);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            snapshot = new BillingV2RenewalContractSnapshot(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
                reader.GetString("status"),
                reader.GetString("payment_mode"),
                reader.GetString("currency"),
                reader.GetInt32("commitment_months"),
                reader.GetInt32("discount_basis_points_snapshot"),
                reader.IsDBNull(
                    reader.GetOrdinal("minimum_commitment_amount_cents"))
                    ? null
                    : reader.GetInt64("minimum_commitment_amount_cents"),
                DateTime.SpecifyKind(
                    reader.GetDateTime("billing_anchor_at"),
                    DateTimeKind.Utc),
                Array.Empty<BillingV2RenewalContractItem>());
        }

        var items = new List<BillingV2RenewalContractItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT
                    item.service_id,
                    item.tier_id,
                    effective_component.subscription_item_id,
                    effective_component.component_id,
                    effective_component.service_price_id,
                    service.code AS service_code,
                    tier.code AS tier_code,
                    effective_component.billing_cadence,
                    item.quantity,
                    effective_component.amount_cents_snapshot,
                    effective_component.discount_eligible_snapshot
                FROM billing_v2_subscription_items item
                INNER JOIN billing_v2_services service
                    ON service.id = item.service_id
                LEFT JOIN billing_v2_service_tiers tier
                    ON tier.id = item.tier_id
                INNER JOIN billing_v2_subscription_item_effective_price_components effective_component
                    ON effective_component.subscription_item_id = item.id
                WHERE item.subscription_id = @subscription_id
                  AND item.status = 'active'
                  AND item.effective_from <= @cycle_period_start
                  AND (item.effective_until IS NULL OR item.effective_until > @cycle_period_start)
                  AND effective_component.status = 'active'
                  AND effective_component.billing_cadence = 'monthly'
                  AND effective_component.effective_from <= @cycle_period_start
                  AND (effective_component.effective_until IS NULL
                       OR effective_component.effective_until > @cycle_period_start)
                ORDER BY service.display_order, tier.display_order, item.id,
                    effective_component.display_order;
                """;
            command.Parameters.AddWithValue("@subscription_id", subscriptionId);
            command.Parameters.AddWithValue("@cycle_period_start", cyclePeriodStartUtc);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new BillingV2RenewalContractItem(
                    MariaDbIdentifierReader.ReadRequired(reader, "service_id"),
                    MariaDbIdentifierReader.ReadNullable(reader, "tier_id"),
                    MariaDbIdentifierReader.ReadRequired(
                        reader,
                        "service_price_id"),
                    reader.GetString("service_code"),
                    reader.IsDBNull(reader.GetOrdinal("tier_code"))
                        ? null
                        : reader.GetString("tier_code"),
                    reader.GetString("billing_cadence"),
                    reader.GetInt32("quantity"),
                    reader.GetInt64("amount_cents_snapshot"),
                    reader.GetBoolean("discount_eligible_snapshot"),
                    MariaDbIdentifierReader.ReadRequired(reader, "subscription_item_id"),
                    MariaDbIdentifierReader.ReadNullable(reader, "component_id")));
            }
        }

        return snapshot with { Items = items };
    }
}
