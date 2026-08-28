using System.Globalization;

namespace Kermaria.ApiInternal.Services.Email;

/// <summary>
/// Modeles transactionnels integres au code. Ils restent l'autorite de repli :
/// une panne SQL ou une ligne absente de <c>email_templates</c> ne doit jamais
/// empecher l'envoi d'un e-mail critique (specification, section 8.1).
/// </summary>
/// <remarks>
/// Les corps sont exprimes avec la meme syntaxe <c>{{variable}}</c> que les
/// modeles administrables : la version code et la version base partagent donc
/// le meme moteur de substitution, sans expression arbitraire ni reflection.
/// Les valeurs derivees (montant formate, libelle de document, bloc de motif)
/// sont calculees ici puis fournies comme variables, afin que l'administrateur
/// n'ait jamais besoin d'une logique conditionnelle dans le gabarit.
/// </remarks>
public static class EmailTemplates
{
    public const string InvoiceIssued = "invoice_issued";
    public const string PaymentReminder = "payment_reminder";
    public const string PaymentConfirmed = "payment_confirmed";
    public const string ContactForm = "contact_form";
    public const string SignupVerification = "signup_verification";
    public const string AccountApproved = "account_approved";
    public const string AccountRejected = "account_rejected";

    public static (string Subject, string Body) Default(string templateKey)
        => Defaults.TryGetValue(templateKey, out var value)
            ? value
            : throw new InvalidOperationException(
                $"Unknown email template '{templateKey}'.");

    /// <summary>Repli code, rendu avec les variables fournies.</summary>
    public static (string Subject, string Body) Render(
        string templateKey,
        IReadOnlyDictionary<string, string?> variables)
    {
        var (subject, body) = Default(templateKey);
        return (
            CommunicationTemplateRenderer.Render(subject, variables),
            CommunicationTemplateRenderer.Render(body, variables));
    }

    public static readonly IReadOnlyDictionary<string, (string Subject, string Body)>
        Defaults = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            [SignupVerification] = (
                "Confirmez votre adresse e-mail",
                """
                Bonjour {{contactName}},

                Merci pour votre demande d'inscription à l'espace client.

                Pour confirmer votre adresse e-mail, cliquez sur le lien
                ci-dessous (valable 24 heures) :
                {{verificationUrl}}

                Une fois votre adresse confirmée, notre équipe examinera votre
                demande avant d'ouvrir votre accès. Vous recevrez un e-mail
                dès qu'une décision sera prise.

                Si vous n'êtes pas à l'origine de cette demande, ignorez
                simplement ce message.

                Cordialement,
                Kermaria
                """),
            [AccountApproved] = (
                "Votre compte a été validé",
                """
                Bonjour {{contactName}},

                Bonne nouvelle : votre demande d'inscription a été validée par
                notre équipe.

                Pour activer votre accès, définissez votre mot de passe via le
                lien ci-dessous (valable 24 heures) :
                {{setPasswordUrl}}

                Ce lien est à usage unique. Une fois votre mot de passe défini,
                vous pourrez vous connecter à votre espace client.

                Cordialement,
                Kermaria
                """),
            [AccountRejected] = (
                "Votre demande d'inscription",
                """
                Bonjour {{contactName}},

                Après examen, nous ne sommes pas en mesure de donner suite à
                votre demande d'inscription pour le moment.
                {{reasonBlock}}
                Si vous pensez qu'il s'agit d'une erreur ou pour toute
                question, vous pouvez nous contacter directement.

                Cordialement,
                Kermaria
                """),
            [ContactForm] = (
                "[Vitrine] {{subject}}",
                """
                Nouveau message reçu depuis le formulaire de contact du site vitrine.

                De   : {{visitorName}} <{{visitorEmail}}>
                Sujet : {{subject}}
                {{offerLine}}
                Message :
                {{message}}

                ---
                Ce message a été émis depuis la page /contact. Répondez directement
                à l'adresse e-mail ci-dessus pour entrer en contact avec le visiteur.
                """),
            [InvoiceIssued] = (
                "Facture {{documentLabel}} disponible",
                """
                Bonjour {{customerName}},

                Votre facture {{documentLabel}} d'un montant de {{amount}} est disponible sur votre espace client.

                Vous pouvez la consulter et la régler en ligne ici :
                {{portalUrl}}

                Cordialement,
                Kermaria
                """),
            [PaymentReminder] = (
                "Relance facture {{documentLabel}}",
                """
                Bonjour {{customerName}},

                Sauf erreur de notre part, la facture {{documentLabel}} d'un montant de {{amount}} reste à régler.

                Vous pouvez la consulter et la régler ici :
                {{portalUrl}}

                Si le règlement a déjà été effectué, merci d'ignorer ce message.

                Cordialement,
                Kermaria
                """),
            [PaymentConfirmed] = (
                "Confirmation de paiement — facture {{documentLabel}}",
                """
                Bonjour {{customerName}},

                Nous accusons réception de votre règlement de {{amount}} pour la facture {{documentLabel}}.

                Merci pour votre paiement.

                Cordialement,
                Kermaria
                """),
        };

    public static IReadOnlyDictionary<string, string?> SignupVerificationVariables(
        string contactName,
        string verificationUrl)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["contactName"] = contactName,
            ["verificationUrl"] = verificationUrl,
        };

    public static IReadOnlyDictionary<string, string?> AccountApprovedVariables(
        string contactName,
        string setPasswordUrl)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["contactName"] = contactName,
            ["setPasswordUrl"] = setPasswordUrl,
        };

    public static IReadOnlyDictionary<string, string?> AccountRejectedVariables(
        string contactName,
        string? reason)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["contactName"] = contactName,
            // Le motif est facultatif : le bloc complet (ligne vide comprise)
            // est calcule ici pour qu'aucun gabarit n'ait besoin de condition.
            ["reasonBlock"] = string.IsNullOrWhiteSpace(reason)
                ? string.Empty
                : $"\nMotif : {reason.Trim()}\n",
        };

    public static IReadOnlyDictionary<string, string?> ContactFormVariables(
        string visitorName,
        string visitorEmail,
        string subjectLine,
        string message,
        string? formuleCode)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["visitorName"] = visitorName,
            ["visitorEmail"] = visitorEmail,
            ["subject"] = string.IsNullOrWhiteSpace(subjectLine)
                ? "(sans sujet)"
                : subjectLine.Trim(),
            ["message"] = message.Trim(),
            ["offerLine"] = string.IsNullOrWhiteSpace(formuleCode)
                ? string.Empty
                : $"Formule référencée : {formuleCode}\n",
        };

    public static IReadOnlyDictionary<string, string?> DocumentVariables(
        string customerName,
        string documentReference,
        string? fiscalNumber,
        int totalAmountCents,
        string currency,
        string? portalUrl)
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["customerName"] = customerName,
            ["documentLabel"] = fiscalNumber ?? documentReference,
            ["amount"] = FormatAmount(totalAmountCents, currency),
        };
        if (portalUrl is not null)
        {
            variables["portalUrl"] = portalUrl;
        }

        return variables;
    }

    private static string FormatAmount(int amountCents, string currency)
    {
        var amount = amountCents / 100m;
        var formatted = amount.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"));
        return $"{formatted} {currency.ToUpperInvariant()}";
    }
}
