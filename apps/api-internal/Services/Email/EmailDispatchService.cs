using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Kermaria.ApiInternal.Services.Email;

public sealed record EmailDispatchResult(
    bool Succeeded,
    string Code,
    string Message);

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
}

public sealed class EmailDispatchService : IEmailDispatchService
{
    private readonly ICommercialRepository _commercialRepository;
    private readonly IBpceInvoicingRepository _bpceRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailLogRepository _emailLog;
    private readonly EmailRuntimeConfiguration _configuration;
    private readonly ILogger<EmailDispatchService> _logger;

    public EmailDispatchService(
        ICommercialRepository commercialRepository,
        IBpceInvoicingRepository bpceRepository,
        IEmailService emailService,
        IEmailLogRepository emailLog,
        EmailRuntimeConfiguration configuration,
        ILogger<EmailDispatchService> logger)
    {
        _commercialRepository = commercialRepository;
        _bpceRepository = bpceRepository;
        _emailService = emailService;
        _emailLog = emailLog;
        _configuration = configuration;
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
            (doc, record) => EmailTemplates.RenderInvoiceIssued(
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
            (doc, record) => EmailTemplates.RenderPaymentReminder(
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
            (doc, record) => EmailTemplates.RenderPaymentConfirmed(
                doc.CustomerDisplayName,
                doc.InternalReference,
                record?.FiscalNumber,
                doc.TotalAmountCents,
                doc.Currency),
            cancellationToken);

    private async Task<EmailDispatchResult> DispatchAsync(
        string documentId,
        string templateName,
        string correlationId,
        Func<DocumentForIssuing, BpceInvoiceRecord?, (string Subject, string Body)> render,
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
        var (subject, body) = render(doc, record);

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
