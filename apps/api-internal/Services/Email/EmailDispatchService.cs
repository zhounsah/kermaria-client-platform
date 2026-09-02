using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Kermaria.ApiInternal.Services.Email;

public sealed record EmailDispatchResult(
    bool Succeeded,
    string Code,
    string Message);

public sealed record ContactFormSubmission(
    string VisitorName,
    string VisitorEmail,
    string SubjectLine,
    string Message,
    string? FormuleCode);

public interface IEmailDispatchService
{
    Task<EmailDispatchResult> SendInvoiceIssuedAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<EmailDispatchResult> SendPaymentReminderAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<EmailDispatchResult> SendPaymentConfirmedAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<EmailDispatchResult> SendContactFormAsync(
        ContactFormSubmission submission,
        string correlationId,
        CancellationToken cancellationToken);

    Task<EmailDispatchResult> SendSignupVerificationAsync(
        string email,
        string contactName,
        string verificationUrl,
        string correlationId,
        CancellationToken cancellationToken);

    Task<EmailDispatchResult> SendAccountApprovedAsync(
        string email,
        string contactName,
        string setPasswordUrl,
        string correlationId,
        CancellationToken cancellationToken);

    Task<EmailDispatchResult> SendAccountRejectedAsync(
        string email,
        string contactName,
        string? reason,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class EmailDispatchService : IEmailDispatchService
{
    private readonly ICommercialDocumentRepository _commercialRepository;
    private readonly IBpceInvoicingRepository _bpceRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailLogRepository _emailLog;
    private readonly EmailRuntimeConfiguration _configuration;
    private readonly ICommunicationTemplateService _templates;
    private readonly ILogger<EmailDispatchService> _logger;

    public EmailDispatchService(
        ICommercialDocumentRepository commercialRepository,
        IBpceInvoicingRepository bpceRepository,
        IEmailService emailService,
        IEmailLogRepository emailLog,
        EmailRuntimeConfiguration configuration,
        ICommunicationTemplateService templates,
        ILogger<EmailDispatchService> logger)
    {
        _commercialRepository = commercialRepository;
        _bpceRepository = bpceRepository;
        _emailService = emailService;
        _emailLog = emailLog;
        _configuration = configuration;
        _templates = templates;
        _logger = logger;
    }

    public Task<EmailDispatchResult> SendInvoiceIssuedAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
        => DispatchAsync(
            documentId,
            EmailTemplates.InvoiceIssued,
            correlationId,
            (doc, record) => EmailTemplates.DocumentVariables(
                doc.CustomerDisplayName,
                doc.InternalReference,
                record?.FiscalNumber,
                doc.TotalAmountCents,
                doc.Currency,
                BuildPortalDocumentUrl(documentId)),
            cancellationToken);

    public Task<EmailDispatchResult> SendPaymentReminderAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
        => DispatchAsync(
            documentId,
            EmailTemplates.PaymentReminder,
            correlationId,
            (doc, record) => EmailTemplates.DocumentVariables(
                doc.CustomerDisplayName,
                doc.InternalReference,
                record?.FiscalNumber,
                doc.TotalAmountCents,
                doc.Currency,
                BuildPortalDocumentUrl(documentId)),
            cancellationToken);

    public Task<EmailDispatchResult> SendPaymentConfirmedAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
        => DispatchAsync(
            documentId,
            EmailTemplates.PaymentConfirmed,
            correlationId,
            (doc, record) => EmailTemplates.DocumentVariables(
                doc.CustomerDisplayName,
                doc.InternalReference,
                record?.FiscalNumber,
                doc.TotalAmountCents,
                doc.Currency,
                portalUrl: null),
            cancellationToken);

    public async Task<EmailDispatchResult> SendContactFormAsync(
        ContactFormSubmission submission,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var recipient = _configuration.ContactFormRecipient?.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            await _emailLog.RecordAsync(
                EmailTemplates.ContactForm,
                string.Empty,
                "(no recipient)",
                string.Empty,
                "no_recipient",
                "CONTACT_FORM_RECIPIENT is not configured.",
                null,
                correlationId,
                false,
                cancellationToken);
            _logger.LogWarning(
                "Contact form email skipped: CONTACT_FORM_RECIPIENT is not configured.");
            // Le defaut de configuration est decrit dans le journal ci-dessus.
            // Le visiteur du formulaire public n'a pas a apprendre l'etat de
            // configuration du serveur.
            return new EmailDispatchResult(
                false, "NO_RECIPIENT",
                "Le message de contact n'a pas pu être remis.");
        }

        var (subject, body) = await _templates.RenderEmailAsync(
            EmailTemplates.ContactForm,
            EmailTemplates.ContactFormVariables(
                submission.VisitorName,
                submission.VisitorEmail,
                submission.SubjectLine,
                submission.Message,
                submission.FormuleCode),
            cancellationToken);

        var message = new EmailMessage(
            recipient,
            subject,
            body,
            EmailTemplates.ContactForm,
            RelatedDocumentId: null,
            CorrelationId: correlationId);

        var delivery = await _emailService.SendAsync(message, cancellationToken);
        await _emailLog.RecordAsync(
            EmailTemplates.ContactForm,
            recipient,
            subject,
            body,
            delivery.Status,
            delivery.ErrorMessage,
            null,
            correlationId,
            delivery.Succeeded,
            cancellationToken);

        if (!delivery.Succeeded)
        {
            // `delivery.ErrorMessage` est la chaine renvoyee par le serveur
            // SMTP : hote, port, echec d'authentification, adresse refusee.
            // Cette reponse est relayee par le BFF public du formulaire de
            // contact ; le detail reste dans le journal d'e-mails ci-dessus,
            // correle par `correlationId`, et ne part pas sur le fil.
            _logger.LogError(
                "Contact form delivery failed status {Status} correlation_id {CorrelationId}: {Error}",
                delivery.Status,
                correlationId,
                delivery.ErrorMessage);
            return new EmailDispatchResult(
                false,
                $"EMAIL_{delivery.Status.ToUpperInvariant()}",
                "Le message de contact n'a pas pu être remis.");
        }

        // Ne pas nommer la boite de reception : la reponse traverse jusqu'au
        // navigateur du visiteur.
        return new EmailDispatchResult(true, "EMAIL_SENT", "Message transmis.");
    }

    public async Task<EmailDispatchResult> SendSignupVerificationAsync(
        string email,
        string contactName,
        string verificationUrl,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var (subject, body) = await _templates.RenderEmailAsync(
            EmailTemplates.SignupVerification,
            EmailTemplates.SignupVerificationVariables(contactName, verificationUrl),
            cancellationToken);
        return await SendAdHocAsync(
            email,
            subject,
            body,
            EmailTemplates.SignupVerification,
            correlationId,
            cancellationToken);
    }

    public async Task<EmailDispatchResult> SendAccountApprovedAsync(
        string email,
        string contactName,
        string setPasswordUrl,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var (subject, body) = await _templates.RenderEmailAsync(
            EmailTemplates.AccountApproved,
            EmailTemplates.AccountApprovedVariables(contactName, setPasswordUrl),
            cancellationToken);
        return await SendAdHocAsync(
            email,
            subject,
            body,
            EmailTemplates.AccountApproved,
            correlationId,
            cancellationToken);
    }

    public async Task<EmailDispatchResult> SendAccountRejectedAsync(
        string email,
        string contactName,
        string? reason,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var (subject, body) = await _templates.RenderEmailAsync(
            EmailTemplates.AccountRejected,
            EmailTemplates.AccountRejectedVariables(contactName, reason),
            cancellationToken);
        return await SendAdHocAsync(
            email,
            subject,
            body,
            EmailTemplates.AccountRejected,
            correlationId,
            cancellationToken);
    }

    // Envoi d'un e-mail transactionnel non lié à un document commercial
    // (inscription self-service). Journalisé dans email_messages avec
    // related_document_id = NULL, comme le formulaire de contact V0.27.
    private async Task<EmailDispatchResult> SendAdHocAsync(
        string recipientRaw,
        string subject,
        string body,
        string template,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var recipient = recipientRaw?.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return new EmailDispatchResult(
                false, "NO_RECIPIENT", "Aucun destinataire.");
        }

        var message = new EmailMessage(
            recipient,
            subject,
            body,
            template,
            RelatedDocumentId: null,
            CorrelationId: correlationId);

        var delivery = await _emailService.SendAsync(message, cancellationToken);
        await _emailLog.RecordAsync(
            template,
            recipient,
            subject,
            body,
            delivery.Status,
            delivery.ErrorMessage,
            null,
            correlationId,
            delivery.Succeeded,
            cancellationToken);

        if (!delivery.Succeeded)
        {
            return new EmailDispatchResult(
                false,
                $"EMAIL_{delivery.Status.ToUpperInvariant()}",
                delivery.ErrorMessage ?? "Email delivery failed.");
        }

        return new EmailDispatchResult(
            true, "EMAIL_SENT", $"Email envoyé à {recipient}.");
    }

    private async Task<EmailDispatchResult> DispatchAsync(
        string documentId,
        string templateName,
        string correlationId,
        Func<DocumentForIssuing, BpceInvoiceRecord?, IReadOnlyDictionary<string, string?>> buildVariables,
        CancellationToken cancellationToken)
    {
        var doc = await _commercialRepository.GetDocumentForIssuingAsync(
            documentId, cancellationToken);
        if (doc is null)
        {
            return new EmailDispatchResult(
                false, "DOCUMENT_NOT_FOUND",
                "The commercial document was not found.");
        }

        var recipient = doc.CustomerBillingEmail?.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            await _emailLog.RecordAsync(
                templateName,
                string.Empty,
                "(no recipient)",
                string.Empty,
                "no_recipient",
                "Customer billing_email is empty.",
                documentId,
                correlationId,
                false,
                cancellationToken);
            _logger.LogWarning(
                "Email skipped for template {Template} document {DocumentId}: customer has no billing_email",
                templateName,
                documentId);
            return new EmailDispatchResult(
                false, "NO_RECIPIENT",
                "Le client n'a pas d'adresse e-mail de facturation enregistrée.");
        }

        var record = await _bpceRepository.GetInvoiceRecordAsync(
            documentId, cancellationToken);
        var (subject, body) = await _templates.RenderEmailAsync(
            templateName,
            buildVariables(doc, record),
            cancellationToken);

        var message = new EmailMessage(
            recipient,
            subject,
            body,
            templateName,
            documentId,
            correlationId);

        var delivery = await _emailService.SendAsync(message, cancellationToken);
        await _emailLog.RecordAsync(
            templateName,
            recipient,
            subject,
            body,
            delivery.Status,
            delivery.ErrorMessage,
            documentId,
            correlationId,
            delivery.Succeeded,
            cancellationToken);

        if (!delivery.Succeeded)
        {
            return new EmailDispatchResult(
                false,
                $"EMAIL_{delivery.Status.ToUpperInvariant()}",
                delivery.ErrorMessage ?? "Email delivery failed.");
        }

        return new EmailDispatchResult(
            true, "EMAIL_SENT",
            $"Email envoyé à {recipient}.");
    }

    private string BuildPortalDocumentUrl(string documentId)
    {
        var baseUrl = _configuration.PortalPublicUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return $"/commercial-documents/{documentId}";
        }
        return $"{baseUrl.TrimEnd('/')}/commercial-documents/{documentId}";
    }
}
