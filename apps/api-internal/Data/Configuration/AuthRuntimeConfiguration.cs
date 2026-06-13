namespace Kermaria.ApiInternal.Data.Configuration;

public sealed record AuthRuntimeConfiguration(TimeSpan SessionDuration);

public static class AuthConfigurationResolver
{
    private const int DefaultSessionDurationMinutes = 60;
    private const int MaximumSessionDurationMinutes = 10080;

    public static AuthRuntimeConfiguration Resolve(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredValue = configuration["SESSION_DURATION_MINUTES"];

        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return new AuthRuntimeConfiguration(
                TimeSpan.FromMinutes(DefaultSessionDurationMinutes));
        }

        if (!int.TryParse(configuredValue, out var minutes)
            || minutes < 0
            || minutes > MaximumSessionDurationMinutes
            || (!environment.IsDevelopment() && minutes < 5))
        {
            throw new InvalidOperationException(
                "SESSION_DURATION_MINUTES is invalid.");
        }

        return new AuthRuntimeConfiguration(TimeSpan.FromMinutes(minutes));
    }
}
