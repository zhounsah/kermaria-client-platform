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
        var preset = catalog.Presets.FirstOrDefault(
            item => string.Equals(
                item.Code,
                selection.PresetCode,
                StringComparison.Ordinal));
        if (preset is null)
        {
            return Blocked("BILLING_V2_PUBLIC_PRESET_UNKNOWN");
        }

        var commitment = catalog.Commitments.FirstOrDefault(
            item => string.Equals(
                item.Code,
                selection.CommitmentCode,
                StringComparison.Ordinal));
        if (commitment is null)
        {
            return Blocked("BILLING_V2_PUBLIC_COMMITMENT_UNKNOWN");
        }

        // Le couple (duree, mode de reglement) doit exister dans le catalogue :
        // c'est lui qui porte la remise, pas la duree seule. Un "comptant" sur
        // une duree qui ne l'autorise pas est refuse en ferme plutot que
        // rabattu silencieusement sur le mensuel.
        if (commitment.Option(selection.PaymentMode) is null)
        {
            return Blocked("BILLING_V2_PUBLIC_PAYMENT_MODE_UNAVAILABLE");
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

        var lines = new List<BillingV2PublicQuoteLine>();

        var baseService = FindService(
            catalog,
            BillingV2PublicCatalogCodes.BaseService);
        if (baseService?.FlatMonthlyAmountCents is null)
        {
            return Blocked("BILLING_V2_PUBLIC_BASE_SERVICE_UNPRICED");
        }

        lines.Add(Line(baseService, null, 1));

        var storagePersonal = ResolveTier(
            catalog,
            BillingV2PublicCatalogCodes.StoragePersonal,
            selection.StoragePersonalTierCode,
            requirePublic: true);
        if (storagePersonal is null)
        {
            return Blocked("BILLING_V2_PUBLIC_STORAGE_PERSONAL_TIER_UNKNOWN");
        }

        lines.Add(Line(storagePersonal.Value.Service, storagePersonal.Value.Tier, 1));

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

            lines.Add(Line(
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

            lines.Add(Line(storageShared.Value.Service, storageShared.Value.Tier, 1));

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

                lines.Add(Line(
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

            lines.Add(Line(vpn.Value.Service, vpn.Value.Tier, 1));
        }

        if (selection.RemoteDesktop)
        {
            var remoteDesktop = FindService(
                catalog,
                BillingV2PublicCatalogCodes.RemoteDesktop);
            if (remoteDesktop?.FlatMonthlyAmountCents is null)
            {
                return Blocked("BILLING_V2_PUBLIC_RDS_UNPRICED");
            }

            lines.Add(Line(remoteDesktop, null, 1));
        }

        if (selection.AdditionalUsers > 0)
        {
            var additionalUser = FindService(
                catalog,
                BillingV2PublicCatalogCodes.AdditionalUser);
            if (additionalUser?.FlatMonthlyAmountCents is null)
            {
                return Blocked("BILLING_V2_PUBLIC_ADDITIONAL_USER_UNPRICED");
            }

            lines.Add(Line(additionalUser, null, selection.AdditionalUsers));
        }

        if (selection.SupportPlus)
        {
            var supportPlus = FindService(
                catalog,
                BillingV2PublicCatalogCodes.SupportPlus);
            if (supportPlus?.FlatMonthlyAmountCents is null)
            {
                return Blocked("BILLING_V2_PUBLIC_SUPPORT_PLUS_UNPRICED");
            }

            lines.Add(Line(supportPlus, null, 1));
        }

        var baseline = Baseline(preset) with
        {
            CommitmentCode = selection.CommitmentCode
        };

        return new BillingV2PublicSelectionResolution(
            Resolved: true,
            Ok,
            lines,
            // Les composants sont la projection stricte des lignes en codes
            // catalogue : ce que le serveur rejouera pour retrouver les vraies
            // lignes de prix ne peut pas diverger de ce qui a ete affiche.
            lines
                .Select(line => new BillingV2PublicSelectionComponent(
                    line.ServiceCode,
                    line.TierCode,
                    line.Quantity))
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

    private static BillingV2PublicQuoteLine Line(
        BillingV2PublicService service,
        BillingV2PublicTier? tier,
        int quantity)
    {
        var unitAmountCents = tier?.MonthlyAmountCents
            ?? service.FlatMonthlyAmountCents
            ?? 0;
        return new BillingV2PublicQuoteLine(
            service.Code,
            tier?.Code,
            service.Name,
            tier?.Label,
            quantity,
            unitAmountCents,
            checked(unitAmountCents * quantity),
            service.DiscountEligible);
    }

    private static BillingV2PublicSelectionResolution Blocked(string reasonCode)
        => new(Resolved: false, reasonCode, [], [], MatchesPresetBaseline: false);
}
