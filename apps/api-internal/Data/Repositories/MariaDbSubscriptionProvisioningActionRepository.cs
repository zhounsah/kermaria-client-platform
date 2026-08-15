using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbSubscriptionProvisioningActionRepository
    : ISubscriptionProvisioningActionRepository
{
    private readonly string _connectionString;

    public MariaDbSubscriptionProvisioningActionRepository(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<SubscriptionProvisioningActionCreateResult> CreateRequestedAsync(
        SubscriptionProvisioningActionCreateRequest request,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("D");
        await using var connection = await OpenAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKeyHash))
        {
            var existingActiveId = await FindActiveByHashAsync(
                connection,
                request.IdempotencyKeyHash,
                cancellationToken);
            if (existingActiveId is not null)
            {
                return new SubscriptionProvisioningActionCreateResult(
                    existingActiveId,
                    Created: false);
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ad_actions (
                id,
                customer_id,
                subscription_id,
                requested_by_user_id,
                action_type,
                target_reference,
                requested_at,
                status,
                result_code,
                correlation_id,
                idempotency_key_hash,
                idempotency_active_hash,
                changed,
                details_json
            ) VALUES (
                @id,
                @customer_id,
                @subscription_id,
                @requested_by_user_id,
                @action_type,
                @target_reference,
                UTC_TIMESTAMP(6),
                'requested',
                NULL,
                @correlation_id,
                @idempotency_key_hash,
                @idempotency_active_hash,
                NULL,
                @details_json
            );
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue(
            "customer_id",
            DbValue(request.CustomerId));
        command.Parameters.AddWithValue("subscription_id", request.SubscriptionId);
        command.Parameters.AddWithValue(
            "requested_by_user_id",
            DbValue(request.RequestedByUserId));
        command.Parameters.AddWithValue("action_type", request.ActionType);
        command.Parameters.AddWithValue(
            "target_reference",
            request.TargetReference);
        command.Parameters.AddWithValue(
            "correlation_id",
            request.CorrelationId);
        command.Parameters.AddWithValue(
            "idempotency_key_hash",
            DbValue(request.IdempotencyKeyHash));
        command.Parameters.AddWithValue(
            "idempotency_active_hash",
            DbValue(request.IdempotencyKeyHash));
        command.Parameters.AddWithValue(
            "details_json",
            DbValue(request.DetailsJson));
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new SubscriptionProvisioningActionCreateResult(
                id,
                Created: true);
        }
        catch (MySqlException exception)
            when (exception.Number == 1062
                && !string.IsNullOrWhiteSpace(request.IdempotencyKeyHash))
        {
            var existingActiveId = await FindActiveByHashAsync(
                connection,
                request.IdempotencyKeyHash,
                cancellationToken);
            if (existingActiveId is not null)
            {
                return new SubscriptionProvisioningActionCreateResult(
                    existingActiveId,
                    Created: false);
            }

            throw;
        }
    }

    public async Task MarkStartedAsync(
        string actionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ad_actions
            SET started_at = COALESCE(started_at, UTC_TIMESTAMP(6)),
                status = 'running'
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("id", actionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(
        string actionId,
        string status,
        string? resultCode,
        bool changed,
        string? detailsJson,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ad_actions
            SET started_at = COALESCE(started_at, UTC_TIMESTAMP(6)),
                completed_at = UTC_TIMESTAMP(6),
                status = @status,
                result_code = @result_code,
                changed = @changed,
                idempotency_active_hash = NULL,
                details_json = @details_json
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("id", actionId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue(
            "result_code",
            DbValue(resultCode));
        command.Parameters.AddWithValue("changed", changed);
        command.Parameters.AddWithValue(
            "details_json",
            DbValue(detailsJson));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionProvisioningActionSummary>>
        GetRecentBySubscriptionAsync(
            string subscriptionId,
            int limit,
            CancellationToken cancellationToken)
    {
        var actions = new List<SubscriptionProvisioningActionSummary>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                action_type,
                status,
                result_code,
                changed,
                correlation_id,
                target_reference,
                requested_at,
                started_at,
                completed_at
            FROM ad_actions
            WHERE subscription_id = @subscription_id
            ORDER BY requested_at DESC, id DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("subscription_id", subscriptionId);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            actions.Add(new SubscriptionProvisioningActionSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("action_type"),
                reader.GetString("status"),
                reader.IsDBNull(reader.GetOrdinal("result_code"))
                    ? null
                    : reader.GetString("result_code"),
                !reader.IsDBNull(reader.GetOrdinal("changed"))
                && reader.GetBoolean("changed"),
                reader.GetString("correlation_id"),
                reader.GetString("target_reference"),
                ToIso(reader.GetDateTime("requested_at")),
                ReadNullableIso(reader, "started_at"),
                ReadNullableIso(reader, "completed_at")));
        }

        return actions;
    }

    private static async Task<string?> FindActiveByHashAsync(
        MySqlConnection connection,
        string idempotencyKeyHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id
            FROM ad_actions
            WHERE idempotency_active_hash = @idempotency_active_hash
              AND status IN ('requested', 'running')
            ORDER BY requested_at ASC, id ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(
            "idempotency_active_hash",
            idempotencyKeyHash);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value is DBNull
            ? null
            : Convert.ToString(value);
    }

    private async Task<MySqlConnection> OpenAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;

    private static string? ReadNullableIso(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column))
            ? null
            : ToIso(reader.GetDateTime(column));

    private static string ToIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}
