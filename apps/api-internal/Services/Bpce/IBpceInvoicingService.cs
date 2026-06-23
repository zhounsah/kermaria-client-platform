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
}
