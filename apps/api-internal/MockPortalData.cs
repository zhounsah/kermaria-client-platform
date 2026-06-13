using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal;

public static class MockPortalData
{
    public static ClientProfile Profile { get; } = new(
        "Zachary HOUNSA-HOUNKPA EI - Client démo",
        "CLI-DEMO-0042",
        "Contact de démonstration",
        "client.demo@example.invalid",
        "+33 0 00 00 00 00",
        "12 rue de la Démonstration",
        "44000 Nantes",
        "France",
        "active");

    public static IReadOnlyList<ServiceSummary> Services { get; } =
    [
        new(
            "svc-personal-hosting-001",
            "SVC-HDP-001",
            "Hébergement dossier personnel",
            "personal_hosting",
            "active",
            "Espace d'hébergement fictif pour un dossier personnel, selon le périmètre convenu.",
            "2026-01-15",
            "Espace personnel et accès nominatif de démonstration",
            "Selon devis"),
        new(
            "svc-backup-001",
            "SVC-SAV-004",
            "Sauvegarde dossier personnel",
            "backup",
            "active",
            "Sauvegarde planifiée avec vérifications prévues, sans garantie absolue de récupération.",
            "2026-01-15",
            "Dossier personnel inclus dans la démonstration",
            "Inclus selon périmètre"),
        new(
            "svc-vpn-001",
            "SVC-VPN-007",
            "Accès VPN privé",
            "vpn",
            "pending",
            "Accès VPN chiffré en cours de qualification, adapté au besoin exprimé.",
            null,
            "Un accès nominatif, sous réserve de validation technique",
            "Selon devis",
            "Vérifications techniques prévues avant toute activation"),
        new(
            "svc-rds-001",
            "SVC-RDS-003",
            "Accès bureau distant / RDS",
            "rds",
            "suspended",
            "Accès distant fictif suspendu dans la démonstration, sans action sur une infrastructure réelle.",
            "2025-10-20",
            "Un environnement distant défini selon le besoin",
            "Selon devis",
            "Une revue du besoin est nécessaire avant toute reprise"),
        new(
            "svc-support-001",
            "SVC-SUP-014",
            "Support technique niveau 1",
            "support",
            "active",
            "Premier niveau d'assistance et d'orientation sur les services inclus au périmètre.",
            "2026-02-01",
            "Diagnostic initial et accompagnement selon périmètre convenu",
            "Inclus selon périmètre")
    ];

    public static IReadOnlyList<InvoiceSummary> Invoices { get; } =
    [
        new(
            "inv-2026-001",
            "FACT-DEMO-2026-0042",
            "paid",
            "2026-05-03",
            "2026-05-17",
            "Mai 2026",
            96m,
            "EUR"),
        new(
            "inv-2026-002",
            "FACT-DEMO-2026-0036",
            "pending",
            "2026-06-03",
            "2026-06-17",
            "Juin 2026",
            96m,
            "EUR"),
        new(
            "inv-2026-003",
            "FACT-DEMO-2026-0030",
            "paid",
            "2026-04-03",
            "2026-04-17",
            "Avril 2026",
            96m,
            "EUR")
    ];

    public static IReadOnlyList<SupportRequestSummary> SupportRequests { get; } =
    [
        new(
            "sup-001",
            "SUP-DEMO-2026-018",
            "Vérification d'une sauvegarde planifiée",
            "open",
            "normal",
            "Sauvegarde dossier personnel",
            "2026-06-10T09:30:00Z",
            "2026-06-10T11:15:00Z"),
        new(
            "sup-002",
            "SUP-DEMO-2026-014",
            "Préparation d'un accès VPN privé",
            "in_progress",
            "high",
            "Accès VPN privé",
            "2026-06-05T14:20:00Z",
            "2026-06-11T08:45:00Z"),
        new(
            "sup-003",
            "SUP-DEMO-2026-009",
            "Mise à jour des coordonnées de contact",
            "closed",
            "low",
            "Compte client",
            "2026-05-22T10:00:00Z",
            "2026-05-23T16:30:00Z")
    ];

    public static IReadOnlyList<ServiceCatalogItem> ServiceCatalog { get; } =
    [
        new(
            "catalog-personal-hosting",
            "Hébergement de dossiers personnels",
            "Hébergement",
            "Mise à disposition d'un espace adapté au volume et aux usages convenus.",
            "Dimensionnement et modalités d'accès à définir",
            "Selon devis"),
        new(
            "catalog-backup",
            "Sauvegarde de données",
            "Continuité",
            "Plan de sauvegarde adapté au besoin, avec vérifications prévues. Aucune solution ne supprime tous les risques.",
            "Sources, fréquence et rétention à confirmer",
            "Selon devis"),
        new(
            "catalog-vpn",
            "VPN privé",
            "Accès",
            "Accès VPN chiffré étudié selon les équipements et les usages attendus.",
            "Accès nominatifs et règles réseau à définir",
            "Selon devis"),
        new(
            "catalog-rds",
            "Accès distant / RDS",
            "Environnement",
            "Solution d'accès distant dimensionnée après qualification du besoin.",
            "Utilisateurs, applications et ressources à confirmer",
            "Selon devis"),
        new(
            "catalog-intervention",
            "Intervention ponctuelle",
            "Assistance",
            "Diagnostic ou intervention ciblée sur un besoin technique identifié.",
            "Périmètre et délai convenus avant intervention",
            "Selon devis"),
        new(
            "catalog-network-advice",
            "Conseil réseau et infrastructure",
            "Conseil",
            "Analyse pragmatique et recommandations adaptées à l'environnement existant.",
            "Entretien, état des lieux et recommandations",
            "Selon devis"),
        new(
            "catalog-documentation",
            "Documentation technique simplifiée",
            "Documentation",
            "Documentation lisible des usages, procédures ou éléments techniques convenus.",
            "Sujet et niveau de détail définis ensemble",
            "Selon devis"),
        new(
            "catalog-migration",
            "Migration de données",
            "Données",
            "Préparation et accompagnement d'une migration avec contrôles adaptés au contexte.",
            "Sources, destination, volume et fenêtre à confirmer",
            "Selon devis")
    ];

    public static PortalSummary Summary { get; } = new(
        Profile.CustomerReference,
        Profile.ContactName,
        Services.Count(service => service.Status == "active"),
        Invoices.Count(invoice => invoice.Status == "pending"),
        Invoices
            .Where(invoice => invoice.Status == "pending")
            .Sum(invoice => invoice.TotalAmount),
        SupportRequests.Count(request => request.Status != "closed"),
        "2026-06-11T08:45:00Z");
}
