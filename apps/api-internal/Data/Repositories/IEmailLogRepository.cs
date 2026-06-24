namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record EmailLogEntry(
    string Id,
    string Template,
    string Recipient,
    string Subject,
    string Status,
    string? ErrorMessage,
    string? RelatedDocumentId,
    string CorrelationId,
    string CreatedAt,
    string? SentAt);

public interface IEmailLogRepository
{
    bool IsPersistent { get; }

    Task<string> RecordAsync(
        string template,
        string recipient,
        string subject,
        string body,
        string status,
        string? errorMessage,
        string? relatedDocumentId,
        string correlationId,
        bool delivered,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EmailLogEntry>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken);
}
