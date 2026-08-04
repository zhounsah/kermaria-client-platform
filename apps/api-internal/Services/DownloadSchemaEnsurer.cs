using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed class DownloadSchemaUnavailableException : Exception
{
    public DownloadSchemaUnavailableException(string message)
        : base(message)
    {
    }
}

public interface IDownloadSchemaEnsurer
{
    Task EnsureAsync(CancellationToken cancellationToken);
}

public sealed class DownloadSchemaEnsurer : IDownloadSchemaEnsurer
{
    private const string MigrationId = "032_secure_downloads";

    private static readonly IReadOnlyList<string> RequiredTables =
    [
        "download_categories",
        "download_resources",
        "download_resource_visibility_rules"
    ];

    private readonly string? _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<DownloadSchemaEnsurer> _logger;
    private volatile bool _ensured;

    public DownloadSchemaEnsurer(
        SqlRuntimeConfiguration configuration,
        ILogger<DownloadSchemaEnsurer> logger)
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

            var missingTables = await GetMissingTablesAsync(
                connection,
                cancellationToken);
            if (missingTables.Count > 0)
            {
                throw new DownloadSchemaUnavailableException(
                    $"MariaDB migration {MigrationId} must be applied before "
                    + "the download centre can be used (missing tables: "
                    + $"{string.Join(", ", missingTables)}).");
            }

            _ensured = true;
            _logger.LogInformation(
                "Download schema already available via migration {MigrationId}.",
                MigrationId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<IReadOnlyList<string>> GetMissingTablesAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var placeholders = RequiredTables
            .Select((_, index) => $"@table_{index}")
            .ToArray();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN ({string.Join(", ", placeholders)});
            """;
        for (var index = 0; index < RequiredTables.Count; index++)
        {
            command.Parameters.AddWithValue(
                placeholders[index],
                RequiredTables[index]);
        }

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var value = reader.GetValue(0);
            var name = value switch
            {
                byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                _ => Convert.ToString(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture)
            };
            if (!string.IsNullOrWhiteSpace(name))
            {
                known.Add(name);
            }
        }

        return RequiredTables
            .Where(table => !known.Contains(table))
            .ToArray();
    }
}
