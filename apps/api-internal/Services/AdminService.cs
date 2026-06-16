using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public interface IAdminService
{
    bool IsPersistent { get; }
    Task<AdminOverview> GetOverviewAsync(
        string adMode,
        bool adOperationsEnabled,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminCustomerSummary>> GetCustomersAsync(
        CancellationToken cancellationToken);
    Task<AdminCustomerDetail> GetCustomerAsync(
        string customerReference,
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
        bool adOperationsEnabled,
        CancellationToken cancellationToken)
        => _repository.GetOverviewAsync(
            adMode,
            adOperationsEnabled,
            cancellationToken);

    public Task<IReadOnlyList<AdminCustomerSummary>> GetCustomersAsync(
        CancellationToken cancellationToken)
        => _repository.GetCustomersAsync(cancellationToken);

    public async Task<AdminCustomerDetail> GetCustomerAsync(
        string customerReference,
        CancellationToken cancellationToken)
        => await _repository.GetCustomerAsync(
                ValidateCustomerReference(customerReference),
                cancellationToken)
            ?? throw new PortalDataNotFoundException();

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

    private static string ValidateCustomerReference(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 1 or > 100)
        {
            throw new PortalValidationException();
        }

        foreach (var character in normalized)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                throw new PortalValidationException();
            }
        }

        return normalized;
    }
}
