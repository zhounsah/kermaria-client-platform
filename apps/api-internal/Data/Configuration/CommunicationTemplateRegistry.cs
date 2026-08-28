using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services.Email;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Substitution stricte de variables <c>{{nom}}</c>. Ce n'est volontairement
/// pas un moteur d'expressions : aucune condition, aucune boucle, aucun acces
/// a l'environnement, aucune reflection (specification, section 8.1).
/// </summary>
public static partial class CommunicationTemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9]{0,63})\s*\}\}")]
    private static partial Regex PlaceholderPattern();

    public static string Render(
        string template,
        IReadOnlyDictionary<string, string?> variables)
        => PlaceholderPattern().Replace(
            template,
            match => variables.TryGetValue(match.Groups[1].Value, out var value)
                ? value ?? string.Empty
                // Une variable hors du contexte d'appel est effacee plutot que
                // laissee visible : la sauvegarde ayant deja rejete les noms
                // inconnus, ce cas ne subsiste que pour un gabarit code.
                : string.Empty);

    /// <summary>Noms de variables reellement presents dans un gabarit.</summary>
    public static IReadOnlyList<string> ReferencedVariables(string template)
        => PlaceholderPattern()
            .Matches(template)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Vrai quand le gabarit n'utilise que des variables autorisees ET ne
    /// contient plus aucune accolade double residuelle : une variable mal
    /// orthographiee doit faire echouer la sauvegarde, pas partir en
    /// production sous forme de texte brut.
    /// </summary>
    public static bool UsesOnlyAllowedVariables(
        string template,
        IReadOnlyCollection<string> allowed)
    {
        var residual = PlaceholderPattern().Replace(
            template,
            match => allowed.Contains(match.Groups[1].Value, StringComparer.Ordinal)
                ? string.Empty
                : match.Value);
        return !residual.Contains("{{", StringComparison.Ordinal)
            && !residual.Contains("}}", StringComparison.Ordinal);
    }
}

public sealed record EmailTemplateDefinition(
    string Key,
    string DisplayName,
    string Description,
    IReadOnlyList<CommunicationTemplateVariable> Variables,
    bool TestSendSupported);

public sealed record NotificationTemplateDefinition(
    string Key,
    string DisplayName,
    string Description,
    string DefaultTitle,
    string DefaultMessage,
    IReadOnlyList<CommunicationTemplateVariable> Variables);

public sealed record SystemSnippetDefinition(
    string Key,
    string DisplayName,
    string Description,
    string DefaultBody,
    int MaxLength);

/// <summary>
/// Registre ferme des communications administrables. Une cle absente d'ici
/// n'est jamais editable ni lue, meme si une ligne existe en base.
/// </summary>
public static class CommunicationTemplateRegistry
{
    public const int MaxSubjectLength = 240;
    public const int MaxBodyLength = 8000;
    public const int MaxNotificationTitleLength = 160;
    public const int MaxNotificationMessageLength = 600;

    private static CommunicationTemplateVariable V(string name, string description)
        => new(name, description);

    private static readonly EmailTemplateDefinition[] EmailDefinitions =
    [
        new(
            EmailTemplates.SignupVerification,
            "Inscription · vérification de l'adresse",
            "Envoyé juste après la soumission du formulaire d'inscription.",
            [
                V("contactName", "Nom du contact ayant demandé l'inscription."),
                V("verificationUrl", "Lien de vérification à usage unique."),
            ],
            TestSendSupported: true),
        new(
            EmailTemplates.AccountApproved,
            "Inscription · compte validé",
            "Envoyé lorsqu'un administrateur approuve une demande d'inscription.",
            [
                V("contactName", "Nom du contact."),
                V("setPasswordUrl", "Lien de définition du mot de passe."),
            ],
            TestSendSupported: true),
        new(
            EmailTemplates.AccountRejected,
            "Inscription · demande refusée",
            "Envoyé lorsqu'une demande d'inscription n'est pas retenue.",
            [
                V("contactName", "Nom du contact."),
                V("reasonBlock", "Bloc « Motif : … » ; vide si aucun motif n'a été saisi."),
            ],
            TestSendSupported: true),
        new(
            EmailTemplates.ContactForm,
            "Vitrine · formulaire de contact",
            "Notification interne d'un message reçu depuis la page /contact.",
            [
                V("visitorName", "Nom saisi par le visiteur."),
                V("visitorEmail", "Adresse e-mail du visiteur."),
                V("subject", "Sujet saisi, ou « (sans sujet) »."),
                V("message", "Corps du message du visiteur."),
                V("offerLine", "Ligne « Formule référencée : … » ; vide sans formule."),
            ],
            TestSendSupported: true),
        new(
            EmailTemplates.InvoiceIssued,
            "Facturation · facture disponible",
            "Envoyé au client lors de l'émission d'une facture.",
            [
                V("customerName", "Nom affiché du client."),
                V("documentLabel", "Numéro fiscal, ou référence interne à défaut."),
                V("amount", "Montant total formaté, devise comprise."),
                V("portalUrl", "Lien vers le document dans l'espace client."),
            ],
            TestSendSupported: false),
        new(
            EmailTemplates.PaymentReminder,
            "Facturation · relance de paiement",
            "Envoyé lors d'une relance sur une facture impayée.",
            [
                V("customerName", "Nom affiché du client."),
                V("documentLabel", "Numéro fiscal, ou référence interne à défaut."),
                V("amount", "Montant restant dû, formaté."),
                V("portalUrl", "Lien vers le document dans l'espace client."),
            ],
            TestSendSupported: false),
        new(
            EmailTemplates.PaymentConfirmed,
            "Facturation · paiement confirmé",
            "Accusé de réception d'un règlement.",
            [
                V("customerName", "Nom affiché du client."),
                V("documentLabel", "Numéro fiscal, ou référence interne à défaut."),
                V("amount", "Montant réglé, formaté."),
            ],
            TestSendSupported: false),
    ];

    private static readonly CommunicationTemplateVariable[] RequestVariables =
    [
        V("requestId", "Identifiant de la demande concernée."),
    ];

    private static readonly NotificationTemplateDefinition[] NotificationDefinitions =
        BuildNotificationDefinitions();

    private static readonly SystemSnippetDefinition[] SnippetDefinitions =
    [
        new(
            "contact_form_confirmation",
            "Contact · confirmation d'envoi",
            "Message affiché au visiteur après l'envoi du formulaire de contact.",
            "Message envoyé. Nous reviendrons vers vous par e-mail.",
            300),
        new(
            "contact_form_privacy_notice",
            "Contact · note de confidentialité",
            "Note courte affichée sous le formulaire de contact.",
            "Vos données ne sont utilisées que pour répondre à votre message. "
            + "Aucun traceur ni cookie de mesure n'est déposé sur ce site.",
            500),
        new(
            "service_temporarily_closed",
            "Message de fermeture temporaire",
            "Texte réutilisable lorsqu'un parcours est temporairement suspendu.",
            "Ce service est momentanément indisponible. Merci de réessayer plus tard "
            + "ou de nous contacter directement.",
            500),
        new(
            "commercial_footer_note",
            "Mention commerciale récurrente",
            "Mention affichable au bas des pages commerciales publiques.",
            "Les tarifs affichés sont recalculés à partir du catalogue au moment de la commande.",
            400),
    ];

    private static readonly Dictionary<string, EmailTemplateDefinition> EmailByKey =
        EmailDefinitions.ToDictionary(item => item.Key, StringComparer.Ordinal);

    private static readonly Dictionary<string, NotificationTemplateDefinition> NotificationByKey =
        NotificationDefinitions.ToDictionary(item => item.Key, StringComparer.Ordinal);

    private static readonly Dictionary<string, SystemSnippetDefinition> SnippetByKey =
        SnippetDefinitions.ToDictionary(item => item.Key, StringComparer.Ordinal);

    public static IReadOnlyList<EmailTemplateDefinition> EmailTemplateDefinitions
        => EmailDefinitions;

    public static IReadOnlyList<NotificationTemplateDefinition> NotificationTemplateDefinitions
        => NotificationDefinitions;

    public static IReadOnlyList<SystemSnippetDefinition> SystemSnippetDefinitions
        => SnippetDefinitions;

    public static EmailTemplateDefinition? FindEmail(string? key)
        => key is not null && EmailByKey.TryGetValue(key, out var value) ? value : null;

    public static NotificationTemplateDefinition? FindNotification(string? key)
        => key is not null && NotificationByKey.TryGetValue(key, out var value) ? value : null;

    public static SystemSnippetDefinition? FindSnippet(string? key)
        => key is not null && SnippetByKey.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Cle composee « type.statut ». Le type seul reste le repli lorsqu'aucun
    /// texte specifique n'est defini pour un statut.
    /// </summary>
    public static string NotificationKeyForStatus(string notificationType, string status)
        => $"{notificationType}.{status}";

    private static NotificationTemplateDefinition[] BuildNotificationDefinitions()
    {
        var definitions = new List<NotificationTemplateDefinition>
        {
            new(
                "support_status_changed",
                "Support · changement de statut (générique)",
                "Utilisé quand aucun texte spécifique n'existe pour le statut atteint.",
                "Mise à jour de votre demande support",
                "Le statut de votre demande support a été mis à jour.",
                RequestVariables),
            new(
                "service_status_changed",
                "Service · changement de statut (générique)",
                "Utilisé quand aucun texte spécifique n'existe pour le statut atteint.",
                "Mise à jour de votre demande de service",
                "Le statut de votre demande de service a été mis à jour.",
                RequestVariables),
            new(
                "support_public_message",
                "Support · nouveau message",
                "Envoyé lorsqu'un message visible du client est ajouté à une demande support.",
                "Nouveau message sur votre demande support",
                "Un nouveau message est disponible sur votre demande.",
                RequestVariables),
            new(
                "service_public_message",
                "Service · nouveau message",
                "Envoyé lorsqu'un message visible du client est ajouté à une demande de service.",
                "Nouveau message sur votre demande de service",
                "Un nouveau message est disponible sur votre demande de service.",
                RequestVariables),
        };

        foreach (var (status, message) in new[]
        {
            ("open", "Votre demande support est ouverte."),
            ("in_progress", "Votre demande support est en cours de traitement."),
            ("waiting_for_customer", "Votre demande support est en attente de votre retour."),
            ("resolved", "Votre demande support a été indiquée comme résolue."),
            ("closed", "Votre demande support a été clôturée."),
            ("cancelled", "Votre demande support a été annulée."),
        })
        {
            definitions.Add(new(
                NotificationKeyForStatus("support_status_changed", status),
                $"Support · statut « {status} »",
                "Texte affiché au client lorsque la demande atteint ce statut.",
                "Mise à jour de votre demande support",
                message,
                RequestVariables));
        }

        foreach (var (status, message) in new[]
        {
            ("received", "Votre demande de service a été reçue."),
            ("under_review", "Votre demande de service est en cours d'étude."),
            ("accepted", "Votre demande de service a été acceptée. Elle sera traitée manuellement."),
            ("rejected", "Votre demande de service n'a pas été retenue."),
            ("cancelled", "Votre demande de service a été annulée."),
            ("completed", "Le traitement manuel de votre demande de service est terminé."),
        })
        {
            definitions.Add(new(
                NotificationKeyForStatus("service_status_changed", status),
                $"Service · statut « {status} »",
                "Texte affiché au client lorsque la demande atteint ce statut.",
                "Mise à jour de votre demande de service",
                message,
                RequestVariables));
        }

        return definitions.ToArray();
    }
}
