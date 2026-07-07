using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbActiveDirectoryLinkRepository
    : IActiveDirectoryLinkRepository
{
    private readonly string _connectionString;

    public MariaDbActiveDirectoryLinkRepository(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<AdCustomerContext?> GetCustomerContextAsync(
        string customerReference,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, external_reference, display_name
            FROM customers
            WHERE external_reference = @customer_reference
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_reference", customerReference);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AdCustomerContext(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("external_reference"),
            reader.GetString("display_name"));
    }

    public async Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerLinksAsync(
        string customerReference,
        CancellationToken cancellationToken)
    {
        var links = new List<CustomerAdLinkSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                link.id,
                customer.external_reference AS customer_reference,
                link.object_guid,
                link.object_sid,
                link.object_type,
                link.sam_account_name,
                link.user_principal_name,
                link.display_name,
                link.distinguished_name,
                link.linked_at,
                actor.display_name AS linked_by
            FROM customer_ad_links link
            INNER JOIN customers customer
                ON customer.id = link.customer_id
            LEFT JOIN portal_users actor
                ON actor.id = link.linked_by_user_id
            WHERE customer.external_reference = @customer_reference
            ORDER BY link.linked_at DESC, link.id DESC;
            """;
        command.Parameters.AddWithValue("@customer_reference", customerReference);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            links.Add(new CustomerAdLinkSummary(
                ReadRequiredIdentifier(reader, "id"),
                reader.GetString("customer_reference"),
                ReadRequiredIdentifier(reader, "object_guid"),
                reader.GetString("object_sid"),
                reader.GetString("object_type"),
                reader.GetString("sam_account_name"),
                ReadNullableString(reader, "user_principal_name"),
                reader.GetString("display_name"),
                reader.GetString("distinguished_name"),
                ToUtcIso(reader.GetDateTime("linked_at")),
                ReadNullableString(reader, "linked_by")));
        }

        return links;
    }

    public async Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerUserLinksAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var links = new List<CustomerAdLinkSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                link.id,
                customer.external_reference AS customer_reference,
                link.object_guid,
                link.object_sid,
                link.object_type,
                link.sam_account_name,
                link.user_principal_name,
                link.display_name,
                link.distinguished_name,
                link.linked_at,
                actor.display_name AS linked_by
            FROM customer_ad_links link
            INNER JOIN customers customer
                ON customer.id = link.customer_id
            LEFT JOIN portal_users actor
                ON actor.id = link.linked_by_user_id
            WHERE link.customer_id = @customer_id
              AND link.object_type = 'user'
            ORDER BY link.linked_at DESC, link.id DESC;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            links.Add(new CustomerAdLinkSummary(
                ReadRequiredIdentifier(reader, "id"),
                reader.GetString("customer_reference"),
                ReadRequiredIdentifier(reader, "object_guid"),
                reader.GetString("object_sid"),
                reader.GetString("object_type"),
                reader.GetString("sam_account_name"),
                ReadNullableString(reader, "user_principal_name"),
                reader.GetString("display_name"),
                reader.GetString("distinguished_name"),
                ToUtcIso(reader.GetDateTime("linked_at")),
                ReadNullableString(reader, "linked_by")));
        }

        return links;
    }

    public async Task<CustomerAdLinkUpsertResult> UpsertCustomerLinkAsync(
        string customerReference,
        string? actorUserId,
        AdDirectoryObjectSummary directoryObject,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        var customerContext = await GetCustomerContextAsync(
            connection,
            transaction,
            customerReference,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Customer context is unavailable for Active Directory link persistence.");

        await using (var existingCommand = connection.CreateCommand())
        {
            existingCommand.Transaction = transaction;
            existingCommand.CommandText =
                """
                SELECT id, customer_id
                FROM customer_ad_links
                WHERE object_guid = @object_guid
                LIMIT 1;
                """;
            existingCommand.Parameters.AddWithValue(
                "@object_guid",
                directoryObject.ObjectGuid);
            await using var existingReader = await existingCommand.ExecuteReaderAsync(
                cancellationToken);
            if (await existingReader.ReadAsync(cancellationToken))
            {
                var existingCustomerId = MariaDbIdentifierReader.ReadRequired(
                    existingReader,
                    "customer_id");
                var existingId = MariaDbIdentifierReader.ReadRequired(
                    existingReader,
                    "id");
                if (!existingCustomerId.Equals(
                        customerContext.CustomerId,
                        StringComparison.Ordinal))
                {
                    throw new PortalAccessDeniedException();
                }

                await transaction.CommitAsync(cancellationToken);
                return new CustomerAdLinkUpsertResult(existingId, false);
            }
        }

        var id = Guid.NewGuid().ToString("D");
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO customer_ad_links (
                    id,
                    customer_id,
                    object_guid,
                    object_sid,
                    object_type,
                    sam_account_name,
                    user_principal_name,
                    display_name,
                    distinguished_name,
                    linked_at,
                    linked_by_user_id
                ) VALUES (
                    @id,
                    @customer_id,
                    @object_guid,
                    @object_sid,
                    @object_type,
                    @sam_account_name,
                    @user_principal_name,
                    @display_name,
                    @distinguished_name,
                    @linked_at,
                    @linked_by_user_id
                );
                """;
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue(
                "@customer_id",
                customerContext.CustomerId);
            command.Parameters.AddWithValue(
                "@object_guid",
                directoryObject.ObjectGuid);
            command.Parameters.AddWithValue(
                "@object_sid",
                directoryObject.ObjectSid);
            command.Parameters.AddWithValue(
                "@object_type",
                directoryObject.ObjectType);
            command.Parameters.AddWithValue(
                "@sam_account_name",
                directoryObject.SamAccountName);
            command.Parameters.AddWithValue(
                "@user_principal_name",
                DbValue(directoryObject.UserPrincipalName));
            command.Parameters.AddWithValue(
                "@display_name",
                directoryObject.DisplayName);
            command.Parameters.AddWithValue(
                "@distinguished_name",
                directoryObject.DistinguishedName);
            command.Parameters.AddWithValue("@linked_at", DateTime.UtcNow);
            command.Parameters.AddWithValue(
                "@linked_by_user_id",
                DbValue(actorUserId));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new CustomerAdLinkUpsertResult(id, true);
    }

    public async Task<bool> RefreshCustomerLinkAsync(
        string targetCustomerReference,
        AdDirectoryObjectSummary directoryObject,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        var targetCustomer = await GetCustomerContextAsync(
            connection,
            transaction,
            targetCustomerReference,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Target customer is unavailable for Active Directory link refresh.");

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE customer_ad_links
            SET customer_id = @customer_id,
                object_sid = @object_sid,
                object_type = @object_type,
                sam_account_name = @sam_account_name,
                user_principal_name = @user_principal_name,
                display_name = @display_name,
                distinguished_name = @distinguished_name
            WHERE object_guid = @object_guid;
            """;
        command.Parameters.AddWithValue(
            "@customer_id",
            targetCustomer.CustomerId);
        command.Parameters.AddWithValue(
            "@object_sid",
            directoryObject.ObjectSid);
        command.Parameters.AddWithValue(
            "@object_type",
            directoryObject.ObjectType);
        command.Parameters.AddWithValue(
            "@sam_account_name",
            directoryObject.SamAccountName);
        command.Parameters.AddWithValue(
            "@user_principal_name",
            DbValue(directoryObject.UserPrincipalName));
        command.Parameters.AddWithValue(
            "@display_name",
            directoryObject.DisplayName);
        command.Parameters.AddWithValue(
            "@distinguished_name",
            directoryObject.DistinguishedName);
        command.Parameters.AddWithValue(
            "@object_guid",
            directoryObject.ObjectGuid);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<CustomerAdLinkSummary?> FindUserLinkByEmailAsync(
        string customerReference,
        string email,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                link.id,
                customer.external_reference AS customer_reference,
                link.object_guid,
                link.object_sid,
                link.object_type,
                link.sam_account_name,
                link.user_principal_name,
                link.display_name,
                link.distinguished_name,
                link.linked_at,
                actor.display_name AS linked_by
            FROM customer_ad_links link
            INNER JOIN customers customer
                ON customer.id = link.customer_id
            LEFT JOIN portal_users actor
                ON actor.id = link.linked_by_user_id
            WHERE customer.external_reference = @customer_reference
              AND link.object_type = 'user'
              AND LOWER(link.user_principal_name) = LOWER(@email)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_reference", customerReference);
        command.Parameters.AddWithValue("@email", email);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CustomerAdLinkSummary(
            ReadRequiredIdentifier(reader, "id"),
            reader.GetString("customer_reference"),
            ReadRequiredIdentifier(reader, "object_guid"),
            reader.GetString("object_sid"),
            reader.GetString("object_type"),
            reader.GetString("sam_account_name"),
            ReadNullableString(reader, "user_principal_name"),
            reader.GetString("display_name"),
            reader.GetString("distinguished_name"),
            ToUtcIso(reader.GetDateTime("linked_at")),
            ReadNullableString(reader, "linked_by"));
    }

    public async Task<bool> DeleteCustomerLinkAsync(
        string customerReference,
        string linkId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE link
            FROM customer_ad_links link
            INNER JOIN customers customer
                ON customer.id = link.customer_id
            WHERE link.id = @id
              AND customer.external_reference = @customer_reference;
            """;
        command.Parameters.AddWithValue("@id", linkId);
        command.Parameters.AddWithValue("@customer_reference", customerReference);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<AdCustomerContext?> GetCustomerContextAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string customerReference,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, external_reference, display_name
            FROM customers
            WHERE external_reference = @customer_reference
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_reference", customerReference);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AdCustomerContext(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("external_reference"),
            reader.GetString("display_name"));
    }

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);

    private static string ReadRequiredIdentifier(
        MySqlDataReader reader,
        string columnName)
        => MariaDbIdentifierReader.ReadRequired(reader, columnName);

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}
