using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Bpce;
using Kermaria.ApiInternal.Services.Provisioning;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kermaria.ApiInternal.SmokeTests;

public static class BillingLegacyIdempotencyTests
{
    public static async Task RunAsync()
    {
        await VerifyPayPalProcessedWebhookStaysIdempotentAsync();
        await VerifyPayPalFailedWebhookCanBeRetriedAsync();
        await VerifyProvisioningConcurrentReconcileDedupesActionsAsync();
        await VerifyRenewalUsesSubscriptionPriceLockAsync();
        await VerifyRenewalWithoutPriceLockIsBlockedAsync();
        VerifyBackfillUsesHistoricalLineInsteadOfCurrentOfferAsync();
        VerifyBackfillRequiresManualReviewWhenHistoryIsMissing();
        await VerifyNewSubscriptionCreatesPriceLockFromResolvedOfferAsync();
    }

    private static async Task VerifyPayPalProcessedWebhookStaysIdempotentAsync()
    {
        var harness = CreatePayPalHarness(failFirstIssue: false);
        var payload = PayPalPaymentCompletedPayload(
            "paypal-event-processed",
            "I-PAYPAL-IDEMPOTENT");

        var first = await harness.Service.ProcessAsync(
            payload,
            "billing-idempotency-paypal-processed-1",
            CancellationToken.None);
        var second = await harness.Service.ProcessAsync(
            payload,
            "billing-idempotency-paypal-processed-2",
            CancellationToken.None);

        Ensure(first.Status == "processed", "Le premier webhook PayPal doit etre traite.");
        Ensure(second.Status == "processed", "Le doublon PayPal deja processed doit rester no-op.");
        Ensure(
            harness.Issuing.IssueCallCount == 1
            && harness.Issuing.ConfirmCallCount == 1,
            "Un webhook PayPal processed ne doit pas reemettre ou reconfirmer la facture.");
        Ensure(
            CurrentSubscription(harness.SubscriptionStore).PaidCyclesCount == 1,
            "Un webhook PayPal processed en double ne doit incrementer qu'un cycle paye.");
    }

    private static async Task VerifyPayPalFailedWebhookCanBeRetriedAsync()
    {
        var harness = CreatePayPalHarness(failFirstIssue: true);
        var payload = PayPalPaymentCompletedPayload(
            "paypal-event-retry",
            "I-PAYPAL-IDEMPOTENT");

        var first = await harness.Service.ProcessAsync(
            payload,
            "billing-idempotency-paypal-retry-1",
            CancellationToken.None);
        var second = await harness.Service.ProcessAsync(
            payload,
            "billing-idempotency-paypal-retry-2",
            CancellationToken.None);

        Ensure(first.Status == "failed", "Le premier webhook PayPal doit exposer l'echec.");
        Ensure(second.Status == "processed", "Un webhook PayPal failed doit pouvoir etre retraite.");
        Ensure(
            harness.EventStore.Events["paypal-event-retry"].Status == "processed",
            "Le statut final de l'evenement PayPal retry doit redevenir processed.");
        Ensure(
            harness.Issuing.IssueCallCount == 2
            && harness.Issuing.ConfirmCallCount == 1,
            "Le retry PayPal doit rejouer l'emission echouee puis confirmer une seule fois.");
        Ensure(
            CurrentSubscription(harness.SubscriptionStore).PaidCyclesCount == 1,
            "Le retry PayPal ne doit enregistrer qu'un seul paiement local.");
    }

    private static async Task VerifyProvisioningConcurrentReconcileDedupesActionsAsync()
    {
        var subscriptionStore = new MockSubscriptionStore();
        var subscription = CreateProvisioningSubscription();
        subscriptionStore.Subscriptions.Add(subscription);
        var actionStore = new MockSubscriptionProvisioningActionStore();
        var provisioning = new BlockingProvisioningService();
        var manager = new SubscriptionProvisioningManager(
            new MockSubscriptionRepository(subscriptionStore),
            new StaticActiveDirectoryLinkRepository(),
            new MockSubscriptionProvisioningActionRepository(actionStore),
            provisioning,
            new StaticOfferTopologyService(),
            NoOpBillingV2ProvisioningShadowService.Instance,
            NoOpBillingV2ProvisioningService.Instance,
            new StaticActiveDirectoryService(),
            new SubscriptionProvisioningRuntimeConfiguration(
                new Dictionary<string, IReadOnlyList<string>>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["PACK-ACCES-1M-MENS"] = ["GG_VPN"]
                },
                new Dictionary<string, IReadOnlyList<string>>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["ACCES-VPN"] = ["GG_VPN"],
                    ["ACCES-RDS"] = ["GG_RDS"],
                    ["NEXTCLOUD"] = ["GG_NextCloud"]
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                MaxAttempts: 1,
                RetryDelayMs: 0),
            new StaticAdGroupProvisioner(),
            NullLogger<SubscriptionProvisioningManager>.Instance);

        var first = manager.ReconcileAsync(
            subscription,
            "subscription.provisioning.reconcile",
            "billing-idempotency-provisioning-1",
            requestedByUserId: null,
            targetUserSamAccountNames: null,
            CancellationToken.None);
        await provisioning.WaitUntilEnteredAsync();

        var second = manager.ReconcileAsync(
            subscription,
            "subscription.provisioning.reconcile",
            "billing-idempotency-provisioning-2",
            requestedByUserId: null,
            targetUserSamAccountNames: null,
            CancellationToken.None);
        await Task.Delay(50);
        provisioning.Release();
        await Task.WhenAll(first, second);

        Ensure(
            provisioning.ExecutionCount == 1,
            "Deux reconciles concurrents identiques ne doivent executer le provisioning AD qu'une seule fois.");
        Ensure(
            actionStore.Actions.Count == 1,
            "Deux reconciles concurrents identiques ne doivent creer qu'une action AD active.");
        Ensure(
            actionStore.Actions[0].Status == "succeeded",
            "L'action AD dedupee doit conserver le resultat du reconcile execute.");
    }

    private static async Task VerifyRenewalUsesSubscriptionPriceLockAsync()
    {
        var store = new MockCommercialStore();
        var repository = new MockCommercialRepository(store);
        const string subscriptionId = "subscription-renewal-price-lock";
        const string offerId = "offer-pack-dossier-1m-monthly";

        await repository.EnsureSubscriptionPriceLockAsync(
            subscriptionId,
            offerId,
            unitPriceCents: 1190,
            taxRateBasisPoints: null,
            "EUR",
            "test_contract_snapshot",
            CancellationToken.None);

        lock (store.SyncRoot)
        {
            var offer = store.Offers.Single(candidate => candidate.Id == offerId);
            offer.PriceAmountCents = 7777;
        }

        var documentId = await repository.CreateBillingDocumentForSubscriptionAsync(
            new SubscriptionBillingDocumentRequest(
                "customer-renewal-price-lock",
                offerId,
                subscriptionId,
                "Renouvellement test",
                []),
            "billing-renewal-price-lock",
            CancellationToken.None);

        lock (store.SyncRoot)
        {
            var line = store.Lines[documentId].Single();
            Ensure(
                line.UnitPriceCents == 1190
                && line.LineTotalCents == 1190,
                "Le renouvellement legacy doit utiliser le price lock contractuel, pas le prix courant commercial_offers.");
            Ensure(
                store.SubscriptionPriceLocks.Count(lockRow =>
                    lockRow.SubscriptionId == subscriptionId
                    && lockRow.Status == "active") == 1,
                "Le renouvellement legacy ne doit pas creer plusieurs locks actifs.");
        }
    }

    private static async Task VerifyRenewalWithoutPriceLockIsBlockedAsync()
    {
        var store = new MockCommercialStore();
        var repository = new MockCommercialRepository(store);
        const string subscriptionId = "subscription-renewal-without-lock";
        const string offerId = "offer-pack-dossier-1m-monthly";

        try
        {
            _ = await repository.CreateBillingDocumentForSubscriptionAsync(
                new SubscriptionBillingDocumentRequest(
                    "customer-renewal-without-lock",
                    offerId,
                    subscriptionId,
                    "Renouvellement sans lock",
                    []),
                "billing-renewal-without-lock",
                CancellationToken.None);
            throw new InvalidOperationException(
                "Un renouvellement sans price lock a cree une facture.");
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(
                "active contractual price lock",
                StringComparison.Ordinal))
        {
            // attendu : pas de fallback silencieux vers commercial_offers.
        }

        lock (store.SyncRoot)
        {
            Ensure(
                !store.Documents.Any(document =>
                    document.SubscriptionId == subscriptionId),
                "Un renouvellement sans price lock ne doit creer aucun document commercial.");
        }
    }

    private static void VerifyBackfillUsesHistoricalLineInsteadOfCurrentOfferAsync()
    {
        const int originalContractPriceCents = 1900;
        const int currentOfferPriceCents = 7777;

        var plan = LegacySubscriptionPriceLockBackfillPlanner.Plan(
            [
                new(
                    "subscription-historical-price",
                    "offer-pack-acces-1m-monthly",
                    "active")
            ],
            [
                new(
                    "subscription-historical-price",
                    "offer-pack-acces-1m-monthly",
                    originalContractPriceCents,
                    2000,
                    "EUR",
                    new DateTime(2026, 01, 10, 10, 00, 00, DateTimeKind.Utc),
                    10,
                    new DateTime(2026, 01, 10, 10, 00, 00, DateTimeKind.Utc),
                    "line-initial-contract")
            ],
            new HashSet<string>(StringComparer.Ordinal));

        Ensure(
            currentOfferPriceCents != originalContractPriceCents,
            "Le test doit simuler une offre modifiee apres souscription.");
        Ensure(
            plan.Locks.Count == 1
            && plan.Locks[0].UnitPriceCents == originalContractPriceCents,
            "Le backfill doit utiliser la ligne historique X, jamais le prix courant Y de commercial_offers.");
        Ensure(
            plan.ReviewRequired.Count == 0,
            "Un historique fiable doit eviter la revue manuelle.");
    }

    private static void VerifyBackfillRequiresManualReviewWhenHistoryIsMissing()
    {
        var plan = LegacySubscriptionPriceLockBackfillPlanner.Plan(
            [
                new(
                    "subscription-no-history",
                    "offer-pack-acces-1m-monthly",
                    "active")
            ],
            Array.Empty<LegacySubscriptionHistoricalBillingLine>(),
            new HashSet<string>(StringComparer.Ordinal));

        Ensure(
            plan.Locks.Count == 0,
            "Le backfill ne doit pas inventer de price lock sans historique exploitable.");
        Ensure(
            plan.ReviewRequired.Count == 1
            && plan.ReviewRequired[0].Reason == "missing_reliable_historical_price",
            "Un abonnement sans historique fiable doit etre explicitement marque pour revue.");
    }

    private static async Task VerifyNewSubscriptionCreatesPriceLockFromResolvedOfferAsync()
    {
        var commercialStore = new MockCommercialStore();
        var commercialRepository = new MockCommercialRepository(commercialStore);
        var subscriptionStore = new MockSubscriptionStore();
        var subscriptionRepository = new MockSubscriptionRepository(
            subscriptionStore);
        var catalog = new LegacyBillingCatalogAdapter(
            commercialRepository,
            new PayPalRuntimeConfiguration(
                PayPalMode.Sandbox,
                ClientId: null,
                ClientSecret: null),
            new StripeRuntimeConfiguration(StripeMode.Disabled));
        var service = new SubscriptionService(
            subscriptionRepository,
            catalog,
            commercialRepository,
            NoOpBillingV2NewSubscriptionService.Instance,
            NoOpBillingV2PortalSubscriptionProjection.Instance,
            new StaticSubscriptionProvisioningManager(),
            NullLogger<SubscriptionService>.Instance);
        var offer = commercialStore.Offers.Single(candidate =>
            candidate.Id == "offer-pack-acces-1m-monthly");
        offer.PriceAmountCents = 2345;

        var subscription = await service.CreateBilledPendingAsync(
            new PortalSessionContext(
                "session-new-lock",
                "user-new-lock",
                "customer-new-lock",
                "CLI-NEW-LOCK",
                "client@example.invalid",
                "Client New Lock",
                "active",
                "customer_user",
                null,
                DateTime.UtcNow.AddHours(1)),
            offer.Id,
            CancellationToken.None);

        lock (commercialStore.SyncRoot)
        {
            var priceLock = commercialStore.SubscriptionPriceLocks.Single(
                candidate => candidate.SubscriptionId == subscription.Id);
            Ensure(
                priceLock.UnitPriceCents == 2345
                && priceLock.OfferId == offer.Id
                && priceLock.Reason == "legacy_subscription_created",
                "Un nouvel abonnement doit creer un lock avec le prix resolu a la souscription.");
        }
    }

    private static PayPalHarness CreatePayPalHarness(bool failFirstIssue)
    {
        var subscriptionStore = new MockSubscriptionStore();
        var subscription = CreatePayPalSubscription();
        subscriptionStore.Subscriptions.Add(subscription);
        var eventStore = new MockPayPalWebhookStore();
        var commercialStore = new MockCommercialStore();
        commercialStore.SubscriptionPriceLocks.Add(
            new MockSubscriptionBillingPriceLock(
                Guid.NewGuid().ToString("D"),
                subscription.Id,
                subscription.CommercialOfferId,
                subscription.PriceAmountCents,
                subscription.TaxRateBasisPoints,
                subscription.Currency,
                "test_contract_snapshot",
                "active"));
        var issuing = new CountingInvoiceIssuingService(failFirstIssue);
        var subscriptionService = new RepositoryBackedSubscriptionService(
            new MockSubscriptionRepository(subscriptionStore));
        var service = new PayPalWebhookService(
            new MockPayPalWebhookRepository(eventStore),
            new MockSubscriptionRepository(subscriptionStore),
            subscriptionService,
            new MockCommercialRepository(commercialStore),
            issuing,
            new NoopAuditService(),
            NullLogger<PayPalWebhookService>.Instance);

        return new PayPalHarness(
            service,
            eventStore,
            subscriptionStore,
            issuing);
    }

    private static PayPalWebhookEventPayload PayPalPaymentCompletedPayload(
        string eventId,
        string paypalSubscriptionId)
        => new(
            eventId,
            "PAYMENT.SALE.COMPLETED",
            paypalSubscriptionId,
            $$"""
              {
                "id": "{{eventId}}",
                "event_type": "PAYMENT.SALE.COMPLETED",
                "resource": {
                  "billing_agreement_id": "{{paypalSubscriptionId}}"
                }
              }
              """);

    private static SubscriptionSummary CreatePayPalSubscription()
        => new(
            "subscription-paypal-idempotency",
            "customer-idempotency",
            "CLI-IDEMPOTENCY",
            "Client Idempotency",
            "offer-pack-acces-1m-monthly",
            "Pack Acces a Distance",
            "PACK-ACCES-1M-MENS",
            "pack-acces-distance",
            "paypal",
            "P-PAYPAL-PLAN",
            "I-PAYPAL-IDEMPOTENT",
            null,
            null,
            "active",
            1900,
            2500,
            2000,
            "standard",
            "",
            1,
            1,
            "monthly",
            0,
            null,
            null,
            false,
            "EUR",
            DateTime.UtcNow.AddDays(-30).ToString("O"),
            DateTime.UtcNow.ToString("O"),
            null,
            DateTime.UtcNow.AddDays(-30).ToString("O"),
            DateTime.UtcNow.ToString("O"));

    private static SubscriptionSummary CreateProvisioningSubscription()
        => CreatePayPalSubscription() with
        {
            Id = "subscription-provisioning-idempotency",
            Rail = "billing",
            PayPalPlanId = null,
            PayPalSubscriptionId = null
        };

    private static SubscriptionSummary CurrentSubscription(MockSubscriptionStore store)
    {
        lock (store.SyncRoot)
        {
            return store.Subscriptions.Single();
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record PayPalHarness(
        PayPalWebhookService Service,
        MockPayPalWebhookStore EventStore,
        MockSubscriptionStore SubscriptionStore,
        CountingInvoiceIssuingService Issuing);

    private sealed class CountingInvoiceIssuingService : IInvoiceIssuingService
    {
        private readonly bool _failFirstIssue;

        public CountingInvoiceIssuingService(bool failFirstIssue)
        {
            _failFirstIssue = failFirstIssue;
        }

        public int IssueCallCount { get; private set; }
        public int ConfirmCallCount { get; private set; }

        public Task<IssueInvoiceResult> IssueInvoiceAsync(
            string documentId,
            bool sendEmail,
            string correlationId,
            CancellationToken cancellationToken)
        {
            IssueCallCount++;
            if (_failFirstIssue && IssueCallCount == 1)
            {
                return Task.FromResult(new IssueInvoiceResult(
                    false,
                    "TEST_FAILURE",
                    "Echec BPCE simule."));
            }

            return Task.FromResult(new IssueInvoiceResult(
                true,
                "INVOICE_ISSUED",
                "Facture emise.",
                new BpceIssuedInvoiceInfo(
                    $"INV-{IssueCallCount}",
                    null,
                    "issued",
                    DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    100,
                    "EUR",
                    PdfAvailable: false)));
        }

        public Task<byte[]?> GetCachedInvoicePdfAsync(
            string documentId,
            CancellationToken cancellationToken)
            => Task.FromResult<byte[]?>(null);

        public Task<byte[]?> EnsureInvoicePdfAsync(
            string documentId,
            CancellationToken cancellationToken)
            => Task.FromResult<byte[]?>(null);

        public Task<BpceInvoiceRecord?> GetInvoiceRecordAsync(
            string documentId,
            CancellationToken cancellationToken)
            => Task.FromResult<BpceInvoiceRecord?>(null);

        public Task<IssueInvoiceResult> ConfirmPaymentAsync(
            string documentId,
            string correlationId,
            string paymentMethod,
            CancellationToken cancellationToken)
        {
            ConfirmCallCount++;
            return Task.FromResult(new IssueInvoiceResult(
                true,
                "PAYMENT_CONFIRMED",
                "Paiement confirme."));
        }
    }

    private sealed class NoopAuditService : IAuditService
    {
        public Task RecordAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RepositoryBackedSubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _repository;

        public RepositoryBackedSubscriptionService(ISubscriptionRepository repository)
        {
            _repository = repository;
        }

        public bool IsPersistent => false;

        public Task<IReadOnlyList<SubscriptionSummary>> GetClientSubscriptionsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
            => _repository.GetByCustomerAsync(session.CustomerId, cancellationToken);

        public Task<IReadOnlyList<SubscriptionSummary>> GetAdminSubscriptionsAsync(
            CancellationToken cancellationToken)
            => _repository.GetAllAsync(cancellationToken);

        public Task<SubscriptionSummary> GetSubscriptionAsync(
            string subscriptionId,
            CancellationToken cancellationToken)
            => _repository.GetByIdAsync(subscriptionId, cancellationToken)
                .ContinueWith(task => task.Result
                    ?? throw new InvalidOperationException(
                        $"Subscription {subscriptionId} not found."),
                    cancellationToken);

        public Task<SubscriptionLookup> ResolveSubscribableOfferAsync(
            string offerId,
            string rail,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SubscriptionSummary> CreatePendingAsync(
            PortalSessionContext session,
            string offerId,
            string rail,
            string externalSubscriptionId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SubscriptionSummary> CreateBilledPendingAsync(
            PortalSessionContext session,
            string offerId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SubscriptionSummary> MarkAsPendingActivationAsync(
            PortalSessionContext session,
            string subscriptionId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AdminSubscriptionDetail> GetAdminSubscriptionDetailAsync(
            string subscriptionId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SubscriptionSummary> ActivateAsync(
            string subscriptionId,
            DateTime startedAtUtc,
            DateTime nextBillingAtUtc,
            string correlationId,
            CancellationToken cancellationToken)
            => _repository.ActivateAsync(
                subscriptionId,
                startedAtUtc,
                nextBillingAtUtc,
                nextBillingAtUtc,
                cancellationToken);

        public Task<SubscriptionSummary> RecordPaymentAsync(
            string subscriptionId,
            DateTime paidAtUtc,
            CancellationToken cancellationToken)
            => _repository.RecordPaymentAsync(
                subscriptionId,
                paidAtUtc.AddMonths(1),
                paidAtUtc.AddMonths(1),
                cancellationToken);

        public Task<SubscriptionSummary> UpdateStatusAsync(
            string subscriptionId,
            string newStatus,
            string provisioningActionType,
            string correlationId,
            string? requestedByUserId,
            CancellationToken cancellationToken)
            => _repository.UpdateStatusAsync(
                subscriptionId,
                newStatus,
                cancellationToken);

        public Task<SubscriptionSummary> ClientCancelAsync(
            PortalSessionContext session,
            string subscriptionId,
            string correlationId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SubscriptionSummary> AdminCancelAsync(
            string subscriptionId,
            string correlationId,
            string? requestedByUserId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SubscriptionProvisioningSummary> ReconcileProvisioningAsync(
            string subscriptionId,
            string actionType,
            string correlationId,
            string? requestedByUserId,
            IReadOnlyList<string>? targetUserSamAccountNames,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class StaticActiveDirectoryLinkRepository
        : IActiveDirectoryLinkRepository
    {
        private static readonly CustomerAdLinkSummary Link = new(
            "ad-link-idempotency",
            "CLI-IDEMPOTENCY",
            "object-guid-idempotency",
            "object-sid-idempotency",
            "user",
            "user.idempotency",
            "user.idempotency@example.invalid",
            "User Idempotency",
            "CN=User Idempotency,OU=Users,OU=CLI-IDEMPOTENCY,DC=example,DC=invalid",
            DateTime.UtcNow.AddDays(-1).ToString("O"),
            null);

        public bool IsPersistent => false;

        public Task<AdCustomerContext?> GetCustomerContextAsync(
            string customerReference,
            CancellationToken cancellationToken)
            => Task.FromResult<AdCustomerContext?>(new(
                "customer-idempotency",
                customerReference,
                "Client Idempotency"));

        public Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerLinksAsync(
            string customerReference,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CustomerAdLinkSummary>>([Link]);

        public Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerUserLinksAsync(
            string customerId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CustomerAdLinkSummary>>([Link]);

        public Task<CustomerAdLinkUpsertResult> UpsertCustomerLinkAsync(
            string customerReference,
            string? actorUserId,
            AdDirectoryObjectSummary directoryObject,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CustomerAdLinkUpsertResult> UpsertPortalUserLinkAsync(
            string customerReference,
            string portalUserId,
            string? actorUserId,
            AdDirectoryObjectSummary directoryObject,
            string? adDomain,
            string? adProvisioningStatus,
            DateTime? adProvisionedAtUtc,
            string? lastPasswordSyncStatus,
            DateTime? lastPasswordSyncAtUtc,
            string? koxoExportStatus,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> UpdateUserPasswordSyncStatusAsync(
            string portalUserId,
            string status,
            DateTime changedAtUtc,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> DeleteCustomerLinkAsync(
            string customerReference,
            string linkId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> RefreshCustomerLinkAsync(
            string targetCustomerReference,
            AdDirectoryObjectSummary directoryObject,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CustomerAdLinkSummary?> FindUserLinkByEmailAsync(
            string customerReference,
            string email,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PortalUserAdLinkRecord?> FindUserLinkByPortalUserIdAsync(
            string portalUserId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class StaticOfferTopologyService : ICommercialOfferTopologyService
    {
        public Task<IReadOnlyList<string>> ResolveMappedGroupsAsync(
            SubscriptionSummary subscription,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(
                subscription.OfferExternalReference == "PACK-ACCES-1M-MENS"
                    ? ["GG_VPN"]
                    : Array.Empty<string>());

        public Task<IReadOnlyList<string>> ResolveTechnicalServiceReferencesAsync(
            SubscriptionSummary subscription,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(["ACCES-VPN"]);

        public Task<IReadOnlyList<string>> ResolveServiceMappedGroupsAsync(
            string technicalServiceReference,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(["GG_VPN"]);

        public Task<string> ResolveServiceLabelAsync(
            string technicalServiceReference,
            CancellationToken cancellationToken)
            => Task.FromResult(technicalServiceReference);

        public Task<IReadOnlyList<CatalogTechnicalServiceDefinition>> GetTechnicalServicesAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CatalogTechnicalServiceDefinition>>(
                [
                    new("ACCES-VPN", "Acces VPN", ["GG_VPN"]),
                    new("ACCES-RDS", "RDS", ["GG_RDS"]),
                    new("NEXTCLOUD", "Nextcloud", ["GG_NextCloud"])
                ]);

        public Task<IReadOnlyList<string>> GetManagedGroupSamAccountNamesAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(["GG_VPN"]);
    }

    private sealed class StaticActiveDirectoryService : IActiveDirectoryService
    {
        public string ModeName => "mock";

        public Task<AdStatusResponse> GetStatusAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(new AdStatusResponse(
                "mock",
                "ready",
                ConfigurationValid: true,
                ReadsEnabled: true,
                WritesEnabled: true,
                Domain: "example.invalid",
                ClientsOuDn: "OU=Clients,DC=example,DC=invalid",
                AllowedRoots: ["DC=example,DC=invalid"],
                ConnectTimeoutMs: 100,
                QueryTimeoutMs: 100,
                MaxResults: 10));

        public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchUsersAsync(string? query, string? customerReference, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchGroupsAsync(string? query, string? customerReference, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> ResolveObjectForLinkAsync(string customerReference, string? distinguishedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> CreateUserAsync(string customerReference, CreateAdUserRequest? request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> CreateGroupAsync(string customerReference, CreateAdGroupRequest? request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> AddGroupMemberAsync(string customerReference, string? groupSamAccountName, string? userSamAccountName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> RemoveGroupMemberAsync(string customerReference, string? groupSamAccountName, string? userSamAccountName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> DisableUserAsync(string customerReference, string? samAccountName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserToDisabledAsync(string customerReference, string? samAccountName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> GetUserEffectiveGroupsAsync(
            string customerReference,
            string? samAccountName,
            CancellationToken cancellationToken)
            => Task.FromResult(new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(
                200,
                "OK",
                "OK",
                Array.Empty<AdDirectoryObjectSummary>()));

        public Task<AdServiceResult<AdDirectoryObjectSummary>> RenameUserAsync(string customerReference, string? currentSamAccountName, RenameAdUserRequest? request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserAsync(string customerReference, string? samAccountName, MoveAdUserRequest? request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> ChangeUserPasswordAsync(string customerReference, string? samAccountName, string? currentPassword, string? newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdServiceResult<AdDirectoryObjectSummary>> SetUserPasswordAsync(string customerReference, string? samAccountName, string? newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StaticAdGroupProvisioner : IAdGroupProvisioner
    {
        public string ModeName => "mock";
        public bool RequiresConfiguredGroupDistinguishedNames => false;

        public Task<AdGroupProvisionerResult> AddUserToGroupAsync(
            CustomerAdLinkSummary user,
            string groupSamAccountName,
            string? groupDistinguishedName,
            CancellationToken cancellationToken)
            => Task.FromResult(new AdGroupProvisionerResult(
                200,
                "GROUP_MEMBER_ADDED",
                "OK",
                Changed: true));

        public Task<AdGroupProvisionerResult> RemoveUserFromGroupAsync(
            CustomerAdLinkSummary user,
            string groupSamAccountName,
            string? groupDistinguishedName,
            CancellationToken cancellationToken)
            => Task.FromResult(new AdGroupProvisionerResult(
                200,
                "GROUP_MEMBER_REMOVED",
                "OK",
                Changed: true));

        public Task<AdDirectoryObjectSummary?> ResolveUserByEmployeeNumberAsync(
            string employeeNumber,
            CancellationToken cancellationToken)
            => Task.FromResult<AdDirectoryObjectSummary?>(null);
    }

    private sealed class StaticSubscriptionProvisioningManager
        : ISubscriptionProvisioningManager
    {
        public Task<SubscriptionProvisioningSummary> GetSummaryAsync(
            SubscriptionSummary subscription,
            CancellationToken cancellationToken)
            => Task.FromResult(new SubscriptionProvisioningSummary(
                "not_required",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<SubscriptionProvisioningTargetUserSummary>(),
                CanRetry: false,
                LastResultCode: null,
                Array.Empty<SubscriptionProvisioningActionSummary>()));

        public Task<SubscriptionProvisioningSummary> ReconcileAsync(
            SubscriptionSummary subscription,
            string actionType,
            string correlationId,
            string? requestedByUserId,
            IReadOnlyList<string>? targetUserSamAccountNames,
            CancellationToken cancellationToken)
            => GetSummaryAsync(subscription, cancellationToken);
    }

    private sealed class BlockingProvisioningService : IProvisioningService
    {
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ExecutionCount { get; private set; }

        public async Task<ProvisioningExecutionResult> ReconcileAsync(
            ProvisioningExecutionRequest request,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new ProvisioningExecutionResult(
                true,
                true,
                "PROVISIONING_SYNCHRONIZED",
                [
                    new(
                        "GG_VPN",
                        request.TargetUsers[0].SamAccountName,
                        "add",
                        "GROUP_MEMBER_ADDED",
                        Changed: true)
                ]);
        }

        public Task WaitUntilEnteredAsync()
            => _entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Release()
            => _release.TrySetResult();
    }
}
