namespace Kermaria.ApiInternal.Data.Configuration;

public enum BpceIntegrationMode
{
    Disabled,
    Mock,
    Live
}

public sealed record BpceRuntimeConfiguration(
    BpceIntegrationMode Mode,
    string BaseUrl,
    string? RefreshToken,
    string? SenderId,
    int RequestTimeoutMs,
    bool ConfigurationValid)
{
    public string ModeName => Mode.ToString().ToLowerInvariant();

    public bool RequestsEnabled => Mode is BpceIntegrationMode.Live;

    public bool MockEnabled => Mode is BpceIntegrationMode.Mock;
}

public static class BpceConfigurationResolver
{
    private const string DefaultBaseUrl =
        "https://www.gestion-factures.banquepopulaire.fr";

    public static BpceRuntimeConfiguration Resolve(IConfiguration configuration)
    {
        var mode = ParseMode(configuration["BPCE_INTEGRATION_MODE"]);
        var baseUrl = NormalizeBaseUrl(configuration["BPCE_BASE_URL"])
            ?? DefaultBaseUrl;
        var refreshToken = mode is BpceIntegrationMode.Live
            ? NullIfWhiteSpace(configuration["BPCE_REFRESH_TOKEN"])
            : null;
        var senderId = mode is BpceIntegrationMode.Live
            ? NullIfWhiteSpace(configuration["BPCE_SENDER_ID"])
            : null;
        var requestTimeoutMs = ParseMilliseconds(
            configuration["BPCE_REQUEST_TIMEOUT_MS"],
            10000,
            minimum: 1000,
            maximum: 30000);

        var configurationValid = mode switch
        {
            BpceIntegrationMode.Live => refreshToken is not null,
            _ => true
        };

        return new BpceRuntimeConfiguration(
            mode,
            baseUrl,
            refreshToken,
            senderId,
            requestTimeoutMs,
            configurationValid);
    }

    private static BpceIntegrationMode ParseMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "disabled" => BpceIntegrationMode.Disabled,
            "mock" => BpceIntegrationMode.Mock,
            "live" => BpceIntegrationMode.Live,
            _ => BpceIntegrationMode.Disabled
        };
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeBaseUrl(string? value)
    {
        var trimmed = NullIfWhiteSpace(value);
        if (trimmed is null)
        {
            return null;
        }

        return trimmed.EndsWith('/')
            ? trimmed[..^1]
            : trimmed;
    }

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
}
