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
    /// Reprise apres appel indetermine, par relecture BORNEE.
    ///
    /// Phase 3 : on ne balaye plus le compte Stripe. On ne relit que ce qui a
    /// ete persiste (session, payment intent, abonnement provider) ; sans
    /// aucun identifiant, on echoue en ferme et le retry normal repart avec la
    /// meme cle d'idempotence, que Stripe deduplique.
    /// </summary>
    Task<BillingV2StripeSessionSnapshot?> FindCheckoutSessionAsync(
        BillingV2StripeSessionLocator locator,
        CancellationToken cancellationToken);

    /// <summary>
    /// Relit l'abonnement Stripe. Necessaire des la Phase 3 : une session
    /// payee ne dit rien de l'etat de l'abonnement quelques heures plus tard.
    /// </summary>
    Task<BillingV2StripeSubscriptionSnapshot?> GetSubscriptionAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Relit une invoice Stripe. C'est LA preuve financiere d'un cycle : un
    /// renouvellement n'a pas de session checkout.
    /// </summary>
    Task<BillingV2StripeInvoiceSnapshot?> GetInvoiceAsync(
        string providerInvoiceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Derniere invoice connue d'un abonnement provider. Relecture bornee :
    /// un seul objet, cible par son identifiant.
    /// </summary>
    Task<BillingV2StripeInvoiceSnapshot?> GetLatestInvoiceForSubscriptionAsync(
        string providerSubscriptionId,
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

    public Task<BillingV2StripeSessionSnapshot?> FindCheckoutSessionAsync(
        BillingV2StripeSessionLocator locator,
        CancellationToken cancellationToken)
        => Task.FromResult<BillingV2StripeSessionSnapshot?>(null);

    public Task<BillingV2StripeSubscriptionSnapshot?> GetSubscriptionAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken)
        => Task.FromResult<BillingV2StripeSubscriptionSnapshot?>(null);

    public Task<BillingV2StripeInvoiceSnapshot?> GetInvoiceAsync(
        string providerInvoiceId,
        CancellationToken cancellationToken)
        => Task.FromResult<BillingV2StripeInvoiceSnapshot?>(null);

    public Task<BillingV2StripeInvoiceSnapshot?>
        GetLatestInvoiceForSubscriptionAsync(
            string providerSubscriptionId,
            CancellationToken cancellationToken)
        => Task.FromResult<BillingV2StripeInvoiceSnapshot?>(null);
}

public sealed class BillingV2StripeGateway : IBillingV2StripeGateway
{
    public const string HttpClientName = "billing-v2-stripe";
    private const string SessionsUrl = "https://api.stripe.com/v1/checkout/sessions";
    private const string SubscriptionsUrl = "https://api.stripe.com/v1/subscriptions";
    private const string InvoicesUrl = "https://api.stripe.com/v1/invoices";

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

    public async Task<BillingV2StripeSessionSnapshot?> FindCheckoutSessionAsync(
        BillingV2StripeSessionLocator locator,
        CancellationToken cancellationToken)
    {
        if (!CanExecute)
        {
            return null;
        }

        // La strategie est decidee hors reseau, donc testable seule.
        var plan = BillingV2StripeSessionLookupPolicy.Plan(locator);
        if (!plan.CanLookup || plan.Target is null)
        {
            // Fail closed : aucun identifiant persiste, donc aucun objet a
            // relire. On ne balaye PAS le compte Stripe pour deviner.
            _logger.LogWarning(
                "Billing V2 Stripe lookup refused: {ReasonCode}. The retry will reuse the same idempotency key instead.",
                plan.ReasonCode);
            return null;
        }

        return plan.Method switch
        {
            BillingV2StripeSessionLookupPolicy.MethodSession =>
                await GetCheckoutSessionAsync(plan.Target, cancellationToken),
            // Une session se retrouve depuis son payment intent ou son
            // abonnement par une requete FILTREE cote serveur - une seule page,
            // un seul objet attendu, pas un parcours du compte.
            BillingV2StripeSessionLookupPolicy.MethodPaymentIntent =>
                await FindSessionByFilterAsync(
                    "payment_intent",
                    plan.Target,
                    cancellationToken),
            BillingV2StripeSessionLookupPolicy.MethodSubscription =>
                await FindSessionByFilterAsync(
                    "subscription",
                    plan.Target,
                    cancellationToken),
            _ => null
        };
    }

    private async Task<BillingV2StripeSessionSnapshot?> FindSessionByFilterAsync(
        string filterName,
        string filterValue,
        CancellationToken cancellationToken)
    {
        var body = await GetAsync(
            $"{SessionsUrl}?{filterName}={Uri.EscapeDataString(filterValue)}&limit=1",
            cancellationToken);
        if (body is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind is not JsonValueKind.Array)
        {
            return null;
        }

        foreach (var element in data.EnumerateArray())
        {
            return ParseSession(element);
        }

        return null;
    }

    public async Task<BillingV2StripeSubscriptionSnapshot?>
        GetSubscriptionAsync(
            string providerSubscriptionId,
            CancellationToken cancellationToken)
    {
        if (!CanExecute || string.IsNullOrWhiteSpace(providerSubscriptionId))
        {
            return null;
        }

        var body = await GetAsync(
            $"{SubscriptionsUrl}/{Uri.EscapeDataString(providerSubscriptionId)}",
            cancellationToken);
        if (body is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return new BillingV2StripeSubscriptionSnapshot(
            ReadString(root, "id") ?? providerSubscriptionId,
            ReadString(root, "status") ?? "unknown",
            ReadReference(root, "customer"),
            ReadReference(root, "latest_invoice"),
            ReadMetadata(root));
    }

    public async Task<BillingV2StripeInvoiceSnapshot?> GetInvoiceAsync(
        string providerInvoiceId,
        CancellationToken cancellationToken)
    {
        if (!CanExecute || string.IsNullOrWhiteSpace(providerInvoiceId))
        {
            return null;
        }

        var body = await GetAsync(
            $"{InvoicesUrl}/{Uri.EscapeDataString(providerInvoiceId)}",
            cancellationToken);
        return body is null ? null : ParseInvoice(body);
    }

    public async Task<BillingV2StripeInvoiceSnapshot?>
        GetLatestInvoiceForSubscriptionAsync(
            string providerSubscriptionId,
            CancellationToken cancellationToken)
    {
        var subscription = await GetSubscriptionAsync(
            providerSubscriptionId,
            cancellationToken);
        return string.IsNullOrWhiteSpace(subscription?.LatestInvoiceId)
            ? null
            : await GetInvoiceAsync(
                subscription!.LatestInvoiceId!,
                cancellationToken);
    }

    private async Task<string?> GetAsync(
        string url,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _stripe.SecretKey);
        var response = await _httpClientFactory
            .CreateClient(HttpClientName)
            .SendAsync(message, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsStringAsync(cancellationToken)
            : null;
    }

    private static BillingV2StripeInvoiceSnapshot? ParseInvoice(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var id = ReadString(root, "id");
        if (id is null)
        {
            return null;
        }

        return new BillingV2StripeInvoiceSnapshot(
            id,
            ReadReference(root, "subscription"),
            ReadReference(root, "customer"),
            ReadString(root, "status") ?? "unknown",
            ReadString(root, "currency"),
            ReadLong(root, "amount_paid"),
            ReadLong(root, "amount_due"),
            ReadReference(root, "payment_intent"),
            ReadString(root, "billing_reason"),
            ReadMetadata(root),
            ReadInvoicePeriodStart(root));
    }

    /// <summary>
    /// Debut de la periode facturee. Sert uniquement a identifier DE QUEL
    /// cycle parle l'invoice ; le rang est ensuite calcule depuis l'ancre
    /// contractuelle locale, et le montant vient toujours du contrat.
    /// </summary>
    private static DateTime? ReadInvoicePeriodStart(JsonElement root)
    {
        // La periode de LIGNE fait foi, pas celle de l'entete. Mesure faite sur
        // une vraie facture `subscription_cycle` : l'entete portait la periode
        // PRECEDENTE (15/08 -> 15/09) tandis que la ligne portait la periode
        // reellement facturee (15/09 -> 15/10). Lire l'entete ramenait le cycle
        // au rang de la charge initiale, et le renouvellement n'etait jamais
        // facture.
        long? epoch = null;
        if (root.TryGetProperty("lines", out var lines)
            && lines.TryGetProperty("data", out var data)
            && data.ValueKind is JsonValueKind.Array)
        {
            foreach (var line in data.EnumerateArray())
            {
                if (line.TryGetProperty("period", out var period))
                {
                    epoch = ReadLong(period, "start");
                }

                break;
            }
        }

        epoch ??= ReadLong(root, "period_start");

        return epoch is null
            ? null
            : DateTimeOffset.FromUnixTimeSeconds(epoch.Value).UtcDateTime;
    }

    private static long? ReadLong(JsonElement root, string name)
        => root.TryGetProperty(name, out var element)
           && element.ValueKind is JsonValueKind.Number
            ? element.GetInt64()
            : null;

    private static IReadOnlyDictionary<string, string> ReadMetadata(
        JsonElement root)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("metadata", out var element)
            && element.ValueKind is JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var value = property.Value.GetString();
                if (value is not null)
                {
                    metadata[property.Name] = value;
                }
            }
        }

        return metadata;
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
            metadata,
            // L'URL d'approbation appartient a la session relue : sans elle,
            // une reprise laissait l'abonnement sans moyen de payer.
            ReadString(root, "url"));
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
