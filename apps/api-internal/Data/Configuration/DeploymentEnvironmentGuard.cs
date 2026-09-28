using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Configuration;

/// <summary>
/// Environnement de deploiement fonctionnel (APP_ENV), orthogonal a
/// l'environnement ASP.NET Core. L'instance DEV tourne en
/// ASPNETCORE_ENVIRONMENT=Staging pour garder l'authentification de service,
/// MariaDB obligatoire et les cookies securises, mais APP_ENV=Development lui
/// interdit toute ressource transactionnelle de production.
/// </summary>
public enum DeploymentEnvironment
{
    Unspecified,
    Development,
    Production
}

public sealed record DeploymentEnvironmentReport(
    DeploymentEnvironment Environment,
    string HostEnvironmentName,
    string DatabaseName,
    string StripeMode,
    string StripeKeyFamily,
    string OutboxExecutor,
    bool ProvisioningEnabled,
    IReadOnlyList<string> Violations)
{
    public string EnvironmentName => Environment.ToString();
}

public static class DeploymentEnvironmentGuard
{
    public const string AppEnvironmentVariable = "APP_ENV";
    public const string ResponseHeaderName = "X-Kermaria-App-Env";

    /// <summary>
    /// Code de sortie EX_CONFIG : distingue un refus de configuration d'un
    /// crash ordinaire dans le gestionnaire de services.
    /// </summary>
    public const int ConfigurationExitCode = 78;

    private static readonly string[] ProvisioningFlags =
    [
        "BILLING_V2_PROVISIONING_ENABLED",
        "BILLING_V2_ADDITIONAL_USER_PROVISIONING_ENABLED",
        "BILLING_V2_SERVICE_FULFILLMENT_ENABLED",
        "BILLING_V2_VPS_LOCAL_PROVISIONING_ENABLED",
        "BILLING_V2_VPS_CLOUD_AUTOMATION_ENABLED"
    ];

    // Points de sortie qui agissent sur l'annuaire ou le stockage reels.
    private static readonly string[] ProvisioningEndpoints =
    [
        "KOXO_SYNC_WEBHOOK_URL",
        "BILLING_V2_KOXO_STORAGE_URL"
    ];

    private static readonly Regex DevelopmentDatabaseName = new(
        "^[a-z0-9_]+_dev$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DevelopmentAccountName = new(
        "^[a-z0-9_]+_dev(_[a-z0-9_]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DevelopmentClientsOu = new(
        "(?:^|,)OU=CLIENTS DEV(?:,|$)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private const int DevelopmentKoxoWebhookPort = 8043;

    private static readonly Regex GrantLine = new(
        @"^GRANT\s+(?<privileges>.+?)\s+ON\s+(?<target>.+?)\s+TO\s",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool IsDevelopmentDatabaseName(string? databaseName)
        => !string.IsNullOrWhiteSpace(databaseName)
            && DevelopmentDatabaseName.IsMatch(databaseName.Trim());

    public static bool IsDevelopmentAccountName(string? accountName)
        => !string.IsNullOrWhiteSpace(accountName)
            && DevelopmentAccountName.IsMatch(accountName.Trim());

    /// <summary>
    /// Isole les groupes AD de service administres par le catalogue. Le nom
    /// SAM est la seule reference portee par <c>billing_v2_provisioning_rules</c>
    /// : le DN arrive plus tard de la configuration annuaire. Garder cette
    /// validation ici la fait suivre exactement le meme APP_ENV que les OU,
    /// namespace KoXo et endpoints de provisioning.
    /// </summary>
    public static bool TryValidateAdGroupTarget(
        DeploymentEnvironment environment,
        string? targetReference,
        out string reasonCode)
    {
        var normalized = targetReference?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            reasonCode = "AD_GROUP_TARGET_MISSING";
            return false;
        }

        var isDevelopmentTarget = normalized.EndsWith(
            "_DEV", StringComparison.OrdinalIgnoreCase);
        switch (environment)
        {
            case DeploymentEnvironment.Development when !isDevelopmentTarget:
                reasonCode = "AD_GROUP_TARGET_OUTSIDE_DEVELOPMENT";
                return false;
            case DeploymentEnvironment.Production when isDevelopmentTarget:
                reasonCode = "AD_GROUP_TARGET_DEVELOPMENT_FORBIDDEN";
                return false;
            default:
                reasonCode = string.Empty;
                return true;
        }
    }

    /// <summary>
    /// Famille de cle Stripe (prefixe seul). Ne renvoie jamais la cle.
    /// </summary>
    public static string DescribeStripeKeyFamily(string? key)
    {
        var normalized = key?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return "none";
        }

        foreach (var family in new[] { "sk_live", "sk_test", "rk_live", "rk_test" })
        {
            if (normalized.StartsWith(family + "_", StringComparison.Ordinal))
            {
                return family;
            }
        }

        return "unknown";
    }

    public static DeploymentEnvironmentReport Evaluate(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var violations = new List<string>();
        var environment = ResolveEnvironment(
            configuration,
            hostEnvironment,
            violations);

        var databaseName = configuration["SQL_DATABASE"]?.Trim() ?? string.Empty;
        var sqlUsername = configuration["SQL_USERNAME"]?.Trim();
        var stripeMode = configuration["STRIPE_MODE"]?.Trim().ToLowerInvariant();
        var stripeSecretKey = configuration["STRIPE_SECRET_KEY"];
        var stripeKeyFamily = DescribeStripeKeyFamily(stripeSecretKey);
        var stripePublishableKey =
            configuration["STRIPE_PUBLISHABLE_KEY"]?.Trim() ?? string.Empty;

        var provisioningRequests = ProvisioningFlags
            .Where(flag => IsTrue(configuration[flag]))
            .Concat(ProvisioningEndpoints
                .Where(key => !string.IsNullOrWhiteSpace(configuration[key])))
            .ToList();
        if (string.Equals(
                configuration["AD_INTEGRATION_MODE"]?.Trim(),
                "controlled_write",
                StringComparison.OrdinalIgnoreCase))
        {
            provisioningRequests.Add("AD_INTEGRATION_MODE=controlled_write");
        }

        var provisioningEnabled = provisioningRequests.Count > 0;

        switch (environment)
        {
            case DeploymentEnvironment.Development:
                ValidateDevelopment(
                    configuration,
                    hostEnvironment,
                    databaseName,
                    sqlUsername,
                    stripeMode,
                    stripeKeyFamily,
                    stripePublishableKey,
                    provisioningRequests,
                    violations);
                provisioningEnabled = provisioningEnabled
                    && IsDevelopmentProvisioningAllowed(configuration);
                break;
            case DeploymentEnvironment.Production:
                ValidateProduction(
                    hostEnvironment,
                    databaseName,
                    sqlUsername,
                    stripeMode,
                    stripeKeyFamily,
                    stripePublishableKey,
                    violations);
                break;
        }

        ValidateKoxoNamespace(configuration, environment, violations);
        ValidateDirectoryTargetIsolation(configuration, environment, violations);

        return new DeploymentEnvironmentReport(
            environment,
            hostEnvironment.EnvironmentName,
            string.IsNullOrEmpty(databaseName) ? "(none)" : databaseName,
            string.IsNullOrEmpty(stripeMode) ? "disabled" : stripeMode,
            stripeKeyFamily,
            DescribeOutboxExecutor(configuration),
            provisioningEnabled,
            violations);
    }

    /// <summary>
    /// Verifie en base que le compte de l'instance DEV ne voit que sa base.
    /// Les droits reels priment sur la configuration : un compte de
    /// production nomme a tort `*_dev` serait refuse ici.
    /// </summary>
    public static async Task<IReadOnlyList<string>> VerifyDatabaseIsolationAsync(
        SqlRuntimeConfiguration sqlConfiguration,
        CancellationToken cancellationToken)
    {
        if (!sqlConfiguration.IsPersistent
            || string.IsNullOrWhiteSpace(sqlConfiguration.ConnectionString))
        {
            return ["Persistance MariaDB absente : l'isolation de la base DEV ne peut pas etre verifiee."];
        }

        var expectedDatabase = new MySqlConnectionStringBuilder(
            sqlConfiguration.ConnectionString).Database;

        try
        {
            await using var connection =
                new MySqlConnection(sqlConfiguration.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var databaseCommand = connection.CreateCommand();
            databaseCommand.CommandText = "SELECT DATABASE()";
            var currentDatabase = Convert.ToString(
                await databaseCommand.ExecuteScalarAsync(cancellationToken));

            var grants = new List<string>();
            await using (var grantsCommand = connection.CreateCommand())
            {
                grantsCommand.CommandText = "SHOW GRANTS FOR CURRENT_USER()";
                await using var reader =
                    await grantsCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    grants.Add(reader.GetString(0));
                }
            }

            var violations = new List<string>();
            if (!string.Equals(
                    currentDatabase,
                    expectedDatabase,
                    StringComparison.Ordinal))
            {
                violations.Add(
                    $"DATABASE() retourne {currentDatabase} au lieu de {expectedDatabase}.");
            }

            violations.AddRange(FindGrantViolations(expectedDatabase, grants));
            return violations;
        }
        catch (Exception exception) when (exception is MySqlException
            or InvalidOperationException
            or TimeoutException)
        {
            return [$"Isolation de la base DEV non verifiable ({exception.GetType().Name})."];
        }
    }

    /// <summary>
    /// Un compte DEV n'a droit qu'a USAGE sur *.* et a des privileges sur sa
    /// seule base. Tout autre perimetre (base PROD, joker, role) est refuse.
    /// </summary>
    public static IReadOnlyList<string> FindGrantViolations(
        string expectedDatabase,
        IEnumerable<string> grants)
    {
        var violations = new List<string>();
        foreach (var grant in grants)
        {
            var match = GrantLine.Match(grant.Trim());
            if (!match.Success)
            {
                violations.Add("Le compte SQL DEV porte un role ou un droit non analysable.");
                continue;
            }

            var privileges = match.Groups["privileges"].Value.Trim();
            var target = match.Groups["target"].Value.Trim();
            if (target == "*.*")
            {
                if (!string.Equals(
                        privileges,
                        "USAGE",
                        StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(
                        "Le compte SQL DEV detient des privileges globaux (*.*).");
                }

                continue;
            }

            var grantedDatabase = ExtractGrantDatabase(target);
            if (!string.Equals(
                    grantedDatabase,
                    expectedDatabase,
                    StringComparison.Ordinal))
            {
                violations.Add(
                    $"Le compte SQL DEV a des droits hors de {expectedDatabase} (base {grantedDatabase ?? "?"}).");
            }
        }

        return violations;
    }

    /// <summary>
    /// Un evenement Stripe n'est traite que par l'environnement du meme mode :
    /// livemode=true exige STRIPE_MODE=live, livemode=false tout autre mode.
    /// </summary>
    public static bool TryValidateStripeLivemode(
        string? rawPayload,
        StripeRuntimeConfiguration stripeConfiguration,
        out string reasonCode)
    {
        bool? livemode = null;
        if (!string.IsNullOrWhiteSpace(rawPayload))
        {
            try
            {
                using var document = JsonDocument.Parse(rawPayload);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty(
                        "livemode",
                        out var livemodeElement)
                    && livemodeElement.ValueKind is JsonValueKind.True
                        or JsonValueKind.False)
                {
                    livemode = livemodeElement.GetBoolean();
                }
            }
            catch (JsonException)
            {
                livemode = null;
            }
        }

        if (livemode is null)
        {
            reasonCode = "STRIPE_LIVEMODE_MISSING";
            return false;
        }

        if (livemode.Value != stripeConfiguration.IsLive)
        {
            reasonCode = "STRIPE_LIVEMODE_MISMATCH";
            return false;
        }

        reasonCode = string.Empty;
        return true;
    }

    private static DeploymentEnvironment ResolveEnvironment(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        List<string> violations)
    {
        var raw = configuration[AppEnvironmentVariable]?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            // Retrocompatibilite : la production en place ne pose pas APP_ENV.
            return hostEnvironment.IsProduction()
                ? DeploymentEnvironment.Production
                : DeploymentEnvironment.Unspecified;
        }

        switch (raw.ToLowerInvariant())
        {
            case "development":
            case "dev":
                return DeploymentEnvironment.Development;
            case "production":
            case "prod":
                return DeploymentEnvironment.Production;
            default:
                violations.Add(
                    "APP_ENV doit valoir Development ou Production.");
                return DeploymentEnvironment.Unspecified;
        }
    }

    private static void ValidateDevelopment(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        string databaseName,
        string? sqlUsername,
        string? stripeMode,
        string stripeKeyFamily,
        string stripePublishableKey,
        IReadOnlyList<string> provisioningRequests,
        List<string> violations)
    {
        if (hostEnvironment.IsProduction())
        {
            violations.Add(
                "APP_ENV=Development est incompatible avec ASPNETCORE_ENVIRONMENT=Production.");
        }

        if (stripeKeyFamily is "sk_live" or "rk_live")
        {
            violations.Add(
                $"APP_ENV=Development refuse une cle Stripe live (STRIPE_SECRET_KEY de famille {stripeKeyFamily}).");
        }

        if (stripePublishableKey.StartsWith("pk_live_", StringComparison.Ordinal))
        {
            violations.Add(
                "APP_ENV=Development refuse une cle publiable Stripe live (STRIPE_PUBLISHABLE_KEY).");
        }

        if (stripeMode == "live")
        {
            violations.Add("APP_ENV=Development refuse STRIPE_MODE=live.");
        }

        if (!string.IsNullOrEmpty(databaseName)
            && !IsDevelopmentDatabaseName(databaseName))
        {
            violations.Add(
                $"APP_ENV=Development exige une base DEV (*_dev) ; SQL_DATABASE={databaseName}.");
        }

        if (!string.IsNullOrEmpty(databaseName)
            && !IsDevelopmentAccountName(sqlUsername))
        {
            violations.Add(
                "APP_ENV=Development exige un compte SQL DEV (SQL_USERNAME en *_dev ou *_dev_*).");
        }

        if (string.Equals(
                configuration["PAYPAL_MODE"]?.Trim(),
                "live",
                StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(
                "APP_ENV=Development refuse PAYPAL_MODE=live.");
        }

        if (string.Equals(
                configuration["BPCE_INTEGRATION_MODE"]?.Trim(),
                "live",
                StringComparison.OrdinalIgnoreCase)
            && !IsTrue(configuration["ALLOW_DEV_BPCE_LIVE"]))
        {
            violations.Add(
                "APP_ENV=Development refuse BPCE_INTEGRATION_MODE=live sans ALLOW_DEV_BPCE_LIVE=true.");
        }

        if (string.Equals(
                configuration["EMAIL_INTEGRATION_MODE"]?.Trim(),
                "live",
                StringComparison.OrdinalIgnoreCase))
        {
            var allowlistOnly = !string.Equals(
                configuration["EMAIL_LIVE_ALLOWLIST_ONLY"]?.Trim(),
                "false",
                StringComparison.OrdinalIgnoreCase);
            var allowlist = (configuration["EMAIL_LIVE_ALLOWLIST"] ?? string.Empty)
                .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries);
            if (!allowlistOnly
                || allowlist.Length == 0
                || allowlist.Any(entry => entry.Contains('*')))
            {
                violations.Add(
                    "APP_ENV=Development n'autorise EMAIL_INTEGRATION_MODE=live qu'avec une allowlist explicite (EMAIL_LIVE_ALLOWLIST_ONLY=true, sans joker).");
            }
        }

        if (provisioningRequests.Count > 0
            && !IsDevelopmentProvisioningAllowed(configuration))
        {
            violations.Add(
                "APP_ENV=Development : provisioning reel demande ("
                + string.Join(", ", provisioningRequests)
                + ") sans PROVISIONING_ENABLED=true ET ALLOW_DEV_PROVISIONING=true.");
        }
    }

    private static void ValidateProduction(
        IHostEnvironment hostEnvironment,
        string databaseName,
        string? sqlUsername,
        string? stripeMode,
        string stripeKeyFamily,
        string stripePublishableKey,
        List<string> violations)
    {
        if (hostEnvironment.IsDevelopment())
        {
            violations.Add(
                "APP_ENV=Production est incompatible avec ASPNETCORE_ENVIRONMENT=Development.");
        }

        if (stripeKeyFamily is "sk_test" or "rk_test")
        {
            violations.Add(
                $"APP_ENV=Production refuse une cle Stripe de test (STRIPE_SECRET_KEY de famille {stripeKeyFamily}).");
        }

        if (stripePublishableKey.StartsWith("pk_test_", StringComparison.Ordinal))
        {
            violations.Add(
                "APP_ENV=Production refuse une cle publiable Stripe de test (STRIPE_PUBLISHABLE_KEY).");
        }

        if (stripeMode == "test")
        {
            violations.Add("APP_ENV=Production refuse STRIPE_MODE=test.");
        }

        if (IsDevelopmentDatabaseName(databaseName))
        {
            violations.Add(
                $"APP_ENV=Production refuse une base DEV ; SQL_DATABASE={databaseName}.");
        }

        if (IsDevelopmentAccountName(sqlUsername))
        {
            violations.Add(
                "APP_ENV=Production refuse un compte SQL DEV (SQL_USERNAME en *_dev).");
        }
    }

    /// <summary>
    /// DEV et PROD partagent le domaine <c>clients.home.bzh</c> et le meme
    /// KoXoAdm : seul le namespace KoXo separe leurs identites. Une DEV qui agit
    /// sur l'annuaire ou declenche KoXo doit donc en avoir un propre, et la
    /// production ne peut jamais en porter un autre que le sien.
    /// </summary>
    private static void ValidateKoxoNamespace(
        IConfiguration configuration,
        DeploymentEnvironment environment,
        List<string> violations)
    {
        var candidate = Services.KoxoNamespaceResolver.ReadCandidate(configuration);
        var errors = Services.KoxoNamespaceResolver.Validate(candidate);
        if (errors.Count > 0)
        {
            violations.Add("Namespace KoXo invalide : " + string.Join(" ; ", errors) + ".");
            return;
        }

        switch (environment)
        {
            case DeploymentEnvironment.Development:
                var directoryEffects = string.Equals(
                        configuration["AD_INTEGRATION_MODE"]?.Trim(),
                        "controlled_write",
                        StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(configuration["KOXO_SYNC_WEBHOOK_URL"]);
                if (directoryEffects && candidate.IsProduction)
                {
                    violations.Add(
                        "APP_ENV=Development refuse le namespace KoXo de production quand l'annuaire ou KoXo sont actifs (AD_INTEGRATION_MODE=controlled_write ou KOXO_SYNC_WEBHOOK_URL) : definir "
                        + Services.KoxoNamespace.IdentifierPrefixVariable + ", "
                        + Services.KoxoNamespace.CustomerReferencePrefixVariable + ", "
                        + Services.KoxoNamespace.PrimaryGroupClientsVariable + " et "
                        + Services.KoxoNamespace.PrimaryGroupDemoVariable + ".");
                }

                break;
            case DeploymentEnvironment.Production:
                if (!candidate.IsProduction)
                {
                    violations.Add(
                        "APP_ENV=Production refuse un namespace KoXo hors production (prefixe d'identifiant "
                        + candidate.IdentifierPrefix + ").");
                }

                break;
        }
    }

    /// <summary>
    /// Le namespace protege les identifiants, mais ne suffit pas si une
    /// configuration DEV pointe directement vers les cibles AD ou KoXo PROD.
    /// Ces bornes sont aussi appliquees a la PROD dans l'autre sens.
    /// </summary>
    private static void ValidateDirectoryTargetIsolation(
        IConfiguration configuration,
        DeploymentEnvironment environment,
        List<string> violations)
    {
        var adMode = configuration["AD_INTEGRATION_MODE"]?.Trim();
        var clientsOu = configuration["AD_CLIENTS_OU_DN"]?.Trim();
        var webhook = configuration["KOXO_SYNC_WEBHOOK_URL"]?.Trim();
        var directoryWrites = string.Equals(
            adMode,
            "controlled_write",
            StringComparison.OrdinalIgnoreCase);
        var hasWebhook = !string.IsNullOrWhiteSpace(webhook);

        switch (environment)
        {
            case DeploymentEnvironment.Development:
                if (directoryWrites && !IsDevelopmentClientsOu(clientsOu))
                {
                    violations.Add(
                        "APP_ENV=Development exige AD_CLIENTS_OU_DN sous OU=CLIENTS DEV pour AD_INTEGRATION_MODE=controlled_write.");
                }

                if (hasWebhook && !IsDevelopmentWebhook(webhook))
                {
                    violations.Add(
                        "APP_ENV=Development exige KOXO_SYNC_WEBHOOK_URL vers le recepteur DEV :8043.");
                }

                break;
            case DeploymentEnvironment.Production:
                if (IsDevelopmentClientsOu(clientsOu))
                {
                    violations.Add(
                        "APP_ENV=Production refuse AD_CLIENTS_OU_DN sous OU=CLIENTS DEV.");
                }

                if (hasWebhook && IsDevelopmentWebhook(webhook))
                {
                    violations.Add(
                        "APP_ENV=Production refuse KOXO_SYNC_WEBHOOK_URL vers le recepteur DEV :8043.");
                }

                break;
        }
    }

    private static bool IsDevelopmentClientsOu(string? distinguishedName)
        => !string.IsNullOrWhiteSpace(distinguishedName)
            && DevelopmentClientsOu.IsMatch(distinguishedName.Trim());

    private static bool IsDevelopmentWebhook(string? rawUrl)
        => Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)
            && uri.Port == DevelopmentKoxoWebhookPort;

    private static bool IsDevelopmentProvisioningAllowed(IConfiguration configuration)
        => IsTrue(configuration["PROVISIONING_ENABLED"])
            && IsTrue(configuration["ALLOW_DEV_PROVISIONING"]);

    private static string DescribeOutboxExecutor(IConfiguration configuration)
    {
        var outbox = IsTrue(configuration["BILLING_V2_PROVIDER_OUTBOX_ENABLED"]);
        var executor = IsTrue(configuration["BILLING_V2_PROVIDER_EXECUTOR_ENABLED"]);
        return (outbox, executor) switch
        {
            (true, true) => "enabled",
            (true, false) => "outbox enabled, executor disabled",
            (false, true) => "executor enabled, outbox worker disabled",
            _ => "disabled"
        };
    }

    private static string? ExtractGrantDatabase(string target)
    {
        // Formes : `db`.*, `db`.`table`, FUNCTION `db`.`f`, PROCEDURE `db`.`p`.
        var start = target.IndexOf('`');
        if (start < 0)
        {
            return null;
        }

        var end = target.IndexOf('`', start + 1);
        if (end < 0)
        {
            return null;
        }

        return target[(start + 1)..end]
            .Replace("\\_", "_", StringComparison.Ordinal)
            .Replace("\\%", "%", StringComparison.Ordinal);
    }

    private static bool IsTrue(string? value)
        => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
