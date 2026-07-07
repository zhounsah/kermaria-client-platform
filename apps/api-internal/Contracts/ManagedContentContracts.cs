using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public record ManagedContentSummary(
    string Key,
    string ContentType,
    string Title,
    string PublicPath,
    string? VersionLabel,
    string? UpdatedAt);

public sealed record ManagedContentDetail(
    string Key,
    string ContentType,
    string Title,
    string PublicPath,
    string? VersionLabel,
    string BodyMarkdown,
    string? CreatedAt,
    string? UpdatedAt)
    : ManagedContentSummary(
        Key,
        ContentType,
        Title,
        PublicPath,
        VersionLabel,
        UpdatedAt);

public sealed record ManagedContentPayload(
    string? BodyMarkdown,
    string? VersionLabel);

public sealed record ManagedContentMutationResponse(
    string Key,
    bool Changed,
    string UpdatedAt,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
