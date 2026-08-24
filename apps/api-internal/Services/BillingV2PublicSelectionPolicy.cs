namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2PublicSelectionResolution(
    bool Resolved,
    string ReasonCode,
    IReadOnlyList<BillingV2PublicQuoteLine> Lines,
    IReadOnlyList<BillingV2PublicSelectionComponent> Components,
    bool MatchesPresetBaseline);

/// <summary>
/// Traduit une intention de configuration en lignes tarifaires, en faisant
/// respecter les dependances du catalogue V2.
///
/// Politique pure et testable : aucune I/O, aucun etat. Elle echoue en ferme
/// sur toute selection incoherente plutot que de la corriger en silence — une
/// sauvegarde dont le palier ne suit pas le stockage couvert serait une
/// promesse commerciale que le provisioning ne peut pas tenir.
/// </summary>
public static class BillingV2PublicSelectionPolicy
{
    public const string Ok = "BILLING_V2_PUBLIC_SELECTION_OK";

    public static BillingV2PublicSelection Baseline(
        BillingV2PublicPreset preset)
    {
        var storagePersonal = FindItem(
            preset,
            BillingV2PublicCatalogCodes.StoragePersonal);
        var storageShared = FindItem(
            preset,
            BillingV2PublicCatalogCodes.StorageShared);
        var vpn = FindItem(preset, BillingV2PublicCatalogCodes.VpnAccess);
        var additionalUser = FindItem(
            preset,
            BillingV2PublicCatalogCodes.AdditionalUser);

        return new BillingV2PublicSelection(
            preset.Code,
            "FLEX",
            BillingV2PaymentModes.Monthly,
            storagePersonal?.TierCode ?? "32",
            FindItem(preset, BillingV2PublicCatalogCodes.BackupPersonal)
                is not null,
            storageShared?.TierCode,
            FindItem(preset, BillingV2PublicCatalogCodes.BackupShared)
                is not null,
            vpn?.TierCode,
            FindItem(preset, BillingV2PublicCatalogCodes.RemoteDesktop)
                is not null,
            additionalUser?.Quantity ?? 0,
            FindItem(preset, BillingV2PublicCatalogCodes.SupportPlus)
                is not null);
    }

    public static BillingV2PublicSelectionResolution Resolve(
        BillingV2PublicCatalogSnapshot catalog,
        BillingV2PublicSelection selection)
    {
        // Un engagement n'est exige que s'il est demande. Une selection
        // ponctuelle n'engage a rien : lui imposer un terme reviendrait a
        // fabriquer une duree contractuelle que le client n'a pas souscrite.
        if (selection.CommitmentCode is { Length: > 0 })
        {
            var commitment = catalog.Commitments.FirstOrDefault(
                item => string.Equals(
                    item.Code,
                    selection.CommitmentCode,
                    StringComparison.Ordinal));
            if (commitment is null)
            {
                return Blocked("BILLING_V2_PUBLIC_COMMITMENT_UNKNOWN");
            }

            // Le couple (duree, mode de reglement) doit exister dans le
            // catalogue : c'est lui qui porte la remise, pas la duree seule. Un
            // "comptant" sur une duree qui ne l'autorise pas est refuse en
            // ferme plutot que rabattu silencieusement sur le mensuel.
            if (commitment.Option(selection.PaymentMode) is null)
            {
                return Blocked("BILLING_V2_PUBLIC_PAYMENT_MODE_UNAVAILABLE");
            }
        }
        else if (string.Equals(
                     selection.PaymentMode,
                     BillingV2PaymentModes.Upfront,
                     StringComparison.Ordinal))
        {
            // Le comptant est un mode de reglement d'un engagement. Sans
            // engagement il n'a pas de duree a prepayer.
            return Blocked("BILLING_V2_PUBLIC_PAYMENT_MODE_UNAVAILABLE");
        }

        // V2.1 : la forme generique est deja une intention en composants. Les
        // anciens champs restent routes vers la logique historique ci-dessous
        // pour ne pas casser /formules ni le diagnostic public.
        if (selection.Components is { Count: > 0 })
        {
            return ResolveGeneric(catalog, selection);
        }

        // Hors composants explicites, la composition est celle d'une formule :
        // sans formule, il n'y a rien a facturer.
        if (selection.PresetCode is not { Length: > 0 })
        {
            return Blocked("BILLING_V2_PUBLIC_SELECTION_EMPTY");
        }

        var preset = catalog.Presets.FirstOrDefault(
            item => string.Equals(
                item.Code,
                selection.PresetCode,
                StringComparison.Ordinal));
        if (preset is null)
        {
            return Blocked("BILLING_V2_PUBLIC_PRESET_UNKNOWN");
        }

        if (selection.AdditionalUsers < 0
            || selection.AdditionalUsers
                > BillingV2PublicCatalogCodes.MaxAdditionalUsers)
        {
            return Blocked("BILLING_V2_PUBLIC_ADDITIONAL_USERS_OUT_OF_RANGE");
        }

        if (selection.BackupShared
            && string.IsNullOrWhiteSpace(selection.StorageSharedTierCode))
        {
            return Blocked("BILLING_V2_PUBLIC_SHARED_BACKUP_WITHOUT_STORAGE");
        }

        // Chaque entree est un (service, palier, quantite) retenu. Les lignes
        // tarifaires n'en sont derivees qu'a la fin : un meme composant peut
        // produire plusieurs lignes (mensuel + mise en service), mais reste UN
        // seul composant du point de vue de la selection.
        var picks = new List<PickedComponent>();

        var baseService = FindService(
            catalog,
            BillingV2PublicCatalogCodes.BaseService);
        if (baseService is null || !HasBillableComponent(baseService, null))
        {
            return Blocked("BILLING_V2_PUBLIC_BASE_SERVICE_UNPRICED");
        }

        picks.Add(new PickedComponent(baseService, null, 1));

        var storagePersonal = ResolveTier(
            catalog,
            BillingV2PublicCatalogCodes.StoragePersonal,
            selection.StoragePersonalTierCode,
            requirePublic: true);
        if (storagePersonal is null)
        {
            return Blocked("BILLING_V2_PUBLIC_STORAGE_PERSONAL_TIER_UNKNOWN");
        }

        picks.Add(new PickedComponent(
            storagePersonal.Value.Service,
            storagePersonal.Value.Tier,
            1));

        if (selection.BackupPersonal)
        {
            // Dependance same_numeric_value : le palier de sauvegarde suit la
            // capacite couverte, il n'est jamais choisi librement.
            var backupPersonal = ResolveTierByNumericValue(
                catalog,
                BillingV2PublicCatalogCodes.BackupPersonal,
                storagePersonal.Value.Tier.NumericValue);
            if (backupPersonal is null)
            {
                return Blocked(
                    "BILLING_V2_PUBLIC_BACKUP_PERSONAL_TIER_UNAVAILABLE");
            }

            picks.Add(new PickedComponent(
                backupPersonal.Value.Service,
                backupPersonal.Value.Tier,
                1));
        }

        if (!string.IsNullOrWhiteSpace(selection.StorageSharedTierCode))
        {
            var storageShared = ResolveTier(
                catalog,
                BillingV2PublicCatalogCodes.StorageShared,
                selection.StorageSharedTierCode,
                requirePublic: true);
            if (storageShared is null)
            {
                return Blocked("BILLING_V2_PUBLIC_STORAGE_SHARED_TIER_UNKNOWN");
            }

            picks.Add(new PickedComponent(
                storageShared.Value.Service,
                storageShared.Value.Tier,
                1));

            if (selection.BackupShared)
            {
                var backupShared = ResolveTierByNumericValue(
                    catalog,
                    BillingV2PublicCatalogCodes.BackupShared,
                    storageShared.Value.Tier.NumericValue);
                if (backupShared is null)
                {
                    return Blocked(
                        "BILLING_V2_PUBLIC_BACKUP_SHARED_TIER_UNAVAILABLE");
                }

                picks.Add(new PickedComponent(
                    backupShared.Value.Service,
                    backupShared.Value.Tier,
                    1));
            }
        }

        if (!string.IsNullOrWhiteSpace(selection.VpnTierCode))
        {
            var vpn = ResolveTier(
                catalog,
                BillingV2PublicCatalogCodes.VpnAccess,
                selection.VpnTierCode,
                requirePublic: true);
            if (vpn is null)
            {
                return Blocked("BILLING_V2_PUBLIC_VPN_TIER_UNKNOWN");
            }

            picks.Add(new PickedComponent(vpn.Value.Service, vpn.Value.Tier, 1));
        }

        if (selection.RemoteDesktop)
        {
            var remoteDesktop = FindService(
                catalog,
                BillingV2PublicCatalogCodes.RemoteDesktop);
            if (remoteDesktop is null
                || !HasBillableComponent(remoteDesktop, null))
            {
                return Blocked("BILLING_V2_PUBLIC_RDS_UNPRICED");
            }

            picks.Add(new PickedComponent(remoteDesktop, null, 1));
        }

        if (selection.AdditionalUsers > 0)
        {
            var additionalUser = FindService(
                catalog,
                BillingV2PublicCatalogCodes.AdditionalUser);
            if (additionalUser is null
                || !HasBillableComponent(additionalUser, null))
            {
                return Blocked("BILLING_V2_PUBLIC_ADDITIONAL_USER_UNPRICED");
            }

            picks.Add(new PickedComponent(
                additionalUser,
                null,
                selection.AdditionalUsers));
        }

        if (selection.SupportPlus)
        {
            var supportPlus = FindService(
                catalog,
                BillingV2PublicCatalogCodes.SupportPlus);
            if (supportPlus is null || !HasBillableComponent(supportPlus, null))
            {
                return Blocked("BILLING_V2_PUBLIC_SUPPORT_PLUS_UNPRICED");
            }

            picks.Add(new PickedComponent(supportPlus, null, 1));
        }

        var baseline = Baseline(preset) with
        {
            CommitmentCode = selection.CommitmentCode
        };

        return new BillingV2PublicSelectionResolution(
            Resolved: true,
            Ok,
            Expand(picks),
            // Les composants sont la projection stricte des elements retenus en
            // codes catalogue : ce que le serveur rejouera pour retrouver les
            // vraies lignes de prix ne peut pas diverger de ce qui a ete
            // affiche. Un element retenu reste UN composant meme quand il
            // produit plusieurs lignes tarifaires.
            picks
                .Select(pick => new BillingV2PublicSelectionComponent(
                    pick.Service.Code,
                    pick.Tier?.Code,
                    pick.Quantity))
                .ToArray(),
            MatchesBaseline(baseline, selection));
    }

    /// <summary>
    /// L'engagement ne fait pas partie de la composition : deux durees d'une
    /// meme formule restent la formule standard, et restent donc
    /// souscriptibles par le parcours authoritative existant.
    /// </summary>
    private static bool MatchesBaseline(
        BillingV2PublicSelection baseline,
        BillingV2PublicSelection selection)
        => string.Equals(
               baseline.StoragePersonalTierCode,
               selection.StoragePersonalTierCode,
               StringComparison.Ordinal)
           && baseline.BackupPersonal == selection.BackupPersonal
           && string.Equals(
               baseline.StorageSharedTierCode ?? string.Empty,
               selection.StorageSharedTierCode ?? string.Empty,
               StringComparison.Ordinal)
           && baseline.BackupShared == selection.BackupShared
           && string.Equals(
               baseline.VpnTierCode ?? string.Empty,
               selection.VpnTierCode ?? string.Empty,
               StringComparison.Ordinal)
           && baseline.RemoteDesktop == selection.RemoteDesktop
           && baseline.AdditionalUsers == selection.AdditionalUsers
           && baseline.SupportPlus == selection.SupportPlus;

    private static BillingV2PublicSelectionResolution ResolveGeneric(
        BillingV2PublicCatalogSnapshot catalog,
        BillingV2PublicSelection selection)
    {
        // Une selection directe n'a pas de formule d'origine. Si elle en
        // declare une, elle doit exister : un code inconnu signale un
        // catalogue perime cote navigateur, pas une composition libre.
        if (selection.PresetCode is { Length: > 0 }
            && !catalog.Presets.Any(item => string.Equals(
                item.Code,
                selection.PresetCode,
                StringComparison.Ordinal)))
        {
            return Blocked("BILLING_V2_PUBLIC_PRESET_UNKNOWN");
        }

        var canonicalComponents = selection.Components!
            .GroupBy(
                component => new
                {
                    Service = component.ServiceCode.Trim(),
                    Tier = component.TierCode?.Trim()
                })
            .OrderBy(group => group.Key.Service, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Tier, StringComparer.Ordinal)
            .Select(group => new BillingV2PublicSelectionComponent(
                group.Key.Service,
                group.Key.Tier,
                group.Sum(component => component.Quantity)))
            .ToArray();
        if (canonicalComponents.Length == 0
            || canonicalComponents.Any(component =>
                string.IsNullOrWhiteSpace(component.ServiceCode)
                || component.Quantity <= 0))
        {
            return Blocked("BILLING_V2_PUBLIC_COMPONENT_INVALID");
        }

        var picks = new List<PickedComponent>();
        foreach (var component in canonicalComponents)
        {
            var service = FindService(catalog, component.ServiceCode);
            if (service is null)
            {
                return Blocked("BILLING_V2_PUBLIC_COMPONENT_SERVICE_UNKNOWN");
            }
            if (!service.SelfServiceOrderable)
            {
                return Blocked("BILLING_V2_PUBLIC_COMPONENT_REQUIRES_QUOTE");
            }

            BillingV2PublicTier? tier = null;
            if (component.TierCode is not null)
            {
                tier = service.Tiers.FirstOrDefault(candidate =>
                    string.Equals(candidate.Code, component.TierCode, StringComparison.Ordinal));
                if (tier is null || !tier.PublicSelectable)
                {
                    return Blocked("BILLING_V2_PUBLIC_COMPONENT_TIER_UNKNOWN");
                }
            }
            else if (service.Tiers.Count > 0)
            {
                return Blocked("BILLING_V2_PUBLIC_COMPONENT_TIER_REQUIRED");
            }

            // Un service sans aucune composante facturable au declenchement
            // initial n'est pas commandable : ni mensuel, ni ponctuel.
            if (!HasBillableComponent(service, tier))
            {
                return Blocked("BILLING_V2_PUBLIC_COMPONENT_UNPRICED");
            }

            picks.Add(new PickedComponent(service, tier, component.Quantity));
        }

        return new BillingV2PublicSelectionResolution(
            true,
            Ok,
            Expand(picks),
            canonicalComponents,
            MatchesPresetBaseline: false);
    }

    /// <summary>
    /// Element retenu par la selection : un couple (service, palier) et sa
    /// quantite. Il ne porte aucun montant — les composantes tarifaires sont
    /// resolues au moment de produire les lignes.
    /// </summary>
    private sealed record PickedComponent(
        BillingV2PublicService Service,
        BillingV2PublicTier? Tier,
        int Quantity);

    private static BillingV2PublicPresetItem? FindItem(
        BillingV2PublicPreset preset,
        string serviceCode)
        => preset.Items.FirstOrDefault(
            item => string.Equals(
                item.ServiceCode,
                serviceCode,
                StringComparison.Ordinal));

    private static BillingV2PublicService? FindService(
        BillingV2PublicCatalogSnapshot catalog,
        string serviceCode)
        => catalog.Services.FirstOrDefault(
            item => string.Equals(
                item.Code,
                serviceCode,
                StringComparison.Ordinal));

    private static (BillingV2PublicService Service, BillingV2PublicTier Tier)?
        ResolveTier(
            BillingV2PublicCatalogSnapshot catalog,
            string serviceCode,
            string? tierCode,
            bool requirePublic)
    {
        var service = FindService(catalog, serviceCode);
        if (service is null || string.IsNullOrWhiteSpace(tierCode))
        {
            return null;
        }

        var tier = service.Tiers.FirstOrDefault(
            item => string.Equals(item.Code, tierCode, StringComparison.Ordinal));
        if (tier is null || (requirePublic && !tier.PublicSelectable))
        {
            return null;
        }

        return (service, tier);
    }

    private static (BillingV2PublicService Service, BillingV2PublicTier Tier)?
        ResolveTierByNumericValue(
            BillingV2PublicCatalogSnapshot catalog,
            string serviceCode,
            int? numericValue)
    {
        var service = FindService(catalog, serviceCode);
        if (service is null || numericValue is null)
        {
            return null;
        }

        var tier = service.Tiers.FirstOrDefault(
            item => item.NumericValue == numericValue);
        return tier is null ? null : (service, tier);
    }

    /// <summary>
    /// Composantes facturables a la souscription initiale pour un element
    /// retenu, dans un ordre stable : le mensuel d'abord, puis le ponctuel.
    /// </summary>
    private static IReadOnlyList<BillingV2PublicPriceComponent>
        BillableComponents(
            BillingV2PublicService service,
            BillingV2PublicTier? tier)
        => service.ComponentsFor(tier)
            .Where(component => component.AppliesToInitialSubscription)
            .OrderByDescending(component => component.IsRecurring)
            .ThenBy(component => component.BillingCadence, StringComparer.Ordinal)
            .ThenBy(component => component.PriceCode ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

    private static bool HasBillableComponent(
        BillingV2PublicService service,
        BillingV2PublicTier? tier)
        => BillableComponents(service, tier).Count > 0;

    /// <summary>
    /// Developpe les elements retenus en lignes de devis : une ligne par
    /// composante tarifaire applicable. Un VPS mensuel avec frais de mise en
    /// service produit donc deux lignes, et le devis public affiche exactement
    /// ce que le checkout authoritative facturera.
    /// </summary>
    private static IReadOnlyList<BillingV2PublicQuoteLine> Expand(
        IReadOnlyList<PickedComponent> picks)
        => picks
            .SelectMany(pick => BillableComponents(pick.Service, pick.Tier)
                .Select(component => Line(
                    pick.Service,
                    pick.Tier,
                    pick.Quantity,
                    component)))
            .ToArray();

    private static BillingV2PublicQuoteLine Line(
        BillingV2PublicService service,
        BillingV2PublicTier? tier,
        int quantity,
        BillingV2PublicPriceComponent component)
        => new(
            service.Code,
            tier?.Code,
            service.Name,
            LineDetail(tier, component),
            quantity,
            component.AmountCents,
            checked(component.AmountCents * quantity),
            component.DiscountEligible,
            component.BillingCadence);

    private const string SetupFeeLabel = "frais de mise en service";

    private static string? LineDetail(
        BillingV2PublicTier? tier,
        BillingV2PublicPriceComponent component)
    {
        if (component.IsRecurring)
        {
            return tier?.Label;
        }

        return tier?.Label is { Length: > 0 } label
            ? $"{label} — {SetupFeeLabel}"
            : "Frais de mise en service";
    }

    private static BillingV2PublicSelectionResolution Blocked(string reasonCode)
        => new(Resolved: false, reasonCode, [], [], MatchesPresetBaseline: false);
}
