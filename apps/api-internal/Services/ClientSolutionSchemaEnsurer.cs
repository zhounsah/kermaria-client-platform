using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed class ClientSolutionSchemaUnavailableException : Exception
{
    public ClientSolutionSchemaUnavailableException(string message)
        : base(message)
    {
    }
}

public interface IClientSolutionSchemaEnsurer
{
    Task EnsureAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Verification en lecture seule de la precondition de schema. Aucun DDL n'est
/// execute au fil des requetes : le compte applicatif n'a pas les droits de
/// schema, et une tentative echouerait sur `schema_migrations` avant meme
/// d'examiner les migrations en attente.
/// </summary>
public sealed class ClientSolutionSchemaEnsurer : IClientSolutionSchemaEnsurer
{
    private const string MigrationId = "041_client_solutions_portal";

    private static readonly IReadOnlyList<string> RequiredTables =
    [
        "client_solutions",
        "client_solution_portal_settings"
    ];

    private readonly string? _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<ClientSolutionSchemaEnsurer> _logger;
    private volatile bool _ensured;

    public ClientSolutionSchemaEnsurer(
        SqlRuntimeConfiguration configuration,
        ILogger<ClientSolutionSchemaEnsurer> logger)
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
                throw new ClientSolutionSchemaUnavailableException(
                    $"MariaDB migration {MigrationId} must be applied before "
                    + "the client solutions portal can be used (missing tables: "
                    + $"{string.Join(", ", missingTables)}).");
            }

            _ensured = true;
            _logger.LogInformation(
                "Client solution schema already available via migration {MigrationId}.",
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
