namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockBpceInvoicingRepository : IBpceInvoicingRepository
{
    private readonly Dictionary<string, BpceCustomerLink> _customerLinks = new();
    private readonly Dictionary<string, BpceInvoiceRecord> _invoiceRecords = new();
    private readonly Dictionary<string, byte[]> _pdfs = new();
    private readonly object _sync = new();

    public Task<BpceCustomerLink?> GetCustomerLinkAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _customerLinks.TryGetValue(customerId, out var link);
            return Task.FromResult(link);
        }
    }

    public Task UpsertCustomerLinkAsync(
        string customerId,
        string bpceCustomerId,
        string bpceExternalId,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _customerLinks[customerId] = new BpceCustomerLink(
                customerId,
                bpceCustomerId,
                bpceExternalId,
                DateTime.UtcNow.ToString("o"));
        }

        return Task.CompletedTask;
    }

    public Task<BpceInvoiceRecord?> GetInvoiceRecordAsync(
        string commercialDocumentId,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _invoiceRecords.TryGetValue(commercialDocumentId, out var record);
            return Task.FromResult(record);
        }
    }

    public Task CreateInvoiceRecordAsync(
        string commercialDocumentId,
        string bpceInvoiceId,
        string bpceCustomerId,
        string issueDate,
        int totalAmountCents,
        string currency,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _invoiceRecords[commercialDocumentId] = new BpceInvoiceRecord(
                commercialDocumentId,
                bpceInvoiceId,
                bpceCustomerId,
                "draft",
                null,
                issueDate,
                totalAmountCents,
                currency,
                null,
                DateTime.UtcNow.ToString("o"),
                null);
        }

        return Task.CompletedTask;
    }

    public Task UpdateInvoiceValidatedAsync(
        string commercialDocumentId,
        string? fiscalNumber,
        string status,
        byte[]? pdfContent,
        string? pdfHash,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!_invoiceRecords.TryGetValue(
                    commercialDocumentId,
                    out var existing))
            {
                return Task.CompletedTask;
            }

            _invoiceRecords[commercialDocumentId] = existing with
            {
                FiscalNumber = fiscalNumber,
                Status = status,
                PdfHash = pdfHash,
                ValidatedAt = DateTime.UtcNow.ToString("o")
            };

            if (pdfContent is not null)
            {
                _pdfs[commercialDocumentId] = pdfContent;
            }
        }

        return Task.CompletedTask;
    }

    public Task<byte[]?> GetInvoicePdfAsync(
        string commercialDocumentId,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _pdfs.TryGetValue(commercialDocumentId, out var pdf);
            return Task.FromResult(pdf);
        }
    }
}
