namespace Kermaria.ApiInternal.Data.Configuration;

public sealed record AdPasswordRuntimeConfiguration(
    bool ChangeEnabled,
    int MaxFailuresPer15Minutes,
    TimeSpan FailureWindow,
    TimeSpan LockoutDuration);

public static class AdPasswordConfigurationResolver
{
    private const int DefaultMaxFailures = 3;
    private static readonly TimeSpan DefaultFailureWindow =
        TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DefaultLockoutDuration =
        TimeSpan.FromMinutes(15);

    public static AdPasswordRuntimeConfiguration Resolve(
        IConfiguration configuration)
    {
        var changeEnabled = string.Equals(
            configuration["AD_PASSWORD_CHANGE_ENABLED"]?.Trim(),
            "true",
            StringComparison.OrdinalIgnoreCase);

        var maxFailures = ParseInteger(
            configuration["AD_PASSWORD_RATE_LIMIT_PER_15MIN"],
            DefaultMaxFailures,
            minimum: 1,
            maximum: 50);

        return new AdPasswordRuntimeConfiguration(
            changeEnabled,
            maxFailures,
            DefaultFailureWindow,
            DefaultLockoutDuration);
    }

    private static int ParseInteger(
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
