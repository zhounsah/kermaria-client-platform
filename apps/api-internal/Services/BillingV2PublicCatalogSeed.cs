namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Reprise fidele du catalogue seede par la migration 048.
///
/// Ce n'est PAS une deuxieme autorite tarifaire : des que les tables
/// billing_v2_* existent, le service lit la base et ignore ce fichier. Il sert
/// uniquement de repli lisible quand le schema V2 n'est pas encore applique
/// (poste de developpement sans base, base de production encore en 045), pour
/// que la conception commerciale soit visible sans monter une base locale.
///
/// Toute divergence avec 048 est un defaut : le test
/// --billing-v2-public-catalog verifie que les quatre formules retombent bien
/// sur 11,90 / 15,80 / 36,70 / 48,50 EUR.
/// </summary>
public static class BillingV2PublicCatalogSeed
{
    public const string SourceName = "seed";
    public const string DatabaseSourceName = "database";
    public const string Currency = "EUR";

    public static BillingV2PublicCatalogSnapshot Snapshot()
        => new(
            SourceName,
            Currency,
            Presets(),
            Services(),
            Commitments(),
            CheckoutRoutes());

    public static IReadOnlyList<BillingV2PublicCommitment> Commitments()
        =>
        [
            new("FLEX", "Sans engagement", 1, 0),
            new("TERM-6", "Engagement 6 mois", 6, 1000),
            new("TERM-12", "Engagement 12 mois", 12, 1500)
        ];

    /// <summary>
    /// Les douze offres legacy a paiement mensuel. Les variantes "comptant"
    /// (upfront) existent en base mais ne sont volontairement pas exposees au
    /// lancement.
    /// </summary>
    public static IReadOnlyList<BillingV2PublicCheckoutRoute> CheckoutRoutes()
        =>
        [
            new("pack-dossier-securise", "FLEX",
                "61000000-0000-0000-0000-000000000101"),
            new("pack-dossier-securise", "TERM-6",
                "61000000-0000-0000-0000-000000000102"),
            new("pack-dossier-securise", "TERM-12",
                "61000000-0000-0000-0000-000000000104"),
            new("pack-acces-distance", "FLEX",
                "61000000-0000-0000-0000-000000000106"),
            new("pack-acces-distance", "TERM-6",
                "61000000-0000-0000-0000-000000000107"),
            new("pack-acces-distance", "TERM-12",
                "61000000-0000-0000-0000-000000000109"),
            new("pack-bureau-windows-distance", "FLEX",
                "61000000-0000-0000-0000-000000000111"),
            new("pack-bureau-windows-distance", "TERM-6",
                "61000000-0000-0000-0000-000000000112"),
            new("pack-bureau-windows-distance", "TERM-12",
                "61000000-0000-0000-0000-000000000114"),
            new("pack-pro-association", "FLEX",
                "61000000-0000-0000-0000-000000000116"),
            new("pack-pro-association", "TERM-6",
                "61000000-0000-0000-0000-000000000117"),
            new("pack-pro-association", "TERM-12",
                "61000000-0000-0000-0000-000000000119")
        ];

    public static IReadOnlyList<BillingV2PublicService> Services()
        =>
        [
            new(
                BillingV2PublicCatalogCodes.BaseService,
                "Socle de service",
                "Socle",
                "subscription",
                690,
                []),
            new(
                BillingV2PublicCatalogCodes.StoragePersonal,
                "Stockage personnel",
                "Stockage",
                "user",
                null,
                [
                    new("16", "16 Go", null, 16, 200, true),
                    new("32", "32 Go", null, 32, 300, true),
                    new("64", "64 Go", null, 64, 500, true),
                    new("128", "128 Go", null, 128, 700, true),
                    new("256", "256 Go", null, 256, 990, true),
                    new("512", "512 Go", null, 512, 1590, false)
                ]),
            new(
                BillingV2PublicCatalogCodes.StorageShared,
                "Stockage partage",
                "Stockage",
                "subscription",
                null,
                [
                    new("32", "32 Go", null, 32, 390, true),
                    new("64", "64 Go", null, 64, 590, true),
                    new("128", "128 Go", null, 128, 890, true),
                    new("256", "256 Go", null, 256, 1390, true),
                    new("512", "512 Go", null, 512, 1990, false)
                ]),
            new(
                BillingV2PublicCatalogCodes.BackupPersonal,
                "Sauvegarde du stockage personnel",
                "Sauvegarde",
                "user",
                null,
                [
                    new("16", "16 Go proteges", null, 16, 100, false),
                    new("32", "32 Go proteges", null, 32, 200, false),
                    new("64", "64 Go proteges", null, 64, 300, false),
                    new("128", "128 Go proteges", null, 128, 400, false),
                    new("256", "256 Go proteges", null, 256, 600, false),
                    new("512", "512 Go proteges", null, 512, 900, false)
                ]),
            new(
                BillingV2PublicCatalogCodes.BackupShared,
                "Sauvegarde du stockage partage",
                "Sauvegarde",
                "subscription",
                null,
                [
                    new("32", "32 Go proteges", null, 32, 200, false),
                    new("64", "64 Go proteges", null, 64, 350, false),
                    new("128", "128 Go proteges", null, 128, 500, false),
                    new("256", "256 Go proteges", null, 256, 850, false),
                    new("512", "512 Go proteges", null, 512, 1200, false)
                ]),
            new(
                BillingV2PublicCatalogCodes.VpnAccess,
                "Acces VPN",
                "Acces",
                "user",
                null,
                [
                    new(
                        "ESSENTIAL",
                        "VPN Essentiel",
                        "Pour l'acces securise aux fichiers et les usages courants.",
                        100,
                        390,
                        true),
                    new(
                        "PLUS",
                        "VPN Plus",
                        "Pour une utilisation reguliere et des transferts plus importants.",
                        250,
                        590,
                        true),
                    new(
                        "PERFORMANCE",
                        "VPN Performance",
                        "Pour les usages intensifs et les transferts volumineux.",
                        500,
                        890,
                        true),
                    new(
                        "PRO",
                        "VPN Pro",
                        "Pour les structures ayant des besoins reseau importants.",
                        1000,
                        1290,
                        false)
                ]),
            new(
                BillingV2PublicCatalogCodes.RemoteDesktop,
                "Acces bureau distant RDS",
                "Acces",
                "user",
                1590,
                []),
            new(
                BillingV2PublicCatalogCodes.AdditionalUser,
                "Utilisateur supplementaire",
                "Utilisateurs",
                "user",
                390,
                []),
            new(
                BillingV2PublicCatalogCodes.SupportPlus,
                "Support Plus",
                "Support",
                "subscription",
                990,
                [])
        ];

    public static IReadOnlyList<BillingV2PublicPreset> Presets()
        =>
        [
            new(
                "pack-dossier-securise",
                "Dossier securise",
                "Configuration recommandee : socle, 32 Go de stockage personnel et sauvegarde quotidienne.",
                10,
                [
                    Item(BillingV2PublicCatalogCodes.BaseService, null,
                        "subscription", 690, editable: false),
                    Item(BillingV2PublicCatalogCodes.StoragePersonal, "32",
                        "primary_user", 300),
                    Item(BillingV2PublicCatalogCodes.BackupPersonal, "32",
                        "primary_user", 200)
                ]),
            new(
                "pack-acces-distance",
                "Acces securise",
                "Configuration recommandee : socle, 32 Go de stockage personnel, sauvegarde et VPN Essentiel.",
                20,
                [
                    Item(BillingV2PublicCatalogCodes.BaseService, null,
                        "subscription", 690, editable: false),
                    Item(BillingV2PublicCatalogCodes.StoragePersonal, "32",
                        "primary_user", 300),
                    Item(BillingV2PublicCatalogCodes.BackupPersonal, "32",
                        "primary_user", 200),
                    Item(BillingV2PublicCatalogCodes.VpnAccess, "ESSENTIAL",
                        "primary_user", 390)
                ]),
            new(
                "pack-bureau-windows-distance",
                "Bureau Windows",
                "Configuration recommandee : socle, 64 Go de stockage personnel, sauvegarde, VPN Plus et acces RDS.",
                30,
                [
                    Item(BillingV2PublicCatalogCodes.BaseService, null,
                        "subscription", 690, editable: false),
                    Item(BillingV2PublicCatalogCodes.StoragePersonal, "64",
                        "primary_user", 500),
                    Item(BillingV2PublicCatalogCodes.BackupPersonal, "64",
                        "primary_user", 300),
                    Item(BillingV2PublicCatalogCodes.VpnAccess, "PLUS",
                        "primary_user", 590),
                    Item(BillingV2PublicCatalogCodes.RemoteDesktop, null,
                        "primary_user", 1590)
                ]),
            new(
                "pack-pro-association",
                "Pro / Association",
                "Configuration recommandee : socle, stockage personnel 64 Go, espace partage 128 Go, sauvegardes, VPN Plus, un utilisateur supplementaire et Support Plus.",
                40,
                [
                    Item(BillingV2PublicCatalogCodes.BaseService, null,
                        "subscription", 690, editable: false),
                    Item(BillingV2PublicCatalogCodes.StoragePersonal, "64",
                        "primary_user", 500),
                    Item(BillingV2PublicCatalogCodes.BackupPersonal, "64",
                        "primary_user", 300),
                    Item(BillingV2PublicCatalogCodes.VpnAccess, "PLUS",
                        "primary_user", 590),
                    Item(BillingV2PublicCatalogCodes.StorageShared, "128",
                        "subscription", 890),
                    Item(BillingV2PublicCatalogCodes.BackupShared, "128",
                        "subscription", 500),
                    Item(BillingV2PublicCatalogCodes.AdditionalUser, null,
                        "additional_user", 390),
                    Item(BillingV2PublicCatalogCodes.SupportPlus, null,
                        "subscription", 990)
                ])
        ];

    private static BillingV2PublicPresetItem Item(
        string serviceCode,
        string? tierCode,
        string scopeTemplate,
        long amountCents,
        bool editable = true)
        => new(serviceCode, tierCode, scopeTemplate, 1, amountCents, editable);
}
