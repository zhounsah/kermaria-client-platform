using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Lecture unique et partagee des items de preset et de leur prix applicable.
///
/// Ce composant existe pour une raison precise : avant lui, deux chemins de
/// creation d'abonnement (BillingV2NewSubscriptionService et
/// BillingV2AuthoritativeCheckoutService) lisaient les prix differemment. L'un
/// dedupliquait silencieusement en SQL, l'autre pas du tout - et une fenetre de
/// validite chevauchante y doublait la ligne facturee sans aucune erreur.
///
/// La requete renvoie donc TOUS les prix actifs applicables, et la resolution
/// est faite en C# par une politique testable qui echoue en ferme en cas
/// d'ambiguite (APP-15). Deux versions d'un meme prix ne sont jamais sommees
/// comme deux services.
/// </summary>
public static class BillingV2PresetItemReader
{
    public static async Task<IReadOnlyList<BillingV2NewSubscriptionPresetItem>>
        ReadAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string presetId,
            DateTime now,
            CancellationToken cancellationToken)
    {
        var rows = await ReadRowsAsync(
            connection,
            transaction,
            presetId,
            now,
            cancellationToken);

        var items = new List<BillingV2NewSubscriptionPresetItem>();
        foreach (var group in rows.GroupBy(
                     row => (row.PresetItemId, row.Candidate.BillingCadence)))
        {
            var first = group.First();
            var candidates = group
                .Select(row => row.Candidate)
                .ToArray();
            var resolution = BillingV2ServicePriceResolutionPolicy.Resolve(
                candidates,
                first.ServiceCode,
                first.TierCode);
            if (!resolution.Resolved || resolution.Price is null)
            {
                throw new InvalidOperationException(
                    $"{resolution.ReasonCode}: {resolution.Diagnostic}");
            }

            var price = resolution.Price;
            items.Add(new BillingV2NewSubscriptionPresetItem(
                $"{first.PresetItemId}#{price.BillingCadence}",
                first.ServiceId,
                first.TierId,
                price.ServicePriceId,
                first.ServiceCode,
                first.TierCode,
                price.PriceCode,
                first.ScopeTemplate,
                first.Quantity,
                price.AmountCents,
                price.Currency,
                price.BillingCadence,
                first.DiscountEligible));
        }

        // Les lignes arrivent deja triees par display_order puis id, et
        // GroupBy preserve l'ordre de premiere apparition : l'ordre des items
        // est donc deterministe sans tri supplementaire.
        return items;
    }

    private static async Task<IReadOnlyList<BillingV2PresetItemRow>>
        ReadRowsAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string presetId,
            DateTime now,
            CancellationToken cancellationToken)
    {
        var rows = new List<BillingV2PresetItemRow>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                preset_item.id AS preset_item_id,
                preset_item.service_id,
                preset_item.tier_id,
                preset_item.scope_template,
                preset_item.quantity,
                preset_item.display_order,
                service.code AS service_code,
                service.discount_eligible,
                tier.code AS tier_code,
                price.id AS service_price_id,
                price.price_code,
                price.price_version,
                price.amount_cents,
                price.currency,
                price.billing_cadence,
                price.valid_from
            FROM billing_v2_preset_items preset_item
            INNER JOIN billing_v2_services service
                ON service.id = preset_item.service_id
               AND service.status = 'active'
            LEFT JOIN billing_v2_service_tiers tier
                ON tier.id = preset_item.tier_id
               AND tier.status = 'active'
            INNER JOIN billing_v2_service_prices price
                ON price.service_id = service.id
               AND price.tier_id <=> preset_item.tier_id
               AND price.status = 'active'
               AND price.valid_from <= @now
               AND (price.valid_until IS NULL OR price.valid_until > @now)
            WHERE preset_item.preset_id = @preset_id
            ORDER BY preset_item.display_order,
                     preset_item.id,
                     price.price_version DESC,
                     price.valid_from DESC;
            """;
        command.Parameters.AddWithValue("@preset_id", presetId);
        command.Parameters.AddWithValue("@now", now);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new BillingV2PresetItemRow(
                MariaDbIdentifierReader.ReadRequired(reader, "preset_item_id"),
                MariaDbIdentifierReader.ReadRequired(reader, "service_id"),
                MariaDbIdentifierReader.ReadNullable(reader, "tier_id"),
                reader.GetString("service_code"),
                reader.IsDBNull(reader.GetOrdinal("tier_code"))
                    ? null
                    : reader.GetString("tier_code"),
                reader.GetString("scope_template"),
                reader.GetInt32("quantity"),
                reader.GetInt32("display_order"),
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

        return rows;
    }

    private sealed record BillingV2PresetItemRow(
        string PresetItemId,
        string ServiceId,
        string? TierId,
        string ServiceCode,
        string? TierCode,
        string ScopeTemplate,
        int Quantity,
        int DisplayOrder,
        bool DiscountEligible,
        BillingV2ServicePriceCandidate Candidate);
}
