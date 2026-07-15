using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public static class DownloadStatuses
{
    public const string Active = "active";
    public const string Inactive = "inactive";

    public static readonly IReadOnlySet<string> KnownValues =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Active,
            Inactive
        };

    public static bool IsKnown(string value)
        => KnownValues.Contains(value);
}

public static class DownloadResourceTypes
{
    public const string Software = "software";
    public const string Script = "script";
    public const string Rdp = "rdp";
    public const string Document = "document";
    public const string Tool = "tool";
    public const string Other = "other";

    public static readonly IReadOnlySet<string> KnownValues =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Software,
            Script,
            Rdp,
            Document,
            Tool,
            Other
        };

    public static bool IsKnown(string value)
        => KnownValues.Contains(value);
}

public static class DownloadSourceKinds
{
    public const string InternalFile = "internal_file";
    public const string ExternalUrl = "external_url";

    public static readonly IReadOnlySet<string> KnownValues =
        new HashSet<string>(StringComparer.Ordinal)
        {
            InternalFile,
            ExternalUrl
        };

    public static bool IsKnown(string value)
        => KnownValues.Contains(value);
}

public static class DownloadVisibilityModes
{
    public const string AllClients = "all_clients";
    public const string Targeted = "targeted";

    public static readonly IReadOnlySet<string> KnownValues =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AllClients,
            Targeted
        };

    public static bool IsKnown(string value)
        => KnownValues.Contains(value);
}

public static class DownloadVisibilityTargetTypes
{
    public const string PublicPackCode = "public_pack_code";
    public const string OfferExternalReference = "offer_external_reference";
    public const string ServiceType = "service_type";
    public const string ProvisioningGroup = "provisioning_group";

    public static readonly IReadOnlySet<string> KnownValues =
        new HashSet<string>(StringComparer.Ordinal)
        {
            PublicPackCode,
            OfferExternalReference,
            ServiceType,
            ProvisioningGroup
        };

    public static bool IsKnown(string value)
        => KnownValues.Contains(value);
}

public sealed record DownloadCategory(
    string Id,
    string Slug,
    string Title,
    string? Description,
    string Status,
    int DisplayOrder,
    int ResourceCount,
    string CreatedAt,
    string UpdatedAt);

public sealed record DownloadVisibilityRule(
    string Id,
    string ResourceId,
    string TargetType,
    string TargetValue);

public sealed record DownloadResource(
    string Id,
    string CategoryId,
    string CategoryTitle,
    string Title,
    string ShortDescription,
    string ResourceType,
    string SourceKind,
    string VisibilityMode,
    string Status,
    string? ExternalUrl,
    string? VersionLabel,
    string? InstallationInstructions,
    int DisplayOrder,
    bool HasInternalFile,
    string? FileOriginalName,
    string? FileContentType,
    long? FileSizeBytes,
    string? FileExtension,
    string CreatedAt,
    string UpdatedAt,
    IReadOnlyList<DownloadVisibilityRule> Rules);

public sealed record PortalDownloadItem(
    string Id,
    string Title,
    string ShortDescription,
    string ResourceType,
    string? VersionLabel,
    string? UpdatedAt,
    string? InstallationInstructions);

public sealed record PortalDownloadCategory(
    string Id,
    string Slug,
    string Title,
    string? Description,
    IReadOnlyList<PortalDownloadItem> Items);

public sealed record DownloadCategoryPayload(
    string? Slug,
    string? Title,
    string? Description,
    string? Status,
    int? DisplayOrder);

public sealed record DownloadVisibilityRulePayload(
    string? TargetType,
    string? TargetValue);

public sealed record DownloadResourcePayload(
    string? CategoryId,
    string? Title,
    string? ShortDescription,
    string? ResourceType,
    string? SourceKind,
    string? VisibilityMode,
    string? Status,
    string? ExternalUrl,
    string? VersionLabel,
    string? InstallationInstructions,
    int? DisplayOrder,
    IReadOnlyList<DownloadVisibilityRulePayload>? VisibilityRules);

public sealed record DownloadCategoryMutationResponse(
    string Id,
    bool Changed,
    string UpdatedAt,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

public sealed record DownloadResourceMutationResponse(
    string Id,
    bool Changed,
    string UpdatedAt,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
