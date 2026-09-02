using System.Net;
using System.Reflection;
using System.Net.Http;
using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Tests purs du coeur de remboursement Billing V2.
///
/// Ils couvrent les invariants qui doivent rester vrais meme avant une base
/// MariaDB jetable : le montant est relu du settlement, la preuve provider est
/// stricte, et un retry ne change jamais la cle Stripe. Les tests SQL
/// correspondants restent opt-in et exigent BILLING_V2_TEST_MARIADB_CONNECTION.
/// </summary>
public static class BillingV2RefundTests
{
    public static async Task RunAsync()
    {
        VerifyFullRefundUsesSettledFinancialSource();
        VerifyUnsettledPaymentIsRefused();
        VerifyIssuedDocumentIsRefusedPendingCreditNote();
        VerifyPendingDocumentIsRefusedBeforeProviderDispatch();
        VerifyRefundKeyIsStableForDuplicateRequests();
        VerifyPendingAndFailureNeverConfirmSettlement();
        VerifyProviderAmountAndCurrencyMustMatch();
        VerifyProviderConfirmationIsIdempotentForWebhookAndReconciliation();
        VerifyConfirmedRefundBlocksRenewalAndQueuesProviderCancellation();
        VerifyExecutionIsFailClosed();
        VerifyExecutionRequiresProviderOutbox();
        VerifyRuntimeFlagIsFailClosed();
        VerifyReadinessSeparatesEvidenceFromActivation();
        VerifyMissingBillingEventIsRefused();
        VerifyInvalidSourceAmountIsRefused();
        VerifyUnresolvedProviderPaymentIsRefused();
        VerifyRecurringWithoutProviderAnchorIsRefusedAtRequest();
        VerifySecondRefundOnRefundedSourceIsRefused();
        VerifyUnobservedProviderRefundIsRefused();
        VerifyRefundOnAnotherPaymentIsRefused();
        VerifyCanceledProviderRefundIsAFailure();
        VerifyMissingSubscriptionBlocksCompensation();
        VerifyExecutionRequiresSqlAndStripeGateway();
        VerifyOutboxPayloadRoundTripAndRejection();
        VerifyPartialRefundIsNotSilentlyIntroduced();
        await VerifyStripeRefundRequestIsIdempotentAndAuthoritativeAsync();
        await VerifyAmbiguousProviderTimeoutStaysIndeterminateAsync();
        await VerifyBoundedProviderReconciliationFindsOnlyOurRefundAsync();
    }

    private static void VerifyFullRefundUsesSettledFinancialSource()
    {
        var decision = BillingV2RefundPolicy.EvaluateFullRequest(Source());
        Ensure(
            decision.IsValid && decision.AmountCents == 2_290
            && decision.Currency == "EUR",
            "Un remboursement integral doit reprendre exactement montant et devise settled.");
    }

    private static void VerifyUnsettledPaymentIsRefused()
    {
        var decision = BillingV2RefundPolicy.EvaluateFullRequest(
            Source() with { SettlementStatus = BillingV2SettlementStatuses.Pending });
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_REFUND_PAYMENT_NOT_SETTLED",
            "Un paiement non settled ne peut pas faire l'objet d'un refund.");
    }

    private static void VerifyIssuedDocumentIsRefusedPendingCreditNote()
    {
        var decision = BillingV2RefundPolicy.EvaluateFullRequest(
            Source() with { DocumentStatus = BillingV2EventDocumentStatuses.Issued });
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_REFUND_CREDIT_NOTE_REQUIRED",
            "Une facture emise exige un avoir canonique avant tout refund.");
    }

    private static void VerifyPendingDocumentIsRefusedBeforeProviderDispatch()
    {
        var decision = BillingV2RefundPolicy.EvaluateFullRequest(
            Source() with { DocumentStatus = BillingV2EventDocumentStatuses.Pending });
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_REFUND_DOCUMENT_IN_PROGRESS",
            "Un document en cours bloque le dispatch pour eviter une course avec BPCE.");
    }

    private static void VerifyRefundKeyIsStableForDuplicateRequests()
    {
        const string eventId = "11111111-2222-3333-4444-555555555555";
        var first = BillingV2RefundOutbox.CanonicalIdempotencyKey(eventId);
        var second = BillingV2RefundOutbox.CanonicalIdempotencyKey(eventId);
        Ensure(
            first == second
            && BillingV2RefundOutbox.ComputeIdempotencyHash(eventId)
                == BillingV2RefundOutbox.ComputeIdempotencyHash(eventId),
            "Deux demandes du meme BillingEvent doivent converger vers un seul refund provider.");
    }

    private static void VerifyPendingAndFailureNeverConfirmSettlement()
    {
        var pending = BillingV2RefundConfirmationPolicy.Evaluate(
            Source(), Observation(status: "pending"));
        var failed = BillingV2RefundConfirmationPolicy.Evaluate(
            Source(), Observation(status: "failed"));

        Ensure(
            !pending.IsConfirmed && !pending.IsFailed
            && pending.ReasonCode == "BILLING_V2_REFUND_PROVIDER_PENDING",
            "Un refund provider pending ne vaut jamais settlement refunded.");
        Ensure(
            !failed.IsConfirmed && failed.IsFailed
            && failed.ReasonCode == "BILLING_V2_REFUND_PROVIDER_FAILED",
            "Un refund provider failed ne vaut jamais settlement refunded.");
    }

    private static void VerifyProviderAmountAndCurrencyMustMatch()
    {
        var amount = BillingV2RefundConfirmationPolicy.Evaluate(
            Source(), Observation(amountCents: 2_289));
        var currency = BillingV2RefundConfirmationPolicy.Evaluate(
            Source(), Observation(currency: "USD"));
        Ensure(
            !amount.IsConfirmed
            && amount.ReasonCode == "BILLING_V2_REFUND_AMOUNT_MISMATCH",
            "Un montant provider different bloque refunded.");
        Ensure(
            !currency.IsConfirmed
            && currency.ReasonCode == "BILLING_V2_REFUND_CURRENCY_MISMATCH",
            "Une devise provider differente bloque refunded.");
    }

    private static void VerifyProviderConfirmationIsIdempotentForWebhookAndReconciliation()
    {
        var first = BillingV2RefundConfirmationPolicy.Evaluate(Source(), Observation());
        var duplicateWebhook = BillingV2RefundConfirmationPolicy.Evaluate(
            Source(), Observation());
        var repeatedReconciliation = BillingV2RefundConfirmationPolicy.Evaluate(
            Source(), Observation());
        Ensure(
            first.IsConfirmed && duplicateWebhook.IsConfirmed
            && repeatedReconciliation.IsConfirmed,
            "Webhook double et reconciliation repetee doivent converger sans creer une seconde autorite.");
    }

    private static void VerifyConfirmedRefundBlocksRenewalAndQueuesProviderCancellation()
    {
        var recurring = BillingV2RefundSubscriptionCompensationPolicy.Evaluate(Source());
        var oneTime = BillingV2RefundSubscriptionCompensationPolicy.Evaluate(
            Source() with { HasRecurringComponent = false, ProviderSubscriptionId = null });
        var unresolved = BillingV2RefundSubscriptionCompensationPolicy.Evaluate(
            Source() with { ProviderSubscriptionId = null });

        Ensure(
            recurring.IsValid && recurring.BlockLocalRenewal
            && recurring.QueueProviderCancellation,
            "Un refund confirme d'un abonnement bloque le renewal et annule le contrat provider.");
        Ensure(
            oneTime.IsValid && oneTime.BlockLocalRenewal
            && !oneTime.QueueProviderCancellation,
            "Un produit sans recurrence ne doit pas inventer une annulation provider.");
        Ensure(
            !unresolved.IsValid
            && unresolved.ReasonCode == "BILLING_V2_REFUND_RECURRING_SUBSCRIPTION_UNRESOLVED",
            "Un abonnement recurrent sans ancre provider echoue en ferme.");
    }

    private static void VerifyExecutionIsFailClosed()
    {
        var gate = BillingV2RefundExecutionGate.Evaluate(
            new BillingV2RuntimeConfiguration(
                false, false, false, false, true, false, RefundsEnabled: false),
            persistentSqlAvailable: true,
            stripeGatewayAvailable: true);
        Ensure(
            !gate.IsValid && gate.ReasonCode == "BILLING_V2_REFUND_FLAG_OFF",
            "Absent ou false doit desactiver toute execution refund.");
    }

    private static void VerifyRuntimeFlagIsFailClosed()
    {
        var absent = BillingV2RuntimeConfiguration.Resolve(
            new ConfigurationBuilder().Build());
        var falseValue = BillingV2RuntimeConfiguration.Resolve(
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["BILLING_V2_REFUNDS_ENABLED"] = "false"
                }).Build());
        var trueValue = BillingV2RuntimeConfiguration.Resolve(
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["BILLING_V2_REFUNDS_ENABLED"] = "true"
                }).Build());
        Ensure(
            !absent.RefundsEnabled && !falseValue.RefundsEnabled
            && trueValue.RefundsEnabled,
            "Le flag refunds doit rester fail-closed : absent/false OFF, true ON.");
    }

    private static void VerifyExecutionRequiresProviderOutbox()
    {
        var gate = BillingV2RefundExecutionGate.Evaluate(
            new BillingV2RuntimeConfiguration(
                false, false, false, false, true, false, RefundsEnabled: true),
            persistentSqlAvailable: true,
            stripeGatewayAvailable: true);
        Ensure(
            !gate.IsValid
            && gate.ReasonCode == "BILLING_V2_REFUND_PROVIDER_OUTBOX_OFF",
            "Une demande refund ne doit pas etre acceptee sans outbox executable.");
    }

    private static void VerifyReadinessSeparatesEvidenceFromActivation()
    {
        var incomplete = BillingV2LifecycleReadinessGate.Evaluate(
            new BillingV2LifecycleReadinessInputs(
                PersistentSqlAvailable: true,
                FinancialCoreSchemaReady: true,
                RenewalSchemaReady: true,
                AuthoritativeCheckoutEnabled: true,
                ProviderExecutorEnabled: true,
                StripeConfigured: true,
                StripePriceMappingsReady: true,
                ReconciliationWorkerActivatable: true,
                DocumentIssuanceReady: true,
                BpceInvoiceLookupSupported: false,
                ProvisioningEnabled: false,
                PayPalConfigured: false,
                RefundSchemaReady: false,
                RefundStripeOperational: false,
                RefundDocumentCorrectionOperational: false));
        var fullyProved = BillingV2LifecycleReadinessGate.Evaluate(
            new BillingV2LifecycleReadinessInputs(
                PersistentSqlAvailable: true,
                FinancialCoreSchemaReady: true,
                RenewalSchemaReady: true,
                AuthoritativeCheckoutEnabled: true,
                ProviderExecutorEnabled: true,
                StripeConfigured: true,
                StripePriceMappingsReady: true,
                ReconciliationWorkerActivatable: true,
                DocumentIssuanceReady: true,
                BpceInvoiceLookupSupported: true,
                ProvisioningEnabled: false,
                PayPalConfigured: false,
                RefundSchemaReady: true,
                RefundStripeOperational: true,
                RefundDocumentCorrectionOperational: true));

        Ensure(
            incomplete.Single(component => component.Component
                == BillingV2ReadinessComponents.RefundCoreCode).State
                == BillingV2ReadinessStates.Ready,
            "Le code refund present doit etre distingue des preuves externes.");
        Ensure(
            incomplete.Single(component => component.Component
                == BillingV2ReadinessComponents.RefundSchema).State
                == BillingV2ReadinessStates.NotReady
            && incomplete.Single(component => component.Component
                == BillingV2ReadinessComponents.RefundStripe).State
                == BillingV2ReadinessStates.NotReady
            && incomplete.Single(component => component.Component
                == BillingV2ReadinessComponents.RefundDocumentCorrection).State
                == BillingV2ReadinessStates.NotReady
            && incomplete.Single(component => component.Component
                == BillingV2ReadinessComponents.Refunds).State
                == BillingV2ReadinessStates.NotReady,
            "Aucune preuve manquante ne doit etre maquillee en refund activable.");
        Ensure(
            fullyProved.Single(component => component.Component
                == BillingV2ReadinessComponents.Refunds).State
                == BillingV2ReadinessStates.Ready,
            "La capacite refund ne devient READY que lorsque schema, Stripe et document sont tous prouves.");
    }

    private static async Task VerifyStripeRefundRequestIsIdempotentAndAuthoritativeAsync()
    {
        var handler = new RefundHttpHandler((request, body) =>
        {
            Ensure(request.Method == HttpMethod.Post
                && request.RequestUri!.AbsolutePath == "/v1/refunds",
                "Le rail existant Stripe doit recevoir la commande de refund.");
            Ensure(body.Contains("payment_intent=pi_123", StringComparison.Ordinal)
                && body.Contains("amount=2290", StringComparison.Ordinal)
                && body.Contains(
                    "metadata%5Bbilling_v2_refund_id%5D=refund-123",
                    StringComparison.Ordinal),
                "Stripe ne recoit que le PaymentIntent persiste, le montant authoritative et le refund id.");
            Ensure(request.Headers.TryGetValues("Idempotency-Key", out var keys)
                && keys.Single() == "billing-v2-refund|full|event-123",
                "Le retry Stripe doit reutiliser une cle idempotente stable.");
        },
        """{"id":"re_123","status":"pending","amount":2290,"currency":"eur","payment_intent":"pi_123","metadata":{"billing_v2_refund_id":"refund-123"}}""");
        var gateway = Gateway(handler);
        var result = await gateway.CreateRefundAsync(new BillingV2StripeRefundCreateRequest(
            "pi_123", "EUR", 2_290, "billing-v2-refund|full|event-123", "refund-123"),
            CancellationToken.None);
        Ensure(
            result.Succeeded && result.Refund?.RefundId == "re_123"
            && result.Refund.AmountCents == 2_290,
            "Le rail parse la reponse provider sans autorite montant navigateur.");
        Ensure(handler.RequestCount == 1, "Une demande conduit a un seul POST Stripe.");
    }

    private static async Task VerifyAmbiguousProviderTimeoutStaysIndeterminateAsync()
    {
        var gateway = Gateway(new RefundHttpHandler(
            (_, _) => throw new HttpRequestException("simulated timeout"),
            responseBody: null));
        var result = await gateway.CreateRefundAsync(new BillingV2StripeRefundCreateRequest(
            "pi_123", "EUR", 2_290, "stable-key", "refund-123"), CancellationToken.None);
        Ensure(
            !result.Succeeded && result.Retryable
            && result.ReasonCode == "BILLING_V2_STRIPE_REFUND_CALL_INDETERMINATE",
            "Une reponse perdue reste ambigue : le worker doit relire Stripe avant tout retry.");
    }

    private static async Task VerifyBoundedProviderReconciliationFindsOnlyOurRefundAsync()
    {
        var body = """
            {"data":[
              {"id":"re_other","status":"succeeded","amount":2290,"currency":"eur","payment_intent":"pi_123","metadata":{"billing_v2_refund_id":"other"}},
              {"id":"re_123","status":"succeeded","amount":2290,"currency":"eur","payment_intent":"pi_123","metadata":{"billing_v2_refund_id":"refund-123"}}
            ]}
            """;
        var handler = new RefundHttpHandler((request, _) =>
        {
            Ensure(request.Method == HttpMethod.Get
                && request.RequestUri!.Query.Contains("payment_intent=pi_123", StringComparison.Ordinal),
                "La reprise est bornee au PaymentIntent persiste.");
        }, body);
        var found = await Gateway(handler).FindRefundAsync(
            new BillingV2StripeRefundLocator(null, "pi_123", "refund-123"),
            CancellationToken.None);
        Ensure(
            found?.RefundId == "re_123" && handler.RequestCount == 1,
            "La reconciliation ne choisit pas le refund d'un autre workflow.");
    }

    private static BillingV2StripeGateway Gateway(HttpMessageHandler handler)
        => new(
            new BillingV2RuntimeConfiguration(
                false, false, false, true, true, false, RefundsEnabled: true),
            new StripeRuntimeConfiguration(StripeMode.Test, "sk_test_refund_core"),
            new RefundHttpClientFactory(new HttpClient(handler)),
            NullLogger<BillingV2StripeGateway>.Instance);

    // --- Couverture ajoutee : branches de refus jamais exercees ------------
    //
    // Chaque test ci-dessous porte sur un `ReasonCode` que les politiques
    // peuvent produire et qu'aucune assertion ne verifiait. Sans elles, une
    // regression qui transforme un refus en autorisation passe silencieusement.

    /// <summary>Objet inexistant : aucun BillingEvent derriere la demande.</summary>
    private static void VerifyMissingBillingEventIsRefused()
    {
        var decision = BillingV2RefundPolicy.EvaluateFullRequest(null);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_REFUND_BILLING_EVENT_NOT_FOUND"
            && decision.AmountCents == 0
            && decision.Currency is null,
            "Une demande sans BillingEvent doit etre refusee sans montant ni devise.");
    }

    /// <summary>
    /// Donnees invalides : un montant nul, negatif ou une devise vide ne
    /// doivent jamais produire un ordre de remboursement.
    /// </summary>
    private static void VerifyInvalidSourceAmountIsRefused()
    {
        foreach (var broken in new[]
        {
            Source() with { TotalAmountCents = 0 },
            Source() with { TotalAmountCents = -1 },
            Source() with { Currency = "" },
            Source() with { Currency = "   " },
        })
        {
            var decision = BillingV2RefundPolicy.EvaluateFullRequest(broken);
            Ensure(
                !decision.IsValid
                && decision.ReasonCode == "BILLING_V2_REFUND_SOURCE_AMOUNT_INVALID",
                "Un montant ou une devise invalide doit bloquer la demande, "
                + $"or {broken.TotalAmountCents} / « {broken.Currency} » a produit "
                + decision.ReasonCode + ".");
        }
    }

    /// <summary>
    /// Le remboursement vise un paiement provider precis. Chacune des quatre
    /// coordonnees manquantes doit fermer la demande, y compris un provider
    /// autre que Stripe : rien d'autre n'est executable aujourd'hui.
    /// </summary>
    private static void VerifyUnresolvedProviderPaymentIsRefused()
    {
        foreach (var broken in new[]
        {
            Source() with { PaymentAttemptId = null },
            Source() with { PaymentAttemptId = "  " },
            Source() with { Provider = "paypal" },
            Source() with { Provider = null },
            Source() with { Environment = null },
            Source() with { ProviderPaymentId = null },
        })
        {
            var decision = BillingV2RefundPolicy.EvaluateFullRequest(broken);
            Ensure(
                !decision.IsValid
                && decision.ReasonCode
                    == "BILLING_V2_REFUND_PROVIDER_PAYMENT_UNRESOLVED",
                "Une coordonnee provider manquante doit fermer la demande, "
                + $"or le provider « {broken.Provider} » a produit "
                + decision.ReasonCode + ".");
        }
    }

    /// <summary>
    /// Un abonnement recurrent sans ancre provider est deja refuse a la
    /// compensation ; il doit l'etre des la demande, avant tout appel sortant.
    /// </summary>
    private static void VerifyRecurringWithoutProviderAnchorIsRefusedAtRequest()
    {
        var decision = BillingV2RefundPolicy.EvaluateFullRequest(
            Source() with { ProviderSubscriptionId = null });
        Ensure(
            !decision.IsValid
            && decision.ReasonCode
                == "BILLING_V2_REFUND_RECURRING_SUBSCRIPTION_UNRESOLVED",
            "Un abonnement recurrent sans ancre provider doit etre refuse avant dispatch.");
    }

    /// <summary>
    /// Double remboursement. Une source deja passee a `refunded` ne doit pas
    /// pouvoir etre confirmee une seconde fois : c'est la relecture qui
    /// protege, pas la memoire d'un webhook.
    /// </summary>
    private static void VerifySecondRefundOnRefundedSourceIsRefused()
    {
        var replay = BillingV2RefundConfirmationPolicy.Evaluate(
            Source() with { SettlementStatus = BillingV2SettlementStatuses.Refunded },
            Observation());
        Ensure(
            !replay.IsConfirmed && !replay.IsFailed
            && replay.ReasonCode == "BILLING_V2_REFUND_SOURCE_NO_LONGER_SETTLED",
            "Une source deja remboursee ne doit pas etre confirmee une seconde fois.");

        // Meme demande, meme cle : un second POST converge sur le refund
        // existant plutot que d'en creer un deuxieme chez le provider.
        var replayDecision = BillingV2RefundPolicy.EvaluateFullRequest(
            Source() with { SettlementStatus = BillingV2SettlementStatuses.Refunded });
        Ensure(
            !replayDecision.IsValid
            && replayDecision.ReasonCode == "BILLING_V2_REFUND_PAYMENT_NOT_SETTLED",
            "Une seconde demande sur une charge remboursee doit etre refusee.");
    }

    /// <summary>
    /// Sans refund identifie chez le provider, rien n'est confirme : ni un
    /// POST reussi, ni un webhook sans identifiant.
    /// </summary>
    private static void VerifyUnobservedProviderRefundIsRefused()
    {
        foreach (var observation in new BillingV2RefundProviderObservation?[]
        {
            null,
            new(null, "succeeded", 2_290, "EUR", "pi_123"),
            new("   ", "succeeded", 2_290, "EUR", "pi_123"),
        })
        {
            var decision = BillingV2RefundConfirmationPolicy.Evaluate(
                Source(), observation);
            Ensure(
                !decision.IsConfirmed && !decision.IsFailed
                && decision.ReasonCode == "BILLING_V2_REFUND_PROVIDER_NOT_OBSERVED",
                "Sans identifiant de refund provider, aucune confirmation.");
        }
    }

    /// <summary>
    /// Preuve provider strictement liee au paiement d'origine : un refund
    /// reussi qui porte sur un AUTRE paiement ne doit jamais solder celui-ci.
    /// </summary>
    private static void VerifyRefundOnAnotherPaymentIsRefused()
    {
        var decision = BillingV2RefundConfirmationPolicy.Evaluate(
            Source(),
            new BillingV2RefundProviderObservation(
                "re_999", "succeeded", 2_290, "EUR", "pi_someone_else"));
        Ensure(
            !decision.IsConfirmed && !decision.IsFailed
            && decision.ReasonCode == "BILLING_V2_REFUND_PROVIDER_PAYMENT_MISMATCH",
            "Un refund observe sur un autre paiement ne doit pas confirmer celui-ci.");
    }

    /// <summary>`canceled` est un echec, au meme titre que `failed`.</summary>
    private static void VerifyCanceledProviderRefundIsAFailure()
    {
        var decision = BillingV2RefundConfirmationPolicy.Evaluate(
            Source(), Observation(status: "canceled"));
        Ensure(
            !decision.IsConfirmed && decision.IsFailed
            && decision.ReasonCode == "BILLING_V2_REFUND_PROVIDER_FAILED",
            "Un refund provider annule doit etre traite comme un echec, pas comme un pending.");

        // La casse vient du provider : elle ne doit pas changer la decision.
        var upperCase = BillingV2RefundConfirmationPolicy.Evaluate(
            Source(), Observation(status: "SUCCEEDED"));
        Ensure(
            upperCase.IsConfirmed,
            "Le statut provider doit etre compare sans tenir compte de la casse.");
    }

    /// <summary>Compensation impossible sans abonnement resolu.</summary>
    private static void VerifyMissingSubscriptionBlocksCompensation()
    {
        foreach (var broken in new[]
        {
            Source() with { SubscriptionId = "" },
            Source() with { SubscriptionId = "   " },
        })
        {
            var decision =
                BillingV2RefundSubscriptionCompensationPolicy.Evaluate(broken);
            Ensure(
                !decision.IsValid
                && !decision.BlockLocalRenewal
                && !decision.QueueProviderCancellation
                && decision.ReasonCode == "BILLING_V2_REFUND_SUBSCRIPTION_UNRESOLVED",
                "Sans abonnement resolu, aucune compensation ne doit etre decidee.");
        }
    }

    /// <summary>
    /// Les deux dernieres barrieres du portillon d'execution, jamais
    /// exercees : persistance et joignabilite Stripe.
    /// </summary>
    private static void VerifyExecutionRequiresSqlAndStripeGateway()
    {
        var enabled = new BillingV2RuntimeConfiguration(
            false, false, false, ProviderOutboxEnabled: true, true, false,
            RefundsEnabled: true);

        var withoutSql = BillingV2RefundExecutionGate.Evaluate(
            enabled, persistentSqlAvailable: false, stripeGatewayAvailable: true);
        Ensure(
            !withoutSql.IsValid
            && withoutSql.ReasonCode == "BILLING_V2_REFUND_NO_PERSISTENT_SQL",
            "Sans persistance reelle, aucun refund ne doit etre execute.");

        var withoutStripe = BillingV2RefundExecutionGate.Evaluate(
            enabled, persistentSqlAvailable: true, stripeGatewayAvailable: false);
        Ensure(
            !withoutStripe.IsValid
            && withoutStripe.ReasonCode
                == "BILLING_V2_REFUND_STRIPE_GATEWAY_UNAVAILABLE",
            "Sans passerelle Stripe joignable, aucun refund ne doit etre execute.");

        var ready = BillingV2RefundExecutionGate.Evaluate(
            enabled, persistentSqlAvailable: true, stripeGatewayAvailable: true);
        Ensure(
            ready.IsValid && ready.ReasonCode == "BILLING_V2_REFUND_READY",
            "Toutes conditions reunies, le portillon doit autoriser l'execution.");
    }

    /// <summary>
    /// La charge utile d'outbox fait l'aller-retour sans perte, et une charge
    /// illisible echoue explicitement plutot que de produire un ordre vide.
    /// </summary>
    private static void VerifyOutboxPayloadRoundTripAndRejection()
    {
        var payload = new BillingV2RefundOutboxPayload(
            "refund-1", "event-123", "stripe", "test", "pi_123");
        var parsed = BillingV2RefundOutbox.Parse(
            BillingV2RefundOutbox.Serialize(payload));
        Ensure(
            parsed == payload,
            "La charge utile d'outbox doit survivre a l'aller-retour JSON.");

        var rejected = false;
        try
        {
            BillingV2RefundOutbox.Parse("null");
        }
        catch (InvalidOperationException exception)
        {
            rejected = exception.Message.Contains(
                "BILLING_V2_REFUND_OUTBOX_PAYLOAD_INVALID",
                StringComparison.Ordinal);
        }

        Ensure(
            rejected,
            "Une charge utile d'outbox illisible doit echouer explicitement.");
    }

    /// <summary>
    /// Le remboursement partiel n'existe pas en V2.1 : la politique n'expose
    /// qu'une demande integrale, et la cle d'idempotence porte `full` dans son
    /// texte canonique.
    /// </summary>
    /// <remarks>
    /// Ce test protege une non-fonctionnalite. Le jour ou un remboursement
    /// partiel sera introduit, la cle devra distinguer le montant : sans cela
    /// un partiel et un integral sur le meme BillingEvent partageraient la
    /// meme cle d'idempotence Stripe, et le second serait silencieusement
    /// resolu par le premier — donc un client non rembourse de la difference.
    /// </remarks>
    private static void VerifyPartialRefundIsNotSilentlyIntroduced()
    {
        var entryPoints = typeof(BillingV2RefundPolicy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => method.Name)
            .ToArray();
        Ensure(
            entryPoints.Length == 1 && entryPoints[0] == "EvaluateFullRequest",
            "La politique de remboursement ne doit exposer qu'une demande "
            + $"integrale (trouve : {string.Join(", ", entryPoints)}).");

        Ensure(
            BillingV2RefundOutbox.CanonicalIdempotencyKey("event-123")
                == "billing-v2-refund|full|event-123",
            "La cle d'idempotence doit porter la portee du remboursement : "
            + "un partiel introduit plus tard ne doit pas la partager.");
    }

    private static BillingV2RefundSourceSnapshot Source()
        => new(
            "event-123", "subscription-123", BillingV2SettlementStatuses.Settled,
            BillingV2EventDocumentStatuses.None, 2_290, "eur", "attempt-123",
            "stripe", "test", "pi_123", HasRecurringComponent: true,
            ProviderSubscriptionId: "sub_123");

    private static BillingV2RefundProviderObservation Observation(
        string status = "succeeded", long amountCents = 2_290, string currency = "EUR")
        => new("re_123", status, amountCents, currency, "pi_123");

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class RefundHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RefundHttpHandler(
        Action<HttpRequestMessage, string> assertion,
        string? responseBody) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            assertion(request, body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody ?? string.Empty)
            };
        }
    }
}
