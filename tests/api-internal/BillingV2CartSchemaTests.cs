using System.Security.Cryptography;
using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Test volontairement opt-in. Il exige une MariaDB explicitement jetable,
/// ayant deja recu la migration 090 ; il ne s'execute jamais dans la suite
/// smoke par defaut.
/// </summary>
public static class BillingV2CartSchemaTests
{
    private const string ConnectionVariable = "BILLING_V2_TEST_MARIADB_CONNECTION";

    public static async Task RunAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"{ConnectionVariable} doit designer une MariaDB de test jetable.");

        var ownerHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiredCartId = Guid.NewGuid().ToString("D");
        var replacementCartId = Guid.NewGuid().ToString("D");
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            await InsertAnonymousCartAsync(connection, transaction, expiredCartId,
                ownerHash, now.AddDays(-31));

            // Equivalent transactionnel du precheck current/get-or-create :
            // A open mais expire devient expired avant l'insertion de B.
            await using (var expire = connection.CreateCommand())
            {
                expire.Transaction = transaction;
                expire.CommandText = """
                    UPDATE billing_v2_carts SET status = 'expired', updated_at = @now
                    WHERE status = 'open' AND expires_at <= @now;
                    """;
                expire.Parameters.AddWithValue("@now", now);
                await expire.ExecuteNonQueryAsync();
            }

            await InsertAnonymousCartAsync(connection, transaction, replacementCartId,
                ownerHash, now.AddDays(30));
            await using var read = connection.CreateCommand();
            read.Transaction = transaction;
            read.CommandText = "SELECT status FROM billing_v2_carts WHERE id = @id;";
            read.Parameters.AddWithValue("@id", expiredCartId);
            var status = Convert.ToString(await read.ExecuteScalarAsync());
            if (!string.Equals(status, "expired", StringComparison.Ordinal))
                throw new InvalidOperationException("Le Cart A devait etre expire avant la creation de B.");

            await transaction.RollbackAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task InsertAnonymousCartAsync(MySqlConnection connection,
        MySqlTransaction transaction, string id, string ownerHash, DateTime expiresAtUtc)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO billing_v2_carts
                (id, anonymous_session_hash, open_anonymous_slot, status, currency, version,
                 created_at, updated_at, last_activity_at, expires_at)
            VALUES (@id, @owner, 1, 'open', 'EUR', 1, UTC_TIMESTAMP(6),
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), @expires);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@owner", ownerHash);
        command.Parameters.AddWithValue("@expires", expiresAtUtc);
        await command.ExecuteNonQueryAsync();
    }
}
