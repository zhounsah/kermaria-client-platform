namespace Kermaria.ApiInternal.Data.Configuration;

/// <summary>
/// Registre ferme des permissions du Centre de configuration
/// (specification, section 20).
///
/// Il existe pour repondre a une question que le code seul ne repond pas :
/// « qui peut faire quoi, et sur quelle surface ». Les permissions y sont
/// volontairement separees — un role qui n'a que l'acces au contenu editorial
/// ne doit pas heriter des mutations sensibles.
///
/// Ce registre ne **decide** rien : l'autorisation reste
/// <c>IEditorialRepository.HasAdminPermissionAsync</c>, revalidee cote
/// API-INTERNAL a chaque mutation. Il documente et rend verifiable.
/// </summary>
public sealed record SettingsPermission(
    string Code,
    string Label,
    string Description,
    // "low" | "medium" | "high" | "critical"
    string Risk,
    IReadOnlyList<string> Surfaces);

public static class SettingsPermissionRegistry
{
    private static readonly IReadOnlyList<SettingsPermission> Permissions =
    [
        new(
            "settings.read",
            "Lecture du Centre de configuration",
            "Consulter les parametres, l'etat des integrations et la vue runtime. Aucun secret n'est jamais lisible, quelle que soit la permission.",
            "low",
            [
                "/admin/settings",
                "/admin/settings/messages",
                "/admin/settings/diagnostic",
                "/admin/settings/billing",
                "/admin/settings/demonstrations",
                "/admin/settings/integrations",
                "/admin/settings/runtime",
                "/admin/settings/directory",
                "/admin/settings/audit"
            ]),
        new(
            "settings.write",
            "Modification des parametres applicatifs",
            "Modifier les parametres du registre ferme. Un parametre verrouille par le code reste refuse, meme avec cette permission.",
            "medium",
            ["/admin/settings"]),
        new(
            "settings.templates.write",
            "Modification des communications",
            "Modifier les modeles d'e-mail, de notification et les fragments systeme, et declencher un envoi de test.",
            "medium",
            ["/admin/settings/messages"]),
        new(
            "settings.diagnostic.write",
            "Modification du diagnostic",
            "Enregistrer un brouillon et publier le parcours de diagnostic. Une publication change ce que voient de vrais prospects.",
            "high",
            ["/admin/settings/diagnostic"]),
        new(
            "settings.billing.write",
            "Modification fiscale",
            "Planifier ou annuler une mention fiscale. Une mention erronee sur une facture engage l'entreprise ; le calcul de la taxe reste hors de portee.",
            "critical",
            ["/admin/settings/billing"]),
        new(
            "settings.demo.write",
            "Modification des modeles de demonstration",
            "Creer, modifier et supprimer les modeles semes sur les comptes de demonstration.",
            "medium",
            ["/admin/settings/demonstrations"]),
        new(
            "settings.integrations.test",
            "Test des integrations",
            "Declencher l'envoi de test SMTP. L'envoi reste borne par l'allowlist : il ne peut pas atteindre un vrai client.",
            "high",
            ["/admin/settings/integrations"]),
    ];

    public static IReadOnlyList<SettingsPermission> All => Permissions;

    public static bool Contains(string? code)
        => code is not null
            && Permissions.Any(permission => string.Equals(
                permission.Code,
                code,
                StringComparison.Ordinal));
}
