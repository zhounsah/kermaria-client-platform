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
/// d'une offre legacy correspondante.
///
/// Les lignes du devis viennent des composantes tarifaires resolues par
/// <see cref="BillingV2PublicSelectionPolicy"/>, avec leur cadence reelle. Un
/// service mensuel assorti de frais de mise en service produit donc ici les
/// deux memes lignes que le checkout authoritative facturera : le devis public
/// et le resolver ne peuvent plus diverger.
/// </summary>
public static class BillingV2PublicQuoteBuilder
{
    public const string CheckoutCustomConfiguration =
        "BILLING_V2_PUBLIC_CUSTOM_CONFIGURATION_NOT_CHECKOUTABLE";

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

        // Engagement facultatif : un achat ponctuel n'en a pas. Sans terme, la
        // duree vaut 1 mois et la remise 0 — aucune remise ne peut etre
        // accordee par defaut.
        var commitment = selection.CommitmentCode is { Length: > 0 } code
            ? catalog.Commitments.FirstOrDefault(
                item => string.Equals(item.Code, code, StringComparison.Ordinal))
            : null;
        // La resolution a deja verifie l'existence de l'option : la remise
        // affichee est donc toujours celle du catalogue, jamais une valeur de
        // repli fabriquee ici.
        var discountBasisPoints =
            commitment?.Option(selection.PaymentMode)?.DiscountBasisPoints ?? 0;
        var months = Math.Max(1, commitment?.Months ?? 1);

        var result = pricing.Calculate(new BillingV2PricingRequest(
            resolution.Lines
                .Select(line => new BillingV2PricingItem(
                    $"{line.ServiceCode}/{line.TierCode ?? "-"}#{line.BillingCadence}",
                    line.ServiceCode,
                    line.TierCode,
                    line.ServiceCode,
                    line.UnitAmountCents,
                    line.Quantity,
                    // La cadence de la ligne, pas celle du service : un setup
                    // ponctuel ne doit jamais entrer dans le MRR ni dans la
                    // projection d'engagement.
                    line.BillingCadence,
                    line.DiscountEligible))
                .ToArray(),
            discountBasisPoints,
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

        return new BillingV2PublicQuote(
            selection.PresetCode,
            selection.CommitmentCode,
            months,
            selection.PaymentMode,
            discountBasisPoints,
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
            checkoutReasonCode);
    }
}
