using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record BackupReportPayload(
    string? Provider,
    string? ExternalJobId,
    string? ExternalSessionId,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? Result,
    long? ProtectedBytes,
    int? DurationSeconds,
    int? RetentionDays,
    DateTime? NextRunAt,
    string? PublicMessage);

public sealed record BackupIngestionResult(
    bool Accepted,
    bool Mapped,
    bool RunInserted,
    string? BackupJobId,
    string ProtectionStatus,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

public sealed record BackupRunSummary(
    string Id,
    string StartedAt,
    string? FinishedAt,
    string Result,
    string ResultLabel,
    long? ProtectedBytes,
    int? DurationSeconds,
    string? PublicMessage);

public sealed record BackupJobSummary(
    string Id,
    string ServiceId,
    string ServiceName,
    string Provider,
    string Status,
    string ProtectionStatus,
    string ProtectionStatusLabel,
    string? LastRunAt,
    string? LastSuccessAt,
    string? LastResult,
    string? LastResultLabel,
    long? ProtectedBytes,
    int? DurationSeconds,
    int? RetentionDays,
    string? NextRunAt,
    string? LastErrorPublic,
    string? CollectedAt,
    string? LastVerifiedAt,
    string? VerificationStatus);

public sealed record BackupJobDetail(
    BackupJobSummary Job,
    IReadOnlyList<BackupRunSummary> Runs);

public sealed record BackupRestoreRequestPayload(
    string? ItemPath,
    DateTime? DesiredRestoreAt,
    string? Description,
    string? Priority);

public sealed record BackupIntegrationSummary(
    string Id,
    string Provider,
    string ExternalJobId,
    string CustomerId,
    string CustomerReference,
    string CustomerName,
    string ServiceId,
    string ServiceName,
    bool Enabled,
    int ExpectedIntervalMinutes,
    int CriticalAfterMinutes,
    int StaleAfterMinutes,
    string? LastCollectedAt,
    string? LastCollectionStatus,
    string? LastCollectionMessage,
    string CreatedAt,
    string UpdatedAt);

public sealed record BackupIntegrationPayload(
    string? Id,
    string? Provider,
    string? ExternalJobId,
    string? CustomerId,
    string? ServiceId,
    bool Enabled,
    int? ExpectedIntervalMinutes,
    int? CriticalAfterMinutes,
    int? StaleAfterMinutes);
