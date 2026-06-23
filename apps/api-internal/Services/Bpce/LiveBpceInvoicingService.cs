using System.Net.Http;
using System.Text.Json.Serialization;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Bpce;

public sealed class LiveBpceInvoicingService : IBpceInvoicingService
{
    private const string SendersPath = "/inv/api/v5/senders/";

    private readonly BpceRuntimeConfiguration _configuration;
    private readonly IBpceTokenCache _tokenCache;
    private readonly IBpceApiClient _apiClient;
    private readonly ILogger<LiveBpceInvoicingService> _logger;

    public LiveBpceInvoicingService(
        BpceRuntimeConfiguration configuration,
        IBpceTokenCache tokenCache,
        IBpceApiClient apiClient,
        ILogger<LiveBpceInvoicingService> logger)
    {
        _configuration = configuration;
        _tokenCache = tokenCache;
        _apiClient = apiClient;
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

    public async Task<BpceServiceResult<IReadOnlyList<BpceSenderInfo>>> ListSendersAsync(
        CancellationToken cancellationToken)
    {
        if (!_configuration.ConfigurationValid)
        {
            return UnconfiguredList();
        }

        try
        {
            var payload = await _apiClient.GetJsonAsync<BpceSenderListPayload>(
                SendersPath,
                cancellationToken);
            var rawSenders = payload?.Results ?? Array.Empty<BpceSenderApiDto>();
            var senders = rawSenders
                .Select(MapToSenderInfo)
                .ToArray();
            return new BpceServiceResult<IReadOnlyList<BpceSenderInfo>>(
                StatusCodes.Status200OK,
                "BPCE_SENDERS_FOUND",
                "BPCE senders returned.",
                senders);
        }
        catch (Exception exception)
            when (exception is BpceAuthenticationException
                or HttpRequestException
                or TaskCanceledException)
        {
            _logger.LogWarning(
                exception,
                "BPCE sender list could not be retrieved");
            return new BpceServiceResult<IReadOnlyList<BpceSenderInfo>>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNREACHABLE",
                "BPCE invoicing API could not be reached.",
                Array.Empty<BpceSenderInfo>());
        }
    }

    public async Task<BpceServiceResult<BpceSenderInfo>> GetSenderAsync(
        string senderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(senderId))
        {
            return new BpceServiceResult<BpceSenderInfo>(
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                "Sender identifier is required.");
        }

        if (!_configuration.ConfigurationValid)
        {
            return new BpceServiceResult<BpceSenderInfo>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNCONFIGURED",
                "BPCE invoicing API is not configured.");
        }

        try
        {
            var payload = await _apiClient.GetJsonAsync<BpceSenderApiDto>(
                $"{SendersPath}{senderId.Trim()}/",
                cancellationToken);
            if (payload is null)
            {
                return new BpceServiceResult<BpceSenderInfo>(
                    StatusCodes.Status404NotFound,
                    "BPCE_SENDER_NOT_FOUND",
                    "The requested BPCE sender was not found.");
            }

            return new BpceServiceResult<BpceSenderInfo>(
                StatusCodes.Status200OK,
                "BPCE_SENDER_FOUND",
                "BPCE sender returned.",
                MapToSenderInfo(payload));
        }
        catch (Exception exception)
            when (exception is BpceAuthenticationException
                or HttpRequestException
                or TaskCanceledException)
        {
            _logger.LogWarning(
                exception,
                "BPCE sender {SenderId} could not be retrieved",
                senderId);
            return new BpceServiceResult<BpceSenderInfo>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNREACHABLE",
                "BPCE invoicing API could not be reached.");
        }
    }

    private static BpceServiceResult<IReadOnlyList<BpceSenderInfo>> UnconfiguredList()
        => new(
            StatusCodes.Status503ServiceUnavailable,
            "BPCE_UNCONFIGURED",
            "BPCE invoicing API is not configured.",
            Array.Empty<BpceSenderInfo>());

    private static BpceSenderInfo MapToSenderInfo(BpceSenderApiDto dto)
        => new(
            Id: dto.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Name: dto.Name,
            ProfileName: dto.ProfileName,
            Siren: dto.Siren,
            Siret: dto.Siret,
            Email: dto.Email,
            Country: dto.Country,
            Locale: dto.Locale,
            IsDefault: dto.IsDefault,
            IsArchived: dto.IsArchived);

    private sealed record BpceSenderListPayload(
        [property: JsonPropertyName("count")] int? Count,
        [property: JsonPropertyName("next")] string? Next,
        [property: JsonPropertyName("previous")] string? Previous,
        [property: JsonPropertyName("results")]
            IReadOnlyList<BpceSenderApiDto>? Results);

    private sealed record BpceSenderApiDto(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("profile_name")] string? ProfileName,
        [property: JsonPropertyName("siren")] string? Siren,
        [property: JsonPropertyName("siret")] string? Siret,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("locale")] string? Locale,
        [property: JsonPropertyName("is_default")] bool IsDefault,
        [property: JsonPropertyName("is_archived")] bool IsArchived);
}
