using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.Services;

public sealed record StoredManagedContentEntry(
    string Key,
    string ContentType,
    string Title,
    string PublicPath,
    string BodyMarkdown,
    string? VersionLabel,
    string? CreatedAt,
    string? UpdatedAt);

public sealed record ValidatedManagedContentEntry(
    string Key,
    string ContentType,
    string Title,
    string PublicPath,
    string BodyMarkdown,
    string? VersionLabel);

internal sealed record ManagedContentDefinition(
    string Key,
    string ContentType,
    string Title,
    string PublicPath,
    int SortOrder,
    string? PackCode = null,
    string? PackLabel = null,
    string? PackAudience = null,
    string? PackDescription = null,
    IReadOnlyList<string>? TechnicalServiceReferences = null,
    string? SeedFileName = null);

public interface IManagedContentService
{
    bool IsPersistent { get; }

    Task<ManagedContentDetail> GetPublicAsync(
        string key,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ManagedContentSummary>> GetAdminListAsync(
        CancellationToken cancellationToken);

    Task<ManagedContentDetail> GetAdminDetailAsync(
        string key,
        CancellationToken cancellationToken);

    Task<ManagedContentMutationResponse> UpsertAsync(
        string key,
        ManagedContentPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed partial class ManagedContentService : IManagedContentService
{
    private const int MaxBodyLength = 120_000;
    private const int MaxVersionLength = 160;
    private readonly IManagedContentRepository _repository;
    private readonly IServiceTopologyService _topologyService;

    private static readonly IReadOnlyList<ManagedContentDefinition> Definitions =
    [
        new(
            "legal:cgv",
            "legal",
            "Conditions générales de vente",
            "/cgv",
            10,
            SeedFileName: "cgv.md"),
        new(
            "legal:politique-confidentialite",
            "legal",
            "Politique de confidentialité",
            "/politique-confidentialite",
            15,
            SeedFileName: "politique-confidentialite.md"),
        new(
            "legal:mentions-legales",
            "legal",
            "Mentions légales",
            "/mentions-legales",
            20,
            SeedFileName: "mentions-legales.md"),
        new(
            "page:a-propos",
            "page",
            "À propos de Zachary IT",
            "/a-propos",
            30,
            SeedFileName: "a-propos.md"),
        new(
            "page:infrastructure",
            "page",
            "Infrastructure et exploitation des services Zachary IT",
            "/infrastructure",
            35,
            SeedFileName: "infrastructure.md"),
        new("storefront:services", "storefront_page", "Pages principales — Catalogue des services", "/services", 40),
        new("storefront:tarifs", "storefront_page", "Pages principales — Tarifs Zachary IT", "/tarifs", 45),
        new("storefront:cloud-hebergement", "storefront_page", "Catégories services — Cloud & Hébergement", "/services/cloud-hebergement", 50),
        new("storefront:domaines-messagerie", "storefront_page", "Catégories services — Domaines & Messagerie", "/services/domaines-messagerie", 51),
        new("storefront:reseau-securite", "storefront_page", "Catégories services — Réseau & Sécurité", "/services/reseau-securite", 52),
        new("storefront:support-it", "storefront_page", "Catégories services — Support & IT", "/services/support-it", 53),
        new("storefront:vps", "storefront_page", "Pages services SEO — VPS", "/services/vps", 60),
        new("storefront:infogerance-vps", "storefront_page", "Pages services SEO — Infogérance VPS", "/services/infogerance-vps", 61),
        new("storefront:hebergement-web", "storefront_page", "Pages services SEO — Hébergement web", "/services/hebergement-web", 62),
        new("storefront:maintenance-linux", "storefront_page", "Pages services SEO — Maintenance Linux", "/services/maintenance-linux", 63),
        new("storefront:maintenance-wordpress", "storefront_page", "Pages services SEO — Maintenance WordPress", "/services/maintenance-wordpress", 64),
        new("storefront:sauvegarde-externalisee", "storefront_page", "Pages services SEO — Sauvegarde externalisée", "/services/sauvegarde-externalisee", 65),
        new("storefront:supervision-informatique", "storefront_page", "Pages services SEO — Supervision informatique", "/services/supervision-informatique", 66),
        new("storefront:supervision-nas", "storefront_page", "Pages services SEO — Supervision NAS", "/services/supervision-nas", 67),
        new("storefront:vpn-entreprise", "storefront_page", "Pages services SEO — VPN entreprise", "/services/vpn-entreprise", 68),
        new("storefront:bureau-windows-distance", "storefront_page", "Pages services SEO — Bureau Windows à distance", "/services/bureau-windows-distance", 69),
        new("storefront:unifi", "storefront_page", "Pages services SEO — UniFi", "/services/unifi", 70),
        new("storefront:firewall", "storefront_page", "Pages services SEO — Firewall", "/services/firewall", 71),
        new("storefront:cloudflare-waf", "storefront_page", "Pages services SEO — Cloudflare WAF", "/services/cloudflare-waf", 72),
        new("storefront:gestion-dns-domaines", "storefront_page", "Pages services SEO — Gestion DNS et domaines", "/services/gestion-dns-domaines", 73),
        new("storefront:messagerie-professionnelle", "storefront_page", "Pages services SEO — Messagerie professionnelle", "/services/messagerie-professionnelle", 74),
        new(
            "pack-sheet:pack-dossier-securise",
            "pack_sheet",
            "Fiche technique - Pack Dossier Sécurisé",
            "/offres/dossier-securise",
            110,
            PackCode: "pack-dossier-securise",
            PackLabel: "Pack Dossier Sécurisé",
            PackAudience:
                "Pour une personne qui veut un dossier personnel simple et protégé.",
            PackDescription:
                "Un espace de fichiers à distance, sécurisé et sauvegardé, sans jargon technique à gérer.",
            TechnicalServiceReferences:
            [
                "STORAGE-PERSONAL",
                "BACKUP-PERSONAL"
            ]),
        new(
            "pack-sheet:pack-acces-distance",
            "pack_sheet",
            "Fiche technique - Pack Accès à Distance",
            "/offres/acces-distance",
            120,
            PackCode: "pack-acces-distance",
            PackLabel: "Pack Accès à Distance",
            PackAudience:
                "Pour une personne qui veut retrouver ses fichiers via un accès plus encadré.",
            PackDescription:
                "La base du dossier sécurisé, enrichie d'un accès VPN personnel et d'une supervision légère.",
            TechnicalServiceReferences:
            [
                "STORAGE-PERSONAL",
                "BACKUP-PERSONAL",
                "VPN-ACCESS",
                "MONITORING-INTERNAL",
                "SUPPORT-STANDARD"
            ]),
        new(
            "pack-sheet:pack-bureau-windows-distance",
            "pack_sheet",
            "Fiche technique - Pack Bureau Windows à Distance",
            "/offres/bureau-windows-distance",
            130,
            PackCode: "pack-bureau-windows-distance",
            PackLabel: "Pack Bureau Windows à Distance",
            PackAudience:
                "Pour retrouver un bureau Windows complet depuis l'extérieur.",
            PackDescription:
                "Un bureau Windows à distance avec accès VPN, stockage, sauvegarde et suivi du service.",
            TechnicalServiceReferences:
            [
                "RDS-ACCESS",
                "VPN-ACCESS",
                "STORAGE-PERSONAL",
                "BACKUP-PERSONAL",
                "MONITORING-INTERNAL",
                "SUPPORT-STANDARD"
            ]),
        new(
            "pack-sheet:pack-pro-association",
            "pack_sheet",
            "Fiche technique - Pack Pro / Association",
            "/offres/pro-association",
            140,
            PackCode: "pack-pro-association",
            PackLabel: "Pack Pro / Association",
            PackAudience:
                "Pour une petite équipe qui veut une offre plus large et encadrée.",
            PackDescription:
                "Une formule plus complète pour une petite structure, avec plus de capacité.",
            TechnicalServiceReferences:
            [
                "USER-ADDITIONAL",
                "STORAGE-PERSONAL",
                "STORAGE-SHARED",
                "VPN-ACCESS",
                "BACKUP-PERSONAL",
                "MONITORING-INTERNAL",
                "SUPPORT-STANDARD"
            ])
    ];

    private static readonly IReadOnlyDictionary<string, ManagedContentDefinition>
        DefinitionsByKey = Definitions.ToDictionary(
            definition => definition.Key,
            StringComparer.Ordinal);

    public ManagedContentService(
        IManagedContentRepository repository,
        IServiceTopologyService topologyService)
    {
        _repository = repository;
        _topologyService = topologyService;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public async Task<ManagedContentDetail> GetPublicAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var definition = ResolveDefinition(key);
        await EnsureSeededAsync([definition], cancellationToken);

        var entry = await _repository.GetAsync(definition.Key, cancellationToken);
        return entry is null
            ? throw new PortalDataNotFoundException()
            : ToDetail(definition, entry);
    }

    public async Task<IReadOnlyList<ManagedContentSummary>> GetAdminListAsync(
        CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(Definitions, cancellationToken);
        var stored = await _repository.GetAllAsync(cancellationToken);
        var byKey = stored.ToDictionary(entry => entry.Key, StringComparer.Ordinal);

        return Definitions
            .Where(definition => byKey.ContainsKey(definition.Key))
            .OrderBy(definition => definition.SortOrder)
            .Select(definition => ToSummary(definition, byKey[definition.Key]))
            .ToArray();
    }

    public async Task<ManagedContentDetail> GetAdminDetailAsync(
        string key,
        CancellationToken cancellationToken)
        => await GetPublicAsync(key, cancellationToken);

    public async Task<ManagedContentMutationResponse> UpsertAsync(
        string key,
        ManagedContentPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var definition = ResolveDefinition(key);
        await EnsureSeededAsync([definition], cancellationToken);
        var validated = ValidatePayload(definition, payload);

        return await _repository.UpsertAsync(
            validated,
            correlationId,
            cancellationToken);
    }

    private async Task EnsureSeededAsync(
        IReadOnlyList<ManagedContentDefinition> targetDefinitions,
        CancellationToken cancellationToken)
    {
        var stored = await _repository.GetAllAsync(cancellationToken);
        var missing = GetMissingDefinitions(stored, targetDefinitions);
        if (missing.Count == 0)
        {
            return;
        }

        var servicesByCode =
            missing.Any(definition => definition.ContentType == "pack_sheet")
                ? await LoadServicesByCodeAsync(cancellationToken)
                : new Dictionary<string, CatalogTechnicalServiceDefinition>(
                    StringComparer.OrdinalIgnoreCase);

        var seedEntries = missing
            .Select(definition => definition.ContentType switch
            {
                "pack_sheet" => CreatePackSheetSeed(definition, servicesByCode),
                "storefront_page" => CreateStorefrontSeed(definition),
                _ => CreateMarkdownFileSeed(definition)
            })
            .ToArray();

        await _repository.SeedMissingAsync(seedEntries, cancellationToken);
    }

    // Les references techniques citees par une fiche de pack designent des
    // services du catalogue Billing V2 (`billing_v2_services.code`).
    private async Task<Dictionary<string, CatalogTechnicalServiceDefinition>>
        LoadServicesByCodeAsync(CancellationToken cancellationToken)
    {
        return (await _topologyService.GetTechnicalServicesAsync(cancellationToken))
            .ToDictionary(
                service => service.TechnicalServiceReference,
                StringComparer.OrdinalIgnoreCase);
    }

    private static ManagedContentDefinition ResolveDefinition(string key)
    {
        var normalized = NormalizeRequiredText(key, 3, 120);
        return DefinitionsByKey.TryGetValue(normalized, out var definition)
            ? definition
            : throw new PortalValidationException();
    }

    private static IReadOnlyList<ManagedContentDefinition> GetMissingDefinitions(
        IReadOnlyList<StoredManagedContentEntry> stored,
        IReadOnlyList<ManagedContentDefinition> targetDefinitions)
    {
        var existingKeys = stored
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        return targetDefinitions
            .Where(definition => !existingKeys.Contains(definition.Key))
            .ToArray();
    }

    private static ManagedContentSummary ToSummary(
        ManagedContentDefinition definition,
        StoredManagedContentEntry entry)
        => new(
            definition.Key,
            definition.ContentType,
            definition.Title,
            definition.PublicPath,
            entry.VersionLabel,
            entry.UpdatedAt);

    private static ManagedContentDetail ToDetail(
        ManagedContentDefinition definition,
        StoredManagedContentEntry entry)
        => new(
            definition.Key,
            definition.ContentType,
            definition.Title,
            definition.PublicPath,
            entry.VersionLabel,
            entry.BodyMarkdown,
            entry.CreatedAt,
            entry.UpdatedAt);

    private static ValidatedManagedContentEntry ValidatePayload(
        ManagedContentDefinition definition,
        ManagedContentPayload payload)
    {
        var bodyMarkdown = NormalizeRequiredText(
            payload.BodyMarkdown,
            10,
            MaxBodyLength);
        var versionLabel = NormalizeOptionalText(
            payload.VersionLabel,
            MaxVersionLength);

        if (definition.ContentType == "storefront_page")
        {
            bodyMarkdown = ValidateStorefrontJson(definition.Key, bodyMarkdown);
            versionLabel = null;
        }

        return new ValidatedManagedContentEntry(
            definition.Key,
            definition.ContentType,
            definition.Title,
            definition.PublicPath,
            NormalizeMarkdown(bodyMarkdown),
            versionLabel);
    }

    private static ValidatedManagedContentEntry CreateMarkdownFileSeed(
        ManagedContentDefinition definition)
    {
        var path = ResolveSeedFilePath(definition.SeedFileName);
        var content = File.ReadAllText(path, Encoding.UTF8);
        var lines = content.Replace("\r\n", "\n").Split('\n');
        string? versionLabel = null;
        var remaining = new List<string>(lines.Length);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                continue;
            }

            if (versionLabel is null
                && (line.StartsWith("**Version du :", StringComparison.Ordinal)
                    || line.StartsWith(
                        "**Dernière mise à jour :",
                        StringComparison.Ordinal))
                && line.EndsWith("**", StringComparison.Ordinal))
            {
                versionLabel = line.Trim('*').Trim();
                continue;
            }

            remaining.Add(rawLine);
        }

        return new ValidatedManagedContentEntry(
            definition.Key,
            definition.ContentType,
            definition.Title,
            definition.PublicPath,
            NormalizeMarkdown(string.Join("\n", remaining)),
            versionLabel);
    }

    private static ValidatedManagedContentEntry CreatePackSheetSeed(
        ManagedContentDefinition definition,
        IReadOnlyDictionary<string, CatalogTechnicalServiceDefinition> servicesByCode)
    {
        var linkedComponents = (definition.TechnicalServiceReferences ?? [])
            .Select(reference =>
                servicesByCode.TryGetValue(reference, out var service)
                    ? service
                    : null)
            .Where(service => service is not null)
            .ToArray();
        var missingComponentCount =
            (definition.TechnicalServiceReferences?.Count ?? 0)
            - linkedComponents.Length;

        var builder = new StringBuilder();
        builder.AppendLine("## Présentation");
        builder.AppendLine();
        builder.AppendLine(
            definition.PackDescription
            ?? "Cette fiche technique décrit le périmètre opérationnel du pack.");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(definition.PackAudience))
        {
            builder.AppendLine($"Public visé : {definition.PackAudience}");
            builder.AppendLine();
        }

        builder.AppendLine("## Composants techniques liés");
        builder.AppendLine();
        builder.AppendLine(
            linkedComponents.Length > 0
                ? $"La composition technique liée à ce pack est calculée automatiquement à partir du catalogue commercial actif. {linkedComponents.Length} composant(s) sont actuellement rattaché(s) et affiché(s) séparément sur la page publique."
                : "La composition technique liée à ce pack est calculée automatiquement à partir du catalogue commercial actif et affichée séparément sur la page publique.");

        if (missingComponentCount > 0)
        {
            builder.AppendLine();
            builder.AppendLine(
                $"Certains composants attendus ne sont pas encore retrouvés dans le catalogue actif ({missingComponentCount} référence(s) à qualifier).");
        }

        builder.AppendLine();
        builder.AppendLine("## Pré-requis");
        builder.AppendLine();
        builder.AppendLine(
            "- Un échange de cadrage reste nécessaire pour confirmer les usages, équipements et contraintes d'accès.");
        builder.AppendLine(
            "- Les accès nominatifs, volumes de données et besoins de support doivent être validés avant activation.");
        builder.AppendLine();
        builder.AppendLine("## Limites");
        builder.AppendLine();
        builder.AppendLine(
            "- Cette fiche décrit le périmètre standard du pack et ne remplace pas un devis ou des conditions particulières.");
        builder.AppendLine(
            "- Les demandes hors périmètre, urgentes ou spécifiques peuvent nécessiter une prestation complémentaire.");
        builder.AppendLine();
        builder.AppendLine("## Support");
        builder.AppendLine();
        builder.AppendLine(
            "- Le niveau de support inclus correspond au périmètre standard affiché sur la vitrine.");
        builder.AppendLine(
            "- Les changements structurels, migrations étendues ou reprises complexes sont qualifiés séparément.");

        return new ValidatedManagedContentEntry(
            definition.Key,
            definition.ContentType,
            definition.Title,
            definition.PublicPath,
            NormalizeMarkdown(builder.ToString()),
            null);
    }

    private static ValidatedManagedContentEntry CreateStorefrontSeed(
        ManagedContentDefinition definition)
        => new(
            definition.Key,
            definition.ContentType,
            definition.Title,
            definition.PublicPath,
            StorefrontContentSeed.CreateJson(definition.Key),
            null);

    private static string ValidateStorefrontJson(string definitionKey, string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !HasText(root, "title", 3, 200)
                || !HasText(root, "lead", 10, 1200)
                || !HasText(root, "seoTitle", 10, 200)
                || !HasText(root, "seoDescription", 30, 400)
                || !HasText(root, "ctaLabel", 3, 80)
                || !HasAllowedRoute(root, "ctaHref")
                || !HasObjectArray(root, "sections", 1, 12, "heading", "bodyMarkdown")
                || !HasObjectArray(root, "faq", 2, 12, "question", "answer")
                || !HasObjectArray(root, "relatedLinks", 1, 12, "label", "href", routeField: "href"))
            {
                throw new PortalValidationException();
            }

            // Les montants restent exclusivement dans Billing. Le contenu peut
            // dire « Sur devis » mais ne peut pas introduire un prix public.
            if (definitionKey == "storefront:services"
                && (!HasServicesLandingProblemEntries(root)
                    || !HasServicesLandingCategories(root)))
            {
                throw new PortalValidationException();
            }
            if (CurrencyAmountPattern().IsMatch(value))
            {
                throw new PortalValidationException();
            }

            return JsonSerializer.Serialize(root);
        }
        catch (JsonException)
        {
            throw new PortalValidationException();
        }
    }

    private static bool HasText(JsonElement parent, string property, int min, int max)
        => parent.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { } text
            && text.Trim().Length >= min
            && text.Trim().Length <= max;

    private static bool HasAllowedRoute(JsonElement parent, string property)
        => parent.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { } route
            && route.StartsWith("/", StringComparison.Ordinal)
            && !route.StartsWith("//", StringComparison.Ordinal)
            && route.Length <= 160;

    private static bool HasObjectArray(
        JsonElement parent,
        string property,
        int min,
        int max,
        string firstTextProperty,
        string secondTextProperty,
        string? routeField = null)
    {
        if (!parent.TryGetProperty(property, out var values)
            || values.ValueKind != JsonValueKind.Array
            || values.GetArrayLength() < min
            || values.GetArrayLength() > max)
        {
            return false;
        }

        return values.EnumerateArray().All(item => item.ValueKind == JsonValueKind.Object
            && HasText(item, firstTextProperty, 2, 4000)
            && (routeField is null
                ? HasText(item, secondTextProperty, 3, 12000)
                : HasAllowedRoute(item, routeField)));
    }
    private static bool HasServicesLandingProblemEntries(JsonElement root)
    {
        if (!root.TryGetProperty("problemEntries", out var values)
            || values.ValueKind != JsonValueKind.Array
            || values.GetArrayLength() != 6)
        {
            return false;
        }

        var destinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in values.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !HasText(item, "title", 3, 120)
                || !HasText(item, "description", 10, 400)
                || !item.TryGetProperty("href", out var hrefValue)
                || hrefValue.ValueKind != JsonValueKind.String
                || hrefValue.GetString() is not { } href
                || !IsAllowedServicesProblemRoute(href)
                || !destinations.Add(href))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasServicesLandingCategories(JsonElement root)
    {
        if (!root.TryGetProperty("relatedLinks", out var values)
            || values.ValueKind != JsonValueKind.Array
            || values.GetArrayLength() != 4)
        {
            return false;
        }

        var destinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in values.EnumerateArray())
        {
            if (!item.TryGetProperty("href", out var hrefValue)
                || hrefValue.ValueKind != JsonValueKind.String
                || hrefValue.GetString() is not { } href
                || !IsServicesCategoryRoute(href)
                || !destinations.Add(href))
            {
                return false;
            }
        }

        return destinations.Count == 4;
    }

    private static bool IsAllowedServicesProblemRoute(string route)
        => route is "/services/messagerie-professionnelle"
            or "/services/sauvegarde-externalisee"
            or "/vpn-ou-bureau-a-distance-que-choisir"
            or "/services/unifi"
            or "/services/cloud-hebergement"
            or "/services/support-it";

    private static bool IsServicesCategoryRoute(string route)
        => route is "/services/cloud-hebergement"
            or "/services/domaines-messagerie"
            or "/services/reseau-securite"
            or "/services/support-it";
    [GeneratedRegex(@"(?<![\p{L}\p{N}])\d{1,6}(?:[.,]\d{1,2})?\s*(?:\u20AC|euros?|eur)(?![\p{L}])", RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyAmountPattern();

    private static string ResolveSeedFilePath(string? seedFileName)
    {
        if (string.IsNullOrWhiteSpace(seedFileName))
        {
            throw new PortalValidationException();
        }

        return Path.Combine(AppContext.BaseDirectory, "SeedContent", seedFileName);
    }

    private static string NormalizeMarkdown(string value)
    {
        var normalized = value.Replace("\r\n", "\n").Trim();
        if (normalized.Length == 0)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string NormalizeRequiredText(
        string? value,
        int minLength,
        int maxLength)
    {
        var normalized = NormalizeOptionalText(value, maxLength);
        if (normalized is null || normalized.Length < minLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }
}
