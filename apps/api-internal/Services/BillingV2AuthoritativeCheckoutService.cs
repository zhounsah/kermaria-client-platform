using System.Data;
using System.Security.Cryptography;
using System.Text;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Demande de checkout authoritative. Deux formes, une seule doit etre
/// renseignee :
///
/// - <see cref="LegacyOfferId"/> : parcours historique, indexe par offre ;
/// - <see cref="Selection"/> : souscription V2 native, ou la configuration
///   elle-meme est l'identite metier. C'est la seule forme capable de
///   representer une configuration personnalisee.
///
/// Aucune des deux ne transporte de montant : le total est recalcule ici par
/// BillingV2PricingEngine a partir du catalogue serveur.
/// </summary>
public sealed record BillingV2AuthoritativeCheckoutRequest(
    string? LegacyOfferId,
    BillingV2PublicSelection? Selection,
    string Provider,
    string IdempotencyKey,
    string SuccessUrl,
    string CancelUrl);

/// <summary>
/// Composition facturable resolue, quelle que soit la forme de la demande.
/// Tout le chemin d'ecriture en aval ne connait plus que ce type : legacy et
/// natif partagent donc exactement le meme code de creation, de tarification
/// et de BillingEvent.
/// </summary>
public sealed record BillingV2AuthoritativeCheckoutComposition(
    string PresetId,
    string CommitmentTermId,
    string PaymentMode,
    int CommitmentMonths,
    int DiscountBasisPoints,
    IReadOnlyList<BillingV2NewSubscriptionPresetItem> Items,
    string? LegacyOfferId,
    string SelectionCanonical,
    string SelectionFingerprint);

/// <summary>
/// Empreinte de l'identite metier d'une demande. Elle remplace le
/// `legacy_offer_id` comme ancre : deux configurations differentes ne peuvent
/// pas se retrouver rattachees a la meme intention, et deux demandes
/// identiques y retombent forcement.
/// </summary>
public static class BillingV2CheckoutSelectionFingerprint
{
    public const string LegacyPrefix = "billing_v2.legacy_offer|";

    public static string ForLegacyOffer(string legacyOfferId)
        => Hash($"{LegacyPrefix}{legacyOfferId}");

    public static string ForSelection(string selectionCanonical)
        => Hash(selectionCanonical);

    private static string Hash(string canonical)
        => Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
}

public sealed record BillingV2AuthoritativeCheckoutResult(
    bool Created,
    string SubscriptionId,
    string Provider,
    string Environment,
    string OutboxEventId,
    string IdempotencyKeyHash,
    long TotalDueNowCents,
    string ReasonCode,
    string? ApprovalUrl);

public sealed record BillingV2AuthoritativeCheckoutReadiness(
    bool Authorized,
    string ReasonCode);

public sealed record BillingV2SubscriptionPriceLockPlan(
    string LockType,
    long AmountCents,
    string Currency,
    DateTime EffectiveFromUtc,
    DateTime EffectiveUntilUtc,
    string? SourceLegacyOfferId,
    string Reason);

public interface IBillingV2AuthoritativeCheckoutService
{
    Task<BillingV2AuthoritativeCheckoutResult> CreateAsync(
        PortalSessionContext session,
        BillingV2AuthoritativeCheckoutRequest request,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class BillingV2AuthoritativeCheckoutService
    : IBillingV2AuthoritativeCheckoutService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly IBillingV2CheckoutReadinessService _readiness;
    private readonly IBillingV2PricingEngine _pricing;
    private readonly IBillingV2PublicCatalogService _catalog;

    public BillingV2AuthoritativeCheckoutService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration runtime,
        PayPalRuntimeConfiguration paypal,
        StripeRuntimeConfiguration stripe,
        IBillingV2CheckoutReadinessService readiness,
        IBillingV2PricingEngine pricing,
        IBillingV2PublicCatalogService catalog)
    {
        _sql = sql;
        _runtime = runtime;
        _paypal = paypal;
        _stripe = stripe;
        _readiness = readiness;
        _pricing = pricing;
        _catalog = catalog;
    }

    public async Task<BillingV2AuthoritativeCheckoutResult> CreateAsync(
        PortalSessionContext session,
        BillingV2AuthoritativeCheckoutRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var provider = NormalizeProvider(request.Provider);
        var environment = ResolveEnvironment(provider);
        var gate = BillingV2AuthoritativeCheckoutGate.Evaluate(
            _runtime,
            _sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString),
            request.IdempotencyKey);
        if (!gate.Authorized)
        {
            throw new InvalidOperationException(gate.ReasonCode);
        }

        var now = DateTime.UtcNow;

        await using var readConnection =
            new MySqlConnection(_sql.ConnectionString);
        await readConnection.OpenAsync(cancellationToken);

        // La composition est resolue AVANT toute recherche d'intention : c'est
        // elle qui porte l'identite metier de la demande. Une selection
        // invalide echoue donc ici, avant la moindre ecriture.
        var composition = await ResolveCompositionAsync(
            readConnection,
            request,
            now,
            cancellationToken);
        var requestFingerprintHash =
            BillingV2AuthoritativeCheckoutIdempotencyPolicy
                .ComputeRequestFingerprintHash(
                    session.CustomerId,
                    session.UserId,
                    provider,
                    environment,
                    composition.SelectionFingerprint);
        var intentRequest = new BillingV2SubscriptionIntentRequest(
            session.CustomerId,
            request.IdempotencyKey,
            composition.SelectionFingerprint,
            provider,
            environment);
        var intentCanonical =
            BillingV2SubscriptionIntentKey.Canonical(intentRequest);
        var intentHash = BillingV2SubscriptionIntentKey.Hash(intentCanonical);

        // L'ancre persistee de la cle recue est la ligne de demande. Elle est
        // relue AVANT toute creation financiere : c'est ce qui rend le refus
        // ferme possible. Sans cette lecture, une cle rejouee avec un contenu
        // different ouvrait une seconde intention, et l'INSERT IGNORE final
        // avalait silencieusement sa ligne de registre — laissant un contrat
        // orphelin, indispatchable et invisible des projections de droits.
        var anchored = await ReadCheckoutRequestByKeyAsync(
            readConnection,
            transaction: null,
            session.CustomerId,
            provider,
            environment,
            request.IdempotencyKey,
            cancellationToken);
        if (anchored is not null)
        {
            EnsureSameSelection(anchored, composition.SelectionFingerprint);
            return await BuildResultFromRequestAsync(
                readConnection,
                anchored,
                cancellationToken);
        }

        // L'ancre d'idempotence est l'intention serveur.
        // 1) meme client_request_id -> meme intention (double clic, retry) ;
        // 2) sinon, intention encore ouverte pour la MEME configuration : un
        //    rafraichissement de navigateur fabrique forcement un nouveau
        //    client_request_id, il doit quand meme retomber sur l'intention
        //    existante au lieu d'en ouvrir une seconde.
        var existingIntent = await BillingV2FinancialCoreStore
                .FindIntentByHashAsync(
                    readConnection,
                    transaction: null,
                    intentHash,
                    cancellationToken)
            ?? await BillingV2FinancialCoreStore
                .FindOpenIntentForSelectionAsync(
                    readConnection,
                    transaction: null,
                    session.CustomerId,
                    composition.SelectionFingerprint,
                    provider,
                    environment,
                    now,
                    cancellationToken);
        if (existingIntent is not null)
        {
            return await BuildExistingResultAsync(
                readConnection,
                existingIntent,
                provider,
                environment,
                cancellationToken);
        }

        var presetItems = composition.Items;
        var mapping = composition;
        var pricing = CalculatePricing(mapping, presetItems, now);
        var checkoutReadiness = await _readiness.CheckAsync(
            new BillingV2CheckoutReadinessRequest(
                presetItems
                    .Select(item => item.ServicePriceId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                provider,
                environment),
            cancellationToken);
        var checkoutPlan = BillingV2CheckoutPlanner.Plan(
            checkoutReadiness,
            presetItems,
            pricing);
        var subscriptionId = Guid.NewGuid().ToString("D");
        var providerRequest = new BillingV2ProviderCheckoutCommandRequest(
            subscriptionId,
            session.CustomerId,
            session.Email,
            request.SuccessUrl,
            request.CancelUrl,
            checkoutPlan,
            checkoutReadiness,
            correlationId,
            session.UserId);
        var providerPlan = BillingV2ProviderCheckoutCommandPlanner.Plan(
            providerRequest);

        // L'evenement financier est construit AVANT toute ecriture : la
        // fabrique re-verifie que la somme des lignes retombe exactement sur le
        // total du Pricing Engine, et echoue en ferme sinon.
        // Periode contractuelle en jours civils Paris, derivee de l'ancre, et
        // non une arithmetique sur l'instant UTC courant : c'est ce qui la rend
        // reproductible aux bornes (minuit Paris, changement d'heure, fin de
        // mois) et identique a celle que portera le document.
        var contractPeriod = BillingV2BillingCalendar.ResolvePeriod(
            now,
            mapping.PaymentMode,
            mapping.CommitmentMonths);
        var periodStart = contractPeriod.StartUtc;
        var periodEnd = contractPeriod.EndUtc;
        var eventBuild = BillingV2BillingEventFactory.BuildInitialCharge(
            new BillingV2BillingEventBuildRequest(
                mapping.PaymentMode,
                mapping.CommitmentMonths,
                mapping.DiscountBasisPoints,
                checkoutPlan.Currency,
                presetItems,
                pricing,
                periodStart,
                periodEnd,
                $"billing_v2.billing_event|initial_charge|{intentHash}"));

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var itemPlan = BillingV2NewSubscriptionPlanner.Plan(
            session,
            presetItems);
        // Plancher d'engagement partage avec BillingV2NewSubscriptionService :
        // 45 % du MRR initial remise, jamais 100 %.
        var minimumCommitmentAmountCents =
            BillingV2CommitmentFloorPolicy.Resolve(
                _pricing,
                mapping.PaymentMode,
                mapping.CommitmentMonths,
                pricing.DiscountedRecurringAmountCents);
        // Dates contractuelles derivees de la MEME ancre que le BillingEvent :
        // la periode facturee et la periode de droits ne peuvent pas diverger.
        var lifecycle = BillingV2SubscriptionLifecyclePolicy.Plan(
            mapping.PaymentMode,
            mapping.CommitmentMonths,
            now);
        await InsertSubscriptionAsync(
            connection,
            transaction,
            subscriptionId,
            session.CustomerId,
            mapping,
            minimumCommitmentAmountCents,
            lifecycle,
            now,
            cancellationToken);

        // INSERT IGNORE puis relecture : sous concurrence, le perdant annule sa
        // propre transaction (donc son abonnement brouillon) et repart de
        // l'intention du gagnant, au lieu de creer un second contrat.
        var changeId = Guid.NewGuid().ToString("D");
        var intentInserted = await BillingV2FinancialCoreStore
            .TryInsertIntentAsync(
                connection,
                transaction,
                changeId,
                subscriptionId,
                request.IdempotencyKey,
                intentCanonical,
                intentHash,
                IntentInitialSubscriptionVersion,
                now,
                now.AddMinutes(IntentExpiryMinutes),
                session.UserId,
                cancellationToken);
        if (!intentInserted)
        {
            await transaction.RollbackAsync(cancellationToken);
            var winner = await BillingV2FinancialCoreStore
                .FindIntentByHashAsync(
                    readConnection,
                    transaction: null,
                    intentHash,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "BILLING_V2_INTENT_NOT_PERSISTED");
            return await BuildExistingResultAsync(
                readConnection,
                winner,
                provider,
                environment,
                cancellationToken);
        }

        var billingEventId = Guid.NewGuid().ToString("D");
        await BillingV2FinancialCoreStore.InsertFinalizedBillingEventAsync(
            connection,
            transaction,
            billingEventId,
            session.CustomerId,
            subscriptionId,
            changeId,
            eventBuild.Draft,
            mapping.PaymentMode,
            mapping.CommitmentMonths,
            mapping.DiscountBasisPoints,
            periodStart,
            periodEnd,
            now,
            now.AddMinutes(SettlementDeadlineMinutes),
            eventBuild.LineSources,
            cancellationToken,
            // Convention Phase 3 : la charge initiale est le cycle 1. Elle
            // devient ainsi unique en base par abonnement, au meme titre que
            // chaque renouvellement l'est par cycle.
            BillingV2RenewalPolicy.InitialCycleSequence);

        var requestId = Guid.NewGuid().ToString("D");
        var requestAnchored = await TryInsertCheckoutRequestAsync(
            connection,
            transaction,
            requestId,
            session,
            request,
            composition,
            provider,
            environment,
            requestFingerprintHash,
            subscriptionId,
            changeId,
            billingEventId,
            cancellationToken);
        if (!requestAnchored)
        {
            // Course perdue sur la cle d'idempotence : tout ce que cette
            // transaction a ecrit disparait, y compris l'abonnement brouillon
            // et son BillingEvent. On repart de la demande gagnante.
            await transaction.RollbackAsync(cancellationToken);
            var winner = await ReadCheckoutRequestByKeyAsync(
                    readConnection,
                    transaction: null,
                    session.CustomerId,
                    provider,
                    environment,
                    request.IdempotencyKey,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "BILLING_V2_CHECKOUT_REQUEST_NOT_PERSISTED");
            EnsureSameSelection(winner, composition.SelectionFingerprint);
            return await BuildResultFromRequestAsync(
                readConnection,
                winner,
                cancellationToken);
        }

        foreach (var user in itemPlan.Users)
        {
            await InsertUserAsync(
                connection,
                transaction,
                subscriptionId,
                user,
                now,
                cancellationToken);
        }

        foreach (var item in itemPlan.Items)
        {
            await InsertItemAsync(
                connection,
                transaction,
                subscriptionId,
                item,
                lifecycle,
                now,
                cancellationToken);
            await InsertItemProvisioningAsync(
                connection,
                transaction,
                item,
                now,
                cancellationToken);
        }

        var priceLock = BillingV2AuthoritativeCheckoutPriceLockPolicy.Plan(
            composition.LegacyOfferId,
            mapping.PaymentMode,
            mapping.CommitmentMonths,
            pricing,
            now);
        await InsertSubscriptionPriceLockAsync(
            connection,
            transaction,
            subscriptionId,
            priceLock,
            cancellationToken);

        var outboxEventId = Guid.NewGuid().ToString("D");
        await InsertOutboxEventAsync(
            connection,
            transaction,
            outboxEventId,
            providerPlan,
            cancellationToken);
        await InsertAuditAsync(
            connection,
            transaction,
            subscriptionId,
            session.UserId,
            providerPlan.PayloadText,
            cancellationToken);
        await MarkCheckoutRequestQueuedAsync(
            connection,
            transaction,
            requestId,
            outboxEventId,
            providerPlan.IdempotencyKeyHash,
            checkoutReadiness.ReasonCode,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BillingV2AuthoritativeCheckoutResult(
            Created: true,
            subscriptionId,
            provider,
            environment,
            outboxEventId,
            providerPlan.IdempotencyKeyHash,
            eventBuild.Draft.TotalAmountCents,
            checkoutReadiness.ReasonCode,
            ApprovalUrl: null);
    }

    private const long IntentInitialSubscriptionVersion = 1;
    private const int IntentExpiryMinutes = 60;
    private const int SettlementDeadlineMinutes = 60;

    /// <summary>
    /// Reconstruit la reponse a partir d'une intention deja ouverte. Le total
    /// annonce provient du BillingEvent, jamais d'un recalcul catalogue : un
    /// rejeu ne peut donc pas afficher un montant different du premier appel.
    /// </summary>
    private static async Task<BillingV2AuthoritativeCheckoutResult>
        BuildExistingResultAsync(
            MySqlConnection connection,
            BillingV2IntentRecord intent,
            string provider,
            string environment,
            CancellationToken cancellationToken)
    {
        var checkoutRequest = await ReadCheckoutRequestByChangeAsync(
            connection,
            intent.Id,
            cancellationToken);
        var approvalUrl = await ReadApprovalUrlAsync(
            connection,
            transaction: null,
            intent.SubscriptionId,
            checkoutRequest?.IdempotencyKeyHash,
            cancellationToken);
        var total = 0L;
        if (intent.BillingEventId is not null)
        {
            var billingEvent = await BillingV2FinancialCoreStore
                .ReadBillingEventAsync(
                    connection,
                    transaction: null,
                    intent.BillingEventId,
                    cancellationToken);
            total = billingEvent?.TotalAmountCents ?? 0;
        }

        return new BillingV2AuthoritativeCheckoutResult(
            Created: false,
            intent.SubscriptionId,
            checkoutRequest?.Provider ?? provider,
            checkoutRequest?.Environment ?? environment,
            checkoutRequest?.OutboxEventId ?? string.Empty,
            checkoutRequest?.IdempotencyKeyHash ?? string.Empty,
            total,
            checkoutRequest?.ReasonCode
                ?? "BILLING_V2_AUTHORITATIVE_CHECKOUT_IDEMPOTENT_NOOP",
            approvalUrl);
    }

    private static async Task<BillingV2AuthoritativeCheckoutRequestRecord?>
        ReadCheckoutRequestByChangeAsync(
            MySqlConnection connection,
            string subscriptionChangeId,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                {CheckoutRequestColumns}
            FROM billing_v2_authoritative_checkout_requests
            WHERE subscription_change_id = @change_id
            ORDER BY created_at ASC, id ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@change_id", subscriptionChangeId);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadRequestRecord(reader);
    }

    private const string CheckoutRequestColumns =
        """
        id,
        subscription_id,
        provider,
        environment,
        request_fingerprint_hash,
        selection_fingerprint,
        billing_event_id,
        outbox_event_id,
        idempotency_key_hash,
        reason_code
        """;

    private static BillingV2AuthoritativeCheckoutRequestRecord
        ReadRequestRecord(MySqlDataReader reader)
        => new(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "subscription_id"),
            reader.GetString("provider"),
            reader.GetString("environment"),
            reader.GetString("request_fingerprint_hash"),
            reader.GetString("selection_fingerprint"),
            MariaDbIdentifierReader.ReadNullable(reader, "billing_event_id"),
            MariaDbIdentifierReader.ReadNullable(reader, "outbox_event_id"),
            reader.IsDBNull(reader.GetOrdinal("idempotency_key_hash"))
                ? null
                : reader.GetString("idempotency_key_hash"),
            reader.IsDBNull(reader.GetOrdinal("reason_code"))
                ? null
                : reader.GetString("reason_code"));

    private string ResolveEnvironment(string provider)
        => provider == "stripe" ? _stripe.ModeName : _paypal.ModeName;

    private static string NormalizeProvider(string provider)
    {
        var normalized = provider.Trim().ToLowerInvariant();
        if (normalized is not ("stripe" or "paypal"))
        {
            throw new InvalidOperationException(
                "BILLING_V2_PROVIDER_UNSUPPORTED");
        }

        return normalized;
    }

    /// <summary>
    /// Resout la demande en composition facturable.
    ///
    /// Les deux chemins convergent volontairement vers le meme type : une
    /// configuration personnalisee ne beneficie d'aucun raccourci, elle passe
    /// par les memes prix, le meme Pricing Engine et le meme BillingEvent que
    /// la formule standard.
    /// </summary>
    private async Task<BillingV2AuthoritativeCheckoutComposition>
        ResolveCompositionAsync(
            MySqlConnection connection,
            BillingV2AuthoritativeCheckoutRequest request,
            DateTime now,
            CancellationToken cancellationToken)
    {
        if (request.Selection is { } selection)
        {
            if (!string.IsNullOrWhiteSpace(request.LegacyOfferId))
            {
                // Deux identites metier pour une meme demande : refus, sinon
                // c'est l'ordre du code qui deciderait de ce qui est facture.
                throw new InvalidOperationException(
                    "BILLING_V2_CHECKOUT_AMBIGUOUS_SELECTION");
            }

            var catalog = await _catalog.GetCatalogAsync(cancellationToken);
            var resolved = await BillingV2NativeSelectionResolver.ResolveAsync(
                connection,
                catalog,
                selection,
                now,
                cancellationToken);

            return new BillingV2AuthoritativeCheckoutComposition(
                resolved.PresetId,
                resolved.CommitmentTermId,
                resolved.PaymentMode,
                resolved.CommitmentMonths,
                resolved.DiscountBasisPoints,
                resolved.Items,
                LegacyOfferId: null,
                resolved.SelectionCanonical,
                BillingV2CheckoutSelectionFingerprint.ForSelection(
                    resolved.SelectionCanonical));
        }

        if (string.IsNullOrWhiteSpace(request.LegacyOfferId))
        {
            throw new InvalidOperationException(
                "BILLING_V2_CHECKOUT_SELECTION_REQUIRED");
        }

        var mapping = await ReadMappingAsync(
            connection,
            request.LegacyOfferId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "BILLING_V2_LEGACY_OFFER_MAPPING_NOT_FOUND");
        var presetItems = await ReadPresetItemsAsync(
            connection,
            mapping.PresetId,
            now,
            cancellationToken);
        if (presetItems.Count == 0)
        {
            throw new InvalidOperationException(
                "BILLING_V2_PRESET_HAS_NO_ITEMS");
        }

        return new BillingV2AuthoritativeCheckoutComposition(
            mapping.PresetId,
            mapping.CommitmentTermId,
            mapping.PaymentMode,
            mapping.CommitmentMonths,
            mapping.DiscountBasisPoints,
            presetItems,
            request.LegacyOfferId,
            $"{BillingV2CheckoutSelectionFingerprint.LegacyPrefix}{request.LegacyOfferId}",
            BillingV2CheckoutSelectionFingerprint.ForLegacyOffer(
                request.LegacyOfferId));
    }

    private BillingV2PricingResult CalculatePricing(
        BillingV2AuthoritativeCheckoutComposition mapping,
        IReadOnlyList<BillingV2NewSubscriptionPresetItem> presetItems,
        DateTime now)
        => _pricing.Calculate(new BillingV2PricingRequest(
            presetItems.Select(item => new BillingV2PricingItem(
                item.PresetItemId,
                item.ServiceCode,
                item.TierCode,
                item.PriceCode,
                item.AmountCents,
                item.Quantity,
                item.BillingCadence,
                item.DiscountEligible)).ToArray(),
            mapping.DiscountBasisPoints,
            mapping.PaymentMode,
            mapping.CommitmentMonths,
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            now));

    private static async Task<BillingV2AuthoritativeCheckoutMapping?>
        ReadMappingAsync(
            MySqlConnection connection,
            string legacyOfferId,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                mapping.preset_id,
                mapping.commitment_term_id,
                mapping.payment_mode,
                term.commitment_months,
                option_row.discount_basis_points
            FROM billing_v2_legacy_offer_mappings mapping
            INNER JOIN billing_v2_commitment_terms term
                ON term.id = mapping.commitment_term_id
               AND term.status = 'active'
            INNER JOIN billing_v2_commitment_payment_options option_row
                ON option_row.commitment_term_id = term.id
               AND option_row.payment_mode = mapping.payment_mode
               AND option_row.status = 'active'
            WHERE mapping.legacy_offer_id = @legacy_offer_id
              AND mapping.status = 'active'
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@legacy_offer_id", legacyOfferId);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2AuthoritativeCheckoutMapping(
            MariaDbIdentifierReader.ReadRequired(reader, "preset_id"),
            MariaDbIdentifierReader.ReadRequired(reader, "commitment_term_id"),
            reader.GetString("payment_mode"),
            reader.GetInt32("commitment_months"),
            reader.GetInt32("discount_basis_points"));
    }

    // Lecture deleguee au lecteur partage : meme requete et meme resolution
    // d'ambiguite de prix que BillingV2NewSubscriptionService, pour que les
    // deux chemins de creation ne puissent plus diverger.
    private static async Task<IReadOnlyList<BillingV2NewSubscriptionPresetItem>>
        ReadPresetItemsAsync(
            MySqlConnection connection,
            string presetId,
            DateTime now,
            CancellationToken cancellationToken)
        => await BillingV2PresetItemReader.ReadAsync(
            connection,
            transaction: null,
            presetId,
            now,
            cancellationToken);

    /// <summary>
    /// Retourne false quand l'unicite
    /// (customer_id, provider, environment, idempotency_key) est violee, c'est
    /// a dire quand une autre requete concurrente a ancre la meme cle. L'appelant
    /// annule alors sa transaction : rien de financier ne survit a la course.
    /// </summary>
    private static async Task<bool> TryInsertCheckoutRequestAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string requestId,
        PortalSessionContext session,
        BillingV2AuthoritativeCheckoutRequest request,
        BillingV2AuthoritativeCheckoutComposition composition,
        string provider,
        string environment,
        string requestFingerprintHash,
        string subscriptionId,
        string subscriptionChangeId,
        string billingEventId,
        CancellationToken cancellationToken)
    {
        try
        {
            await InsertCheckoutRequestAsync(
                connection,
                transaction,
                requestId,
                session,
                request,
                composition,
                provider,
                environment,
                requestFingerprintHash,
                subscriptionId,
                subscriptionChangeId,
                billingEventId,
                cancellationToken);
            return true;
        }
        catch (MySqlException exception)
            when (exception.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            return false;
        }
    }

    private static async Task InsertCheckoutRequestAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string requestId,
        PortalSessionContext session,
        BillingV2AuthoritativeCheckoutRequest request,
        BillingV2AuthoritativeCheckoutComposition composition,
        string provider,
        string environment,
        string requestFingerprintHash,
        string subscriptionId,
        string subscriptionChangeId,
        string billingEventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_authoritative_checkout_requests (
                id,
                customer_id,
                actor_reference,
                idempotency_key,
                request_fingerprint_hash,
                legacy_offer_id,
                selection_fingerprint,
                selection_canonical,
                provider,
                environment,
                subscription_id,
                subscription_change_id,
                billing_event_id,
                status,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @customer_id,
                @actor_reference,
                @idempotency_key,
                @request_fingerprint_hash,
                @legacy_offer_id,
                @selection_fingerprint,
                @selection_canonical,
                @provider,
                @environment,
                @subscription_id,
                @subscription_change_id,
                @billing_event_id,
                'pending',
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue(
            "@subscription_change_id",
            subscriptionChangeId);
        command.Parameters.AddWithValue("@billing_event_id", billingEventId);
        command.Parameters.AddWithValue("@id", requestId);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);
        command.Parameters.AddWithValue("@actor_reference", session.UserId);
        command.Parameters.AddWithValue(
            "@idempotency_key",
            request.IdempotencyKey);
        command.Parameters.AddWithValue(
            "@request_fingerprint_hash",
            requestFingerprintHash);
        command.Parameters.AddWithValue(
            "@legacy_offer_id",
            composition.LegacyOfferId is null
                ? DBNull.Value
                : composition.LegacyOfferId);
        command.Parameters.AddWithValue(
            "@selection_fingerprint",
            composition.SelectionFingerprint);
        command.Parameters.AddWithValue(
            "@selection_canonical",
            composition.SelectionCanonical);
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@environment", environment);
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Lecture de l'ancre d'idempotence. La cle est portee par la ligne de
    /// demande, et la ligne est unique sur exactement le quadruplet contraint
    /// en base : (customer_id, provider, environment, idempotency_key). Filtrer
    /// sur les quatre colonnes evite de confondre deux demandes qui partagent
    /// la cle mais pas le rail.
    /// </summary>
    private static async Task<BillingV2AuthoritativeCheckoutRequestRecord?>
        ReadCheckoutRequestByKeyAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string customerId,
            string provider,
            string environment,
            string idempotencyKey,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            SELECT
                {CheckoutRequestColumns}
            FROM billing_v2_authoritative_checkout_requests
            WHERE customer_id = @customer_id
              AND provider = @provider
              AND environment = @environment
              AND idempotency_key = @idempotency_key
            ORDER BY created_at ASC, id ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@environment", environment);
        command.Parameters.AddWithValue("@idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRequestRecord(reader)
            : null;
    }

    private static async Task<string?> ReadApprovalUrlAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string subscriptionId,
        string? idempotencyKeyHash,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKeyHash))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT approval_url
            FROM billing_v2_provider_checkout_sessions
            WHERE subscription_id = @subscription_id
              AND idempotency_key_hash = @idempotency_key_hash
              AND status = 'pending_approval'
              AND approval_url IS NOT NULL
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@idempotency_key_hash",
            idempotencyKeyHash);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string approvalUrl
            && !string.IsNullOrWhiteSpace(approvalUrl)
                ? approvalUrl
                : null;
    }

    private static async Task InsertSubscriptionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        string customerId,
        BillingV2AuthoritativeCheckoutComposition mapping,
        long? minimumCommitmentAmountCents,
        BillingV2SubscriptionLifecyclePlan lifecycle,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_subscriptions (
                id,
                customer_id,
                originating_preset_id,
                commitment_term_id,
                status,
                payment_mode,
                currency,
                discount_basis_points_snapshot,
                minimum_commitment_amount_cents,
                billing_model,
                commitment_started_at,
                commitment_ends_at,
                current_period_started_at,
                current_period_ends_at,
                renews_at,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @customer_id,
                @originating_preset_id,
                @commitment_term_id,
                'pending_approval',
                @payment_mode,
                'EUR',
                @discount_basis_points,
                @minimum_commitment_amount_cents,
                'v2',
                @commitment_started_at,
                @commitment_ends_at,
                @current_period_started_at,
                @current_period_ends_at,
                @renews_at,
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue(
            "@commitment_started_at",
            lifecycle.CommitmentStartedAtUtc);
        command.Parameters.AddWithValue(
            "@commitment_ends_at",
            lifecycle.CommitmentEndsAtUtc);
        command.Parameters.AddWithValue(
            "@current_period_started_at",
            lifecycle.CurrentPeriodStartedAtUtc);
        command.Parameters.AddWithValue(
            "@current_period_ends_at",
            lifecycle.CurrentPeriodEndsAtUtc);
        command.Parameters.AddWithValue(
            "@renews_at",
            lifecycle.RenewsAtUtc.HasValue
                ? lifecycle.RenewsAtUtc.Value
                : DBNull.Value);
        command.Parameters.AddWithValue("@id", subscriptionId);
        command.Parameters.AddWithValue("@customer_id", customerId);
        command.Parameters.AddWithValue(
            "@originating_preset_id",
            mapping.PresetId);
        command.Parameters.AddWithValue(
            "@commitment_term_id",
            mapping.CommitmentTermId);
        command.Parameters.AddWithValue("@payment_mode", mapping.PaymentMode);
        command.Parameters.AddWithValue(
            "@discount_basis_points",
            mapping.DiscountBasisPoints);
        command.Parameters.AddWithValue(
            "@minimum_commitment_amount_cents",
            minimumCommitmentAmountCents.HasValue
                ? minimumCommitmentAmountCents.Value
                : DBNull.Value);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertUserAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        BillingV2NewSubscriptionUserPlan user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_subscription_users (
                id,
                subscription_id,
                identity_reference,
                display_name,
                email,
                is_primary,
                status,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @subscription_id,
                @identity_reference,
                @display_name,
                @email,
                @is_primary,
                'active',
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue("@id", user.Id);
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@identity_reference",
            string.IsNullOrWhiteSpace(user.IdentityReference)
                ? DBNull.Value
                : user.IdentityReference);
        command.Parameters.AddWithValue("@display_name", user.DisplayName);
        command.Parameters.AddWithValue(
            "@email",
            string.IsNullOrWhiteSpace(user.Email) ? DBNull.Value : user.Email);
        command.Parameters.AddWithValue("@is_primary", user.IsPrimary);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertItemAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        BillingV2NewSubscriptionItemPlan item,
        BillingV2SubscriptionLifecyclePlan lifecycle,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_subscription_items (
                id,
                subscription_id,
                subscription_user_id,
                service_id,
                tier_id,
                service_price_id,
                scope_type,
                quantity,
                amount_cents_snapshot,
                currency,
                discount_eligible_snapshot,
                source,
                effective_from,
                effective_until,
                status,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @subscription_id,
                @subscription_user_id,
                @service_id,
                @tier_id,
                @service_price_id,
                @scope_type,
                @quantity,
                @amount_cents_snapshot,
                @currency,
                @discount_eligible_snapshot,
                @source,
                @effective_from,
                @effective_until,
                'active',
                @created_at,
                @updated_at
            );
            """;
        // Comptant : les droits sont bornes a la periode payee. Sans cette
        // borne, un contrat prepaye laissait des droits illimites apres son
        // terme, aucune projection ne le bornant par ailleurs.
        command.Parameters.AddWithValue(
            "@effective_until",
            lifecycle.RenewsAtUtc is null
                ? lifecycle.CommitmentEndsAtUtc
                : DBNull.Value);
        command.Parameters.AddWithValue("@id", item.Id);
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@subscription_user_id",
            item.UserId is null ? DBNull.Value : item.UserId);
        command.Parameters.AddWithValue("@service_id", item.ServiceId);
        command.Parameters.AddWithValue(
            "@tier_id",
            item.TierId is null ? DBNull.Value : item.TierId);
        command.Parameters.AddWithValue("@service_price_id", item.ServicePriceId);
        command.Parameters.AddWithValue("@scope_type", item.ScopeType);
        command.Parameters.AddWithValue("@quantity", item.Quantity);
        command.Parameters.AddWithValue(
            "@amount_cents_snapshot",
            item.AmountCentsSnapshot);
        command.Parameters.AddWithValue("@currency", item.Currency);
        command.Parameters.AddWithValue(
            "@discount_eligible_snapshot",
            item.DiscountEligibleSnapshot);
        command.Parameters.AddWithValue("@source", item.Source);
        command.Parameters.AddWithValue(
            "@effective_from",
            lifecycle.CommitmentStartedAtUtc);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertItemProvisioningAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2NewSubscriptionItemPlan item,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_subscription_item_provisioning (
                subscription_item_id,
                provisioned_tier_id,
                provisioned_quantity,
                provisioning_status,
                created_at,
                updated_at
            ) VALUES (
                @subscription_item_id,
                @provisioned_tier_id,
                @provisioned_quantity,
                'pending',
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue("@subscription_item_id", item.Id);
        command.Parameters.AddWithValue(
            "@provisioned_tier_id",
            item.TierId is null ? DBNull.Value : item.TierId);
        command.Parameters.AddWithValue(
            "@provisioned_quantity",
            item.Quantity);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertSubscriptionPriceLockAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        BillingV2SubscriptionPriceLockPlan priceLock,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_subscription_price_locks (
                id,
                subscription_id,
                lock_type,
                amount_cents,
                currency,
                effective_from,
                effective_until,
                source_legacy_offer_id,
                reason,
                status,
                created_at
            ) VALUES (
                @id,
                @subscription_id,
                @lock_type,
                @amount_cents,
                @currency,
                @effective_from,
                @effective_until,
                @source_legacy_offer_id,
                @reason,
                'active',
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue("@lock_type", priceLock.LockType);
        command.Parameters.AddWithValue("@amount_cents", priceLock.AmountCents);
        command.Parameters.AddWithValue("@currency", priceLock.Currency);
        command.Parameters.AddWithValue(
            "@effective_from",
            priceLock.EffectiveFromUtc);
        command.Parameters.AddWithValue(
            "@effective_until",
            priceLock.EffectiveUntilUtc);
        command.Parameters.AddWithValue(
            "@source_legacy_offer_id",
            priceLock.SourceLegacyOfferId is null
                ? DBNull.Value
                : priceLock.SourceLegacyOfferId);
        command.Parameters.AddWithValue("@reason", priceLock.Reason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOutboxEventAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string outboxEventId,
        BillingV2ProviderCheckoutCommandPlan plan,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_outbox_events (
                id,
                aggregate_type,
                aggregate_id,
                event_type,
                payload_text,
                idempotency_key_hash,
                status,
                retry_count,
                available_at,
                created_at
            ) VALUES (
                @id,
                @aggregate_type,
                @aggregate_id,
                @event_type,
                @payload_text,
                @idempotency_key_hash,
                'pending',
                0,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            )
            ON DUPLICATE KEY UPDATE
                id = id;
            """;
        command.Parameters.AddWithValue("@id", outboxEventId);
        command.Parameters.AddWithValue("@aggregate_type", plan.AggregateType);
        command.Parameters.AddWithValue("@aggregate_id", plan.AggregateId);
        command.Parameters.AddWithValue("@event_type", plan.EventType);
        command.Parameters.AddWithValue("@payload_text", plan.PayloadText);
        command.Parameters.AddWithValue(
            "@idempotency_key_hash",
            plan.IdempotencyKeyHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        string actorReference,
        string detailsText,
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
                'billing_v2.authoritative_checkout_requested',
                @actor_reference,
                @details_text,
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@entity_id", subscriptionId);
        command.Parameters.AddWithValue("@actor_reference", actorReference);
        command.Parameters.AddWithValue("@details_text", detailsText);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkCheckoutRequestQueuedAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string requestId,
        string outboxEventId,
        string idempotencyKeyHash,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_authoritative_checkout_requests
            SET status = 'queued',
                outbox_event_id = @outbox_event_id,
                idempotency_key_hash = @idempotency_key_hash,
                reason_code = @reason_code,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", requestId);
        command.Parameters.AddWithValue("@outbox_event_id", outboxEventId);
        command.Parameters.AddWithValue(
            "@idempotency_key_hash",
            idempotencyKeyHash);
        command.Parameters.AddWithValue("@reason_code", reasonCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Une cle d'idempotence appartient a UNE configuration. La rejouer avec
    /// une autre selection n'est pas un rejeu : c'est une seconde demande
    /// deguisee. On echoue en ferme plutot que d'ouvrir un second contrat.
    /// </summary>
    public const string IdempotencyReusedReasonCode =
        "BILLING_V2_IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_SELECTION";

    private static void EnsureSameSelection(
        BillingV2AuthoritativeCheckoutRequestRecord existing,
        string selectionFingerprint)
    {
        if (!BillingV2AuthoritativeCheckoutIdempotencyPolicy
            .MatchesRequestFingerprint(
                existing.SelectionFingerprint,
                selectionFingerprint))
        {
            throw new InvalidOperationException(IdempotencyReusedReasonCode);
        }
    }

    /// <summary>
    /// Reponse d'un rejeu reconstruite depuis la ligne de demande ancree. Le
    /// montant vient du BillingEvent deja fige, jamais d'un recalcul catalogue.
    /// </summary>
    private static async Task<BillingV2AuthoritativeCheckoutResult>
        BuildResultFromRequestAsync(
            MySqlConnection connection,
            BillingV2AuthoritativeCheckoutRequestRecord existing,
            CancellationToken cancellationToken)
    {
        var total = 0L;
        if (existing.BillingEventId is not null)
        {
            var billingEvent = await BillingV2FinancialCoreStore
                .ReadBillingEventAsync(
                    connection,
                    transaction: null,
                    existing.BillingEventId,
                    cancellationToken);
            total = billingEvent?.TotalAmountCents ?? 0;
        }

        var approvalUrl = await ReadApprovalUrlAsync(
            connection,
            transaction: null,
            existing.SubscriptionId,
            existing.IdempotencyKeyHash,
            cancellationToken);

        return new BillingV2AuthoritativeCheckoutResult(
            Created: false,
            existing.SubscriptionId,
            existing.Provider,
            existing.Environment,
            existing.OutboxEventId ?? string.Empty,
            existing.IdempotencyKeyHash ?? string.Empty,
            total,
            existing.ReasonCode
                ?? "BILLING_V2_AUTHORITATIVE_CHECKOUT_IDEMPOTENT_NOOP",
            approvalUrl);
    }

    private sealed record BillingV2AuthoritativeCheckoutMapping(
        string PresetId,
        string CommitmentTermId,
        string PaymentMode,
        int CommitmentMonths,
        int DiscountBasisPoints);

    private sealed record BillingV2AuthoritativeCheckoutRequestRecord(
        string Id,
        string SubscriptionId,
        string Provider,
        string Environment,
        string RequestFingerprintHash,
        string SelectionFingerprint,
        string? BillingEventId,
        string? OutboxEventId,
        string? IdempotencyKeyHash,
        string? ReasonCode);
}

public static class BillingV2AuthoritativeCheckoutIdempotencyPolicy
{
    public static string ComputeRequestFingerprintHash(
        string customerId,
        string actorReference,
        string provider,
        string environment,
        string selectionFingerprint)
    {
        var fingerprint = string.Join(
            "|",
            customerId,
            provider,
            environment,
            selectionFingerprint,
            actorReference);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool MatchesRequestFingerprint(
        string existingFingerprintHash,
        string requestFingerprintHash)
        => string.Equals(
            existingFingerprintHash,
            requestFingerprintHash,
            StringComparison.OrdinalIgnoreCase);
}

public static class BillingV2AuthoritativeCheckoutPriceLockPolicy
{
    public const string CheckoutReason = "v2_authoritative_checkout";

    public static BillingV2SubscriptionPriceLockPlan Plan(
        string? sourceLegacyOfferId,
        string paymentMode,
        int commitmentMonths,
        BillingV2PricingResult pricing,
        DateTime nowUtc)
    {
        var months = Math.Max(1, commitmentMonths);
        if (string.Equals(
                paymentMode,
                BillingV2PaymentModes.Upfront,
                StringComparison.Ordinal))
        {
            return new BillingV2SubscriptionPriceLockPlan(
                BillingV2PriceLockTypes.UpfrontPrepaid,
                pricing.UpfrontRecurringAmountCents,
                "EUR",
                nowUtc,
                nowUtc.AddMonths(months),
                sourceLegacyOfferId,
                CheckoutReason);
        }

        return new BillingV2SubscriptionPriceLockPlan(
            BillingV2PriceLockTypes.MonthlyRecurring,
            pricing.PayableRecurringAmountCents,
            "EUR",
            nowUtc,
            nowUtc.AddMonths(months),
            sourceLegacyOfferId,
            CheckoutReason);
    }
}

public static class BillingV2AuthoritativeCheckoutGate
{
    public static BillingV2AuthoritativeCheckoutReadiness Evaluate(
        BillingV2RuntimeConfiguration runtime,
        bool persistentSqlAvailable,
        string? idempotencyKey)
    {
        if (!runtime.NewSubscriptionsEnabled)
        {
            return Blocked("BILLING_V2_NEW_SUBSCRIPTIONS_FLAG_OFF");
        }

        if (!runtime.AuthoritativeCheckoutEnabled)
        {
            return Blocked("BILLING_V2_AUTHORITATIVE_CHECKOUT_FLAG_OFF");
        }

        if (!runtime.FirstRealSubscriptionApproved)
        {
            return Blocked(
                "BILLING_V2_FIRST_REAL_SUBSCRIPTION_NOT_APPROVED");
        }

        if (!persistentSqlAvailable)
        {
            return Blocked("BILLING_V2_AUTHORITATIVE_CHECKOUT_NO_SQL");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 128)
        {
            return Blocked(
                "BILLING_V2_AUTHORITATIVE_CHECKOUT_IDEMPOTENCY_REQUIRED");
        }

        return new BillingV2AuthoritativeCheckoutReadiness(
            Authorized: true,
            "BILLING_V2_AUTHORITATIVE_CHECKOUT_LOCALLY_READY");
    }

    private static BillingV2AuthoritativeCheckoutReadiness Blocked(
        string reasonCode)
        => new(Authorized: false, reasonCode);
}
