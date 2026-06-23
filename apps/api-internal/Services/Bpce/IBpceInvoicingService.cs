using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Services.Bpce;

public interface IBpceInvoicingService
{
    string ModeName { get; }

    Task<BpceStatusResponse> GetStatusAsync(CancellationToken cancellationToken);
}
