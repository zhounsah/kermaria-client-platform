namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Regles pures pour les composants tarifaires d'un droit unique. Cette policy
/// empeche qu'un upgrade reprenne implicitement un setup initial : seul un prix
/// marque explicitement <c>subscription_change</c> peut etre charge a nouveau.
/// </summary>
public sealed record BillingV2PriceComponentSnapshot(
    string ServicePriceId,
    string BillingCadence,
    string ChargeTrigger,
    long AmountCents,
    string Currency,
    bool DiscountEligible,
    int DisplayOrder);

public static class BillingV2ComponentizedPricingPolicy
{
    public const string InitialSubscription = "initial_subscription";
    public const string SubscriptionChange = "subscription_change";

    public static IReadOnlyList<BillingV2PriceComponentSnapshot> ForInitialCharge(
        IReadOnlyList<BillingV2PriceComponentSnapshot> components)
        => components
            .Where(component => component.BillingCadence == "monthly"
                || (component.BillingCadence == "one_time"
                    && component.ChargeTrigger == InitialSubscription))
            .OrderBy(component => component.DisplayOrder)
            .ThenBy(component => component.ServicePriceId, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<BillingV2PriceComponentSnapshot> ForSubscriptionChange(
        IReadOnlyList<BillingV2PriceComponentSnapshot> components)
        => components
            .Where(component => component.BillingCadence == "monthly"
                || (component.BillingCadence == "one_time"
                    && component.ChargeTrigger == SubscriptionChange))
            .OrderBy(component => component.DisplayOrder)
            .ThenBy(component => component.ServicePriceId, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<BillingV2PriceComponentSnapshot> ForRenewal(
        IReadOnlyList<BillingV2PriceComponentSnapshot> components)
        => components
            .Where(component => component.BillingCadence == "monthly")
            .OrderBy(component => component.DisplayOrder)
            .ThenBy(component => component.ServicePriceId, StringComparer.Ordinal)
            .ToArray();
}
