using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public interface IAdminService
{
    bool IsPersistent { get; }
    Task<AdminOverview> GetOverviewAsync(
        string adMode,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminCustomerSummary>> GetCustomersAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminSupportRequestSummary>> GetSupportRequestsAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminServiceRequestSummary>> GetServiceRequestsAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminSessionSummary>> GetSessionsAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminAuditLogEntry>> GetAuditLogsAsync(
        CancellationToken cancellationToken);
}

public sealed class AdminService : IAdminService
{
    private readonly IAdminRepository _repository;

    public AdminService(IAdminRepository repository)
    {
        _repository = repository;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public Task<AdminOverview> GetOverviewAsync(
        string adMode,
        CancellationToken cancellationToken)
        => _repository.GetOverviewAsync(adMode, cancellationToken);

    public Task<IReadOnlyList<AdminCustomerSummary>> GetCustomersAsync(
        CancellationToken cancellationToken)
        => _repository.GetCustomersAsync(cancellationToken);

    public Task<IReadOnlyList<AdminSupportRequestSummary>>
        GetSupportRequestsAsync(CancellationToken cancellationToken)
        => _repository.GetSupportRequestsAsync(cancellationToken);

    public Task<IReadOnlyList<AdminServiceRequestSummary>>
        GetServiceRequestsAsync(CancellationToken cancellationToken)
        => _repository.GetServiceRequestsAsync(cancellationToken);

    public Task<IReadOnlyList<AdminSessionSummary>> GetSessionsAsync(
        CancellationToken cancellationToken)
        => _repository.GetSessionsAsync(cancellationToken);

    public Task<IReadOnlyList<AdminAuditLogEntry>> GetAuditLogsAsync(
        CancellationToken cancellationToken)
        => _repository.GetAuditLogsAsync(100, cancellationToken);
}
