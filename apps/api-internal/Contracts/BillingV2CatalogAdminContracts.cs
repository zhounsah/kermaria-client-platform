namespace Kermaria.ApiInternal.Contracts;

/// <summary>
/// Contrats d'administration du catalogue Billing V2/V2.1.
/// </summary>
/// <remarks>
/// <para>
/// Ces contrats decrivent la <b>seule</b> autorite commerciale du produit. Il
/// n'existe plus de catalogue parallele : services, paliers, versions de prix,
/// formules, engagements et rattachements provider sont administres ici et
/// nulle part ailleurs.
/// </para>
/// <para>
/// Un point merite d'etre lu avant toute evolution :
/// <c>billing_v2_service_prices</c> est <b>versionnee et immuable</b>. Aucun
/// payload ne permet de modifier le montant d'un prix existant. Un changement
/// de tarif produit une revision — fermeture de l'ancienne fenetre par
/// <c>valid_until</c> et insertion de la version N+1 — parce qu'un abonnement
/// deja vendu oppose au client le montant en vigueur au moment de la vente.
/// Reecrire la ligne changerait retroactivement ce que le client a accepte.
/// </para>
/// </remarks>
public sealed record BillingV2AdminCatalogSnapshot(
    // "mariadb" quand le catalogue est lu en base, "unavailable" quand la
    // persistance n'est pas disponible. L'administration n'a pas de repli
    // fictif : editer un catalogue qui n'existe pas donnerait l'illusion d'un
    // enregistrement.
    string Source,
    bool Editable,
    string Currency,
    IReadOnlyList<BillingV2AdminService> Services,
    IReadOnlyList<BillingV2AdminPreset> Presets,
    IReadOnlyList<BillingV2AdminCommitment> Commitments);

public sealed record BillingV2AdminService(
    string Id,
    string Code,
    string Name,
    string? Description,
    string? Category,
    string BillingType,
    string DefaultScopeType,
    string PricingModel,
    bool MandatoryForSubscription,
    bool DiscountEligible,
    bool PublicVisible,
    bool SelfServiceOrderable,
    string Status,
    int DisplayOrder,
    string? UpdatedByReference,
    IReadOnlyList<BillingV2AdminTier> Tiers,
    // Prix rattaches au service lui-meme (tier_id NULL). Un service tarife au
    // palier n'en porte aucun.
    IReadOnlyList<BillingV2AdminPrice> FlatPrices);

public sealed record BillingV2AdminTier(
    string Id,
    string ServiceId,
    string Code,
    string Name,
    string? PublicLabel,
    string? Description,
    long? NumericValue,
    string? Unit,
    bool PublicSelectable,
    string Status,
    int DisplayOrder,
    IReadOnlyList<BillingV2AdminTierAttribute> Attributes,
    IReadOnlyList<BillingV2AdminPrice> Prices);

public sealed record BillingV2AdminTierAttribute(
    string AttributeCode,
    long? ValueNumeric,
    string? ValueText,
    string? Unit);

public sealed record BillingV2AdminPrice(
    string Id,
    string ServiceId,
    string? TierId,
    string PriceCode,
    int PriceVersion,
    long AmountCents,
    string Currency,
    string BillingCadence,
    string ChargeTrigger,
    int? TaxRateBasisPoints,
    DateTime ValidFrom,
    DateTime? ValidUntil,
    string Status,
    string? CreatedByReference,
    string? SupersedesPriceId,
    DateTime CreatedAt,
    IReadOnlyList<BillingV2AdminProviderMapping> ProviderMappings)
{
    /// <summary>
    /// Fenetre courante : deja ouverte, pas encore fermee, et active.
    /// </summary>
    public bool IsCurrent(DateTime asOfUtc)
        => string.Equals(Status, "active", StringComparison.Ordinal)
           && ValidFrom <= asOfUtc
           && (ValidUntil is null || ValidUntil > asOfUtc);

    /// <summary>
    /// Revision planifiee : active mais dont la fenetre n'a pas commence.
    /// </summary>
    public bool IsScheduled(DateTime asOfUtc)
        => string.Equals(Status, "active", StringComparison.Ordinal)
           && ValidFrom > asOfUtc;
}

public sealed record BillingV2AdminProviderMapping(
    string Id,
    string ServicePriceId,
    string Provider,
    string Environment,
    string? ExternalProductId,
    string? ExternalPriceId,
    string? ExternalPlanId,
    string Status);

public sealed record BillingV2AdminPreset(
    string Id,
    string Code,
    string Name,
    string? Description,
    string Status,
    bool IsPublic,
    int DisplayOrder,
    IReadOnlyList<BillingV2AdminPresetItem> Items);

public sealed record BillingV2AdminPresetItem(
    string Id,
    string ServiceId,
    string ServiceCode,
    string? TierId,
    string? TierCode,
    string ScopeTemplate,
    int Quantity,
    bool RequiredItem,
    bool CustomerEditable,
    int DisplayOrder);

public sealed record BillingV2AdminCommitment(
    string Id,
    string Code,
    string Name,
    int CommitmentMonths,
    int? DiscountBasisPoints,
    bool AllowMonthlyPayment,
    bool AllowUpfrontPayment,
    string Status,
    int DisplayOrder,
    IReadOnlyList<BillingV2AdminCommitmentPaymentOption> PaymentOptions);

public sealed record BillingV2AdminCommitmentPaymentOption(
    string Id,
    string PaymentMode,
    int DiscountBasisPoints,
    string Status,
    int DisplayOrder);

// ---------------------------------------------------------------------------
// Payloads
// ---------------------------------------------------------------------------

public sealed record BillingV2AdminServicePayload(
    string? Name,
    string? Description,
    string? Category,
    string? Status,
    int? DisplayOrder,
    bool? PublicVisible,
    bool? SelfServiceOrderable,
    bool? DiscountEligible,
    bool? MandatoryForSubscription);

public sealed record BillingV2AdminServiceCreatePayload(
    string? Code,
    string? Name,
    string? Description,
    string? Category,
    string? BillingType,
    string? DefaultScopeType,
    string? PricingModel,
    bool? MandatoryForSubscription,
    bool? DiscountEligible,
    int? DisplayOrder);

public sealed record BillingV2AdminTierPayload(
    string? Label,
    string? PublicLabel,
    string? Description,
    string? Status,
    int? DisplayOrder,
    bool? PublicSelectable,
    long? NumericValue,
    string? Unit,
    IReadOnlyList<BillingV2AdminTierAttributePayload>? Attributes);

public sealed record BillingV2AdminTierCreatePayload(
    string? Code,
    string? Label,
    string? PublicLabel,
    string? Description,
    int? DisplayOrder,
    long? NumericValue,
    string? Unit,
    IReadOnlyList<BillingV2AdminTierAttributePayload>? Attributes);

public sealed record BillingV2AdminTierAttributePayload(
    string? AttributeCode,
    long? ValueNumeric,
    string? ValueText,
    string? Unit);

/// <summary>
/// Revision tarifaire. <c>EffectiveAt</c> nul signifie « maintenant ».
/// </summary>
/// <remarks>
/// Ce payload ne porte volontairement pas d'identifiant de prix : on ne
/// « modifie » pas un prix, on en publie une nouvelle version. Le service
/// determine seul quelle fenetre doit etre fermee.
/// </remarks>
public sealed record BillingV2AdminPriceRevisionPayload(
    string? ServiceId,
    string? TierId,
    long? AmountCents,
    string? Currency,
    string? BillingCadence,
    string? ChargeTrigger,
    int? TaxRateBasisPoints,
    DateTime? EffectiveAt);

/// <summary>
/// Retrait d'un tarif sans remplacant : la fenetre est fermee, rien n'est
/// ouvert derriere. Le service devient non commandable pour cette combinaison.
/// </summary>
public sealed record BillingV2AdminPriceDeactivationPayload(
    DateTime? EffectiveAt);

public sealed record BillingV2AdminPresetPayload(
    string? Code,
    string? Name,
    string? Description,
    string? Status,
    bool? IsPublic,
    int? DisplayOrder);

public sealed record BillingV2AdminPresetItemPayload(
    string? ServiceId,
    string? TierId,
    string? ScopeTemplate,
    int? Quantity,
    bool? RequiredItem,
    bool? CustomerEditable,
    int? DisplayOrder);

public sealed record BillingV2AdminCommitmentPayload(
    string? Code,
    string? Name,
    int? CommitmentMonths,
    int? DiscountBasisPoints,
    bool? AllowMonthlyPayment,
    bool? AllowUpfrontPayment,
    string? Status,
    int? DisplayOrder);

public sealed record BillingV2AdminCommitmentPaymentOptionPayload(
    string? PaymentMode,
    int? DiscountBasisPoints,
    string? Status,
    int? DisplayOrder);

public sealed record BillingV2AdminProviderMappingPayload(
    string? Provider,
    string? Environment,
    string? ExternalProductId,
    string? ExternalPriceId,
    string? ExternalPlanId,
    string? Status);

public sealed record BillingV2AdminCatalogMutationResponse(
    string Code,
    string Message,
    string? Id = null);

/// <summary>
/// Lisibilite commerciale d'un rail de paiement.
/// </summary>
/// <remarks>
/// Sert la regle « ne pas rendre une offre self-service si son rail de
/// paiement requis est incomplet ». Le rail Stripe de Billing V2 fonctionne en
/// <c>price_data</c> inline : il ne depend d'aucun <c>price_id</c> externe. Un
/// mapping manquant n'est donc pas bloquant pour Stripe, mais il l'est pour un
/// rail qui exige un plan preexistant.
/// </remarks>
public sealed record BillingV2AdminCatalogProviderCoverage(
    string Provider,
    string Environment,
    bool RequiresExternalMapping,
    int CurrentPriceCount,
    int MappedPriceCount,
    IReadOnlyList<string> UnmappedPriceCodes);
