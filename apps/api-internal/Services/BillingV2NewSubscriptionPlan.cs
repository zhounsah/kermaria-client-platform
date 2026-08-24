using System.Data;
using System.Globalization;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2NewSubscriptionPlan(
    IReadOnlyList<BillingV2NewSubscriptionUserPlan> Users,
    IReadOnlyList<BillingV2NewSubscriptionItemPlan> Items);

public sealed record BillingV2NewSubscriptionUserPlan(
    string Id,
    string? IdentityReference,
    string DisplayName,
    string? Email,
    bool IsPrimary);

public sealed record BillingV2NewSubscriptionPresetItem(
    string PresetItemId,
    string ServiceId,
    string? TierId,
    string ServicePriceId,
    string ServiceCode,
    string? TierCode,
    string PriceCode,
    string ScopeTemplate,
    int Quantity,
    long AmountCents,
    string Currency,
    string BillingCadence,
    bool DiscountEligible);

public sealed record BillingV2NewSubscriptionItemPlan(
    string Id,
    string PriceComponentId,
    string? UserId,
    string ServiceId,
    string? TierId,
    string ServicePriceId,
    string ScopeType,
    int Quantity,
    long AmountCentsSnapshot,
    string Currency,
    bool DiscountEligibleSnapshot,
    string Source)
{
    // Les colonnes historiques de l'item restent un miroir de compatibilite.
    // L'autorite V2.1 est exclusivement cette collection de composants.
    public IReadOnlyList<BillingV2NewSubscriptionPriceComponentPlan> PriceComponents { get; init; }
        = Array.Empty<BillingV2NewSubscriptionPriceComponentPlan>();
}

public sealed record BillingV2NewSubscriptionPriceComponentPlan(
    string Id,
    string PresetItemId,
    string ServicePriceId,
    long AmountCentsSnapshot,
    string Currency,
    bool DiscountEligibleSnapshot,
    int DisplayOrder);

public static class BillingV2NewSubscriptionPlanner
{
    public static BillingV2NewSubscriptionPlan Plan(
        PortalSessionContext session,
        IReadOnlyList<BillingV2NewSubscriptionPresetItem> presetItems)
    {
        var users = new List<BillingV2NewSubscriptionUserPlan>();
        var items = new List<BillingV2NewSubscriptionItemPlan>();
        BillingV2NewSubscriptionUserPlan? primaryUser = null;
        var additionalUserIndex = 0;

        foreach (var group in presetItems.GroupBy(item => new
                 {
                     item.ServiceId,
                     item.TierId,
                     item.ScopeTemplate,
                     item.Quantity
                 }))
        {
            var presetItem = group.First();
            var user = ResolveUserForScope(
                session,
                presetItem.ScopeTemplate,
                users,
                ref primaryUser,
                ref additionalUserIndex);
            var components = group.Select((component, index) =>
                    new BillingV2NewSubscriptionPriceComponentPlan(
                        Guid.NewGuid().ToString("D"),
                        component.PresetItemId,
                        component.ServicePriceId,
                        component.AmountCents,
                        component.Currency,
                        component.DiscountEligible,
                        index))
                .ToArray();
            var mirror = components.First();
            items.Add(new BillingV2NewSubscriptionItemPlan(
                Guid.NewGuid().ToString("D"),
                mirror.Id,
                user?.Id,
                presetItem.ServiceId,
                presetItem.TierId,
                mirror.ServicePriceId,
                user is null ? "subscription" : "user",
                presetItem.Quantity,
                mirror.AmountCentsSnapshot,
                mirror.Currency,
                mirror.DiscountEligibleSnapshot,
                "preset")
            {
                PriceComponents = components
            });
        }

        return new BillingV2NewSubscriptionPlan(users, items);
    }

    private static BillingV2NewSubscriptionUserPlan? ResolveUserForScope(
        PortalSessionContext session,
        string scopeTemplate,
        List<BillingV2NewSubscriptionUserPlan> users,
        ref BillingV2NewSubscriptionUserPlan? primaryUser,
        ref int additionalUserIndex)
    {
        if (string.Equals(
                scopeTemplate,
                "subscription",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(
                scopeTemplate,
                "primary_user",
                StringComparison.OrdinalIgnoreCase))
        {
            primaryUser ??= new BillingV2NewSubscriptionUserPlan(
                Guid.NewGuid().ToString("D"),
                session.UserId,
                session.DisplayName,
                session.Email,
                IsPrimary: true);
            if (!users.Contains(primaryUser))
            {
                users.Add(primaryUser);
            }

            return primaryUser;
        }

        if (string.Equals(
                scopeTemplate,
                "additional_user",
                StringComparison.OrdinalIgnoreCase))
        {
            additionalUserIndex++;
            var user = new BillingV2NewSubscriptionUserPlan(
                Guid.NewGuid().ToString("D"),
                IdentityReference: null,
                $"Utilisateur additionnel {additionalUserIndex}",
                Email: null,
                IsPrimary: false);
            users.Add(user);
            return user;
        }

        throw new ArgumentException(
            $"Unsupported Billing V2 preset item scope '{scopeTemplate}'.",
            nameof(scopeTemplate));
    }
}
