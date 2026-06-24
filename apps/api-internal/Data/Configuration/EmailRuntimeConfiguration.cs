namespace Kermaria.ApiInternal.Data.Configuration;

public enum EmailIntegrationMode
{
    Disabled,
    Mock,
    Live
}

public sealed record EmailRuntimeConfiguration(
    EmailIntegrationMode Mode,
    string? SmtpHost,
    int SmtpPort,
    bool SmtpUseStartTls,
    string? SmtpUsername,
    string? SmtpPassword,
    string? FromAddress,
    string FromDisplayName,
    string PortalPublicUrl,
    int RequestTimeoutMs,
    bool ConfigurationValid)
{
    public string ModeName => Mode.ToString().ToLowerInvariant();

    public bool SendsEnabled => Mode is EmailIntegrationMode.Live;
}

public static class EmailConfigurationResolver
{
    public static EmailRuntimeConfiguration Resolve(IConfiguration configuration)
    {
        var mode = ParseMode(configuration["EMAIL_INTEGRATION_MODE"]);
        var smtpHost = NullIfWhiteSpace(configuration["SMTP_HOST"]);
        var smtpPort = ParsePort(configuration["SMTP_PORT"], 587);
        var useStartTls = ParseBool(configuration["SMTP_USE_STARTTLS"], true);
        var smtpUsername = NullIfWhiteSpace(configuration["SMTP_USERNAME"]);
        var smtpPassword = NullIfWhiteSpace(configuration["SMTP_PASSWORD"]);
        var fromAddress = NullIfWhiteSpace(configuration["SMTP_FROM_ADDRESS"]);
        var fromDisplayName =
            NullIfWhiteSpace(configuration["SMTP_FROM_DISPLAY_NAME"])
            ?? "Kermaria";
        var portalPublicUrl =
            NullIfWhiteSpace(configuration["PUBLIC_PORTAL_URL"])
            ?? string.Empty;
        var requestTimeoutMs = ParseMilliseconds(
            configuration["SMTP_TIMEOUT_MS"],
            10000,
            minimum: 1000,
            maximum: 30000);

        var configurationValid = mode switch
        {
            EmailIntegrationMode.Live =>
                !string.IsNullOrWhiteSpace(smtpHost)
                && !string.IsNullOrWhiteSpace(fromAddress),
            _ => true
        };

        return new EmailRuntimeConfiguration(
            mode,
            smtpHost,
            smtpPort,
            useStartTls,
            smtpUsername,
            smtpPassword,
            fromAddress,
            fromDisplayName,
            portalPublicUrl,
            requestTimeoutMs,
            configurationValid);
    }

    private static EmailIntegrationMode ParseMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "disabled" => EmailIntegrationMode.Disabled,
            "mock" => EmailIntegrationMode.Mock,
            "live" => EmailIntegrationMode.Live,
            _ => EmailIntegrationMode.Disabled
        };
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int ParsePort(string? value, int fallback)
    {
        if (!int.TryParse(value, out var parsed))
        {
            return fallback;
        }
        return Math.Clamp(parsed, 1, 65535);
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
