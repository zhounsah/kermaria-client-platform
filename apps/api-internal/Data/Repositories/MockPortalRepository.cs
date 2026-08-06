using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockPortalRepository : IPortalRepository
{
    // Corrections de coordonnees appliquees en memoire, par utilisateur. Le
    // mode mock ne persiste rien : l'overlay disparait au redemarrage, mais il
    // permet de rejouer le parcours complet sans MariaDB.
    private static readonly Dictionary<string, ClientProfileUpdate> ProfileOverlay =
        new(StringComparer.Ordinal);

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
        => Task.FromResult(BuildProfile(session));

    public Task<ClientProfile> UpdateProfileAsync(
        PortalSessionContext session,
        ClientProfileUpdate update,
        CancellationToken cancellationToken)
    {
        lock (ProfileOverlay)
        {
            ProfileOverlay[session.UserId] = update;
        }

        return Task.FromResult(BuildProfile(session));
    }

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

    private static ClientProfile BuildProfile(PortalSessionContext session)
    {
        var profile = MockPortalData.Profile with
        {
            CustomerReference = session.CustomerReference,
            ContactName = session.DisplayName,
            Email = session.Email
        };

        ClientProfileUpdate? overlay;
        lock (ProfileOverlay)
        {
            ProfileOverlay.TryGetValue(session.UserId, out overlay);
        }

        return overlay is null
            ? profile
            : profile with
            {
                ContactName = overlay.ContactName,
                Phone = overlay.Phone,
                Address = overlay.Address,
                City = overlay.City,
                Country = overlay.Country
            };
    }

    private static string CreateReference(string prefix)
        => $"{prefix}-MOCK-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
}
