using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbSubscriptionRepository : ISubscriptionRepository
{
    private readonly string _connectionString;

    public MariaDbSubscriptionRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<SubscriptionSummary>> GetByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var rows = new List<SubscriptionSummary>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BaseSelect + """
            WHERE subscription.customer_id = @customerId
            ORDER BY subscription.updated_at DESC, subscription.id DESC;
            """;
        command.Parameters.AddWithValue("customerId", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(Read(reader));
        }

        return rows;
    }

    public async Task<IReadOnlyList<SubscriptionSummary>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var rows = new List<SubscriptionSummary>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BaseSelect + """
            ORDER BY subscription.updated_at DESC, subscription.id DESC;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(Read(reader));
        }

        return rows;
    }

    public async Task<SubscriptionSummary?> GetByIdAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BaseSelect + """
            WHERE subscription.id = @id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("id", subscriptionId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Read(reader);
    }

    private async Task<MySqlConnection> OpenAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private const string BaseSelect =
        """
        SELECT
            subscription.id,
            subscription.customer_id,
            customer.external_reference AS customer_reference,
            customer.display_name AS customer_name,
            subscription.commercial_offer_id,
            offer.name AS offer_name,
            subscription.paypal_plan_id,
            subscription.paypal_subscription_id,
            subscription.status,
            offer.price_amount_cents,
            offer.currency,
            subscription.started_at,
            subscription.next_billing_at,
            subscription.cancelled_at,
            subscription.created_at,
            subscription.updated_at
        FROM subscriptions subscription
        INNER JOIN customers customer
            ON customer.id = subscription.customer_id
        INNER JOIN commercial_offers offer
            ON offer.id = subscription.commercial_offer_id
        """;

    private static SubscriptionSummary Read(MySqlDataReader reader)
        => new(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            reader.GetString("customer_reference"),
            reader.GetString("customer_name"),
            MariaDbIdentifierReader.ReadRequired(reader, "commercial_offer_id"),
            reader.GetString("offer_name"),
            reader.GetString("paypal_plan_id"),
            reader.IsDBNull(reader.GetOrdinal("paypal_subscription_id"))
                ? null
                : reader.GetString("paypal_subscription_id"),
            reader.GetString("status"),
            reader.GetInt32("price_amount_cents"),
            reader.GetString("currency"),
            ReadNullableIso(reader, "started_at"),
            ReadNullableIso(reader, "next_billing_at"),
            ReadNullableIso(reader, "cancelled_at"),
            ToIso(reader.GetDateTime("created_at")),
            ToIso(reader.GetDateTime("updated_at")));

    private static string? ReadNullableIso(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column))
            ? null
            : ToIso(reader.GetDateTime(column));

    private static string ToIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}
