using System.Globalization;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface ISettingsAuditService
{
    Task<SettingsAuditView> SearchAsync(
        string? from,
        string? to,
        string? actor,
        string? category,
        string? risk,
        string? outcome,
        string? correlationId,
        string? target,
        int? limit,
        CancellationToken cancellationToken);

    Task<SettingsPermissionOverview> GetPermissionsAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Audit consolide du Centre de configuration (specification, section 29).
///
/// Le service ne cree pas un second journal : il lit le journal d'audit
/// existant en le restreignant au registre ferme des actions du Centre. Un
/// journal parallele divergerait du premier, et c'est le premier qui fait foi.
///
/// La categorie et le niveau de risque ne sont pas stockes en base : ils sont
/// rattaches ici depuis le registre. Les deduire du nom de l'action produirait
/// des classements faux des qu'une action serait renommee.
/// </summary>
public sealed class SettingsAuditService : ISettingsAuditService
{
    private const int DefaultLimit = 100;
    private const int MaximumLimit = 200;

    private static readonly IReadOnlyList<string> KnownOutcomes =
        ["success", "refused", "error"];

    private static readonly IReadOnlyDictionary<string, string> CategoryLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["settings"] = "Parametres applicatifs",
            ["communications"] = "Communications",
            ["diagnostic"] = "Diagnostic",
            ["billing"] = "Facturation et fiscalite",
            ["demonstrations"] = "Demonstrations",
            ["integrations"] = "Integrations"
        };

    private readonly ISettingsAuditRepository _repository;
    private readonly IEditorialRepository _editorial;
    private readonly ILogger<SettingsAuditService> _logger;

    public SettingsAuditService(
        ISettingsAuditRepository repository,
        IEditorialRepository editorial,
        ILogger<SettingsAuditService> logger)
    {
        _repository = repository;
        _editorial = editorial;
        _logger = logger;
    }

    public async Task<SettingsAuditView> SearchAsync(
        string? from,
        string? to,
        string? actor,
        string? category,
        string? risk,
        string? outcome,
        string? correlationId,
        string? target,
        int? limit,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = Normalize(category);
        var normalizedRisk = Normalize(risk);
        var normalizedOutcome = Normalize(outcome);
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaximumLimit);

        string? warning = null;

        if (normalizedCategory is not null
            && !SettingsAuditRegistry.Categories.Contains(normalizedCategory))
        {
            warning = "La categorie demandee n'existe pas ; aucun evenement n'est retenu.";
        }
        else if (normalizedRisk is not null
            && !SettingsAuditRegistry.Risks.Contains(normalizedRisk))
        {
            warning = "Le niveau de risque demande n'existe pas ; aucun evenement n'est retenu.";
        }

        var actions = SettingsAuditRegistry.Resolve(normalizedCategory, normalizedRisk);

        var fromUtc = ParseInstant(from);
        var toUtc = ParseInstant(to);
        if (fromUtc is not null && toUtc is not null && fromUtc > toUtc)
        {
            // Inverser silencieusement les bornes masquerait la saisie fautive ;
            // refuser la fenetre la rend visible sans rien inventer.
            warning = "La date de debut est posterieure a la date de fin ; aucun evenement n'est retenu.";
            actions = [];
        }

        var entries = Array.Empty<SettingsAuditEntryView>();
        if (actions.Count > 0)
        {
            try
            {
                var stored = await _repository.SearchAsync(
                    new SettingsAuditQuery(
                        actions,
                        fromUtc,
                        toUtc,
                        Normalize(actor),
                        normalizedOutcome,
                        Normalize(correlationId),
                        Normalize(target),
                        effectiveLimit),
                    cancellationToken);

                entries = stored.Select(Project).ToArray();
            }
            catch (Exception exception) when (
                exception is MySqlException or InvalidOperationException)
            {
                _logger.LogWarning(
                    exception,
                    "Settings audit search unavailable");
                warning = "Le journal d'audit est momentanement indisponible.";
            }
        }

        return new SettingsAuditView(
            entries,
            SettingsAuditRegistry.All
                .Select(action => new SettingsAuditActionView(
                    action.Action,
                    action.Label,
                    action.Category,
                    action.Risk))
                .ToArray(),
            SettingsAuditRegistry.Categories
                .Select(key => new SettingsAuditCategoryView(
                    key,
                    CategoryLabels.TryGetValue(key, out var label) ? label : key))
                .ToArray(),
            SettingsAuditRegistry.Risks,
            KnownOutcomes,
            new SettingsAuditFilterEcho(
                Normalize(from),
                Normalize(to),
                Normalize(actor),
                normalizedCategory,
                normalizedRisk,
                normalizedOutcome,
                Normalize(correlationId),
                Normalize(target),
                effectiveLimit),
            _repository.IsPersistent,
            entries.Length >= effectiveLimit,
            warning);
    }

    public async Task<SettingsPermissionOverview> GetPermissionsAsync(
        CancellationToken cancellationToken)
    {
        var codes = SettingsPermissionRegistry.All
            .Select(permission => permission.Code)
            .ToArray();

        IReadOnlyDictionary<string, int> counts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        try
        {
            counts = await _editorial.GetAdminPermissionGrantCountsAsync(
                codes,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is MySqlException or InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "Admin permission grant counts unavailable");
        }

        var permissions = SettingsPermissionRegistry.All
            .Select(permission =>
            {
                var granted = counts.TryGetValue(permission.Code, out var count)
                    ? count
                    : 0;
                return new SettingsPermissionView(
                    permission.Code,
                    permission.Label,
                    permission.Description,
                    permission.Risk,
                    permission.Surfaces,
                    granted > 0 ? "granted" : "denied",
                    granted);
            })
            .ToArray();

        var missingGrant = permissions.Any(
            permission => permission.State == "denied");
        return new SettingsPermissionOverview(
            permissions,
            BootstrapOpen: false,
            missingGrant
                ? "Au moins une permission n'a aucune attribution explicite : elle est refusee a tous les comptes (fail-closed)."
                : "Chaque permission possede au moins une attribution explicite : seuls les comptes designes y accedent.");
    }

    private static SettingsAuditEntryView Project(SettingsAuditEntry entry)
    {
        var action = SettingsAuditRegistry.Find(entry.Action);
        return new SettingsAuditEntryView(
            entry.OccurredAt,
            entry.Actor,
            entry.Action,
            action?.Label ?? entry.Action,
            action?.Category ?? "settings",
            action?.Risk ?? "medium",
            entry.Outcome,
            entry.ReasonCode,
            entry.TargetType,
            entry.TargetReference,
            entry.CorrelationId,
            entry.SourceAddress);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Une borne de periode est interpretee en UTC, comme les horodatages
    /// stockes. Une saisie illisible est ignoree plutot que rejetee : la
    /// fenetre reste alors ouverte de ce cote, ce qui ne masque aucun
    /// evenement.
    /// </summary>
    private static DateTime? ParseInstant(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        if (DateTime.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return null;
    }
}
