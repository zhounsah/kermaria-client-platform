using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2DownloadAccessScope(
    IReadOnlySet<string> PublicPackCodes,
    IReadOnlySet<string> OfferExternalReferences,
    IReadOnlySet<string> ProvisioningGroups);

public interface IBillingV2DownloadAccessProjection
{
    Task<BillingV2DownloadAccessScope> GetClientAccessScopeAsync(
        string customerId,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2DownloadAccessProjection
    : IBillingV2DownloadAccessProjection
{
    public static NoOpBillingV2DownloadAccessProjection Instance { get; } =
        new();

    private NoOpBillingV2DownloadAccessProjection()
    {
    }

    public Task<BillingV2DownloadAccessScope> GetClientAccessScopeAsync(
        string customerId,
        CancellationToken cancellationToken)
        => Task.FromResult(BillingV2DownloadAccessScopePolicy.Create(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>()));
}

public sealed class BillingV2DownloadAccessProjection
    : IBillingV2DownloadAccessProjection
{
    private readonly string _connectionString;

    public BillingV2DownloadAccessProjection(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public async Task<BillingV2DownloadAccessScope> GetClientAccessScopeAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var publicPackCodes = new List<string>();
        var offerExternalReferences = new List<string>();
        var provisioningGroups = new List<string>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = LegacyTargetsSql;
            command.Parameters.AddWithValue("@customer_id", customerId);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                publicPackCodes.Add(ReadNullableString(reader, "public_pack_code"));
                offerExternalReferences.Add(
                    ReadNullableString(reader, "offer_external_reference"));
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ProvisioningGroupsSql;
            command.Parameters.AddWithValue("@customer_id", customerId);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                provisioningGroups.Add(
                    ReadNullableString(reader, "target_reference"));
            }
        }

        return BillingV2DownloadAccessScopePolicy.Create(
            publicPackCodes,
            offerExternalReferences,
            provisioningGroups);
    }

    private const string LegacyTargetsSql =
        """
        SELECT DISTINCT
            offer.public_pack_code,
            offer.external_reference AS offer_external_reference
        FROM billing_v2_subscriptions subscription
        INNER JOIN billing_v2_authoritative_checkout_requests request
            ON request.subscription_id = subscription.id
        LEFT JOIN commercial_offers offer
            ON offer.id = request.legacy_offer_id
        WHERE subscription.customer_id = @customer_id
          AND subscription.status = 'active'
          AND NOT EXISTS (
              SELECT 1
              FROM subscriptions legacy_subscription
              WHERE legacy_subscription.id = subscription.id
          );
        """;

    private const string ProvisioningGroupsSql =
        """
        SELECT DISTINCT
            rule.target_reference
        FROM billing_v2_subscriptions subscription
        INNER JOIN billing_v2_subscription_items item
            ON item.subscription_id = subscription.id
        INNER JOIN billing_v2_services service
            ON service.id = item.service_id
        LEFT JOIN billing_v2_service_tiers tier
            ON tier.id = item.tier_id
        INNER JOIN billing_v2_provisioning_rules rule
            ON rule.service_id = service.id
           AND rule.status = 'active'
           AND rule.target_type = 'ad_group'
           AND (
                rule.tier_id IS NULL
                OR rule.tier_id = tier.id
           )
        WHERE subscription.customer_id = @customer_id
          AND subscription.status = 'active'
          AND item.status = 'active'
          AND item.effective_from <= UTC_TIMESTAMP(6)
          AND (
                item.effective_until IS NULL
                OR item.effective_until > UTC_TIMESTAMP(6)
              )
          AND NOT EXISTS (
              SELECT 1
              FROM subscriptions legacy_subscription
              WHERE legacy_subscription.id = subscription.id
          );
        """;

    private static string ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? string.Empty
            : reader.GetString(columnName);
}

public static class BillingV2DownloadAccessScopePolicy
{
    public static BillingV2DownloadAccessScope Create(
        IEnumerable<string?> publicPackCodes,
        IEnumerable<string?> offerExternalReferences,
        IEnumerable<string?> provisioningGroups)
        => new(
            Normalize(publicPackCodes, StringComparer.Ordinal),
            Normalize(offerExternalReferences, StringComparer.Ordinal),
            Normalize(provisioningGroups, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlySet<string> Normalize(
        IEnumerable<string?> values,
        IEqualityComparer<string> comparer)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Where(value => value.Length > 0)
            .ToHashSet(comparer);
}
