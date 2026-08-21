using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Source unique de lecture des prix contractuels V2. Un item V2.0 est expose
/// comme une composante virtuelle par la vue SQL ; un item componentized ne
/// lit jamais ses colonnes miroir historiques comme autorite financiere.
/// </summary>
public sealed record BillingV2SubscriptionItemPriceComponent(
    string? ComponentId,
    string SubscriptionItemId,
    string ServicePriceId,
    string BillingCadence,
    string ChargeTrigger,
    long AmountCentsSnapshot,
    string Currency,
    bool DiscountEligibleSnapshot,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveUntilUtc,
    int DisplayOrder);

public static class BillingV2SubscriptionItemPriceComponentResolver
{
    public static async Task<IReadOnlyList<BillingV2SubscriptionItemPriceComponent>>
        ReadForSubscriptionAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string subscriptionId,
            DateTime asOfUtc,
            string? cadence,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                component.component_id,
                component.subscription_item_id,
                component.service_price_id,
                component.billing_cadence,
                component.charge_trigger,
                component.amount_cents_snapshot,
                component.currency,
                component.discount_eligible_snapshot,
                component.effective_from,
                component.effective_until,
                component.display_order
            FROM billing_v2_subscription_item_effective_price_components component
            INNER JOIN billing_v2_subscription_items item
                ON item.id = component.subscription_item_id
            WHERE item.subscription_id = @subscription_id
              AND item.status = 'active'
              AND item.effective_from <= @as_of
              AND (item.effective_until IS NULL OR item.effective_until > @as_of)
              AND component.status = 'active'
              AND component.effective_from <= @as_of
              AND (component.effective_until IS NULL OR component.effective_until > @as_of)
              AND (@cadence IS NULL OR component.billing_cadence = @cadence)
            ORDER BY item.id, component.display_order, component.component_id;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue("@as_of", asOfUtc);
        command.Parameters.AddWithValue("@cadence", cadence is null ? DBNull.Value : cadence);

        var components = new List<BillingV2SubscriptionItemPriceComponent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            components.Add(new BillingV2SubscriptionItemPriceComponent(
                MariaDbIdentifierReader.ReadNullable(reader, "component_id"),
                MariaDbIdentifierReader.ReadRequired(reader, "subscription_item_id"),
                MariaDbIdentifierReader.ReadRequired(reader, "service_price_id"),
                reader.GetString("billing_cadence"),
                reader.GetString("charge_trigger"),
                reader.GetInt64("amount_cents_snapshot"),
                reader.GetString("currency"),
                reader.GetBoolean("discount_eligible_snapshot"),
                DateTime.SpecifyKind(reader.GetDateTime("effective_from"), DateTimeKind.Utc),
                reader.IsDBNull(reader.GetOrdinal("effective_until"))
                    ? null
                    : DateTime.SpecifyKind(reader.GetDateTime("effective_until"), DateTimeKind.Utc),
                reader.GetInt32("display_order")));
        }

        return components;
    }
}
