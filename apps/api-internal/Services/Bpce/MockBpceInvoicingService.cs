using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Bpce;

public sealed class MockBpceInvoicingService : IBpceInvoicingService
{
    private const string MockSenderId = "MOCK-SENDER-EI-ZACHARY";

    private readonly BpceRuntimeConfiguration _configuration;

    public MockBpceInvoicingService(BpceRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ModeName => _configuration.ModeName;

    public Task<BpceStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(new BpceStatusResponse(
            _configuration.ModeName,
            "mock",
            true,
            true,
            _configuration.BaseUrl,
            _configuration.RequestTimeoutMs));
}
