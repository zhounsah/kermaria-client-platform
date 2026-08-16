namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2CheckoutProviderLine(
    string ServicePriceId,
    string ProviderExternalId,
    int Quantity,
    long AmountCents);

public sealed record BillingV2CheckoutPlan(
    string Provider,
    string Environment,
    string Currency,
    long RecurringAmountCents,
    long OneTimeAmountCents,
    long TotalDueNowCents,
    IReadOnlyList<BillingV2CheckoutProviderLine> ProviderLines);

public static class BillingV2CheckoutPlanner
{
    public static BillingV2CheckoutPlan Plan(
        BillingV2CheckoutReadinessDecision readiness,
        IReadOnlyList<BillingV2NewSubscriptionPresetItem> presetItems,
        BillingV2PricingResult pricing)
    {
        if (!readiness.Authorized)
        {
            throw new InvalidOperationException(
                $"Billing V2 checkout is not ready: {readiness.ReasonCode}.");
        }

        if (presetItems.Count == 0)
        {
            throw new InvalidOperationException(
                "Billing V2 checkout requires at least one preset item.");
        }

        var provider = readiness.ProviderMappings.Provider;
        var requiresMappings = BillingV2ProviderPricingAuthorityPolicy
            .RequiresProviderPriceMappings(provider);

        if (requiresMappings
            && readiness.ProviderMappings.ResolvedMappings.Count == 0)
        {
            throw new InvalidOperationException(
                "Billing V2 checkout requires resolved provider mappings.");
        }

        var providerMappings = readiness.ProviderMappings.ResolvedMappings
            .ToDictionary(
                mapping => mapping.ServicePriceId,
                mapping => mapping,
                StringComparer.Ordinal);
        var providerLines = new List<BillingV2CheckoutProviderLine>();
        foreach (var item in presetItems)
        {
            if (!providerMappings.TryGetValue(
                    item.ServicePriceId,
                    out var mapping))
            {
                // Rail tarifiant en ligne : la ligne provider n'existe pas, le
                // montant vient du BillingEvent. On ne fabrique pas de
                // reference provider fictive.
                if (!requiresMappings)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Billing V2 checkout is missing provider mapping for service price {item.ServicePriceId}.");
            }

            providerLines.Add(new BillingV2CheckoutProviderLine(
                item.ServicePriceId,
                mapping.ProviderExternalId,
                item.Quantity,
                checked(item.AmountCents * item.Quantity)));
        }

        var currency = presetItems
            .Select(item => item.Currency)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SingleOrDefault()
            ?? "EUR";

        return new BillingV2CheckoutPlan(
            string.IsNullOrWhiteSpace(provider)
                ? readiness.ProviderMappings.ResolvedMappings[0].Provider
                : provider,
            string.IsNullOrWhiteSpace(readiness.ProviderMappings.Environment)
                ? readiness.ProviderMappings.ResolvedMappings[0].Environment
                : readiness.ProviderMappings.Environment,
            currency,
            pricing.PayableRecurringAmountCents,
            pricing.OneTimeSubtotalCents,
            pricing.TotalDueNowCents,
            providerLines);
    }
}
