namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Fabrique le devis affiche au client.
///
/// Le montant vient de BillingV2PricingEngine, jamais d'une addition faite
/// dans le navigateur : la page peut afficher cette projection, elle ne peut
/// pas la produire. Le devis n'engage rien — il ne cree ni intention, ni
/// BillingEvent, ni session provider.
/// </summary>
public static class BillingV2PublicQuoteBuilder
{
    public const string CheckoutCustomConfiguration =
        "BILLING_V2_PUBLIC_CUSTOM_CONFIGURATION_NOT_CHECKOUTABLE";

    public const string CheckoutRouteMissing =
        "BILLING_V2_PUBLIC_CHECKOUT_ROUTE_MISSING";

    public static BillingV2PublicQuote Build(
        BillingV2PublicCatalogSnapshot catalog,
        BillingV2PublicSelection selection,
        IBillingV2PricingEngine pricing,
        BillingV2AuthoritativeCheckoutReadiness checkoutReadiness)
    {
        var resolution = BillingV2PublicSelectionPolicy.Resolve(
            catalog,
            selection);
        if (!resolution.Resolved)
        {
            throw new InvalidOperationException(resolution.ReasonCode);
        }

        var commitment = catalog.Commitments.First(
            item => string.Equals(
                item.Code,
                selection.CommitmentCode,
                StringComparison.Ordinal));

        var result = pricing.Calculate(new BillingV2PricingRequest(
            resolution.Lines
                .Select(line => new BillingV2PricingItem(
                    line.ServiceCode,
                    line.ServiceCode,
                    line.Detail,
                    line.ServiceCode,
                    line.UnitAmountCents,
                    line.Quantity,
                    BillingV2BillingCadences.Monthly,
                    line.DiscountEligible))
                .ToArray(),
            commitment.DiscountBasisPoints,
            BillingV2PaymentModes.Monthly,
            Math.Max(1, commitment.Months),
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            DateTime.UtcNow));

        var route = catalog.CheckoutRoutes.FirstOrDefault(
            item => string.Equals(
                        item.PresetCode,
                        selection.PresetCode,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.CommitmentCode,
                        selection.CommitmentCode,
                        StringComparison.Ordinal));

        var (checkoutAvailable, checkoutReasonCode) = ResolveCheckout(
            resolution.MatchesPresetBaseline,
            route,
            checkoutReadiness);

        return new BillingV2PublicQuote(
            selection.PresetCode,
            selection.CommitmentCode,
            commitment.Months,
            commitment.DiscountBasisPoints,
            catalog.Currency,
            result.RecurringSubtotalCents,
            result.RecurringDiscountCents,
            result.PayableRecurringAmountCents,
            result.OneTimeSubtotalCents,
            result.TotalDueNowCents,
            resolution.Lines,
            resolution.MatchesPresetBaseline,
            checkoutAvailable,
            checkoutAvailable ? route?.LegacyOfferId : null,
            checkoutReasonCode);
    }

    /// <summary>
    /// Le parcours authoritative valide est indexe par offre legacy, donc par
    /// formule standard. Une configuration personnalisee n'a pas d'offre
    /// correspondante : on le dit explicitement au lieu de fabriquer une route
    /// approximative qui facturerait autre chose que ce qui est affiche.
    /// </summary>
    private static (bool Available, string ReasonCode) ResolveCheckout(
        bool matchesPresetBaseline,
        BillingV2PublicCheckoutRoute? route,
        BillingV2AuthoritativeCheckoutReadiness checkoutReadiness)
    {
        if (!matchesPresetBaseline)
        {
            return (false, CheckoutCustomConfiguration);
        }

        if (route is null)
        {
            return (false, CheckoutRouteMissing);
        }

        return checkoutReadiness.Authorized
            ? (true, checkoutReadiness.ReasonCode)
            : (false, checkoutReadiness.ReasonCode);
    }
}
