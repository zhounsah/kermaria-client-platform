using System.Data;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbBackupRepository : IBackupRepository
{
    private const int HistoryLimit = 31;
    private readonly string _connectionString;
    private readonly IBackupProtectionService _protection;
    private readonly ILogger<MariaDbBackupRepository> _logger;

    public MariaDbBackupRepository(
        SqlRuntimeConfiguration configuration,
        IBackupProtectionService protection,
        ILogger<MariaDbBackupRepository> logger)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
        _protection = protection;
        _logger = logger;
    }

    public bool IsPersistent => true;

    public async Task<BackupIngestionResult> IngestReportAsync(
        BackupReportPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalized = _protection.NormalizeReport(payload);
        var provider = normalized.Provider!;
        var externalJobId = normalized.ExternalJobId!;
        var externalSessionId = normalized.ExternalSessionId!;
        var result = normalized.Result!;
        var startedAt = ToUtc(normalized.StartedAt!.Value);
        var finishedAt = ToNullableUtc(normalized.FinishedAt);
        var now = DateTime.UtcNow;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var mapping = await FindMappingAsync(
            connection,
            transaction,
            provider,
            externalJobId,
            cancellationToken);
        if (mapping is null || !mapping.Enabled)
        {
            await transaction.CommitAsync(cancellationToken);
            _logger.LogWarning(
                "Backup report ignored for provider {Provider}: mapping missing or disabled correlation_id {CorrelationId}",
                provider,
                correlationId);
            return new BackupIngestionResult(
                true,
                false,
                false,
                null,
                BackupProtectionStatuses.Unknown,
                correlationId);
        }

        var lastSuccessAt = result is "success" or "warning"
            ? finishedAt ?? startedAt
            : await GetCurrentLastSuccessAsync(
                connection,
                transaction,
                provider,
                externalJobId,
                cancellationToken);
        var protectionStatus = _protection.ComputeProtectionStatus(
            now,
            lastSuccessAt,
            result,
            now,
            mapping.ExpectedIntervalMinutes,
            mapping.CriticalAfterMinutes,
            mapping.StaleAfterMinutes);
        var backupJobId = await UpsertBackupJobAsync(
            connection,
            transaction,
            mapping,
            normalized,
            startedAt,
            finishedAt,
            result,
            lastSuccessAt,
            protectionStatus,
            now,
            cancellationToken);
        var runInserted = await InsertRunAsync(
            connection,
            transaction,
            backupJobId,
            externalSessionId,
            startedAt,
            finishedAt,
            result,
            normalized.ProtectedBytes,
            normalized.DurationSeconds,
            normalized.PublicMessage,
            now,
            cancellationToken);
        await UpdateIntegrationCollectionAsync(
            connection,
            transaction,
            mapping.Id,
            "success",
            runInserted ? "Nouvelle session enregistree." : "Session deja connue.",
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation(
            "Backup report accepted provider {Provider} mapped true run_inserted {RunInserted} protection_status {ProtectionStatus} correlation_id {CorrelationId}",
            provider,
            runInserted,
            protectionStatus,
            correlationId);

        return new BackupIngestionResult(
            true,
            true,
            runInserted,
            backupJobId,
            protectionStatus,
            correlationId);
    }

    public async Task<IReadOnlyList<BackupJobSummary>> GetClientBackupsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        var backups = new List<BackupJobSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                job.id,
                job.service_id,
                service.name AS service_name,
                job.provider,
                job.status,
                job.protection_status,
                job.last_run_at,
                job.last_success_at,
                job.last_result,
                job.protected_bytes,
                job.duration_seconds,
                job.retention_days,
                job.next_run_at,
                job.last_error_public,
                job.collected_at,
                job.last_verified_at,
                job.verification_status,
                integration.expected_interval_minutes,
                integration.critical_after_minutes,
                integration.stale_after_minutes
            FROM backup_jobs job
            INNER JOIN customer_services service
                ON service.id = job.service_id
                AND service.customer_id = job.customer_id
            INNER JOIN backup_integrations integration
                ON integration.provider = job.provider
                AND integration.external_job_id = job.external_job_id
            WHERE job.customer_id = @customer_id
              AND integration.enabled = TRUE
            ORDER BY service.name;
            """;
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            backups.Add(ReadBackupJob(reader));
        }

        return backups;
    }

    public async Task<BackupJobDetail?> GetClientBackupAsync(
        PortalSessionContext session,
        string backupJobId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var job = await GetClientBackupSummaryAsync(
            connection,
            session.CustomerId,
            backupJobId,
            cancellationToken);
        if (job is null)
        {
            return null;
        }

        var runs = new List<BackupRunSummary>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                started_at,
                finished_at,
                result,
                protected_bytes,
                duration_seconds,
                public_message
            FROM backup_runs
            WHERE backup_job_id = @backup_job_id
            ORDER BY started_at DESC
            LIMIT 31;
            """;
        command.Parameters.AddWithValue("@backup_job_id", backupJobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var result = reader.GetString("result");
            runs.Add(new BackupRunSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                ToUtcIso(reader.GetDateTime("started_at")),
                ReadNullableDateTime(reader, "finished_at") is { } finishedAt
                    ? ToUtcIso(finishedAt)
                    : null,
                result,
                _protection.PublicResultLabel(result),
                ReadNullableInt64(reader, "protected_bytes"),
                ReadNullableInt32(reader, "duration_seconds"),
                ReadNullableString(reader, "public_message")));
        }

        return new BackupJobDetail(job, runs);
    }

    public async Task<RequestMutationResponse> CreateRestoreRequestAsync(
        PortalSessionContext session,
        string backupJobId,
        BackupRestoreRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        var normalized = _protection.NormalizeRestoreRequest(payload);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var target = await ResolveClientBackupServiceAsync(
            connection,
            transaction,
            session.CustomerId,
            backupJobId,
            cancellationToken);
        if (target is null)
        {
            throw new PortalAccessDeniedException();
        }

        var id = Guid.NewGuid().ToString("D");
        var reference = CreateReference("RST");
        var now = DateTime.UtcNow;
        var description = BuildRestoreDescription(normalized, target.Value.ServiceName);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO support_requests (
                    id,
                    customer_id,
                    created_by_user_id,
                    service_id,
                    reference,
                    subject,
                    description,
                    priority,
                    category,
                    status,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @customer_id,
                    @created_by_user_id,
                    @service_id,
                    @reference,
                    @subject,
                    @description,
                    @priority,
                    'restore_request',
                    'open',
                    @created_at,
                    @updated_at
                );
                """;
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@customer_id", session.CustomerId);
            command.Parameters.AddWithValue("@created_by_user_id", session.UserId);
            command.Parameters.AddWithValue("@service_id", target.Value.ServiceId);
            command.Parameters.AddWithValue("@reference", reference);
            command.Parameters.AddWithValue(
                "@subject",
                $"Demande de restauration - {target.Value.ServiceName}");
            command.Parameters.AddWithValue("@description", description);
            command.Parameters.AddWithValue("@priority", normalized.Priority);
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertRequestCreatedEventAsync(
            connection,
            transaction,
            id,
            session.UserId,
            correlationId,
            now,
            cancellationToken);
        await InsertAuditAsync(
            connection,
            transaction,
            new AuditEvent(
                correlationId,
                "backup.restore_request.create",
                "success",
                TargetType: "support_request",
                TargetReference: reference,
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: sourceAddress),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RequestMutationResponse(
            id,
            reference,
            "open",
            true,
            correlationId);
    }

    public async Task<IReadOnlyList<BackupIntegrationSummary>>
        GetAdminIntegrationsAsync(CancellationToken cancellationToken)
    {
        var integrations = new List<BackupIntegrationSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                integration.id,
                integration.provider,
                integration.external_job_id,
                integration.customer_id,
                customer.external_reference AS customer_reference,
                customer.display_name AS customer_name,
                integration.service_id,
                service.name AS service_name,
                integration.enabled,
                integration.expected_interval_minutes,
                integration.critical_after_minutes,
                integration.stale_after_minutes,
                integration.last_collected_at,
                integration.last_collection_status,
                integration.last_collection_message,
                integration.created_at,
                integration.updated_at
            FROM backup_integrations integration
            INNER JOIN customers customer
                ON customer.id = integration.customer_id
            INNER JOIN customer_services service
                ON service.id = integration.service_id
                AND service.customer_id = integration.customer_id
            ORDER BY customer.external_reference, service.name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            integrations.Add(ReadIntegration(reader));
        }

        return integrations;
    }

    public async Task<BackupIntegrationSummary> UpsertAdminIntegrationAsync(
        BackupIntegrationPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalized = _protection.NormalizeIntegration(payload);
        var id = normalized.Id ?? Guid.NewGuid().ToString("D");
        var now = DateTime.UtcNow;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var serviceMatchesCustomer = await ServiceMatchesCustomerAsync(
            connection,
            transaction,
            normalized.CustomerId!,
            normalized.ServiceId!,
            cancellationToken);
        if (!serviceMatchesCustomer)
        {
            throw new PortalValidationException();
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO backup_integrations (
                    id,
                    provider,
                    external_job_id,
                    customer_id,
                    service_id,
                    enabled,
                    expected_interval_minutes,
                    critical_after_minutes,
                    stale_after_minutes,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @provider,
                    @external_job_id,
                    @customer_id,
                    @service_id,
                    @enabled,
                    @expected_interval_minutes,
                    @critical_after_minutes,
                    @stale_after_minutes,
                    @created_at,
                    @updated_at
                )
                ON DUPLICATE KEY UPDATE
                    customer_id = VALUES(customer_id),
                    service_id = VALUES(service_id),
                    enabled = VALUES(enabled),
                    expected_interval_minutes =
                        VALUES(expected_interval_minutes),
                    critical_after_minutes = VALUES(critical_after_minutes),
                    stale_after_minutes = VALUES(stale_after_minutes),
                    updated_at = VALUES(updated_at);
                """;
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@provider", normalized.Provider);
            command.Parameters.AddWithValue(
                "@external_job_id",
                normalized.ExternalJobId);
            command.Parameters.AddWithValue("@customer_id", normalized.CustomerId);
            command.Parameters.AddWithValue("@service_id", normalized.ServiceId);
            command.Parameters.AddWithValue("@enabled", normalized.Enabled);
            command.Parameters.AddWithValue(
                "@expected_interval_minutes",
                normalized.ExpectedIntervalMinutes);
            command.Parameters.AddWithValue(
                "@critical_after_minutes",
                normalized.CriticalAfterMinutes);
            command.Parameters.AddWithValue(
                "@stale_after_minutes",
                normalized.StaleAfterMinutes);
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            new AuditEvent(
                correlationId,
                "backup.integration.upsert",
                "success",
                TargetType: "backup_integration",
                TargetReference: normalized.Provider,
                CustomerId: normalized.CustomerId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var integrations = await GetAdminIntegrationsAsync(cancellationToken);
        return integrations.First(integration =>
            integration.Provider == normalized.Provider
            && integration.ExternalJobId == normalized.ExternalJobId);
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task<BackupMapping?> FindMappingAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string provider,
        string externalJobId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                id,
                customer_id,
                service_id,
                enabled,
                expected_interval_minutes,
                critical_after_minutes,
                stale_after_minutes
            FROM backup_integrations
            WHERE provider = @provider
              AND external_job_id = @external_job_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@external_job_id", externalJobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new BackupMapping(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
                MariaDbIdentifierReader.ReadRequired(reader, "service_id"),
                reader.GetBoolean("enabled"),
                reader.GetInt32("expected_interval_minutes"),
                reader.GetInt32("critical_after_minutes"),
                reader.GetInt32("stale_after_minutes"),
                provider,
                externalJobId)
            : null;
    }

    private async Task<DateTime?> GetCurrentLastSuccessAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string provider,
        string externalJobId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT last_success_at
            FROM backup_jobs
            WHERE provider = @provider AND external_job_id = @external_job_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@external_job_id", externalJobId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : ToUtc((DateTime)result);
    }

    private async Task<string> UpsertBackupJobAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BackupMapping mapping,
        BackupReportPayload payload,
        DateTime startedAt,
        DateTime? finishedAt,
        string result,
        DateTime? lastSuccessAt,
        string protectionStatus,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existingId = await GetBackupJobIdAsync(
            connection,
            transaction,
            mapping.Provider,
            mapping.ExternalJobId,
            cancellationToken);
        var id = existingId ?? Guid.NewGuid().ToString("D");
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO backup_jobs (
                id,
                customer_id,
                service_id,
                provider,
                external_job_id,
                status,
                protection_status,
                last_run_at,
                last_success_at,
                last_result,
                protected_bytes,
                duration_seconds,
                retention_days,
                next_run_at,
                last_error_public,
                collected_at,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @customer_id,
                @service_id,
                @provider,
                @external_job_id,
                'active',
                @protection_status,
                @last_run_at,
                @last_success_at,
                @last_result,
                @protected_bytes,
                @duration_seconds,
                @retention_days,
                @next_run_at,
                @last_error_public,
                @collected_at,
                @created_at,
                @updated_at
            )
            ON DUPLICATE KEY UPDATE
                customer_id = VALUES(customer_id),
                service_id = VALUES(service_id),
                status = 'active',
                protection_status = VALUES(protection_status),
                last_run_at = VALUES(last_run_at),
                last_success_at = COALESCE(
                    VALUES(last_success_at),
                    last_success_at
                ),
                last_result = VALUES(last_result),
                protected_bytes = VALUES(protected_bytes),
                duration_seconds = VALUES(duration_seconds),
                retention_days = VALUES(retention_days),
                next_run_at = VALUES(next_run_at),
                last_error_public = VALUES(last_error_public),
                collected_at = VALUES(collected_at),
                updated_at = VALUES(updated_at);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@customer_id", mapping.CustomerId);
        command.Parameters.AddWithValue("@service_id", mapping.ServiceId);
        command.Parameters.AddWithValue("@provider", mapping.Provider);
        command.Parameters.AddWithValue(
            "@external_job_id",
            mapping.ExternalJobId);
        command.Parameters.AddWithValue("@protection_status", protectionStatus);
        command.Parameters.AddWithValue("@last_run_at", finishedAt ?? startedAt);
        command.Parameters.AddWithValue(
            "@last_success_at",
            lastSuccessAt is null ? DBNull.Value : lastSuccessAt.Value);
        command.Parameters.AddWithValue("@last_result", result);
        command.Parameters.AddWithValue(
            "@protected_bytes",
            payload.ProtectedBytes is null ? DBNull.Value : payload.ProtectedBytes);
        command.Parameters.AddWithValue(
            "@duration_seconds",
            payload.DurationSeconds is null
                ? DBNull.Value
                : payload.DurationSeconds);
        command.Parameters.AddWithValue(
            "@retention_days",
            payload.RetentionDays is null ? DBNull.Value : payload.RetentionDays);
        command.Parameters.AddWithValue(
            "@next_run_at",
            payload.NextRunAt is null
                ? DBNull.Value
                : ToUtc(payload.NextRunAt.Value));
        command.Parameters.AddWithValue(
            "@last_error_public",
            result is "failed" or "warning"
                ? DbValue(payload.PublicMessage)
                : DBNull.Value);
        command.Parameters.AddWithValue("@collected_at", now);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    private static async Task<string?> GetBackupJobIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string provider,
        string externalJobId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id
            FROM backup_jobs
            WHERE provider = @provider AND external_job_id = @external_job_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@external_job_id", externalJobId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return MariaDbIdentifierReader.ConvertNullableValue(
            result,
            "backup_jobs.id");
    }

    private static async Task<bool> InsertRunAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string backupJobId,
        string externalSessionId,
        DateTime startedAt,
        DateTime? finishedAt,
        string result,
        long? protectedBytes,
        int? durationSeconds,
        string? publicMessage,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT IGNORE INTO backup_runs (
                id,
                backup_job_id,
                external_session_id,
                started_at,
                finished_at,
                result,
                protected_bytes,
                duration_seconds,
                public_message,
                created_at
            ) VALUES (
                @id,
                @backup_job_id,
                @external_session_id,
                @started_at,
                @finished_at,
                @result,
                @protected_bytes,
                @duration_seconds,
                @public_message,
                @created_at
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@backup_job_id", backupJobId);
        command.Parameters.AddWithValue(
            "@external_session_id",
            externalSessionId);
        command.Parameters.AddWithValue("@started_at", startedAt);
        command.Parameters.AddWithValue(
            "@finished_at",
            finishedAt is null ? DBNull.Value : finishedAt.Value);
        command.Parameters.AddWithValue("@result", result);
        command.Parameters.AddWithValue(
            "@protected_bytes",
            protectedBytes is null ? DBNull.Value : protectedBytes);
        command.Parameters.AddWithValue(
            "@duration_seconds",
            durationSeconds is null ? DBNull.Value : durationSeconds);
        command.Parameters.AddWithValue("@public_message", DbValue(publicMessage));
        command.Parameters.AddWithValue("@created_at", now);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static async Task UpdateIntegrationCollectionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string integrationId,
        string status,
        string message,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE backup_integrations
            SET last_collected_at = @last_collected_at,
                last_collection_status = @last_collection_status,
                last_collection_message = @last_collection_message,
                updated_at = @updated_at
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", integrationId);
        command.Parameters.AddWithValue("@last_collected_at", now);
        command.Parameters.AddWithValue("@last_collection_status", status);
        command.Parameters.AddWithValue("@last_collection_message", message);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<BackupJobSummary?> GetClientBackupSummaryAsync(
        MySqlConnection connection,
        string customerId,
        string backupJobId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                job.id,
                job.service_id,
                service.name AS service_name,
                job.provider,
                job.status,
                job.protection_status,
                job.last_run_at,
                job.last_success_at,
                job.last_result,
                job.protected_bytes,
                job.duration_seconds,
                job.retention_days,
                job.next_run_at,
                job.last_error_public,
                job.collected_at,
                job.last_verified_at,
                job.verification_status,
                integration.expected_interval_minutes,
                integration.critical_after_minutes,
                integration.stale_after_minutes
            FROM backup_jobs job
            INNER JOIN customer_services service
                ON service.id = job.service_id
                AND service.customer_id = job.customer_id
            INNER JOIN backup_integrations integration
                ON integration.provider = job.provider
                AND integration.external_job_id = job.external_job_id
            WHERE job.id = @id
              AND job.customer_id = @customer_id
              AND integration.enabled = TRUE
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", backupJobId);
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadBackupJob(reader)
            : null;
    }

    private BackupJobSummary ReadBackupJob(MySqlDataReader reader)
    {
        var lastSuccessAt = ReadNullableDateTime(reader, "last_success_at");
        var collectedAt = ReadNullableDateTime(reader, "collected_at");
        var lastResult = ReadNullableString(reader, "last_result");
        var protectionStatus = _protection.ComputeProtectionStatus(
            DateTime.UtcNow,
            lastSuccessAt,
            lastResult,
            collectedAt,
            reader.GetInt32("expected_interval_minutes"),
            reader.GetInt32("critical_after_minutes"),
            reader.GetInt32("stale_after_minutes"));

        return new BackupJobSummary(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "service_id"),
            reader.GetString("service_name"),
            reader.GetString("provider"),
            reader.GetString("status"),
            protectionStatus,
            _protection.PublicProtectionLabel(protectionStatus),
            ReadNullableDateTime(reader, "last_run_at") is { } lastRunAt
                ? ToUtcIso(lastRunAt)
                : null,
            lastSuccessAt is null ? null : ToUtcIso(lastSuccessAt.Value),
            lastResult,
            lastResult is null ? null : _protection.PublicResultLabel(lastResult),
            ReadNullableInt64(reader, "protected_bytes"),
            ReadNullableInt32(reader, "duration_seconds"),
            ReadNullableInt32(reader, "retention_days"),
            ReadNullableDateTime(reader, "next_run_at") is { } nextRunAt
                ? ToUtcIso(nextRunAt)
                : null,
            ReadNullableString(reader, "last_error_public"),
            collectedAt is null ? null : ToUtcIso(collectedAt.Value),
            ReadNullableDateTime(reader, "last_verified_at") is { } verifiedAt
                ? ToUtcIso(verifiedAt)
                : null,
            ReadNullableString(reader, "verification_status"));
    }

    private static async Task<(string ServiceId, string ServiceName)?>
        ResolveClientBackupServiceAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string customerId,
            string backupJobId,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT job.service_id, service.name AS service_name
            FROM backup_jobs job
            INNER JOIN customer_services service
                ON service.id = job.service_id
                AND service.customer_id = job.customer_id
            WHERE job.id = @id AND job.customer_id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", backupJobId);
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (
                MariaDbIdentifierReader.ReadRequired(reader, "service_id"),
                reader.GetString("service_name"))
            : null;
    }

    private static async Task<bool> ServiceMatchesCustomerAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string customerId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM customer_services
            WHERE id = @service_id AND customer_id = @customer_id;
            """;
        command.Parameters.AddWithValue("@service_id", serviceId);
        command.Parameters.AddWithValue("@customer_id", customerId);
        var count = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken));
        return count == 1;
    }

    private static async Task InsertRequestCreatedEventAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string requestId,
        string actorUserId,
        string correlationId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO request_events (
                id,
                request_type,
                request_id,
                actor_user_id,
                event_type,
                old_status,
                new_status,
                correlation_id,
                created_at
            ) VALUES (
                @id,
                'support',
                @request_id,
                @actor_user_id,
                'created',
                NULL,
                'open',
                @correlation_id,
                @created_at
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@request_id", requestId);
        command.Parameters.AddWithValue("@actor_user_id", actorUserId);
        command.Parameters.AddWithValue("@correlation_id", correlationId);
        command.Parameters.AddWithValue("@created_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO audit_logs (
                id,
                occurred_at,
                correlation_id,
                actor_user_id,
                actor_service,
                customer_id,
                action,
                target_type,
                target_reference,
                outcome,
                reason_code,
                source_address,
                metadata_json
            ) VALUES (
                @id,
                @occurred_at,
                @correlation_id,
                @actor_user_id,
                @actor_service,
                @customer_id,
                @action,
                @target_type,
                @target_reference,
                @outcome,
                @reason_code,
                @source_address,
                @metadata_json
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@occurred_at", DateTime.UtcNow);
        command.Parameters.AddWithValue(
            "@correlation_id",
            auditEvent.CorrelationId);
        command.Parameters.AddWithValue(
            "@actor_user_id",
            DbValue(auditEvent.ActorUserId));
        command.Parameters.AddWithValue(
            "@actor_service",
            "API-INTERNAL");
        command.Parameters.AddWithValue("@customer_id", DbValue(auditEvent.CustomerId));
        command.Parameters.AddWithValue("@action", auditEvent.Action);
        command.Parameters.AddWithValue(
            "@target_type",
            DbValue(auditEvent.TargetType));
        command.Parameters.AddWithValue(
            "@target_reference",
            DbValue(auditEvent.TargetReference));
        command.Parameters.AddWithValue("@outcome", auditEvent.Outcome);
        command.Parameters.AddWithValue(
            "@reason_code",
            DbValue(auditEvent.ReasonCode));
        command.Parameters.AddWithValue(
            "@source_address",
            DbValue(auditEvent.SourceAddress));
        command.Parameters.AddWithValue("@metadata_json", DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private BackupIntegrationSummary ReadIntegration(MySqlDataReader reader)
    {
        var staleAfterMinutes = reader.GetInt32("stale_after_minutes");
        var collectedAt = ReadNullableDateTime(reader, "last_collected_at");
        var rawStatus = ReadNullableString(reader, "last_collection_status");
        var status = IsCollectionStale(collectedAt, staleAfterMinutes)
            ? "stale"
            : rawStatus;
        var message = status == "stale"
            ? "Le collecteur ne remonte plus de donnees recentes."
            : ReadNullableString(reader, "last_collection_message");

        return new BackupIntegrationSummary(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("provider"),
            reader.GetString("external_job_id"),
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            reader.GetString("customer_reference"),
            reader.GetString("customer_name"),
            MariaDbIdentifierReader.ReadRequired(reader, "service_id"),
            reader.GetString("service_name"),
            reader.GetBoolean("enabled"),
            reader.GetInt32("expected_interval_minutes"),
            reader.GetInt32("critical_after_minutes"),
            staleAfterMinutes,
            collectedAt is null ? null : ToUtcIso(collectedAt.Value),
            status,
            message,
            ToUtcIso(reader.GetDateTime("created_at")),
            ToUtcIso(reader.GetDateTime("updated_at")));
    }

    private static bool IsCollectionStale(
        DateTime? collectedAt,
        int staleAfterMinutes)
        => collectedAt is null
            || DateTime.UtcNow - ToUtc(collectedAt.Value)
                > TimeSpan.FromMinutes(staleAfterMinutes);

    private static string BuildRestoreDescription(
        BackupRestoreRequestPayload payload,
        string serviceName)
    {
        var desired = payload.DesiredRestoreAt is null
            ? "Date de restauration souhaitee : non precisee."
            : $"Date de restauration souhaitee : {ToUtcIso(ToUtc(payload.DesiredRestoreAt.Value))}.";
        return string.Join(
            Environment.NewLine,
            [
                $"Service concerne : {serviceName}",
                $"Element ou dossier : {(string.IsNullOrWhiteSpace(payload.ItemPath) ? "non precise" : payload.ItemPath)}",
                desired,
                $"Description : {(string.IsNullOrWhiteSpace(payload.Description) ? "non precisee" : payload.Description)}"
            ]);
    }

    private static DateTime ToUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc
            ? value
            : value.ToUniversalTime();

    private static DateTime? ToNullableUtc(DateTime? value)
        => value is null ? null : ToUtc(value.Value);

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

    private static string CreateReference(string prefix)
        => $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

    private static object DbValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static DateTime? ReadNullableDateTime(
        MySqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : ToUtc(reader.GetDateTime(ordinal));
    }

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt32(
        MySqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static long? ReadNullableInt64(
        MySqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private sealed record BackupMapping(
        string Id,
        string CustomerId,
        string ServiceId,
        bool Enabled,
        int ExpectedIntervalMinutes,
        int CriticalAfterMinutes,
        int StaleAfterMinutes,
        string Provider,
        string ExternalJobId);
}
