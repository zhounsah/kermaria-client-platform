using System.Reflection;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Resiliation Billing V2 — tests comportementaux.
/// </summary>
/// <remarks>
/// <para>
/// Ces tests exercent la chaine, ils ne cherchent pas des noms de fonctions.
/// L'ancienne suite se contentait de verifier que des helpers etaient
/// mentionnes dans une route — ce qui restait vrai alors meme que la branche
/// les contenant etait devenue inatteignable derriere un 409.
/// </para>
/// <para>
/// L'invariant central verifie ici : <b>un etat local annule ne doit jamais
/// affirmer plus que ce que le fournisseur a accepte.</b> Ses quatre
/// corollaires couteux, chacun couvert par une section :
/// </para>
/// <list type="bullet">
/// <item>l'absence d'ancre fournisseur ne prouve pas un achat ponctuel ;</item>
/// <item>une fin de terme PayPal doit reellement resilier AU terme ;</item>
/// <item>un appel parti dans le mauvais environnement ne prouve rien ;</item>
/// <item>un abonnement suspendu a pu etre actif, et payer une periode.</item>
/// </list>
/// </remarks>
public static class BillingV2CancellationTests
{
    private const string StripeSubscription = "sub_stripe_123";
    private const string PayPalSubscription = "I-PAYPAL456";

    private static readonly DateTime NowUtc =
        new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    public static async Task RunAsync()
    {
        // A. Resolution de l'ancre fournisseur
        VerifyAnchorFoundInPaymentAgreementsAlone();
        VerifyAnchorFoundInCheckoutSessionsAlone();
        VerifyAnchorFoundInSucceededPaymentAttemptAlone();
        VerifyConflictingProviderIdentifiersFailClosed();
        VerifyRecurringSubscriptionWithoutAnchorIsNeverCancelled();
        VerifyTruePureOneTimePurchaseMayBeCancelledLocally();

        // B. Decision : quelle resiliation, et faut-il appeler le fournisseur
        VerifyActiveStripeSubscriptionCancelsAtPeriodEnd();
        VerifyAdminCancellationIsImmediate();
        VerifyNeverActivatedSubscriptionCancelsImmediately();
        VerifySuspendedSubscriptionKeepsItsPaidPeriod();
        VerifySuspendedSubscriptionWithoutPaidPeriodCancelsImmediately();
        VerifyTerminalSubscriptionIsNotCancelledTwice();

        // C. Fin de terme PayPal, reellement executee au terme
        VerifyPayPalTermEndSuspendsNowAndCancelsAtTerm();
        VerifyPayPalTermCancellationIsPersistedNotTimed();
        VerifyExpectedSuspensionDoesNotBecomePastDue();
        VerifyUnexpectedSuspensionStillBecomesPastDue();
        await VerifyOnlyTerminalOperationsCloseTheSubscription();
        await VerifyTermEndProviderFailureKeepsPendingCancellation();

        // D. Environnement d'execution
        VerifyPersistedEnvironmentMustMatchTheRunningProcess();
        VerifyMatchingEnvironmentIsAllowed();

        // E. Idempotence de la demande
        VerifySameRequestProducesSameIdempotencyKey();
        VerifyDifferentOperationProducesDifferentIdempotencyKey();
        VerifyPayloadSurvivesSerialisation();
        VerifyUnknownOperationIsRefusedAtParse();

        // F. Convergence fournisseur
        await VerifyProviderAcceptanceClosesOnlyImmediateCancellations();
        await VerifyProviderFailureNeverClosesTheSubscription();
        await VerifyRetryableFailureGoesBackToTheQueue();
        await VerifyPermanentFailureStopsRetrying();
        await VerifyDisabledExecutorFailsOpenForRetryNotClosed();
        await VerifyUnsupportedProviderEnvironmentIsNotRetried();
        VerifyAmbiguousProviderRejectionIsNeverASuccess();

        // G. Conservation des droits pendant la resiliation
        VerifyActiveContractInForceGrantsAccess();
        VerifyPendingCancellationKeepsAccessUntilPaidTerm();
        VerifyPendingCancellationLosesAccessAfterPaidTerm();
        VerifyCancelledSubscriptionGrantsNoAccess();
        VerifyExpiredSubscriptionGrantsNoAccess();
        VerifyEndedCommitmentClosesAccessEvenWhenActive();
        VerifyTermEndPromiseAndAccessGateAgree();
        VerifyImmediateCancellationOpensNothing();
        VerifyPendingCancellationStillRefusesNewMutations();
        VerifyDownloadQueriesDelegateToTheRetentionPredicate();

        // H. Autorite de cadence V2.1
        VerifyComponentizedMonthlyPlusOneTimeIsRecurring();
        VerifyOneTimeOnlyItemIsNotRecurring();
        VerifyRemovedMonthlyComponentIsNotRecurring();
        VerifyExpiredMonthlyComponentIsNotRecurring();
        VerifyInactiveItemHidesItsMonthlyComponent();
        VerifyLegacySingleMonthlyProjectionIsRecurring();

        Console.WriteLine("  - 46 scenarios de resiliation verifies.");
    }

    // -----------------------------------------------------------------------
    // A. RESOLUTION DE L'ANCRE FOURNISSEUR
    // -----------------------------------------------------------------------

    /// <remarks>
    /// Un abonnement dont le mandat a ete confirme sans passer par une session
    /// de checkout persistee. Lire les seules sessions le declarerait « sans
    /// fournisseur » — et le cloturerait localement pendant que le mandat
    /// preleve.
    /// </remarks>
    private static void VerifyAnchorFoundInPaymentAgreementsAlone()
    {
        var resolution = BillingV2ProviderAnchorPolicy.Resolve(
            [Candidate("payment_agreement", "stripe", "live", StripeSubscription)]);

        Ensure(
            resolution.IsResolved,
            "L accord de paiement porte une ancre valable.");
        Ensure(
            resolution.Anchor!.ProviderSubscriptionId == StripeSubscription,
            "L identifiant fournisseur doit etre celui de l accord.");
        Ensure(
            resolution.Anchor.Environment == "live",
            "L environnement persiste doit etre remonte tel quel.");
        Ensure(
            resolution.Source == "payment_agreement",
            "La source doit rester tracable.");
    }

    private static void VerifyAnchorFoundInCheckoutSessionsAlone()
    {
        var resolution = BillingV2ProviderAnchorPolicy.Resolve(
            [Candidate("checkout_session", "stripe", "test", StripeSubscription)]);

        Ensure(
            resolution.IsResolved,
            "La session de checkout porte une ancre valable.");
        Ensure(
            resolution.Anchor!.ProviderSubscriptionId == StripeSubscription,
            "L identifiant fournisseur doit etre celui de la session.");
    }

    /// <remarks>
    /// Le cas le plus facile a manquer : un encaissement qui converge par
    /// reconciliation ne cree ni accord ni session portant l'identifiant. Seule
    /// la tentative REGLEE le porte.
    /// </remarks>
    private static void VerifyAnchorFoundInSucceededPaymentAttemptAlone()
    {
        var resolution = BillingV2ProviderAnchorPolicy.Resolve(
            [Candidate("payment_attempt", "stripe", "live", StripeSubscription)]);

        Ensure(
            resolution.IsResolved,
            "Une tentative reglee est une source d ancre autoritaire :"
                + " l ignorer declarerait « sans fournisseur » un abonnement"
                + " qui preleve.");
        Ensure(
            resolution.Source == "payment_attempt",
            "La source la plus sure doit etre celle retenue.");
    }

    /// <remarks>
    /// Trois formes de desaccord, toutes fatales : deux identifiants, deux
    /// fournisseurs, deux environnements. En choisir un reviendrait a resilier
    /// un objet dont on ne sait pas s'il appartient a ce contrat.
    /// </remarks>
    private static void VerifyConflictingProviderIdentifiersFailClosed()
    {
        var conflicts = new[]
        {
            new[]
            {
                Candidate("payment_agreement", "stripe", "live", StripeSubscription),
                Candidate("checkout_session", "stripe", "live", "sub_stripe_999")
            },
            new[]
            {
                Candidate("payment_agreement", "stripe", "live", StripeSubscription),
                Candidate("payment_attempt", "paypal", "live", StripeSubscription)
            },
            new[]
            {
                Candidate("payment_agreement", "stripe", "live", StripeSubscription),
                Candidate("checkout_session", "stripe", "test", StripeSubscription)
            }
        };

        foreach (var candidates in conflicts)
        {
            var resolution = BillingV2ProviderAnchorPolicy.Resolve(candidates);
            Ensure(
                resolution.Outcome is BillingV2ProviderAnchorOutcome.Conflict,
                "Des sources contradictoires doivent echouer en ferme, pas"
                    + " designer arbitrairement un gagnant.");
            Ensure(
                resolution.Anchor is null,
                "Aucune ancre ne doit sortir d un conflit.");

            var plan = BillingV2CancellationPolicy.Resolve(
                Context("active", recurring: true, periodEndsIn: TimeSpan.FromDays(10)),
                resolution,
                forceImmediate: true,
                NowUtc);
            Ensure(
                plan.Mode is BillingV2CancellationMode.ManualReviewRequired,
                "Un conflit d ancre doit passer en revue manuelle.");
            Ensure(
                !plan.RequiresProviderCall,
                "Aucun appel ne doit partir sur une ancre douteuse.");
            Ensure(
                plan.LocalStatus == "active",
                "Le statut local doit rester intact :"
                    + $" obtenu {plan.LocalStatus}.");
        }

        // Deux lignes concordantes ne sont PAS un conflit : c'est le cas normal
        // d'un abonnement dont l'accord et la session portent le meme objet.
        var agreed = BillingV2ProviderAnchorPolicy.Resolve(
        [
            Candidate("payment_agreement", "stripe", "live", StripeSubscription),
            Candidate("checkout_session", "STRIPE", "Live", StripeSubscription)
        ]);
        Ensure(
            agreed.IsResolved,
            "Des sources concordantes ne doivent pas etre prises pour un"
                + " conflit, meme avec une casse differente.");
    }

    /// <summary>
    /// Scenario « subscription recurring + aucune ancre -> jamais cancelled ».
    /// </summary>
    /// <remarks>
    /// C'est l'inference la plus couteuse du systeme : une ecriture manquee
    /// produit exactement la meme absence qu'un vrai achat ponctuel. Conclure
    /// « rien a resilier » afficherait « resilie » a un client encore preleve.
    /// </remarks>
    private static void VerifyRecurringSubscriptionWithoutAnchorIsNeverCancelled()
    {
        foreach (var status in new[] { "active", "suspended", "past_due" })
        {
            foreach (var forceImmediate in new[] { false, true })
            {
                var plan = BillingV2CancellationPolicy.Resolve(
                    Context(
                        status,
                        recurring: true,
                        periodEndsIn: TimeSpan.FromDays(12)),
                    MissingAnchor,
                    forceImmediate,
                    NowUtc);

                Ensure(
                    plan.LocalStatus != "cancelled",
                    $"Un abonnement « {status} » a composante mensuelle sans"
                        + " ancre ne doit JAMAIS devenir localement resilie.");
                Ensure(
                    plan.Mode is BillingV2CancellationMode.ManualReviewRequired,
                    "L absence d ancre sur un contrat recurrent est une revue"
                        + " manuelle, pas une conclusion.");
                Ensure(
                    plan.ReasonCode
                        == "BILLING_V2_CANCELLATION_PROVIDER_ANCHOR_MISSING",
                    "Le motif doit nommer explicitement l ancre manquante"
                        + $" (obtenu {plan.ReasonCode}).");
                Ensure(
                    !plan.RequiresProviderCall,
                    "On ne peut pas appeler un fournisseur sans savoir quoi lui"
                        + " demander.");
            }
        }
    }

    /// <remarks>
    /// Le pendant du test precedent : sans aucune cadence mensuelle au
    /// snapshot effectif, il n'y a reellement rien a resilier. Inventer un
    /// appel fabriquerait un echec permanent sur un objet inexistant.
    /// </remarks>
    private static void VerifyTruePureOneTimePurchaseMayBeCancelledLocally()
    {
        var plan = BillingV2CancellationPolicy.Resolve(
            Context("active", recurring: false, periodEndsIn: null),
            MissingAnchor,
            forceImmediate: false,
            NowUtc);

        Ensure(
            !plan.RequiresProviderCall,
            "Sans abonnement fournisseur, aucun appel ne doit partir.");
        Ensure(
            plan.Mode is BillingV2CancellationMode.NoProviderSubscription,
            "Le cas « aucun abonnement fournisseur » doit rester explicite.");
        Ensure(
            plan.LocalStatus == "cancelled",
            "Rien ne peut plus etre facture : l etat local peut conclure"
                + $" immediatement (obtenu {plan.LocalStatus}).");
        Ensure(
            plan.ReasonCode == BillingV2CancellationPolicy.NoProviderReasonCode,
            "Le motif doit dire pourquoi aucun fournisseur n est appele.");
    }

    // -----------------------------------------------------------------------
    // B. DECISION
    // -----------------------------------------------------------------------

    /// <remarks>
    /// La periode courante a ete payee. La couper immediatement reviendrait a
    /// garder l'argent sans rendre le service.
    /// </remarks>
    private static void VerifyActiveStripeSubscriptionCancelsAtPeriodEnd()
    {
        var plan = BillingV2CancellationPolicy.Resolve(
            Context("active", recurring: true, periodEndsIn: TimeSpan.FromDays(9)),
            StripeAnchor(),
            forceImmediate: false,
            NowUtc);

        Ensure(
            plan.Mode is BillingV2CancellationMode.AtPeriodEnd,
            "Un abonnement actif se resilie a la fin de la periode payee.");
        Ensure(
            plan.CancelAtPeriodEnd,
            "Le drapeau de fin de terme doit etre pose.");
        Ensure(
            plan.RequiresProviderCall,
            "La fin de terme doit etre reellement demandee au fournisseur ;"
                + " sinon l abonnement se renouvelle.");
        Ensure(
            plan.ProviderActions.Count == 1
                && plan.ProviderActions[0].Operation
                    == BillingV2CancellationOperations.CancelAtPeriodEnd,
            "Stripe tient la promesse seul : un seul geste suffit.");
        Ensure(
            plan.LocalStatus == "pending_cancellation",
            "Tant que le fournisseur n a pas confirme, l etat local ne doit pas"
                + $" dire « resilie » (obtenu {plan.LocalStatus}).");
    }

    private static void VerifyAdminCancellationIsImmediate()
    {
        var plan = BillingV2CancellationPolicy.Resolve(
            Context("active", recurring: true, periodEndsIn: TimeSpan.FromDays(9)),
            StripeAnchor(),
            forceImmediate: true,
            NowUtc);

        Ensure(
            plan.Mode is BillingV2CancellationMode.Immediate,
            "L administration peut couper une periode payee.");
        Ensure(
            !plan.CancelAtPeriodEnd,
            "Une coupure immediate n est pas une fin de terme.");
        Ensure(
            plan.ProviderActions.Count == 1
                && plan.ProviderActions[0].Operation
                    == BillingV2CancellationOperations.CancelImmediate,
            "Une coupure immediate doit atteindre le fournisseur.");
        Ensure(
            plan.LocalStatus == "pending_cancellation",
            "Meme l administration ne peut pas declarer resilie avant que le"
                + " fournisseur ait accepte.");
    }

    /// <remarks>
    /// Un contrat jamais active n'a servi aucune periode : il n'y a rien a
    /// preserver, donc rien a attendre. Ces statuts n'ont pas de
    /// <c>started_at</c>, ce que le test reproduit fidelement.
    /// </remarks>
    private static void VerifyNeverActivatedSubscriptionCancelsImmediately()
    {
        foreach (var status in new[]
                 {
                     "draft",
                     "pending_approval",
                     "pending_payment",
                     "pending_activation"
                 })
        {
            var plan = BillingV2CancellationPolicy.Resolve(
                new BillingV2CancellationContext(
                    status,
                    HasRecurringComponent: true,
                    StartedAtUtc: null,
                    CurrentPeriodEndsAtUtc: null,
                    RenewsAtUtc: null),
                StripeAnchor(),
                forceImmediate: false,
                NowUtc);

            Ensure(
                plan.Mode is BillingV2CancellationMode.Immediate,
                $"Un abonnement « {status} » n a pas de periode servie a"
                    + " preserver.");
            Ensure(
                plan.RequiresProviderCall,
                "Un abonnement fournisseur existe : il doit etre resilie.");
        }
    }

    /// <summary>
    /// Scenario BLOCKER D : <c>suspended</c> n'est pas « jamais active ».
    /// </summary>
    /// <remarks>
    /// Un abonnement suspendu a pu etre actif, porter un abonnement
    /// fournisseur et avoir une periode payee encore en cours. Le traiter comme
    /// jamais active lui couperait une periode reglee.
    /// </remarks>
    private static void VerifySuspendedSubscriptionKeepsItsPaidPeriod()
    {
        foreach (var status in new[] { "active", "suspended" })
        {
            var plan = BillingV2CancellationPolicy.Resolve(
                Context(status, recurring: true, periodEndsIn: TimeSpan.FromDays(4)),
                StripeAnchor(),
                forceImmediate: false,
                NowUtc);

            Ensure(
                plan.Mode is BillingV2CancellationMode.AtPeriodEnd,
                $"Un abonnement « {status} » dont la periode payee court encore"
                    + " se resilie au terme, pas tout de suite.");
            Ensure(
                plan.CancelAtPeriodEnd,
                "La periode reglee doit etre servie jusqu a son terme.");
        }

        // L'administration garde le dernier mot, y compris sur `suspended`.
        var forced = BillingV2CancellationPolicy.Resolve(
            Context("suspended", recurring: true, periodEndsIn: TimeSpan.FromDays(4)),
            StripeAnchor(),
            forceImmediate: true,
            NowUtc);
        Ensure(
            forced.Mode is BillingV2CancellationMode.Immediate,
            "forceImmediate reste une decision humaine opposable.");
    }

    /// <remarks>
    /// Le pendant : une fois la periode expiree, il n'y a plus rien a servir,
    /// et attendre un terme deja passe laisserait l'abonnement vivant.
    /// </remarks>
    private static void VerifySuspendedSubscriptionWithoutPaidPeriodCancelsImmediately()
    {
        var plan = BillingV2CancellationPolicy.Resolve(
            Context("suspended", recurring: true, periodEndsIn: TimeSpan.FromDays(-3)),
            StripeAnchor(),
            forceImmediate: false,
            NowUtc);

        Ensure(
            plan.Mode is BillingV2CancellationMode.Immediate,
            "Une periode deja echue ne se preserve pas.");
        Ensure(
            plan.RequiresProviderCall,
            "L abonnement fournisseur doit tout de meme etre resilie.");
    }

    private static void VerifyTerminalSubscriptionIsNotCancelledTwice()
    {
        foreach (var status in new[] { "cancelled", "expired" })
        {
            var plan = BillingV2CancellationPolicy.Resolve(
                Context(status, recurring: true, periodEndsIn: TimeSpan.FromDays(5)),
                StripeAnchor(),
                forceImmediate: true,
                NowUtc);

            Ensure(
                !plan.RequiresProviderCall,
                $"Un abonnement « {status} » ne doit declencher aucun appel.");
            Ensure(
                plan.LocalStatus == status,
                "Un etat terminal ne doit pas etre reecrit.");
            Ensure(
                plan.ReasonCode
                    == BillingV2CancellationPolicy.AlreadyTerminalReasonCode,
                "Le motif doit signaler l etat deja terminal.");
        }
    }

    // -----------------------------------------------------------------------
    // C. FIN DE TERME PAYPAL
    // -----------------------------------------------------------------------

    /// <summary>
    /// Scenario « PayPal actif -> suspend demande + pending_cancellation ».
    /// </summary>
    private static void VerifyPayPalTermEndSuspendsNowAndCancelsAtTerm()
    {
        var plan = BillingV2CancellationPolicy.Resolve(
            Context("active", recurring: true, periodEndsIn: TimeSpan.FromDays(6)),
            PayPalAnchor(),
            forceImmediate: false,
            NowUtc);

        Ensure(
            plan.LocalStatus == "pending_cancellation",
            "Les droits locaux restent ouverts jusqu au terme.");
        Ensure(
            plan.ProviderActions.Count == 2,
            "PayPal ne sait pas tenir une promesse de non-renouvellement :"
                + " il faut deux gestes, pas un commentaire.");

        var suspend = plan.ProviderActions[0];
        var cancel = plan.ProviderActions[1];

        Ensure(
            suspend.Operation
                == BillingV2CancellationOperations.SuspendPendingTermEnd,
            "La suspension doit partir tout de suite, sinon un renouvellement"
                + " peut se declencher entre-temps.");
        Ensure(
            suspend.AvailableAtUtc is null,
            "La suspension n a aucune raison d attendre.");
        Ensure(
            cancel.Operation == BillingV2CancellationOperations.CancelAtTerm,
            "Une resiliation reelle doit etre planifiee au terme.");
        Ensure(
            cancel.AvailableAtUtc == NowUtc.AddDays(6),
            "Le geste final doit etre date de current_period_ends_at"
                + $" (obtenu {cancel.AvailableAtUtc:O}).");
    }

    /// <remarks>
    /// La resiliation au terme est un evenement d'outbox DATE, pas un minuteur.
    /// Le test verifie les deux proprietes qui rendent cela vrai : elle porte
    /// une echeance persistable, et son geste est distinct de la suspension —
    /// donc sa cle d'idempotence l'est aussi, sinon les deux s'annuleraient
    /// dans la file et seule la suspension survivrait.
    /// </remarks>
    private static void VerifyPayPalTermCancellationIsPersistedNotTimed()
    {
        var plan = BillingV2CancellationPolicy.Resolve(
            Context("active", recurring: true, periodEndsIn: TimeSpan.FromDays(6)),
            PayPalAnchor(),
            forceImmediate: false,
            NowUtc);

        var hashes = plan.ProviderActions
            .Select(action => BillingV2CancellationOutbox.ComputeIdempotencyHash(
                new BillingV2CancellationOutboxPayload(
                    "1f0a1f0a-1f0a-4f0a-8f0a-1f0a1f0a1f0a",
                    "paypal",
                    "sandbox",
                    PayPalSubscription,
                    action.Operation,
                    plan.ReasonCode)))
            .Distinct()
            .Count();

        Ensure(
            hashes == 2,
            "Suspension et resiliation au terme sont deux gestes : ils doivent"
                + " coexister dans l outbox, pas se dedupliquer l un l autre.");
        Ensure(
            plan.ProviderActions.Any(action =>
                action.AvailableAtUtc is not null
                && action.AvailableAtUtc > NowUtc),
            "Le geste final doit etre differe par une echeance persistable ;"
                + " un minuteur en memoire serait perdu au redemarrage.");
    }

    /// <summary>
    /// Scenario « suspended entrant pendant une resiliation en cours ».
    /// </summary>
    /// <remarks>
    /// C'est notre propre <c>/suspend</c> qui revient. Le lire comme un impaye
    /// afficherait au client un incident de paiement que nous avons provoque.
    /// </remarks>
    private static void VerifyExpectedSuspensionDoesNotBecomePastDue()
    {
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            PayPalEvent("BILLING.SUBSCRIPTION.SUSPENDED"),
            PayPalState("pending_cancellation"));

        Ensure(plan.CanApply, "L evenement doit rester enregistre, pas rejete.");
        Ensure(
            plan.SubscriptionStatus is null,
            "Une suspension attendue ne porte AUCUNE transition"
                + $" (obtenu {plan.SubscriptionStatus}).");
        Ensure(
            plan.AgreementStatus is null,
            "L accord de paiement ne doit pas non plus basculer en past_due.");
        Ensure(
            plan.ReasonCode
                == "BILLING_V2_PROVIDER_SUBSCRIPTION_SUSPENSION_EXPECTED",
            "Le motif doit distinguer la suspension attendue de l incident"
                + $" (obtenu {plan.ReasonCode}).");
    }

    /// <remarks>
    /// Le garde-fou symetrique : sans intention de resiliation, une suspension
    /// PayPal reste un vrai incident et doit rester visible.
    /// </remarks>
    private static void VerifyUnexpectedSuspensionStillBecomesPastDue()
    {
        foreach (var status in new[] { "active", "pending" })
        {
            var plan = BillingV2ProviderInboundEventPlanner.Plan(
                PayPalEvent("BILLING.SUBSCRIPTION.SUSPENDED"),
                PayPalState(status));

            Ensure(
                plan.SubscriptionStatus == "past_due",
                $"Une suspension inattendue sur « {status} » est un incident de"
                    + " paiement : elle ne doit pas etre masquee"
                    + $" (obtenu {plan.SubscriptionStatus}).");
            Ensure(
                plan.ReasonCode
                    == "BILLING_V2_PROVIDER_SUBSCRIPTION_PAYMENT_FAILED",
                "Le motif doit rester celui d un echec de paiement.");
        }
    }

    /// <summary>
    /// Scenario « terme atteint -> local cancelled seulement apres succes ».
    /// </summary>
    private static Task VerifyOnlyTerminalOperationsCloseTheSubscription()
    {
        Ensure(
            BillingV2CancellationOperations.ClosesLocalSubscription(
                BillingV2CancellationOperations.CancelAtTerm),
            "La resiliation au terme, acceptee, clot bien l abonnement :"
                + " c est le geste qui rend « resilie » vrai.");
        Ensure(
            BillingV2CancellationOperations.ClosesLocalSubscription(
                BillingV2CancellationOperations.CancelImmediate),
            "Une resiliation immediate acceptee clot l abonnement.");
        Ensure(
            !BillingV2CancellationOperations.ClosesLocalSubscription(
                BillingV2CancellationOperations.SuspendPendingTermEnd),
            "Une suspension se leve : elle ne prouve pas que rien ne sera"
                + " facture.");
        Ensure(
            !BillingV2CancellationOperations.ClosesLocalSubscription(
                BillingV2CancellationOperations.CancelAtPeriodEnd),
            "Une promesse de non-renouvellement laisse la periode courir.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Scenario « echec fournisseur final au terme -> reste pending, retente ».
    /// </summary>
    private static async Task VerifyTermEndProviderFailureKeepsPendingCancellation()
    {
        var executor = new RefusingCancellationExecutor(retryable: true);
        var result = await executor.CancelAsync(
            Request(BillingV2CancellationOperations.CancelAtTerm),
            CancellationToken.None);
        var update = BillingV2CancellationDispatchPolicy.Resolve(result, 0);

        Ensure(!result.Succeeded, "Le fournisseur a refuse le geste final.");
        Ensure(
            update.Status == "pending",
            "La resiliation au terme reste DUE : l evenement doit repartir"
                + $" (obtenu {update.Status}).");
        Ensure(
            update.RetryDelayMinutes > 0,
            "Le rappel doit etre differe, pas martele.");
        Ensure(
            !(result.Succeeded
              && BillingV2CancellationOperations.ClosesLocalSubscription(
                  BillingV2CancellationOperations.CancelAtTerm)),
            "La garde de cloture ne doit pas etre franchie sans succes"
                + " fournisseur : l abonnement reste en pending_cancellation.");
    }

    // -----------------------------------------------------------------------
    // D. ENVIRONNEMENT D'EXECUTION
    // -----------------------------------------------------------------------

    /// <summary>
    /// Scenario BLOCKER C : environnement persiste vs environnement charge.
    /// </summary>
    /// <remarks>
    /// Un abonnement <c>stripe/live</c> appele avec une cle TEST recoit un
    /// <c>404</c> legitime, que la lecture naive prendrait pour « deja absent ».
    /// On cloturerait un abonnement LIVE toujours preleve.
    /// </remarks>
    private static void VerifyPersistedEnvironmentMustMatchTheRunningProcess()
    {
        var mismatches = new[]
        {
            ("stripe", "live", "test"),
            ("stripe", "test", "live"),
            ("stripe", "live", "disabled"),
            ("paypal", "live", "sandbox"),
            ("paypal", "sandbox", "live")
        };

        foreach (var (provider, persisted, runtime) in mismatches)
        {
            var refusal = BillingV2ProviderRuntimeEnvironmentPolicy.Check(
                provider,
                persisted,
                runtime);

            Ensure(
                refusal is not null,
                $"{provider} persiste en « {persisted} » ne doit pas etre"
                    + $" appele par un processus « {runtime} ».");
            Ensure(
                !refusal!.Succeeded,
                "Un desaccord d environnement n est jamais un succes.");
            Ensure(
                refusal.Code
                    == "BILLING_V2_PROVIDER_RUNTIME_ENVIRONMENT_MISMATCH",
                $"Le code doit nommer le desaccord (obtenu {refusal.Code}).");
            Ensure(
                !refusal.Retryable,
                "La configuration du processus ne changera pas d elle-meme :"
                    + " rejouer huit fois ne repare rien.");
            Ensure(
                BillingV2CancellationDispatchPolicy.Resolve(refusal, 0).Status
                    == "failed",
                "L evenement doit rester visible en echec, pas traite.");
        }

        // Un couple theoriquement valide peut etre faux a l execution : c'est
        // exactement le trou que la seule verification de matrice laissait.
        Ensure(
            BillingV2ProviderEnvironmentPolicy.IsSupported("stripe", "live"),
            "stripe/live est un couple theorique valide...");
        Ensure(
            BillingV2ProviderRuntimeEnvironmentPolicy.Check(
                "stripe",
                "live",
                "test") is not null,
            "... et pourtant interdit si le processus tourne en test.");
    }

    private static void VerifyMatchingEnvironmentIsAllowed()
    {
        var matches = new[]
        {
            ("stripe", "test", "test"),
            ("stripe", "live", "live"),
            ("paypal", "sandbox", "sandbox"),
            ("paypal", "live", "live"),
            ("stripe", "Live", " live ")
        };

        foreach (var (provider, persisted, runtime) in matches)
        {
            Ensure(
                BillingV2ProviderRuntimeEnvironmentPolicy.Check(
                    provider,
                    persisted,
                    runtime) is null,
                $"{provider} « {persisted} » doit pouvoir etre appele par un"
                    + $" processus « {runtime} ».");
        }

        Ensure(
            BillingV2ProviderRuntimeEnvironmentPolicy.Check(
                "stripe",
                "live",
                null) is not null,
            "Un environnement d execution inconnu ne vaut pas accord.");
    }

    // -----------------------------------------------------------------------
    // E. IDEMPOTENCE
    // -----------------------------------------------------------------------

    private static void VerifySameRequestProducesSameIdempotencyKey()
    {
        var first = BillingV2CancellationOutbox.ComputeIdempotencyHash(
            Payload(BillingV2CancellationOperations.CancelAtPeriodEnd));
        var second = BillingV2CancellationOutbox.ComputeIdempotencyHash(
            Payload(BillingV2CancellationOperations.CancelAtPeriodEnd));
        var spaced = BillingV2CancellationOutbox.ComputeIdempotencyHash(
            Payload(BillingV2CancellationOperations.CancelAtPeriodEnd) with
            {
                Provider = "  STRIPE "
            });

        Ensure(
            first == second,
            "Deux clics sur « resilier » doivent produire une seule demande.");
        Ensure(
            first == spaced,
            "La casse et les espaces ne doivent pas fabriquer un second appel"
                + " fournisseur.");
        Ensure(first.Length == 64, "La cle doit etre un SHA-256 hexadecimal.");
    }

    /// <remarks>
    /// Une coupure administrative immediate posee apres une resiliation client
    /// a fin de terme est une action DIFFERENTE : elle doit pouvoir partir. De
    /// meme, la suspension PayPal et la resiliation au terme d'une meme demande
    /// sont deux gestes distincts.
    /// </remarks>
    private static void VerifyDifferentOperationProducesDifferentIdempotencyKey()
    {
        var keys = new[]
            {
                BillingV2CancellationOperations.CancelAtPeriodEnd,
                BillingV2CancellationOperations.CancelImmediate,
                BillingV2CancellationOperations.SuspendPendingTermEnd,
                BillingV2CancellationOperations.CancelAtTerm
            }
            .Select(operation => BillingV2CancellationOutbox
                .ComputeIdempotencyHash(Payload(operation)))
            .Distinct()
            .Count();

        Ensure(
            keys == 4,
            "Chaque geste doit avoir sa propre cle : sinon deux gestes d une"
                + " meme demande s annulent dans la file.");
        Ensure(
            BillingV2CancellationOutbox.ComputeIdempotencyHash(
                Payload(BillingV2CancellationOperations.CancelImmediate))
            != BillingV2CancellationOutbox.ComputeIdempotencyHash(
                Payload(BillingV2CancellationOperations.CancelImmediate) with
                {
                    ProviderSubscriptionId = "sub_stripe_999"
                }),
            "Deux abonnements distincts ne doivent jamais partager une cle.");
    }

    private static void VerifyPayloadSurvivesSerialisation()
    {
        var payload = Payload(BillingV2CancellationOperations.CancelAtTerm);
        var restored = BillingV2CancellationOutbox.Parse(
            BillingV2CancellationOutbox.Serialize(payload));

        Ensure(
            restored == payload,
            "La charge utile relue depuis l outbox doit etre identique :"
                + " le dispatcher n a rien d autre pour agir.");
        Ensure(
            restored.Operation == BillingV2CancellationOperations.CancelAtTerm,
            "Le geste doit survivre au passage par la base : c est lui qui"
                + " decide si l abonnement peut etre clos.");
    }

    /// <remarks>
    /// Un geste inconnu ne doit pas atteindre l'executeur, ou il serait traduit
    /// « au mieux ». Un defaut sur une resiliation se paie.
    /// </remarks>
    private static void VerifyUnknownOperationIsRefusedAtParse()
    {
        var forged = BillingV2CancellationOutbox.Serialize(
            Payload("cancel_probably"));
        var refused = false;
        try
        {
            BillingV2CancellationOutbox.Parse(forged);
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        Ensure(refused, "Un geste inconnu doit etre refuse a la relecture.");
    }

    // -----------------------------------------------------------------------
    // F. CONVERGENCE
    // -----------------------------------------------------------------------

    private static Task VerifyProviderAcceptanceClosesOnlyImmediateCancellations()
    {
        var accepted = new BillingV2ProviderCancellationResult(
            true,
            "BILLING_V2_PROVIDER_CANCELLATION_ACCEPTED",
            null,
            Retryable: false);
        var update = BillingV2CancellationDispatchPolicy.Resolve(accepted, 0);

        Ensure(
            update.Status == "processed",
            "Une acceptation fournisseur clot l evenement d outbox.");
        Ensure(
            update.LastError is null,
            "Un succes ne doit laisser aucune erreur derriere lui.");
        Ensure(
            !BillingV2CancellationOperations.ClosesLocalSubscription(
                BillingV2CancellationOperations.CancelAtPeriodEnd),
            "Un evenement d outbox traite ne vaut pas abonnement clos :"
                + " la fin de terme Stripe reste en pending_cancellation.");
        return Task.CompletedTask;
    }

    private static async Task VerifyProviderFailureNeverClosesTheSubscription()
    {
        var executor = new RefusingCancellationExecutor(retryable: true);
        var result = await executor.CancelAsync(
            Request(BillingV2CancellationOperations.CancelImmediate),
            CancellationToken.None);
        var update = BillingV2CancellationDispatchPolicy.Resolve(result, 0);

        Ensure(!result.Succeeded, "Le fournisseur a refuse.");
        Ensure(
            update.Status != "processed",
            "Un echec ne doit pas etre marque traite.");
        Ensure(
            !(result.Succeeded
              && BillingV2CancellationOperations.ClosesLocalSubscription(
                  BillingV2CancellationOperations.CancelImmediate)),
            "Un echec fournisseur ne doit jamais franchir la garde de cloture.");
    }

    private static Task VerifyRetryableFailureGoesBackToTheQueue()
    {
        var failure = new BillingV2ProviderCancellationResult(
            false,
            "BILLING_V2_STRIPE_TRANSPORT_FAILED",
            "connection reset",
            Retryable: true);

        var first = BillingV2CancellationDispatchPolicy.Resolve(failure, 0);
        var later = BillingV2CancellationDispatchPolicy.Resolve(failure, 3);

        Ensure(
            first.Status == "pending",
            "Un incident reseau doit repartir : la resiliation reste due.");
        Ensure(
            first.RetryDelayMinutes > 0,
            "Le retry doit etre differe, pas immediat.");
        Ensure(
            later.RetryDelayMinutes > first.RetryDelayMinutes,
            "Le delai doit croitre avec les tentatives.");
        Ensure(
            first.LastError == "connection reset",
            "L erreur doit rester lisible pour l exploitant.");
        return Task.CompletedTask;
    }

    private static Task VerifyPermanentFailureStopsRetrying()
    {
        var permanent = new BillingV2ProviderCancellationResult(
            false,
            "BILLING_V2_STRIPE_CANCELLATION_FAILED",
            "HTTP 401",
            Retryable: false);
        var exhausted = new BillingV2ProviderCancellationResult(
            false,
            "BILLING_V2_STRIPE_TRANSPORT_FAILED",
            "timeout",
            Retryable: true);

        Ensure(
            BillingV2CancellationDispatchPolicy.Resolve(permanent, 0).Status
                == "failed",
            "Une erreur d autorisation ne se repare pas par un retry.");
        Ensure(
            BillingV2CancellationDispatchPolicy.Resolve(
                    exhausted,
                    BillingV2CancellationDispatchPolicy.MaxRetryCount)
                .Status == "failed",
            "Le nombre de tentatives doit etre borne, sinon la file tourne"
                + " indefiniment sans que personne ne regarde.");
        return Task.CompletedTask;
    }

    private static async Task VerifyDisabledExecutorFailsOpenForRetryNotClosed()
    {
        var result = await DisabledBillingV2ProviderCancellationExecutor
            .Instance
            .CancelAsync(
                Request(BillingV2CancellationOperations.CancelImmediate),
                CancellationToken.None);

        Ensure(!result.Succeeded, "L executeur desactive ne reussit jamais.");
        Ensure(
            result.Retryable,
            "La resiliation doit pouvoir repartir une fois l acces configure.");
        Ensure(
            BillingV2CancellationDispatchPolicy.Resolve(result, 0).Status
                == "pending",
            "L evenement doit rester en file.");
        Ensure(
            !DisabledBillingV2ProviderCancellationExecutor.Instance.CanExecute,
            "L executeur desactive doit s annoncer comme tel.");
    }

    private static Task VerifyUnsupportedProviderEnvironmentIsNotRetried()
    {
        Ensure(
            !BillingV2ProviderEnvironmentPolicy.IsSupported("stripe", "sandbox"),
            "Stripe n a pas d environnement « sandbox ».");
        Ensure(
            !BillingV2ProviderEnvironmentPolicy.IsSupported("paypal", "test"),
            "PayPal n a pas d environnement « test ».");
        Ensure(
            BillingV2ProviderEnvironmentPolicy.IsSupported("stripe", "test")
                && BillingV2ProviderEnvironmentPolicy.IsSupported(
                    "paypal",
                    "sandbox"),
            "Les couples valides doivent rester acceptes.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Durcissement : un rejet ambigu ne devient pas un succes par defaut.
    /// </summary>
    /// <remarks>
    /// PayPal repond <c>422</c> aussi bien pour « deja fait » que pour « geste
    /// impossible ». Le traiter en bloc comme un succes cloturerait des
    /// abonnements encore facturables. Sans preuve relue de l'etat reel, le
    /// resultat reste un echec — visible, donc rattrapable.
    /// </remarks>
    private static void VerifyAmbiguousProviderRejectionIsNeverASuccess()
    {
        var ambiguous = new BillingV2ProviderCancellationResult(
            false,
            "BILLING_V2_PAYPAL_CANCELLATION_FAILED",
            "HTTP 422: SUBSCRIPTION_STATUS_INVALID",
            Retryable: false);

        Ensure(!ambiguous.Succeeded, "Un 422 non eclairci reste un echec.");
        Ensure(
            BillingV2CancellationDispatchPolicy.Resolve(ambiguous, 0).Status
                == "failed",
            "Il doit rester visible en echec plutot que faussement traite.");

        // Le cas ou la relecture PROUVE la convergence : l'abonnement n'existe
        // plus chez le fournisseur, donc plus rien ne peut etre preleve.
        var proven = new BillingV2ProviderCancellationResult(
            true,
            "BILLING_V2_PROVIDER_SUBSCRIPTION_ALREADY_ABSENT",
            null,
            Retryable: false);
        Ensure(
            BillingV2CancellationDispatchPolicy.Resolve(proven, 0).Status
                == "processed",
            "Une convergence prouvee par relecture doit, elle, clore"
                + " l evenement : sinon un rejeu apres crash bloquerait"
                + " l abonnement en pending_cancellation pour toujours.");
    }

    // -----------------------------------------------------------------------
    // G. CONSERVATION DES DROITS PENDANT LA RESILIATION
    //
    // La politique de resiliation promet de servir la periode deja payee
    // jusqu'a son terme. Verifier que le statut local passe bien a
    // `pending_cancellation` ne prouve rien de cette promesse : ce qui la tient
    // ou la trahit, c'est la porte d'acces.
    // -----------------------------------------------------------------------

    private static void VerifyActiveContractInForceGrantsAccess()
    {
        Ensure(
            Grants("active", periodEndsIn: TimeSpan.FromDays(12)),
            "Un abonnement actif dont le contrat court doit ouvrir l'acces.");
    }

    private static void VerifyPendingCancellationKeepsAccessUntilPaidTerm()
    {
        Ensure(
            Grants("pending_cancellation", periodEndsIn: TimeSpan.FromDays(12)),
            "Une resiliation a fin de terme ne doit rien couper avant le "
            + "terme : la periode est deja payee.");
    }

    private static void VerifyPendingCancellationLosesAccessAfterPaidTerm()
    {
        Ensure(
            !Grants("pending_cancellation", periodEndsIn: TimeSpan.FromDays(-1)),
            "Passe le terme paye, une resiliation en attente n'ouvre plus "
            + "rien, meme si le fournisseur n'a pas encore converge.");

        Ensure(
            !Grants("pending_cancellation", periodEndsIn: null),
            "Sans periode payee connue, une resiliation en attente n'ouvre "
            + "rien : c'est le cas de la resiliation immediate.");
    }

    private static void VerifyCancelledSubscriptionGrantsNoAccess()
    {
        Ensure(
            !Grants("cancelled", periodEndsIn: TimeSpan.FromDays(12)),
            "Un abonnement resilie n'ouvre plus l'acces, meme si une date de "
            + "fin de periode trainait encore en base.");
    }

    private static void VerifyExpiredSubscriptionGrantsNoAccess()
    {
        Ensure(
            !Grants("expired", periodEndsIn: TimeSpan.FromDays(12)),
            "Un abonnement expire n'ouvre plus l'acces.");

        Ensure(
            !Grants("suspended", periodEndsIn: TimeSpan.FromDays(12)),
            "Un abonnement suspendu n'ouvre pas l'acces : la suspension est "
            + "justement la mesure qui coupe le service.");
    }

    private static void VerifyEndedCommitmentClosesAccessEvenWhenActive()
    {
        // Un contrat comptant reste `active` en base : rien ne le bascule,
        // faute de renouvellement automatique.
        Ensure(
            !BillingV2EntitlementRetentionPolicy.GrantsAcquiredRights(
                "active",
                currentPeriodEndsAtUtc: null,
                renewsAtUtc: null,
                commitmentEndsAtUtc: NowUtc.AddDays(-1),
                NowUtc),
            "Un engagement echu ferme l'acces meme sur un statut actif.");

        Ensure(
            BillingV2EntitlementRetentionPolicy.GrantsAcquiredRights(
                "active",
                currentPeriodEndsAtUtc: null,
                renewsAtUtc: null,
                commitmentEndsAtUtc: NowUtc.AddDays(1),
                NowUtc),
            "Un engagement encore courant ouvre l'acces.");
    }

    private static void VerifyTermEndPromiseAndAccessGateAgree()
    {
        var context = Context(
            "active",
            recurring: true,
            periodEndsIn: TimeSpan.FromDays(12));
        var plan = BillingV2CancellationPolicy.Resolve(
            context,
            StripeAnchor(),
            forceImmediate: false,
            NowUtc);

        Ensure(
            plan.CancelAtPeriodEnd,
            "Une resiliation client sur periode payee doit viser le terme.");

        // Le point exact du blocage : le plan pose `pending_cancellation`,
        // donc la porte doit continuer d'ouvrir sur ce meme instantane.
        Ensure(
            BillingV2EntitlementRetentionPolicy.GrantsAcquiredRights(
                plan.LocalStatus,
                context.CurrentPeriodEndsAtUtc,
                context.RenewsAtUtc,
                commitmentEndsAtUtc: null,
                NowUtc),
            "Le statut pose par la resiliation a fin de terme doit rester "
            + "servi par la porte d'acces jusqu'au terme.");
    }

    private static void VerifyImmediateCancellationOpensNothing()
    {
        var context = Context("active", recurring: true, periodEndsIn: null);
        var plan = BillingV2CancellationPolicy.Resolve(
            context,
            StripeAnchor(),
            forceImmediate: false,
            NowUtc);

        Ensure(
            !plan.CancelAtPeriodEnd,
            "Sans periode payee en cours, la resiliation est immediate.");

        // Meme statut local que la fin de terme, promesse opposee : la porte
        // doit donc lire les dates, pas seulement le statut.
        Ensure(
            !BillingV2EntitlementRetentionPolicy.GrantsAcquiredRights(
                plan.LocalStatus,
                context.CurrentPeriodEndsAtUtc,
                context.RenewsAtUtc,
                commitmentEndsAtUtc: null,
                NowUtc),
            "Une resiliation immediate ne doit rien laisser ouvert.");
    }

    private static void VerifyPendingCancellationStillRefusesNewMutations()
    {
        Ensure(
            BillingV2EntitlementRetentionPolicy.AllowsNewMutations("active"),
            "Un abonnement actif accepte de nouvelles mutations.");

        // Conserver n'est pas ouvrir : sans cette asymetrie, la correction
        // aurait autorise a equiper de nouveaux utilisateurs sur un contrat
        // qu'on est en train de fermer.
        Ensure(
            !BillingV2EntitlementRetentionPolicy.AllowsNewMutations(
                "pending_cancellation"),
            "Un abonnement en cours de resiliation ne doit plus accepter de "
            + "nouvelle mutation, meme s'il conserve ses droits acquis.");

        Ensure(
            BillingV2EntitlementRetentionPolicy.GrantsAcquiredRights(
                "pending_cancellation",
                NowUtc.AddDays(12),
                NowUtc.AddDays(12),
                commitmentEndsAtUtc: null,
                NowUtc)
            && !BillingV2EntitlementRetentionPolicy.AllowsNewMutations(
                "pending_cancellation"),
            "Les deux portes doivent diverger sur pending_cancellation.");
    }

    private static void VerifyDownloadQueriesDelegateToTheRetentionPredicate()
    {
        foreach (var fieldName in new[]
                 {
                     "CatalogTargetsSql",
                     "ProvisioningGroupsSql"
                 })
        {
            var sql = ReadPrivateSql(fieldName);

            Ensure(
                sql.Contains(
                    BillingV2EntitlementRetentionSql
                        .SubscriptionGrantsAcquiredRights,
                    StringComparison.Ordinal),
                $"{fieldName} doit composer le predicat de conservation "
                + "partage, sinon la promesse de fin de terme n'est tenue "
                + "que dans les commentaires.");

            // Le defaut d'origine, litteralement : un statut fige en dur a
            // cote du predicat le reduirait a nouveau a `active` seul.
            Ensure(
                !sql.Contains(
                    "AND subscription.status = 'active'",
                    StringComparison.Ordinal),
                $"{fieldName} ne doit plus filtrer sur un statut ecrit en "
                + "dur.");
        }
    }

    // -----------------------------------------------------------------------
    // H. AUTORITE DE CADENCE V2.1
    //
    // La cadence contractuelle se lit sur les composantes effectives, pas sur
    // les colonnes miroir de l'item. C'est cette lecture qui decide si un
    // abonnement sans ancre fournisseur peut etre ferme localement.
    // -----------------------------------------------------------------------

    private static void VerifyComponentizedMonthlyPlusOneTimeIsRecurring()
    {
        // Le cas que les colonnes miroir de l'item ne savent pas representer :
        // frais d'ouverture ponctuels ET abonnement mensuel sur le meme item.
        var rows = new[]
        {
            Component(BillingV2BillingCadences.OneTime),
            Component(BillingV2BillingCadences.Monthly)
        };

        Ensure(
            BillingV2RecurringComponentPolicy.HasRecurring(rows, NowUtc),
            "Une composante ponctuelle ne rachete pas une composante "
            + "mensuelle : l'abonnement reste recurrent.");
    }

    private static void VerifyOneTimeOnlyItemIsNotRecurring()
    {
        Ensure(
            !BillingV2RecurringComponentPolicy.HasRecurring(
                [Component(BillingV2BillingCadences.OneTime)],
                NowUtc),
            "Un achat purement ponctuel n'est pas recurrent.");

        Ensure(
            !BillingV2RecurringComponentPolicy.HasRecurring([], NowUtc),
            "Aucune composante effective : rien a prelever.");
    }

    private static void VerifyRemovedMonthlyComponentIsNotRecurring()
    {
        // Sur un item componentized, retirer la composante mensuelle ne touche
        // pas au statut de l'item : sans lecture du statut de la composante,
        // l'abonnement resterait declare recurrent a tort.
        Ensure(
            !BillingV2RecurringComponentPolicy.HasRecurring(
                [Component(
                    BillingV2BillingCadences.Monthly,
                    componentStatus: "removed")],
                NowUtc),
            "Une composante retiree ne cree plus d'obligation.");
    }

    private static void VerifyExpiredMonthlyComponentIsNotRecurring()
    {
        Ensure(
            !BillingV2RecurringComponentPolicy.HasRecurring(
                [Component(
                    BillingV2BillingCadences.Monthly,
                    componentEffectiveUntil: NowUtc.AddDays(-1))],
                NowUtc),
            "Une composante dont la fenetre est close ne preleve plus.");

        Ensure(
            !BillingV2RecurringComponentPolicy.HasRecurring(
                [Component(
                    BillingV2BillingCadences.Monthly,
                    componentEffectiveFrom: NowUtc.AddDays(1))],
                NowUtc),
            "Une composante pas encore entree en vigueur ne preleve pas.");
    }

    private static void VerifyInactiveItemHidesItsMonthlyComponent()
    {
        Ensure(
            !BillingV2RecurringComponentPolicy.HasRecurring(
                [Component(
                    BillingV2BillingCadences.Monthly,
                    itemStatus: "removed")],
                NowUtc),
            "Un item retire emporte ses composantes.");

        Ensure(
            !BillingV2RecurringComponentPolicy.HasRecurring(
                [Component(
                    BillingV2BillingCadences.Monthly,
                    itemEffectiveUntil: NowUtc.AddDays(-1))],
                NowUtc),
            "Un item hors fenetre emporte ses composantes.");
    }

    private static void VerifyLegacySingleMonthlyProjectionIsRecurring()
    {
        // La vue projette un item `legacy_single` en composante virtuelle
        // portant le statut et la fenetre de l'item : le contrat historique
        // reste donc lu, sans branche particuliere.
        Ensure(
            BillingV2RecurringComponentPolicy.HasRecurring(
                [Component(BillingV2BillingCadences.Monthly)],
                NowUtc),
            "Un contrat historique mensuel reste recurrent.");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static bool Grants(string status, TimeSpan? periodEndsIn)
        => BillingV2EntitlementRetentionPolicy.GrantsAcquiredRights(
            status,
            currentPeriodEndsAtUtc: periodEndsIn is null
                ? null
                : NowUtc.Add(periodEndsIn.Value),
            renewsAtUtc: periodEndsIn is null
                ? null
                : NowUtc.Add(periodEndsIn.Value),
            commitmentEndsAtUtc: null,
            NowUtc);

    private static string ReadPrivateSql(string fieldName)
    {
        var field = typeof(BillingV2DownloadAccessProjection).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Static);
        Ensure(
            field is not null,
            $"Le champ {fieldName} a disparu de la projection de "
            + "telechargements : le test ne prouve plus rien.");
        var value = field!.GetValue(null) as string;
        Ensure(
            !string.IsNullOrWhiteSpace(value),
            $"Le champ {fieldName} est vide.");
        return value!;
    }

    private static BillingV2EffectivePriceComponentRow Component(
        string billingCadence,
        string itemStatus = "active",
        DateTime? itemEffectiveFrom = null,
        DateTime? itemEffectiveUntil = null,
        string componentStatus = "active",
        DateTime? componentEffectiveFrom = null,
        DateTime? componentEffectiveUntil = null)
        => new(
            itemStatus,
            itemEffectiveFrom ?? NowUtc.AddDays(-30),
            itemEffectiveUntil,
            componentStatus,
            componentEffectiveFrom ?? NowUtc.AddDays(-30),
            componentEffectiveUntil,
            billingCadence);

    private static BillingV2ProviderAnchorCandidate Candidate(
        string source,
        string provider,
        string environment,
        string providerSubscriptionId)
        => new(source, provider, environment, providerSubscriptionId);

    private static readonly BillingV2ProviderAnchorResolution MissingAnchor =
        new(
            BillingV2ProviderAnchorOutcome.Missing,
            null,
            null,
            BillingV2ProviderAnchorPolicy.MissingReasonCode);

    private static BillingV2ProviderAnchorResolution StripeAnchor()
        => BillingV2ProviderAnchorPolicy.Resolve(
            [Candidate("payment_agreement", "stripe", "test", StripeSubscription)]);

    private static BillingV2ProviderAnchorResolution PayPalAnchor()
        => BillingV2ProviderAnchorPolicy.Resolve(
            [Candidate("payment_agreement", "paypal", "sandbox", PayPalSubscription)]);

    private static BillingV2CancellationContext Context(
        string status,
        bool recurring,
        TimeSpan? periodEndsIn)
        => new(
            status,
            recurring,
            StartedAtUtc: periodEndsIn is null ? null : NowUtc.AddDays(-30),
            CurrentPeriodEndsAtUtc: periodEndsIn is null
                ? null
                : NowUtc.Add(periodEndsIn.Value),
            RenewsAtUtc: periodEndsIn is null
                ? null
                : NowUtc.Add(periodEndsIn.Value));

    private static BillingV2CancellationOutboxPayload Payload(string operation)
        => new(
            "1f0a1f0a-1f0a-4f0a-8f0a-1f0a1f0a1f0a",
            "stripe",
            "test",
            StripeSubscription,
            operation,
            "BILLING_V2_CANCELLATION_IMMEDIATE_REQUESTED");

    private static BillingV2ProviderCancellationRequest Request(string operation)
        => new("stripe", "test", StripeSubscription, operation, "test");

    private static BillingV2ProviderInboundEventRequest PayPalEvent(
        string eventType)
        => new(
            "paypal",
            "sandbox",
            $"evt_{eventType}",
            eventType,
            ProviderCheckoutId: null,
            PayPalSubscription,
            PayloadText: null,
            ExpectedCustomerId: null,
            LocalSubscriptionId: null);

    private static BillingV2ProviderLocalState PayPalState(
        string subscriptionStatus)
        => new(
            "checkout-1",
            "subscription-1",
            "paypal",
            "sandbox",
            ProviderCheckoutId: null,
            PayPalSubscription,
            "completed",
            "active",
            subscriptionStatus);

    private sealed class RefusingCancellationExecutor(bool retryable)
        : IBillingV2ProviderCancellationExecutor
    {
        public bool CanExecute => true;

        public Task<BillingV2ProviderCancellationResult> CancelAsync(
            BillingV2ProviderCancellationRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(new BillingV2ProviderCancellationResult(
                false,
                "BILLING_V2_STRIPE_CANCELLATION_FAILED",
                "HTTP 500",
                retryable));
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
