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
    string? SelfServiceFlow = null,
    bool ApprovalEmailPending = false);

public sealed record SignupVerificationTarget(
    string Id,
    string Status,
    DateTime? VerificationTokenExpiresAtUtc,
    string? ApprovedUserId,
    string? SelfServiceFlow = null,
    bool AutoApprovalRequested = false,
    bool ApprovalEmailPending = false);

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

/// <param name="InitialKoxoSecret">
/// Secret deja scelle destine a KoXo, pose dans la <b>meme</b> transaction que
/// le compte portail. Fourni par les parcours self-service, qui connaissent le
/// mot de passe des l'inscription ; nul pour l'inscription standard, dont le
/// mot de passe n'existe qu'au set-password.
/// </param>
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
    string? SelfServiceFlow = null,
    PortalPasswordSecret? InitialKoxoSecret = null);

/// <summary>
/// Etats de l'amorcage de l'identite AD du compte principal.
/// </summary>
/// <remarks>
/// Valeurs de la contrainte CHECK de la migration 096 : toute divergence
/// serait rejetee par la base.
/// </remarks>
public static class PrimaryIdentityBootstrapStatuses
{
    public const string AwaitingPassword = "awaiting_password";
    public const string KoxoPending = "koxo_pending";
    public const string DirectoryReady = "directory_ready";
    public const string Completed = "completed";
    public const string Failed = "failed";

    public static readonly IReadOnlyList<string> All =
    [
        AwaitingPassword,
        KoxoPending,
        DirectoryReady,
        Completed,
        Failed
    ];

    /// <summary>Etats ou l'objet AD n'est pas encore lie au compte.</summary>
    public static bool IsBootstrapping(string? status)
        => status is KoxoPending or DirectoryReady;
}

public static class PrimaryIdentityBootstrapOrigins
{
    public const string Signup = "signup";
    public const string SelfServiceCart = "self_service_cart";
    public const string SelfServiceVps = "self_service_vps";

    public static string FromSelfServiceFlow(string? selfServiceFlow)
        => selfServiceFlow switch
        {
            "cart" => SelfServiceCart,
            "vps" => SelfServiceVps,
            _ => Signup
        };

    /// <summary>
    /// Les parcours self-service creent le compte avant la preuve de
    /// possession de l'adresse ; l'inscription standard ne l'approuve
    /// qu'apres.
    /// </summary>
    public static bool RequiresEmailVerification(string origin)
        => !string.Equals(origin, Signup, StringComparison.Ordinal);
}

/// <summary>
/// Etat complet d'un amorcage, relu avec tout ce que la regle d'export exige.
/// </summary>
/// <remarks>
/// Ne porte jamais de secret : <see cref="SecretAvailable"/> dit seulement
/// qu'un chiffre non expire existe pour ce compte.
/// </remarks>
public sealed record PrimaryIdentityBootstrapRecord(
    string Id,
    string PortalUserId,
    string CustomerId,
    string CustomerReference,
    string? KoxoGroupReference,
    string SignupId,
    string KoxoUniqueIdentifier,
    string? PortalUserKoxoUniqueIdentifier,
    string Origin,
    bool EmailVerificationRequired,
    bool EmailVerified,
    string Status,
    string? FailureCode,
    string? DirectoryObjectGuid,
    DateTime? KoxoTriggeredAtUtc,
    bool PortalUserActive,
    bool CustomerActive,
    bool IsDemo,
    string? DemoKind,
    bool IdentityComplete,
    bool HasUserLink,
    bool SecretAvailable,
    bool HasAdditionalUserLifecycle);

public static class PrimaryIdentityRecoveryCodes
{
    public const string Issued = "PRIMARY_IDENTITY_RECOVERY_ISSUED";
    public const string SignupNotFound = "SIGNUP_NOT_FOUND";
    public const string InvalidState = "INVALID_STATE";
    public const string EmailVerificationRequired = "EMAIL_VERIFICATION_REQUIRED";
    public const string AlreadyLinked = "PRIMARY_IDENTITY_ALREADY_LINKED";
    public const string InProgress = "PRIMARY_IDENTITY_IN_PROGRESS";
    public const string Conflict = "PRIMARY_IDENTITY_CONFLICT";
}

/// <summary>
/// Regles de reprise, appliquees a l'etat lu sous verrou. Une seule
/// implementation : MariaDB et le mock executent litteralement la meme.
/// </summary>
public static class PrimaryIdentityRecoveryRules
{
    public static string? ClassifySignup(string status, string? portalUserId)
    {
        if (portalUserId is null)
        {
            return PrimaryIdentityRecoveryCodes.InvalidState;
        }

        // Un compte self-service dont l'adresse n'est pas prouvee reprend par
        // la verification e-mail : c'est elle qui relance l'amorcage.
        return status switch
        {
            "approved" => null,
            "email_pending" => PrimaryIdentityRecoveryCodes.EmailVerificationRequired,
            _ => PrimaryIdentityRecoveryCodes.InvalidState
        };
    }

    public static string? ClassifyBootstrap(string? status, bool secretAvailable)
        => status switch
        {
            null => null,
            PrimaryIdentityBootstrapStatuses.AwaitingPassword => null,
            // Un secret vivant suffit a KoXo : redemander un mot de passe ne
            // ferait que remplacer un secret valable.
            PrimaryIdentityBootstrapStatuses.KoxoPending => secretAvailable
                ? PrimaryIdentityRecoveryCodes.InProgress
                : null,
            // L'objet AD existe deja : il ne manque que le lien, qui ne
            // demande aucun mot de passe.
            PrimaryIdentityBootstrapStatuses.DirectoryReady =>
                PrimaryIdentityRecoveryCodes.InProgress,
            PrimaryIdentityBootstrapStatuses.Completed =>
                PrimaryIdentityRecoveryCodes.AlreadyLinked,
            _ => PrimaryIdentityRecoveryCodes.Conflict
        };
}

/// <summary>
/// Resultat d'une demande de reprise. Ne contient jamais le jeton clair.
/// </summary>
public sealed record PrimaryIdentityRecoveryTarget(
    string Code,
    string? PortalUserId = null,
    string? Email = null,
    string? ContactName = null)
{
    public bool Succeeded => string.Equals(
        Code,
        PrimaryIdentityRecoveryCodes.Issued,
        StringComparison.Ordinal);
}

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

    Task<bool> MarkEmailVerifiedAsync(
        string id,
        CancellationToken cancellationToken,
        string? expectedVerificationHash = null,
        bool autoApprovalRequested = false);

    Task<bool> RotatePendingVerificationTokenAsync(
        string id,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime resendAllowedBeforeUtc,
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

    Task<bool> TryClaimApprovalEmailRetryAsync(
        string signupId,
        DateTime nowUtc,
        DateTime nextRetryAtUtc,
        CancellationToken cancellationToken);

    Task ClearApprovalEmailPendingAsync(
        string signupId,
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

    // ------------------------------------------------------------------
    // Amorcage de l'identite AD du compte principal (migration 096)
    // ------------------------------------------------------------------

    Task<PrimaryIdentityBootstrapRecord?> GetPrimaryIdentityBootstrapAsync(
        string portalUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Amorcages qui n'attendent plus que KoXo / l'annuaire : etat
    /// <c>koxo_pending</c> ou <c>directory_ready</c>, e-mail verifie quand le
    /// parcours l'exige. Les plus anciennement tentes d'abord.
    /// </summary>
    Task<IReadOnlyList<PrimaryIdentityBootstrapRecord>>
        ListPrimaryIdentityBootstrapCandidatesAsync(
            int limit,
            CancellationToken cancellationToken);

    /// <summary>
    /// Pose le mot de passe d'un compte principal <b>sans lien AD</b> et, si
    /// <paramref name="koxoSecret"/> est fourni, depose le secret destine a
    /// KoXo et fait passer l'amorcage en <c>koxo_pending</c> — le tout dans une
    /// seule transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// C'est ce qui rompt la seconde boucle : <see cref="SetPasswordAsync"/>
    /// exige un lien pour y poser l'etat de synchronisation, or la premiere
    /// creation KoXo a justement lieu avant tout lien. Ici l'etat de
    /// synchronisation vit dans la ligne d'amorcage.
    /// </para>
    /// <para>
    /// Refuse (exception, rien n'est ecrit) si un lien AD utilisateur est
    /// apparu entre-temps : le changement de mot de passe d'un compte lie
    /// passe par <see cref="SetPasswordAsync"/>.
    /// </para>
    /// </remarks>
    Task SetPasswordForPrimaryIdentityBootstrapAsync(
        string signupId,
        string portalUserId,
        string passwordHash,
        PortalPasswordSecret? koxoSecret,
        DateTime atUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>koxo_pending</c> -&gt; <c>directory_ready</c> en figeant l'objectGUID.
    /// Idempotent pour le meme objectGUID, refuse tout autre.
    /// </summary>
    Task<bool> MarkPrimaryIdentityDirectoryResolvedAsync(
        string id,
        string directoryObjectGuid,
        DateTime resolvedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>directory_ready</c> -&gt; <c>completed</c>, <b>seulement</b> si le lien
    /// <c>customer_ad_links(user)</c> de ce compte existe avec l'objectGUID
    /// adopte. La condition est dans l'ecriture elle-meme, pas supposee.
    /// </summary>
    Task<bool> MarkPrimaryIdentityCompletedAsync(
        string id,
        DateTime linkedAtUtc,
        CancellationToken cancellationToken);

    Task<bool> MarkPrimaryIdentityFailedAsync(
        string id,
        string failureCode,
        string? failureDetail,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>koxo_pending</c> -&gt; <c>awaiting_password</c> quand le secret a
    /// disparu (expiration) avant que KoXo ne cree l'objet.
    /// </summary>
    Task<bool> MarkPrimaryIdentityAwaitingPasswordAsync(
        string id,
        string reasonCode,
        CancellationToken cancellationToken);

    Task TouchPrimaryIdentityAttemptAsync(
        string id,
        bool koxoTriggered,
        DateTime atUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reprise d'un compte principal sans identite AD dont le mot de passe
    /// clair est perdu : place l'amorcage en <c>awaiting_password</c> (le cree
    /// au besoin) et emet un jeton de definition de mot de passe, en une seule
    /// transaction.
    /// </summary>
    /// <remarks>
    /// N'essaie jamais de retrouver l'ancien mot de passe : seul le nouveau,
    /// saisi par le titulaire de la boite, alimentera KoXo.
    /// </remarks>
    Task<PrimaryIdentityRecoveryTarget> RequestPrimaryIdentityRecoveryAsync(
        string signupId,
        string passwordSetupTokenHash,
        DateTime passwordSetupExpiresAtUtc,
        DateTime atUtc,
        CancellationToken cancellationToken);
}
