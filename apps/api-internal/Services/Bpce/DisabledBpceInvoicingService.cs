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
}
