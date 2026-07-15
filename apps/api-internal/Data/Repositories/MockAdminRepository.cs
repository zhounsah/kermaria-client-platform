using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockAdminRepository : IAdminRepository
{
    private readonly MockAuthenticationStore _authenticationStore;

    public MockAdminRepository(MockAuthenticationStore authenticationStore)
    {
        _authenticationStore = authenticationStore;
    }

    public bool IsPersistent => false;

    public async Task<AdminOverview> GetOverviewAsync(
        string adMode,
        bool adOperationsEnabled,
        CancellationToken cancellationToken)
        => new(
            1,
            _authenticationStore.Users.Values.Count(
                user => user.Status == "active"),
            _authenticationStore.Sessions.Values.Count(
                session => session.RevokedAtUtc is null
                    && session.ExpiresAtUtc > DateTime.UtcNow),
            MockPortalData.SupportRequests.Count(
                request => request.Status != "closed"),
            1,
            await GetAuditLogsAsync(10, cancellationToken),
            adMode,
            adOperationsEnabled);

    public Task<IReadOnlyList<AdminCustomerSummary>> GetCustomersAsync(
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AdminCustomerSummary>>(
        [
            new(
                MockPortalData.Profile.CustomerReference,
                MockPortalData.Profile.CompanyName,
                MockPortalData.Profile.AccountStatus,
                MockPortalData.Services.Count,
                MockPortalData.SupportRequests.Count(
                    request => request.Status != "closed"),
                "2026-01-01T00:00:00Z",
                MockPortalData.Summary.LastUpdatedAt)
        ]);

    public Task<AdminCustomerDetail?> GetCustomerAsync(
        string customerReference,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                customerReference,
                MockPortalData.Profile.CustomerReference,
                StringComparison.Ordinal))
        {
            return Task.FromResult<AdminCustomerDetail?>(null);
        }

        var serviceRequests = new[]
        {
            new AdminServiceRequestSummary(
                "service-request-mock-001",
                "SRV-MOCK-ADMIN-001",
                MockPortalData.Profile.CustomerReference,
                MockPortalData.Profile.CompanyName,
                "VPN privé",
                "Qualification d'un accès",
                "Demande fictive en lecture seule.",
                "received",
                false,
                "2026-06-12T10:00:00Z",
                "2026-06-12T10:00:00Z",
                false,
                true)
        };

        var commercialDocuments = new[]
        {
            new AdminCommercialDocumentSummary(
                "commercial-doc-mock-001",
                "quote_draft",
                "shared_with_customer",
                "Proposition d'accompagnement VPN",
                "COM-20260612-0001",
                "EUR",
                19400,
                3880,
                23280,
                "Document informatif - ne constitue pas une facture officielle.",
                "2026-06-12T10:00:00Z",
                "2026-06-12T10:30:00Z",
                "2026-06-12T10:30:00Z",
                "service-request-mock-001",
                "SRV-MOCK-ADMIN-001",
                null,
                MockPortalData.Profile.CustomerReference,
                MockPortalData.Profile.CompanyName)
        };

        return Task.FromResult<AdminCustomerDetail?>(
            new AdminCustomerDetail(
                MockPortalData.Profile.CustomerReference,
                MockPortalData.Profile,
                "2026-01-01T00:00:00Z",
                MockPortalData.Summary.LastUpdatedAt,
                1,
                1,
                _authenticationStore.Sessions.Values.Count(
                    session => session.UserRole == PortalRoles.ClientUser
                        && session.RevokedAtUtc is null
                        && session.ExpiresAtUtc > DateTime.UtcNow),
                MockPortalData.Services.Count(service => service.Status == "active"),
                MockPortalData.Invoices.Count(invoice => invoice.Status == "pending"),
                MockPortalData.SupportRequests.Count(request => request.Status != "closed"),
                serviceRequests.Count(request => request.Status is "received" or "under_review" or "accepted"),
                commercialDocuments.Count(
                    document => document.Status == "shared_with_customer"),
                MockPortalData.Services,
                MockPortalData.Invoices,
                MockPortalData.SupportRequests.Select(
                    request => new AdminSupportRequestSummary(
                        request.Id,
                        request.Reference,
                        MockPortalData.Profile.CustomerReference,
                        MockPortalData.Profile.CompanyName,
                        request.ServiceName,
                        request.Priority,
                        request.Status,
                        request.Subject,
                        request.CreatedAt,
                        request.UpdatedAt,
                        false,
                        request.Status is "open" or "in_progress"))
                    .ToArray(),
                serviceRequests,
                commercialDocuments,
                [
                    new AdminActivityItem(
                        "support",
                        MockPortalData.SupportRequests[0].Id,
                        MockPortalData.SupportRequests[0].Reference,
                        MockPortalData.Profile.CustomerReference,
                        MockPortalData.Profile.CompanyName,
                        MockPortalData.SupportRequests[0].Subject,
                        MockPortalData.SupportRequests[0].Status,
                        "client",
                        MockPortalData.Profile.ContactName,
                        MockPortalData.SupportRequests[0].UpdatedAt)
                ],
                [
                    new AdminAuditLogEntry(
                        DateTime.UtcNow.ToString("O"),
                        "API-INTERNAL",
                        "admin.customers.detail.read",
                        "success",
                        null,
                        MockPortalData.Profile.CustomerReference,
                        "mock-admin-customer-detail",
                        "127.0.0.0")
                ]));
    }

    public Task<IReadOnlyList<AdminSupportRequestSummary>>
        GetSupportRequestsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AdminSupportRequestSummary>>(
            MockPortalData.SupportRequests.Select(request =>
                new AdminSupportRequestSummary(
                    request.Id,
                    request.Reference,
                    MockPortalData.Profile.CustomerReference,
                    MockPortalData.Profile.CompanyName,
                    request.ServiceName,
                    request.Priority,
                    request.Status,
                    request.Subject,
                    request.CreatedAt,
                    request.UpdatedAt,
                    false,
                    request.Status is "open" or "in_progress")).ToArray());

    public Task<IReadOnlyList<AdminServiceRequestSummary>>
        GetServiceRequestsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AdminServiceRequestSummary>>(
        [
            new(
                "service-request-mock-001",
                "SRV-MOCK-ADMIN-001",
                MockPortalData.Profile.CustomerReference,
                MockPortalData.Profile.CompanyName,
                "VPN privé",
                "Qualification d'un accès",
                "Demande fictive en lecture seule.",
                "received",
                false,
                "2026-06-12T10:00:00Z",
                "2026-06-12T10:00:00Z",
                false,
                true)
        ]);

    public Task<IReadOnlyList<AdminSessionSummary>> GetSessionsAsync(
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AdminSessionSummary>>(
            _authenticationStore.Sessions.Values
                .OrderByDescending(session => session.ExpiresAtUtc)
                .Select(session => new AdminSessionSummary(
                    session.DisplayName,
                    session.Email,
                    session.UserRole,
                    session.UserRole == PortalRoles.ClientUser
                        ? session.CustomerReference
                        : null,
                    session.LastSeenAtUtc?.ToString("O")
                        ?? session.ExpiresAtUtc.AddMinutes(-60).ToString("O"),
                    session.ExpiresAtUtc.ToString("O"),
                    session.LastSeenAtUtc?.ToString("O"),
                    "127.0.0.0",
                    "Client de test",
                    session.RevokedAtUtc is not null
                        ? "revoked"
                        : session.ExpiresAtUtc <= DateTime.UtcNow
                            ? "expired"
                            : "active"))
                .ToArray());

    public Task<IReadOnlyList<AdminAuditLogEntry>> GetAuditLogsAsync(
        int limit,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AdminAuditLogEntry>>(
        [
            new(
                DateTime.UtcNow.ToString("O"),
                "API-INTERNAL",
                "admin.overview.read",
                "success",
                null,
                null,
                "mock-admin-audit",
                "127.0.0.0")
        ]);
}
