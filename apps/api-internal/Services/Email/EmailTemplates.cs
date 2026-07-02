using System.Globalization;

namespace Kermaria.ApiInternal.Services.Email;

public static class EmailTemplates
{
    public const string InvoiceIssued = "invoice_issued";
    public const string PaymentReminder = "payment_reminder";
    public const string PaymentConfirmed = "payment_confirmed";
    public const string ContactForm = "contact_form";
    public const string SignupVerification = "signup_verification";
    public const string AccountApproved = "account_approved";
    public const string AccountRejected = "account_rejected";

    public static (string Subject, string Body) RenderSignupVerification(
        string contactName,
        string verificationUrl)
    {
        var subject = "Confirmez votre adresse e-mail";
        var body = $"""
            Bonjour {contactName},

            Merci pour votre demande d'inscription à l'espace client.

            Pour confirmer votre adresse e-mail, cliquez sur le lien
            ci-dessous (valable 24 heures) :
            {verificationUrl}

            Une fois votre adresse confirmée, notre équipe examinera votre
            demande avant d'ouvrir votre accès. Vous recevrez un e-mail
            dès qu'une décision sera prise.

            Si vous n'êtes pas à l'origine de cette demande, ignorez
            simplement ce message.

            Cordialement,
            Kermaria
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) RenderAccountApproved(
        string contactName,
        string setPasswordUrl)
    {
        var subject = "Votre compte a été validé";
        var body = $"""
            Bonjour {contactName},

            Bonne nouvelle : votre demande d'inscription a été validée par
            notre équipe.

            Pour activer votre accès, définissez votre mot de passe via le
            lien ci-dessous (valable 24 heures) :
            {setPasswordUrl}

            Ce lien est à usage unique. Une fois votre mot de passe défini,
            vous pourrez vous connecter à votre espace client.

            Cordialement,
            Kermaria
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) RenderAccountRejected(
        string contactName,
        string? reason)
    {
        var subject = "Votre demande d'inscription";
        var reasonLine = string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : $"\nMotif : {reason.Trim()}\n";
        var body = $"""
            Bonjour {contactName},

            Après examen, nous ne sommes pas en mesure de donner suite à
            votre demande d'inscription pour le moment.
            {reasonLine}
            Si vous pensez qu'il s'agit d'une erreur ou pour toute
            question, vous pouvez nous contacter directement.

            Cordialement,
            Kermaria
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) RenderContactForm(
        string visitorName,
        string visitorEmail,
        string subjectLine,
        string message,
        string? offerReference)
    {
        var trimmedSubject = string.IsNullOrWhiteSpace(subjectLine)
            ? "(sans sujet)"
            : subjectLine.Trim();
        var subject = $"[Vitrine] {trimmedSubject}";
        var offerLine = string.IsNullOrWhiteSpace(offerReference)
            ? string.Empty
            : $"Offre référencée : {offerReference}\n";
        var body = $"""
            Nouveau message reçu depuis le formulaire de contact du site vitrine.

            De   : {visitorName} <{visitorEmail}>
            Sujet : {trimmedSubject}
            {offerLine}
            Message :
            {message.Trim()}

            ---
            Ce message a été émis depuis la page /contact. Répondez directement
            à l'adresse e-mail ci-dessus pour entrer en contact avec le visiteur.
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) RenderInvoiceIssued(
        string customerName,
        string documentReference,
        string? fiscalNumber,
        int totalAmountCents,
        string currency,
        string portalUrl)
    {
        var subject =
            $"Facture {fiscalNumber ?? documentReference} disponible";
        var body = $"""
            Bonjour {customerName},

            Votre facture {fiscalNumber ?? documentReference} d'un montant de {FormatAmount(totalAmountCents, currency)} est disponible sur votre espace client.

            Vous pouvez la consulter et la régler en ligne ici :
            {portalUrl}

            Cordialement,
            Kermaria
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) RenderPaymentReminder(
        string customerName,
        string documentReference,
        string? fiscalNumber,
        int totalAmountCents,
        string currency,
        string portalUrl)
    {
        var subject =
            $"Relance facture {fiscalNumber ?? documentReference}";
        var body = $"""
            Bonjour {customerName},

            Sauf erreur de notre part, la facture {fiscalNumber ?? documentReference} d'un montant de {FormatAmount(totalAmountCents, currency)} reste à régler.

            Vous pouvez la consulter et la régler ici :
            {portalUrl}

            Si le règlement a déjà été effectué, merci d'ignorer ce message.

            Cordialement,
            Kermaria
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) RenderPaymentConfirmed(
        string customerName,
        string documentReference,
        string? fiscalNumber,
        int totalAmountCents,
        string currency)
    {
        var subject =
            $"Confirmation de paiement — facture {fiscalNumber ?? documentReference}";
        var body = $"""
            Bonjour {customerName},

            Nous accusons réception de votre règlement de {FormatAmount(totalAmountCents, currency)} pour la facture {fiscalNumber ?? documentReference}.

            Merci pour votre paiement.

            Cordialement,
            Kermaria
            """;
        return (subject, body);
    }

    private static string FormatAmount(int amountCents, string currency)
    {
        var amount = amountCents / 100m;
        var formatted = amount.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"));
        return $"{formatted} {currency.ToUpperInvariant()}";
    }
}
