using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbBillingV2KoxoTargetingRepository
    : IBillingV2KoxoTargetingRepository
{
    private readonly string _connectionString;

    public MariaDbBillingV2KoxoTargetingRepository(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<BillingV2KoxoPortalUserRecord?> FindPortalUserAsync(
        string customerId,
        string portalUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Les deux identifiants sont exacts et parametres. Aucun LIKE, aucun
        // repli sur l'email ou le nom : le ciblage d'un quota ne tolere pas
        // qu'une correspondance approchee designe une autre personne.
        command.CommandText =
            """
            SELECT
                portal_user.id AS portal_user_id,
                portal_user.customer_id AS customer_id,
                portal_user.koxo_unique_identifier AS koxo_unique_identifier,
                customer.external_reference AS customer_reference,
                customer.is_demo AS is_demo,
                customer.koxo_group_reference AS koxo_group_reference
            FROM portal_users portal_user
            INNER JOIN customers customer
                ON customer.id = portal_user.customer_id
            WHERE portal_user.id = @portal_user_id
              AND portal_user.customer_id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@portal_user_id", portalUserId);
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2KoxoPortalUserRecord(
            MariaDbIdentifierReader.ReadRequired(reader, "portal_user_id"),
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            reader.GetString("customer_reference"),
            ReadNullableString(reader, "koxo_unique_identifier"),
            reader.GetBoolean("is_demo"),
            ReadNullableString(reader, "koxo_group_reference"));
    }

    public async Task<BillingV2KoxoCustomerRecord?> FindCustomerAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                customer.id AS customer_id,
                customer.external_reference AS customer_reference,
                customer.is_demo AS is_demo,
                customer.koxo_group_reference AS koxo_group_reference
            FROM customers customer
            WHERE customer.id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2KoxoCustomerRecord(
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            reader.GetString("customer_reference"),
            reader.GetBoolean("is_demo"),
            ReadNullableString(reader, "koxo_group_reference"));
    }

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);
}
