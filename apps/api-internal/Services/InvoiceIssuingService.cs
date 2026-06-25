using System.Security.Cryptography;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Bpce;
using Kermaria.ApiInternal.Services.Email;

namespace Kermaria.ApiInternal.Services;

public sealed record IssueInvoiceResult(
    bool Succeeded,
    string Code,
    string Message,
    BpceIssuedInvoiceInfo? Invoice = null);

public interface IInvoiceIssuingService
{
    Task<IssueInvoiceResult> IssueInvoiceAsync(
        string documentId,
        bool sendEmail,
        string correlationId,
        CancellationToken cancellationToken);

    Task<byte[]?> GetCachedInvoicePdfAsync(
        string documentId,
        CancellationToken cancellationToken);

    Task<byte[]?> EnsureInvoicePdfAsync(
        string documentId,
        CancellationToken cancellationToken);

    Task<BpceInvoiceRecord?> GetInvoiceRecordAsync(
        string documentId,
        CancellationToken cancellationToken);

    Task<IssueInvoiceResult> ConfirmPaymentAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class InvoiceIssuingService : IInvoiceIssuingService
{
    private readonly ICommercialRepository _commercialRepository;
    private readonly IBpceInvoicingService _bpce;
    private readonly IBpceInvoicingRepository _bpceRepository;
    private readonly IEmailDispatchService _emailDispatch;
    private readonly ILogger<InvoiceIssuingService> _logger;

    public InvoiceIssuingService(
        ICommercialRepository commercialRepository,
        IBpceInvoicingService bpce,
        IBpceInvoicingRepository bpceRepository,
        IEmailDispatchService emailDispatch,
        ILogger<InvoiceIssuingService> logger)
    {
        _commercialRepository = commercialRepository;
        _bpce = bpce;
        _bpceRepository = bpceRepository;
        _emailDispatch = emailDispatch;
        _logger = logger;
    }

    public async Task<IssueInvoiceResult> IssueInvoiceAsync(
        string documentId,
        bool sendEmail,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var existing = await _bpceRepository.GetInvoiceRecordAsync(
            documentId, cancellationToken);
        if (existing is not null)
        {
            return new IssueInvoiceResult(
                false,
                "INVOICE_ALREADY_ISSUED",
                "This document has already been issued as a BPCE invoice.",
                MapRecord(existing));
        }

        var doc = await _commercialRepository.GetDocumentForIssuingAsync(
            documentId, cancellationToken);

        if (doc is null)
        {
            return new IssueInvoiceResult(
                false,
                "DOCUMENT_NOT_FOUND",
                "The commercial document was not found.");
        }

        if (doc.Status != CommercialStatuses.SharedWithCustomer)
        {
            return new IssueInvoiceResult(
                false,
                "DOCUMENT_NOT_ISSUABLE",
                $"Only documents in '{CommercialStatuses.SharedWithCustomer}' status can be issued. Current status: {doc.Status}.");
        }

        if (doc.TotalAmountCents <= 0)
        {
            return new IssueInvoiceResult(
                false,
                "DOCUMENT_EMPTY",
                "The document has no billable amount.");
        }

        var customerResult = await _bpce.UpsertCustomerAsync(
            doc.CustomerExternalReference,
            doc.CustomerDisplayName,
            doc.CustomerBillingEmail,
            doc.CustomerAddress,
            doc.CustomerCity,
            doc.CustomerCountry,
            cancellationToken);

        if (customerResult.StatusCode >= 400 || customerResult.Value is null)
        {
            _logger.LogWarning(
                "BPCE customer upsert failed [{Code}] {Message} for document {DocumentId}",
                customerResult.Code,
                customerResult.Message,
                documentId);
            return new IssueInvoiceResult(
                false,
                customerResult.Code,
                customerResult.Message);
        }

        var bpceCustomerId = customerResult.Value;

        await _bpceRepository.UpsertCustomerLinkAsync(
            doc.CustomerId,
            bpceCustomerId,
            doc.CustomerExternalReference,
            cancellationToken);

        var issueDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var lines = doc.Lines
            .Select(l => new BpceInvoiceLineInput(
                l.Label,
                l.Description,
                l.Quantity,
                l.UnitLabel,
                l.UnitPriceCents / 100m,
                l.TaxRateBasisPoints.HasValue
                    ? l.TaxRateBasisPoints.Value / 100m
                    : null,
                l.SortOrder))
            .ToArray();

        var draftResult = await _bpce.CreateDraftInvoiceAsync(
            bpceCustomerId,
            doc.InternalReference,
            doc.DocumentTitle,
            issueDate,
            lines,
            cancellationToken);

        if (draftResult.StatusCode >= 400 || draftResult.Value is null)
        {
            _logger.LogWarning(
                "BPCE draft invoice creation failed [{Code}] {Message} for document {DocumentId}",
                draftResult.Code,
                draftResult.Message,
                documentId);
            return new IssueInvoiceResult(
                false,
                draftResult.Code,
                draftResult.Message);
        }

        var bpceInvoiceId = draftResult.Value;

        await _bpceRepository.CreateInvoiceRecordAsync(
            documentId,
            bpceInvoiceId,
            bpceCustomerId,
            issueDate,
            doc.TotalAmountCents,
            doc.Currency,
            cancellationToken);

        var validateResult = await _bpce.ValidateInvoiceAsync(
            bpceInvoiceId, sendEmail, cancellationToken);

        string? fiscalNumber = null;
        var invoiceStatus = "draft";

        if (validateResult.StatusCode < 400 && validateResult.Value != default)
        {
            (fiscalNumber, invoiceStatus) = validateResult.Value;
        }
        else
        {
            _logger.LogWarning(
                "BPCE invoice {BpceInvoiceId} validation failed [{Code}] — document {DocumentId} will remain draft",
                bpceInvoiceId,
                validateResult.Code,
                documentId);
        }

        byte[]? pdfBytes = null;
        string? pdfHash = null;

        if (invoiceStatus != "draft")
        {
            var pdfResult = await _bpce.GetInvoicePdfAsync(
                bpceInvoiceId, cancellationToken);
            if (pdfResult.StatusCode < 400 && pdfResult.Value is not null)
            {
                pdfBytes = pdfResult.Value;
                pdfHash = Convert.ToHexString(
                    SHA256.HashData(pdfBytes)).ToLowerInvariant();
            }
        }

        await _bpceRepository.UpdateInvoiceValidatedAsync(
            documentId,
            fiscalNumber,
            invoiceStatus,
            pdfBytes,
            pdfHash,
            cancellationToken);

        if (invoiceStatus != "draft")
        {
            await _commercialRepository.MarkDocumentIssuedAsync(
                documentId, correlationId, cancellationToken);

            if (sendEmail)
            {
                await TryDispatchEmailAsync(
                    "invoice_issued",
                    documentId,
                    () => _emailDispatch.SendInvoiceIssuedAsync(
                        documentId, correlationId, cancellationToken));
            }
        }

        var record = await _bpceRepository.GetInvoiceRecordAsync(
            documentId, cancellationToken);

        return new IssueInvoiceResult(
            true,
            invoiceStatus == "draft"
                ? "INVOICE_ISSUED_PENDING_VALIDATION"
                : "INVOICE_ISSUED",
            invoiceStatus == "draft"
                ? "Invoice was created at BPCE but validation is pending. Check the BPCE dashboard."
                : $"Invoice issued successfully. Fiscal number: {fiscalNumber}",
            record is not null ? MapRecord(record) : null);
    }

    public async Task<IssueInvoiceResult> ConfirmPaymentAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var record = await _bpceRepository.GetInvoiceRecordAsync(
            documentId, cancellationToken);

        if (record is null)
        {
            return new IssueInvoiceResult(
                false, "INVOICE_NOT_FOUND",
                "No issued invoice found for this document.");
        }

        if (record.Status == "paid")
        {
            return new IssueInvoiceResult(
                true, "ALREADY_PAID",
                "This invoice is already marked as paid.",
                MapRecord(record));
        }

        var markResult = await _bpce.MarkInvoiceAsPaidAsync(
            record.BpceInvoiceId, cancellationToken);

        if (markResult.StatusCode >= 400)
        {
            _logger.LogWarning(
                "BPCE mark_as_paid failed [{Code}] {Message} for document {DocumentId}",
                markResult.Code, markResult.Message, documentId);
            return new IssueInvoiceResult(
                false, markResult.Code, markResult.Message);
        }

        await _bpceRepository.UpdateInvoiceValidatedAsync(
            documentId,
            record.FiscalNumber,
            "paid",
            null,
            record.PdfHash,
            cancellationToken);

        await _commercialRepository.MarkDocumentPaidAsync(
            documentId, correlationId, cancellationToken);

        await TryDispatchEmailAsync(
            "payment_confirmed",
            documentId,
            () => _emailDispatch.SendPaymentConfirmedAsync(
                documentId, correlationId, cancellationToken));

        var updated = await _bpceRepository.GetInvoiceRecordAsync(
            documentId, cancellationToken);

        return new IssueInvoiceResult(
            true, "PAYMENT_CONFIRMED",
            "Invoice marked as paid.",
            updated is not null ? MapRecord(updated) : null);
    }

    private async Task TryDispatchEmailAsync(
        string templateName,
        string documentId,
        Func<Task<Email.EmailDispatchResult>> dispatch)
    {
        try
        {
            var result = await dispatch();
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Email dispatch returned failure [{Code}] {Message} for template {Template} document {DocumentId}",
                    result.Code,
                    result.Message,
                    templateName,
                    documentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Email dispatch threw for template {Template} document {DocumentId} - swallowing to preserve BPCE flow",
                templateName,
                documentId);
        }
    }

    public Task<byte[]?> GetCachedInvoicePdfAsync(
        string documentId,
        CancellationToken cancellationToken)
        => _bpceRepository.GetInvoicePdfAsync(documentId, cancellationToken);

    public async Task<byte[]?> EnsureInvoicePdfAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        var cached = await _bpceRepository.GetInvoicePdfAsync(
            documentId, cancellationToken);
        if (cached is not null && cached.Length > 0)
        {
            return cached;
        }

        var record = await _bpceRepository.GetInvoiceRecordAsync(
            documentId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var fetched = await _bpce.GetInvoicePdfAsync(
            record.BpceInvoiceId, cancellationToken);
        if (fetched.StatusCode >= 400 || fetched.Value is null || fetched.Value.Length == 0)
        {
            _logger.LogInformation(
                "BPCE PDF on-demand fetch unsuccessful for invoice {InvoiceId} document {DocumentId} status {StatusCode}",
                record.BpceInvoiceId,
                documentId,
                fetched.StatusCode);
            return null;
        }

        var pdfHash = Convert.ToHexString(
            SHA256.HashData(fetched.Value)).ToLowerInvariant();
        await _bpceRepository.UpdateInvoiceValidatedAsync(
            documentId,
            record.FiscalNumber,
            record.Status,
            fetched.Value,
            pdfHash,
            cancellationToken);

        return fetched.Value;
    }

    public Task<BpceInvoiceRecord?> GetInvoiceRecordAsync(
        string documentId,
        CancellationToken cancellationToken)
        => _bpceRepository.GetInvoiceRecordAsync(documentId, cancellationToken);

    private static BpceIssuedInvoiceInfo MapRecord(BpceInvoiceRecord record)
        => new(
            record.BpceInvoiceId,
            record.FiscalNumber,
            record.Status,
            record.IssueDate,
            record.TotalAmountCents,
            record.Currency,
            record.PdfHash is not null);
}
