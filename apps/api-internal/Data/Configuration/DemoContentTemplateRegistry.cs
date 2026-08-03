using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Configuration;

/// <summary>
/// Registre declaratif (config-only, V1.1 Lot 2) des templates de contenu
/// (axe A) semables sur un compte de demo. Ajouter un template = ajouter une
/// entree ici, sans toucher au moteur de seeding. Le passage en table
/// administrable (<c>demo_content_templates</c>) reste une evolution possible.
/// </summary>
public sealed record DemoContentTemplate(
    string Key,
    string Label,
    IReadOnlyList<DemoTemplateService> Services);

/// <summary>Un service semé par un template (une ligne <c>customer_services</c>).</summary>
public sealed record DemoTemplateService(
    string ServiceType,
    string Name,
    string Description,
    string Scope);

public static class DemoContentTemplateRegistry
{
    private const string CommercialTerms = "Démonstration";

    private static readonly IReadOnlyList<DemoContentTemplate> Templates =
    [
        new DemoContentTemplate(
            "tpe",
            "TPE",
            [
                new DemoTemplateService(
                    "personal_hosting",
                    "Hébergement dossier personnel",
                    "Espace de démonstration selon le périmètre convenu.",
                    "Espace personnel et accès nominatif de démonstration"),
                new DemoTemplateService(
                    "backup",
                    "Sauvegarde dossier personnel",
                    "Sauvegarde de démonstration du dossier personnel.",
                    "Sauvegarde de démonstration"),
            ]),
        new DemoContentTemplate(
            "association",
            "Association",
            [
                new DemoTemplateService(
                    "personal_hosting",
                    "Hébergement partagé",
                    "Espace partagé de démonstration pour l'association.",
                    "Espace partagé de démonstration"),
                new DemoTemplateService(
                    "backup",
                    "Sauvegarde",
                    "Sauvegarde de démonstration.",
                    "Sauvegarde de démonstration"),
                new DemoTemplateService(
                    "vpn",
                    "Accès VPN",
                    "Accès VPN de démonstration.",
                    "Accès VPN de démonstration"),
                new DemoTemplateService(
                    "support",
                    "Support niveau 1",
                    "Support de démonstration de premier niveau.",
                    "Support de démonstration"),
            ]),
        new DemoContentTemplate(
            "pme-multisite",
            "PME multisite",
            [
                new DemoTemplateService(
                    "personal_hosting",
                    "Hébergement multisite",
                    "Espace multisite de démonstration.",
                    "Espace multisite de démonstration"),
                new DemoTemplateService(
                    "backup",
                    "Sauvegarde",
                    "Sauvegarde de démonstration.",
                    "Sauvegarde de démonstration"),
                new DemoTemplateService(
                    "vpn",
                    "Accès VPN",
                    "Accès VPN de démonstration.",
                    "Accès VPN de démonstration"),
                new DemoTemplateService(
                    "rds",
                    "Bureau distant / RDS",
                    "Accès bureau distant de démonstration.",
                    "Bureau distant de démonstration"),
                new DemoTemplateService(
                    "monitoring",
                    "Supervision",
                    "Supervision de démonstration.",
                    "Supervision de démonstration"),
            ]),
        new DemoContentTemplate(
            "ad-koxo",
            "Client avec AD/KoXo",
            [
                new DemoTemplateService(
                    "rds",
                    "Bureau distant / RDS",
                    "Accès bureau distant de démonstration.",
                    "Bureau distant de démonstration"),
                new DemoTemplateService(
                    "vpn",
                    "Accès VPN",
                    "Accès VPN de démonstration.",
                    "Accès VPN de démonstration"),
                new DemoTemplateService(
                    "storage",
                    "Stockage",
                    "Stockage de démonstration.",
                    "Stockage de démonstration"),
                new DemoTemplateService(
                    "user",
                    "Utilisateur AD",
                    "Utilisateur AD de démonstration.",
                    "Utilisateur AD de démonstration"),
            ]),
    ];

    public static string CommercialTermsLabel => CommercialTerms;

    public static IReadOnlyList<DemoContentTemplate> All => Templates;

    public static DemoContentTemplate? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim();
        return Templates.FirstOrDefault(
            template => string.Equals(
                template.Key,
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<DemoContentTemplateSummary> Summaries()
        => Templates
            .Select(template => new DemoContentTemplateSummary(
                template.Key,
                template.Label,
                template.Services.Select(service => service.Name).ToArray()))
            .ToArray();
}
