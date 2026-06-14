using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IRequestWorkflowRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<SupportRequestSummary>> GetClientSupportRequestsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceRequestSummary>> GetClientServiceRequestsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<PortalSupportRequestDetail?> GetClientSupportRequestAsync(
        PortalSessionContext session,
        string requestId,
        CancellationToken cancellationToken);
    Task<PortalServiceRequestDetail?> GetClientServiceRequestAsync(
        PortalSessionContext session,
        string requestId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminSupportRequestSummary>> GetAdminSupportRequestsAsync(
        AdminRequestListQuery query,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminServiceRequestSummary>> GetAdminServiceRequestsAsync(
        AdminRequestListQuery query,
        CancellationToken cancellationToken);
    Task<AdminSupportRequestDetail?> GetAdminSupportRequestAsync(
        string requestId,
        CancellationToken cancellationToken);
    Task<AdminServiceRequestDetail?> GetAdminServiceRequestAsync(
        string requestId,
        CancellationToken cancellationToken);

    Task<RequestMutationResponse> UpdateStatusAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string status,
        string correlationId,
        CancellationToken cancellationToken);
    Task<RequestMutationResponse> AddInternalNoteAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string note,
        string correlationId,
        CancellationToken cancellationToken);
    Task<RequestMutationResponse> AddPublicMessageAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string message,
        string correlationId,
        CancellationToken cancellationToken);
}
