namespace Kermaria.ApiInternal.Contracts;

public sealed record CatalogConfigurationInput(
    string? PackKey,
    int? CommitmentMonths,
    string? PaymentMode,
    int? Users,
    int? StorageGb,
    bool? NeedsVpn,
    bool? NeedsWindowsDesktop);

public sealed record CatalogConfigurationRequestSnapshot(
    string PackKey,
    int CommitmentMonths,
    string PaymentMode,
    int? Users,
    int? StorageGb,
    bool? NeedsVpn,
    bool? NeedsWindowsDesktop,
    string RequestedAt);

public sealed record CatalogPriceLine(
    string Label,
    string OfferId,
    string OfferExternalReference,
    int Quantity,
    int UnitPriceExVatCents,
    int UnitPriceIncVatCents,
    int TotalPriceExVatCents,
    int TotalPriceIncVatCents,
    int? TaxRateBasisPoints,
    string FiscalRegime,
    string FiscalMention);

public sealed record CatalogPriceSimulation(
    int MonthlyPriceExVatCents,
    int MonthlyPriceIncVatCents,
    int SetupPriceExVatCents,
    int SetupPriceIncVatCents,
    int FirstChargeExVatCents,
    int FirstChargeIncVatCents,
    int? VatRateBasisPoints,
    string FiscalRegime,
    string FiscalMention,
    IReadOnlyList<CatalogPriceLine> RecurringItems,
    IReadOnlyList<CatalogPriceLine> OneTimeItems,
    IReadOnlyList<string> Warnings);

public sealed record CatalogConfigurationResolution(
    string Status,
    CatalogConfigurationInput RequestedConfiguration,
    CatalogConfigurationInput? ResolvedConfiguration,
    string? SuggestedPackKey,
    SignupPackSelectionSnapshot? PackSelection,
    CatalogPriceSimulation? PriceSimulation,
    IReadOnlyList<string> Warnings);

public sealed record CatalogConfigurationSnapshot(
    CatalogConfigurationRequestSnapshot RequestedConfiguration,
    CatalogConfigurationResolution Resolution);
