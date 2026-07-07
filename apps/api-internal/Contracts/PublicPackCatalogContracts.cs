using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record PublicPackComparisonValue(
    string Kind,
    string? Text);

public sealed record PublicPackPresentation(
    string PackCode,
    string Label,
    string ShortLabel,
    string Headline,
    string Audience,
    string Description,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Included,
    string? HighlightLabel,
    int DisplayOrder);

public sealed record PublicPackComparisonRow(
    string Id,
    string Label,
    int SortOrder,
    IReadOnlyDictionary<string, PublicPackComparisonValue> Values);

public sealed record PublicPackCatalogContent(
    string PageEyebrow,
    string PageTitle,
    string PageDescription,
    string ComparisonColumnLabel,
    string FootnotePrimary,
    string FootnoteSecondary,
    IReadOnlyList<PublicPackPresentation> Packs,
    IReadOnlyList<PublicPackComparisonRow> ComparisonRows,
    string? UpdatedAt);

public sealed record PublicPackCatalogContentPayload(
    string? PageEyebrow,
    string? PageTitle,
    string? PageDescription,
    string? ComparisonColumnLabel,
    string? FootnotePrimary,
    string? FootnoteSecondary,
    IReadOnlyList<PublicPackPresentation>? Packs,
    IReadOnlyList<PublicPackComparisonRow>? ComparisonRows);

public sealed record PublicPackCatalogMutationResponse(
    bool Changed,
    string UpdatedAt,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
