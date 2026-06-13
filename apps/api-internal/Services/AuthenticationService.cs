using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Kermaria.ApiInternal.Services;

public sealed record SessionCreationResult(
    string SessionToken,
    AuthenticatedPortalUser User,
    DateTime ExpiresAtUtc);

public interface IAuthenticationService
{
    Task<SessionCreationResult> CreateSessionAsync(
        LoginRequest request,
        string correlationId,
        string? sourceAddress,
        string? userAgent,
        CancellationToken cancellationToken);
    Task<PortalSessionContext> ResolveSessionAsync(
        string? sessionToken,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken);
    Task RevokeSessionAsync(
        string? sessionToken,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken);
}

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IAuthenticationRepository _repository;
    private readonly IPortalPasswordService _passwordService;
    private readonly ISessionTokenService _tokenService;
    private readonly IAuditService _auditService;
    private readonly AuthRuntimeConfiguration _configuration;
    private readonly string _dummyPasswordHash;

    public AuthenticationService(
        IAuthenticationRepository repository,
        IPortalPasswordService passwordService,
        ISessionTokenService tokenService,
        IAuditService auditService,
        AuthRuntimeConfiguration configuration)
    {
        _repository = repository;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _auditService = auditService;
        _configuration = configuration;
        _dummyPasswordHash = passwordService.DummyHash;
    }

    public async Task<SessionCreationResult> CreateSessionAsync(
        LoginRequest request,
        string correlationId,
        string? sourceAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || request.Email.Length > 254
            || request.Password.Length > 1024)
        {
            await RecordLoginFailureAsync(
                correlationId,
                sourceAddress,
                "invalid_credentials",
                cancellationToken);
            throw new InvalidCredentialsException();
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _repository.FindUserByEmailAsync(
            normalizedEmail,
            cancellationToken);
        var passwordHash = user?.PasswordHash ?? _dummyPasswordHash;
        var verification = _passwordService.Verify(
            user?.Id ?? "authentication-dummy",
            passwordHash,
            request.Password);

        if (user is null
            || user.Status != "active"
            || verification == PasswordVerificationResult.Failed)
        {
            await RecordLoginFailureAsync(
                correlationId,
                sourceAddress,
                user?.Status == "disabled"
                    ? "account_disabled"
                    : "invalid_credentials",
                cancellationToken,
                user);
            throw new InvalidCredentialsException();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await _repository.UpdatePasswordHashAsync(
                user.Id,
                _passwordService.HashPassword(user.Id, request.Password),
                cancellationToken);
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.Add(_configuration.SessionDuration);
        var token = _tokenService.Generate();
        var tokenHash = _tokenService.Hash(token);
        var sessionId = Guid.NewGuid().ToString("D");

        await _repository.CreateSessionAsync(
            sessionId,
            user.Id,
            tokenHash,
            now,
            expiresAt,
            sourceAddress,
            NormalizeUserAgent(userAgent),
            cancellationToken);
        await _repository.UpdateLastLoginAsync(
            user.Id,
            now,
            cancellationToken);
        await _auditService.RecordAsync(
            new AuditEvent(
                correlationId,
                "auth.login",
                "success",
                TargetType: "portal_session",
                TargetReference: sessionId,
                CustomerId: user.CustomerId,
                ActorUserId: user.Id,
                SourceAddress: sourceAddress),
            cancellationToken);

        return new SessionCreationResult(
            token,
            ToPublicUser(user),
            expiresAt);
    }

    public async Task<PortalSessionContext> ResolveSessionAsync(
        string? sessionToken,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new SessionRequiredException();
        }

        var session = await _repository.FindSessionAsync(
            _tokenService.Hash(sessionToken),
            cancellationToken);

        if (session is null)
        {
            throw new SessionInvalidException();
        }

        if (session.RevokedAtUtc is not null)
        {
            await RecordSessionRefusalAsync(
                session,
                correlationId,
                sourceAddress,
                "session_revoked",
                cancellationToken);
            throw new SessionRevokedException();
        }

        var now = DateTime.UtcNow;
        if (session.ExpiresAtUtc <= now)
        {
            await RecordSessionRefusalAsync(
                session,
                correlationId,
                sourceAddress,
                "session_expired",
                cancellationToken);
            throw new SessionExpiredException();
        }

        if (session.UserStatus != "active")
        {
            await RecordSessionRefusalAsync(
                session,
                correlationId,
                sourceAddress,
                "account_disabled",
                cancellationToken);
            throw new PortalAccessDeniedException();
        }

        if (session.LastSeenAtUtc is null
            || session.LastSeenAtUtc < now.AddMinutes(-5))
        {
            await _repository.TouchSessionAsync(
                session.Id,
                now,
                cancellationToken);
        }

        return new PortalSessionContext(
            session.Id,
            session.UserId,
            session.CustomerId,
            session.CustomerReference,
            session.Email,
            session.DisplayName,
            session.UserStatus,
            session.ExpiresAtUtc);
    }

    public async Task RevokeSessionAsync(
        string? sessionToken,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        var session = await ResolveSessionAsync(
            sessionToken,
            correlationId,
            sourceAddress,
            cancellationToken);
        await _repository.RevokeSessionAsync(
            session.SessionId,
            DateTime.UtcNow,
            cancellationToken);
        await _auditService.RecordAsync(
            new AuditEvent(
                correlationId,
                "auth.logout",
                "success",
                TargetType: "portal_session",
                TargetReference: session.SessionId,
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: sourceAddress),
            cancellationToken);
    }

    private async Task RecordLoginFailureAsync(
        string correlationId,
        string? sourceAddress,
        string reasonCode,
        CancellationToken cancellationToken,
        PortalUserCredential? user = null)
    {
        await _auditService.RecordAsync(
            new AuditEvent(
                correlationId,
                "auth.login",
                "refused",
                reasonCode,
                "portal_session",
                CustomerId: user?.CustomerId,
                ActorUserId: user?.Id,
                SourceAddress: sourceAddress),
            cancellationToken);
    }

    private async Task RecordSessionRefusalAsync(
        PortalSessionRecord session,
        string correlationId,
        string? sourceAddress,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(
            new AuditEvent(
                correlationId,
                "auth.session",
                "refused",
                reasonCode,
                "portal_session",
                session.Id,
                session.CustomerId,
                session.UserId,
                sourceAddress),
            cancellationToken);
    }

    private static AuthenticatedPortalUser ToPublicUser(
        PortalUserCredential user)
        => new(
            user.DisplayName,
            user.Email,
            user.CustomerReference,
            user.Status);

    private static string? NormalizeUserAgent(string? userAgent)
        => string.IsNullOrWhiteSpace(userAgent)
            ? null
            : userAgent.Trim()[..Math.Min(userAgent.Trim().Length, 500)];
}

public sealed class InvalidCredentialsException : Exception;
public sealed class SessionRequiredException : Exception;
public sealed class SessionInvalidException : Exception;
public sealed class SessionExpiredException : Exception;
public sealed class SessionRevokedException : Exception;
public sealed class PortalAccessDeniedException : Exception;
