namespace Kermaria.ApiInternal.Data.Configuration;

public sealed record KoxoSyncWebhookRuntimeConfiguration(
    Uri? Url,
    string? BearerToken,
    TimeSpan Timeout,
    bool AllowInsecureHttp)
{
    public bool Enabled => Url is not null && !string.IsNullOrWhiteSpace(BearerToken);
}

public static class KoxoSyncWebhookConfigurationResolver
{
    private const int DefaultTimeoutSeconds = 10;
    private const int MaximumTimeoutSeconds = 60;

    public static KoxoSyncWebhookRuntimeConfiguration Resolve(
        IConfiguration configuration)
    {
        var urlValue = configuration["KOXO_SYNC_WEBHOOK_URL"]?.Trim();
        var tokenValue = configuration["KOXO_SYNC_WEBHOOK_TOKEN"]?.Trim();
        var allowInsecureHttp = ParseBool(
            configuration["KOXO_SYNC_WEBHOOK_ALLOW_INSECURE_HTTP"],
            fallback: false);

        if (string.IsNullOrWhiteSpace(urlValue)
            && string.IsNullOrWhiteSpace(tokenValue))
        {
            return new KoxoSyncWebhookRuntimeConfiguration(
                Url: null,
                BearerToken: null,
                Timeout: TimeSpan.FromSeconds(DefaultTimeoutSeconds),
                AllowInsecureHttp: allowInsecureHttp);
        }

        if (string.IsNullOrWhiteSpace(urlValue)
            || !Uri.TryCreate(urlValue, UriKind.Absolute, out var url))
        {
            throw new InvalidOperationException("KOXO_SYNC_WEBHOOK_URL is invalid.");
        }

        if (!string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(url.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(url.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(url.Host, "::1", StringComparison.OrdinalIgnoreCase)
            && !allowInsecureHttp)
        {
            throw new InvalidOperationException(
                "KOXO_SYNC_WEBHOOK_URL must use HTTPS unless KOXO_SYNC_WEBHOOK_ALLOW_INSECURE_HTTP=true.");
        }

        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            throw new InvalidOperationException("KOXO_SYNC_WEBHOOK_TOKEN is required.");
        }

        return new KoxoSyncWebhookRuntimeConfiguration(
            Url: url,
            BearerToken: tokenValue,
            Timeout: TimeSpan.FromSeconds(ParseInt(
                configuration["KOXO_SYNC_WEBHOOK_TIMEOUT_SECONDS"],
                fallback: DefaultTimeoutSeconds,
                minimum: 1,
                maximum: MaximumTimeoutSeconds)),
            AllowInsecureHttp: allowInsecureHttp);
    }

    private static bool ParseBool(string? value, bool fallback)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" => fallback,
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => fallback
        };
    }

    private static int ParseInt(
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
