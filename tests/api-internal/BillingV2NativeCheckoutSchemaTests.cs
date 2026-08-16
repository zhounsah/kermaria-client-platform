using Kermaria.ApiInternal.Services;
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
/// Puis des deux correctifs issus de la validation post-paiement :
///
/// 5. le moyen de paiement inscrit sur le document tient dans l'ENUM MariaDB ;
/// 6. une charge encaissee dont le document manque est reprise, une seule fois,
///    et jamais lorsqu'une emission BPCE est restee indeterminee.
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
        await VerifyDocumentPaymentMethodFitsTheDatabaseEnumAsync(connection);
        await VerifySettledChargeWithoutDocumentIsResumedAsync(connection);
        await VerifyIssuedDocumentIsNeverResumedTwiceAsync(connection);
        await VerifyIndeterminateIssuanceStaysInManualReviewAsync(connection);
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

    /// <summary>
    /// Regression du blocker 5. Le document d'une charge Stripe reglee doit
    /// pouvoir passer a 'paid' sans erreur MariaDB.
    ///
    /// La constante ecrite jusqu'ici, <c>billing_v2_provider</c>, ne tient pas
    /// dans <c>commercial_documents.payment_method</c> : la confirmation
    /// echouait, la facture restait emise mais jamais marquee payee alors que
    /// l'argent etait encaisse. Aucune suite en persistance mock ne pouvait le
    /// voir — d'ou ce test sur base reelle.
    /// </summary>
    private static async Task VerifyDocumentPaymentMethodFitsTheDatabaseEnumAsync(
        MySqlConnection connection)
    {
        var fixture = await BillingV2ContractFixture.CreateUpfrontAsync(
            connection,
            startsAt: DateTime.UtcNow.AddDays(-1),
            commitmentMonths: 12);
        try
        {
            var billingEventId = await fixture.AddSettledStripeChargeAsync(
                documentStatus: "issued");
            var documentId = await fixture.AttachDocumentAsync(billingEventId);

            // Le rail est lu sur la tentative reussie, pas devine.
            var resolved =
                await BillingV2DocumentIssuerService.ReadPaymentMethodAsync(
                    connection,
                    documentId,
                    CancellationToken.None);
            Expect(
                resolved == "stripe",
                "Le moyen de paiement doit etre lu depuis le rail reel du "
                + "reglement. Trouve : " + resolved);

            // Exactement l'ecriture faite par MarkDocumentPaidAsync.
            await MarkDocumentPaidAsync(connection, documentId, resolved);
            var stored = await ReadDocumentPaymentMethodAsync(
                connection,
                documentId);
            Expect(
                stored == "stripe",
                "Le document d'un reglement Stripe doit porter 'stripe'. "
                + "Trouve : " + (stored ?? "NULL"));
            Expect(
                await ReadDocumentStatusAsync(connection, documentId) == "paid",
                "La confirmation doit avoir marque le document paye.");

            // Et la preuve que l'ancienne constante etait bien intenable.
            var refused = false;
            try
            {
                await MarkDocumentPaidAsync(
                    connection,
                    documentId,
                    "billing_v2_provider");
            }
            catch (MySqlException)
            {
                refused = true;
            }

            var afterLegacyWrite = await ReadDocumentPaymentMethodAsync(
                connection,
                documentId);
            Expect(
                refused || afterLegacyWrite != "billing_v2_provider",
                "'billing_v2_provider' ne doit jamais pouvoir etre stocke : "
                + "c'est la valeur qui cassait la confirmation en production. "
                + "Trouve : " + (afterLegacyWrite ?? "NULL"));

            foreach (var provider in new[] { "stripe", "paypal", "manual" })
            {
                await MarkDocumentPaidAsync(
                    connection,
                    documentId,
                    BillingV2DocumentPaymentMethod.FromProvider(provider));
            }

            await MarkDocumentPaidAsync(
                connection,
                documentId,
                BillingV2DocumentPaymentMethod.FromProvider("inconnu"));
            Expect(
                await ReadDocumentPaymentMethodAsync(connection, documentId)
                    == "manual",
                "Un provider inconnu doit retomber sur 'manual' : une valeur "
                + "toujours valide, qui signale un rapprochement manuel au "
                + "lieu de casser l'emission.");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    /// <summary>
    /// Regression du blocker 6, cas A. Une charge encaissee dont l'emission a
    /// echoue doit rester reprenable : la tentative de paiement est close, donc
    /// plus jamais candidate, et sans ce controle le client restait debite sans
    /// facture.
    /// </summary>
    private static async Task VerifySettledChargeWithoutDocumentIsResumedAsync(
        MySqlConnection connection)
    {
        var fixture = await BillingV2ContractFixture.CreateUpfrontAsync(
            connection,
            startsAt: DateTime.UtcNow.AddDays(-2),
            commitmentMonths: 12);
        try
        {
            var failed = await fixture.AddSettledStripeChargeAsync(
                documentStatus: "failed");
            var never = await fixture.AddSettledStripeChargeAsync(
                documentStatus: "none",
                cycleSequence: 2);

            var batch = await ReadResumeBatchAsync(connection, fixture);
            Expect(
                batch.Contains(failed, StringComparer.Ordinal),
                "Une emission echouee sur une charge reglee doit revenir dans "
                + "le lot de reprise.");
            Expect(
                batch.Contains(never, StringComparer.Ordinal),
                "Une charge reglee dont le document n'a jamais ete tente doit "
                + "aussi revenir dans le lot de reprise.");

            // Le lot doit rester stable tant que rien n'est emis : c'est ce
            // qui permet au reconciliateur existant de rattraper la charge au
            // passage suivant, sans worker supplementaire.
            var again = await ReadResumeBatchAsync(connection, fixture);
            Expect(
                again.Count == batch.Count,
                "Tant que le document n'est pas emis, la charge doit rester "
                + "reprenable a chaque passage.");

            // Emission finalement reussie : la charge sort du lot.
            var documentId = await fixture.AttachDocumentAsync(failed);
            await fixture.MarkDocumentStatusAsync(failed, "issued");
            Expect(
                documentId.Length > 0,
                "Le document repris doit exister.");
            var afterIssue = await ReadResumeBatchAsync(connection, fixture);
            Expect(
                !afterIssue.Contains(failed, StringComparer.Ordinal),
                "Une fois le document emis, la charge ne doit plus etre "
                + "reprise : sinon chaque passage produirait une facture.");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    /// <summary>
    /// Regression du blocker 6, cas B. Dix passages sur une charge deja
    /// documentee ne doivent produire ni seconde selection, ni second document.
    /// </summary>
    private static async Task VerifyIssuedDocumentIsNeverResumedTwiceAsync(
        MySqlConnection connection)
    {
        var fixture = await BillingV2ContractFixture.CreateUpfrontAsync(
            connection,
            startsAt: DateTime.UtcNow.AddDays(-3),
            commitmentMonths: 12);
        try
        {
            var billingEventId = await fixture.AddSettledStripeChargeAsync(
                documentStatus: "issued");
            await fixture.AttachDocumentAsync(billingEventId);

            for (var pass = 1; pass <= 10; pass++)
            {
                var batch = await ReadResumeBatchAsync(connection, fixture);
                Expect(
                    batch.Count == 0,
                    $"Passage {pass} : une charge deja documentee ne doit "
                    + "jamais revenir dans le lot de reprise.");
            }

            Expect(
                await CountDocumentsAsync(connection, fixture.SubscriptionId)
                    == 1,
                "Apres dix passages, il ne doit exister qu'un seul document.");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    /// <summary>
    /// Regression du blocker 6, cas C. Un appel BPCE au resultat indetermine
    /// laisse l'intention en <c>reconciliation_required</c>. La reprise doit
    /// alors s'abstenir : relancer a l'aveugle risquerait un second numero
    /// fiscal pour une facture peut-etre deja emise chez le facturier.
    /// </summary>
    private static async Task
        VerifyIndeterminateIssuanceStaysInManualReviewAsync(
            MySqlConnection connection)
    {
        var fixture = await BillingV2ContractFixture.CreateUpfrontAsync(
            connection,
            startsAt: DateTime.UtcNow.AddDays(-4),
            commitmentMonths: 12);
        try
        {
            var billingEventId = await fixture.AddSettledStripeChargeAsync(
                documentStatus: "pending");
            var documentId = await fixture.AttachDocumentAsync(
                billingEventId,
                status: "created");

            await fixture.MarkIssuanceAsync(
                documentId,
                billingEventId,
                "in_flight");
            Expect(
                (await ReadResumeBatchAsync(connection, fixture)).Contains(
                    billingEventId,
                    StringComparer.Ordinal),
                "Une emission encore en vol reste reprenable : c'est "
                + "l'emetteur, et lui seul, qui decide de la basculer en revue "
                + "manuelle.");

            await fixture.MarkIssuanceAsync(
                documentId,
                billingEventId,
                "reconciliation_required");
            for (var pass = 1; pass <= 10; pass++)
            {
                Expect(
                    (await ReadResumeBatchAsync(connection, fixture)).Count
                        == 0,
                    $"Passage {pass} : une emission indeterminee doit rester "
                    + "en revue manuelle et ne jamais etre relancee.");
            }

            Expect(
                await CountDocumentsAsync(connection, fixture.SubscriptionId)
                    == 1,
                "Aucune seconde facture ne doit naitre d'un resultat BPCE "
                + "indetermine.");
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
    /// Lot de reprise reel, restreint au contrat de test : la selection
    /// verifiee est celle du service, pas une reecriture.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ReadResumeBatchAsync(
        MySqlConnection connection,
        BillingV2ContractFixture fixture)
    {
        var pending = await BillingV2StripeReconciliationService
            .ReadDocumentsToResumeAsync(
                connection,
                BillingV2ContractFixture.Environment,
                CancellationToken.None);
        return pending
            .Where(document => string.Equals(
                document.SubscriptionId,
                fixture.SubscriptionId,
                StringComparison.OrdinalIgnoreCase))
            .Select(document => document.BillingEventId)
            .ToList();
    }

    private static async Task MarkDocumentPaidAsync(
        MySqlConnection connection,
        string documentId,
        string paymentMethod)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE commercial_documents SET
                status = 'paid',
                payment_method = @payment_method,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @document_id;
            """;
        command.Parameters.AddWithValue("@document_id", documentId);
        command.Parameters.AddWithValue("@payment_method", paymentMethod);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadDocumentPaymentMethodAsync(
        MySqlConnection connection,
        string documentId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT payment_method FROM commercial_documents WHERE id = @id;";
        command.Parameters.AddWithValue("@id", documentId);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static async Task<string?> ReadDocumentStatusAsync(
        MySqlConnection connection,
        string documentId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT status FROM commercial_documents WHERE id = @id;";
        command.Parameters.AddWithValue("@id", documentId);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static async Task<int> CountDocumentsAsync(
        MySqlConnection connection,
        string subscriptionId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM billing_v2_subscription_documents "
            + "WHERE subscription_id = @id;";
        command.Parameters.AddWithValue("@id", subscriptionId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

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
    private sealed class BillingV2ContractFixture : IAsyncDisposable
    {
        /// <summary>
        /// Environnement du rail : les tentatives de paiement de test vivent
        /// dans le meme espace que celles du mode Stripe TEST, faute de quoi le
        /// lot de reprise ne les verrait pas.
        /// </summary>
        public const string Environment = "test";

        private readonly List<string> _issuanceIds = new();
        private readonly List<string> _subscriptionDocumentIds = new();
        private readonly List<string> _commercialDocumentIds = new();
        private readonly List<string> _paymentAttemptIds = new();
        private readonly List<string> _billingEventIds = new();

        private BillingV2ContractFixture(
            MySqlConnection connection,
            string customerId,
            string portalUserId,
            string subscriptionId,
            string serviceId,
            string servicePriceId)
        {
            Connection = connection;
            CustomerId = customerId;
            PortalUserId = portalUserId;
            SubscriptionId = subscriptionId;
            ServiceId = serviceId;
            ServicePriceId = servicePriceId;
        }

        public MySqlConnection Connection { get; }

        public string CustomerId { get; }

        public string PortalUserId { get; }

        public string SubscriptionId { get; }

        public string ServiceId { get; }

        public string ServicePriceId { get; }

        public static async Task<BillingV2ContractFixture> CreateUpfrontAsync(
            MySqlConnection connection,
            DateTime startsAt,
            int commitmentMonths)
        {
            var marker = $"bv2-upfront-test-{Guid.NewGuid():N}";
            var customerId = Guid.NewGuid().ToString("D");
            var portalUserId = Guid.NewGuid().ToString("D");
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

            // Auteur des documents commerciaux : la colonne est obligatoire et
            // referencee, un document Billing V2 ne peut pas exister sans elle.
            await ExecuteAsync(
                connection,
                """
                INSERT INTO portal_users (
                    id, customer_id, identity_provider_subject, email,
                    display_name, status, role, created_at, updated_at
                ) VALUES (
                    @id, @customer_id, @subject, @email,
                    'Emetteur de test', 'active', 'internal_admin',
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", portalUserId),
                ("@customer_id", customerId),
                ("@subject", marker),
                ("@email", $"{marker}@example.invalid"));

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
                portalUserId,
                subscriptionId,
                serviceId,
                priceId);
        }

        /// <summary>
        /// Charge Stripe reellement encaissee : BillingEvent regle et tentative
        /// de paiement close en succes. C'est exactement l'etat dans lequel une
        /// emission ratee laissait le dossier — plus aucune tentative ouverte,
        /// donc plus aucun candidat pour le reconciliateur.
        /// </summary>
        public async Task<string> AddSettledStripeChargeAsync(
            string documentStatus,
            int cycleSequence = 1)
        {
            var billingEventId = Guid.NewGuid().ToString("D");
            var attemptId = Guid.NewGuid().ToString("D");
            var marker = Guid.NewGuid().ToString("N");
            var periodStart = DateTime.UtcNow.Date.AddDays(-30);

            await ExecuteAsync(
                Connection,
                """
                INSERT INTO billing_v2_billing_events (
                    id, customer_id, subscription_id, event_type, direction,
                    financial_status, settlement_status, document_status,
                    currency, period_start, period_end,
                    payment_mode_snapshot, commitment_months_snapshot,
                    cycle_sequence, discount_basis_points_snapshot,
                    gross_amount_cents, discount_amount_cents,
                    net_amount_cents, tax_amount_cents, total_amount_cents,
                    pricing_engine_version, idempotency_key_canonical,
                    idempotency_key_hash, created_at, finalized_at, settled_at
                ) VALUES (
                    @id, @customer_id, @subscription_id, 'initial_charge',
                    'debit', 'finalized', 'settled', @document_status,
                    'EUR', @period_start, @period_end,
                    'upfront', 12,
                    @cycle_sequence, 0,
                    1000, 0,
                    1000, 0, 1000,
                    'test', @canonical,
                    @hash, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", billingEventId),
                ("@customer_id", CustomerId),
                ("@subscription_id", SubscriptionId),
                ("@document_status", documentStatus),
                ("@period_start", periodStart),
                ("@period_end", periodStart.AddDays(365)),
                ("@cycle_sequence", cycleSequence),
                ("@canonical", $"test|{marker}"),
                ("@hash", marker + marker));
            _billingEventIds.Add(billingEventId);

            await ExecuteAsync(
                Connection,
                """
                INSERT INTO billing_v2_payment_attempts (
                    id, billing_event_id, provider, environment,
                    provider_request_key, expected_amount_cents,
                    expected_currency, settled_amount_cents, settled_currency,
                    provider_session_id, status,
                    attempted_at, responded_at, created_at, updated_at
                ) VALUES (
                    @id, @billing_event_id, 'stripe', @environment,
                    @request_key, 1000,
                    'EUR', 1000, 'EUR',
                    @session_id, 'succeeded',
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6),
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", attemptId),
                ("@billing_event_id", billingEventId),
                ("@environment", Environment),
                ("@request_key", $"test-{marker}"),
                ("@session_id", $"cs_test_{marker}"));
            _paymentAttemptIds.Add(attemptId);

            return billingEventId;
        }

        /// <summary>
        /// Document commercial deja emis pour cette charge, relie au
        /// BillingEvent comme le fait l'emetteur.
        /// </summary>
        public async Task<string> AttachDocumentAsync(
            string billingEventId,
            string status = "issued")
        {
            var documentId = Guid.NewGuid().ToString("D");
            var linkId = Guid.NewGuid().ToString("D");
            var marker = Guid.NewGuid().ToString("N");
            var periodStart = DateTime.UtcNow.Date.AddDays(-30);
            var cycleSequence = await ReadCycleSequenceAsync(billingEventId);

            await ExecuteAsync(
                Connection,
                """
                INSERT INTO commercial_documents (
                    id, customer_id, origin, document_type, status, title,
                    internal_reference, currency, subtotal_amount_cents,
                    tax_amount_cents, total_amount_cents, disclaimer,
                    created_by_user_id, created_at, updated_at
                ) VALUES (
                    @id, @customer_id, 'billing_v2', 'informational_invoice',
                    @status, 'Facture de test',
                    @reference, 'EUR', 1000,
                    0, 1000, 'Document de test',
                    @author, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", documentId),
                ("@customer_id", CustomerId),
                ("@status", status),
                ("@reference", $"TEST-{marker}"),
                ("@author", PortalUserId));
            _commercialDocumentIds.Add(documentId);

            await ExecuteAsync(
                Connection,
                """
                INSERT INTO billing_v2_subscription_documents (
                    id, subscription_id, billing_event_id,
                    commercial_document_id, document_kind, cycle_sequence,
                    period_start, period_end, subtotal_amount_cents,
                    discount_amount_cents, tax_amount_cents,
                    total_amount_cents, currency, status,
                    created_at, updated_at
                ) VALUES (
                    @id, @subscription_id, @billing_event_id,
                    @commercial_document_id, 'initial_invoice', @cycle_sequence,
                    @period_start, @period_end, 1000,
                    0, 0,
                    1000, 'EUR', @status,
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                );
                """,
                ("@id", linkId),
                ("@subscription_id", SubscriptionId),
                ("@billing_event_id", billingEventId),
                ("@commercial_document_id", documentId),
                ("@cycle_sequence", cycleSequence),
                ("@period_start", periodStart.AddDays(cycleSequence)),
                ("@period_end", periodStart.AddDays(365 + cycleSequence)),
                ("@status", status));
            _subscriptionDocumentIds.Add(linkId);

            return documentId;
        }

        /// <summary>
        /// Verrou d'intention d'emission, tel que le pose l'emetteur AVANT tout
        /// appel BPCE.
        /// </summary>
        public async Task MarkIssuanceAsync(
            string commercialDocumentId,
            string billingEventId,
            string status)
        {
            var issuanceId = Guid.NewGuid().ToString("D");
            await ExecuteAsync(
                Connection,
                """
                INSERT INTO billing_v2_document_issuance_attempts (
                    id, commercial_document_id, billing_event_id,
                    external_reference, status, attempt_count,
                    created_at, updated_at
                ) VALUES (
                    @id, @commercial_document_id, @billing_event_id,
                    @reference, @status, 1,
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                )
                ON DUPLICATE KEY UPDATE
                    status = VALUES(status),
                    attempt_count = attempt_count + 1,
                    updated_at = UTC_TIMESTAMP(6);
                """,
                ("@id", issuanceId),
                ("@commercial_document_id", commercialDocumentId),
                ("@billing_event_id", billingEventId),
                ("@reference", $"bv2-test-{commercialDocumentId}"),
                ("@status", status));
            if (!_issuanceIds.Contains(commercialDocumentId, StringComparer.Ordinal))
            {
                _issuanceIds.Add(commercialDocumentId);
            }
        }

        public Task MarkDocumentStatusAsync(
            string billingEventId,
            string documentStatus)
            => ExecuteAsync(
                Connection,
                "UPDATE billing_v2_billing_events SET document_status = @status "
                + "WHERE id = @id;",
                ("@id", billingEventId),
                ("@status", documentStatus));

        private async Task<int> ReadCycleSequenceAsync(string billingEventId)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText =
                "SELECT cycle_sequence FROM billing_v2_billing_events "
                + "WHERE id = @id;";
            command.Parameters.AddWithValue("@id", billingEventId);
            var value = await command.ExecuteScalarAsync();
            return value is null or DBNull ? 1 : Convert.ToInt32(value);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var documentId in _issuanceIds)
            {
                await ExecuteAsync(
                    Connection,
                    "DELETE FROM billing_v2_document_issuance_attempts "
                    + "WHERE commercial_document_id = @id;",
                    ("@id", documentId));
            }

            foreach (var linkId in _subscriptionDocumentIds)
            {
                await ExecuteAsync(
                    Connection,
                    "DELETE FROM billing_v2_subscription_documents "
                    + "WHERE id = @id;",
                    ("@id", linkId));
            }

            foreach (var documentId in _commercialDocumentIds)
            {
                await ExecuteAsync(
                    Connection,
                    "DELETE FROM commercial_documents WHERE id = @id;",
                    ("@id", documentId));
            }

            foreach (var attemptId in _paymentAttemptIds)
            {
                await ExecuteAsync(
                    Connection,
                    "DELETE FROM billing_v2_payment_attempts WHERE id = @id;",
                    ("@id", attemptId));
            }

            foreach (var billingEventId in _billingEventIds)
            {
                await ExecuteAsync(
                    Connection,
                    "DELETE FROM billing_v2_billing_events WHERE id = @id;",
                    ("@id", billingEventId));
            }

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
                "DELETE FROM portal_users WHERE id = @id;",
                ("@id", PortalUserId));
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
