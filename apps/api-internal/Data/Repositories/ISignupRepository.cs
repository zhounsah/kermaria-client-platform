using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

// V0.38 : persistance des demandes d'inscription. Le workflow reste
// mono-utilisateur, mais la couche stocke maintenant une identite client
// et utilisateur structuree pour preparer l'alignement AD.
public sealed record SignupInsert(
    string Id,
    string CompanyName,
    string ContactName,
    string Email,
    string? Phone,
    string? Message,
    SignupCustomerData Customer,
    SignupUserData PrimaryUser,
    SignupPackSelectionSnapshot? PackSelection,
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
    SignupCustomerData Customer,
    SignupUserData PrimaryUser,
    SignupPackSelectionSnapshot? PackSelection,
    string? SourceAddress,
    DateTime? VerificationTokenExpiresAtUtc,
    string? ApprovedUserId,
    string? ApprovedCustomerId,
    string? ApprovedCustomerReference,
    DateTime? ApprovedAtUtc,
    DateTime? PasswordSetupExpiresAtUtc,
    bool ApprovedUserHasPassword,
    string? AdProvisioningStatus,
    string? LastPasswordSyncStatus,
    string? KoxoExportStatus,
    string? ApprovedUserSamAccountName,
    string? ApprovedUserPrincipalName,
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
    SignupCustomerData Customer,
    SignupUserData PrimaryUser,
    string UserId,
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
    // existe deja pour cet e-mail dans la fenetre donnee.
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

    Task<SignupPendingRecord?> GetLatestApprovedByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken);

    // Cree customer + portal_user (sans mot de passe) et bascule la
    // demande en 'approved'. Retourne null si la demande est absente ou
    // n'est pas en etat 'email_verified'.
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

    Task RefreshPasswordSetupTokenAsync(
        string signupId,
        string passwordSetupTokenHash,
        DateTime passwordSetupExpiresAtUtc,
        CancellationToken cancellationToken);

    Task SetPasswordAsync(
        string signupId,
        string portalUserId,
        string passwordHash,
        CancellationToken cancellationToken);
}
