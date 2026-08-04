using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public static class ClientSolutionStatuses
{
    public const string Published = "published";
    public const string Draft = "draft";

    public static readonly IReadOnlySet<string> KnownValues =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Published,
            Draft
        };

    public static bool IsKnown(string value)
        => KnownValues.Contains(value);
}

public static class ClientSolutionLogoContentTypes
{
    public const int MaxSizeBytes = 512 * 1024;

    public static readonly IReadOnlySet<string> KnownValues =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/png",
            "image/jpeg",
            "image/webp",
            "image/svg+xml"
        };

    public static bool IsKnown(string? value)
        => value is { Length: > 0 } && KnownValues.Contains(value);
}

/// <summary>
/// Tuile telle qu'elle est publiee sur le site vitrine : aucune donnee
/// d'administration n'est exposee ici.
/// </summary>
public sealed record PublicClientSolution(
    string Id,
    string Slug,
    string Title,
    string? Tagline,
    string TargetUrl,
    bool OpensInNewTab,
    bool HasLogo,
    string? LogoUpdatedAt,
    int DisplayOrder);

public sealed record ClientSolutionPortalSettings(
    string? Eyebrow,
    string Title,
    string? Description,
    string? FooterNote,
    string? UpdatedAt);

public sealed record PublicClientSolutionPortal(
    ClientSolutionPortalSettings Settings,
    IReadOnlyList<PublicClientSolution> Solutions);

public sealed record ClientSolution(
    string Id,
    string Slug,
    string Title,
    string? Tagline,
    string TargetUrl,
    bool OpensInNewTab,
    string Status,
    int DisplayOrder,
    bool HasLogo,
    string? LogoOriginalName,
    string? LogoContentType,
    int? LogoSizeBytes,
    string? LogoUpdatedAt,
    string CreatedAt,
    string UpdatedAt);

public sealed record AdminClientSolutionPortal(
    ClientSolutionPortalSettings Settings,
    IReadOnlyList<ClientSolution> Solutions);

public sealed record ClientSolutionPayload(
    string? Slug,
    string? Title,
    string? Tagline,
    string? TargetUrl,
    bool? OpensInNewTab,
    string? Status,
    int? DisplayOrder);

public sealed record ClientSolutionPortalSettingsPayload(
    string? Eyebrow,
    string? Title,
    string? Description,
    string? FooterNote);

public sealed record ClientSolutionMutationResponse(
    string Id,
    bool Changed,
    string UpdatedAt,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

public sealed record ClientSolutionPortalMutationResponse(
    bool Changed,
    string UpdatedAt,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
