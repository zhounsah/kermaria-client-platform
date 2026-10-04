using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services.Provisioning;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IKoxoQualityIntentRepository
{
    Task<KoxoQualityIntent?> ReadAsync(string customerId, CancellationToken cancellationToken);
    Task<KoxoQualityIntentPublication> PublishAsync(string customerId, long expectedRevision,
        IReadOnlyDictionary<string, IReadOnlyList<string>> byIdentity, CancellationToken cancellationToken);
    Task<KoxoQualityLease?> ClaimNextAsync(CancellationToken cancellationToken);
    Task<bool> FinishAsync(KoxoQualityLease lease, bool verified, string resultCode, CancellationToken cancellationToken);
}

public sealed class MariaDbKoxoQualityIntentRepository(SqlRuntimeConfiguration configuration)
    : IKoxoQualityIntentRepository
{
    private async Task<MySqlConnection> OpenAsync(CancellationToken token)
    {
        var connection = new MySqlConnection(configuration.ConnectionString
            ?? throw new InvalidOperationException("SQL_CONFIGURATION_UNAVAILABLE"));
        try
        {
            await connection.OpenAsync(token);
            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='koxo_quality_intents';";
            if (Convert.ToInt32(await check.ExecuteScalarAsync(token)) != 1)
                throw new InvalidOperationException("KOXO_QUALITY_INTENT_SCHEMA_MISSING");
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task<KoxoQualityIntent?> ReadAsync(string customerId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await ReadAsync(connection, null, customerId, cancellationToken);
    }

    private static async Task<KoxoQualityIntent?> ReadAsync(MySqlConnection connection,
        MySqlTransaction? transaction, string customerId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT revision, applied_revision, desired_json FROM koxo_quality_intents WHERE customer_id=@customer;";
        command.Parameters.AddWithValue("@customer", customerId);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token)
            ? new(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2)) : null;
    }

    public async Task<KoxoQualityIntentPublication> PublishAsync(string customerId, long expectedRevision,
        IReadOnlyDictionary<string, IReadOnlyList<string>> byIdentity, CancellationToken cancellationToken)
    {
        var json = KoxoQualityIntentPolicy.Serialize(byIdentity);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // Le parent existe avant la premiere publication : son verrou couvre
        // aussi la concurrence INSERT/INSERT quand aucun intent n'existe encore.
        await using (var owner = connection.CreateCommand())
        {
            owner.Transaction = transaction;
            owner.CommandText = "SELECT id FROM customers WHERE id=@customer FOR UPDATE;";
            owner.Parameters.AddWithValue("@customer", customerId);
            if (await owner.ExecuteScalarAsync(cancellationToken) is null)
                throw new InvalidOperationException("KOXO_QUALITY_CUSTOMER_NOT_FOUND");
        }
        foreach (var identity in byIdentity.Keys)
        {
            await using var owner = connection.CreateCommand();
            owner.Transaction = transaction;
            owner.CommandText = "SELECT id FROM portal_users WHERE id=@identity AND customer_id=@customer FOR UPDATE;";
            owner.Parameters.AddWithValue("@identity", identity);
            owner.Parameters.AddWithValue("@customer", customerId);
            if (await owner.ExecuteScalarAsync(cancellationToken) is null)
                throw new InvalidOperationException("KOXO_QUALITY_IDENTITY_CUSTOMER_MISMATCH");
        }
        var current = await ReadAsync(connection, transaction, customerId, cancellationToken);
        var result = KoxoQualityIntentPolicy.Publish(current, expectedRevision, json);
        if (result.Accepted && result.Changed)
        {
            await using var write = connection.CreateCommand();
            write.Transaction = transaction;
            write.CommandText = """
                INSERT INTO koxo_quality_intents (customer_id, revision, applied_revision, desired_json)
                VALUES (@customer, @revision, 0, @json)
                ON DUPLICATE KEY UPDATE revision=@revision, desired_json=@json, updated_at=UTC_TIMESTAMP(6),
                    lease_token=NULL, lease_expires_at=NULL, attempt_count=0,
                    next_attempt_at=UTC_TIMESTAMP(6), last_result_code=NULL;
                """;
            write.Parameters.AddWithValue("@customer", customerId);
            write.Parameters.AddWithValue("@revision", result.Intent!.Revision);
            write.Parameters.AddWithValue("@json", json);
            await write.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<KoxoQualityLease?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        KoxoQualityLease lease;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                SELECT customer_id, revision, applied_revision, desired_json, attempt_count
                FROM koxo_quality_intents
                WHERE applied_revision<revision AND next_attempt_at<=UTC_TIMESTAMP(6)
                  AND (lease_expires_at IS NULL OR lease_expires_at<=UTC_TIMESTAMP(6))
                ORDER BY next_attempt_at, customer_id LIMIT 1 FOR UPDATE SKIP LOCKED;
                """;
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            lease = new(MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
                new(reader.GetInt64(1), reader.GetInt64(2), reader.GetString(3)),
                Guid.NewGuid().ToString("D"), checked(reader.GetInt32(4) + 1));
        }
        await using var claim = connection.CreateCommand();
        claim.Transaction = transaction;
        claim.CommandText = """
            UPDATE koxo_quality_intents SET lease_token=@lease,
                lease_expires_at=DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 120 SECOND), attempt_count=@attempt
            WHERE customer_id=@customer AND revision=@revision;
            """;
        claim.Parameters.AddWithValue("@lease", lease.Token);
        claim.Parameters.AddWithValue("@attempt", lease.Attempt);
        claim.Parameters.AddWithValue("@customer", lease.CustomerId);
        claim.Parameters.AddWithValue("@revision", lease.Intent.Revision);
        await claim.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return lease;
    }

    public async Task<bool> FinishAsync(KoxoQualityLease lease, bool verified, string resultCode, CancellationToken cancellationToken)
    {
        if (resultCode is not ("KOXO_QUALITIES_APPLIED" or "KOXO_QUALITIES_UNVERIFIED" or "KOXO_QUALITIES_EXECUTION_FAILED" or "KOXO_QUALITIES_SUPERSEDED")
            || verified != (resultCode == "KOXO_QUALITIES_APPLIED"))
            throw new ArgumentException("KOXO_QUALITY_RESULT_INVALID");
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE koxo_quality_intents
            SET applied_revision=IF(@verified, @revision, applied_revision),
                applied_at=IF(@verified, UTC_TIMESTAMP(6), applied_at),
                lease_token=NULL, lease_expires_at=NULL, last_result_code=@code,
                next_attempt_at=DATE_ADD(UTC_TIMESTAMP(6), INTERVAL @delay SECOND)
            WHERE customer_id=@customer AND revision=@revision AND applied_revision<@revision
              AND lease_token=@lease AND lease_expires_at>UTC_TIMESTAMP(6);
            """;
        command.Parameters.AddWithValue("@customer", lease.CustomerId);
        command.Parameters.AddWithValue("@revision", lease.Intent.Revision);
        command.Parameters.AddWithValue("@lease", lease.Token);
        command.Parameters.AddWithValue("@verified", verified);
        command.Parameters.AddWithValue("@code", resultCode);
        command.Parameters.AddWithValue("@delay", Math.Min(300, 5 * (1 << Math.Clamp(lease.Attempt - 1, 0, 6))));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
