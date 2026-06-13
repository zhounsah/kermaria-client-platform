namespace Kermaria.ApiInternal.Data.Configuration;

public sealed record AuthRuntimeConfiguration(
    TimeSpan SessionDuration,
    int LoginMaxFailures,
    TimeSpan LoginLockoutDuration);

public static class AuthConfigurationResolver
{
    private const int DefaultSessionDurationMinutes = 60;
    private const int MaximumSessionDurationMinutes = 10080;
    private const int DefaultLoginMaxFailures = 5;
    private const int DefaultLoginLockoutMinutes = 10;

    public static AuthRuntimeConfiguration Resolve(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredValue = configuration["SESSION_DURATION_MINUTES"];

        var sessionDurationMinutes = ParseSessionDuration(
            configuredValue,
            environment);
        var maxFailures = ParseInteger(
            configuration["LOGIN_MAX_FAILURES"],
            DefaultLoginMaxFailures,
            minimum: 2,
            maximum: 20,
            "LOGIN_MAX_FAILURES");
        var lockoutMinutes = ParseInteger(
            configuration["LOGIN_LOCKOUT_MINUTES"],
            DefaultLoginLockoutMinutes,
            minimum: 1,
            maximum: 120,
            "LOGIN_LOCKOUT_MINUTES");

        return new AuthRuntimeConfiguration(
            TimeSpan.FromMinutes(sessionDurationMinutes),
            maxFailures,
            TimeSpan.FromMinutes(lockoutMinutes));
    }

    private static int ParseSessionDuration(
        string? configuredValue,
        IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return DefaultSessionDurationMinutes;
        }

        if (!int.TryParse(configuredValue, out var minutes)
            || minutes < 0
            || minutes > MaximumSessionDurationMinutes
            || (!environment.IsDevelopment() && minutes < 5))
        {
            throw new InvalidOperationException(
                "SESSION_DURATION_MINUTES is invalid.");
        }

        return minutes;
    }

    private static int ParseInteger(
        string? configuredValue,
        int defaultValue,
        int minimum,
        int maximum,
        string variableName)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return defaultValue;
        }

        if (!int.TryParse(configuredValue, out var value)
            || value < minimum
            || value > maximum)
        {
            throw new InvalidOperationException(
                $"{variableName} is invalid.");
        }

        return value;
    }
}
