using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbRecurringCheckoutRepository
    : IRecurringCheckoutRepository
{
    private readonly string _connectionString;

    public MariaDbRecurringCheckoutRepository(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<RecurringCheckoutItemRecord>> GetItemsAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var items = new List<RecurringCheckoutItemRecord>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT offer_id, commitment_months, payment_mode
            FROM recurring_checkout_items
            WHERE customer_id = @customerId
            ORDER BY created_at, id
            """;
        command.Parameters.AddWithValue("customerId", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new RecurringCheckoutItemRecord(
                MariaDbIdentifierReader.ReadRequired(reader, "offer_id"),
                reader.GetInt32("commitment_months"),
                reader.GetString("payment_mode")));
        }

        return items;
    }

    public async Task UpsertItemAsync(
        string customerId,
        string offerId,
        int commitmentMonths,
        string paymentMode,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO recurring_checkout_items (
                id,
                customer_id,
                offer_id,
                commitment_months,
                payment_mode,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @customerId,
                @offerId,
                @commitmentMonths,
                @paymentMode,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            )
            ON DUPLICATE KEY UPDATE
                commitment_months = @commitmentMonths,
                payment_mode = @paymentMode,
                updated_at = UTC_TIMESTAMP(6)
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("customerId", customerId);
        command.Parameters.AddWithValue("offerId", offerId);
        command.Parameters.AddWithValue("commitmentMonths", commitmentMonths);
        command.Parameters.AddWithValue("paymentMode", paymentMode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveItemAsync(
        string customerId,
        string offerId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM recurring_checkout_items
            WHERE customer_id = @customerId
              AND offer_id = @offerId
            """;
        command.Parameters.AddWithValue("customerId", customerId);
        command.Parameters.AddWithValue("offerId", offerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM recurring_checkout_items
            WHERE customer_id = @customerId
            """;
        command.Parameters.AddWithValue("customerId", customerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
