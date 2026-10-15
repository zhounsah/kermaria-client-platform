using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kermaria.ApiInternal.SmokeTests;

public static class BillingV2PrivateVatTestTests
{
    private const string UserId = "40df072e-9ba2-4aa5-9f98-025d5dd0ae63";
    private const string CustomerId = "bb868d7c-2375-4323-be18-68635713445d";
    private const string PilotId = "9ab5b4aa-31b4-43ab-ae84-d1c3932b4ce5";

    public static void Run()
    {
        var now = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);
        var runtime = Runtime(now);
        var stripe = Stripe();
        var session = Session(now);
        var selection = Selection();
        var composition = Composition();
        var pricing = Pricing();
        var request = Request(selection);

        Require(BillingV2PrivateVatTestPolicy.SelectionFingerprint ==
            "accb84353fbc9b9632f3fb276dfa4650767e28b45911a4dff93f54a0971ca9ff",
            "La sélection privée doit garder son empreinte attendue.");

        var first = BillingV2PrivateVatTestPolicy.CheckActor(
            runtime, stripe, session, now);
        var retry = BillingV2PrivateVatTestPolicy.CheckActor(
            runtime, stripe, session, now.AddMinutes(1));
        Require(first.Authorized && retry.Authorized
            && first.IdempotencyKey == retry.IdempotencyKey
            && first.IdempotencyKey == "private-vat-test:" + PilotId,
            "Tous les rejeux de la fenêtre doivent partager la même clé serveur.");
        Require(BillingV2AuthoritativeCheckoutGate.Evaluate(
                runtime, true, first.IdempotencyKey, true).Authorized
            && !BillingV2AuthoritativeCheckoutGate.Evaluate(
                runtime, true, "browser-key").Authorized
            && !BillingV2AuthoritativeCheckoutGate.Evaluate(
                runtime with { PrivateVatTestEnabled = false }, true,
                first.IdempotencyKey, true).Authorized,
            "Les trois drapeaux publics fermés ne doivent pas être rouverts.");
        var launch = new BillingV2LaunchReadinessSnapshot(true, true);
        var mappings = new BillingV2ProviderPriceMappingStatus(
            true, [], [], [], "stripe", "live");
        Require(BillingV2CheckoutReadinessGate.Evaluate(
                runtime, launch, mappings,
                BillingV2DocumentReadinessStatus.ReadyForCheckout,
                privateVatTestAuthorized: true).Authorized
            && !BillingV2CheckoutReadinessGate.Evaluate(
                runtime, launch, mappings,
                BillingV2DocumentReadinessStatus.ReadyForCheckout).Authorized,
            "La readiness privée ne doit pas devenir une readiness publique.");

        var allowed = BillingV2PrivateVatTestPolicy.CheckSelectionAndAmount(
            runtime, stripe, session, request, composition, pricing,
            "stripe", now);
        Require(allowed.Authorized,
            "Le compte, la sélection et le montant exacts doivent être admis.");
        Require(!BillingV2PrivateVatTestPolicy.CheckActor(
            runtime, stripe, session with { UserId = Guid.NewGuid().ToString() },
            now).Authorized,
            "Un autre utilisateur du même client doit être refusé.");
        Require(!BillingV2PrivateVatTestPolicy.CheckActor(
            runtime, stripe, session with { CustomerId = Guid.NewGuid().ToString() },
            now).Authorized,
            "Un autre client doit être refusé.");
        Require(!BillingV2PrivateVatTestPolicy.CheckSelectionAndAmount(
            runtime, stripe, session, request with
            {
                Selection = selection with
                {
                    Components = [new("VPN-ACCESS", "PLUS", 1)]
                }
            }, composition, pricing, "stripe", now).Authorized,
            "Une autre sélection doit être refusée avant écriture.");
        Require(!BillingV2PrivateVatTestPolicy.CheckSelectionAndAmount(
            runtime, stripe, session, request, composition,
            pricing with { TotalDueNowCents = 591 }, "stripe", now).Authorized,
            "Un centime au-dessus de 590 doit être refusé.");
        Require(!BillingV2PrivateVatTestPolicy.CheckSelectionAndAmount(
            runtime, stripe, session, request, composition,
            pricing with { PayableRecurringAmountCents = 1200 }, "stripe", now)
            .Authorized,
            "Un premier paiement à 590 avec renouvellement supérieur doit être refusé.");
        Require(!BillingV2PrivateVatTestPolicy.CheckSelectionAndAmount(
            runtime, stripe, session, request with
            {
                CartCheckoutLink = new("cart", 1, 590, 590, "cgv")
            }, composition, pricing, "stripe", now).Authorized,
            "Le Cart ne doit pas contourner le parcours direct privé.");
        Require(!BillingV2PrivateVatTestPolicy.CheckActor(
            runtime, stripe, session, now.AddMinutes(30)).Authorized,
            "La création doit cesser 30 minutes avant l'expiration Stripe.");
        Require(!BillingV2PrivateVatTestPolicy.CheckActor(
            runtime with { PrivateVatTestExpiresAtUtc = now.AddHours(5) },
            stripe, session, now).Authorized,
            "Une fenêtre de plus de deux heures doit être refusée.");
        Require(!BillingV2PrivateVatTestPolicy.CheckActor(
            runtime with { AuthoritativeCheckoutEnabled = true },
            stripe, session, now).Authorized,
            "Le mode privé ne doit pas coexister avec un Checkout public ouvert.");
        Require(!BillingV2PrivateVatTestPolicy.CheckActor(
            runtime, stripe with { FrenchInclusiveVatTaxRateId = "txr_other" },
            session, now).Authorized,
            "Un autre taux Stripe Live doit être refusé.");

        var eventSnapshot = BillingEvent();
        Require(BillingV2PrivateVatTestPolicy.CheckDispatch(
            runtime, stripe, UserId, CustomerId, first.IdempotencyKey,
            BillingV2PrivateVatTestPolicy.SelectionFingerprint,
            eventSnapshot, now).Authorized,
            "Le dispatch doit relire la provenance persistée.");
        Require(!BillingV2PrivateVatTestPolicy.CheckDispatch(
            runtime, stripe, Guid.NewGuid().ToString(), CustomerId,
            first.IdempotencyKey,
            BillingV2PrivateVatTestPolicy.SelectionFingerprint,
            eventSnapshot, now).Authorized,
            "Un outbox d'un autre acteur ne doit pas appeler Stripe.");
        Require(!BillingV2PrivateVatTestPolicy.CheckDispatch(
            runtime, stripe, UserId, CustomerId, first.IdempotencyKey,
            BillingV2PrivateVatTestPolicy.SelectionFingerprint,
            eventSnapshot with { TaxAmountCents = 0 }, now).Authorized,
            "Le dispatch doit refuser une taxe incohérente.");

        var gatewayRequest = GatewayRequest(runtime);
        Require(BillingV2PrivateVatTestPolicy.CheckGateway(
            runtime, stripe, gatewayRequest, now).Authorized,
            "La frontière Stripe doit accepter seulement le montant et le taux exacts.");
        // Reproduit le vrai chemin outbox -> fabrique -> garde. La fabrique
        // convertit EUR en eur pour Stripe ; le garde doit admettre les deux
        // casses sans relâcher la restriction à la devise euro.
        var fromFactory = PrivateGatewayRequestFromEvent(
            eventSnapshot, runtime);
        Require(fromFactory.Currency == "eur"
            && BillingV2PrivateVatTestPolicy.CheckGateway(
                runtime, stripe, fromFactory, now).Authorized
            && BillingV2StripeCheckoutRequestFactory.ToFormParameters(
                fromFactory)["line_items[0][price_data][currency]"] == "eur",
            "La requête de la véritable fabrique doit passer le garde sans modifier la devise Stripe.");
        var otherCurrency = PrivateGatewayRequestFromEvent(
            eventSnapshot with { Currency = "USD" }, runtime);
        var otherCurrencyDecision = BillingV2PrivateVatTestPolicy.CheckGateway(
            runtime, stripe, otherCurrency, now);
        Require(otherCurrency.Currency == "usd"
            && !otherCurrencyDecision.Authorized
            && otherCurrencyDecision.ReasonCode ==
                "PRIVATE_VAT_TEST_GATEWAY_SCOPE_MISMATCH",
            "Une autre devise issue de la fabrique doit rester refusée.");
        var form = BillingV2StripeCheckoutRequestFactory.ToFormParameters(
            gatewayRequest);
        Require(form["expires_at"] == new DateTimeOffset(
                runtime.PrivateVatTestExpiresAtUtc!.Value).ToUnixTimeSeconds()
                .ToString()
            && form["line_items[0][tax_rates][0]"] ==
                BillingV2PrivateVatTestPolicy.LiveTaxRateId,
            "La Session Stripe doit porter l'expiration et le taux inclusif Live.");
        Require(!BillingV2PrivateVatTestPolicy.CheckGateway(
            runtime, stripe,
            gatewayRequest with { ExpectedAmountCents = 591 }, now).Authorized,
            "Un appel direct à la passerelle ne doit pas augmenter le montant.");
        Require(!BillingV2PrivateVatTestPolicy.CheckGateway(
            runtime, stripe, gatewayRequest, now.AddMinutes(30)).Authorized,
            "La passerelle doit refuser un dispatch tardif.");
        var liveRuntime = Runtime(DateTime.UtcNow);
        var realGateway = new BillingV2StripeGateway(
            liveRuntime, stripe, new ThrowingHttpClientFactory(),
            NullLogger<BillingV2StripeGateway>.Instance);
        var refusedGateway = realGateway.CreateCheckoutSessionAsync(
            GatewayRequest(liveRuntime) with { ExpectedAmountCents = 591 },
            CancellationToken.None).GetAwaiter().GetResult();
        Require(!refusedGateway.Succeeded
            && refusedGateway.Code == "PRIVATE_VAT_TEST_GATEWAY_SCOPE_MISMATCH",
            "La passerelle réelle doit refuser avant tout GET Tax Rate ou POST Stripe.");
        var legacyExecutor = new BillingV2ProviderCheckoutExecutor(
            runtime,
            new PayPalRuntimeConfiguration(PayPalMode.Sandbox, null, null),
            stripe,
            new ThrowingHttpClientFactory());
        var legacy = legacyExecutor.ExecuteAsync(
            new BillingV2ProviderCheckoutExecutionRequest(
                "outbox", "hash", "{}"), CancellationToken.None)
            .GetAwaiter().GetResult();
        Require(!legacy.Succeeded
            && legacy.Code == "PRIVATE_VAT_TEST_LEGACY_PROVIDER_CHECKOUT_CLOSED",
            "Le chemin Price ID/PayPal doit être fermé avant toute requête HTTP.");

        var evidence = InvoiceEvidence(now, first.IdempotencyKey!);
        var document = InvoiceDocument();
        Require(BillingV2PrivateVatTestPolicy.CheckSettledInvoice(
            runtime with { PrivateVatTestEnabled = false }, stripe,
            evidence, document).Authorized,
            "Une facture déjà due doit pouvoir être émise après fermeture de la fenêtre.");
        Require(!BillingV2PrivateVatTestPolicy.CheckSettledInvoice(
            runtime, stripe, evidence with { EventTaxCents = 0 }, document)
            .Authorized,
            "Une facture à TVA nulle ne doit pas bénéficier de la dérogation BPCE.");
        Require(!BillingV2PrivateVatTestPolicy.CheckSettledInvoice(
            runtime, stripe, evidence,
            document with { TotalAmountCents = 591 }).Authorized,
            "Le document BPCE ne doit pas diverger de Stripe.");
        var laterWindow = runtime with
        {
            PrivateVatTestStartsAtUtc = now.AddHours(3),
            PrivateVatTestExpiresAtUtc = now.AddHours(4)
        };
        Require(BillingV2PrivateVatTestPolicy.CheckConfiguration(
                laterWindow, stripe, now.AddHours(3)).Authorized
            && !BillingV2PrivateVatTestPolicy.CheckSettledInvoice(
                laterWindow, stripe, evidence, document).Authorized,
            "Une nouvelle fenêtre ne doit pas faire passer une ancienne facture pour une opération de la nouvelle recette.");

        var sql = new SqlRuntimeConfiguration(
            PortalPersistenceMode.MariaDb, "mariadb", "test", "test", true);
        Require(BillingV2ProviderInboundGate.CanProcess(sql)
            && BillingV2ReconciliationGate.CanProcess(sql, stripe),
            "Webhook et rapprochement restent opérationnels quand Checkout est fermé.");
        Require(!BillingV2ProviderInboundGate.CanProcess(
            sql with { ConnectionString = null })
            && !BillingV2ReconciliationGate.CanProcess(
                sql, new StripeRuntimeConfiguration(StripeMode.Disabled)),
            "Webhook et rapprochement restent fermés sans leurs dépendances.");
    }

    private static BillingV2RuntimeConfiguration Runtime(DateTime now)
        => new(false, false, false, true, true, true,
            PrivateVatTestEnabled: true,
            PrivateVatTestId: PilotId,
            PrivateVatTestUserId: UserId,
            PrivateVatTestCustomerId: CustomerId,
            PrivateVatTestSelectionFingerprint:
                BillingV2PrivateVatTestPolicy.SelectionFingerprint,
            PrivateVatTestStartsAtUtc: now.AddMinutes(-1),
            PrivateVatTestExpiresAtUtc: now.AddMinutes(59));

    private static StripeRuntimeConfiguration Stripe()
        => new(StripeMode.Live, "sk_live_fake_for_unit_test",
            BillingV2PrivateVatTestPolicy.LiveTaxRateId);

    private static PortalSessionContext Session(DateTime now)
        => new("session", UserId, CustomerId, "CUST-TEST",
            "test@example.invalid", "Compte test", "active", "client_user",
            now, now.AddHours(1));

    private static BillingV2PublicSelection Selection()
        => new(null, "FLEX", "monthly", "", false, null, false, null,
            false, 0, false,
            [new BillingV2PublicSelectionComponent("STORAGE-SHARED", "64", 1)]);

    private static BillingV2AuthoritativeCheckoutRequest Request(
        BillingV2PublicSelection selection)
        => new(selection, "stripe", "browser-key",
            "https://example.invalid/success", "https://example.invalid/cancel");

    private static BillingV2AuthoritativeCheckoutComposition Composition()
        => new(null, "term-flex", "monthly", 1, 0,
            [new BillingV2NewSubscriptionPresetItem(
                "item", "service", "tier", "price", "STORAGE-SHARED", "64",
                "price-code", "subscription", 1, 590, "EUR", "monthly", true)],
            BillingV2PrivateVatTestPolicy.SelectionCanonical,
            BillingV2PrivateVatTestPolicy.SelectionFingerprint);

    private static BillingV2PricingResult Pricing()
        => new(590, 0, 590, 0, 590, 590, 0, 0, 590, null);

    private static BillingV2FinalizedBillingEvent BillingEvent()
        => new("event", "subscription", CustomerId, "finalized", "none",
            "EUR", "monthly", 1, 590, 590, 0, 98, 1, true);

    private static BillingV2StripeCheckoutRequest GatewayRequest(
        BillingV2RuntimeConfiguration runtime)
        => new(BillingV2StripeModes.Subscription, "EUR", 590,
            [new BillingV2StripeLineRequest("Abonnement mensuel", 590, true)],
            "test@example.invalid", "https://example.invalid/success",
            "https://example.invalid/cancel",
            new Dictionary<string, string>
            {
                ["customer_id"] = CustomerId,
                ["private_vat_test_id"] = PilotId,
                ["private_vat_test_user_id"] = UserId
            }, "attempt-key", null, BillingV2PrivateVatTestPolicy.LiveTaxRateId,
            PilotId, runtime.PrivateVatTestExpiresAtUtc);

    private static BillingV2StripeCheckoutRequest PrivateGatewayRequestFromEvent(
        BillingV2FinalizedBillingEvent billingEvent,
        BillingV2RuntimeConfiguration runtime)
    {
        var request = BillingV2StripeCheckoutRequestFactory.Build(
            billingEvent, "attempt", "attempt-key", "test@example.invalid",
            "https://example.invalid/success",
            "https://example.invalid/cancel",
            inclusiveTaxRateId: BillingV2PrivateVatTestPolicy.LiveTaxRateId);
        var metadata = request.Metadata.ToDictionary(
            pair => pair.Key, pair => pair.Value,
            StringComparer.Ordinal);
        metadata["private_vat_test_id"] = PilotId;
        metadata["private_vat_test_user_id"] = UserId;
        return request with
        {
            Metadata = metadata,
            PrivateVatTestId = PilotId,
            ExpiresAtUtc = runtime.PrivateVatTestExpiresAtUtc
        };
    }

    private static BillingV2PrivateVatInvoiceEvidence InvoiceEvidence(
        DateTime now, string idempotencyKey)
        => new(UserId, CustomerId, idempotencyKey,
            BillingV2PrivateVatTestPolicy.SelectionFingerprint,
            590, 98, "settled", now);

    private static DocumentForIssuing InvoiceDocument()
        => new("document", CustomerId, "CUST-TEST", "Compte test",
            "test@example.invalid", null, null, "France",
            "Facture", "REF-TEST", "EUR", 590, "shared_with_customer",
            [new CommercialDocumentLine(
                "line", "Stockage partagé", "Période de test", 1m,
                "période", 492, 2000, "standard", "TVA 20 %", 492, 10,
                "2026-10-14", "2026-10-14", BillingVatPolicy.FrenchStandard)]);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => throw new InvalidOperationException(
                "Aucun appel HTTP fournisseur ne doit partir pendant ce test.");
    }
}
