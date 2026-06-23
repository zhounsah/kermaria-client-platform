using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Bpce;

public sealed class BpceApiClient : IBpceApiClient
{
    private readonly BpceRuntimeConfiguration _configuration;
    private readonly IBpceTokenCache _tokenCache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BpceApiClient> _logger;

    public BpceApiClient(
        BpceRuntimeConfiguration configuration,
        IBpceTokenCache tokenCache,
        IHttpClientFactory httpClientFactory,
        ILogger<BpceApiClient> logger)
    {
        _configuration = configuration;
        _tokenCache = tokenCache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<T?> GetJsonAsync<T>(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var url = BuildAbsoluteUrl(relativePath);
        var client = _httpClientFactory.CreateClient(BpceTokenCache.HttpClientName);

        using var firstResponse = await SendAuthenticatedAsync(
            client,
            url,
            cancellationToken);
        if (firstResponse.StatusCode != HttpStatusCode.Unauthorized)
        {
            return await DeserializeAsync<T>(firstResponse, cancellationToken);
        }

        _logger.LogInformation(
            "BPCE returned 401; invalidating cached access token and retrying once");
        _tokenCache.Invalidate();

        using var retryResponse = await SendAuthenticatedAsync(
            client,
            url,
            cancellationToken);
        return await DeserializeAsync<T>(retryResponse, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpClient client,
        Uri url,
        CancellationToken cancellationToken)
    {
        var token = await _tokenCache.GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token);
        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<T?> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private Uri BuildAbsoluteUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException(
                "Relative path must be provided.",
                nameof(relativePath));
        }

        var normalizedPath = relativePath.StartsWith('/')
            ? relativePath
            : "/" + relativePath;
        return new Uri($"{_configuration.BaseUrl}{normalizedPath}");
    }
}
