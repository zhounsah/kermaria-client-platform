using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Precondition de lancement : le modele commercial legacy a bien disparu du
/// schema.
/// </summary>
/// <remarks>
/// <para>
/// Cette porte comptait auparavant les abonnements reels portes par la table
/// <c>subscriptions</c> : tant qu'il en restait un, Billing V2 ne pouvait pas
/// devenir l'autorite commerciale sans creer deux verites concurrentes. Le
/// sujet de cette porte a change, pas son role : la table legacy n'existe plus
/// et la question devient « la migration destructive a-t-elle ete appliquee sur
/// cette base ? ».
/// </para>
/// <para>
/// La verification est strictement en lecture seule sur
/// <c>information_schema.tables</c>. Le compte applicatif n'a aucun droit de
/// schema : interroger la table elle-meme leverait une <c>MySqlException</c> et
/// masquerait la vraie cause derriere un <c>SQL_UNAVAILABLE</c>.
/// </para>
/// <para>
/// L'echec est ferme : une base ou l'on ne peut pas conclure n'est pas declaree
/// prete.
/// </para>
/// </remarks>
public sealed record BillingV2LaunchReadinessSnapshot(
    bool LegacyBillingSchemaRemoved,
    bool VerifiedAgainstPersistentSql)
{
    /// <summary>
    /// Tables legacy encore presentes. Vide quand la migration destructive a
    /// ete appliquee.
    /// </summary>
    public IReadOnlyList<string> RemainingLegacyTables { get; init; } =
        Array.Empty<string>();
}

public interface IBillingV2LaunchReadinessService
{
    Task<BillingV2LaunchReadinessSnapshot> CheckAsync(
        CancellationToken cancellationToken);
}

public sealed class BillingV2LaunchReadinessService
    : IBillingV2LaunchReadinessService
{
    // Les quatre tables qui portaient le modele commercial concurrent. Les
    // tables de liaison suivent leur sort et ne sont pas listees deux fois.
    private static readonly string[] LegacyTables =
    [
        "commercial_offers",
        "subscriptions",
        "cart_items",
        "recurring_checkout_items"
    ];

    private readonly SqlRuntimeConfiguration _sql;

    public BillingV2LaunchReadinessService(SqlRuntimeConfiguration sql)
    {
        _sql = sql;
    }

    public async Task<BillingV2LaunchReadinessSnapshot> CheckAsync(
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return new BillingV2LaunchReadinessSnapshot(
                LegacyBillingSchemaRemoved: false,
                VerifiedAgainstPersistentSql: false);
        }

        var remaining = new List<string>();
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN (
                    'commercial_offers',
                    'subscriptions',
                    'cart_items',
                    'recurring_checkout_items'
                  )
            ORDER BY table_name;
            """;

        await using (var reader = await command.ExecuteReaderAsync(
            cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                remaining.Add(reader.GetString(0));
            }
        }

        return new BillingV2LaunchReadinessSnapshot(
            LegacyBillingSchemaRemoved: remaining.Count == 0,
            VerifiedAgainstPersistentSql: true)
        {
            RemainingLegacyTables = remaining
        };
    }
}

public static class BillingV2LaunchReadinessGate
{
    public static BillingV2LaunchReadinessSnapshot Evaluate(
        IReadOnlyList<string> remainingLegacyTables)
        => new(
            LegacyBillingSchemaRemoved: remainingLegacyTables.Count == 0,
            VerifiedAgainstPersistentSql: true)
        {
            RemainingLegacyTables = remainingLegacyTables
        };

    public static IReadOnlyList<string> KnownLegacyTables => LegacyTableNames;

    private static readonly string[] LegacyTableNames =
    [
        "commercial_offers",
        "subscriptions",
        "cart_items",
        "recurring_checkout_items"
    ];
}
