using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Tests des invariants APPLICATIFS du coeur financier Billing V2 (Phase 1).
///
/// Ces invariants ne sont pas exprimables en CHECK MariaDB (contraintes
/// inter-lignes et inter-tables). Ils sont portes par des politiques pures ;
/// ces tests sont donc leur seule garantie.
///
/// Specification : docs/billing-v2/FINANCIAL-CORE.md
/// </summary>
public static class BillingV2FinancialCoreTests
{
    public static Task RunAsync()
    {
        // APP-1 a APP-4 : arithmetique evenement / lignes.
        VerifyCoherentEventIsAccepted();
        VerifyFinalizedEventWithoutLinesIsRefused();
        VerifyLineSumMismatchIsRefused();
        VerifyEventTotalMismatchIsRefused();
        VerifyLineCurrencyMismatchIsRefused();
        VerifyEmptyCurrencyIsRefused();
        VerifyNegativeLineInDebitIsRefused();
        VerifyDiscountAllocationAcrossLinesIsChecked();
        VerifyMissingPricingEngineVersionIsRefused();
        VerifyDuplicateLineOrderIsRefused();

        // APP-5 a APP-9 : machine a etats.
        VerifyAllowedFinancialTransitions();
        VerifyTransitionToDraftIsRefused();
        VerifyTransitionFromVoidIsRefused();
        VerifyVoidWithSuccessfulSettlementIsRefused();
        VerifyVoidWithIssuedDocumentIsRefused();
        VerifyIdempotencyKeyIsNeverReused();

        // APP-10 / APP-11 : attendu vs constate.
        VerifySettlementConfirmedOnExactMatch();
        VerifySettlementAmountMismatchIsNotASuccess();
        VerifySettlementCurrencyMismatchIsNotASuccess();
        VerifyUnobservedSettlementStaysPending();

        // APP-12 / APP-13 : PaymentAttempt.
        VerifyProviderCallRequiresPersistedAttempt();
        VerifyRetryReusesPersistedProviderKey();
        VerifyTerminalAttemptCannotBeRetried();
        VerifyAttemptContextMismatchIsRefused();

        // APP-14 : optimistic locking.
        VerifyCompareAndSwapConflictIsExplicit();
        VerifyVersionIncrement();

        // APP-15 : ambiguite de prix.
        VerifySinglePriceResolves();
        VerifyHighestPriceVersionWins();
        VerifyAmbiguousPriceFailsClosed();
        VerifyMissingPriceFailsClosed();

        // Bug E1 : plancher d'engagement.
        VerifyCommitmentFloorIsFortyFivePercent();
        VerifyCommitmentFloorOnlyAppliesToCommittedMonthly();

        // Safety fix F : evenements provider inertes.
        VerifySubscriptionCreatedDoesNotActivate();
        VerifySubscriptionUpdatedDoesNotActivate();
        VerifyGenuineActivationStillActivates();
        VerifyInertSignalDoesNotReplayProvisioning();

        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------
    // APP-1 a APP-4
    // ------------------------------------------------------------------

    private static void VerifyCoherentEventIsAccepted()
    {
        var draft = Event(
            gross: 2000,
            discount: 200,
            net: 1800,
            tax: 360,
            total: 2160,
            lines:
            [
                Line(0, unit: 1200, quantity: 1, discount: 120, tax: 216),
                Line(1, unit: 800, quantity: 1, discount: 80, tax: 144)
            ]);

        var decision = BillingV2BillingEventPolicy.ValidateForFinalization(draft);
        Ensure(decision.IsValid, "Un evenement coherent doit etre accepte.");
    }

    private static void VerifyFinalizedEventWithoutLinesIsRefused()
    {
        var draft = Event(0, 0, 0, 0, 0, lines: []);
        var decision = BillingV2BillingEventPolicy.ValidateForFinalization(draft);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_EVENT_HAS_NO_LINES",
            "APP-1 : un evenement finalise sans ligne doit etre refuse.");
    }

    private static void VerifyLineSumMismatchIsRefused()
    {
        // L'evenement est arithmetiquement correct, la base l'accepterait.
        // Seule la somme des lignes ne correspond pas : c'est exactement ce
        // qu'aucun CHECK MariaDB ne peut voir.
        var draft = Event(
            gross: 2000,
            discount: 0,
            net: 2000,
            tax: 0,
            total: 2000,
            lines: [Line(0, unit: 1200, quantity: 1, discount: 0, tax: 0)]);

        var decision = BillingV2BillingEventPolicy.ValidateForFinalization(draft);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_EVENT_LINES_GROSS_SUM_MISMATCH",
            "APP-2 : la somme des lignes doit egaler le brut de l'evenement.");
    }

    private static void VerifyEventTotalMismatchIsRefused()
    {
        var draft = Event(
            gross: 1000,
            discount: 0,
            net: 1000,
            tax: 200,
            total: 1100,
            lines: [Line(0, unit: 1000, quantity: 1, discount: 0, tax: 200)]);

        var decision = BillingV2BillingEventPolicy.ValidateForFinalization(draft);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_EVENT_TOTAL_MISMATCH",
            "total doit valoir net + taxe.");
    }

    private static void VerifyLineCurrencyMismatchIsRefused()
    {
        var draft = Event(
            gross: 1000,
            discount: 0,
            net: 1000,
            tax: 0,
            total: 1000,
            lines:
            [
                Line(0, unit: 1000, quantity: 1, discount: 0, tax: 0)
                    with { Currency = "USD" }
            ]);

        var decision = BillingV2BillingEventPolicy.ValidateForFinalization(draft);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_EVENT_LINE_CURRENCY_MISMATCH",
            "APP-3 : une ligne ne peut pas changer de devise.");
    }

    private static void VerifyEmptyCurrencyIsRefused()
    {
        var draft = Event(
            gross: 1000,
            discount: 0,
            net: 1000,
            tax: 0,
            total: 1000,
            lines: [Line(0, unit: 1000, quantity: 1, discount: 0, tax: 0)])
            with { Currency = "   " };

        var decision = BillingV2BillingEventPolicy.ValidateForFinalization(draft);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_EVENT_CURRENCY_INVALID",
            "Une devise vide doit etre refusee.");
    }

    private static void VerifyNegativeLineInDebitIsRefused()
    {
        var draft = Event(
            gross: 1000,
            discount: 0,
            net: 1000,
            tax: 0,
            total: 1000,
            lines:
            [
                Line(0, unit: 1000, quantity: 1, discount: 0, tax: 0)
                    with { TaxAmountCents = -50, TotalAmountCents = 950 }
            ]);

        var decision = BillingV2BillingEventPolicy.ValidateForFinalization(draft);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_EVENT_LINE_NEGATIVE_IN_DEBIT",
            "APP-4 : aucune ligne negative dans un evenement debit.");
    }

    private static void VerifyDiscountAllocationAcrossLinesIsChecked()
    {
        // Ventilation qui perd un centime : 200 annonces, 199 ventiles.
        var draft = Event(
            gross: 2000,
            discount: 200,
            net: 1800,
            tax: 0,
            total: 1800,
            lines:
            [
                Line(0, unit: 1000, quantity: 1, discount: 100, tax: 0),
                Line(1, unit: 1000, quantity: 1, discount: 99, tax: 0)
            ]);

        var decision = BillingV2BillingEventPolicy.ValidateForFinalization(draft);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode
                == "BILLING_V2_EVENT_LINES_DISCOUNT_SUM_MISMATCH",
            "APP-2 : un centime de remise perdu doit etre detecte.");
    }

    private static void VerifyMissingPricingEngineVersionIsRefused()
    {
        var draft = Event(
            gross: 1000,
            discount: 0,
            net: 1000,
            tax: 0,
            total: 1000,
            lines: [Line(0, unit: 1000, quantity: 1, discount: 0, tax: 0)])
            with { PricingEngineVersion = "" };

        var decision = BillingV2BillingEventPolicy.ValidateForFinalization(draft);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode
                == "BILLING_V2_EVENT_PRICING_ENGINE_VERSION_MISSING",
            "Sans version de moteur, une facture n'est pas re-verifiable.");
    }

    private static void VerifyDuplicateLineOrderIsRefused()
    {
        var draft = Event(
            gross: 2000,
            discount: 0,
            net: 2000,
            tax: 0,
            total: 2000,
            lines:
            [
                Line(0, unit: 1000, quantity: 1, discount: 0, tax: 0),
                Line(0, unit: 1000, quantity: 1, discount: 0, tax: 0)
            ]);

        var decision = BillingV2BillingEventPolicy.ValidateForFinalization(draft);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode == "BILLING_V2_EVENT_LINE_ORDER_DUPLICATED",
            "L'ordre des lignes doit rester deterministe pour la ventilation.");
    }

    // ------------------------------------------------------------------
    // APP-5 a APP-9
    // ------------------------------------------------------------------

    private static void VerifyAllowedFinancialTransitions()
    {
        var draft = State(BillingV2FinancialStatuses.Draft);
        Ensure(
            BillingV2BillingEventStateMachine.CanTransition(
                draft,
                BillingV2FinancialStatuses.Finalized).IsValid,
            "draft -> finalized doit etre autorise.");
        Ensure(
            BillingV2BillingEventStateMachine.CanTransition(
                draft,
                BillingV2FinancialStatuses.Void).IsValid,
            "draft -> void doit etre autorise.");
        Ensure(
            BillingV2BillingEventStateMachine.CanTransition(
                State(BillingV2FinancialStatuses.Finalized),
                BillingV2FinancialStatuses.Void).IsValid,
            "finalized -> void doit etre autorise si rien n'est acquis.");
    }

    private static void VerifyTransitionToDraftIsRefused()
    {
        var decision = BillingV2BillingEventStateMachine.CanTransition(
            State(BillingV2FinancialStatuses.Finalized),
            BillingV2FinancialStatuses.Draft);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode
                == "BILLING_V2_EVENT_TRANSITION_TO_DRAFT_FORBIDDEN",
            "APP-7 : un evenement finalise ne redevient pas un brouillon.");
    }

    private static void VerifyTransitionFromVoidIsRefused()
    {
        var decision = BillingV2BillingEventStateMachine.CanTransition(
            State(BillingV2FinancialStatuses.Void),
            BillingV2FinancialStatuses.Finalized);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode
                == "BILLING_V2_EVENT_TRANSITION_FROM_VOID_FORBIDDEN",
            "APP-8 : void est terminal.");
    }

    private static void VerifyVoidWithSuccessfulSettlementIsRefused()
    {
        foreach (var settled in new[]
                 {
                     BillingV2SettlementStatuses.Settled,
                     BillingV2SettlementStatuses.PartiallySettled,
                     BillingV2SettlementStatuses.Refunded
                 })
        {
            var decision = BillingV2BillingEventStateMachine.CanTransition(
                State(BillingV2FinancialStatuses.Finalized, settlement: settled),
                BillingV2FinancialStatuses.Void);
            Ensure(
                !decision.IsValid
                && decision.ReasonCode
                    == "BILLING_V2_EVENT_VOID_FORBIDDEN_SETTLED",
                $"APP-5 : void interdit avec settlement {settled}.");
        }
    }

    private static void VerifyVoidWithIssuedDocumentIsRefused()
    {
        var decision = BillingV2BillingEventStateMachine.CanTransition(
            State(
                BillingV2FinancialStatuses.Finalized,
                document: BillingV2EventDocumentStatuses.Issued),
            BillingV2FinancialStatuses.Void);
        Ensure(
            !decision.IsValid
            && decision.ReasonCode
                == "BILLING_V2_EVENT_VOID_FORBIDDEN_DOCUMENT_ISSUED",
            "APP-6 : void interdit si un document legal est emis.");
    }

    private static void VerifyIdempotencyKeyIsNeverReused()
    {
        foreach (var status in new[]
                 {
                     BillingV2FinancialStatuses.Draft,
                     BillingV2FinancialStatuses.Finalized,
                     BillingV2FinancialStatuses.Void
                 })
        {
            var decision =
                BillingV2BillingEventStateMachine.CanReuseIdempotencyKey(
                    State(status));
            Ensure(
                !decision.IsValid
                && decision.ReasonCode
                    == "BILLING_V2_EVENT_IDEMPOTENCY_KEY_ALREADY_CONSUMED",
                $"APP-9 : cle jamais reutilisee, meme depuis {status}.");
        }
    }

    // ------------------------------------------------------------------
    // APP-10 / APP-11
    // ------------------------------------------------------------------

    private static void VerifySettlementConfirmedOnExactMatch()
    {
        var decision = BillingV2SettlementPolicy.Evaluate(
            new BillingV2SettlementObservation(2160, "EUR", 2160, "EUR"));
        Ensure(
            decision.SettlementStatus == BillingV2SettlementStatuses.Settled,
            "Un settlement exact doit etre confirme.");
    }

    private static void VerifySettlementAmountMismatchIsNotASuccess()
    {
        var under = BillingV2SettlementPolicy.Evaluate(
            new BillingV2SettlementObservation(2160, "EUR", 1990, "EUR"));
        Ensure(
            under.SettlementStatus == BillingV2SettlementStatuses.AmountMismatch,
            "APP-10 : un encaissement partiel n'est pas un succes.");

        var over = BillingV2SettlementPolicy.Evaluate(
            new BillingV2SettlementObservation(2160, "EUR", 2400, "EUR"));
        Ensure(
            over.SettlementStatus == BillingV2SettlementStatuses.AmountMismatch,
            "APP-10 : un encaissement superieur n'est pas un succes non plus.");
    }

    private static void VerifySettlementCurrencyMismatchIsNotASuccess()
    {
        var decision = BillingV2SettlementPolicy.Evaluate(
            new BillingV2SettlementObservation(2160, "EUR", 2160, "USD"));
        Ensure(
            decision.SettlementStatus == BillingV2SettlementStatuses.AmountMismatch
            && decision.ReasonCode == "BILLING_V2_SETTLEMENT_CURRENCY_MISMATCH",
            "APP-11 : meme montant dans une autre devise n'est pas un succes.");
    }

    private static void VerifyUnobservedSettlementStaysPending()
    {
        var decision = BillingV2SettlementPolicy.Evaluate(
            new BillingV2SettlementObservation(2160, "EUR", null, null));
        Ensure(
            decision.SettlementStatus == BillingV2SettlementStatuses.Pending,
            "Sans constat, on ne conclut jamais a un succes par defaut.");
    }

    // ------------------------------------------------------------------
    // APP-12 / APP-13
    // ------------------------------------------------------------------

    private static void VerifyProviderCallRequiresPersistedAttempt()
    {
        var decision = BillingV2PaymentAttemptPolicy.EvaluateProviderCall(
            persistedAttempt: null,
            "stripe",
            "test");
        Ensure(
            !decision.CanCall
            && decision.ReasonCode
                == "BILLING_V2_PAYMENT_ATTEMPT_NOT_PERSISTED",
            "APP-12 : pas de ligne persistee, pas d'appel provider.");
    }

    private static void VerifyRetryReusesPersistedProviderKey()
    {
        var attempt = new BillingV2PaymentAttemptSnapshot(
            "attempt-1",
            "stripe",
            "test",
            "billing-v2-evt-42",
            BillingV2PaymentAttemptStatuses.InFlight);
        var decision = BillingV2PaymentAttemptPolicy.EvaluateProviderCall(
            attempt,
            "stripe",
            "test");
        Ensure(
            decision.CanCall
            && decision.ProviderRequestKey == "billing-v2-evt-42",
            "APP-13 : un retry reutilise la cle persistee, il n'en invente pas.");
    }

    private static void VerifyTerminalAttemptCannotBeRetried()
    {
        foreach (var status in new[]
                 {
                     BillingV2PaymentAttemptStatuses.Succeeded,
                     BillingV2PaymentAttemptStatuses.Abandoned
                 })
        {
            var decision = BillingV2PaymentAttemptPolicy.EvaluateProviderCall(
                new BillingV2PaymentAttemptSnapshot(
                    "attempt-1",
                    "stripe",
                    "test",
                    "billing-v2-evt-42",
                    status),
                "stripe",
                "test");
            Ensure(
                !decision.CanCall
                && decision.ReasonCode
                    == "BILLING_V2_PAYMENT_ATTEMPT_ALREADY_TERMINAL",
                $"Une tentative {status} ne doit pas repartir chez le provider.");
        }
    }

    private static void VerifyAttemptContextMismatchIsRefused()
    {
        var decision = BillingV2PaymentAttemptPolicy.EvaluateProviderCall(
            new BillingV2PaymentAttemptSnapshot(
                "attempt-1",
                "stripe",
                "live",
                "billing-v2-evt-42",
                BillingV2PaymentAttemptStatuses.Created),
            "stripe",
            "test");
        Ensure(
            !decision.CanCall
            && decision.ReasonCode
                == "BILLING_V2_PAYMENT_ATTEMPT_CONTEXT_MISMATCH",
            "Une tentative live ne doit pas etre rejouee en test.");
    }

    // ------------------------------------------------------------------
    // APP-14
    // ------------------------------------------------------------------

    private static void VerifyCompareAndSwapConflictIsExplicit()
    {
        var conflict = BillingV2SubscriptionVersionPolicy
            .EvaluateCompareAndSwap(0);
        Ensure(
            !conflict.IsValid
            && conflict.ReasonCode
                == "BILLING_V2_SUBSCRIPTION_VERSION_CONFLICT",
            "APP-14 : un conflit de version remonte, il n'est jamais avale.");

        Ensure(
            BillingV2SubscriptionVersionPolicy
                .EvaluateCompareAndSwap(1).IsValid,
            "Un compare-and-swap gagnant doit etre valide.");

        Ensure(
            !BillingV2SubscriptionVersionPolicy
                .EvaluateCompareAndSwap(2).IsValid,
            "Plusieurs lignes affectees signalent une anomalie.");
    }

    private static void VerifyVersionIncrement()
    {
        Ensure(
            BillingV2SubscriptionVersionPolicy.NextVersion(1) == 2,
            "La version s'incremente de 1.");

        var threw = false;
        try
        {
            BillingV2SubscriptionVersionPolicy.NextVersion(0);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        Ensure(threw, "La version initiale deterministe est 1, jamais 0.");
    }

    // ------------------------------------------------------------------
    // APP-15
    // ------------------------------------------------------------------

    private static void VerifySinglePriceResolves()
    {
        var resolution = BillingV2ServicePriceResolutionPolicy.Resolve(
            [Price("p1", version: 1, amount: 1190)],
            "STORAGE-PERSONAL",
            "128");
        Ensure(
            resolution.Resolved
            && resolution.Price!.AmountCents == 1190,
            "Un prix unique doit se resoudre.");
    }

    private static void VerifyHighestPriceVersionWins()
    {
        var resolution = BillingV2ServicePriceResolutionPolicy.Resolve(
            [
                Price("p1", version: 1, amount: 1190),
                Price("p2", version: 2, amount: 1290)
            ],
            "STORAGE-PERSONAL",
            "128");
        Ensure(
            resolution.Resolved
            && resolution.ReasonCode
                == BillingV2ServicePriceResolutionPolicy
                    .ResolvedByVersionReasonCode
            && resolution.Price!.AmountCents == 1290,
            "APP-15 : la version la plus haute tranche, sans jamais sommer.");
        Ensure(
            resolution.Price!.AmountCents != 1190 + 1290,
            "Deux versions d'un prix ne sont jamais additionnees.");
    }

    private static void VerifyAmbiguousPriceFailsClosed()
    {
        var resolution = BillingV2ServicePriceResolutionPolicy.Resolve(
            [
                Price("p1", version: 2, amount: 1190),
                Price("p2", version: 2, amount: 1290)
            ],
            "STORAGE-PERSONAL",
            "128");
        Ensure(
            !resolution.Resolved
            && resolution.Price is null
            && resolution.ReasonCode
                == BillingV2ServicePriceResolutionPolicy.AmbiguousReasonCode,
            "APP-15 : a egalite de version, on echoue en ferme.");
    }

    private static void VerifyMissingPriceFailsClosed()
    {
        var resolution = BillingV2ServicePriceResolutionPolicy.Resolve(
            [],
            "STORAGE-PERSONAL",
            "128");
        Ensure(
            !resolution.Resolved
            && resolution.ReasonCode
                == BillingV2ServicePriceResolutionPolicy.NotFoundReasonCode,
            "Aucun prix applicable doit echouer explicitement.");
    }

    // ------------------------------------------------------------------
    // Bug E1 : plancher d'engagement
    // ------------------------------------------------------------------

    private static void VerifyCommitmentFloorIsFortyFivePercent()
    {
        var engine = new BillingV2PricingEngine();

        // Cas documente dans TEST-PLAN.md : MRR remise 40,00 EUR -> 18,00 EUR.
        var floor = BillingV2CommitmentFloorPolicy.Resolve(
            engine,
            BillingV2PaymentModes.Monthly,
            commitmentMonths: 12,
            discountedRecurringAmountCents: 4000);
        Ensure(
            floor == 1800,
            "E1 : le plancher vaut 45 % du MRR initial remise, pas 100 %.");
        Ensure(
            floor != 4000,
            "E1 : le plancher ne doit jamais valoir le MRR complet.");
    }

    private static void VerifyCommitmentFloorOnlyAppliesToCommittedMonthly()
    {
        var engine = new BillingV2PricingEngine();

        Ensure(
            BillingV2CommitmentFloorPolicy.Resolve(
                engine,
                BillingV2PaymentModes.Monthly,
                commitmentMonths: 1,
                discountedRecurringAmountCents: 4000) is null,
            "Sans engagement, aucun plancher.");

        Ensure(
            BillingV2CommitmentFloorPolicy.Resolve(
                engine,
                BillingV2PaymentModes.Upfront,
                commitmentMonths: 12,
                discountedRecurringAmountCents: 4000) is null,
            "En comptant, le plancher mensuel ne s'applique pas.");
    }

    // ------------------------------------------------------------------
    // Safety fix F : evenements provider inertes
    // ------------------------------------------------------------------

    private static void VerifySubscriptionCreatedDoesNotActivate()
        => EnsureInertProviderEvent("customer.subscription.created");

    private static void VerifySubscriptionUpdatedDoesNotActivate()
        => EnsureInertProviderEvent("customer.subscription.updated");

    private static void EnsureInertProviderEvent(string eventType)
    {
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderEvent(eventType),
            ProviderState());

        Ensure(
            plan.CanApply,
            $"{eventType} doit rester enregistrable sans erreur.");
        Ensure(
            plan.SubscriptionStatus is null,
            $"F : {eventType} ne doit provoquer aucune activation V2.");
        Ensure(
            plan.AgreementStatus is null && plan.CheckoutStatus is null,
            $"F : {eventType} ne doit muter aucun statut local.");
        Ensure(
            !BillingV2ProviderInboundProvisioningPolicy.ShouldAttempt(
                plan,
                ProviderState()),
            $"F : {eventType} ne doit declencher ni facture ni provisioning.");
    }

    private static void VerifyGenuineActivationStillActivates()
    {
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderEvent("billing_v2.subscription_activated"),
            ProviderState());

        Ensure(
            plan.CanApply && plan.SubscriptionStatus == "active",
            "Une activation explicite doit continuer a activer.");
        Ensure(
            BillingV2ProviderInboundProvisioningPolicy.ShouldAttempt(
                plan,
                ProviderState()),
            "Une activation explicite reste eligible au provisioning.");
    }

    private static void VerifyInertSignalDoesNotReplayProvisioning()
    {
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderEvent("customer.subscription.updated"),
            ProviderState());

        Ensure(
            !BillingV2ProviderInboundProvisioningPolicy
                .ShouldAttemptProcessedReplay(plan.ReasonCode),
            "F : un rejeu de signal inerte ne relance pas le provisioning.");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

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

    private static BillingV2BillingEventDraft Event(
        long gross,
        long discount,
        long net,
        long tax,
        long total,
        IReadOnlyList<BillingV2BillingEventLineDraft> lines)
        => new(
            BillingV2BillingEventTypes.InitialCharge,
            BillingV2BillingEventDirections.Debit,
            "EUR",
            gross,
            discount,
            net,
            tax,
            total,
            "pricing-engine-v1",
            "billing_v2|initial_charge|subscription-1|2026-08-01",
            lines);

    private static BillingV2BillingEventLineDraft Line(
        int displayOrder,
        long unit,
        int quantity,
        long discount,
        long tax)
    {
        var gross = unit * quantity;
        var net = gross - discount;
        return new BillingV2BillingEventLineDraft(
            displayOrder,
            $"SERVICE-{displayOrder}",
            TierCode: null,
            $"Service {displayOrder}",
            quantity,
            unit,
            gross,
            discount,
            net,
            tax,
            net + tax,
            "EUR");
    }

    private static BillingV2BillingEventStateSnapshot State(
        string financial,
        string settlement = BillingV2SettlementStatuses.None,
        string document = BillingV2EventDocumentStatuses.None)
        => new(financial, settlement, document);

    private static BillingV2ServicePriceCandidate Price(
        string id,
        int version,
        long amount)
        => new(
            id,
            $"{id}-EUR-V{version}",
            version,
            amount,
            "EUR",
            BillingV2BillingCadences.Monthly,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
