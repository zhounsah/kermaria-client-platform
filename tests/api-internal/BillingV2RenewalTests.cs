using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Tests purs du cycle de vie de renouvellement (Phase 3).
///
/// Aucun appel reseau, aucune base : ces tests sont la specification
/// executable de ce que le rail a le droit de faire.
/// </summary>
public static class BillingV2RenewalTests
{
    public static Task RunAsync()
    {
        VerifyRenewalUsesContractualPrice();
        VerifyCommitmentFloorProducesExactAmount();
        VerifyRenewalExcludesOneTimeItems();
        VerifyRenewalRefusesInitialCycle();
        VerifyRenewalRefusesUpfront();
        VerifyRenewalIdempotencyKeyIsCycleScoped();
        VerifyCycleResolutionIgnoresWallClock();
        VerifyCycleResolutionFailsClosedWithoutPeriod();
        VerifyEndOfMonthCycles();
        VerifyDaylightSavingCycles();
        VerifySignalClassification();
        VerifySubscriptionEventsCannotProvePayment();
        VerifyInvoiceVerificationRejectsMismatch();
        VerifyInvoiceVerificationRejectsForeignObjects();
        VerifyPastDueDoesNotDeprovision();
        VerifyGracePolicyNeverDeprovisions();
        VerifyLookupIsBoundedOrFailsClosed();
        VerifyReconciliationCandidateWithInvoiceOnly();
        VerifyReadinessMatrix();
        VerifyLaunchScopeIsFrozen();
        VerifyReconciliationMetrics();
        VerifyReconciliationIntervalGuard();
        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------
    // Scenario F : le catalogue change apres la souscription
    // -----------------------------------------------------------------

    private static void VerifyRenewalUsesContractualPrice()
    {
        // Le contrat porte 4500 c. Le catalogue a beau passer a 9900, il
        // n'entre a aucun moment dans la construction du renouvellement : la
        // seule source de montant est l'item contractuel.
        var result = BillingV2RenewalChargeFactory.Build(
            Request(cycle: 2, items: [Item("SVC", 4500, quantity: 1)]));

        Ensure(
            result.Draft.TotalAmountCents == 4500,
            "Scenario F : le renouvellement doit utiliser le prix contractuel.");
        Ensure(
            result.Draft.Lines.Count == 1
            && result.Draft.Lines[0].UnitAmountCents == 4500,
            "Scenario F : la ligne doit porter le montant fige au contrat.");
    }

    // -----------------------------------------------------------------
    // Scenario G : plancher d'engagement a 45 %
    // -----------------------------------------------------------------

    private static void VerifyCommitmentFloorProducesExactAmount()
    {
        // MRR contractuel 10 000 c, remise 10 % -> 9 000 c. Plancher pose a
        // 9 500 c : le complement doit etre une LIGNE, pas un ecart glisse
        // dans le total, sinon l'invariant "total = somme des lignes" tombe.
        var result = BillingV2RenewalChargeFactory.Build(
            Request(
                cycle: 3,
                items: [Item("SVC", 10000, quantity: 1)],
                discountBasisPoints: 1000,
                floorCents: 9500));

        Ensure(
            result.Draft.TotalAmountCents == 9500,
            "Scenario G : le total doit valoir exactement le plancher.");
        Ensure(
            result.Draft.Lines.Sum(line => line.TotalAmountCents) == 9500,
            "Scenario G : le total doit rester la somme exacte des lignes.");
        Ensure(
            result.Draft.Lines.Any(line => string.Equals(
                line.ServiceCode,
                BillingV2RenewalChargeFactory.CommitmentFloorServiceCode,
                StringComparison.Ordinal)),
            "Scenario G : le complement de plancher doit etre explicite.");

        // Plancher non mordant : aucune ligne technique ne doit apparaitre.
        var withoutFloor = BillingV2RenewalChargeFactory.Build(
            Request(
                cycle: 3,
                items: [Item("SVC", 10000, quantity: 1)],
                discountBasisPoints: 1000,
                floorCents: 8000));
        Ensure(
            withoutFloor.Draft.TotalAmountCents == 9000
            && withoutFloor.Draft.Lines.Count == 1,
            "Un plancher non mordant ne doit rien ajouter.");
    }

    private static void VerifyRenewalExcludesOneTimeItems()
    {
        var result = BillingV2RenewalChargeFactory.Build(
            Request(
                cycle: 2,
                items:
                [
                    Item("SVC", 4500, quantity: 1),
                    Item(
                        "SETUP",
                        20000,
                        quantity: 1,
                        cadence: BillingV2BillingCadences.OneTime)
                ]));

        Ensure(
            result.Draft.TotalAmountCents == 4500,
            "Une prestation ponctuelle ne doit jamais etre refacturee.");
    }

    private static void VerifyRenewalRefusesInitialCycle()
        => EnsureThrows(
            () => BillingV2RenewalChargeFactory.Build(
                Request(cycle: 1, items: [Item("SVC", 4500, 1)])),
            "BILLING_V2_RENEWAL_CYCLE_IS_INITIAL_CHARGE",
            "Le cycle 1 est la charge initiale, pas un renouvellement.");

    private static void VerifyRenewalRefusesUpfront()
        => EnsureThrows(
            () => BillingV2RenewalChargeFactory.Build(
                Request(
                    cycle: 2,
                    items: [Item("SVC", 4500, 1)],
                    paymentMode: BillingV2PaymentModes.Upfront)),
            "BILLING_V2_RENEWAL_UPFRONT_NOT_SUPPORTED",
            "Un terme prepaye n'a pas de renouvellement mensuel.");

    private static void VerifyRenewalIdempotencyKeyIsCycleScoped()
    {
        var a = BillingV2RenewalChargeFactory.Build(
            Request(cycle: 17, items: [Item("SVC", 4500, 1)]));
        var b = BillingV2RenewalChargeFactory.Build(
            Request(cycle: 17, items: [Item("SVC", 4500, 1)]));
        var c = BillingV2RenewalChargeFactory.Build(
            Request(cycle: 18, items: [Item("SVC", 4500, 1)]));

        Ensure(
            a.Draft.IdempotencyKeyCanonical == b.Draft.IdempotencyKeyCanonical,
            "Deux calculs du meme cycle doivent produire la meme cle.");
        Ensure(
            a.Draft.IdempotencyKeyCanonical != c.Draft.IdempotencyKeyCanonical,
            "Deux cycles distincts doivent produire des cles distinctes.");
    }

    // -----------------------------------------------------------------
    // Cycles : jamais l'horloge
    // -----------------------------------------------------------------

    private static void VerifyCycleResolutionIgnoresWallClock()
    {
        var anchor = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc);

        // Meme periode fournie -> meme cycle, quelle que soit l'heure a
        // laquelle le webhook arrive.
        var first = BillingV2RenewalCycleResolver.Resolve(
            anchor,
            1,
            new DateTime(2026, 5, 10, 3, 0, 0, DateTimeKind.Utc));
        var second = BillingV2RenewalCycleResolver.Resolve(
            anchor,
            1,
            new DateTime(2026, 5, 10, 23, 30, 0, DateTimeKind.Utc));

        Ensure(
            first.Resolved && first.CycleSequence == 3,
            "Le troisieme cycle doit demarrer deux mois apres l'ancre.");
        Ensure(
            first.CycleSequence == second.CycleSequence,
            "Le rang du cycle ne doit pas dependre de l'heure d'arrivee.");
    }

    private static void VerifyCycleResolutionFailsClosedWithoutPeriod()
    {
        var resolution = BillingV2RenewalCycleResolver.Resolve(
            new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            1,
            providerPeriodStartUtc: null);

        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == "BILLING_V2_RENEWAL_CYCLE_PERIOD_UNKNOWN",
            "Sans periode exploitable, on ne facture rien : fail closed.");
    }

    // -----------------------------------------------------------------
    // Scenario L : fin de mois. Scenario K : changement d'heure.
    // -----------------------------------------------------------------

    private static void VerifyEndOfMonthCycles()
    {
        // Ancre au 31 janvier. Le cycle 2 se rabat sur fevrier, mais le cycle
        // 3 REMONTE au 31 mars : chaque periode est calculee depuis l'ancre,
        // pas depuis la borne precedente. C'est ce qui evite la derive.
        var anchor = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);

        var cycle2 = BillingV2BillingCalendar.ResolveCyclePeriod(anchor, 1, 2);
        var cycle3 = BillingV2BillingCalendar.ResolveCyclePeriod(anchor, 1, 3);

        Ensure(
            cycle2.CivilStart == new DateOnly(2026, 2, 28),
            "Le cycle 2 doit se rabattre au 28 fevrier 2026.");
        Ensure(
            cycle3.CivilStart == new DateOnly(2026, 3, 31),
            "Le cycle 3 doit revenir au 31 mars, sans derive cumulative.");

        // Annee bissextile : le rabattement vise le 29.
        var leap = BillingV2BillingCalendar.ResolveCyclePeriod(
            new DateTime(2028, 1, 31, 12, 0, 0, DateTimeKind.Utc), 1, 2);
        Ensure(
            leap.CivilStart == new DateOnly(2028, 2, 29),
            "En annee bissextile, le rabattement doit viser le 29 fevrier.");
    }

    private static void VerifyDaylightSavingCycles()
    {
        // Ancre le 29 mars 2026, veille du passage a l'heure d'ete. Les bornes
        // restent des jours civils Paris, et minuit existe toujours - c'est ce
        // qui rend la conversion sure aux deux bascules.
        var anchor = new DateTime(2026, 3, 28, 23, 30, 0, DateTimeKind.Utc);
        var period = BillingV2BillingCalendar.ResolveCyclePeriod(anchor, 1, 2);

        Ensure(
            BillingV2BillingCalendar.CivilDate(anchor)
                == new DateOnly(2026, 3, 29),
            "Scenario K : 23h30 UTC le 28 mars vaut deja le 29 a Paris.");
        Ensure(
            period.CivilStart == new DateOnly(2026, 4, 29)
            && period.CivilEnd == new DateOnly(2026, 5, 29),
            "Scenario K : les bornes doivent rester des jours civils Paris.");

        // Bascule d'automne : le jour de 25 heures ne decale rien non plus.
        var autumn = new DateTime(2026, 10, 24, 23, 0, 0, DateTimeKind.Utc);
        Ensure(
            BillingV2BillingCalendar.CivilDate(autumn)
                == new DateOnly(2026, 10, 25),
            "Scenario K : la bascule d'automne ne doit pas decaler le jour.");
    }

    // -----------------------------------------------------------------
    // Signaux : aucun ne prouve un paiement
    // -----------------------------------------------------------------

    private static void VerifySignalClassification()
    {
        var paid = BillingV2RenewalSignalClassifier.Classify("invoice.paid");
        Ensure(
            paid.Recognized
            && paid.RequiresInvoiceRefetch
            && paid.CanProveSettlement,
            "`invoice.paid` doit declencher une relecture d'invoice.");

        var failed = BillingV2RenewalSignalClassifier.Classify(
            "invoice.payment_failed");
        Ensure(
            failed.Recognized
            && failed.RequiresInvoiceRefetch
            && !failed.CanProveSettlement,
            "Un echec de paiement doit se verifier, pas se croire.");

        Ensure(
            !BillingV2RenewalSignalClassifier
                .Classify("customer.created").Recognized,
            "Un evenement hors perimetre ne doit pas etre reconnu.");
    }

    private static void VerifySubscriptionEventsCannotProvePayment()
    {
        foreach (var eventType in new[]
                 {
                     "customer.subscription.created",
                     "customer.subscription.updated"
                 })
        {
            var signal = BillingV2RenewalSignalClassifier.Classify(eventType);
            Ensure(
                signal.Recognized && !signal.CanProveSettlement,
                $"`{eventType}` ne doit jamais pouvoir prouver un paiement.");
            Ensure(
                !signal.RequiresInvoiceRefetch,
                $"`{eventType}` ne designe aucune invoice a encaisser.");
        }

        // Et le controle de sante lui-meme ne conclut jamais a un encaissement.
        var health = BillingV2StripeLifecycleVerifier.VerifySubscriptionHealth(
            new BillingV2StripeSubscriptionSnapshot(
                "sub_1",
                "active",
                "cus_1",
                "in_1",
                new Dictionary<string, string>(StringComparer.Ordinal)));
        Ensure(
            !health.Settled,
            "Un abonnement sain ne prouve toujours pas qu'on a ete paye.");
    }

    // -----------------------------------------------------------------
    // Scenario H (volet pur) : montant Stripe different
    // -----------------------------------------------------------------

    private static void VerifyInvoiceVerificationRejectsMismatch()
    {
        var expectation = Expectation(expectedAmount: 4500);

        var wrongAmount = BillingV2StripeLifecycleVerifier.VerifyInvoice(
            Invoice(status: "paid", amountPaid: 4400),
            Subscription("active"),
            expectation);
        Ensure(
            !wrongAmount.Settled
            && wrongAmount.Outcome == BillingV2RenewalOutcomes.AmountMismatch,
            "Scenario H : un montant different ne doit jamais etre encaisse.");

        var wrongCurrency = BillingV2StripeLifecycleVerifier.VerifyInvoice(
            Invoice(status: "paid", amountPaid: 4500, currency: "usd"),
            Subscription("active"),
            expectation);
        Ensure(
            !wrongCurrency.Settled
            && wrongCurrency.Outcome == BillingV2RenewalOutcomes.AmountMismatch,
            "Scenario H : une devise differente ne doit jamais etre encaissee.");

        var exact = BillingV2StripeLifecycleVerifier.VerifyInvoice(
            Invoice(status: "paid", amountPaid: 4500),
            Subscription("active"),
            expectation);
        Ensure(
            exact.Settled && exact.SettledAmountCents == 4500,
            "Un montant exact dans la bonne devise doit etre accepte.");

        // Le mismatch remonte en revue humaine, jamais en retrait d'acces.
        var decision = BillingV2RenewalGracePolicy.Resolve(wrongAmount.Outcome);
        Ensure(
            decision.PaymentState
                == BillingV2SubscriptionPaymentStates.ManualReview
            && decision.KeepsProvisioning,
            "Scenario H : mismatch = revue humaine, acces conserve.");
    }

    private static void VerifyInvoiceVerificationRejectsForeignObjects()
    {
        var expectation = Expectation(
            expectedAmount: 4500,
            providerSubscriptionId: "sub_ours");

        var foreign = BillingV2StripeLifecycleVerifier.VerifyInvoice(
            Invoice(
                status: "paid",
                amountPaid: 4500,
                subscriptionId: "sub_someone_else"),
            Subscription("active"),
            expectation);

        Ensure(
            !foreign.Settled,
            "Une invoice d'un autre abonnement ne doit jamais nous regler.");
    }

    // -----------------------------------------------------------------
    // Scenario I : impaye. Aucun retrait automatique.
    // -----------------------------------------------------------------

    private static void VerifyPastDueDoesNotDeprovision()
    {
        var pastDue = BillingV2StripeLifecycleVerifier.VerifyInvoice(
            Invoice(status: "open", amountPaid: 0),
            Subscription("past_due"),
            Expectation(expectedAmount: 4500));

        Ensure(
            !pastDue.Settled
            && pastDue.Outcome == BillingV2RenewalOutcomes.PastDue,
            "Scenario I : un abonnement past_due ne doit rien regler.");

        var decision = BillingV2RenewalGracePolicy.Resolve(pastDue.Outcome);
        Ensure(
            decision.PaymentState
                == BillingV2SubscriptionPaymentStates.PaymentAttention,
            "Scenario I : l'impaye doit produire un etat local explicite.");
        Ensure(
            decision.KeepsProvisioning,
            "Scenario I : aucun retrait AD automatique.");
    }

    private static void VerifyGracePolicyNeverDeprovisions()
    {
        Ensure(
            !BillingV2RenewalGracePolicy.AutomaticDeprovisioningEnabled,
            "La politique V2.0 interdit tout deprovisioning automatique.");

        foreach (var outcome in new[]
                 {
                     BillingV2RenewalOutcomes.Paid,
                     BillingV2RenewalOutcomes.Pending,
                     BillingV2RenewalOutcomes.Failed,
                     BillingV2RenewalOutcomes.PastDue,
                     BillingV2RenewalOutcomes.Unpaid,
                     BillingV2RenewalOutcomes.Cancelled,
                     BillingV2RenewalOutcomes.AmountMismatch,
                     "quelque_chose_d_inattendu"
                 })
        {
            Ensure(
                BillingV2RenewalGracePolicy.Resolve(outcome).KeepsProvisioning,
                $"Aucune issue ne doit deprovisionner (issue : {outcome}).");
        }
    }

    // -----------------------------------------------------------------
    // Point 8 : relecture bornee
    // -----------------------------------------------------------------

    private static void VerifyLookupIsBoundedOrFailsClosed()
    {
        var bySession = BillingV2StripeSessionLookupPolicy.Plan(
            new BillingV2StripeSessionLocator("cs_1", "pi_1", "sub_1", "key"));
        Ensure(
            bySession.CanLookup
            && bySession.Method
                == BillingV2StripeSessionLookupPolicy.MethodSession,
            "L'identifiant de session doit primer : c'est le plus precis.");

        var byIntent = BillingV2StripeSessionLookupPolicy.Plan(
            new BillingV2StripeSessionLocator(null, "pi_1", "sub_1", "key"));
        Ensure(
            byIntent.Method
                == BillingV2StripeSessionLookupPolicy.MethodPaymentIntent,
            "A defaut de session, le payment intent doit servir de cible.");

        var bySubscription = BillingV2StripeSessionLookupPolicy.Plan(
            new BillingV2StripeSessionLocator(null, null, "sub_1", "key"));
        Ensure(
            bySubscription.Method
                == BillingV2StripeSessionLookupPolicy.MethodSubscription,
            "En dernier recours, l'abonnement provider sert de cible.");

        var nothing = BillingV2StripeSessionLookupPolicy.Plan(
            new BillingV2StripeSessionLocator(null, null, null, "key"));
        Ensure(
            !nothing.CanLookup
            && nothing.ReasonCode
                == "BILLING_V2_STRIPE_LOOKUP_NO_PERSISTED_IDENTIFIER",
            "Sans identifiant persiste, on echoue en ferme - pas de scan.");
    }

    private static void VerifyReconciliationCandidateWithInvoiceOnly()
    {
        // Un renouvellement n'a pas de session checkout. Avant la Phase 3, ce
        // candidat etait ecarte faute de session : il ne serait jamais entre
        // en reconciliation.
        var decision = BillingV2ReconciliationPolicy.Evaluate(
            new BillingV2ReconciliationCandidate(
                "attempt-1",
                "event-1",
                BillingV2PaymentAttemptStatuses.InFlight,
                ProviderSessionId: null,
                ReconciliationAttempts: 0,
                ProviderInvoiceId: "in_1"));

        Ensure(
            decision.ShouldRefetch,
            "Une tentative sans session mais avec invoice doit etre relue.");

        var orphan = BillingV2ReconciliationPolicy.Evaluate(
            new BillingV2ReconciliationCandidate(
                "attempt-2",
                "event-2",
                BillingV2PaymentAttemptStatuses.InFlight,
                ProviderSessionId: null,
                ReconciliationAttempts: 0));
        Ensure(
            !orphan.ShouldRefetch,
            "Sans aucun identifiant provider, il n'y a rien a relire.");
    }

    // -----------------------------------------------------------------
    // Point 10 : matrice de readiness
    // -----------------------------------------------------------------

    private static void VerifyReadinessMatrix()
    {
        var ready = BillingV2LifecycleReadinessGate.Evaluate(
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
                ProvisioningEnabled: true,
                PayPalConfigured: false));

        Ensure(
            BillingV2LifecycleReadinessGate.StripeLaunchBlockers(ready).Count
                == 0,
            "Une plateforme complete ne doit plus avoir de blocage Stripe.");

        // La propriete centrale du point 10 : PayPal NOT READY ne bloque pas
        // Stripe, parce qu'il ne fait pas partie des composants requis.
        var paypal = ready.Single(component =>
            component.Component == BillingV2ReadinessComponents.PayPal);
        Ensure(
            paypal.State == BillingV2ReadinessStates.NotReady,
            "PayPal doit rester explicitement NOT READY.");
        Ensure(
            !BillingV2LifecycleReadinessGate.BlocksStripeLaunch(paypal),
            "PayPal NOT READY ne doit pas bloquer Stripe.");

        // BPCE reste MANUAL tant que la recherche de facture n'existe pas.
        var bpce = ready.Single(component =>
            component.Component == BillingV2ReadinessComponents.BpceRecovery);
        Ensure(
            bpce.State == BillingV2ReadinessStates.Manual,
            "La reprise BPCE doit rester une operation humaine.");

        // Reconciliateur non activable : la, le lancement est bien bloque.
        var withoutWorker = BillingV2LifecycleReadinessGate.Evaluate(
            new BillingV2LifecycleReadinessInputs(
                true, true, true, true, true, true, true,
                ReconciliationWorkerActivatable: false,
                DocumentIssuanceReady: true,
                BpceInvoiceLookupSupported: false,
                ProvisioningEnabled: true,
                PayPalConfigured: false));
        Ensure(
            BillingV2LifecycleReadinessGate
                .StripeLaunchBlockers(withoutWorker)
                .Any(component => component.Component
                    == BillingV2ReadinessComponents.StripeReconciliation),
            "Sans reconciliateur activable, le lancement doit etre bloque.");
    }

    // -----------------------------------------------------------------
    // Phase 4, point 1 : perimetre de lancement gele
    // -----------------------------------------------------------------

    private static void VerifyLaunchScopeIsFrozen()
    {
        // Le seul cas autorise au lancement.
        Ensure(
            BillingV2LaunchScope.EvaluateCheckout(
                "stripe", BillingV2PaymentModes.Monthly, 0).IsValid,
            "Stripe mensuel sans TVA doit rester le cas autorise.");

        // Comptant 6/12 mois : le calcul existe, l'encaissement est refuse.
        var upfront = BillingV2LaunchScope.EvaluateCheckout(
            "stripe", BillingV2PaymentModes.Upfront, 0);
        Ensure(
            !upfront.IsValid
            && upfront.ReasonCode
                == "BILLING_V2_SCOPE_UPFRONT_OUT_OF_LAUNCH_SCOPE",
            "Le comptant doit etre refuse au dispatch.");

        var paypal = BillingV2LaunchScope.EvaluateCheckout(
            "paypal", BillingV2PaymentModes.Monthly, 0);
        Ensure(
            !paypal.IsValid
            && paypal.ReasonCode
                == "BILLING_V2_SCOPE_PROVIDER_OUT_OF_LAUNCH_SCOPE",
            "PayPal doit etre refuse au dispatch.");

        Ensure(
            !BillingV2LaunchScope.EvaluateCheckout(
                "stripe", BillingV2PaymentModes.Monthly, 1).IsValid,
            "Une TVA non nulle doit etre refusee au dispatch.");

        // Les capacites hors perimetre doivent rester fermees : ce test est la
        // pour qu'un passage a true soit un acte delibere, pas un effet de bord.
        Ensure(
            !BillingV2LaunchScope.UpfrontPaymentEnabled
            && !BillingV2LaunchScope.PayPalEnabled
            && !BillingV2LaunchScope.SelfServiceUpgradesEnabled
            && !BillingV2LaunchScope.SelfServiceDowngradesEnabled
            && !BillingV2LaunchScope.CreditLedgerEnabled
            && !BillingV2LaunchScope.RefundsEnabled
            && !BillingV2LaunchScope.ChargebacksEnabled
            && !BillingV2LaunchScope.SelfServiceCancellationEnabled
            && !BillingV2LaunchScope.NonZeroTaxEnabled,
            "Le perimetre gele doit rester ferme.");
        Ensure(
            BillingV2LaunchScope.StripeMonthlyEnabled,
            "Stripe mensuel doit rester la cible READY.");
    }

    // -----------------------------------------------------------------
    // Point 1 : metriques et cadence du worker
    // -----------------------------------------------------------------

    private static void VerifyReconciliationMetrics()
    {
        var run = new BillingV2ReconciliationRunResult(
            Examined: 10,
            Claimed: 8,
            Settled: 3,
            ReconciliationRequired: 2,
            ReasonCode: "BILLING_V2_RECONCILIATION_RUN_COMPLETED",
            Failed: 1);

        Ensure(
            run.Pending == 4,
            "Le restant doit etre examine moins ce qui a conclu.");
        Ensure(
            new BillingV2ReconciliationRunResult(2, 2, 5, 0, "x").Pending == 0,
            "Le restant ne doit jamais devenir negatif.");
    }

    private static void VerifyReconciliationIntervalGuard()
    {
        Ensure(
            BillingV2RuntimeConfiguration.ResolveInterval("600") == 600,
            "Une frequence explicite valide doit etre respectee.");
        Ensure(
            BillingV2RuntimeConfiguration.ResolveInterval(null)
                == BillingV2RuntimeConfiguration
                    .DefaultReconciliationIntervalSeconds,
            "Une frequence absente doit retomber sur la valeur par defaut.");
        Ensure(
            BillingV2RuntimeConfiguration.ResolveInterval("1")
                == BillingV2RuntimeConfiguration
                    .DefaultReconciliationIntervalSeconds,
            "Une frequence trop agressive ne doit pas marteler Stripe.");
    }

    // -----------------------------------------------------------------
    // Fabriques
    // -----------------------------------------------------------------

    private const string SubscriptionId = "11111111-1111-1111-1111-111111111111";

    private static BillingV2RenewalChargeRequest Request(
        int cycle,
        IReadOnlyList<BillingV2RenewalContractItem> items,
        string paymentMode = BillingV2PaymentModes.Monthly,
        int discountBasisPoints = 0,
        long? floorCents = null)
        => new(
            SubscriptionId,
            cycle,
            paymentMode,
            CommitmentMonths: 12,
            discountBasisPoints,
            "EUR",
            floorCents,
            items,
            BillingV2BillingCalendar.ResolveCyclePeriod(
                new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc),
                1,
                Math.Max(1, cycle)));

    private static BillingV2RenewalContractItem Item(
        string code,
        long unitAmountCents,
        int quantity,
        string cadence = BillingV2BillingCadences.Monthly)
        => new(
            $"service-{code}",
            null,
            $"price-{code}",
            code,
            null,
            cadence,
            quantity,
            unitAmountCents,
            DiscountEligible: true);

    private static BillingV2StripeLifecycleExpectation Expectation(
        long expectedAmount,
        string? providerSubscriptionId = null)
        => new(
            "event-1",
            SubscriptionId,
            "attempt-1",
            "EUR",
            expectedAmount,
            providerSubscriptionId,
            ExpectedProviderCustomerId: null);

    private static BillingV2StripeInvoiceSnapshot Invoice(
        string status,
        long amountPaid,
        string currency = "eur",
        string? subscriptionId = null)
        => new(
            "in_1",
            subscriptionId,
            "cus_1",
            status,
            currency,
            amountPaid,
            amountPaid,
            "pi_1",
            "subscription_cycle",
            new Dictionary<string, string>(StringComparer.Ordinal),
            new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc));

    private static BillingV2StripeSubscriptionSnapshot Subscription(
        string status)
        => new(
            "sub_ours",
            status,
            "cus_1",
            "in_1",
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static void EnsureThrows(
        Action action,
        string expectedCode,
        string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(expectedCode, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
