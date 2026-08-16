using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2RuntimeConfiguration(
    bool CatalogShadowModeEnabled,
    bool ProvisioningShadowModeEnabled,
    bool NewSubscriptionsEnabled,
    bool AuthoritativeCheckoutEnabled,
    bool FirstRealSubscriptionApproved,
    bool ProviderOutboxEnabled,
    bool ProviderExecutorEnabled,
    bool ProvisioningEnabled,
    // Phase 3. Le reconciliateur est le seul composant Billing V2 capable
    // d'appeler Stripe SANS action utilisateur : il reste donc OFF par defaut
    // et s'active explicitement.
    bool ReconciliationWorkerEnabled = false,
    int ReconciliationIntervalSeconds =
        BillingV2RuntimeConfiguration.DefaultReconciliationIntervalSeconds)
{
    public const int DefaultReconciliationIntervalSeconds = 300;
    public const int MinimumReconciliationIntervalSeconds = 30;

    public static BillingV2RuntimeConfiguration Resolve(
        IConfiguration configuration)
        => ResolveCore(configuration) with
        {
            ReconciliationWorkerEnabled = string.Equals(
                configuration["BILLING_V2_RECONCILIATION_WORKER_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            ReconciliationIntervalSeconds = ResolveInterval(
                configuration["BILLING_V2_RECONCILIATION_INTERVAL_SECONDS"])
        };

    /// <summary>
    /// Une fréquence absente, illisible ou trop agressive retombe sur la
    /// valeur par defaut plutot que de marteler l'API Stripe.
    /// </summary>
    public static int ResolveInterval(string? rawValue)
        => int.TryParse(rawValue, out var seconds)
           && seconds >= MinimumReconciliationIntervalSeconds
            ? seconds
            : DefaultReconciliationIntervalSeconds;
    private static BillingV2RuntimeConfiguration ResolveCore(
        IConfiguration configuration)
        => new(
            string.Equals(
                configuration["BILLING_V2_CATALOG_SHADOW_MODE"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_PROVISIONING_SHADOW_MODE"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_PROVIDER_OUTBOX_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_PROVIDER_EXECUTOR_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_PROVISIONING_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase));
}

public sealed record BillingCatalogResolvedOffer(
    CommercialOfferSummary Offer,
    int PriceAmountCents,
    int SetupFeeAmountCents,
    int BillingIntervalMonths,
    int CommitmentMonths,
    string PaymentMode,
    string? StripePriceId,
    string? PayPalPlanId,
    string? ProviderExternalId);

public interface IBillingCatalog
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<CommercialOfferSummary>> GetClientCatalogAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CommercialOfferSummary>> GetAdminCatalogAsync(
        CancellationToken cancellationToken);

    Task<CommercialOfferSummary?> FindClientOfferByIdAsync(
        string offerId,
        CancellationToken cancellationToken);

    Task<BillingCatalogResolvedOffer> ResolveSubscribableOfferAsync(
        string offerId,
        string rail,
        CancellationToken cancellationToken);

    string? ResolveProviderExternalId(CommercialOfferSummary offer, string rail);
}

public sealed class LegacyBillingCatalogAdapter : IBillingCatalog
{
    private readonly ICommercialRepository _commercialRepository;
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly StripeRuntimeConfiguration _stripe;

    public LegacyBillingCatalogAdapter(
        ICommercialRepository commercialRepository,
        PayPalRuntimeConfiguration paypal,
        StripeRuntimeConfiguration stripe)
    {
        _commercialRepository = commercialRepository;
        _paypal = paypal;
        _stripe = stripe;
    }

    public bool IsPersistent => _commercialRepository.IsPersistent;

    public Task<IReadOnlyList<CommercialOfferSummary>> GetClientCatalogAsync(
        CancellationToken cancellationToken)
        => _commercialRepository.GetClientCatalogAsync(cancellationToken);

    public Task<IReadOnlyList<CommercialOfferSummary>> GetAdminCatalogAsync(
        CancellationToken cancellationToken)
        => _commercialRepository.GetAdminCatalogAsync(cancellationToken);

    public async Task<CommercialOfferSummary?> FindClientOfferByIdAsync(
        string offerId,
        CancellationToken cancellationToken)
    {
        var catalog = await GetClientCatalogAsync(cancellationToken);
        return catalog.FirstOrDefault(candidate => string.Equals(
            candidate.Id,
            offerId,
            StringComparison.Ordinal));
    }

    public async Task<BillingCatalogResolvedOffer> ResolveSubscribableOfferAsync(
        string offerId,
        string rail,
        CancellationToken cancellationToken)
    {
        var offer = await FindClientOfferByIdAsync(offerId, cancellationToken)
            ?? throw new PortalDataNotFoundException();
        var providerExternalId = ResolveProviderExternalId(offer, rail);

        if (!string.Equals(
                offer.BillingCadence,
                CommercialStatuses.CadenceMonthly,
                StringComparison.Ordinal)
            || offer.PriceAmountCents <= 0)
        {
            throw new PortalValidationException();
        }

        if (!string.Equals(rail, "billing", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(providerExternalId))
        {
            throw new PortalValidationException();
        }

        return new BillingCatalogResolvedOffer(
            offer,
            offer.PriceAmountCents,
            offer.SetupFeeAmountCents ?? 0,
            offer.BillingIntervalMonths ?? 1,
            offer.CommitmentMonths ?? offer.BillingIntervalMonths ?? 1,
            offer.PaymentMode ?? CommercialStatuses.PaymentModeMonthly,
            _stripe.IsLive ? offer.StripePriceIdLive : offer.StripePriceIdTest,
            _paypal.IsLive ? offer.PayPalPlanIdLive : offer.PayPalPlanIdSandbox,
            string.Equals(rail, "billing", StringComparison.Ordinal)
                ? string.Empty
                : providerExternalId);
    }

    public string? ResolveProviderExternalId(
        CommercialOfferSummary offer,
        string rail)
        => string.Equals(rail, "stripe", StringComparison.Ordinal)
            ? (_stripe.IsLive
                ? offer.StripePriceIdLive
                : offer.StripePriceIdTest)
            : string.Equals(rail, "billing", StringComparison.Ordinal)
                ? string.Empty
                : (_paypal.IsLive
                    ? offer.PayPalPlanIdLive
                    : offer.PayPalPlanIdSandbox);
}

public sealed class ShadowBillingCatalogAdapter : IBillingCatalog
{
    private readonly IBillingCatalog _legacy;
    private readonly IBillingCatalog _v2;
    private readonly BillingV2RuntimeConfiguration _configuration;
    private readonly ILogger<ShadowBillingCatalogAdapter> _logger;

    public ShadowBillingCatalogAdapter(
        LegacyBillingCatalogAdapter legacy,
        IBillingCatalog v2,
        BillingV2RuntimeConfiguration configuration,
        ILogger<ShadowBillingCatalogAdapter> logger)
    {
        _legacy = legacy;
        _v2 = v2;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsPersistent => _legacy.IsPersistent;

    public async Task<IReadOnlyList<CommercialOfferSummary>> GetClientCatalogAsync(
        CancellationToken cancellationToken)
    {
        var legacy = await _legacy.GetClientCatalogAsync(cancellationToken);
        await CompareCatalogAsync(legacy, clientOnly: true, cancellationToken);
        return legacy;
    }

    public async Task<IReadOnlyList<CommercialOfferSummary>> GetAdminCatalogAsync(
        CancellationToken cancellationToken)
    {
        var legacy = await _legacy.GetAdminCatalogAsync(cancellationToken);
        await CompareCatalogAsync(legacy, clientOnly: false, cancellationToken);
        return legacy;
    }

    public async Task<CommercialOfferSummary?> FindClientOfferByIdAsync(
        string offerId,
        CancellationToken cancellationToken)
    {
        var legacy = await _legacy.FindClientOfferByIdAsync(
            offerId,
            cancellationToken);
        if (legacy is not null)
        {
            await CompareOfferAsync(legacy, cancellationToken);
        }

        return legacy;
    }

    public async Task<BillingCatalogResolvedOffer> ResolveSubscribableOfferAsync(
        string offerId,
        string rail,
        CancellationToken cancellationToken)
    {
        var legacy = await _legacy.ResolveSubscribableOfferAsync(
            offerId,
            rail,
            cancellationToken);
        await CompareResolvedOfferAsync(legacy, cancellationToken);
        return legacy;
    }

    public string? ResolveProviderExternalId(
        CommercialOfferSummary offer,
        string rail)
        => _legacy.ResolveProviderExternalId(offer, rail);

    private bool CanRunShadow => _configuration.CatalogShadowModeEnabled
        && _v2.IsPersistent;

    private async Task CompareCatalogAsync(
        IReadOnlyList<CommercialOfferSummary> legacy,
        bool clientOnly,
        CancellationToken cancellationToken)
    {
        if (!CanRunShadow)
        {
            return;
        }

        try
        {
            var v2 = clientOnly
                ? await _v2.GetClientCatalogAsync(cancellationToken)
                : await _v2.GetAdminCatalogAsync(cancellationToken);
            var legacyPublicPacks = legacy
                .Where(offer => IsPublicPack(offer.ExternalReference))
                .ToDictionary(
                    offer => offer.Id,
                    StringComparer.OrdinalIgnoreCase);
            var v2ByLegacyId = v2.ToDictionary(
                offer => offer.Id,
                StringComparer.OrdinalIgnoreCase);

            foreach (var (offerId, legacyOffer) in legacyPublicPacks)
            {
                if (!v2ByLegacyId.TryGetValue(offerId, out var v2Offer))
                {
                    LogMismatch(
                        offerId,
                        legacyOffer.ExternalReference,
                        "missing-v2-offer");
                    continue;
                }

                CompareOfferFields(legacyOffer, v2Offer);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 shadow catalog comparison failed.");
        }
    }

    private async Task CompareOfferAsync(
        CommercialOfferSummary legacy,
        CancellationToken cancellationToken)
    {
        if (!CanRunShadow || !IsPublicPack(legacy.ExternalReference))
        {
            return;
        }

        try
        {
            var v2 = await _v2.FindClientOfferByIdAsync(
                legacy.Id,
                cancellationToken);
            if (v2 is null)
            {
                LogMismatch(
                    legacy.Id,
                    legacy.ExternalReference,
                    "missing-v2-offer");
                return;
            }

            CompareOfferFields(legacy, v2);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 shadow offer comparison failed for legacy offer {OfferId}.",
                legacy.Id);
        }
    }

    private async Task CompareResolvedOfferAsync(
        BillingCatalogResolvedOffer legacy,
        CancellationToken cancellationToken)
    {
        if (!CanRunShadow || !IsPublicPack(legacy.Offer.ExternalReference))
        {
            return;
        }

        try
        {
            var v2 = await _v2.ResolveSubscribableOfferAsync(
                legacy.Offer.Id,
                "billing",
                cancellationToken);
            if (legacy.PriceAmountCents != v2.PriceAmountCents
                || legacy.SetupFeeAmountCents != v2.SetupFeeAmountCents
                || legacy.BillingIntervalMonths != v2.BillingIntervalMonths
                || legacy.CommitmentMonths != v2.CommitmentMonths
                || !string.Equals(
                    legacy.PaymentMode,
                    v2.PaymentMode,
                    StringComparison.Ordinal))
            {
                LogMismatch(
                    legacy.Offer.Id,
                    legacy.Offer.ExternalReference,
                    "resolved-offer");
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 shadow resolved-offer comparison failed for legacy offer {OfferId}.",
                legacy.Offer.Id);
        }
    }

    private void CompareOfferFields(
        CommercialOfferSummary legacy,
        CommercialOfferSummary v2)
    {
        if (legacy.PriceAmountCents != v2.PriceAmountCents
            || (legacy.SetupFeeAmountCents ?? 0) != (v2.SetupFeeAmountCents ?? 0)
            || (legacy.BillingIntervalMonths ?? 1) != (v2.BillingIntervalMonths ?? 1)
            || (legacy.CommitmentMonths ?? legacy.BillingIntervalMonths ?? 1)
                != (v2.CommitmentMonths ?? v2.BillingIntervalMonths ?? 1)
            || !string.Equals(
                legacy.PaymentMode ?? CommercialStatuses.PaymentModeMonthly,
                v2.PaymentMode ?? CommercialStatuses.PaymentModeMonthly,
                StringComparison.Ordinal))
        {
            LogMismatch(
                legacy.Id,
                legacy.ExternalReference,
                "catalog-fields");
        }
    }

    private void LogMismatch(
        string offerId,
        string? externalReference,
        string mismatchKind)
        => _logger.LogWarning(
            "Billing V2 shadow catalog mismatch {MismatchKind} for legacy offer {OfferId} ({ExternalReference}). Legacy result remains authoritative.",
            mismatchKind,
            offerId,
            externalReference);

    private static bool IsPublicPack(string? externalReference)
        => !string.IsNullOrWhiteSpace(externalReference)
            && externalReference.StartsWith(
                "PACK-",
                StringComparison.OrdinalIgnoreCase);
}

public sealed class V2BillingCatalogAdapter : IBillingCatalog
{
    private static readonly IFiscalPolicy FiscalPolicy = new FiscalPolicy();

    private readonly SqlRuntimeConfiguration _configuration;
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly StripeRuntimeConfiguration _stripe;

    public V2BillingCatalogAdapter(
        SqlRuntimeConfiguration configuration,
        PayPalRuntimeConfiguration paypal,
        StripeRuntimeConfiguration stripe)
    {
        _configuration = configuration;
        _paypal = paypal;
        _stripe = stripe;
    }

    public bool IsPersistent => _configuration.IsPersistent
        && !string.IsNullOrWhiteSpace(_configuration.ConnectionString);

    public Task<IReadOnlyList<CommercialOfferSummary>> GetClientCatalogAsync(
        CancellationToken cancellationToken)
        => LoadCatalogAsync(activeOnly: true, cancellationToken);

    public Task<IReadOnlyList<CommercialOfferSummary>> GetAdminCatalogAsync(
        CancellationToken cancellationToken)
        => LoadCatalogAsync(activeOnly: false, cancellationToken);

    public async Task<CommercialOfferSummary?> FindClientOfferByIdAsync(
        string offerId,
        CancellationToken cancellationToken)
    {
        var catalog = await GetClientCatalogAsync(cancellationToken);
        return catalog.FirstOrDefault(candidate => string.Equals(
            candidate.Id,
            offerId,
            StringComparison.Ordinal));
    }

    public async Task<BillingCatalogResolvedOffer> ResolveSubscribableOfferAsync(
        string offerId,
        string rail,
        CancellationToken cancellationToken)
    {
        var offer = await FindClientOfferByIdAsync(offerId, cancellationToken)
            ?? throw new PortalDataNotFoundException();
        var providerExternalId = ResolveProviderExternalId(offer, rail);

        if (!string.Equals(
                offer.BillingCadence,
                CommercialStatuses.CadenceMonthly,
                StringComparison.Ordinal)
            || offer.PriceAmountCents <= 0)
        {
            throw new PortalValidationException();
        }

        if (!string.Equals(rail, "billing", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(providerExternalId))
        {
            throw new PortalValidationException();
        }

        return new BillingCatalogResolvedOffer(
            offer,
            offer.PriceAmountCents,
            offer.SetupFeeAmountCents ?? 0,
            offer.BillingIntervalMonths ?? 1,
            offer.CommitmentMonths ?? offer.BillingIntervalMonths ?? 1,
            offer.PaymentMode ?? CommercialStatuses.PaymentModeMonthly,
            _stripe.IsLive ? offer.StripePriceIdLive : offer.StripePriceIdTest,
            _paypal.IsLive ? offer.PayPalPlanIdLive : offer.PayPalPlanIdSandbox,
            string.Equals(rail, "billing", StringComparison.Ordinal)
                ? string.Empty
                : providerExternalId);
    }

    public string? ResolveProviderExternalId(
        CommercialOfferSummary offer,
        string rail)
        => string.Equals(rail, "stripe", StringComparison.Ordinal)
            ? (_stripe.IsLive
                ? offer.StripePriceIdLive
                : offer.StripePriceIdTest)
            : string.Equals(rail, "billing", StringComparison.Ordinal)
                ? string.Empty
                : (_paypal.IsLive
                    ? offer.PayPalPlanIdLive
                    : offer.PayPalPlanIdSandbox);

    private async Task<IReadOnlyList<CommercialOfferSummary>> LoadCatalogAsync(
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        if (!IsPersistent)
        {
            return Array.Empty<CommercialOfferSummary>();
        }

        var offers = new List<CommercialOfferSummary>();
        var setupFeeAmountCents = await LoadSetupFeeAmountCentsAsync(
            cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                m.legacy_offer_id,
                m.legacy_external_reference,
                m.payment_mode,
                m.status AS mapping_status,
                p.code AS preset_code,
                p.name AS preset_name,
                p.description AS preset_description,
                p.status AS preset_status,
                p.display_order,
                t.commitment_months,
                o.discount_basis_points,
                COALESCE(SUM(
                    CASE
                        WHEN sv.billing_type = 'recurring'
                        THEN sp.amount_cents * pi.quantity
                        ELSE 0
                    END
                ), 0) AS recurring_subtotal_cents
            FROM billing_v2_legacy_offer_mappings m
            JOIN billing_v2_offer_presets p
              ON p.id = m.preset_id
            JOIN billing_v2_commitment_terms t
              ON t.id = m.commitment_term_id
            JOIN billing_v2_commitment_payment_options o
              ON o.commitment_term_id = t.id
             AND o.payment_mode = m.payment_mode
             AND o.status = 'active'
            JOIN billing_v2_preset_items pi
              ON pi.preset_id = p.id
            JOIN billing_v2_services sv
              ON sv.id = pi.service_id
            LEFT JOIN billing_v2_service_prices sp
              ON sp.service_id = pi.service_id
             AND (
                    (sp.tier_id IS NULL AND pi.tier_id IS NULL)
                    OR sp.tier_id = pi.tier_id
                 )
             AND sp.currency = 'EUR'
             AND sp.status = 'active'
             AND sp.billing_cadence = 'monthly'
             AND sp.valid_from <= UTC_TIMESTAMP(6)
             AND (sp.valid_until IS NULL OR sp.valid_until > UTC_TIMESTAMP(6))
            WHERE (@active_only = 0 OR (m.status = 'active' AND p.status = 'active'))
            GROUP BY
                m.legacy_offer_id,
                m.legacy_external_reference,
                m.payment_mode,
                m.status,
                p.code,
                p.name,
                p.description,
                p.status,
                p.display_order,
                t.commitment_months,
                o.discount_basis_points
            ORDER BY p.display_order, t.commitment_months, m.payment_mode;
            """;
        command.Parameters.AddWithValue("@active_only", activeOnly ? 1 : 0);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            offers.Add(ReadOffer(reader, setupFeeAmountCents));
        }

        return offers;
    }

    private async Task<int> LoadSetupFeeAmountCentsAsync(
        CancellationToken cancellationToken)
    {
        if (!IsPersistent)
        {
            return 0;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT sp.amount_cents
            FROM billing_v2_service_prices sp
            JOIN billing_v2_services s
              ON s.id = sp.service_id
            WHERE s.code = 'INIT-SERVICE'
              AND sp.billing_cadence = 'one_time'
              AND sp.currency = 'EUR'
              AND sp.status = 'active'
              AND sp.valid_from <= UTC_TIMESTAMP(6)
              AND (sp.valid_until IS NULL OR sp.valid_until > UTC_TIMESTAMP(6))
            ORDER BY sp.valid_from DESC, sp.price_version DESC
            LIMIT 1;
            """;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value is DBNull ? 0 : Convert.ToInt32(value);
    }

    private static CommercialOfferSummary ReadOffer(
        MySqlDataReader reader,
        int setupFeeAmountCents)
    {
        var subtotal = Convert.ToInt32(reader["recurring_subtotal_cents"]);
        var discountBasisPoints = Convert.ToInt32(
            reader["discount_basis_points"]);
        var commitmentMonths = Convert.ToInt32(reader["commitment_months"]);
        var paymentMode = Convert.ToString(reader["payment_mode"])
            ?? CommercialStatuses.PaymentModeMonthly;
        var priceAmountCents = CalculatePriceAmountCents(
            subtotal,
            discountBasisPoints,
            commitmentMonths,
            paymentMode);
        var fiscal = FiscalPolicy.Resolve(null);
        var now = DateTime.UtcNow.ToString("O");

        return new CommercialOfferSummary(
            Convert.ToString(reader["legacy_offer_id"]) ?? string.Empty,
            Convert.ToString(reader["preset_name"]) ?? string.Empty,
            Convert.ToString(reader["preset_description"]) ?? string.Empty,
            "Abonnements",
            string.Equals(
                paymentMode,
                "upfront",
                StringComparison.Ordinal)
                ? "forfait"
                : "mois",
            "ht",
            priceAmountCents,
            "EUR",
            fiscal.TaxRateBasisPoints,
            fiscal.FiscalRegime,
            fiscal.FiscalMention,
            Convert.ToString(reader["legacy_external_reference"]),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Convert.ToString(reader["mapping_status"])
                ?? CommercialStatuses.OfferActive,
            Convert.ToInt32(reader["display_order"]),
            CommercialStatuses.CadenceMonthly,
            setupFeeAmountCents,
            1,
            commitmentMonths,
            paymentMode,
            Convert.ToString(reader["preset_code"]),
            null,
            null,
            null,
            null,
            now,
            now);
    }

    private static int CalculatePriceAmountCents(
        int recurringSubtotalCents,
        int discountBasisPoints,
        int commitmentMonths,
        string paymentMode)
    {
        var baseAmount = string.Equals(
            paymentMode,
            "upfront",
            StringComparison.Ordinal)
            ? recurringSubtotalCents * commitmentMonths
            : recurringSubtotalCents;

        return (int)((baseAmount * (10000L - discountBasisPoints) + 5000L)
            / 10000L);
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_configuration.ConnectionString!);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
