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
    string? ConfigurationKind,
    string? ConfigurationReference,
    int DisplayOrder,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

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
    int Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime LastActivityAtUtc,
    DateTime ExpiresAtUtc,
    string? CheckedOutSubscriptionId,
    IReadOnlyList<BillingV2CartItem> Items);

public sealed record BillingV2CartIssue(
    string Code,
    string Severity,
    string? CartItemId,
    string? ServiceCode,
    string Message,
    bool Blocking);

public sealed record BillingV2CartQuoteLine(
    string CartItemId,
    string ServiceCode,
    string? TierCode,
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

public sealed record BillingV2CartMutationResult(
    string Code,
    BillingV2Cart? Cart = null,
    BillingV2CartQuote? Quote = null);
