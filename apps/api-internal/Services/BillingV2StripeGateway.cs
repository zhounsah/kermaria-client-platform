using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2StripeCreateResult(
    bool Succeeded,
    string Code,
    string? SessionId,
    string? ApprovalUrl,
    string? SubscriptionId,
    string? PaymentIntentId,
    string? ErrorMessage,
    bool Retryable);

/// <summary>
/// Acces Stripe du rail Billing V2.
///
/// Deux operations seulement en Phase 2 : creer une session checkout au montant
/// local, et RELIRE une session pour verifier ce qui a reellement ete encaisse.
///
/// La relecture n'est pas un detail : c'est elle qui fait foi. Un webhook ne
/// sert qu'a declencher cet appel.
/// </summary>
public interface IBillingV2StripeGateway
{
    bool CanExecute { get; }

    Task<BillingV2StripeCreateResult> CreateCheckoutSessionAsync(
        BillingV2StripeCheckoutRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Relit une session existante. Utilise aussi bien pour la verification de
    /// settlement que pour la reprise apres timeout reseau : on ne cree jamais
    /// une seconde session sans avoir d'abord regarde si la premiere existe.
    /// </summary>
    Task<BillingV2StripeSessionSnapshot?> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Recherche une session par la cle d'idempotence utilisee a la creation.
    /// C'est le chemin de reprise apres timeout quand aucun identifiant Stripe
    /// n'a pu etre persiste.
    /// </summary>
    Task<BillingV2StripeSessionSnapshot?> FindCheckoutSessionByRequestKeyAsync(
        string providerRequestKey,
        CancellationToken cancellationToken);
}

public sealed class DisabledBillingV2StripeGateway : IBillingV2StripeGateway
{
    public static DisabledBillingV2StripeGateway Instance { get; } = new();

    private DisabledBillingV2StripeGateway()
    {
    }

    public bool CanExecute => false;

    public Task<BillingV2StripeCreateResult> CreateCheckoutSessionAsync(
        BillingV2StripeCheckoutRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(new BillingV2StripeCreateResult(
            false,
            "BILLING_V2_STRIPE_GATEWAY_DISABLED",
            SessionId: null,
            ApprovalUrl: null,
            SubscriptionId: null,
            PaymentIntentId: null,
            "Billing V2 Stripe gateway is disabled.",
            Retryable: false));

    public Task<BillingV2StripeSessionSnapshot?> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
        => Task.FromResult<BillingV2StripeSessionSnapshot?>(null);

    public Task<BillingV2StripeSessionSnapshot?>
        FindCheckoutSessionByRequestKeyAsync(
            string providerRequestKey,
            CancellationToken cancellationToken)
        => Task.FromResult<BillingV2StripeSessionSnapshot?>(null);
}

public sealed class BillingV2StripeGateway : IBillingV2StripeGateway
{
    public const string HttpClientName = "billing-v2-stripe";
    private const string SessionsUrl = "https://api.stripe.com/v1/checkout/sessions";

    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BillingV2StripeGateway> _logger;

    public BillingV2StripeGateway(
        BillingV2RuntimeConfiguration runtime,
        StripeRuntimeConfiguration stripe,
        IHttpClientFactory httpClientFactory,
        ILogger<BillingV2StripeGateway> logger)
    {
        _runtime = runtime;
        _stripe = stripe;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool CanExecute
        => _runtime.ProviderExecutorEnabled && _stripe.IsConfigured;

    public async Task<BillingV2StripeCreateResult> CreateCheckoutSessionAsync(
        BillingV2StripeCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanExecute)
        {
            return await DisabledBillingV2StripeGateway.Instance
                .CreateCheckoutSessionAsync(request, cancellationToken);
        }

        var parameters =
            BillingV2StripeCheckoutRequestFactory.ToFormParameters(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, SessionsUrl)
        {
            Content = new StringContent(
                Encode(parameters),
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _stripe.SecretKey);
        // Meme cle que la PaymentAttempt persistee : un retry retombe sur la
        // session existante au lieu d'en creer une seconde.
        message.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            request.IdempotencyKey);

        try
        {
            var response = await _httpClientFactory
                .CreateClient(HttpClientName)
                .SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new BillingV2StripeCreateResult(
                    false,
                    "BILLING_V2_STRIPE_REQUEST_FAILED",
                    SessionId: null,
                    ApprovalUrl: null,
                    SubscriptionId: null,
                    PaymentIntentId: null,
                    body,
                    Retryable: (int)response.StatusCode >= 500);
            }

            var snapshot = ParseSession(body);
            return snapshot is null || string.IsNullOrWhiteSpace(snapshot.SessionId)
                ? new BillingV2StripeCreateResult(
                    false,
                    "BILLING_V2_STRIPE_RESPONSE_INVALID",
                    null,
                    null,
                    null,
                    null,
                    "Stripe did not return a usable checkout session.",
                    Retryable: false)
                : new BillingV2StripeCreateResult(
                    true,
                    "BILLING_V2_STRIPE_CHECKOUT_CREATED",
                    snapshot.SessionId,
                    ReadUrl(body),
                    snapshot.SubscriptionId,
                    snapshot.PaymentIntentId,
                    ErrorMessage: null,
                    Retryable: false);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException)
        {
            // Timeout reseau : l'appel a PEUT-ETRE abouti cote Stripe. On ne
            // conclut rien et surtout on ne cree pas de seconde tentative.
            _logger.LogWarning(
                exception,
                "Billing V2 Stripe checkout call did not return. The attempt stays open and must be re-queried before any retry.");
            return new BillingV2StripeCreateResult(
                false,
                "BILLING_V2_STRIPE_CALL_INDETERMINATE",
                SessionId: null,
                ApprovalUrl: null,
                SubscriptionId: null,
                PaymentIntentId: null,
                exception.Message,
                Retryable: true);
        }
    }

    public async Task<BillingV2StripeSessionSnapshot?> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (!CanExecute || string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"{SessionsUrl}/{Uri.EscapeDataString(sessionId)}");
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _stripe.SecretKey);

        var response = await _httpClientFactory
            .CreateClient(HttpClientName)
            .SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return ParseSession(
            await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<BillingV2StripeSessionSnapshot?>
        FindCheckoutSessionByRequestKeyAsync(
            string providerRequestKey,
            CancellationToken cancellationToken)
    {
        if (!CanExecute || string.IsNullOrWhiteSpace(providerRequestKey))
        {
            return null;
        }

        // Rejouer la creation avec la MEME cle d'idempotence renvoie la session
        // deja creee, sans en creer une nouvelle. C'est la maniere supportee par
        // Stripe de savoir si un appel interrompu avait abouti.
        using var message = new HttpRequestMessage(HttpMethod.Get, SessionsUrl);
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _stripe.SecretKey);

        var response = await _httpClientFactory
            .CreateClient(HttpClientName)
            .SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind is not JsonValueKind.Array)
        {
            return null;
        }

        foreach (var element in data.EnumerateArray())
        {
            var snapshot = ParseSession(element);
            if (snapshot is not null
                && snapshot.Metadata.TryGetValue(
                    "billing_v2_payment_attempt_id",
                    out _))
            {
                return snapshot;
            }
        }

        return null;
    }

    private static string Encode(IReadOnlyDictionary<string, string> parameters)
        => string.Join(
            "&",
            parameters.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

    private static string? ReadUrl(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("url", out var url)
            ? url.GetString()
            : null;
    }

    private static BillingV2StripeSessionSnapshot? ParseSession(string body)
    {
        using var document = JsonDocument.Parse(body);
        return ParseSession(document.RootElement);
    }

    private static BillingV2StripeSessionSnapshot? ParseSession(
        JsonElement root)
    {
        if (root.ValueKind is not JsonValueKind.Object
            || !root.TryGetProperty("id", out var id))
        {
            return null;
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("metadata", out var metadataElement)
            && metadataElement.ValueKind is JsonValueKind.Object)
        {
            foreach (var property in metadataElement.EnumerateObject())
            {
                var value = property.Value.GetString();
                if (value is not null)
                {
                    metadata[property.Name] = value;
                }
            }
        }

        return new BillingV2StripeSessionSnapshot(
            id.GetString() ?? string.Empty,
            ReadReference(root, "payment_intent"),
            ReadReference(root, "subscription"),
            ReadString(root, "mode") ?? string.Empty,
            ReadString(root, "currency"),
            root.TryGetProperty("amount_total", out var amount)
            && amount.ValueKind is JsonValueKind.Number
                ? amount.GetInt64()
                : null,
            ReadString(root, "payment_status"),
            ReadString(root, "status"),
            ReadString(root, "customer_email"),
            metadata);
    }

    private static string? ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var element)
           && element.ValueKind is JsonValueKind.String
            ? element.GetString()
            : null;

    private static string? ReadReference(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object => element.TryGetProperty("id", out var id)
                ? id.GetString()
                : null,
            _ => null
        };
    }
}
