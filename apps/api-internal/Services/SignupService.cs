using System.Globalization;
using System.Text;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Email;
using Kermaria.ApiInternal.Services.Provisioning;
using Microsoft.Extensions.Logging;

namespace Kermaria.ApiInternal.Services;

public sealed record SignupOperationResult(
    bool Succeeded,
    string Code,
    string Message,
    string? SelfServiceFlow = null);

/// <summary>
/// Resultat d'une inscription immediate reservee au configurateur VPS
/// self-service. Le jeton de session ne quitte jamais API-INTERNAL autrement
/// que par le BFF, qui le place dans le cookie HttpOnly existant.
/// </summary>
public sealed record SignupSelfServiceOperationResult(
    bool Succeeded,
    string Code,
    string Message,
    SessionCreationResult? Session = null);

/// <summary>
/// Resultat d'un pas de convergence de l'identite AD du compte principal.
/// Aucune donnee annuaire ni aucun secret : seulement l'etape atteinte.
/// </summary>
public sealed record PrimaryIdentityBootstrapResult(
    bool Succeeded,
    string Code,
    string? Status);

public interface ISignupService
{
    bool IsPersistent { get; }

    Task<SignupOperationResult> SubmitAsync(
        SignupSubmitPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<(SignupOperationResult Result, AdminCustomerCreateResponse? Customer)> CreateManualCustomerAsync(
        AdminCustomerCreatePayload payload,
        CancellationToken cancellationToken);

    Task<SignupSelfServiceOperationResult> CompleteSelfServiceVpsAsync(
        SignupSubmitPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SignupSelfServiceOperationResult> CompleteSelfServiceCartAsync(
        SignupSubmitPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> VerifyEmailAsync(
        string? token,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> ResendSelfServiceEmailVerificationAsync(
        PortalSessionContext session,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SignupAdminSummary>> ListAsync(
        string? statusFilter,
        CancellationToken cancellationToken);

    Task<SignupAdminDetail?> GetAsync(
        string id,
        CancellationToken cancellationToken);

    Task<PendingBillingV2SelectionSummary?> GetPendingBillingV2SelectionAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> ApproveAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> RejectAsync(
        string id,
        string? reason,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> InitializePasswordAsync(
        string id,
        string? password,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> ResendPasswordSetupEmailAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> ResendVerificationEmailAsync(
        string id, string correlationId, CancellationToken cancellationToken);

    Task<SignupOperationResult> SetPasswordAsync(
        string? token,
        string? password,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> ValidateSetPasswordTokenAsync(
        string? token,
        CancellationToken cancellationToken);

    /// <summary>
    /// Fait avancer l'amorcage de l'identite AD d'un compte principal.
    /// Idempotent.
    /// </summary>
    Task<PrimaryIdentityBootstrapResult> ConvergePrimaryIdentityAsync(
        string portalUserId,
        CancellationToken cancellationToken);

    /// <summary>Passage du worker : les amorcages qui n'attendent que l'annuaire.</summary>
    Task<int> ConvergePendingPrimaryIdentitiesAsync(
        int batchSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reprise administrateur d'un compte principal sans identite AD : envoie
    /// un lien de definition de mot de passe qui fera entrer le compte dans
    /// l'amorcage.
    /// </summary>
    Task<SignupOperationResult> RequestPrimaryIdentityRecoveryAsync(
        string signupId,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class SignupService : ISignupService
{
    private const int MinPasswordLength = 12;
    private const int MaxPasswordLength = 200;
    private const int MaxEmailLength = 320;
    private const int MaxNameLength = 200;
    private const int MaxMessageLength = 2000;
    private const int MaxCustomerTypeLength = 32;
    private const int MaxPostalCodeLength = 32;
    private const int MaxCountryLength = 100;
    private const int MaxShortNameLength = 120;
    private const int MaxInitialsLength = 16;
    private static readonly TimeSpan SelfServiceVerificationResendCooldown =
        TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AdminVerificationResendCooldown = TimeSpan.FromMinutes(1);
    private static readonly HashSet<string> AllowedPersonalTitles =
        new(StringComparer.Ordinal)
        {
            "madame",
            "monsieur"
        };

    private readonly ISignupRepository _repository;
    private readonly IEmailDispatchService _emailDispatch;
    private readonly IPortalPasswordService _passwordService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly IActiveDirectoryLinkRepository _activeDirectoryLinkRepository;
    private readonly IAdGroupProvisioner _adGroupProvisioner;
    private readonly IKoxoPendingPasswordStore _pendingPasswords;
    private readonly IKoxoSyncWebhookTriggerService _koxoSyncWebhookTriggerService;
    private readonly SignupRuntimeConfiguration _configuration;
    private readonly IApplicationSettingsService _settings;
    private readonly EmailRuntimeConfiguration _emailConfiguration;
    private readonly AdRuntimeConfiguration _adConfiguration;
    private readonly ILogger<SignupService> _logger;

    public SignupService(
        ISignupRepository repository,
        IEmailDispatchService emailDispatch,
        IPortalPasswordService passwordService,
        IAuthenticationService authenticationService,
        IActiveDirectoryService activeDirectoryService,
        IActiveDirectoryLinkRepository activeDirectoryLinkRepository,
        IAdGroupProvisioner adGroupProvisioner,
        IKoxoPendingPasswordStore pendingPasswords,
        IKoxoSyncWebhookTriggerService koxoSyncWebhookTriggerService,
        SignupRuntimeConfiguration configuration,
        IApplicationSettingsService settings,
        EmailRuntimeConfiguration emailConfiguration,
        AdRuntimeConfiguration adConfiguration,
        ILogger<SignupService> logger)
    {
        _repository = repository;
        _emailDispatch = emailDispatch;
        _passwordService = passwordService;
        _authenticationService = authenticationService;
        _activeDirectoryService = activeDirectoryService;
        _activeDirectoryLinkRepository = activeDirectoryLinkRepository;
        _adGroupProvisioner = adGroupProvisioner;
        _pendingPasswords = pendingPasswords;
        _koxoSyncWebhookTriggerService = koxoSyncWebhookTriggerService;
        _configuration = configuration;
        _settings = settings;
        _emailConfiguration = emailConfiguration;
        _adConfiguration = adConfiguration;
        _logger = logger;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public async Task<(SignupOperationResult Result, AdminCustomerCreateResponse? Customer)> CreateManualCustomerAsync(
        AdminCustomerCreatePayload payload,
        CancellationToken cancellationToken)
    {
        var customer = NormalizeManualCustomer(payload);
        if (customer is null)
        {
            return (new SignupOperationResult(
                false,
                "INVALID_CUSTOMER",
                "Les informations client sont incomplètes ou invalides."), null);
        }

        var email = customer.BillingEmail!;
        if (await _repository.HasBlockingSignupOrUserAsync(email, cancellationToken)
            || await _repository.HasExistingCustomerEmailAsync(email, cancellationToken))
        {
            return (new SignupOperationResult(
                false,
                "CUSTOMER_EMAIL_ALREADY_USED",
                "Cette adresse e-mail est déjà utilisée."), null);
        }

        ManualCustomerCreateResult created;
        try
        {
            created = await _repository.CreateManualCustomerAsync(
                new ManualCustomerCreateRequest(
                    Guid.NewGuid().ToString(),
                    GenerateCustomerReference(),
                    customer),
                cancellationToken);
        }
        catch (InvalidOperationException exception) when (
            string.Equals(
                exception.Message,
                "CUSTOMER_EMAIL_ALREADY_USED",
                StringComparison.Ordinal))
        {
            return (new SignupOperationResult(
                false,
                "CUSTOMER_EMAIL_ALREADY_USED",
                "Cette adresse e-mail est déjà utilisée."), null);
        }
        return (new SignupOperationResult(
                true,
                "CUSTOMER_CREATED",
                "La fiche client a été créée sans accès portail."),
            new AdminCustomerCreateResponse(
                created.CustomerReference,
                created.DisplayName,
                created.BillingEmail,
                created.Status));
    }

    public async Task<SignupOperationResult> SubmitAsync(
        SignupSubmitPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var runtime = await _settings.GetSignupConfigurationAsync(_configuration, cancellationToken);
        if (!runtime.Enabled)
        {
            return new SignupOperationResult(
                false,
                "SIGNUP_DISABLED",
                "Les inscriptions ne sont pas ouvertes.");
        }

        var normalized = await NormalizeSubmissionAsync(payload, cancellationToken);
        if (normalized is null)
        {
            return new SignupOperationResult(
                false,
                "INVALID_REQUEST",
                "Les informations transmises sont invalides.");
        }

        // Compte existant ou demande deja active : reponse identique a un succes,
        // pour ne pas reveler qu'une adresse est connue.
        if (await _repository.HasBlockingSignupOrUserAsync(
                normalized.Email,
                cancellationToken))
        {
            _logger.LogInformation(
                "Signup submission ignored (duplicate or existing account) correlation_id {CorrelationId}",
                correlationId);
            return Accepted();
        }

        var now = DateTime.UtcNow;
        var sourceAddress = NormalizeOptional(payload.SourceAddress, 45);

        // Limite par adresse : refus explicite, aucune information sur le
        // demandeur. Le BFF pose deja un limiteur en memoire ; celui-ci est
        // compte en base, donc insensible a un redemarrage du portail et pilote
        // par le parametre administrable.
        if (!string.IsNullOrEmpty(sourceAddress))
        {
            var perAddress = await _repository.CountRecentSignupsBySourceAddressAsync(
                sourceAddress,
                now.AddHours(-1),
                cancellationToken);
            if (perAddress >= runtime.RateLimitPerIpPerHour)
            {
                _logger.LogInformation(
                    "Signup submission rate limited by source address correlation_id {CorrelationId}",
                    correlationId);
                return new SignupOperationResult(
                    false,
                    "RATE_LIMITED",
                    "Trop de demandes successives. Reessayez plus tard.");
            }
        }

        // Limite par adresse e-mail : silencieuse, meme motif de non-divulgation
        // que le doublon ci-dessus.
        var perEmail = await _repository.CountRecentSignupsByEmailAsync(
            normalized.Email,
            now.AddHours(-24),
            cancellationToken);
        if (perEmail >= runtime.RateLimitPerEmailPer24h)
        {
            _logger.LogInformation(
                "Signup submission rate limited by email correlation_id {CorrelationId}",
                correlationId);
            return Accepted();
        }

        var token = GenerateToken();
        var insert = new SignupInsert(
            Guid.NewGuid().ToString("D"),
            normalized.CompanyName,
            normalized.ContactName,
            normalized.Email,
            normalized.Phone,
            normalized.Message,
            normalized.Customer,
            normalized.PrimaryUser,
            HashToken(token),
            now.AddHours(runtime.VerificationTokenTtlHours),
            sourceAddress,
            NormalizeOptional(payload.UserAgent, 500),
            BillingV2Selection: normalized.BillingV2Selection);
        await _repository.InsertPendingAsync(insert, cancellationToken);

        var verificationUrl = BuildUrl("/signup/verify", token);
        var delivery = await _emailDispatch.SendSignupVerificationAsync(
            normalized.Email,
            normalized.ContactName,
            verificationUrl,
            correlationId,
            cancellationToken);
        if (!delivery.Succeeded)
        {
            _logger.LogWarning(
                "Signup verification email not delivered ({Code}) correlation_id {CorrelationId}",
                delivery.Code,
                correlationId);
        }

        return Accepted();
    }

    /// <summary>
    /// Cree immediatement le client et son acces portail pour un VPS dont la
    /// capacite self-service a deja ete revalidee par le point d'entree.
    ///
    /// Le workflow historique (verification e-mail puis approbation humaine)
    /// reste inchange pour toute autre inscription. Ici, hCaptcha et les
    /// limites de debit sont deja appliques par le BFF, puis les memes limites
    /// durables sont reevaluees ci-dessous. Comme tout compte client
    /// principal, le compte VPS possede une identite AD : il entre dans le
    /// meme amorcage que le Cart, meme si le VPS lui-meme n'ajoute aucun
    /// groupe de securite.
    /// </summary>
    public Task<SignupSelfServiceOperationResult>
        CompleteSelfServiceVpsAsync(
            SignupSubmitPayload payload,
            string correlationId,
            CancellationToken cancellationToken) =>
        CompleteSelfServiceAccountAsync(
            payload,
            correlationId,
            "SELF_SERVICE_VPS_ACCOUNT_CREATED",
            "Compte créé. Reprise de votre configuration VPS.",
            "Votre compte a été créé, mais la connexion automatique est indisponible. Connectez-vous pour reprendre votre configuration VPS.",
            "vps",
            cancellationToken);

    /// <summary>
    /// Cree immediatement l'acces client pour un Cart dont la possession a
    /// deja ete prouvee par le BFF. Cette primitive ne claim pas le Cart et
    /// n'ouvre aucun checkout : la page de reprise utilise ensuite la commande
    /// Cart existante, sous session, dans son propre flux transactionnel.
    /// </summary>
    public Task<SignupSelfServiceOperationResult>
        CompleteSelfServiceCartAsync(
            SignupSubmitPayload payload,
            string correlationId,
            CancellationToken cancellationToken) =>
        CompleteSelfServiceAccountAsync(
            payload,
            correlationId,
            "SELF_SERVICE_CART_ACCOUNT_CREATED",
            "Compte créé. Reprise de votre panier.",
            "Votre compte a été créé, mais la connexion automatique est indisponible. Connectez-vous pour reprendre votre panier.",
            "cart",
            cancellationToken);

    private async Task<SignupSelfServiceOperationResult>
        CompleteSelfServiceAccountAsync(
            SignupSubmitPayload payload,
            string correlationId,
        string successCode,
        string successMessage,
        string sessionUnavailableMessage,
        string selfServiceFlow,
        CancellationToken cancellationToken)
    {
        var runtime = await _settings.GetSignupConfigurationAsync(
            _configuration,
            cancellationToken);
        if (!runtime.Enabled)
        {
            return new SignupSelfServiceOperationResult(
                false,
                "SIGNUP_DISABLED",
                "Les inscriptions ne sont pas ouvertes.");
        }

        var normalized = await NormalizeSubmissionAsync(payload, cancellationToken);
        if (normalized is null || payload.Password is null
            || payload.Password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            return new SignupSelfServiceOperationResult(
                false,
                "INVALID_REQUEST",
                "Les informations transmises sont invalides.");
        }

        if (await _repository.HasBlockingSignupOrUserAsync(
                normalized.Email,
                cancellationToken))
        {
            // Une identite self-service non verifiee ne doit pas immobiliser
            // definitivement l'adresse si son premier lien a expire. La
            // reponse reste non revelatrice : seul le titulaire de la boite
            // peut exploiter le lien renouvelé qui lui est envoye.
            var recoveryToken = GenerateToken();
            var recovery = await _repository.RotateSelfServiceVerificationTokenByEmailAsync(
                normalized.Email,
                HashToken(recoveryToken),
                DateTime.UtcNow.AddHours(runtime.VerificationTokenTtlHours),
                DateTime.UtcNow.Subtract(SelfServiceVerificationResendCooldown),
                cancellationToken);
            if (recovery is not null)
            {
                await SendVerificationEmailSafelyAsync(
                    recovery,
                    recoveryToken,
                    correlationId,
                    cancellationToken);
            }

            return new SignupSelfServiceOperationResult(
                false,
                "ACCOUNT_ALREADY_EXISTS",
                "Cet accès ne peut pas être créé. Connectez-vous ou utilisez une autre adresse e-mail.");
        }

        var now = DateTime.UtcNow;
        var sourceAddress = NormalizeOptional(payload.SourceAddress, 45);
        if (!string.IsNullOrEmpty(sourceAddress))
        {
            var perAddress = await _repository.CountRecentSignupsBySourceAddressAsync(
                sourceAddress,
                now.AddHours(-1),
                cancellationToken);
            if (perAddress >= runtime.RateLimitPerIpPerHour)
            {
                return new SignupSelfServiceOperationResult(
                    false,
                    "RATE_LIMITED",
                    "Trop de demandes successives. Reessayez plus tard.");
            }
        }

        var perEmail = await _repository.CountRecentSignupsByEmailAsync(
            normalized.Email,
            now.AddHours(-24),
            cancellationToken);
        if (perEmail >= runtime.RateLimitPerEmailPer24h)
        {
            return new SignupSelfServiceOperationResult(
                false,
                "RATE_LIMITED",
                "Trop de demandes successives. Reessayez plus tard.");
        }

        // Le token est conserve jusqu'a la preuve de possession. La creation
        // immediate du compte ne rend jamais l'adresse e-mail verifiee.
        var signupId = Guid.NewGuid().ToString("D");
        var verificationToken = GenerateToken();
        await _repository.InsertPendingAsync(
            new SignupInsert(
                signupId,
                normalized.CompanyName,
                normalized.ContactName,
                normalized.Email,
                normalized.Phone,
                normalized.Message,
                normalized.Customer,
                normalized.PrimaryUser,
                HashToken(verificationToken),
                now.AddHours(runtime.VerificationTokenTtlHours),
                sourceAddress,
                NormalizeOptional(payload.UserAgent, 500),
                BillingV2Selection: null,
                SelfServiceFlow: selfServiceFlow),
            cancellationToken);

        var customerId = Guid.NewGuid().ToString("D");
        var userId = Guid.NewGuid().ToString("D");
        // Le mot de passe clair n'existe qu'ici : il est scelle pour KoXo et
        // depose avec le compte, dans la meme transaction. L'amorcage de
        // l'identite AD nait donc en `koxo_pending`, mais aucun export ni
        // aucune creation annuaire n'a lieu avant la preuve de possession de
        // l'adresse. Pas de seconde implementation AD ici : tout passe par le
        // meme cycle que l'inscription standard.
        var approval = await _repository.ApproveAsync(
            new SignupApprovalRequest(
                signupId,
                customerId,
                GenerateCustomerReference(),
                normalized.Customer,
                normalized.PrimaryUser,
                userId,
                PasswordSetupTokenHash: null,
                PasswordSetupExpiresAtUtc: null,
                InitialPasswordHash: _passwordService.HashPassword(userId, payload.Password),
                EmailVerified: false,
                SelfServiceFlow: selfServiceFlow,
                InitialKoxoSecret: SealInitialPrimaryIdentitySecret(userId, payload.Password)),
            cancellationToken);
        if (approval is null)
        {
            return new SignupSelfServiceOperationResult(
                false,
                "SIGNUP_CREATION_FAILED",
                "Le compte n'a pas pu être créé. Réessayez plus tard.");
        }

        try
        {
            var session = await _authenticationService.CreateSessionAsync(
                new LoginRequest(normalized.Email, payload.Password),
                correlationId,
                sourceAddress,
                NormalizeOptional(payload.UserAgent, 500),
                cancellationToken);
            return new SignupSelfServiceOperationResult(
                true,
                successCode,
                successMessage,
                session);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Self-service {Flow} account was created but its initial session could not be opened correlation_id {CorrelationId}",
                selfServiceFlow,
                correlationId);
            return new SignupSelfServiceOperationResult(
                false,
                "SIGNUP_SESSION_UNAVAILABLE",
                sessionUnavailableMessage);
        }
        finally
        {
            await SendVerificationEmailSafelyAsync(
                new SignupVerificationResendTarget(
                    normalized.Email,
                    normalized.ContactName,
                    selfServiceFlow),
                verificationToken,
                correlationId,
                cancellationToken,
                BuildSelfServiceContinuationPath(payload, selfServiceFlow));
        }
    }

    /// <summary>
    /// Secret initial de l'amorcage self-service, ou <c>null</c> quand il ne
    /// peut pas etre retenu.
    /// </summary>
    /// <remarks>
    /// <c>null</c> ne bloque pas la creation du compte : l'amorcage nait alors
    /// en <c>awaiting_password</c>, visiblement, et la verification de
    /// l'adresse enverra un lien de definition du mot de passe. Refuser un
    /// achat parce que le relais KoXo est mal configure ne protegerait rien.
    /// </remarks>
    private PortalPasswordSecret? SealInitialPrimaryIdentitySecret(
        string portalUserId,
        string password)
    {
        if (!_adConfiguration.WritesEnabled)
        {
            return null;
        }

        var secret = _pendingPasswords.IsOperational
            ? _pendingPasswords.Seal(portalUserId, password)
            : null;
        if (secret is null)
        {
            _logger.LogError(
                "KoXo password handoff is not operational: primary identity bootstrap of portal_user_id {PortalUserId} starts awaiting a password.",
                portalUserId);
        }

        return secret;
    }

    public async Task<SignupOperationResult> VerifyEmailAsync(
        string? token,
        CancellationToken cancellationToken)
    {
        var normalized = token?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return TokenInvalid();
        }

        var target = await _repository.FindPendingByVerificationHashAsync(
            HashToken(normalized),
            cancellationToken);
        if (target is null)
        {
            return TokenInvalid();
        }

        if (string.Equals(target.Status, "approved", StringComparison.Ordinal)
            || string.Equals(target.Status, "email_verified", StringComparison.Ordinal))
        {
            return new SignupOperationResult(
                true,
                "EMAIL_ALREADY_VERIFIED",
                "Adresse e-mail déjà confirmée.",
                target.SelfServiceFlow);
        }

        if (!string.Equals(target.Status, "email_pending", StringComparison.Ordinal)) return TokenInvalid();

        if (target.VerificationTokenExpiresAtUtc is { } expiry
            && expiry < DateTime.UtcNow)
        {
            return new SignupOperationResult(
                false,
                "TOKEN_EXPIRED",
                "Ce lien de verification a expire. Renouvelez votre demande.");
        }

        if (!await _repository.MarkEmailVerifiedAsync(target.Id, cancellationToken, HashToken(normalized)))
            return TokenInvalid();
        if (target.ApprovedUserId is not null)
        {
            // Compte self-service deja cree : la preuve de possession rend son
            // amorcage AD eligible a l'export KoXo.
            await AdvancePrimaryIdentityAfterEmailVerificationAsync(
                target.Id,
                target.ApprovedUserId,
                cancellationToken);
        }

        return new SignupOperationResult(
            true,
            "EMAIL_VERIFIED",
            target.ApprovedUserId is null
                ? "Adresse e-mail confirmee. Votre demande est en attente de validation."
                : "Adresse e-mail confirmée. Vous pouvez reprendre votre souscription.",
            target.SelfServiceFlow);
    }

    public async Task<SignupOperationResult> ResendSelfServiceEmailVerificationAsync(
        PortalSessionContext session,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var runtime = await _settings.GetSignupConfigurationAsync(_configuration, cancellationToken);
        var token = GenerateToken();
        var target = await _repository.RotateSelfServiceVerificationTokenAsync(
            session.UserId,
            HashToken(token),
            DateTime.UtcNow.AddHours(runtime.VerificationTokenTtlHours),
            DateTime.UtcNow.Subtract(SelfServiceVerificationResendCooldown),
            cancellationToken);
        if (target is null)
        {
            return new SignupOperationResult(
                false,
                "EMAIL_VERIFICATION_RESEND_UNAVAILABLE",
                "Un lien vient déjà d'être envoyé. Réessayez plus tard si nécessaire.");
        }

        await SendVerificationEmailSafelyAsync(target, token, correlationId, cancellationToken);
        return new SignupOperationResult(
            true,
            "EMAIL_VERIFICATION_RESENT",
            "Un nouveau lien de vérification a été envoyé à votre adresse e-mail.");
    }

    private async Task SendVerificationEmailSafelyAsync(
        SignupVerificationResendTarget target,
        string verificationToken,
        string correlationId,
        CancellationToken cancellationToken,
        string? continuationPath = null)
    {
        try
        {
            // Le seul clair vit dans ce lien de livraison. Les doubles de test
            // remplacent ce dispatch ; aucun test ne contacte SMTP.
            var delivery = await _emailDispatch.SendSignupVerificationAsync(
                target.Email,
                target.ContactName,
                BuildUrl("/signup/verify", verificationToken, continuationPath),
                correlationId,
                cancellationToken);
            if (!delivery.Succeeded)
            {
                _logger.LogWarning("Self-service email verification delivery failed ({Code}) correlation_id {CorrelationId}", delivery.Code, correlationId);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // La creation de compte est deja durable. Une indisponibilite SMTP
            // ne doit ni annuler la session locale, ni faire croire au client
            // que le compte n'existe pas.
            _logger.LogError(exception, "Self-service email verification dispatch failed correlation_id {CorrelationId}", correlationId);
        }
    }

    public async Task<IReadOnlyList<SignupAdminSummary>> ListAsync(
        string? statusFilter,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeStatusFilter(statusFilter);
        var records = await _repository.ListAsync(
            normalized,
            50,
            cancellationToken);
        return records.Select(ToSummary).ToList();
    }

    public async Task<SignupAdminDetail?> GetAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(id, cancellationToken);
        return record is null ? null : ToDetail(record);
    }

    public async Task<PendingBillingV2SelectionSummary?> GetPendingBillingV2SelectionAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetLatestApprovedByCustomerIdAsync(
            session.CustomerId,
            cancellationToken);
        if (record?.BillingV2Selection is null)
        {
            return null;
        }

        return new PendingBillingV2SelectionSummary(
            record.Id,
            record.Status,
            ToNullableIso(record.ApprovedAtUtc),
            ToIso(record.CreatedAtUtc),
            record.BillingV2Selection);
    }


    public async Task<SignupOperationResult> ApproveAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(id, cancellationToken);
        if (record is null)
        {
            return new SignupOperationResult(
                false,
                "SIGNUP_NOT_FOUND",
                "Demande introuvable.");
        }

        if (!string.Equals(
                record.Status,
                "email_verified",
                StringComparison.Ordinal))
        {
            return new SignupOperationResult(
                false,
                "INVALID_STATE",
                "Seules les demandes verifiees par e-mail peuvent etre approuvees.");
        }

        var passwordToken = GenerateToken();
        var request = new SignupApprovalRequest(
            record.Id,
            Guid.NewGuid().ToString("D"),
            GenerateCustomerReference(),
            record.Customer,
            record.PrimaryUser,
            Guid.NewGuid().ToString("D"),
            HashToken(passwordToken),
            DateTime.UtcNow.AddHours((await _settings.GetSignupConfigurationAsync(_configuration, cancellationToken)).PasswordSetupTokenTtlHours));

        var result = await _repository.ApproveAsync(request, cancellationToken);
        if (result is null)
        {
            return new SignupOperationResult(
                false,
                "INVALID_STATE",
                "La demande n'a pas pu etre approuvee dans son etat actuel.");
        }

        // Des l'approbation, pour que KoXo ait cree l'identite quand le client
        // suivra son lien. Sans ce declenchement, le set-password arriverait
        // avant l'identite et repondrait AD_IDENTITY_NOT_READY.
        await SendKoxoSyncTriggerAsync(
            result.SignupId,
            result.UserId,
            result.CustomerReference,
            "signup_approved",
            cancellationToken);

        var setPasswordUrl = BuildUrl("/set-password", passwordToken);
        var delivery = await _emailDispatch.SendAccountApprovedAsync(
            result.Email,
            result.ContactName,
            setPasswordUrl,
            correlationId,
            cancellationToken);
        if (!delivery.Succeeded)
        {
            _logger.LogWarning(
                "Account approved email not delivered ({Code}) correlation_id {CorrelationId}",
                delivery.Code,
                correlationId);
        }

        return new SignupOperationResult(
            true,
            "SIGNUP_APPROVED",
            $"Compte cree ({result.CustomerReference}). Un lien de definition de mot de passe a ete envoye.");
    }

    public async Task<SignupOperationResult> RejectAsync(
        string id,
        string? reason,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(id, cancellationToken);
        if (record is null)
        {
            return new SignupOperationResult(
                false,
                "SIGNUP_NOT_FOUND",
                "Demande introuvable.");
        }

        var normalizedReason = NormalizeOptional(reason, 500);
        var rejected = await _repository.RejectAsync(
            id,
            normalizedReason,
            cancellationToken);
        if (!rejected)
        {
            return new SignupOperationResult(
                false,
                "INVALID_STATE",
                "Seules les demandes en cours peuvent etre refusees.");
        }

        var delivery = await _emailDispatch.SendAccountRejectedAsync(
            record.Email,
            record.ContactName,
            normalizedReason,
            correlationId,
            cancellationToken);
        if (!delivery.Succeeded)
        {
            _logger.LogWarning(
                "Account rejected email not delivered ({Code}) correlation_id {CorrelationId}",
                delivery.Code,
                correlationId);
        }

        return new SignupOperationResult(
            true,
            "SIGNUP_REJECTED",
            "Demande refusee.");
    }

    public async Task<SignupOperationResult> InitializePasswordAsync(
        string id,
        string? password,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(id, cancellationToken);
        if (record is null)
        {
            return new SignupOperationResult(
                false,
                "SIGNUP_NOT_FOUND",
                "Demande introuvable.");
        }

        if (!IsAwaitingPasswordSetup(record))
        {
            return new SignupOperationResult(
                false,
                "INVALID_STATE",
                "Ce compte n'est plus en attente de definition du mot de passe.");
        }

        if (password is null
            || password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            return new SignupOperationResult(
                false,
                "INVALID_PASSWORD",
                $"Le mot de passe doit comporter entre {MinPasswordLength} et {MaxPasswordLength} caracteres.");
        }

        var passwordError = await ApplyPasswordAsync(
            record,
            password,
            cancellationToken);
        if (passwordError is not null)
        {
            return passwordError;
        }

        return new SignupOperationResult(
            true,
            "PASSWORD_INITIALIZED",
            "Mot de passe initialise. Le client peut maintenant se connecter avec son adresse e-mail.");
    }

    public async Task<SignupOperationResult> ResendVerificationEmailAsync(
        string id, string correlationId, CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(id, cancellationToken);
        if (record is null)
            return new(false, "SIGNUP_NOT_FOUND", "Demande introuvable.");
        if (record.Status != "email_pending")
            return new(false, "INVALID_STATE", "Cette demande n'est plus en attente de confirmation e-mail.");

        var runtime = await _settings.GetSignupConfigurationAsync(_configuration, cancellationToken);
        var token = GenerateToken();
        var now = DateTime.UtcNow;
        if (!await _repository.RotatePendingVerificationTokenAsync(
            id, HashToken(token), now.AddHours(runtime.VerificationTokenTtlHours),
            now.Subtract(AdminVerificationResendCooldown), cancellationToken))
            return new(false, "VERIFICATION_RESEND_NOT_READY", "La demande a changé ou un lien vient d'être envoyé. Attendez une minute puis actualisez la fiche.");

        var delivery = await _emailDispatch.SendSignupVerificationAsync(
            record.Email, record.ContactName, BuildUrl("/signup/verify", token),
            correlationId, cancellationToken);
        if (!delivery.Succeeded)
            return new(false, "EMAIL_VERIFICATION_DELIVERY_FAILED", "Le nouveau lien a été généré, mais son envoi a échoué. Réessayez après une minute.");
        return new(true, "VERIFICATION_EMAIL_SENT", "Un nouveau lien de confirmation a été envoyé. L'ancien lien n'est plus valide.");
    }

    public async Task<SignupOperationResult> ResendPasswordSetupEmailAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(id, cancellationToken);
        if (record is null)
        {
            return new SignupOperationResult(
                false,
                "SIGNUP_NOT_FOUND",
                "Demande introuvable.");
        }

        if (!IsAwaitingPasswordSetup(record))
        {
            return new SignupOperationResult(
                false,
                "INVALID_STATE",
                "Ce compte n'est plus en attente de definition du mot de passe.");
        }

        var passwordToken = GenerateToken();
        await _repository.RefreshPasswordSetupTokenAsync(
            record.Id,
            HashToken(passwordToken),
            DateTime.UtcNow.AddHours((await _settings.GetSignupConfigurationAsync(_configuration, cancellationToken)).PasswordSetupTokenTtlHours),
            cancellationToken);

        var setPasswordUrl = BuildUrl("/set-password", passwordToken);
        var delivery = await _emailDispatch.SendAccountApprovedAsync(
            record.Email,
            record.ContactName,
            setPasswordUrl,
            correlationId,
            cancellationToken);
        if (!delivery.Succeeded)
        {
            _logger.LogWarning(
                "Password setup email resend not delivered ({Code}) correlation_id {CorrelationId}",
                delivery.Code,
                correlationId);
            return new SignupOperationResult(
                false,
                delivery.Code,
                "Le nouveau lien a bien ete genere, mais l'e-mail n'a pas pu etre envoye.");
        }

        return new SignupOperationResult(
            true,
            "PASSWORD_SETUP_EMAIL_SENT",
            "Un nouveau lien de definition du mot de passe a ete envoye.");
    }

    public async Task<SignupOperationResult> SetPasswordAsync(
        string? token,
        string? password,
        CancellationToken cancellationToken)
    {
        var normalizedToken = token?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return TokenInvalid();
        }

        if (password is null
            || password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            return new SignupOperationResult(
                false,
                "INVALID_PASSWORD",
                $"Le mot de passe doit comporter entre {MinPasswordLength} et {MaxPasswordLength} caracteres.");
        }

        var target = await _repository.FindApprovedByPasswordHashAsync(
            HashToken(normalizedToken),
            cancellationToken);
        if (target is null)
        {
            return TokenInvalid();
        }

        if (target.PasswordSetupExpiresAtUtc is { } expiry
            && expiry < DateTime.UtcNow)
        {
            return new SignupOperationResult(
                false,
                "TOKEN_EXPIRED",
                "Ce lien de definition de mot de passe a expire.");
        }

        var record = await _repository.GetByIdAsync(
            target.SignupId,
            cancellationToken);
        if (record is null || record.ApprovedUserId is null)
        {
            return TokenInvalid();
        }

        var passwordError = await ApplyPasswordAsync(
            record,
            password,
            cancellationToken);
        if (passwordError is not null)
        {
            return passwordError;
        }

        return new SignupOperationResult(
            true,
            "PASSWORD_SET",
            "Mot de passe defini. Vous pouvez desormais vous connecter.");
    }

    public async Task<SignupOperationResult> ValidateSetPasswordTokenAsync(
        string? token,
        CancellationToken cancellationToken)
    {
        var normalizedToken = token?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return TokenInvalid();
        }

        var target = await _repository.FindApprovedByPasswordHashAsync(
            HashToken(normalizedToken),
            cancellationToken);
        if (target is null)
        {
            return TokenInvalid();
        }

        if (target.PasswordSetupExpiresAtUtc is { } expiry
            && expiry < DateTime.UtcNow)
        {
            return new SignupOperationResult(
                false,
                "TOKEN_EXPIRED",
                "Ce lien de definition de mot de passe a expire.");
        }

        return new SignupOperationResult(
            true,
            "TOKEN_VALID",
            "Lien valide. Choisissez votre mot de passe.");
    }

    private async Task<SignupOperationResult?> ApplyPasswordAsync(
        SignupPendingRecord record,
        string password,
        CancellationToken cancellationToken)
    {
        if (record.ApprovedUserId is null)
        {
            return new SignupOperationResult(
                false,
                "INVALID_STATE",
                "Le compte approuve est incomplet.");
        }

        if (!_adConfiguration.WritesEnabled)
        {
            // Annuaire desactive : seul le portail recoit le mot de passe.
            // L'amorcage, cree avec le compte, reste en attente et sera repris
            // par un nouveau mot de passe une fois l'annuaire ouvert.
            return await StorePortalPasswordAsync(record, password, null, cancellationToken);
        }

        if (!_adConfiguration.ConfigurationValid)
        {
            return new SignupOperationResult(
                false,
                "AD_CONFIGURATION_INVALID",
                "La configuration Active Directory est incomplete.");
        }

        var existingLink =
            await _activeDirectoryLinkRepository.FindUserLinkByPortalUserIdAsync(
                record.ApprovedUserId,
                cancellationToken);
        if (existingLink is not null)
        {
            // Identite deja liee : c'est un changement de mot de passe, pas un
            // amorcage. Le secret suit le lien existant.
            var (adError, koxoSecret) = await ProvisionActiveDirectoryAsync(
                record,
                existingLink,
                password,
                cancellationToken);
            if (adError is not null)
            {
                return adError;
            }

            return await StorePortalPasswordAsync(
                record,
                password,
                koxoSecret,
                cancellationToken);
        }

        return await BootstrapPrimaryIdentityPasswordAsync(
            record,
            password,
            cancellationToken);
    }

    /// <summary>
    /// Mot de passe d'un compte principal <b>sans</b> identite AD liee.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rompt les deux boucles historiques. Avant, sous autorite KoXo, on
    /// cherchait l'identite AVANT de rien ecrire : absente — elle l'etait
    /// toujours, puisqu'un compte sans lien n'etait jamais exporte — le
    /// set-password repondait <c>AD_IDENTITY_NOT_READY</c> indefiniment. Et le
    /// depot refusait de deposer le secret sans lien a mettre a jour.
    /// </para>
    /// <para>
    /// Desormais le secret scelle, le condensat du portail et l'amorcage
    /// <c>koxo_pending</c> sont ecrits dans UNE transaction ; l'amorcage rend
    /// le compte exportable, KoXo cree l'identite, l'adoption par
    /// <c>employeeNumber</c> pose le lien. Le compte portail est utilisable des
    /// le COMMIT, meme si l'annuaire converge plus tard.
    /// </para>
    /// </remarks>
    private async Task<SignupOperationResult?> BootstrapPrimaryIdentityPasswordAsync(
        SignupPendingRecord record,
        string password,
        CancellationToken cancellationToken)
    {
        // Fail-closed avant tout point de non-retour : sans magasin
        // exploitable, le jeton serait consomme pour un secret qui
        // n'atteindrait jamais l'annuaire.
        var koxoSecret = _pendingPasswords.IsOperational
            ? _pendingPasswords.Seal(record.ApprovedUserId!, password)
            : null;
        if (koxoSecret is null)
        {
            return new SignupOperationResult(
                false,
                "KOXO_PASSWORD_HANDOFF_UNAVAILABLE",
                "Le mot de passe ne peut pas etre transmis a KoXo pour le moment.");
        }

        var passwordHash = _passwordService.HashPassword(
            record.ApprovedUserId!,
            password);
        try
        {
            await _repository.SetPasswordForPrimaryIdentityBootstrapAsync(
                record.Id,
                record.ApprovedUserId!,
                passwordHash,
                koxoSecret,
                DateTime.UtcNow,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Primary identity bootstrap password could not be stored for portal_user_id {PortalUserId}",
                record.ApprovedUserId);
            return new SignupOperationResult(
                false,
                "PASSWORD_CHANGE_STORAGE_UNAVAILABLE",
                "Le mot de passe n'a pas pu etre enregistre : rien n'a ete modifie. Reessayez plus tard.");
        }

        // Apres le COMMIT seulement. Le declenchement et la premiere tentative
        // de convergence sont des rattrapages : l'amorcage durable est deja la,
        // et le worker le reprendra.
        await TriggerKoxoSyncWebhookAsync(record, cancellationToken);
        await TryConvergePrimaryIdentityAsync(
            record.ApprovedUserId!,
            password,
            allowKoxoTrigger: false,
            cancellationToken);
        return null;
    }

    private async Task<SignupOperationResult?> StorePortalPasswordAsync(
        SignupPendingRecord record,
        string password,
        PortalPasswordSecret? koxoSecret,
        CancellationToken cancellationToken)
    {
        var passwordHash = _passwordService.HashPassword(
            record.ApprovedUserId!,
            password);

        // Une seule unite de travail : condensat portail, retrait du jeton et
        // secret destine a KoXo. Un echec ne doit laisser aucun secret derriere
        // lui — le jeton reste alors utilisable et la personne peut recommencer.
        try
        {
            await _repository.SetPasswordAsync(
                record.Id,
                record.ApprovedUserId!,
                passwordHash,
                koxoSecret,
                DateTime.UtcNow,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Signup password could not be stored for portal_user_id {PortalUserId}",
                record.ApprovedUserId);
            return new SignupOperationResult(
                false,
                "PASSWORD_CHANGE_STORAGE_UNAVAILABLE",
                "Le mot de passe n'a pas pu etre enregistre : rien n'a ete modifie. Reessayez plus tard.");
        }

        // Apres le COMMIT seulement, et en rattrapage : la synchronisation
        // planifiee repassera de toute facon sur le secret desormais durable.
        await TriggerKoxoSyncWebhookAsync(record, cancellationToken);
        return null;
    }

    private Task TriggerKoxoSyncWebhookAsync(
        SignupPendingRecord record,
        CancellationToken cancellationToken)
        => SendKoxoSyncTriggerAsync(record, "password_set", cancellationToken);

    private Task SendKoxoSyncTriggerAsync(
        SignupPendingRecord record,
        string trigger,
        CancellationToken cancellationToken)
        => SendKoxoSyncTriggerAsync(
            record.Id,
            record.ApprovedUserId,
            record.ApprovedCustomerReference,
            trigger,
            cancellationToken);

    /// <summary>
    /// Notifie KoXo qu'une donnee exportee a change, pour qu'il applique le
    /// changement a l'annuaire.
    /// </summary>
    /// <remarks>
    /// Plus de filtre sur <c>koxo_export_status</c> : ne declencher que sur
    /// <c>koxo_pending</c> revenait a ne notifier QUE la premiere creation, si
    /// bien que toute modification ulterieure restait invisible de l'annuaire
    /// jusqu'a la synchronisation planifiee suivante.
    ///
    /// L'echec est journalise sans etre propage : la synchronisation est un
    /// rattrapage, pas une condition de succes de l'operation appelante.
    /// </remarks>
    private async Task SendKoxoSyncTriggerAsync(
        string signupId,
        string? portalUserId,
        string? customerReference,
        string trigger,
        CancellationToken cancellationToken)
    {
        if (!_adConfiguration.WritesEnabled
            || portalUserId is null
            || string.IsNullOrWhiteSpace(customerReference))
        {
            return;
        }

        var correlationId = Guid.NewGuid().ToString("D");
        try
        {
            await _koxoSyncWebhookTriggerService.TriggerAsync(
                new KoxoSyncWebhookTriggerRequest(
                    signupId,
                    portalUserId,
                    customerReference,
                    trigger,
                    correlationId,
                    DateTime.UtcNow.ToString("O")),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "KoXo sync webhook trigger failed for signup_id {SignupId}, portal_user_id {PortalUserId}, customer_reference {CustomerReference}, trigger {Trigger}, correlation_id {CorrelationId}",
                signupId,
                portalUserId,
                customerReference,
                trigger,
                correlationId);
        }
    }

    /// <summary>
    /// Changement de mot de passe d'un compte dont l'identite AD est deja
    /// liee.
    /// </summary>
    /// <remarks>
    /// Rend le secret <b>scelle</b> destine a KoXo, sans l'ecrire : son depot a
    /// lieu dans la transaction qui pose le condensat du portail. Publie ici,
    /// il survivait a l'echec de cette transaction, et KoXo appliquait alors a
    /// l'annuaire un mot de passe que le portail ne connaissait pas.
    /// </remarks>
    private async Task<(SignupOperationResult? error, PortalPasswordSecret? secret)>
        ProvisionActiveDirectoryAsync(
        SignupPendingRecord record,
        PortalUserAdLinkRecord existingLink,
        string password,
        CancellationToken cancellationToken)
    {
        if (_adConfiguration.KoxoOwnsDirectory)
        {
            // Fail-closed avant tout point de non-retour : sans magasin
            // exploitable, le secret n'atteindrait jamais l'annuaire.
            var sealed_ = _pendingPasswords.IsOperational
                ? _pendingPasswords.Seal(record.ApprovedUserId!, password)
                : null;
            if (sealed_ is null)
            {
                return (new SignupOperationResult(
                    false,
                    "KOXO_PASSWORD_HANDOFF_UNAVAILABLE",
                    "Le mot de passe ne peut pas etre transmis a KoXo pour le moment."), null);
            }

            // L'etat de synchronisation est pose par la meme transaction que
            // le secret : l'annoncer ici le rendrait vrai avant que le
            // secret n'existe.
            return (null, sealed_);
        }

        // Mode Mock : aucun KoXo derriere, c'est bien a l'application
        // d'appliquer le mot de passe a l'objet deja lie.
        var now = DateTime.UtcNow;
        var syncResult = await _activeDirectoryService.SetUserPasswordAsync(
            existingLink.CustomerReference,
            existingLink.SamAccountName,
            password,
            cancellationToken);
        if (syncResult.StatusCode >= 400 || syncResult.Value is null)
        {
            return (MapAdProvisioningFailure(
                syncResult,
                "Le compte Active Directory n'a pas pu etre synchronise."), null);
        }

        await _activeDirectoryLinkRepository.UpsertPortalUserLinkAsync(
            existingLink.CustomerReference,
            record.ApprovedUserId!,
            actorUserId: null,
            syncResult.Value,
            _adConfiguration.Domain,
            "succeeded",
            existingLink.AdProvisionedAtUtc ?? now,
            "succeeded",
            now,
            existingLink.KoxoExportStatus ?? "koxo_pending",
            cancellationToken);
        return (null, null);
    }

    // ------------------------------------------------------------------
    // Amorcage de l'identite AD du compte principal
    // ------------------------------------------------------------------

    private static readonly TimeSpan PrimaryIdentityKoxoTriggerInterval =
        TimeSpan.FromMinutes(10);

    public async Task<PrimaryIdentityBootstrapResult> ConvergePrimaryIdentityAsync(
        string portalUserId,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetPrimaryIdentityBootstrapAsync(
            portalUserId,
            cancellationToken);
        if (record is null)
        {
            return new PrimaryIdentityBootstrapResult(
                false,
                PrimaryIdentityBootstrapCodes.LifecycleMissing,
                null);
        }

        return (await ConvergePrimaryIdentityCoreAsync(
            record,
            plaintextPassword: null,
            allowKoxoTrigger: true,
            cancellationToken)).Result;
    }

    public async Task<int> ConvergePendingPrimaryIdentitiesAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (!_adConfiguration.WritesEnabled || batchSize <= 0)
        {
            return 0;
        }

        var candidates = await _repository.ListPrimaryIdentityBootstrapCandidatesAsync(
            batchSize,
            cancellationToken);
        var completed = 0;
        foreach (var candidate in candidates)
        {
            var triggered = false;
            try
            {
                var outcome = await ConvergePrimaryIdentityCoreAsync(
                    candidate,
                    plaintextPassword: null,
                    allowKoxoTrigger: true,
                    cancellationToken);
                triggered = outcome.KoxoTriggered;
                if (string.Equals(
                        outcome.Result.Code,
                        PrimaryIdentityBootstrapCodes.Completed,
                        StringComparison.Ordinal))
                {
                    completed++;
                    continue;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Primary identity convergence failed for portal_user_id {PortalUserId}; a later pass will retry it.",
                    candidate.PortalUserId);
            }

            // Rotation des candidats : un compte qui n'avance pas laisse passer
            // les suivants au lieu de monopoliser chaque passage.
            if (!triggered)
            {
                try
                {
                    await _repository.TouchPrimaryIdentityAttemptAsync(
                        candidate.Id,
                        koxoTriggered: false,
                        DateTime.UtcNow,
                        cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        exception,
                        "Could not rotate primary identity candidate {BootstrapId}.",
                        candidate.Id);
                }
            }
        }

        return completed;
    }

    private async Task TryConvergePrimaryIdentityAsync(
        string portalUserId,
        string? plaintextPassword,
        bool allowKoxoTrigger,
        CancellationToken cancellationToken)
    {
        try
        {
            var record = await _repository.GetPrimaryIdentityBootstrapAsync(
                portalUserId,
                cancellationToken);
            if (record is not null)
            {
                await ConvergePrimaryIdentityCoreAsync(
                    record,
                    plaintextPassword,
                    allowKoxoTrigger,
                    cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Rattrapage seulement : l'amorcage est durable et le worker le
            // reprendra. L'operation appelante a deja abouti.
            _logger.LogWarning(
                exception,
                "Primary identity convergence deferred for portal_user_id {PortalUserId}.",
                portalUserId);
        }
    }

    private readonly record struct PrimaryIdentityConvergence(
        PrimaryIdentityBootstrapResult Result,
        bool KoxoTriggered);

    /// <summary>
    /// Fait avancer l'amorcage d'un cran, ou constate qu'il n'avance pas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Strictement idempotente : chaque etape est conditionnee a l'etat courant
    /// et reverifiee. Rejouee sur un amorcage deja conclu, elle ne cree ni
    /// second objet, ni second lien ; rejouee apres la creation KoXo mais
    /// avant le lien, elle retrouve le meme objet par son employeeNumber.
    /// </para>
    /// <para>
    /// <paramref name="plaintextPassword"/> ne sert qu'en mode mock, ou aucun
    /// KoXo n'applique le mot de passe. A defaut, le secret en attente est
    /// relu : c'est sa raison d'etre.
    /// </para>
    /// </remarks>
    private async Task<PrimaryIdentityConvergence> ConvergePrimaryIdentityCoreAsync(
        PrimaryIdentityBootstrapRecord record,
        string? plaintextPassword,
        bool allowKoxoTrigger,
        CancellationToken cancellationToken)
    {
        switch (record.Status)
        {
            case PrimaryIdentityBootstrapStatuses.Completed:
                // Un amorcage conclu ne conserve aucun secret ; un acquittement
                // interrompu se rattrape ici.
                await _pendingPasswords.AcknowledgeAsync(
                    record.PortalUserId,
                    cancellationToken);
                return Converged(true, PrimaryIdentityBootstrapCodes.Completed, record);

            case PrimaryIdentityBootstrapStatuses.Failed:
                return Converged(false, PrimaryIdentityBootstrapCodes.Failed, record);

            case PrimaryIdentityBootstrapStatuses.AwaitingPassword:
                return Converged(false, PrimaryIdentityBootstrapCodes.AwaitingPassword, record);
        }

        if (!_adConfiguration.WritesEnabled)
        {
            return Converged(false, PrimaryIdentityBootstrapCodes.DirectoryDisabled, record);
        }

        if (!_adConfiguration.ConfigurationValid)
        {
            return Converged(false, PrimaryIdentityBootstrapCodes.ConfigurationInvalid, record);
        }

        // Adresse non prouvee, compte inactif, identifiant KoXo incoherent :
        // aucune action annuaire, ni creation, ni adoption.
        var blocker = PrimaryIdentityBootstrapPolicy.GetIdentityBlocker(record);
        if (blocker is not null)
        {
            return Converged(false, blocker, record);
        }

        // Un lien deja present prime sur toute resolution : c'est le retry
        // apres adoption reussie, et resoudre de nouveau risquerait d'adopter
        // un objet different.
        var existingLink = await _activeDirectoryLinkRepository
            .FindUserLinkByPortalUserIdAsync(record.PortalUserId, cancellationToken);
        if (existingLink is not null)
        {
            return Converged(await FinishPrimaryIdentityFromLinkAsync(
                record,
                existingLink.ObjectGuid,
                cancellationToken));
        }

        AdDirectoryObjectSummary? directoryObject;
        if (_adConfiguration.KoxoOwnsDirectory)
        {
            // KoXo cree l'identite ; on ne fait que l'adopter, par son seul
            // employeeNumber, sans aucun rapprochement approchant.
            directoryObject = await _adGroupProvisioner.ResolveUserByEmployeeNumberAsync(
                record.KoxoUniqueIdentifier,
                cancellationToken);
            if (directoryObject is null)
            {
                if (record.Status == PrimaryIdentityBootstrapStatuses.KoxoPending
                    && !record.SecretAvailable)
                {
                    // Ni objet ni secret : KoXo ne pourra plus rien creer. Seul
                    // un nouveau mot de passe, fourni par le titulaire, relance
                    // le cycle — jamais une reconstitution de l'ancien.
                    await _repository.MarkPrimaryIdentityAwaitingPasswordAsync(
                        record.Id,
                        PrimaryIdentityBootstrapCodes.SecretMissing,
                        cancellationToken);
                    return Converged(
                        false,
                        PrimaryIdentityBootstrapCodes.SecretMissing,
                        record with { Status = PrimaryIdentityBootstrapStatuses.AwaitingPassword });
                }

                var triggered = false;
                if (allowKoxoTrigger
                    && (record.KoxoTriggeredAtUtc is not { } lastTrigger
                        || DateTime.UtcNow - lastTrigger >= PrimaryIdentityKoxoTriggerInterval))
                {
                    // Chaque synchronisation KoXo est globale : on la redemande
                    // au plus toutes les dix minutes par compte, pas a chaque
                    // passage du worker.
                    await SendKoxoSyncTriggerAsync(
                        record.SignupId,
                        record.PortalUserId,
                        record.CustomerReference,
                        "primary_identity_missing",
                        cancellationToken);
                    await _repository.TouchPrimaryIdentityAttemptAsync(
                        record.Id,
                        koxoTriggered: true,
                        DateTime.UtcNow,
                        cancellationToken);
                    triggered = true;
                }

                return new PrimaryIdentityConvergence(
                    new PrimaryIdentityBootstrapResult(
                        false,
                        PrimaryIdentityBootstrapCodes.DirectoryNotReady,
                        record.Status),
                    triggered);
            }
        }
        else
        {
            var signup = await _repository.GetByIdAsync(record.SignupId, cancellationToken);
            if (signup is null)
            {
                return Converged(false, PrimaryIdentityBootstrapCodes.LifecycleMissing, record);
            }

            var password = plaintextPassword
                ?? await _pendingPasswords.PeekAsync(record.PortalUserId, cancellationToken);
            var creation = await EnsurePortalAdUserAsync(
                signup,
                record,
                password,
                cancellationToken);
            if (creation.error is not null)
            {
                return Converged(false, PrimaryIdentityBootstrapCodes.DirectoryCreationFailed, record);
            }

            directoryObject = creation.directoryObject!;
        }

        var invalid = PrimaryIdentityBootstrapPolicy.ValidateDirectoryObject(
            directoryObject,
            record);
        if (invalid is not null)
        {
            // Un objet porte le bon employeeNumber mais ne satisfait pas la
            // verification : l'adopter donnerait des droits reels au mauvais
            // perimetre. Arbitrage humain, aucune nouvelle tentative seule.
            await _repository.MarkPrimaryIdentityFailedAsync(
                record.Id,
                invalid,
                $"object_guid={directoryObject.ObjectGuid};dn={directoryObject.DistinguishedName}",
                cancellationToken);
            return Converged(
                false,
                invalid,
                record with { Status = PrimaryIdentityBootstrapStatuses.Failed });
        }

        if (_adConfiguration.KoxoOwnsDirectory && directoryObject.IsDisabled)
        {
            // KoXo reactive un compte present dans son CSV : on attend plutot
            // que de lier une identite inutilisable.
            return Converged(false, PrimaryIdentityBootstrapCodes.DirectoryNotReady, record);
        }

        var objectGuid = Guid.Parse(directoryObject.ObjectGuid).ToString("D");
        if (record.DirectoryObjectGuid is not null
            && !string.Equals(record.DirectoryObjectGuid, objectGuid, StringComparison.OrdinalIgnoreCase))
        {
            // L'amorcage designait deja un autre objet : basculer transfererait
            // une identite, avec ses droits reels.
            await _repository.MarkPrimaryIdentityFailedAsync(
                record.Id,
                PrimaryIdentityBootstrapCodes.Conflict,
                $"expected={record.DirectoryObjectGuid};resolved={objectGuid}",
                cancellationToken);
            return Converged(
                false,
                PrimaryIdentityBootstrapCodes.Conflict,
                record with { Status = PrimaryIdentityBootstrapStatuses.Failed });
        }

        if (!await _repository.MarkPrimaryIdentityDirectoryResolvedAsync(
                record.Id,
                objectGuid,
                DateTime.UtcNow,
                cancellationToken))
        {
            return Converged(await ReadConcurrentOutcomeAsync(record, cancellationToken));
        }

        try
        {
            await _activeDirectoryLinkRepository.UpsertPortalUserLinkAsync(
                record.CustomerReference,
                record.PortalUserId,
                actorUserId: null,
                directoryObject,
                _adConfiguration.Domain,
                "succeeded",
                DateTime.UtcNow,
                "succeeded",
                DateTime.UtcNow,
                "koxo_pending",
                cancellationToken);
        }
        catch (AmbiguousAdLinkException exception)
        {
            // Le depot refuse tout transfert d'identite d'un utilisateur portail
            // a un autre. Reessayer produirait le meme refus.
            await _repository.MarkPrimaryIdentityFailedAsync(
                record.Id,
                PrimaryIdentityBootstrapCodes.Conflict,
                exception.Message,
                cancellationToken);
            return Converged(
                false,
                PrimaryIdentityBootstrapCodes.Conflict,
                record with { Status = PrimaryIdentityBootstrapStatuses.Failed });
        }
        catch (PortalAccessDeniedException)
        {
            // L'objet est deja lie a un autre client.
            await _repository.MarkPrimaryIdentityFailedAsync(
                record.Id,
                PrimaryIdentityBootstrapCodes.CustomerMismatch,
                $"object_guid={objectGuid}",
                cancellationToken);
            return Converged(
                false,
                PrimaryIdentityBootstrapCodes.CustomerMismatch,
                record with { Status = PrimaryIdentityBootstrapStatuses.Failed });
        }

        return Converged(await FinishPrimaryIdentityFromLinkAsync(
            record with
            {
                Status = PrimaryIdentityBootstrapStatuses.DirectoryReady,
                DirectoryObjectGuid = objectGuid
            },
            objectGuid,
            cancellationToken));
    }

    /// <summary>
    /// Confirme le lien par relecture, acquitte le secret, puis conclut.
    /// </summary>
    /// <remarks>
    /// La conclusion est refusee par le depot tant que le lien de ce compte,
    /// sur cet objectGUID, n'est pas persiste : « completed » ne peut pas
    /// preceder le lien.
    /// </remarks>
    private async Task<PrimaryIdentityBootstrapResult> FinishPrimaryIdentityFromLinkAsync(
        PrimaryIdentityBootstrapRecord record,
        string linkObjectGuid,
        CancellationToken cancellationToken)
    {
        var objectGuid = Guid.TryParse(linkObjectGuid, out var parsed)
            ? parsed.ToString("D")
            : linkObjectGuid;
        if (record.DirectoryObjectGuid is null)
        {
            if (!await _repository.MarkPrimaryIdentityDirectoryResolvedAsync(
                    record.Id,
                    objectGuid,
                    DateTime.UtcNow,
                    cancellationToken))
            {
                return await ReadConcurrentOutcomeAsync(record, cancellationToken);
            }
        }
        else if (!string.Equals(record.DirectoryObjectGuid, objectGuid, StringComparison.OrdinalIgnoreCase))
        {
            await _repository.MarkPrimaryIdentityFailedAsync(
                record.Id,
                PrimaryIdentityBootstrapCodes.Conflict,
                $"expected={record.DirectoryObjectGuid};linked={objectGuid}",
                cancellationToken);
            return new PrimaryIdentityBootstrapResult(
                false,
                PrimaryIdentityBootstrapCodes.Conflict,
                PrimaryIdentityBootstrapStatuses.Failed);
        }

        var confirmed = await _activeDirectoryLinkRepository
            .FindUserLinkByPortalUserIdAsync(record.PortalUserId, cancellationToken);
        if (confirmed is null
            || !string.Equals(confirmed.ObjectGuid, objectGuid, StringComparison.OrdinalIgnoreCase))
        {
            return new PrimaryIdentityBootstrapResult(
                false,
                PrimaryIdentityBootstrapCodes.LinkNotConfirmed,
                PrimaryIdentityBootstrapStatuses.DirectoryReady);
        }

        // Le lien vient d'etre relu : KoXo a cree l'identite et repris le mot
        // de passe. Seulement maintenant le secret peut disparaitre — avant la
        // conclusion, pour qu'un arret entre les deux laisse un amorcage que
        // le rejeu acquitte puis conclut.
        await _pendingPasswords.AcknowledgeAsync(record.PortalUserId, cancellationToken);
        if (!await _repository.MarkPrimaryIdentityCompletedAsync(
                record.Id,
                DateTime.UtcNow,
                cancellationToken))
        {
            return await ReadConcurrentOutcomeAsync(record, cancellationToken);
        }

        return new PrimaryIdentityBootstrapResult(
            true,
            PrimaryIdentityBootstrapCodes.Completed,
            PrimaryIdentityBootstrapStatuses.Completed);
    }

    /// <summary>
    /// Une transition refusee peut signifier qu'un appel concurrent a deja
    /// conclu : on relit plutot que de supposer un conflit.
    /// </summary>
    private async Task<PrimaryIdentityBootstrapResult> ReadConcurrentOutcomeAsync(
        PrimaryIdentityBootstrapRecord record,
        CancellationToken cancellationToken)
    {
        var current = await _repository.GetPrimaryIdentityBootstrapAsync(
            record.PortalUserId,
            cancellationToken);
        return current?.Status == PrimaryIdentityBootstrapStatuses.Completed
            ? new PrimaryIdentityBootstrapResult(
                true,
                PrimaryIdentityBootstrapCodes.Completed,
                PrimaryIdentityBootstrapStatuses.Completed)
            : new PrimaryIdentityBootstrapResult(
                false,
                current?.Status == PrimaryIdentityBootstrapStatuses.Failed
                    ? PrimaryIdentityBootstrapCodes.Failed
                    : PrimaryIdentityBootstrapCodes.Conflict,
                current?.Status);
    }

    private static PrimaryIdentityConvergence Converged(
        bool succeeded,
        string code,
        PrimaryIdentityBootstrapRecord record)
        => new(new PrimaryIdentityBootstrapResult(succeeded, code, record.Status), false);

    private static PrimaryIdentityConvergence Converged(PrimaryIdentityBootstrapResult result)
        => new(result, false);

    /// <summary>
    /// Cree l'objet annuaire simule, en mode mock uniquement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Jamais atteint quand KoXo est maitre de l'annuaire. Le nom de compte est
    /// derive du nom <b>et</b> de l'identifiant KoXo : il est donc stable pour
    /// un compte donne, et un rejeu apres une creation reussie retrouve le meme
    /// objet au lieu d'en creer un second sous un suffixe.
    /// </para>
    /// </remarks>
    private async Task<(AdDirectoryObjectSummary? directoryObject, SignupOperationResult? error)>
        EnsurePortalAdUserAsync(
            SignupPendingRecord signup,
            PrimaryIdentityBootstrapRecord bootstrap,
            string? password,
            CancellationToken cancellationToken)
    {
        var samAccountName = BuildPrimarySamAccountName(
            signup.PrimaryUser.GivenName,
            signup.PrimaryUser.Surname,
            signup.PrimaryUser.Email ?? signup.Email,
            bootstrap.KoxoUniqueIdentifier);
        var userPrincipalName = _adConfiguration.Domain is null
            ? null
            : $"{samAccountName}@{_adConfiguration.Domain}";
        var createRequest = new CreateAdUserRequest(
            samAccountName,
            signup.PrimaryUser.DisplayName ?? signup.ContactName,
            signup.PrimaryUser.GivenName,
            signup.PrimaryUser.Surname,
            userPrincipalName,
            $"{signup.CompanyName} ({bootstrap.CustomerReference})",
            signup.PrimaryUser.PersonalTitle,
            signup.PrimaryUser.Initials,
            signup.PrimaryUser.Email ?? signup.Email,
            signup.PrimaryUser.Phone ?? signup.Phone ?? signup.Customer.Phone,
            signup.Customer.DisplayName ?? signup.CompanyName,
            bootstrap.KoxoUniqueIdentifier);
        var createResult = await _activeDirectoryService.CreateUserAsync(
            bootstrap.CustomerReference,
            createRequest,
            cancellationToken);

        AdDirectoryObjectSummary directoryObject;
        if (createResult.StatusCode < 400 && createResult.Value is not null)
        {
            directoryObject = createResult.Value;
        }
        else if (string.Equals(
                     createResult.Code,
                     "AD_OBJECT_ALREADY_EXISTS",
                     StringComparison.Ordinal))
        {
            // Rejeu : le nom est deterministe, l'objet est donc celui-ci ou
            // aucun. On le relit dans le perimetre de ce client uniquement.
            var searchResult = await _activeDirectoryService.SearchUsersAsync(
                samAccountName,
                bootstrap.CustomerReference,
                cancellationToken);
            var existingUser = searchResult.StatusCode >= 400
                ? null
                : searchResult.Value?.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.SamAccountName,
                        samAccountName,
                        StringComparison.OrdinalIgnoreCase));
            if (existingUser is null)
            {
                return (
                    null,
                    new SignupOperationResult(
                        false,
                        "AD_OBJECT_ALREADY_EXISTS",
                        "Un compte Active Directory existe deja avec cette identite technique."));
            }

            directoryObject = existingUser;
        }
        else
        {
            return (
                null,
                MapAdProvisioningFailure(
                    createResult,
                    "Le compte Active Directory n'a pas pu etre cree."));
        }

        if (password is null)
        {
            return (directoryObject, null);
        }

        // Mode Mock : aucun KoXo derriere, c'est bien a l'application
        // d'appliquer le mot de passe, sans quoi le compte simule resterait
        // desactive et sans mot de passe.
        var passwordResult = await _activeDirectoryService.SetUserPasswordAsync(
            bootstrap.CustomerReference,
            directoryObject.SamAccountName,
            password,
            cancellationToken);
        return passwordResult.StatusCode >= 400 || passwordResult.Value is null
            ? (directoryObject, null)
            : (passwordResult.Value, null);
    }

    // ------------------------------------------------------------------
    // Verification e-mail et reprise
    // ------------------------------------------------------------------

    /// <summary>
    /// L'adresse d'un compte self-service vient d'etre prouvee : l'amorcage
    /// devient eligible a l'export, et la synchronisation est relancee.
    /// </summary>
    /// <remarks>
    /// Idempotent : la verification ne passe qu'une fois par
    /// <c>email_pending</c>. Sans secret disponible (expire, jamais scelle),
    /// la seule reprise legitime est un nouveau mot de passe choisi par le
    /// titulaire : un lien de definition lui est envoye.
    /// </remarks>
    private async Task AdvancePrimaryIdentityAfterEmailVerificationAsync(
        string signupId,
        string portalUserId,
        CancellationToken cancellationToken)
    {
        if (!_adConfiguration.WritesEnabled)
        {
            return;
        }

        try
        {
            var bootstrap = await _repository.GetPrimaryIdentityBootstrapAsync(
                portalUserId,
                cancellationToken);
            if (bootstrap is not null
                && PrimaryIdentityBootstrapStatuses.IsBootstrapping(bootstrap.Status)
                && bootstrap.SecretAvailable)
            {
                await SendKoxoSyncTriggerAsync(
                    signupId,
                    portalUserId,
                    bootstrap.CustomerReference,
                    "primary_identity_email_verified",
                    cancellationToken);
                await _repository.TouchPrimaryIdentityAttemptAsync(
                    bootstrap.Id,
                    koxoTriggered: true,
                    DateTime.UtcNow,
                    cancellationToken);
                await TryConvergePrimaryIdentityAsync(
                    portalUserId,
                    plaintextPassword: null,
                    allowKoxoTrigger: false,
                    cancellationToken);
                return;
            }

            if (bootstrap is null
                || bootstrap.Status == PrimaryIdentityBootstrapStatuses.AwaitingPassword
                || bootstrap.Status == PrimaryIdentityBootstrapStatuses.KoxoPending)
            {
                var recovery = await RequestPrimaryIdentityRecoveryAsync(
                    signupId,
                    Guid.NewGuid().ToString("D"),
                    cancellationToken);
                _logger.LogInformation(
                    "Primary identity recovery after email verification for portal_user_id {PortalUserId}: {Code}",
                    portalUserId,
                    recovery.Code);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // La verification est acquise ; l'amorcage sera repris par le
            // worker ou par une reprise explicite.
            _logger.LogWarning(
                exception,
                "Primary identity advance after email verification failed for portal_user_id {PortalUserId}.",
                portalUserId);
        }
    }

    /// <summary>
    /// Reprise d'un compte principal sans identite AD dont le mot de passe
    /// clair est perdu (comptes crees avant l'amorcage, secret expire).
    /// </summary>
    /// <remarks>
    /// Aucune tentative de retrouver l'ancien mot de passe : un lien de
    /// definition est envoye a l'adresse du compte, et le nouveau mot de passe
    /// entre dans l'amorcage par le set-password existant. Rejouable : chaque
    /// demande rend le lien precedent inutilisable, et une identite deja liee
    /// ou en cours de liaison est refusee.
    /// </remarks>
    public async Task<SignupOperationResult> RequestPrimaryIdentityRecoveryAsync(
        string signupId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!_adConfiguration.WritesEnabled)
        {
            return new SignupOperationResult(
                false,
                PrimaryIdentityBootstrapCodes.DirectoryDisabled,
                "Les ecritures Active Directory sont desactivees : la reprise est sans objet.");
        }

        var token = GenerateToken();
        var runtime = await _settings.GetSignupConfigurationAsync(
            _configuration,
            cancellationToken);
        var now = DateTime.UtcNow;
        var target = await _repository.RequestPrimaryIdentityRecoveryAsync(
            signupId,
            HashToken(token),
            now.AddHours(runtime.PasswordSetupTokenTtlHours),
            now,
            cancellationToken);
        if (!target.Succeeded)
        {
            return new SignupOperationResult(
                false,
                target.Code,
                DescribePrimaryIdentityRecoveryRefusal(target.Code));
        }

        var delivery = await _emailDispatch.SendAccountApprovedAsync(
            target.Email!,
            target.ContactName!,
            BuildUrl("/set-password", token),
            correlationId,
            cancellationToken);
        if (!delivery.Succeeded)
        {
            _logger.LogWarning(
                "Primary identity recovery email not delivered ({Code}) correlation_id {CorrelationId}",
                delivery.Code,
                correlationId);
            return new SignupOperationResult(
                false,
                delivery.Code,
                "Le lien de reprise a bien ete genere, mais l'e-mail n'a pas pu etre envoye.");
        }

        return new SignupOperationResult(
            true,
            PrimaryIdentityRecoveryCodes.Issued,
            "Un lien de definition du mot de passe a ete envoye pour finaliser l'identite du compte.");
    }

    private static string DescribePrimaryIdentityRecoveryRefusal(string code)
        => code switch
        {
            PrimaryIdentityRecoveryCodes.SignupNotFound => "Demande introuvable.",
            PrimaryIdentityRecoveryCodes.EmailVerificationRequired =>
                "L'adresse e-mail n'est pas encore verifiee : la verification relancera l'amorcage.",
            PrimaryIdentityRecoveryCodes.AlreadyLinked =>
                "Ce compte possede deja son identite Active Directory.",
            PrimaryIdentityRecoveryCodes.InProgress =>
                "L'identite de ce compte est deja en cours de creation.",
            PrimaryIdentityRecoveryCodes.Conflict =>
                "L'identite de ce compte est en conflit : un arbitrage est necessaire.",
            _ => "Ce compte ne peut pas etre repris dans son etat actuel."
        };

    private static SignupOperationResult MapAdProvisioningFailure<T>(
        AdServiceResult<T> result,
        string fallbackMessage)
        => new(
            false,
            result.Code,
            string.IsNullOrWhiteSpace(result.Message)
                ? fallbackMessage
                : result.Message);

    private static SignupOperationResult Accepted()
        => new(
            true,
            "SIGNUP_ACCEPTED",
            "Demande enregistree. Verifiez votre boite mail pour confirmer votre adresse.");

    private static SignupOperationResult TokenInvalid()
        => new(
            false,
            "TOKEN_INVALID",
            "Ce lien est invalide ou a deja ete utilise.");

    private string BuildUrl(string path, string token, string? continuationPath = null)
    {
        var baseUrl = _emailConfiguration.PortalPublicUrl;
        var prefix = string.IsNullOrWhiteSpace(baseUrl)
            ? string.Empty
            : baseUrl.TrimEnd('/');
        var url = $"{prefix}{path}?token={Uri.EscapeDataString(token)}";
        return string.IsNullOrWhiteSpace(continuationPath)
            ? url
            : $"{url}&next={Uri.EscapeDataString(continuationPath)}";
    }

    // Le lien e-mail ne porte jamais le secret anonyme du Cart. Il contient
    // seulement une continuation relative déjà bornée par le BFF au moment de
    // l'inscription ; la page de vérification applique sa propre allowlist
    // avant d'en faire un lien visible.
    private static string? BuildSelfServiceContinuationPath(
        SignupSubmitPayload payload,
        string selfServiceFlow)
    {
        if (selfServiceFlow == "cart"
            && Guid.TryParseExact(payload.SelfServiceCartIntent?.CartId, "D", out var cartId))
        {
            return $"/souscription?cart={cartId:D}";
        }

        if (selfServiceFlow == "vps"
            && IsCatalogCode(payload.SelfServiceVpsIntent?.ServiceCode)
            && IsCatalogCode(payload.SelfServiceVpsIntent?.TierCode))
        {
            return "/services/vps/choisir?serviceCode="
                + Uri.EscapeDataString(payload.SelfServiceVpsIntent!.ServiceCode!)
                + "&tierCode="
                + Uri.EscapeDataString(payload.SelfServiceVpsIntent.TierCode!);
        }

        return null;
    }

    private static bool IsCatalogCode(string? value) => value is { Length: > 0 and <= 80 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    // Delegue a PortalSetupToken : le cycle de vie des utilisateurs
    // additionnels Billing V2 emet le meme type de lien, et deux generateurs
    // divergents produiraient deux niveaux de securite pour un meme usage.
    private static string GenerateToken() => PortalSetupToken.Generate();

    private static string HashToken(string token) => PortalSetupToken.Hash(token);

    private static string GenerateCustomerReference()
        => CustomerReferenceGenerator.Generate();

    private async Task<NormalizedSignupSubmission?> NormalizeSubmissionAsync(
        SignupSubmitPayload payload,
        CancellationToken cancellationToken)
    {
        var customerType = NormalizeCustomerType(
            payload.Customer?.CustomerType,
            payload.CompanyName);
        var companyName = NormalizeOptional(
            payload.Customer?.DisplayName ?? payload.CompanyName,
            MaxNameLength);
        var message = NormalizeOptional(payload.Message, MaxMessageLength);

        var givenName = NormalizeOptional(
            payload.PrimaryUser?.GivenName,
            MaxShortNameLength);
        var surname = NormalizeOptional(
            payload.PrimaryUser?.Surname,
            MaxShortNameLength);
        if (givenName is null || surname is null)
        {
            var split = SplitLegacyName(
                payload.PrimaryUser?.DisplayName ?? payload.ContactName);
            givenName ??= split.givenName;
            surname ??= split.surname;
        }

        var displayName = NormalizeOptional(
            payload.PrimaryUser?.DisplayName
            ?? BuildDisplayName(givenName, surname)
            ?? payload.ContactName,
            MaxNameLength);
        var email = NormalizeEmail(
            payload.PrimaryUser?.Email
            ?? payload.Customer?.BillingEmail
            ?? payload.Email);
        var customerEmail = NormalizeEmail(
            payload.Customer?.BillingEmail
            ?? payload.PrimaryUser?.Email
            ?? payload.Email);
        var customerPhone = NormalizeOptional(
            payload.Customer?.Phone,
            40);
        var primaryPhone = NormalizeOptional(
            payload.PrimaryUser?.Phone ?? payload.Phone,
            40);
        var addressLine1 = NormalizeOptional(
            payload.Customer?.AddressLine1,
            255);
        var addressLine2 = NormalizeOptional(
            payload.Customer?.AddressLine2,
            255);
        var postalCode = NormalizeOptional(
            payload.Customer?.PostalCode,
            MaxPostalCodeLength);
        var city = NormalizeOptional(
            payload.Customer?.City,
            160);
        var country = NormalizeOptional(
            payload.Customer?.Country,
            MaxCountryLength);
        var initials = NormalizeInitials(
            payload.PrimaryUser?.Initials,
            givenName,
            surname);
        var personalTitle = NormalizePersonalTitle(
            payload.PrimaryUser?.PersonalTitle);
        var birthDate = NormalizeBirthDate(payload.PrimaryUser?.BirthDate);

        if (companyName is null
            || customerType is null
            || displayName is null
            || email is null
            || customerEmail is null
            || addressLine1 is null
            || postalCode is null
            || city is null
            || country is null
            || personalTitle is null
            || givenName is null
            || surname is null
            || birthDate is null)
        {
            return null;
        }

        var customer = new SignupCustomerData(
            customerType,
            companyName,
            customerEmail,
            customerPhone ?? primaryPhone,
            addressLine1,
            addressLine2,
            postalCode,
            city,
            country);
        var primaryUser = new SignupUserData(
            personalTitle,
            givenName,
            surname,
            birthDate,
            initials,
            displayName,
            email,
            primaryPhone ?? customerPhone,
            payload.PrimaryUser?.IsPrimaryContact ?? true);

        // Une demande d'inscription peut arriver sans selection commerciale :
        // le formulaire de contact ne configure rien. Quand une selection est
        // presente, elle est necessairement Billing V2 — il n'existe plus
        // d'autre catalogue.
        BillingV2PublicSelection? billingV2Selection = null;
        if (payload.BillingV2Selection is not null)
        {
            billingV2Selection = payload.BillingV2Selection.ToSelection();
            if (!IsValidBillingV2Selection(billingV2Selection))
            {
                return null;
            }
        }


        return new NormalizedSignupSubmission(
            companyName,
            displayName,
            email,
            primaryPhone ?? customerPhone,
            message,
            customer,
            primaryUser,
            billingV2Selection);
    }

    // Cette forme restreinte reutilise les normaliseurs du domaine signup,
    // sans exiger l'identite personnelle necessaire a la creation d'un acces
    // portail. Elle reste donc sans effet email, mot de passe, AD ou KoXo.
    private static SignupCustomerData? NormalizeManualCustomer(
        AdminCustomerCreatePayload payload)
    {
        var customerType = NormalizeCustomerType(payload.CustomerType, null);
        var displayName = NormalizeOptional(payload.DisplayName, MaxNameLength);
        var billingEmail = NormalizeEmail(payload.BillingEmail);
        var phone = NormalizeOptional(payload.Phone, 40);
        var addressLine1 = NormalizeOptional(payload.AddressLine1, 255);
        var addressLine2 = NormalizeOptional(payload.AddressLine2, 255);
        var postalCode = NormalizeOptional(payload.PostalCode, MaxPostalCodeLength);
        var city = NormalizeOptional(payload.City, 160);
        var country = NormalizeOptional(payload.Country, MaxCountryLength);

        return customerType is null
            || displayName is null
            || billingEmail is null
            || addressLine1 is null
            || postalCode is null
            || city is null
            || country is null
            ? null
            : new SignupCustomerData(
                customerType,
                displayName,
                billingEmail,
                phone,
                addressLine1,
                addressLine2,
                postalCode,
                city,
                country);
    }

    private static string? NormalizeCustomerType(
        string? value,
        string? legacyCompanyName)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "individual" or "professional" or "association" => normalized,
            _ when !string.IsNullOrWhiteSpace(legacyCompanyName) => "professional",
            _ => null
        };
    }

    private static string? NormalizeEmail(string? value)
    {
        var email = value?.Trim().ToLowerInvariant();
        return email is null
            || email.Length is < 3 or > MaxEmailLength
            || !IsPlausibleEmail(email)
            ? null
            : email;
    }

    private static string? NormalizeInitials(
        string? value,
        string? givenName,
        string? surname)
    {
        var direct = NormalizeOptional(value, MaxInitialsLength);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct.ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(givenName)
            || string.IsNullOrWhiteSpace(surname))
        {
            return null;
        }

        return $"{char.ToUpperInvariant(givenName[0])}{char.ToUpperInvariant(surname[0])}";
    }

    private static string? NormalizePersonalTitle(string? value)
    {
        var normalized = NormalizeOptional(value, MaxCustomerTypeLength)
            ?.ToLowerInvariant();
        return normalized is not null && AllowedPersonalTitles.Contains(normalized)
            ? normalized
            : null;
    }

    private static string? NormalizeBirthDate(string? value)
    {
        var normalized = NormalizeOptional(value, 10);
        if (normalized is null)
        {
            return null;
        }

        return DateOnly.TryParseExact(
                normalized,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var birthDate)
            ? birthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    private static (string? givenName, string? surname) SplitLegacyName(
        string? displayName)
    {
        var normalized = NormalizeOptional(displayName, MaxNameLength);
        if (normalized is null)
        {
            return (null, null);
        }

        var parts = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return (null, null);
        }

        if (parts.Length == 1)
        {
            return (parts[0], parts[0]);
        }

        return (
            NormalizeOptional(parts[0], MaxShortNameLength),
            NormalizeOptional(string.Join(' ', parts.Skip(1)), MaxShortNameLength));
    }

    private static string? BuildDisplayName(
        string? givenName,
        string? surname)
    {
        if (string.IsNullOrWhiteSpace(givenName)
            || string.IsNullOrWhiteSpace(surname))
        {
            return null;
        }

        return $"{givenName.Trim()} {surname.Trim()}";
    }

    private static string BuildSamAccountNameBase(
        string? givenName,
        string? surname,
        string fallbackEmail)
    {
        var normalizedGivenName = NormalizeSamSegment(givenName);
        var normalizedSurname = NormalizeSamSegment(surname);
        if (!string.IsNullOrWhiteSpace(normalizedGivenName)
            && !string.IsNullOrWhiteSpace(normalizedSurname))
        {
            var initial = normalizedGivenName[0].ToString();
            var surnamePart = normalizedSurname.Length <= 6
                ? normalizedSurname
                : normalizedSurname[..6];
            return $"{initial}{surnamePart}".ToLowerInvariant();
        }

        var localPart = fallbackEmail.Split('@', 2)[0];
        var normalizedLocalPart = NormalizeSamSegment(localPart);
        if (!string.IsNullOrWhiteSpace(normalizedLocalPart))
        {
            return normalizedLocalPart.Length <= 12
                ? normalizedLocalPart.ToLowerInvariant()
                : normalizedLocalPart[..12].ToLowerInvariant();
        }

        return "portaluser";
    }

    /// <summary>
    /// Nom de compte du mode mock : base lisible derivee du nom, suffixee des
    /// chiffres de l'identifiant KoXo. Unique et stable pour un compte donne,
    /// et borne a 20 caracteres comme un sAMAccountName reel.
    /// </summary>
    private static string BuildPrimarySamAccountName(
        string? givenName,
        string? surname,
        string fallbackEmail,
        string koxoUniqueIdentifier)
    {
        var digits = new string(koxoUniqueIdentifier.Where(char.IsAsciiDigit).ToArray());
        var baseSam = BuildSamAccountNameBase(givenName, surname, fallbackEmail);
        var maxBaseLength = Math.Max(1, 20 - digits.Length);
        return (baseSam.Length <= maxBaseLength ? baseSam : baseSam[..maxBaseLength])
            + digits;
    }

    private static string NormalizeSamSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? NormalizeStatusFilter(string? statusFilter)
    {
        var normalized = statusFilter?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "email_pending" or "email_verified" or "approved"
                or "rejected" or "expired" => normalized,
            _ => null
        };
    }

    private static bool IsPlausibleEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex != email.LastIndexOf('@'))
        {
            return false;
        }

        var domain = email[(atIndex + 1)..];
        return domain.Contains('.')
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !email.Contains(' ');
    }

    private static SignupAdminSummary ToSummary(SignupPendingRecord record)
        => new(
            record.Id,
            record.Status,
            record.CompanyName,
            record.ContactName,
            record.Email,
            record.Status is "email_verified" or "approved",
            ToIso(record.CreatedAtUtc),
            ToNullableIso(record.ApprovedAtUtc),
            ToNullableIso(record.RejectedAtUtc));

    private static SignupAdminDetail ToDetail(SignupPendingRecord record)
        => new(
            record.Id,
            record.Status,
            record.CompanyName,
            record.ContactName,
            record.Email,
            record.Phone,
            record.Message,
            record.BillingV2Selection,
            record.SourceAddress,
            record.RejectedReason,
            ToIso(record.CreatedAtUtc),
            ToIso(record.UpdatedAtUtc),
            ToNullableIso(record.ApprovedAtUtc),
            ToNullableIso(record.RejectedAtUtc),
            record.Customer,
            record.PrimaryUser,
            record.ApprovedUserId is null
                ? null
                : new SignupAdminAccountAccess(
                    record.ApprovedCustomerReference,
                    record.ApprovedUserHasPassword,
                    ToNullableIso(record.PasswordSetupExpiresAtUtc),
                    record.AdProvisioningStatus,
                    record.LastPasswordSyncStatus,
                    record.KoxoExportStatus,
                    record.ApprovedUserSamAccountName,
                    record.ApprovedUserPrincipalName));

    // Deux formes sont legitimes : une formule (`PresetCode` + palier de
    // stockage personnel) ou une selection directe de composants sans formule.
    // Exiger un preset dans les deux cas obligerait a en fabriquer un faux pour
    // un simple achat ponctuel.
    private static bool IsValidBillingV2Selection(BillingV2PublicSelection selection)
    {
        if (selection.PaymentMode != BillingV2PaymentModes.Monthly
            && selection.PaymentMode != BillingV2PaymentModes.Upfront)
        {
            return false;
        }

        if (selection.AdditionalUsers is < 0 or > 10)
        {
            return false;
        }

        if (selection.Components is { Count: > 0 })
        {
            return selection.Components.All(component =>
                !string.IsNullOrWhiteSpace(component.ServiceCode)
                && component.Quantity > 0);
        }

        return !string.IsNullOrWhiteSpace(selection.PresetCode)
            && !string.IsNullOrWhiteSpace(selection.StoragePersonalTierCode);
    }


    private static bool IsAwaitingPasswordSetup(SignupPendingRecord record)
        => string.Equals(record.Status, "approved", StringComparison.Ordinal)
            && record.ApprovedUserId is not null
            && !record.ApprovedUserHasPassword;

    private static string ToIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

    private static string? ToNullableIso(DateTime? value)
        => value is null ? null : ToIso(value.Value);

    private sealed record NormalizedSignupSubmission(
        string CompanyName,
        string ContactName,
        string Email,
        string? Phone,
        string? Message,
        SignupCustomerData Customer,
        SignupUserData PrimaryUser,
        BillingV2PublicSelection? BillingV2Selection);
}
