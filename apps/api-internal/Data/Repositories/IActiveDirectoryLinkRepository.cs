using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record AdCustomerContext(
    string CustomerId,
    string CustomerReference,
    string DisplayName);

public sealed record CustomerAdLinkUpsertResult(
    string Id,
    bool Changed);

public interface IActiveDirectoryLinkRepository
{
    bool IsPersistent { get; }

    Task<AdCustomerContext?> GetCustomerContextAsync(
        string customerReference,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerLinksAsync(
        string customerReference,
        CancellationToken cancellationToken);
    Task<CustomerAdLinkUpsertResult> UpsertCustomerLinkAsync(
        string customerReference,
        string? actorUserId,
        AdDirectoryObjectSummary directoryObject,
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
}
