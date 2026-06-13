namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record PortalUserCredential(
    string Id,
    string CustomerId,
    string CustomerReference,
    string Email,
    string DisplayName,
    string Status,
    string Role,
    string? PasswordHash,
    DateTime? LastLoginAtUtc,
    int FailedLoginCount,
    DateTime? LastFailedLoginAtUtc,
    DateTime? LockedUntilUtc);

public sealed record PortalSessionRecord(
    string Id,
    string UserId,
    string CustomerId,
    string CustomerReference,
    string Email,
    string DisplayName,
    string UserStatus,
    string UserRole,
    DateTime? LastLoginAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    DateTime? LastSeenAtUtc);

public sealed record PortalSessionContext(
    string SessionId,
    string UserId,
    string CustomerId,
    string CustomerReference,
    string Email,
    string DisplayName,
    string UserStatus,
    string UserRole,
    DateTime? LastLoginAtUtc,
    DateTime ExpiresAtUtc);

public sealed record LoginFailureState(
    int FailedLoginCount,
    DateTime? LockedUntilUtc);

public interface IAuthenticationRepository
{
    Task<PortalUserCredential?> FindUserByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);
    Task CreateSessionAsync(
        string id,
        string userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? sourceAddress,
        string? userAgent,
        CancellationToken cancellationToken);
    Task<PortalSessionRecord?> FindSessionAsync(
        string tokenHash,
        CancellationToken cancellationToken);
    Task TouchSessionAsync(
        string sessionId,
        DateTime seenAtUtc,
        CancellationToken cancellationToken);
    Task RevokeSessionAsync(
        string sessionId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken);
    Task<int> RevokeOtherSessionsAsync(
        string userId,
        string currentSessionId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken);
    Task<LoginFailureState> RecordFailedLoginAsync(
        string userId,
        DateTime failedAtUtc,
        DateTime failureWindowStartUtc,
        int maximumFailures,
        DateTime lockedUntilUtc,
        CancellationToken cancellationToken);
    Task ResetLoginFailuresAsync(
        string userId,
        CancellationToken cancellationToken);
    Task UpdateLastLoginAsync(
        string userId,
        DateTime loggedInAtUtc,
        CancellationToken cancellationToken);
    Task UpdatePasswordHashAsync(
        string userId,
        string passwordHash,
        CancellationToken cancellationToken);
}
