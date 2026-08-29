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
/// <summary>
/// Composante tarifaire applicable a un service ou a un palier.
///
/// Un meme (service, palier) peut en porter plusieurs simultanement — par
/// exemple un abonnement mensuel ET des frais de mise en service ponctuels.
/// C'est cette liste, et non une pretendue « cadence du service », qui decide
/// des lignes facturables : `billing_v2_services.billing_type` reste une
/// metadonnee commerciale et n'a aucune autorite tarifaire.
///
/// La cle metier d'un prix est le quintuplet
/// (service, palier, devise, cadence, declencheur) : deux composantes ne se
/// confondent que si elles partagent les cinq.
/// </summary>
public sealed record BillingV2PublicPriceComponent(
    string BillingCadence,
    string ChargeTrigger,
    long AmountCents,
    string Currency,
    bool DiscountEligible,
    string? ServicePriceId = null,
    string? PriceCode = null)
{
    public bool IsRecurring => string.Equals(
        BillingCadence,
        BillingV2BillingCadences.Monthly,
        StringComparison.Ordinal);

    /// <summary>
    /// Composante facturable a la souscription initiale.
    /// </summary>
    /// <remarks>
    /// Le filtre porte sur le declencheur seul, volontairement : un prix
    /// marque <c>subscription_change</c> n'est jamais encaisse pendant un
    /// <c>initial_subscription</c>, quelle que soit sa cadence. Cette regle est
    /// plus stricte que <see cref="BillingV2ComponentizedPricingPolicy"/>, qui
    /// s'applique a des composantes DEJA rattachees a un droit (donc choisies a
    /// leur propre declenchement) ; ici on choisit dans le catalogue vivant, ou
    /// deux prix mensuels de declencheurs differents pourraient coexister.
    /// </remarks>
    public bool AppliesToInitialSubscription => string.Equals(
        ChargeTrigger,
        BillingV2ComponentizedPricingPolicy.InitialSubscription,
        StringComparison.Ordinal);
}

public sealed record BillingV2PublicTier(
    string Code,
    string Label,
    string? Description,
    int? NumericValue,
    long MonthlyAmountCents,
    bool PublicSelectable,
    IReadOnlyList<BillingV2PublicPriceComponent>? PriceComponents = null)
{
    /// <summary>
    /// Composantes tarifaires du palier. A defaut de liste explicite — seed de
    /// repli, doubles de test — le montant mensuel declare vaut composante
    /// unique : la projection publique ne peut donc jamais se retrouver sans
    /// prix du tout.
    /// </summary>
    public IReadOnlyList<BillingV2PublicPriceComponent> Components
        => PriceComponents is { Count: > 0 }
            ? PriceComponents
            : [BillingV2PublicPriceComponents.Monthly(MonthlyAmountCents)];
}

/// <summary>
/// Nature commerciale d'un service. Metadonnee d'affichage et de tri
/// uniquement : elle ne determine aucune ligne tarifaire.
/// </summary>
public static class BillingV2PublicBillingTypes
{
    public const string Recurring = "recurring";
    public const string OneTime = "one_time";
    public const string Included = "included";
}

public static class BillingV2PublicPriceComponents
{
    public const string DefaultCurrency = "EUR";

    public static BillingV2PublicPriceComponent Monthly(
        long amountCents,
        bool discountEligible = true)
        => new(
            BillingV2BillingCadences.Monthly,
            BillingV2ComponentizedPricingPolicy.InitialSubscription,
            amountCents,
            DefaultCurrency,
            discountEligible);

    public static BillingV2PublicPriceComponent OneTime(
        long amountCents)
        => new(
            BillingV2BillingCadences.OneTime,
            BillingV2ComponentizedPricingPolicy.InitialSubscription,
            amountCents,
            DefaultCurrency,
            DiscountEligible: false);
}

public sealed record BillingV2PublicService(
    string Code,
    string Name,
    string Category,
    string ScopeType,
    long? FlatMonthlyAmountCents,
    IReadOnlyList<BillingV2PublicTier> Tiers,
    bool DiscountEligible = true,
    bool PublicVisible = true,
    bool SelfServiceOrderable = true,
    string BillingType = BillingV2PublicBillingTypes.Recurring,
    IReadOnlyList<BillingV2PublicPriceComponent>? FlatPriceComponents = null,
    // Presentation seulement : la description commerciale du catalogue, sans
    // aucune autorite sur les lignes tarifaires (specification, section 19).
    string? Description = null)
{
    /// <summary>
    /// Composantes tarifaires du service sans palier. Vide quand le service
    /// n'est tarife que par palier.
    /// </summary>
    public IReadOnlyList<BillingV2PublicPriceComponent> FlatComponents
        => FlatPriceComponents is { Count: > 0 }
            ? FlatPriceComponents
            : FlatMonthlyAmountCents is { } monthly
                ? [BillingV2PublicPriceComponents.Monthly(
                    monthly,
                    DiscountEligible)]
                : [];

    /// <summary>
    /// Composantes applicables a un palier donne, ou au service lui-meme
    /// quand il n'a pas de palier. Le repli « montant mensuel declare » herite
    /// de l'eligibilite a la remise du service : elle n'est jamais supposee.
    /// </summary>
    public IReadOnlyList<BillingV2PublicPriceComponent> ComponentsFor(
        BillingV2PublicTier? tier)
    {
        if (tier is null)
        {
            return FlatComponents;
        }

        return tier.PriceComponents is { Count: > 0 }
            ? tier.PriceComponents
            : [BillingV2PublicPriceComponents.Monthly(
                tier.MonthlyAmountCents,
                DiscountEligible)];
    }
}

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

public sealed record BillingV2PublicCatalogSnapshot(
    string Source,
    string Currency,
    IReadOnlyList<BillingV2PublicPreset> Presets,
    IReadOnlyList<BillingV2PublicService> Services,
    IReadOnlyList<BillingV2PublicCommitment> Commitments);

/// <summary>
/// Intention de configuration exprimee par le client. Aucun montant : le
/// navigateur ne transmet que des codes catalogue.
/// </summary>
/// <remarks>
/// Deux formes coexistent, sans qu'aucune ne soit un cas particulier de
/// l'autre :
///
/// * <b>preset-based</b> : <see cref="PresetCode"/> designe une formule, et
///   les champs historiques (stockage, sauvegarde, VPN...) decrivent la
///   configuration retenue ;
/// * <b>direct-components</b> : <see cref="PresetCode"/> est nul et
///   <see cref="Components"/> porte integralement la composition. Aucun preset
///   technique n'est fabrique pour la representer : le modele V2 accepte deja
///   une souscription sans formule
///   (`billing_v2_subscriptions.originating_preset_id` nullable).
///
/// <see cref="CommitmentCode"/> est nul quand le produit n'engage a rien —
/// typiquement un achat ponctuel. La remise vaut alors 0 et la duree
/// d'engagement 1, sans qu'aucun terme ne soit rattache au contrat.
/// </remarks>
public sealed record BillingV2PublicSelection(
    string? PresetCode,
    string? CommitmentCode,
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
            // L'absence de preset ou d'engagement fait partie de l'identite
            // metier : deux selections identiques en composants mais l'une
            // rattachee a une formule et l'autre non ne sont pas la meme
            // intention, et ne doivent pas partager une empreinte.
            return string.Join(
                "|",
                "billing_v2.public_selection.components",
                PresetCode ?? "-",
                CommitmentCode ?? "-",
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
            PresetCode ?? "-",
            CommitmentCode ?? "-",
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
    {
        var presetCode = string.IsNullOrWhiteSpace(PresetCode)
            ? null
            : PresetCode.Trim();

        return new BillingV2PublicSelection(
            presetCode,
            // Une formule sans engagement explicite reste en FLEX : c'est le
            // comportement historique de /formules et du diagnostic. Une
            // selection directe, elle, n'invente aucun engagement — un achat
            // ponctuel n'en a pas.
            string.IsNullOrWhiteSpace(CommitmentCode)
                ? (presetCode is null ? null : "FLEX")
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
    bool DiscountEligible,
    string BillingCadence = BillingV2BillingCadences.Monthly);

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
    string? PresetCode,
    string? CommitmentCode,
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
    string CheckoutReasonCode);

public static class BillingV2PublicCheckoutModes
{
    /// <summary>Selection V2 native, avec ou sans formule d'origine.</summary>
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
