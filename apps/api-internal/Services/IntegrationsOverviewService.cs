using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Email;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Console d'observation des integrations : « observer et tester sans reveler
/// les secrets » (specification, section 16).
///
/// Trois regles tenues partout ici :
/// 1. aucun secret ne sort. Un secret est reduit a sa presence, jamais a un
///    prefixe ni a une longueur, qui aideraient a le reconstituer ;
/// 2. une absence de test est dite explicitement, avec sa raison. Une page qui
///    ne montre rien laisse croire que tout va bien ;
/// 3. rien n'est mutable ici. Les modes d'integration commandent des appels
///    reels chez des tiers : ils se changent sur la machine, puis au
///    redemarrage.
/// </summary>
public interface IIntegrationsOverviewService
{
    Task<IntegrationsOverview> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Envoi de test SMTP. Borne par l'allowlist live : c'est le seul garde-fou
    /// qui empeche d'ecrire a un vrai client depuis une page d'administration.
    /// </summary>
    Task<IntegrationTestResponse> SendSmtpTestAsync(
        string? recipient,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class IntegrationsOverviewService : IIntegrationsOverviewService
{
    private const int EmailLogSample = 50;

    private readonly EmailRuntimeConfiguration _email;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly BpceRuntimeConfiguration _bpce;
    private readonly KoxoSyncWebhookRuntimeConfiguration _koxo;
    private readonly BillingV2RuntimeConfiguration _billing;
    private readonly IEmailLogRepository _emailLog;
    private readonly IEmailService _emailService;
    private readonly IBackupRepository _backups;
    private readonly ILogger<IntegrationsOverviewService> _logger;

    public IntegrationsOverviewService(
        EmailRuntimeConfiguration email,
        StripeRuntimeConfiguration stripe,
        PayPalRuntimeConfiguration paypal,
        BpceRuntimeConfiguration bpce,
        KoxoSyncWebhookRuntimeConfiguration koxo,
        BillingV2RuntimeConfiguration billing,
        IEmailLogRepository emailLog,
        IEmailService emailService,
        IBackupRepository backups,
        ILogger<IntegrationsOverviewService> logger)
    {
        _email = email;
        _stripe = stripe;
        _paypal = paypal;
        _bpce = bpce;
        _koxo = koxo;
        _billing = billing;
        _emailLog = emailLog;
        _emailService = emailService;
        _backups = backups;
        _logger = logger;
    }

    public async Task<IntegrationsOverview> GetAsync(
        CancellationToken cancellationToken)
    {
        var emailLog = await ReadEmailLogAsync(cancellationToken);
        var veeam = await ReadVeeamAsync(cancellationToken);
        return new IntegrationsOverview(
            [
                BuildSmtp(emailLog),
                BuildStripe(),
                BuildPayPal(),
                BuildBpce(),
                veeam,
                BuildKoxo(),
            ],
            Iso(DateTime.UtcNow));
    }

    public async Task<IntegrationTestResponse> SendSmtpTestAsync(
        string? recipient,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalized = (recipient ?? string.Empty).Trim();
        if (normalized.Length is < 5 or > 254
            || !normalized.Contains('@', StringComparison.Ordinal))
        {
            return new IntegrationTestResponse(
                "SMTP_TEST_INVALID_RECIPIENT",
                "Adresse de destination invalide.",
                correlationId);
        }

        if (_email.Mode is EmailIntegrationMode.Disabled)
        {
            return new IntegrationTestResponse(
                "SMTP_TEST_DISABLED",
                "L'integration e-mail est desactivee : aucun envoi n'est tente.",
                correlationId);
        }

        // Garde-fou principal : meme en `live`, l'allowlist decide. Sans elle,
        // une page d'administration permettrait d'ecrire a n'importe qui.
        if (!_email.IsRecipientAllowed(normalized))
        {
            return new IntegrationTestResponse(
                "SMTP_TEST_BLOCKED_ALLOWLIST",
                "Ce destinataire n'est pas dans l'allowlist d'envoi. Le test est refuse.",
                correlationId);
        }

        var message = new EmailMessage(
            normalized,
            "Test de configuration SMTP",
            "Ce message confirme que la configuration SMTP du portail Kermaria "
            + "permet un envoi. Aucune action n'est attendue.",
            "smtp_test",
            null,
            correlationId);

        EmailDeliveryResult delivery;
        try
        {
            delivery = await _emailService.SendAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Envoi de test SMTP en echec.");
            delivery = new EmailDeliveryResult(false, "error", "Envoi impossible.");
        }

        try
        {
            await _emailLog.RecordAsync(
                message.Template,
                message.Recipient,
                message.Subject,
                message.Body,
                delivery.Status,
                delivery.ErrorMessage,
                null,
                correlationId,
                delivery.Succeeded,
                cancellationToken);
        }
        catch (Exception exception)
        {
            // La trace ne conditionne pas le resultat de l'envoi.
            _logger.LogError(exception, "Journalisation du test SMTP impossible.");
        }

        return delivery.Succeeded
            ? new IntegrationTestResponse(
                "SMTP_TEST_SENT",
                _emailService.SendsEnabled
                    ? "Message de test remis au serveur SMTP."
                    : "Mode simule : le message a ete journalise sans envoi reel.",
                correlationId)
            : new IntegrationTestResponse(
                "SMTP_TEST_FAILED",
                "L'envoi a echoue. Consultez le journal e-mails pour le detail.",
                correlationId);
    }

    private IntegrationView BuildSmtp(EmailLogDigest log)
    {
        var allowlistOpen = _email.Mode is EmailIntegrationMode.Live
            && !_email.LiveAllowlistOnly;
        var facts = new List<IntegrationFact>
        {
            new("Mode", _email.ModeName, "state"),
            new("Hote", _email.SmtpHost ?? "Non configure"),
            new("Port", _email.SmtpPort.ToString()),
            new("STARTTLS", _email.SmtpUseStartTls ? "Active" : "Desactive", "state"),
            new("Compte", _email.SmtpUsername ?? "Non configure"),
            new("Mot de passe", Present(_email.SmtpPassword), "secret"),
            new("Expediteur", _email.FromAddress ?? "Non configure"),
            new("Nom affiche", _email.FromDisplayName),
            new("Delai maximal", $"{_email.RequestTimeoutMs} ms"),
            new(
                "Allowlist",
                _email.LiveAllowlistOnly
                    ? $"Active — {_email.LiveAllowlist.Count} entree(s)"
                    : "Desactivee",
                "state"),
        };

        var testAvailable = _email.Mode is not EmailIntegrationMode.Disabled;
        return new IntegrationView(
            "smtp",
            "SMTP",
            _email.ModeName,
            _email.ConfigurationValid && _email.SmtpHost is not null,
            _email.ConfigurationValid
                ? allowlistOpen ? "warning" : "healthy"
                : "warning",
            _email.ConfigurationValid
                ? allowlistOpen
                    ? "L'allowlist est desactivee en mode live : tout destinataire peut recevoir un message."
                    : null
                : "La configuration SMTP live est incomplete.",
            _email.Mode is EmailIntegrationMode.Live
                ? "Mode live : les messages partent reellement."
                : null,
            facts,
            [
                new IntegrationOperation(
                    "smtp_test",
                    "Envoyer un message de test",
                    "L'envoi est borne par l'allowlist : un destinataire hors allowlist est refuse.",
                    testAvailable,
                    testAvailable
                        ? null
                        : "L'integration e-mail est desactivee."),
            ],
            log.LastSuccessAt,
            log.LastErrorAt,
            log.LastErrorSummary);
    }

    private IntegrationView BuildStripe()
    {
        var keyMatchesMode = _stripe.SecretKeyMatchesMode(_stripe.SecretKey);
        var hasKey = !string.IsNullOrWhiteSpace(_stripe.SecretKey);
        var facts = new List<IntegrationFact>
        {
            new("Mode", _stripe.ModeName, "state"),
            new("Cle secrete", Present(_stripe.SecretKey), "secret"),
            new(
                "Coherence cle / mode",
                !hasKey
                    ? "Aucune cle"
                    : keyMatchesMode
                        ? "Coherente"
                        : "Incoherente",
                "state"),
            new(
                "Checkout autoritaire",
                OnOff(_billing.AuthoritativeCheckoutEnabled),
                "state"),
            new(
                "Executeur prestataire",
                OnOff(_billing.ProviderExecutorEnabled),
                "state"),
            new(
                "Premier abonnement reel",
                OnOff(_billing.FirstRealSubscriptionApproved),
                "state"),
        };

        var warning = hasKey && !keyMatchesMode
            ? "La cle secrete ne correspond pas au mode declare : tout appel serait rejete."
            : _stripe.Enabled && !hasKey
                ? "Aucune cle secrete : les appels Stripe sont impossibles."
                : null;

        return new IntegrationView(
            "stripe",
            "Stripe",
            _stripe.ModeName,
            _stripe.IsConfigured,
            _stripe.IsLive ? "warning" : warning is null ? "healthy" : "warning",
            warning,
            _stripe.IsLive
                ? "Mode live : les operations engagent de vrais paiements."
                : null,
            facts,
            [
                new IntegrationOperation(
                    "stripe_ping",
                    "Verifier la connectivite",
                    "Non propose : toute verification authentifiee est un appel sortant reel chez le prestataire de paiement.",
                    false,
                    "Aucune verification non destructive n'est cablee a ce jour."),
            ],
            null,
            null,
            null);
    }

    private IntegrationView BuildPayPal()
    {
        var facts = new List<IntegrationFact>
        {
            new("Mode", _paypal.ModeName, "state"),
            new("Client ID", Present(_paypal.ClientId), "secret"),
            new("Client Secret", Present(_paypal.ClientSecret), "secret"),
            // L'URL d'API est publique et derivee du mode : elle ne revele rien
            // et rend le mode verifiable d'un coup d'oeil.
            new("API", _paypal.ApiBaseUrl),
        };

        return new IntegrationView(
            "paypal",
            "PayPal",
            _paypal.ModeName,
            _paypal.IsConfigured,
            _paypal.IsLive ? "warning" : _paypal.IsConfigured ? "healthy" : "info",
            _paypal.IsConfigured
                ? null
                : "Identifiants incomplets : les operations PayPal sont impossibles.",
            _paypal.IsLive
                ? "Mode live : les operations engagent de vrais paiements."
                : null,
            facts,
            [
                new IntegrationOperation(
                    "paypal_ping",
                    "Verifier la connectivite",
                    "Non propose : l'obtention d'un jeton est un appel sortant reel chez le prestataire de paiement.",
                    false,
                    "Aucune verification non destructive n'est cablee a ce jour."),
            ],
            null,
            null,
            null);
    }

    private IntegrationView BuildBpce()
    {
        var facts = new List<IntegrationFact>
        {
            new("Mode", _bpce.ModeName, "state"),
            new("URL de base", _bpce.BaseUrl),
            new("Identifiant emetteur", _bpce.SenderId ?? "Non configure"),
            new("Jeton de rafraichissement", Present(_bpce.RefreshToken), "secret"),
            new("Delai maximal", $"{_bpce.RequestTimeoutMs} ms"),
            new(
                "Configuration",
                _bpce.ConfigurationValid ? "Valide" : "Incomplete",
                "state"),
        };

        return new IntegrationView(
            "bpce",
            "BPCE — facturation",
            _bpce.ModeName,
            _bpce.ConfigurationValid && _bpce.Mode is not BpceIntegrationMode.Disabled,
            _bpce.ConfigurationValid
                ? _bpce.RequestsEnabled ? "warning" : "healthy"
                : "warning",
            _bpce.ConfigurationValid
                ? null
                : "Mode live sans jeton de rafraichissement : aucune facture ne peut etre emise.",
            _bpce.RequestsEnabled
                ? "Mode live : les emissions de facture sont reelles."
                : null,
            facts,
            [
                new IntegrationOperation(
                    "bpce_sender_check",
                    "Controler l'emetteur",
                    "Non propose : le controle consomme le quota d'appels de l'API bancaire.",
                    false,
                    "Aucun controle non consommateur n'est cable a ce jour."),
            ],
            null,
            null,
            null);
    }

    private IntegrationView BuildKoxo()
    {
        // L'URL du webhook porte un chemin utile au diagnostic mais le jeton
        // circule en en-tete : c'est lui, et lui seul, qu'il faut taire.
        var endpoint = _koxo.Url is null
            ? "Non configure"
            : $"{_koxo.Url.Scheme}://{_koxo.Url.Host}:{_koxo.Url.Port}{_koxo.Url.AbsolutePath}";
        var insecure = _koxo.AllowInsecureHttp;
        var facts = new List<IntegrationFact>
        {
            new("Point d'entree", endpoint),
            new("Jeton", Present(_koxo.BearerToken), "secret"),
            new("Delai maximal", $"{(int)_koxo.Timeout.TotalMilliseconds} ms"),
            new("HTTP non chiffre", insecure ? "Autorise" : "Refuse", "state"),
        };

        return new IntegrationView(
            "koxo",
            "KoXo — synchronisation",
            _koxo.Enabled ? "configured" : "disabled",
            _koxo.Enabled,
            _koxo.Enabled ? insecure ? "warning" : "healthy" : "info",
            insecure
                ? "HTTP non chiffre autorise : le jeton circulerait en clair."
                : null,
            // Rappel de la portee reelle : cette route est globale, une absence
            // dans le CSV desactive le compte correspondant.
            "La synchronisation KoXo est globale : le CSV fait autorite et une ligne retiree desactive le compte correspondant.",
            facts,
            [
                new IntegrationOperation(
                    "koxo_sync",
                    "Declencher une synchronisation",
                    "Non propose ici : la synchronisation est globale et desactive les comptes absents du CSV.",
                    false,
                    "Operation trop large pour une page d'observation ; elle reste sur /admin/koxo."),
            ],
            null,
            null,
            null);
    }

    private async Task<IntegrationView> ReadVeeamAsync(
        CancellationToken cancellationToken)
    {
        var facts = new List<IntegrationFact>();
        string state;
        string? warning = null;
        string? lastSuccess = null;
        string? lastError = null;
        string? lastErrorSummary = null;
        var configured = false;

        try
        {
            var integrations = await _backups.GetAdminIntegrationsAsync(cancellationToken);
            var enabled = integrations.Where(item => item.Enabled).ToArray();
            configured = integrations.Count > 0;

            var collected = enabled
                .Where(item => !string.IsNullOrWhiteSpace(item.LastCollectedAt))
                .OrderByDescending(item => item.LastCollectedAt, StringComparer.Ordinal)
                .ToArray();
            var failing = enabled
                .Where(item => !string.Equals(
                    item.LastCollectionStatus,
                    "success",
                    StringComparison.OrdinalIgnoreCase)
                    && item.LastCollectionStatus is not null)
                .ToArray();

            lastSuccess = collected
                .FirstOrDefault(item => string.Equals(
                    item.LastCollectionStatus,
                    "success",
                    StringComparison.OrdinalIgnoreCase))
                ?.LastCollectedAt;
            var firstFailure = failing.FirstOrDefault();
            lastError = firstFailure?.LastCollectedAt;
            lastErrorSummary = firstFailure?.LastCollectionMessage;

            facts.Add(new IntegrationFact(
                "Jobs suivis",
                $"{enabled.Length} actif(s) sur {integrations.Count}"));
            facts.Add(new IntegrationFact(
                "Jobs jamais collectes",
                enabled.Count(item => string.IsNullOrWhiteSpace(item.LastCollectedAt))
                    .ToString()));
            facts.Add(new IntegrationFact(
                "Jobs en erreur",
                failing.Length.ToString(),
                "state"));

            state = integrations.Count == 0
                ? "info"
                : failing.Length > 0
                    ? "warning"
                    : "healthy";
            if (failing.Length > 0)
            {
                warning = "Au moins un job de sauvegarde n'a pas ete collecte correctement.";
            }
            else if (integrations.Count == 0)
            {
                warning = "Aucun job de sauvegarde n'est declare : le collecteur n'a rien a remonter.";
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Lecture impossible de l'etat du collecteur Veeam.");
            state = "warning";
            warning = "L'etat du collecteur ne peut pas etre lu pour le moment.";
        }

        // Le collecteur est un composant externe : ses identifiants ne sont pas
        // lus par API-INTERNAL et ne peuvent donc pas fuir par cette page.
        facts.Add(new IntegrationFact(
            "Collecteur",
            "Externe — pousse ses releves vers l'API",
            "state"));

        return new IntegrationView(
            "veeam",
            "Veeam — sauvegardes",
            "push",
            configured,
            state,
            warning,
            null,
            facts,
            [
                new IntegrationOperation(
                    "veeam_collect",
                    "Declencher une collecte",
                    "Non propose : le collecteur est externe et pousse ses releves, l'API ne l'appelle pas.",
                    false,
                    "Le sens de l'echange interdit un declenchement depuis l'API."),
            ],
            lastSuccess,
            lastError,
            lastErrorSummary);
    }

    private async Task<EmailLogDigest> ReadEmailLogAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _emailLog.ListRecentAsync(EmailLogSample, cancellationToken);
            var success = entries.FirstOrDefault(entry => string.Equals(
                entry.Status,
                "sent",
                StringComparison.OrdinalIgnoreCase));
            var failure = entries.FirstOrDefault(entry => !string.Equals(
                entry.Status,
                "sent",
                StringComparison.OrdinalIgnoreCase));
            return new EmailLogDigest(
                success?.SentAt ?? success?.CreatedAt,
                failure?.CreatedAt,
                // Le journal peut porter une adresse : on ne remonte que le
                // statut, qui suffit au diagnostic.
                failure is null ? null : $"Dernier envoi non delivre : {failure.Status}.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Lecture impossible du journal e-mails.");
            return new EmailLogDigest(null, null, null);
        }
    }

    private static string Present(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Non configure" : "Configure";

    private static string OnOff(bool value) => value ? "Active" : "Desactive";

    private static string Iso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");

    private sealed record EmailLogDigest(
        string? LastSuccessAt,
        string? LastErrorAt,
        string? LastErrorSummary);
}
