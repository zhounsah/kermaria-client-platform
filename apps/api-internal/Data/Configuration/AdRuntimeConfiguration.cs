namespace Kermaria.ApiInternal.Data.Configuration;

public enum AdIntegrationMode
{
    Disabled,
    Mock,
    Test,
    Enabled
}

public sealed record AdRuntimeConfiguration(
    AdIntegrationMode Mode,
    string? Domain,
    string? ClientsOuDn,
    string? ServiceAccountUsername,
    string? ServiceAccountPassword,
    IReadOnlySet<string> AllowedGroups,
    bool ConfigurationValid)
{
    public string ModeName => Mode.ToString().ToLowerInvariant();

    public bool IsTargetInAllowedOu(string? distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName)
            || string.IsNullOrWhiteSpace(ClientsOuDn))
        {
            return false;
        }

        return distinguishedName.EndsWith(
            $",{ClientsOuDn}",
            StringComparison.OrdinalIgnoreCase);
    }

    public bool IsGroupAllowed(string? groupName)
    {
        return !string.IsNullOrWhiteSpace(groupName)
            && AllowedGroups.Contains(groupName);
    }
}

public static class AdConfigurationResolver
{
    private const string RequiredTestOuRoot =
        "OU=TEST_SITE_WEB,DC=home,DC=bzh";

    public static AdRuntimeConfiguration Resolve(IConfiguration configuration)
    {
        var mode = ParseMode(configuration["AD_INTEGRATION_MODE"]);
        var requiresConfiguration = mode is
            AdIntegrationMode.Test or AdIntegrationMode.Enabled;
        var domain = NullIfWhiteSpace(configuration["AD_DOMAIN"]);
        var clientsOuDn = NullIfWhiteSpace(configuration["AD_CLIENTS_OU_DN"]);
        var serviceAccountUsername = requiresConfiguration
            ? NullIfWhiteSpace(configuration["AD_SERVICE_ACCOUNT_USERNAME"])
            : null;
        var serviceAccountPassword = requiresConfiguration
            ? NullIfWhiteSpace(configuration["AD_SERVICE_ACCOUNT_PASSWORD"])
            : null;
        var allowedGroups = (configuration["AD_ALLOWED_GROUPS"] ?? string.Empty)
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var configurationValid = !requiresConfiguration
            || (
                domain is not null
                && clientsOuDn is not null
                && IsInTestOuTree(clientsOuDn)
                && !clientsOuDn.Equals(
                    "OU=KoXoAdm,DC=home,DC=bzh",
                    StringComparison.OrdinalIgnoreCase)
                && serviceAccountUsername is not null
                && serviceAccountPassword is not null
                && allowedGroups.Count > 0
            );

        return new AdRuntimeConfiguration(
            mode,
            domain,
            clientsOuDn,
            serviceAccountUsername,
            serviceAccountPassword,
            allowedGroups,
            configurationValid);
    }

    private static AdIntegrationMode ParseMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "disabled" => AdIntegrationMode.Disabled,
            "mock" => AdIntegrationMode.Mock,
            "test" => AdIntegrationMode.Test,
            "enabled" => AdIntegrationMode.Enabled,
            _ => AdIntegrationMode.Disabled
        };
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsInTestOuTree(string clientsOuDn)
    {
        return clientsOuDn.Equals(
                RequiredTestOuRoot,
                StringComparison.OrdinalIgnoreCase)
            || clientsOuDn.EndsWith(
                $",{RequiredTestOuRoot}",
                StringComparison.OrdinalIgnoreCase);
    }
}
