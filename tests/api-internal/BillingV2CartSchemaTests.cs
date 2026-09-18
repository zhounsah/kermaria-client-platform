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

        var expiryOwnerHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var concurrentOwnerHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        try
        {
            await VerifyTargetedExpiryReleasesCurrentSlotAsync(connectionString, expiryOwnerHash);
            // Cette seconde course part sans Cart preexistant : elle couvre
            // donc aussi l'arbitrage de creation par les indexes de slots.
            await VerifyConcurrentCurrentAsync(connectionString, concurrentOwnerHash);
        }
        finally
        {
            // La cible est explicitement jetable ; le nettoyage est néanmoins
            // borne au hash aleatoire de cette execution.
            await DeleteOwnerCartsAsync(connectionString, expiryOwnerHash);
            await DeleteOwnerCartsAsync(connectionString, concurrentOwnerHash);
        }
    }

    private static async Task VerifyTargetedExpiryReleasesCurrentSlotAsync(
        string connectionString, string ownerHash)
    {
        var expiredCartId = Guid.NewGuid().ToString("D");
        var replacementCartId = Guid.NewGuid().ToString("D");
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        await InsertAnonymousCartAsync(connection, transaction, expiredCartId,
            ownerHash, now.AddDays(-31));

        await using (var expire = connection.CreateCommand())
        {
            expire.Transaction = transaction;
            expire.CommandText = """
                UPDATE billing_v2_carts
                SET status = 'expired', open_customer_slot = NULL,
                    open_anonymous_slot = NULL, updated_at = @now
                WHERE status = 'open' AND expires_at <= @now
                  AND anonymous_session_hash = @owner AND open_anonymous_slot = 1
                  AND currency = 'EUR';
                """;
            expire.Parameters.AddWithValue("@now", now);
            expire.Parameters.AddWithValue("@owner", ownerHash);
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
        await transaction.CommitAsync();
    }

    /// <summary>
    /// Reproduit le point de contention current avec dix transactions et la
    /// meme contrainte de slots que le service. Cette verification est opt-in
    /// car elle execute de vraies transactions InnoDB sur une base jetable.
    /// </summary>
    private static async Task VerifyConcurrentCurrentAsync(string connectionString, string ownerHash)
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => GetOrCreateAnonymousCurrentAsync(connectionString, ownerHash)));
        if (results.Distinct(StringComparer.Ordinal).Count() != 1)
            throw new InvalidOperationException("Les current concurrents doivent converger vers un seul Cart.");

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var count = connection.CreateCommand();
        count.CommandText = """
            SELECT COUNT(*) FROM billing_v2_carts
            WHERE anonymous_session_hash = @owner AND currency = 'EUR'
              AND status = 'open' AND open_anonymous_slot = 1;
            """;
        count.Parameters.AddWithValue("@owner", ownerHash);
        if (Convert.ToInt32(await count.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("Un seul Cart open owner/devise doit subsister apres la course.");
    }

    private static async Task<string> GetOrCreateAnonymousCurrentAsync(string connectionString, string ownerHash)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                var now = DateTime.UtcNow;
                await ExpireAnonymousCurrentAsync(connection, transaction, ownerHash, now);
                var cartId = await ReadAnonymousCurrentAsync(connection, transaction, ownerHash, false);
                if (cartId is null)
                {
                    try
                    {
                        cartId = Guid.NewGuid().ToString("D");
                        await InsertAnonymousCartAsync(connection, transaction, cartId, ownerHash, now.AddDays(30));
                    }
                    catch (MySqlException exception) when (exception.Number == 1062)
                    {
                        cartId = await ReadAnonymousCurrentAsync(connection, transaction, ownerHash, true);
                    }
                }
                else
                {
                    cartId = await ReadAnonymousCurrentAsync(connection, transaction, ownerHash, true);
                }
                if (cartId is null) throw new InvalidOperationException("Cart concurrent absent apres collision de cle.");
                await using var touch = connection.CreateCommand();
                touch.Transaction = transaction;
                touch.CommandText = """
                    UPDATE billing_v2_carts SET updated_at = @now, last_activity_at = @now,
                        expires_at = @expires WHERE id = @id AND status = 'open';
                    """;
                touch.Parameters.AddWithValue("@id", cartId);
                touch.Parameters.AddWithValue("@now", now);
                touch.Parameters.AddWithValue("@expires", now.AddDays(30));
                await touch.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
                return cartId;
            }
            catch (MySqlException exception) when ((exception.Number == 1213
                || string.Equals(exception.SqlState, "40001", StringComparison.Ordinal)) && attempt < 2)
            {
                // Le retry repart d'une transaction et d'une connexion neuves.
            }
        }
        throw new InvalidOperationException("La course current a epuise les retries de deadlock.");
    }

    private static async Task ExpireAnonymousCurrentAsync(MySqlConnection connection,
        MySqlTransaction transaction, string ownerHash, DateTime now)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE billing_v2_carts
            SET status = 'expired', open_customer_slot = NULL,
                open_anonymous_slot = NULL, updated_at = @now
            WHERE status = 'open' AND expires_at <= @now
              AND anonymous_session_hash = @owner AND open_anonymous_slot = 1
              AND currency = 'EUR';
            """;
        command.Parameters.AddWithValue("@owner", ownerHash);
        command.Parameters.AddWithValue("@now", now);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadAnonymousCurrentAsync(MySqlConnection connection,
        MySqlTransaction transaction, string ownerHash, bool forUpdate)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id FROM billing_v2_carts
            WHERE anonymous_session_hash = @owner AND open_anonymous_slot = 1
              AND status = 'open' AND currency = 'EUR'
            """ + (forUpdate ? " FOR UPDATE;" : ";");
        command.Parameters.AddWithValue("@owner", ownerHash);
        var value = await command.ExecuteScalarAsync();
        return value switch
        {
            Guid identifier => identifier.ToString("D"),
            null or DBNull => null,
            _ => Convert.ToString(value)
        };
    }

    private static async Task DeleteOwnerCartsAsync(string connectionString, string ownerHash)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM billing_v2_carts WHERE anonymous_session_hash = @owner;";
        delete.Parameters.AddWithValue("@owner", ownerHash);
        await delete.ExecuteNonQueryAsync();
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
