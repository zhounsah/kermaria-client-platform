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
    IReadOnlyList<string> AllowedRoots,
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
    string? Description,
    string? PersonalTitle,
    string? Initials,
    string? Email,
    string? Phone,
    string? CompanyName,
    string? EmployeeNumber);

public sealed record CreateAdGroupRequest(
    string? SamAccountName,
    string? DisplayName,
    string? Description);

public sealed record AdGroupMemberRequest(
    string? UserSamAccountName);

public sealed record RenameAdUserRequest(
    string? NewSamAccountName,
    string? NewDisplayName,
    string? NewUserPrincipalName);

public sealed record MoveAdUserRequest(
    string? TargetCustomerReference,
    string? TargetContainer);

public sealed record ChangeAdPasswordRequest(
    string? CurrentPassword,
    string? NewPassword);

public sealed record AdPasswordChangeResponse(
    string Code,
    string Message,
    string Mode,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

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

public sealed record AdProvisioningDiagnostic(
    string Code,
    string Message,
    string TargetType,
    IReadOnlyList<string> AllowedRoots,
    IReadOnlyList<string> AffectedUserDistinguishedNames,
    IReadOnlyList<string> AffectedGroupDistinguishedNames,
    IReadOnlyList<string> LinkedUserReferences);

public sealed record AdminCustomerAdSubscriptionContext(
    string Id,
    string OfferName,
    string? OfferExternalReference,
    string? PublicPackCode,
    string Status,
    IReadOnlyList<string> MappedGroups,
    IReadOnlyList<string> CoveredServiceTechnicalReferences);

public sealed record ProvisionableServiceSummary(
    string TechnicalServiceReference,
    string Label,
    IReadOnlyList<string> GroupSamAccountNames,
    IReadOnlyList<string> SubscriptionIds,
    IReadOnlyList<string> CoveredSubscriptionIds,
    bool IsCoveredByActiveSubscription,
    bool IsManualEligible,
    bool IsOverrideRequired,
    string CurrentStatus,
    IReadOnlyList<AdProvisioningDiagnostic> Diagnostics);

public sealed record ProvisionableGroupSummary(
    string GroupSamAccountName,
    string Label,
    IReadOnlyList<string> TechnicalServiceReferences,
    IReadOnlyList<string> SubscriptionIds,
    IReadOnlyList<string> CoveredSubscriptionIds,
    bool IsCoveredByActiveSubscription,
    bool IsManualEligible,
    bool IsOverrideRequired,
    string CurrentStatus,
    IReadOnlyList<AdProvisioningDiagnostic> Diagnostics);

public sealed record AdminCustomerAdWorkspace(
    string CustomerReference,
    string CustomerName,
    AdStatusResponse? AdStatus,
    IReadOnlyList<CustomerAdLinkSummary> Links,
    IReadOnlyList<SubscriptionProvisioningTargetUserSummary> LinkedUsers,
    AdminCustomerAdSubscriptionContext? SubscriptionContext,
    IReadOnlyList<AdminCustomerAdSubscriptionContext> Subscriptions,
    IReadOnlyList<string> ManagedGroups,
    string ProvisioningStatus,
    string? LastResultCode,
    IReadOnlyList<ProvisionableServiceSummary> Services,
    IReadOnlyList<ProvisionableGroupSummary> Groups,
    IReadOnlyList<AdProvisioningDiagnostic> Diagnostics);

public sealed record CustomerAdProvisioningMutationRequest(
    string? Operation,
    IReadOnlyList<string>? TargetUserSamAccountNames,
    [property: JsonPropertyName("override")] bool? IsOverride,
    string? SubscriptionId);

public sealed record CustomerAdProvisioningMutationResponse(
    string Code,
    string Message,
    bool Changed,
    [property: JsonPropertyName("correlation_id")] string CorrelationId,
    AdminCustomerAdWorkspace Workspace);
