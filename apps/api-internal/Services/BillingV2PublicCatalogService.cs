using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface IBillingV2PublicCatalogService
{
    Task<BillingV2PublicCatalogSnapshot> GetCatalogAsync(
        CancellationToken cancellationToken);

    Task<BillingV2PublicQuote> QuoteAsync(
        BillingV2PublicSelection selection,
        CancellationToken cancellationToken);
}

/// <summary>
/// Projection publique du catalogue V2 et calcul de devis.
///
/// Lecture seule stricte. Le devis passe par BillingV2PricingEngine, le meme
/// moteur que le checkout authoritative : une projection affichee ne peut donc
/// pas diverger de ce qui sera reellement facture, et le navigateur n'envoie
/// jamais de montant.
/// </summary>
public sealed class BillingV2PublicCatalogService : IBillingV2PublicCatalogService
{
    private static readonly string[] RequiredTables =
    [
        "billing_v2_offer_presets",
        "billing_v2_preset_items",
        "billing_v2_services",
        "billing_v2_service_tiers",
        "billing_v2_service_prices",
        "billing_v2_commitment_terms",
        "billing_v2_commitment_payment_options"
    ];

    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly IBillingV2PricingEngine _pricing;
    private readonly ILogger<BillingV2PublicCatalogService> _logger;

    public BillingV2PublicCatalogService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration runtime,
        IBillingV2PricingEngine pricing,
        ILogger<BillingV2PublicCatalogService> logger)
    {
        _sql = sql;
        _runtime = runtime;
        _pricing = pricing;
        _logger = logger;
    }

    public async Task<BillingV2PublicCatalogSnapshot> GetCatalogAsync(
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return BillingV2PublicCatalogSeed.Snapshot();
        }

        try
        {
            await using var connection = new MySqlConnection(
                _sql.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Precondition de schema verifiee EN LECTURE SEULE : le compte
            // applicatif n'applique jamais de DDL, et une base encore en amont
            // de la migration 047 doit degrader proprement au lieu de renvoyer
            // SQL_UNAVAILABLE sur une page publique.
            if (!await SchemaIsReadyAsync(connection, cancellationToken))
            {
                _logger.LogInformation(
                    "Catalogue Billing V2 absent du schema : repli sur le seed.");
                return BillingV2PublicCatalogSeed.Snapshot();
            }

            var now = DateTime.UtcNow;
            var services = await ReadServicesAsync(
                connection,
                now,
                cancellationToken);
            var presets = await ReadPresetsAsync(
                connection,
                services,
                cancellationToken);
            var commitments = await ReadCommitmentsAsync(
                connection,
                cancellationToken);

            if (presets.Count == 0 || services.Count == 0)
            {
                return BillingV2PublicCatalogSeed.Snapshot();
            }

            return new BillingV2PublicCatalogSnapshot(
                BillingV2PublicCatalogSeed.DatabaseSourceName,
                BillingV2PublicCatalogSeed.Currency,
                presets,
                services,
                commitments);
        }
        catch (MySqlException exception)
        {
            _logger.LogWarning(
                exception,
                "Lecture du catalogue Billing V2 indisponible : repli sur le seed.");
            return BillingV2PublicCatalogSeed.Snapshot();
        }
    }

    public async Task<BillingV2PublicQuote> QuoteAsync(
        BillingV2PublicSelection selection,
        CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(cancellationToken);
        return BillingV2PublicQuoteBuilder.Build(
            catalog,
            selection,
            _pricing,
            BillingV2AuthoritativeCheckoutGate.Evaluate(
                _runtime,
                _sql.IsPersistent
                    && !string.IsNullOrWhiteSpace(_sql.ConnectionString),
                "billing-v2-public-quote-probe"));
    }

    private static async Task<bool> SchemaIsReadyAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN (
                  'billing_v2_offer_presets',
                  'billing_v2_preset_items',
                  'billing_v2_services',
                  'billing_v2_service_tiers',
                  'billing_v2_service_prices',
                  'billing_v2_commitment_terms',
                  'billing_v2_commitment_payment_options');
            """;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value) == RequiredTables.Length;
    }

    private static async Task<IReadOnlyList<BillingV2PublicService>>
        ReadServicesAsync(
            MySqlConnection connection,
            DateTime now,
            CancellationToken cancellationToken)
    {
        var rows = new List<ServiceRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT
                    service.code AS service_code,
                    service.name AS service_name,
                    service.category,
                    service.billing_type,
                    service.default_scope_type,
                    service.discount_eligible,
                    service.public_visible,
                    service.self_service_orderable,
                    service.display_order,
                    tier.code AS tier_code,
                    tier.public_label,
                    tier.description AS tier_description,
                    tier.numeric_value,
                    tier.public_selectable,
                    tier.display_order AS tier_display_order,
                    price.price_code,
                    price.price_version,
                    price.amount_cents,
                    price.currency,
                    price.billing_cadence,
                    price.charge_trigger,
                    price.valid_from,
                    price.id AS service_price_id
                FROM billing_v2_services service
                LEFT JOIN billing_v2_service_tiers tier
                    ON tier.service_id = service.id
                   AND tier.status = 'active'
                INNER JOIN billing_v2_service_prices price
                    ON price.service_id = service.id
                   AND price.tier_id <=> tier.id
                   AND price.status = 'active'
                   AND price.charge_trigger = @charge_trigger
                   AND price.valid_from <= @now
                   AND (price.valid_until IS NULL OR price.valid_until > @now)
                WHERE service.status = 'active'
                  AND service.public_visible = 1
                ORDER BY service.display_order,
                         service.code,
                         tier.display_order,
                         price.billing_cadence,
                         price.price_version DESC,
                         price.valid_from DESC;
                """;
            command.Parameters.AddWithValue("@now", now);
            command.Parameters.AddWithValue(
                "@charge_trigger",
                BillingV2ComponentizedPricingPolicy.InitialSubscription);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadServiceRow(reader));
            }
        }

        // Le socle n'est pas public_selectable en base (il est impose), mais la
        // projection publique doit quand meme l'afficher et le facturer.
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT
                    service.code AS service_code,
                    service.name AS service_name,
                    service.category,
                    service.billing_type,
                    service.default_scope_type,
                    service.discount_eligible,
                    service.public_visible,
                    service.self_service_orderable,
                    service.display_order,
                    NULL AS tier_code,
                    NULL AS public_label,
                    NULL AS tier_description,
                    NULL AS numeric_value,
                    0 AS public_selectable,
                    0 AS tier_display_order,
                    price.price_code,
                    price.price_version,
                    price.amount_cents,
                    price.currency,
                    price.billing_cadence,
                    price.charge_trigger,
                    price.valid_from,
                    price.id AS service_price_id
                FROM billing_v2_services service
                INNER JOIN billing_v2_service_prices price
                    ON price.service_id = service.id
                   AND price.tier_id IS NULL
                   AND price.status = 'active'
                   AND price.charge_trigger = @charge_trigger
                   AND price.valid_from <= @now
                   AND (price.valid_until IS NULL OR price.valid_until > @now)
                WHERE service.status = 'active'
                  AND service.code = 'BASE-SERVICE'
                ORDER BY price.billing_cadence,
                         price.price_version DESC,
                         price.valid_from DESC;
                """;
            command.Parameters.AddWithValue("@now", now);
            command.Parameters.AddWithValue(
                "@charge_trigger",
                BillingV2ComponentizedPricingPolicy.InitialSubscription);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadServiceRow(reader));
            }
        }

        return BuildServices(rows);
    }

    /// <summary>
    /// Assemble la projection publique a partir des lignes de prix.
    ///
    /// Un meme (service, palier) peut porter plusieurs composantes tarifaires
    /// simultanees — mensuel et frais de mise en service, par exemple. Chaque
    /// cadence est resolue independamment, avec la meme politique d'ambiguite
    /// que le resolver authoritative : `billing_type` n'intervient pas, il
    /// reste une metadonnee commerciale.
    /// </summary>
    private static IReadOnlyList<BillingV2PublicService> BuildServices(
        IReadOnlyList<ServiceRow> rows)
    {
        var services = new List<BillingV2PublicService>();
        foreach (var serviceGroup in rows
                     .GroupBy(row => row.ServiceCode, StringComparer.Ordinal)
                     .OrderBy(group => group.First().DisplayOrder))
        {
            var first = serviceGroup.First();
            var tiers = new List<BillingV2PublicTier>();
            IReadOnlyList<BillingV2PublicPriceComponent> flatComponents = [];

            foreach (var tierGroup in serviceGroup
                         .GroupBy(row => row.TierCode ?? string.Empty,
                             StringComparer.Ordinal))
            {
                var tierFirst = tierGroup.First();
                var components = ResolveComponents(
                    tierGroup,
                    first.ServiceCode,
                    tierFirst.TierCode,
                    first.DiscountEligible);
                if (components.Count == 0)
                {
                    // Une ambiguite de prix ne doit pas rendre la page
                    // publique fausse : le palier concerne disparait de
                    // l'offre au lieu d'afficher un montant arbitraire.
                    continue;
                }

                if (tierFirst.TierCode is null)
                {
                    flatComponents = components;
                    continue;
                }

                tiers.Add(new BillingV2PublicTier(
                    tierFirst.TierCode,
                    tierFirst.TierLabel ?? tierFirst.TierCode,
                    tierFirst.TierDescription,
                    tierFirst.NumericValue,
                    MonthlyAmountOf(components),
                    tierFirst.PublicSelectable,
                    components));
            }

            services.Add(new BillingV2PublicService(
                first.ServiceCode,
                first.ServiceName,
                first.Category,
                first.ScopeType,
                // Conserve pour la compatibilite des projections d'affichage.
                // Il vaut 0 quand le service n'a aucune composante mensuelle,
                // et n'est jamais l'autorite du calcul.
                flatComponents.Count == 0
                    ? null
                    : MonthlyAmountOf(flatComponents),
                tiers
                    .OrderBy(tier => tier.NumericValue ?? int.MaxValue)
                    .ToArray(),
                first.DiscountEligible,
                first.PublicVisible,
                first.SelfServiceOrderable,
                first.BillingType,
                flatComponents));
        }

        return services;
    }

    private static long MonthlyAmountOf(
        IReadOnlyList<BillingV2PublicPriceComponent> components)
        => components
            .Where(component => component.IsRecurring)
            .Sum(component => component.AmountCents);

    /// <summary>
    /// Resout une composante par cadence. Une ambiguite dans UNE cadence
    /// invalide l'ensemble : ne jamais choisir un prix arbitrairement.
    /// </summary>
    private static IReadOnlyList<BillingV2PublicPriceComponent>
        ResolveComponents(
            IEnumerable<ServiceRow> tierRows,
            string serviceCode,
            string? tierCode,
            bool discountEligible)
    {
        var components = new List<BillingV2PublicPriceComponent>();
        foreach (var cadenceGroup in tierRows.GroupBy(
                     row => (row.Candidate.BillingCadence, row.ChargeTrigger, row.Candidate.Currency)))
        {
            var resolution = BillingV2ServicePriceResolutionPolicy.Resolve(
                cadenceGroup.Select(row => row.Candidate).ToArray(),
                serviceCode,
                tierCode);
            if (!resolution.Resolved || resolution.Price is null)
            {
                return [];
            }

            components.Add(new BillingV2PublicPriceComponent(
                resolution.Price.BillingCadence,
                cadenceGroup.Key.ChargeTrigger,
                resolution.Price.AmountCents,
                resolution.Price.Currency,
                // Un frais ponctuel n'est jamais remise : la remise
                // d'engagement porte sur le recurrent.
                discountEligible
                    && resolution.Price.BillingCadence
                        == BillingV2BillingCadences.Monthly,
                resolution.Price.ServicePriceId,
                resolution.Price.PriceCode));
        }

        return components;
    }

    private static async Task<IReadOnlyList<BillingV2PublicPreset>>
        ReadPresetsAsync(
            MySqlConnection connection,
            IReadOnlyList<BillingV2PublicService> services,
            CancellationToken cancellationToken)
    {
        var rows = new List<PresetRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT
                    preset.code AS preset_code,
                    preset.name AS preset_name,
                    preset.description AS preset_description,
                    preset.display_order AS preset_display_order,
                    service.code AS service_code,
                    tier.code AS tier_code,
                    item.scope_template,
                    item.quantity,
                    item.customer_editable,
                    item.display_order AS item_display_order
                FROM billing_v2_offer_presets preset
                INNER JOIN billing_v2_preset_items item
                    ON item.preset_id = preset.id
                INNER JOIN billing_v2_services service
                    ON service.id = item.service_id
                LEFT JOIN billing_v2_service_tiers tier
                    ON tier.id = item.tier_id
                WHERE preset.status = 'active'
                  AND preset.is_public = 1
                ORDER BY preset.display_order,
                         preset.code,
                         item.display_order;
                """;
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new PresetRow(
                    reader.GetString("preset_code"),
                    reader.GetString("preset_name"),
                    reader.IsDBNull(reader.GetOrdinal("preset_description"))
                        ? string.Empty
                        : reader.GetString("preset_description"),
                    reader.GetInt32("preset_display_order"),
                    reader.GetString("service_code"),
                    reader.IsDBNull(reader.GetOrdinal("tier_code"))
                        ? null
                        : reader.GetString("tier_code"),
                    reader.GetString("scope_template"),
                    reader.GetInt32("quantity"),
                    reader.GetBoolean("customer_editable")));
            }
        }

        var presets = new List<BillingV2PublicPreset>();
        foreach (var group in rows.GroupBy(
                     row => row.PresetCode,
                     StringComparer.Ordinal))
        {
            var first = group.First();
            var items = new List<BillingV2PublicPresetItem>();
            foreach (var row in group)
            {
                var amountCents = ResolveAmountCents(
                    services,
                    row.ServiceCode,
                    row.TierCode);
                if (amountCents is null)
                {
                    continue;
                }

                items.Add(new BillingV2PublicPresetItem(
                    row.ServiceCode,
                    row.TierCode,
                    row.ScopeTemplate,
                    row.Quantity,
                    amountCents.Value,
                    row.CustomerEditable));
            }

            presets.Add(new BillingV2PublicPreset(
                first.PresetCode,
                first.PresetName,
                first.PresetDescription,
                first.PresetDisplayOrder,
                items));
        }

        return presets;
    }

    private static long? ResolveAmountCents(
        IReadOnlyList<BillingV2PublicService> services,
        string serviceCode,
        string? tierCode)
    {
        var service = services.FirstOrDefault(
            item => string.Equals(
                item.Code,
                serviceCode,
                StringComparison.Ordinal));
        if (service is null)
        {
            return null;
        }

        if (tierCode is null)
        {
            return service.FlatMonthlyAmountCents;
        }

        return service.Tiers
            .FirstOrDefault(
                tier => string.Equals(
                    tier.Code,
                    tierCode,
                    StringComparison.Ordinal))
            ?.MonthlyAmountCents;
    }

    private static async Task<IReadOnlyList<BillingV2PublicCommitment>>
        ReadCommitmentsAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
    {
        var rows = new List<CommitmentRow>();
        await using var command = connection.CreateCommand();
        // La remise est portee par le couple (duree, mode de reglement) : le
        // mensuel et le comptant d'une meme duree sont deux options distinctes.
        // Les drapeaux `allow_*_payment` restent l'autorite sur ce qui peut
        // etre propose.
        command.CommandText =
            """
            SELECT
                term.code,
                term.name,
                term.commitment_months,
                term.display_order,
                option_row.payment_mode,
                option_row.discount_basis_points
            FROM billing_v2_commitment_terms term
            INNER JOIN billing_v2_commitment_payment_options option_row
                ON option_row.commitment_term_id = term.id
               AND option_row.status = 'active'
               AND ((option_row.payment_mode = 'monthly'
                     AND term.allow_monthly_payment = 1)
                 OR (option_row.payment_mode = 'upfront'
                     AND term.allow_upfront_payment = 1))
            WHERE term.status = 'active'
            ORDER BY term.display_order, option_row.display_order;
            """;
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CommitmentRow(
                reader.GetString("code"),
                reader.GetString("name"),
                reader.GetInt32("commitment_months"),
                reader.GetInt32("display_order"),
                reader.GetString("payment_mode"),
                reader.GetInt32("discount_basis_points")));
        }

        return rows
            .GroupBy(row => row.Code, StringComparer.Ordinal)
            .OrderBy(group => group.First().DisplayOrder)
            .Select(group => new BillingV2PublicCommitment(
                group.Key,
                group.First().Name,
                group.First().CommitmentMonths,
                group
                    .Select(row => new BillingV2PublicPaymentOption(
                        row.PaymentMode,
                        row.DiscountBasisPoints))
                    .ToArray()))
            .ToArray();
    }

    /// <summary>
    /// Un service sans palier remonte par LEFT JOIN avec toutes les colonnes
    /// de <c>billing_v2_service_tiers</c> a NULL (RDS-ACCESS, USER-ADDITIONAL,
    /// SUPPORT-PLUS). <c>GetBoolean</c> jette alors un InvalidCastException et
    /// tout le catalogue public tombe. La lecture doit donc etre defensive :
    /// absence de palier = pas de palier selectionnable, ce qui n'invente
    /// aucun palier et laisse intact le <c>public_selectable</c> du service,
    /// deja filtre par la requete.
    /// </summary>
    private static bool ReadFlag(
        MySqlDataReader reader,
        string columnName,
        bool whenNull)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? whenNull
            : reader.GetBoolean(columnName);

    private static ServiceRow ReadServiceRow(MySqlDataReader reader)
        => new(
            reader.GetString("service_code"),
            reader.GetString("service_name"),
            reader.GetString("category"),
            reader.GetString("billing_type"),
            reader.GetString("default_scope_type"),
            ReadFlag(reader, "discount_eligible", whenNull: false),
            ReadFlag(reader, "public_visible", whenNull: false),
            ReadFlag(reader, "self_service_orderable", whenNull: false),
            reader.GetInt32("display_order"),
            reader.IsDBNull(reader.GetOrdinal("tier_code"))
                ? null
                : reader.GetString("tier_code"),
            reader.IsDBNull(reader.GetOrdinal("public_label"))
                ? null
                : reader.GetString("public_label"),
            reader.IsDBNull(reader.GetOrdinal("tier_description"))
                ? null
                : reader.GetString("tier_description"),
            reader.IsDBNull(reader.GetOrdinal("numeric_value"))
                ? null
                : reader.GetInt32("numeric_value"),
            ReadFlag(reader, "public_selectable", whenNull: false),
            reader.GetString("charge_trigger"),
            new BillingV2ServicePriceCandidate(
                MariaDbIdentifierReader.ReadRequired(reader, "service_price_id"),
                reader.GetString("price_code"),
                reader.GetInt32("price_version"),
                reader.GetInt64("amount_cents"),
                reader.GetString("currency"),
                reader.GetString("billing_cadence"),
                reader.GetDateTime("valid_from")));

    private sealed record ServiceRow(
        string ServiceCode,
        string ServiceName,
        string Category,
        string BillingType,
        string ScopeType,
        bool DiscountEligible,
        bool PublicVisible,
        bool SelfServiceOrderable,
        int DisplayOrder,
        string? TierCode,
        string? TierLabel,
        string? TierDescription,
        int? NumericValue,
        bool PublicSelectable,
        string ChargeTrigger,
        BillingV2ServicePriceCandidate Candidate);

    private sealed record CommitmentRow(
        string Code,
        string Name,
        int CommitmentMonths,
        int DisplayOrder,
        string PaymentMode,
        int DiscountBasisPoints);

    private sealed record PresetRow(
        string PresetCode,
        string PresetName,
        string PresetDescription,
        int PresetDisplayOrder,
        string ServiceCode,
        string? TierCode,
        string ScopeTemplate,
        int Quantity,
        bool CustomerEditable);
}
