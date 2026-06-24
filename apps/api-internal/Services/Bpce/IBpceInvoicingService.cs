using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Services.Bpce;

public interface IBpceInvoicingService
{
    string ModeName { get; }

    Task<BpceStatusResponse> GetStatusAsync(CancellationToken cancellationToken);

    Task<BpceServiceResult<IReadOnlyList<BpceSenderInfo>>> ListSendersAsync(
        CancellationToken cancellationToken);

    Task<BpceServiceResult<BpceSenderInfo>> GetSenderAsync(
        string senderId,
        CancellationToken cancellationToken);

    Task<BpceServiceResult<string>> UpsertCustomerAsync(
        string externalReference,
        string displayName,
        string? email,
        string? address,
        string? city,
        string? country,
        CancellationToken cancellationToken);

    Task<BpceServiceResult<string>> CreateDraftInvoiceAsync(
        string bpceCustomerId,
        string externalReference,
        string title,
        string issueDate,
        IReadOnlyList<BpceInvoiceLineInput> lines,
        CancellationToken cancellationToken);

    Task<BpceServiceResult<(string? FiscalNumber, string Status)>> ValidateInvoiceAsync(
        string bpceInvoiceId,
        bool sendEmail,
        CancellationToken cancellationToken);

    Task<BpceServiceResult<byte[]>> GetInvoicePdfAsync(
        string bpceInvoiceId,
        CancellationToken cancellationToken);

    Task<BpceServiceResult<bool>> MarkInvoiceAsPaidAsync(
        string bpceInvoiceId,
        CancellationToken cancellationToken);
}

public sealed record BpceInvoiceLineInput(
    string Label,
    string Description,
    decimal Quantity,
    string UnitLabel,
    decimal UnitPriceEuros,
    decimal? TaxRatePercent,
    int SortOrder);
