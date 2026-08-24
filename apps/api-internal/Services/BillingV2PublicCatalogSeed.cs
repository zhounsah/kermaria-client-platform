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
///
/// Les libelles portent les memes accents que 048. Sans eux, ce repli - qui
/// est ce que voit reellement un visiteur tant que le schema V2 n'est pas
/// applique - affichait « Dossier securise » ou « 32 Go proteges » en pleine
/// vitrine. Le nom commercial destine au client ne se corrige pas ici mais
/// dans la couche d'affichage du portail, sinon les deux sources
/// divergeraient.
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
            Commitments());

    /// <summary>
    /// Reprise exacte de `billing_v2_commitment_payment_options` (migration
    /// 048) : FLEX n'autorise que le mensuel, 6 et 12 mois portent chacun une
    /// remise differente selon le mode de reglement.
    /// </summary>
    public static IReadOnlyList<BillingV2PublicCommitment> Commitments()
        =>
        [
            new("FLEX", "Sans engagement", 1,
                [new(BillingV2PaymentModes.Monthly, 0)]),
            new("TERM-6", "Engagement 6 mois", 6,
                [
                    new(BillingV2PaymentModes.Monthly, 1000),
                    new(BillingV2PaymentModes.Upfront, 1500)
                ]),
            new("TERM-12", "Engagement 12 mois", 12,
                [
                    new(BillingV2PaymentModes.Monthly, 1500),
                    new(BillingV2PaymentModes.Upfront, 2000)
                ])
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
                "Stockage partagé",
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
                    new("16", "16 Go protégés", null, 16, 100, false),
                    new("32", "32 Go protégés", null, 32, 200, false),
                    new("64", "64 Go protégés", null, 64, 300, false),
                    new("128", "128 Go protégés", null, 128, 400, false),
                    new("256", "256 Go protégés", null, 256, 600, false),
                    new("512", "512 Go protégés", null, 512, 900, false)
                ]),
            new(
                BillingV2PublicCatalogCodes.BackupShared,
                "Sauvegarde du stockage partagé",
                "Sauvegarde",
                "subscription",
                null,
                [
                    new("32", "32 Go protégés", null, 32, 200, false),
                    new("64", "64 Go protégés", null, 64, 350, false),
                    new("128", "128 Go protégés", null, 128, 500, false),
                    new("256", "256 Go protégés", null, 256, 850, false),
                    new("512", "512 Go protégés", null, 512, 1200, false)
                ]),
            new(
                BillingV2PublicCatalogCodes.VpnAccess,
                "Accès VPN",
                "Accès",
                "user",
                null,
                [
                    new(
                        "ESSENTIAL",
                        "VPN Essentiel",
                        "Pour l'accès sécurisé aux fichiers et les usages courants.",
                        100,
                        390,
                        true),
                    new(
                        "PLUS",
                        "VPN Plus",
                        "Pour une utilisation régulière et des transferts plus importants.",
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
                        "Pour les structures ayant des besoins réseau importants.",
                        1000,
                        1290,
                        false)
                ]),
            new(
                BillingV2PublicCatalogCodes.RemoteDesktop,
                "Accès bureau distant RDS",
                "Accès",
                "user",
                1590,
                []),
            new(
                BillingV2PublicCatalogCodes.AdditionalUser,
                "Utilisateur supplémentaire",
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
                "Dossier sécurisé",
                "Configuration recommandée : mise en service, 32 Go de stockage personnel et sauvegarde quotidienne.",
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
                "Accès sécurisé",
                "Configuration recommandée : mise en service, 32 Go de stockage personnel, sauvegarde et VPN Essentiel.",
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
                "Bureau à distance",
                "Configuration recommandée : mise en service, 64 Go de stockage personnel, sauvegarde, VPN Plus et accès au bureau Windows.",
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
                "Configuration recommandée : mise en service, stockage personnel 64 Go, espace partagé 128 Go, sauvegardes, VPN Plus, un utilisateur supplémentaire et Support Plus.",
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
