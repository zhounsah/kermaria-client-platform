using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
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
    int Failed = 0,
    // Charges deja encaissees dont le document restait a emettre, reprises par
    // ce passage.
    int DocumentsResumed = 0)
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

/// <summary>
/// Charge encaissee dont le document reste a emettre.
/// </summary>
public sealed record BillingV2PendingDocument(
    string SubscriptionId,
    string BillingEventId);

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

        // Reprise documentaire. Une charge encaissee dont l'emission a echoue
        // sortait definitivement du circuit : la tentative de paiement est
        // close, donc plus jamais candidate, et rien d'autre ne repassait
        // dessus. Le client restait debite sans facture. Le controle porte
        // donc sur l'invariant lui-meme - BillingEvent regle + document non
        // emis - et non sur l'etat de la tentative.
        var documentsResumed = await ResumePendingDocumentsAsync(
            connection,
            cancellationToken);

        var result = new BillingV2ReconciliationRunResult(
            candidates.Count,
            claimed,
            settled,
            manual,
            candidates.Count == 0 && documentsResumed == 0
                ? "BILLING_V2_RECONCILIATION_NOTHING_PENDING"
                : "BILLING_V2_RECONCILIATION_RUN_COMPLETED",
            failed,
            documentsResumed);
        if (candidates.Count > 0 || documentsResumed > 0)
        {
            _logger.LogInformation(
                "Billing V2 reconciliation run: pending={Pending} reconciled={Reconciled} failed={Failed} reconciliation_required={ManualReview} documents_resumed={DocumentsResumed} (examined={Examined}, claimed={Claimed}).",
                result.Pending,
                result.Settled,
                result.Failed,
                result.ReconciliationRequired,
                result.DocumentsResumed,
                result.Examined,
                result.Claimed);
        }

        return result;
    }

    private sealed record AttemptOutcome(int Claimed, int Settled, int Manual);

    /// <summary>
    /// Reprend l'emission des documents dus sur des charges deja encaissees.
    ///
    /// Fail-closed et idempotent : rien n'est cree ici. Le lot est constitue
    /// depuis l'etat financier - seuls les BillingEvents <c>settled</c> dont le
    /// document n'est pas <c>issued</c> - puis confie a l'emetteur existant,
    /// qui reprend un document deja present au lieu d'en fabriquer un second et
    /// qui porte deja le verrou d'intention BPCE.
    ///
    /// Les emissions laissees en <c>reconciliation_required</c> sont
    /// volontairement exclues : un resultat BPCE indetermine doit rester en
    /// revue manuelle, jamais etre relance en boucle au risque d'un second
    /// numero fiscal.
    /// </summary>
    private async Task<int> ResumePendingDocumentsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        var pending = await ReadDocumentsToResumeAsync(
            connection,
            _stripe.ModeName,
            cancellationToken);
        if (pending.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation(
            "Billing V2 reconciliation is resuming {Count} settled charge(s) whose document is still missing.",
            pending.Count);
        var resumed = 0;
        foreach (var document in pending)
        {
            await TryIssueDocumentAsync(
                connection,
                document.SubscriptionId,
                document.BillingEventId,
                cancellationToken);
            resumed++;
        }

        return resumed;
    }

    /// <summary>
    /// Lot de reprise. Expose pour etre verifiable sur MariaDB reelle : la
    /// selection est le coeur du controle, et une suite en persistance mock ne
    /// peut rien prouver de son comportement SQL.
    /// </summary>
    public static async Task<IReadOnlyList<BillingV2PendingDocument>>
        ReadDocumentsToResumeAsync(
            MySqlConnection connection,
            string environment,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                event_row.id AS billing_event_id,
                event_row.subscription_id
            FROM billing_v2_billing_events event_row
            INNER JOIN billing_v2_payment_attempts attempt
                ON attempt.billing_event_id = event_row.id
            LEFT JOIN billing_v2_subscription_documents doc
                ON doc.billing_event_id = event_row.id
            LEFT JOIN billing_v2_document_issuance_attempts issuance
                ON issuance.commercial_document_id = doc.commercial_document_id
            WHERE event_row.settlement_status = @settled
              AND event_row.document_status <> @issued
              AND attempt.status = @succeeded
              AND attempt.provider = 'stripe'
              AND attempt.environment = @environment
              AND (issuance.status IS NULL
                   OR issuance.status <> @manual_review)
            GROUP BY event_row.id, event_row.subscription_id
            ORDER BY event_row.settled_at ASC, event_row.id ASC
            LIMIT @batch;
            """;
        command.Parameters.AddWithValue(
            "@settled",
            BillingV2SettlementStatuses.Settled);
        command.Parameters.AddWithValue("@issued", "issued");
        command.Parameters.AddWithValue("@succeeded", "succeeded");
        command.Parameters.AddWithValue("@environment", environment);
        command.Parameters.AddWithValue(
            "@manual_review",
            BillingV2DocumentIssuanceStatuses.ReconciliationRequired);
        command.Parameters.AddWithValue("@batch", BatchSize);

        var pending = new List<BillingV2PendingDocument>();
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            pending.Add(new BillingV2PendingDocument(
                MariaDbIdentifierReader.ReadRequired(
                    reader,
                    "subscription_id"),
                MariaDbIdentifierReader.ReadRequired(
                    reader,
                    "billing_event_id")));
        }

        return pending;
    }

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
