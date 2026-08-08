using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockBackupStore
{
    public object SyncRoot { get; } = new();

    public List<MockBackupIntegration> Integrations { get; } =
    [
        new(
            "backup-integration-mock-001",
            "veeam",
            "mock-job-backup-001",
            "mock-customer",
            MockPortalData.Profile.CustomerReference,
            MockPortalData.Profile.CompanyName,
            "svc-backup-001",
            "Sauvegarde",
            true,
            1440,
            2160,
            180,
            DateTime.UtcNow.AddMinutes(-15),
            "success",
            "Derniere collecte mock reussie.",
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow.AddMinutes(-15))
    ];

    public List<MockBackupJob> Jobs { get; } =
    [
        new(
            "backup-job-mock-001",
            "mock-customer",
            "svc-backup-001",
            "Sauvegarde",
            "veeam",
            "mock-job-backup-001",
            "active",
            BackupProtectionStatuses.Protected,
            DateTime.UtcNow.Date.AddHours(1),
            DateTime.UtcNow.Date.AddHours(1),
            "success",
            20_078_972_109,
            942,
            31,
            DateTime.UtcNow.Date.AddDays(1).AddHours(1),
            null,
            DateTime.UtcNow.AddMinutes(-15),
            null,
            null)
    ];

    public List<MockBackupRun> Runs { get; } =
    [
        new(
            "backup-run-mock-001",
            "backup-job-mock-001",
            "mock-session-001",
            DateTime.UtcNow.Date.AddHours(1),
            DateTime.UtcNow.Date.AddHours(1).AddMinutes(15),
            "success",
            20_078_972_109,
            942,
            null),
        new(
            "backup-run-mock-002",
            "backup-job-mock-001",
            "mock-session-002",
            DateTime.UtcNow.Date.AddDays(-1).AddHours(1),
            DateTime.UtcNow.Date.AddDays(-1).AddHours(1).AddMinutes(14),
            "success",
            20_078_972_109,
            884,
            null),
        new(
            "backup-run-mock-003",
            "backup-job-mock-001",
            "mock-session-003",
            DateTime.UtcNow.Date.AddDays(-2).AddHours(1),
            DateTime.UtcNow.Date.AddDays(-2).AddHours(1).AddMinutes(16),
            "warning",
            19_971_504_742,
            1001,
            "Un avertissement non bloquant a ete detecte.")
    ];
}

public sealed class MockBackupRepository : IBackupRepository
{
    private readonly MockBackupStore _store;
    private readonly IBackupProtectionService _protection;

    public MockBackupRepository(
        MockBackupStore store,
        IBackupProtectionService protection)
    {
        _store = store;
        _protection = protection;
    }

    public bool IsPersistent => false;

    public Task<BackupIngestionResult> IngestReportAsync(
        BackupReportPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalized = _protection.NormalizeReport(payload);
        lock (_store.SyncRoot)
        {
            var mapping = _store.Integrations.FirstOrDefault(integration =>
                integration.Provider == normalized.Provider
                && integration.ExternalJobId == normalized.ExternalJobId
                && integration.Enabled);
            if (mapping is null)
            {
                return Task.FromResult(new BackupIngestionResult(
                    true,
                    false,
                    false,
                    null,
                    BackupProtectionStatuses.Unknown,
                    correlationId));
            }

            var now = DateTime.UtcNow;
            var startedAt = normalized.StartedAt!.Value.ToUniversalTime();
            var finishedAt = normalized.FinishedAt?.ToUniversalTime();
            var lastSuccessAt = normalized.Result is "success" or "warning"
                ? finishedAt ?? startedAt
                : _store.Jobs.FirstOrDefault(job =>
                    job.Provider == normalized.Provider
                    && job.ExternalJobId == normalized.ExternalJobId)?
                    .LastSuccessAt;
            var protectionStatus = _protection.ComputeProtectionStatus(
                now,
                lastSuccessAt,
                normalized.Result,
                now,
                mapping.ExpectedIntervalMinutes,
                mapping.CriticalAfterMinutes,
                mapping.StaleAfterMinutes);
            var job = _store.Jobs.FirstOrDefault(candidate =>
                candidate.Provider == normalized.Provider
                && candidate.ExternalJobId == normalized.ExternalJobId);
            if (job is null)
            {
                job = new MockBackupJob(
                    Guid.NewGuid().ToString("D"),
                    mapping.CustomerId,
                    mapping.ServiceId,
                    mapping.ServiceName,
                    mapping.Provider,
                    mapping.ExternalJobId,
                    "active",
                    protectionStatus,
                    finishedAt ?? startedAt,
                    lastSuccessAt,
                    normalized.Result,
                    normalized.ProtectedBytes,
                    normalized.DurationSeconds,
                    normalized.RetentionDays,
                    normalized.NextRunAt,
                    normalized.PublicMessage,
                    now,
                    null,
                    null);
                _store.Jobs.Add(job);
            }
            else
            {
                job.ProtectionStatus = protectionStatus;
                job.LastRunAt = finishedAt ?? startedAt;
                job.LastSuccessAt = lastSuccessAt ?? job.LastSuccessAt;
                job.LastResult = normalized.Result;
                job.ProtectedBytes = normalized.ProtectedBytes;
                job.DurationSeconds = normalized.DurationSeconds;
                job.RetentionDays = normalized.RetentionDays;
                job.NextRunAt = normalized.NextRunAt;
                job.LastErrorPublic = normalized.Result is "failed" or "warning"
                    ? normalized.PublicMessage
                    : null;
                job.CollectedAt = now;
            }

            var runInserted = !_store.Runs.Any(run =>
                run.BackupJobId == job.Id
                && run.ExternalSessionId == normalized.ExternalSessionId);
            if (runInserted)
            {
                _store.Runs.Add(new MockBackupRun(
                    Guid.NewGuid().ToString("D"),
                    job.Id,
                    normalized.ExternalSessionId!,
                    startedAt,
                    finishedAt,
                    normalized.Result!,
                    normalized.ProtectedBytes,
                    normalized.DurationSeconds,
                    normalized.PublicMessage));
            }

            mapping.LastCollectedAt = now;
            mapping.LastCollectionStatus = "success";
            mapping.LastCollectionMessage = runInserted
                ? "Nouvelle session enregistree."
                : "Session deja connue.";

            return Task.FromResult(new BackupIngestionResult(
                true,
                true,
                runInserted,
                job.Id,
                protectionStatus,
                correlationId));
        }
    }

    public Task<IReadOnlyList<BackupJobSummary>> GetClientBackupsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<BackupJobSummary>>(
                _store.Jobs
                    .Where(job => job.CustomerId == session.CustomerId)
                    .Select(ToSummary)
                    .ToArray());
        }
    }

    public Task<BackupJobDetail?> GetClientBackupAsync(
        PortalSessionContext session,
        string backupJobId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var job = _store.Jobs.FirstOrDefault(candidate =>
                candidate.Id == backupJobId
                && candidate.CustomerId == session.CustomerId);
            if (job is null)
            {
                return Task.FromResult<BackupJobDetail?>(null);
            }

            var runs = _store.Runs
                .Where(run => run.BackupJobId == job.Id)
                .OrderByDescending(run => run.StartedAt)
                .Take(31)
                .Select(run => new BackupRunSummary(
                    run.Id,
                    ToUtcIso(run.StartedAt),
                    run.FinishedAt is null ? null : ToUtcIso(run.FinishedAt.Value),
                    run.Result,
                    _protection.PublicResultLabel(run.Result),
                    run.ProtectedBytes,
                    run.DurationSeconds,
                    run.PublicMessage))
                .ToArray();
            return Task.FromResult<BackupJobDetail?>(
                new BackupJobDetail(ToSummary(job), runs));
        }
    }

    public Task<RequestMutationResponse> CreateRestoreRequestAsync(
        PortalSessionContext session,
        string backupJobId,
        BackupRestoreRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        _ = _protection.NormalizeRestoreRequest(payload);
        lock (_store.SyncRoot)
        {
            if (!_store.Jobs.Any(job =>
                    job.Id == backupJobId
                    && job.CustomerId == session.CustomerId))
            {
                throw new PortalAccessDeniedException();
            }
        }

        return Task.FromResult(new RequestMutationResponse(
            Guid.NewGuid().ToString("D"),
            $"RST-MOCK-{Random.Shared.Next(1000, 9999)}",
            "open",
            true,
            correlationId));
    }

    public Task<IReadOnlyList<BackupIntegrationSummary>>
        GetAdminIntegrationsAsync(CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<BackupIntegrationSummary>>(
                _store.Integrations.Select(ToIntegrationSummary).ToArray());
        }
    }

    public Task<BackupIntegrationSummary> UpsertAdminIntegrationAsync(
        BackupIntegrationPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalized = _protection.NormalizeIntegration(payload);
        lock (_store.SyncRoot)
        {
            var integration = _store.Integrations.FirstOrDefault(candidate =>
                candidate.Provider == normalized.Provider
                && candidate.ExternalJobId == normalized.ExternalJobId);
            if (integration is null)
            {
                integration = new MockBackupIntegration(
                    normalized.Id ?? Guid.NewGuid().ToString("D"),
                    normalized.Provider!,
                    normalized.ExternalJobId!,
                    normalized.CustomerId!,
                    "CLI-MOCK",
                    "Client mock",
                    normalized.ServiceId!,
                    "Service mock",
                    normalized.Enabled,
                    normalized.ExpectedIntervalMinutes!.Value,
                    normalized.CriticalAfterMinutes!.Value,
                    normalized.StaleAfterMinutes!.Value,
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow);
                _store.Integrations.Add(integration);
            }
            else
            {
                integration.CustomerId = normalized.CustomerId!;
                integration.ServiceId = normalized.ServiceId!;
                integration.Enabled = normalized.Enabled;
                integration.ExpectedIntervalMinutes =
                    normalized.ExpectedIntervalMinutes!.Value;
                integration.CriticalAfterMinutes =
                    normalized.CriticalAfterMinutes!.Value;
                integration.StaleAfterMinutes =
                    normalized.StaleAfterMinutes!.Value;
                integration.UpdatedAt = DateTime.UtcNow;
            }

            return Task.FromResult(ToIntegrationSummary(integration));
        }
    }

    private BackupJobSummary ToSummary(MockBackupJob job)
    {
        var integration = _store.Integrations.FirstOrDefault(candidate =>
            candidate.Provider == job.Provider
            && candidate.ExternalJobId == job.ExternalJobId);
        var protectionStatus = _protection.ComputeProtectionStatus(
            DateTime.UtcNow,
            job.LastSuccessAt,
            job.LastResult,
            job.CollectedAt,
            integration?.ExpectedIntervalMinutes ?? 1440,
            integration?.CriticalAfterMinutes ?? 2160,
            integration?.StaleAfterMinutes ?? 180);
        return new BackupJobSummary(
            job.Id,
            job.ServiceId,
            job.ServiceName,
            job.Provider,
            job.Status,
            protectionStatus,
            _protection.PublicProtectionLabel(protectionStatus),
            ToUtcIso(job.LastRunAt),
            job.LastSuccessAt is null ? null : ToUtcIso(job.LastSuccessAt.Value),
            job.LastResult,
            job.LastResult is null
                ? null
                : _protection.PublicResultLabel(job.LastResult),
            job.ProtectedBytes,
            job.DurationSeconds,
            job.RetentionDays,
            job.NextRunAt is null ? null : ToUtcIso(job.NextRunAt.Value),
            job.LastErrorPublic,
            ToUtcIso(job.CollectedAt),
            job.LastVerifiedAt is null ? null : ToUtcIso(job.LastVerifiedAt.Value),
            job.VerificationStatus);
    }

    private static BackupIntegrationSummary ToIntegrationSummary(
        MockBackupIntegration integration)
    {
        var stale = integration.LastCollectedAt is null
            || DateTime.UtcNow - integration.LastCollectedAt.Value.ToUniversalTime()
                > TimeSpan.FromMinutes(integration.StaleAfterMinutes);
        var status = stale ? "stale" : integration.LastCollectionStatus;
        var message = stale
            ? "Le collecteur ne remonte plus de donnees recentes."
            : integration.LastCollectionMessage;

        return new BackupIntegrationSummary(
            integration.Id,
            integration.Provider,
            integration.ExternalJobId,
            integration.CustomerId,
            integration.CustomerReference,
            integration.CustomerName,
            integration.ServiceId,
            integration.ServiceName,
            integration.Enabled,
            integration.ExpectedIntervalMinutes,
            integration.CriticalAfterMinutes,
            integration.StaleAfterMinutes,
            integration.LastCollectedAt is null
                ? null
                : ToUtcIso(integration.LastCollectedAt.Value),
            status,
            message,
            ToUtcIso(integration.CreatedAt),
            ToUtcIso(integration.UpdatedAt));
    }

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}

public sealed record MockBackupRun(
    string Id,
    string BackupJobId,
    string ExternalSessionId,
    DateTime StartedAt,
    DateTime? FinishedAt,
    string Result,
    long? ProtectedBytes,
    int? DurationSeconds,
    string? PublicMessage);

public sealed class MockBackupJob
{
    public MockBackupJob(
        string id,
        string customerId,
        string serviceId,
        string serviceName,
        string provider,
        string externalJobId,
        string status,
        string protectionStatus,
        DateTime lastRunAt,
        DateTime? lastSuccessAt,
        string? lastResult,
        long? protectedBytes,
        int? durationSeconds,
        int? retentionDays,
        DateTime? nextRunAt,
        string? lastErrorPublic,
        DateTime collectedAt,
        DateTime? lastVerifiedAt,
        string? verificationStatus)
    {
        Id = id;
        CustomerId = customerId;
        ServiceId = serviceId;
        ServiceName = serviceName;
        Provider = provider;
        ExternalJobId = externalJobId;
        Status = status;
        ProtectionStatus = protectionStatus;
        LastRunAt = lastRunAt;
        LastSuccessAt = lastSuccessAt;
        LastResult = lastResult;
        ProtectedBytes = protectedBytes;
        DurationSeconds = durationSeconds;
        RetentionDays = retentionDays;
        NextRunAt = nextRunAt;
        LastErrorPublic = lastErrorPublic;
        CollectedAt = collectedAt;
        LastVerifiedAt = lastVerifiedAt;
        VerificationStatus = verificationStatus;
    }

    public string Id { get; }
    public string CustomerId { get; }
    public string ServiceId { get; }
    public string ServiceName { get; }
    public string Provider { get; }
    public string ExternalJobId { get; }
    public string Status { get; set; }
    public string ProtectionStatus { get; set; }
    public DateTime LastRunAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public string? LastResult { get; set; }
    public long? ProtectedBytes { get; set; }
    public int? DurationSeconds { get; set; }
    public int? RetentionDays { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string? LastErrorPublic { get; set; }
    public DateTime CollectedAt { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
    public string? VerificationStatus { get; set; }
}

public sealed class MockBackupIntegration
{
    public MockBackupIntegration(
        string id,
        string provider,
        string externalJobId,
        string customerId,
        string customerReference,
        string customerName,
        string serviceId,
        string serviceName,
        bool enabled,
        int expectedIntervalMinutes,
        int criticalAfterMinutes,
        int staleAfterMinutes,
        DateTime? lastCollectedAt,
        string? lastCollectionStatus,
        string? lastCollectionMessage,
        DateTime createdAt,
        DateTime updatedAt)
    {
        Id = id;
        Provider = provider;
        ExternalJobId = externalJobId;
        CustomerId = customerId;
        CustomerReference = customerReference;
        CustomerName = customerName;
        ServiceId = serviceId;
        ServiceName = serviceName;
        Enabled = enabled;
        ExpectedIntervalMinutes = expectedIntervalMinutes;
        CriticalAfterMinutes = criticalAfterMinutes;
        StaleAfterMinutes = staleAfterMinutes;
        LastCollectedAt = lastCollectedAt;
        LastCollectionStatus = lastCollectionStatus;
        LastCollectionMessage = lastCollectionMessage;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public string Id { get; }
    public string Provider { get; }
    public string ExternalJobId { get; }
    public string CustomerId { get; set; }
    public string CustomerReference { get; set; }
    public string CustomerName { get; set; }
    public string ServiceId { get; set; }
    public string ServiceName { get; set; }
    public bool Enabled { get; set; }
    public int ExpectedIntervalMinutes { get; set; }
    public int CriticalAfterMinutes { get; set; }
    public int StaleAfterMinutes { get; set; }
    public DateTime? LastCollectedAt { get; set; }
    public string? LastCollectionStatus { get; set; }
    public string? LastCollectionMessage { get; set; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; set; }
}
