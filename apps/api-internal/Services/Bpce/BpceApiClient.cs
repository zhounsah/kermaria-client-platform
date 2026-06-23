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
            client, HttpMethod.Get, url, null, cancellationToken);
        if (firstResponse.StatusCode != HttpStatusCode.Unauthorized)
        {
            return await DeserializeJsonAsync<T>(firstResponse, cancellationToken);
        }

        _tokenCache.Invalidate();
        using var retryResponse = await SendAuthenticatedAsync(
            client, HttpMethod.Get, url, null, cancellationToken);
        return await DeserializeJsonAsync<T>(retryResponse, cancellationToken);
    }

    public async Task<byte[]?> GetBinaryAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var url = BuildAbsoluteUrl(relativePath);
        var client = _httpClientFactory.CreateClient(BpceTokenCache.HttpClientName);

        using var firstResponse = await SendAuthenticatedAsync(
            client, HttpMethod.Get, url, null, cancellationToken);
        if (firstResponse.StatusCode != HttpStatusCode.Unauthorized)
        {
            return await ReadBinaryAsync(firstResponse, cancellationToken);
        }

        _tokenCache.Invalidate();
        using var retryResponse = await SendAuthenticatedAsync(
            client, HttpMethod.Get, url, null, cancellationToken);
        return await ReadBinaryAsync(retryResponse, cancellationToken);
    }

    public async Task<TResponse?> PostJsonAsync<TResponse>(
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        var url = BuildAbsoluteUrl(relativePath);
        var client = _httpClientFactory.CreateClient(BpceTokenCache.HttpClientName);
        var content = JsonContent.Create(payload);

        using var firstResponse = await SendAuthenticatedAsync(
            client, HttpMethod.Post, url, content, cancellationToken);
        if (firstResponse.StatusCode != HttpStatusCode.Unauthorized)
        {
            return await DeserializeJsonAsync<TResponse>(
                firstResponse,
                cancellationToken);
        }

        _tokenCache.Invalidate();
        var retryContent = JsonContent.Create(payload);
        using var retryResponse = await SendAuthenticatedAsync(
            client, HttpMethod.Post, url, retryContent, cancellationToken);
        return await DeserializeJsonAsync<TResponse>(retryResponse, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpClient client,
        HttpMethod method,
        Uri url,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var token = await _tokenCache.GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", token);
        if (content is not null)
        {
            request.Content = content;
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private async Task<T?> DeserializeJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(
                cancellationToken);
            _logger.LogWarning(
                "BPCE API error {StatusCode} on {Method} {Uri}: {Body}",
                (int)response.StatusCode,
                response.RequestMessage?.Method,
                response.RequestMessage?.RequestUri,
                errorBody);
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private static async Task<byte[]?> ReadBinaryAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private Uri BuildAbsoluteUrl(string relativePath)
    {
        var normalizedPath = relativePath.StartsWith('/')
            ? relativePath
            : "/" + relativePath;
        return new Uri($"{_configuration.BaseUrl}{normalizedPath}");
    }
}
