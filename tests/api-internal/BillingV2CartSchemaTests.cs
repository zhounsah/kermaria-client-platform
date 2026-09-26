using System.Security.Cryptography;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
        var claimToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var claimOwnerHash = new BillingV2CartOwner(null, claimToken).AnonymousTokenHash!;
        var claimCustomerId = Guid.NewGuid().ToString("D");
        var accountCartId = Guid.NewGuid().ToString("D");
        var anonymousCartId = Guid.NewGuid().ToString("D");
        var soleClaimToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var soleClaimHash = new BillingV2CartOwner(null, soleClaimToken).AnonymousTokenHash!;
        var soleClaimCartId = Guid.NewGuid().ToString("D");
        var soleClaimCustomerId = Guid.NewGuid().ToString("D");
        var raceToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var raceHash = new BillingV2CartOwner(null, raceToken).AnonymousTokenHash!;
        var raceCartId = Guid.NewGuid().ToString("D");
        var raceCustomerId = Guid.NewGuid().ToString("D");
        var explicitToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var explicitHash = new BillingV2CartOwner(null, explicitToken).AnonymousTokenHash!;
        var explicitAccountCartId = Guid.NewGuid().ToString("D");
        var explicitAnonymousCartId = Guid.NewGuid().ToString("D");
        var explicitCustomerId = Guid.NewGuid().ToString("D");
        try
        {
            var fixture = await ReadFilledCartFixtureAsync(connectionString);
            await VerifyTargetedExpiryReleasesCurrentSlotAsync(connectionString, expiryOwnerHash);
            // Cette seconde course part sans Cart preexistant : elle couvre
            // donc aussi l'arbitrage de creation par les indexes de slots.
            await VerifyConcurrentCurrentAsync(connectionString, concurrentOwnerHash);
            await VerifyConcurrentClaimKeepsAccountCartAsync(connectionString, claimToken,
                claimOwnerHash, claimCustomerId, accountCartId, anonymousCartId);
            await VerifySoleClaimAndClearAsync(connectionString, fixture, soleClaimToken,
                soleClaimHash, soleClaimCartId, soleClaimCustomerId);
            await VerifyConcurrentClaimWithoutAccountCartAsync(connectionString, fixture,
                raceToken, raceHash, raceCartId, raceCustomerId);
            await VerifyExplicitClaimResumesAccountCartAsync(connectionString, fixture,
                explicitToken, explicitHash, explicitCustomerId,
                explicitAccountCartId, explicitAnonymousCartId);
        }
        finally
        {
            // La cible est explicitement jetable ; le nettoyage est néanmoins
            // borne au hash aleatoire de cette execution.
            await DeleteOwnerCartsAsync(connectionString, expiryOwnerHash);
            await DeleteOwnerCartsAsync(connectionString, concurrentOwnerHash);
            await DeleteOwnerCartsAsync(connectionString, claimOwnerHash);
            await DeleteCartByIdAsync(connectionString, accountCartId);
            await DeleteCartByIdAsync(connectionString, anonymousCartId);
            await DeleteOwnerCartsAsync(connectionString, soleClaimHash);
            await DeleteCartByIdAsync(connectionString, soleClaimCartId);
            await DeleteOwnerCartsAsync(connectionString, raceHash);
            await DeleteCartByIdAsync(connectionString, raceCartId);
            await DeleteOwnerCartsAsync(connectionString, explicitHash);
            await DeleteCartByIdAsync(connectionString, explicitAccountCartId);
            await DeleteCartByIdAsync(connectionString, explicitAnonymousCartId);
        }
    }

    /// <summary>
    /// Contenu d'un vrai panier rempli, pris dans le catalogue seedé par les
    /// migrations : un preset d'au moins deux lignes et un couple engagement /
    /// mode de paiement actif. Aucun code catalogue n'est présumé.
    /// </summary>
    private sealed record FilledCartFixture(string PresetId,
        IReadOnlyList<(string PresetItemId, string ServiceId, string? TierId, string Scope, int Quantity)> Items,
        string CommitmentTermId, string PaymentMode);

    private sealed record CartRow(string Status, string? CustomerId, string? AnonymousHash,
        bool CustomerSlot, bool AnonymousSlot, int Version, string? PresetId,
        string? CommitmentTermId, string? PaymentMode, int ItemCount);

    private static async Task<FilledCartFixture> ReadFilledCartFixtureAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        string? presetId;
        await using (var preset = connection.CreateCommand())
        {
            preset.CommandText = """
                SELECT preset.id FROM billing_v2_offer_presets preset
                JOIN billing_v2_preset_items item ON item.preset_id = preset.id
                GROUP BY preset.id, preset.code HAVING COUNT(*) >= 2
                ORDER BY preset.code LIMIT 1;
                """;
            presetId = ToId(await preset.ExecuteScalarAsync());
        }
        if (presetId is null)
            throw new InvalidOperationException("Le catalogue seedé doit contenir un preset d'au moins deux lignes.");
        var items = new List<(string, string, string?, string, int)>();
        await using (var read = connection.CreateCommand())
        {
            read.CommandText = """
                SELECT id, service_id, tier_id, scope_template, quantity
                FROM billing_v2_preset_items WHERE preset_id = @preset
                ORDER BY display_order, id;
                """;
            read.Parameters.AddWithValue("@preset", presetId);
            await using var reader = await read.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                items.Add((ToId(reader.GetValue(0))!, ToId(reader.GetValue(1))!,
                    ToId(reader.GetValue(2)), reader.GetString(3), reader.GetInt32(4)));
        }
        await using var option = connection.CreateCommand();
        option.CommandText = """
            SELECT commitment_term_id, payment_mode FROM billing_v2_commitment_payment_options
            WHERE status = 'active' ORDER BY commitment_term_id, payment_mode LIMIT 1;
            """;
        await using var optionReader = await option.ExecuteReaderAsync();
        if (!await optionReader.ReadAsync())
            throw new InvalidOperationException("Le catalogue seedé doit contenir un couple engagement / mode de paiement actif.");
        return new FilledCartFixture(presetId, items,
            ToId(optionReader.GetValue(0))!, optionReader.GetString(1));
    }

    /// <summary>
    /// Insère un Cart open (client ou anonyme) portant le preset, l'engagement,
    /// le mode de paiement et les <paramref name="itemCount"/> premières lignes
    /// du preset, comme un panier réellement composé.
    /// </summary>
    private static async Task InsertFilledCartAsync(string connectionString, FilledCartFixture fixture,
        string cartId, string? customerId, string? anonymousHash, int itemCount)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var cart = connection.CreateCommand())
        {
            cart.Transaction = transaction;
            cart.CommandText = """
                INSERT INTO billing_v2_carts
                    (id, customer_id, anonymous_session_hash, open_customer_slot, open_anonymous_slot,
                     status, currency, commitment_term_id, payment_mode, source_preset_id, version,
                     created_at, updated_at, last_activity_at, expires_at)
                VALUES (@id, @customer, @anonymous, @customer_slot, @anonymous_slot,
                        'open', 'EUR', @commitment, @payment, @preset, 1,
                        UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), @expires);
                """;
            cart.Parameters.AddWithValue("@id", cartId);
            cart.Parameters.AddWithValue("@customer", (object?)customerId ?? DBNull.Value);
            cart.Parameters.AddWithValue("@anonymous", (object?)anonymousHash ?? DBNull.Value);
            cart.Parameters.AddWithValue("@customer_slot", customerId is null ? DBNull.Value : (object)1);
            cart.Parameters.AddWithValue("@anonymous_slot", anonymousHash is null ? DBNull.Value : (object)1);
            cart.Parameters.AddWithValue("@commitment", fixture.CommitmentTermId);
            cart.Parameters.AddWithValue("@payment", fixture.PaymentMode);
            cart.Parameters.AddWithValue("@preset", fixture.PresetId);
            cart.Parameters.AddWithValue("@expires", DateTime.UtcNow.AddDays(30));
            await cart.ExecuteNonQueryAsync();
        }
        for (var index = 0; index < itemCount; index++)
        {
            var item = fixture.Items[index];
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO billing_v2_cart_items
                    (id, cart_id, service_id, tier_id, quantity, scope_template,
                     source_preset_item_id, display_order)
                VALUES (@id, @cart, @service, @tier, @quantity, @scope, @preset_item, @order);
                """;
            insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            insert.Parameters.AddWithValue("@cart", cartId);
            insert.Parameters.AddWithValue("@service", item.ServiceId);
            insert.Parameters.AddWithValue("@tier", (object?)item.TierId ?? DBNull.Value);
            insert.Parameters.AddWithValue("@quantity", item.Quantity);
            insert.Parameters.AddWithValue("@scope", item.Scope);
            insert.Parameters.AddWithValue("@preset_item", item.PresetItemId);
            insert.Parameters.AddWithValue("@order", index);
            await insert.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    /// <summary>Relit l'état persistant réel, hors service, sur une connexion neuve.</summary>
    private static async Task<CartRow> ReadCartRowAsync(string connectionString, string cartId)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT status, customer_id, anonymous_session_hash, open_customer_slot,
                   open_anonymous_slot, version, source_preset_id, commitment_term_id, payment_mode,
                   (SELECT COUNT(*) FROM billing_v2_cart_items item WHERE item.cart_id = cart.id)
            FROM billing_v2_carts cart WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", cartId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Cart de test introuvable.");
        return new CartRow(reader.GetString(0), ToId(reader.GetValue(1)),
            reader.IsDBNull(2) ? null : reader.GetString(2), !reader.IsDBNull(3), !reader.IsDBNull(4),
            reader.GetInt32(5), ToId(reader.GetValue(6)), ToId(reader.GetValue(7)),
            reader.IsDBNull(8) ? null : reader.GetString(8), Convert.ToInt32(reader.GetValue(9)));
    }

    private static async Task<int> CountOpenCartsAsync(string connectionString,
        string? customerId, string? anonymousHash)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = customerId is not null
            ? "SELECT COUNT(*) FROM billing_v2_carts WHERE customer_id = @owner AND status = 'open';"
            : "SELECT COUNT(*) FROM billing_v2_carts WHERE anonymous_session_hash = @owner AND status = 'open';";
        command.Parameters.AddWithValue("@owner", customerId ?? anonymousHash!);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static string? ToId(object? value) => value switch
    {
        null or DBNull => null,
        Guid identifier => identifier.ToString("D"),
        _ => Convert.ToString(value)
    };

    /// <summary>
    /// Claim puis vidage d'un vrai panier rempli : le claim conserve le contenu,
    /// le vidage retire lignes et choix commerciaux sans changer d'identité.
    /// </summary>
    private static async Task VerifySoleClaimAndClearAsync(string connectionString,
        FilledCartFixture fixture, string token, string ownerHash, string cartId, string customerId)
    {
        var itemCount = fixture.Items.Count;
        await InsertFilledCartAsync(connectionString, fixture, cartId, null, ownerHash, itemCount);
        var service = CreateCartService(connectionString);
        var claimed = await service.ClaimCurrentAsync(token, customerId, CancellationToken.None);
        if (claimed.Code != "CART_CLAIMED" || claimed.Cart?.Id != cartId
            || claimed.Cart.CustomerId != customerId || claimed.Cart.Status != "open")
            throw new InvalidOperationException("Le claim sans panier client doit garder B et son identifiant.");
        var beforeClear = await ReadCartRowAsync(connectionString, cartId);
        if (beforeClear.ItemCount != itemCount || beforeClear.ItemCount < 2
            || beforeClear.PresetId != fixture.PresetId
            || beforeClear.CommitmentTermId != fixture.CommitmentTermId
            || beforeClear.PaymentMode != fixture.PaymentMode
            || beforeClear.Version != claimed.Cart.Version)
            throw new InvalidOperationException("Le claim doit conserver les lignes et les choix du panier.");

        var owner = new BillingV2CartOwner(customerId, null);
        var cleared = await service.ClearAsync(owner, cartId, claimed.Cart.Version, CancellationToken.None);
        if (cleared.Code != "CART_CLEARED" || cleared.Cart?.Id != cartId
            || cleared.Cart.Status != "open" || cleared.Cart.Version != claimed.Cart.Version + 1
            || cleared.Cart.Items.Count != 0 || cleared.Cart.SourcePresetId is not null
            || cleared.Cart.CommitmentTermId is not null || cleared.Cart.PaymentMode is not null)
            throw new InvalidOperationException("Le vidage doit conserver le Cart open et invalider sa version.");
        var afterClear = await ReadCartRowAsync(connectionString, cartId);
        if (afterClear != beforeClear with
            {
                Version = beforeClear.Version + 1, PresetId = null, CommitmentTermId = null,
                PaymentMode = null, ItemCount = 0
            })
            throw new InvalidOperationException("Le vidage persistant doit supprimer les lignes et remettre les choix à NULL.");

        var stale = await service.ClearAsync(owner, cartId, claimed.Cart.Version, CancellationToken.None);
        if (stale.Code != "CART_VERSION_CONFLICT")
            throw new InvalidOperationException("Un ancien expectedVersion doit être refusé après vidage.");
        if (await ReadCartRowAsync(connectionString, cartId) != afterClear)
            throw new InvalidOperationException("Un vidage refusé ne doit rien modifier.");
    }

    /// <summary>
    /// Course de claim_current sur un même panier anonyme, sans panier client :
    /// un seul gagnant revendique, les autres constatent qu'il n'y a plus rien
    /// à reprendre. Toute exception compte comme un échec (elle serait un 500).
    /// </summary>
    private static async Task VerifyConcurrentClaimWithoutAccountCartAsync(string connectionString,
        FilledCartFixture fixture, string token, string ownerHash, string cartId, string customerId)
    {
        await InsertFilledCartAsync(connectionString, fixture, cartId, null, ownerHash, fixture.Items.Count);
        var service = CreateCartService(connectionString);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try
            {
                var result = await service.ClaimCurrentAsync(token, customerId, CancellationToken.None);
                return (Code: result.Code, CartId: result.Cart?.Id);
            }
            catch (Exception exception)
            {
                return (Code: "EXCEPTION:" + exception.GetType().Name + ":" + exception.Message, CartId: (string?)null);
            }
        }));
        var codes = string.Join(",", outcomes.Select(outcome => outcome.Code));
        if (outcomes.Count(outcome => outcome.Code == "CART_CLAIMED") != 1
            || outcomes.Any(outcome => outcome.Code is not "CART_CLAIMED" and not "CART_NOTHING_TO_CLAIM")
            || outcomes.Single(outcome => outcome.Code == "CART_CLAIMED").CartId != cartId)
            throw new InvalidOperationException($"Course de claim sans panier client incohérente : {codes}.");

        var row = await ReadCartRowAsync(connectionString, cartId);
        if (row != new CartRow("open", customerId, null, true, false, 2, fixture.PresetId,
                fixture.CommitmentTermId, fixture.PaymentMode, fixture.Items.Count))
            throw new InvalidOperationException("Le panier revendiqué doit être open, au client, avec un seul slot.");
        if (await CountOpenCartsAsync(connectionString, customerId, null) != 1
            || await CountOpenCartsAsync(connectionString, null, ownerHash) != 0)
            throw new InvalidOperationException("La course doit laisser un seul panier open et aucun panier anonyme open.");
    }

    /// <summary>
    /// Chemin RESUMED via la commande claim explicite (version fournie par le
    /// client) : le panier client existant gagne sans fusion.
    /// </summary>
    private static async Task VerifyExplicitClaimResumesAccountCartAsync(string connectionString,
        FilledCartFixture fixture, string token, string ownerHash, string customerId,
        string accountCartId, string anonymousCartId)
    {
        await InsertFilledCartAsync(connectionString, fixture, accountCartId, customerId, null, 1);
        await InsertFilledCartAsync(connectionString, fixture, anonymousCartId, null, ownerHash, fixture.Items.Count);
        var accountBefore = await ReadCartRowAsync(connectionString, accountCartId);
        var anonymousBefore = await ReadCartRowAsync(connectionString, anonymousCartId);
        var service = CreateCartService(connectionString);

        var stale = await service.ClaimAsync(token, customerId, anonymousBefore.Version + 1, CancellationToken.None);
        if (stale.Code != "CART_VERSION_CONFLICT"
            || await ReadCartRowAsync(connectionString, anonymousCartId) != anonymousBefore
            || await ReadCartRowAsync(connectionString, accountCartId) != accountBefore)
            throw new InvalidOperationException("Un claim à version périmée doit être refusé sans effet.");

        var resumed = await service.ClaimAsync(token, customerId, anonymousBefore.Version, CancellationToken.None);
        if (resumed.Code != "CART_CLAIM_RESUMED" || resumed.Cart?.Id != accountCartId
            || resumed.Cart.CustomerId != customerId || resumed.Cart.Items.Count != 1)
            throw new InvalidOperationException("Le claim explicite doit reprendre le panier client existant.");
        if (await ReadCartRowAsync(connectionString, accountCartId) != accountBefore)
            throw new InvalidOperationException("Le panier client gagnant ne doit recevoir aucune fusion.");
        if (await ReadCartRowAsync(connectionString, anonymousCartId) != anonymousBefore with
            {
                Status = "expired", AnonymousSlot = false, Version = anonymousBefore.Version + 1
            })
            throw new InvalidOperationException("Le panier anonyme perdant doit être expiré, lignes conservées sur lui.");
        if (await CountOpenCartsAsync(connectionString, customerId, null) != 1
            || await CountOpenCartsAsync(connectionString, null, ownerHash) != 0)
            throw new InvalidOperationException("Le claim explicite doit laisser un seul panier open.");
    }

    private static BillingV2CartService CreateCartService(string connectionString)
        => new(new SqlRuntimeConfiguration(PortalPersistenceMode.MariaDb,
                "mariadb", connectionString, "disposable-test", true),
            null!, null!, NullLogger<BillingV2CartService>.Instance);

    private static async Task VerifyConcurrentClaimKeepsAccountCartAsync(string connectionString,
        string token, string ownerHash, string customerId, string accountCartId, string anonymousCartId)
    {
        await using (var connection = new MySqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await InsertAnonymousCartAsync(connection, transaction, anonymousCartId,
                ownerHash, DateTime.UtcNow.AddDays(30));
            await using var account = connection.CreateCommand();
            account.Transaction = transaction;
            account.CommandText = """
                INSERT INTO billing_v2_carts
                    (id, customer_id, open_customer_slot, status, currency, version,
                     created_at, updated_at, last_activity_at, expires_at)
                VALUES (@id, @customer, 1, 'open', 'EUR', 1, UTC_TIMESTAMP(6),
                        UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), @expires);
                """;
            account.Parameters.AddWithValue("@id", accountCartId);
            account.Parameters.AddWithValue("@customer", customerId);
            account.Parameters.AddWithValue("@expires", DateTime.UtcNow.AddDays(30));
            await account.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        var service = CreateCartService(connectionString);
        var claims = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => service.ClaimCurrentAsync(token, customerId, CancellationToken.None)));
        if (claims.Any(result => result.Code is not "CART_CLAIM_RESUMED" and not "CART_NOTHING_TO_CLAIM"))
            throw new InvalidOperationException("Les claims concurrents doivent aboutir sans conflit bloquant.");
        if (claims.All(result => result.Code != "CART_CLAIM_RESUMED")
            || claims.Where(result => result.Cart is not null)
                .Any(result => result.Cart!.Id != accountCartId))
            throw new InvalidOperationException("Le Cart customer existant doit toujours gagner.");

        await using var verification = new MySqlConnection(connectionString);
        await verification.OpenAsync();
        await using var check = verification.CreateCommand();
        check.CommandText = """
            SELECT id, status, open_customer_slot, open_anonymous_slot, version,
                   checked_out_subscription_id
            FROM billing_v2_carts WHERE id IN (@account, @anonymous);
            """;
        check.Parameters.AddWithValue("@account", accountCartId);
        check.Parameters.AddWithValue("@anonymous", anonymousCartId);
        var states = new Dictionary<string, (string Status, bool CustomerSlot, bool AnonymousSlot, int Version, bool CheckedOut)>();
        await using var reader = await check.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetValue(0) switch
            {
                Guid value => value.ToString("D"),
                var value => Convert.ToString(value)!
            };
            states[id] = (reader.GetString(1), !reader.IsDBNull(2),
                !reader.IsDBNull(3), reader.GetInt32(4), !reader.IsDBNull(5));
        }
        if (states.Count != 2
            || states[accountCartId] != ("open", true, false, 1, false)
            || states[anonymousCartId] != ("expired", false, false, 2, false))
            throw new InvalidOperationException("Le claim doit garder A open et liberer le slot de B.");
    }

    private static async Task DeleteCartByIdAsync(string connectionString, string id)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM billing_v2_carts WHERE id = @id;";
        delete.Parameters.AddWithValue("@id", id);
        await delete.ExecuteNonQueryAsync();
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
