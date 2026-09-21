using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Services;

public static class BillingV2CartStatuses
{
    public const string Open = "open";
    public const string CheckedOut = "checked_out";
    public const string Expired = "expired";
}

public static class BillingV2CartConfigurationPolicies
{
    public const string NotRequired = "not_required";
    public const string RequiredBeforeCheckout = "required_before_checkout";
    public const string DeferrableInMixedCart = "deferrable_in_mixed_cart";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        StringComparer.Ordinal)
    {
        NotRequired,
        RequiredBeforeCheckout,
        DeferrableInMixedCart
    };
}

public static class BillingV2CartCommercialReadiness
{
    public const string Ready = "ready";
    public const string Blocked = "blocked";
}

public static class BillingV2CartConfigurationReadiness
{
    public const string NotRequired = "not_required";
    public const string Complete = "complete";
    public const string Deferred = "deferred";
    public const string Blocked = "blocked";
}

public static class BillingV2CartProvisioningReadiness
{
    public const string Ready = "ready";
    public const string Deferred = "deferred";
    public const string Blocked = "blocked";
}

public static class BillingV2CartQuoteStatuses
{
    public const string Current = "current";
    public const string Stale = "stale";
    public const string Expired = "expired";
}

/// <summary>
/// Provenance durable, strictement descriptive, d'une ligne Cart. Elle ne
/// porte ni un prix ni une autorisation : les politiques Cart restent relues
/// cote serveur a chaque mutation.
/// </summary>
public static class BillingV2CartItemOrigins
{
    public const string Direct = "direct";
    public const string Preset = "preset";
    public const string Dependency = "dependency";
    public const string Structural = "structural";
    public const string Legacy = "legacy";
}

public sealed record BillingV2CartOwner(string? CustomerId, string? AnonymousToken)
{
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(CustomerId);

    public string? AnonymousTokenHash => string.IsNullOrWhiteSpace(AnonymousToken)
        ? null
        : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AnonymousToken)));

    public bool IsValid => IsAuthenticated || AnonymousTokenHash is not null;
}

public sealed record BillingV2CartItem(
    string Id,
    string CartId,
    string ServiceId,
    string ServiceCode,
    string? TierId,
    string? TierCode,
    int Quantity,
    string ScopeTemplate,
    string? SubjectBinding,
    string? SourcePresetItemId,
    bool? RequiredItem,
    bool? CustomerEditable,
    string Origin,
    bool IsStructural,
    bool IsRequiredByPreset,
    bool IsRequiredByDependency,
    bool IsExplicitCommercialSelection,
    bool CanEdit,
    bool CanRemove,
    bool CountsAsCommercialSelection,
    string? DisplayReason,
    int MinimumQuantity,
    int MaximumQuantity,
    string? ConfigurationKind,
    string? ConfigurationReference,
    int DisplayOrder,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Définition autorisée d'un preset, distincte des items sélectionnés du Cart.</summary>
public sealed record BillingV2CartPresetDefinitionItem(
    string PresetItemId,
    string ServiceCode,
    string? TierCode,
    string ScopeTemplate,
    int DefaultQuantity,
    bool RequiredItem,
    bool CustomerEditable,
    bool SelectedByDefault,
    int MinimumQuantity,
    int MaximumQuantity,
    int DisplayOrder);

public sealed record BillingV2Cart(
    string Id,
    string? CustomerId,
    [property: JsonIgnore] string? AnonymousSessionHash,
    string Status,
    string Currency,
    string? CommitmentTermId,
    string? CommitmentCode,
    string? PaymentMode,
    string? SourcePresetId,
    string? SourcePresetCode,
    int Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime LastActivityAtUtc,
    DateTime ExpiresAtUtc,
    string? CheckedOutSubscriptionId,
    IReadOnlyList<BillingV2CartItem> Items,
    IReadOnlyList<BillingV2CartPresetDefinitionItem>? PresetDefinition = null);

public sealed record BillingV2CartIssue(
    string Code,
    string Severity,
    string? CartItemId,
    string? ServiceCode,
    string Message,
    bool Blocking,
    string? CustomerMessage = null);

public sealed record BillingV2CartQuoteLine(
    string CartItemId,
    string ServiceCode,
    string? TierCode,
    string Label,
    string? Detail,
    string ServicePriceId,
    string PriceCode,
    string BillingCadence,
    long UnitAmountCents,
    int Quantity,
    long AmountCents,
    bool DiscountEligible,
    string? FeeDeduplicationKey,
    string? FeeDeduplicationContext = null);

public sealed record BillingV2CartQuote(
    string CartId,
    int CartVersion,
    int QuoteVersion,
    string CompositionFingerprint,
    string Currency,
    long RecurringSubtotalCents,
    long RecurringDiscountCents,
    long RecurringTotalCents,
    long OneTimeDueNowCents,
    long TotalDueNowCents,
    DateTime CalculatedAtUtc,
    DateTime ExpiresAtUtc,
    string QuoteStatus,
    IReadOnlyList<BillingV2CartQuoteLine> Lines,
    IReadOnlyList<BillingV2CartIssue> DependencyIssues,
    IReadOnlyList<BillingV2CartIssue> ScopeIssues,
    IReadOnlyList<BillingV2CartIssue> ConfigurationIssues,
    string CommercialReadiness,
    string ConfigurationReadiness,
    string ProvisioningReadiness);

public enum BillingV2CartMutationOutcome
{
    Success,
    Conflict,
    NotFound,
    ValidationFailure
}

/// <summary>
/// Unique classification transport des resultats metier Cart. Les erreurs de
/// disponibilite SQL restent des exceptions : le middleware API les transforme
/// deja en SQL_UNAVAILABLE / 503 avant qu'un resultat Cart n'existe.
/// </summary>
public static class BillingV2CartMutationResults
{
    private static readonly IReadOnlySet<string> SuccessCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "CART_OK",
        "CART_FORMULA_SELECTION_IMPORTED",
        "CART_PRESET_INITIALIZED",
        "CART_ITEM_ADDED",
        "CART_ITEM_ALREADY_PRESENT",
        "CART_ITEM_UPDATED",
        "CART_ITEM_REMOVED",
        "CART_COMMITMENT_UPDATED",
        "CART_PAYMENT_MODE_UPDATED",
        "CART_QUOTED",
        "CART_EXPIRED",
        "CART_CLAIMED",
        "CART_NOTHING_TO_CLAIM",
        "CART_LEGACY_SELECTION_PROJECTED"
    };

    private static readonly IReadOnlySet<string> ConflictCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "CART_PRESET_CONFLICT",
        "CART_MERGE_REQUIRES_REVIEW",
        "CART_PRESET_ITEM_ALREADY_SELECTED",
        "CART_PRESET_ITEM_CONFLICT",
        "CART_VERSION_CONFLICT",
        "CART_CLAIM_CONFLICT",
        "CART_IMMUTABLE",
        "CART_ITEM_TIER_CONFLICT",
        "CART_STRUCTURAL_ITEM_REQUIRED",
        "CART_DEPENDENCY_REQUIRED",
        "CART_PRESET_ITEM_REQUIRED",
        "CART_PRESET_ITEM_IMMUTABLE"
    };

    private static readonly IReadOnlySet<string> NotFoundCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "CART_NOT_FOUND",
        "CART_ITEM_NOT_FOUND"
    };

    public static BillingV2CartMutationOutcome Classify(string code)
    {
        if (SuccessCodes.Contains(code)) return BillingV2CartMutationOutcome.Success;
        if (ConflictCodes.Contains(code)) return BillingV2CartMutationOutcome.Conflict;
        if (NotFoundCodes.Contains(code)) return BillingV2CartMutationOutcome.NotFound;
        return BillingV2CartMutationOutcome.ValidationFailure;
    }

    public static int HttpStatusCode(BillingV2CartMutationResult result)
        => Classify(result.Code) switch
        {
            BillingV2CartMutationOutcome.Success => 200,
            BillingV2CartMutationOutcome.Conflict => 409,
            BillingV2CartMutationOutcome.NotFound => 404,
            _ => 400
        };
}

public sealed record BillingV2CartMutationResult(
    string Code,
    BillingV2Cart? Cart = null,
    BillingV2CartQuote? Quote = null,
    string? ExistingPresetId = null,
    BillingV2PublicSelection? LegacySelection = null)
{
    [JsonIgnore]
    public BillingV2CartMutationOutcome Outcome => BillingV2CartMutationResults.Classify(Code);
}
