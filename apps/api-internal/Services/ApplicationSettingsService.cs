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
        // Fonction critique jamais validee pour la production : une demande
        // approuvee sans revue humaine provisionnerait un acces client. Elle est
        // exposee pour etre visible, mais reste non editable et le service force
        // `false` — une ligne posee directement en base ne la reactive pas.
        new("signup_auto_approve", "signup", "Approbation automatique", "Fonction critique non validée : toute demande serait approuvée sans revue humaine. Verrouillée à « désactivée » côté API.", "bool", "false", Classification: "code_invariant", Risk: "high", Editable: false),
        new("session_duration_minutes", "security", "Durée de session", "Borne fonctionnelle : 5 à 10 080 minutes.", "int", "60", 5, 10080, Risk: "medium"),
        new("login_max_failures", "security", "Échecs de connexion autorisés", "Borne fonctionnelle : 2 à 20.", "int", "5", 2, 20, Risk: "medium"),
        new("login_lockout_minutes", "security", "Verrouillage de connexion", "Borne fonctionnelle : 1 à 120 minutes.", "int", "10", 1, 120, Risk: "medium"),
        // Les contenus d'e-mail ont quitte ce registre generique au profit des
        // tables specialisees `email_templates` (migration 074) : whitelist de
        // variables par modele, revisions dediees et repli code. Les anciennes
        // cles `email_*` eventuellement presentes en base sont ignorees, comme
        // toute cle hors registre.
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

    public async Task<PortalBillingConfiguration> GetPortalBillingConfigurationAsync(PortalBillingConfiguration fallback, CancellationToken cancellationToken)
    {
        var values = await StoredValuesAsync(cancellationToken);
        return new PortalBillingConfiguration(OptionalText(values, "billing_iban", fallback.Iban), OptionalText(values, "billing_bic", fallback.Bic), OptionalText(values, "billing_paypal_url", fallback.PaypalUrl), Text(values, "billing_transfer_label", fallback.TransferLabel));
    }

    public async Task<ApplicationSettingMutationResponse> UpdateAsync(string key, ApplicationSettingUpdateRequest request, string actorUserId, string correlationId, CancellationToken cancellationToken)
    {
        if (!ByKey.TryGetValue(key, out var definition)) return new("SETTINGS_UNKNOWN_KEY", "Ce paramètre n'appartient pas au registre autorisé.", null, correlationId);
        if (!definition.Editable) return new("SETTINGS_READ_ONLY", "Ce paramètre est verrouillé par le code et ne peut pas être modifié depuis l'administration.", null, correlationId);
        if (!TryNormalize(definition, request.Value, out var valueJson)) return new("SETTINGS_INVALID_VALUE", "La valeur ne respecte pas les contraintes de ce paramètre.", null, correlationId);
        var previous = await _repository.GetAsync(key, cancellationToken);
        var next = new StoredApplicationSetting(key, definition.Category, valueJson, definition.ValueType, request.ExpectedVersion + 1, DateTime.UtcNow, actorUserId);
        if (!await _repository.TryUpsertAsync(next, request.ExpectedVersion, cancellationToken)) return new("SETTINGS_VERSION_CONFLICT", "Ce paramètre a été modifié par un autre administrateur. Rechargez la page.", null, correlationId);
        await _repository.AddRevisionAsync(key, next.Version, previous?.ValueJson, next.ValueJson, actorUserId, correlationId, cancellationToken);
        return new("SETTINGS_UPDATED", "Paramètre enregistré.", ToItem(definition, next), correlationId);
    }

    // L'administration doit voir la valeur *appliquee*, pas la ligne brute : une
    // valeur stockee devenue inacceptable s'affiche donc comme la valeur par
    // defaut. La version stockee est conservee pour que la concurrence
    // optimiste reste utilisable et qu'un reglage corrompu puisse etre reecrit.
    private static ApplicationSettingItem ToItem(Definition definition, StoredApplicationSetting? stored)
    {
        var effective = stored is null ? null : EffectiveValue(definition, stored);
        return new(definition.Key, definition.Category, definition.Label, definition.Description, definition.ValueType, effective ?? JsonDocument.Parse(definition.DefaultJson).RootElement.Clone(), definition.Classification, definition.Risk, definition.Editable, definition.RestartRequired, definition.Sensitive, effective is null ? "default" : "database", stored?.Version ?? 0, stored?.UpdatedAtUtc.ToString("O"));
    }

    private static bool TryNormalize(Definition definition, JsonElement value, out string valueJson)
    {
        valueJson = "";
        if (definition.ValueType == "bool" && (value.ValueKind is JsonValueKind.True or JsonValueKind.False)) { valueJson = value.GetRawText(); return true; }
        if (definition.ValueType == "int" && value.TryGetInt32(out var integer) && integer >= (definition.Minimum ?? int.MinValue) && integer <= (definition.Maximum ?? int.MaxValue)) { valueJson = integer.ToString(System.Globalization.CultureInfo.InvariantCulture); return true; }
        if (definition.ValueType is "string" or "email" or "url" && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()?.Trim() ?? "";
            if (definition.Key is "billing_iban" or "billing_bic") text = text.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
            if (text.Length > (definition.MaxLength ?? 4000) || text.Any(char.IsControl) || text.Contains('<') || text.Contains('>')) return false;
            if (definition.ValueType == "email" && text.Length > 0 && (!text.Contains('@') || text.Contains(' '))) return false;
            if (definition.ValueType == "url" && text.Length > 0 && (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)) return false;
            if (definition.Key == "billing_iban" && text.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(text.Replace(" ", ""), "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) return false;
            if (definition.Key == "billing_bic" && text.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(text, "^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) return false;
            valueJson = JsonSerializer.Serialize(text); return true;
        }
        return false;
    }

    // Une valeur relue depuis MariaDB repasse par la validation d'ecriture : une
    // ligne hors bornes — heritee d'un registre plus permissif, posee a la main
    // ou issue d'une restauration — ne doit jamais devenir applicable simplement
    // parce qu'elle vient de la base. Elle est ignoree, et le repli est la
    // valeur d'environnement, jamais une configuration plus permissive.
    private async Task<IReadOnlyDictionary<string, JsonElement>> StoredValuesAsync(CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in await _repository.GetAllAsync(cancellationToken))
        {
            if (!ByKey.TryGetValue(item.Key, out var definition)) continue;
            if (EffectiveValue(definition, item) is not { } effective) continue;
            values[item.Key] = effective;
        }

        return values;
    }

    // Retourne la valeur stockee uniquement si elle est encore acceptable au
    // regard du registre courant, sous sa forme normalisee. `null` signifie
    // « ignorer cette ligne », que la cle soit verrouillee, la charge illisible
    // ou la valeur hors bornes.
    private static JsonElement? EffectiveValue(Definition definition, StoredApplicationSetting stored)
    {
        if (!definition.Editable) return null;
        JsonElement parsed;
        try { parsed = JsonDocument.Parse(stored.ValueJson).RootElement.Clone(); }
        catch (JsonException) { return null; }
        if (!TryNormalize(definition, parsed, out var normalized)) return null;
        return JsonDocument.Parse(normalized).RootElement.Clone();
    }

    private static bool Bool(IReadOnlyDictionary<string, JsonElement> values, string key, bool fallback) => values.TryGetValue(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;
    private static int Int(IReadOnlyDictionary<string, JsonElement> values, string key, int fallback) => values.TryGetValue(key, out var value) && value.TryGetInt32(out var parsed) ? parsed : fallback;
    private static string Text(IReadOnlyDictionary<string, JsonElement> values, string key, string fallback) => values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : fallback;
    private static string? OptionalText(IReadOnlyDictionary<string, JsonElement> values, string key, string? fallback)
        => values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString()
            : fallback;
}
