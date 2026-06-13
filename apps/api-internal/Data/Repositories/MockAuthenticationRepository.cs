using System.Collections.Concurrent;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockAuthenticationStore
{
    public MockAuthenticationStore(
        IConfiguration configuration,
        IPortalPasswordService passwordService)
    {
        AddUser(
            configuration["DEMO_PORTAL_EMAIL"],
            configuration["DEMO_PORTAL_PASSWORD"],
            "mock-portal-user",
            MockPortalData.Profile.ContactName,
            configuration["DEMO_PORTAL_STATUS"]?.Trim().ToLowerInvariant()
                ?? "active",
            Contracts.PortalRoles.ClientUser,
            passwordService);
        AddUser(
            configuration["DEMO_INTERNAL_ADMIN_EMAIL"],
            configuration["DEMO_INTERNAL_ADMIN_PASSWORD"],
            "mock-internal-admin",
            "Administrateur interne de démonstration",
            "active",
            Contracts.PortalRoles.InternalAdmin,
            passwordService);
    }

    public ConcurrentDictionary<string, PortalUserCredential> Users { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, PortalSessionRecord> Sessions { get; } =
        new(StringComparer.Ordinal);

    private void AddUser(
        string? emailValue,
        string? password,
        string userId,
        string displayName,
        string status,
        string role,
        IPortalPasswordService passwordService)
    {
        var email = emailValue?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (Users.ContainsKey(email))
        {
            throw new InvalidOperationException(
                "Development demo user emails must be distinct.");
        }

        Users[email] = new PortalUserCredential(
            userId,
            "mock-customer",
            MockPortalData.Profile.CustomerReference,
            email,
            displayName,
            status,
            role,
            passwordService.HashPassword(userId, password),
            null,
            0,
            null,
            null);
    }
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
        _store.Users.TryGetValue(normalizedEmail, out var user);
        return Task.FromResult(user);
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
        var user = _store.Users.Values.FirstOrDefault(
            candidate => candidate.Id == userId)
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
            user.Role,
            user.LastLoginAtUtc,
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

    public Task<int> RevokeOtherSessionsAsync(
        string userId,
        string currentSessionId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken)
    {
        var revokedCount = 0;
        foreach (var entry in _store.Sessions.ToArray())
        {
            if (entry.Value.UserId != userId
                || entry.Value.Id == currentSessionId
                || entry.Value.RevokedAtUtc is not null
                || entry.Value.ExpiresAtUtc <= revokedAtUtc)
            {
                continue;
            }

            _store.Sessions[entry.Key] = entry.Value with
            {
                RevokedAtUtc = revokedAtUtc
            };
            revokedCount++;
        }

        return Task.FromResult(revokedCount);
    }

    public Task<LoginFailureState> RecordFailedLoginAsync(
        string userId,
        DateTime failedAtUtc,
        DateTime failureWindowStartUtc,
        int maximumFailures,
        DateTime lockedUntilUtc,
        CancellationToken cancellationToken)
    {
        var user = FindUser(userId);
        var nextCount = user.LastFailedLoginAtUtc is null
            || user.LastFailedLoginAtUtc < failureWindowStartUtc
                ? 1
                : user.FailedLoginCount + 1;
        DateTime? nextLockedUntil = nextCount >= maximumFailures
            ? lockedUntilUtc
            : null;
        ReplaceUser(user with
        {
            FailedLoginCount = nextCount,
            LastFailedLoginAtUtc = failedAtUtc,
            LockedUntilUtc = nextLockedUntil
        });
        return Task.FromResult(
            new LoginFailureState(nextCount, nextLockedUntil));
    }

    public Task ResetLoginFailuresAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var user = FindUser(userId);
        ReplaceUser(user with
        {
            FailedLoginCount = 0,
            LastFailedLoginAtUtc = null,
            LockedUntilUtc = null
        });
        return Task.CompletedTask;
    }

    public Task UpdateLastLoginAsync(
        string userId,
        DateTime loggedInAtUtc,
        CancellationToken cancellationToken)
    {
        var user = FindUser(userId);
        ReplaceUser(user with { LastLoginAtUtc = loggedInAtUtc });
        return Task.CompletedTask;
    }

    public Task UpdatePasswordHashAsync(
        string userId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        var user = FindUser(userId);
        ReplaceUser(user with { PasswordHash = passwordHash });
        return Task.CompletedTask;
    }

    private PortalUserCredential FindUser(string userId)
        => _store.Users.Values.FirstOrDefault(user => user.Id == userId)
            ?? throw new InvalidOperationException(
                "Mock portal user is not configured.");

    private void ReplaceUser(PortalUserCredential user)
        => _store.Users[user.Email] = user;
}
