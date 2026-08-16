using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2ProviderCheckoutExecutionRequest(
    string OutboxEventId,
    string IdempotencyKeyHash,
    string PayloadText);

public sealed record BillingV2ProviderCheckoutExecutionResult(
    bool Succeeded,
    string Code,
    string? ProviderCheckoutId,
    string? ProviderSubscriptionId,
    string? ApprovalUrl,
    string? ErrorMessage);

public sealed record BillingV2ProviderHttpRequest(
    string Provider,
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string Body);

public interface IBillingV2ProviderCheckoutExecutor
{
    bool CanExecute { get; }

    Task<BillingV2ProviderCheckoutExecutionResult> ExecuteAsync(
        BillingV2ProviderCheckoutExecutionRequest request,
        CancellationToken cancellationToken);
}

public sealed class DisabledBillingV2ProviderCheckoutExecutor
    : IBillingV2ProviderCheckoutExecutor
{
    public static DisabledBillingV2ProviderCheckoutExecutor Instance { get; }
        = new();

    private DisabledBillingV2ProviderCheckoutExecutor()
    {
    }

    public bool CanExecute => false;

    public Task<BillingV2ProviderCheckoutExecutionResult> ExecuteAsync(
        BillingV2ProviderCheckoutExecutionRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(new BillingV2ProviderCheckoutExecutionResult(
            false,
            "BILLING_V2_PROVIDER_EXECUTOR_DISABLED",
            ProviderCheckoutId: null,
            ProviderSubscriptionId: null,
            ApprovalUrl: null,
            ErrorMessage: "Billing V2 provider executor is disabled."));
}

public sealed class BillingV2ProviderCheckoutExecutor
    : IBillingV2ProviderCheckoutExecutor
{
    public const string HttpClientName = "billing-v2-provider";

    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly IHttpClientFactory _httpClientFactory;

    public BillingV2ProviderCheckoutExecutor(
        BillingV2RuntimeConfiguration runtime,
        PayPalRuntimeConfiguration paypal,
        StripeRuntimeConfiguration stripe,
        IHttpClientFactory httpClientFactory)
    {
        _runtime = runtime;
        _paypal = paypal;
        _stripe = stripe;
        _httpClientFactory = httpClientFactory;
    }

    public bool CanExecute =>
        _runtime.ProviderExecutorEnabled
        && (
            _stripe.IsConfigured
            || _paypal.IsConfigured
        );

    public async Task<BillingV2ProviderCheckoutExecutionResult> ExecuteAsync(
        BillingV2ProviderCheckoutExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanExecute)
        {
            return await DisabledBillingV2ProviderCheckoutExecutor.Instance
                .ExecuteAsync(request, cancellationToken);
        }

        var payload = BillingV2ProviderCheckoutPayload.Parse(
            request.PayloadText);
        return payload.Provider switch
        {
            "stripe" => await ExecuteStripeAsync(
                request,
                payload,
                cancellationToken),
            "paypal" => await ExecutePayPalAsync(
                request,
                payload,
                cancellationToken),
            _ => new BillingV2ProviderCheckoutExecutionResult(
                false,
                "BILLING_V2_PROVIDER_UNSUPPORTED",
                ProviderCheckoutId: null,
                ProviderSubscriptionId: null,
                ApprovalUrl: null,
                ErrorMessage: $"Provider '{payload.Provider}' is unsupported.")
        };
    }

    private async Task<BillingV2ProviderCheckoutExecutionResult>
        ExecuteStripeAsync(
            BillingV2ProviderCheckoutExecutionRequest request,
            BillingV2ProviderCheckoutPayload payload,
            CancellationToken cancellationToken)
    {
        if (!_stripe.IsConfigured)
        {
            return new BillingV2ProviderCheckoutExecutionResult(
                false,
                "BILLING_V2_STRIPE_NOT_CONFIGURED",
                ProviderCheckoutId: null,
                ProviderSubscriptionId: null,
                ApprovalUrl: null,
                ErrorMessage: "Stripe is not configured.");
        }

        var httpRequest = BillingV2StripeCheckoutRequestBuilder.Build(
            request,
            payload,
            _stripe.SecretKey!);
        using var message = ToHttpRequestMessage(httpRequest);
        var response = await _httpClientFactory
            .CreateClient(HttpClientName)
            .SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Failed("BILLING_V2_STRIPE_REQUEST_FAILED", body);
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var sessionId = root.GetProperty("id").GetString();
        var approvalUrl = root.TryGetProperty("url", out var urlElement)
            ? urlElement.GetString()
            : null;
        return string.IsNullOrWhiteSpace(sessionId)
            || string.IsNullOrWhiteSpace(approvalUrl)
            ? Failed(
                "BILLING_V2_STRIPE_RESPONSE_INVALID",
                "Stripe did not return a checkout session id and URL.")
            : new BillingV2ProviderCheckoutExecutionResult(
                true,
                "BILLING_V2_PROVIDER_CHECKOUT_CREATED",
                sessionId,
                ProviderSubscriptionId: null,
                approvalUrl,
                ErrorMessage: null);
    }

    private async Task<BillingV2ProviderCheckoutExecutionResult>
        ExecutePayPalAsync(
            BillingV2ProviderCheckoutExecutionRequest request,
            BillingV2ProviderCheckoutPayload payload,
            CancellationToken cancellationToken)
    {
        if (!_paypal.IsConfigured)
        {
            return new BillingV2ProviderCheckoutExecutionResult(
                false,
                "BILLING_V2_PAYPAL_NOT_CONFIGURED",
                ProviderCheckoutId: null,
                ProviderSubscriptionId: null,
                ApprovalUrl: null,
                ErrorMessage: "PayPal is not configured.");
        }

        var tokenResult = await CreatePayPalAccessTokenAsync(cancellationToken);
        if (!tokenResult.Succeeded || string.IsNullOrWhiteSpace(tokenResult.Token))
        {
            return Failed(tokenResult.Code, tokenResult.ErrorMessage);
        }

        var httpRequest = BillingV2PayPalSubscriptionRequestBuilder.Build(
            request,
            payload,
            _paypal.ApiBaseUrl,
            tokenResult.Token);
        using var message = ToHttpRequestMessage(httpRequest);
        var response = await _httpClientFactory
            .CreateClient(HttpClientName)
            .SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Failed("BILLING_V2_PAYPAL_REQUEST_FAILED", body);
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var subscriptionId = root.GetProperty("id").GetString();
        var approvalUrl = ReadPayPalApprovalUrl(root);
        return string.IsNullOrWhiteSpace(subscriptionId)
            || string.IsNullOrWhiteSpace(approvalUrl)
            ? Failed(
                "BILLING_V2_PAYPAL_RESPONSE_INVALID",
                "PayPal did not return a subscription id and approval URL.")
            : new BillingV2ProviderCheckoutExecutionResult(
                true,
                "BILLING_V2_PROVIDER_CHECKOUT_CREATED",
                ProviderCheckoutId: null,
                subscriptionId,
                approvalUrl,
                ErrorMessage: null);
    }

    private async Task<PayPalTokenResult> CreatePayPalAccessTokenAsync(
        CancellationToken cancellationToken)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{_paypal.ClientId}:{_paypal.ClientSecret}"));
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_paypal.ApiBaseUrl}/v1/oauth2/token")
        {
            Content = new StringContent(
                "grant_type=client_credentials",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        var response = await _httpClientFactory
            .CreateClient(HttpClientName)
            .SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new PayPalTokenResult(
                false,
                "BILLING_V2_PAYPAL_AUTH_FAILED",
                Token: null,
                body);
        }

        using var document = JsonDocument.Parse(body);
        var token = document.RootElement.TryGetProperty(
                "access_token",
                out var tokenElement)
            ? tokenElement.GetString()
            : null;
        return string.IsNullOrWhiteSpace(token)
            ? new PayPalTokenResult(
                false,
                "BILLING_V2_PAYPAL_AUTH_RESPONSE_INVALID",
                Token: null,
                "PayPal did not return an access token.")
            : new PayPalTokenResult(
                true,
                "BILLING_V2_PAYPAL_AUTH_OK",
                token,
                ErrorMessage: null);
    }

    private static string? ReadPayPalApprovalUrl(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var links)
            || links.ValueKind is not JsonValueKind.Array)
        {
            return null;
        }

        foreach (var link in links.EnumerateArray())
        {
            if (link.TryGetProperty("rel", out var rel)
                && string.Equals(
                    rel.GetString(),
                    "approve",
                    StringComparison.OrdinalIgnoreCase)
                && link.TryGetProperty("href", out var href))
            {
                return href.GetString();
            }
        }

        return null;
    }

    private static HttpRequestMessage ToHttpRequestMessage(
        BillingV2ProviderHttpRequest request)
    {
        var message = new HttpRequestMessage(
            new HttpMethod(request.Method),
            request.Url)
        {
            Content = new StringContent(
                request.Body,
                Encoding.UTF8,
                request.Headers.TryGetValue("Content-Type", out var contentType)
                    ? contentType
                    : "application/json")
        };
        foreach (var (name, value) in request.Headers)
        {
            if (string.Equals(name, "Content-Type", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(name, "Idempotency-Key", StringComparison.Ordinal))
            {
                message.Headers.TryAddWithoutValidation(name, value);
                continue;
            }

            if (string.Equals(name, "Authorization", StringComparison.Ordinal))
            {
                message.Headers.TryAddWithoutValidation(name, value);
                continue;
            }

            message.Headers.Add(name, value);
        }

        return message;
    }

    private static BillingV2ProviderCheckoutExecutionResult Failed(
        string code,
        string? message)
        => new(
            false,
            code,
            ProviderCheckoutId: null,
            ProviderSubscriptionId: null,
            ApprovalUrl: null,
            ErrorMessage: message);

    private sealed record PayPalTokenResult(
        bool Succeeded,
        string Code,
        string? Token,
        string? ErrorMessage);
}

public sealed record BillingV2ProviderCheckoutPayload(
    string SubscriptionId,
    string CustomerId,
    string CustomerEmail,
    string Provider,
    string Environment,
    string Currency,
    long RecurringAmountCents,
    long OneTimeAmountCents,
    long TotalDueNowCents,
    string SuccessUrl,
    string CancelUrl,
    string CorrelationId,
    IReadOnlyList<BillingV2ProviderCheckoutPayloadLine> Lines)
{
    public static BillingV2ProviderCheckoutPayload Parse(string payloadText)
        => JsonSerializer.Deserialize<BillingV2ProviderCheckoutPayload>(
               payloadText,
               JsonOptions)
           ?? throw new InvalidOperationException(
               "Billing V2 provider checkout payload is invalid.");

    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web);
}

public sealed record BillingV2ProviderCheckoutPayloadLine(
    string ServicePriceId,
    string ProviderExternalId,
    int Quantity,
    long AmountCents);

public static class BillingV2StripeCheckoutRequestBuilder
{
    public static BillingV2ProviderHttpRequest Build(
        BillingV2ProviderCheckoutExecutionRequest request,
        BillingV2ProviderCheckoutPayload payload,
        string secretKey)
    {
        var parameters = new Dictionary<string, string>
        {
            ["mode"] = "subscription",
            ["customer_email"] = payload.CustomerEmail,
            ["success_url"] = payload.SuccessUrl,
            ["cancel_url"] = payload.CancelUrl,
            ["metadata[billing_v2_subscription_id]"] = payload.SubscriptionId,
            ["metadata[customer_id]"] = payload.CustomerId,
            ["subscription_data[metadata][billing_v2_subscription_id]"] =
                payload.SubscriptionId,
            ["subscription_data[metadata][customer_id]"] = payload.CustomerId
        };

        for (var index = 0; index < payload.Lines.Count; index++)
        {
            var line = payload.Lines[index];
            parameters[$"line_items[{index}][price]"] =
                line.ProviderExternalId;
            parameters[$"line_items[{index}][quantity]"] =
                line.Quantity.ToString();
        }

        return new BillingV2ProviderHttpRequest(
            "stripe",
            "POST",
            "https://api.stripe.com/v1/checkout/sessions",
            new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {secretKey}",
                ["Content-Type"] = "application/x-www-form-urlencoded",
                ["Idempotency-Key"] = request.IdempotencyKeyHash
            },
            string.Join(
                "&",
                parameters.Select(pair =>
                    $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")));
    }
}

public static class BillingV2PayPalSubscriptionRequestBuilder
{
    public static BillingV2ProviderHttpRequest Build(
        BillingV2ProviderCheckoutExecutionRequest request,
        BillingV2ProviderCheckoutPayload payload,
        string apiBaseUrl,
        string accessToken)
    {
        var planIds = payload.Lines
            .Select(line => line.ProviderExternalId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (planIds.Length != 1)
        {
            throw new InvalidOperationException(
                "PayPal Billing V2 subscription checkout requires exactly one provider plan id.");
        }

        var body = JsonSerializer.Serialize(
            new
            {
                plan_id = planIds[0],
                custom_id = payload.SubscriptionId,
                subscriber = new { email_address = payload.CustomerEmail },
                application_context = new
                {
                    return_url = payload.SuccessUrl,
                    cancel_url = payload.CancelUrl,
                    user_action = "SUBSCRIBE_NOW"
                }
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return new BillingV2ProviderHttpRequest(
            "paypal",
            "POST",
            $"{apiBaseUrl}/v1/billing/subscriptions",
            new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {accessToken}",
                ["Content-Type"] = "application/json",
                ["Idempotency-Key"] = request.IdempotencyKeyHash,
                ["PayPal-Request-Id"] = request.IdempotencyKeyHash
            },
            body);
    }
}
