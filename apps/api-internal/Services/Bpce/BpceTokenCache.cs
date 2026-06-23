using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Bpce;

public sealed class BpceTokenCache : IBpceTokenCache, IDisposable
{
    public const string HttpClientName = "bpce";

    private const string RefreshPath = "/api/jwt/auth/token/refresh/";
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(60);

    private readonly BpceRuntimeConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BpceTokenCache> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public BpceTokenCache(
        BpceRuntimeConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<BpceTokenCache> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken)
    {
        if (_configuration.RefreshToken is null)
        {
            throw new InvalidOperationException(
                "BPCE refresh token is not configured.");
        }

        if (IsCachedTokenUsable())
        {
            return _accessToken!;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (IsCachedTokenUsable())
            {
                return _accessToken!;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_configuration.BaseUrl}{RefreshPath}")
            {
                Content = JsonContent.Create(new BpceRefreshTokenRequest(
                    _configuration.RefreshToken))
            };

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "BPCE token refresh failed with status {StatusCode}",
                    (int)response.StatusCode);
                throw new BpceAuthenticationException(
                    $"BPCE refresh failed with status {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<BpceTokenResponse>(
                cancellationToken);
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.Access)
                || payload.AccessExpiresAt == default)
            {
                throw new BpceAuthenticationException(
                    "BPCE refresh response is missing the access token or its expiry.");
            }

            _accessToken = payload.Access;
            _expiresAt = payload.AccessExpiresAt;
            return _accessToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Invalidate()
    {
        _accessToken = null;
        _expiresAt = DateTimeOffset.MinValue;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }

    private bool IsCachedTokenUsable()
        => _accessToken is not null
            && _expiresAt - ExpiryMargin > DateTimeOffset.UtcNow;

    private sealed record BpceRefreshTokenRequest(
        [property: JsonPropertyName("refresh")] string Refresh);

    private sealed record BpceTokenResponse(
        [property: JsonPropertyName("access")] string Access,
        [property: JsonPropertyName("access_expires_at")] DateTimeOffset AccessExpiresAt);
}

public sealed class BpceAuthenticationException : Exception
{
    public BpceAuthenticationException(string message) : base(message)
    {
    }
}
