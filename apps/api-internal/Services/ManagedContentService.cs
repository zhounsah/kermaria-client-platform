using System.Text;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

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

public sealed class ManagedContentService : IManagedContentService
{
    private const int MaxBodyLength = 120_000;
    private const int MaxVersionLength = 160;
    private readonly IManagedContentRepository _repository;
    private readonly ICommercialRepository _commercialRepository;

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
                "STOCK-PERSO-32",
                "SAVE-PERSO"
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
                "STOCK-PERSO-32",
                "SAVE-PERSO",
                "ACCES-VPN",
                "SUPERV-SERVICE",
                "SUPPORT-LV1"
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
                "ACCES-RDS",
                "ACCES-VPN",
                "STOCK-PERSO-32",
                "SAVE-PERSO",
                "SUPERV-SERVICE",
                "SUPPORT-LV1"
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
                "Une formule plus complète pour une petite structure, avec plus de capacité et une documentation simplifiée.",
            TechnicalServiceReferences:
            [
                "USER-ADD",
                "STOCK-PERSO-32",
                "STOCK-SUP-32",
                "ACCES-VPN",
                "SAVE-PERSO",
                "SUPERV-SERVICE",
                "SUPPORT-LV1",
                "DOC-TECH"
            ])
    ];

    private static readonly IReadOnlyDictionary<string, ManagedContentDefinition>
        DefinitionsByKey = Definitions.ToDictionary(
            definition => definition.Key,
            StringComparer.Ordinal);

    public ManagedContentService(
        IManagedContentRepository repository,
        ICommercialRepository commercialRepository)
    {
        _repository = repository;
        _commercialRepository = commercialRepository;
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

        var offersByReference =
            missing.Any(definition => definition.ContentType == "pack_sheet")
                ? await LoadOffersByReferenceAsync(cancellationToken)
                : new Dictionary<string, CommercialOfferSummary>(StringComparer.Ordinal);

        var seedEntries = missing
            .Select(definition => definition.ContentType == "pack_sheet"
                ? CreatePackSheetSeed(definition, offersByReference)
                : CreateMarkdownFileSeed(definition))
            .ToArray();

        await _repository.SeedMissingAsync(seedEntries, cancellationToken);
    }

    private async Task<Dictionary<string, CommercialOfferSummary>>
        LoadOffersByReferenceAsync(CancellationToken cancellationToken)
    {
        return (await _commercialRepository.GetAdminCatalogAsync(cancellationToken))
            .Where(offer => !string.IsNullOrWhiteSpace(offer.ExternalReference))
            .ToDictionary(
                offer => offer.ExternalReference!,
                StringComparer.Ordinal);
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
        IReadOnlyDictionary<string, CommercialOfferSummary> offersByReference)
    {
        var linkedComponents = (definition.TechnicalServiceReferences ?? [])
            .Select(reference =>
                offersByReference.TryGetValue(reference, out var offer)
                    ? offer
                    : null)
            .Where(offer => offer is not null)
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
