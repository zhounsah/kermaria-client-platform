using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface IBilledRecurringCheckoutSchemaEnsurer
{
    Task EnsureAsync(CancellationToken cancellationToken);
}

public sealed class BilledRecurringCheckoutSchemaEnsurer
    : IBilledRecurringCheckoutSchemaEnsurer
{
    private const string MigrationId = "029_billed_recurring_checkout";
    private readonly string? _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<BilledRecurringCheckoutSchemaEnsurer> _logger;
    private volatile bool _ensured;

    public BilledRecurringCheckoutSchemaEnsurer(
        SqlRuntimeConfiguration configuration,
        ILogger<BilledRecurringCheckoutSchemaEnsurer> logger)
    {
        _connectionString = configuration.IsPersistent
            ? configuration.ConnectionString
            : null;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        if (_ensured || string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_ensured)
            {
                return;
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            if (await IsMigrationAppliedAsync(connection, cancellationToken))
            {
                _ensured = true;
                _logger.LogInformation(
                    "Billed recurring checkout schema already available via migration {MigrationId}.",
                    MigrationId);
                return;
            }

            throw new InvalidOperationException(
                $"MariaDB migration {MigrationId} must be applied before billed recurring checkout can be used.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM schema_migrations
            WHERE migration_id = @migration_id;
            """;
        command.Parameters.AddWithValue("@migration_id", MigrationId);
        var count = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }
}
