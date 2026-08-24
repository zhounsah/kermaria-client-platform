using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services.Provisioning;

/// <summary>
/// Service technique du catalogue, tel que Billing V2 le decrit.
/// </summary>
/// <remarks>
/// La reference technique est <c>billing_v2_services.code</c>. Aucune colonne
/// dediee n'a ete ajoutee : l'identite commerciale du service et son identite
/// technique d'entitlement sont la meme chose dans le modele V2, et
/// <see cref="BillingV2ClientServiceEntitlementPolicy"/> retombait deja sur
/// <c>service_code</c>. Introduire un doublon aurait cree deux verites a
/// synchroniser sans qu'aucune semantique ne les distingue.
/// </remarks>
public sealed record CatalogTechnicalServiceDefinition(
    string TechnicalServiceReference,
    string Label,
    IReadOnlyList<string> GroupSamAccountNames,
    string? Category = null,
    string? Description = null);

/// <summary>
/// Topologie technique du catalogue : quels services existent, comment ils
/// s'appellent, et quels groupes Active Directory ils pilotent.
///
/// Source unique : Billing V2. Les groupes viennent de
/// <c>billing_v2_provisioning_rules</c> filtrees sur
/// <c>target_type = 'ad_group'</c> — la table qui decrit reellement ce que le
/// provisioning sait faire. Aucune offre commerciale n'intervient : une offre
/// ne portait ces groupes que par recopie, et cette recopie pouvait diverger
/// des regles effectivement appliquees.
///
/// Lecture seule stricte, comme l'exige le compte applicatif.
/// </summary>
public interface IServiceTopologyService
{
    Task<IReadOnlyList<string>> ResolveServiceMappedGroupsAsync(
        string technicalServiceReference,
        CancellationToken cancellationToken);

    Task<string> ResolveServiceLabelAsync(
        string technicalServiceReference,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogTechnicalServiceDefinition>>
        GetTechnicalServicesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetManagedGroupSamAccountNamesAsync(
        CancellationToken cancellationToken);
}

public sealed class BillingV2ServiceTopologyService : IServiceTopologyService
{
    private const string AdGroupTargetType = "ad_group";

    private readonly SqlRuntimeConfiguration _sql;
    private readonly ILogger<BillingV2ServiceTopologyService> _logger;
    private Task<TopologySnapshot>? _snapshotTask;

    public BillingV2ServiceTopologyService(
        SqlRuntimeConfiguration sql,
        ILogger<BillingV2ServiceTopologyService> logger)
    {
        _sql = sql;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ResolveServiceMappedGroupsAsync(
        string technicalServiceReference,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        var reference = Normalize(technicalServiceReference);
        return reference.Length > 0
            && snapshot.ServicesByReference.TryGetValue(reference, out var service)
                ? service.GroupSamAccountNames
                : Array.Empty<string>();
    }

    public async Task<string> ResolveServiceLabelAsync(
        string technicalServiceReference,
        CancellationToken cancellationToken)
    {
        var reference = Normalize(technicalServiceReference);
        if (reference.Length == 0)
        {
            return "Service";
        }

        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.ServicesByReference.TryGetValue(reference, out var service)
            ? service.Label
            : CreateFallbackLabel(reference);
    }

    public async Task<IReadOnlyList<CatalogTechnicalServiceDefinition>>
        GetTechnicalServicesAsync(CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.TechnicalServices;
    }

    public async Task<IReadOnlyList<string>> GetManagedGroupSamAccountNamesAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.ManagedGroupSamAccountNames;
    }

    private Task<TopologySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken)
        => _snapshotTask ??= LoadSnapshotAsync(cancellationToken);

    private async Task<TopologySnapshot> LoadSnapshotAsync(
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return TopologySnapshot.Empty;
        }

        try
        {
            await using var connection = new MySqlConnection(_sql.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            // LEFT JOIN volontaire : un service sans regle de groupe reste un
            // service du catalogue. Le taire ferait disparaitre de la fiche
            // client tout ce qui ne se materialise pas par une appartenance AD
            // (stockage, sauvegarde, socle...).
            command.CommandText =
                """
                SELECT
                    service.code AS service_code,
                    service.name AS service_name,
                    service.category AS service_category,
                    service.description AS service_description,
                    rule.target_reference
                FROM billing_v2_services service
                LEFT JOIN billing_v2_provisioning_rules rule
                    ON rule.service_id = service.id
                   AND rule.status = 'active'
                   AND rule.target_type = @target_type
                   AND rule.target_reference IS NOT NULL
                WHERE service.status = 'active'
                ORDER BY service.display_order, service.code;
                """;
            command.Parameters.AddWithValue("@target_type", AdGroupTargetType);

            var groupsByService =
                new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
            var metadata = new Dictionary<string, ServiceMetadata>(
                StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = Normalize(reader.GetString("service_code"));
                if (code.Length == 0)
                {
                    continue;
                }

                metadata[code] = new ServiceMetadata(
                    reader.GetString("service_name"),
                    ReadNullable(reader, "service_category"),
                    ReadNullable(reader, "service_description"));
                if (!groupsByService.TryGetValue(code, out var groups))
                {
                    groups = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    groupsByService[code] = groups;
                }

                if (!reader.IsDBNull(reader.GetOrdinal("target_reference")))
                {
                    var group = Normalize(reader.GetString("target_reference"));
                    if (group.Length > 0)
                    {
                        groups.Add(group);
                    }
                }
            }

            var technicalServices = groupsByService
                .Select(entry =>
                {
                    metadata.TryGetValue(entry.Key, out var details);
                    return new CatalogTechnicalServiceDefinition(
                        entry.Key,
                        details?.Name ?? CreateFallbackLabel(entry.Key),
                        entry.Value.ToArray(),
                        details?.Category,
                        details?.Description);
                })
                .OrderBy(service => service.Label, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new TopologySnapshot(
                technicalServices.ToDictionary(
                    service => service.TechnicalServiceReference,
                    service => service,
                    StringComparer.OrdinalIgnoreCase),
                technicalServices,
                technicalServices
                    .SelectMany(service => service.GroupSamAccountNames)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
        catch (MySqlException exception)
        {
            // Une topologie indisponible ne doit pas faire tomber le portail :
            // elle degrade en « aucun groupe connu », ce qui bloque toute
            // action AD au lieu d'en inventer une.
            _logger.LogWarning(
                exception,
                "Topologie technique Billing V2 indisponible : aucun groupe resolu.");
            return TopologySnapshot.Empty;
        }
    }

    private static string? ReadNullable(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column))
            ? null
            : reader.GetString(column);

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string CreateFallbackLabel(string technicalServiceReference)
    {
        var tokens = technicalServiceReference
            .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.ToLowerInvariant())
            .Select(token => token.Length switch
            {
                0 => token,
                1 => token.ToUpperInvariant(),
                _ => char.ToUpperInvariant(token[0]) + token[1..]
            });
        return string.Join(" ", tokens);
    }

    private sealed record ServiceMetadata(
        string Name,
        string? Category,
        string? Description);

    private sealed record TopologySnapshot(
        IReadOnlyDictionary<string, CatalogTechnicalServiceDefinition> ServicesByReference,
        IReadOnlyList<CatalogTechnicalServiceDefinition> TechnicalServices,
        IReadOnlyList<string> ManagedGroupSamAccountNames)
    {
        public static TopologySnapshot Empty { get; } = new(
            new Dictionary<string, CatalogTechnicalServiceDefinition>(
                StringComparer.OrdinalIgnoreCase),
            Array.Empty<CatalogTechnicalServiceDefinition>(),
            Array.Empty<string>());
    }
}
