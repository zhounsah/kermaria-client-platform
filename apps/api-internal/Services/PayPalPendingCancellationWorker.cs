using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services;

public sealed class PayPalPendingCancellationWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly ILogger<PayPalPendingCancellationWorker> _logger;
    private readonly HttpClient _httpClient = new();
    private string? _cachedAccessToken;
    private DateTime _accessTokenExpiresAtUtc = DateTime.MinValue;

    public PayPalPendingCancellationWorker(
        IServiceScopeFactory scopeFactory,
        PayPalRuntimeConfiguration paypal,
        ILogger<PayPalPendingCancellationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _paypal = paypal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_paypal.IsConfigured)
        {
            _logger.LogInformation(
                "PayPal pending-cancellation worker disabled because PAYPAL_CLIENT_ID or PAYPAL_CLIENT_SECRET is missing.");
            return;
        }

        using var timer = new PeriodicTimer(PollInterval);

        await ProcessDueSubscriptionsAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessDueSubscriptionsAsync(stoppingToken);
        }
    }

    private async Task ProcessDueSubscriptionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var nowUtc = DateTime.UtcNow;

        IReadOnlyList<Contracts.SubscriptionSummary> allSubscriptions;
        try
        {
            allSubscriptions = await subscriptions.GetAdminSubscriptionsAsync(
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Unable to load subscriptions for PayPal term-end cancellation worker.");
            return;
        }

        var dueSubscriptions = allSubscriptions.Where(subscription =>
        {
            if (subscription.Status != "pending_cancellation"
                || subscription.Rail != "paypal"
                || !subscription.CancelAtTermEnd)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(subscription.NextBillingAt))
            {
                return false;
            }

            if (!DateTime.TryParse(
                    subscription.NextBillingAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal
                    | DateTimeStyles.AdjustToUniversal,
                    out var nextBillingAtUtc))
            {
                return false;
            }

            return nextBillingAtUtc <= nowUtc;
        }).ToArray();

        foreach (var subscription in dueSubscriptions)
        {
            var correlationId = $"paypal-term-end-{Guid.NewGuid():N}";
            try
            {
                if (!string.IsNullOrWhiteSpace(subscription.PayPalSubscriptionId))
                {
                    await CancelSubscriptionAsync(
                        subscription.PayPalSubscriptionId,
                        "Term-end cancellation requested from Kermaria portal.",
                        cancellationToken);
                }

                await subscriptions.UpdateStatusAsync(
                    subscription.Id,
                    "cancelled",
                    "subscription.provisioning.term_end_cancel",
                    correlationId,
                    requestedByUserId: null,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "PayPal term-end cancellation worker failed for subscription {SubscriptionId}.",
                    subscription.Id);
            }
        }
    }

    private async Task CancelSubscriptionAsync(
        string paypalSubscriptionId,
        string reason,
        CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_paypal.ApiBaseUrl}/v1/billing/subscriptions/{Uri.EscapeDataString(paypalSubscriptionId)}/cancel");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { reason }),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent
            || response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"PayPal term-end cancellation failed ({(int)response.StatusCode}): {body}");
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken)
            && _accessTokenExpiresAtUtc > DateTime.UtcNow.AddMinutes(1))
        {
            return _cachedAccessToken;
        }

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_paypal.ClientId}:{_paypal.ClientSecret}"));
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_paypal.ApiBaseUrl}/v1/oauth2/token");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new StringContent(
            "grant_type=client_credentials",
            Encoding.UTF8,
            "application/x-www-form-urlencoded");

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        _cachedAccessToken = document.RootElement
            .GetProperty("access_token")
            .GetString()
            ?? throw new InvalidOperationException(
                "PayPal access_token missing in OAuth response.");
        var expiresInSeconds = document.RootElement.GetProperty("expires_in")
            .GetInt32();
        _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresInSeconds);
        return _cachedAccessToken;
    }
}
