namespace Kermaria.ApiInternal.Data.Configuration;

// V0.26 : configuration de l'inscription self-service. Le kill switch
// SIGNUP_ENABLED est appliqué en premier lieu côté webportal (routes
// publiques) ; il est répliqué ici pour que l'API-INTERNAL refuse toute
// soumission si le flag est désactivé, même en cas d'appel direct.
public sealed record SignupRuntimeConfiguration(
    bool Enabled,
    int RateLimitPerIpPerHour,
    int RateLimitPerEmailPer24h,
    int VerificationTokenTtlHours,
    int PasswordSetupTokenTtlHours,
    bool AutoApprove);

public static class SignupConfigurationResolver
{
    public static SignupRuntimeConfiguration Resolve(
        IConfiguration configuration)
    {
        return new SignupRuntimeConfiguration(
            Enabled: ParseBool(configuration["SIGNUP_ENABLED"], false),
            RateLimitPerIpPerHour: ParseInt(
                configuration["SIGNUP_RATE_LIMIT_PER_IP_PER_HOUR"],
                fallback: 3,
                minimum: 1,
                maximum: 100),
            RateLimitPerEmailPer24h: ParseInt(
                configuration["SIGNUP_RATE_LIMIT_PER_EMAIL_PER_24H"],
                fallback: 1,
                minimum: 1,
                maximum: 100),
            VerificationTokenTtlHours: ParseInt(
                configuration["SIGNUP_VERIFICATION_TOKEN_TTL_HOURS"],
                fallback: 24,
                minimum: 1,
                maximum: 168),
            PasswordSetupTokenTtlHours: ParseInt(
                configuration["SIGNUP_PASSWORD_SETUP_TOKEN_TTL_HOURS"],
                fallback: 24,
                minimum: 1,
                maximum: 168),
            // Toujours false jusqu'à V1.0 RC. Réservé : aucun code ne
            // bascule sur cette valeur dans ce lot (validation manuelle
            // obligatoire). Documenté dans .env.example.
            AutoApprove: ParseBool(configuration["SIGNUP_AUTO_APPROVE"], false));
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
