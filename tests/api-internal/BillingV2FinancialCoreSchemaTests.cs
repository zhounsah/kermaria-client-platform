using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Tests des invariants DB du coeur financier Billing V2 (Phase 1).
///
/// Ces tests exigent une MariaDB JETABLE, fournie explicitement via la variable
/// d'environnement BILLING_V2_TEST_MARIADB_CONNECTION. Sans elle, la suite ne
/// s'execute pas et le dit clairement : elle n'est jamais silencieusement
/// "verte" par absence de base.
///
/// La base pointee doit deja porter les migrations 001 a 057. Ces tests
/// n'ecrivent que dans les tables du coeur financier et nettoient derriere eux.
///
/// Ne JAMAIS pointer cette variable vers une base de recette ou de production.
/// </summary>
public static class BillingV2FinancialCoreSchemaTests
{
    private const string ConnectionVariable =
        "BILLING_V2_TEST_MARIADB_CONNECTION";

    public static async Task RunAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} n'est pas defini. Cette suite exige une "
                + "MariaDB jetable portant les migrations 001 a 057. "
                + "Elle ne peut pas etre consideree comme passee sans base.");
        }

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        await VerifySchemaShapeAsync(connection);

        var fixture = await BillingV2SchemaFixture.CreateAsync(connection);
        try
        {
            await VerifyCoherentEventIsAcceptedAsync(connection, fixture);
            await VerifyTotalMustEqualNetPlusTaxAsync(connection, fixture);
            await VerifyNetMustEqualGrossMinusDiscountAsync(connection, fixture);
            await VerifyNegativeAmountIsRejectedAsync(connection, fixture);
            await VerifyEmptyCurrencyIsRejectedAsync(connection, fixture);
            await VerifyPeriodMustBeOrderedAsync(connection, fixture);
            await VerifyFinalizedRequiresTimestampAsync(connection, fixture);
            await VerifyIdempotencyKeyIsUniqueAsync(connection, fixture);
            await VerifyUnsupportedStatusIsRejectedAsync(connection, fixture);
            await VerifyLineArithmeticIsEnforcedAsync(connection, fixture);
            await VerifyPaymentAttemptKeyIsUniqueAsync(connection, fixture);
            await VerifySucceededAttemptRequiresMatchingSettlementAsync(
                connection,
                fixture);
            await VerifyOptimisticLockingAsync(connection, fixture);
        }
        finally
        {
            await fixture.CleanupAsync(connection);
        }

        // Phase 2 : scenarios d'idempotence du rail Stripe sur la meme base.
        await BillingV2StripeRailSchemaTests.RunAsync(connectionString);

        // Phase 2.5 : scenarios de panne (reconciliation, cycles, BPCE).
        await BillingV2HardeningSchemaTests.RunAsync(connectionString);

        // Phase 3 : cycle de vie du renouvellement Stripe.
        await BillingV2RenewalSchemaTests.RunAsync(connectionString);
    }

    private static async Task VerifySchemaShapeAsync(MySqlConnection connection)
    {
        Ensure(
            await ScalarLongAsync(
                connection,
                """
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'billing_v2_subscriptions'
                  AND COLUMN_NAME = 'version'
                  AND DATA_TYPE = 'bigint'
                  AND IS_NULLABLE = 'NO'
                  AND COLUMN_DEFAULT = '1';
                """) == 1,
            "version doit exister en BIGINT NOT NULL DEFAULT 1.");

        foreach (var table in new[]
                 {
                     "billing_v2_billing_events",
                     "billing_v2_billing_event_lines",
                     "billing_v2_payment_attempts"
                 })
        {
            Ensure(
                await ScalarLongAsync(
                    connection,
                    $"""
                     SELECT COUNT(*) FROM information_schema.TABLES
                     WHERE TABLE_SCHEMA = DATABASE()
                       AND TABLE_NAME = '{table}';
                     """) == 1,
                $"La table {table} doit exister.");
        }

        foreach (var column in new[]
                 {
                     "client_request_id",
                     "idempotency_key_canonical",
                     "idempotency_key_hash",
                     "base_subscription_version",
                     "expires_at",
                     "failure_reason_code",
                     "reconciliation_reason_code"
                 })
        {
            Ensure(
                await ScalarLongAsync(
                    connection,
                    $"""
                     SELECT COUNT(*) FROM information_schema.COLUMNS
                     WHERE TABLE_SCHEMA = DATABASE()
                       AND TABLE_NAME = 'billing_v2_subscription_changes'
                       AND COLUMN_NAME = '{column}';
                     """) == 1,
                $"billing_v2_subscription_changes.{column} doit exister.");
        }

        foreach (var table in new[]
                 {
                     "billing_v2_authoritative_checkout_requests",
                     "billing_v2_provider_checkout_sessions",
                     "billing_v2_subscription_documents"
                 })
        {
            Ensure(
                await ScalarLongAsync(
                    connection,
                    $"""
                     SELECT COUNT(*) FROM information_schema.COLUMNS
                     WHERE TABLE_SCHEMA = DATABASE()
                       AND TABLE_NAME = '{table}'
                       AND COLUMN_NAME = 'billing_event_id';
                     """) == 1,
                $"{table} doit pouvoir referencer un billing_event_id.");
        }

        // DB-18 : 1:1 BillingEvent <-> document.
        Ensure(
            await ScalarLongAsync(
                connection,
                """
                SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'billing_v2_subscription_documents'
                  AND INDEX_NAME =
                      'uq_billing_v2_subscription_document_billing_event'
                  AND NON_UNIQUE = 0;
                """) == 1,
            "DB-18 : un document V2 ne peut referencer qu'un BillingEvent.");
    }

    private static async Task VerifyCoherentEventIsAcceptedAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
    {
        var eventId = await InsertEventAsync(
            connection,
            fixture,
            key: "coherent",
            gross: 2000,
            discount: 200,
            net: 1800,
            tax: 360,
            total: 2160);
        Ensure(eventId is not null, "Un evenement coherent doit etre accepte.");
    }

    private static async Task VerifyTotalMustEqualNetPlusTaxAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
        => await EnsureRejectedAsync(
            () => InsertEventAsync(
                connection,
                fixture,
                key: "total-mismatch",
                gross: 1000,
                discount: 0,
                net: 1000,
                tax: 200,
                total: 1100),
            "DB-1 : total doit valoir net + taxe.");

    private static async Task VerifyNetMustEqualGrossMinusDiscountAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
        => await EnsureRejectedAsync(
            () => InsertEventAsync(
                connection,
                fixture,
                key: "net-mismatch",
                gross: 1000,
                discount: 100,
                net: 950,
                tax: 0,
                total: 950),
            "DB-2 : net doit valoir brut - remise.");

    private static async Task VerifyNegativeAmountIsRejectedAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
        => await EnsureRejectedAsync(
            () => InsertEventAsync(
                connection,
                fixture,
                key: "negative",
                gross: 1000,
                discount: 0,
                net: 1000,
                tax: -100,
                total: 900),
            "DB-3 : aucun montant negatif.");

    private static async Task VerifyEmptyCurrencyIsRejectedAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
        => await EnsureRejectedAsync(
            () => InsertEventAsync(
                connection,
                fixture,
                key: "empty-currency",
                gross: 1000,
                discount: 0,
                net: 1000,
                tax: 0,
                total: 1000,
                currency: "  "),
            "DB-5 : la devise ne peut pas etre vide.");

    private static async Task VerifyPeriodMustBeOrderedAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
        => await EnsureRejectedAsync(
            () => InsertEventAsync(
                connection,
                fixture,
                key: "period",
                gross: 1000,
                discount: 0,
                net: 1000,
                tax: 0,
                total: 1000,
                periodStart: "2026-09-01 00:00:00",
                periodEnd: "2026-08-01 00:00:00"),
            "DB-6 : period_end doit suivre period_start.");

    private static async Task VerifyFinalizedRequiresTimestampAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
        => await EnsureRejectedAsync(
            () => InsertEventAsync(
                connection,
                fixture,
                key: "finalized-no-timestamp",
                gross: 1000,
                discount: 0,
                net: 1000,
                tax: 0,
                total: 1000,
                financialStatus: "finalized"),
            "DB-11 : finalized exige finalized_at.");

    private static async Task VerifyIdempotencyKeyIsUniqueAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
    {
        await InsertEventAsync(
            connection,
            fixture,
            key: "idempotency-once",
            gross: 1000,
            discount: 0,
            net: 1000,
            tax: 0,
            total: 1000);

        await EnsureRejectedAsync(
            () => InsertEventAsync(
                connection,
                fixture,
                key: "idempotency-once",
                gross: 500,
                discount: 0,
                net: 500,
                tax: 0,
                total: 500),
            "DB-13 : une cle d'idempotence ne se reutilise jamais.");
    }

    private static async Task VerifyUnsupportedStatusIsRejectedAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
        => await EnsureRejectedAsync(
            () => InsertEventAsync(
                connection,
                fixture,
                key: "bad-settlement",
                gross: 1000,
                discount: 0,
                net: 1000,
                tax: 0,
                total: 1000,
                settlementStatus: "probably_paid"),
            "DB-9 : settlement_status hors enumeration doit etre rejete.");

    private static async Task VerifyLineArithmeticIsEnforcedAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
    {
        var eventId = await InsertEventAsync(
            connection,
            fixture,
            key: "with-lines",
            gross: 1000,
            discount: 0,
            net: 1000,
            tax: 0,
            total: 1000);

        await EnsureRejectedAsync(
            () => InsertLineAsync(
                connection,
                fixture,
                eventId!,
                displayOrder: 0,
                quantity: 2,
                unit: 500,
                gross: 900,
                net: 900,
                total: 900),
            "DB-16 : brut de ligne = unitaire x quantite.");

        await EnsureRejectedAsync(
            () => InsertLineAsync(
                connection,
                fixture,
                eventId!,
                displayOrder: 1,
                quantity: 0,
                unit: 500,
                gross: 0,
                net: 0,
                total: 0),
            "DB-17 : la quantite doit etre strictement positive.");
    }

    private static async Task VerifyPaymentAttemptKeyIsUniqueAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
    {
        var eventId = await InsertEventAsync(
            connection,
            fixture,
            key: "attempt-host",
            gross: 1000,
            discount: 0,
            net: 1000,
            tax: 0,
            total: 1000);

        await InsertAttemptAsync(
            connection,
            fixture,
            eventId!,
            requestKey: "shared-request-key",
            status: "in_flight");

        await EnsureRejectedAsync(
            () => InsertAttemptAsync(
                connection,
                fixture,
                eventId!,
                requestKey: "shared-request-key",
                status: "created"),
            "DB-15 : provider_request_key unique par provider/environnement.");
    }

    private static async Task
        VerifySucceededAttemptRequiresMatchingSettlementAsync(
            MySqlConnection connection,
            BillingV2SchemaFixture fixture)
    {
        var eventId = await InsertEventAsync(
            connection,
            fixture,
            key: "attempt-settlement",
            gross: 1000,
            discount: 0,
            net: 1000,
            tax: 0,
            total: 1000);

        await EnsureRejectedAsync(
            () => InsertAttemptAsync(
                connection,
                fixture,
                eventId!,
                requestKey: "settle-short",
                status: "succeeded",
                expectedAmount: 2160,
                settledAmount: 1990,
                settledCurrency: "EUR"),
            "APP-10 en base : un succes exige settled == expected.");

        await EnsureRejectedAsync(
            () => InsertAttemptAsync(
                connection,
                fixture,
                eventId!,
                requestKey: "settle-currency",
                status: "succeeded",
                expectedAmount: 2160,
                settledAmount: 2160,
                settledCurrency: "USD"),
            "APP-11 en base : un succes exige la meme devise.");

        await EnsureRejectedAsync(
            () => InsertAttemptAsync(
                connection,
                fixture,
                eventId!,
                requestKey: "settle-unknown",
                status: "succeeded",
                expectedAmount: 2160,
                settledAmount: null,
                settledCurrency: null),
            "Un succes sans montant constate doit etre rejete.");

        await InsertAttemptAsync(
            connection,
            fixture,
            eventId!,
            requestKey: "settle-exact",
            status: "succeeded",
            expectedAmount: 2160,
            settledAmount: 2160,
            settledCurrency: "EUR");
    }

    private static async Task VerifyOptimisticLockingAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture)
    {
        var initialVersion = await ScalarLongAsync(
            connection,
            $"""
             SELECT version FROM billing_v2_subscriptions
             WHERE id = '{fixture.SubscriptionId}';
             """);
        Ensure(initialVersion == 1, "La version initiale deterministe est 1.");

        var winner = await CompareAndSwapAsync(
            connection,
            fixture.SubscriptionId,
            expectedVersion: 1);
        Ensure(
            BillingV2FinancialCoreVersionAssert(winner),
            "Le premier compare-and-swap doit gagner.");

        // Deuxieme ecrivain, parti de la meme lecture : il doit perdre, et
        // c'est ce zero ligne affectee qui empeche le lost update.
        var loser = await CompareAndSwapAsync(
            connection,
            fixture.SubscriptionId,
            expectedVersion: 1);
        Ensure(
            loser == 0,
            "APP-14 : un ecrivain sur version perimee doit affecter 0 ligne.");

        Ensure(
            await ScalarLongAsync(
                connection,
                $"""
                 SELECT version FROM billing_v2_subscriptions
                 WHERE id = '{fixture.SubscriptionId}';
                 """) == 2,
            "La version ne doit avancer que d'un cran malgre deux ecrivains.");
    }

    private static bool BillingV2FinancialCoreVersionAssert(int affectedRows)
        => affectedRows == 1;

    private static async Task<int> CompareAndSwapAsync(
        MySqlConnection connection,
        string subscriptionId,
        long expectedVersion)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE billing_v2_subscriptions
            SET status = 'active',
                version = version + 1,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND version = @expected_version;
            """;
        command.Parameters.AddWithValue("@id", subscriptionId);
        command.Parameters.AddWithValue("@expected_version", expectedVersion);
        return await command.ExecuteNonQueryAsync();
    }

    // ------------------------------------------------------------------
    // Insertions
    // ------------------------------------------------------------------

    private static async Task<string?> InsertEventAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture,
        string key,
        long gross,
        long discount,
        long net,
        long tax,
        long total,
        string currency = "EUR",
        string financialStatus = "draft",
        string settlementStatus = "none",
        string periodStart = "2026-08-01 00:00:00",
        string periodEnd = "2026-09-01 00:00:00")
    {
        var id = Guid.NewGuid().ToString("D");
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO billing_v2_billing_events (
                id, customer_id, subscription_id,
                event_type, direction,
                financial_status, settlement_status, document_status,
                currency, period_start, period_end,
                payment_mode_snapshot, commitment_months_snapshot,
                discount_basis_points_snapshot,
                gross_amount_cents, discount_amount_cents, net_amount_cents,
                tax_amount_cents, total_amount_cents,
                pricing_engine_version,
                idempotency_key_canonical, idempotency_key_hash,
                created_at
            ) VALUES (
                @id, @customer_id, @subscription_id,
                'initial_charge', 'debit',
                @financial_status, @settlement_status, 'none',
                @currency, @period_start, @period_end,
                'monthly', 12, 1500,
                @gross, @discount, @net, @tax, @total,
                'pricing-engine-v1',
                @canonical, SHA2(@canonical, 256),
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@customer_id", fixture.CustomerId);
        command.Parameters.AddWithValue(
            "@subscription_id",
            fixture.SubscriptionId);
        command.Parameters.AddWithValue("@financial_status", financialStatus);
        command.Parameters.AddWithValue("@settlement_status", settlementStatus);
        command.Parameters.AddWithValue("@currency", currency);
        command.Parameters.AddWithValue("@period_start", periodStart);
        command.Parameters.AddWithValue("@period_end", periodEnd);
        command.Parameters.AddWithValue("@gross", gross);
        command.Parameters.AddWithValue("@discount", discount);
        command.Parameters.AddWithValue("@net", net);
        command.Parameters.AddWithValue("@tax", tax);
        command.Parameters.AddWithValue("@total", total);
        command.Parameters.AddWithValue(
            "@canonical",
            $"{fixture.Marker}|{key}");
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private static async Task<string?> InsertLineAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture,
        string billingEventId,
        int displayOrder,
        int quantity,
        long unit,
        long gross,
        long net,
        long total)
    {
        var id = Guid.NewGuid().ToString("D");
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO billing_v2_billing_event_lines (
                id, billing_event_id,
                service_id, service_price_id,
                service_code, description,
                quantity, unit_amount_cents, gross_amount_cents,
                discount_allocated_amount_cents, net_amount_cents,
                tax_amount_cents, total_amount_cents, currency,
                period_start, period_end, display_order, created_at
            ) VALUES (
                @id, @billing_event_id,
                @service_id, @service_price_id,
                'TEST-SERVICE', 'Ligne de test',
                @quantity, @unit, @gross,
                0, @net,
                0, @total, 'EUR',
                '2026-08-01 00:00:00', '2026-09-01 00:00:00',
                @display_order, UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@billing_event_id", billingEventId);
        command.Parameters.AddWithValue("@service_id", fixture.ServiceId);
        command.Parameters.AddWithValue(
            "@service_price_id",
            fixture.ServicePriceId);
        command.Parameters.AddWithValue("@quantity", quantity);
        command.Parameters.AddWithValue("@unit", unit);
        command.Parameters.AddWithValue("@gross", gross);
        command.Parameters.AddWithValue("@net", net);
        command.Parameters.AddWithValue("@total", total);
        command.Parameters.AddWithValue("@display_order", displayOrder);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private static async Task<string?> InsertAttemptAsync(
        MySqlConnection connection,
        BillingV2SchemaFixture fixture,
        string billingEventId,
        string requestKey,
        string status,
        long expectedAmount = 1000,
        long? settledAmount = null,
        string? settledCurrency = null)
    {
        var id = Guid.NewGuid().ToString("D");
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO billing_v2_payment_attempts (
                id, billing_event_id, provider, environment,
                provider_request_key,
                expected_amount_cents, expected_currency,
                settled_amount_cents, settled_currency,
                status, attempted_at, created_at, updated_at
            ) VALUES (
                @id, @billing_event_id, 'stripe', 'test',
                @request_key,
                @expected_amount, 'EUR',
                @settled_amount, @settled_currency,
                @status, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@billing_event_id", billingEventId);
        command.Parameters.AddWithValue(
            "@request_key",
            $"{fixture.Marker}-{requestKey}");
        command.Parameters.AddWithValue("@expected_amount", expectedAmount);
        command.Parameters.AddWithValue(
            "@settled_amount",
            settledAmount.HasValue ? settledAmount.Value : DBNull.Value);
        command.Parameters.AddWithValue(
            "@settled_currency",
            settledCurrency is null ? DBNull.Value : settledCurrency);
        command.Parameters.AddWithValue("@status", status);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static async Task EnsureRejectedAsync(
        Func<Task<string?>> action,
        string message)
    {
        try
        {
            await action();
        }
        catch (MySqlException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"La base aurait du rejeter cette ecriture : {message}");
    }

    private static async Task<long> ScalarLongAsync(
        MySqlConnection connection,
        string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt64(value);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record BillingV2SchemaFixture(
        string Marker,
        string CustomerId,
        string SubscriptionId,
        string ServiceId,
        string ServicePriceId)
    {
        public static async Task<BillingV2SchemaFixture> CreateAsync(
            MySqlConnection connection)
        {
            var marker = $"bv2-core-test-{Guid.NewGuid():N}";
            var customerId = Guid.NewGuid().ToString("D");
            var subscriptionId = Guid.NewGuid().ToString("D");
            var serviceId = Guid.NewGuid().ToString("D");
            var priceId = Guid.NewGuid().ToString("D");

            await ExecuteAsync(
                connection,
                """
                INSERT INTO customers (
                    id, external_reference, display_name, status,
                    created_at, updated_at
                ) VALUES (
                    @id, @reference, 'Client de test coeur financier', 'active',
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", customerId),
                ("@reference", marker));

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_services (
                    id, code, name, billing_type, default_scope_type,
                    discount_eligible, status, created_at, updated_at
                ) VALUES (
                    @id, @code, 'Service de test', 'recurring', 'subscription',
                    1, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", serviceId),
                ("@code", $"TEST-{marker[..16]}"));

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_service_prices (
                    id, service_id, price_code, price_version,
                    amount_cents, currency, billing_cadence,
                    valid_from, status, created_at
                ) VALUES (
                    @id, @service_id, @code, 1,
                    1000, 'EUR', 'monthly',
                    '2026-01-01 00:00:00', 'active', UTC_TIMESTAMP(6)
                );
                """,
                ("@id", priceId),
                ("@service_id", serviceId),
                ("@code", $"TEST-PRICE-{marker[..16]}"));

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_subscriptions (
                    id, customer_id, status, payment_mode, currency,
                    billing_model, created_at, updated_at
                ) VALUES (
                    @id, @customer_id, 'draft', 'monthly', 'EUR',
                    'v2', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", subscriptionId),
                ("@customer_id", customerId));

            return new BillingV2SchemaFixture(
                marker,
                customerId,
                subscriptionId,
                serviceId,
                priceId);
        }

        public async Task CleanupAsync(MySqlConnection connection)
        {
            await ExecuteAsync(
                connection,
                """
                DELETE attempt FROM billing_v2_payment_attempts attempt
                INNER JOIN billing_v2_billing_events event_row
                    ON event_row.id = attempt.billing_event_id
                WHERE event_row.subscription_id = @subscription_id;
                """,
                ("@subscription_id", SubscriptionId));
            await ExecuteAsync(
                connection,
                """
                DELETE line FROM billing_v2_billing_event_lines line
                INNER JOIN billing_v2_billing_events event_row
                    ON event_row.id = line.billing_event_id
                WHERE event_row.subscription_id = @subscription_id;
                """,
                ("@subscription_id", SubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_billing_events WHERE subscription_id = @id;",
                ("@id", SubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_subscriptions WHERE id = @id;",
                ("@id", SubscriptionId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_service_prices WHERE id = @id;",
                ("@id", ServicePriceId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_services WHERE id = @id;",
                ("@id", ServiceId));
            await ExecuteAsync(
                connection,
                "DELETE FROM customers WHERE id = @id;",
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
