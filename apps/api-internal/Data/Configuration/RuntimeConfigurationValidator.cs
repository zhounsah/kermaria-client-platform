namespace Kermaria.ApiInternal.Data.Configuration;

public sealed class RuntimeConfigurationException : Exception
{
    public RuntimeConfigurationException(IReadOnlyCollection<string> variables)
        : base(
            $"Configuration invalide : {string.Join(", ", variables.Order())}.")
    {
        Variables = variables;
    }

    public IReadOnlyCollection<string> Variables { get; }
}

public static class RuntimeConfigurationValidator
{
    private static readonly string[] MariaDbVariables =
    [
        "SQL_HOST",
        "SQL_PORT",
        "SQL_DATABASE",
        "SQL_USERNAME",
        "SQL_PASSWORD"
    ];

    private static readonly string[] DevelopmentSeedPasswordVariables =
    [
        "DEMO_PORTAL_PASSWORD",
        "DEMO_INTERNAL_ADMIN_PASSWORD"
    ];

    public static void Validate(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var invalidVariables = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        ValidateEnvironmentNames(invalidVariables);

        if (environment.IsDevelopment())
        {
            return;
        }

        var provider = configuration["SQL_PROVIDER"]?.Trim();
        if (!string.Equals(
                provider,
                "mariadb",
                StringComparison.OrdinalIgnoreCase))
        {
            invalidVariables.Add("SQL_PROVIDER");
        }
        else
        {
            foreach (var variable in MariaDbVariables)
            {
                if (string.IsNullOrWhiteSpace(configuration[variable]))
                {
                    invalidVariables.Add(variable);
                }
            }
        }

        ValidateSecret(
            configuration,
            "SQL_PASSWORD",
            invalidVariables);
        ValidateSecret(
            configuration,
            "SERVICE_AUTH_TOKEN",
            invalidVariables);

        if (string.Equals(
                configuration["SESSION_COOKIE_SECURE"]?.Trim(),
                "false",
                StringComparison.OrdinalIgnoreCase))
        {
            invalidVariables.Add("SESSION_COOKIE_SECURE");
        }

        var adMode = configuration["AD_INTEGRATION_MODE"]?.Trim();
        if (string.Equals(
                adMode,
                "read_only",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                adMode,
                "controlled_write",
                StringComparison.OrdinalIgnoreCase))
        {
            foreach (var variable in new[]
            {
                "AD_DOMAIN",
                "AD_CLIENTS_OU_DN",
                "AD_SERVICE_ACCOUNT_USERNAME"
            })
            {
                if (string.IsNullOrWhiteSpace(configuration[variable]))
                {
                    invalidVariables.Add(variable);
                }
            }

            ValidateSecret(
                configuration,
                "AD_SERVICE_ACCOUNT_PASSWORD",
                invalidVariables);
        }

        foreach (var variable in DevelopmentSeedPasswordVariables)
        {
            if (!string.IsNullOrWhiteSpace(configuration[variable]))
            {
                invalidVariables.Add(variable);
            }
        }

        if (invalidVariables.Count > 0)
        {
            throw new RuntimeConfigurationException(invalidVariables);
        }
    }

    public static bool IsPlaceholderSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "password" or "changeme" or "change-me"
            or "test" or "dev-local-token"
            || normalized.StartsWith("test", StringComparison.Ordinal)
            || normalized.Contains("replace_with", StringComparison.Ordinal)
            || normalized.Contains("replace-with", StringComparison.Ordinal)
            || normalized.Contains("example", StringComparison.Ordinal)
            || normalized.Contains("placeholder", StringComparison.Ordinal);
    }

    private static void ValidateSecret(
        IConfiguration configuration,
        string variableName,
        ISet<string> invalidVariables)
    {
        if (IsPlaceholderSecret(configuration[variableName]))
        {
            invalidVariables.Add(variableName);
        }
    }

    private static void ValidateEnvironmentNames(
        ISet<string> invalidVariables)
    {
        var aspNetEnvironment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var dotNetEnvironment =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (!string.IsNullOrWhiteSpace(aspNetEnvironment)
            && !string.IsNullOrWhiteSpace(dotNetEnvironment)
            && !string.Equals(
                aspNetEnvironment,
                dotNetEnvironment,
                StringComparison.OrdinalIgnoreCase))
        {
            invalidVariables.Add("ASPNETCORE_ENVIRONMENT");
            invalidVariables.Add("DOTNET_ENVIRONMENT");
        }
    }
}
