using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Bpce;

public sealed class DisabledBpceInvoicingService : IBpceInvoicingService
{
    private readonly BpceRuntimeConfiguration _configuration;

    public DisabledBpceInvoicingService(BpceRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ModeName => _configuration.ModeName;

    public Task<BpceStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(new BpceStatusResponse(
            _configuration.ModeName,
            "disabled",
            true,
            false,
            _configuration.BaseUrl,
            _configuration.RequestTimeoutMs));

    public Task<BpceServiceResult<IReadOnlyList<BpceSenderInfo>>> ListSendersAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(new BpceServiceResult<IReadOnlyList<BpceSenderInfo>>(
            StatusCodes.Status501NotImplemented,
            "BPCE_INTEGRATION_DISABLED",
            "BPCE invoicing integration is disabled.",
            Array.Empty<BpceSenderInfo>()));

    public Task<BpceServiceResult<BpceSenderInfo>> GetSenderAsync(
        string senderId,
        CancellationToken cancellationToken)
        => Task.FromResult(new BpceServiceResult<BpceSenderInfo>(
            StatusCodes.Status501NotImplemented,
            "BPCE_INTEGRATION_DISABLED",
            "BPCE invoicing integration is disabled."));

    public Task<BpceServiceResult<string>> UpsertCustomerAsync(
        string externalReference,
        string displayName,
        string? email,
        string? address,
        string? city,
        string? country,
        CancellationToken cancellationToken)
        => Task.FromResult(new BpceServiceResult<string>(
            StatusCodes.Status501NotImplemented,
            "BPCE_INTEGRATION_DISABLED",
            "BPCE invoicing integration is disabled."));

    public Task<BpceServiceResult<string>> CreateDraftInvoiceAsync(
        string bpceCustomerId,
        string externalReference,
        string title,
        string issueDate,
        IReadOnlyList<BpceInvoiceLineInput> lines,
        CancellationToken cancellationToken)
        => Task.FromResult(new BpceServiceResult<string>(
            StatusCodes.Status501NotImplemented,
            "BPCE_INTEGRATION_DISABLED",
            "BPCE invoicing integration is disabled."));

    public Task<BpceServiceResult<(string? FiscalNumber, string Status)>> ValidateInvoiceAsync(
        string bpceInvoiceId,
        bool sendEmail,
        CancellationToken cancellationToken)
        => Task.FromResult(new BpceServiceResult<(string?, string)>(
            StatusCodes.Status501NotImplemented,
            "BPCE_INTEGRATION_DISABLED",
            "BPCE invoicing integration is disabled."));

    public Task<BpceServiceResult<byte[]>> GetInvoicePdfAsync(
        string bpceInvoiceId,
        CancellationToken cancellationToken)
        => Task.FromResult(new BpceServiceResult<byte[]>(
            StatusCodes.Status501NotImplemented,
            "BPCE_INTEGRATION_DISABLED",
            "BPCE invoicing integration is disabled."));
}
