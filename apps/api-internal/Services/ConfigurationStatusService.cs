using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services;

public interface IConfigurationStatusService
{
    ConfigurationStatusSnapshot GetSnapshot();
}

/// <summary>
/// Projection d'observabilite : les secrets sont reduits a leur presence et les
/// reglages runtime restent en lecture seule. Cette classe est volontairement
/// distincte du registre de settings afin de ne pas transformer l'UI en editeur
/// de variables d'environnement.
/// </summary>
public sealed class ConfigurationStatusService : IConfigurationStatusService
{
    private readonly IHostEnvironment _environment;
    private readonly SqlRuntimeConfiguration _sql;
    private readonly AdRuntimeConfiguration _ad;
    private readonly EmailRuntimeConfiguration _email;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly BpceRuntimeConfiguration _bpce;
    private readonly KoxoSyncWebhookRuntimeConfiguration _koxo;
    private readonly DownloadStorageRuntimeConfiguration _downloads;
    private readonly BillingV2RuntimeConfiguration _billing;

    public ConfigurationStatusService(IHostEnvironment environment, SqlRuntimeConfiguration sql, AdRuntimeConfiguration ad, EmailRuntimeConfiguration email, StripeRuntimeConfiguration stripe, PayPalRuntimeConfiguration paypal, BpceRuntimeConfiguration bpce, KoxoSyncWebhookRuntimeConfiguration koxo, DownloadStorageRuntimeConfiguration downloads, BillingV2RuntimeConfiguration billing)
        => (_environment, _sql, _ad, _email, _stripe, _paypal, _bpce, _koxo, _downloads, _billing) = (environment, sql, ad, email, stripe, paypal, bpce, koxo, downloads, billing);

    public ConfigurationStatusSnapshot GetSnapshot() => new(
    [
        Domain("directory", "Active Directory & KoXo", _ad.ConfigurationValid ? "healthy" : "warning",
        [ Fact("Autorité identités", _ad.IdentityAuthority), Fact("Autorité mots de passe", _ad.PasswordAuthority), Fact("Accès API", _ad.DirectoryAccess), Fact("Groupes de services", _ad.GroupMembershipWritePolicy), Fact("Cycle de vie API", _ad.UserLifecycleWritePolicy), Fact("Compte LDAP", _ad.ServiceAccountUsername ?? "Non configuré"), Fact("Secret LDAP", State(_ad.ServiceAccountPassword), true), Fact("Webhook KoXo", _koxo.Enabled ? "Configuré" : "Non configuré"), Fact("Racines autorisées", _ad.AllowedRoots.Count.ToString()) ], _ad.ConfigurationValid ? null : "La configuration d'annuaire est invalide : les écritures restent refusées."),
        Domain("email", "E-mails", _email.ConfigurationValid ? "healthy" : "warning",
        [ Fact("Mode", _email.ModeName), Fact("SMTP", State(_email.SmtpPassword), true), Fact("Expéditeur", _email.FromAddress ?? "Non configuré"), Fact("Allowlist live", _email.LiveAllowlistOnly ? "Activée" : "Désactivée") ], _email.ConfigurationValid ? null : "La configuration SMTP live est incomplète."),
        Domain("billing", "Billing V2", "info",
        [ Fact("Nouvelles souscriptions", OnOff(_billing.NewSubscriptionsEnabled)), Fact("Checkout autoritaire", OnOff(_billing.AuthoritativeCheckoutEnabled)), Fact("Provider executor", OnOff(_billing.ProviderExecutorEnabled)), Fact("Provisioning", OnOff(_billing.ProvisioningEnabled)), Fact("Premier abonnement réel", OnOff(_billing.FirstRealSubscriptionApproved)) ]),
        Domain("integrations", "Intégrations", "info",
        [ Fact("Stripe", $"{_stripe.ModeName} · {State(_stripe.SecretKey)}"), Fact("PayPal", $"{_paypal.ModeName} · {State(_paypal.ClientSecret)}"), Fact("BPCE", $"{_bpce.ModeName} · {State(_bpce.RefreshToken)}"), Fact("KoXo", _koxo.Enabled ? "Configuré" : "Non configuré") ]),
        Domain("infrastructure", "Infrastructure", _sql.ConfigurationValid ? "healthy" : "warning",
        [ Fact("Environnement", _environment.EnvironmentName), Fact("Persistance", _sql.IsPersistent ? "MariaDB" : "Mock"), Fact("SQL", _sql.StatusReason), Fact("Stockage téléchargements", _downloads.RootPath), Fact("Configuration stockage", _downloads.IsExplicitlyConfigured ? "Explicite" : "Défaut") ], _sql.ConfigurationValid ? null : "La persistance SQL n'est pas prête.")
    ]);

    private static ConfigurationStatusDomain Domain(string key, string label, string state, IReadOnlyList<ConfigurationStatusFact> facts, string? warning = null) => new(key, label, state, facts, warning);
    private static ConfigurationStatusFact Fact(string label, string value, bool sensitive = false) => new(label, value, sensitive);
    private static string State(string? value) => string.IsNullOrWhiteSpace(value) ? "Non configuré" : "Configuré";
    private static string OnOff(bool value) => value ? "Activé" : "Désactivé";
}
