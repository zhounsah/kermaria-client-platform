namespace Kermaria.ApiInternal.Data.Configuration;

public enum PayPalMode
{
    Sandbox,
    Live
}

public sealed record PayPalRuntimeConfiguration(PayPalMode Mode)
{
    public string ModeName => Mode.ToString().ToLowerInvariant();

    public bool IsLive => Mode is PayPalMode.Live;
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
        return new PayPalRuntimeConfiguration(mode);
    }
}
