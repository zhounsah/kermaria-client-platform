namespace Kermaria.ApiInternal.Data.Configuration;

public enum StripeMode
{
    Disabled,
    Test,
    Live
}

public sealed record StripeRuntimeConfiguration(StripeMode Mode)
{
    public string ModeName => Mode.ToString().ToLowerInvariant();

    public bool IsLive => Mode is StripeMode.Live;

    public bool Enabled => Mode is not StripeMode.Disabled;
}

public static class StripeConfigurationResolver
{
    public static StripeRuntimeConfiguration Resolve(IConfiguration configuration)
    {
        var raw = configuration["STRIPE_MODE"]?.Trim().ToLowerInvariant();
        var mode = raw switch
        {
            "test" => StripeMode.Test,
            "live" => StripeMode.Live,
            _ => StripeMode.Disabled
        };
        return new StripeRuntimeConfiguration(mode);
    }
}
