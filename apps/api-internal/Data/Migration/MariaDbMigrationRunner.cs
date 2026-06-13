using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Migration;

public sealed class MariaDbMigrationRunner
{
    private const string StatementSeparator = "-- statement-break";
    private readonly SqlRuntimeConfiguration _configuration;
    private readonly ILogger<MariaDbMigrationRunner> _logger;
    private readonly IConfiguration _applicationConfiguration;
    private readonly IHostEnvironment _environment;
    private readonly IPortalPasswordService _passwordService;

    public MariaDbMigrationRunner(
        SqlRuntimeConfiguration configuration,
        IConfiguration applicationConfiguration,
        IHostEnvironment environment,
        IPortalPasswordService passwordService,
        ILogger<MariaDbMigrationRunner> logger)
    {
        _configuration = configuration;
        _applicationConfiguration = applicationConfiguration;
        _environment = environment;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task ApplyAsync(
        bool seedDevelopmentData,
        CancellationToken cancellationToken = default)
    {
        if (!_configuration.IsPersistent
            || string.IsNullOrWhiteSpace(_configuration.ConnectionString))
        {
            throw new InvalidOperationException(
                "MariaDB must be configured before applying migrations.");
        }

        await using var connection = new MySqlConnection(
            _configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureMigrationTableAsync(connection, cancellationToken);

        var migrationDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Migrations",
            "MariaDb");
        var migrationFiles = Directory
            .EnumerateFiles(migrationDirectory, "*.sql")
            .Where(path => char.IsDigit(Path.GetFileName(path)[0]))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (var migrationFile in migrationFiles)
        {
            var migrationId = Path.GetFileNameWithoutExtension(migrationFile);

            if (await IsAppliedAsync(
                    connection,
                    migrationId,
                    cancellationToken))
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(
                migrationFile,
                cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                cancellationToken);

            foreach (var statement in SplitStatements(sql))
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = statement;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var historyCommand = connection.CreateCommand())
            {
                historyCommand.Transaction = transaction;
                historyCommand.CommandText =
                    """
                    INSERT INTO schema_migrations (migration_id, applied_at)
                    VALUES (@migration_id, @applied_at);
                    """;
                historyCommand.Parameters.AddWithValue(
                    "@migration_id",
                    migrationId);
                historyCommand.Parameters.AddWithValue(
                    "@applied_at",
                    DateTime.UtcNow);
                await historyCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "Applied MariaDB migration {MigrationId}",
                migrationId);
        }

        if (seedDevelopmentData)
        {
            await SeedIfEmptyAsync(
                connection,
                migrationDirectory,
                cancellationToken);
            await SeedDemoPortalUserAsync(connection, cancellationToken);
        }
    }

    private async Task SeedDemoPortalUserAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        var email = _applicationConfiguration["DEMO_PORTAL_EMAIL"]?.Trim();
        var password = _applicationConfiguration["DEMO_PORTAL_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "Development portal user seed skipped because demo credentials are not configured");
            return;
        }

        await using var customerCommand = connection.CreateCommand();
        customerCommand.CommandText =
            "SELECT id FROM customers ORDER BY created_at LIMIT 1;";
        var customerIdValue = await customerCommand.ExecuteScalarAsync(
            cancellationToken);
        var customerId = customerIdValue?.ToString();

        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new InvalidOperationException(
                "A development customer is required before seeding a portal user.");
        }

        await using var userLookup = connection.CreateCommand();
        userLookup.CommandText =
            """
            SELECT id
            FROM portal_users
            WHERE customer_id = @customer_id
            ORDER BY created_at
            LIMIT 1;
            """;
        userLookup.Parameters.AddWithValue("@customer_id", customerId);
        var existingUserId = (
            await userLookup.ExecuteScalarAsync(cancellationToken))?.ToString();
        var userId = string.IsNullOrWhiteSpace(existingUserId)
            ? Guid.NewGuid().ToString("D")
            : existingUserId;
        var passwordHash = _passwordService.HashPassword(userId, password);
        var now = DateTime.UtcNow;

        await using var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(existingUserId))
        {
            command.CommandText =
                """
                INSERT INTO portal_users (
                    id,
                    customer_id,
                    identity_provider_subject,
                    email,
                    password_hash,
                    display_name,
                    status,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @customer_id,
                    @identity_provider_subject,
                    @email,
                    @password_hash,
                    'Client démo',
                    'active',
                    @created_at,
                    @updated_at
                );
                """;
            command.Parameters.AddWithValue(
                "@identity_provider_subject",
                $"local-demo-{userId}");
            command.Parameters.AddWithValue("@customer_id", customerId);
            command.Parameters.AddWithValue("@created_at", now);
        }
        else
        {
            command.CommandText =
                """
                UPDATE portal_users
                SET email = @email,
                    password_hash = @password_hash,
                    status = 'active',
                    updated_at = @updated_at
                WHERE id = @id;
                """;
        }

        command.Parameters.AddWithValue("@id", userId);
        command.Parameters.AddWithValue("@email", email.ToLowerInvariant());
        command.Parameters.AddWithValue("@password_hash", passwordHash);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation(
            "Development portal user credentials configured without logging credential values");
    }

    private static async Task EnsureMigrationTableAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                migration_id VARCHAR(160) NOT NULL PRIMARY KEY,
                applied_at DATETIME(6) NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
              COLLATE=utf8mb4_unicode_ci;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsAppliedAsync(
        MySqlConnection connection,
        string migrationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM schema_migrations
            WHERE migration_id = @migration_id;
            """;
        command.Parameters.AddWithValue("@migration_id", migrationId);
        var count = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    private async Task SeedIfEmptyAsync(
        MySqlConnection connection,
        string migrationDirectory,
        CancellationToken cancellationToken)
    {
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM customers;";
        var customerCount = Convert.ToInt32(
            await countCommand.ExecuteScalarAsync(cancellationToken));

        if (customerCount > 0)
        {
            _logger.LogInformation(
                "Development seed skipped because customer data already exists");
            return;
        }

        var seedPath = Path.Combine(
            migrationDirectory,
            "seed_development.sql");
        var sql = await File.ReadAllTextAsync(seedPath, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        foreach (var statement in SplitStatements(sql))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation(
            "Inserted fictional MariaDB development seed data");
    }

    private static IEnumerable<string> SplitStatements(string sql)
    {
        return sql
            .Split(
                StatementSeparator,
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Where(statement => !string.IsNullOrWhiteSpace(statement));
    }
}
