using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public sealed record ValidatedPublicPackCatalogContent(
    string PageEyebrow,
    string PageTitle,
    string PageDescription,
    string ComparisonColumnLabel,
    string FootnotePrimary,
    string FootnoteSecondary,
    IReadOnlyList<PublicPackPresentation> Packs,
    IReadOnlyList<PublicPackComparisonRow> ComparisonRows);

public interface IPublicPackCatalogService
{
    bool IsPersistent { get; }

    Task<PublicPackCatalogContent> GetAsync(CancellationToken cancellationToken);

    Task<PublicPackCatalogMutationResponse> UpsertAsync(
        PublicPackCatalogContentPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed partial class PublicPackCatalogService : IPublicPackCatalogService
{
    private const int MaxTitleLength = 200;
    private const int MaxTextLength = 4000;
    private const int MaxShortTextLength = 160;
    private const int MaxItemsPerList = 12;
    private readonly IPublicPackCatalogRepository _repository;

    private static readonly string[] AllowedPackCodes =
    [
        "pack-dossier-securise",
        "pack-acces-distance",
        "pack-bureau-windows-distance",
        "pack-pro-association"
    ];

    private static readonly IReadOnlyDictionary<string, PublicPackPresentation>
        DefaultPacks =
            new Dictionary<string, PublicPackPresentation>(
                StringComparer.Ordinal)
            {
                ["pack-dossier-securise"] = new(
                    "pack-dossier-securise",
                    "Pack Dossier Sécurisé",
                    "Dossier Sécurisé",
                    "Vos fichiers essentiels restent accessibles et sauvegardés.",
                    "Pour une personne qui veut un dossier personnel simple et protégé.",
                    "Un espace de fichiers à distance, sécurisé et sauvegardé, sans jargon technique à gérer.",
                    [
                        "Dossier personnel sécurisé 32 Go",
                        "Accès à distance aux fichiers",
                        "Sauvegarde régulière",
                        "Support de base"
                    ],
                    [
                        "32 Go de stockage personnel",
                        "Accès distant à vos documents",
                        "Sauvegardes planifiées",
                        "Aide de base en cas de besoin"
                    ],
                    null,
                    10),
                ["pack-acces-distance"] = new(
                    "pack-acces-distance",
                    "Pack Accès à Distance",
                    "Accès à Distance",
                    "Travaillez à distance avec un accès privé supervisé.",
                    "Pour une personne qui veut retrouver ses fichiers via un accès plus encadré.",
                    "La base du dossier sécurisé, enrichie d'un accès VPN personnel et d'une supervision légère.",
                    [
                        "Tout le pack Dossier Sécurisé",
                        "Accès VPN personnel",
                        "Supervision du service",
                        "Support niveau 1"
                    ],
                    [
                        "Stockage personnel et sauvegarde",
                        "VPN personnel pour se connecter",
                        "Supervision du service",
                        "Support niveau 1"
                    ],
                    null,
                    20),
                ["pack-bureau-windows-distance"] = new(
                    "pack-bureau-windows-distance",
                    "Pack Bureau Windows à Distance",
                    "Bureau Windows",
                    "Un environnement Windows distant prêt à l'emploi.",
                    "Pour retrouver un bureau Windows complet depuis l'extérieur.",
                    "Un bureau Windows à distance avec accès VPN, stockage, sauvegarde et suivi du service.",
                    [
                        "Bureau Windows à distance",
                        "Accès VPN personnel",
                        "Stockage 32 Go et sauvegarde",
                        "Supervision et support niveau 1"
                    ],
                    [
                        "Accès à un bureau Windows distant",
                        "VPN personnel inclus",
                        "32 Go de stockage et sauvegardes",
                        "Supervision et support niveau 1"
                    ],
                    null,
                    30),
                ["pack-pro-association"] = new(
                    "pack-pro-association",
                    "Pack Pro / Association",
                    "Pro / Association",
                    "Une base complète pour une petite structure ou une association.",
                    "Pour une petite équipe qui veut une offre plus large et encadrée.",
                    "Une formule plus complète pour une petite structure, avec plus de capacité et une documentation simplifiée.",
                    [
                        "2 utilisateurs et 64 Go de stockage",
                        "Accès VPN personnel",
                        "Sauvegarde et supervision",
                        "Support niveau 1 et documentation simplifiée"
                    ],
                    [
                        "Base de stockage et capacité additionnelle",
                        "VPN personnel",
                        "Sauvegarde et supervision",
                        "Support niveau 1 et documentation"
                    ],
                    null,
                    40)
            };

    public PublicPackCatalogService(IPublicPackCatalogRepository repository)
    {
        _repository = repository;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public async Task<PublicPackCatalogContent> GetAsync(
        CancellationToken cancellationToken)
        => await _repository.GetAsync(cancellationToken)
            ?? CreateDefaultContent();

    public Task<PublicPackCatalogMutationResponse> UpsertAsync(
        PublicPackCatalogContentPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.UpsertAsync(
            ValidatePayload(payload),
            correlationId,
            cancellationToken);

    private static PublicPackCatalogContent CreateDefaultContent()
        => new(
            "Catalogue packs",
            "Des packs simples, lisibles et prêts à activer",
            "Comparez les packs, choisissez votre durée d'engagement, puis lancez votre demande sans avoir à comprendre les briques techniques internes.",
            "Fonctionnalités clés",
            "Les tarifs affichés sont hors taxes et correspondent au catalogue public actuel. Le détail technique reste géré en interne pour le provisionnement et le support.",
            "Besoin d'un accompagnement spécifique ? Passez par le formulaire de contact.",
            AllowedPackCodes.Select(code => DefaultPacks[code]).ToArray(),
            [
                CreateComparisonRow(
                    "storage",
                    "Stockage sécurisé inclus",
                    10,
                    ("pack-dossier-securise", "text", "32 Go"),
                    ("pack-acces-distance", "text", "32 Go"),
                    ("pack-bureau-windows-distance", "text", "32 Go"),
                    ("pack-pro-association", "text", "64 Go")),
                CreateComparisonRow(
                    "remote-files",
                    "Accès distant aux fichiers",
                    20,
                    ("pack-dossier-securise", "included", null),
                    ("pack-acces-distance", "included", null),
                    ("pack-bureau-windows-distance", "included", null),
                    ("pack-pro-association", "included", null)),
                CreateComparisonRow(
                    "vpn",
                    "Accès VPN personnel",
                    30,
                    ("pack-dossier-securise", "excluded", null),
                    ("pack-acces-distance", "included", null),
                    ("pack-bureau-windows-distance", "included", null),
                    ("pack-pro-association", "included", null)),
                CreateComparisonRow(
                    "backup",
                    "Sauvegarde régulière",
                    40,
                    ("pack-dossier-securise", "included", null),
                    ("pack-acces-distance", "included", null),
                    ("pack-bureau-windows-distance", "included", null),
                    ("pack-pro-association", "included", null)),
                CreateComparisonRow(
                    "supervision",
                    "Supervision du service",
                    50,
                    ("pack-dossier-securise", "excluded", null),
                    ("pack-acces-distance", "included", null),
                    ("pack-bureau-windows-distance", "included", null),
                    ("pack-pro-association", "included", null)),
                CreateComparisonRow(
                    "windows-desktop",
                    "Bureau Windows à distance",
                    60,
                    ("pack-dossier-securise", "excluded", null),
                    ("pack-acces-distance", "excluded", null),
                    ("pack-bureau-windows-distance", "included", null),
                    ("pack-pro-association", "excluded", null)),
                CreateComparisonRow(
                    "support",
                    "Support inclus",
                    70,
                    ("pack-dossier-securise", "text", "Base"),
                    ("pack-acces-distance", "text", "Niveau 1"),
                    ("pack-bureau-windows-distance", "text", "Niveau 1"),
                    ("pack-pro-association", "text", "Niveau 1")),
                CreateComparisonRow(
                    "users",
                    "Utilisateurs inclus",
                    80,
                    ("pack-dossier-securise", "text", "1"),
                    ("pack-acces-distance", "text", "1"),
                    ("pack-bureau-windows-distance", "text", "1"),
                    ("pack-pro-association", "text", "2")),
                CreateComparisonRow(
                    "documentation",
                    "Documentation simplifiée",
                    90,
                    ("pack-dossier-securise", "excluded", null),
                    ("pack-acces-distance", "excluded", null),
                    ("pack-bureau-windows-distance", "excluded", null),
                    ("pack-pro-association", "included", null))
            ],
            null);

    private static PublicPackComparisonRow CreateComparisonRow(
        string id,
        string label,
        int sortOrder,
        params (string PackCode, string Kind, string? Text)[] values)
        => new(
            id,
            label,
            sortOrder,
            values.ToDictionary(
                item => item.PackCode,
                item => new PublicPackComparisonValue(item.Kind, item.Text),
                StringComparer.Ordinal));

    private static ValidatedPublicPackCatalogContent ValidatePayload(
        PublicPackCatalogContentPayload payload)
    {
        var pageEyebrow = ValidateText(payload.PageEyebrow, 2, MaxShortTextLength);
        var pageTitle = ValidateText(payload.PageTitle, 3, MaxTitleLength);
        var pageDescription = ValidateText(payload.PageDescription, 10, MaxTextLength);
        var comparisonColumnLabel = ValidateText(
            payload.ComparisonColumnLabel,
            2,
            MaxShortTextLength);
        var footnotePrimary = ValidateText(payload.FootnotePrimary, 10, MaxTextLength);
        var footnoteSecondary = ValidateText(
            payload.FootnoteSecondary,
            3,
            MaxTextLength);
        var packs = ValidatePacks(payload.Packs);
        var comparisonRows = ValidateComparisonRows(payload.ComparisonRows);

        return new ValidatedPublicPackCatalogContent(
            pageEyebrow,
            pageTitle,
            pageDescription,
            comparisonColumnLabel,
            footnotePrimary,
            footnoteSecondary,
            packs,
            comparisonRows);
    }

    private static IReadOnlyList<PublicPackPresentation> ValidatePacks(
        IReadOnlyList<PublicPackPresentation>? packs)
    {
        if (packs is null || packs.Count != AllowedPackCodes.Length)
        {
            throw new PortalValidationException();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var validated = new List<PublicPackPresentation>(packs.Count);
        foreach (var pack in packs)
        {
            var packCode = Normalize(pack.PackCode);
            if (packCode is null
                || !AllowedPackCodes.Contains(packCode, StringComparer.Ordinal)
                || !seen.Add(packCode))
            {
                throw new PortalValidationException();
            }

            if (pack.DisplayOrder is < 0 or > 100000)
            {
                throw new PortalValidationException();
            }

            validated.Add(
                new PublicPackPresentation(
                    packCode,
                    ValidateText(pack.Label, 3, MaxTitleLength),
                    ValidateText(pack.ShortLabel, 2, MaxShortTextLength),
                    ValidateText(pack.Headline, 3, MaxTitleLength),
                    ValidateText(pack.Audience, 3, MaxTextLength),
                    ValidateText(pack.Description, 3, MaxTextLength),
                    ValidateList(pack.Highlights),
                    ValidateList(pack.Included),
                    ValidateOptionalText(pack.HighlightLabel, 1, 60),
                    pack.DisplayOrder));
        }

        return validated
            .OrderBy(pack => pack.DisplayOrder)
            .ThenBy(pack => pack.PackCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PublicPackComparisonRow> ValidateComparisonRows(
        IReadOnlyList<PublicPackComparisonRow>? rows)
    {
        if (rows is null || rows.Count == 0 || rows.Count > 30)
        {
            throw new PortalValidationException();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var validated = new List<PublicPackComparisonRow>(rows.Count);
        foreach (var row in rows)
        {
            var rowId = Normalize(row.Id);
            if (rowId is null
                || row.SortOrder is < 0 or > 100000
                || !ComparisonRowIdPattern().IsMatch(rowId)
                || !seen.Add(rowId))
            {
                throw new PortalValidationException();
            }

            if (row.Values.Count != AllowedPackCodes.Length)
            {
                throw new PortalValidationException();
            }

            var values = new Dictionary<string, PublicPackComparisonValue>(
                StringComparer.Ordinal);
            foreach (var packCode in AllowedPackCodes)
            {
                if (!row.Values.TryGetValue(packCode, out var value))
                {
                    throw new PortalValidationException();
                }

                var kind = Normalize(value.Kind);
                if (kind is not ("included" or "excluded" or "text"))
                {
                    throw new PortalValidationException();
                }

                var text = kind == "text"
                    ? ValidateText(value.Text, 1, 80)
                    : ValidateOptionalText(value.Text, 0, 80);
                values[packCode] = new PublicPackComparisonValue(
                    kind,
                    kind == "text" ? text : text);
            }

            validated.Add(
                new PublicPackComparisonRow(
                    rowId,
                    ValidateText(row.Label, 2, MaxShortTextLength),
                    row.SortOrder,
                    values));
        }

        return validated
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ValidateList(IReadOnlyList<string> values)
    {
        if (values.Count == 0 || values.Count > MaxItemsPerList)
        {
            throw new PortalValidationException();
        }

        return values
            .Select(item => ValidateText(item, 2, MaxShortTextLength))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string ValidateText(string? value, int minLength, int maxLength)
    {
        var normalized = Normalize(value);
        if (normalized is null
            || normalized.Length < minLength
            || normalized.Length > maxLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? ValidateOptionalText(
        string? value,
        int minLength,
        int maxLength)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length < minLength || normalized.Length > maxLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    [GeneratedRegex("^[a-z0-9-]{1,64}$")]
    private static partial Regex ComparisonRowIdPattern();
}
