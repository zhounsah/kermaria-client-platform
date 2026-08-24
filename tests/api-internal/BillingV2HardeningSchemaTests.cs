using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Scenarios de panne Phase 2.5, sur MariaDB jetable, avec un faux Stripe.
///
/// Aucun appel Stripe reel : <see cref="FakeStripeGateway"/> repond en memoire.
/// </summary>
public static class BillingV2HardeningSchemaTests
{
    public static async Task RunAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        var fixture = await HardeningFixture.CreateAsync(connection);
        try
        {
            await ScenarioG_RenewalCycleTwiceYieldsOneEventAsync(
                connection, fixture);
            await ScenarioD_TwoWorkersClaimOnceAsync(connection, fixture);
            await ScenarioA_WebhookNeverReceivedAsync(
                connection, fixture, connectionString);
            await ScenarioB_WebhookAndReconcilerAsync(
                connection, fixture, connectionString);
            await ScenarioC_AmountMismatchNeverActivatesAsync(
                connection, fixture, connectionString);
            await ScenarioEF_DocumentIssuanceIntentAsync(connection, fixture);
        }
        finally
        {
            await fixture.CleanupAsync(connection);
        }
    }

    // Scenario G : renouvellement du cycle 17 lance deux fois
    private static async Task ScenarioG_RenewalCycleTwiceYieldsOneEventAsync(
        MySqlConnection connection,
        HardeningFixture fixture)
    {
        var canonical = BillingV2RenewalPolicy.Canonical(
            fixture.SubscriptionId, 17);

        var first = await fixture.TryInsertRenewalAsync(
            connection, 17, canonical);
        var second = await fixture.TryInsertRenewalAsync(
            connection, 17, canonical);

        Ensure(first, "Le premier lancement du cycle 17 doit creer l'evenement.");
        Ensure(
            !second,
            "Scenario G : le second lancement du cycle 17 doit etre rejete.");
        Ensure(
            await ScalarLongAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_billing_events "
                + "WHERE subscription_id = @p AND cycle_sequence = 17",
                fixture.SubscriptionId) == 1,
            "Scenario G : un seul BillingEvent pour le cycle 17.");

        // Un autre cycle reste possible : la contrainte porte bien sur le rang.
        Ensure(
            await fixture.TryInsertRenewalAsync(
                connection, 18, BillingV2RenewalPolicy.Canonical(
                    fixture.SubscriptionId, 18)),
            "Le cycle 18 doit rester creable.");
    }

    // Scenario D : deux workers de reconciliation
    private static async Task ScenarioD_TwoWorkersClaimOnceAsync(
        MySqlConnection connection,
        HardeningFixture fixture)
    {
        var attemptId = await fixture.InsertAttemptAsync(
            connection,
            fixture.PrimaryBillingEventId,
            "cs_reconcile_d",
            BillingV2PaymentAttemptStatuses.InFlight);

        var now = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        var workerA = await BillingV2FinancialCoreStore
            .TryClaimReconciliationAsync(connection, attemptId, now, 120, default);
        var workerB = await BillingV2FinancialCoreStore
            .TryClaimReconciliationAsync(connection, attemptId, now, 120, default);

        Ensure(workerA, "Le premier worker doit obtenir le bail.");
        Ensure(
            !workerB,
            "Scenario D : le second worker ne doit pas obtenir le meme bail.");
        Ensure(
            await ScalarLongAsync(
                connection,
                "SELECT reconciliation_attempts FROM billing_v2_payment_attempts "
                + "WHERE id = @p",
                attemptId) == 1,
            "Scenario D : une seule prise en charge comptabilisee.");

        // Bail expire : la tentative redevient reclamable.
        Ensure(
            await BillingV2FinancialCoreStore.TryClaimReconciliationAsync(
                connection, attemptId, now.AddSeconds(200), 120, default),
            "Un bail expire doit pouvoir etre repris.");
    }

    // Scenario A : webhook jamais recu, le reconciliateur retrouve le paiement
    private static async Task ScenarioA_WebhookNeverReceivedAsync(
        MySqlConnection connection,
        HardeningFixture fixture,
        string connectionString)
    {
        var context = await fixture.PrepareSettlementCaseAsync(
            connection, "cs_scenario_a", 4046, "case-a");
        var gateway = FakeStripeGateway.Paid(
            "cs_scenario_a", context, amountCents: 4046, currency: "eur");
        var rail = BuildRail(connectionString, gateway);

        var result = await rail.VerifyAndSettleAsync(
            context.SubscriptionId, default);

        Ensure(
            result.Settled,
            "Scenario A : la relecture Stripe doit conclure a l'encaissement.");
        Ensure(
            gateway.GetCalls == 1 && gateway.CreateCalls == 0,
            "Scenario A : relecture seule, aucun nouveau checkout.");
        await EnsureSingleActivationAsync(connection, context, "Scenario A");
    }

    // Scenario B : webhook et reconciliateur simultanes
    private static async Task ScenarioB_WebhookAndReconcilerAsync(
        MySqlConnection connection,
        HardeningFixture fixture,
        string connectionString)
    {
        var context = await fixture.PrepareSettlementCaseAsync(
            connection, "cs_scenario_b", 4046, "case-b");
        var gateway = FakeStripeGateway.Paid(
            "cs_scenario_b", context, amountCents: 4046, currency: "eur");
        var rail = BuildRail(connectionString, gateway);

        // Les deux chemins appellent exactement le meme point d'entree.
        var viaWebhook = await rail.VerifyAndSettleAsync(
            context.SubscriptionId, default);
        var viaReconciler = await rail.VerifyAndSettleAsync(
            context.SubscriptionId, default);

        Ensure(
            viaWebhook.Settled && viaReconciler.Settled,
            "Scenario B : les deux chemins doivent converger vers regle.");
        Ensure(
            viaReconciler.ReasonCode
                == "BILLING_V2_STRIPE_SETTLEMENT_ALREADY_APPLIED",
            "Scenario B : le second passage doit etre un no-op explicite.");
        await EnsureSingleActivationAsync(connection, context, "Scenario B");
    }

    // Scenario C : montant Stripe different
    private static async Task ScenarioC_AmountMismatchNeverActivatesAsync(
        MySqlConnection connection,
        HardeningFixture fixture,
        string connectionString)
    {
        var context = await fixture.PrepareSettlementCaseAsync(
            connection, "cs_scenario_c", 4046, "case-c");
        var gateway = FakeStripeGateway.Paid(
            "cs_scenario_c", context, amountCents: 1990, currency: "eur");
        var rail = BuildRail(connectionString, gateway);

        var result = await rail.VerifyAndSettleAsync(
            context.SubscriptionId, default);

        Ensure(
            !result.Settled && result.ReconciliationRequired,
            "Scenario C : un ecart de montant ne doit jamais activer.");
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT status FROM billing_v2_subscriptions WHERE id = @p",
                context.SubscriptionId) != "active",
            "Scenario C : l'abonnement ne doit pas passer actif.");
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT settlement_status FROM billing_v2_billing_events "
                + "WHERE id = @p",
                context.BillingEventId)
                == BillingV2SettlementStatuses.AmountMismatch,
            "Scenario C : l'evenement doit porter amount_mismatch.");
    }

    // Scenarios E et F : intention d'emission documentaire
    private static async Task ScenarioEF_DocumentIssuanceIntentAsync(
        MySqlConnection connection,
        HardeningFixture fixture)
    {
        var documentId = await fixture.InsertCommercialDocumentAsync(connection);
        var reference = BillingV2DocumentIssuancePolicy
            .BuildExternalReference(documentId);

        var first = await fixture.TryInsertIssuanceIntentAsync(
            connection, documentId, reference);
        var second = await fixture.TryInsertIssuanceIntentAsync(
            connection, documentId, reference);

        Ensure(first, "La premiere intention d'emission doit etre creee.");
        Ensure(
            !second,
            "Scenario F : un rejeu ne cree pas une seconde intention.");
        Ensure(
            await ScalarLongAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_document_issuance_attempts "
                + "WHERE commercial_document_id = @p",
                documentId) == 1,
            "Scenario F : une seule emission logique par document.");

        // Scenario E : appel BPCE au sort indetermine.
        await ExecuteAsync(
            connection,
            "UPDATE billing_v2_document_issuance_attempts "
            + "SET status = 'in_flight', attempt_count = 1 "
            + "WHERE commercial_document_id = @p",
            ("@p", documentId));

        var indeterminate = BillingV2DocumentIssuancePolicy.ResolveIndeterminate(
            BillingV2DocumentIssuancePolicy
                .InvoiceLookupByExternalReferenceSupported,
            lookupFoundExistingInvoice: false);
        Ensure(
            !indeterminate.CanCallProvider && indeterminate.RequiresManualReview,
            "Scenario E : sans recherche BPCE possible, aucun second appel.");

        await ExecuteAsync(
            connection,
            "UPDATE billing_v2_document_issuance_attempts "
            + "SET status = 'reconciliation_required' "
            + "WHERE commercial_document_id = @p",
            ("@p", documentId));
        Ensure(
            !BillingV2DocumentIssuancePolicy.Evaluate(
                new BillingV2DocumentIssuanceAttempt(
                    "x", documentId, reference,
                    BillingV2DocumentIssuanceStatuses.ReconciliationRequired,
                    null, 1)).CanCallProvider,
            "Scenario E : l'etat reconciliation_required bloque tout retry.");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static async Task EnsureSingleActivationAsync(
        MySqlConnection connection,
        SettlementCase context,
        string label)
    {
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT status FROM billing_v2_subscriptions WHERE id = @p",
                context.SubscriptionId) == "active",
            $"{label} : l'abonnement doit etre actif.");
        Ensure(
            await ScalarLongAsync(
                connection,
                "SELECT version FROM billing_v2_subscriptions WHERE id = @p",
                context.SubscriptionId) == 2,
            $"{label} : une seule activation, donc une seule montee de version.");
        Ensure(
            await ScalarLongAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_billing_events "
                + "WHERE id = @p AND settlement_status = 'settled'",
                context.BillingEventId) == 1,
            $"{label} : un seul settlement logique.");
    }

    private static BillingV2StripeRailService BuildRail(
        string connectionString,
        IBillingV2StripeGateway gateway)
        => new(
            new SqlRuntimeConfiguration(
                PortalPersistenceMode.MariaDb,
                "mariadb",
                connectionString,
                "test",
                true),
            new StripeRuntimeConfiguration(StripeMode.Test, "sk_test_fake"),
            gateway,
            SystemBillingV2Clock.Instance,
            NullLogger<BillingV2StripeRailService>.Instance);

    private static async Task<long> ScalarLongAsync(
        MySqlConnection connection, string sql, string parameter)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@p", parameter);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt64(value);
    }

    private static async Task<string?> ScalarStringAsync(
        MySqlConnection connection, string sql, string parameter)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@p", parameter);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value);
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

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public sealed record SettlementCase(
        string SubscriptionId,
        string BillingEventId,
        string AttemptId);

    /// <summary>
    /// Faux Stripe : repond depuis la memoire, compte les appels, et ne cree
    /// jamais rien. Permet de verifier qu'une reconciliation RELIT et ne
    /// recree pas.
    /// </summary>
    private sealed class FakeStripeGateway : IBillingV2StripeGateway
    {
        private BillingV2StripeSessionSnapshot? _snapshot;

        public int GetCalls { get; private set; }

        public int CreateCalls { get; private set; }

        public bool CanExecute => true;

        public static FakeStripeGateway Paid(
            string sessionId,
            SettlementCase context,
            long amountCents,
            string currency)
            => new()
            {
                _snapshot = new BillingV2StripeSessionSnapshot(
                    sessionId,
                    "pi_fake",
                    "sub_fake",
                    BillingV2StripeModes.Subscription,
                    currency,
                    amountCents,
                    "paid",
                    "complete",
                    "client@example.invalid",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["billing_v2_billing_event_id"] = context.BillingEventId,
                        ["billing_v2_subscription_id"] = context.SubscriptionId,
                        ["billing_v2_payment_attempt_id"] = context.AttemptId
                    })
            };

        public Task<BillingV2StripeCreateResult> CreateCheckoutSessionAsync(
            BillingV2StripeCheckoutRequest request,
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            throw new InvalidOperationException(
                "La reconciliation ne doit jamais creer de checkout.");
        }

        public Task<BillingV2StripeSessionSnapshot?> GetCheckoutSessionAsync(
            string sessionId,
            CancellationToken cancellationToken)
        {
            GetCalls++;
            return Task.FromResult(_snapshot);
        }

        public Task<BillingV2StripeSessionSnapshot?> FindCheckoutSessionAsync(
            BillingV2StripeSessionLocator locator,
            CancellationToken cancellationToken)
            => Task.FromResult(_snapshot);

        // Phase 3 : sans abonnement ni invoice cote faux Stripe, la
        // verification de cycle de vie ne peut pas degrader un verdict de
        // session deja etabli. C'est exactement le comportement attendu.
        public Task<BillingV2StripeSubscriptionSnapshot?> GetSubscriptionAsync(
            string providerSubscriptionId,
            CancellationToken cancellationToken)
            => Task.FromResult<BillingV2StripeSubscriptionSnapshot?>(null);

        public Task<BillingV2StripeInvoiceSnapshot?> GetInvoiceAsync(
            string providerInvoiceId,
            CancellationToken cancellationToken)
            => Task.FromResult<BillingV2StripeInvoiceSnapshot?>(null);

        public Task<BillingV2StripeInvoiceSnapshot?>
            GetLatestInvoiceForSubscriptionAsync(
                string providerSubscriptionId,
                CancellationToken cancellationToken)
            => Task.FromResult<BillingV2StripeInvoiceSnapshot?>(null);
    }

    private sealed record HardeningFixture(
        string Marker,
        string CustomerId,
        string SubscriptionId,
        string ServiceId,
        string ServicePriceId,
        string PrimaryBillingEventId,
        string PortalUserId)
    {
        public static async Task<HardeningFixture> CreateAsync(
            MySqlConnection connection)
        {
            var marker = $"bv2-hard-{Guid.NewGuid():N}";
            var customerId = Guid.NewGuid().ToString("D");
            var subscriptionId = Guid.NewGuid().ToString("D");
            var serviceId = Guid.NewGuid().ToString("D");
            var priceId = Guid.NewGuid().ToString("D");

            await ExecuteAsync(
                connection,
                "INSERT INTO customers (id, external_reference, display_name, "
                + "status, created_at, updated_at) VALUES (@id, @ref, "
                + "'Client hardening', 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", customerId), ("@ref", marker));
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_services (id, code, name, billing_type, "
                + "default_scope_type, discount_eligible, status, created_at, "
                + "updated_at) VALUES (@id, @code, 'Service', 'recurring', "
                + "'subscription', 1, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", serviceId), ("@code", $"HARD-{marker[..12]}"));
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_service_prices (id, service_id, "
                + "price_code, price_version, amount_cents, currency, "
                + "billing_cadence, valid_from, status, created_at) VALUES "
                + "(@id, @svc, @code, 1, 4046, 'EUR', 'monthly', "
                + "'2026-01-01 00:00:00', 'active', UTC_TIMESTAMP(6))",
                ("@id", priceId), ("@svc", serviceId),
                ("@code", $"HARD-P-{marker[..12]}"));
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_subscriptions (id, customer_id, status, "
                + "payment_mode, currency, billing_model, created_at, updated_at) "
                + "VALUES (@id, @cust, 'pending_approval', 'monthly', 'EUR', "
                + "'v2', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", subscriptionId), ("@cust", customerId));

            var portalUserId = Guid.NewGuid().ToString("D");
            await ExecuteAsync(
                connection,
                "INSERT INTO portal_users (id, customer_id, "
                + "identity_provider_subject, email, display_name, status, "
                + "role, created_at, updated_at) VALUES (@id, @cust, @subject, "
                + "@email, 'Utilisateur hardening', 'active', 'client_user', "
                + "UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", portalUserId), ("@cust", customerId),
                ("@subject", $"hardening-{marker}"),
                ("@email", $"{marker}@example.invalid"));

            var fixture = new HardeningFixture(
                marker, customerId, subscriptionId, serviceId, priceId,
                Guid.NewGuid().ToString("D"), portalUserId);
            await fixture.InsertEventAsync(
                connection,
                fixture.PrimaryBillingEventId,
                fixture.SubscriptionId,
                4046,
                $"{marker}|primary",
                cycleSequence: 1,
                eventType: "initial_charge");
            return fixture;
        }

        public async Task<bool> TryInsertRenewalAsync(
            MySqlConnection connection,
            int cycleSequence,
            string canonical)
        {
            try
            {
                await InsertEventAsync(
                    connection,
                    Guid.NewGuid().ToString("D"),
                    SubscriptionId,
                    4046,
                    canonical,
                    cycleSequence,
                    "renewal_charge");
                return true;
            }
            catch (MySqlException)
            {
                return false;
            }
        }

        public async Task<SettlementCase> PrepareSettlementCaseAsync(
            MySqlConnection connection,
            string sessionId,
            long amountCents,
            string keySuffix)
        {
            var subscriptionId = Guid.NewGuid().ToString("D");
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_subscriptions (id, customer_id, status, "
                + "payment_mode, currency, billing_model, created_at, updated_at) "
                + "VALUES (@id, @cust, 'pending_approval', 'monthly', 'EUR', "
                + "'v2', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", subscriptionId), ("@cust", CustomerId));

            var eventId = Guid.NewGuid().ToString("D");
            await InsertEventAsync(
                connection, eventId, subscriptionId, amountCents,
                $"{Marker}|{keySuffix}", 1, "initial_charge");

            // La demande de checkout porte le lien vers l'evenement financier :
            // c'est par elle que le rail retrouve le montant attendu.
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_authoritative_checkout_requests "
                + "(id, customer_id, idempotency_key, request_fingerprint_hash, "
                + "selection_fingerprint, "
                + "provider, environment, subscription_id, "
                + "billing_event_id, status, created_at, updated_at) VALUES "
                + "(@id, @cust, @key, SHA2(@key, 256), "
                + "SHA2(CONCAT('billing_v2.selection|', @key), 256), "
                + "'stripe', 'test', "
                + "@sub, @evt, 'pending', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", Guid.NewGuid().ToString("D")), ("@cust", CustomerId),
                ("@key", $"{Marker}-{keySuffix}"),
                ("@sub", subscriptionId), ("@evt", eventId));

            var attemptId = await InsertAttemptAsync(
                connection, eventId, sessionId,
                BillingV2PaymentAttemptStatuses.InFlight, amountCents);
            return new SettlementCase(subscriptionId, eventId, attemptId);
        }

        public async Task<string> InsertAttemptAsync(
            MySqlConnection connection,
            string billingEventId,
            string sessionId,
            string status,
            long expectedAmountCents = 4046)
        {
            var id = Guid.NewGuid().ToString("D");
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_payment_attempts (id, billing_event_id, "
                + "provider, environment, provider_request_key, "
                + "expected_amount_cents, expected_currency, "
                + "provider_session_id, status, attempted_at, created_at, "
                + "updated_at) VALUES (@id, @evt, 'stripe', 'test', @key, "
                + "@amount, 'EUR', @session, @status, UTC_TIMESTAMP(6), "
                + "UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", id), ("@evt", billingEventId),
                ("@key", BillingV2FinancialCoreStore.BuildProviderRequestKey(
                    billingEventId)),
                ("@amount", expectedAmountCents),
                ("@session", sessionId), ("@status", status));
            return id;
        }

        public async Task<string> InsertCommercialDocumentAsync(
            MySqlConnection connection)
        {
            var id = Guid.NewGuid().ToString("D");
            await ExecuteAsync(
                connection,
                "INSERT INTO commercial_documents (id, customer_id, "
                + "document_type, status, internal_reference, title, "
                + "disclaimer, created_by_user_id, created_at, updated_at) "
                + "VALUES (@id, @cust, 'invoice', 'draft', @ref, "
                + "'Facture test hardening', 'Document de test.', @user, "
                + "UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", id), ("@cust", CustomerId),
                ("@ref", $"{Marker[..16]}-DOC"), ("@user", PortalUserId));
            return id;
        }

        public async Task<bool> TryInsertIssuanceIntentAsync(
            MySqlConnection connection,
            string documentId,
            string reference)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT IGNORE INTO billing_v2_document_issuance_attempts "
                + "(id, commercial_document_id, external_reference, status, "
                + "attempt_count, created_at, updated_at) VALUES (@id, @doc, "
                + "@ref, 'created', 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))";
            command.Parameters.AddWithValue(
                "@id", Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue("@doc", documentId);
            command.Parameters.AddWithValue("@ref", reference);
            return await command.ExecuteNonQueryAsync() == 1;
        }

        private async Task InsertEventAsync(
            MySqlConnection connection,
            string eventId,
            string subscriptionId,
            long amountCents,
            string canonical,
            int cycleSequence,
            string eventType)
        {
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_billing_events (id, customer_id, "
                + "subscription_id, event_type, direction, financial_status, "
                + "settlement_status, document_status, currency, period_start, "
                + "period_end, payment_mode_snapshot, commitment_months_snapshot, "
                + "cycle_sequence, discount_basis_points_snapshot, "
                + "gross_amount_cents, discount_amount_cents, net_amount_cents, "
                + "tax_amount_cents, total_amount_cents, pricing_engine_version, "
                + "idempotency_key_canonical, idempotency_key_hash, created_at, "
                + "finalized_at) VALUES (@id, @cust, @sub, @type, 'debit', "
                + "'finalized', 'none', 'none', 'EUR', '2026-08-01', "
                + "'2026-09-01', 'monthly', 12, @cycle, 1500, @amount, 0, "
                + "@amount, 0, @amount, 'pricing-engine-v1', @canonical, "
                + "SHA2(@canonical, 256), UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", eventId), ("@cust", CustomerId), ("@sub", subscriptionId),
                ("@type", eventType), ("@cycle", cycleSequence),
                ("@amount", amountCents), ("@canonical", canonical));
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_billing_event_lines (id, "
                + "billing_event_id, service_id, service_price_id, service_code, "
                + "description, billing_cadence, quantity, unit_amount_cents, "
                + "gross_amount_cents, discount_allocated_amount_cents, "
                + "net_amount_cents, tax_amount_cents, total_amount_cents, "
                + "currency, period_start, period_end, display_order, created_at) "
                + "VALUES (@id, @evt, @svc, @price, 'HARD', 'Ligne', 'monthly', "
                + "1, @amount, @amount, 0, @amount, 0, @amount, 'EUR', "
                + "'2026-08-01', '2026-09-01', 0, UTC_TIMESTAMP(6))",
                ("@id", Guid.NewGuid().ToString("D")), ("@evt", eventId),
                ("@svc", ServiceId), ("@price", ServicePriceId),
                ("@amount", amountCents));
        }

        public async Task CleanupAsync(MySqlConnection connection)
        {
            foreach (var sql in new[]
                     {
                         "DELETE FROM billing_v2_document_issuance_attempts "
                         + "WHERE commercial_document_id IN (SELECT id FROM "
                         + "commercial_documents WHERE customer_id = @id)",
                         "DELETE FROM commercial_documents WHERE customer_id = @id",
                         "DELETE a FROM billing_v2_payment_attempts a INNER JOIN "
                         + "billing_v2_billing_events e ON e.id = a.billing_event_id "
                         + "WHERE e.customer_id = @id",
                         "DELETE l FROM billing_v2_billing_event_lines l INNER JOIN "
                         + "billing_v2_billing_events e ON e.id = l.billing_event_id "
                         + "WHERE e.customer_id = @id",
                         "DELETE FROM billing_v2_authoritative_checkout_requests "
                         + "WHERE customer_id = @id",
                         "DELETE FROM billing_v2_billing_events WHERE customer_id = @id",
                         "DELETE FROM billing_v2_subscriptions WHERE customer_id = @id"
                     })
            {
                await ExecuteAsync(connection, sql, ("@id", CustomerId));
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
                connection, "DELETE FROM portal_users WHERE customer_id = @id",
                ("@id", CustomerId));
            await ExecuteAsync(
                connection, "DELETE FROM customers WHERE id = @id",
                ("@id", CustomerId));
        }
    }
}
