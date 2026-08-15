using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2ReconciliationRunResult(
    int Examined,
    int Claimed,
    int Settled,
    int ReconciliationRequired,
    string ReasonCode);

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
    private readonly IBillingV2Clock _clock;
    private readonly ILogger<BillingV2StripeReconciliationService> _logger;

    public BillingV2StripeReconciliationService(
        SqlRuntimeConfiguration sql,
        StripeRuntimeConfiguration stripe,
        BillingV2RuntimeConfiguration runtime,
        IBillingV2StripeRailService rail,
        IBillingV2Clock clock,
        ILogger<BillingV2StripeReconciliationService> logger)
    {
        _sql = sql;
        _stripe = stripe;
        _runtime = runtime;
        _rail = rail;
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

        foreach (var candidate in candidates)
        {
            var decision = BillingV2ReconciliationPolicy.Evaluate(candidate);

            if (decision.GiveUp)
            {
                await BillingV2FinancialCoreStore
                    .MarkReconciliationRequiredAsync(
                        connection,
                        candidate.AttemptId,
                        decision.ReasonCode,
                        _clock.UtcNow,
                        cancellationToken);
                manual++;
                continue;
            }

            if (!decision.ShouldRefetch)
            {
                continue;
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
                continue;
            }

            claimed++;

            var subscriptionId = await BillingV2FinancialCoreStore
                .ReadSubscriptionIdForEventAsync(
                    connection,
                    candidate.BillingEventId,
                    cancellationToken);
            if (subscriptionId is null)
            {
                await BillingV2FinancialCoreStore
                    .MarkReconciliationRequiredAsync(
                        connection,
                        candidate.AttemptId,
                        "BILLING_V2_RECONCILIATION_SUBSCRIPTION_NOT_FOUND",
                        _clock.UtcNow,
                        cancellationToken);
                manual++;
                continue;
            }

            // Meme chemin que le webhook : relecture Stripe, verification
            // stricte, verrou d'abonnement et compare-and-swap.
            var outcome = await _rail.VerifyAndSettleAsync(
                subscriptionId,
                cancellationToken);

            if (outcome.Settled)
            {
                settled++;
                continue;
            }

            if (outcome.ReconciliationRequired)
            {
                await BillingV2FinancialCoreStore
                    .MarkReconciliationRequiredAsync(
                        connection,
                        candidate.AttemptId,
                        outcome.ReasonCode,
                        _clock.UtcNow,
                        cancellationToken);
                manual++;
                continue;
            }

            // Toujours pas concluant : on repasse plus tard, sans rien changer
            // a l'etat financier.
            await BillingV2FinancialCoreStore.ScheduleNextReconciliationAsync(
                connection,
                candidate.AttemptId,
                _clock.UtcNow,
                decision.NextDelaySeconds,
                cancellationToken);
        }

        return new BillingV2ReconciliationRunResult(
            candidates.Count,
            claimed,
            settled,
            manual,
            candidates.Count == 0
                ? "BILLING_V2_RECONCILIATION_NOTHING_PENDING"
                : "BILLING_V2_RECONCILIATION_RUN_COMPLETED");
    }
}
