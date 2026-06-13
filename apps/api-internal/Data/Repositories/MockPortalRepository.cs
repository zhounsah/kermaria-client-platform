using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockPortalRepository : IPortalRepository
{
    public bool IsPersistent => false;

    public Task<PortalSummary> GetSummaryAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => Task.FromResult(MockPortalData.Summary with
        {
            CustomerReference = session.CustomerReference,
            ContactName = session.DisplayName
        });

    public Task<ClientProfile> GetProfileAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => Task.FromResult(MockPortalData.Profile with
        {
            CustomerReference = session.CustomerReference,
            ContactName = session.DisplayName,
            Email = session.Email
        });

    public Task<IReadOnlyList<ServiceSummary>> GetServicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => Task.FromResult(MockPortalData.Services);

    public Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => Task.FromResult(MockPortalData.Invoices);

    public Task<IReadOnlyList<ServiceCatalogItem>> GetServiceCatalogAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(MockPortalData.ServiceCatalog);

    public Task<IReadOnlyList<SupportRequestSummary>> GetSupportRequestsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => Task.FromResult(MockPortalData.SupportRequests);

    public Task<SubmissionResponse> CreateSupportRequestAsync(
        PortalSessionContext session,
        SupportRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        if (payload.ServiceId != "account"
            && !MockPortalData.Services.Any(
                service => service.Id == payload.ServiceId))
        {
            throw new PortalAccessDeniedException();
        }

        return Task.FromResult(new SubmissionResponse(
            CreateReference("SUP"),
            "mock_received",
            false,
            "Demande mock reçue. Aucune donnée n'a été persistée.",
            correlationId));
    }

    public Task<SubmissionResponse> CreateServiceRequestAsync(
        PortalSessionContext session,
        ServiceRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        if (!MockPortalData.ServiceCatalog.Any(
                item => item.Id == payload.CatalogItemId))
        {
            throw new PortalValidationException();
        }

        return Task.FromResult(new SubmissionResponse(
            CreateReference("SRV"),
            "mock_received",
            false,
            "Demande de service mock reçue. Aucun devis ni paiement n'a été créé.",
            correlationId));
    }

    public Task AppendAuditAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private static string CreateReference(string prefix)
        => $"{prefix}-MOCK-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
}
