using System.Security.Cryptography;
using System.Text;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
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

    Task<SignupOperationResult> ApproveAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SignupOperationResult> RejectAsync(
        string id,
        string? reason,
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

    private readonly ISignupRepository _repository;
    private readonly IEmailDispatchService _emailDispatch;
    private readonly IPortalPasswordService _passwordService;
    private readonly SignupRuntimeConfiguration _configuration;
    private readonly EmailRuntimeConfiguration _emailConfiguration;
    private readonly ILogger<SignupService> _logger;

    public SignupService(
        ISignupRepository repository,
        IEmailDispatchService emailDispatch,
        IPortalPasswordService passwordService,
        SignupRuntimeConfiguration configuration,
        EmailRuntimeConfiguration emailConfiguration,
        ILogger<SignupService> logger)
    {
        _repository = repository;
        _emailDispatch = emailDispatch;
        _passwordService = passwordService;
        _configuration = configuration;
        _emailConfiguration = emailConfiguration;
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
                false, "SIGNUP_DISABLED",
                "Les inscriptions ne sont pas ouvertes.");
        }

        var companyName = payload.CompanyName?.Trim() ?? string.Empty;
        var contactName = payload.ContactName?.Trim() ?? string.Empty;
        var email = payload.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var phone = NormalizeOptional(payload.Phone, 40);
        var message = NormalizeOptional(payload.Message, MaxMessageLength);

        if (companyName.Length is < 1 or > MaxNameLength
            || contactName.Length is < 1 or > MaxNameLength
            || email.Length is < 3 or > MaxEmailLength
            || !IsPlausibleEmail(email))
        {
            return new SignupOperationResult(
                false, "INVALID_REQUEST",
                "Les informations transmises sont invalides.");
        }

        // Fenêtre de 24h : couvre le rate limit 1/email/24h et
        // l'idempotence. Réponse identique que l'e-mail soit déjà pris ou
        // non (non-leak) : on ne révèle jamais qu'un compte existe.
        var windowStart = DateTime.UtcNow.AddHours(-24);
        var alreadyKnown = await _repository.HasRecentSignupOrUserAsync(
            email, windowStart, cancellationToken);
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
            companyName,
            contactName,
            email,
            phone,
            message,
            HashToken(token),
            DateTime.UtcNow.AddHours(_configuration.VerificationTokenTtlHours),
            NormalizeOptional(payload.SourceAddress, 45),
            NormalizeOptional(payload.UserAgent, 500));
        await _repository.InsertPendingAsync(insert, cancellationToken);

        var verificationUrl = BuildUrl("/signup/verify", token);
        var delivery = await _emailDispatch.SendSignupVerificationAsync(
            email, contactName, verificationUrl, correlationId,
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
            HashToken(normalized), cancellationToken);
        if (target is null
            || !string.Equals(target.Status, "email_pending",
                StringComparison.Ordinal))
        {
            return TokenInvalid();
        }

        if (target.VerificationTokenExpiresAtUtc is { } expiry
            && expiry < DateTime.UtcNow)
        {
            return new SignupOperationResult(
                false, "TOKEN_EXPIRED",
                "Ce lien de vérification a expiré. Renouvelez votre demande.");
        }

        await _repository.MarkEmailVerifiedAsync(target.Id, cancellationToken);
        return new SignupOperationResult(
            true, "EMAIL_VERIFIED",
            "Adresse e-mail confirmée. Votre demande est en attente de validation.");
    }

    public async Task<IReadOnlyList<SignupAdminSummary>> ListAsync(
        string? statusFilter,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeStatusFilter(statusFilter);
        var records = await _repository.ListAsync(
            normalized, 50, cancellationToken);
        return records.Select(ToSummary).ToList();
    }

    public async Task<SignupAdminDetail?> GetAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(id, cancellationToken);
        return record is null ? null : ToDetail(record);
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
                false, "SIGNUP_NOT_FOUND", "Demande introuvable.");
        }

        if (!string.Equals(record.Status, "email_verified",
            StringComparison.Ordinal))
        {
            return new SignupOperationResult(
                false, "INVALID_STATE",
                "Seules les demandes vérifiées par e-mail peuvent être approuvées.");
        }

        var passwordToken = GenerateToken();
        var request = new SignupApprovalRequest(
            record.Id,
            Guid.NewGuid().ToString("D"),
            GenerateCustomerReference(),
            record.CompanyName,
            record.Email,
            record.Phone,
            Guid.NewGuid().ToString("D"),
            record.ContactName,
            HashToken(passwordToken),
            DateTime.UtcNow.AddHours(
                _configuration.PasswordSetupTokenTtlHours));

        var result = await _repository.ApproveAsync(request, cancellationToken);
        if (result is null)
        {
            return new SignupOperationResult(
                false, "INVALID_STATE",
                "La demande n'a pas pu être approuvée dans son état actuel.");
        }

        var setPasswordUrl = BuildUrl("/set-password", passwordToken);
        var delivery = await _emailDispatch.SendAccountApprovedAsync(
            result.Email, result.ContactName, setPasswordUrl, correlationId,
            cancellationToken);
        if (!delivery.Succeeded)
        {
            _logger.LogWarning(
                "Account approved email not delivered ({Code}) correlation_id {CorrelationId}",
                delivery.Code,
                correlationId);
        }

        return new SignupOperationResult(
            true, "SIGNUP_APPROVED",
            $"Compte créé ({result.CustomerReference}). Un lien de définition de mot de passe a été envoyé.");
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
                false, "SIGNUP_NOT_FOUND", "Demande introuvable.");
        }

        var normalizedReason = NormalizeOptional(reason, 500);
        var rejected = await _repository.RejectAsync(
            id, normalizedReason, cancellationToken);
        if (!rejected)
        {
            return new SignupOperationResult(
                false, "INVALID_STATE",
                "Seules les demandes en cours peuvent être refusées.");
        }

        var delivery = await _emailDispatch.SendAccountRejectedAsync(
            record.Email, record.ContactName, normalizedReason, correlationId,
            cancellationToken);
        if (!delivery.Succeeded)
        {
            _logger.LogWarning(
                "Account rejected email not delivered ({Code}) correlation_id {CorrelationId}",
                delivery.Code,
                correlationId);
        }

        return new SignupOperationResult(
            true, "SIGNUP_REJECTED", "Demande refusée.");
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
                false, "INVALID_PASSWORD",
                $"Le mot de passe doit comporter entre {MinPasswordLength} et {MaxPasswordLength} caractères.");
        }

        var target = await _repository.FindApprovedByPasswordHashAsync(
            HashToken(normalizedToken), cancellationToken);
        if (target is null)
        {
            return TokenInvalid();
        }

        if (target.PasswordSetupExpiresAtUtc is { } expiry
            && expiry < DateTime.UtcNow)
        {
            return new SignupOperationResult(
                false, "TOKEN_EXPIRED",
                "Ce lien de définition de mot de passe a expiré.");
        }

        var passwordHash = _passwordService.HashPassword(
            target.PortalUserId, password);
        await _repository.SetPasswordAsync(
            target.SignupId, target.PortalUserId, passwordHash,
            cancellationToken);

        return new SignupOperationResult(
            true, "PASSWORD_SET",
            "Mot de passe défini. Vous pouvez désormais vous connecter.");
    }

    // Validation non destructive du lien de définition de mot de passe :
    // utilisée au chargement (GET) de la page /set-password pour décider
    // d'afficher le formulaire ou l'état « lien invalide / expiré » SANS
    // consommer le jeton (la consommation reste le POST SetPasswordAsync).
    // Le hash du jeton étant effacé à la consommation, un lien déjà utilisé
    // n'est plus retrouvé et retombe sur TOKEN_INVALID, comme un lien inconnu.
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
            HashToken(normalizedToken), cancellationToken);
        if (target is null)
        {
            return TokenInvalid();
        }

        if (target.PasswordSetupExpiresAtUtc is { } expiry
            && expiry < DateTime.UtcNow)
        {
            return new SignupOperationResult(
                false, "TOKEN_EXPIRED",
                "Ce lien de définition de mot de passe a expiré.");
        }

        return new SignupOperationResult(
            true, "TOKEN_VALID",
            "Lien valide. Choisissez votre mot de passe.");
    }

    private static SignupOperationResult Accepted()
        => new(
            true, "SIGNUP_ACCEPTED",
            "Demande enregistrée. Vérifiez votre boîte mail pour confirmer votre adresse.");

    private static SignupOperationResult TokenInvalid()
        => new(
            false, "TOKEN_INVALID",
            "Ce lien est invalide ou a déjà été utilisé.");

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
            record.SourceAddress,
            record.RejectedReason,
            ToIso(record.CreatedAtUtc),
            ToIso(record.UpdatedAtUtc),
            ToNullableIso(record.ApprovedAtUtc),
            ToNullableIso(record.RejectedAtUtc));

    private static string ToIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

    private static string? ToNullableIso(DateTime? value)
        => value is null ? null : ToIso(value.Value);
}
