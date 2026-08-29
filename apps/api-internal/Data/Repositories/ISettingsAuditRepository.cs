namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record SettingsAuditQuery(
    IReadOnlyList<string> Actions,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Actor,
    string? Outcome,
    string? CorrelationId,
    string? TargetReference,
    int Limit);

public sealed record SettingsAuditEntry(
    string OccurredAt,
    string Actor,
    string Action,
    string Outcome,
    string? ReasonCode,
    string? TargetType,
    string? TargetReference,
    string CorrelationId,
    string? SourceAddress);

public interface ISettingsAuditRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<SettingsAuditEntry>> SearchAsync(
        SettingsAuditQuery query,
        CancellationToken cancellationToken);
}
