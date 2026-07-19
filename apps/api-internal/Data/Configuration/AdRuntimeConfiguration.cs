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
    string? RequiredOuRoot,
    IReadOnlyList<string> AllowedRoots,
    bool UseCurrentWindowsCredentials,
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

    public bool IsWithinAllowedRoots(string? distinguishedName)
    {
        var normalized = NormalizeDistinguishedName(distinguishedName);
        if (normalized is null)
        {
            return false;
        }

        foreach (var allowedRoot in AllowedRoots)
        {
            if (normalized.Equals(
                    allowedRoot,
                    StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(
                    $",{allowedRoot}",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public string ResolveDomainForDistinguishedName(string? distinguishedName)
        => AdConfigurationResolver.ExtractDomainFromDn(distinguishedName)
            ?? Domain
            ?? string.Empty;

    public string BuildLdapPath(string distinguishedName)
    {
        var normalized = NormalizeDistinguishedName(distinguishedName)
            ?? throw new InvalidOperationException(
                "The Active Directory distinguished name is unavailable.");
        var domain = ResolveDomainForDistinguishedName(normalized);
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new InvalidOperationException(
                "The Active Directory domain is unavailable.");
        }

        return $"LDAP://{domain}/{normalized}";
    }

    public string? NormalizeDistinguishedName(string? distinguishedName)
        => AdConfigurationResolver.NormalizeDn(distinguishedName);
}

public static class AdConfigurationResolver
{
    public static AdRuntimeConfiguration Resolve(IConfiguration configuration)
    {
        var mode = ParseMode(configuration["AD_INTEGRATION_MODE"]);
        var requiresDirectoryConfiguration = mode is
            AdIntegrationMode.ReadOnly
            or AdIntegrationMode.ControlledWrite;
        var domain = NullIfWhiteSpace(configuration["AD_DOMAIN"]);
        var clientsOuDn = NormalizeDn(configuration["AD_CLIENTS_OU_DN"]);
        var requiredOuRoot = NormalizeDn(
            configuration["AD_REQUIRED_OU_ROOT"])
            ?? clientsOuDn;
        var allowedRoots = ParseAllowedRoots(
            configuration["AD_ALLOWED_ROOTS"],
            requiredOuRoot,
            clientsOuDn);
        var useCurrentWindowsCredentials = string.Equals(
            configuration["AD_USE_CURRENT_WINDOWS_CREDENTIALS"]?.Trim(),
            "true",
            StringComparison.OrdinalIgnoreCase);
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
                && requiredOuRoot is not null
                && allowedRoots.Count > 0
                && IsWithinRoot(clientsOuDn, requiredOuRoot)
                && allowedRoots.All(root => IsWithinRoot(root, requiredOuRoot))
                && (
                    useCurrentWindowsCredentials
                    || (
                        serviceAccountUsername is not null
                        && serviceAccountPassword is not null
                    )
                )
            );

        return new AdRuntimeConfiguration(
            mode,
            domain,
            clientsOuDn,
            requiredOuRoot,
            allowedRoots,
            useCurrentWindowsCredentials,
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

    internal static string? NormalizeDn(string? distinguishedName)
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

    internal static string? ExtractDomainFromDn(string? distinguishedName)
    {
        var normalized = NormalizeDn(distinguishedName);
        if (normalized is null)
        {
            return null;
        }

        var components = SplitDn(normalized)
            .Select(part => TryReadDnValue(part, "DC"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        return components.Length == 0
            ? null
            : string.Join(".", components);
    }

    private static IReadOnlyList<string> ParseAllowedRoots(
        string? value,
        string? requiredOuRoot,
        string? clientsOuDn)
    {
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(value))
        {
            foreach (var candidate in value.Split(
                [';', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries))
            {
                var normalized = NormalizeDn(candidate);
                if (normalized is not null)
                {
                    roots.Add(normalized);
                }
            }
        }

        if (roots.Count == 0)
        {
            var fallback = requiredOuRoot ?? clientsOuDn;
            if (fallback is not null)
            {
                roots.Add(fallback);
            }
        }

        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsWithinRoot(
        string distinguishedName,
        string requiredOuRoot)
    {
        return distinguishedName.Equals(
                requiredOuRoot,
                StringComparison.OrdinalIgnoreCase)
            || distinguishedName.EndsWith(
                $",{requiredOuRoot}",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadDnValue(string part, string expectedKey)
    {
        var separatorIndex = FindUnescapedSeparator(part, '=');
        if (separatorIndex <= 0)
        {
            return null;
        }

        var key = part[..separatorIndex].Trim();
        if (!key.Equals(expectedKey, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return part[(separatorIndex + 1)..].Trim();
    }

    private static string[] SplitDn(string distinguishedName)
    {
        var parts = new List<string>();
        var buffer = new System.Text.StringBuilder();
        var escaped = false;

        foreach (var character in distinguishedName)
        {
            if (escaped)
            {
                buffer.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                buffer.Append(character);
                escaped = true;
                continue;
            }

            if (character == ',')
            {
                var part = buffer.ToString().Trim();
                if (part.Length > 0)
                {
                    parts.Add(part);
                }

                buffer.Clear();
                continue;
            }

            buffer.Append(character);
        }

        var lastPart = buffer.ToString().Trim();
        if (lastPart.Length > 0)
        {
            parts.Add(lastPart);
        }

        return parts.ToArray();
    }

    private static int FindUnescapedSeparator(string value, char separator)
    {
        var escaped = false;

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == separator)
            {
                return index;
            }
        }

        return -1;
    }
}
