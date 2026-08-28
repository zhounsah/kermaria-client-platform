namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record StoredEmailTemplate(
    string Key,
    string Subject,
    string Body,
    bool Enabled,
    int Version,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId = null);

public sealed record StoredNotificationTemplate(
    string Key,
    string Title,
    string Message,
    bool Enabled,
    int Version,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId = null);

public sealed record StoredSystemSnippet(
    string Key,
    string Body,
    int Version,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId = null);

public sealed record StoredTemplateRevision(
    string Key,
    int Version,
    string Outcome,
    string? ActorUserId,
    string CorrelationId,
    DateTime CreatedAtUtc);

/// <summary>
/// Persistance des communications administrables. Les tables sont
/// specialisees (migration 074) : elles ne transitent jamais par
/// <c>application_settings</c>.
/// </summary>
public interface ICommunicationTemplateRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<StoredEmailTemplate>> GetEmailTemplatesAsync(
        CancellationToken cancellationToken);

    Task<bool> TryUpsertEmailTemplateAsync(
        StoredEmailTemplate template,
        string displayName,
        int expectedVersion,
        CancellationToken cancellationToken);

    Task AddEmailRevisionAsync(
        StoredEmailTemplate template,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredTemplateRevision>> GetEmailRevisionsAsync(
        string templateKey,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredNotificationTemplate>> GetNotificationTemplatesAsync(
        CancellationToken cancellationToken);

    Task<bool> TryUpsertNotificationTemplateAsync(
        StoredNotificationTemplate template,
        string displayName,
        int expectedVersion,
        CancellationToken cancellationToken);

    Task AddNotificationRevisionAsync(
        StoredNotificationTemplate template,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredTemplateRevision>> GetNotificationRevisionsAsync(
        string templateKey,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredSystemSnippet>> GetSnippetsAsync(
        CancellationToken cancellationToken);

    Task<bool> TryUpsertSnippetAsync(
        StoredSystemSnippet snippet,
        string displayName,
        int expectedVersion,
        CancellationToken cancellationToken);

    Task AddSnippetRevisionAsync(
        StoredSystemSnippet snippet,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredTemplateRevision>> GetSnippetRevisionsAsync(
        string snippetKey,
        int limit,
        CancellationToken cancellationToken);
}
