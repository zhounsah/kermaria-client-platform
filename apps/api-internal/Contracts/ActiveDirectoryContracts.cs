using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record AdHealthResponse(
    string Mode,
    string Status,
    bool ConfigurationValid,
    bool OperationsEnabled);

public sealed record AdOperationResponse(
    string Code,
    string Message,
    string Mode,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

public sealed record ChangePasswordRequest(
    string? TargetDistinguishedName,
    string? CurrentPassword,
    string? NewPassword);

public sealed record CreateUserRequest(
    string? AccountName,
    string? DisplayName);

public sealed record GroupMembershipRequest(
    string? TargetDistinguishedName,
    string? GroupName);
