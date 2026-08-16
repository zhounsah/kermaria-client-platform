using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.Bpce;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kermaria.ApiInternal.SmokeTests;

public static class BillingCatalogCompatibilityTests
{
    private static readonly string[] ExpectedPackReferences =
    [
        "PACK-DOSSIER-1M-MENS",
        "PACK-DOSSIER-6M-MENS",
        "PACK-DOSSIER-6M-COMPT",
        "PACK-DOSSIER-12M-MENS",
        "PACK-DOSSIER-12M-COMPT",
        "PACK-ACCES-1M-MENS",
        "PACK-ACCES-6M-MENS",
        "PACK-ACCES-6M-COMPT",
        "PACK-ACCES-12M-MENS",
        "PACK-ACCES-12M-COMPT",
        "PACK-BUREAU-1M-MENS",
        "PACK-BUREAU-6M-MENS",
        "PACK-BUREAU-6M-COMPT",
        "PACK-BUREAU-12M-MENS",
        "PACK-BUREAU-12M-COMPT",
        "PACK-PRO-1M-MENS",
        "PACK-PRO-6M-MENS",
        "PACK-PRO-6M-COMPT",
        "PACK-PRO-12M-MENS",
        "PACK-PRO-12M-COMPT"
    ];

    public static async Task RunAsync()
    {
        await VerifyClientCatalogMatchesLegacyForPublicPacksAsync();
        await VerifyResolvedSubscriptionOfferMatchesLegacyForPublicPacksAsync();
        await VerifyProviderIdSelectionMatchesLegacyAsync();
        await VerifyShadowCatalogKeepsLegacyAuthoritativeAsync();
        await VerifyConfiguratorPathMatchesLegacyForPublicPacksAsync();
        await VerifyCartPathKeepsPublicPacksOutOfOneShotCartAsync();
    }

    private static async Task VerifyClientCatalogMatchesLegacyForPublicPacksAsync()
    {
        var store = new MockCommercialStore();
        var repository = new MockCommercialRepository(store);
        var adapter = CreateAdapter(repository);

        var legacyPacks = await ReadLegacyPublicPacksAsync(repository);
        var adapterPacks = (await adapter.GetClientCatalogAsync(CancellationToken.None))
            .Where(IsExpectedPack)
            .ToArray();

        Ensure(
            adapterPacks.Length == ExpectedPackReferences.Length,
            "L'adapter Billing doit exposer exactement les 20 PACK-* publics.");
        Ensure(
            adapterPacks.Select(offer => offer.ExternalReference)
                .SequenceEqual(ExpectedPackReferences, StringComparer.Ordinal),
            "L'adapter Billing doit conserver l'ordre legacy des 20 PACK-*.");

        for (var index = 0; index < ExpectedPackReferences.Length; index++)
        {
            EnsureSameOffer(
                legacyPacks[index],
                adapterPacks[index],
                $"PACK index {index}");
        }
    }

    private static async Task VerifyResolvedSubscriptionOfferMatchesLegacyForPublicPacksAsync()
    {
        var store = new MockCommercialStore();
        var repository = new MockCommercialRepository(store);
        var adapter = CreateAdapter(repository);
        var legacyPacks = await ReadLegacyPublicPacksAsync(repository);

        foreach (var legacy in legacyPacks)
        {
            var resolved = await adapter.ResolveSubscribableOfferAsync(
                legacy.Id,
                "billing",
                CancellationToken.None);

            EnsureSameOffer(
                legacy,
                resolved.Offer,
                legacy.ExternalReference ?? legacy.Id);
            Ensure(
                resolved.PriceAmountCents == legacy.PriceAmountCents
                && resolved.SetupFeeAmountCents == (legacy.SetupFeeAmountCents ?? 0)
                && resolved.BillingIntervalMonths == (legacy.BillingIntervalMonths ?? 1)
                && resolved.CommitmentMonths ==
                    (legacy.CommitmentMonths ?? legacy.BillingIntervalMonths ?? 1)
                && resolved.PaymentMode ==
                    (legacy.PaymentMode ?? CommercialStatuses.PaymentModeMonthly)
                && resolved.ProviderExternalId == string.Empty,
                $"Resolution Billing legacy inchangee pour {legacy.ExternalReference}.");
        }
    }

    private static async Task VerifyProviderIdSelectionMatchesLegacyAsync()
    {
        var store = new MockCommercialStore();
        lock (store.SyncRoot)
        {
            var offer = store.Offers.Single(candidate =>
                candidate.Id == "offer-pack-dossier-1m-monthly");
            offer.StripePriceIdTest = "price_test_legacy";
            offer.StripePriceIdLive = "price_live_legacy";
            offer.PayPalPlanIdSandbox = "P-SANDBOX-LEGACY";
            offer.PayPalPlanIdLive = "P-LIVE-LEGACY";
        }

        var repository = new MockCommercialRepository(store);
        var testAdapter = CreateAdapter(repository);
        var liveAdapter = CreateAdapter(
            repository,
            PayPalMode.Live,
            StripeMode.Live);

        var legacyOffer = (await repository.GetClientCatalogAsync(
                CancellationToken.None))
            .Single(offer => offer.Id == "offer-pack-dossier-1m-monthly");

        Ensure(
            testAdapter.ResolveProviderExternalId(legacyOffer, "stripe")
                == legacyOffer.StripePriceIdTest
            && testAdapter.ResolveProviderExternalId(legacyOffer, "paypal")
                == legacyOffer.PayPalPlanIdSandbox
            && testAdapter.ResolveProviderExternalId(legacyOffer, "unexpected")
                == legacyOffer.PayPalPlanIdSandbox
            && liveAdapter.ResolveProviderExternalId(legacyOffer, "stripe")
                == legacyOffer.StripePriceIdLive
            && liveAdapter.ResolveProviderExternalId(legacyOffer, "paypal")
                == legacyOffer.PayPalPlanIdLive,
            "L'adapter Billing doit appliquer la selection legacy test/live des ids fournisseur.");
    }

    private static async Task VerifyShadowCatalogKeepsLegacyAuthoritativeAsync()
    {
        var store = new MockCommercialStore();
        var repository = new MockCommercialRepository(store);
        var legacy = CreateAdapter(repository);
        var legacyOffer = (await ReadLegacyPublicPacksAsync(repository))[0];
        var mismatchingV2 = new FakeBillingCatalog(
            legacyOffer with
            {
                PriceAmountCents = legacyOffer.PriceAmountCents + 123,
                SetupFeeAmountCents = (legacyOffer.SetupFeeAmountCents ?? 0) + 1
            });
        var shadow = new ShadowBillingCatalogAdapter(
            legacy,
            mismatchingV2,
            new BillingV2RuntimeConfiguration(
                CatalogShadowModeEnabled: true,
                ProvisioningShadowModeEnabled: false,
                NewSubscriptionsEnabled: false,
                AuthoritativeCheckoutEnabled: false,
                FirstRealSubscriptionApproved: false,
                ProviderOutboxEnabled: false,
                ProviderExecutorEnabled: false,
                ProvisioningEnabled: false),
            NullLogger<ShadowBillingCatalogAdapter>.Instance);

        var resolved = await shadow.ResolveSubscribableOfferAsync(
            legacyOffer.Id,
            "billing",
            CancellationToken.None);

        Ensure(
            resolved.Offer.Id == legacyOffer.Id
            && resolved.PriceAmountCents == legacyOffer.PriceAmountCents
            && resolved.SetupFeeAmountCents == (legacyOffer.SetupFeeAmountCents ?? 0)
            && mismatchingV2.ResolveCalls == 1,
            "Le shadow catalog doit comparer V2 sans remplacer le resultat legacy.");

        var failingShadow = new ShadowBillingCatalogAdapter(
            legacy,
            new FakeBillingCatalog(legacyOffer, fail: true),
            new BillingV2RuntimeConfiguration(
                CatalogShadowModeEnabled: true,
                ProvisioningShadowModeEnabled: false,
                NewSubscriptionsEnabled: false,
                AuthoritativeCheckoutEnabled: false,
                FirstRealSubscriptionApproved: false,
                ProviderOutboxEnabled: false,
                ProviderExecutorEnabled: false,
                ProvisioningEnabled: false),
            NullLogger<ShadowBillingCatalogAdapter>.Instance);
        var stillLegacy = await failingShadow.ResolveSubscribableOfferAsync(
            legacyOffer.Id,
            "billing",
            CancellationToken.None);

        Ensure(
            stillLegacy.PriceAmountCents == legacyOffer.PriceAmountCents,
            "Une erreur du shadow V2 ne doit jamais casser la resolution legacy.");
    }


    private static async Task VerifyConfiguratorPathMatchesLegacyForPublicPacksAsync()
    {
        var store = new MockCommercialStore();
        var repository = new MockCommercialRepository(store);
        var adapter = CreateAdapter(repository);
        var configuration = new CatalogConfigurationService(
            adapter,
            new FiscalPolicy());
        var legacyPacks = await ReadLegacyPublicPacksAsync(repository);

        foreach (var legacy in legacyPacks)
        {
            var resolution = await configuration.ResolveAsync(
                CreateConfigurationInput(legacy),
                CancellationToken.None);
            var selection = resolution.PackSelection
                ?? throw new InvalidOperationException(
                    $"Aucune selection configurateur pour {legacy.ExternalReference}.");
            var simulation = resolution.PriceSimulation
                ?? throw new InvalidOperationException(
                    $"Aucune simulation configurateur pour {legacy.ExternalReference}.");

            Ensure(
                resolution.Status == "ok",
                $"{legacy.ExternalReference}: le configurateur doit accepter la variante legacy.");
            Ensure(
                selection.OfferId == legacy.Id
                && selection.OfferExternalReference == legacy.ExternalReference
                && selection.PackKey == legacy.PublicPackCode
                && selection.CommitmentMonths == legacy.CommitmentMonths
                && selection.PaymentMode == legacy.PaymentMode
                && selection.BillingIntervalMonths == legacy.BillingIntervalMonths
                && selection.BillingPriceAmountCents == legacy.PriceAmountCents
                && selection.SetupFeeAmountCents == (legacy.SetupFeeAmountCents ?? 0)
                && selection.FirstChargeAmountCents ==
                    legacy.PriceAmountCents + (legacy.SetupFeeAmountCents ?? 0)
                && selection.Currency == legacy.Currency,
                $"{legacy.ExternalReference}: la selection configurateur doit rester identique au catalogue legacy.");
            Ensure(
                simulation.FirstChargeExVatCents == selection.FirstChargeAmountCents
                && simulation.SetupPriceExVatCents == selection.SetupFeeAmountCents
                && simulation.RecurringItems.Count == 1
                && simulation.RecurringItems[0].OfferId == legacy.Id
                && simulation.RecurringItems[0].OfferExternalReference
                    == legacy.ExternalReference,
                $"{legacy.ExternalReference}: la simulation configurateur doit pointer vers la meme offre legacy.");
        }
    }

    private static async Task VerifyCartPathKeepsPublicPacksOutOfOneShotCartAsync()
    {
        var commercialStore = new MockCommercialStore();
        var repository = new MockCommercialRepository(commercialStore);
        var adapter = CreateAdapter(repository);
        var cartStore = new MockCartStore();
        var customerId = "customer-billing-catalog-compat";
        var legacyPacks = await ReadLegacyPublicPacksAsync(repository);
        lock (cartStore.SyncRoot)
        {
            cartStore.Items.AddRange(legacyPacks.Select(offer => new MockCartItem
            {
                CustomerId = customerId,
                OfferId = offer.Id,
                Quantity = 1
            }));
        }

        var cart = new CartService(
            new MockCartRepository(cartStore),
            adapter,
            repository,
            new NoopInvoiceIssuingService(),
            new FiscalPolicy(),
            NullLogger<CartService>.Instance);

        var summary = await cart.GetCartAsync(customerId, CancellationToken.None);

        Ensure(
            summary.ItemCount == 0
            && summary.Items.Count == 0
            && summary.SubtotalCents == 0,
            "Le panier doit continuer a exclure les 20 PACK-* recurrents du flux one-shot.");
        lock (cartStore.SyncRoot)
        {
            Ensure(
                cartStore.Items.Count == 0,
                "Le panier doit auto-nettoyer les PACK-* recurrents devenus ineligibles.");
        }
    }

    private static LegacyBillingCatalogAdapter CreateAdapter(
        ICommercialRepository repository,
        PayPalMode paypalMode = PayPalMode.Sandbox,
        StripeMode stripeMode = StripeMode.Test)
        => new(
            repository,
            new PayPalRuntimeConfiguration(paypalMode, "client", "secret"),
            new StripeRuntimeConfiguration(stripeMode));

    private static async Task<CommercialOfferSummary[]> ReadLegacyPublicPacksAsync(
        ICommercialRepository repository)
        => (await repository.GetClientCatalogAsync(CancellationToken.None))
            .Where(IsExpectedPack)
            .ToArray();

    private static bool IsExpectedPack(CommercialOfferSummary offer)
        => offer.ExternalReference is not null
            && ExpectedPackReferences.Contains(
                offer.ExternalReference,
                StringComparer.Ordinal);

    private static CatalogConfigurationInput CreateConfigurationInput(
        CommercialOfferSummary offer)
    {
        var commitmentMonths = offer.CommitmentMonths
            ?? throw new InvalidOperationException(
                $"{offer.ExternalReference}: engagement manquant.");
        var paymentMode = offer.PaymentMode
            ?? throw new InvalidOperationException(
                $"{offer.ExternalReference}: payment_mode manquant.");

        return offer.PublicPackCode switch
        {
            "pack-dossier-securise" => new(
                offer.PublicPackCode,
                commitmentMonths,
                paymentMode,
                1,
                32,
                false,
                false),
            "pack-acces-distance" => new(
                offer.PublicPackCode,
                commitmentMonths,
                paymentMode,
                1,
                32,
                true,
                false),
            "pack-bureau-windows-distance" => new(
                offer.PublicPackCode,
                commitmentMonths,
                paymentMode,
                1,
                32,
                true,
                true),
            "pack-pro-association" => new(
                offer.PublicPackCode,
                commitmentMonths,
                paymentMode,
                2,
                64,
                true,
                false),
            _ => throw new InvalidOperationException(
                $"{offer.ExternalReference}: pack public inconnu.")
        };
    }

    private static void EnsureSameOffer(
        CommercialOfferSummary expected,
        CommercialOfferSummary actual,
        string context)
    {
        Ensure(actual.Id == expected.Id, $"{context}: id inchange.");
        Ensure(actual.Name == expected.Name, $"{context}: nom inchange.");
        Ensure(actual.Description == expected.Description, $"{context}: description inchangee.");
        Ensure(actual.Category == expected.Category, $"{context}: categorie inchangee.");
        Ensure(actual.UnitLabel == expected.UnitLabel, $"{context}: unite inchangee.");
        Ensure(actual.PriceKind == expected.PriceKind, $"{context}: type de prix inchange.");
        Ensure(
            actual.PriceAmountCents == expected.PriceAmountCents,
            $"{context}: prix inchange.");
        Ensure(actual.Currency == expected.Currency, $"{context}: devise inchangee.");
        Ensure(
            actual.TaxRateBasisPoints == expected.TaxRateBasisPoints,
            $"{context}: TVA inchangee.");
        Ensure(actual.FiscalRegime == expected.FiscalRegime, $"{context}: regime fiscal inchange.");
        Ensure(actual.FiscalMention == expected.FiscalMention, $"{context}: mention fiscale inchangee.");
        Ensure(
            actual.ExternalReference == expected.ExternalReference,
            $"{context}: reference externe inchangee.");
        Ensure(
            actual.TechnicalServiceReferences.SequenceEqual(
                expected.TechnicalServiceReferences,
                StringComparer.Ordinal),
            $"{context}: references techniques inchangees.");
        Ensure(
            actual.ProvisioningGroupSamAccountNames.SequenceEqual(
                expected.ProvisioningGroupSamAccountNames,
                StringComparer.Ordinal),
            $"{context}: groupes provisioning inchanges.");
        Ensure(actual.Status == expected.Status, $"{context}: statut inchange.");
        Ensure(actual.DisplayOrder == expected.DisplayOrder, $"{context}: ordre inchange.");
        Ensure(actual.BillingCadence == expected.BillingCadence, $"{context}: cadence inchangee.");
        Ensure(
            actual.SetupFeeAmountCents == expected.SetupFeeAmountCents,
            $"{context}: frais de mise en service inchanges.");
        Ensure(
            actual.BillingIntervalMonths == expected.BillingIntervalMonths,
            $"{context}: intervalle inchange.");
        Ensure(
            actual.CommitmentMonths == expected.CommitmentMonths,
            $"{context}: engagement inchange.");
        Ensure(actual.PaymentMode == expected.PaymentMode, $"{context}: payment_mode inchange.");
        Ensure(actual.PublicPackCode == expected.PublicPackCode, $"{context}: pack public inchange.");
        Ensure(actual.PayPalPlanIdSandbox == expected.PayPalPlanIdSandbox, $"{context}: PayPal sandbox inchange.");
        Ensure(actual.PayPalPlanIdLive == expected.PayPalPlanIdLive, $"{context}: PayPal live inchange.");
        Ensure(actual.StripePriceIdTest == expected.StripePriceIdTest, $"{context}: Stripe test inchange.");
        Ensure(actual.StripePriceIdLive == expected.StripePriceIdLive, $"{context}: Stripe live inchange.");
        Ensure(actual.CreatedAt == expected.CreatedAt, $"{context}: creation inchangee.");
        Ensure(actual.UpdatedAt == expected.UpdatedAt, $"{context}: mise a jour inchangee.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class NoopInvoiceIssuingService : IInvoiceIssuingService
    {
        public Task<IssueInvoiceResult> IssueInvoiceAsync(
            string documentId,
            bool sendEmail,
            string correlationId,
            CancellationToken cancellationToken)
            => Task.FromResult(new IssueInvoiceResult(true, "noop", string.Empty));

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
            => Task.FromResult(new IssueInvoiceResult(true, "noop", string.Empty));
    }

    private sealed class FakeBillingCatalog : IBillingCatalog
    {
        private readonly CommercialOfferSummary _offer;
        private readonly bool _fail;

        public FakeBillingCatalog(
            CommercialOfferSummary offer,
            bool fail = false)
        {
            _offer = offer;
            _fail = fail;
        }

        public bool IsPersistent => true;

        public int ResolveCalls { get; private set; }

        public Task<IReadOnlyList<CommercialOfferSummary>> GetClientCatalogAsync(
            CancellationToken cancellationToken)
            => _fail
                ? throw new InvalidOperationException("shadow failure")
                : Task.FromResult<IReadOnlyList<CommercialOfferSummary>>([_offer]);

        public Task<IReadOnlyList<CommercialOfferSummary>> GetAdminCatalogAsync(
            CancellationToken cancellationToken)
            => GetClientCatalogAsync(cancellationToken);

        public Task<CommercialOfferSummary?> FindClientOfferByIdAsync(
            string offerId,
            CancellationToken cancellationToken)
            => _fail
                ? throw new InvalidOperationException("shadow failure")
                : Task.FromResult<CommercialOfferSummary?>(_offer);

        public Task<BillingCatalogResolvedOffer> ResolveSubscribableOfferAsync(
            string offerId,
            string rail,
            CancellationToken cancellationToken)
        {
            ResolveCalls++;
            if (_fail)
            {
                throw new InvalidOperationException("shadow failure");
            }

            return Task.FromResult(new BillingCatalogResolvedOffer(
                _offer,
                _offer.PriceAmountCents,
                _offer.SetupFeeAmountCents ?? 0,
                _offer.BillingIntervalMonths ?? 1,
                _offer.CommitmentMonths ?? _offer.BillingIntervalMonths ?? 1,
                _offer.PaymentMode ?? CommercialStatuses.PaymentModeMonthly,
                _offer.StripePriceIdTest,
                _offer.PayPalPlanIdSandbox,
                string.Empty));
        }

        public string? ResolveProviderExternalId(
            CommercialOfferSummary offer,
            string rail)
            => string.Empty;
    }
}
