namespace Kermaria.ApiInternal.Data.Repositories;

// V0.26 : persistance des demandes d'inscription self-service. Les hash
// de jeton ne quittent jamais cette couche ; les DTO exposés à l'API et
// à l'admin n'en contiennent aucun.
public sealed record SignupInsert(
    string Id,
    string CompanyName,
    string ContactName,
    string Email,
    string? Phone,
    string? Message,
    string VerificationTokenHash,
    DateTime VerificationTokenExpiresAtUtc,
    string? SourceAddress,
    string? UserAgent);

public sealed record SignupPendingRecord(
    string Id,
    string Status,
    string CompanyName,
    string ContactName,
    string Email,
    string? Phone,
    string? Message,
    string? SourceAddress,
    DateTime? VerificationTokenExpiresAtUtc,
    string? ApprovedUserId,
    string? ApprovedCustomerId,
    DateTime? ApprovedAtUtc,
    DateTime? RejectedAtUtc,
    string? RejectedReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SignupVerificationTarget(
    string Id,
    string Status,
    DateTime? VerificationTokenExpiresAtUtc);

public sealed record SignupApprovalRequest(
    string SignupId,
    string CustomerId,
    string CustomerReference,
    string CompanyName,
    string BillingEmail,
    string? Phone,
    string UserId,
    string ContactName,
    string PasswordSetupTokenHash,
    DateTime PasswordSetupExpiresAtUtc);

public sealed record SignupApprovalResult(
    string SignupId,
    string CustomerId,
    string CustomerReference,
    string UserId,
    string Email,
    string ContactName);

public sealed record SignupPasswordTarget(
    string SignupId,
    string PortalUserId,
    DateTime? PasswordSetupExpiresAtUtc);

public interface ISignupRepository
{
    bool IsPersistent { get; }

    // Idempotence + non-leak : true si un compte ou une demande active
    // existe déjà pour cet e-mail dans la fenêtre donnée.
    Task<bool> HasRecentSignupOrUserAsync(
        string normalizedEmail,
        DateTime windowStartUtc,
        CancellationToken cancellationToken);

    Task InsertPendingAsync(
        SignupInsert insert,
        CancellationToken cancellationToken);

    Task<SignupVerificationTarget?> FindPendingByVerificationHashAsync(
        string verificationTokenHash,
        CancellationToken cancellationToken);

    Task MarkEmailVerifiedAsync(
        string id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SignupPendingRecord>> ListAsync(
        string? statusFilter,
        int limit,
        CancellationToken cancellationToken);

    Task<SignupPendingRecord?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken);

    // Crée customer + portal_user (sans mot de passe) et bascule la
    // demande en 'approved'. Retourne null si la demande est absente ou
    // n'est pas en état 'email_verified'.
    Task<SignupApprovalResult?> ApproveAsync(
        SignupApprovalRequest request,
        CancellationToken cancellationToken);

    Task<bool> RejectAsync(
        string id,
        string? reason,
        CancellationToken cancellationToken);

    Task<SignupPasswordTarget?> FindApprovedByPasswordHashAsync(
        string passwordSetupTokenHash,
        CancellationToken cancellationToken);

    Task SetPasswordAsync(
        string signupId,
        string portalUserId,
        string passwordHash,
        CancellationToken cancellationToken);
}
