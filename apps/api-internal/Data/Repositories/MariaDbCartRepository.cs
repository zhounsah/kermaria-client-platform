using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbCartRepository : ICartRepository
{
    private readonly string _connectionString;

    public MariaDbCartRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<CartItemRecord>> GetItemsAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var items = new List<CartItemRecord>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT offer_id, quantity
            FROM cart_items
            WHERE customer_id = @customerId
            ORDER BY created_at
            """;
        command.Parameters.AddWithValue("customerId", customerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new CartItemRecord(
                reader.GetString(0),
                reader.GetInt32(1)));
        }

        return items;
    }

    public async Task UpsertItemAsync(
        string customerId,
        string offerId,
        int quantity,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO cart_items (
                id, customer_id, offer_id, quantity, created_at, updated_at
            ) VALUES (
                @id, @customerId, @offerId, @quantity, NOW(6), NOW(6)
            )
            ON DUPLICATE KEY UPDATE
                quantity = @quantity,
                updated_at = NOW(6)
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("customerId", customerId);
        command.Parameters.AddWithValue("offerId", offerId);
        command.Parameters.AddWithValue("quantity", quantity);
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
            DELETE FROM cart_items
            WHERE customer_id = @customerId AND offer_id = @offerId
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
            DELETE FROM cart_items WHERE customer_id = @customerId
            """;
        command.Parameters.AddWithValue("customerId", customerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
