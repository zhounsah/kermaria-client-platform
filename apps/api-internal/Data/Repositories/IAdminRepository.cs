using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IAdminRepository
{
    bool IsPersistent { get; }

    Task<AdminOverview> GetOverviewAsync(
        string adMode,
        bool adOperationsEnabled,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminCustomerSummary>> GetCustomersAsync(
        CancellationToken cancellationToken);
    Task<AdminCustomerDetail?> GetCustomerAsync(
        string customerReference,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminSupportRequestSummary>> GetSupportRequestsAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminServiceRequestSummary>> GetServiceRequestsAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminSessionSummary>> GetSessionsAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminAuditLogEntry>> GetAuditLogsAsync(
        int limit,
        CancellationToken cancellationToken);
}
