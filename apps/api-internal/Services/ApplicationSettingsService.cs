using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public interface IApplicationSettingsService
{
    bool IsPersistent { get; }
    Task<ApplicationSettingsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
    Task<SignupRuntimeConfiguration> GetSignupConfigurationAsync(SignupRuntimeConfiguration fallback, CancellationToken cancellationToken);
    Task<AuthRuntimeConfiguration> GetAuthConfigurationAsync(AuthRuntimeConfiguration fallback, CancellationToken cancellationToken);
    Task<(string Subject, string Body)> RenderEmailTemplateAsync(string templateKey, string fallbackSubject, string fallbackBody, IReadOnlyDictionary<string, string?> variables, CancellationToken cancellationToken);
    Task<PortalBillingConfiguration> GetPortalBillingConfigurationAsync(PortalBillingConfiguration fallback, CancellationToken cancellationToken);
    Task<ApplicationSettingMutationResponse> UpdateAsync(string key, ApplicationSettingUpdateRequest request, string actorUserId, string correlationId, CancellationToken cancellationToken);
}

public sealed class ApplicationSettingsService : IApplicationSettingsService
{
    private sealed record Definition(
        string Key,
        string Category,
        string Label,
        string Description,
        string ValueType,
        string DefaultJson,
        int? Minimum = null,
        int? Maximum = null,
        int? MaxLength = null,
        string Classification = "dynamic",
        string Risk = "low",
        bool Editable = true,
        bool RestartRequired = false,
        bool Sensitive = false);
    private static readonly Definition[] Definitions =
    [
        new("brand_name", "site", "Nom commercial", "Nom affiché de l'entreprise.", "string", "\"Zachary IT\"", MaxLength: 100),
        new("legal_name", "site", "Dénomination juridique", "Nom légal affichable dans les communications.", "string", "\"Zachary IT\"", MaxLength: 160),
        new("contact_email", "site", "E-mail de contact", "Adresse affichée pour le contact général.", "email", "\"\"", MaxLength: 254),
        new("support_email", "site", "E-mail support", "Adresse affichée pour le support.", "email", "\"\"", MaxLength: 254),
        new("default_site_title", "site", "Titre SEO par défaut", "Titre public par défaut, sans HTML.", "string", "\"Zachary IT\"", MaxLength: 70),
        new("default_site_description", "site", "Description SEO par défaut", "Description publique par défaut, sans HTML.", "string", "\"\"", MaxLength: 180),
        new("signup_enabled", "signup", "Inscriptions ouvertes", "Kill switch appliqué directement côté API.", "bool", "false", Risk: "high"),
        new("signup_rate_limit_per_ip_per_hour", "signup", "Limite IP par heure", "Borne fonctionnelle : 1 à 100.", "int", "3", 1, 100),
        new("signup_rate_limit_per_email_per_24h", "signup", "Limite e-mail par 24 h", "Borne fonctionnelle : 1 à 100.", "int", "1", 1, 100),
        new("signup_verification_token_ttl_hours", "signup", "Durée du lien de vérification", "Borne fonctionnelle : 1 à 168 heures.", "int", "24", 1, 168),
        new("signup_password_setup_token_ttl_hours", "signup", "Durée du lien de mot de passe", "Borne fonctionnelle : 1 à 168 heures.", "int", "24", 1, 168),
        new("session_duration_minutes", "security", "Durée de session", "Borne fonctionnelle : 5 à 10 080 minutes.", "int", "60", 5, 10080, Risk: "medium"),
        new("login_max_failures", "security", "Échecs de connexion autorisés", "Borne fonctionnelle : 2 à 20.", "int", "5", 2, 20, Risk: "medium"),
        new("login_lockout_minutes", "security", "Verrouillage de connexion", "Borne fonctionnelle : 1 à 120 minutes.", "int", "10", 1, 120, Risk: "medium"),
        new("email_signup_verification_subject", "messages", "E-mail · vérification · sujet", "Variables : {{contactName}}, {{verificationUrl}}.", "string", "\"\"", MaxLength: 240),
        new("email_signup_verification_body", "messages", "E-mail · vérification · corps", "Texte brut. Variables contrôlées uniquement.", "string", "\"\"", MaxLength: 4000),
        new("email_account_approved_subject", "messages", "E-mail · compte validé · sujet", "Variables : {{contactName}}, {{setPasswordUrl}}.", "string", "\"\"", MaxLength: 240),
        new("email_account_approved_body", "messages", "E-mail · compte validé · corps", "Texte brut. Variables contrôlées uniquement.", "string", "\"\"", MaxLength: 4000),
        new("email_account_rejected_subject", "messages", "E-mail · compte refusé · sujet", "Variables : {{contactName}}, {{reason}}.", "string", "\"\"", MaxLength: 240),
        new("email_account_rejected_body", "messages", "E-mail · compte refusé · corps", "Texte brut. Variables contrôlées uniquement.", "string", "\"\"", MaxLength: 4000),
        new("billing_iban", "billing", "IBAN de règlement", "Coordonnée de règlement pour les nouveaux documents et l'espace client.", "string", "\"\"", MaxLength: 34),
        new("billing_bic", "billing", "BIC", "Code BIC associé au compte de règlement.", "string", "\"\"", MaxLength: 11),
        new("billing_paypal_url", "billing", "Lien PayPal", "URL HTTPS facultative de règlement.", "url", "\"\"", MaxLength: 500),
        new("billing_transfer_label", "billing", "Bénéficiaire virement", "Libellé affiché pour un nouveau règlement.", "string", "\"Zachary HOUNSA-HOUNKPA EI\"", MaxLength: 160),
    ];
    private static readonly Dictionary<string, Definition> ByKey = Definitions.ToDictionary(item => item.Key, StringComparer.Ordinal);
    private readonly IApplicationSettingsRepository _repository;
    public ApplicationSettingsService(IApplicationSettingsRepository repository) => _repository = repository;
    public bool IsPersistent => _repository.IsPersistent;

    public async Task<ApplicationSettingsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var persisted = (await _repository.GetAllAsync(cancellationToken)).ToDictionary(item => item.Key, StringComparer.Ordinal);
        return new ApplicationSettingsSnapshot(Definitions.Select(definition => ToItem(definition, persisted.GetValueOrDefault(definition.Key))).ToArray(), IsPersistent);
    }

    public async Task<SignupRuntimeConfiguration> GetSignupConfigurationAsync(SignupRuntimeConfiguration fallback, CancellationToken cancellationToken)
    {
        var values = await StoredValuesAsync(cancellationToken);
        return fallback with { Enabled = Bool(values, "signup_enabled", fallback.Enabled), RateLimitPerIpPerHour = Int(values, "signup_rate_limit_per_ip_per_hour", fallback.RateLimitPerIpPerHour), RateLimitPerEmailPer24h = Int(values, "signup_rate_limit_per_email_per_24h", fallback.RateLimitPerEmailPer24h), VerificationTokenTtlHours = Int(values, "signup_verification_token_ttl_hours", fallback.VerificationTokenTtlHours), PasswordSetupTokenTtlHours = Int(values, "signup_password_setup_token_ttl_hours", fallback.PasswordSetupTokenTtlHours), AutoApprove = false };
    }

    public async Task<AuthRuntimeConfiguration> GetAuthConfigurationAsync(AuthRuntimeConfiguration fallback, CancellationToken cancellationToken)
    {
        var values = await StoredValuesAsync(cancellationToken);
        return fallback with { SessionDuration = TimeSpan.FromMinutes(Int(values, "session_duration_minutes", (int)fallback.SessionDuration.TotalMinutes)), LoginMaxFailures = Int(values, "login_max_failures", fallback.LoginMaxFailures), LoginLockoutDuration = TimeSpan.FromMinutes(Int(values, "login_lockout_minutes", (int)fallback.LoginLockoutDuration.TotalMinutes)) };
    }

    public async Task<(string Subject, string Body)> RenderEmailTemplateAsync(string templateKey, string fallbackSubject, string fallbackBody, IReadOnlyDictionary<string, string?> variables, CancellationToken cancellationToken)
    {
        var values = await StoredValuesAsync(cancellationToken);
        var subject = Text(values, $"email_{templateKey}_subject", fallbackSubject);
        var body = Text(values, $"email_{templateKey}_body", fallbackBody);
        foreach (var (name, value) in variables) { subject = subject.Replace("{{" + name + "}}", value ?? string.Empty, StringComparison.Ordinal); body = body.Replace("{{" + name + "}}", value ?? string.Empty, StringComparison.Ordinal); }
        return (subject, body);
    }

    public async Task<PortalBillingConfiguration> GetPortalBillingConfigurationAsync(PortalBillingConfiguration fallback, CancellationToken cancellationToken)
    {
        var values = await StoredValuesAsync(cancellationToken);
        return new PortalBillingConfiguration(OptionalText(values, "billing_iban", fallback.Iban), OptionalText(values, "billing_bic", fallback.Bic), OptionalText(values, "billing_paypal_url", fallback.PaypalUrl), Text(values, "billing_transfer_label", fallback.TransferLabel));
    }

    public async Task<ApplicationSettingMutationResponse> UpdateAsync(string key, ApplicationSettingUpdateRequest request, string actorUserId, string correlationId, CancellationToken cancellationToken)
    {
        if (!ByKey.TryGetValue(key, out var definition)) return new("SETTINGS_UNKNOWN_KEY", "Ce paramètre n'appartient pas au registre autorisé.", null, correlationId);
        if (!TryNormalize(definition, request.Value, out var valueJson)) return new("SETTINGS_INVALID_VALUE", "La valeur ne respecte pas les contraintes de ce paramètre.", null, correlationId);
        var previous = await _repository.GetAsync(key, cancellationToken);
        var next = new StoredApplicationSetting(key, definition.Category, valueJson, definition.ValueType, request.ExpectedVersion + 1, DateTime.UtcNow, actorUserId);
        if (!await _repository.TryUpsertAsync(next, request.ExpectedVersion, cancellationToken)) return new("SETTINGS_VERSION_CONFLICT", "Ce paramètre a été modifié par un autre administrateur. Rechargez la page.", null, correlationId);
        await _repository.AddRevisionAsync(key, next.Version, previous?.ValueJson, next.ValueJson, actorUserId, correlationId, cancellationToken);
        return new("SETTINGS_UPDATED", "Paramètre enregistré.", ToItem(definition, next), correlationId);
    }

    private static ApplicationSettingItem ToItem(Definition definition, StoredApplicationSetting? stored)
        => new(definition.Key, definition.Category, definition.Label, definition.Description, definition.ValueType, JsonDocument.Parse(stored?.ValueJson ?? definition.DefaultJson).RootElement.Clone(), definition.Classification, definition.Risk, definition.Editable, definition.RestartRequired, definition.Sensitive, stored is null ? "default" : "database", stored?.Version ?? 0, stored?.UpdatedAtUtc.ToString("O"));

    private static bool TryNormalize(Definition definition, JsonElement value, out string valueJson)
    {
        valueJson = "";
        if (definition.ValueType == "bool" && (value.ValueKind is JsonValueKind.True or JsonValueKind.False)) { valueJson = value.GetRawText(); return true; }
        if (definition.ValueType == "int" && value.TryGetInt32(out var integer) && integer >= definition.Minimum && integer <= definition.Maximum) { valueJson = integer.ToString(System.Globalization.CultureInfo.InvariantCulture); return true; }
        if (definition.ValueType is "string" or "email" or "url" && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()?.Trim() ?? "";
            if (definition.Key is "billing_iban" or "billing_bic") text = text.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
            if (text.Length > (definition.MaxLength ?? 4000) || text.Any(char.IsControl) || text.Contains('<') || text.Contains('>')) return false;
            if (definition.Key.StartsWith("email_", StringComparison.Ordinal) && !HasOnlyAllowedTemplateVariables(definition.Key, text)) return false;
            if (definition.ValueType == "email" && text.Length > 0 && (!text.Contains('@') || text.Contains(' '))) return false;
            if (definition.ValueType == "url" && text.Length > 0 && (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)) return false;
            if (definition.Key == "billing_iban" && text.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(text.Replace(" ", ""), "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) return false;
            if (definition.Key == "billing_bic" && text.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(text, "^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) return false;
            valueJson = JsonSerializer.Serialize(text); return true;
        }
        return false;
    }

    private async Task<IReadOnlyDictionary<string, JsonElement>> StoredValuesAsync(CancellationToken cancellationToken)
        => (await _repository.GetAllAsync(cancellationToken)).ToDictionary(item => item.Key, item => JsonDocument.Parse(item.ValueJson).RootElement.Clone(), StringComparer.Ordinal);

    private static bool Bool(IReadOnlyDictionary<string, JsonElement> values, string key, bool fallback) => values.TryGetValue(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;
    private static int Int(IReadOnlyDictionary<string, JsonElement> values, string key, int fallback) => values.TryGetValue(key, out var value) && value.TryGetInt32(out var parsed) ? parsed : fallback;
    private static string Text(IReadOnlyDictionary<string, JsonElement> values, string key, string fallback) => values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : fallback;
    private static string? OptionalText(IReadOnlyDictionary<string, JsonElement> values, string key, string? fallback)
        => values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString()
            : fallback;
    private static bool HasOnlyAllowedTemplateVariables(string key, string text)
    {
        var allowed = key.Contains("signup_verification", StringComparison.Ordinal) ? new[] { "contactName", "verificationUrl" } : key.Contains("account_approved", StringComparison.Ordinal) ? new[] { "contactName", "setPasswordUrl" } : key.Contains("account_rejected", StringComparison.Ordinal) ? new[] { "contactName", "reason" } : Array.Empty<string>();
        var remaining = System.Text.RegularExpressions.Regex.Replace(text, "\\{\\{([A-Za-z][A-Za-z0-9]*)\\}\\}", match => allowed.Contains(match.Groups[1].Value, StringComparer.Ordinal) ? string.Empty : match.Value);
        return !remaining.Contains("{{", StringComparison.Ordinal) && !remaining.Contains("}}", StringComparison.Ordinal);
    }
}
