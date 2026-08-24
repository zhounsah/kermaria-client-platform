using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2DownloadAccessScope(
    IReadOnlySet<string> PresetCodes,
    IReadOnlySet<string> ServiceCodes,
    IReadOnlySet<string> ProvisioningGroups);

public interface IBillingV2DownloadAccessProjection
{
    Task<BillingV2DownloadAccessScope> GetClientAccessScopeAsync(
        string customerId,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2DownloadAccessProjection
    : IBillingV2DownloadAccessProjection
{
    public static NoOpBillingV2DownloadAccessProjection Instance { get; } =
        new();

    private NoOpBillingV2DownloadAccessProjection()
    {
    }

    public Task<BillingV2DownloadAccessScope> GetClientAccessScopeAsync(
        string customerId,
        CancellationToken cancellationToken)
        => Task.FromResult(BillingV2DownloadAccessScopePolicy.Create(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>()));
}

public sealed class BillingV2DownloadAccessProjection
    : IBillingV2DownloadAccessProjection
{
    // Le predicat de conservation partage, nomme une fois pour les deux
    // requetes de cette porte. Declare avant elles : les initialiseurs
    // statiques s'executent dans l'ordre textuel.
    private static readonly string AcquiredRightsSql =
        BillingV2EntitlementRetentionSql.SubscriptionGrantsAcquiredRights;

    private readonly string _connectionString;

    public BillingV2DownloadAccessProjection(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public async Task<BillingV2DownloadAccessScope> GetClientAccessScopeAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var presetCodes = new List<string>();
        var serviceCodes = new List<string>();
        var provisioningGroups = new List<string>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = CatalogTargetsSql;
            command.Parameters.AddWithValue("@customer_id", customerId);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                presetCodes.Add(ReadNullableString(reader, "preset_code"));
                serviceCodes.Add(ReadNullableString(reader, "service_code"));
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ProvisioningGroupsSql;
            command.Parameters.AddWithValue("@customer_id", customerId);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                provisioningGroups.Add(
                    ReadNullableString(reader, "target_reference"));
            }
        }

        return BillingV2DownloadAccessScopePolicy.Create(
            presetCodes,
            serviceCodes,
            provisioningGroups);
    }

    // Porte d'acces a des droits deja acquis : le predicat de conservation
    // partage decide, pas un `status = 'active'` ecrit en dur. Un abonnement
    // resilie a fin de terme reste `pending_cancellation` jusqu'au terme de la
    // periode encaissee ; lui couper les telechargements des le clic
    // reprendrait un service deja paye. La fenetre contractuelle reste
    // appliquee dans le meme predicat : un contrat comptant arrive a terme
    // reste `active` en base, faute de renouvellement automatique.
    //
    // Les deux axes de ciblage viennent desormais du catalogue V2 : le code de
    // formule (`billing_v2_offer_presets.code`) et le code de service
    // (`billing_v2_services.code`). Une souscription directe, sans formule,
    // n'expose donc que ses services — ce qui est exact : elle n'appartient a
    // aucune formule.
    private static readonly string CatalogTargetsSql =
        $"""
        SELECT DISTINCT
            preset.code AS preset_code,
            service.code AS service_code
        FROM billing_v2_subscriptions subscription
        INNER JOIN billing_v2_subscription_items item
            ON item.subscription_id = subscription.id
        INNER JOIN billing_v2_services service
            ON service.id = item.service_id
        LEFT JOIN billing_v2_offer_presets preset
            ON preset.id = subscription.originating_preset_id
        WHERE subscription.customer_id = @customer_id
          AND item.status = 'active'
          AND item.effective_from <= UTC_TIMESTAMP(6)
          AND (
                item.effective_until IS NULL
                OR item.effective_until > UTC_TIMESTAMP(6)
              )
          AND {AcquiredRightsSql};
        """;

    // Meme porte, meme predicat : ces groupes AD portent l'acces deja acquis a
    // un service en cours, pas l'autorisation d'en provisionner un nouveau.
    // C'est cette derniere qui reste reservee aux abonnements `active`, via
    // `AllowsNewMutations` et les gardes d'ecriture des depots.
    private static readonly string ProvisioningGroupsSql =
        $"""
        SELECT DISTINCT
            rule.target_reference
        FROM billing_v2_subscriptions subscription
        INNER JOIN billing_v2_subscription_items item
            ON item.subscription_id = subscription.id
        INNER JOIN billing_v2_services service
            ON service.id = item.service_id
        LEFT JOIN billing_v2_service_tiers tier
            ON tier.id = item.tier_id
        INNER JOIN billing_v2_provisioning_rules rule
            ON rule.service_id = service.id
           AND rule.status = 'active'
           AND rule.target_type = 'ad_group'
           AND (
                rule.tier_id IS NULL
                OR rule.tier_id = tier.id
           )
        WHERE subscription.customer_id = @customer_id
          AND item.status = 'active'
          AND item.effective_from <= UTC_TIMESTAMP(6)
          AND (
                item.effective_until IS NULL
                OR item.effective_until > UTC_TIMESTAMP(6)
              )
          AND {AcquiredRightsSql};
        """;

    private static string ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? string.Empty
            : reader.GetString(columnName);
}

public static class BillingV2DownloadAccessScopePolicy
{
    public static BillingV2DownloadAccessScope Create(
        IEnumerable<string?> presetCodes,
        IEnumerable<string?> serviceCodes,
        IEnumerable<string?> provisioningGroups)
        => new(
            Normalize(presetCodes, StringComparer.Ordinal),
            Normalize(serviceCodes, StringComparer.Ordinal),
            Normalize(provisioningGroups, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlySet<string> Normalize(
        IEnumerable<string?> values,
        IEqualityComparer<string> comparer)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Where(value => value.Length > 0)
            .ToHashSet(comparer);
}
