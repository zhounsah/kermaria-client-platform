namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record BpceCustomerLink(
    string CustomerId,
    string BpceCustomerId,
    string BpceExternalId,
    string SyncedAt);

public sealed record BpceInvoiceRecord(
    string CommercialDocumentId,
    string BpceInvoiceId,
    string BpceCustomerId,
    string Status,
    string? FiscalNumber,
    string IssueDate,
    int TotalAmountCents,
    string Currency,
    string? PdfHash,
    string CreatedAt,
    string? ValidatedAt);

public interface IBpceInvoicingRepository
{
    Task<BpceCustomerLink?> GetCustomerLinkAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task UpsertCustomerLinkAsync(
        string customerId,
        string bpceCustomerId,
        string bpceExternalId,
        CancellationToken cancellationToken);

    Task<BpceInvoiceRecord?> GetInvoiceRecordAsync(
        string commercialDocumentId,
        CancellationToken cancellationToken);

    Task CreateInvoiceRecordAsync(
        string commercialDocumentId,
        string bpceInvoiceId,
        string bpceCustomerId,
        string issueDate,
        int totalAmountCents,
        string currency,
        CancellationToken cancellationToken);

    Task UpdateInvoiceValidatedAsync(
        string commercialDocumentId,
        string? fiscalNumber,
        string status,
        byte[]? pdfContent,
        string? pdfHash,
        CancellationToken cancellationToken);

    Task<byte[]?> GetInvoicePdfAsync(
        string commercialDocumentId,
        CancellationToken cancellationToken);
}
