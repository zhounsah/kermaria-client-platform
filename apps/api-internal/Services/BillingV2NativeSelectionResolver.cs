using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Configuration V2 native resolue en base : les identifiants reels
/// (`preset_id`, `commitment_term_id`, `service_price_id`) que le checkout
/// authoritative attendait jusqu'ici d'une offre legacy.
/// </summary>
/// <remarks>
/// <see cref="PresetId"/> et <see cref="CommitmentTermId"/> sont nuls pour une
/// selection directe : le schema V2 accepte deja une souscription sans formule
/// ni engagement (`originating_preset_id` et `commitment_term_id` nullables).
/// Aucun preset ni terme technique n'est fabrique pour combler ces colonnes.
/// </remarks>
public sealed record BillingV2ResolvedNativeSelection(
    string? PresetId,
    string? PresetCode,
    string? CommitmentTermId,
    string? CommitmentCode,
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

        // Formule d'origine : facultative. Une selection directe n'en a pas,
        // et la colonne correspondante reste NULL.
        string? presetId = null;
        if (selection.PresetCode is { Length: > 0 } presetCode)
        {
            presetId = await ReadPresetIdAsync(
                    connection,
                    presetCode,
                    cancellationToken)
                ?? throw new InvalidOperationException(PresetUnknown);
        }

        // Engagement : facultatif lui aussi. Sans terme, la duree vaut 1 mois
        // et la remise 0 — aucune remise ne peut etre accordee par defaut.
        CommitmentTermRow? term = null;
        if (selection.CommitmentCode is { Length: > 0 } commitmentCode)
        {
            var commitment = catalog.Commitments.First(
                item => string.Equals(
                    item.Code,
                    commitmentCode,
                    StringComparison.Ordinal));
            var paymentOption = commitment.Option(selection.PaymentMode)
                ?? throw new InvalidOperationException(PaymentModeUnavailable);

            term = await ReadCommitmentTermAsync(
                    connection,
                    commitmentCode,
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
            term?.CommitmentTermId,
            selection.CommitmentCode,
            selection.PaymentMode,
            Math.Max(1, term?.CommitmentMonths ?? 1),
            term?.DiscountBasisPoints ?? 0,
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
    /// Portee de facturation par service.
    ///
    /// Deux sources, dans cet ordre :
    ///
    /// 1. les presets actifs, qui restent l'autorite pour les services qu'ils
    ///    composent — une portee divergente entre deux presets echoue en ferme,
    ///    elle rendrait le provisioning non deterministe ;
    /// 2. a defaut, `billing_v2_services.default_scope_type`, seule source
    ///    native pour un service qui n'entre dans aucune formule. Sans cette
    ///    retombee, tout le catalogue hors formule (DNS, VPS, supervision...)
    ///    serait insouscriptible alors qu'il est complet en base.
    ///
    /// La correspondance est volontairement explicite : `user` designe le
    /// titulaire du contrat (`primary_user`), jamais un utilisateur
    /// supplementaire, qui reste porte par les presets.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>>
        ReadScopeTemplatesAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
    {
        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
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
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT code, default_scope_type
                FROM billing_v2_services
                WHERE status = 'active';
                """;
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var serviceCode = reader.GetString("code");
                if (templates.ContainsKey(serviceCode))
                {
                    continue;
                }

                var scopeTemplate = MapDefaultScopeType(
                    reader.GetString("default_scope_type"));
                if (scopeTemplate is not null)
                {
                    templates[serviceCode] = scopeTemplate;
                }
            }
        }

        return templates;
    }

    private static string? MapDefaultScopeType(string defaultScopeType)
        => defaultScopeType switch
        {
            "subscription" => "subscription",
            "user" => "primary_user",
            _ => null
        };

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
                    price.charge_trigger,
                    price.valid_from
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
                ORDER BY service.code,
                         tier.code,
                         price.billing_cadence,
                         price.price_version DESC,
                         price.valid_from DESC;
                """;
            command.Parameters.AddWithValue("@now", now);
            // Un prix marque `subscription_change` finance un changement de
            // configuration, jamais la souscription initiale : il ne doit pas
            // pouvoir entrer dans un premier checkout.
            command.Parameters.AddWithValue(
                "@charge_trigger",
                BillingV2ComponentizedPricingPolicy.InitialSubscription);
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
                    reader.GetString("charge_trigger"),
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
            var resolvedComponents = new List<ServicePriceRow>();
            // La cle metier d'un prix est le quintuplet
            // (service, palier, devise, cadence, declencheur). Grouper sur la
            // seule cadence confondrait deux prix qui n'ont pas le meme role.
            foreach (var componentGroup in group.GroupBy(
                         row => (
                             row.Candidate.Currency,
                             row.Candidate.BillingCadence,
                             row.ChargeTrigger)))
            {
                var resolved = BillingV2ServicePriceResolutionPolicy.Resolve(
                    componentGroup.Select(row => row.Candidate).ToArray(),
                    first.ServiceCode,
                    first.TierCode);
                if (!resolved.Resolved || resolved.Price is null)
                {
                    // Une ambiguite dans UNE composante suffit a invalider
                    // cette configuration : ne jamais choisir arbitrairement.
                    resolvedComponents.Clear();
                    break;
                }

                resolvedComponents.Add(new ServicePriceRow(
                    first.ServiceId,
                    first.TierId,
                    resolved.Price.ServicePriceId,
                    resolved.Price.PriceCode,
                    resolved.Price.AmountCents,
                    resolved.Price.Currency,
                    resolved.Price.BillingCadence,
                    // Un frais ponctuel n'est jamais remise : la remise
                    // d'engagement porte sur le recurrent. Meme regle que la
                    // projection publique, pour que devis et facturation
                    // retombent sur le meme montant.
                    first.DiscountEligible
                        && resolved.Price.BillingCadence
                            == BillingV2BillingCadences.Monthly));
            }

            if (resolvedComponents.Count > 0)
            {
                prices[group.Key] = resolvedComponents
                    .OrderByDescending(row =>
                        row.BillingCadence == BillingV2BillingCadences.Monthly)
                    .ThenBy(row => row.PriceCode, StringComparer.Ordinal)
                    .ToArray();
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
        string ChargeTrigger,
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
