using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services.Provisioning;

/// <summary>
/// Groupes Active Directory attendus, abonnement Billing V2 par abonnement
/// Billing V2.
/// </summary>
/// <remarks>
/// <para>
/// Un abonnement porte des items ; chaque item designe un service et
/// eventuellement un palier ; les regles de provisioning
/// (<c>billing_v2_provisioning_rules</c>) disent quels groupes ce couple
/// ouvre. C'est la seule source : une offre commerciale ne portait ces groupes
/// que par recopie.
/// </para>
/// <para>
/// Une regle sans palier (<c>tier_id IS NULL</c>) vaut pour tous les paliers du
/// service ; une regle avec palier ne vaut que pour celui-ci. Confondre les
/// deux accorderait a un palier d'entree les droits d'un palier superieur.
/// </para>
/// </remarks>
public interface IBillingV2SubscriptionAdGroupProjection
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>>
        GetGroupsBySubscriptionAsync(
            string customerId,
            CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2SubscriptionAdGroupProjection
    : IBillingV2SubscriptionAdGroupProjection
{
    public static NoOpBillingV2SubscriptionAdGroupProjection Instance { get; }
        = new();

    private NoOpBillingV2SubscriptionAdGroupProjection()
    {
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>>
        GetGroupsBySubscriptionAsync(
            string customerId,
            CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
}

public sealed class BillingV2SubscriptionAdGroupProjection
    : IBillingV2SubscriptionAdGroupProjection
{
    private readonly string _connectionString;

    public BillingV2SubscriptionAdGroupProjection(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>>
        GetGroupsBySubscriptionAsync(
            string customerId,
            CancellationToken cancellationToken)
    {
        var accumulator = new Dictionary<string, SortedSet<string>>(
            StringComparer.Ordinal);
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var subscriptionId = MariaDbIdentifierReader.ReadRequired(
                reader,
                "subscription_id");
            if (!accumulator.TryGetValue(subscriptionId, out var groups))
            {
                groups = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                accumulator[subscriptionId] = groups;
            }

            var ordinal = reader.GetOrdinal("target_reference");
            if (reader.IsDBNull(ordinal))
            {
                continue;
            }

            var group = reader.GetString(ordinal).Trim();
            if (group.Length > 0)
            {
                groups.Add(group);
            }
        }

        return accumulator.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value.ToArray(),
            StringComparer.Ordinal);
    }

    // LEFT JOIN sur les regles : un abonnement dont aucun service n'ouvre de
    // groupe doit apparaitre avec une liste vide, pas disparaitre. Le workbench
    // administrateur doit pouvoir dire « rien a provisionner » plutot que
    // « abonnement inconnu ».
    private const string SelectSql =
        """
        SELECT DISTINCT
            subscription.id AS subscription_id,
            rule.target_reference
        FROM billing_v2_subscriptions subscription
        INNER JOIN billing_v2_subscription_items item
            ON item.subscription_id = subscription.id
        INNER JOIN billing_v2_services service
            ON service.id = item.service_id
        LEFT JOIN billing_v2_provisioning_rules rule
            ON rule.service_id = service.id
           AND rule.status = 'active'
           AND rule.target_type = 'ad_group'
           AND (rule.tier_id IS NULL OR rule.tier_id = item.tier_id)
        WHERE subscription.customer_id = @customer_id
          AND item.status = 'active'
          AND item.effective_from <= UTC_TIMESTAMP(6)
          AND (
                item.effective_until IS NULL
                OR item.effective_until > UTC_TIMESTAMP(6)
              );
        """;
}
