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
    bool DiscountEligible = true,
    bool PublicVisible = true,
    bool SelfServiceOrderable = true);

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

/// <summary>
/// Variante de reglement d'un engagement. La remise n'est pas portee par la
/// duree seule : 6 mois payes au mois et 6 mois payes comptant sont deux
/// options distinctes, exactement comme en base
/// (`billing_v2_commitment_payment_options`).
/// </summary>
public sealed record BillingV2PublicPaymentOption(
    string PaymentMode,
    int DiscountBasisPoints);

public sealed record BillingV2PublicCommitment(
    string Code,
    string Name,
    int Months,
    IReadOnlyList<BillingV2PublicPaymentOption> PaymentOptions)
{
    public BillingV2PublicPaymentOption? Option(string paymentMode)
        => PaymentOptions.FirstOrDefault(
            option => string.Equals(
                option.PaymentMode,
                paymentMode,
                StringComparison.Ordinal));
}

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
    string PaymentMode,
    string StoragePersonalTierCode,
    bool BackupPersonal,
    string? StorageSharedTierCode,
    bool BackupShared,
    string? VpnTierCode,
    bool RemoteDesktop,
    int AdditionalUsers,
    bool SupportPlus,
    IReadOnlyList<BillingV2PublicSelectionComponent>? Components = null)
{
    /// <summary>
    /// Forme canonique de la selection metier. Elle sert d'ancre d'idempotence
    /// cote serveur : deux configurations differentes ne peuvent pas se
    /// retrouver rattachees a la meme intention, et un rafraichissement de la
    /// page retombe sur l'intention deja ouverte pour la meme configuration.
    ///
    /// Aucun montant n'y entre : seuls des codes catalogue.
    /// </summary>
    public string Canonical()
    {
        if (Components is { Count: > 0 })
        {
            return string.Join(
                "|",
                "billing_v2.public_selection.components",
                PresetCode,
                CommitmentCode,
                PaymentMode,
                string.Join(
                    ";",
                    Components
                        .OrderBy(component => component.ServiceCode, StringComparer.Ordinal)
                        .ThenBy(component => component.TierCode, StringComparer.Ordinal)
                        .ThenBy(component => component.Quantity)
                        .Select(component => $"{component.ServiceCode}/{component.TierCode ?? "-"}/{component.Quantity}")));
        }

        return string.Join(
            "|",
            "billing_v2.public_selection",
            PresetCode,
            CommitmentCode,
            PaymentMode,
            $"sp={StoragePersonalTierCode}",
            $"bp={(BackupPersonal ? 1 : 0)}",
            $"ss={StorageSharedTierCode ?? "-"}",
            $"bs={(BackupShared ? 1 : 0)}",
            $"vpn={VpnTierCode ?? "-"}",
            $"rds={(RemoteDesktop ? 1 : 0)}",
            $"users={AdditionalUsers}",
            $"support={(SupportPlus ? 1 : 0)}");
    }
}

/// <summary>
/// Charge utile acceptee du navigateur. Elle ne porte aucun montant : tout
/// champ tarifaire envoye par le client serait ignore, la seule autorite
/// etant le catalogue serveur et BillingV2PricingEngine.
/// </summary>
public sealed class BillingV2PublicSelectionInput
{
    public string? PresetCode { get; set; }

    public string? CommitmentCode { get; set; }

    public string? PaymentMode { get; set; }

    public string? StoragePersonalTierCode { get; set; }

    public bool BackupPersonal { get; set; }

    public string? StorageSharedTierCode { get; set; }

    public bool BackupShared { get; set; }

    public string? VpnTierCode { get; set; }

    public bool RemoteDesktop { get; set; }

    public int AdditionalUsers { get; set; }

    public bool SupportPlus { get; set; }

    public List<BillingV2PublicSelectionComponentInput>? Components { get; set; }

    public BillingV2PublicSelection ToSelection()
        => new(
            (PresetCode ?? string.Empty).Trim(),
            string.IsNullOrWhiteSpace(CommitmentCode)
                ? "FLEX"
                : CommitmentCode.Trim(),
            string.IsNullOrWhiteSpace(PaymentMode)
                ? BillingV2PaymentModes.Monthly
                : PaymentMode.Trim().ToLowerInvariant(),
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
            SupportPlus,
            Components?
                .Select(component => component.ToComponent())
                .ToArray());
}

public sealed class BillingV2PublicSelectionComponentInput
{
    public string? ServiceCode { get; set; }

    public string? TierCode { get; set; }

    public int Quantity { get; set; }

    public BillingV2PublicSelectionComponent ToComponent()
        => new(
            (ServiceCode ?? string.Empty).Trim(),
            string.IsNullOrWhiteSpace(TierCode) ? null : TierCode.Trim(),
            Quantity);
}

public sealed record BillingV2PublicQuoteLine(
    string ServiceCode,
    string? TierCode,
    string Label,
    string? Detail,
    int Quantity,
    long UnitAmountCents,
    long AmountCents,
    bool DiscountEligible);

/// <summary>
/// Composant retenu, exprime en codes catalogue. C'est cette liste — et non
/// les libelles d'affichage — qui est rejouee cote serveur pour retrouver les
/// vraies lignes de prix en base au moment de la souscription.
/// </summary>
public sealed record BillingV2PublicSelectionComponent(
    string ServiceCode,
    string? TierCode,
    int Quantity);

public sealed record BillingV2PublicQuote(
    string PresetCode,
    string CommitmentCode,
    int CommitmentMonths,
    string PaymentMode,
    int DiscountBasisPoints,
    string Currency,
    long MonthlyBeforeDiscountCents,
    long MonthlyDiscountCents,
    long MonthlyAfterDiscountCents,
    long OneTimeCents,
    long TotalDueNowCents,
    long CommitmentTotalBeforeDiscountCents,
    long CommitmentTotalAfterDiscountCents,
    long CommitmentSavingsCents,
    IReadOnlyList<BillingV2PublicQuoteLine> Lines,
    bool MatchesPresetBaseline,
    bool CheckoutAvailable,
    string CheckoutMode,
    string? CheckoutLegacyOfferId,
    string CheckoutReasonCode);

public static class BillingV2PublicCheckoutModes
{
    /// <summary>Selection V2 native : aucune offre legacy necessaire.</summary>
    public const string Native = "native";

    public const string Unavailable = "unavailable";
}

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
