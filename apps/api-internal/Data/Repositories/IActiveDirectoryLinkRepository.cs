using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record AdCustomerContext(
    string CustomerId,
    string CustomerReference,
    string DisplayName);

public sealed record CustomerAdLinkUpsertResult(
    string Id,
    bool Changed);

public sealed record PortalUserAdLinkRecord(
    string Id,
    string CustomerId,
    string CustomerReference,
    string PortalUserId,
    string ObjectGuid,
    string ObjectSid,
    string SamAccountName,
    string? UserPrincipalName,
    string DisplayName,
    string DistinguishedName,
    string? AdDomain,
    string? AdProvisioningStatus,
    DateTime? AdProvisionedAtUtc,
    DateTime? LastPasswordSyncAtUtc,
    string? LastPasswordSyncStatus,
    string? KoxoExportStatus);

public interface IActiveDirectoryLinkRepository
{
    bool IsPersistent { get; }

    Task<AdCustomerContext?> GetCustomerContextAsync(
        string customerReference,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerLinksAsync(
        string customerReference,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerUserLinksAsync(
        string customerId,
        CancellationToken cancellationToken);
    Task<CustomerAdLinkUpsertResult> UpsertCustomerLinkAsync(
        string customerReference,
        string? actorUserId,
        AdDirectoryObjectSummary directoryObject,
        CancellationToken cancellationToken);
    Task<CustomerAdLinkUpsertResult> UpsertPortalUserLinkAsync(
        string customerReference,
        string portalUserId,
        string? actorUserId,
        AdDirectoryObjectSummary directoryObject,
        string? adDomain,
        string? adProvisioningStatus,
        DateTime? adProvisionedAtUtc,
        string? lastPasswordSyncStatus,
        DateTime? lastPasswordSyncAtUtc,
        string? koxoExportStatus,
        CancellationToken cancellationToken);
    Task<bool> UpdateUserPasswordSyncStatusAsync(
        string portalUserId,
        string status,
        DateTime changedAtUtc,
        CancellationToken cancellationToken);
    Task<bool> DeleteCustomerLinkAsync(
        string customerReference,
        string linkId,
        CancellationToken cancellationToken);
    Task<bool> RefreshCustomerLinkAsync(
        string targetCustomerReference,
        AdDirectoryObjectSummary directoryObject,
        CancellationToken cancellationToken);
    Task<CustomerAdLinkSummary?> FindUserLinkByEmailAsync(
        string customerReference,
        string email,
        CancellationToken cancellationToken);
    Task<PortalUserAdLinkRecord?> FindUserLinkByPortalUserIdAsync(
        string portalUserId,
        CancellationToken cancellationToken);
}
