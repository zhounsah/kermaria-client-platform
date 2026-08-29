using System.Text.Json;
using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Autorite des modeles de contenu de demonstration.
///
/// Regle de bascule, volontairement binaire : tant que la table est vide ou
/// illisible, le registre C# fait autorite ; des qu'elle contient au moins un
/// modele, elle fait autorite entierement. Aucune fusion des deux sources, qui
/// produirait des modeles fantomes impossibles a supprimer depuis
/// l'administration.
///
/// Le repli n'est jamais plus permissif que la persistance : il sert exactement
/// les memes modeles que ceux livres avec le code.
/// </summary>
public interface IDemoContentTemplateService
{
    bool IsPersistent { get; }

    /// <summary>Modeles actifs, dans l'ordre d'affichage.</summary>
    Task<IReadOnlyList<DemoContentTemplate>> ListActiveAsync(
        CancellationToken cancellationToken);

    Task<DemoContentTemplate?> FindActiveAsync(
        string? key,
        CancellationToken cancellationToken);

    Task<DemoContentTemplateAdminView> GetAdminViewAsync(
        CancellationToken cancellationToken);

    Task<DemoContentTemplateMutationResponse> SaveAsync(
        DemoContentTemplateSavePayload payload,
        string? actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DemoContentTemplateMutationResponse> DeleteAsync(
        string templateKey,
        int expectedVersion,
        string? actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Recopie les modeles du code dans la table, en une fois, pour amorcer la
    /// bascule. Refuse si la table contient deja quelque chose : c'est une
    /// amorce, pas une restauration.
    /// </summary>
    Task<DemoContentTemplateMutationResponse> ImportCodeTemplatesAsync(
        string? actorUserId,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class DemoContentTemplateService : IDemoContentTemplateService
{
    private const int MaxServices = 20;
    private static readonly Regex KeyPattern = new(
        "^[a-z0-9][a-z0-9-]{1,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IDemoContentTemplateRepository _repository;
    private readonly IDemoProfileRepository _profiles;
    private readonly AdRuntimeConfiguration _adConfiguration;
    private readonly DemoConversionRuntimeConfiguration _conversionConfiguration;
    private readonly ILogger<DemoContentTemplateService> _logger;

    public DemoContentTemplateService(
        IDemoContentTemplateRepository repository,
        IDemoProfileRepository profiles,
        AdRuntimeConfiguration adConfiguration,
        DemoConversionRuntimeConfiguration conversionConfiguration,
        ILogger<DemoContentTemplateService> logger)
    {
        _repository = repository;
        _profiles = profiles;
        _adConfiguration = adConfiguration;
        _conversionConfiguration = conversionConfiguration;
        _logger = logger;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public async Task<IReadOnlyList<DemoContentTemplate>> ListActiveAsync(
        CancellationToken cancellationToken)
    {
        var stored = await ReadStoredAsync(cancellationToken);
        if (stored.Count == 0)
        {
            return DemoContentTemplateRegistry.All;
        }

        return stored
            .Where(item => item.Enabled)
            .Select(ToDomain)
            .ToArray();
    }

    public async Task<DemoContentTemplate?> FindActiveAsync(
        string? key,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim();
        var templates = await ListActiveAsync(cancellationToken);
        return templates.FirstOrDefault(
            template => string.Equals(
                template.Key,
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DemoContentTemplateAdminView> GetAdminViewAsync(
        CancellationToken cancellationToken)
    {
        var stored = await ReadStoredAsync(cancellationToken);
        var usageRead = await ReadUsageAsync(cancellationToken);
        var revisions = await ReadRevisionsAsync(cancellationToken);
        return BuildView(stored, usageRead.Usage, revisions);
    }

    public async Task<DemoContentTemplateMutationResponse> SaveAsync(
        DemoContentTemplateSavePayload payload,
        string? actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var key = (payload.TemplateKey ?? string.Empty).Trim().ToLowerInvariant();
        if (!KeyPattern.IsMatch(key))
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_INVALID_KEY",
                "La cle doit etre en minuscules, sans espace, et comporter au moins deux caracteres.",
                correlationId,
                cancellationToken);
        }

        var label = (payload.Label ?? string.Empty).Trim();
        if (label.Length is < 2 or > 120)
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_INVALID_LABEL",
                "Le libelle doit comporter entre 2 et 120 caracteres.",
                correlationId,
                cancellationToken);
        }

        var description = (payload.Description ?? string.Empty).Trim();
        if (description.Length > 500)
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_INVALID_DESCRIPTION",
                "La description ne peut pas depasser 500 caracteres.",
                correlationId,
                cancellationToken);
        }

        var services = payload.Services ?? [];
        if (services.Count == 0)
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_NO_SERVICE",
                "Un modele doit decrire au moins un service.",
                correlationId,
                cancellationToken);
        }

        if (services.Count > MaxServices)
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_TOO_MANY_SERVICES",
                $"Un modele ne peut pas depasser {MaxServices} services.",
                correlationId,
                cancellationToken);
        }

        var normalizedServices = new List<StoredDemoTemplateService>(services.Count);
        // La composition a la carte identifie un service par son nom, sans tenir
        // compte de la casse : deux noms qui ne different que par la casse
        // rendraient la selection ambigue.
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 0;
        foreach (var service in services)
        {
            var serviceType = (service.ServiceType ?? string.Empty).Trim();
            if (!ServiceTypeRegistry.Contains(serviceType))
            {
                return await FailureAsync(
                    "DEMO_TEMPLATE_UNKNOWN_SERVICE_TYPE",
                    "Type de service inconnu du code : il ne pourrait etre ni provisionne ni affiche correctement.",
                    correlationId,
                    cancellationToken);
            }

            var name = (service.Name ?? string.Empty).Trim();
            var serviceDescription = (service.Description ?? string.Empty).Trim();
            var scope = (service.Scope ?? string.Empty).Trim();
            if (name.Length is < 2 or > 160
                || serviceDescription.Length is < 2 or > 500
                || scope.Length is < 2 or > 300)
            {
                return await FailureAsync(
                    "DEMO_TEMPLATE_INVALID_SERVICE",
                    "Chaque service exige un nom, une description et un perimetre non vides.",
                    correlationId,
                    cancellationToken);
            }

            if (!names.Add(name))
            {
                return await FailureAsync(
                    "DEMO_TEMPLATE_DUPLICATE_SERVICE_NAME",
                    "Deux services d'un meme modele ne peuvent pas porter le meme nom.",
                    correlationId,
                    cancellationToken);
            }

            normalizedServices.Add(new StoredDemoTemplateService(
                serviceType,
                name,
                serviceDescription,
                scope,
                order += 10));
        }

        var template = new StoredDemoContentTemplate(
            key,
            label,
            description,
            payload.Enabled,
            Math.Clamp(payload.DisplayOrder ?? 100, 0, 100000),
            payload.ExpectedVersion + 1,
            DateTime.UtcNow,
            actorUserId,
            normalizedServices);

        // Modele et revision partent ensemble : un modele enregistre sans trace
        // laisse l'historique affirmer que rien n'a change alors que ce que
        // voient les prospects a change.
        bool saved;
        try
        {
            saved = await _repository.TrySaveAsync(
                template,
                payload.ExpectedVersion,
                Describe(template),
                correlationId,
                "saved",
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Ecriture impossible du modele de demonstration {TemplateKey}.",
                key);
            return await FailureAsync(
                "DEMO_TEMPLATE_STORAGE_UNAVAILABLE",
                "La persistance des modeles de demonstration est indisponible : rien n'a ete modifie.",
                correlationId,
                cancellationToken);
        }

        if (!saved)
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_VERSION_CONFLICT",
                "Le modele a ete modifie entre-temps. Rechargez la page avant de reessayer.",
                correlationId,
                cancellationToken);
        }

        return await SuccessAsync(
            "DEMO_TEMPLATE_SAVED",
            "Modele enregistre.",
            correlationId,
            cancellationToken);
    }

    public async Task<DemoContentTemplateMutationResponse> DeleteAsync(
        string templateKey,
        int expectedVersion,
        string? actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var key = (templateKey ?? string.Empty).Trim().ToLowerInvariant();
        if (!KeyPattern.IsMatch(key))
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_INVALID_KEY",
                "Cle de modele invalide.",
                correlationId,
                cancellationToken);
        }

        // Un profil qui pointe vers un modele disparu creerait des comptes de
        // demonstration sans aucun service, silencieusement.
        var usageRead = await ReadUsageAsync(cancellationToken);
        if (!usageRead.Reliable)
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_USAGE_UNAVAILABLE",
                "Impossible de verifier si ce modele est utilise. Suppression refusee par securite.",
                correlationId,
                cancellationToken);
        }

        if (usageRead.Usage.TryGetValue(key, out var profiles) && profiles.Count > 0)
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_IN_USE",
                $"Ce modele est reference par : {string.Join(", ", profiles)}. Modifiez ces profils d'abord.",
                correlationId,
                cancellationToken);
        }

        bool deleted;
        try
        {
            deleted = await _repository.TryDeleteAsync(
                key,
                expectedVersion,
                actorUserId,
                correlationId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Suppression impossible du modele de demonstration {TemplateKey}.",
                key);
            return await FailureAsync(
                "DEMO_TEMPLATE_STORAGE_UNAVAILABLE",
                "La persistance des modeles de demonstration est indisponible : rien n'a ete supprime.",
                correlationId,
                cancellationToken);
        }

        if (!deleted)
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_VERSION_CONFLICT",
                "Le modele a ete modifie ou supprime entre-temps.",
                correlationId,
                cancellationToken);
        }

        return await SuccessAsync(
            "DEMO_TEMPLATE_DELETED",
            "Modele supprime.",
            correlationId,
            cancellationToken);
    }

    public async Task<DemoContentTemplateMutationResponse> ImportCodeTemplatesAsync(
        string? actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var order = 0;
        var serviceOrder = 0;
        var items = DemoContentTemplateRegistry.All
            .Select(template =>
            {
                serviceOrder = 0;
                var candidate = new StoredDemoContentTemplate(
                    template.Key,
                    template.Label,
                    string.Empty,
                    true,
                    order += 10,
                    1,
                    DateTime.UtcNow,
                    actorUserId,
                    template.Services
                        .Select(service => new StoredDemoTemplateService(
                            service.ServiceType,
                            service.Name,
                            service.Description,
                            service.Scope,
                            serviceOrder += 10))
                        .ToArray());
                return new DemoContentTemplateImportItem(candidate, Describe(candidate));
            })
            .ToArray();

        // Tout ou rien. Une amorce partielle laissait une table non vide, que
        // la regle de bascule considere ensuite comme faisant autorite : les
        // modeles manquants devenaient invisibles, et l'amorce refusait de
        // recommencer parce que la table n'etait plus vide.
        bool imported;
        try
        {
            imported = await _repository.TryImportAsync(
                items,
                correlationId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Amorce impossible des modeles de demonstration : aucun modele recopie.");
            return await FailureAsync(
                "DEMO_TEMPLATE_STORAGE_UNAVAILABLE",
                "La persistance des modeles de demonstration est indisponible : aucun modele n'a ete recopie.",
                correlationId,
                cancellationToken);
        }

        if (!imported)
        {
            return await FailureAsync(
                "DEMO_TEMPLATE_ALREADY_ADMINISTERED",
                "Les modeles sont deja administres en base : l'amorce ne s'applique qu'a une table vide.",
                correlationId,
                cancellationToken);
        }

        return await SuccessAsync(
            "DEMO_TEMPLATE_IMPORTED",
            "Modeles du code recopies en base. Ils sont desormais administrables.",
            correlationId,
            cancellationToken);
    }

    private async Task<IReadOnlyList<StoredDemoContentTemplate>> ReadStoredAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await _repository.ListAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Repli ferme au sens metier : on retombe sur les modeles du code,
            // jamais sur une liste vide qui creerait des comptes sans service.
            _logger.LogError(
                exception,
                "Lecture impossible des modeles de demonstration : repli sur le registre du code.");
            return [];
        }
    }

    private async Task<(IReadOnlyDictionary<string, List<string>> Usage, bool Reliable)> ReadUsageAsync(
        CancellationToken cancellationToken)
    {
        var usage = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var profile in await _profiles.ListAsync(cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(profile.ContentTemplateKey))
                {
                    continue;
                }

                var key = profile.ContentTemplateKey.Trim();
                if (!usage.TryGetValue(key, out var list))
                {
                    list = [];
                    usage[key] = list;
                }

                list.Add(profile.Key);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Lecture impossible des profils de demonstration : usage des modeles inconnu.");
            return (usage, false);
        }

        return (usage, true);
    }

    private async Task<IReadOnlyList<StoredTemplateRevision>> ReadRevisionsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await _repository.GetRevisionsAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Lecture impossible de l'historique des modeles de demonstration.");
            return [];
        }
    }

    /// <summary>
    /// Charge historisee d'un modele : ce qui a ete enregistre, pas un resume.
    /// </summary>
    private static string Describe(StoredDemoContentTemplate template)
        => JsonSerializer.Serialize(new
        {
            template.TemplateKey,
            template.Label,
            template.Description,
            template.Enabled,
            template.DisplayOrder,
            Services = template.Services.Select(service => new
            {
                service.ServiceType,
                service.Name,
                service.Description,
                service.Scope,
            }),
        });

    private async Task<DemoContentTemplateMutationResponse> SuccessAsync(
        string code,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
        => new(code, message, await GetAdminViewAsync(cancellationToken), correlationId);

    private async Task<DemoContentTemplateMutationResponse> FailureAsync(
        string code,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
        => new(code, message, await GetAdminViewAsync(cancellationToken), correlationId);

    private DemoContentTemplateAdminView BuildView(
        IReadOnlyList<StoredDemoContentTemplate> stored,
        IReadOnlyDictionary<string, List<string>> usage,
        IReadOnlyList<StoredTemplateRevision> revisions)
    {
        var administered = stored.Count > 0;
        var items = administered
            ? stored.Select(template => new DemoContentTemplateItem(
                template.TemplateKey,
                template.Label,
                template.Description,
                template.Enabled,
                template.DisplayOrder,
                template.Version,
                "database",
                true,
                Iso(template.UpdatedAtUtc),
                template.UpdatedByUserId,
                template.Services
                    .Select(service => new DemoContentTemplateServiceItem(
                        service.ServiceType,
                        service.Name,
                        service.Description,
                        service.Scope))
                    .ToArray(),
                Usage(usage, template.TemplateKey))).ToArray()
            : DemoContentTemplateRegistry.All
                .Select((template, index) => new DemoContentTemplateItem(
                    template.Key,
                    template.Label,
                    string.Empty,
                    true,
                    (index + 1) * 10,
                    0,
                    "code",
                    false,
                    null,
                    null,
                    template.Services
                        .Select(service => new DemoContentTemplateServiceItem(
                            service.ServiceType,
                            service.Name,
                            service.Description,
                            service.Scope))
                        .ToArray(),
                    Usage(usage, template.Key)))
                .ToArray();

        return new DemoContentTemplateAdminView(
            items,
            ServiceTypeRegistry.Known.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            revisions
                .Select(revision => new DemoContentTemplateRevisionItem(
                    revision.Key,
                    revision.Version,
                    revision.Outcome,
                    revision.ActorUserId,
                    revision.CorrelationId,
                    Iso(revision.CreatedAtUtc)))
                .ToArray(),
            administered ? "database" : "code",
            _repository.IsPersistent,
            DemoContentTemplateRegistry.CommercialTermsLabel,
            BuildConversionView());
    }

    // La destination de conversion n'est pas editable ici : elle deplace de
    // vraies identites AD, et une valeur hors racines autorisees serait de toute
    // facon refusee au moment du deplacement. L'administration la voit, avec le
    // verdict de validation, et la corrige sur la machine.
    private DemoConversionTargetView BuildConversionView()
    {
        var target = _conversionConfiguration.TargetOrganizationalUnitDn;
        return new DemoConversionTargetView(
            "DEMO_CONVERSION_TARGET_OU_DN",
            target,
            !string.IsNullOrWhiteSpace(target),
            !string.IsNullOrWhiteSpace(target)
                && _adConfiguration.IsWithinAllowedRoots(target),
            _adConfiguration.AllowedRoots,
            _adConfiguration.ModeName,
            "restart_required",
            true);
    }

    private static IReadOnlyList<string> Usage(
        IReadOnlyDictionary<string, List<string>> usage,
        string templateKey)
        => usage.TryGetValue(templateKey, out var profiles)
            ? profiles.OrderBy(item => item, StringComparer.Ordinal).ToArray()
            : [];

    private static DemoContentTemplate ToDomain(StoredDemoContentTemplate template)
        => new(
            template.TemplateKey,
            template.Label,
            template.Services
                .Select(service => new DemoTemplateService(
                    service.ServiceType,
                    service.Name,
                    service.Description,
                    service.Scope))
                .ToArray());

    private static string Iso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");
}
