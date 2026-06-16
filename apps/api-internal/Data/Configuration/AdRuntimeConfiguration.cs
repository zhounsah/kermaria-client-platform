namespace Kermaria.ApiInternal.Data.Configuration;

public enum AdIntegrationMode
{
    Disabled,
    Mock,
    ReadOnly,
    ControlledWrite
}

public sealed record AdRuntimeConfiguration(
    AdIntegrationMode Mode,
    string? Domain,
    string? ClientsOuDn,
    string? ServiceAccountUsername,
    string? ServiceAccountPassword,
    int ConnectTimeoutMs,
    int QueryTimeoutMs,
    int MaxResults,
    bool ConfigurationValid)
{
    public string ModeName => Mode switch
    {
        AdIntegrationMode.ReadOnly => "read_only",
        AdIntegrationMode.ControlledWrite => "controlled_write",
        _ => Mode.ToString().ToLowerInvariant()
    };

    public bool ReadsEnabled => Mode is
        AdIntegrationMode.Mock
        or AdIntegrationMode.ReadOnly
        or AdIntegrationMode.ControlledWrite;

    public bool WritesEnabled => Mode is
        AdIntegrationMode.Mock
        or AdIntegrationMode.ControlledWrite;
}

public static class AdConfigurationResolver
{
    private const string RequiredTestOuRoot =
        "OU=TEST_SITE_WEB,DC=home,DC=bzh";

    public static AdRuntimeConfiguration Resolve(IConfiguration configuration)
    {
        var mode = ParseMode(configuration["AD_INTEGRATION_MODE"]);
        var requiresDirectoryConfiguration = mode is
            AdIntegrationMode.ReadOnly
            or AdIntegrationMode.ControlledWrite;
        var domain = NullIfWhiteSpace(configuration["AD_DOMAIN"]);
        var clientsOuDn = NormalizeDn(configuration["AD_CLIENTS_OU_DN"]);
        var serviceAccountUsername = requiresDirectoryConfiguration
            ? NullIfWhiteSpace(configuration["AD_SERVICE_ACCOUNT_USERNAME"])
            : null;
        var serviceAccountPassword = requiresDirectoryConfiguration
            ? NullIfWhiteSpace(configuration["AD_SERVICE_ACCOUNT_PASSWORD"])
            : null;
        var connectTimeoutMs = ParseMilliseconds(
            configuration["AD_CONNECT_TIMEOUT_MS"],
            3000,
            minimum: 500,
            maximum: 10000);
        var queryTimeoutMs = ParseMilliseconds(
            configuration["AD_QUERY_TIMEOUT_MS"],
            5000,
            minimum: 500,
            maximum: 30000);
        var maxResults = ParseMilliseconds(
            configuration["AD_MAX_RESULTS"],
            25,
            minimum: 1,
            maximum: 100);

        var configurationValid = !requiresDirectoryConfiguration
            || (
                domain is not null
                && clientsOuDn is not null
                && string.Equals(
                    clientsOuDn,
                    RequiredTestOuRoot,
                    StringComparison.OrdinalIgnoreCase)
                && serviceAccountUsername is not null
                && serviceAccountPassword is not null
            );

        return new AdRuntimeConfiguration(
            mode,
            domain,
            clientsOuDn,
            serviceAccountUsername,
            serviceAccountPassword,
            connectTimeoutMs,
            queryTimeoutMs,
            maxResults,
            configurationValid);
    }

    private static AdIntegrationMode ParseMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "disabled" => AdIntegrationMode.Disabled,
            "mock" => AdIntegrationMode.Mock,
            "read_only" => AdIntegrationMode.ReadOnly,
            "controlled_write" => AdIntegrationMode.ControlledWrite,
            _ => AdIntegrationMode.Disabled
        };
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int ParseMilliseconds(
        string? value,
        int fallback,
        int minimum,
        int maximum)
    {
        if (!int.TryParse(value, out var parsed))
        {
            return fallback;
        }

        return Math.Clamp(parsed, minimum, maximum);
    }

    private static string? NormalizeDn(string? distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return null;
        }

        var parts = distinguishedName
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();

        return parts.Length == 0
            ? null
            : string.Join(",", parts);
    }
}
