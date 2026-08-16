using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Services;

public interface ICatalogConfigurationService
{
    Task<CatalogConfigurationResolution> ResolveAsync(
        CatalogConfigurationInput input,
        CancellationToken cancellationToken);

    CatalogConfigurationSnapshot CreateSnapshot(
        CatalogConfigurationResolution resolution);
}

public sealed class CatalogConfigurationService : ICatalogConfigurationService
{
    private const string PackDossier = "pack-dossier-securise";
    private const string PackAcces = "pack-acces-distance";
    private const string PackBureau = "pack-bureau-windows-distance";
    private const string PackPro = "pack-pro-association";
    private const string StatusOk = "ok";
    private const string StatusRequiresDifferentOffer =
        "requires_different_offer";
    private const string StatusRequiresQuote = "requires_quote";

    private static readonly IReadOnlyDictionary<string, PackCapabilities>
        Capabilities = new Dictionary<string, PackCapabilities>(
            StringComparer.Ordinal)
        {
            [PackDossier] = new(1, 32, SupportsVpn: false, SupportsWindows: false),
            [PackAcces] = new(1, 32, SupportsVpn: true, SupportsWindows: false),
            [PackBureau] = new(1, 32, SupportsVpn: true, SupportsWindows: true),
            [PackPro] = new(2, 64, SupportsVpn: true, SupportsWindows: false)
        };

    private readonly IBillingCatalog _billingCatalog;
    private readonly IFiscalPolicy _fiscalPolicy;

    public CatalogConfigurationService(
        IBillingCatalog billingCatalog,
        IFiscalPolicy fiscalPolicy)
    {
        _billingCatalog = billingCatalog;
        _fiscalPolicy = fiscalPolicy;
    }

    public async Task<CatalogConfigurationResolution> ResolveAsync(
        CatalogConfigurationInput input,
        CancellationToken cancellationToken)
    {
        var requested = Normalize(input);
        var decision = ChooseTargetPack(requested);
        if (decision.Status == StatusRequiresQuote)
        {
            return new CatalogConfigurationResolution(
                decision.Status,
                requested,
                null,
                null,
                null,
                null,
                decision.Warnings);
        }

        var targetPackKey = decision.TargetPackKey ?? requested.PackKey!;
        var catalog = await _billingCatalog.GetClientCatalogAsync(
            cancellationToken);
        var variant = ResolveVariant(
            catalog,
            targetPackKey,
            requested.CommitmentMonths!.Value,
            requested.PaymentMode!);
        if (variant is null)
        {
            return new CatalogConfigurationResolution(
                StatusRequiresQuote,
                requested,
                null,
                null,
                null,
                null,
                [.. decision.Warnings, "variant_unavailable"]);
        }

        var targetCapabilities = Capabilities[targetPackKey];
        var resolvedConfiguration = requested with
        {
            PackKey = targetPackKey,
            NeedsVpn = targetCapabilities.SupportsVpn ? true : requested.NeedsVpn,
            NeedsWindowsDesktop = targetCapabilities.SupportsWindows
                ? true
                : requested.NeedsWindowsDesktop
        };
        var packSelection = CreatePackSelection(
            variant,
            targetPackKey,
            requested.CommitmentMonths.Value,
            requested.PaymentMode!);
        var simulation = CreatePriceSimulation(
            variant,
            packSelection,
            decision.Warnings,
            _fiscalPolicy);
        var status = string.Equals(
                targetPackKey,
                requested.PackKey,
                StringComparison.Ordinal)
            ? StatusOk
            : StatusRequiresDifferentOffer;

        return new CatalogConfigurationResolution(
            status,
            requested,
            resolvedConfiguration,
            status == StatusRequiresDifferentOffer ? targetPackKey : null,
            packSelection,
            simulation,
            decision.Warnings);
    }

    public CatalogConfigurationSnapshot CreateSnapshot(
        CatalogConfigurationResolution resolution)
    {
        var requested = resolution.RequestedConfiguration;
        if (requested.PackKey is null
            || requested.CommitmentMonths is null
            || requested.PaymentMode is null)
        {
            throw new PortalValidationException();
        }

        return new CatalogConfigurationSnapshot(
            new CatalogConfigurationRequestSnapshot(
                requested.PackKey,
                requested.CommitmentMonths.Value,
                requested.PaymentMode,
                requested.Users,
                requested.StorageGb,
                requested.NeedsVpn,
                requested.NeedsWindowsDesktop,
                DateTime.UtcNow.ToString("O")),
            resolution);
    }

    private static CatalogConfigurationInput Normalize(
        CatalogConfigurationInput input)
    {
        var packKey = input.PackKey?.Trim();
        if (packKey is null || !Capabilities.ContainsKey(packKey))
        {
            throw new PortalValidationException();
        }

        var commitmentMonths = input.CommitmentMonths;
        if (commitmentMonths is not 1 and not 6 and not 12)
        {
            throw new PortalValidationException();
        }

        var paymentMode = input.PaymentMode?.Trim().ToLowerInvariant();
        if (commitmentMonths == 1)
        {
            if (paymentMode != CommercialStatuses.PaymentModeMonthly)
            {
                throw new PortalValidationException();
            }
        }
        else if (paymentMode is not CommercialStatuses.PaymentModeMonthly
                 and not CommercialStatuses.PaymentModeUpfront)
        {
            throw new PortalValidationException();
        }

        var users = input.Users;
        if (users is < 1 or > 50)
        {
            throw new PortalValidationException();
        }

        var storageGb = input.StorageGb;
        if (storageGb is not null && storageGb is not 8 and not 32 and not 64)
        {
            throw new PortalValidationException();
        }

        var capabilities = Capabilities[packKey];
        return new CatalogConfigurationInput(
            packKey,
            commitmentMonths,
            paymentMode,
            users,
            storageGb,
            capabilities.SupportsVpn ? true : input.NeedsVpn,
            capabilities.SupportsWindows ? true : input.NeedsWindowsDesktop);
    }

    private static ConfigurationDecision ChooseTargetPack(
        CatalogConfigurationInput requested)
    {
        var warnings = new List<string>();
        var users = requested.Users ?? 1;
        var storageGb = requested.StorageGb;
        if (storageGb is null)
        {
            warnings.Add("storage_unknown");
        }

        if (users > 2)
        {
            warnings.Add("users_not_standard");
            return new ConfigurationDecision(
                StatusRequiresQuote,
                null,
                warnings);
        }

        if (storageGb > 64)
        {
            warnings.Add("storage_not_standard");
            return new ConfigurationDecision(
                StatusRequiresQuote,
                null,
                warnings);
        }

        var needsWindows = requested.NeedsWindowsDesktop == true;
        var needsVpn = requested.NeedsVpn == true;
        if (needsWindows && users > 1)
        {
            warnings.Add("windows_team_not_standard");
            return new ConfigurationDecision(
                StatusRequiresQuote,
                null,
                warnings);
        }

        if (needsWindows && storageGb > 32)
        {
            warnings.Add("windows_storage_not_standard");
            return new ConfigurationDecision(
                StatusRequiresQuote,
                null,
                warnings);
        }

        var targetPackKey =
            needsWindows ? PackBureau
            : users > 1 || storageGb > 32 ? PackPro
            : needsVpn ? PackAcces
            : requested.PackKey!;

        var requestedCapabilities = Capabilities[requested.PackKey!];
        if (!Satisfies(requestedCapabilities, users, storageGb, needsVpn, needsWindows))
        {
            warnings.Add("requested_pack_adjusted");
            return new ConfigurationDecision(
                StatusRequiresDifferentOffer,
                targetPackKey,
                warnings);
        }

        return new ConfigurationDecision(StatusOk, requested.PackKey!, warnings);
    }

    private static bool Satisfies(
        PackCapabilities capabilities,
        int users,
        int? storageGb,
        bool needsVpn,
        bool needsWindows)
        => capabilities.IncludedUsers >= users
           && (storageGb is null || capabilities.IncludedStorageGb >= storageGb)
           && (!needsVpn || capabilities.SupportsVpn)
           && (!needsWindows || capabilities.SupportsWindows);

    private static CommercialOfferSummary? ResolveVariant(
        IReadOnlyList<CommercialOfferSummary> catalog,
        string packKey,
        int commitmentMonths,
        string paymentMode)
        => catalog.FirstOrDefault(offer =>
            string.Equals(offer.Status, CommercialStatuses.OfferActive, StringComparison.Ordinal)
            && string.Equals(offer.BillingCadence, CommercialStatuses.CadenceMonthly, StringComparison.Ordinal)
            && string.Equals(offer.PublicPackCode, packKey, StringComparison.Ordinal)
            && offer.CommitmentMonths == commitmentMonths
            && string.Equals(offer.PaymentMode, paymentMode, StringComparison.Ordinal)
            && offer.PriceAmountCents > 0);

    private static SignupPackSelectionSnapshot CreatePackSelection(
        CommercialOfferSummary offer,
        string packKey,
        int commitmentMonths,
        string paymentMode)
    {
        var billingIntervalMonths =
            offer.BillingIntervalMonths
            ?? (paymentMode == CommercialStatuses.PaymentModeUpfront
                ? commitmentMonths
                : 1);
        var billingPriceAmountCents = offer.PriceAmountCents;
        var monthlyPriceAmountCents = billingIntervalMonths > 1
            ? (int)Math.Round(
                billingPriceAmountCents / (decimal)commitmentMonths,
                0,
                MidpointRounding.AwayFromZero)
            : billingPriceAmountCents;
        var setupFeeAmountCents = offer.SetupFeeAmountCents ?? 0;

        return new SignupPackSelectionSnapshot(
            packKey,
            offer.Name,
            offer.Id,
            offer.ExternalReference
                ?? throw new PortalValidationException(),
            commitmentMonths,
            paymentMode,
            billingIntervalMonths,
            DiscountForCommitment(commitmentMonths),
            monthlyPriceAmountCents,
            billingPriceAmountCents,
            setupFeeAmountCents,
            billingPriceAmountCents + setupFeeAmountCents,
            offer.FiscalRegime,
            offer.FiscalMention,
            "EUR");
    }

    private static CatalogPriceSimulation CreatePriceSimulation(
        CommercialOfferSummary offer,
        SignupPackSelectionSnapshot packSelection,
        IReadOnlyList<string> warnings,
        IFiscalPolicy fiscalPolicy)
    {
        var fiscal = fiscalPolicy.Resolve(offer.TaxRateBasisPoints);
        var monthlyInc = fiscalPolicy.AmountIncludingTax(
            packSelection.MonthlyPriceAmountCents,
            offer.TaxRateBasisPoints);
        var setupInc = fiscalPolicy.AmountIncludingTax(
            packSelection.SetupFeeAmountCents,
            offer.TaxRateBasisPoints);
        var firstChargeInc = fiscalPolicy.AmountIncludingTax(
            packSelection.FirstChargeAmountCents,
            offer.TaxRateBasisPoints);
        var recurringLine = new CatalogPriceLine(
            offer.Name,
            offer.Id,
            packSelection.OfferExternalReference,
            1,
            packSelection.MonthlyPriceAmountCents,
            monthlyInc,
            packSelection.MonthlyPriceAmountCents,
            monthlyInc,
            fiscal.TaxRateBasisPoints,
            fiscal.FiscalRegime,
            fiscal.FiscalMention);
        var oneTimeItems = packSelection.SetupFeeAmountCents > 0
            ? new[]
            {
                new CatalogPriceLine(
                    "Mise en service",
                    offer.Id,
                    packSelection.OfferExternalReference,
                    1,
                    packSelection.SetupFeeAmountCents,
                    setupInc,
                    packSelection.SetupFeeAmountCents,
                    setupInc,
                    fiscal.TaxRateBasisPoints,
                    fiscal.FiscalRegime,
                    fiscal.FiscalMention)
            }
            : Array.Empty<CatalogPriceLine>();

        return new CatalogPriceSimulation(
            packSelection.MonthlyPriceAmountCents,
            monthlyInc,
            packSelection.SetupFeeAmountCents,
            setupInc,
            packSelection.FirstChargeAmountCents,
            firstChargeInc,
            fiscal.TaxRateBasisPoints,
            fiscal.FiscalRegime,
            fiscal.FiscalMention,
            [recurringLine],
            oneTimeItems,
            warnings);
    }

    private static int DiscountForCommitment(int commitmentMonths)
        => commitmentMonths switch
        {
            6 => 10,
            12 => 20,
            _ => 0
        };

    private sealed record PackCapabilities(
        int IncludedUsers,
        int IncludedStorageGb,
        bool SupportsVpn,
        bool SupportsWindows);

    private sealed record ConfigurationDecision(
        string Status,
        string? TargetPackKey,
        IReadOnlyList<string> Warnings);
}
