using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Configuration V2 native resolue en base : les identifiants reels
/// (`preset_id`, `commitment_term_id`, `service_price_id`) que le checkout
/// authoritative attendait jusqu'ici d'une offre legacy.
/// </summary>
public sealed record BillingV2ResolvedNativeSelection(
    string PresetId,
    string PresetCode,
    string CommitmentTermId,
    string CommitmentCode,
    string PaymentMode,
    int CommitmentMonths,
    int DiscountBasisPoints,
    IReadOnlyList<BillingV2NewSubscriptionPresetItem> Items,
    string SelectionCanonical);

/// <summary>
/// Traduit une intention de configuration publique en items facturables
/// reels, sans passer par une offre legacy.
///
/// Deux garde-fous structurent ce composant :
///
/// 1. La selection est d'abord revalidee par
///    <see cref="BillingV2PublicSelectionPolicy"/> contre le catalogue lu en
///    base. Ce que le navigateur envoie n'est donc jamais pris pour argent
///    comptant : un palier non public, une dependance non respectee ou un mode
///    de reglement non ouvert echoue avant toute ecriture.
/// 2. Les montants ne viennent pas du catalogue projete mais des lignes
///    `billing_v2_service_prices` relues ici, avec la meme politique de
///    resolution d'ambiguite que le chemin preset
///    (<see cref="BillingV2ServicePriceResolutionPolicy"/>). Le Pricing Engine
///    reste le seul a calculer un total.
///
/// Aucune ecriture, aucun DDL : lecture seule, comme l'exige le compte
/// applicatif.
/// </summary>
public static class BillingV2NativeSelectionResolver
{
    public const string PresetUnknown = "BILLING_V2_PUBLIC_PRESET_UNKNOWN";
    public const string CommitmentUnknown =
        "BILLING_V2_PUBLIC_COMMITMENT_UNKNOWN";
    public const string PaymentModeUnavailable =
        "BILLING_V2_PUBLIC_PAYMENT_MODE_UNAVAILABLE";
    public const string ServicePriceUnknown =
        "BILLING_V2_PUBLIC_SERVICE_PRICE_UNKNOWN";
    public const string ScopeTemplateUnknown =
        "BILLING_V2_PUBLIC_SCOPE_TEMPLATE_UNKNOWN";
    public const string ScopeTemplateAmbiguous =
        "BILLING_V2_PUBLIC_SCOPE_TEMPLATE_AMBIGUOUS";

    public static async Task<BillingV2ResolvedNativeSelection> ResolveAsync(
        MySqlConnection connection,
        BillingV2PublicCatalogSnapshot catalog,
        BillingV2PublicSelection selection,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var resolution = BillingV2PublicSelectionPolicy.Resolve(
            catalog,
            selection);
        if (!resolution.Resolved)
        {
            throw new InvalidOperationException(resolution.ReasonCode);
        }

        var commitment = catalog.Commitments.First(
            item => string.Equals(
                item.Code,
                selection.CommitmentCode,
                StringComparison.Ordinal));
        var paymentOption = commitment.Option(selection.PaymentMode)
            ?? throw new InvalidOperationException(PaymentModeUnavailable);

        var presetId = await ReadPresetIdAsync(
                connection,
                selection.PresetCode,
                cancellationToken)
            ?? throw new InvalidOperationException(PresetUnknown);
        var term = await ReadCommitmentTermAsync(
                connection,
                selection.CommitmentCode,
                selection.PaymentMode,
                cancellationToken)
            ?? throw new InvalidOperationException(CommitmentUnknown);

        // La remise annoncee au client et celle qui sera facturee doivent
        // provenir de la meme ligne : un ecart signale un catalogue projete
        // perime, et doit echouer plutot que sous-facturer.
        if (term.DiscountBasisPoints != paymentOption.DiscountBasisPoints)
        {
            throw new InvalidOperationException(
                "BILLING_V2_PUBLIC_DISCOUNT_MISMATCH");
        }

        var prices = await ReadServicePricesAsync(
            connection,
            now,
            cancellationToken);
        var scopeTemplates = await ReadScopeTemplatesAsync(
            connection,
            cancellationToken);

        var items = new List<BillingV2NewSubscriptionPresetItem>();
        foreach (var component in resolution.Components)
        {
            var key = PriceKey(component.ServiceCode, component.TierCode);
            if (!prices.TryGetValue(key, out var priceComponents))
            {
                throw new InvalidOperationException(
                    $"{ServicePriceUnknown}: {key}");
            }

            if (!scopeTemplates.TryGetValue(
                    component.ServiceCode,
                    out var scopeTemplate))
            {
                throw new InvalidOperationException(
                    $"{ScopeTemplateUnknown}: {component.ServiceCode}");
            }

            // Un utilisateur supplementaire est une identite, pas une
            // quantite : n items de quantite 1 donnent n comptes provisionnes,
            // la ou une seule ligne de quantite n n'en donnerait qu'un.
            var splitPerUnit = string.Equals(
                scopeTemplate,
                "additional_user",
                StringComparison.Ordinal);
            var lineCount = splitPerUnit ? component.Quantity : 1;
            var quantity = splitPerUnit ? 1 : component.Quantity;

            foreach (var price in priceComponents)
            {
                for (var index = 0; index < lineCount; index++)
                {
                    items.Add(new BillingV2NewSubscriptionPresetItem(
                        splitPerUnit
                            ? $"{key}#{index + 1}#{price.BillingCadence}"
                            : $"{key}#{price.BillingCadence}",
                        price.ServiceId,
                        price.TierId,
                        price.ServicePriceId,
                        component.ServiceCode,
                        component.TierCode,
                        price.PriceCode,
                        scopeTemplate,
                        quantity,
                        price.AmountCents,
                        price.Currency,
                        price.BillingCadence,
                        price.DiscountEligible));
                }
            }
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException("BILLING_V2_PRESET_HAS_NO_ITEMS");
        }

        return new BillingV2ResolvedNativeSelection(
            presetId,
            selection.PresetCode,
            term.CommitmentTermId,
            selection.CommitmentCode,
            selection.PaymentMode,
            Math.Max(1, term.CommitmentMonths),
            term.DiscountBasisPoints,
            items,
            selection.Canonical());
    }

    private static string PriceKey(string serviceCode, string? tierCode)
        => $"{serviceCode}/{tierCode ?? "-"}";

    private static async Task<string?> ReadPresetIdAsync(
        MySqlConnection connection,
        string presetCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id
            FROM billing_v2_offer_presets
            WHERE code = @code
              AND status = 'active'
              AND is_public = 1
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@code", presetCode);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MariaDbIdentifierReader.ReadRequired(reader, "id")
            : null;
    }

    private static async Task<CommitmentTermRow?> ReadCommitmentTermAsync(
        MySqlConnection connection,
        string commitmentCode,
        string paymentMode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                term.id,
                term.commitment_months,
                option_row.discount_basis_points
            FROM billing_v2_commitment_terms term
            INNER JOIN billing_v2_commitment_payment_options option_row
                ON option_row.commitment_term_id = term.id
               AND option_row.payment_mode = @payment_mode
               AND option_row.status = 'active'
            WHERE term.code = @code
              AND term.status = 'active'
              AND ((@payment_mode = 'monthly'
                    AND term.allow_monthly_payment = 1)
                OR (@payment_mode = 'upfront'
                    AND term.allow_upfront_payment = 1))
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@code", commitmentCode);
        command.Parameters.AddWithValue("@payment_mode", paymentMode);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CommitmentTermRow(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetInt32("commitment_months"),
            reader.GetInt32("discount_basis_points"));
    }

    /// <summary>
    /// Portee de facturation par service, deduite des presets actifs plutot
    /// que d'une table de correspondance codee en dur. Une portee divergente
    /// entre deux presets echoue en ferme : elle rendrait le provisioning
    /// non deterministe.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>>
        ReadScopeTemplatesAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
    {
        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                service.code AS service_code,
                MIN(item.scope_template) AS scope_template,
                COUNT(DISTINCT item.scope_template) AS scope_count
            FROM billing_v2_preset_items item
            INNER JOIN billing_v2_services service
                ON service.id = item.service_id
            INNER JOIN billing_v2_offer_presets preset
                ON preset.id = item.preset_id
               AND preset.status = 'active'
            GROUP BY service.code;
            """;
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var serviceCode = reader.GetString("service_code");
            if (reader.GetInt32("scope_count") != 1)
            {
                throw new InvalidOperationException(
                    $"{ScopeTemplateAmbiguous}: {serviceCode}");
            }

            templates[serviceCode] = reader.GetString("scope_template");
        }

        return templates;
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<ServicePriceRow>>>
        ReadServicePricesAsync(
            MySqlConnection connection,
            DateTime now,
            CancellationToken cancellationToken)
    {
        var rows = new List<ServicePriceCandidateRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT
                    service.id AS service_id,
                    service.code AS service_code,
                    service.discount_eligible,
                    tier.id AS tier_id,
                    tier.code AS tier_code,
                    price.id AS service_price_id,
                    price.price_code,
                    price.price_version,
                    price.amount_cents,
                    price.currency,
                    price.billing_cadence,
                    price.valid_from
                FROM billing_v2_services service
                LEFT JOIN billing_v2_service_tiers tier
                    ON tier.service_id = service.id
                   AND tier.status = 'active'
                INNER JOIN billing_v2_service_prices price
                    ON price.service_id = service.id
                   AND price.tier_id <=> tier.id
                   AND price.status = 'active'
                   AND price.valid_from <= @now
                   AND (price.valid_until IS NULL OR price.valid_until > @now)
                WHERE service.status = 'active'
                ORDER BY service.code,
                         tier.code,
                         price.price_version DESC,
                         price.valid_from DESC;
                """;
            command.Parameters.AddWithValue("@now", now);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new ServicePriceCandidateRow(
                    MariaDbIdentifierReader.ReadRequired(reader, "service_id"),
                    reader.GetString("service_code"),
                    MariaDbIdentifierReader.ReadNullable(reader, "tier_id"),
                    reader.IsDBNull(reader.GetOrdinal("tier_code"))
                        ? null
                        : reader.GetString("tier_code"),
                    reader.GetBoolean("discount_eligible"),
                    new BillingV2ServicePriceCandidate(
                        MariaDbIdentifierReader.ReadRequired(
                            reader,
                            "service_price_id"),
                        reader.GetString("price_code"),
                        reader.GetInt32("price_version"),
                        reader.GetInt64("amount_cents"),
                        reader.GetString("currency"),
                        reader.GetString("billing_cadence"),
                        reader.GetDateTime("valid_from"))));
            }
        }

        var prices = new Dictionary<string, IReadOnlyList<ServicePriceRow>>(
            StringComparer.Ordinal);
        foreach (var group in rows.GroupBy(
                     row => PriceKey(row.ServiceCode, row.TierCode),
                     StringComparer.Ordinal))
        {
            var first = group.First();
            var resolvedByCadence = new List<ServicePriceRow>();
            foreach (var cadenceGroup in group.GroupBy(
                         row => row.Candidate.BillingCadence,
                         StringComparer.Ordinal))
            {
                var resolved = BillingV2ServicePriceResolutionPolicy.Resolve(
                    cadenceGroup.Select(row => row.Candidate).ToArray(),
                    first.ServiceCode,
                    first.TierCode);
                if (!resolved.Resolved || resolved.Price is null)
                {
                    // Une ambiguite dans UNE cadence suffit a invalider cette
                    // configuration : ne jamais choisir arbitrairement.
                    resolvedByCadence.Clear();
                    break;
                }

                resolvedByCadence.Add(new ServicePriceRow(
                    first.ServiceId,
                    first.TierId,
                    resolved.Price.ServicePriceId,
                    resolved.Price.PriceCode,
                    resolved.Price.AmountCents,
                    resolved.Price.Currency,
                    resolved.Price.BillingCadence,
                    first.DiscountEligible));
            }

            if (resolvedByCadence.Count > 0)
            {
                prices[group.Key] = resolvedByCadence;
            }
        }

        return prices;
    }

    private sealed record CommitmentTermRow(
        string CommitmentTermId,
        int CommitmentMonths,
        int DiscountBasisPoints);

    private sealed record ServicePriceCandidateRow(
        string ServiceId,
        string ServiceCode,
        string? TierId,
        string? TierCode,
        bool DiscountEligible,
        BillingV2ServicePriceCandidate Candidate);

    private sealed record ServicePriceRow(
        string ServiceId,
        string? TierId,
        string ServicePriceId,
        string PriceCode,
        long AmountCents,
        string Currency,
        string BillingCadence,
        bool DiscountEligible);
}
