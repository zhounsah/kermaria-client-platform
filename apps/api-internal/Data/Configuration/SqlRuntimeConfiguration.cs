using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Configuration;

public enum PortalPersistenceMode
{
    Mock,
    MariaDb
}

public sealed record SqlRuntimeConfiguration(
    PortalPersistenceMode Mode,
    string Provider,
    string? ConnectionString,
    string StatusReason,
    bool ConfigurationValid)
{
    public bool IsPersistent => Mode == PortalPersistenceMode.MariaDb;
}

public sealed class SqlConfigurationException : Exception
{
    public SqlConfigurationException(string code, string message)
        : base($"{code}: {message}")
    {
        Code = code;
    }

    public string Code { get; }
}

public static class SqlConfigurationResolver
{
    private static readonly string[] RequiredVariables =
    [
        "SQL_HOST",
        "SQL_PORT",
        "SQL_DATABASE",
        "SQL_USERNAME",
        "SQL_PASSWORD"
    ];

    public static SqlRuntimeConfiguration Resolve(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var provider = configuration["SQL_PROVIDER"]?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(provider))
        {
            return ResolveMissing(
                environment,
                "SQL_PROVIDER is not configured",
                providerWasRequested: false);
        }

        if (provider != "mariadb")
        {
            throw new SqlConfigurationException(
                "SQL_PROVIDER_UNSUPPORTED",
                "Le fournisseur SQL configuré n'est pas pris en charge.");
        }

        var missingVariables = RequiredVariables
            .Where(name => string.IsNullOrWhiteSpace(configuration[name]))
            .ToArray();

        if (missingVariables.Length > 0)
        {
            return ResolveMissing(
                environment,
                "MariaDB configuration is incomplete",
                providerWasRequested: true);
        }

        if (!uint.TryParse(configuration["SQL_PORT"], out var port)
            || port is 0 or > 65535)
        {
            throw new SqlConfigurationException(
                "SQL_CONFIG_INVALID",
                "La configuration SQL est invalide.");
        }

        var connectionString = new MySqlConnectionStringBuilder
        {
            Server = configuration["SQL_HOST"],
            Port = port,
            Database = configuration["SQL_DATABASE"],
            UserID = configuration["SQL_USERNAME"],
            Password = configuration["SQL_PASSWORD"],
            CharacterSet = "utf8mb4",
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 15,
            SslMode = MySqlSslMode.Preferred
        }.ConnectionString;

        return new SqlRuntimeConfiguration(
            PortalPersistenceMode.MariaDb,
            provider,
            connectionString,
            "mariadb-configured",
            ConfigurationValid: true);
    }

    private static SqlRuntimeConfiguration ResolveMissing(
        IHostEnvironment environment,
        string statusReason,
        bool providerWasRequested)
    {
        if (!environment.IsDevelopment())
        {
            throw new SqlConfigurationException(
                "SQL_CONFIG_MISSING",
                "La configuration SQL requise est absente.");
        }

        return new SqlRuntimeConfiguration(
            PortalPersistenceMode.Mock,
            providerWasRequested ? "mariadb" : "mock",
            null,
            statusReason,
            ConfigurationValid: !providerWasRequested);
    }
}
