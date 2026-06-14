using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record OperationalReadinessResult(
    bool IsHealthy,
    IReadOnlyDictionary<string, string> Checks);

public sealed class OperationalReadinessService
{
    private readonly SqlRuntimeConfiguration _sqlConfiguration;
    private readonly AdRuntimeConfiguration _adConfiguration;
    private readonly ILogger<OperationalReadinessService> _logger;

    public OperationalReadinessService(
        SqlRuntimeConfiguration sqlConfiguration,
        AdRuntimeConfiguration adConfiguration,
        ILogger<OperationalReadinessService> logger)
    {
        _sqlConfiguration = sqlConfiguration;
        _adConfiguration = adConfiguration;
        _logger = logger;
    }

    public async Task<OperationalReadinessResult> CheckAsync(
        CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["configuration"] = _sqlConfiguration.ConfigurationValid
                && _adConfiguration.ConfigurationValid
                    ? "healthy"
                    : "unhealthy",
            ["mariadb"] = await CheckMariaDbAsync(cancellationToken),
            ["ad"] = _adConfiguration.ModeName
        };

        var isHealthy = checks["configuration"] == "healthy"
            && checks["mariadb"] != "unhealthy";

        return new OperationalReadinessResult(isHealthy, checks);
    }

    private async Task<string> CheckMariaDbAsync(
        CancellationToken cancellationToken)
    {
        if (!_sqlConfiguration.IsPersistent)
        {
            return _sqlConfiguration.Provider == "mariadb"
                ? "unhealthy"
                : "not_configured";
        }

        try
        {
            await using var connection = new MySqlConnection(
                _sqlConfiguration.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);
            return "healthy";
        }
        catch (Exception exception) when (
            exception is MySqlException
                or TimeoutException
                or InvalidOperationException)
        {
            _logger.LogWarning(
                "Readiness check failed for MariaDB without exposing connection details");
            return "unhealthy";
        }
    }
}
