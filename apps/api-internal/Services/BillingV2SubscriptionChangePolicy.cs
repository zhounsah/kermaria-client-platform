namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Politique pure du premier changement V2.1. Elle ne reecrit jamais un item :
/// un successeur componentized est cree apres fermeture temporelle de l'ancien
/// droit. Les frais initiaux ne sont pas reintroduits lors d'un upgrade.
/// </summary>
public static class BillingV2SubscriptionChangePolicy
{
    public const string Upgrade = "upgrade";
    public const string Downgrade = "downgrade";

    public static DateTime ResolveEffectiveAt(
        string changeKind,
        DateTime requestedAtUtc,
        DateTime nextRenewalBoundaryUtc)
        => changeKind switch
        {
            Upgrade => requestedAtUtc,
            Downgrade => nextRenewalBoundaryUtc,
            _ => throw new InvalidOperationException("BILLING_V2_CHANGE_KIND_UNKNOWN")
        };

    public static IReadOnlyList<BillingV2PriceComponentSnapshot> ComponentsForSuccessor(
        string changeKind,
        IReadOnlyList<BillingV2PriceComponentSnapshot> newComponents)
        => changeKind switch
        {
            Upgrade => BillingV2ComponentizedPricingPolicy.ForSubscriptionChange(newComponents),
            Downgrade => newComponents
                .Where(component => component.BillingCadence == "monthly")
                .OrderBy(component => component.DisplayOrder)
                .ToArray(),
            _ => throw new InvalidOperationException("BILLING_V2_CHANGE_KIND_UNKNOWN")
        };
}
