namespace Kermaria.ApiInternal.Contracts;

public sealed record SitePageBlock(
    string Id,
    string Type,
    string? Title = null,
    string? Body = null,
    string? Href = null,
    string? Label = null,
    string? MediaId = null,
    string? Action = null,
    IReadOnlyList<SitePageBlockItem>? Items = null,
    IReadOnlyList<SitePageFormField>? Fields = null,
    string? WidgetKey = null);

public sealed record SitePageBlockItem(string Title, string? Body, string? Href, string? Label = null);
public sealed record SitePageFormField(
    string Id, string Label, string Type, bool Required,
    IReadOnlyList<string>? Options = null);

public sealed record SitePageLayout(
    string PageKey, string Area, long Version,
    IReadOnlyList<SitePageBlock> Blocks, string? UpdatedAt);

public sealed record SitePageMutationPayload(
    string PageKey, string Area, long ExpectedVersion,
    IReadOnlyList<SitePageBlock> Blocks);

public sealed record SitePageRevision(
    string PageKey, long Version, string CreatedAt, string CreatedBy);

public sealed record SitePageRestorePayload(
    string PageKey, string Area, long Version, long ExpectedVersion);

public sealed record SiteMediaAsset(
    string Id, string FileName, string ContentType, string AltText,
    int ByteLength, string CreatedAt);
