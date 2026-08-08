using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IBackupRepository
{
    bool IsPersistent { get; }

    Task<BackupIngestionResult> IngestReportAsync(
        BackupReportPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BackupJobSummary>> GetClientBackupsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<BackupJobDetail?> GetClientBackupAsync(
        PortalSessionContext session,
        string backupJobId,
        CancellationToken cancellationToken);

    Task<RequestMutationResponse> CreateRestoreRequestAsync(
        PortalSessionContext session,
        string backupJobId,
        BackupRestoreRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BackupIntegrationSummary>> GetAdminIntegrationsAsync(
        CancellationToken cancellationToken);

    Task<BackupIntegrationSummary> UpsertAdminIntegrationAsync(
        BackupIntegrationPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
}
