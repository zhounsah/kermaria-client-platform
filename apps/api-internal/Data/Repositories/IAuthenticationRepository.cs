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

    /// <summary>
    /// Change le mot de passe du portail et, quand KoXo fait autorite, depose
    /// le secret qui lui est destine — <b>dans la meme unite de travail</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deux autorites detiennent le mot de passe : le portail, par son
    /// condensat, et KoXo, qui le reapplique a l'annuaire depuis le CSV. Les
    /// ecrire l'une apres l'autre laisse une fenetre ou l'une a change et
    /// l'autre non. Le sens de la divergence n'a pas d'importance : dans les
    /// deux cas la personne se retrouve avec un mot de passe pour ses services
    /// et un autre pour le portail, sans aucune erreur pour l'expliquer.
    /// </para>
    /// <para>
    /// Le secret arrive deja <b>scelle</b> : le clair ne franchit pas cette
    /// frontiere, et ni le depot ni la transaction ne le voient. Nul quand KoXo
    /// n'est pas l'autorite — il n'y a alors aucun relais a alimenter.
    /// </para>
    /// <para>
    /// Aucun appel reseau ici : le declenchement KoXo est fait par l'appelant
    /// <b>apres</b> le COMMIT. Une transaction ouverte pendant un appel sortant
    /// exposerait la base a la latence d'un tiers.
    /// </para>
    /// </remarks>
    /// <returns>
    /// Faux si l'utilisateur portail vise n'existe plus : rien n'est alors
    /// ecrit. Une panne de stockage <b>leve</b> — elle ne doit pas etre confondue
    /// avec un refus metier.
    /// </returns>
    Task<bool> TryChangePasswordWithKoxoHandoffAsync(
        string userId,
        string passwordHash,
        PortalPasswordSecret? koxoSecret,
        DateTime atUtc,
        CancellationToken cancellationToken);
}
