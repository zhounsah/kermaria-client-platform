using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public static class EditorialContentTypes
{
    public const string WikiArticle = "wiki_article";
    public const string SeoPage = "seo_page";
    public const string Faq = "faq";

    public static readonly IReadOnlySet<string> KnownValues =
        new HashSet<string>(StringComparer.Ordinal)
        {
            WikiArticle,
            SeoPage,
            Faq
        };

    public static bool IsKnown(string? value)
        => value is { Length: > 0 } && KnownValues.Contains(value);
}

public static class EditorialContentStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Archived = "archived";
    public const string Scheduled = "scheduled";

    public static readonly IReadOnlySet<string> KnownValues =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Draft,
            Published,
            Archived,
            Scheduled
        };

    public static bool IsKnown(string? value)
        => value is { Length: > 0 } && KnownValues.Contains(value);
}

public sealed record EditorialCategory(
    string Id,
    string ContentType,
    string Name,
    string Slug,
    string? Description,
    int SortOrder,
    string CreatedAt,
    string UpdatedAt);

public record EditorialContentSummary(
    string Id,
    string ContentType,
    string Title,
    string Slug,
    string? Summary,
    string? CategoryId,
    string? CategoryName,
    int? CategorySortOrder,
    string Status,
    int SortOrder,
    bool NoIndex,
    IReadOnlyList<string> FaqScopes,
    string? PublishedAt,
    string UpdatedAt,
    string? PublicPath);

public sealed record EditorialContentDetail(
    string Id,
    string ContentType,
    string Title,
    string Slug,
    string? Summary,
    string BodyMarkdown,
    string? CategoryId,
    string? CategoryName,
    int? CategorySortOrder,
    string Status,
    string? SeoTitle,
    string? SeoDescription,
    string? CanonicalUrl,
    bool NoIndex,
    int SortOrder,
    IReadOnlyList<string> FaqScopes,
    string? PublishedAt,
    string CreatedAt,
    string UpdatedAt,
    string? CreatedByUserId,
    string? UpdatedByUserId,
    string? PublicPath)
    : EditorialContentSummary(
        Id,
        ContentType,
        Title,
        Slug,
        Summary,
        CategoryId,
        CategoryName,
        CategorySortOrder,
        Status,
        SortOrder,
        NoIndex,
        FaqScopes,
        PublishedAt,
        UpdatedAt,
        PublicPath);

public sealed record EditorialContentPayload(
    string? ContentType,
    string? Title,
    string? Slug,
    string? Summary,
    string? BodyMarkdown,
    string? CategoryId,
    string? Status,
    string? SeoTitle,
    string? SeoDescription,
    string? CanonicalUrl,
    bool? NoIndex,
    int? SortOrder,
    IReadOnlyList<string>? FaqScopes);

public sealed record EditorialCategoryPayload(
    string? ContentType,
    string? Name,
    string? Slug,
    string? Description,
    int? SortOrder);

public sealed record EditorialListResponse(
    IReadOnlyList<EditorialContentSummary> Items,
    IReadOnlyList<EditorialCategory> Categories);

public record EditorialRevisionSummary(
    string Id,
    string ContentId,
    int VersionNumber,
    string Action,
    string CreatedAt,
    string? CreatedByUserId);

public sealed record EditorialRevisionDetail(
    string Id,
    string ContentId,
    int VersionNumber,
    string Action,
    string CreatedAt,
    string? CreatedByUserId,
    EditorialContentDetail Snapshot)
    : EditorialRevisionSummary(
        Id,
        ContentId,
        VersionNumber,
        Action,
        CreatedAt,
        CreatedByUserId);

public sealed record EditorialRedirect(
    string Id,
    string ContentType,
    string OldPath,
    string NewPath,
    string CreatedAt);

public sealed record EditorialMutationResponse(
    string Id,
    bool Changed,
    string UpdatedAt,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
