using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public interface IBackupService
{
    bool IsPersistent { get; }

    Task<BackupIngestionResult> IngestReportAsync(
        BackupReportPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BackupJobSummary>> GetClientBackupsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<BackupJobDetail> GetClientBackupAsync(
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

public sealed class BackupService : IBackupService
{
    private readonly IBackupRepository _repository;

    public BackupService(IBackupRepository repository)
    {
        _repository = repository;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public Task<BackupIngestionResult> IngestReportAsync(
        BackupReportPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.IngestReportAsync(payload, correlationId, cancellationToken);

    public Task<IReadOnlyList<BackupJobSummary>> GetClientBackupsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => _repository.GetClientBackupsAsync(session, cancellationToken);

    public async Task<BackupJobDetail> GetClientBackupAsync(
        PortalSessionContext session,
        string backupJobId,
        CancellationToken cancellationToken)
        => await _repository.GetClientBackupAsync(
                session,
                ValidateIdentifier(backupJobId),
                cancellationToken)
            ?? throw new PortalDataNotFoundException();

    public Task<RequestMutationResponse> CreateRestoreRequestAsync(
        PortalSessionContext session,
        string backupJobId,
        BackupRestoreRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
        => _repository.CreateRestoreRequestAsync(
            session,
            ValidateIdentifier(backupJobId),
            payload,
            correlationId,
            sourceAddress,
            cancellationToken);

    public Task<IReadOnlyList<BackupIntegrationSummary>>
        GetAdminIntegrationsAsync(CancellationToken cancellationToken)
        => _repository.GetAdminIntegrationsAsync(cancellationToken);

    public Task<BackupIntegrationSummary> UpsertAdminIntegrationAsync(
        BackupIntegrationPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.UpsertAdminIntegrationAsync(
            payload,
            correlationId,
            cancellationToken);

    private static string ValidateIdentifier(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 1 or > 100)
        {
            throw new PortalValidationException();
        }

        foreach (var character in normalized)
        {
            if (!char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.')
            {
                throw new PortalValidationException();
            }
        }

        return normalized;
    }
}
