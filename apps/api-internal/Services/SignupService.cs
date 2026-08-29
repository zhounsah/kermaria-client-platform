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
    string Message);

public interface ISignupService
{
    bool IsPersistent { get; }

    Task<SignupOperationResult> SubmitAsync(
        SignupSubmitPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> VerifyEmailAsync(
        string? token,
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

    Task<SignupOperationResult> SetPasswordAsync(
        string? token,
        string? password,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> ValidateSetPasswordTokenAsync(
        string? token,
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
    private static readonly HashSet<string> AllowedPersonalTitles =
        new(StringComparer.Ordinal)
        {
            "madame",
            "monsieur"
        };

    private readonly ISignupRepository _repository;
    private readonly IEmailDispatchService _emailDispatch;
    private readonly IPortalPasswordService _passwordService;
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
        if (target is null
            || !string.Equals(
                target.Status,
                "email_pending",
                StringComparison.Ordinal))
        {
            return TokenInvalid();
        }

        if (target.VerificationTokenExpiresAtUtc is { } expiry
            && expiry < DateTime.UtcNow)
        {
            return new SignupOperationResult(
                false,
                "TOKEN_EXPIRED",
                "Ce lien de verification a expire. Renouvelez votre demande.");
        }

        await _repository.MarkEmailVerifiedAsync(target.Id, cancellationToken);
        return new SignupOperationResult(
            true,
            "EMAIL_VERIFIED",
            "Adresse e-mail confirmee. Votre demande est en attente de validation.");
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

        var (adError, koxoSecret) = await ProvisionActiveDirectoryAsync(
            record,
            password,
            cancellationToken);
        if (adError is not null)
        {
            return adError;
        }

        var passwordHash = _passwordService.HashPassword(
            record.ApprovedUserId,
            password);

        // Une seule unite de travail : condensat portail, retrait du jeton et
        // secret destine a KoXo. Un echec ne doit laisser aucun secret derriere
        // lui — le jeton reste alors utilisable et la personne peut recommencer.
        try
        {
            await _repository.SetPasswordAsync(
                record.Id,
                record.ApprovedUserId,
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

    /// <remarks>
    /// Rend le secret <b>scelle</b> destine a KoXo, sans l'ecrire : son depot a
    /// lieu dans la transaction qui pose le condensat du portail. Publie ici,
    /// il survivait a l'echec de cette transaction, et KoXo appliquait alors a
    /// l'annuaire un mot de passe que le portail ne connaissait pas.
    /// </remarks>
    private async Task<(SignupOperationResult? error, PortalPasswordSecret? secret)>
        ProvisionActiveDirectoryAsync(
        SignupPendingRecord record,
        string password,
        CancellationToken cancellationToken)
    {
        if (!_adConfiguration.WritesEnabled)
        {
            return (null, null);
        }

        if (!_adConfiguration.ConfigurationValid)
        {
            return (new SignupOperationResult(
                false,
                "AD_CONFIGURATION_INVALID",
                "La configuration Active Directory est incomplete."), null);
        }

        if (record.ApprovedUserId is null
            || string.IsNullOrWhiteSpace(record.ApprovedCustomerReference))
        {
            return (new SignupOperationResult(
                false,
                "INVALID_STATE",
                "Le compte approuve ne peut pas etre relie a Active Directory."), null);
        }

        var now = DateTime.UtcNow;
        var existingLink =
            await _activeDirectoryLinkRepository.FindUserLinkByPortalUserIdAsync(
                record.ApprovedUserId,
                cancellationToken);

        if (existingLink is not null)
        {
            if (_adConfiguration.KoxoOwnsDirectory)
            {
                // Fail-closed avant tout point de non-retour : sans magasin
                // exploitable, le secret n'atteindrait jamais l'annuaire.
                var sealed_ = _pendingPasswords.IsOperational
                    ? _pendingPasswords.Seal(record.ApprovedUserId, password)
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
                record.ApprovedUserId,
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

        // Quand KoXo est reellement en place, c'est LUI qui cree l'identite :
        // l'application se contente de l'adopter via son employeeNumber.
        // Creer nous-memes produirait un DOUBLON, le sAMAccountName derive ici
        // (initiale + 6 lettres du nom) differant de celui derive par KoXo
        // (prenom.nom) — le mot de passe du client atterrirait alors sur le
        // compte dont les services ne se servent pas.
        //
        // En mode Mock il n'y a pas de KoXo derriere, donc on continue de creer :
        // sans cela plus personne ne creerait l'identite et le parcours de
        // definition du mot de passe resterait bloque.
        AdDirectoryObjectSummary? adUserObject;
        if (_adConfiguration.KoxoOwnsDirectory)
        {
            var adoption = await AdoptKoxoIdentityAsync(record, cancellationToken);
            if (adoption.error is not null)
            {
                return (adoption.error, null);
            }

            adUserObject = adoption.directoryObject;
        }
        else
        {
            var adUser = await EnsurePortalAdUserAsync(
                record,
                cancellationToken);
            if (adUser.error is not null)
            {
                return (adUser.error, null);
            }

            adUserObject = adUser.directoryObject;
        }

        PortalPasswordSecret? koxoSecret = null;
        if (_adConfiguration.KoxoOwnsDirectory)
        {
            // On n'ecrit PAS le mot de passe par LDAP. Avec ForcePasswords=1,
            // KoXo reecrit le mot de passe de l'annuaire a chaque
            // synchronisation depuis la colonne 14 du CSV : une ecriture LDAP
            // serait ecrasee au passage suivant et le client perdrait
            // NextCloud, RDS et le VPN sans aucune erreur visible. On scelle
            // donc le mot de passe pour l'export ; son depot a lieu dans la
            // transaction qui pose le condensat du portail, et le declenchement
            // qui suit dans ApplyPasswordAsync le fait appliquer par KoXo.
            koxoSecret = _pendingPasswords.IsOperational
                ? _pendingPasswords.Seal(record.ApprovedUserId, password)
                : null;
            if (koxoSecret is null)
            {
                return (new SignupOperationResult(
                    false,
                    "KOXO_PASSWORD_HANDOFF_UNAVAILABLE",
                    "Le mot de passe ne peut pas etre transmis a KoXo pour le moment."), null);
            }
        }
        else
        {
            // Mode Mock : aucun KoXo derriere, c'est bien a l'application
            // d'appliquer le mot de passe, sans quoi le compte simule resterait
            // desactive et sans mot de passe.
            var passwordResult = await _activeDirectoryService.SetUserPasswordAsync(
                record.ApprovedCustomerReference,
                adUserObject!.SamAccountName,
                password,
                cancellationToken);
            if (passwordResult.StatusCode >= 400 || passwordResult.Value is null)
            {
                return (MapAdProvisioningFailure(
                    passwordResult,
                    "Le mot de passe Active Directory n'a pas pu etre applique."), null);
            }

            adUserObject = passwordResult.Value;
        }

        await _activeDirectoryLinkRepository.UpsertPortalUserLinkAsync(
            record.ApprovedCustomerReference,
            record.ApprovedUserId,
            actorUserId: null,
            adUserObject!,
            _adConfiguration.Domain,
            "succeeded",
            now,
            "succeeded",
            now,
            "koxo_pending",
            cancellationToken);

        return (null, koxoSecret);
    }

    /// <summary>
    /// Retrouve l'identite creee par KoXo au lieu d'en creer une.
    /// </summary>
    /// <remarks>
    /// L'absence n'est pas une erreur mais un etat d'attente : la
    /// synchronisation KoXo est asynchrone. On la relance et on invite a
    /// reessayer, plutot que de creer un doublon sous un sAMAccountName que
    /// KoXo n'utiliserait pas.
    /// </remarks>
    private async Task<(AdDirectoryObjectSummary? directoryObject, SignupOperationResult? error)>
        AdoptKoxoIdentityAsync(
            SignupPendingRecord record,
            CancellationToken cancellationToken)
    {
        var koxoIdentifier = await _repository.GetKoxoUniqueIdentifierAsync(
            record.ApprovedUserId!,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(koxoIdentifier))
        {
            return (
                null,
                new SignupOperationResult(
                    false,
                    "INVALID_STATE",
                    "Le compte approuve n'a pas d'identifiant de synchronisation."));
        }

        var directoryObject = await _adGroupProvisioner
            .ResolveUserByEmployeeNumberAsync(koxoIdentifier, cancellationToken);
        if (directoryObject is not null)
        {
            return (directoryObject, null);
        }

        await SendKoxoSyncTriggerAsync(record, "identity_missing", cancellationToken);
        return (
            null,
            new SignupOperationResult(
                false,
                "AD_IDENTITY_NOT_READY",
                "Votre espace est en cours de creation. Merci de reessayer dans une minute."));
    }

    private async Task<(AdDirectoryObjectSummary? directoryObject, SignupOperationResult? error)>
        EnsurePortalAdUserAsync(
            SignupPendingRecord record,
            CancellationToken cancellationToken)
    {
        var samResolution = await ResolveAvailableSamAccountNameAsync(
            record,
            cancellationToken);
        if (samResolution.error is not null)
        {
            return (null, samResolution.error);
        }

        var userPrincipalName = _adConfiguration.Domain is null
            ? null
            : $"{samResolution.samAccountName}@{_adConfiguration.Domain}";
        var createRequest = new CreateAdUserRequest(
            samResolution.samAccountName,
            record.PrimaryUser.DisplayName ?? record.ContactName,
            record.PrimaryUser.GivenName,
            record.PrimaryUser.Surname,
            userPrincipalName,
            $"{record.CompanyName} ({record.ApprovedCustomerReference})",
            record.PrimaryUser.PersonalTitle,
            record.PrimaryUser.Initials,
            record.PrimaryUser.Email ?? record.Email,
            record.PrimaryUser.Phone ?? record.Phone ?? record.Customer.Phone,
            record.Customer.DisplayName ?? record.CompanyName,
            record.ApprovedCustomerReference);
        var createResult = await _activeDirectoryService.CreateUserAsync(
            record.ApprovedCustomerReference!,
            createRequest,
            cancellationToken);

        if (createResult.StatusCode < 400 && createResult.Value is not null)
        {
            return (createResult.Value, null);
        }

        if (!string.Equals(
                createResult.Code,
                "AD_OBJECT_ALREADY_EXISTS",
                StringComparison.Ordinal))
        {
            return (
                null,
                MapAdProvisioningFailure(
                    createResult,
                    "Le compte Active Directory n'a pas pu etre cree."));
        }

        var searchResult = await _activeDirectoryService.SearchUsersAsync(
            samResolution.samAccountName,
            record.ApprovedCustomerReference,
            cancellationToken);
        if (searchResult.StatusCode >= 400)
        {
            return (
                null,
                MapAdProvisioningFailure(
                    searchResult,
                    "Le compte Active Directory existe deja mais n'a pas pu etre retrouve."));
        }

        var existingUser = searchResult.Value?.FirstOrDefault(candidate =>
            string.Equals(
                candidate.SamAccountName,
                samResolution.samAccountName,
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

        return (existingUser, null);
    }

    private async Task<(string? samAccountName, SignupOperationResult? error)>
        ResolveAvailableSamAccountNameAsync(
            SignupPendingRecord record,
            CancellationToken cancellationToken)
    {
        var baseSam = BuildSamAccountNameBase(
            record.PrimaryUser.GivenName,
            record.PrimaryUser.Surname,
            record.PrimaryUser.Email ?? record.Email);

        for (var suffix = 0; suffix < 100; suffix++)
        {
            var candidate = BuildSamCandidate(baseSam, suffix);
            var searchResult = await _activeDirectoryService.SearchUsersAsync(
                candidate,
                customerReference: null,
                cancellationToken);
            if (searchResult.StatusCode >= 400)
            {
                return (
                    null,
                    MapAdProvisioningFailure(
                        searchResult,
                        "La disponibilite de l'identite Active Directory n'a pas pu etre verifiee."));
            }

            var exists = searchResult.Value?.Any(user =>
                string.Equals(
                    user.SamAccountName,
                    candidate,
                    StringComparison.OrdinalIgnoreCase)) == true;
            if (!exists)
            {
                return (candidate, null);
            }
        }

        return (
            null,
            new SignupOperationResult(
                false,
                "AD_SAM_EXHAUSTED",
                "Aucun identifiant Active Directory libre n'a pu etre calcule."));
    }

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

    private string BuildUrl(string path, string token)
    {
        var baseUrl = _emailConfiguration.PortalPublicUrl;
        var prefix = string.IsNullOrWhiteSpace(baseUrl)
            ? string.Empty
            : baseUrl.TrimEnd('/');
        return $"{prefix}{path}?token={Uri.EscapeDataString(token)}";
    }

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

    private static string BuildSamCandidate(
        string baseSam,
        int suffix)
    {
        if (suffix == 0)
        {
            return baseSam;
        }

        var suffixText = suffix.ToString(CultureInfo.InvariantCulture);
        var maxBaseLength = Math.Max(1, 64 - suffixText.Length);
        var trimmedBase = baseSam.Length <= maxBaseLength
            ? baseSam
            : baseSam[..maxBaseLength];
        return $"{trimmedBase}{suffixText}";
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
