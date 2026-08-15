using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Scenarios de renouvellement Phase 3, sur MariaDB jetable.
///
/// Aucun appel Stripe reel : <see cref="FakeRenewalStripeGateway"/> repond en
/// memoire et LEVE si on tente de creer un checkout - un renouvellement se
/// relit, il ne se rachete pas.
/// </summary>
public static class BillingV2RenewalSchemaTests
{
    public static async Task RunAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        var fixture = await RenewalFixture.CreateAsync(connection);
        try
        {
            await ScenarioA_CycleTwoPaidAsync(
                connection, fixture, connectionString);
            await ScenarioB_InvoicePaidThreeTimesAsync(
                connection, fixture, connectionString);
            await ScenarioE_TwoWorkersOneCycleAsync(
                connection, fixture, connectionString);
            await ScenarioF_CatalogPriceChangedAsync(
                connection, fixture, connectionString);
            await ScenarioH_AmountMismatchAsync(
                connection, fixture, connectionString);
            await ScenarioI_FailedRenewalKeepsAccessAsync(
                connection, fixture, connectionString);
            await ScenarioAB_CycleDocumentIsUniqueAsync(
                connection, fixture, connectionString);
            await ScenarioJ_BpceTimeoutKeepsPaymentKnownAsync(
                connection, fixture);
            await VerifyRenewalSchemaAsync(connection);
        }
        finally
        {
            await fixture.CleanupAsync(connection);
        }
    }

    // -----------------------------------------------------------------
    // Scenario A : cycle 2 paye normalement
    // -----------------------------------------------------------------

    private static async Task ScenarioA_CycleTwoPaidAsync(
        MySqlConnection connection,
        RenewalFixture fixture,
        string connectionString)
    {
        var subscriptionId = await fixture.CreateActiveSubscriptionAsync(
            connection, "case-a", unitAmountCents: 4500);
        var gateway = FakeRenewalStripeGateway.Paid(
            amountCents: 4500,
            periodStart: fixture.CycleStart(2),
            fixture.ProviderSubscriptionId("case-a"));
        var renewals = BuildRenewals(connectionString, gateway);

        var result = await renewals.HandleProviderSignalAsync(
            subscriptionId, default);

        Ensure(
            result.Settled,
            "Scenario A : un cycle 2 paye doit etre regle.");
        Ensure(
            result.CycleSequence == 2,
            "Scenario A : le signal doit designer le cycle 2.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_billing_events "
                + "WHERE subscription_id = @p AND event_type = 'renewal_charge'",
                subscriptionId) == 1,
            "Scenario A : un seul BillingEvent de renouvellement.");
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT settlement_status FROM billing_v2_billing_events "
                + "WHERE subscription_id = @p AND event_type = 'renewal_charge'",
                subscriptionId) == BillingV2SettlementStatuses.Settled,
            "Scenario A : le cycle doit etre marque encaisse.");
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT status FROM billing_v2_subscriptions WHERE id = @p",
                subscriptionId) == "active",
            "Scenario A : l'abonnement doit rester actif.");
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT payment_state FROM billing_v2_subscriptions "
                + "WHERE id = @p",
                subscriptionId)
            == BillingV2SubscriptionPaymentStates.Current,
            "Scenario A : aucun incident de paiement ne doit etre signale.");
        Ensure(
            gateway.CreateCalls == 0,
            "Scenario A : un renouvellement ne cree jamais de checkout.");
    }

    // -----------------------------------------------------------------
    // Scenario B : le meme `invoice.paid` recu trois fois
    // -----------------------------------------------------------------

    private static async Task ScenarioB_InvoicePaidThreeTimesAsync(
        MySqlConnection connection,
        RenewalFixture fixture,
        string connectionString)
    {
        var subscriptionId = await fixture.CreateActiveSubscriptionAsync(
            connection, "case-b", unitAmountCents: 4500);
        var gateway = FakeRenewalStripeGateway.Paid(
            amountCents: 4500,
            periodStart: fixture.CycleStart(2),
            fixture.ProviderSubscriptionId("case-b"));
        var renewals = BuildRenewals(connectionString, gateway);

        var first = await renewals.HandleProviderSignalAsync(
            subscriptionId, default);
        var second = await renewals.HandleProviderSignalAsync(
            subscriptionId, default);
        var third = await renewals.HandleProviderSignalAsync(
            subscriptionId, default);

        Ensure(
            first.Settled && second.Settled && third.Settled,
            "Scenario B : les trois rejeux doivent conclure au meme etat.");
        Ensure(
            second.ReasonCode == "BILLING_V2_RENEWAL_SETTLEMENT_ALREADY_APPLIED"
            && third.ReasonCode
                == "BILLING_V2_RENEWAL_SETTLEMENT_ALREADY_APPLIED",
            "Scenario B : les rejeux doivent etre des no-op explicites.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_billing_events "
                + "WHERE subscription_id = @p AND event_type = 'renewal_charge'",
                subscriptionId) == 1,
            "Scenario B : aucun BillingEvent en double.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_payment_attempts attempt "
                + "INNER JOIN billing_v2_billing_events event_row "
                + "ON event_row.id = attempt.billing_event_id "
                + "WHERE event_row.subscription_id = @p "
                + "AND event_row.event_type = 'renewal_charge'",
                subscriptionId) == 1,
            "Scenario B : une seule tentative de paiement logique.");
    }

    // -----------------------------------------------------------------
    // Scenarios C + D + E : concurrence sur un meme cycle
    // -----------------------------------------------------------------

    private static async Task ScenarioE_TwoWorkersOneCycleAsync(
        MySqlConnection connection,
        RenewalFixture fixture,
        string connectionString)
    {
        var subscriptionId = await fixture.CreateActiveSubscriptionAsync(
            connection, "case-e", unitAmountCents: 4500);
        var gateway = FakeRenewalStripeGateway.Paid(
            amountCents: 4500,
            periodStart: fixture.CycleStart(17),
            fixture.ProviderSubscriptionId("case-e"));

        // Deux "workers" : deux services distincts, deux connexions distinctes,
        // qui visent le meme cycle 17 en meme temps.
        var workerA = BuildRenewals(connectionString, gateway);
        var workerB = BuildRenewals(connectionString, gateway);

        var results = await Task.WhenAll(
            workerA.EnsureRenewalChargeAsync(subscriptionId, 17, default),
            workerB.EnsureRenewalChargeAsync(subscriptionId, 17, default));

        Ensure(
            results.Count(result => result.Created) == 1,
            "Scenario E : un seul worker doit creer le cycle 17.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_billing_events "
                + "WHERE subscription_id = @p AND cycle_sequence = 17",
                subscriptionId) == 1,
            "Scenario E : un seul BillingEvent pour le cycle 17.");

        // Scenario C + D : le webhook perdu, puis rattrape, et les deux
        // chemins concurrents ne produisent qu'une transition.
        var viaReconciler = await workerA.HandleProviderSignalAsync(
            subscriptionId, default);
        var viaWebhook = await workerB.HandleProviderSignalAsync(
            subscriptionId, default);

        Ensure(
            viaReconciler.Settled,
            "Scenario C : le rattrapage doit retrouver le paiement.");
        Ensure(
            viaWebhook.Settled
            && viaWebhook.ReasonCode
                == "BILLING_V2_RENEWAL_SETTLEMENT_ALREADY_APPLIED",
            "Scenario D : la seconde voie doit etre un no-op.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_billing_events "
                + "WHERE subscription_id = @p AND cycle_sequence = 17 "
                + "AND settlement_status = 'settled'",
                subscriptionId) == 1,
            "Scenario D : un seul settlement effectif.");
    }

    // -----------------------------------------------------------------
    // Scenario F : le prix catalogue change apres la souscription
    // -----------------------------------------------------------------

    private static async Task ScenarioF_CatalogPriceChangedAsync(
        MySqlConnection connection,
        RenewalFixture fixture,
        string connectionString)
    {
        var subscriptionId = await fixture.CreateActiveSubscriptionAsync(
            connection, "case-f", unitAmountCents: 4500);

        // Le catalogue double. Le contrat, lui, ne bouge pas.
        await ExecuteAsync(
            connection,
            "UPDATE billing_v2_service_prices SET amount_cents = 9900 "
            + "WHERE id = @p",
            ("@p", fixture.ServicePriceId));

        var renewals = BuildRenewals(
            connectionString,
            FakeRenewalStripeGateway.Paid(
                amountCents: 4500,
                periodStart: fixture.CycleStart(2),
                fixture.ProviderSubscriptionId("case-f")));
        var ensured = await renewals.EnsureRenewalChargeAsync(
            subscriptionId, 2, default);

        Ensure(
            ensured.Created && ensured.ExpectedAmountCents == 4500,
            "Scenario F : le renouvellement doit garder le prix contractuel.");

        // On remet le catalogue en etat pour ne pas polluer les cas suivants.
        await ExecuteAsync(
            connection,
            "UPDATE billing_v2_service_prices SET amount_cents = 4500 "
            + "WHERE id = @p",
            ("@p", fixture.ServicePriceId));
    }

    // -----------------------------------------------------------------
    // Scenario H : montant Stripe different de l'attendu
    // -----------------------------------------------------------------

    private static async Task ScenarioH_AmountMismatchAsync(
        MySqlConnection connection,
        RenewalFixture fixture,
        string connectionString)
    {
        var subscriptionId = await fixture.CreateActiveSubscriptionAsync(
            connection, "case-h", unitAmountCents: 4500);
        var renewals = BuildRenewals(
            connectionString,
            FakeRenewalStripeGateway.Paid(
                amountCents: 4400,
                periodStart: fixture.CycleStart(2),
                fixture.ProviderSubscriptionId("case-h")));

        var result = await renewals.HandleProviderSignalAsync(
            subscriptionId, default);

        Ensure(
            !result.Settled,
            "Scenario H : un montant different ne doit jamais etre encaisse.");
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT settlement_status FROM billing_v2_billing_events "
                + "WHERE subscription_id = @p AND event_type = 'renewal_charge'",
                subscriptionId) == BillingV2SettlementStatuses.AmountMismatch,
            "Scenario H : l'ecart doit etre trace en amount_mismatch.");
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT payment_state FROM billing_v2_subscriptions "
                + "WHERE id = @p",
                subscriptionId)
            == BillingV2SubscriptionPaymentStates.ManualReview,
            "Scenario H : l'abonnement doit partir en revue humaine.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_subscription_documents "
                + "WHERE subscription_id = @p AND status = 'paid'",
                subscriptionId) == 0,
            "Scenario H : aucune facture ne doit etre marquee payee.");
        // L'acces reste en place : c'est la politique de grace V2.0.
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT status FROM billing_v2_subscriptions WHERE id = @p",
                subscriptionId) == "active",
            "Scenario H : l'acces ne doit pas etre coupe pour un ecart.");
    }

    // -----------------------------------------------------------------
    // Scenario I : renouvellement echoue / past_due
    // -----------------------------------------------------------------

    private static async Task ScenarioI_FailedRenewalKeepsAccessAsync(
        MySqlConnection connection,
        RenewalFixture fixture,
        string connectionString)
    {
        var subscriptionId = await fixture.CreateActiveSubscriptionAsync(
            connection, "case-i", unitAmountCents: 4500);
        var provisioningBefore = await CountAsync(
            connection,
            "SELECT COUNT(*) FROM billing_v2_subscription_items "
            + "WHERE subscription_id = @p AND status = 'active'",
            subscriptionId);

        var renewals = BuildRenewals(
            connectionString,
            FakeRenewalStripeGateway.PastDue(
                periodStart: fixture.CycleStart(2),
                fixture.ProviderSubscriptionId("case-i")));
        var result = await renewals.HandleProviderSignalAsync(
            subscriptionId, default);

        Ensure(
            !result.Settled,
            "Scenario I : un impaye ne doit jamais etre regle.");
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT payment_state FROM billing_v2_subscriptions "
                + "WHERE id = @p",
                subscriptionId)
            == BillingV2SubscriptionPaymentStates.PaymentAttention,
            "Scenario I : l'impaye doit produire un etat payment_attention.");
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT status FROM billing_v2_subscriptions WHERE id = @p",
                subscriptionId) == "active",
            "Scenario I : l'abonnement reste actif, l'acces est conserve.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_subscription_items "
                + "WHERE subscription_id = @p AND status = 'active'",
                subscriptionId) == provisioningBefore,
            "Scenario I : aucun droit ne doit etre retire automatiquement.");
    }

    // -----------------------------------------------------------------
    // Scenarios A + B (volet documentaire) : une facture par cycle, meme
    // apres N rejeux du worker d'emission.
    // -----------------------------------------------------------------

    private static async Task ScenarioAB_CycleDocumentIsUniqueAsync(
        MySqlConnection connection,
        RenewalFixture fixture,
        string connectionString)
    {
        var subscriptionId = await fixture.CreateActiveSubscriptionAsync(
            connection, "case-doc", unitAmountCents: 4500);
        var gateway = FakeRenewalStripeGateway.Paid(
            amountCents: 4500,
            periodStart: fixture.CycleStart(2),
            fixture.ProviderSubscriptionId("case-doc"));
        var renewals = BuildRenewals(connectionString, gateway);
        var settlement = await renewals.HandleProviderSignalAsync(
            subscriptionId, default);
        Ensure(
            settlement.Settled && settlement.BillingEventId is not null,
            "Le cycle doit d'abord etre encaisse.");

        var bpce = new FakeInvoiceIssuingService();
        var issuer = BuildDocumentIssuer(connectionString, bpce);

        // Trois passages du worker d'emission sur le meme cycle.
        var first = await issuer.EnsureCycleInvoiceAsync(
            subscriptionId, settlement.BillingEventId!, 2, "corr-doc", default);
        var second = await issuer.EnsureCycleInvoiceAsync(
            subscriptionId, settlement.BillingEventId!, 2, "corr-doc", default);
        var third = await issuer.EnsureCycleInvoiceAsync(
            subscriptionId, settlement.BillingEventId!, 2, "corr-doc", default);

        Ensure(
            first.Succeeded,
            "Scenario A : le cycle encaisse doit produire une facture.");
        Ensure(
            first.CommercialDocumentId == second.CommercialDocumentId
            && second.CommercialDocumentId == third.CommercialDocumentId,
            "Scenario B : les rejeux doivent viser le meme document.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_subscription_documents "
                + "WHERE subscription_id = @p "
                + "AND document_kind = 'renewal_subscription_invoice'",
                subscriptionId) == 1,
            "Scenario B : un seul document pour le cycle 2.");
        Ensure(
            bpce.IssueCalls == 1,
            "Scenario B : une seule emission BPCE logique malgre 3 rejeux.");

        // Les montants viennent des snapshots du BillingEvent, pas d'un
        // recalcul : le document doit valoir exactement l'evenement.
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_subscription_documents doc "
                + "INNER JOIN billing_v2_billing_events event_row "
                + "ON event_row.id = doc.billing_event_id "
                + "WHERE doc.subscription_id = @p "
                + "AND doc.total_amount_cents = event_row.total_amount_cents "
                + "AND doc.cycle_sequence = event_row.cycle_sequence",
                subscriptionId) == 1,
            "Le document doit reproduire exactement le BillingEvent.");
    }

    // -----------------------------------------------------------------
    // Scenario J : BPCE repond, puis le reseau coupe
    // -----------------------------------------------------------------

    private static async Task ScenarioJ_BpceTimeoutKeepsPaymentKnownAsync(
        MySqlConnection connection,
        RenewalFixture fixture)
    {
        var subscriptionId = await fixture.CreateActiveSubscriptionAsync(
            connection, "case-j", unitAmountCents: 4500);
        var eventId = await fixture.InsertSettledRenewalEventAsync(
            connection, subscriptionId, cycleSequence: 2, amountCents: 4500);
        var documentId = await fixture.InsertCommercialDocumentAsync(
            connection, subscriptionId, eventId, cycleSequence: 2);

        // Premiere tentative : l'intention part, puis le reseau coupe. Elle
        // reste `in_flight` : on ignore si BPCE a cree la facture.
        await ExecuteAsync(
            connection,
            "INSERT INTO billing_v2_document_issuance_attempts (id, "
            + "commercial_document_id, billing_event_id, external_reference, "
            + "status, attempt_count, created_at, updated_at) VALUES (@id, "
            + "@doc, @evt, @ref, 'in_flight', 1, UTC_TIMESTAMP(6), "
            + "UTC_TIMESTAMP(6))",
            ("@id", Guid.NewGuid().ToString("D")),
            ("@doc", documentId),
            ("@evt", eventId),
            ("@ref", BillingV2DocumentIssuancePolicy.BuildExternalReference(
                documentId)));

        var decision = BillingV2DocumentIssuancePolicy.ResolveIndeterminate(
            BillingV2DocumentIssuancePolicy
                .InvoiceLookupByExternalReferenceSupported,
            lookupFoundExistingInvoice: false);

        Ensure(
            !decision.CanCallProvider && decision.RequiresManualReview,
            "Scenario J : apres coupure, aucune seconde facture automatique.");

        // Le paiement, lui, reste connu comme acquis. C'est le point du
        // scenario : une panne documentaire ne doit pas effacer un encaissement.
        Ensure(
            await ScalarStringAsync(
                connection,
                "SELECT settlement_status FROM billing_v2_billing_events "
                + "WHERE id = @p",
                eventId) == BillingV2SettlementStatuses.Settled,
            "Scenario J : l'abonnement paye doit rester connu comme paye.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM billing_v2_document_issuance_attempts "
                + "WHERE commercial_document_id = @p",
                documentId) == 1,
            "Scenario J : une seule intention d'emission par document.");
    }

    // -----------------------------------------------------------------
    // Invariants de schema apportes par la migration 061
    // -----------------------------------------------------------------

    private static async Task VerifyRenewalSchemaAsync(
        MySqlConnection connection)
    {
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM information_schema.columns "
                + "WHERE table_schema = DATABASE() "
                + "AND table_name = 'billing_v2_subscriptions' "
                + "AND column_name = @p",
                "payment_state") == 1,
            "La colonne payment_state doit exister.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM information_schema.columns "
                + "WHERE table_schema = DATABASE() "
                + "AND table_name = 'billing_v2_subscriptions' "
                + "AND column_name = @p",
                "billing_anchor_at") == 1,
            "L'ancre contractuelle doit exister.");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM information_schema.statistics "
                + "WHERE table_schema = DATABASE() "
                + "AND table_name = 'billing_v2_subscription_documents' "
                + "AND index_name = @p",
                "uq_billing_v2_subscription_document_billing_event") > 0,
            "Le document doit rester unique par BillingEvent (1:1 V2.0).");
        Ensure(
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM information_schema.statistics "
                + "WHERE table_schema = DATABASE() "
                + "AND table_name = 'billing_v2_subscription_documents' "
                + "AND index_name = @p",
                "uq_billing_v2_subscription_document_cycle") > 0,
            "Le document doit etre unique par cycle.");
    }

    // -----------------------------------------------------------------
    // Outillage
    // -----------------------------------------------------------------

    private static IBillingV2RenewalService BuildRenewals(
        string connectionString,
        IBillingV2StripeGateway gateway)
    {
        var sql = new SqlRuntimeConfiguration(
            PortalPersistenceMode.MariaDb,
            "mariadb",
            connectionString,
            "test",
            true);
        var stripe = new StripeRuntimeConfiguration(StripeMode.Test, "sk_test_fake");
        var rail = new BillingV2StripeRailService(
            sql,
            stripe,
            gateway,
            SystemBillingV2Clock.Instance,
            NullLogger<BillingV2StripeRailService>.Instance);
        return new BillingV2RenewalService(
            sql,
            SystemBillingV2Clock.Instance,
            gateway,
            rail,
            NullLogger<BillingV2RenewalService>.Instance);
    }

    private static IBillingV2DocumentIssuerService BuildDocumentIssuer(
        string connectionString,
        IInvoiceIssuingService invoiceIssuing)
        => new BillingV2DocumentIssuerService(
            new SqlRuntimeConfiguration(
                PortalPersistenceMode.MariaDb,
                "mariadb",
                connectionString,
                "test",
                true),
            invoiceIssuing,
            NullLogger<BillingV2DocumentIssuerService>.Instance);

    /// <summary>
    /// Faux BPCE : compte les emissions reelles. Un second appel signalerait
    /// une seconde facture, donc un second numero fiscal.
    /// </summary>
    private sealed class FakeInvoiceIssuingService : IInvoiceIssuingService
    {
        private readonly HashSet<string> _issued = new(StringComparer.Ordinal);

        public int IssueCalls { get; private set; }

        public Task<IssueInvoiceResult> IssueInvoiceAsync(
            string documentId,
            bool sendEmail,
            string correlationId,
            CancellationToken cancellationToken)
        {
            if (!_issued.Add(documentId))
            {
                return Task.FromResult(new IssueInvoiceResult(
                    false, "INVOICE_ALREADY_ISSUED", "Deja emise."));
            }

            IssueCalls++;
            return Task.FromResult(new IssueInvoiceResult(
                true, "INVOICE_ISSUED", "Emise."));
        }

        public Task<IssueInvoiceResult> ConfirmPaymentAsync(
            string documentId,
            string correlationId,
            string paymentMethod,
            CancellationToken cancellationToken)
            => Task.FromResult(new IssueInvoiceResult(
                true, "INVOICE_MARKED_PAID", "Payee."));

        public Task<byte[]?> GetCachedInvoicePdfAsync(
            string documentId, CancellationToken cancellationToken)
            => Task.FromResult<byte[]?>(null);

        public Task<byte[]?> EnsureInvoicePdfAsync(
            string documentId, CancellationToken cancellationToken)
            => Task.FromResult<byte[]?>(null);

        public Task<BpceInvoiceRecord?> GetInvoiceRecordAsync(
            string documentId, CancellationToken cancellationToken)
            => Task.FromResult<BpceInvoiceRecord?>(null);
    }

    private static async Task<long> CountAsync(
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

    /// <summary>
    /// Faux Stripe de renouvellement. Il LEVE si on tente de creer un
    /// checkout : c'est le garde-fou du scenario A.
    /// </summary>
    private sealed class FakeRenewalStripeGateway : IBillingV2StripeGateway
    {
        private BillingV2StripeInvoiceSnapshot? _invoice;
        private string _subscriptionStatus = "active";
        private string _providerSubscriptionId = "sub_fake";

        public int CreateCalls { get; private set; }

        public bool CanExecute => true;

        public static FakeRenewalStripeGateway Paid(
            long amountCents,
            DateTime periodStart,
            string providerSubscriptionId)
            => new()
            {
                _providerSubscriptionId = providerSubscriptionId,
                _invoice = Invoice(
                    "paid", amountCents, periodStart, providerSubscriptionId)
            };

        public static FakeRenewalStripeGateway PastDue(
            DateTime periodStart,
            string providerSubscriptionId)
            => new()
            {
                _providerSubscriptionId = providerSubscriptionId,
                _invoice = Invoice(
                    "open", 0, periodStart, providerSubscriptionId),
                _subscriptionStatus = "past_due"
            };

        private static BillingV2StripeInvoiceSnapshot Invoice(
            string status,
            long amountCents,
            DateTime periodStart,
            string providerSubscriptionId)
            => new(
                $"in_{providerSubscriptionId}",
                providerSubscriptionId,
                "cus_fake",
                status,
                "eur",
                amountCents,
                amountCents,
                $"pi_{providerSubscriptionId}",
                "subscription_cycle",
                new Dictionary<string, string>(StringComparer.Ordinal),
                periodStart);

        public Task<BillingV2StripeCreateResult> CreateCheckoutSessionAsync(
            BillingV2StripeCheckoutRequest request,
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            throw new InvalidOperationException(
                "Un renouvellement ne doit jamais creer de checkout.");
        }

        public Task<BillingV2StripeSessionSnapshot?> GetCheckoutSessionAsync(
            string sessionId,
            CancellationToken cancellationToken)
            => Task.FromResult<BillingV2StripeSessionSnapshot?>(null);

        public Task<BillingV2StripeSessionSnapshot?> FindCheckoutSessionAsync(
            BillingV2StripeSessionLocator locator,
            CancellationToken cancellationToken)
            => Task.FromResult<BillingV2StripeSessionSnapshot?>(null);

        public Task<BillingV2StripeSubscriptionSnapshot?> GetSubscriptionAsync(
            string providerSubscriptionId,
            CancellationToken cancellationToken)
            => Task.FromResult<BillingV2StripeSubscriptionSnapshot?>(
                new BillingV2StripeSubscriptionSnapshot(
                    _providerSubscriptionId,
                    _subscriptionStatus,
                    "cus_fake",
                    $"in_{_providerSubscriptionId}",
                    new Dictionary<string, string>(StringComparer.Ordinal)));

        public Task<BillingV2StripeInvoiceSnapshot?> GetInvoiceAsync(
            string providerInvoiceId,
            CancellationToken cancellationToken)
            => Task.FromResult(_invoice);

        public Task<BillingV2StripeInvoiceSnapshot?>
            GetLatestInvoiceForSubscriptionAsync(
                string providerSubscriptionId,
                CancellationToken cancellationToken)
            => Task.FromResult(_invoice);
    }

    private sealed record RenewalFixture(
        string Marker,
        string CustomerId,
        string ServiceId,
        string ServicePriceId,
        string CommitmentTermId,
        string PortalUserId,
        DateTime AnchorUtc)
    {
        private readonly List<string> _subscriptionIds = [];
        private readonly List<string> _documentIds = [];

        /// <summary>
        /// `provider_subscription_id` est unique par provider+environnement :
        /// chaque cas de test a donc son propre identifiant Stripe.
        /// </summary>
        public string ProviderSubscriptionId(string label)
            => $"sub_{Marker[..16]}_{label}";

        public DateTime CycleStart(int cycleSequence)
            => BillingV2BillingCalendar
                .ResolveCyclePeriod(AnchorUtc, 1, cycleSequence)
                .StartUtc;

        public static async Task<RenewalFixture> CreateAsync(
            MySqlConnection connection)
        {
            var marker = $"bv2-renew-{Guid.NewGuid():N}";
            var customerId = Guid.NewGuid().ToString("D");
            var serviceId = Guid.NewGuid().ToString("D");
            var priceId = Guid.NewGuid().ToString("D");
            var termId = Guid.NewGuid().ToString("D");
            var portalUserId = Guid.NewGuid().ToString("D");

            await ExecuteAsync(
                connection,
                "INSERT INTO customers (id, external_reference, display_name, "
                + "status, created_at, updated_at) VALUES (@id, @ref, "
                + "'Client renouvellement', 'active', UTC_TIMESTAMP(6), "
                + "UTC_TIMESTAMP(6))",
                ("@id", customerId), ("@ref", marker));
            await ExecuteAsync(
                connection,
                "INSERT INTO portal_users (id, customer_id, "
                + "identity_provider_subject, email, display_name, status, "
                + "role, created_at, updated_at) VALUES (@id, @cust, @subject, "
                + "@email, 'Admin renouvellement', 'active', 'internal_admin', "
                + "UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", portalUserId), ("@cust", customerId),
                ("@subject", $"renewal-{marker}"),
                ("@email", $"{marker}@example.invalid"));
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_services (id, code, name, billing_type, "
                + "default_scope_type, discount_eligible, status, created_at, "
                + "updated_at) VALUES (@id, @code, 'Service', 'recurring', "
                + "'subscription', 1, 'active', UTC_TIMESTAMP(6), "
                + "UTC_TIMESTAMP(6))",
                ("@id", serviceId), ("@code", $"RNW-{marker[..12]}"));
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_service_prices (id, service_id, "
                + "price_code, price_version, amount_cents, currency, "
                + "billing_cadence, valid_from, status, created_at) VALUES "
                + "(@id, @svc, @code, 1, 4500, 'EUR', 'monthly', "
                + "'2026-01-01 00:00:00', 'active', UTC_TIMESTAMP(6))",
                ("@id", priceId), ("@svc", serviceId),
                ("@code", $"RNW-P-{marker[..12]}"));
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_commitment_terms (id, code, name, "
                + "commitment_months, discount_basis_points, status, "
                + "created_at, updated_at) VALUES (@id, @code, 'Engagement 12', "
                + "12, 0, 'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", termId), ("@code", $"RNW-T-{marker[..12]}"));

            return new RenewalFixture(
                marker,
                customerId,
                serviceId,
                priceId,
                termId,
                portalUserId,
                new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));
        }

        public async Task<string> CreateActiveSubscriptionAsync(
            MySqlConnection connection,
            string label,
            long unitAmountCents)
        {
            var subscriptionId = Guid.NewGuid().ToString("D");
            _subscriptionIds.Add(subscriptionId);

            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_subscriptions (id, customer_id, "
                + "commitment_term_id, status, payment_mode, currency, "
                + "billing_model, started_at, billing_anchor_at, "
                + "discount_basis_points_snapshot, created_at, updated_at) "
                + "VALUES (@id, @cust, @term, 'active', 'monthly', 'EUR', 'v2', "
                + "@anchor, @anchor, 0, @anchor, @anchor)",
                ("@id", subscriptionId), ("@cust", CustomerId),
                ("@term", CommitmentTermId), ("@anchor", AnchorUtc));

            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_subscription_items (id, "
                + "subscription_id, service_id, service_price_id, scope_type, "
                + "quantity, amount_cents_snapshot, currency, "
                + "discount_eligible_snapshot, source, effective_from, status, "
                + "created_at, updated_at) VALUES (@id, @sub, @svc, @price, "
                + "'subscription', 1, @amount, 'EUR', 1, 'manual', @anchor, "
                + "'active', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", Guid.NewGuid().ToString("D")),
                ("@sub", subscriptionId), ("@svc", ServiceId),
                ("@price", ServicePriceId), ("@amount", unitAmountCents),
                ("@anchor", AnchorUtc));

            // Accord de paiement : c'est lui qui porte l'identifiant de
            // l'abonnement Stripe, donc la cible de la relecture bornee.
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_payment_agreements (id, "
                + "subscription_id, provider, environment, "
                + "provider_subscription_id, status, created_at, updated_at) "
                + "VALUES (@id, @sub, 'stripe', 'test', @provider, 'active', "
                + "UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", Guid.NewGuid().ToString("D")),
                ("@sub", subscriptionId),
                ("@provider", ProviderSubscriptionId(label)));

            return subscriptionId;
        }

        public async Task<string> InsertSettledRenewalEventAsync(
            MySqlConnection connection,
            string subscriptionId,
            int cycleSequence,
            long amountCents)
        {
            var eventId = Guid.NewGuid().ToString("D");
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_billing_events (id, customer_id, "
                + "subscription_id, event_type, direction, financial_status, "
                + "settlement_status, document_status, currency, period_start, "
                + "period_end, payment_mode_snapshot, "
                + "commitment_months_snapshot, cycle_sequence, "
                + "discount_basis_points_snapshot, gross_amount_cents, "
                + "discount_amount_cents, net_amount_cents, tax_amount_cents, "
                + "total_amount_cents, pricing_engine_version, "
                + "idempotency_key_canonical, idempotency_key_hash, "
                + "created_at, finalized_at) VALUES (@id, @cust, @sub, "
                + "'renewal_charge', 'debit', 'finalized', 'settled', 'none', "
                + "'EUR', @start, @end, 'monthly', 12, @cycle, 0, @amount, 0, "
                + "@amount, 0, @amount, 'test', @canonical, "
                + "SHA2(@canonical, 256), UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                ("@id", eventId), ("@cust", CustomerId),
                ("@sub", subscriptionId),
                ("@start", CycleStart(cycleSequence)),
                ("@end", CycleStart(cycleSequence + 1)),
                ("@cycle", cycleSequence), ("@amount", amountCents),
                ("@canonical", $"{Marker}|settled|{subscriptionId}|{cycleSequence}"));
            return eventId;
        }

        public async Task<string> InsertCommercialDocumentAsync(
            MySqlConnection connection,
            string subscriptionId,
            string billingEventId,
            int cycleSequence)
        {
            var documentId = Guid.NewGuid().ToString("D");
            _documentIds.Add(documentId);
            await ExecuteAsync(
                connection,
                "INSERT INTO commercial_documents (id, customer_id, origin, "
                + "document_type, status, title, internal_reference, currency, "
                + "subtotal_amount_cents, tax_amount_cents, "
                + "total_amount_cents, disclaimer, created_by_user_id, "
                + "created_at, updated_at) VALUES (@id, @cust, 'billing_v2', "
                + "'informational_invoice', 'shared_with_customer', "
                + "'Facture renouvellement', @ref, 'EUR', 4500, 0, 4500, "
                + "'Document informatif.', @user, UTC_TIMESTAMP(6), "
                + "UTC_TIMESTAMP(6))",
                ("@id", documentId), ("@cust", CustomerId),
                ("@ref", $"{Marker}-{cycleSequence}"),
                ("@user", PortalUserId));
            await ExecuteAsync(
                connection,
                "INSERT INTO billing_v2_subscription_documents (id, "
                + "subscription_id, commercial_document_id, billing_event_id, "
                + "document_kind, cycle_sequence, period_start, period_end, "
                + "subtotal_amount_cents, discount_amount_cents, "
                + "tax_amount_cents, total_amount_cents, currency, status, "
                + "created_at, updated_at) VALUES (@id, @sub, @doc, @evt, "
                + "'renewal_subscription_invoice', @cycle, @start, @end, 4500, "
                + "0, 0, 4500, 'EUR', 'created', UTC_TIMESTAMP(6), "
                + "UTC_TIMESTAMP(6))",
                ("@id", Guid.NewGuid().ToString("D")),
                ("@sub", subscriptionId), ("@doc", documentId),
                ("@evt", billingEventId), ("@cycle", cycleSequence),
                ("@start", CycleStart(cycleSequence).Date),
                ("@end", CycleStart(cycleSequence + 1).Date));
            return documentId;
        }

        public async Task CleanupAsync(MySqlConnection connection)
        {
            // Ordre impose par les cles etrangeres.
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_document_issuance_attempts "
                + "WHERE external_reference LIKE @p",
                ("@p", "BV2-DOC-%"));
            foreach (var subscriptionId in _subscriptionIds)
            {
                await ExecuteAsync(
                    connection,
                    "DELETE FROM billing_v2_subscription_documents "
                    + "WHERE subscription_id = @p",
                    ("@p", subscriptionId));
                await ExecuteAsync(
                    connection,
                    "DELETE FROM billing_v2_payment_attempts WHERE "
                    + "billing_event_id IN (SELECT id FROM "
                    + "billing_v2_billing_events WHERE subscription_id = @p)",
                    ("@p", subscriptionId));
                await ExecuteAsync(
                    connection,
                    "DELETE FROM billing_v2_billing_event_lines WHERE "
                    + "billing_event_id IN (SELECT id FROM "
                    + "billing_v2_billing_events WHERE subscription_id = @p)",
                    ("@p", subscriptionId));
                await ExecuteAsync(
                    connection,
                    "DELETE FROM billing_v2_billing_events "
                    + "WHERE subscription_id = @p",
                    ("@p", subscriptionId));
                await ExecuteAsync(
                    connection,
                    "DELETE FROM billing_v2_payment_agreements "
                    + "WHERE subscription_id = @p",
                    ("@p", subscriptionId));
                await ExecuteAsync(
                    connection,
                    "DELETE FROM billing_v2_subscription_items "
                    + "WHERE subscription_id = @p",
                    ("@p", subscriptionId));
                await ExecuteAsync(
                    connection,
                    "DELETE FROM billing_v2_subscriptions WHERE id = @p",
                    ("@p", subscriptionId));
            }

            // Les documents crees par le service ne sont pas dans la liste du
            // fixture : on nettoie par client, sinon la suppression de
            // portal_users bute sur `fk_commercial_documents_author`.
            await ExecuteAsync(
                connection,
                "DELETE FROM commercial_document_lines WHERE document_id IN "
                + "(SELECT id FROM commercial_documents WHERE customer_id = @p)",
                ("@p", CustomerId));
            await ExecuteAsync(
                connection,
                "DELETE FROM commercial_documents WHERE customer_id = @p",
                ("@p", CustomerId));

            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_service_prices WHERE id = @p",
                ("@p", ServicePriceId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_services WHERE id = @p",
                ("@p", ServiceId));
            await ExecuteAsync(
                connection,
                "DELETE FROM billing_v2_commitment_terms WHERE id = @p",
                ("@p", CommitmentTermId));
            await ExecuteAsync(
                connection,
                "DELETE FROM portal_users WHERE id = @p",
                ("@p", PortalUserId));
            await ExecuteAsync(
                connection,
                "DELETE FROM customers WHERE id = @p",
                ("@p", CustomerId));
        }
    }
}
