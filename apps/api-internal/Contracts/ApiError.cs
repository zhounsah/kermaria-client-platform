using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record ApiError(
    string Code,
    string Message,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
