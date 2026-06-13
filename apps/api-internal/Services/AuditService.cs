using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record AuditEvent(
    string CorrelationId,
    string Action,
    string Outcome,
    string? ReasonCode = null,
    string? TargetType = null,
    string? TargetReference = null,
    string? CustomerId = null,
    string? ActorUserId = null,
    string? SourceAddress = null);

public interface IAuditService
{
    Task RecordAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}

public sealed class AuditService : IAuditService
{
    private readonly IPortalRepository _repository;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IPortalRepository repository,
        ILogger<AuditService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task RecordAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Audit action {Action} outcome {Outcome} reason_code {ReasonCode} target_type {TargetType} correlation_id {CorrelationId}",
            auditEvent.Action,
            auditEvent.Outcome,
            auditEvent.ReasonCode,
            auditEvent.TargetType,
            auditEvent.CorrelationId);

        try
        {
            await _repository.AppendAuditAsync(auditEvent, cancellationToken);
        }
        catch (Exception exception) when (
            exception is MySqlException or InvalidOperationException)
        {
            _logger.LogWarning(
                "Audit persistence unavailable for correlation_id {CorrelationId}",
                auditEvent.CorrelationId);
        }
    }
}
