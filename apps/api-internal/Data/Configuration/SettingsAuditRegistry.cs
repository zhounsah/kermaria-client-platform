namespace Kermaria.ApiInternal.Data.Configuration;

/// <summary>
/// Registre ferme des evenements d'audit du Centre de configuration.
///
/// Il est ferme pour deux raisons. D'abord, la page d'audit filtre le journal
/// general : sans liste explicite, elle melangerait les mutations de
/// configuration a toute l'activite du portail. Ensuite, chaque evenement porte
/// ici sa categorie et son niveau de risque — une information que le journal
/// brut ne contient pas et qu'il serait faux de deduire du nom de l'action.
///
/// Ajouter un evenement ici est un acte de code, comme l'ajout de l'action
/// auditee elle-meme.
/// </summary>
public sealed record SettingsAuditAction(
    string Action,
    string Category,
    string Label,
    // "low" | "medium" | "high" | "critical" — voir specification, section 21.
    string Risk);

public static class SettingsAuditRegistry
{
    private static readonly IReadOnlyList<SettingsAuditAction> Actions =
    [
        new("setting_changed", "settings", "Parametre applicatif modifie", "medium"),
        new("email_template_changed", "communications", "Modele d'e-mail modifie", "medium"),
        new("email_template_restored", "communications", "Modele d'e-mail restaure", "low"),
        new("email_template_test", "communications", "Envoi de test d'un modele", "medium"),
        new("notification_template_changed", "communications", "Modele de notification modifie", "medium"),
        new("notification_template_restored", "communications", "Modele de notification restaure", "low"),
        new("system_snippet_changed", "communications", "Fragment systeme modifie", "low"),
        new("system_snippet_restored", "communications", "Fragment systeme restaure", "low"),
        new("diagnostic_draft_changed", "diagnostic", "Brouillon de diagnostic enregistre", "low"),
        // Publier remplace le parcours vu par de vrais prospects.
        new("diagnostic_published", "diagnostic", "Diagnostic publie", "high"),
        // Une mention erronee sur une facture engage l'entreprise.
        new("fiscal_mention_scheduled", "billing", "Mention fiscale planifiee", "critical"),
        new("fiscal_mention_cancelled", "billing", "Mention fiscale planifiee annulee", "medium"),
        new("demo_template_saved", "demonstrations", "Modele de demonstration enregistre", "medium"),
        new("demo_template_deleted", "demonstrations", "Modele de demonstration supprime", "medium"),
        new("demo_template_imported", "demonstrations", "Modeles du code recopies en base", "medium"),
        // Un envoi de test part reellement, meme borne par l'allowlist.
        new("integration_smtp_test", "integrations", "Envoi de test SMTP", "high"),
    ];

    private static readonly IReadOnlyList<string> CategoryOrder =
    [
        "settings",
        "communications",
        "diagnostic",
        "billing",
        "demonstrations",
        "integrations"
    ];

    public static IReadOnlyList<SettingsAuditAction> All => Actions;

    public static IReadOnlyList<string> Categories => CategoryOrder;

    public static IReadOnlyList<string> Risks
        => ["low", "medium", "high", "critical"];

    public static IReadOnlyList<string> ActionNames
        => Actions.Select(action => action.Action).ToArray();

    public static SettingsAuditAction? Find(string? action)
        => action is null
            ? null
            : Actions.FirstOrDefault(item => string.Equals(
                item.Action,
                action,
                StringComparison.Ordinal));

    /// <summary>
    /// Actions retenues par un filtre categorie / risque. Un filtre inconnu ne
    /// selectionne rien : il vaut mieux une liste vide qu'un filtre ignore, qui
    /// laisserait croire que l'exhaustivite a ete verifiee.
    /// </summary>
    public static IReadOnlyList<string> Resolve(string? category, string? risk)
        => Actions
            .Where(action => category is null
                || string.Equals(action.Category, category, StringComparison.Ordinal))
            .Where(action => risk is null
                || string.Equals(action.Risk, risk, StringComparison.Ordinal))
            .Select(action => action.Action)
            .ToArray();
}
