using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record AdStatusResponse(
    string Mode,
    string Status,
    bool ConfigurationValid,
    bool ReadsEnabled,
    bool WritesEnabled,
    string? Domain,
    string? ClientsOuDn,
    int ConnectTimeoutMs,
    int QueryTimeoutMs,
    int MaxResults);

public sealed record AdDirectoryObjectSummary(
    string ObjectGuid,
    string ObjectSid,
    string ObjectType,
    string SamAccountName,
    string? UserPrincipalName,
    string DisplayName,
    string DistinguishedName,
    string CustomerReference,
    bool IsDisabled);

public sealed record CustomerAdLinkSummary(
    string Id,
    string CustomerReference,
    string ObjectGuid,
    string ObjectSid,
    string ObjectType,
    string SamAccountName,
    string? UserPrincipalName,
    string DisplayName,
    string DistinguishedName,
    string LinkedAt,
    string? LinkedBy);

public sealed record CreateCustomerAdLinkRequest(
    string? DistinguishedName);

public sealed record CreateAdUserRequest(
    string? SamAccountName,
    string? DisplayName,
    string? GivenName,
    string? Surname,
    string? UserPrincipalName,
    string? Description);

public sealed record CreateAdGroupRequest(
    string? SamAccountName,
    string? DisplayName,
    string? Description);

public sealed record AdGroupMemberRequest(
    string? UserSamAccountName);

public sealed record AdMutationResponse(
    string Code,
    string Message,
    string Mode,
    bool Changed,
    [property: JsonPropertyName("correlation_id")] string CorrelationId,
    AdDirectoryObjectSummary? Object = null,
    [property: JsonPropertyName("link_id")] string? LinkId = null);

public sealed record AdLinkMutationResponse(
    string Id,
    string Code,
    string Message,
    bool Changed,
    [property: JsonPropertyName("correlation_id")] string CorrelationId,
    AdDirectoryObjectSummary? Object = null);
