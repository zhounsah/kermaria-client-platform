using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

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
    string VerificationTokenHash,
    DateTime VerificationTokenExpiresAtUtc,
    string? SourceAddress,
    string? UserAgent,
    BillingV2PublicSelection? BillingV2Selection = null,
    string? SelfServiceFlow = null);

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
    DateTime UpdatedAtUtc,
    BillingV2PublicSelection? BillingV2Selection = null,
    DateTime? EmailVerifiedAtUtc = null,
    string? SelfServiceFlow = null);

public sealed record SignupVerificationTarget(
    string Id,
    string Status,
    DateTime? VerificationTokenExpiresAtUtc,
    string? ApprovedUserId,
    string? SelfServiceFlow = null);

/// <summary>
/// Projection minimale, liee a l'identite de session et non au customer.
/// <c>EmailVerified</c> est une preuve durable; <c>VerificationRequired</c>
/// indique qu'un workflow self-service courant doit encore obtenir cette preuve.
/// </summary>
public sealed record PortalEmailVerificationState(
    bool EmailVerified,
    bool VerificationRequired);

public sealed record SignupVerificationResendTarget(
    string Email,
    string ContactName,
    string SelfServiceFlow);

public sealed record SignupApprovalRequest(
    string SignupId,
    string CustomerId,
    string CustomerReference,
    SignupCustomerData Customer,
    SignupUserData PrimaryUser,
    string UserId,
    string? PasswordSetupTokenHash,
    DateTime? PasswordSetupExpiresAtUtc,
    string? InitialPasswordHash = null,
    bool EmailVerified = true,
    string? SelfServiceFlow = null);

public sealed record SignupApprovalResult(
    string SignupId,
    string CustomerId,
    string CustomerReference,
    string UserId,
    string KoxoUniqueIdentifier,
    string Email,
    string ContactName);

public sealed record SignupPasswordTarget(
    string SignupId,
    string PortalUserId,
    DateTime? PasswordSetupExpiresAtUtc);

public sealed record ManualCustomerCreateRequest(
    string CustomerId,
    string CustomerReference,
    SignupCustomerData Customer);

public sealed record ManualCustomerCreateResult(
    string CustomerReference,
    string DisplayName,
    string BillingEmail,
    string Status);

public interface ISignupRepository
{
    bool IsPersistent { get; }

    // Idempotence + non-leak : true si un compte existe deja ou si une demande
    // est encore active pour cet e-mail. Independant de toute limite de debit :
    // ce cas doit rester bloquant quelle que soit la configuration.
    Task<bool> HasBlockingSignupOrUserAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    /// <summary>
    /// Contrôle explicite de doublon pour la création manuelle d'une fiche.
    /// Il ne modifie pas la sémantique non-révélatrice du signup public.
    /// </summary>
    Task<bool> HasExistingCustomerEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    /// <summary>La possession d'e-mail est une policy distincte de la session et du customer.</summary>
    Task<PortalEmailVerificationState> GetPortalUserEmailVerificationStateAsync(
        string portalUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cree uniquement la fiche client. Cette primitive ne cree pas de
    /// <c>portal_user</c>, ne stocke aucun mot de passe et ne declenche aucune
    /// integration externe.
    /// </summary>
    Task<ManualCustomerCreateResult> CreateManualCustomerAsync(
        ManualCustomerCreateRequest request,
        CancellationToken cancellationToken);

    // Limites de debit appliquees cote API-INTERNAL. Elles sont comptees en
    // base et survivent donc a un redemarrage du portail, contrairement au
    // limiteur en memoire du BFF qui reste une premiere barriere.
    Task<int> CountRecentSignupsByEmailAsync(
        string normalizedEmail,
        DateTime windowStartUtc,
        CancellationToken cancellationToken);

    Task<int> CountRecentSignupsBySourceAddressAsync(
        string sourceAddress,
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

    /// <summary>
    /// Fait tourner atomiquement le token d'un workflow self-service non
    /// verifie. La valeur retournee ne contient jamais le token clair.
    /// </summary>
    Task<SignupVerificationResendTarget?> RotateSelfServiceVerificationTokenAsync(
        string portalUserId,
        string verificationTokenHash,
        DateTime verificationTokenExpiresAtUtc,
        DateTime resendAllowedBeforeUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Variante non authentifiee employee uniquement pour permettre a la boite
    /// e-mail de reprendre un compte self-service deja cree. Elle ne retourne
    /// aucune information au navigateur sur l'existence de l'identite.
    /// </summary>
    Task<SignupVerificationResendTarget?> RotateSelfServiceVerificationTokenByEmailAsync(
        string normalizedEmail,
        string verificationTokenHash,
        DateTime verificationTokenExpiresAtUtc,
        DateTime resendAllowedBeforeUtc,
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

    /// <summary>
    /// Pose le mot de passe du portail, retire le jeton de definition et, quand
    /// KoXo fait autorite, depose le secret qui lui est destine — le tout dans
    /// une seule transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le secret etait auparavant publie <b>avant</b> cet appel. Un echec ici
    /// laissait donc un mot de passe pret pour l'annuaire alors que le portail
    /// gardait l'ancien : a la synchronisation suivante, KoXo appliquait le
    /// nouveau a NextCloud, RDS et au VPN, et la personne ne pouvait plus se
    /// connecter au portail avec. Aucune erreur n'exprimait cet ecart.
    /// </para>
    /// <para>
    /// <paramref name="koxoSecret"/> arrive deja scelle : le clair ne franchit
    /// pas cette frontiere. Nul quand KoXo n'est pas l'autorite.
    /// </para>
    /// </remarks>
    Task SetPasswordAsync(
        string signupId,
        string portalUserId,
        string passwordHash,
        PortalPasswordSecret? koxoSecret,
        DateTime atUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Identifiant <c>CLI-NNNNNN</c> publie dans le CSV KoXo pour cet
    /// utilisateur, ou <c>null</c> s'il n'en a pas.
    /// </summary>
    /// <remarks>
    /// KoXo le reporte dans l'attribut <c>employeeNumber</c> de l'identite
    /// qu'il cree. C'est la seule cle de rattachement fiable : le nom subit une
    /// translitteration et le <c>sAMAccountName</c> est derive par KoXo, donc
    /// aucun des deux n'est predictible cote application.
    /// </remarks>
    Task<string?> GetKoxoUniqueIdentifierAsync(
        string portalUserId,
        CancellationToken cancellationToken);
}
