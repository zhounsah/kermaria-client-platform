using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Services.Provisioning;

/// <summary>
/// Issue d'une reconciliation, du point de vue de l'appelant.
/// </summary>
/// <remarks>
/// Les issues sont volontairement distinguables : un quota deja bon
/// (<see cref="Noop"/>) et un quota qu'on vient de poser
/// (<see cref="Applied"/>) sont tous deux des succes, mais confondre les deux
/// interdirait de constater qu'une reconciliation ne converge jamais. Les trois
/// issues suivantes ne sont pas des succes et ne doivent jamais etre comptees
/// comme telles.
/// </remarks>
public enum BillingV2KoxoStorageOutcome
{
    /// <summary>L'etat constate est deja l'etat desire.</summary>
    Noop,

    /// <summary>L'augmentation a ete appliquee ET verifiee.</summary>
    Applied,

    /// <summary>Le quota demande est inferieur au quota en place.</summary>
    BlockedReduction,

    /// <summary>L'objet KoXo vise n'existe pas.</summary>
    TargetNotFound,

    /// <summary>Toute autre issue, y compris l'absence de preuve.</summary>
    Failed,
}

/// <summary>
/// Niveau de preuve reellement obtenu apres une application.
/// </summary>
/// <remarks>
/// <see cref="XmlVerified"/> atteste que la fiche KoXo porte bien
/// <c>EnableFolderQuota=1</c> et le quota demande, relue apres coup.
/// <see cref="FullyVerified"/> ajoute la constatation du quota effectif cote
/// gestionnaire de ressources. La distinction est explicite parce que la
/// premiere ne prouve pas la seconde : KoXo peut avoir enregistre l'intention
/// sans que la limite soit posee sur le volume.
/// </remarks>
public static class BillingV2KoxoStorageVerification
{
    public const string None = "none";
    public const string XmlVerified = "xml_verified";
    public const string FullyVerified = "fully_verified";
}

public static class BillingV2KoxoStorageApplyReasons
{
    public const string Applied =
        "BILLING_V2_KOXO_STORAGE_APPLIED";

    public const string Noop =
        "BILLING_V2_KOXO_STORAGE_NOOP";

    public const string ProviderNotConfigured =
        "BILLING_V2_KOXO_STORAGE_PROVIDER_NOT_CONFIGURED";

    /// <summary>Le point d'entree a refuse l'authentification.</summary>
    public const string Unauthorized =
        "BILLING_V2_KOXO_STORAGE_UNAUTHORIZED";

    /// <summary>Transport injoignable, delai depasse, reponse illisible.</summary>
    public const string TransportFailed =
        "BILLING_V2_KOXO_STORAGE_TRANSPORT_FAILED";

    /// <summary>
    /// La reponse ne decrit pas la cible envoyee.
    /// </summary>
    /// <remarks>
    /// Un accuse de reception qui ne renvoie pas la meme cible ne prouve rien
    /// sur elle. Le rapprochement est fait sur la cle de cible, pas sur l'ordre
    /// des reponses.
    /// </remarks>
    public const string ResponseMismatch =
        "BILLING_V2_KOXO_STORAGE_RESPONSE_MISMATCH";

    /// <summary>
    /// La cible n'a pas ete tentee parce qu'une precedente a echoue.
    /// </summary>
    public const string NotAttempted =
        "BILLING_V2_KOXO_STORAGE_NOT_ATTEMPTED";

    /// <summary>
    /// Le lot contient au moins une cible non appliquee.
    /// </summary>
    public const string BatchIncomplete =
        "BILLING_V2_KOXO_STORAGE_BATCH_INCOMPLETE";
}

public sealed record BillingV2KoxoStorageTargetResult(
    string SubscriptionItemId,
    string TargetKey,
    BillingV2KoxoStorageOutcome Outcome,
    string ReasonCode,
    string Verification)
{
    public bool Succeeded => Outcome is BillingV2KoxoStorageOutcome.Noop
        or BillingV2KoxoStorageOutcome.Applied;
}

/// <summary>
/// Resultat d'un lot de reconciliation.
/// </summary>
/// <remarks>
/// <see cref="Succeeded"/> est calcule, jamais fourni : un lot n'est un succes
/// que si CHACUNE de ses cibles en est un. Un succes partiel presente comme un
/// succes global laisserait un abonnement passer pour provisionne alors qu'une
/// partie de son stockage ne l'est pas.
/// </remarks>
public sealed record BillingV2KoxoStorageApplyResult(
    string ReasonCode,
    IReadOnlyList<BillingV2KoxoStorageTargetResult> Results)
{
    public bool Succeeded => Results.All(result => result.Succeeded);

    public static BillingV2KoxoStorageApplyResult Noop()
        => new(
            BillingV2KoxoStorageApplyReasons.Noop,
            Array.Empty<BillingV2KoxoStorageTargetResult>());

    public static BillingV2KoxoStorageApplyResult Fail(string reasonCode)
        => new(
            reasonCode,
            new[]
            {
                new BillingV2KoxoStorageTargetResult(
                    SubscriptionItemId: string.Empty,
                    TargetKey: string.Empty,
                    BillingV2KoxoStorageOutcome.Failed,
                    reasonCode,
                    BillingV2KoxoStorageVerification.None),
            });

    public static BillingV2KoxoStorageApplyResult From(
        IReadOnlyList<BillingV2KoxoStorageTargetResult> results)
        => new(
            results.All(result => result.Succeeded)
                ? BillingV2KoxoStorageApplyReasons.Applied
                : BillingV2KoxoStorageApplyReasons.BatchIncomplete,
            results);
}

public sealed record BillingV2KoxoStorageGateDecision(
    bool MayContinue,
    string ReasonCode);

/// <summary>
/// Decide si le provisioning peut se poursuivre apres l'etape de stockage.
/// </summary>
/// <remarks>
/// Extrait de l'orchestration pour rester verifiable sans base ni annuaire.
/// L'invariant tient en une phrase : tant que le socle de stockage n'est pas
/// integralement en place, aucun droit qui en depend n'est accorde. Un acces
/// VPN ou RDS vers un environnement personnel absent ouvre une session sur un
/// poste vide, ce qui se lit comme une panne cote client.
/// </remarks>
public static class BillingV2KoxoStorageGate
{
    public const string NotRequired =
        "BILLING_V2_KOXO_STORAGE_NOT_REQUIRED";

    public const string Ready =
        "BILLING_V2_KOXO_STORAGE_READY";

    public static BillingV2KoxoStorageGateDecision Evaluate(
        int storagePlanCount,
        BillingV2KoxoStorageTargetResolution? resolution,
        BillingV2KoxoStorageApplyResult? applied)
    {
        if (storagePlanCount == 0)
        {
            return new BillingV2KoxoStorageGateDecision(true, NotRequired);
        }

        // Une resolution absente n'est pas un feu vert : elle signale une
        // orchestration qui a saute l'etape.
        if (resolution is null || !resolution.Resolved)
        {
            return new BillingV2KoxoStorageGateDecision(
                false,
                resolution?.ReasonCode
                    ?? BillingV2KoxoStorageApplyReasons.NotAttempted);
        }

        if (applied is null || !applied.Succeeded)
        {
            return new BillingV2KoxoStorageGateDecision(
                false,
                applied?.ReasonCode
                    ?? BillingV2KoxoStorageApplyReasons.NotAttempted);
        }

        return new BillingV2KoxoStorageGateDecision(true, Ready);
    }
}

/// <summary>
/// Configuration du point d'entree de reconciliation cible.
/// </summary>
/// <remarks>
/// <para>
/// Absente, la configuration laisse le provider dormant : il n'y a pas de repli
/// implicite. Une adresse par defaut devinee ferait porter un quota reel a un
/// hote non valide.
/// </para>
/// <para>
/// L'URL vise l'operation CIBLEE. La synchronisation globale par CSV vit sur une
/// autre route et n'est jamais appelee par ce chemin : elle reconcilie
/// l'ensemble de l'annuaire et une desactivation de masse ne se rattrape pas.
/// </para>
/// </remarks>
public sealed record BillingV2KoxoStorageProviderConfiguration(
    Uri? Url,
    string? BearerToken,
    TimeSpan Timeout)
{
    public const string HttpClientName = "billing-v2-koxo-storage";

    private const int DefaultTimeoutSeconds = 180;
    private const int MinimumTimeoutSeconds = 10;
    private const int MaximumTimeoutSeconds = 600;

    public bool Configured => Url is not null
        && !string.IsNullOrWhiteSpace(BearerToken);

    public static BillingV2KoxoStorageProviderConfiguration Resolve(
        IConfiguration configuration)
    {
        var urlValue = configuration["BILLING_V2_KOXO_STORAGE_URL"]?.Trim();
        var tokenValue = configuration["BILLING_V2_KOXO_STORAGE_TOKEN"]?.Trim();
        var allowInsecureHttp = ParseBool(
            configuration["BILLING_V2_KOXO_STORAGE_ALLOW_INSECURE_HTTP"]);

        var timeout = TimeSpan.FromSeconds(ParseInt(
            configuration["BILLING_V2_KOXO_STORAGE_TIMEOUT_SECONDS"],
            DefaultTimeoutSeconds,
            MinimumTimeoutSeconds,
            MaximumTimeoutSeconds));

        if (string.IsNullOrWhiteSpace(urlValue)
            && string.IsNullOrWhiteSpace(tokenValue))
        {
            return new BillingV2KoxoStorageProviderConfiguration(
                Url: null,
                BearerToken: null,
                timeout);
        }

        // Une configuration a moitie posee est une erreur d'exploitation, pas
        // une intention de rester dormant : la signaler vaut mieux que de
        // retomber silencieusement sur le provider inerte.
        if (string.IsNullOrWhiteSpace(urlValue)
            || !Uri.TryCreate(urlValue, UriKind.Absolute, out var url))
        {
            throw new InvalidOperationException(
                "BILLING_V2_KOXO_STORAGE_URL is invalid.");
        }

        if (!string.Equals(
                url.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase)
            && !IsLoopback(url)
            && !allowInsecureHttp)
        {
            throw new InvalidOperationException(
                "BILLING_V2_KOXO_STORAGE_URL must use HTTPS unless BILLING_V2_KOXO_STORAGE_ALLOW_INSECURE_HTTP=true.");
        }

        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            throw new InvalidOperationException(
                "BILLING_V2_KOXO_STORAGE_TOKEN is required.");
        }

        return new BillingV2KoxoStorageProviderConfiguration(
            url,
            tokenValue,
            timeout);
    }

    private static bool IsLoopback(Uri url)
        => string.Equals(url.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(url.Host, "127.0.0.1", StringComparison.Ordinal)
            || string.Equals(url.Host, "::1", StringComparison.Ordinal);

    private static bool ParseBool(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            _ => false,
        };

    private static int ParseInt(
        string? value,
        int fallback,
        int minimum,
        int maximum)
        => int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;
}

/// <summary>
/// Applique des quotas deja resolus en appelant l'operation ciblee de SRV-21.
/// </summary>
/// <remarks>
/// <para>
/// Une requete par cible, sequentiellement : <c>KoXoAdm.exe</c> ne supporte pas
/// deux instances concurrentes, et le recepteur serialise deja par un verrou.
/// Paralleliser ne gagnerait rien et transformerait la contention en echecs.
/// </para>
/// <para>
/// La premiere cible non appliquee arrete le lot. Les suivantes sont rendues
/// explicitement comme non tentees : les declarer echouees pretendrait un
/// constat qui n'a pas eu lieu, et les omettre ferait croire a un lot plus
/// petit qu'il n'est.
/// </para>
/// </remarks>
public sealed class HttpBillingV2KoxoStorageProvider
    : IBillingV2KoxoStorageProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BillingV2KoxoStorageProviderConfiguration _configuration;
    private readonly ILogger<HttpBillingV2KoxoStorageProvider> _logger;

    public HttpBillingV2KoxoStorageProvider(
        IHttpClientFactory httpClientFactory,
        BillingV2KoxoStorageProviderConfiguration configuration,
        ILogger<HttpBillingV2KoxoStorageProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public BillingV2KoxoStorageReadiness CheckReadiness(
        IReadOnlyList<BillingV2StorageQuotaPlan> quotas)
    {
        if (quotas.Count == 0)
        {
            return new BillingV2KoxoStorageReadiness(
                CanApplyQuotas: true,
                BillingV2KoxoStorageApplyReasons.Noop);
        }

        return _configuration.Configured
            ? new BillingV2KoxoStorageReadiness(
                CanApplyQuotas: true,
                BillingV2KoxoStorageApplyReasons.Applied)
            : new BillingV2KoxoStorageReadiness(
                CanApplyQuotas: false,
                BillingV2KoxoStorageApplyReasons.ProviderNotConfigured);
    }

    public async Task<BillingV2KoxoStorageApplyResult> ApplyAsync(
        IReadOnlyList<BillingV2ResolvedKoxoStorageTarget> targets,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
        {
            return BillingV2KoxoStorageApplyResult.Noop();
        }

        if (!_configuration.Configured)
        {
            return BillingV2KoxoStorageApplyResult.Fail(
                BillingV2KoxoStorageApplyReasons.ProviderNotConfigured);
        }

        var client = _httpClientFactory.CreateClient(
            BillingV2KoxoStorageProviderConfiguration.HttpClientName);
        var results = new List<BillingV2KoxoStorageTargetResult>(targets.Count);
        var aborted = false;
        foreach (var target in targets)
        {
            if (aborted)
            {
                results.Add(new BillingV2KoxoStorageTargetResult(
                    target.SubscriptionItemId,
                    target.TargetKey,
                    BillingV2KoxoStorageOutcome.Failed,
                    BillingV2KoxoStorageApplyReasons.NotAttempted,
                    BillingV2KoxoStorageVerification.None));
                continue;
            }

            var result = await ReconcileAsync(
                client,
                target,
                correlationId,
                cancellationToken);
            results.Add(result);
            if (!result.Succeeded)
            {
                aborted = true;
            }
        }

        return BillingV2KoxoStorageApplyResult.From(results);
    }

    private async Task<BillingV2KoxoStorageTargetResult> ReconcileAsync(
        HttpClient client,
        BillingV2ResolvedKoxoStorageTarget target,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var request = new StorageReconcileRequest(
            correlationId,
            target.Kind == BillingV2KoxoStorageTargetKind.User
                ? "user"
                : "secondary_group",
            target.UserId,
            target.PrimaryGroup,
            target.SecondaryGroup,
            target.QuotaMebibytes,
            target.SubscriptionItemId,
            target.TargetKey);

        try
        {
            using var response = await client.PostAsJsonAsync(
                _configuration.Url,
                request,
                SerializerOptions,
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden)
            {
                return Failure(
                    target,
                    BillingV2KoxoStorageApplyReasons.Unauthorized);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<StorageReconcileResponse>(
                    SerializerOptions,
                    cancellationToken);
            if (payload is null)
            {
                return Failure(
                    target,
                    BillingV2KoxoStorageApplyReasons.TransportFailed);
            }

            // L'accuse doit designer la cible envoyee. Sans ce controle, une
            // reponse concernant un autre objet validerait celui-ci.
            if (!string.Equals(
                    payload.TargetKey?.Trim(),
                    target.TargetKey,
                    StringComparison.Ordinal))
            {
                return Failure(
                    target,
                    BillingV2KoxoStorageApplyReasons.ResponseMismatch);
            }

            var outcome = ParseOutcome(payload.Status);
            if (outcome is BillingV2KoxoStorageOutcome.Applied
                && !string.Equals(
                    payload.Verification?.Trim(),
                    BillingV2KoxoStorageVerification.XmlVerified,
                    StringComparison.Ordinal)
                && !string.Equals(
                    payload.Verification?.Trim(),
                    BillingV2KoxoStorageVerification.FullyVerified,
                    StringComparison.Ordinal))
            {
                // Une application sans preuve relue n'est pas une application.
                return Failure(
                    target,
                    BillingV2KoxoStorageApplyReasons.ResponseMismatch);
            }

            return new BillingV2KoxoStorageTargetResult(
                target.SubscriptionItemId,
                target.TargetKey,
                outcome,
                string.IsNullOrWhiteSpace(payload.ReasonCode)
                    ? BillingV2KoxoStorageApplyReasons.TransportFailed
                    : payload.ReasonCode.Trim(),
                string.IsNullOrWhiteSpace(payload.Verification)
                    ? BillingV2KoxoStorageVerification.None
                    : payload.Verification.Trim());
        }
        catch (Exception exception)
            when (exception is HttpRequestException
                or TaskCanceledException
                or JsonException)
        {
            // Ni l'adresse, ni le jeton, ni le contenu de la reponse ne sont
            // journalises : seule la nature de l'incident l'est.
            _logger.LogWarning(
                "Billing V2 KoXo storage reconcile failed for item {SubscriptionItemId} ({Kind}): {Error}.",
                target.SubscriptionItemId,
                target.Kind,
                exception.GetType().Name);
            return Failure(
                target,
                BillingV2KoxoStorageApplyReasons.TransportFailed);
        }
    }

    private static BillingV2KoxoStorageTargetResult Failure(
        BillingV2ResolvedKoxoStorageTarget target,
        string reasonCode)
        => new(
            target.SubscriptionItemId,
            target.TargetKey,
            BillingV2KoxoStorageOutcome.Failed,
            reasonCode,
            BillingV2KoxoStorageVerification.None);

    /// <summary>
    /// Traduit un statut de reponse, en refusant tout ce qui n'est pas connu.
    /// </summary>
    /// <remarks>
    /// Le defaut est <see cref="BillingV2KoxoStorageOutcome.Failed"/> : un
    /// statut inconnu vient soit d'une version plus recente du recepteur, soit
    /// d'une reponse qui n'est pas la sienne. Aucun des deux ne justifie de
    /// conclure au succes.
    /// </remarks>
    private static BillingV2KoxoStorageOutcome ParseOutcome(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "noop" => BillingV2KoxoStorageOutcome.Noop,
            "applied" => BillingV2KoxoStorageOutcome.Applied,
            "blocked_reduction" => BillingV2KoxoStorageOutcome.BlockedReduction,
            "not_materialized" or "target_not_found"
                => BillingV2KoxoStorageOutcome.TargetNotFound,
            _ => BillingV2KoxoStorageOutcome.Failed,
        };

    private sealed record StorageReconcileRequest(
        string CorrelationId,
        string TargetKind,
        string? UserId,
        string? PrimaryGroup,
        string? SecondaryGroup,
        long DesiredQuotaMib,
        string SubscriptionItemId,
        string TargetKey);

    private sealed record StorageReconcileResponse(
        string? Status,
        string? ReasonCode,
        string? Verification,
        string? TargetKey);
}
