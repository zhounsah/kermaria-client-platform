using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IPortalRepository
{
    bool IsPersistent { get; }

    Task<PortalSummary> GetSummaryAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<ClientProfile> GetProfileAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceSummary>> GetServicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceCatalogItem>> GetServiceCatalogAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportRequestSummary>> GetSupportRequestsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<SubmissionResponse> CreateSupportRequestAsync(
        PortalSessionContext session,
        SupportRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken);
    Task<SubmissionResponse> CreateServiceRequestAsync(
        PortalSessionContext session,
        ServiceRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken);
    Task AppendAuditAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken);
}
