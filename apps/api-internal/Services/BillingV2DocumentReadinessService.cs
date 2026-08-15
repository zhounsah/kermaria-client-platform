using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface IBillingV2DocumentReadinessService
{
    Task<BillingV2DocumentReadinessStatus> CheckAsync(
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2DocumentReadinessService
    : IBillingV2DocumentReadinessService
{
    public static NoOpBillingV2DocumentReadinessService Instance { get; }
        = new();

    private NoOpBillingV2DocumentReadinessService()
    {
    }

    public Task<BillingV2DocumentReadinessStatus> CheckAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(BillingV2DocumentReadinessStatus.NotReady);
}

public sealed class BillingV2DocumentReadinessService
    : IBillingV2DocumentReadinessService
{
    private static readonly string[] RequiredTables =
    [
        "commercial_documents",
        "commercial_document_lines",
        "bpce_invoices",
        "billing_v2_subscription_documents",
        "billing_v2_document_line_snapshots"
    ];

    private readonly SqlRuntimeConfiguration _sql;
    private readonly BpceRuntimeConfiguration _bpce;
    private readonly ILogger<BillingV2DocumentReadinessService> _logger;

    public BillingV2DocumentReadinessService(
        SqlRuntimeConfiguration sql,
        BpceRuntimeConfiguration bpce,
        ILogger<BillingV2DocumentReadinessService> logger)
    {
        _sql = sql;
        _bpce = bpce;
        _logger = logger;
    }

    public async Task<BillingV2DocumentReadinessStatus> CheckAsync(
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return new BillingV2DocumentReadinessStatus(
                false,
                "BILLING_V2_DOCUMENT_NO_PERSISTENT_SQL",
                "Billing V2 document readiness requires persistent SQL.");
        }

        if (_bpce.Mode is BpceIntegrationMode.Disabled)
        {
            return BillingV2DocumentReadinessStatus.NotReady;
        }

        try
        {
            var missing = await LoadMissingTablesAsync(cancellationToken);
            if (missing.Count > 0)
            {
                return new BillingV2DocumentReadinessStatus(
                    false,
                    "BILLING_V2_DOCUMENT_SCHEMA_INCOMPLETE",
                    $"Missing document tables: {string.Join(", ", missing)}.");
            }
        }
        catch (MySqlException exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 document readiness check failed.");
            return new BillingV2DocumentReadinessStatus(
                false,
                "BILLING_V2_DOCUMENT_READINESS_UNVERIFIED",
                "Billing V2 document readiness could not be verified.");
        }

        return BillingV2DocumentReadinessStatus.ReadyForCheckout;
    }

    private async Task<IReadOnlyList<string>> LoadMissingTablesAsync(
        CancellationToken cancellationToken)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN (
                'commercial_documents',
                'commercial_document_lines',
                'bpce_invoices',
                'billing_v2_subscription_documents',
                'billing_v2_document_line_snapshots'
              );
            """;
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            existing.Add(reader.GetString("table_name"));
        }

        return RequiredTables
            .Where(table => !existing.Contains(table))
            .OrderBy(table => table, StringComparer.Ordinal)
            .ToArray();
    }
}
