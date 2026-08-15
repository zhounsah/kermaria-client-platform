using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Rail Stripe Billing V2 - Phase 2, scenarios d'idempotence sur base reelle.
///
/// Couvre les scenarios qui ne sont demontrables qu'avec une vraie base :
/// unicite de l'intention, de l'evenement financier et de la tentative sous
/// double appel, recuperation apres rafraichissement navigateur, rejeu de
/// callback et de webhook, et absence de lost update sous concurrence.
///
/// Exige BILLING_V2_TEST_MARIADB_CONNECTION vers une MariaDB JETABLE portant
/// les migrations 001 a 058.
/// </summary>
public static class BillingV2StripeRailSchemaTests
{
    public static async Task RunAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        var fixture = await RailFixture.CreateAsync(connection);
        try
        {
            await VerifyDoubleClickCreatesOneOfEachAsync(connection, fixture);
            await VerifyBrowserRefreshRecoversSameIntentAsync(connection, fixture);
            await VerifyDeliberateNewChoiceCreatesNewIntentAsync(
                connection,
                fixture);
            await VerifyRepeatedCallbackCreatesNoNewObjectAsync(
                connection,
                fixture);
            await VerifyRepeatedWebhookYieldsOneSettlementAsync(
                connection,
                fixture);
            await VerifyConcurrentEventsDoNotLostUpdateAsync(
                connection,
                fixture);
            await VerifyAmountMismatchNeverActivatesAsync(connection, fixture);
        }
        finally
        {
            await fixture.CleanupAsync(connection);
        }
    }

    // Scenario 1 : double clic / meme client_request_id
    private static async Task VerifyDoubleClickCreatesOneOfEachAsync(
        MySqlConnection connection,
        RailFixture fixture)
    {
        var canonical = BillingV2SubscriptionIntentKey.Canonical(
            fixture.Intent("req-double-click"));
        var hash = BillingV2SubscriptionIntentKey.Hash(canonical);

        var firstId = Guid.NewGuid().ToString("D");
        var secondId = Guid.NewGuid().ToString("D");
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var first = await BillingV2FinancialCoreStore.TryInsertIntentAsync(
                connection, transaction, firstId, fixture.SubscriptionId,
                "req-double-click", canonical, hash, 1,
                DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, default);
            var second = await BillingV2FinancialCoreStore.TryInsertIntentAsync(
                connection, transaction, secondId, fixture.SubscriptionId,
                "req-double-click", canonical, hash, 1,
                DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, default);
            Ensure(first, "Le premier clic doit creer l'intention.");
            Ensure(!second, "Scenario 1 : le second clic ne cree pas d'intention.");
            await transaction.CommitAsync();
        }

        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_subscription_changes "
                + "WHERE idempotency_key_hash = @p",
                hash) == 1,
            "Scenario 1 : exactement un SubscriptionChange.");

        // La demande de checkout existante est reliee a l'intention : c'est
        // par elle que la selection metier (client + offre + rail) est
        // retrouvee lors d'un rafraichissement navigateur.
        await fixture.InsertCheckoutRequestAsync(
            connection,
            firstId,
            "req-double-click");

        var billingEventId = await fixture.InsertBillingEventAsync(
            connection,
            firstId,
            "double-click",
            4046);
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_billing_events "
                + "WHERE subscription_change_id = @p",
                firstId) == 1,
            "Scenario 1 : exactement un BillingEvent.");

        // Deux resolutions successives de la tentative : une seule ligne.
        for (var i = 0; i < 2; i++)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await BillingV2FinancialCoreStore.ResolveOrCreateAttemptAsync(
                connection, transaction, billingEventId, "stripe", "test",
                4046, "EUR", DateTime.UtcNow, default);
            await transaction.CommitAsync();
        }

        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_payment_attempts "
                + "WHERE billing_event_id = @p",
                billingEventId) == 1,
            "Scenario 1 : exactement une PaymentAttempt.");
    }

    // Scenario 2 : rafraichissement navigateur
    private static async Task VerifyBrowserRefreshRecoversSameIntentAsync(
        MySqlConnection connection,
        RailFixture fixture)
    {
        // Le rafraichissement fabrique un NOUVEAU client_request_id : la
        // recherche par hash echoue, la recherche par selection metier doit
        // retomber sur l'intention deja ouverte.
        var refreshedCanonical = BillingV2SubscriptionIntentKey.Canonical(
            fixture.Intent("req-after-refresh"));
        var refreshedHash = BillingV2SubscriptionIntentKey.Hash(
            refreshedCanonical);

        Ensure(
            await BillingV2FinancialCoreStore.FindIntentByHashAsync(
                connection, null, refreshedHash, default) is null,
            "Un nouveau client_request_id ne matche pas par hash.");

        var recovered = await BillingV2FinancialCoreStore
            .FindOpenIntentForSelectionAsync(
                connection, null, fixture.CustomerId, fixture.LegacyOfferId,
                "stripe", "test", DateTime.UtcNow, default);
        Ensure(
            recovered is not null,
            "Scenario 2 : le rafraichissement doit retrouver l'intention ouverte.");
        Ensure(
            recovered!.SubscriptionId == fixture.SubscriptionId,
            "Scenario 2 : c'est bien le meme abonnement qui est repris.");
    }

    private static async Task VerifyDeliberateNewChoiceCreatesNewIntentAsync(
        MySqlConnection connection,
        RailFixture fixture)
    {
        var other = await BillingV2FinancialCoreStore
            .FindOpenIntentForSelectionAsync(
                connection, null, fixture.CustomerId, "PACK-DIFFERENT",
                "stripe", "test", DateTime.UtcNow, default);
        Ensure(
            other is null,
            "Un choix volontairement different ne doit pas reutiliser l'intention.");
    }

    // Scenario 3 : callback x2
    private static async Task VerifyRepeatedCallbackCreatesNoNewObjectAsync(
        MySqlConnection connection,
        RailFixture fixture)
    {
        var before = await FinancialObjectCountAsync(connection, fixture);
        for (var i = 0; i < 2; i++)
        {
            var canonical = BillingV2SubscriptionIntentKey.Canonical(
                fixture.Intent("req-double-click"));
            var hash = BillingV2SubscriptionIntentKey.Hash(canonical);
            var intent = await BillingV2FinancialCoreStore
                .FindIntentByHashAsync(connection, null, hash, default);
            Ensure(intent is not null, "Le callback doit retrouver l'intention.");

            await using var transaction = await connection.BeginTransactionAsync();
            await BillingV2FinancialCoreStore.ResolveOrCreateAttemptAsync(
                connection, transaction, fixture.BillingEventId!, "stripe",
                "test", 4046, "EUR", DateTime.UtcNow, default);
            await transaction.CommitAsync();
        }

        Ensure(
            await FinancialObjectCountAsync(connection, fixture) == before,
            "Scenario 3 : deux callbacks ne creent aucun objet financier.");
    }

    // Scenario 4 : webhook x3 => un seul settlement logique
    private static async Task VerifyRepeatedWebhookYieldsOneSettlementAsync(
        MySqlConnection connection,
        RailFixture fixture)
    {
        for (var i = 0; i < 3; i++)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await BillingV2FinancialCoreStore.ApplySettlementAsync(
                connection, transaction, fixture.BillingEventId!,
                BillingV2SettlementStatuses.Settled,
                "BILLING_V2_STRIPE_SETTLEMENT_CONFIRMED",
                DateTime.UtcNow.AddSeconds(i), default);
            await transaction.CommitAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT settlement_status, settled_at
            FROM billing_v2_billing_events WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", fixture.BillingEventId);
        await using var reader = await command.ExecuteReaderAsync();
        Ensure(await reader.ReadAsync(), "L'evenement doit exister.");
        var settledAt = reader.GetDateTime("settled_at");
        Ensure(
            reader.GetString("settlement_status")
                == BillingV2SettlementStatuses.Settled,
            "Scenario 4 : le settlement est acquis.");
        await reader.CloseAsync();

        // Un quatrieme rejeu ne doit pas deplacer l'horodatage : le settlement
        // est logiquement unique, pas repete.
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await BillingV2FinancialCoreStore.ApplySettlementAsync(
                connection, transaction, fixture.BillingEventId!,
                BillingV2SettlementStatuses.Settled, "REPLAY",
                DateTime.UtcNow.AddMinutes(5), default);
            await transaction.CommitAsync();
        }

        await using var recheck = connection.CreateCommand();
        recheck.CommandText =
            "SELECT settled_at FROM billing_v2_billing_events WHERE id = @id;";
        recheck.Parameters.AddWithValue("@id", fixture.BillingEventId);
        var after = (DateTime)(await recheck.ExecuteScalarAsync())!;
        Ensure(
            after == settledAt,
            "Scenario 4 : un rejeu ne redate pas un settlement deja acquis.");
    }

    // Scenario 9 : deux evenements Stripe concurrents
    private static async Task VerifyConcurrentEventsDoNotLostUpdateAsync(
        MySqlConnection connection,
        RailFixture fixture)
    {
        var version = await ScalarLongAsync(
            connection,
            "SELECT version FROM billing_v2_subscriptions WHERE id = @p",
            fixture.SubscriptionId);

        await using var transaction = await connection.BeginTransactionAsync();
        var winner = await BillingV2FinancialCoreStore
            .TryAdvanceSubscriptionAsync(
                connection, transaction, fixture.SubscriptionId, version,
                "active", DateTime.UtcNow, default);
        Ensure(winner.IsValid, "Le premier evenement doit gagner.");

        // Le second est parti de la MEME lecture de version : il doit perdre.
        var loser = await BillingV2FinancialCoreStore
            .TryAdvanceSubscriptionAsync(
                connection, transaction, fixture.SubscriptionId, version,
                "cancelled", DateTime.UtcNow, default);
        Ensure(
            !loser.IsValid
            && loser.ReasonCode == "BILLING_V2_SUBSCRIPTION_VERSION_CONFLICT",
            "Scenario 9 : le second doit remonter un conflit explicite.");
        await transaction.CommitAsync();

        Ensure(
            await ScalarLongAsync(
                connection,
                "SELECT version FROM billing_v2_subscriptions WHERE id = @p",
                fixture.SubscriptionId) == version + 1,
            "Scenario 9 : la version n'avance que d'un cran.");
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT status FROM billing_v2_subscriptions WHERE id = @p",
                fixture.SubscriptionId) == "active",
            "Scenario 9 : le perdant n'a pas ecrase le gagnant.");
    }

    // Scenarios 6/7 cote base : un ecart n'active jamais
    private static async Task VerifyAmountMismatchNeverActivatesAsync(
        MySqlConnection connection,
        RailFixture fixture)
    {
        var eventId = await fixture.InsertBillingEventAsync(
            connection,
            fixture.SecondChangeId!,
            "mismatch",
            5000);

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await BillingV2FinancialCoreStore.ApplySettlementAsync(
                connection, transaction, eventId,
                BillingV2SettlementStatuses.AmountMismatch,
                "BILLING_V2_SETTLEMENT_AMOUNT_MISMATCH",
                DateTime.UtcNow, default);
            await transaction.CommitAsync();
        }

        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT settlement_status FROM billing_v2_billing_events "
                + "WHERE id = @p",
                eventId) == BillingV2SettlementStatuses.AmountMismatch,
            "Un ecart de montant doit rester en amount_mismatch.");
        Ensure(
            await ScalarLongAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_billing_events "
                + "WHERE id = @p AND settled_at IS NOT NULL",
                eventId) == 0,
            "Un ecart de montant ne doit jamais horodater un encaissement.");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static async Task<long> FinancialObjectCountAsync(
        MySqlConnection connection,
        RailFixture fixture)
        => await ScalarLongAsync(
               connection,
               "SELECT COUNT(*) FROM billing_v2_billing_events "
               + "WHERE subscription_id = @p",
               fixture.SubscriptionId)
           + await ScalarLongAsync(
               connection,
               "SELECT COUNT(*) FROM billing_v2_payment_attempts attempt "
               + "INNER JOIN billing_v2_billing_events e "
               + "ON e.id = attempt.billing_event_id "
               + "WHERE e.subscription_id = @p",
               fixture.SubscriptionId)
           + await ScalarLongAsync(
               connection,
               "SELECT COUNT(*) FROM billing_v2_subscription_changes "
               + "WHERE subscription_id = @p",
               fixture.SubscriptionId);

    private static async Task<long> CountAsync(
        MySqlConnection connection,
        string sql,
        string parameter)
        => await ScalarLongAsync(connection, sql, parameter);

    private static async Task<long> ScalarLongAsync(
        MySqlConnection connection,
        string sql,
        string parameter)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@p", parameter);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt64(value);
    }

    private static async Task<string?> ScalarStringAsync(
        MySqlConnection connection,
        string sql,
        string parameter)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@p", parameter);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record RailFixture(
        string Marker,
        string CustomerId,
        string SubscriptionId,
        string ServiceId,
        string ServicePriceId,
        string LegacyOfferId)
    {
        public string? BillingEventId { get; set; }

        public string? SecondChangeId { get; set; }

        public BillingV2SubscriptionIntentRequest Intent(string clientRequestId)
            => new(CustomerId, clientRequestId, LegacyOfferId, "stripe", "test");

        public static async Task<RailFixture> CreateAsync(
            MySqlConnection connection)
        {
            var marker = $"bv2-rail-{Guid.NewGuid():N}";
            var fixture = new RailFixture(
                marker,
                Guid.NewGuid().ToString("D"),
                Guid.NewGuid().ToString("D"),
                Guid.NewGuid().ToString("D"),
                Guid.NewGuid().ToString("D"),
                $"PACK-{marker[..12]}");

            await ExecuteAsync(
                connection,
                """
                INSERT INTO customers (id, external_reference, display_name,
                    status, created_at, updated_at)
                VALUES (@id, @ref, 'Client rail Stripe', 'active',
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
                """,
                ("@id", fixture.CustomerId),
                ("@ref", marker));
            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_services (id, code, name, billing_type,
                    default_scope_type, discount_eligible, status,
                    created_at, updated_at)
                VALUES (@id, @code, 'Service rail', 'recurring', 'subscription',
                    1, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
                """,
                ("@id", fixture.ServiceId),
                ("@code", $"RAIL-{marker[..12]}"));
            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_service_prices (id, service_id,
                    price_code, price_version, amount_cents, currency,
                    billing_cadence, valid_from, status, created_at)
                VALUES (@id, @service_id, @code, 1, 4046, 'EUR', 'monthly',
                    '2026-01-01 00:00:00', 'active', UTC_TIMESTAMP(6));
                """,
                ("@id", fixture.ServicePriceId),
                ("@service_id", fixture.ServiceId),
                ("@code", $"RAIL-PRICE-{marker[..12]}"));
            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_subscriptions (id, customer_id, status,
                    payment_mode, currency, billing_model, created_at, updated_at)
                VALUES (@id, @customer_id, 'pending_approval', 'monthly', 'EUR',
                    'v2', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
                """,
                ("@id", fixture.SubscriptionId),
                ("@customer_id", fixture.CustomerId));
            return fixture;
        }

        public async Task InsertCheckoutRequestAsync(
            MySqlConnection connection,
            string changeId,
            string clientRequestId)
            => await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_authoritative_checkout_requests (
                    id, customer_id, idempotency_key, request_fingerprint_hash,
                    legacy_offer_id, provider, environment,
                    subscription_id, subscription_change_id,
                    status, created_at, updated_at)
                VALUES (@id, @customer_id, @key, SHA2(@key, 256),
                    @offer, 'stripe', 'test',
                    @subscription_id, @change_id,
                    'pending', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
                """,
                ("@id", Guid.NewGuid().ToString("D")),
                ("@customer_id", CustomerId),
                ("@key", clientRequestId),
                ("@offer", LegacyOfferId),
                ("@subscription_id", SubscriptionId),
                ("@change_id", changeId));

        public async Task<string> InsertBillingEventAsync(
            MySqlConnection connection,
            string changeId,
            string keySuffix,
            long amountCents)
        {
            var eventId = Guid.NewGuid().ToString("D");
            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_billing_events (
                    id, customer_id, subscription_id, subscription_change_id,
                    event_type, direction, financial_status, settlement_status,
                    document_status, currency, period_start, period_end,
                    payment_mode_snapshot, commitment_months_snapshot,
                    discount_basis_points_snapshot,
                    gross_amount_cents, discount_amount_cents, net_amount_cents,
                    tax_amount_cents, total_amount_cents, pricing_engine_version,
                    idempotency_key_canonical, idempotency_key_hash,
                    created_at, finalized_at)
                VALUES (@id, @customer_id, @subscription_id, @change_id,
                    'initial_charge', 'debit', 'finalized', 'none',
                    'none', 'EUR', '2026-08-01', '2026-09-01',
                    'monthly', 12, 1500,
                    @amount, 0, @amount, 0, @amount, 'pricing-engine-v1',
                    @canonical, SHA2(@canonical, 256),
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
                """,
                ("@id", eventId),
                ("@customer_id", CustomerId),
                ("@subscription_id", SubscriptionId),
                ("@change_id", changeId),
                ("@amount", amountCents),
                ("@canonical", $"{Marker}|{keySuffix}"));
            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_billing_event_lines (
                    id, billing_event_id, service_id, service_price_id,
                    service_code, description, billing_cadence,
                    quantity, unit_amount_cents, gross_amount_cents,
                    discount_allocated_amount_cents, net_amount_cents,
                    tax_amount_cents, total_amount_cents, currency,
                    period_start, period_end, display_order, created_at)
                VALUES (@id, @event_id, @service_id, @price_id,
                    'RAIL', 'Ligne rail', 'monthly',
                    1, @amount, @amount, 0, @amount, 0, @amount, 'EUR',
                    '2026-08-01', '2026-09-01', 0, UTC_TIMESTAMP(6));
                """,
                ("@id", Guid.NewGuid().ToString("D")),
                ("@event_id", eventId),
                ("@service_id", ServiceId),
                ("@price_id", ServicePriceId),
                ("@amount", amountCents));

            BillingEventId ??= eventId;
            if (SecondChangeId is null)
            {
                SecondChangeId = changeId;
            }

            return eventId;
        }

        public async Task CleanupAsync(MySqlConnection connection)
        {
            foreach (var sql in new[]
                     {
                         "DELETE a FROM billing_v2_payment_attempts a "
                         + "INNER JOIN billing_v2_billing_events e "
                         + "ON e.id = a.billing_event_id "
                         + "WHERE e.subscription_id = @id",
                         "DELETE l FROM billing_v2_billing_event_lines l "
                         + "INNER JOIN billing_v2_billing_events e "
                         + "ON e.id = l.billing_event_id "
                         + "WHERE e.subscription_id = @id",
                         "DELETE FROM billing_v2_billing_events WHERE subscription_id = @id",
                         "DELETE FROM billing_v2_authoritative_checkout_requests "
                         + "WHERE subscription_id = @id",
                         "DELETE FROM billing_v2_subscription_changes WHERE subscription_id = @id",
                         "DELETE FROM billing_v2_subscriptions WHERE id = @id"
                     })
            {
                await ExecuteAsync(connection, sql, ("@id", SubscriptionId));
            }

            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_service_prices WHERE id = @id",
                ("@id", ServicePriceId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_services WHERE id = @id",
                ("@id", ServiceId));
            await ExecuteAsync(
                connection,
                "DELETE FROM customers WHERE id = @id",
                ("@id", CustomerId));
        }

        private static async Task ExecuteAsync(
            MySqlConnection connection,
            string sql,
            params (string Name, object Value)[] parameters)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            await command.ExecuteNonQueryAsync();
        }
    }
}
