namespace Kermaria.ApiInternal.Data.Configuration;

public enum PayPalMode
{
    Sandbox,
    Live
}

public sealed record PayPalRuntimeConfiguration(
    PayPalMode Mode,
    string? ClientId,
    string? ClientSecret)
{
    public string ModeName => Mode.ToString().ToLowerInvariant();

    public bool IsLive => Mode is PayPalMode.Live;

    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret);

    public string ApiBaseUrl => IsLive
        ? "https://api-m.paypal.com"
        : "https://api-m.sandbox.paypal.com";
}

public static class PayPalConfigurationResolver
{
    public static PayPalRuntimeConfiguration Resolve(IConfiguration configuration)
    {
        var raw = configuration["PAYPAL_MODE"]?.Trim().ToLowerInvariant();
        var mode = raw switch
        {
            "live" => PayPalMode.Live,
            _ => PayPalMode.Sandbox
        };

        return new PayPalRuntimeConfiguration(
            mode,
            configuration["PAYPAL_CLIENT_ID"]?.Trim(),
            configuration["PAYPAL_CLIENT_SECRET"]?.Trim());
    }
}
