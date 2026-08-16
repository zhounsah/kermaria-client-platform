using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Tests MariaDB reels des quatre correctifs issus de la validation du commit
/// 67aba8b :
///
/// 1. le catalogue public survit a un service sans palier (LEFT JOIN NULL) ;
/// 2. la cle d'idempotence est ancree sur la ligne de demande, et sa
///    reutilisation avec une autre selection est refusee en ferme ;
/// 3. un contrat comptant porte ses dates d'engagement et borne ses droits ;
/// 4. passe son terme, un contrat comptant n'ouvre plus rien.
///
/// Exige une MariaDB JETABLE via BILLING_V2_TEST_MARIADB_CONNECTION, portant
/// les migrations 001 a 063. Sans base, la suite echoue explicitement : elle
/// n'est jamais silencieusement verte.
///
/// Ne JAMAIS pointer cette variable vers une base de recette ou de production.
/// </summary>
public static class BillingV2NativeCheckoutSchemaTests
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
                + "MariaDB jetable portant les migrations 001 a 063. "
                + "Elle ne peut pas etre consideree comme passee sans base.");
        }

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        await VerifyMigration063ShapeAsync(connection);
        await VerifyCatalogHasServicesWithoutTierAsync(connection);
        await VerifyIdempotencyKeyIsUniquePerRailAsync(connection);
        await VerifySelectionFingerprintIsIndexedAsync(connection);
        await VerifyUpfrontContractBoundsItsRightsAsync(connection);
        await VerifyExpiredUpfrontContractGrantsNothingAsync(connection);
    }

    /// <summary>
    /// Forme attendue apres 063 : l'empreinte de selection est obligatoire et
    /// l'offre legacy devient facultative, sinon une configuration
    /// personnalisee ne peut pas exister en base.
    /// </summary>
    private static async Task VerifyMigration063ShapeAsync(
        MySqlConnection connection)
    {
        var selection = await ReadColumnAsync(
            connection,
            "billing_v2_authoritative_checkout_requests",
            "selection_fingerprint");
        Expect(
            selection is { IsNullable: false, DataType: "char" },
            "selection_fingerprint doit exister en CHAR NOT NULL apres 063.");

        var legacy = await ReadColumnAsync(
            connection,
            "billing_v2_authoritative_checkout_requests",
            "legacy_offer_id");
        Expect(
            legacy is { IsNullable: true },
            "legacy_offer_id doit devenir nullable apres 063 : une "
            + "configuration personnalisee n'a pas d'offre legacy.");

        var canonical = await ReadColumnAsync(
            connection,
            "billing_v2_authoritative_checkout_requests",
            "selection_canonical");
        Expect(
            canonical is not null,
            "selection_canonical doit exister apres 063.");
    }

    /// <summary>
    /// Regression du blocker 1. La requete publique fait un LEFT JOIN sur les
    /// paliers : les services qui n'en ont pas remontent avec
    /// <c>tier.public_selectable</c> a NULL. Le catalogue doit contenir de tels
    /// services, sinon ce test ne prouve rien — et la lecture doit rester
    /// possible sans exception de conversion.
    /// </summary>
    private static async Task VerifyCatalogHasServicesWithoutTierAsync(
        MySqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT service.code AS service_code,
                   tier.public_selectable AS tier_public_selectable
            FROM billing_v2_services service
            LEFT JOIN billing_v2_service_tiers tier
                ON tier.service_id = service.id
               AND tier.status = 'active'
            INNER JOIN billing_v2_service_prices price
                ON price.service_id = service.id
               AND price.tier_id <=> tier.id
               AND price.status = 'active'
               AND price.billing_cadence = 'monthly'
            WHERE service.status = 'active'
              AND service.public_selectable = 1
              AND tier.id IS NULL;
            """;
        var codes = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                Expect(
                    reader.IsDBNull(
                        reader.GetOrdinal("tier_public_selectable")),
                    "Un service sans palier doit bien remonter "
                    + "tier.public_selectable a NULL.");
                codes.Add(reader.GetString("service_code"));
            }
        }

        foreach (var expected in new[]
                 {
                     "RDS-ACCESS",
                     "USER-ADDITIONAL",
                     "SUPPORT-PLUS"
                 })
        {
            Expect(
                codes.Contains(expected, StringComparer.Ordinal),
                $"{expected} doit figurer parmi les services publics sans "
                + "palier : c'est le cas qui faisait tomber tout le catalogue.");
        }
    }

    /// <summary>
    /// Regression du blocker 2, cote base : la cle d'idempotence est unique par
    /// rail. C'est cette contrainte qui rend le refus ferme possible cote code
    /// au lieu d'un INSERT IGNORE silencieux.
    /// </summary>
    private static async Task VerifyIdempotencyKeyIsUniquePerRailAsync(
        MySqlConnection connection)
    {
        var columns = await ReadUniqueIndexColumnsAsync(
            connection,
            "billing_v2_authoritative_checkout_requests",
            "uq_billing_v2_authoritative_checkout_request");
        Expect(
            columns.SequenceEqual(
                new[]
                {
                    "customer_id",
                    "provider",
                    "environment",
                    "idempotency_key"
                },
                StringComparer.Ordinal),
            "L'unicite doit porter sur (customer_id, provider, environment, "
            + "idempotency_key) : c'est l'ancre relue avant toute creation "
            + "financiere. Trouve : "
            + string.Join(", ", columns));
    }

    private static async Task VerifySelectionFingerprintIsIndexedAsync(
        MySqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'billing_v2_authoritative_checkout_requests'
              AND COLUMN_NAME = 'selection_fingerprint';
            """;
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        Expect(
            count > 0,
            "selection_fingerprint doit etre indexe : la recherche "
            + "d'intention ouverte s'appuie dessus.");
    }

    /// <summary>
    /// Regression du blocker 3. Un contrat comptant doit porter ses bornes
    /// contractuelles et ne promettre aucun renouvellement, et ses lignes
    /// doivent expirer avec lui.
    /// </summary>
    private static async Task VerifyUpfrontContractBoundsItsRightsAsync(
        MySqlConnection connection)
    {
        var fixture = await BillingV2ContractFixture.CreateUpfrontAsync(
            connection,
            startsAt: DateTime.UtcNow.AddMonths(-1),
            commitmentMonths: 12);
        try
        {
            var row = await ReadContractAsync(connection, fixture.SubscriptionId);
            Expect(
                row.CommitmentStartedAt is not null
                && row.CommitmentEndsAt is not null,
                "Un contrat comptant doit porter commitment_started_at et "
                + "commitment_ends_at.");
            Expect(
                row.CommitmentEndsAt!.Value
                == row.CommitmentStartedAt!.Value.AddMonths(12),
                "La fin d'engagement doit valoir le debut plus la duree.");
            Expect(
                row.RenewsAt is null,
                "Un contrat comptant ne doit annoncer aucune date de "
                + "renouvellement : le renouvellement est manuel.");
            Expect(
                row.CurrentPeriodEndsAt == row.CommitmentEndsAt,
                "En comptant, la periode courante EST la periode payee.");

            var bounded = await ReadItemBoundAsync(
                connection,
                fixture.SubscriptionId);
            Expect(
                bounded is not null && bounded == row.CommitmentEndsAt,
                "Les lignes d'un contrat comptant doivent expirer au terme, "
                + "sinon les droits sont illimites apres la periode payee.");

            var granted = await CountGrantedItemsAsync(
                connection,
                fixture.CustomerId);
            Expect(
                granted > 0,
                "Pendant la periode payee, les droits doivent etre ouverts.");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    /// <summary>
    /// Regression du blocker 4. Passe son terme, un contrat comptant reste
    /// 'active' en base — aucun renouvellement automatique ne le bascule. Les
    /// projections de droits ne doivent malgre tout plus rien ouvrir.
    /// </summary>
    private static async Task VerifyExpiredUpfrontContractGrantsNothingAsync(
        MySqlConnection connection)
    {
        var fixture = await BillingV2ContractFixture.CreateUpfrontAsync(
            connection,
            startsAt: DateTime.UtcNow.AddMonths(-18),
            commitmentMonths: 12);
        try
        {
            var row = await ReadContractAsync(connection, fixture.SubscriptionId);
            Expect(
                row.Status == "active",
                "Le contrat expire reste 'active' en base : c'est justement "
                + "pourquoi la projection doit le borner elle-meme.");
            Expect(
                row.CommitmentEndsAt < DateTime.UtcNow,
                "Le contrat de ce test doit bien etre arrive a terme.");

            var granted = await CountGrantedItemsAsync(
                connection,
                fixture.CustomerId);
            Expect(
                granted == 0,
                "Passe le terme paye, aucune ligne ne doit plus ouvrir de "
                + "droit. Trouve : " + granted);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------
    // Lectures
    // ------------------------------------------------------------------

    /// <summary>
    /// Reproduit la clause de fenetre contractuelle appliquee par les
    /// projections de droits, telechargement et provisioning.
    /// </summary>
    private static async Task<int> CountGrantedItemsAsync(
        MySqlConnection connection,
        string customerId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM billing_v2_subscriptions subscription
            INNER JOIN billing_v2_subscription_items item
                ON item.subscription_id = subscription.id
            WHERE subscription.customer_id = @customer_id
              AND subscription.status = 'active'
              AND item.status = 'active'
              AND item.effective_from <= UTC_TIMESTAMP(6)
              AND (
                    item.effective_until IS NULL
                    OR item.effective_until > UTC_TIMESTAMP(6)
                  )
              AND (
                    subscription.renews_at IS NOT NULL
                    OR subscription.commitment_ends_at IS NULL
                    OR subscription.commitment_ends_at > UTC_TIMESTAMP(6)
                  );
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<ContractRow> ReadContractAsync(
        MySqlConnection connection,
        string subscriptionId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT status,
                   payment_mode,
                   commitment_started_at,
                   commitment_ends_at,
                   current_period_ends_at,
                   renews_at
            FROM billing_v2_subscriptions
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", subscriptionId);
        await using var reader = await command.ExecuteReaderAsync();
        Expect(await reader.ReadAsync(), "Le contrat de test doit exister.");
        return new ContractRow(
            reader.GetString("status"),
            reader.GetString("payment_mode"),
            ReadNullableDate(reader, "commitment_started_at"),
            ReadNullableDate(reader, "commitment_ends_at"),
            ReadNullableDate(reader, "current_period_ends_at"),
            ReadNullableDate(reader, "renews_at"));
    }

    private static async Task<DateTime?> ReadItemBoundAsync(
        MySqlConnection connection,
        string subscriptionId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT effective_until
            FROM billing_v2_subscription_items
            WHERE subscription_id = @id
            ORDER BY created_at
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", subscriptionId);
        var value = await command.ExecuteScalarAsync();
        return value is DateTime date ? date : null;
    }

    private static async Task<ColumnShape?> ReadColumnAsync(
        MySqlConnection connection,
        string tableName,
        string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT IS_NULLABLE, DATA_TYPE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @table_name
              AND COLUMN_NAME = @column_name;
            """;
        command.Parameters.AddWithValue("@table_name", tableName);
        command.Parameters.AddWithValue("@column_name", columnName);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ColumnShape(
            string.Equals(
                reader.GetString("IS_NULLABLE"),
                "YES",
                StringComparison.OrdinalIgnoreCase),
            reader.GetString("DATA_TYPE").ToLowerInvariant());
    }

    private static async Task<IReadOnlyList<string>>
        ReadUniqueIndexColumnsAsync(
            MySqlConnection connection,
            string tableName,
            string indexName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COLUMN_NAME
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @table_name
              AND INDEX_NAME = @index_name
              AND NON_UNIQUE = 0
            ORDER BY SEQ_IN_INDEX;
            """;
        command.Parameters.AddWithValue("@table_name", tableName);
        command.Parameters.AddWithValue("@index_name", indexName);
        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString("COLUMN_NAME"));
        }

        return columns;
    }

    private static DateTime? ReadNullableDate(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetDateTime(columnName);

    private static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record ColumnShape(bool IsNullable, string DataType);

    /// <summary>
    /// Contrat comptant complet et jetable : client, service, prix, abonnement
    /// date, et une ligne bornee au terme. Ecrit exactement ce que produit le
    /// checkout autoritatif, pour que le test porte sur la forme persistee et
    /// non sur une reconstruction approximative.
    /// </summary>
    private sealed record BillingV2ContractFixture(
        MySqlConnection Connection,
        string CustomerId,
        string SubscriptionId,
        string ServiceId,
        string ServicePriceId) : IAsyncDisposable
    {
        public static async Task<BillingV2ContractFixture> CreateUpfrontAsync(
            MySqlConnection connection,
            DateTime startsAt,
            int commitmentMonths)
        {
            var marker = $"bv2-upfront-test-{Guid.NewGuid():N}";
            var customerId = Guid.NewGuid().ToString("D");
            var subscriptionId = Guid.NewGuid().ToString("D");
            var serviceId = Guid.NewGuid().ToString("D");
            var priceId = Guid.NewGuid().ToString("D");
            var itemId = Guid.NewGuid().ToString("D");
            var endsAt = startsAt.AddMonths(commitmentMonths);

            await ExecuteAsync(
                connection,
                """
                INSERT INTO customers (
                    id, external_reference, display_name, status,
                    created_at, updated_at
                ) VALUES (
                    @id, @reference, 'Client de test comptant', 'active',
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
                ("@code", $"TESTUP-{marker[..14]}"));

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
                ("@code", $"TESTUP-PRICE-{marker[..14]}"));

            // Comptant : periode courante = periode d'engagement, aucune date
            // de renouvellement. C'est exactement ce que persiste desormais
            // BillingV2SubscriptionLifecyclePolicy.
            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_subscriptions (
                    id, customer_id, status, payment_mode, currency,
                    billing_model, commitment_started_at, commitment_ends_at,
                    current_period_started_at, current_period_ends_at,
                    renews_at, created_at, updated_at
                ) VALUES (
                    @id, @customer_id, 'active', 'upfront', 'EUR',
                    'v2', @starts_at, @ends_at,
                    @starts_at, @ends_at,
                    NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", subscriptionId),
                ("@customer_id", customerId),
                ("@starts_at", startsAt),
                ("@ends_at", endsAt));

            await ExecuteAsync(
                connection,
                """
                INSERT INTO billing_v2_subscription_items (
                    id, subscription_id, service_id, service_price_id,
                    scope_type, quantity, amount_cents_snapshot, currency,
                    discount_eligible_snapshot, source,
                    effective_from, effective_until, status,
                    created_at, updated_at
                ) VALUES (
                    @id, @subscription_id, @service_id, @service_price_id,
                    'subscription', 1, 1000, 'EUR',
                    1, 'preset',
                    @starts_at, @ends_at, 'active',
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", itemId),
                ("@subscription_id", subscriptionId),
                ("@service_id", serviceId),
                ("@service_price_id", priceId),
                ("@starts_at", startsAt),
                ("@ends_at", endsAt));

            return new BillingV2ContractFixture(
                connection,
                customerId,
                subscriptionId,
                serviceId,
                priceId);
        }

        public async ValueTask DisposeAsync()
        {
            await ExecuteAsync(
                Connection,
                "DELETE FROM billing_v2_subscription_items "
                + "WHERE subscription_id = @id;",
                ("@id", SubscriptionId));
            await ExecuteAsync(
                Connection,
                "DELETE FROM billing_v2_subscriptions WHERE id = @id;",
                ("@id", SubscriptionId));
            await ExecuteAsync(
                Connection,
                "DELETE FROM billing_v2_service_prices WHERE id = @id;",
                ("@id", ServicePriceId));
            await ExecuteAsync(
                Connection,
                "DELETE FROM billing_v2_services WHERE id = @id;",
                ("@id", ServiceId));
            await ExecuteAsync(
                Connection,
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

    private sealed record ContractRow(
        string Status,
        string PaymentMode,
        DateTime? CommitmentStartedAt,
        DateTime? CommitmentEndsAt,
        DateTime? CurrentPeriodEndsAt,
        DateTime? RenewsAt);
}
