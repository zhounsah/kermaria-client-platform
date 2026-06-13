using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record AuthenticatedPortalUser(
    string DisplayName,
    string Email,
    string CustomerReference,
    string Status);

public sealed record InternalSessionResponse(
    AuthenticatedPortalUser User,
    string ExpiresAt);

public sealed record InternalSessionCreatedResponse(
    [property: JsonPropertyName("sessionToken")] string SessionToken,
    AuthenticatedPortalUser User,
    string ExpiresAt);
