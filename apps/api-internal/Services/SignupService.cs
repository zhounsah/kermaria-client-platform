using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Email;
using Microsoft.Extensions.Logging;

namespace Kermaria.ApiInternal.Services;

public sealed record SignupOperationResult(
    bool Succeeded,
    string Code,
    string Message);

public interface ISignupService
{
    bool Enabled { get; }
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

    Task<PendingPackSelectionSummary?> GetPendingPackSelectionAsync(
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

    private readonly ISignupRepository _repository;
    private readonly IEmailDispatchService _emailDispatch;
    private readonly IPortalPasswordService _passwordService;
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly IActiveDirectoryLinkRepository _activeDirectoryLinkRepository;
    private readonly SignupRuntimeConfiguration _configuration;
    private readonly EmailRuntimeConfiguration _emailConfiguration;
    private readonly AdRuntimeConfiguration _adConfiguration;
    private readonly ILogger<SignupService> _logger;

    public SignupService(
        ISignupRepository repository,
        IEmailDispatchService emailDispatch,
        IPortalPasswordService passwordService,
        IActiveDirectoryService activeDirectoryService,
        IActiveDirectoryLinkRepository activeDirectoryLinkRepository,
        SignupRuntimeConfiguration configuration,
        EmailRuntimeConfiguration emailConfiguration,
        AdRuntimeConfiguration adConfiguration,
        ILogger<SignupService> logger)
    {
        _repository = repository;
        _emailDispatch = emailDispatch;
        _passwordService = passwordService;
        _activeDirectoryService = activeDirectoryService;
        _activeDirectoryLinkRepository = activeDirectoryLinkRepository;
        _configuration = configuration;
        _emailConfiguration = emailConfiguration;
        _adConfiguration = adConfiguration;
        _logger = logger;
    }

    public bool Enabled => _configuration.Enabled;

    public bool IsPersistent => _repository.IsPersistent;

    public async Task<SignupOperationResult> SubmitAsync(
        SignupSubmitPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!_configuration.Enabled)
        {
            return new SignupOperationResult(
                false,
                "SIGNUP_DISABLED",
                "Les inscriptions ne sont pas ouvertes.");
        }

        var normalized = NormalizeSubmission(payload);
        if (normalized is null)
        {
            return new SignupOperationResult(
                false,
                "INVALID_REQUEST",
                "Les informations transmises sont invalides.");
        }

        var windowStart = DateTime.UtcNow.AddHours(-24);
        var alreadyKnown = await _repository.HasRecentSignupOrUserAsync(
            normalized.Email,
            windowStart,
            cancellationToken);
        if (alreadyKnown)
        {
            _logger.LogInformation(
                "Signup submission ignored (duplicate or existing account) correlation_id {CorrelationId}",
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
            normalized.PackSelection,
            HashToken(token),
            DateTime.UtcNow.AddHours(_configuration.VerificationTokenTtlHours),
            NormalizeOptional(payload.SourceAddress, 45),
            NormalizeOptional(payload.UserAgent, 500));
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

    public async Task<PendingPackSelectionSummary?> GetPendingPackSelectionAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetLatestApprovedByCustomerIdAsync(
            session.CustomerId,
            cancellationToken);
        if (record?.PackSelection is null)
        {
            return null;
        }

        return new PendingPackSelectionSummary(
            record.Id,
            record.Status,
            ToNullableIso(record.ApprovedAtUtc),
            ToIso(record.CreatedAtUtc),
            record.PackSelection);
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
            DateTime.UtcNow.AddHours(_configuration.PasswordSetupTokenTtlHours));

        var result = await _repository.ApproveAsync(request, cancellationToken);
        if (result is null)
        {
            return new SignupOperationResult(
                false,
                "INVALID_STATE",
                "La demande n'a pas pu etre approuvee dans son etat actuel.");
        }

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
            DateTime.UtcNow.AddHours(_configuration.PasswordSetupTokenTtlHours),
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

        var adError = await ProvisionActiveDirectoryAsync(
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
        await _repository.SetPasswordAsync(
            record.Id,
            record.ApprovedUserId,
            passwordHash,
            cancellationToken);
        return null;
    }

    private async Task<SignupOperationResult?> ProvisionActiveDirectoryAsync(
        SignupPendingRecord record,
        string password,
        CancellationToken cancellationToken)
    {
        if (!_adConfiguration.WritesEnabled)
        {
            return null;
        }

        if (!_adConfiguration.ConfigurationValid)
        {
            return new SignupOperationResult(
                false,
                "AD_CONFIGURATION_INVALID",
                "La configuration Active Directory est incomplete.");
        }

        if (record.ApprovedUserId is null
            || string.IsNullOrWhiteSpace(record.ApprovedCustomerReference))
        {
            return new SignupOperationResult(
                false,
                "INVALID_STATE",
                "Le compte approuve ne peut pas etre relie a Active Directory.");
        }

        var now = DateTime.UtcNow;
        var existingLink =
            await _activeDirectoryLinkRepository.FindUserLinkByPortalUserIdAsync(
                record.ApprovedUserId,
                cancellationToken);

        if (existingLink is not null)
        {
            var syncResult = await _activeDirectoryService.SetUserPasswordAsync(
                existingLink.CustomerReference,
                existingLink.SamAccountName,
                password,
                cancellationToken);
            if (syncResult.StatusCode >= 400 || syncResult.Value is null)
            {
                return MapAdProvisioningFailure(
                    syncResult,
                    "Le compte Active Directory n'a pas pu etre synchronise.");
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
            return null;
        }

        var adUser = await EnsurePortalAdUserAsync(
            record,
            cancellationToken);
        if (adUser.error is not null)
        {
            return adUser.error;
        }

        var passwordResult = await _activeDirectoryService.SetUserPasswordAsync(
            record.ApprovedCustomerReference,
            adUser.directoryObject!.SamAccountName,
            password,
            cancellationToken);
        if (passwordResult.StatusCode >= 400 || passwordResult.Value is null)
        {
            return MapAdProvisioningFailure(
                passwordResult,
                "Le mot de passe Active Directory n'a pas pu etre applique.");
        }

        await _activeDirectoryLinkRepository.UpsertPortalUserLinkAsync(
            record.ApprovedCustomerReference,
            record.ApprovedUserId,
            actorUserId: null,
            passwordResult.Value,
            _adConfiguration.Domain,
            "succeeded",
            now,
            "succeeded",
            now,
            "koxo_pending",
            cancellationToken);

        return null;
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

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateCustomerReference()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        Span<char> buffer = stackalloc char[6];
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = alphabet[
                RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return $"CLI-{new string(buffer)}";
    }

    private static NormalizedSignupSubmission? NormalizeSubmission(
        SignupSubmitPayload payload)
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
        var personalTitle = NormalizeOptional(
            payload.PrimaryUser?.PersonalTitle,
            MaxCustomerTypeLength);

        if (companyName is null
            || customerType is null
            || displayName is null
            || email is null
            || customerEmail is null
            || addressLine1 is null
            || postalCode is null
            || city is null
            || country is null
            || givenName is null
            || surname is null)
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
            initials,
            displayName,
            email,
            primaryPhone ?? customerPhone,
            payload.PrimaryUser?.IsPrimaryContact ?? true);

        return new NormalizedSignupSubmission(
            companyName,
            displayName,
            email,
            primaryPhone ?? customerPhone,
            message,
            customer,
            primaryUser,
            ValidatePackSelection(payload.PackSelection));
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
            record.PackSelection,
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

    private static SignupPackSelectionSnapshot? ValidatePackSelection(
        SignupPackSelectionSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        var packKey = snapshot.PackKey?.Trim();
        var packLabel = snapshot.PackLabel?.Trim();
        var offerId = snapshot.OfferId?.Trim();
        var offerExternalReference = snapshot.OfferExternalReference?.Trim();
        var paymentMode = snapshot.PaymentMode?.Trim();
        var currency = snapshot.Currency?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(packKey)
            || string.IsNullOrWhiteSpace(packLabel)
            || string.IsNullOrWhiteSpace(offerId)
            || string.IsNullOrWhiteSpace(offerExternalReference)
            || string.IsNullOrWhiteSpace(paymentMode)
            || string.IsNullOrWhiteSpace(currency)
            || snapshot.CommitmentMonths is not 1 and not 6 and not 12
            || snapshot.BillingIntervalMonths is < 1 or > 12
            || snapshot.DiscountPercent is < 0 or > 100
            || snapshot.MonthlyPriceAmountCents < 0
            || snapshot.BillingPriceAmountCents < 0
            || snapshot.SetupFeeAmountCents < 0
            || snapshot.FirstChargeAmountCents < 0
            || currency != "EUR")
        {
            throw new PortalValidationException();
        }

        return snapshot with
        {
            PackKey = packKey,
            PackLabel = packLabel,
            OfferId = offerId,
            OfferExternalReference = offerExternalReference,
            PaymentMode = paymentMode,
            Currency = currency
        };
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
        SignupPackSelectionSnapshot? PackSelection);
}
