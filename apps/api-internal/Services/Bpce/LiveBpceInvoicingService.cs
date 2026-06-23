using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Bpce;

public sealed class LiveBpceInvoicingService : IBpceInvoicingService
{
    private readonly BpceRuntimeConfiguration _configuration;
    private readonly IBpceTokenCache _tokenCache;
    private readonly ILogger<LiveBpceInvoicingService> _logger;

    public LiveBpceInvoicingService(
        BpceRuntimeConfiguration configuration,
        IBpceTokenCache tokenCache,
        ILogger<LiveBpceInvoicingService> logger)
    {
        _configuration = configuration;
        _tokenCache = tokenCache;
        _logger = logger;
    }

    public string ModeName => _configuration.ModeName;

    public async Task<BpceStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        if (!_configuration.ConfigurationValid)
        {
            return new BpceStatusResponse(
                _configuration.ModeName,
                "unconfigured",
                false,
                false,
                _configuration.BaseUrl,
                _configuration.RequestTimeoutMs);
        }

        try
        {
            _ = await _tokenCache.GetAccessTokenAsync(cancellationToken);
            return new BpceStatusResponse(
                _configuration.ModeName,
                "connected",
                true,
                _configuration.SenderId is not null,
                _configuration.BaseUrl,
                _configuration.RequestTimeoutMs);
        }
        catch (Exception exception)
            when (exception is BpceAuthenticationException
                or HttpRequestException
                or TaskCanceledException)
        {
            _logger.LogWarning(
                exception,
                "BPCE live status probe could not establish a session");
            return new BpceStatusResponse(
                _configuration.ModeName,
                "unreachable",
                _configuration.ConfigurationValid,
                _configuration.SenderId is not null,
                _configuration.BaseUrl,
                _configuration.RequestTimeoutMs);
        }
    }
}
