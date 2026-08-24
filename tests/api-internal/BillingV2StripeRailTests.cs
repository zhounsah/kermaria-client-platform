using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Rail Stripe Billing V2 - Phase 2, tests purs.
///
/// Couvrent les scenarios d'idempotence E2E qui ne demandent pas de base :
/// determinisme de la cle provider, montant Stripe issu du BillingEvent,
/// verification de settlement, et refus d'activation sur signal non confirme.
/// </summary>
public static class BillingV2StripeRailTests
{
    public static Task RunAsync()
    {
        // A. Intention serveur
        VerifyIntentKeyIsStableForSameSelection();
        VerifyIntentKeyChangesOnDeliberateNewChoice();
        VerifyIntentKeyRequiresClientRequestId();

        // B. BillingEvent obligatoire
        VerifyDispatchRefusedWithoutBillingEvent();
        VerifyDispatchRefusedWhenEventNotFinalized();
        VerifyDispatchRefusedWithoutLines();
        VerifyDispatchRefusedOnInvalidCurrency();
        VerifyDispatchRefusedWhenAlreadySettled();
        VerifyDispatchRefusedWhenTaxIsPresent();

        // C. PaymentAttempt : scenario 5
        VerifyRetryReusesSameProviderRequestKey();

        // D. Montant Stripe : scenarios 10 et 11
        VerifyMonthlyStripeAmountEqualsLocalAmount();
        VerifyUpfrontProducesSingleChargeWithoutRecurrence();
        VerifyPureOneTimeSelectionUsesPaymentMode();
        VerifySetupFeeIsASeparateOneShotLine();
        VerifyStripeNeverReceivesAnExternalPriceId();
        VerifyKnownProviderCustomerReplacesEmail();
        VerifyAbsentProviderCustomerKeepsEmail();
        VerifyProviderCustomerIsStripeOnly();
        VerifyApprovalUrlReplayReturnsPersistedUrl();
        VerifyApprovalUrlReplayFallsBackToRefetchedUrl();
        VerifyApprovalUrlReplayFailsClosedWhenUnrecoverable();
        VerifyStripeIsReadyWithoutProviderPriceMappings();
        VerifyPayPalStillRequiresProviderPriceMappings();
        VerifyProviderEnvironmentMatrixRefusesImpossibleCouples();

        // E. Settlement verifie : scenarios 6, 7, 8
        VerifyAmountMismatchBlocksActivation();
        VerifyCurrencyMismatchBlocksActivation();
        VerifyCompletedSessionWithoutPaymentBlocksActivation();
        VerifyForeignSessionBlocksActivation();
        VerifyExactSettlementIsConfirmed();
        VerifyMissingSessionStaysPending();

        // F. Safety
        VerifyCheckoutSessionCompletedNoLongerActivates();
        VerifySubscriptionCreatedAndUpdatedStayInert();
        VerifyOnlyStripeSignalsTriggerVerification();

        // Ventilation deterministe
        VerifyDiscountAllocationIsExactAndDeterministic();

        // Construction de l'evenement financier
        VerifyMonthlyBillingEventMatchesPricingEngine();
        VerifyUpfrontBillingEventMatchesPricingEngine();

        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------
    // A. Intention serveur
    // ------------------------------------------------------------------

    private static void VerifyIntentKeyIsStableForSameSelection()
    {
        var first = BillingV2SubscriptionIntentKey.Canonical(Intent("req-1"));
        var second = BillingV2SubscriptionIntentKey.Canonical(Intent("req-1"));
        Ensure(
            string.Equals(first, second, StringComparison.Ordinal)
            && BillingV2SubscriptionIntentKey.Hash(first)
                == BillingV2SubscriptionIntentKey.Hash(second),
            "Scenario 1 : le meme client_request_id doit donner la meme ancre.");
    }

    private static void VerifyIntentKeyChangesOnDeliberateNewChoice()
    {
        var baseline = BillingV2SubscriptionIntentKey.Canonical(Intent("req-1"));
        var otherSelection = BillingV2SubscriptionIntentKey.Canonical(
            Intent("req-1") with { SelectionFingerprint = "fingerprint-autre" });
        var otherRail = BillingV2SubscriptionIntentKey.Canonical(
            Intent("req-1") with { Provider = "paypal" });
        Ensure(
            !string.Equals(baseline, otherSelection, StringComparison.Ordinal)
            && !string.Equals(baseline, otherRail, StringComparison.Ordinal),
            "Un choix volontairement different doit creer une intention distincte.");
    }

    private static void VerifyIntentKeyRequiresClientRequestId()
    {
        var threw = false;
        try
        {
            BillingV2SubscriptionIntentKey.Canonical(Intent("   "));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Ensure(threw, "Aucune ancre ne doit etre inventee sans client_request_id.");
    }

    // ------------------------------------------------------------------
    // B. BillingEvent obligatoire
    // ------------------------------------------------------------------

    private static void VerifyDispatchRefusedWithoutBillingEvent()
        => EnsureRefused(
            BillingV2StripeDispatchGuard.Evaluate(null),
            "BILLING_V2_STRIPE_DISPATCH_WITHOUT_BILLING_EVENT",
            "Aucun checkout ne part sans BillingEvent.");

    private static void VerifyDispatchRefusedWhenEventNotFinalized()
        => EnsureRefused(
            BillingV2StripeDispatchGuard.Evaluate(
                Event() with { FinancialStatus = BillingV2FinancialStatuses.Draft }),
            "BILLING_V2_STRIPE_DISPATCH_EVENT_NOT_FINALIZED",
            "Un evenement brouillon ne doit pas partir chez Stripe.");

    private static void VerifyDispatchRefusedWithoutLines()
        => EnsureRefused(
            BillingV2StripeDispatchGuard.Evaluate(Event() with { LineCount = 0 }),
            "BILLING_V2_STRIPE_DISPATCH_EVENT_HAS_NO_LINES",
            "Un evenement sans ligne ne doit pas partir chez Stripe.");

    private static void VerifyDispatchRefusedOnInvalidCurrency()
        => EnsureRefused(
            BillingV2StripeDispatchGuard.Evaluate(Event() with { Currency = "  " }),
            "BILLING_V2_STRIPE_DISPATCH_CURRENCY_INVALID",
            "Une devise vide doit bloquer le dispatch.");

    private static void VerifyDispatchRefusedWhenAlreadySettled()
        => EnsureRefused(
            BillingV2StripeDispatchGuard.Evaluate(
                Event() with
                {
                    SettlementStatus = BillingV2SettlementStatuses.Settled
                }),
            "BILLING_V2_STRIPE_DISPATCH_ALREADY_SETTLED",
            "Un evenement deja encaisse ne doit pas repartir chez Stripe.");

    private static void VerifyDispatchRefusedWhenTaxIsPresent()
        => EnsureRefused(
            BillingV2StripeDispatchGuard.Evaluate(
                Event() with { TaxAmountCents = 200 }),
            "BILLING_V2_STRIPE_DISPATCH_TAX_NOT_SUPPORTED",
            "Une TVA non nulle doit echouer en ferme sur le rail Phase 2.");

    // ------------------------------------------------------------------
    // C. Scenario 5 : timeout puis retry
    // ------------------------------------------------------------------

    private static void VerifyRetryReusesSameProviderRequestKey()
    {
        const string eventId = "11111111-2222-3333-4444-555555555555";
        var first = BillingV2FinancialCoreStore.BuildProviderRequestKey(eventId);
        var second = BillingV2FinancialCoreStore.BuildProviderRequestKey(eventId);
        Ensure(
            string.Equals(first, second, StringComparison.Ordinal),
            "Scenario 5 : la cle provider est derivee de l'evenement, donc stable.");

        var attempt = new BillingV2PaymentAttemptSnapshot(
            "attempt-1",
            "stripe",
            "test",
            first,
            BillingV2PaymentAttemptStatuses.InFlight);
        var decision = BillingV2PaymentAttemptPolicy.EvaluateProviderCall(
            attempt,
            "stripe",
            "test");
        Ensure(
            decision.CanCall && decision.ProviderRequestKey == first,
            "Scenario 5 : apres timeout, le retry reprend la meme tentative.");

        var request = BillingV2StripeCheckoutRequestFactory.Build(
            Event(),
            "attempt-1",
            first,
            "client@example.invalid",
            "https://portal.invalid/ok",
            "https://portal.invalid/ko");
        Ensure(
            request.IdempotencyKey == first,
            "Scenario 5 : la cle persistee est bien celle envoyee a Stripe.");
    }

    // ------------------------------------------------------------------
    // D. Scenarios 10 et 11 : le montant vient du BillingEvent
    // ------------------------------------------------------------------

    private static void VerifyMonthlyStripeAmountEqualsLocalAmount()
    {
        var billingEvent = Event() with
        {
            PaymentModeSnapshot = BillingV2PaymentModes.Monthly,
            RecurringAmountCents = 4046,
            OneTimeAmountCents = 0,
            TotalAmountCents = 4046
        };
        var request = Build(billingEvent);

        Ensure(
            request.Mode == BillingV2StripeModes.Subscription,
            "Scenario 11 : le mensuel utilise mode=subscription.");
        Ensure(
            request.ExpectedAmountCents == 4046
            && request.Lines.Sum(line => line.UnitAmountCents) == 4046,
            "Scenario 11 : le montant Stripe egale exactement le montant local.");
        Ensure(
            request.Lines.Count == 1 && request.Lines[0].Recurring,
            "Scenario 11 : une seule ligne recurrente au MRR contractuel.");

        var parameters =
            BillingV2StripeCheckoutRequestFactory.ToFormParameters(request);
        Ensure(
            parameters["line_items[0][price_data][unit_amount]"] == "4046",
            "Le montant transmis est le MRR remise, pas un tarif catalogue.");
        Ensure(
            parameters["line_items[0][price_data][recurring][interval]"] == "month",
            "La ligne mensuelle doit porter une recurrence mensuelle.");
    }

    private static void VerifyUpfrontProducesSingleChargeWithoutRecurrence()
    {
        var billingEvent = Event() with
        {
            PaymentModeSnapshot = BillingV2PaymentModes.Upfront,
            CommitmentMonthsSnapshot = 12,
            RecurringAmountCents = 38400,
            OneTimeAmountCents = 0,
            TotalAmountCents = 38400
        };
        var request = Build(billingEvent);

        Ensure(
            request.Mode == BillingV2StripeModes.Payment,
            "Scenario 10 : le comptant utilise mode=payment.");
        Ensure(
            request.Lines.All(line => !line.Recurring),
            "Scenario 10 : aucune recurrence Stripe pour un paiement comptant.");
        Ensure(
            request.ExpectedAmountCents == 38400
            && request.Lines.Sum(line => line.UnitAmountCents) == 38400,
            "Scenario 10 : une seule charge du montant upfront exact.");

        var parameters =
            BillingV2StripeCheckoutRequestFactory.ToFormParameters(request);
        Ensure(
            parameters["mode"] == "payment"
            && !parameters.Keys.Any(key => key.Contains("recurring")),
            "Scenario 10 : aucune subscription mensuelle Stripe n'est creee.");
    }

    /// <summary>
    /// Achat purement ponctuel : mode de reglement mensuel au sens du contrat,
    /// mais aucune ligne recurrente.
    /// </summary>
    /// <remarks>
    /// Deduire le mode Stripe du seul `payment_mode` demandait ici une session
    /// `mode=subscription` sans aucun prix recurrent — que l'API Stripe refuse.
    /// Le mode doit suivre les lignes reellement construites.
    /// </remarks>
    private static void VerifyPureOneTimeSelectionUsesPaymentMode()
    {
        var request = Build(Event() with
        {
            PaymentModeSnapshot = BillingV2PaymentModes.Monthly,
            CommitmentMonthsSnapshot = 1,
            RecurringAmountCents = 0,
            OneTimeAmountCents = 19900,
            TotalAmountCents = 19900
        });

        Ensure(
            request.Mode == BillingV2StripeModes.Payment,
            "Une selection sans composante recurrente n'ouvre pas d'abonnement.");
        Ensure(
            request.Lines.Count == 1 && !request.Lines[0].Recurring,
            "Une seule ligne, ponctuelle.");
        Ensure(
            request.ExpectedAmountCents == 19900,
            "Le montant preleve reste celui du BillingEvent.");

        var parameters =
            BillingV2StripeCheckoutRequestFactory.ToFormParameters(request);
        Ensure(
            !parameters.Keys.Any(key => key.Contains("recurring")),
            "Aucune recurrence Stripe ne doit etre demandee.");
    }

    private static void VerifySetupFeeIsASeparateOneShotLine()
    {
        var request = Build(Event() with
        {
            PaymentModeSnapshot = BillingV2PaymentModes.Monthly,
            RecurringAmountCents = 4046,
            OneTimeAmountCents = 1290,
            TotalAmountCents = 5336
        });

        Ensure(request.Lines.Count == 2, "Frais de mise en service separes.");
        Ensure(
            request.Lines[0].Recurring && !request.Lines[1].Recurring,
            "La setup fee est one-shot, la part abonnement reste recurrente.");
        Ensure(
            request.Lines.Sum(line => line.UnitAmountCents) == 5336,
            "La somme des lignes Stripe egale le total du BillingEvent.");
    }

    // ------------------------------------------------------------------
    // Rattachement a un client Stripe deja connu (Phase 4, Test Clock)
    //
    // Une horloge de test ne peut s'attacher qu'a un client cree a l'avance.
    // `customer_email` fait creer le client par Stripe : il faut donc pouvoir
    // passer un `customer` deja connu. Rien n'est cree par ce chemin.
    // ------------------------------------------------------------------

    private static void VerifyKnownProviderCustomerReplacesEmail()
    {
        var parameters = BillingV2StripeCheckoutRequestFactory.ToFormParameters(
            Build(Event(), providerCustomerId: "cus_test_123"));

        Ensure(
            parameters.TryGetValue("customer", out var customer)
            && customer == "cus_test_123",
            "Un client connu doit etre transmis en `customer`.");
        Ensure(
            !parameters.ContainsKey("customer_email"),
            "Stripe refuse `customer` et `customer_email` ensemble : "
            + "l'email ne doit plus etre envoye.");
        // Le reste de la requete ne bouge pas : meme montant, meme mode.
        Ensure(
            parameters["mode"] == BillingV2StripeModes.Subscription
            && parameters.Keys.Any(key => key.Contains("price_data][unit_amount")),
            "Le rattachement client ne doit rien changer d'autre.");
    }

    private static void VerifyAbsentProviderCustomerKeepsEmail()
    {
        var parameters = BillingV2StripeCheckoutRequestFactory.ToFormParameters(
            Build(Event()));

        Ensure(
            parameters.TryGetValue("customer_email", out var email)
            && email == "client@example.invalid",
            "Sans client connu, le comportement d'avant doit etre conserve.");
        Ensure(
            !parameters.ContainsKey("customer"),
            "Aucun `customer` ne doit apparaitre sans identifiant persiste.");

        // Une valeur vide ou blanche n'est pas un identifiant : elle ne doit
        // pas basculer le comportement en silence.
        foreach (var blank in new[] { "", "   " })
        {
            var fallback = BillingV2StripeCheckoutRequestFactory.ToFormParameters(
                Build(Event(), providerCustomerId: blank));
            Ensure(
                fallback.ContainsKey("customer_email")
                && !fallback.ContainsKey("customer"),
                "Un identifiant vide doit retomber sur `customer_email`.");
        }
    }

    /// <summary>
    /// Replay d'un dispatch dont la session existe deja : l'URL persistee doit
    /// etre rendue telle quelle. Avant correction, la reprise renvoyait null et
    /// l'abonnement restait `pending_approval` sans moyen de payer.
    /// </summary>
    private static void VerifyApprovalUrlReplayReturnsPersistedUrl()
    {
        var recovery = BillingV2StripeApprovalUrlRecoveryPolicy.Resolve(
            "https://checkout.stripe.com/c/pay/cs_test_persisted",
            "https://checkout.stripe.com/c/pay/cs_test_refetched");

        Ensure(
            recovery.Recovered
            && recovery.ApprovalUrl
                == "https://checkout.stripe.com/c/pay/cs_test_persisted"
            && !recovery.RequiresManualReview
            && recovery.ReasonCode
                == "BILLING_V2_STRIPE_APPROVAL_URL_PERSISTED",
            "Un replay de dispatch doit rendre l'URL d'approbation deja persistee, sans recreer de session.");
    }

    /// <summary>
    /// Rien en base mais la session relue porte son `url` : on rend celle-ci.
    /// On ne cree jamais une seconde session pour obtenir une URL, ce serait un
    /// second encaissement possible.
    /// </summary>
    private static void VerifyApprovalUrlReplayFallsBackToRefetchedUrl()
    {
        var recovery = BillingV2StripeApprovalUrlRecoveryPolicy.Resolve(
            null,
            "https://checkout.stripe.com/c/pay/cs_test_refetched");
        var blank = BillingV2StripeApprovalUrlRecoveryPolicy.Resolve(
            "   ",
            "https://checkout.stripe.com/c/pay/cs_test_refetched");

        Ensure(
            recovery.Recovered
            && recovery.ApprovalUrl
                == "https://checkout.stripe.com/c/pay/cs_test_refetched"
            && recovery.ReasonCode
                == "BILLING_V2_STRIPE_APPROVAL_URL_REFETCHED"
            && blank.ApprovalUrl
                == "https://checkout.stripe.com/c/pay/cs_test_refetched",
            "Sans URL persistee, le replay doit rendre l'URL de la session relue.");
    }

    /// <summary>
    /// Aucune URL sure recuperable : echec ferme et revue manuelle, jamais une
    /// nouvelle session.
    /// </summary>
    private static void VerifyApprovalUrlReplayFailsClosedWhenUnrecoverable()
    {
        var recovery = BillingV2StripeApprovalUrlRecoveryPolicy.Resolve(
            null,
            null);

        Ensure(
            !recovery.Recovered
            && recovery.ApprovalUrl is null
            && recovery.RequiresManualReview
            && recovery.ReasonCode
                == "BILLING_V2_STRIPE_APPROVAL_URL_UNRECOVERABLE",
            "Sans URL recuperable, le replay doit echouer en ferme vers revue manuelle.");
    }

    /// <summary>
    /// Le rail Stripe V2 envoie `price_data` inline depuis le BillingEvent :
    /// aucun `price_…` Stripe n'est consomme. Un abonnement mensuel Stripe doit
    /// donc etre READY avec ZERO mapping provider. Exiger l'inverse bloquait le
    /// lancement sur une donnee que le rail n'utilise pas.
    /// </summary>
    private static void VerifyStripeIsReadyWithoutProviderPriceMappings()
    {
        var status = BillingV2ProviderPriceMappingGate.Evaluate(
            new[] { "price-base", "price-storage", "price-backup" },
            Array.Empty<BillingV2ProviderPriceMapping>(),
            "stripe",
            "live");

        Ensure(
            status.Ready
            && status.MissingServicePriceIds.Count == 0
            && status.AmbiguousServicePriceIds.Count == 0
            && status.ResolvedMappings.Count == 0
            && status.Provider == "stripe"
            && status.Environment == "live"
            && !BillingV2ProviderPricingAuthorityPolicy
                .RequiresProviderPriceMappings("stripe")
            && BillingV2ProviderPricingAuthorityPolicy.PricesInline("stripe"),
            "Stripe mensuel doit etre READY sans aucun mapping provider, le montant venant du BillingEvent.");
    }

    /// <summary>
    /// PayPal envoie un `plan_id` et ne sait pas tarifier en ligne : ses
    /// mappings restent exiges. L'assouplissement ne doit pas fuiter.
    /// </summary>
    /// <summary>
    /// Un environnement n'existe que rapporte a son fournisseur.
    /// </summary>
    /// <remarks>
    /// Le couple `stripe/sandbox` n'existe pas — Stripe appelle son bac a sable
    /// « test ». Le couple `paypal/test` n'existe pas non plus. Accepter l'un
    /// des deux enregistrerait un rattachement de prix que le rail ne
    /// retrouverait jamais : la commande echouerait au paiement, en production,
    /// sans qu'aucun controle back-office ne l'ait signale.
    /// </remarks>
    private static void VerifyProviderEnvironmentMatrixRefusesImpossibleCouples()
    {
        Ensure(
            BillingV2ProviderEnvironmentPolicy.IsSupported("stripe", "test")
            && BillingV2ProviderEnvironmentPolicy.IsSupported("stripe", "live"),
            "Stripe accepte `test` et `live`.");
        Ensure(
            !BillingV2ProviderEnvironmentPolicy.IsSupported("stripe", "sandbox"),
            "Stripe n'a pas de `sandbox` : le couple doit etre refuse.");

        Ensure(
            BillingV2ProviderEnvironmentPolicy.IsSupported("paypal", "sandbox")
            && BillingV2ProviderEnvironmentPolicy.IsSupported("paypal", "live"),
            "PayPal accepte `sandbox` et `live`.");
        Ensure(
            !BillingV2ProviderEnvironmentPolicy.IsSupported("paypal", "test"),
            "PayPal n'a pas de `test` : le couple doit etre refuse.");

        Ensure(
            !BillingV2ProviderEnvironmentPolicy.IsSupported("adyen", "live"),
            "Un fournisseur inconnu est refuse quel que soit l'environnement.");
        Ensure(
            BillingV2ProviderEnvironmentPolicy.EnvironmentsFor("adyen") is null,
            "Fournisseur inconnu et environnement invalide restent deux causes "
            + "distinctes.");
        Ensure(
            BillingV2ProviderEnvironmentPolicy.IsSupported("  Stripe ", "LIVE"),
            "La casse et les espaces ne doivent pas faire refuser un couple "
            + "valide saisi depuis un formulaire.");

        Ensure(
            BillingV2ProviderEnvironmentPolicy.Providers.Count == 2,
            "Ouvrir un troisieme fournisseur doit etre un choix explicite.");
    }

    private static void VerifyPayPalStillRequiresProviderPriceMappings()
    {
        var status = BillingV2ProviderPriceMappingGate.Evaluate(
            new[] { "price-base" },
            Array.Empty<BillingV2ProviderPriceMapping>(),
            "paypal",
            "live");

        Ensure(
            !status.Ready
            && status.MissingServicePriceIds.Count == 1
            && status.MissingServicePriceIds[0] == "price-base"
            && BillingV2ProviderPricingAuthorityPolicy
                .RequiresProviderPriceMappings("paypal"),
            "PayPal doit continuer a exiger ses mappings provider.");
    }

    private static void VerifyProviderCustomerIsStripeOnly()
    {
        // Le champ vit dans la requete Stripe et nulle part ailleurs : aucun
        // autre rail ne le lit, donc PayPal ne peut pas etre affecte. Et de
        // toute facon PayPal est refuse en amont par le perimetre gele.
        Ensure(
            !BillingV2LaunchScope.EvaluateCheckout(
                "paypal", BillingV2PaymentModes.Monthly, 0).IsValid,
            "PayPal reste refuse : ce changement ne l'atteint pas.");

        var request = Build(Event(), providerCustomerId: "cus_test_123");
        Ensure(
            request.ProviderCustomerId == "cus_test_123",
            "L'identifiant client ne vit que dans la requete Stripe.");
    }

    private static void VerifyStripeNeverReceivesAnExternalPriceId()
    {
        var parameters = BillingV2StripeCheckoutRequestFactory.ToFormParameters(
            Build(Event()));
        Ensure(
            parameters.Keys.All(key => !key.EndsWith("[price]", StringComparison.Ordinal)),
            "Aucun price_id externe ne doit determiner le total contractuel.");
        Ensure(
            parameters.Keys.Any(key => key.Contains("price_data][unit_amount")),
            "Le montant doit etre transmis explicitement en price_data.");
        Ensure(
            parameters["metadata[billing_v2_billing_event_id]"] == "event-1",
            "La session doit porter l'evenement financier en metadata.");
    }

    // ------------------------------------------------------------------
    // E. Scenarios 6, 7, 8 : settlement verifie
    // ------------------------------------------------------------------

    private static void VerifyAmountMismatchBlocksActivation()
    {
        var result = BillingV2StripeSettlementVerifier.Verify(
            Session(amount: 1990),
            Expectation());
        Ensure(
            !result.Settled
            && result.SettlementStatus == BillingV2SettlementStatuses.AmountMismatch
            && result.AttemptStatus == BillingV2PaymentAttemptStatuses.AmountMismatch,
            "Scenario 6 : un montant different ne doit jamais activer.");
    }

    private static void VerifyCurrencyMismatchBlocksActivation()
    {
        var result = BillingV2StripeSettlementVerifier.Verify(
            Session(currency: "usd"),
            Expectation());
        Ensure(
            !result.Settled
            && result.SettlementStatus == BillingV2SettlementStatuses.AmountMismatch,
            "Scenario 7 : une devise differente ne doit jamais activer.");
    }

    private static void VerifyCompletedSessionWithoutPaymentBlocksActivation()
    {
        var result = BillingV2StripeSettlementVerifier.Verify(
            Session(paymentStatus: "unpaid", sessionStatus: "complete"),
            Expectation());
        Ensure(
            !result.Settled
            && result.ReasonCode == "BILLING_V2_STRIPE_PAYMENT_NOT_CONFIRMED"
            && result.SettlementStatus == BillingV2SettlementStatuses.Pending,
            "Scenario 8 : session complete mais non payee => aucune activation.");
    }

    private static void VerifyForeignSessionBlocksActivation()
    {
        var foreign = Session() with
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["billing_v2_billing_event_id"] = "autre-event",
                ["billing_v2_subscription_id"] = "subscription-1",
                ["billing_v2_payment_attempt_id"] = "attempt-1"
            }
        };
        var result = BillingV2StripeSettlementVerifier.Verify(
            foreign,
            Expectation());
        Ensure(
            !result.Settled
            && result.ReasonCode == "BILLING_V2_STRIPE_BILLING_EVENT_MISMATCH",
            "Une session appartenant a un autre evenement ne doit rien activer.");
    }

    private static void VerifyExactSettlementIsConfirmed()
    {
        var result = BillingV2StripeSettlementVerifier.Verify(
            Session(),
            Expectation());
        Ensure(
            result.Settled
            && result.SettlementStatus == BillingV2SettlementStatuses.Settled
            && result.SettledAmountCents == 4046
            && result.SettledCurrency == "EUR",
            "Un encaissement exact et confirme doit etre reconnu.");
    }

    private static void VerifyMissingSessionStaysPending()
    {
        var result = BillingV2StripeSettlementVerifier.Verify(
            null,
            Expectation());
        Ensure(
            !result.Settled
            && result.SettlementStatus == BillingV2SettlementStatuses.Pending,
            "Sans objet relu, on reste en attente, jamais en succes.");
    }

    // ------------------------------------------------------------------
    // F. Safety
    // ------------------------------------------------------------------

    private static void VerifyCheckoutSessionCompletedNoLongerActivates()
    {
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderEvent("checkout.session.completed"),
            ProviderState());
        Ensure(
            plan.SubscriptionStatus is null,
            "F : checkout.session.completed ne doit plus activer directement.");
        Ensure(
            !BillingV2ProviderInboundProvisioningPolicy.ShouldAttempt(
                plan,
                ProviderState()),
            "F : ce signal ne doit declencher ni facture ni provisioning direct.");
        Ensure(
            BillingV2ProviderInboundProvisioningPolicy.ShouldVerifySettlement(
                "stripe",
                plan.ReasonCode),
            "F : il doit en revanche declencher une relecture Stripe.");
    }

    private static void VerifySubscriptionCreatedAndUpdatedStayInert()
    {
        foreach (var eventType in new[]
                 {
                     "customer.subscription.created",
                     "customer.subscription.updated"
                 })
        {
            var plan = BillingV2ProviderInboundEventPlanner.Plan(
                ProviderEvent(eventType),
                ProviderState());
            Ensure(
                plan.SubscriptionStatus is null
                && plan.AgreementStatus is null
                && plan.CheckoutStatus is null,
                $"F : {eventType} doit rester un signal inerte.");
            Ensure(
                !BillingV2ProviderInboundProvisioningPolicy
                    .ShouldVerifySettlement("stripe", plan.ReasonCode),
                $"F : {eventType} ne doit meme pas declencher de verification.");
        }
    }

    private static void VerifyOnlyStripeSignalsTriggerVerification()
        => Ensure(
            !BillingV2ProviderInboundProvisioningPolicy.ShouldVerifySettlement(
                "paypal",
                "BILLING_V2_PROVIDER_CHECKOUT_COMPLETED_SIGNAL"),
            "PayPal n'est pas branche en Phase 2 et ne doit pas etre verifie ici.");

    // ------------------------------------------------------------------
    // Ventilation et construction de l'evenement
    // ------------------------------------------------------------------

    private static void VerifyDiscountAllocationIsExactAndDeterministic()
    {
        var weights = new long[] { 1190, 1580, 3670 };
        var first = BillingV2DiscountAllocator.Allocate(1000, weights);
        var second = BillingV2DiscountAllocator.Allocate(1000, weights);

        Ensure(
            first.Sum() == 1000,
            "La ventilation ne doit ni perdre ni inventer de centime.");
        Ensure(
            first.SequenceEqual(second),
            "Deux executions identiques doivent ventiler a l'identique.");
        Ensure(
            BillingV2DiscountAllocator.Allocate(0, weights).All(v => v == 0),
            "Sans remise, aucune ventilation.");
    }

    private static void VerifyMonthlyBillingEventMatchesPricingEngine()
    {
        var engine = new BillingV2PricingEngine();
        var items = new[]
        {
            Item("base", 1190),
            Item("storage", 1580),
            Item("setup", 1290, BillingV2BillingCadences.OneTime, false)
        };
        var pricing = engine.Calculate(new BillingV2PricingRequest(
            items.Select(ToPricingItem).ToArray(),
            DiscountBasisPoints: 1500,
            BillingV2PaymentModes.Monthly,
            CommitmentMonths: 12,
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            AsOfUtc: Utc(2026, 8, 15)));

        var build = BillingV2BillingEventFactory.BuildInitialCharge(
            new BillingV2BillingEventBuildRequest(
                BillingV2PaymentModes.Monthly,
                12,
                1500,
                "EUR",
                items,
                pricing,
                Utc(2026, 8, 15),
                Utc(2026, 9, 15),
                "billing_v2.billing_event|initial_charge|test-monthly"));

        Ensure(
            build.Draft.TotalAmountCents == pricing.TotalDueNowCents,
            "Le total de l'evenement doit egaler celui du Pricing Engine.");
        Ensure(
            build.OneTimeAmountCents == 1290,
            "La prestation ponctuelle ne recoit pas la remise d'engagement.");
        Ensure(
            build.RecurringAmountCents
                == pricing.DiscountedRecurringAmountCents,
            "La part recurrente doit egaler le MRR remise.");
        Ensure(
            BillingV2BillingEventPolicy
                .ValidateForFinalization(build.Draft).IsValid,
            "L'evenement construit doit satisfaire les invariants applicatifs.");
    }

    private static void VerifyUpfrontBillingEventMatchesPricingEngine()
    {
        var engine = new BillingV2PricingEngine();
        var items = new[] { Item("base", 1190), Item("storage", 1580) };
        var pricing = engine.Calculate(new BillingV2PricingRequest(
            items.Select(ToPricingItem).ToArray(),
            DiscountBasisPoints: 2000,
            BillingV2PaymentModes.Upfront,
            CommitmentMonths: 12,
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            AsOfUtc: Utc(2026, 8, 15)));

        var build = BillingV2BillingEventFactory.BuildInitialCharge(
            new BillingV2BillingEventBuildRequest(
                BillingV2PaymentModes.Upfront,
                12,
                2000,
                "EUR",
                items,
                pricing,
                Utc(2026, 8, 15),
                Utc(2027, 8, 15),
                "billing_v2.billing_event|initial_charge|test-upfront"));

        Ensure(
            build.Draft.TotalAmountCents == pricing.TotalDueNowCents,
            "Le total upfront doit egaler celui du Pricing Engine.");
        Ensure(
            build.Draft.Lines.All(line =>
                line.Description.Contains("12 mois prepayes")),
            "Les lignes upfront doivent porter la duree prepayee.");
        Ensure(
            BillingV2BillingEventPolicy
                .ValidateForFinalization(build.Draft).IsValid,
            "L'evenement upfront doit satisfaire les invariants applicatifs.");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static BillingV2StripeCheckoutRequest Build(
        BillingV2FinalizedBillingEvent billingEvent,
        string? providerCustomerId = null)
        => BillingV2StripeCheckoutRequestFactory.Build(
            billingEvent,
            "attempt-1",
            "bv2-evt-event-1",
            "client@example.invalid",
            "https://portal.invalid/ok",
            "https://portal.invalid/ko",
            providerCustomerId);

    private static BillingV2SubscriptionIntentRequest Intent(
        string clientRequestId)
        => new(
            "customer-1",
            clientRequestId,
            "fingerprint-base",
            "stripe",
            "test");

    private static BillingV2FinalizedBillingEvent Event()
        => new(
            "event-1",
            "subscription-1",
            "customer-1",
            BillingV2FinancialStatuses.Finalized,
            BillingV2SettlementStatuses.None,
            "EUR",
            BillingV2PaymentModes.Monthly,
            12,
            4046,
            4046,
            0,
            0,
            2);

    private static BillingV2StripeSessionSnapshot Session(
        long amount = 4046,
        string currency = "eur",
        string paymentStatus = "paid",
        string sessionStatus = "complete")
        => new(
            "cs_test_1",
            "pi_test_1",
            "sub_test_1",
            BillingV2StripeModes.Subscription,
            currency,
            amount,
            paymentStatus,
            sessionStatus,
            "client@example.invalid",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["billing_v2_billing_event_id"] = "event-1",
                ["billing_v2_subscription_id"] = "subscription-1",
                ["billing_v2_payment_attempt_id"] = "attempt-1"
            });

    private static BillingV2StripeVerificationExpectation Expectation()
        => new(
            "event-1",
            "subscription-1",
            "attempt-1",
            "EUR",
            4046,
            BillingV2StripeModes.Subscription,
            ExpectedCustomerEmail: null);

    private static BillingV2ProviderInboundEventRequest ProviderEvent(
        string eventType)
        => new(
            "stripe",
            "test",
            $"evt_{eventType}",
            eventType,
            "cs_test_1",
            "sub_test_1",
            PayloadText: null,
            ExpectedCustomerId: null,
            LocalSubscriptionId: null);

    private static BillingV2ProviderLocalState ProviderState()
        => new(
            "checkout-1",
            "subscription-1",
            "stripe",
            "test",
            "cs_test_1",
            "sub_test_1",
            "pending_approval",
            "pending",
            "pending_approval");

    private static BillingV2NewSubscriptionPresetItem Item(
        string code,
        long amountCents,
        string cadence = BillingV2BillingCadences.Monthly,
        bool discountEligible = true)
        => new(
            $"preset-{code}",
            $"service-{code}",
            TierId: null,
            $"price-{code}",
            code.ToUpperInvariant(),
            TierCode: null,
            $"{code.ToUpperInvariant()}-EUR-V1",
            "subscription",
            1,
            amountCents,
            "EUR",
            cadence,
            discountEligible);

    private static BillingV2PricingItem ToPricingItem(
        BillingV2NewSubscriptionPresetItem item)
        => new(
            item.PresetItemId,
            item.ServiceCode,
            item.TierCode,
            item.PriceCode,
            item.AmountCents,
            item.Quantity,
            item.BillingCadence,
            item.DiscountEligible);

    private static DateTime Utc(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static void EnsureRefused(
        BillingV2FinancialDecision decision,
        string expectedReasonCode,
        string message)
        => Ensure(
            !decision.IsValid && decision.ReasonCode == expectedReasonCode,
            $"{message} (attendu {expectedReasonCode}, obtenu {decision.ReasonCode})");

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
