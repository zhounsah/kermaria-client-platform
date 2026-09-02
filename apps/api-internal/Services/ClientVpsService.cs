using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface IClientVpsService
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<ClientVpsSummary>> GetClientVpsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<ClientVpsDetail?> GetClientVpsAsync(
        PortalSessionContext session,
        string vpsId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Lecture operationnelle des VPS d'un client. Cette projection ne reutilise
/// pas la revue VPS administrative : son SQL ne selectionne jamais les cibles
/// d'infrastructure, notes internes ou identifiants fournisseur.
/// </summary>
public sealed class ClientVpsService : IClientVpsService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly IBillingV2PublicCatalogService _catalog;

    public ClientVpsService(
        SqlRuntimeConfiguration sql,
        IBillingV2PublicCatalogService catalog)
    {
        _sql = sql;
        _catalog = catalog;
    }

    public bool IsPersistent => _sql.IsPersistent;

    public async Task<IReadOnlyList<ClientVpsSummary>> GetClientVpsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        if (!IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return [];
        }

        var catalog = await _catalog.GetCatalogAsync(cancellationToken);
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaReadyAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ListSelectSql;
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var vps = new List<ClientVpsSummary>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = ReadRow(reader);
            var catalogEntry = ResolveCatalogEntry(catalog, row.ServiceCode, row.TierCode);
            vps.Add(new ClientVpsSummary(
                row.Id,
                row.ServiceCode,
                catalogEntry.ServiceName,
                row.TierCode,
                catalogEntry.TierLabel,
                row.Hostname,
                ToClientProvisioningStatus(row.ProvisioningStatus),
                row.PublicIpAddress,
                row.ProvisioningStartedAt,
                row.ActivatedAt));
        }

        return vps;
    }

    public async Task<ClientVpsDetail?> GetClientVpsAsync(
        PortalSessionContext session,
        string vpsId,
        CancellationToken cancellationToken)
    {
        if (!IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return null;
        }

        var catalog = await _catalog.GetCatalogAsync(cancellationToken);
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaReadyAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = DetailSelectSql;
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);
        command.Parameters.AddWithValue("@vps_id", vpsId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var row = ReadRow(reader);
        var catalogEntry = ResolveCatalogEntry(catalog, row.ServiceCode, row.TierCode);
        return new ClientVpsDetail(
            row.Id,
            row.ServiceCode,
            catalogEntry.ServiceName,
            row.TierCode,
            catalogEntry.TierLabel,
            row.Hostname,
            row.OperatingSystem,
            row.Usage,
            row.ManagementMode,
            row.InternetExposure,
            ToClientProvisioningStatus(row.ProvisioningStatus),
            row.PublicIpAddress,
            row.ProvisioningStartedAt,
            row.ActivatedAt,
            catalogEntry.Specifications);
    }

    private static CatalogEntry ResolveCatalogEntry(
        BillingV2PublicCatalogSnapshot catalog,
        string serviceCode,
        string tierCode)
    {
        var service = catalog.Services.FirstOrDefault(candidate =>
            string.Equals(candidate.Code, serviceCode, StringComparison.OrdinalIgnoreCase));
        var tier = service?.Tiers.FirstOrDefault(candidate =>
            string.Equals(candidate.Code, tierCode, StringComparison.OrdinalIgnoreCase));
        return new CatalogEntry(
            service?.Name ?? serviceCode,
            tier?.Label ?? tierCode,
            new ClientVpsSpecifications(
                AttributeValue(tier, "vcpu_count"),
                AttributeValue(tier, "ram_gib"),
                AttributeValue(tier, "disk_gib")));
    }

    private static long? AttributeValue(BillingV2PublicTier? tier, string code)
        => tier?.Attributes.FirstOrDefault(attribute =>
            string.Equals(attribute.Code, code, StringComparison.OrdinalIgnoreCase))?.ValueNumeric;

    private static string ToClientProvisioningStatus(string status)
        => status switch
        {
            "active" => "active",
            "provisioning" => "in_progress",
            "failed" => "attention_required",
            _ => "preparing"
        };

    private static ClientVpsRow ReadRow(MySqlDataReader reader)
        => new(
            MariaDbIdentifierReader.ReadRequired(reader, "vps_id"),
            reader.GetString("service_code"),
            reader.GetString("tier_code"),
            reader.GetString("hostname"),
            reader.GetString("operating_system"),
            reader.GetString("usage_description"),
            reader.GetString("management_mode"),
            reader.GetString("internet_exposure"),
            reader.GetString("provisioning_status"),
            ReadNullableString(reader, "public_ip_address"),
            ReadNullableDateTime(reader, "provisioning_started_at"),
            ReadNullableDateTime(reader, "activated_at"));

    private static string? ReadNullableString(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(column);

    private static DateTime? ReadNullableDateTime(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetDateTime(column);

    private static async Task EnsureSchemaReadyAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN (
                  'billing_v2_vps_technical_requests',
                  'billing_v2_vps_technical_request_revisions',
                  'billing_v2_vps_technical_request_checkouts',
                  'billing_v2_billing_events');
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 4)
        {
            throw new InvalidOperationException("CLIENT_VPS_SCHEMA_UNAVAILABLE");
        }

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'billing_v2_vps_technical_requests'
              AND column_name IN (
                  'provisioning_status', 'public_ip_address',
                  'provisioning_started_at', 'activated_at');
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 4)
        {
            throw new InvalidOperationException("CLIENT_VPS_SCHEMA_UNAVAILABLE");
        }
    }

    private const string SelectBaseSql =
        """
        SELECT request_row.id AS vps_id,
               request_row.service_code,
               request_row.tier_code,
               revision.hostname,
               revision.operating_system,
               revision.usage_description,
               revision.management_mode,
               revision.internet_exposure,
               request_row.provisioning_status,
               request_row.public_ip_address,
               request_row.provisioning_started_at,
               request_row.activated_at
        FROM billing_v2_vps_technical_requests request_row
        INNER JOIN billing_v2_vps_technical_request_checkouts checkout_link
            ON checkout_link.technical_request_id = request_row.id
           AND checkout_link.technical_request_revision_number = request_row.current_revision
        INNER JOIN billing_v2_billing_events event_row
            ON event_row.id = checkout_link.billing_event_id
        INNER JOIN billing_v2_vps_technical_request_revisions revision
            ON revision.technical_request_id = request_row.id
           AND revision.revision_number = checkout_link.technical_request_revision_number
        WHERE request_row.customer_id = @customer_id
          AND event_row.settlement_status = 'settled'
        """;

    private const string ListSelectSql = SelectBaseSql +
        """
        ORDER BY request_row.activated_at DESC, request_row.created_at DESC, request_row.id ASC;
        """;

    private const string DetailSelectSql = SelectBaseSql +
        """
          AND request_row.id = @vps_id
        LIMIT 1;
        """;

    private sealed record CatalogEntry(
        string ServiceName,
        string TierLabel,
        ClientVpsSpecifications Specifications);

    private sealed record ClientVpsRow(
        string Id,
        string ServiceCode,
        string TierCode,
        string Hostname,
        string OperatingSystem,
        string Usage,
        string ManagementMode,
        string InternetExposure,
        string ProvisioningStatus,
        string? PublicIpAddress,
        DateTime? ProvisioningStartedAt,
        DateTime? ActivatedAt);
}
