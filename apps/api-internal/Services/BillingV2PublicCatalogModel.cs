namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Projection commerciale publique du catalogue Billing V2.
///
/// Ce fichier ne contient QUE de la lecture et de la projection. Il n'ecrit
/// rien, ne cree aucun abonnement et ne touche ni au PaymentAttempt, ni au
/// settlement, ni au document, ni au renouvellement. Le montant final reste
/// calcule par BillingV2PricingEngine cote serveur : la selection envoyee par
/// le navigateur est une intention, jamais un montant.
/// </summary>
public sealed record BillingV2PublicTier(
    string Code,
    string Label,
    string? Description,
    int? NumericValue,
    long MonthlyAmountCents,
    bool PublicSelectable);

public sealed record BillingV2PublicService(
    string Code,
    string Name,
    string Category,
    string ScopeType,
    long? FlatMonthlyAmountCents,
    IReadOnlyList<BillingV2PublicTier> Tiers,
    bool DiscountEligible = true);

public sealed record BillingV2PublicPresetItem(
    string ServiceCode,
    string? TierCode,
    string ScopeTemplate,
    int Quantity,
    long AmountCents,
    bool CustomerEditable);

public sealed record BillingV2PublicPreset(
    string Code,
    string Name,
    string Description,
    int DisplayOrder,
    IReadOnlyList<BillingV2PublicPresetItem> Items)
{
    /// <summary>
    /// Total mensuel de la configuration recommandee, sans remise. Calcule
    /// ici pour que la page publique n'ait jamais a additionner des prix
    /// elle-meme.
    /// </summary>
    public long BaselineMonthlyAmountCents
        => Items.Sum(item => item.AmountCents * item.Quantity);
}

public sealed record BillingV2PublicCommitment(
    string Code,
    string Name,
    int Months,
    int DiscountBasisPoints);

public sealed record BillingV2PublicCheckoutRoute(
    string PresetCode,
    string CommitmentCode,
    string LegacyOfferId);

public sealed record BillingV2PublicCatalogSnapshot(
    string Source,
    string Currency,
    IReadOnlyList<BillingV2PublicPreset> Presets,
    IReadOnlyList<BillingV2PublicService> Services,
    IReadOnlyList<BillingV2PublicCommitment> Commitments,
    IReadOnlyList<BillingV2PublicCheckoutRoute> CheckoutRoutes);

/// <summary>
/// Intention de configuration exprimee par le client. Aucun montant : le
/// navigateur ne transmet que des codes catalogue.
/// </summary>
public sealed record BillingV2PublicSelection(
    string PresetCode,
    string CommitmentCode,
    string StoragePersonalTierCode,
    bool BackupPersonal,
    string? StorageSharedTierCode,
    bool BackupShared,
    string? VpnTierCode,
    bool RemoteDesktop,
    int AdditionalUsers,
    bool SupportPlus);

/// <summary>
/// Charge utile acceptee du navigateur. Elle ne porte aucun montant : tout
/// champ tarifaire envoye par le client serait ignore, la seule autorite
/// etant le catalogue serveur et BillingV2PricingEngine.
/// </summary>
public sealed class BillingV2PublicSelectionInput
{
    public string? PresetCode { get; set; }

    public string? CommitmentCode { get; set; }

    public string? StoragePersonalTierCode { get; set; }

    public bool BackupPersonal { get; set; }

    public string? StorageSharedTierCode { get; set; }

    public bool BackupShared { get; set; }

    public string? VpnTierCode { get; set; }

    public bool RemoteDesktop { get; set; }

    public int AdditionalUsers { get; set; }

    public bool SupportPlus { get; set; }

    public BillingV2PublicSelection ToSelection()
        => new(
            (PresetCode ?? string.Empty).Trim(),
            string.IsNullOrWhiteSpace(CommitmentCode)
                ? "FLEX"
                : CommitmentCode.Trim(),
            (StoragePersonalTierCode ?? string.Empty).Trim(),
            BackupPersonal,
            string.IsNullOrWhiteSpace(StorageSharedTierCode)
                ? null
                : StorageSharedTierCode.Trim(),
            BackupShared,
            string.IsNullOrWhiteSpace(VpnTierCode)
                ? null
                : VpnTierCode.Trim(),
            RemoteDesktop,
            AdditionalUsers,
            SupportPlus);
}

public sealed record BillingV2PublicQuoteLine(
    string ServiceCode,
    string Label,
    string? Detail,
    int Quantity,
    long UnitAmountCents,
    long AmountCents,
    bool DiscountEligible);

public sealed record BillingV2PublicQuote(
    string PresetCode,
    string CommitmentCode,
    int CommitmentMonths,
    int DiscountBasisPoints,
    string Currency,
    long MonthlyBeforeDiscountCents,
    long MonthlyDiscountCents,
    long MonthlyAfterDiscountCents,
    long OneTimeCents,
    long TotalDueNowCents,
    IReadOnlyList<BillingV2PublicQuoteLine> Lines,
    bool MatchesPresetBaseline,
    bool CheckoutAvailable,
    string? CheckoutLegacyOfferId,
    string CheckoutReasonCode);

public static class BillingV2PublicCatalogCodes
{
    public const string BaseService = "BASE-SERVICE";
    public const string StoragePersonal = "STORAGE-PERSONAL";
    public const string StorageShared = "STORAGE-SHARED";
    public const string BackupPersonal = "BACKUP-PERSONAL";
    public const string BackupShared = "BACKUP-SHARED";
    public const string VpnAccess = "VPN-ACCESS";
    public const string RemoteDesktop = "RDS-ACCESS";
    public const string AdditionalUser = "USER-ADDITIONAL";
    public const string SupportPlus = "SUPPORT-PLUS";

    /// <summary>
    /// Nombre maximum d'utilisateurs supplementaires exposes publiquement.
    /// Au dela, la demande passe par un contact commercial : le catalogue V2
    /// ne porte aucun palier de volume.
    /// </summary>
    public const int MaxAdditionalUsers = 10;
}
