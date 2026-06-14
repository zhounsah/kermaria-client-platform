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
            false);

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
                    request.UpdatedAt)).ToArray());

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
                "2026-06-12T10:00:00Z")
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
