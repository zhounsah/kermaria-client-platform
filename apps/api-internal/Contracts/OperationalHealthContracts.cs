using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record OperationalHealthResponse(
    string Status,
    string Service,
    string Check,
    [property: JsonPropertyName("timestamp_utc")]
    DateTime TimestampUtc,
    IReadOnlyDictionary<string, string>? Checks = null);
