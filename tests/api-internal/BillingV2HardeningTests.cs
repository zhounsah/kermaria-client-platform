using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Hardening Phase 2.5, tests purs : calendrier Europe/Paris, cycles de
/// renouvellement, politique de reconciliation, idempotence d'emission BPCE et
/// regle d'intention unique.
/// </summary>
public static class BillingV2HardeningTests
{
    public static Task RunAsync()
    {
        // 6 / scenario H : calendrier civil Paris
        VerifySubscriptionCreatedAroundParisMidnight();
        VerifyPeriodDoesNotDependOnUtcDate();
        VerifyUpfrontPeriodSpansCommitment();
        VerifyEndOfMonthAnchorIsClamped();
        VerifyDaylightSavingBoundariesAreSafe();
        VerifyCivilDayStartConversion();

        // 4 / scenario G : cycles de renouvellement
        VerifyRenewalCycleKeyIsStable();
        VerifyRenewalCyclesAreDistinct();
        VerifyInitialCycleIsNotARenewal();
        VerifyCyclePeriodsAreContiguousAndAnchored();
        VerifyCycleSequenceAtIsDerivedFromAnchor();

        // 3 : politique de reconciliation
        VerifyReconcilableStatusesOnly();
        VerifyReconciliationRequiresPersistedSession();
        VerifyReconciliationBackoffIsBounded();
        VerifyReconciliationGivesUpAndAsksForReview();

        // 5 / scenarios E et F : idempotence BPCE
        VerifyIssuanceRefusedWithoutPersistedIntent();
        VerifyIssuanceReferenceIsStable();
        VerifyIndeterminateFailsClosedWithoutLookup();
        VerifyIndeterminateRecoversWhenLookupFindsInvoice();
        VerifyIndeterminateMayRetryWhenLookupProvesAbsence();
        VerifySucceededIssuanceIsNeverRepeated();
        VerifyExhaustedIssuanceEscalates();

        // 7 : regle d'intention unique
        VerifyPendingIntentRuleScope();

        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------
    // Calendrier Europe/Paris
    // ------------------------------------------------------------------

    private static void VerifySubscriptionCreatedAroundParisMidnight()
    {
        // 16 aout 2026, 00h30 Paris = 15 aout 22h30 UTC (CEST, UTC+2).
        // L'ancien code prenait .Date sur l'UTC et datait la periode au 15.
        var anchor = new DateTime(2026, 8, 15, 22, 30, 0, DateTimeKind.Utc);

        Ensure(
            BillingV2BillingCalendar.CivilDate(anchor)
                == new DateOnly(2026, 8, 16),
            "Scenario H : le jour civil Paris est le 16, pas le 15 UTC.");

        var period = BillingV2BillingCalendar.ResolvePeriod(
            anchor,
            BillingV2PaymentModes.Monthly,
            12);
        Ensure(
            period.CivilStart == new DateOnly(2026, 8, 16)
            && period.CivilEnd == new DateOnly(2026, 9, 16),
            "Scenario H : la periode doit courir du 16 aout au 16 septembre.");
        Ensure(
            anchor.Date == new DateOnly(2026, 8, 15).ToDateTime(TimeOnly.MinValue),
            "Le piege est bien reel : .Date sur l'UTC aurait donne le 15.");
    }

    private static void VerifyPeriodDoesNotDependOnUtcDate()
    {
        // Deux instants du meme jour civil Paris, de part et d'autre de minuit
        // UTC, doivent produire la meme date civile.
        var beforeUtcMidnight = new DateTime(2026, 1, 20, 23, 30, 0, DateTimeKind.Utc);
        var afterUtcMidnight = new DateTime(2026, 1, 21, 0, 30, 0, DateTimeKind.Utc);
        Ensure(
            BillingV2BillingCalendar.CivilDate(beforeUtcMidnight)
                == new DateOnly(2026, 1, 21)
            && BillingV2BillingCalendar.CivilDate(afterUtcMidnight)
                == new DateOnly(2026, 1, 21),
            "En hiver (UTC+1), 23h30 UTC est deja le lendemain a Paris.");
    }

    private static void VerifyUpfrontPeriodSpansCommitment()
    {
        var period = BillingV2BillingCalendar.ResolvePeriod(
            new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc),
            BillingV2PaymentModes.Upfront,
            12);
        Ensure(
            period.CivilStart == new DateOnly(2026, 3, 10)
            && period.CivilEnd == new DateOnly(2027, 3, 10),
            "Le comptant 12 mois doit couvrir douze mois civils.");
    }

    private static void VerifyEndOfMonthAnchorIsClamped()
    {
        var period = BillingV2BillingCalendar.ResolvePeriod(
            new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc),
            BillingV2PaymentModes.Monthly,
            1);
        Ensure(
            period.CivilEnd == new DateOnly(2026, 2, 28),
            "Une ancre au 31 janvier se rabat sur le 28 fevrier.");

        // Le cycle 3 repart de l'ancre, pas de la borne rabattue : il ne reste
        // donc pas coince en fin de mois court.
        var third = BillingV2BillingCalendar.ResolveCyclePeriod(
            new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc),
            1,
            3);
        Ensure(
            third.CivilStart == new DateOnly(2026, 3, 31),
            "Le cycle 3 repart de l'ancre du 31 et retrouve le 31 mars.");
    }

    private static void VerifyDaylightSavingBoundariesAreSafe()
    {
        // 2026 : passage a l'heure d'ete le 29 mars, retour le 25 octobre.
        foreach (var day in new[]
                 {
                     new DateOnly(2026, 3, 29),
                     new DateOnly(2026, 10, 25)
                 })
        {
            var utc = BillingV2BillingCalendar.ToUtcStartOfCivilDay(day);
            Ensure(
                BillingV2BillingCalendar.CivilDate(utc) == day,
                $"Minuit civil du {day} doit faire l'aller-retour sans derive.");
        }
    }

    private static void VerifyCivilDayStartConversion()
    {
        // Ete : Paris = UTC+2, minuit civil = 22h00 UTC la veille.
        Ensure(
            BillingV2BillingCalendar.ToUtcStartOfCivilDay(
                new DateOnly(2026, 7, 1))
                == new DateTime(2026, 6, 30, 22, 0, 0, DateTimeKind.Utc),
            "Minuit du 1er juillet Paris vaut 30 juin 22h UTC.");
        // Hiver : Paris = UTC+1.
        Ensure(
            BillingV2BillingCalendar.ToUtcStartOfCivilDay(
                new DateOnly(2026, 1, 1))
                == new DateTime(2025, 12, 31, 23, 0, 0, DateTimeKind.Utc),
            "Minuit du 1er janvier Paris vaut 31 decembre 23h UTC.");
    }

    // ------------------------------------------------------------------
    // Cycles de renouvellement
    // ------------------------------------------------------------------

    private static void VerifyRenewalCycleKeyIsStable()
    {
        var first = BillingV2RenewalPolicy.Canonical("subscription-1", 17);
        var second = BillingV2RenewalPolicy.Canonical("subscription-1", 17);
        Ensure(
            string.Equals(first, second, StringComparison.Ordinal)
            && BillingV2RenewalPolicy.Hash(first)
                == BillingV2RenewalPolicy.Hash(second),
            "Scenario G : deux lancements du cycle 17 donnent la meme cle.");
        Ensure(
            !first.Contains(DateTime.UtcNow.Year.ToString()),
            "La cle de cycle ne doit contenir aucune trace d'heure courante.");
    }

    private static void VerifyRenewalCyclesAreDistinct()
        => Ensure(
            BillingV2RenewalPolicy.Canonical("subscription-1", 17)
                != BillingV2RenewalPolicy.Canonical("subscription-1", 18),
            "Deux cycles differents doivent avoir des cles differentes.");

    private static void VerifyInitialCycleIsNotARenewal()
    {
        var threw = false;
        try
        {
            BillingV2RenewalPolicy.ResolveCycle(
                "subscription-1",
                new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                1,
                BillingV2RenewalPolicy.InitialCycleSequence);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        Ensure(threw, "Le cycle 1 est la charge initiale, pas un renouvellement.");
    }

    private static void VerifyCyclePeriodsAreContiguousAndAnchored()
    {
        var anchor = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var second = BillingV2RenewalPolicy.ResolveCycle(
            "subscription-1", anchor, 1, 2);
        var third = BillingV2RenewalPolicy.ResolveCycle(
            "subscription-1", anchor, 1, 3);

        Ensure(
            second.Period.CivilStart == new DateOnly(2026, 2, 15)
            && second.Period.CivilEnd == new DateOnly(2026, 3, 15),
            "Le cycle 2 couvre le deuxieme mois contractuel.");
        Ensure(
            third.Period.CivilStart == second.Period.CivilEnd,
            "Les cycles doivent etre contigus.");
    }

    private static void VerifyCycleSequenceAtIsDerivedFromAnchor()
    {
        var anchor = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        Ensure(
            BillingV2RenewalPolicy.CycleSequenceAt(
                anchor, 1, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)) == 1,
            "Avant le premier anniversaire, on est encore au cycle 1.");
        Ensure(
            BillingV2RenewalPolicy.CycleSequenceAt(
                anchor, 1, new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc)) == 2,
            "Au premier anniversaire mensuel, on entre dans le cycle 2.");
        Ensure(
            BillingV2RenewalPolicy.CycleSequenceAt(
                anchor, 1, new DateTime(2026, 2, 14, 12, 0, 0, DateTimeKind.Utc)) == 1,
            "La veille de l'anniversaire, on est toujours au cycle 1.");
    }

    // ------------------------------------------------------------------
    // Reconciliation
    // ------------------------------------------------------------------

    private static void VerifyReconcilableStatusesOnly()
    {
        foreach (var terminal in new[]
                 {
                     BillingV2PaymentAttemptStatuses.Succeeded,
                     BillingV2PaymentAttemptStatuses.Failed,
                     BillingV2PaymentAttemptStatuses.Abandoned,
                     BillingV2PaymentAttemptStatuses.AmountMismatch
                 })
        {
            var decision = BillingV2ReconciliationPolicy.Evaluate(
                Candidate(status: terminal));
            Ensure(
                !decision.ShouldRefetch,
                $"Une tentative {terminal} ne doit pas etre reconciliee.");
        }

        Ensure(
            BillingV2ReconciliationPolicy.Evaluate(
                Candidate(status: BillingV2PaymentAttemptStatuses.InFlight))
                .ShouldRefetch,
            "Une tentative in_flight avec session doit etre relue.");
    }

    private static void VerifyReconciliationRequiresPersistedSession()
    {
        var decision = BillingV2ReconciliationPolicy.Evaluate(
            Candidate(sessionId: null));
        Ensure(
            !decision.ShouldRefetch
            && decision.ReasonCode
                == "BILLING_V2_RECONCILIATION_NO_PROVIDER_SESSION",
            "Sans session persistee il n'y a rien a relire, et rien a recreer.");
    }

    private static void VerifyReconciliationBackoffIsBounded()
    {
        Ensure(
            BillingV2ReconciliationPolicy.NextDelaySeconds(0) == 60,
            "Premier reessai a 1 minute.");
        Ensure(
            BillingV2ReconciliationPolicy.NextDelaySeconds(3) == 480,
            "Backoff exponentiel.");
        Ensure(
            BillingV2ReconciliationPolicy.NextDelaySeconds(50) == 1800,
            "Backoff plafonne a 30 minutes.");
    }

    private static void VerifyReconciliationGivesUpAndAsksForReview()
    {
        var decision = BillingV2ReconciliationPolicy.Evaluate(
            Candidate(attempts: BillingV2ReconciliationPolicy.MaxAttempts));
        Ensure(
            decision.GiveUp
            && !decision.ShouldRefetch
            && decision.ReasonCode == "BILLING_V2_RECONCILIATION_EXHAUSTED",
            "Au-dela du plafond, on escalade au lieu de boucler.");
    }

    // ------------------------------------------------------------------
    // Idempotence d'emission BPCE
    // ------------------------------------------------------------------

    private static void VerifyIssuanceRefusedWithoutPersistedIntent()
    {
        var decision = BillingV2DocumentIssuancePolicy.Evaluate(null);
        Ensure(
            !decision.CanCallProvider
            && decision.ReasonCode
                == "BILLING_V2_DOCUMENT_ISSUANCE_NOT_PERSISTED",
            "Aucun appel BPCE sans intention persistee au prealable.");
    }

    private static void VerifyIssuanceReferenceIsStable()
    {
        var first = BillingV2DocumentIssuancePolicy
            .BuildExternalReference("doc-1");
        var second = BillingV2DocumentIssuancePolicy
            .BuildExternalReference("doc-1");
        Ensure(
            string.Equals(first, second, StringComparison.Ordinal)
            && first.Contains("doc-1"),
            "La reference externe doit etre stable et derivee du document.");
    }

    private static void VerifyIndeterminateFailsClosedWithoutLookup()
    {
        var decision = BillingV2DocumentIssuancePolicy.ResolveIndeterminate(
            lookupSupported: false,
            lookupFoundExistingInvoice: false);
        Ensure(
            !decision.CanCallProvider
            && decision.RequiresManualReview
            && decision.ReasonCode
                == "BILLING_V2_DOCUMENT_ISSUANCE_INDETERMINATE_NO_LOOKUP",
            "Scenario E : sans recherche possible, on echoue en ferme.");
        Ensure(
            !BillingV2DocumentIssuancePolicy
                .InvoiceLookupByExternalReferenceSupported,
            "L'API BPCE actuelle ne sait pas rechercher une facture : "
            + "le drapeau doit rester false tant que ce n'est pas ajoute.");
    }

    private static void VerifyIndeterminateRecoversWhenLookupFindsInvoice()
    {
        var decision = BillingV2DocumentIssuancePolicy.ResolveIndeterminate(
            lookupSupported: true,
            lookupFoundExistingInvoice: true);
        Ensure(
            !decision.CanCallProvider
            && !decision.RequiresManualReview
            && decision.ReasonCode == "BILLING_V2_DOCUMENT_ISSUANCE_RECOVERED",
            "Une facture retrouvee se rattache, elle ne se recree pas.");
    }

    private static void VerifyIndeterminateMayRetryWhenLookupProvesAbsence()
    {
        var decision = BillingV2DocumentIssuancePolicy.ResolveIndeterminate(
            lookupSupported: true,
            lookupFoundExistingInvoice: false);
        Ensure(
            decision.CanCallProvider,
            "Absence prouvee : le retry est alors sur.");
    }

    private static void VerifySucceededIssuanceIsNeverRepeated()
    {
        var decision = BillingV2DocumentIssuancePolicy.Evaluate(
            Issuance(BillingV2DocumentIssuanceStatuses.Succeeded));
        Ensure(
            !decision.CanCallProvider
            && decision.ReasonCode
                == "BILLING_V2_DOCUMENT_ISSUANCE_ALREADY_SUCCEEDED",
            "Scenario F : un rejeu ne reemet pas une facture deja emise.");
    }

    private static void VerifyExhaustedIssuanceEscalates()
    {
        var decision = BillingV2DocumentIssuancePolicy.Evaluate(
            Issuance(
                BillingV2DocumentIssuanceStatuses.Failed,
                attemptCount: BillingV2DocumentIssuancePolicy.MaxAttempts));
        Ensure(
            !decision.CanCallProvider && decision.RequiresManualReview,
            "Au plafond de tentatives, on demande une revue humaine.");
    }

    // ------------------------------------------------------------------
    // Regle d'intention unique
    // ------------------------------------------------------------------

    private static void VerifyPendingIntentRuleScope()
    {
        var baseline = BillingV2SubscriptionIntentKey.Canonical(
            new BillingV2SubscriptionIntentRequest(
                "customer-1", "req-1", "fingerprint-base", "stripe", "test"));

        // Meme selection, meme client_request_id => meme ancre.
        Ensure(
            BillingV2SubscriptionIntentKey.Canonical(
                new BillingV2SubscriptionIntentRequest(
                    "customer-1", "req-1", "fingerprint-base", "stripe", "test"))
                == baseline,
            "Regle 7 : meme selection et meme requete => meme intention.");

        // Chacune des quatre dimensions du scope doit discriminer.
        var variants = new[]
        {
            new BillingV2SubscriptionIntentRequest(
                "customer-2", "req-1", "fingerprint-base", "stripe", "test"),
            new BillingV2SubscriptionIntentRequest(
                "customer-1", "req-1", "fingerprint-autre", "stripe", "test"),
            new BillingV2SubscriptionIntentRequest(
                "customer-1", "req-1", "fingerprint-base", "paypal", "test"),
            new BillingV2SubscriptionIntentRequest(
                "customer-1", "req-1", "fingerprint-base", "stripe", "live")
        };
        foreach (var variant in variants)
        {
            Ensure(
                BillingV2SubscriptionIntentKey.Canonical(variant) != baseline,
                "Regle 7 : le scope est client + selection + provider + env.");
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static BillingV2ReconciliationCandidate Candidate(
        string status = BillingV2PaymentAttemptStatuses.InFlight,
        string? sessionId = "cs_test_1",
        int attempts = 0)
        => new("attempt-1", "event-1", status, sessionId, attempts);

    private static BillingV2DocumentIssuanceAttempt Issuance(
        string status,
        int attemptCount = 1)
        => new(
            "issuance-1",
            "doc-1",
            "BV2-DOC-doc-1",
            status,
            ProviderInvoiceId: null,
            attemptCount);

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
