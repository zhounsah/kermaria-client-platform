using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services;

public sealed record KoxoSyncWebhookTriggerRequest(
    string SignupId,
    string PortalUserId,
    string CustomerReference,
    string Trigger,
    string CorrelationId,
    string RequestedAtUtc);

public interface IKoxoSyncWebhookTriggerService
{
    Task TriggerAsync(
        KoxoSyncWebhookTriggerRequest request,
        CancellationToken cancellationToken);
}

public sealed class KoxoSyncWebhookTriggerService : IKoxoSyncWebhookTriggerService
{
    public const string HttpClientName = "koxo-sync-webhook";

    private readonly KoxoSyncWebhookRuntimeConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KoxoSyncWebhookTriggerService> _logger;

    public KoxoSyncWebhookTriggerService(
        KoxoSyncWebhookRuntimeConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<KoxoSyncWebhookTriggerService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task TriggerAsync(
        KoxoSyncWebhookTriggerRequest request,
        CancellationToken cancellationToken)
    {
        if (!_configuration.Enabled || _configuration.Url is null)
        {
            return;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Post, _configuration.Url)
        {
            Content = JsonContent.Create(new
            {
                signupId = request.SignupId,
                portalUserId = request.PortalUserId,
                customerReference = request.CustomerReference,
                trigger = request.Trigger,
                correlationId = request.CorrelationId,
                requestedAt = request.RequestedAtUtc
            })
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _configuration.BearerToken);

        using var response = await client.SendAsync(message, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "KoXo sync webhook returned {StatusCode} for customer_reference {CustomerReference}, portal_user_id {PortalUserId}, correlation_id {CorrelationId}: {Body}",
            (int)response.StatusCode,
            request.CustomerReference,
            request.PortalUserId,
            request.CorrelationId,
            body);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class DisabledKoxoSyncWebhookTriggerService : IKoxoSyncWebhookTriggerService
{
    public Task TriggerAsync(
        KoxoSyncWebhookTriggerRequest request,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}
