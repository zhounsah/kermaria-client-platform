using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2BlockingLegacySubscription(
    string SubscriptionId,
    string Status,
    string CustomerId,
    string CustomerReference,
    string CustomerName,
    string? CommercialOfferId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record BillingV2LaunchReadinessSnapshot(
    int RealCustomerSubscriptionCount,
    int DemoSubscriptionCount,
    bool VerifiedAgainstPersistentSql)
{
    public bool NoRealCustomerSubscriptions =>
        RealCustomerSubscriptionCount == 0;

    public IReadOnlyList<BillingV2BlockingLegacySubscription>
        BlockingRealSubscriptions { get; init; } =
            Array.Empty<BillingV2BlockingLegacySubscription>();
}

public interface IBillingV2LaunchReadinessService
{
    Task<BillingV2LaunchReadinessSnapshot> CheckAsync(
        CancellationToken cancellationToken);
}

public sealed class BillingV2LaunchReadinessService
    : IBillingV2LaunchReadinessService
{
    private readonly SqlRuntimeConfiguration _sql;

    public BillingV2LaunchReadinessService(SqlRuntimeConfiguration sql)
    {
        _sql = sql;
    }

    public async Task<BillingV2LaunchReadinessSnapshot> CheckAsync(
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return new BillingV2LaunchReadinessSnapshot(
                RealCustomerSubscriptionCount: 0,
                DemoSubscriptionCount: 0,
                VerifiedAgainstPersistentSql: false);
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                SUM(CASE WHEN COALESCE(customer.is_demo, FALSE) = FALSE
                         THEN 1 ELSE 0 END) AS real_count,
                SUM(CASE WHEN COALESCE(customer.is_demo, FALSE) = TRUE
                         THEN 1 ELSE 0 END) AS demo_count
            FROM subscriptions subscription
            INNER JOIN customers customer
                ON customer.id = subscription.customer_id
            WHERE subscription.status IN (
                    'active',
                    'pending_cancellation',
                    'suspended',
                    'pending_activation',
                    'pending_payment',
                    'pending_approval'
                );
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new BillingV2LaunchReadinessSnapshot(
                RealCustomerSubscriptionCount: 0,
                DemoSubscriptionCount: 0,
                VerifiedAgainstPersistentSql: true);
        }

        var snapshot = BillingV2LaunchReadinessGate.Evaluate(
            reader.IsDBNull(reader.GetOrdinal("real_count"))
                ? 0
                : reader.GetInt32("real_count"),
            reader.IsDBNull(reader.GetOrdinal("demo_count"))
                ? 0
                : reader.GetInt32("demo_count"));
        await reader.DisposeAsync();

        if (snapshot.NoRealCustomerSubscriptions)
        {
            return snapshot;
        }

        return snapshot with
        {
            BlockingRealSubscriptions =
                await LoadBlockingRealSubscriptionsAsync(
                    connection,
                    cancellationToken)
        };
    }

    private static async Task<IReadOnlyList<BillingV2BlockingLegacySubscription>>
        LoadBlockingRealSubscriptionsAsync(
            MySqlConnection connection,
            CancellationToken cancellationToken)
    {
        var subscriptions = new List<BillingV2BlockingLegacySubscription>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                subscription.id AS subscription_id,
                subscription.status,
                subscription.customer_id,
                customer.external_reference AS customer_reference,
                customer.display_name AS customer_name,
                subscription.commercial_offer_id,
                subscription.created_at,
                subscription.updated_at
            FROM subscriptions subscription
            INNER JOIN customers customer
                ON customer.id = subscription.customer_id
            WHERE subscription.status IN (
                    'active',
                    'pending_cancellation',
                    'suspended',
                    'pending_activation',
                    'pending_payment',
                    'pending_approval'
                )
              AND COALESCE(customer.is_demo, FALSE) = FALSE
            ORDER BY subscription.updated_at DESC, subscription.id DESC
            LIMIT 50;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            subscriptions.Add(new BillingV2BlockingLegacySubscription(
                MariaDbIdentifierReader.ReadRequired(reader, "subscription_id"),
                reader.GetString("status"),
                MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
                reader.GetString("customer_reference"),
                reader.GetString("customer_name"),
                MariaDbIdentifierReader.ReadNullable(
                    reader,
                    "commercial_offer_id"),
                reader.GetDateTime("created_at"),
                reader.GetDateTime("updated_at")));
        }

        return subscriptions;
    }
}

public static class BillingV2LaunchReadinessGate
{
    public static BillingV2LaunchReadinessSnapshot Evaluate(
        int realCustomerSubscriptionCount,
        int demoSubscriptionCount)
        => new(
            Math.Max(0, realCustomerSubscriptionCount),
            Math.Max(0, demoSubscriptionCount),
            VerifiedAgainstPersistentSql: true);
}
