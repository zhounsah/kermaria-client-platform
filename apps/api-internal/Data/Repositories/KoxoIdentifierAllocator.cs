using System.Globalization;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Alloue les identifiants uniques KoXo <c>CLI-NNNNNN</c> depuis le compteur
/// partage <c>koxo_identifier_counters</c>.
/// </summary>
/// <remarks>
/// Deux chemins consomment ce compteur — l'inscription et la creation d'un
/// compte de demonstration — et il est etat partage : deux implementations qui
/// divergeraient produiraient des identifiants en collision, or la colonne
/// <c>portal_users.koxo_unique_identifier</c> est sous index unique.
///
/// L'appel doit se faire DANS une transaction : le <c>FOR UPDATE</c> serialise
/// les allocations concurrentes.
/// </remarks>
internal static class KoxoIdentifierAllocator
{
    private const string CounterName = "portal_user";

    public static async Task<string> AllocateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText =
            """
            SELECT next_value
            FROM koxo_identifier_counters
            WHERE counter_name = 'portal_user'
            FOR UPDATE;
            """;
        var rawCurrentValue = await selectCommand.ExecuteScalarAsync(cancellationToken);
        var currentValue = rawCurrentValue is null || rawCurrentValue == DBNull.Value
            ? 1L
            : Convert.ToInt64(rawCurrentValue, CultureInfo.InvariantCulture);

        await using var upsertCommand = connection.CreateCommand();
        upsertCommand.Transaction = transaction;
        upsertCommand.CommandText =
            """
            INSERT INTO koxo_identifier_counters (
                counter_name,
                next_value
            ) VALUES (
                @counter_name,
                @next_value
            )
            ON DUPLICATE KEY UPDATE
                next_value = @next_value;
            """;
        upsertCommand.Parameters.AddWithValue("@counter_name", CounterName);
        upsertCommand.Parameters.AddWithValue("@next_value", currentValue + 1);
        await upsertCommand.ExecuteNonQueryAsync(cancellationToken);

        return $"CLI-{currentValue.ToString("D6", CultureInfo.InvariantCulture)}";
    }
}
