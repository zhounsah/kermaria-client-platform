using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Contracts;

/// <summary>
/// Commandes du panier V2. Ces contrats n'exposent volontairement aucun
/// montant : le prix est relu dans le catalogue et calcule cote serveur.
/// </summary>
public sealed record BillingV2CartItemPayload(
    string? ServiceCode,
    string? TierCode,
    int? Quantity,
    string? ScopeTemplate,
    string? SubjectBinding,
    string? SourcePresetId,
    string? SourcePresetItemId,
    string? ConfigurationKind,
    string? ConfigurationReference,
    string? Origin);

public sealed record BillingV2CartMutationPayload(
    int? ExpectedVersion,
    BillingV2CartItemPayload? Item,
    string? CommitmentCode,
    string? PaymentMode,
    string? AnonymousToken);

public sealed record BillingV2CartClaimPayload(
    string? AnonymousToken,
    int? ExpectedVersion);

/// <summary>Envelope unique pour l'API BFF du panier, sans operation checkout.</summary>
public sealed record BillingV2CartCommandPayload(
    string? Command,
    string? CartId,
    string? ItemId,
    string? Currency,
    int? ExpectedVersion,
    string? AnonymousToken,
    BillingV2CartItemPayload? Item,
    string? CommitmentCode,
    string? PaymentMode,
    string? PresetCode,
    bool? ReplacePreset,
    string? PresetItemId,
    BillingV2PublicSelectionInput? FormulaSelection);

/// <summary>
/// Référence opaque du devis que le client vient d'accepter. Aucun montant,
/// aucun provider ni aucune selection ne sont acceptés depuis le navigateur.
/// </summary>
public sealed record BillingV2CartCheckoutPayload(
    string? CartId,
    int? ExpectedCartVersion,
    int? AcceptedQuoteVersion,
    string? AcceptedCompositionFingerprint,
    // Générées par le BFF depuis son origine publique : elles ne sont jamais
    // des champs du contrat navigateur Cart.
    string? SuccessUrl = null,
    string? CancelUrl = null);

public sealed record BillingV2CartCheckoutResponse(
    string Code,
    string? SubscriptionId,
    string? Provider,
    string? ApprovalUrl,
    string? CartStatus,
    int? CartVersion);

/// <summary>
/// Projection de reprise d'un checkout deja ecrit. Les etats provider internes
/// et leurs diagnostics ne traversent pas la frontiere portal.
/// </summary>
public sealed record BillingV2CartCheckoutStatusResponse(
    string Code,
    string? CartId,
    string? SubscriptionId,
    string? SubscriptionStatus,
    string? CheckoutStatus,
    string? Provider,
    string? ApprovalUrl,
    bool Retryable);
