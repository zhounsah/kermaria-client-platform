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

    /// <summary>
    /// Verifie et regle un CYCLE DE RENOUVELLEMENT (Phase 3).
    ///
    /// Un renouvellement n'a pas de session checkout : la preuve financiere
    /// est l'invoice Stripe reellement payee, relue avec les identifiants
    /// persistes. Comme le chemin initial, cette methode ne conclut jamais
    /// depuis un payload brut.
    /// </summary>
    Task<BillingV2StripeSettlementResult> VerifyAndSettleRenewalAsync(
        string billingEventId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Controle de sante declenche par un signal d'abonnement. Ne peut que
    /// DEGRADER l'etat local (payment_attention / manual_review) ; jamais
    /// activer, jamais encaisser, jamais deprovisionner.
    /// </summary>
    Task<BillingV2StripeSettlementResult> EvaluateSubscriptionHealthAsync(
        string subscriptionId,
        string providerSubscriptionId,
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
    private readonly IBillingV2Clock _clock;
    private readonly ILogger<BillingV2StripeRailService> _logger;

    public BillingV2StripeRailService(
        SqlRuntimeConfiguration sql,
        StripeRuntimeConfiguration stripe,
        IBillingV2StripeGateway gateway,
        IBillingV2Clock clock,
        ILogger<BillingV2StripeRailService> logger)
    {
        _sql = sql;
        _stripe = stripe;
        _gateway = gateway;
        _clock = clock;
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

        // Phase 4. Perimetre de lancement gele : une capacite non validee bout
        // en bout ne doit pas pouvoir partir chez un provider, meme si le
        // coeur financier sait la calculer.
        if (billingEvent is not null)
        {
            var scope = BillingV2LaunchScope.EvaluateCheckout(
                Provider,
                billingEvent.PaymentModeSnapshot,
                billingEvent.TaxAmountCents);
            if (!scope.IsValid)
            {
                _logger.LogWarning(
                    "Billing V2 Stripe dispatch refused for subscription {SubscriptionId}: {ReasonCode} (out of launch scope).",
                    subscriptionId,
                    scope.ReasonCode);
                return Failed(scope.ReasonCode, false);
            }
        }

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
                var recovery = BillingV2StripeApprovalUrlRecoveryPolicy.Resolve(
                    await ReadPersistedApprovalUrlAsync(
                        connection,
                        subscriptionId,
                        cancellationToken),
                    existing.ApprovalUrl);
                if (recovery.RequiresManualReview)
                {
                    await BillingV2FinancialCoreStore
                        .MarkReconciliationRequiredAsync(
                            connection,
                            attempt.Id,
                            recovery.ReasonCode,
                            _clock.UtcNow,
                            cancellationToken);
                    return new BillingV2StripeDispatchResult(
                        false,
                        recovery.ReasonCode,
                        existing.SessionId,
                        ApprovalUrl: null,
                        Retryable: false);
                }

                return new BillingV2StripeDispatchResult(
                    true,
                    "BILLING_V2_STRIPE_CHECKOUT_ALREADY_CREATED",
                    existing.SessionId,
                    recovery.ApprovalUrl,
                    Retryable: false);
            }
        }

        var stripeRequest = BillingV2StripeCheckoutRequestFactory.Build(
            billingEvent,
            attempt.Id,
            attempt.ProviderRequestKey,
            customerEmail,
            successUrl,
            cancelUrl,
            // Rattachement a un client Stripe deja connu, s'il y en a un.
            // Sans lui, rien ne change : `customer_email` reste utilise.
            attempt.ProviderCustomerReference);
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
                var recovered = await _gateway.FindCheckoutSessionAsync(
                    new BillingV2StripeSessionLocator(
                        attempt.ProviderSessionId,
                        attempt.ProviderPaymentId,
                        ProviderSubscriptionId: null,
                        attempt.ProviderRequestKey),
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
                    var recovery =
                        BillingV2StripeApprovalUrlRecoveryPolicy.Resolve(
                            await ReadPersistedApprovalUrlAsync(
                                connection,
                                subscriptionId,
                                cancellationToken),
                            recovered.ApprovalUrl);
                    if (recovery.RequiresManualReview)
                    {
                        await BillingV2FinancialCoreStore
                            .MarkReconciliationRequiredAsync(
                                connection,
                                attempt.Id,
                                recovery.ReasonCode,
                                _clock.UtcNow,
                                cancellationToken);
                        return new BillingV2StripeDispatchResult(
                            false,
                            recovery.ReasonCode,
                            recovered.SessionId,
                            ApprovalUrl: null,
                            Retryable: false);
                    }

                    return new BillingV2StripeDispatchResult(
                        true,
                        "BILLING_V2_STRIPE_CHECKOUT_RECOVERED",
                        recovered.SessionId,
                        recovery.ApprovalUrl,
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

        // Phase 3, point 2. `payment_status=paid` sur la session ne suffit plus
        // en mode subscription : on relit l'abonnement ET l'invoice. Une session
        // payee puis un abonnement bascule `past_due` ne doit pas rester lu
        // comme un encaissement acquis.
        if (verification.Settled
            && string.Equals(
                expectedMode,
                BillingV2StripeModes.Subscription,
                StringComparison.Ordinal))
        {
            var lifecycle = await VerifyProviderLifecycleAsync(
                connection,
                attempt,
                snapshot?.SubscriptionId,
                billingEvent,
                cancellationToken);
            if (lifecycle is not null && !lifecycle.Settled)
            {
                verification = verification with
                {
                    Settled = false,
                    AttemptStatus = string.Equals(
                        lifecycle.Outcome,
                        BillingV2RenewalOutcomes.AmountMismatch,
                        StringComparison.Ordinal)
                        ? BillingV2PaymentAttemptStatuses.AmountMismatch
                        : BillingV2PaymentAttemptStatuses.InFlight,
                    SettlementStatus = string.Equals(
                        lifecycle.Outcome,
                        BillingV2RenewalOutcomes.AmountMismatch,
                        StringComparison.Ordinal)
                        ? BillingV2SettlementStatuses.AmountMismatch
                        : BillingV2SettlementStatuses.Pending,
                    ReasonCode = lifecycle.ReasonCode
                };
                await ApplyPaymentStateAsync(
                    connection,
                    subscriptionId,
                    lifecycle.Outcome,
                    cancellationToken);
            }
        }

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
            // La transition VPS est idempotente et reste dans la transaction :
            // elle reparent un event historiquement settled qui aurait ete
            // interrompu entre le settlement et la revue technique.
            await BillingV2VpsTechnicalReviewSettlement.QueuePendingReviewAsync(
                connection,
                transaction,
                billingEvent.Id,
                DateTime.UtcNow,
                cancellationToken);
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

        // Un VPS ne passe en revue qu'apres cette preuve Stripe. Cette ecriture
        // fait partie de la meme transaction que le settlement : un crash ne
        // peut donc pas laisser un BillingEvent settled avec une demande draft.
        await BillingV2VpsTechnicalReviewSettlement.QueuePendingReviewAsync(
            connection,
            transaction,
            billingEvent.Id,
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
    // PHASE 3 : CYCLE DE RENOUVELLEMENT
    // -----------------------------------------------------------------

    public async Task<BillingV2StripeSettlementResult>
        VerifyAndSettleRenewalAsync(
            string billingEventId,
            CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return NotSettled(
                "BILLING_V2_STRIPE_RAIL_NO_PERSISTENT_SQL",
                null,
                false);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var billingEvent = await BillingV2FinancialCoreStore
            .ReadBillingEventAsync(
                connection,
                transaction: null,
                billingEventId,
                cancellationToken);
        if (billingEvent is null)
        {
            return NotSettled(
                "BILLING_V2_RENEWAL_BILLING_EVENT_NOT_FOUND",
                null,
                true);
        }

        var subscriptionId = billingEvent.SubscriptionId;

        // La tentative est resolue AVANT l'appel reseau, exactement comme au
        // checkout initial : meme cle derivee de l'evenement, donc un rejeu
        // retombe sur la meme ligne au lieu d'en creer une seconde.
        BillingV2PaymentAttemptRecord attempt;
        await using (var transaction = await connection.BeginTransactionAsync(
                         IsolationLevel.ReadCommitted,
                         cancellationToken))
        {
            attempt = await BillingV2FinancialCoreStore
                .ResolveOrCreateAttemptAsync(
                    connection,
                    transaction,
                    billingEvent.Id,
                    Provider,
                    _stripe.ModeName,
                    billingEvent.TotalAmountCents,
                    billingEvent.Currency,
                    _clock.UtcNow,
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var locator = await ReadProviderLocatorAsync(
            connection,
            attempt.Id,
            subscriptionId,
            cancellationToken);
        if (locator.ProviderInvoiceId is null
            && locator.ProviderSubscriptionId is null)
        {
            // Rien de persiste a relire : on echoue en ferme plutot que de
            // balayer Stripe pour deviner quelle invoice nous concerne.
            return NotSettled(
                "BILLING_V2_RENEWAL_NO_PROVIDER_OBJECT",
                subscriptionId,
                false);
        }

        var invoice = locator.ProviderInvoiceId is not null
            ? await _gateway.GetInvoiceAsync(
                locator.ProviderInvoiceId,
                cancellationToken)
            : await _gateway.GetLatestInvoiceForSubscriptionAsync(
                locator.ProviderSubscriptionId!,
                cancellationToken);
        var providerSubscription = locator.ProviderSubscriptionId is null
            ? null
            : await _gateway.GetSubscriptionAsync(
                locator.ProviderSubscriptionId,
                cancellationToken);

        var lifecycle = BillingV2StripeLifecycleVerifier.VerifyInvoice(
            invoice,
            providerSubscription,
            new BillingV2StripeLifecycleExpectation(
                billingEvent.Id,
                subscriptionId,
                attempt.Id,
                attempt.ExpectedCurrency,
                attempt.ExpectedAmountCents,
                locator.ProviderSubscriptionId,
                ExpectedProviderCustomerId: null));

        await BillingV2FinancialCoreStore.LinkAttemptProviderObjectsAsync(
            connection,
            transaction: null,
            attempt.Id,
            invoice?.InvoiceId,
            invoice?.SubscriptionId ?? locator.ProviderSubscriptionId,
            _clock.UtcNow,
            cancellationToken);
        await BillingV2FinancialCoreStore.UpdateAttemptAsync(
            connection,
            transaction: null,
            attempt.Id,
            ResolveAttemptStatus(lifecycle),
            providerSessionId: null,
            invoice?.PaymentIntentId,
            BillingV2StripeModes.Subscription,
            invoice?.Status,
            lifecycle.SettledAmountCents,
            lifecycle.SettledCurrency,
            lifecycle.ReasonCode,
            _clock.UtcNow,
            cancellationToken);
        await ApplyPaymentStateAsync(
            connection,
            subscriptionId,
            lifecycle.Outcome,
            cancellationToken);

        if (!lifecycle.Settled)
        {
            var mismatch = string.Equals(
                lifecycle.Outcome,
                BillingV2RenewalOutcomes.AmountMismatch,
                StringComparison.Ordinal);
            if (mismatch)
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
                    lifecycle.ReasonCode,
                    _clock.UtcNow,
                    cancellationToken);
                await mismatchTransaction.CommitAsync(cancellationToken);
                _logger.LogWarning(
                    "Billing V2 renewal mismatch on subscription {SubscriptionId}: {ReasonCode}. No invoice marked paid.",
                    subscriptionId,
                    lifecycle.ReasonCode);
            }

            return NotSettled(lifecycle.ReasonCode, subscriptionId, mismatch);
        }

        // Verrou puis relecture sous verrou : un rejeu de `invoice.paid` ou un
        // passage du reconciliateur au meme instant retombent sur un no-op.
        await using var settleTransaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var version = await BillingV2FinancialCoreStore.LockSubscriptionAsync(
            connection,
            settleTransaction,
            subscriptionId,
            cancellationToken);
        if (version is null)
        {
            await settleTransaction.RollbackAsync(cancellationToken);
            return NotSettled(
                "BILLING_V2_STRIPE_SETTLEMENT_SUBSCRIPTION_NOT_FOUND",
                subscriptionId,
                true);
        }

        if (await IsAlreadySettledAsync(
                connection,
                settleTransaction,
                billingEvent.Id,
                cancellationToken))
        {
            await settleTransaction.CommitAsync(cancellationToken);
            return new BillingV2StripeSettlementResult(
                true,
                "BILLING_V2_RENEWAL_SETTLEMENT_ALREADY_APPLIED",
                subscriptionId,
                false);
        }

        await BillingV2FinancialCoreStore.ApplySettlementAsync(
            connection,
            settleTransaction,
            billingEvent.Id,
            BillingV2SettlementStatuses.Settled,
            lifecycle.ReasonCode,
            _clock.UtcNow,
            cancellationToken);
        // Un renouvellement ne change pas le statut de l'abonnement : il est
        // deja `active`. On incremente quand meme la version pour que toute
        // ecriture concurrente sur cet abonnement soit detectee.
        var swap = await BillingV2FinancialCoreStore
            .TryAdvanceSubscriptionAsync(
                connection,
                settleTransaction,
                subscriptionId,
                version.Value,
                "active",
                _clock.UtcNow,
                cancellationToken);
        if (!swap.IsValid)
        {
            await settleTransaction.RollbackAsync(cancellationToken);
            return NotSettled(swap.ReasonCode, subscriptionId, true);
        }

        await settleTransaction.CommitAsync(cancellationToken);
        return new BillingV2StripeSettlementResult(
            true,
            "BILLING_V2_RENEWAL_SETTLEMENT_CONFIRMED",
            subscriptionId,
            false);
    }

    public async Task<BillingV2StripeSettlementResult>
        EvaluateSubscriptionHealthAsync(
            string subscriptionId,
            string providerSubscriptionId,
            CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return NotSettled(
                "BILLING_V2_STRIPE_RAIL_NO_PERSISTENT_SQL",
                subscriptionId,
                false);
        }

        var providerSubscription = await _gateway.GetSubscriptionAsync(
            providerSubscriptionId,
            cancellationToken);
        var health = BillingV2StripeLifecycleVerifier.VerifySubscriptionHealth(
            providerSubscription);

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await ApplyPaymentStateAsync(
            connection,
            subscriptionId,
            health.Outcome,
            cancellationToken);

        // Jamais `Settled` : un controle de sante ne prouve aucun paiement.
        return NotSettled(
            health.ReasonCode,
            subscriptionId,
            string.Equals(
                health.Outcome,
                BillingV2RenewalOutcomes.Unpaid,
                StringComparison.Ordinal)
            || string.Equals(
                health.Outcome,
                BillingV2RenewalOutcomes.Cancelled,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// Applique la politique de grace : l'etat local devient visible, l'acces
    /// reste en place. Aucun retrait AD, aucun quota touche, aucune donnee
    /// supprimee.
    /// </summary>
    private async Task ApplyPaymentStateAsync(
        MySqlConnection connection,
        string subscriptionId,
        string outcome,
        CancellationToken cancellationToken)
    {
        var decision = BillingV2RenewalGracePolicy.Resolve(outcome);
        if (!decision.KeepsProvisioning)
        {
            throw new InvalidOperationException(
                "BILLING_V2_RENEWAL_DEPROVISIONING_NOT_ALLOWED");
        }

        await BillingV2FinancialCoreStore.SetSubscriptionPaymentStateAsync(
            connection,
            transaction: null,
            subscriptionId,
            decision.PaymentState,
            decision.ReasonCode,
            _clock.UtcNow,
            cancellationToken);
    }

    private async Task<BillingV2StripeLifecycleVerification?>
        VerifyProviderLifecycleAsync(
            MySqlConnection connection,
            BillingV2PaymentAttemptRecord attempt,
            string? providerSubscriptionIdFromSession,
            BillingV2FinalizedBillingEvent billingEvent,
            CancellationToken cancellationToken)
    {
        var providerSubscriptionId = providerSubscriptionIdFromSession;
        if (string.IsNullOrWhiteSpace(providerSubscriptionId))
        {
            var locator = await ReadProviderLocatorAsync(
                connection,
                attempt.Id,
                billingEvent.SubscriptionId,
                cancellationToken);
            providerSubscriptionId = locator.ProviderSubscriptionId;
        }

        if (string.IsNullOrWhiteSpace(providerSubscriptionId))
        {
            // Stripe ne nous a pas encore rattache d'abonnement : rien a
            // verifier de plus, on s'en tient au verdict de la session.
            return null;
        }

        var providerSubscription = await _gateway.GetSubscriptionAsync(
            providerSubscriptionId,
            cancellationToken);
        var invoice = string.IsNullOrWhiteSpace(
            providerSubscription?.LatestInvoiceId)
            ? null
            : await _gateway.GetInvoiceAsync(
                providerSubscription!.LatestInvoiceId!,
                cancellationToken);

        await BillingV2FinancialCoreStore.LinkAttemptProviderObjectsAsync(
            connection,
            transaction: null,
            attempt.Id,
            invoice?.InvoiceId,
            providerSubscriptionId,
            _clock.UtcNow,
            cancellationToken);

        if (invoice is null)
        {
            // Invoice pas encore disponible : on ne DEGRADE pas un paiement
            // deja prouve par la session pour un simple retard de propagation.
            // Seul un abonnement franchement malade (past_due, unpaid,
            // cancelled) remet le verdict en cause.
            var health = BillingV2StripeLifecycleVerifier
                .VerifySubscriptionHealth(providerSubscription);
            return string.Equals(
                health.Outcome,
                BillingV2RenewalOutcomes.Pending,
                StringComparison.Ordinal)
                ? null
                : health;
        }

        return BillingV2StripeLifecycleVerifier.VerifyInvoice(
                invoice,
                providerSubscription,
                new BillingV2StripeLifecycleExpectation(
                    billingEvent.Id,
                    billingEvent.SubscriptionId,
                    attempt.Id,
                    attempt.ExpectedCurrency,
                    attempt.ExpectedAmountCents,
                    providerSubscriptionId,
                    ExpectedProviderCustomerId: null));
    }

    private sealed record ProviderLocator(
        string? ProviderInvoiceId,
        string? ProviderSubscriptionId);

    /// <summary>
    /// URL d'approbation deja persistee pour cet abonnement, s'il y en a une.
    /// C'est la source preferee en reprise : elle a ete ecrite au moment ou la
    /// session a reellement ete creee.
    /// </summary>
    private static async Task<string?> ReadPersistedApprovalUrlAsync(
        MySqlConnection connection,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT approval_url
            FROM billing_v2_provider_checkout_sessions
            WHERE subscription_id = @subscription_id
              AND provider = 'stripe'
              AND approval_url IS NOT NULL
            ORDER BY created_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static async Task<ProviderLocator> ReadProviderLocatorAsync(
        MySqlConnection connection,
        string attemptId,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                attempt_row.provider_invoice_id,
                COALESCE(
                    attempt_row.provider_subscription_id,
                    (SELECT session_row.provider_subscription_id
                       FROM billing_v2_provider_checkout_sessions session_row
                      WHERE session_row.subscription_id = @subscription_id
                        AND session_row.provider = 'stripe'
                        AND session_row.provider_subscription_id IS NOT NULL
                      ORDER BY session_row.created_at ASC
                      LIMIT 1),
                    (SELECT agreement_row.provider_subscription_id
                       FROM billing_v2_payment_agreements agreement_row
                      WHERE agreement_row.subscription_id = @subscription_id
                        AND agreement_row.provider = 'stripe'
                      ORDER BY agreement_row.created_at ASC
                      LIMIT 1)) AS provider_subscription_id
            FROM billing_v2_payment_attempts attempt_row
            WHERE attempt_row.id = @attempt_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@attempt_id", attemptId);
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ProviderLocator(null, null);
        }

        return new ProviderLocator(
            reader.IsDBNull(reader.GetOrdinal("provider_invoice_id"))
                ? null
                : reader.GetString("provider_invoice_id"),
            reader.IsDBNull(reader.GetOrdinal("provider_subscription_id"))
                ? null
                : reader.GetString("provider_subscription_id"));
    }

    private static string ResolveAttemptStatus(
        BillingV2StripeLifecycleVerification lifecycle)
        => lifecycle.Outcome switch
        {
            BillingV2RenewalOutcomes.Paid =>
                BillingV2PaymentAttemptStatuses.Succeeded,
            BillingV2RenewalOutcomes.AmountMismatch =>
                BillingV2PaymentAttemptStatuses.AmountMismatch,
            BillingV2RenewalOutcomes.Failed
                or BillingV2RenewalOutcomes.Unpaid
                or BillingV2RenewalOutcomes.Cancelled =>
                BillingV2PaymentAttemptStatuses.Failed,
            _ => BillingV2PaymentAttemptStatuses.InFlight
        };

    private static BillingV2StripeSettlementResult NotSettled(
        string reasonCode,
        string? subscriptionId,
        bool reconciliationRequired)
        => new(false, reasonCode, subscriptionId, reconciliationRequired);

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
