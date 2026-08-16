namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Fabrique le devis affiche au client.
///
/// Le montant vient de BillingV2PricingEngine, jamais d'une addition faite
/// dans le navigateur : la page peut afficher cette projection, elle ne peut
/// pas la produire. Le devis n'engage rien — il ne cree ni intention, ni
/// BillingEvent, ni session provider.
///
/// Depuis la souscription V2 native, une configuration personnalisee est
/// souscriptible : la disponibilite du checkout ne depend plus de l'existence
/// d'une offre legacy correspondante. L'offre legacy reste exposee quand elle
/// existe, pour compatibilite, mais n'est plus une condition.
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
        // La resolution a deja verifie l'existence de l'option : la remise
        // affichee est donc toujours celle du catalogue, jamais une valeur de
        // repli fabriquee ici.
        var paymentOption = commitment.Option(selection.PaymentMode)!;
        var months = Math.Max(1, commitment.Months);

        var result = pricing.Calculate(new BillingV2PricingRequest(
            resolution.Lines
                .Select(line => new BillingV2PricingItem(
                    line.ServiceCode,
                    line.ServiceCode,
                    line.TierCode,
                    line.ServiceCode,
                    line.UnitAmountCents,
                    line.Quantity,
                    BillingV2BillingCadences.Monthly,
                    line.DiscountEligible))
                .ToArray(),
            paymentOption.DiscountBasisPoints,
            selection.PaymentMode,
            months,
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            DateTime.UtcNow));

        var upfront = string.Equals(
            selection.PaymentMode,
            BillingV2PaymentModes.Upfront,
            StringComparison.Ordinal);

        // En comptant, le total contractuel est encaisse en une fois ; le
        // "prix mensuel" affiche n'est alors qu'un equivalent, derive du total
        // serveur et jamais recompose dans le navigateur.
        var commitmentTotalAfterDiscountCents = upfront
            ? result.TotalDueNowCents
            : checked(result.PayableRecurringAmountCents * months
                + result.OneTimeSubtotalCents);
        var commitmentTotalBeforeDiscountCents = checked(
            result.RecurringSubtotalCents * months
            + result.OneTimeSubtotalCents);
        var monthlyAfterDiscountCents = upfront
            ? (result.UpfrontRecurringAmountCents + months / 2) / months
            : result.PayableRecurringAmountCents;

        // Coherence devis / rail : le perimetre de lancement est la meme
        // autorite des deux cotes. Sans cela, l'interface pouvait proposer un
        // mode que le dispatch refusait ensuite en dur, laissant le client
        // devant une souscription sans page de paiement.
        var scope = BillingV2LaunchScope.EvaluateCheckout(
            "stripe",
            selection.PaymentMode,
            taxAmountCents: 0);
        var checkoutAuthorized = checkoutReadiness.Authorized && scope.IsValid;
        var checkoutReasonCode = checkoutReadiness.Authorized
            ? scope.ReasonCode
            : checkoutReadiness.ReasonCode;

        var route = catalog.CheckoutRoutes.FirstOrDefault(
            item => string.Equals(
                        item.PresetCode,
                        selection.PresetCode,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.CommitmentCode,
                        selection.CommitmentCode,
                        StringComparison.Ordinal));

        return new BillingV2PublicQuote(
            selection.PresetCode,
            selection.CommitmentCode,
            commitment.Months,
            selection.PaymentMode,
            paymentOption.DiscountBasisPoints,
            catalog.Currency,
            result.RecurringSubtotalCents,
            result.RecurringDiscountCents,
            monthlyAfterDiscountCents,
            result.OneTimeSubtotalCents,
            result.TotalDueNowCents,
            commitmentTotalBeforeDiscountCents,
            commitmentTotalAfterDiscountCents,
            checked(commitmentTotalBeforeDiscountCents
                - commitmentTotalAfterDiscountCents),
            resolution.Lines,
            resolution.MatchesPresetBaseline,
            checkoutAuthorized,
            checkoutAuthorized
                ? BillingV2PublicCheckoutModes.Native
                : BillingV2PublicCheckoutModes.Unavailable,
            // Conserve pour compatibilite : une formule standard payee au mois
            // reste rattachable a son offre legacy. Le checkout, lui, n'en
            // depend plus.
            resolution.MatchesPresetBaseline && !upfront
                ? route?.LegacyOfferId
                : null,
            checkoutReasonCode);
    }
}
