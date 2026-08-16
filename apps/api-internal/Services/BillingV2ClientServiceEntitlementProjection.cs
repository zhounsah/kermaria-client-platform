using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2ClientServiceEntitlement(
    string TechnicalServiceReference,
    string SubscriptionId,
    string SubscriptionLabel,
    string SubscriptionStatus,
    string? StartedAt,
    string CreatedAt);

public interface IBillingV2ClientServiceEntitlementProjection
{
    Task<IReadOnlyList<BillingV2ClientServiceEntitlement>>
        GetClientEntitlementsAsync(
            string customerId,
            CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2ClientServiceEntitlementProjection
    : IBillingV2ClientServiceEntitlementProjection
{
    public static NoOpBillingV2ClientServiceEntitlementProjection Instance { get; }
        = new();

    private NoOpBillingV2ClientServiceEntitlementProjection()
    {
    }

    public Task<IReadOnlyList<BillingV2ClientServiceEntitlement>>
        GetClientEntitlementsAsync(
            string customerId,
            CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<BillingV2ClientServiceEntitlement>>(
            Array.Empty<BillingV2ClientServiceEntitlement>());
}

public sealed class BillingV2ClientServiceEntitlementProjection
    : IBillingV2ClientServiceEntitlementProjection
{
    private readonly string _connectionString;

    public BillingV2ClientServiceEntitlementProjection(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public async Task<IReadOnlyList<BillingV2ClientServiceEntitlement>>
        GetClientEntitlementsAsync(
            string customerId,
            CancellationToken cancellationToken)
    {
        var entitlements = new List<BillingV2ClientServiceEntitlement>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var technicalReference =
                BillingV2ClientServiceEntitlementPolicy
                    .ResolveTechnicalServiceReference(
                        ReadNullableString(
                            reader,
                            "legacy_service_reference"),
                        reader.GetString("service_code"));
            entitlements.Add(new BillingV2ClientServiceEntitlement(
                technicalReference,
                MariaDbIdentifierReader.ReadRequired(reader, "subscription_id"),
                ReadNullableString(reader, "preset_name")
                    ?? "Souscription Billing V2",
                reader.GetString("subscription_status"),
                ReadNullableIso(reader, "started_at"),
                ToIso(reader.GetDateTime("created_at"))));
        }

        return entitlements;
    }

    private static readonly string SelectSql =
        $"""
        SELECT DISTINCT
            COALESCE(
                tier_mapping.legacy_service_reference,
                service_mapping.legacy_service_reference,
                service.code
            )
                AS legacy_service_reference,
            service.code AS service_code,
            subscription.id AS subscription_id,
            preset.name AS preset_name,
            subscription.status AS subscription_status,
            subscription.started_at,
            subscription.created_at
        FROM billing_v2_subscriptions subscription
        INNER JOIN billing_v2_authoritative_checkout_requests request
            ON request.subscription_id = subscription.id
        INNER JOIN billing_v2_subscription_items item
            ON item.subscription_id = subscription.id
        INNER JOIN billing_v2_services service
            ON service.id = item.service_id
        LEFT JOIN billing_v2_service_tiers tier
            ON tier.id = item.tier_id
        LEFT JOIN billing_v2_offer_presets preset
            ON preset.id = subscription.originating_preset_id
        LEFT JOIN billing_v2_legacy_service_mappings tier_mapping
            ON tier_mapping.v2_service_code = service.code
           AND tier_mapping.v2_tier_code = tier.code
        LEFT JOIN billing_v2_legacy_service_mappings service_mapping
            ON service_mapping.v2_service_code = service.code
           AND service_mapping.v2_tier_code IS NULL
        WHERE subscription.customer_id = @customer_id
          AND item.status = 'active'
          AND item.effective_from <= UTC_TIMESTAMP(6)
          AND (
                item.effective_until IS NULL
                OR item.effective_until > UTC_TIMESTAMP(6)
              )
          AND {BillingV2ContractWindowSql.SubscriptionStillInForce}
          AND NOT EXISTS (
              SELECT 1
              FROM subscriptions legacy_subscription
              WHERE legacy_subscription.id = subscription.id
          )
        ORDER BY subscription.created_at DESC, legacy_service_reference;
        """;

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);

    private static string? ReadNullableIso(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column))
            ? null
            : ToIso(reader.GetDateTime(column));

    private static string ToIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}

public static class BillingV2ClientServiceEntitlementPolicy
{
    public static string ResolveTechnicalServiceReference(
        string? legacyServiceReference,
        string serviceCode)
        => string.IsNullOrWhiteSpace(legacyServiceReference)
            ? serviceCode.Trim()
            : legacyServiceReference.Trim();
}
