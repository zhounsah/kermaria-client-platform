using System.Net;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.SmokeTests;

public static class BillingV2NewSubscriptionTests
{
    public static async Task RunAsync()
    {
        VerifyPresetPlannerCreatesPrimaryUserItems();
        VerifyPresetPlannerKeepsSubscriptionScopedItemsUnassigned();
        VerifyPresetPlannerCreatesAdditionalUserEntitlement();
        VerifyPayPalPaymentAgreementUsesLegacySubscriptionId();
        VerifyStripePaymentAgreementUsesLegacySubscriptionId();
        VerifyBillingRailDoesNotInventProviderAgreement();
        VerifyProviderPriceMappingsMustCoverAllServicePrices();
        VerifyProviderPriceMappingsDetectMissingServicePrice();
        VerifyProviderPriceMappingsDetectAmbiguousServicePrice();
        VerifyLaunchReadinessIgnoresDemoSubscriptions();
        VerifyLaunchReadinessBlocksRealCustomerSubscriptions();
        VerifyLaunchReadinessCarriesBlockingRealSubscriptions();
        VerifyAuthoritativeCheckoutRequiresDedicatedFlag();
        VerifyAuthoritativeCheckoutRequiresHumanApproval();
        VerifyAuthoritativeCheckoutRequiresProviderOutbox();
        VerifyAuthoritativeCheckoutRequiresProviderExecutor();
        VerifyAuthoritativeCheckoutBlocksRealLegacySubscriptions();
        VerifyAuthoritativeCheckoutRequiresVerifiedLaunchSnapshot();
        VerifyAuthoritativeCheckoutBlocksIncompleteProviderMappings();
        VerifyAuthoritativeCheckoutBlocksWithoutV2DocumentIssuer();
        VerifyAuthoritativeCheckoutAllowsWhenV2DocumentIssuerReady();
        VerifyDocumentSnapshotPlannerPreservesContractedFinancials();
        VerifyDocumentSnapshotPlannerUsesPriceLockInsteadOfCurrentItems();
        VerifyProviderPriceMappingsExposeResolvedProviderIds();
        VerifyCheckoutPlannerRequiresReadiness();
        VerifyCheckoutPlannerBuildsLocalProviderPlan();
        VerifyProviderCheckoutCommandRequiresReadiness();
        VerifyProviderCheckoutCommandUsesStableIdempotency();
        VerifyProviderCheckoutCommandPayloadContainsResolvedProviderLines();
        VerifyStripeCheckoutRequestBuilderUsesResolvedPricesAndIdempotency();
        VerifyPayPalSubscriptionRequestBuilderUsesSinglePlanAndIdempotency();
        VerifyPayPalSubscriptionRequestBuilderRejectsMultiplePlans();
        VerifyProviderOutboxWorkerRequiresDedicatedFlag();
        VerifyProviderOutboxWorkerRequiresExecutor();
        VerifyProviderOutboxClaimPolicyClaimsPendingAndExpiredProcessing();
        VerifyProviderOutboxClaimPolicyBlocksActiveProcessing();
        VerifyProviderOutboxDispatchPolicyMarksSuccessProcessed();
        VerifyProviderOutboxDispatchPolicyRetriesFailures();
        VerifyProviderCheckoutSessionPolicyAcceptsSameReplay();
        VerifyProviderCheckoutSessionPolicyRejectsDivergentProviderIds();
        VerifyDisabledProviderExecutorCannotExecute();
        await VerifyStripeProviderExecutorUsesFakeHttpAndParsesCheckoutAsync();
        await VerifyPayPalProviderExecutorUsesFakeHttpAndParsesSubscriptionAsync();
        VerifyProviderInboundReturnLinksLocalSession();
        VerifyProviderInboundReturnDoesNotTriggerProvisioning();
        VerifyProviderInboundProcessedReturnDoesNotRetryProvisioning();
        VerifyProviderInboundEventIsIdempotentAfterSuccess();
        VerifyProviderInboundProcessedActivationCanRetryProvisioning();
        VerifyProviderInboundFailedEventCanBeRetried();
        VerifyProviderInboundActivationCanTriggerProvisioningRetry();
        VerifyProviderInboundProvisioningFailureKeepsProviderEventProcessed();
        VerifyProviderInboundProvisioningCancellationCanBubble();
        VerifyProviderInboundActivationDoesNotDowngradeActiveSubscription();
        VerifyProviderInboundActivationRequiresProviderSubscriptionId();
        VerifyProviderInboundRejectsDivergentProviderSubscriptionId();
        VerifyProviderReturnExtractorScopesToClient();
        VerifyStripeWebhookExtractorRequiresBillingV2Marker();
        VerifyStripeWebhookExtractorReadsV2CheckoutSession();
        VerifyPayPalWebhookExtractorRequiresV2CustomId();
        VerifyAuthoritativeCheckoutLocalGateRequiresAllFlags();
        VerifyAuthoritativeCheckoutLocalGateRequiresPersistentSql();
        VerifyAuthoritativeCheckoutLocalGateRequiresIdempotencyKey();
        VerifyAuthoritativeCheckoutIdempotencyFingerprintBindsRequest();
        VerifyAuthoritativeCheckoutCreatesContractualPriceLocks();
        VerifyPortalProjectionUsesV2ContractualPriceLock();
        VerifyPortalProjectionFallsBackToSubscriptionItemSnapshots();
        VerifyDownloadAccessScopeKeepsV2LegacyTargets();
        VerifyAuthoritativeCheckoutLocalGateAllowsReadyRequest();
        VerifyAdminReadinessRequiresNoRealLegacySubscriptions();
        VerifyAdminReadinessRequiresVerifiedLaunchSnapshot();
        VerifyAdminReadinessRequiresProviderReady();
        VerifyAdminReadinessExposesBlockingSubscriptions();
        VerifyAdminReadinessExposesOperationalLimitations();
        VerifyAdminReadinessBlocksFirstRealSubscriptionWithoutV2InvoicePath();
        VerifyAdminReadinessAllowsFirstRealSubscriptionWhenHardBlockersCleared();
    }

    private static void VerifyPresetPlannerCreatesPrimaryUserItems()
    {
        var plan = BillingV2NewSubscriptionPlanner.Plan(
            Session(),
            [
                Item("BASE-SERVICE", null, "subscription"),
                Item("STORAGE-PERSONAL", "32", "primary_user"),
                Item("BACKUP-PERSONAL", "32", "primary_user")
            ]);

        Ensure(
            plan.Users.Count == 1
            && plan.Users[0].IsPrimary
            && plan.Users[0].IdentityReference == "user-v2-new",
            "Un preset V2 avec items primary_user doit creer un utilisateur primaire rattache a la session.");
        Ensure(
            plan.Items.Count(item => item.UserId == plan.Users[0].Id) == 2,
            "Les items primary_user doivent pointer vers l'utilisateur primaire.");
    }

    private static void VerifyPresetPlannerKeepsSubscriptionScopedItemsUnassigned()
    {
        var plan = BillingV2NewSubscriptionPlanner.Plan(
            Session(),
            [Item("BASE-SERVICE", null, "subscription")]);

        Ensure(
            plan.Users.Count == 0
            && plan.Items.Count == 1
            && plan.Items[0].UserId is null
            && plan.Items[0].ScopeType == "subscription",
            "Les items scope subscription doivent rester au niveau abonnement.");
    }

    private static void VerifyPresetPlannerCreatesAdditionalUserEntitlement()
    {
        var plan = BillingV2NewSubscriptionPlanner.Plan(
            Session(),
            [
                Item("STORAGE-PERSONAL", "64", "primary_user"),
                Item("USER-ADDITIONAL", null, "additional_user")
            ]);

        var additionalUser = plan.Users.Single(user => !user.IsPrimary);
        Ensure(
            additionalUser.IdentityReference is null
            && additionalUser.DisplayName == "Utilisateur additionnel 1",
            "Un item additional_user doit creer une capacite utilisateur non assignee.");
        Ensure(
            plan.Items.Any(item =>
                item.UserId == additionalUser.Id
                && item.ScopeType == "user"
                && item.Source == "preset"),
            "L'item utilisateur additionnel doit rester traçable comme item issu du preset.");
    }

    private static void VerifyPayPalPaymentAgreementUsesLegacySubscriptionId()
    {
        var agreement = BillingV2ProviderAgreementPlanner.PlanFromLegacy(
            Subscription("paypal", paypalSubscriptionId: "I-PAYPAL-123"),
            new PayPalRuntimeConfiguration(
                PayPalMode.Sandbox,
                "client",
                "secret"),
            new StripeRuntimeConfiguration(StripeMode.Test));

        Ensure(
            agreement is not null
            && agreement.Provider == "paypal"
            && agreement.Environment == "sandbox"
            && agreement.ProviderSubscriptionId == "I-PAYPAL-123"
            && agreement.Status == "pending",
            "L'accord PayPal V2 local doit reprendre l'abonnement fournisseur legacy sans appel externe.");
    }

    private static void VerifyStripePaymentAgreementUsesLegacySubscriptionId()
    {
        var agreement = BillingV2ProviderAgreementPlanner.PlanFromLegacy(
            Subscription("stripe", stripeSubscriptionId: "sub_stripe_123"),
            new PayPalRuntimeConfiguration(
                PayPalMode.Sandbox,
                "client",
                "secret"),
            new StripeRuntimeConfiguration(StripeMode.Test));

        Ensure(
            agreement is not null
            && agreement.Provider == "stripe"
            && agreement.Environment == "test"
            && agreement.ProviderSubscriptionId == "sub_stripe_123"
            && agreement.Status == "pending",
            "L'accord Stripe V2 local doit reprendre l'abonnement fournisseur legacy sans appel externe.");
    }

    private static void VerifyBillingRailDoesNotInventProviderAgreement()
    {
        var agreement = BillingV2ProviderAgreementPlanner.PlanFromLegacy(
            Subscription("billing"),
            new PayPalRuntimeConfiguration(
                PayPalMode.Sandbox,
                "client",
                "secret"),
            new StripeRuntimeConfiguration(StripeMode.Test));

        Ensure(
            agreement is null,
            "Le rail billing local ne doit pas inventer d'accord fournisseur V2.");
    }

    private static void VerifyProviderPriceMappingsMustCoverAllServicePrices()
    {
        var status = BillingV2ProviderPriceMappingGate.Evaluate(
            ["price-storage", "price-vpn"],
            [
                new("price-storage", "stripe", "test", "price_storage_test"),
                new("price-vpn", "stripe", "test", "price_vpn_test")
            ],
            "stripe",
            "test");

        Ensure(
            status.Ready
            && status.MissingServicePriceIds.Count == 0
            && status.AmbiguousServicePriceIds.Count == 0,
            "Les mappings provider V2 doivent couvrir tous les prix de service requis.");
    }

    private static void VerifyProviderPriceMappingsDetectMissingServicePrice()
    {
        var status = BillingV2ProviderPriceMappingGate.Evaluate(
            ["price-storage", "price-vpn"],
            [new("price-storage", "stripe", "test", "price_storage_test")],
            "stripe",
            "test");

        Ensure(
            !status.Ready
            && status.MissingServicePriceIds.SequenceEqual(
                ["price-vpn"],
                StringComparer.Ordinal),
            "Un mapping provider V2 manquant doit bloquer le checkout V2 futur.");
    }

    private static void VerifyProviderPriceMappingsDetectAmbiguousServicePrice()
    {
        var status = BillingV2ProviderPriceMappingGate.Evaluate(
            ["price-storage"],
            [
                new("price-storage", "stripe", "test", "price_storage_test_a"),
                new("price-storage", "stripe", "test", "price_storage_test_b")
            ],
            "stripe",
            "test");

        Ensure(
            !status.Ready
            && status.AmbiguousServicePriceIds.SequenceEqual(
                ["price-storage"],
                StringComparer.Ordinal),
            "Un prix de service V2 avec plusieurs ids provider doit etre detecte comme ambigu.");
    }

    private static void VerifyLaunchReadinessIgnoresDemoSubscriptions()
    {
        var snapshot = BillingV2LaunchReadinessGate.Evaluate(
            realCustomerSubscriptionCount: 0,
            demoSubscriptionCount: 3);

        Ensure(
            snapshot.NoRealCustomerSubscriptions
            && snapshot.DemoSubscriptionCount == 3,
            "Les abonnements demo ne doivent pas etre traites comme des contrats clients reels a migrer.");
    }

    private static void VerifyLaunchReadinessBlocksRealCustomerSubscriptions()
    {
        var snapshot = BillingV2LaunchReadinessGate.Evaluate(
            realCustomerSubscriptionCount: 1,
            demoSubscriptionCount: 0);

        Ensure(
            !snapshot.NoRealCustomerSubscriptions,
            "Un abonnement client reel actif doit bloquer la strategie premier abonnement V2 sans migration.");
    }

    private static void VerifyLaunchReadinessCarriesBlockingRealSubscriptions()
    {
        var updatedAt = new DateTime(2026, 8, 13, 10, 30, 0, DateTimeKind.Utc);
        var snapshot = BillingV2LaunchReadinessGate.Evaluate(
            realCustomerSubscriptionCount: 1,
            demoSubscriptionCount: 2) with
        {
            BlockingRealSubscriptions =
            [
                new BillingV2BlockingLegacySubscription(
                    "subscription-real-1",
                    "active",
                    "customer-real-1",
                    "CLIENT-REAL",
                    "Client reel",
                    "offer-legacy-1",
                    updatedAt.AddMonths(-1),
                    updatedAt)
            ]
        };

        Ensure(
            !snapshot.NoRealCustomerSubscriptions
            && snapshot.BlockingRealSubscriptions.Count == 1
            && snapshot.BlockingRealSubscriptions[0].CustomerReference
                == "CLIENT-REAL",
            "La readiness doit conserver les abonnements reels bloquants pour revue humaine, sans compter les demos comme contrats reels.");
    }

    private static void VerifyAuthoritativeCheckoutRequiresDedicatedFlag()
    {
        var decision = BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: false,
                firstRealSubscriptionApproved: true),
            BillingV2LaunchReadinessGate.Evaluate(0, 0),
            ReadyProviderMappings());

        Ensure(
            !decision.Authorized
            && decision.ReasonCode == "BILLING_V2_CHECKOUT_FLAG_OFF",
            "Le checkout V2 autoritaire doit rester bloque par defaut.");
    }

    private static void VerifyAuthoritativeCheckoutRequiresHumanApproval()
    {
        var decision = BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: false),
            BillingV2LaunchReadinessGate.Evaluate(0, 2),
            ReadyProviderMappings());

        Ensure(
            !decision.Authorized
            && decision.ReasonCode
                == "BILLING_V2_FIRST_REAL_SUBSCRIPTION_NOT_APPROVED",
            "Le premier vrai abonnement V2 doit exiger une validation humaine explicite.");
    }

    private static void VerifyAuthoritativeCheckoutRequiresProviderOutbox()
    {
        var decision = BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerOutboxEnabled: false,
                providerExecutorEnabled: true),
            BillingV2LaunchReadinessGate.Evaluate(0, 0),
            ReadyProviderMappings());

        Ensure(
            !decision.Authorized
            && decision.ReasonCode == "BILLING_V2_PROVIDER_OUTBOX_FLAG_OFF",
            "Le checkout V2 autoritaire ne doit pas declarer ready si l'outbox provider est fermee.");
    }

    private static void VerifyAuthoritativeCheckoutRequiresProviderExecutor()
    {
        var decision = BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerOutboxEnabled: true,
                providerExecutorEnabled: false),
            BillingV2LaunchReadinessGate.Evaluate(0, 0),
            ReadyProviderMappings());

        Ensure(
            !decision.Authorized
            && decision.ReasonCode == "BILLING_V2_PROVIDER_EXECUTOR_FLAG_OFF",
            "Le checkout V2 autoritaire ne doit pas declarer ready sans executor provider capable de creer l'URL d'approbation.");
    }

    private static void VerifyAuthoritativeCheckoutBlocksRealLegacySubscriptions()
    {
        var decision = BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerOutboxEnabled: true,
                providerExecutorEnabled: true),
            BillingV2LaunchReadinessGate.Evaluate(1, 0),
            ReadyProviderMappings());

        Ensure(
            !decision.Authorized
            && decision.ReasonCode
                == "BILLING_V2_REAL_LEGACY_SUBSCRIPTIONS_PRESENT",
            "La presence d'un abonnement client reel actif doit bloquer la strategie premier abonnement V2.");
    }

    private static void VerifyAuthoritativeCheckoutRequiresVerifiedLaunchSnapshot()
    {
        var decision = BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerOutboxEnabled: true,
                providerExecutorEnabled: true),
            new BillingV2LaunchReadinessSnapshot(
                RealCustomerSubscriptionCount: 0,
                DemoSubscriptionCount: 0,
                VerifiedAgainstPersistentSql: false),
            ReadyProviderMappings());

        Ensure(
            !decision.Authorized
            && decision.ReasonCode
                == "BILLING_V2_LAUNCH_READINESS_UNVERIFIED",
            "Un checkout V2 autoritaire ne doit pas prendre un compteur a zero non verifie comme preuve d'absence de contrats reels.");
    }

    private static void VerifyAuthoritativeCheckoutBlocksIncompleteProviderMappings()
    {
        var decision = BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerOutboxEnabled: true,
                providerExecutorEnabled: true),
            BillingV2LaunchReadinessGate.Evaluate(0, 0),
            BillingV2ProviderPriceMappingGate.Evaluate(
                ["price-storage", "price-vpn"],
                [new("price-storage", "stripe", "test", "price_storage_test")],
                "stripe",
                "test"));

        Ensure(
            !decision.Authorized
            && decision.ReasonCode
                == "BILLING_V2_PROVIDER_PRICE_MAPPING_INCOMPLETE",
            "Le checkout V2 autoritaire doit etre fail-closed si un prix de service n'a pas exactement un mapping provider.");
    }

    private static void VerifyAuthoritativeCheckoutBlocksWithoutV2DocumentIssuer()
    {
        var decision = BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerOutboxEnabled: true,
                providerExecutorEnabled: true),
            BillingV2LaunchReadinessGate.Evaluate(0, 3),
            ReadyProviderMappings());

        Ensure(
            !decision.Authorized
            && decision.ReasonCode
                == "BILLING_V2_BPCE_INVOICE_AUTOMATION_NOT_READY",
            "Le checkout V2 autoritaire doit rester bloque tant qu'aucun chemin facture/document V2 teste n'existe.");
    }

    private static void VerifyAuthoritativeCheckoutAllowsWhenV2DocumentIssuerReady()
    {
        var decision = BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerOutboxEnabled: true,
                providerExecutorEnabled: true),
            BillingV2LaunchReadinessGate.Evaluate(0, 3),
            ReadyProviderMappings(),
            BillingV2DocumentReadinessStatus.ReadyForCheckout);

        Ensure(
            decision.Authorized
            && decision.ReasonCode
                == "BILLING_V2_AUTHORITATIVE_CHECKOUT_READY",
            "Le checkout V2 autoritaire ne doit devenir autorisable que si schema, providers, validation humaine et document issuer V2 sont prets.");
    }

    private static void VerifyDocumentSnapshotPlannerPreservesContractedFinancials()
    {
        var source = DocumentSource(
            discountBasisPoints: 1000,
            paymentMode: BillingV2PaymentModes.Monthly,
            items:
            [
                DocumentItem(
                    "item-storage",
                    "STORAGE-PERSONAL",
                    "Stockage personnel",
                    "32",
                    "32 Go",
                    amountCents: 10000,
                    quantity: 1),
                DocumentItem(
                    "item-vpn",
                    "VPN-ACCESS",
                    "Acces VPN",
                    "ESSENTIAL",
                    "Essentiel",
                    amountCents: 5000,
                    quantity: 1)
            ]);

        var plan = BillingV2DocumentSnapshotPlanner.Plan(source);

        Ensure(
            plan.SubtotalAmountCents == 13500
            && plan.DiscountAmountCents == 1500
            && plan.TaxAmountCents == 2700
            && plan.TotalAmountCents == 16200
            && plan.Lines.Count == 2
            && plan.Lines[0].GrossUnitAmountCents == 10000
            && plan.Lines[0].DiscountAmountCents == 1000
            && plan.Lines[0].NetLineAmountCents == 9000
            && plan.Lines[0].TaxAmountCents == 1800
            && plan.Lines[1].DiscountAmountCents == 500,
            "Le document V2 doit snapshotter prix unitaires, quantites, remise, taxes et total final sans relire le catalogue courant.");
    }

    private static void VerifyDocumentSnapshotPlannerUsesPriceLockInsteadOfCurrentItems()
    {
        var source = DocumentSource(
            discountBasisPoints: 0,
            paymentMode: BillingV2PaymentModes.Monthly,
            items:
            [
                DocumentItem(
                    "item-storage",
                    "STORAGE-PERSONAL",
                    "Stockage personnel",
                    "32",
                    "32 Go",
                    amountCents: 99000,
                    quantity: 1)
            ],
            priceLock: new BillingV2DocumentPriceLock(
                BillingV2PriceLockTypes.MonthlyRecurring,
                4200,
                "EUR",
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)));

        var plan = BillingV2DocumentSnapshotPlanner.Plan(source);

        Ensure(
            plan.SubtotalAmountCents == 4200
            && plan.Lines[0].GrossLineAmountCents == 99000
            && plan.Lines[0].DiscountAmountCents == 94800
            && plan.Lines[0].NetLineAmountCents == 4200,
            "Le document V2 doit utiliser le price lock contractuel lorsqu'il existe, pas le prix courant/snapshot item si celui-ci diverge.");
    }

    private static void VerifyProviderPriceMappingsExposeResolvedProviderIds()
    {
        var status = BillingV2ProviderPriceMappingGate.Evaluate(
            ["price-storage"],
            [new("price-storage", "paypal", "sandbox", "P-STORAGE-SANDBOX")],
            "paypal",
            "sandbox");

        Ensure(
            status.Ready
            && status.ResolvedMappings.Count == 1
            && status.ResolvedMappings[0].ProviderExternalId
                == "P-STORAGE-SANDBOX",
            "La readiness provider doit exposer l'id Stripe Price ou PayPal Plan resolu pour le futur checkout V2.");
    }

    private static void VerifyCheckoutPlannerRequiresReadiness()
    {
        try
        {
            _ = BillingV2CheckoutPlanner.Plan(
                BillingV2CheckoutReadinessGate.Evaluate(
                    V2Runtime(
                        authoritativeCheckoutEnabled: false,
                        firstRealSubscriptionApproved: true),
                    BillingV2LaunchReadinessGate.Evaluate(0, 0),
                    ReadyProviderMappings()),
                [Item("STORAGE-PERSONAL", "32", "primary_user")],
                PricingFor([Item("STORAGE-PERSONAL", "32", "primary_user")]));
            throw new InvalidOperationException(
                "Un plan checkout V2 a ete cree malgre une readiness bloquee.");
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(
                "BILLING_V2_CHECKOUT_FLAG_OFF",
                StringComparison.Ordinal))
        {
            // attendu : la planification reste fail-closed.
        }
    }

    private static void VerifyCheckoutPlannerBuildsLocalProviderPlan()
    {
        var items = new[]
        {
            Item("STORAGE-PERSONAL", "32", "primary_user"),
            Item("VPN-ACCESS", "ESSENTIAL", "primary_user")
        };
        var readiness = BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerOutboxEnabled: true,
                providerExecutorEnabled: true),
            BillingV2LaunchReadinessGate.Evaluate(0, 0),
            BillingV2ProviderPriceMappingGate.Evaluate(
                items.Select(item => item.ServicePriceId).ToArray(),
                items.Select((item, index) => new BillingV2ProviderPriceMapping(
                    item.ServicePriceId,
                    "stripe",
                    "test",
                    $"price_v2_test_{index}")).ToArray(),
                "stripe",
                "test"),
            BillingV2DocumentReadinessStatus.ReadyForCheckout);
        var plan = BillingV2CheckoutPlanner.Plan(
            readiness,
            items,
            PricingFor(items));

        Ensure(
            plan.Provider == "stripe"
            && plan.Environment == "test"
            && plan.Currency == "EUR"
            && plan.ProviderLines.Count == 2
            && plan.ProviderLines[0].ProviderExternalId == "price_v2_test_0"
            && plan.ProviderLines[1].ProviderExternalId == "price_v2_test_1"
            && plan.TotalDueNowCents == 200,
            "Le plan checkout V2 local doit reprendre les ids provider resolus sans appel Stripe/PayPal.");
    }

    private static void VerifyProviderCheckoutCommandRequiresReadiness()
    {
        try
        {
            _ = BillingV2ProviderCheckoutCommandPlanner.Plan(
                ProviderCheckoutRequest(
                    BillingV2CheckoutReadinessGate.Evaluate(
                        V2Runtime(
                            authoritativeCheckoutEnabled: false,
                            firstRealSubscriptionApproved: true),
                        BillingV2LaunchReadinessGate.Evaluate(0, 0),
                        ReadyProviderMappings())));
            throw new InvalidOperationException(
                "Une commande provider V2 a ete creee malgre une readiness bloquee.");
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(
                "BILLING_V2_CHECKOUT_FLAG_OFF",
                StringComparison.Ordinal))
        {
            // attendu : aucune commande provider sans readiness.
        }
    }

    private static void VerifyProviderCheckoutCommandUsesStableIdempotency()
    {
        var request = ProviderCheckoutRequest(ReadyCheckoutDecision());
        var first = BillingV2ProviderCheckoutCommandPlanner.Plan(request);
        var second = BillingV2ProviderCheckoutCommandPlanner.Plan(request);

        Ensure(
            first.IdempotencyKeyHash == second.IdempotencyKeyHash
            && first.IdempotencyKeyHash.Length == 64
            && first.EventType
                == "billing_v2.provider_checkout.create_requested",
            "Une commande provider V2 retry-safe doit produire la meme cle d'idempotence outbox.");
    }

    private static void VerifyProviderCheckoutCommandPayloadContainsResolvedProviderLines()
    {
        var command = BillingV2ProviderCheckoutCommandPlanner.Plan(
            ProviderCheckoutRequest(ReadyCheckoutDecision()));

        Ensure(
            command.AggregateType == "billing_v2_subscription"
            && command.AggregateId == "subscription-v2-new"
            && command.PayloadText.Contains(
                "\"provider\":\"stripe\"",
                StringComparison.Ordinal)
            && command.PayloadText.Contains(
                "\"providerExternalId\":\"price_storage_test\"",
                StringComparison.Ordinal)
            && command.PayloadText.Contains(
                "\"successUrl\":\"https://example.invalid/success\"",
                StringComparison.Ordinal),
            "La commande outbox provider V2 doit contenir le contexte local et les ids provider resolus.");
    }

    private static void VerifyStripeCheckoutRequestBuilderUsesResolvedPricesAndIdempotency()
    {
        var command = BillingV2ProviderCheckoutCommandPlanner.Plan(
            ProviderCheckoutRequest(ReadyCheckoutDecision()));
        var request = new BillingV2ProviderCheckoutExecutionRequest(
            "outbox-v2-stripe",
            command.IdempotencyKeyHash,
            command.PayloadText);
        var payload = BillingV2ProviderCheckoutPayload.Parse(
            command.PayloadText);

        var httpRequest = BillingV2StripeCheckoutRequestBuilder.Build(
            request,
            payload,
            "sk_test_fake_billing_v2");

        Ensure(
            httpRequest.Provider == "stripe"
            && httpRequest.Method == "POST"
            && httpRequest.Url.EndsWith(
                "/v1/checkout/sessions",
                StringComparison.Ordinal)
            && httpRequest.Headers["Idempotency-Key"]
                == command.IdempotencyKeyHash
            && httpRequest.Body.Contains(
                "mode=subscription",
                StringComparison.Ordinal)
            && httpRequest.Body.Contains(
                "line_items%5B0%5D%5Bprice%5D=price_storage_test",
                StringComparison.Ordinal)
            && httpRequest.Body.Contains(
                "line_items%5B1%5D%5Bprice%5D=price_vpn_test",
                StringComparison.Ordinal),
            "Le builder Stripe V2 doit utiliser les Price IDs resolus et une cle d'idempotence provider.");
    }

    private static void VerifyPayPalSubscriptionRequestBuilderUsesSinglePlanAndIdempotency()
    {
        var request = new BillingV2ProviderCheckoutExecutionRequest(
            "outbox-v2-paypal",
            "paypal-idempotency-key",
            string.Empty);
        var payload = new BillingV2ProviderCheckoutPayload(
            "subscription-v2-paypal",
            "customer-v2-paypal",
            "client@example.invalid",
            "paypal",
            "sandbox",
            "EUR",
            RecurringAmountCents: 1900,
            OneTimeAmountCents: 0,
            TotalDueNowCents: 1900,
            "https://example.invalid/paypal/success",
            "https://example.invalid/paypal/cancel",
            "correlation-paypal",
            [
                new(
                    "service-price-paypal",
                    "P-PAYPAL-V2-PLAN",
                    Quantity: 1,
                    AmountCents: 1900)
            ]);

        var httpRequest = BillingV2PayPalSubscriptionRequestBuilder.Build(
            request,
            payload,
            "https://api-m.sandbox.paypal.com",
            "paypal-access-token");

        Ensure(
            httpRequest.Provider == "paypal"
            && httpRequest.Url.EndsWith(
                "/v1/billing/subscriptions",
                StringComparison.Ordinal)
            && httpRequest.Headers["PayPal-Request-Id"]
                == "paypal-idempotency-key"
            && httpRequest.Body.Contains(
                "\"plan_id\":\"P-PAYPAL-V2-PLAN\"",
                StringComparison.Ordinal)
            && httpRequest.Body.Contains(
                "\"custom_id\":\"subscription-v2-paypal\"",
                StringComparison.Ordinal),
            "Le builder PayPal V2 doit utiliser un Plan ID resolu et une cle PayPal-Request-Id stable.");
    }

    private static void VerifyPayPalSubscriptionRequestBuilderRejectsMultiplePlans()
    {
        try
        {
            _ = BillingV2PayPalSubscriptionRequestBuilder.Build(
                new BillingV2ProviderCheckoutExecutionRequest(
                    "outbox-v2-paypal-invalid",
                    "paypal-idempotency-key-invalid",
                    string.Empty),
                new BillingV2ProviderCheckoutPayload(
                    "subscription-v2-paypal-invalid",
                    "customer-v2-paypal",
                    "client@example.invalid",
                    "paypal",
                    "sandbox",
                    "EUR",
                    RecurringAmountCents: 2900,
                    OneTimeAmountCents: 0,
                    TotalDueNowCents: 2900,
                    "https://example.invalid/success",
                    "https://example.invalid/cancel",
                    "correlation-paypal-invalid",
                    [
                        new(
                            "service-price-one",
                            "P-PAYPAL-V2-PLAN-ONE",
                            Quantity: 1,
                            AmountCents: 1900),
                        new(
                            "service-price-two",
                            "P-PAYPAL-V2-PLAN-TWO",
                            Quantity: 1,
                            AmountCents: 1000)
                    ]),
                "https://api-m.sandbox.paypal.com",
                "paypal-access-token");
            throw new InvalidOperationException(
                "PayPal V2 a accepte plusieurs Plan IDs dans une subscription.");
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(
                "exactly one provider plan id",
                StringComparison.Ordinal))
        {
            // attendu : le modele PayPal actuel ne supporte qu'un plan.
        }
    }

    private static void VerifyProviderOutboxWorkerRequiresDedicatedFlag()
    {
        var readiness = BillingV2ProviderOutboxGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true),
            persistentSqlAvailable: true,
            providerExecutorConfigured: true);

        Ensure(
            !readiness.CanDispatch
            && readiness.ReasonCode == "BILLING_V2_PROVIDER_OUTBOX_FLAG_OFF",
            "Le worker outbox provider V2 doit rester desactive par defaut.");
    }

    private static void VerifyProviderOutboxWorkerRequiresExecutor()
    {
        var readiness = BillingV2ProviderOutboxGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerOutboxEnabled: true),
            persistentSqlAvailable: true,
            providerExecutorConfigured: false);

        Ensure(
            !readiness.CanDispatch
            && readiness.ReasonCode
                == "BILLING_V2_PROVIDER_OUTBOX_EXECUTOR_NOT_CONFIGURED",
            "Le worker outbox provider V2 ne doit rien executer sans executor Stripe/PayPal explicite.");
    }

    private static void VerifyProviderOutboxClaimPolicyClaimsPendingAndExpiredProcessing()
    {
        var now = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

        Ensure(
            BillingV2ProviderOutboxClaimPolicy.CanClaim(
                "pending",
                now.AddSeconds(-1),
                now)
            && BillingV2ProviderOutboxClaimPolicy.CanClaim(
                BillingV2ProviderOutboxClaimPolicy.ProcessingStatus,
                now.AddSeconds(-1),
                now),
            "L'outbox provider V2 doit pouvoir revendiquer un evenement pending ou un processing expire.");
    }

    private static void VerifyProviderOutboxClaimPolicyBlocksActiveProcessing()
    {
        var now = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

        Ensure(
            !BillingV2ProviderOutboxClaimPolicy.CanClaim(
                BillingV2ProviderOutboxClaimPolicy.ProcessingStatus,
                now.AddMinutes(4),
                now)
            && !BillingV2ProviderOutboxClaimPolicy.CanClaim(
                "processed",
                now.AddSeconds(-1),
                now),
            "L'outbox provider V2 ne doit pas revendiquer un evenement deja en cours non expire ou deja traite.");
    }

    private static void VerifyProviderOutboxDispatchPolicyMarksSuccessProcessed()
    {
        var update = BillingV2ProviderOutboxDispatchPolicy.Resolve(
            new BillingV2ProviderCheckoutExecutionResult(
                true,
                "BILLING_V2_PROVIDER_CHECKOUT_CREATED",
                "cs_v2_test",
                ProviderSubscriptionId: null,
                "https://checkout.example.invalid",
                ErrorMessage: null),
            currentRetryCount: 0);

        Ensure(
            update.Status == "processed"
            && update.RetryDelayMinutes == 0
            && update.LastError is null,
            "Un checkout provider V2 reussi doit marquer l'evenement outbox processed.");
    }

    private static void VerifyProviderOutboxDispatchPolicyRetriesFailures()
    {
        var update = BillingV2ProviderOutboxDispatchPolicy.Resolve(
            new BillingV2ProviderCheckoutExecutionResult(
                false,
                "BILLING_V2_STRIPE_REQUEST_FAILED",
                ProviderCheckoutId: null,
                ProviderSubscriptionId: null,
                ApprovalUrl: null,
                ErrorMessage: "503 temporaire"),
            currentRetryCount: 2);

        Ensure(
            update.Status == "pending"
            && update.RetryDelayMinutes == 15
            && update.LastError?.Contains(
                "BILLING_V2_STRIPE_REQUEST_FAILED",
                StringComparison.Ordinal) == true,
            "Un echec provider V2 doit rester retryable et conserver le diagnostic.");
    }

    private static void VerifyProviderCheckoutSessionPolicyAcceptsSameReplay()
    {
        var payload = ProviderCheckoutPayload(
            subscriptionId: "subscription-v2-replay",
            provider: "stripe");
        var persisted = new BillingV2ProviderCheckoutSessionSnapshot(
            payload.SubscriptionId,
            payload.Provider,
            payload.Environment,
            ProviderCheckoutId: "cs_v2_replay",
            ProviderSubscriptionId: null,
            ApprovalUrl: "https://checkout.example.invalid/replay");
        var result = new BillingV2ProviderCheckoutExecutionResult(
            true,
            "BILLING_V2_PROVIDER_CHECKOUT_CREATED",
            "cs_v2_replay",
            ProviderSubscriptionId: null,
            "https://checkout.example.invalid/replay",
            ErrorMessage: null);

        var consistency = BillingV2ProviderCheckoutSessionPolicy.Evaluate(
            persisted,
            payload,
            result);

        Ensure(
            consistency.IsConsistent,
            "Un retry provider V2 strictement identique doit reutiliser la session locale existante.");
    }

    private static void VerifyProviderCheckoutSessionPolicyRejectsDivergentProviderIds()
    {
        var stripePayload = ProviderCheckoutPayload(
            subscriptionId: "subscription-v2-stripe-conflict",
            provider: "stripe");
        var stripeConflict = BillingV2ProviderCheckoutSessionPolicy.Evaluate(
            new BillingV2ProviderCheckoutSessionSnapshot(
                stripePayload.SubscriptionId,
                stripePayload.Provider,
                stripePayload.Environment,
                ProviderCheckoutId: "cs_v2_original",
                ProviderSubscriptionId: null,
                ApprovalUrl: "https://checkout.example.invalid/original"),
            stripePayload,
            new BillingV2ProviderCheckoutExecutionResult(
                true,
                "BILLING_V2_PROVIDER_CHECKOUT_CREATED",
                "cs_v2_changed",
                ProviderSubscriptionId: null,
                "https://checkout.example.invalid/original",
                ErrorMessage: null));

        var paypalPayload = ProviderCheckoutPayload(
            subscriptionId: "subscription-v2-paypal-conflict",
            provider: "paypal");
        var paypalConflict = BillingV2ProviderCheckoutSessionPolicy.Evaluate(
            new BillingV2ProviderCheckoutSessionSnapshot(
                paypalPayload.SubscriptionId,
                paypalPayload.Provider,
                paypalPayload.Environment,
                ProviderCheckoutId: null,
                ProviderSubscriptionId: "I-PAYPAL-ORIGINAL",
                ApprovalUrl: "https://paypal.example.invalid/original"),
            paypalPayload,
            new BillingV2ProviderCheckoutExecutionResult(
                true,
                "BILLING_V2_PROVIDER_CHECKOUT_CREATED",
                ProviderCheckoutId: null,
                ProviderSubscriptionId: "I-PAYPAL-CHANGED",
                "https://paypal.example.invalid/original",
                ErrorMessage: null));

        Ensure(
            !stripeConflict.IsConsistent
            && !paypalConflict.IsConsistent
            && stripeConflict.ReasonCode
                == BillingV2ProviderCheckoutSessionPolicy.ConflictReasonCode
            && paypalConflict.ReasonCode
                == BillingV2ProviderCheckoutSessionPolicy.ConflictReasonCode,
            "Un retry provider V2 qui change un identifiant Stripe ou PayPal doit etre refuse explicitement.");
    }

    private static void VerifyDisabledProviderExecutorCannotExecute()
    {
        Ensure(
            !DisabledBillingV2ProviderCheckoutExecutor.Instance.CanExecute,
            "L'executor provider V2 par defaut doit etre explicitement disabled.");
    }

    private static async Task VerifyStripeProviderExecutorUsesFakeHttpAndParsesCheckoutAsync()
    {
        var command = BillingV2ProviderCheckoutCommandPlanner.Plan(
            ProviderCheckoutRequest(ReadyCheckoutDecision()));
        var handler = new RecordingHttpMessageHandler(request =>
        {
            Ensure(
                request.Method == HttpMethod.Post
                && request.RequestUri is not null
                && request.RequestUri.AbsoluteUri
                    == "https://api.stripe.com/v1/checkout/sessions",
                "L'executor Stripe V2 doit appeler l'endpoint Checkout Session attendu.");
            Ensure(
                request.Headers.Authorization?.Scheme == "Bearer"
                && request.Headers.Authorization.Parameter == "sk_test_fake_billing_v2",
                "L'executor Stripe V2 doit porter la cle secrete Stripe uniquement dans l'en-tete Authorization.");
            Ensure(
                request.Headers.TryGetValues("Idempotency-Key", out var values)
                && values.Single() == command.IdempotencyKeyHash,
                "L'executor Stripe V2 doit envoyer la cle d'idempotence stable.");

            var body = request.Content is null
                ? string.Empty
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            Ensure(
                body.Contains(
                    "line_items%5B0%5D%5Bprice%5D=price_storage_test",
                    StringComparison.Ordinal)
                && body.Contains(
                    "line_items%5B1%5D%5Bprice%5D=price_vpn_test",
                    StringComparison.Ordinal),
                "L'executor Stripe V2 doit utiliser les Price IDs provider resolus dans la payload envoyee.");

            return JsonResponse(
                """{"id":"cs_v2_fake","url":"https://checkout.stripe.test/session"}""");
        });
        var executor = new BillingV2ProviderCheckoutExecutor(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerExecutorEnabled: true),
            new PayPalRuntimeConfiguration(PayPalMode.Sandbox, null, null),
            new StripeRuntimeConfiguration(
                StripeMode.Test,
                "sk_test_fake_billing_v2"),
            new FakeHttpClientFactory(handler));

        var result = await executor.ExecuteAsync(
            new BillingV2ProviderCheckoutExecutionRequest(
                "outbox-v2-stripe-http",
                command.IdempotencyKeyHash,
                command.PayloadText),
            CancellationToken.None);

        Ensure(
            result.Succeeded
            && result.ProviderCheckoutId == "cs_v2_fake"
            && result.ProviderSubscriptionId is null
            && result.ApprovalUrl == "https://checkout.stripe.test/session"
            && handler.Requests.Count == 1,
            "L'executor Stripe V2 doit parser la session checkout et l'URL d'approbation sans abonnement fournisseur local invente.");
    }

    private static async Task VerifyPayPalProviderExecutorUsesFakeHttpAndParsesSubscriptionAsync()
    {
        var payload = new BillingV2ProviderCheckoutPayload(
            "subscription-v2-paypal-http",
            "customer-v2-paypal-http",
            "client@example.invalid",
            "paypal",
            "sandbox",
            "EUR",
            RecurringAmountCents: 1900,
            OneTimeAmountCents: 0,
            TotalDueNowCents: 1900,
            "https://example.invalid/paypal/success",
            "https://example.invalid/paypal/cancel",
            "correlation-paypal-http",
            [
                new(
                    "service-price-paypal-http",
                    "P-PAYPAL-V2-PLAN",
                    Quantity: 1,
                    AmountCents: 1900)
            ]);
        var payloadText = JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/v1/oauth2/token")
            {
                Ensure(
                    request.Method == HttpMethod.Post
                    && request.Headers.Authorization?.Scheme == "Basic",
                    "L'executor PayPal V2 doit demander un access token OAuth en Basic auth.");
                return JsonResponse("""{"access_token":"paypal-token-fake"}""");
            }

            Ensure(
                request.Method == HttpMethod.Post
                && request.RequestUri is not null
                && request.RequestUri.AbsoluteUri
                    == "https://api-m.sandbox.paypal.com/v1/billing/subscriptions",
                "L'executor PayPal V2 doit appeler l'endpoint subscriptions attendu.");
            Ensure(
                request.Headers.Authorization?.Scheme == "Bearer"
                && request.Headers.Authorization.Parameter == "paypal-token-fake",
                "L'executor PayPal V2 doit reutiliser le token OAuth pour creer la subscription.");
            Ensure(
                request.Headers.TryGetValues("PayPal-Request-Id", out var values)
                && values.Single() == "paypal-http-idempotency",
                "L'executor PayPal V2 doit envoyer une cle PayPal-Request-Id stable.");

            var body = request.Content is null
                ? string.Empty
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            Ensure(
                body.Contains(
                    "\"plan_id\":\"P-PAYPAL-V2-PLAN\"",
                    StringComparison.Ordinal)
                && body.Contains(
                    "\"custom_id\":\"subscription-v2-paypal-http\"",
                    StringComparison.Ordinal),
                "L'executor PayPal V2 doit envoyer le Plan ID resolu et l'id local de subscription.");

            return JsonResponse(
                """
                {
                  "id":"I-PAYPAL-V2-FAKE",
                  "links":[
                    {
                      "rel":"approve",
                      "href":"https://paypal.test/approve"
                    }
                  ]
                }
                """);
        });
        var executor = new BillingV2ProviderCheckoutExecutor(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerExecutorEnabled: true),
            new PayPalRuntimeConfiguration(
                PayPalMode.Sandbox,
                "paypal-client-id",
                "paypal-client-secret"),
            new StripeRuntimeConfiguration(StripeMode.Disabled),
            new FakeHttpClientFactory(handler));

        var result = await executor.ExecuteAsync(
            new BillingV2ProviderCheckoutExecutionRequest(
                "outbox-v2-paypal-http",
                "paypal-http-idempotency",
                payloadText),
            CancellationToken.None);

        Ensure(
            result.Succeeded
            && result.ProviderCheckoutId is null
            && result.ProviderSubscriptionId == "I-PAYPAL-V2-FAKE"
            && result.ApprovalUrl == "https://paypal.test/approve"
            && handler.Requests.Count == 2,
            "L'executor PayPal V2 doit parser l'abonnement provider et l'URL d'approbation apres OAuth.");
    }

    private static void VerifyProviderInboundReturnLinksLocalSession()
    {
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderInboundEvent(
                "billing_v2.checkout_returned",
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_123"),
            ProviderLocalState(
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: null));

        Ensure(
            plan.CanApply
            && !plan.AlreadyApplied
            && plan.CheckoutStatus == "approved"
            && plan.AgreementStatus == "pending"
            && plan.SubscriptionStatus is null
            && plan.ProviderSubscriptionId == "sub_v2_123",
            "Un retour provider V2 doit rattacher la subscription provider a la session checkout locale sans activer le contrat.");
    }

    private static void VerifyProviderInboundReturnDoesNotTriggerProvisioning()
    {
        var state = ProviderLocalState(
            providerCheckoutId: "cs_v2_123",
            providerSubscriptionId: null);
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderInboundEvent(
                "billing_v2.checkout_returned",
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_123"),
            state);

        Ensure(
            !BillingV2ProviderInboundProvisioningPolicy.ShouldAttempt(
                plan,
                state),
            "Un retour checkout V2 ne doit pas declencher le provisioning avant activation provider.");
    }

    private static void VerifyProviderInboundProcessedReturnDoesNotRetryProvisioning()
    {
        Ensure(
            !BillingV2ProviderInboundProvisioningPolicy
                .ShouldAttemptProcessedReplay(
                    "BILLING_V2_PROVIDER_CHECKOUT_RETURN_RECORDED"),
            "Un replay de retour checkout V2 deja traite ne doit pas retenter le provisioning.");
    }

    private static void VerifyProviderInboundEventIsIdempotentAfterSuccess()
    {
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderInboundEvent(
                "checkout.session.completed",
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_123"),
            ProviderLocalState(
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_123",
                checkoutStatus: "completed",
                agreementStatus: "active",
                subscriptionStatus: "active"));

        // Phase 2 : `checkout.session.completed` n'active plus directement.
        // Il reste idempotent (rien a reappliquer), mais son code de raison est
        // desormais un signal declencheur de relecture Stripe, pas une
        // activation.
        Ensure(
            plan.CanApply
            && plan.AlreadyApplied
            && plan.ReasonCode
                == "BILLING_V2_PROVIDER_CHECKOUT_COMPLETED_SIGNAL",
            "Un webhook provider V2 deja applique doit rester idempotent et ne pas recreer d'accord local.");
        Ensure(
            plan.SubscriptionStatus is null,
            "Phase 2 : ce webhook ne doit plus porter d'activation d'abonnement.");
    }

    private static void VerifyProviderInboundProcessedActivationCanRetryProvisioning()
    {
        Ensure(
            BillingV2ProviderInboundProvisioningPolicy
                .ShouldAttemptProcessedReplay(
                    "BILLING_V2_PROVIDER_SUBSCRIPTION_ACTIVATED"),
            "Un replay d'activation provider V2 deja traite doit pouvoir retenter le provisioning idempotent.");
    }

    private static void VerifyProviderInboundFailedEventCanBeRetried()
    {
        var firstPlan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderInboundEvent(
                "unsupported.provider.event",
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_123"),
            ProviderLocalState(
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: null));
        var retryPlan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderInboundEvent(
                "billing_v2.subscription_activated",
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_123"),
            ProviderLocalState(
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: null));

        Ensure(
            !firstPlan.CanApply
            && firstPlan.ReasonCode == "BILLING_V2_PROVIDER_EVENT_UNSUPPORTED"
            && retryPlan.CanApply
            && !retryPlan.AlreadyApplied
            && retryPlan.SubscriptionStatus == "active",
            "Un evenement provider V2 en echec doit pouvoir etre rejoue avec un payload exploitable.");
    }

    private static void VerifyProviderInboundActivationCanTriggerProvisioningRetry()
    {
        var state = ProviderLocalState(
            providerCheckoutId: "cs_v2_123",
            providerSubscriptionId: "sub_v2_123",
            checkoutStatus: "completed",
            agreementStatus: "active",
            subscriptionStatus: "active");
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderInboundEvent(
                "checkout.session.completed",
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_123"),
            state);

        // Phase 2 : le rejeu reste retentable, mais il passe desormais par la
        // relecture Stripe. Le provisioning ne peut plus etre declenche
        // directement par le signal ; il suit la verification de settlement.
        Ensure(
            plan.AlreadyApplied
            && BillingV2ProviderInboundProvisioningPolicy.ShouldVerifySettlement(
                "stripe",
                plan.ReasonCode),
            "Un retry d'activation provider V2 deja applique doit pouvoir retenter le provisioning idempotent.");
        Ensure(
            !BillingV2ProviderInboundProvisioningPolicy.ShouldAttempt(
                plan,
                state),
            "Phase 2 : le provisioning ne part plus directement du signal brut.");
    }

    private static void VerifyProviderInboundProvisioningFailureKeepsProviderEventProcessed()
    {
        Ensure(
            BillingV2ProviderInboundProvisioningFailurePolicy
                .ShouldKeepProviderEventProcessed(
                    new InvalidOperationException("gate failed"))
            && BillingV2ProviderInboundProvisioningFailurePolicy
                .ShouldKeepProviderEventProcessed(
                    new Exception("ad provider unavailable")),
            "Une erreur provisioning post-commit ne doit pas faire echouer retrospectivement l'evenement provider V2 deja traite.");
    }

    private static void VerifyProviderInboundProvisioningCancellationCanBubble()
    {
        Ensure(
            !BillingV2ProviderInboundProvisioningFailurePolicy
                .ShouldKeepProviderEventProcessed(
                    new OperationCanceledException()),
            "Une annulation du traitement provider V2 doit rester propagable au lieu d'etre masquee.");
    }

    private static void VerifyProviderInboundActivationDoesNotDowngradeActiveSubscription()
    {
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderInboundEvent(
                "billing_v2.checkout_returned",
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_123"),
            ProviderLocalState(
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_123",
                checkoutStatus: "completed",
                agreementStatus: "active",
                subscriptionStatus: "active"));

        Ensure(
            plan.CanApply
            && plan.CheckoutStatus == "completed"
            && plan.AgreementStatus == "active"
            && plan.SubscriptionStatus is null,
            "Un retour tardif ne doit pas degrader une session ou un accord provider V2 deja actifs.");
    }

    private static void VerifyProviderInboundActivationRequiresProviderSubscriptionId()
    {
        var plan = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderInboundEvent(
                "billing_v2.subscription_activated",
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: null),
            ProviderLocalState(
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: null));

        Ensure(
            !plan.CanApply
            && plan.ReasonCode
                == "BILLING_V2_PROVIDER_SUBSCRIPTION_ID_MISSING",
            "Un webhook d'activation V2 sans subscription provider ne doit pas inventer d'accord local.");
    }

    private static void VerifyProviderInboundRejectsDivergentProviderSubscriptionId()
    {
        var subscriptionConflict = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderInboundEvent(
                "checkout.session.completed",
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_changed"),
            ProviderLocalState(
                providerCheckoutId: "cs_v2_123",
                providerSubscriptionId: "sub_v2_original"));
        var checkoutConflict = BillingV2ProviderInboundEventPlanner.Plan(
            ProviderInboundEvent(
                "billing_v2.checkout_returned",
                providerCheckoutId: "cs_v2_changed",
                providerSubscriptionId: "sub_v2_123"),
            ProviderLocalState(
                providerCheckoutId: "cs_v2_original",
                providerSubscriptionId: "sub_v2_123"));

        Ensure(
            !subscriptionConflict.CanApply
            && subscriptionConflict.ReasonCode
                == "BILLING_V2_PROVIDER_SUBSCRIPTION_ID_CONFLICT"
            && !checkoutConflict.CanApply
            && checkoutConflict.ReasonCode
                == "BILLING_V2_PROVIDER_CHECKOUT_ID_CONFLICT",
            "Un event provider V2 avec IDs provider contradictoires doit etre refuse et ne pas activer l'abonnement local.");
    }

    private static void VerifyProviderReturnExtractorScopesToClient()
    {
        var request = BillingV2ProviderInboundEventExtractor
            .CreateProviderReturn(
                "stripe",
                "test",
                "cs_v2_return",
                providerSubscriptionId: null,
                payloadText: "https://example.invalid/return",
                expectedCustomerId: "customer-v2-new");

        Ensure(
            request.Provider == "stripe"
            && request.Environment == "test"
            && request.ProviderEventId
                == "return:stripe:test:cs_v2_return"
            && request.EventType == "billing_v2.checkout_returned"
            && request.ProviderCheckoutId == "cs_v2_return"
            && request.ExpectedCustomerId == "customer-v2-new",
            "Un retour portail V2 doit produire un evenement stable et filtre par client.");
    }

    private static void VerifyStripeWebhookExtractorRequiresBillingV2Marker()
    {
        var legacyLike = BillingV2ProviderInboundEventExtractor
            .TryCreateStripeWebhook(
                new StripeWebhookEventPayload(
                    "evt_legacy_checkout",
                    "checkout.session.completed",
                    "cs_legacy",
                    """
                    {"data":{"object":{"id":"cs_legacy","subscription":"sub_legacy"}}}
                    """),
                "test");

        Ensure(
            legacyLike is null,
            "Un webhook Stripe sans metadata billing_v2_subscription_id ne doit pas etre detourne du legacy.");
    }

    private static void VerifyStripeWebhookExtractorReadsV2CheckoutSession()
    {
        var request = BillingV2ProviderInboundEventExtractor
            .TryCreateStripeWebhook(
                new StripeWebhookEventPayload(
                    "evt_v2_checkout",
                    "checkout.session.completed",
                    "cs_v2_123",
                    """
                    {
                      "data": {
                        "object": {
                          "id": "cs_v2_123",
                          "subscription": "sub_v2_123",
                          "metadata": {
                            "billing_v2_subscription_id": "subscription-v2-new"
                          }
                        }
                      }
                    }
                    """),
                "test");

        Ensure(
            request is not null
            && request.Provider == "stripe"
            && request.ProviderCheckoutId == "cs_v2_123"
            && request.ProviderSubscriptionId == "sub_v2_123"
            && request.LocalSubscriptionId == "subscription-v2-new"
            && request.ExpectedCustomerId is null,
            "Un webhook Stripe V2 marque doit extraire la session checkout et la subscription provider sans session client.");
    }

    private static void VerifyPayPalWebhookExtractorRequiresV2CustomId()
    {
        var legacyLike = BillingV2ProviderInboundEventExtractor
            .TryCreatePayPalWebhook(
                new PayPalWebhookEventPayload(
                    "WH-LEGACY",
                    "BILLING.SUBSCRIPTION.ACTIVATED",
                    "I-LEGACY",
                    """
                    {"resource":{"id":"I-LEGACY"}}
                    """),
                "sandbox");
        var v2 = BillingV2ProviderInboundEventExtractor.TryCreatePayPalWebhook(
            new PayPalWebhookEventPayload(
                "WH-V2",
                "BILLING.SUBSCRIPTION.ACTIVATED",
                "I-V2",
                """
                {"resource":{"id":"I-V2","custom_id":"subscription-v2-new"}}
                """),
            "sandbox");

        Ensure(
            legacyLike is null
            && v2 is not null
            && v2.Provider == "paypal"
            && v2.ProviderSubscriptionId == "I-V2"
            && v2.LocalSubscriptionId == "subscription-v2-new",
            "Un webhook PayPal ne doit entrer dans le chemin V2 que si custom_id porte l'abonnement local V2.");
    }

    private static void VerifyAuthoritativeCheckoutLocalGateRequiresAllFlags()
    {
        var decision = BillingV2AuthoritativeCheckoutGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true),
            persistentSqlAvailable: true,
            idempotencyKey: "checkout-request-1");

        Ensure(
            !decision.Authorized
            && decision.ReasonCode == "BILLING_V2_NEW_SUBSCRIPTIONS_FLAG_OFF",
            "Le checkout V2 autoritaire local doit exiger le flag nouveaux abonnements en plus du flag checkout.");
    }

    private static void VerifyAuthoritativeCheckoutLocalGateRequiresPersistentSql()
    {
        var decision = BillingV2AuthoritativeCheckoutGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                newSubscriptionsEnabled: true),
            persistentSqlAvailable: false,
            idempotencyKey: "checkout-request-1");

        Ensure(
            !decision.Authorized
            && decision.ReasonCode == "BILLING_V2_AUTHORITATIVE_CHECKOUT_NO_SQL",
            "Le checkout V2 autoritaire local doit refuser le mode in-memory pour ne pas perdre l'idempotence.");
    }

    private static void VerifyAuthoritativeCheckoutLocalGateRequiresIdempotencyKey()
    {
        var decision = BillingV2AuthoritativeCheckoutGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                newSubscriptionsEnabled: true),
            persistentSqlAvailable: true,
            idempotencyKey: " ");

        Ensure(
            !decision.Authorized
            && decision.ReasonCode
                == "BILLING_V2_AUTHORITATIVE_CHECKOUT_IDEMPOTENCY_REQUIRED",
            "Le checkout V2 autoritaire local doit exiger une cle d'idempotence applicative.");
    }

    private static void VerifyAuthoritativeCheckoutIdempotencyFingerprintBindsRequest()
    {
        var first = BillingV2AuthoritativeCheckoutIdempotencyPolicy
            .ComputeRequestFingerprintHash(
                "customer-v2",
                "user-v2",
                "stripe",
                "test",
                "offer-a");
        var same = BillingV2AuthoritativeCheckoutIdempotencyPolicy
            .ComputeRequestFingerprintHash(
                "customer-v2",
                "user-v2",
                "stripe",
                "test",
                "offer-a");
        var differentOffer = BillingV2AuthoritativeCheckoutIdempotencyPolicy
            .ComputeRequestFingerprintHash(
                "customer-v2",
                "user-v2",
                "stripe",
                "test",
                "offer-b");
        var differentProvider = BillingV2AuthoritativeCheckoutIdempotencyPolicy
            .ComputeRequestFingerprintHash(
                "customer-v2",
                "user-v2",
                "paypal",
                "sandbox",
                "offer-a");

        Ensure(
            first.Length == 64
            && BillingV2AuthoritativeCheckoutIdempotencyPolicy
                .MatchesRequestFingerprint(first, same)
            && !BillingV2AuthoritativeCheckoutIdempotencyPolicy
                .MatchesRequestFingerprint(first, differentOffer)
            && !BillingV2AuthoritativeCheckoutIdempotencyPolicy
                .MatchesRequestFingerprint(first, differentProvider),
            "Une cle d'idempotence checkout V2 ne doit etre rejouable que pour la meme intention metier.");
    }

    private static void VerifyAuthoritativeCheckoutCreatesContractualPriceLocks()
    {
        var now = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
        var monthly = BillingV2AuthoritativeCheckoutPriceLockPolicy.Plan(
            "legacy-offer-monthly",
            BillingV2PaymentModes.Monthly,
            commitmentMonths: 12,
            PricingResult(payableRecurringAmountCents: 3900),
            now);
        var upfront = BillingV2AuthoritativeCheckoutPriceLockPolicy.Plan(
            "legacy-offer-upfront",
            BillingV2PaymentModes.Upfront,
            commitmentMonths: 12,
            PricingResult(
                payableRecurringAmountCents: 0,
                upfrontRecurringAmountCents: 42000),
            now);

        Ensure(
            monthly.LockType == BillingV2PriceLockTypes.MonthlyRecurring
            && monthly.AmountCents == 3900
            && monthly.EffectiveUntilUtc == now.AddMonths(12)
            && monthly.SourceLegacyOfferId == "legacy-offer-monthly"
            && monthly.Reason
                == BillingV2AuthoritativeCheckoutPriceLockPolicy.CheckoutReason
            && upfront.LockType == BillingV2PriceLockTypes.UpfrontPrepaid
            && upfront.AmountCents == 42000
            && upfront.EffectiveUntilUtc == now.AddMonths(12)
            && upfront.SourceLegacyOfferId == "legacy-offer-upfront",
            "Le checkout V2 autoritaire doit creer un price lock contractuel mensuel ou upfront depuis le prix calcule a la souscription.");
    }

    private static void VerifyPortalProjectionUsesV2ContractualPriceLock()
    {
        var summary = BillingV2PortalSubscriptionProjector.Project(
            PortalSubscriptionRow(
                activeLockAmountCents: 3900,
                activeLockType: BillingV2PriceLockTypes.MonthlyRecurring,
                recurringDiscountEligibleCents: 9999,
                provider: "stripe",
                providerSubscriptionId: "sub_v2_portal"));

        Ensure(
            summary.BillingSystem == "billing_v2"
            && summary.Id == "sub-v2-portal"
            && summary.CommercialOfferId == "legacy-offer-v2"
            && summary.PriceAmountCents == 3900
            && summary.StripeSubscriptionId == "sub_v2_portal"
            && summary.PayPalSubscriptionId is null
            && summary.BillingIntervalMonths == 1
            && summary.PaymentMode == BillingV2PaymentModes.Monthly,
            "La projection portail V2 doit afficher le lock contractuel et les identifiants provider sans recreer une subscription legacy.");
    }

    private static void VerifyPortalProjectionFallsBackToSubscriptionItemSnapshots()
    {
        var summary = BillingV2PortalSubscriptionProjector.Project(
            PortalSubscriptionRow(
                paymentMode: BillingV2PaymentModes.Upfront,
                commitmentMonths: 12,
                discountBasisPoints: 1000,
                recurringDiscountEligibleCents: 1000,
                recurringNonDiscountableCents: 200,
                oneTimeCents: 500,
                activeLockAmountCents: null,
                activeLockType: null,
                provider: "paypal",
                providerSubscriptionId: "I-V2-PORTAL"));

        Ensure(
            summary.BillingSystem == "billing_v2"
            && summary.PriceAmountCents == 13200
            && summary.SetupFeeAmountCents == 500
            && summary.BillingIntervalMonths == 12
            && summary.CommitmentMonths == 12
            && summary.PayPalSubscriptionId == "I-V2-PORTAL"
            && summary.StripeSubscriptionId is null,
            "Si le lock n'est pas present, la projection portail V2 doit utiliser uniquement les snapshots d'items deja materialises.");
    }

    private static void VerifyDownloadAccessScopeKeepsV2LegacyTargets()
    {
        var scope = BillingV2DownloadAccessScopePolicy.Create(
            [" pack-dossier-securise ", "pack-dossier-securise", null],
            [" PACK-DOSSIER-1M-MENS ", ""],
            ["GG_VPN", "gg_vpn", " GG_RDS "]);

        Ensure(
            scope.PublicPackCodes.SequenceEqual(["pack-dossier-securise"])
            && scope.OfferExternalReferences.SequenceEqual(["PACK-DOSSIER-1M-MENS"])
            && scope.ProvisioningGroups.Count == 2
            && scope.ProvisioningGroups.Contains("GG_VPN")
            && scope.ProvisioningGroups.Contains("GG_RDS"),
            "Le scope telechargements V2 doit conserver les cibles legacy equivalentes et dedupliquer les groupes AD.");
    }

    private static void VerifyAuthoritativeCheckoutLocalGateAllowsReadyRequest()
    {
        var decision = BillingV2AuthoritativeCheckoutGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                newSubscriptionsEnabled: true),
            persistentSqlAvailable: true,
            idempotencyKey: "checkout-request-1");

        Ensure(
            decision.Authorized
            && decision.ReasonCode
                == "BILLING_V2_AUTHORITATIVE_CHECKOUT_LOCALLY_READY",
            "Le checkout V2 autoritaire local ne doit etre ouvert que quand flags, SQL et idempotence sont presents.");
    }

    private static void VerifyAdminReadinessRequiresNoRealLegacySubscriptions()
    {
        var reason = BillingV2AdminReadinessGate.ResolveReasonCode(
            persistentSqlAvailable: true,
            schemaReady: true,
            AdminRuntimeFlags(),
            new BillingV2AdminLaunchReadiness(
                RealCustomerSubscriptionCount: 1,
                DemoSubscriptionCount: 0,
                NoRealCustomerSubscriptions: false,
                VerifiedAgainstPersistentSql: true),
            [AdminProvider()]);

        Ensure(
            reason == "BILLING_V2_ADMIN_REAL_LEGACY_SUBSCRIPTIONS_PRESENT",
            "La readiness admin V2 doit bloquer le premier abonnement V2 si un contrat client reel actif existe.");
    }

    private static void VerifyAdminReadinessRequiresVerifiedLaunchSnapshot()
    {
        var reason = BillingV2AdminReadinessGate.ResolveReasonCode(
            persistentSqlAvailable: true,
            schemaReady: true,
            AdminRuntimeFlags(),
            new BillingV2AdminLaunchReadiness(
                RealCustomerSubscriptionCount: 0,
                DemoSubscriptionCount: 0,
                NoRealCustomerSubscriptions: true,
                VerifiedAgainstPersistentSql: false),
            [AdminProvider()]);

        Ensure(
            reason == "BILLING_V2_ADMIN_LAUNCH_READINESS_UNVERIFIED",
            "Un compteur a zero ne doit pas etre traite comme preuve sans lecture SQL persistante verifiee.");
    }

    private static void VerifyAdminReadinessRequiresProviderReady()
    {
        var reason = BillingV2AdminReadinessGate.ResolveReasonCode(
            persistentSqlAvailable: true,
            schemaReady: true,
            AdminRuntimeFlags(),
            new BillingV2AdminLaunchReadiness(
                RealCustomerSubscriptionCount: 0,
                DemoSubscriptionCount: 3,
                NoRealCustomerSubscriptions: true,
                VerifiedAgainstPersistentSql: true),
            [AdminProvider(readyForCheckout: false)]);

        Ensure(
            reason == "BILLING_V2_ADMIN_NO_PROVIDER_READY",
            "La readiness admin V2 doit bloquer si aucun provider n'a tous ses mappings et sa configuration.");
    }

    private static void VerifyAdminReadinessExposesBlockingSubscriptions()
    {
        var updatedAt = new DateTime(2026, 8, 13, 11, 15, 0, DateTimeKind.Utc);
        var snapshot = new BillingV2LaunchReadinessSnapshot(
            RealCustomerSubscriptionCount: 1,
            DemoSubscriptionCount: 4,
            VerifiedAgainstPersistentSql: true)
        {
            BlockingRealSubscriptions =
            [
                new BillingV2BlockingLegacySubscription(
                    "subscription-real-admin",
                    "pending_payment",
                    "customer-real-admin",
                    "CLIENT-ADMIN",
                    "Client admin",
                    null,
                    updatedAt.AddDays(-2),
                    updatedAt)
            ]
        };

        var admin = BillingV2AdminReadinessMapper.ToAdminLaunchReadiness(
            snapshot);

        Ensure(
            admin.BlockingRealSubscriptions.Count == 1
            && admin.BlockingRealSubscriptions[0].SubscriptionId
                == "subscription-real-admin"
            && admin.BlockingRealSubscriptions[0].CommercialOfferId is null
            && admin.BlockingRealSubscriptions[0].UpdatedAt
                == updatedAt.ToString("O"),
            "Le snapshot admin doit exposer les abonnements reels bloquants issus de la lecture SQL pour verifier manuellement l'absence de migration a prevoir.");
    }

    private static void VerifyAdminReadinessExposesOperationalLimitations()
    {
        var limitations = BillingV2AdminOperationalLimitations.Default;

        Ensure(
            limitations.Count == 3
            && limitations.Any(limitation =>
                limitation.Code
                    == "BILLING_V2_CANCELLATION_AUTOMATION_NOT_READY"
                && limitation.Severity == "human_review")
            && limitations.Any(limitation =>
                limitation.Code
                    == "BILLING_V2_BPCE_INVOICE_AUTOMATION_NOT_READY"
                && limitation.Severity == "hard_blocker")
            && limitations.Any(limitation =>
                limitation.Code
                    == "BILLING_V2_NEXTCLOUD_QUOTA_PROVIDER_NOT_READY"
                && limitation.Severity == "human_review"),
            "La readiness admin V2 doit exposer les limites operationnelles connues avant le premier vrai abonnement.");
    }

    private static void VerifyAdminReadinessBlocksFirstRealSubscriptionWithoutV2InvoicePath()
    {
        var reason = BillingV2AdminReadinessGate.ResolveReasonCode(
            persistentSqlAvailable: true,
            schemaReady: true,
            AdminRuntimeFlags(),
            new BillingV2AdminLaunchReadiness(
                RealCustomerSubscriptionCount: 0,
                DemoSubscriptionCount: 3,
                NoRealCustomerSubscriptions: true,
                VerifiedAgainstPersistentSql: true),
            [AdminProvider()]);

        Ensure(
            reason == "BILLING_V2_BPCE_INVOICE_AUTOMATION_NOT_READY",
            "La readiness admin V2 doit bloquer le premier vrai abonnement tant que la facture BPCE V2 n'a pas de chemin fiable et teste.");
    }

    private static void VerifyAdminReadinessAllowsFirstRealSubscriptionWhenHardBlockersCleared()
    {
        var reason = BillingV2AdminReadinessGate.ResolveReasonCode(
            persistentSqlAvailable: true,
            schemaReady: true,
            AdminRuntimeFlags(),
            new BillingV2AdminLaunchReadiness(
                RealCustomerSubscriptionCount: 0,
                DemoSubscriptionCount: 3,
                NoRealCustomerSubscriptions: true,
                VerifiedAgainstPersistentSql: true),
            [AdminProvider()],
            operationalLimitations: []);

        Ensure(
            reason == "BILLING_V2_ADMIN_READY_FOR_FIRST_SUBSCRIPTION",
            "La readiness admin V2 ne doit devenir autorisable que lorsque schema, flags, providers, validation humaine et hard blockers sont prets.");
    }

    private static BillingV2AdminRuntimeFlags AdminRuntimeFlags()
        => new(
            CatalogShadowModeEnabled: true,
            ProvisioningShadowModeEnabled: true,
            NewSubscriptionsEnabled: true,
            AuthoritativeCheckoutEnabled: true,
            FirstRealSubscriptionApproved: true,
            ProviderOutboxEnabled: true,
            ProviderExecutorEnabled: true,
            ProvisioningEnabled: false);

    private static BillingV2AdminProviderReadiness AdminProvider(
        bool readyForCheckout = true)
        => new(
            "stripe",
            "test",
            ProviderConfigured: readyForCheckout,
            PriceMappingsReady: readyForCheckout,
            RequiredServicePriceCount: 2,
            ResolvedMappingCount: readyForCheckout ? 2 : 1,
            MissingServicePriceIds: readyForCheckout ? [] : ["price-vpn"],
            AmbiguousServicePriceIds: [],
            ReadyForCheckout: readyForCheckout);

    private static BillingV2RuntimeConfiguration V2Runtime(
        bool authoritativeCheckoutEnabled,
        bool firstRealSubscriptionApproved,
        bool newSubscriptionsEnabled = false,
        bool providerOutboxEnabled = false,
        bool providerExecutorEnabled = false)
        => new(
            CatalogShadowModeEnabled: false,
            ProvisioningShadowModeEnabled: false,
            NewSubscriptionsEnabled: newSubscriptionsEnabled,
            AuthoritativeCheckoutEnabled: authoritativeCheckoutEnabled,
            FirstRealSubscriptionApproved: firstRealSubscriptionApproved,
            ProviderOutboxEnabled: providerOutboxEnabled,
            ProviderExecutorEnabled: providerExecutorEnabled,
            ProvisioningEnabled: false);

    private static BillingV2ProviderPriceMappingStatus ReadyProviderMappings()
        => BillingV2ProviderPriceMappingGate.Evaluate(
            ["price-storage", "price-vpn"],
            [
                new("price-storage", "stripe", "test", "price_storage_test"),
                new("price-vpn", "stripe", "test", "price_vpn_test")
            ],
            "stripe",
            "test");

    private static BillingV2DocumentSource DocumentSource(
        int discountBasisPoints,
        string paymentMode,
        IReadOnlyList<BillingV2DocumentSourceItem> items,
        BillingV2DocumentPriceLock? priceLock = null)
        => new(
            new BillingV2DocumentSubscriptionSource(
                "sub-v2",
                "customer-v2",
                "active",
                paymentMode,
                "EUR",
                discountBasisPoints,
                MinimumCommitmentAmountCents: null,
                new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc),
                CommitmentMonths: 12,
                "CUST-V2",
                "Client V2"),
            items,
            priceLock);

    private static BillingV2DocumentSourceItem DocumentItem(
        string itemId,
        string serviceCode,
        string serviceName,
        string tierCode,
        string tierName,
        long amountCents,
        int quantity,
        string billingCadence = BillingV2BillingCadences.Monthly,
        bool discountEligible = true)
        => new(
            itemId,
            $"price-{itemId}",
            serviceCode,
            serviceName,
            tierCode,
            tierName,
            $"{serviceCode}-{tierCode}-MONTHLY-EUR-V1",
            billingCadence,
            TaxRateBasisPoints: 2000,
            quantity,
            amountCents,
            "EUR",
            discountEligible);

    private static BillingV2CheckoutReadinessDecision ReadyCheckoutDecision()
        => BillingV2CheckoutReadinessGate.Evaluate(
            V2Runtime(
                authoritativeCheckoutEnabled: true,
                firstRealSubscriptionApproved: true,
                providerOutboxEnabled: true,
                providerExecutorEnabled: true),
            BillingV2LaunchReadinessGate.Evaluate(0, 0),
            ReadyProviderMappings(),
            BillingV2DocumentReadinessStatus.ReadyForCheckout);

    private static BillingV2ProviderCheckoutCommandRequest
        ProviderCheckoutRequest(BillingV2CheckoutReadinessDecision readiness)
    {
        var plan = new BillingV2CheckoutPlan(
            "stripe",
            "test",
            "EUR",
            RecurringAmountCents: 2900,
            OneTimeAmountCents: 1500,
            TotalDueNowCents: 4400,
            [
                new(
                    "price-storage",
                    "price_storage_test",
                    Quantity: 1,
                    AmountCents: 1900),
                new(
                    "price-vpn",
                    "price_vpn_test",
                    Quantity: 1,
                    AmountCents: 1000)
            ]);
        return new BillingV2ProviderCheckoutCommandRequest(
            "subscription-v2-new",
            "customer-v2-new",
            "client@example.invalid",
            "https://example.invalid/success",
            "https://example.invalid/cancel",
            plan,
            readiness,
            "correlation-v2-provider-checkout",
            "user-v2-new");
    }

    private static BillingV2ProviderCheckoutPayload ProviderCheckoutPayload(
        string subscriptionId,
        string provider)
        => new(
            subscriptionId,
            "customer-v2-provider-replay",
            "client@example.invalid",
            provider,
            "test",
            "EUR",
            RecurringAmountCents: 1900,
            OneTimeAmountCents: 0,
            TotalDueNowCents: 1900,
            "https://example.invalid/success",
            "https://example.invalid/cancel",
            "correlation-v2-provider-replay",
            [
                new(
                    "service-price-provider-replay",
                    provider == "paypal"
                        ? "P-PAYPAL-V2-PLAN"
                        : "price_provider_replay",
                    Quantity: 1,
                    AmountCents: 1900)
            ]);

    private static BillingV2ProviderInboundEventRequest ProviderInboundEvent(
        string eventType,
        string? providerCheckoutId,
        string? providerSubscriptionId)
        => new(
            "stripe",
            "test",
            $"evt-{eventType}",
            eventType,
            providerCheckoutId,
            providerSubscriptionId,
            PayloadText: null);

    private static BillingV2ProviderLocalState ProviderLocalState(
        string? providerCheckoutId,
        string? providerSubscriptionId,
        string checkoutStatus = "pending_approval",
        string? agreementStatus = "pending",
        string subscriptionStatus = "pending")
        => new(
            "checkout-session-v2",
            "subscription-v2-new",
            "stripe",
            "test",
            providerCheckoutId,
            providerSubscriptionId,
            checkoutStatus,
            agreementStatus,
            subscriptionStatus);

    private static BillingV2PricingResult PricingFor(
        IReadOnlyList<BillingV2NewSubscriptionPresetItem> items)
        => new BillingV2PricingEngine().Calculate(new BillingV2PricingRequest(
            items.Select(item => new BillingV2PricingItem(
                item.PresetItemId,
                item.ServiceCode,
                item.TierCode,
                item.PriceCode,
                item.AmountCents,
                item.Quantity,
                item.BillingCadence,
                item.DiscountEligible)).ToArray(),
            DiscountBasisPoints: 0,
            BillingV2PaymentModes.Monthly,
            CommitmentMonths: 1,
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            DateTime.UtcNow));

    private static BillingV2PricingResult PricingResult(
        long payableRecurringAmountCents,
        long upfrontRecurringAmountCents = 0)
        => new(
            DiscountEligibleRecurringSubtotalCents: 0,
            NonDiscountableRecurringSubtotalCents: 0,
            RecurringSubtotalCents: 0,
            RecurringDiscountCents: 0,
            DiscountedRecurringAmountCents: 0,
            PayableRecurringAmountCents: payableRecurringAmountCents,
            OneTimeSubtotalCents: 0,
            UpfrontRecurringAmountCents: upfrontRecurringAmountCents,
            TotalDueNowCents: checked(
                payableRecurringAmountCents + upfrontRecurringAmountCents),
            AppliedPriceLock: null);

    private static BillingV2NewSubscriptionPresetItem Item(
        string serviceCode,
        string? tierCode,
        string scopeTemplate)
        => new(
            Guid.NewGuid().ToString("D"),
            $"service-{serviceCode}",
            tierCode is null ? null : $"tier-{serviceCode}-{tierCode}",
            $"price-{serviceCode}-{tierCode ?? "none"}",
            serviceCode,
            tierCode,
            $"{serviceCode}-{tierCode ?? "NONE"}-MONTHLY-EUR-V1",
            scopeTemplate,
            Quantity: 1,
            AmountCents: 100,
            "EUR",
            BillingV2BillingCadences.Monthly,
            DiscountEligible: true);

    private static PortalSessionContext Session()
        => new(
            "session-v2-new",
            "user-v2-new",
            "customer-v2-new",
            "CLI-V2-NEW",
            "client@example.invalid",
            "Client V2",
            "active",
            "client_user",
            LastLoginAtUtc: null,
            DateTime.UtcNow.AddHours(1));

    private static BillingV2PortalSubscriptionRow PortalSubscriptionRow(
        string paymentMode = BillingV2PaymentModes.Monthly,
        int commitmentMonths = 1,
        int discountBasisPoints = 0,
        long? activeLockAmountCents = 1900,
        string? activeLockType = BillingV2PriceLockTypes.MonthlyRecurring,
        long recurringDiscountEligibleCents = 1900,
        long recurringNonDiscountableCents = 0,
        long oneTimeCents = 0,
        string provider = "stripe",
        string? providerSubscriptionId = "sub_v2_portal")
        => new(
            "sub-v2-portal",
            "customer-v2-new",
            "CLI-V2-NEW",
            "Client V2",
            "preset-v2-portal",
            "legacy-offer-v2",
            "Pack V2 Portal",
            "PACK-V2-PORTAL",
            "PACK-V2-PORTAL",
            "pack-dossier-securise",
            provider,
            providerSubscriptionId,
            "pending_approval",
            paymentMode,
            "EUR",
            discountBasisPoints,
            null,
            commitmentMonths,
            activeLockAmountCents,
            activeLockType,
            recurringDiscountEligibleCents,
            recurringNonDiscountableCents,
            oneTimeCents,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 13, 12, 30, 0, DateTimeKind.Utc));

    private static SubscriptionSummary Subscription(
        string rail,
        string? paypalSubscriptionId = null,
        string? stripeSubscriptionId = null)
        => new(
            "subscription-v2-new",
            "customer-v2-new",
            "CLI-V2-NEW",
            "Client V2",
            "offer-v2-new",
            "Pack V2 New",
            "PACK-DOSSIER-1M-MENS",
            "pack-dossier-securise",
            rail,
            "P-PAYPAL-PLAN",
            paypalSubscriptionId,
            "price_stripe",
            stripeSubscriptionId,
            "pending",
            900,
            1500,
            null,
            "franchise_base",
            "TVA non applicable",
            1,
            1,
            "monthly",
            0,
            null,
            null,
            false,
            "EUR",
            null,
            null,
            null,
            "2026-08-12T00:00:00Z",
            "2026-08-12T00:00:00Z");

    private static HttpResponseMessage JsonResponse(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                body,
                Encoding.UTF8,
                "application/json")
        };

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
            => new(_handler, disposeHandler: false);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public RecordingHttpMessageHandler(
            Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_handler(request));
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
