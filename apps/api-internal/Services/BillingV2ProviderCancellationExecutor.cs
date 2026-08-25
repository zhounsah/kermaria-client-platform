using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Appel de resiliation chez Stripe et PayPal, depuis API-INTERNAL.
/// </summary>
/// <remarks>
/// <para>
/// La logique fournisseur vit ici et non dans le BFF : le portail n'a ni les
/// secrets, ni les identifiants fournisseur persistes, ni le droit de conclure
/// seul qu'un abonnement a cesse d'etre facturable.
/// </para>
/// <para>
/// Rien n'est deduit d'un code HTTP heureux et flou. Un <c>404</c> ne vaut
/// « objet deja absent » qu'apres avoir prouve que l'appel est parti dans le
/// BON environnement fournisseur — sans quoi il signifie seulement « pas dans
/// cet environnement-ci » et l'abonnement LIVE continue d'etre preleve. Un
/// <c>4xx</c> d'autorisation ou de validation n'est PAS retryable : reessayer
/// huit fois ne le reparera pas. Tout le reste — reseau, 5xx, 429 — est
/// retryable.
/// </para>
/// </remarks>
public sealed class BillingV2ProviderCancellationExecutor
    : IBillingV2ProviderCancellationExecutor
{
    public const string HttpClientName =
        BillingV2ProviderCheckoutExecutor.HttpClientName;

    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly IHttpClientFactory _httpClientFactory;

    public BillingV2ProviderCancellationExecutor(
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
        && (_stripe.IsConfigured || _paypal.IsConfigured);

    public async Task<BillingV2ProviderCancellationResult> CancelAsync(
        BillingV2ProviderCancellationRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanExecute)
        {
            return await DisabledBillingV2ProviderCancellationExecutor.Instance
                .CancelAsync(request, cancellationToken);
        }

        if (!BillingV2ProviderEnvironmentPolicy.IsSupported(
                request.Provider,
                request.Environment))
        {
            // Un couple impossible ne se repare pas par un retry : il vient
            // d'une donnee incoherente, pas d'un incident reseau.
            return new BillingV2ProviderCancellationResult(
                false,
                "BILLING_V2_PROVIDER_ENVIRONMENT_UNSUPPORTED",
                $"Provider '{request.Provider}' / environment "
                    + $"'{request.Environment}' is not a supported pair.",
                Retryable: false);
        }

        return request.Provider switch
        {
            "stripe" => await CancelStripeAsync(request, cancellationToken),
            "paypal" => await CancelPayPalAsync(request, cancellationToken),
            _ => new BillingV2ProviderCancellationResult(
                false,
                "BILLING_V2_PROVIDER_UNSUPPORTED",
                $"Provider '{request.Provider}' is unsupported.",
                Retryable: false)
        };
    }

    /// <remarks>
    /// Stripe distingue deux gestes. <c>DELETE /v1/subscriptions/{id}</c> coupe
    /// tout de suite. <c>POST</c> avec <c>cancel_at_period_end=true</c> laisse
    /// courir la periode deja payee et arrete le renouvellement suivant — et
    /// cette promesse est tenue par Stripe lui-meme, elle ne depend d'aucun
    /// rappel de notre part.
    /// </remarks>
    private async Task<BillingV2ProviderCancellationResult> CancelStripeAsync(
        BillingV2ProviderCancellationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_stripe.IsConfigured)
        {
            return new BillingV2ProviderCancellationResult(
                false,
                "BILLING_V2_STRIPE_NOT_CONFIGURED",
                "Stripe is not configured.",
                Retryable: true);
        }

        // Le couple peut etre theoriquement valide ET faux ici : ce qui compte
        // est l'environnement REELLEMENT charge dans ce processus. Le controle
        // precede tout appel HTTP, parce qu'aucune lecture de reponse ne
        // rattrape ensuite l'ambiguite d'un 404.
        var mismatch = BillingV2ProviderRuntimeEnvironmentPolicy.Check(
            request.Provider,
            request.Environment,
            _stripe.ModeName);
        if (mismatch is not null)
        {
            return mismatch;
        }

        var url =
            $"https://api.stripe.com/v1/subscriptions/"
            + Uri.EscapeDataString(request.ProviderSubscriptionId);

        HttpRequestMessage message;
        switch (request.Operation)
        {
            case BillingV2CancellationOperations.CancelImmediate:
            case BillingV2CancellationOperations.CancelAtTerm:
                message = new HttpRequestMessage(HttpMethod.Delete, url);
                break;
            case BillingV2CancellationOperations.CancelAtPeriodEnd:
                message = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(
                        "cancel_at_period_end=true",
                        Encoding.UTF8,
                        "application/x-www-form-urlencoded")
                };
                break;
            default:
                // `suspend_pending_term_end` n'a pas d'equivalent Stripe et ne
                // doit jamais etre planifie pour ce rail. Le traduire « au
                // mieux » masquerait un bug de planification.
                return new BillingV2ProviderCancellationResult(
                    false,
                    "BILLING_V2_STRIPE_OPERATION_UNSUPPORTED",
                    $"Operation '{request.Operation}' is not supported on "
                        + "Stripe.",
                    Retryable: false);
        }

        using (message)
        {
            message.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _stripe.SecretKey);
            return await SendAsync(
                message,
                "BILLING_V2_STRIPE",
                convergenceProbe: null,
                cancellationToken);
        }
    }

    /// <remarks>
    /// <para>
    /// PayPal n'a pas de « cancel at period end ». Une demande a fin de terme
    /// se joue donc en deux temps : <c>/suspend</c> maintenant, pour qu'aucun
    /// renouvellement ne parte, puis <c>/cancel</c> au terme — planifie comme un
    /// second evenement d'outbox dormant, pas comme une promesse en commentaire.
    /// </para>
    /// <para>
    /// Un rejeu apres crash retombe sur un abonnement deja suspendu ou deja
    /// resilie, et PayPal repond alors <c>422</c>. Ce code ne peut pas etre
    /// traite en bloc comme un succes : il couvre aussi bien « rien a faire »
    /// que « geste impossible ». On releve donc l'etat reel avant de conclure.
    /// </para>
    /// </remarks>
    private async Task<BillingV2ProviderCancellationResult> CancelPayPalAsync(
        BillingV2ProviderCancellationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_paypal.IsConfigured)
        {
            return new BillingV2ProviderCancellationResult(
                false,
                "BILLING_V2_PAYPAL_NOT_CONFIGURED",
                "PayPal is not configured.",
                Retryable: true);
        }

        // Meme regle que pour Stripe : un abonnement persiste en `live` appele
        // avec des identifiants sandbox recevrait un 404 parfaitement legitime,
        // que la lecture naive prendrait pour une convergence.
        var mismatch = BillingV2ProviderRuntimeEnvironmentPolicy.Check(
            request.Provider,
            request.Environment,
            _paypal.ModeName);
        if (mismatch is not null)
        {
            return mismatch;
        }

        var action = request.Operation switch
        {
            BillingV2CancellationOperations.CancelImmediate => "cancel",
            BillingV2CancellationOperations.CancelAtTerm => "cancel",
            BillingV2CancellationOperations.SuspendPendingTermEnd => "suspend",
            _ => null
        };
        if (action is null)
        {
            // `cancel_at_period_end` n'existe pas chez PayPal : le planifier
            // pour ce rail laisserait un abonnement renouvelable.
            return new BillingV2ProviderCancellationResult(
                false,
                "BILLING_V2_PAYPAL_OPERATION_UNSUPPORTED",
                $"Operation '{request.Operation}' is not supported on PayPal.",
                Retryable: false);
        }

        var token = await CreatePayPalAccessTokenAsync(cancellationToken);
        if (token is null)
        {
            return new BillingV2ProviderCancellationResult(
                false,
                "BILLING_V2_PAYPAL_TOKEN_FAILED",
                "PayPal access token could not be obtained.",
                Retryable: true);
        }

        var url =
            $"{_paypal.ApiBaseUrl}/v1/billing/subscriptions/"
            + Uri.EscapeDataString(request.ProviderSubscriptionId)
            + $"/{action}";

        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { reason = request.Reason }),
                Encoding.UTF8,
                "application/json")
        };
        message.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return await SendAsync(
            message,
            "BILLING_V2_PAYPAL",
            convergenceProbe: () => ProbePayPalConvergenceAsync(
                request,
                token,
                cancellationToken),
            cancellationToken);
    }

    /// <param name="convergenceProbe">
    /// Relecture de l'etat fournisseur, appelee uniquement sur un code
    /// ambigu — typiquement le <c>422</c> que PayPal renvoie quand le geste a
    /// deja ete applique. Sans elle, un rejeu apres crash laisserait un
    /// abonnement en <c>pending_cancellation</c> perpetuel alors que le
    /// fournisseur ne peut plus rien prelever.
    /// </param>
    private async Task<BillingV2ProviderCancellationResult> SendAsync(
        HttpRequestMessage message,
        string codePrefix,
        Func<Task<BillingV2ProviderCancellationResult?>>? convergenceProbe,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClientFactory
                .CreateClient(HttpClientName)
                .SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException error)
        {
            return new BillingV2ProviderCancellationResult(
                false,
                $"{codePrefix}_TRANSPORT_FAILED",
                error.Message,
                Retryable: true);
        }
        catch (TaskCanceledException error)
            when (!cancellationToken.IsCancellationRequested)
        {
            return new BillingV2ProviderCancellationResult(
                false,
                $"{codePrefix}_TIMEOUT",
                error.Message,
                Retryable: true);
        }

        using (response)
        {
            var requiresConvergenceProbe =
                convergenceProbe is not null
                && (response.IsSuccessStatusCode
                    || response.StatusCode == HttpStatusCode.NotFound
                    || response.StatusCode == HttpStatusCode.Gone
                    || response.StatusCode == HttpStatusCode.UnprocessableEntity);
            if (requiresConvergenceProbe)
            {
                var probed = await convergenceProbe!();
                if (probed is not null)
                {
                    return probed;
                }

                if (response.IsSuccessStatusCode)
                {
                    return new BillingV2ProviderCancellationResult(
                        false,
                        $"{codePrefix}_CONVERGENCE_NOT_CONFIRMED",
                        $"HTTP {(int)response.StatusCode}: provider state did not confirm the requested cancellation action.",
                        Retryable: true);
                }
            }

            if (response.IsSuccessStatusCode)
            {
                return new BillingV2ProviderCancellationResult(
                    true,
                    "BILLING_V2_PROVIDER_CANCELLATION_ACCEPTED",
                    null,
                    Retryable: false);
            }

            if (convergenceProbe is null
                && (response.StatusCode == HttpStatusCode.NotFound
                    || response.StatusCode == HttpStatusCode.Gone))
            {
                return new BillingV2ProviderCancellationResult(
                    true,
                    "BILLING_V2_PROVIDER_SUBSCRIPTION_ALREADY_ABSENT",
                    null,
                    Retryable: false);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var retryable =
                response.StatusCode == HttpStatusCode.TooManyRequests
                || (int)response.StatusCode >= 500;

            return new BillingV2ProviderCancellationResult(
                false,
                $"{codePrefix}_CANCELLATION_FAILED",
                $"HTTP {(int)response.StatusCode}: {Truncate(body)}",
                retryable);
        }
    }

    /// <summary>
    /// Relit l'abonnement PayPal pour trancher un <c>422</c>.
    /// </summary>
    /// <returns>
    /// Un succes seulement si l'etat observe prouve que le geste demande est
    /// deja acquis. <c>null</c> si la relecture ne prouve rien : on retombe
    /// alors sur le traitement d'echec normal, ce qui laisse l'abonnement
    /// visible plutot que faussement clos.
    /// </returns>
    private async Task<BillingV2ProviderCancellationResult?>
        ProbePayPalConvergenceAsync(
            BillingV2ProviderCancellationRequest request,
            string token,
            CancellationToken cancellationToken)
    {
        var probe = await ReadPayPalSubscriptionStatusAsync(
            request.ProviderSubscriptionId,
            token,
            cancellationToken);
        if (probe is null)
        {
            return null;
        }

        if (probe.IsAbsent)
        {
            return new BillingV2ProviderCancellationResult(
                true,
                "BILLING_V2_PROVIDER_SUBSCRIPTION_ALREADY_ABSENT",
                null,
                Retryable: false);
        }

        var normalized = probe.Status?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized is "CANCELLED" or "EXPIRED")
        {
            return new BillingV2ProviderCancellationResult(
                true,
                "BILLING_V2_PROVIDER_SUBSCRIPTION_ALREADY_ABSENT",
                null,
                Retryable: false);
        }

        if (normalized is "SUSPENDED"
            && request.Operation is BillingV2CancellationOperations.SuspendPendingTermEnd)
        {
            return new BillingV2ProviderCancellationResult(
                true,
                "BILLING_V2_PROVIDER_SUBSCRIPTION_ALREADY_SUSPENDED",
                null,
                Retryable: false);
        }

        return null;
    }

    private async Task<PayPalSubscriptionProbe?> ReadPayPalSubscriptionStatusAsync(
        string providerSubscriptionId,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_paypal.ApiBaseUrl}/v1/billing/subscriptions/"
                + Uri.EscapeDataString(providerSubscriptionId));
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await _httpClientFactory
                .CreateClient(HttpClientName)
                .SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound
                || response.StatusCode == HttpStatusCode.Gone)
            {
                return new PayPalSubscriptionProbe(IsAbsent: true, Status: null);
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            var status = document.RootElement.TryGetProperty("status", out var element)
                ? element.GetString()
                : null;
            return new PayPalSubscriptionProbe(IsAbsent: false, status);
        }
        catch (Exception error)
            when (error is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private sealed record PayPalSubscriptionProbe(bool IsAbsent, string? Status);

    private async Task<string?> CreatePayPalAccessTokenAsync(
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

        try
        {
            using var response = await _httpClientFactory
                .CreateClient(HttpClientName)
                .SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty(
                    "access_token",
                    out var element)
                ? element.GetString()
                : null;
        }
        catch (Exception error)
            when (error is HttpRequestException or TaskCanceledException
                      or JsonException)
        {
            return null;
        }
    }

    // Le corps d'erreur fournisseur peut contenir des identifiants ; on n'en
    // garde qu'un extrait, suffisant pour diagnostiquer.
    private static string Truncate(string value)
        => value.Length <= 500 ? value : value[..500];
}
