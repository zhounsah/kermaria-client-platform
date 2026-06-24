using System.Globalization;

namespace Kermaria.ApiInternal.Services.Email;

public static class EmailTemplates
{
    public const string InvoiceIssued = "invoice_issued";
    public const string PaymentReminder = "payment_reminder";
    public const string PaymentConfirmed = "payment_confirmed";

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
