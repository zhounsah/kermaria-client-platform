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
    private readonly IClientServiceCatalogService _serviceCatalogService;

    public AdminService(
        IAdminRepository repository,
        IClientServiceCatalogService serviceCatalogService)
    {
        _repository = repository;
        _serviceCatalogService = serviceCatalogService;
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
    {
        var normalizedCustomerReference = ValidateCustomerReference(
            customerReference);
        var customer = await _repository.GetCustomerAsync(
                normalizedCustomerReference,
                cancellationToken)
            ?? throw new PortalDataNotFoundException();

        var services = await _serviceCatalogService.GetServicesAsync(
            BuildAdminProjectionSession(customer),
            cancellationToken);

        return customer with
        {
            ActiveServiceCount = services.Count(service => service.Status == "active"),
            Services = services
        };
    }

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

    private static PortalSessionContext BuildAdminProjectionSession(
        AdminCustomerDetail customer)
        => new(
            SessionId: $"admin-projection-{customer.CustomerId}",
            UserId: "internal-admin",
            CustomerId: customer.CustomerId,
            CustomerReference: customer.Identity.CustomerReference,
            Email: customer.Identity.Email,
            DisplayName: customer.Identity.ContactName,
            UserStatus: "active",
            UserRole: PortalRoles.InternalAdmin,
            LastLoginAtUtc: null,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(5));
}
