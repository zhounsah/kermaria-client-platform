using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2ReconciliationRunResult(
    int Examined,
    int Claimed,
    int Settled,
    int ReconciliationRequired,
    string ReasonCode,
    // Phase 3 : une tentative qui explose ne doit ni disparaitre du compte
    // rendu, ni empecher les suivantes d'etre traitees.
    int Failed = 0)
{
    /// <summary>
    /// Restant a traiter apres ce passage : examine moins ce qui a conclu.
    /// C'est la metrique a surveiller - si elle ne descend jamais, quelque
    /// chose ne converge pas.
    /// </summary>
    public int Pending => Math.Max(
        0,
        Examined - Settled - ReconciliationRequired - Failed);
}

public interface IBillingV2StripeReconciliationService
{
    Task<BillingV2ReconciliationRunResult> ReconcilePendingAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Reconciliateur Stripe V2 (Phase 2.5).
///
/// Raison d'etre : le webhook reste un signal, et un signal peut ne jamais
/// arriver. Sans ce worker, un client debite laisserait une tentative
/// `in_flight` indefiniment, sans activation et sans alerte.
///
/// Ce que le worker ne fait jamais :
/// - creer un nouveau checkout ;
/// - creer une nouvelle PaymentAttempt ;
/// - activer sur autre chose qu'une relecture Stripe verifiee.
///
/// Il reutilise le meme BillingEvent et la meme PaymentAttempt, prend un bail
/// avant de travailler, et delegue la transition a
/// <see cref="IBillingV2StripeRailService.VerifyAndSettleAsync"/>, qui porte
/// deja le verrou d'abonnement et le compare-and-swap. Deux workers
/// concurrents ne peuvent donc pas produire deux activations.
/// </summary>
public sealed class BillingV2StripeReconciliationService
    : IBillingV2StripeReconciliationService
{
    /// <summary>
    /// Duree du bail. Assez longue pour couvrir un aller-retour Stripe lent,
    /// assez courte pour qu'un worker mort ne bloque pas la tentative.
    /// </summary>
    public const int LeaseSeconds = 120;

    public const int BatchSize = 20;

    private readonly SqlRuntimeConfiguration _sql;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly IBillingV2StripeRailService _rail;
    private readonly IBillingV2DocumentIssuerService _documents;
    private readonly IBillingV2Clock _clock;
    private readonly ILogger<BillingV2StripeReconciliationService> _logger;

    public BillingV2StripeReconciliationService(
        SqlRuntimeConfiguration sql,
        StripeRuntimeConfiguration stripe,
        BillingV2RuntimeConfiguration runtime,
        IBillingV2StripeRailService rail,
        IBillingV2DocumentIssuerService documents,
        IBillingV2Clock clock,
        ILogger<BillingV2StripeReconciliationService> logger)
    {
        _sql = sql;
        _stripe = stripe;
        _runtime = runtime;
        _rail = rail;
        _documents = documents;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BillingV2ReconciliationRunResult> ReconcilePendingAsync(
        CancellationToken cancellationToken)
    {
        if (!_runtime.AuthoritativeCheckoutEnabled
            || !_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return new BillingV2ReconciliationRunResult(
                0, 0, 0, 0, "BILLING_V2_RECONCILIATION_GATE_CLOSED");
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var now = _clock.UtcNow;

        // Menage prealable : une tentative dont la charge est deja reglee n'a
        // plus rien a reconcilier. On la ferme au lieu de la laisser tourner
        // en boucle dans le lot.
        var closed = await BillingV2FinancialCoreStore
            .CloseAttemptsCoveredBySettledEventAsync(
                connection,
                "stripe",
                _stripe.ModeName,
                now,
                cancellationToken);
        if (closed > 0)
        {
            _logger.LogInformation(
                "Billing V2 reconciliation closed {Closed} attempt(s) already covered by a settled billing event.",
                closed);
        }

        var candidates = await BillingV2FinancialCoreStore
            .ReadReconciliationCandidatesAsync(
                connection,
                "stripe",
                _stripe.ModeName,
                now,
                BatchSize,
                cancellationToken);

        var claimed = 0;
        var settled = 0;
        var manual = 0;
        var failed = 0;

        foreach (var candidate in candidates)
        {
            // Isolation par tentative : une tentative qui echoue ne doit pas
            // priver les suivantes de leur passage. Sans ce cadre, un seul
            // objet Stripe malforme gelait tout le lot.
            try
            {
                var outcome = await ReconcileOneAsync(
                    connection,
                    candidate,
                    cancellationToken);
                claimed += outcome.Claimed;
                settled += outcome.Settled;
                manual += outcome.Manual;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failed++;
                _logger.LogError(
                    exception,
                    "Billing V2 reconciliation of attempt {AttemptId} failed. Remaining attempts in this batch are still processed.",
                    candidate.AttemptId);
                await TryReleaseLeaseAsync(
                    connection,
                    candidate,
                    cancellationToken);
            }
        }

        var result = new BillingV2ReconciliationRunResult(
            candidates.Count,
            claimed,
            settled,
            manual,
            candidates.Count == 0
                ? "BILLING_V2_RECONCILIATION_NOTHING_PENDING"
                : "BILLING_V2_RECONCILIATION_RUN_COMPLETED",
            failed);
        if (candidates.Count > 0)
        {
            _logger.LogInformation(
                "Billing V2 reconciliation run: pending={Pending} reconciled={Reconciled} failed={Failed} reconciliation_required={ManualReview} (examined={Examined}, claimed={Claimed}).",
                result.Pending,
                result.Settled,
                result.Failed,
                result.ReconciliationRequired,
                result.Examined,
                result.Claimed);
        }

        return result;
    }

    private sealed record AttemptOutcome(int Claimed, int Settled, int Manual);

    /// <summary>
    /// Emet le document du cycle encaisse, exactement comme le ferait le
    /// webhook. Le rang du cycle est relu sur le BillingEvent : cycle 1 =
    /// facture initiale, au-dela = facture de renouvellement. Un echec
    /// d'emission ne remet jamais en cause l'encaissement deja verifie.
    /// </summary>
    private async Task TryIssueDocumentAsync(
        MySqlConnection connection,
        string subscriptionId,
        string billingEventId,
        CancellationToken cancellationToken)
    {
        try
        {
            var cycleSequence = await ReadCycleSequenceAsync(
                connection,
                billingEventId,
                cancellationToken);
            var result = cycleSequence > BillingV2RenewalPolicy.InitialCycleSequence
                ? await _documents.EnsureCycleInvoiceAsync(
                    subscriptionId,
                    billingEventId,
                    cycleSequence,
                    $"billing-v2-reconciliation-document-{billingEventId}",
                    cancellationToken)
                : await _documents.EnsureInitialInvoiceAsync(
                    subscriptionId,
                    $"billing-v2-document-{subscriptionId}",
                    cancellationToken);
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Billing V2 reconciliation document issuing for subscription {SubscriptionId} cycle {Cycle} returned {ReasonCode}. It can be retried idempotently.",
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
                "Billing V2 reconciliation document issuing failed for subscription {SubscriptionId}. Settlement stands and issuing can be retried idempotently.",
                subscriptionId);
        }
    }

    private static async Task<int> ReadCycleSequenceAsync(
        MySqlConnection connection,
        string billingEventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT cycle_sequence
            FROM billing_v2_billing_events
            WHERE id = @billing_event_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@billing_event_id", billingEventId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value is DBNull
            ? BillingV2RenewalPolicy.InitialCycleSequence
            : Convert.ToInt32(value);
    }

    private async Task<AttemptOutcome> ReconcileOneAsync(
        MySqlConnection connection,
        BillingV2ReconciliationCandidate candidate,
        CancellationToken cancellationToken)
    {
        var decision = BillingV2ReconciliationPolicy.Evaluate(candidate);

        if (decision.GiveUp)
        {
            await BillingV2FinancialCoreStore.MarkReconciliationRequiredAsync(
                connection,
                candidate.AttemptId,
                decision.ReasonCode,
                _clock.UtcNow,
                cancellationToken);
            return new AttemptOutcome(0, 0, 1);
        }

        if (!decision.ShouldRefetch)
        {
            return new AttemptOutcome(0, 0, 0);
        }

        // Bail : un seul worker travaille cette tentative.
        if (!await BillingV2FinancialCoreStore.TryClaimReconciliationAsync(
                connection,
                candidate.AttemptId,
                _clock.UtcNow,
                LeaseSeconds,
                cancellationToken))
        {
            _logger.LogInformation(
                "Billing V2 reconciliation attempt {AttemptId} is already leased by another worker.",
                candidate.AttemptId);
            return new AttemptOutcome(0, 0, 0);
        }

        var subscriptionId = await BillingV2FinancialCoreStore
            .ReadSubscriptionIdForEventAsync(
                connection,
                candidate.BillingEventId,
                cancellationToken);
        if (subscriptionId is null)
        {
            await BillingV2FinancialCoreStore.MarkReconciliationRequiredAsync(
                connection,
                candidate.AttemptId,
                "BILLING_V2_RECONCILIATION_SUBSCRIPTION_NOT_FOUND",
                _clock.UtcNow,
                cancellationToken);
            return new AttemptOutcome(1, 0, 1);
        }

        // Meme chemin que le webhook : relecture Stripe, verification
        // stricte, verrou d'abonnement et compare-and-swap.
        var outcome = await _rail.VerifyAndSettleAsync(
            subscriptionId,
            cancellationToken);

        if (outcome.Settled)
        {
            // Le reconciliateur existe pour les webhooks manques. Sans cette
            // emission, un encaissement qui converge par ce chemin resterait
            // sans document : client debite, jamais facture. L'emetteur est
            // idempotent, l'appeler depuis les deux chemins est sans risque.
            await TryIssueDocumentAsync(
                connection,
                subscriptionId,
                candidate.BillingEventId,
                cancellationToken);
            return new AttemptOutcome(1, 1, 0);
        }

        if (outcome.ReconciliationRequired)
        {
            await BillingV2FinancialCoreStore.MarkReconciliationRequiredAsync(
                connection,
                candidate.AttemptId,
                outcome.ReasonCode,
                _clock.UtcNow,
                cancellationToken);
            return new AttemptOutcome(1, 0, 1);
        }

        // Toujours pas concluant : on repasse plus tard, sans rien changer
        // a l'etat financier.
        await BillingV2FinancialCoreStore.ScheduleNextReconciliationAsync(
            connection,
            candidate.AttemptId,
            _clock.UtcNow,
            decision.NextDelaySeconds,
            cancellationToken);
        return new AttemptOutcome(1, 0, 0);
    }

    /// <summary>
    /// Rend le bail apres une exception, pour qu'une tentative qui a plante ne
    /// reste pas bloquee jusqu'a l'expiration du bail.
    /// </summary>
    private async Task TryReleaseLeaseAsync(
        MySqlConnection connection,
        BillingV2ReconciliationCandidate candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            await BillingV2FinancialCoreStore.ScheduleNextReconciliationAsync(
                connection,
                candidate.AttemptId,
                _clock.UtcNow,
                BillingV2ReconciliationPolicy.NextDelaySeconds(
                    candidate.ReconciliationAttempts),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 reconciliation could not release the lease on attempt {AttemptId}. It will free itself when the lease expires.",
                candidate.AttemptId);
        }
    }
}

/// <summary>
/// Declencheur periodique du reconciliateur (Phase 3, point 1).
///
/// OFF par defaut : sans <c>BILLING_V2_RECONCILIATION_WORKER_ENABLED=true</c>,
/// il n'est meme pas enregistre, donc aucun appel provider ne peut partir de
/// lui. Plusieurs instances sont sures : la concurrence est arbitree en base
/// par le bail, pas par une election de leader.
/// </summary>
public sealed class BillingV2StripeReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly ILogger<BillingV2StripeReconciliationWorker> _logger;

    public BillingV2StripeReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        BillingV2RuntimeConfiguration runtime,
        ILogger<BillingV2StripeReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _runtime = runtime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_runtime.ReconciliationWorkerEnabled)
        {
            _logger.LogInformation(
                "Billing V2 reconciliation worker is disabled. No Stripe call will be made by it.");
            return;
        }

        var interval = TimeSpan.FromSeconds(
            _runtime.ReconciliationIntervalSeconds);
        _logger.LogInformation(
            "Billing V2 reconciliation worker started with a {Seconds}s interval.",
            interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reconciler = scope.ServiceProvider
                    .GetRequiredService<IBillingV2StripeReconciliationService>();
                await reconciler.ReconcilePendingAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Un passage rate ne doit pas tuer le worker : le suivant
                // reprendra les memes tentatives, la reconciliation est
                // idempotente par construction.
                _logger.LogError(
                    exception,
                    "Billing V2 reconciliation run failed. The next run will retry the same attempts.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
