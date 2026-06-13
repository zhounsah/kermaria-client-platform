using System.Collections.Concurrent;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockAuthenticationStore
{
    public MockAuthenticationStore(
        IConfiguration configuration,
        IPortalPasswordService passwordService)
    {
        var email = configuration["DEMO_PORTAL_EMAIL"]?.Trim().ToLowerInvariant();
        var password = configuration["DEMO_PORTAL_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        const string userId = "mock-portal-user";
        User = new PortalUserCredential(
            userId,
            "mock-customer",
            MockPortalData.Profile.CustomerReference,
            email,
            MockPortalData.Profile.ContactName,
            configuration["DEMO_PORTAL_STATUS"]?.Trim().ToLowerInvariant()
                ?? "active",
            passwordService.HashPassword(userId, password));
    }

    public PortalUserCredential? User { get; }
    public ConcurrentDictionary<string, PortalSessionRecord> Sessions { get; } =
        new(StringComparer.Ordinal);
}

public sealed class MockAuthenticationRepository : IAuthenticationRepository
{
    private readonly MockAuthenticationStore _store;

    public MockAuthenticationRepository(MockAuthenticationStore store)
    {
        _store = store;
    }

    public Task<PortalUserCredential?> FindUserByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var user = _store.User;
        return Task.FromResult(
            user is not null
            && string.Equals(
                user.Email,
                normalizedEmail,
                StringComparison.OrdinalIgnoreCase)
                ? user
                : null);
    }

    public Task CreateSessionAsync(
        string id,
        string userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? sourceAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var user = _store.User
            ?? throw new InvalidOperationException(
                "Mock portal user is not configured.");
        _store.Sessions[tokenHash] = new PortalSessionRecord(
            id,
            userId,
            user.CustomerId,
            user.CustomerReference,
            user.Email,
            user.DisplayName,
            user.Status,
            expiresAtUtc,
            null,
            createdAtUtc);
        return Task.CompletedTask;
    }

    public Task<PortalSessionRecord?> FindSessionAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        _store.Sessions.TryGetValue(tokenHash, out var session);
        return Task.FromResult(session);
    }

    public Task TouchSessionAsync(
        string sessionId,
        DateTime seenAtUtc,
        CancellationToken cancellationToken)
    {
        var entry = _store.Sessions.FirstOrDefault(
            pair => pair.Value.Id == sessionId);
        if (!string.IsNullOrEmpty(entry.Key))
        {
            _store.Sessions[entry.Key] = entry.Value with
            {
                LastSeenAtUtc = seenAtUtc
            };
        }

        return Task.CompletedTask;
    }

    public Task RevokeSessionAsync(
        string sessionId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken)
    {
        var entry = _store.Sessions.FirstOrDefault(
            pair => pair.Value.Id == sessionId);
        if (!string.IsNullOrEmpty(entry.Key))
        {
            _store.Sessions[entry.Key] = entry.Value with
            {
                RevokedAtUtc = revokedAtUtc
            };
        }

        return Task.CompletedTask;
    }

    public Task UpdateLastLoginAsync(
        string userId,
        DateTime loggedInAtUtc,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task UpdatePasswordHashAsync(
        string userId,
        string passwordHash,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}
