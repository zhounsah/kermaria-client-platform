using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record LoginRequest(string? Email, string? Password);

public static class PortalRoles
{
    public const string ClientUser = "client_user";
    public const string InternalAdmin = "internal_admin";

    public static bool IsKnown(string role)
        => role is ClientUser or InternalAdmin;
}

public sealed record AuthenticatedPortalUser(
    string DisplayName,
    string Email,
    string? CustomerReference,
    string Status,
    string Role,
    string? LastLoginAt);

public sealed record InternalSessionResponse(
    AuthenticatedPortalUser User,
    string ExpiresAt);

public sealed record InternalSessionCreatedResponse(
    [property: JsonPropertyName("sessionToken")] string SessionToken,
    AuthenticatedPortalUser User,
    string ExpiresAt);
