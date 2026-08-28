namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Persistance de developpement uniquement. L'etat vit en memoire de processus
/// et disparait au redemarrage : l'UI l'annonce explicitement.
/// </summary>
public sealed class MockCommunicationTemplateRepository
    : ICommunicationTemplateRepository
{
    private static readonly Dictionary<string, StoredEmailTemplate> Emails =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, StoredNotificationTemplate> Notifications =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, StoredSystemSnippet> Snippets =
        new(StringComparer.Ordinal);
    private static readonly List<StoredTemplateRevision> EmailRevisions = [];
    private static readonly List<StoredTemplateRevision> NotificationRevisions = [];
    private static readonly List<StoredTemplateRevision> SnippetRevisions = [];
    private static readonly object Gate = new();

    public bool IsPersistent => false;

    public Task<IReadOnlyList<StoredEmailTemplate>> GetEmailTemplatesAsync(
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            return Task.FromResult<IReadOnlyList<StoredEmailTemplate>>(
                Emails.Values.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray());
        }
    }

    public Task<bool> TryUpsertEmailTemplateAsync(
        StoredEmailTemplate template,
        string displayName,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (!MatchesVersion(Emails.TryGetValue(template.Key, out var current), current?.Version, expectedVersion))
            {
                return Task.FromResult(false);
            }

            Emails[template.Key] = template;
            return Task.FromResult(true);
        }
    }

    public Task AddEmailRevisionAsync(
        StoredEmailTemplate template,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            EmailRevisions.Add(new StoredTemplateRevision(
                template.Key,
                template.Version,
                outcome,
                template.UpdatedByUserId,
                correlationId,
                DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredTemplateRevision>> GetEmailRevisionsAsync(
        string templateKey,
        int limit,
        CancellationToken cancellationToken)
        => ReadRevisions(EmailRevisions, templateKey, limit);

    public Task<IReadOnlyList<StoredNotificationTemplate>> GetNotificationTemplatesAsync(
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            return Task.FromResult<IReadOnlyList<StoredNotificationTemplate>>(
                Notifications.Values.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray());
        }
    }

    public Task<bool> TryUpsertNotificationTemplateAsync(
        StoredNotificationTemplate template,
        string displayName,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (!MatchesVersion(Notifications.TryGetValue(template.Key, out var current), current?.Version, expectedVersion))
            {
                return Task.FromResult(false);
            }

            Notifications[template.Key] = template;
            return Task.FromResult(true);
        }
    }

    public Task AddNotificationRevisionAsync(
        StoredNotificationTemplate template,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            NotificationRevisions.Add(new StoredTemplateRevision(
                template.Key,
                template.Version,
                outcome,
                template.UpdatedByUserId,
                correlationId,
                DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredTemplateRevision>> GetNotificationRevisionsAsync(
        string templateKey,
        int limit,
        CancellationToken cancellationToken)
        => ReadRevisions(NotificationRevisions, templateKey, limit);

    public Task<IReadOnlyList<StoredSystemSnippet>> GetSnippetsAsync(
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            return Task.FromResult<IReadOnlyList<StoredSystemSnippet>>(
                Snippets.Values.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray());
        }
    }

    public Task<bool> TryUpsertSnippetAsync(
        StoredSystemSnippet snippet,
        string displayName,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (!MatchesVersion(Snippets.TryGetValue(snippet.Key, out var current), current?.Version, expectedVersion))
            {
                return Task.FromResult(false);
            }

            Snippets[snippet.Key] = snippet;
            return Task.FromResult(true);
        }
    }

    public Task AddSnippetRevisionAsync(
        StoredSystemSnippet snippet,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            SnippetRevisions.Add(new StoredTemplateRevision(
                snippet.Key,
                snippet.Version,
                outcome,
                snippet.UpdatedByUserId,
                correlationId,
                DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredTemplateRevision>> GetSnippetRevisionsAsync(
        string snippetKey,
        int limit,
        CancellationToken cancellationToken)
        => ReadRevisions(SnippetRevisions, snippetKey, limit);

    private static bool MatchesVersion(bool exists, int? currentVersion, int expectedVersion)
        => exists ? currentVersion == expectedVersion : expectedVersion == 0;

    private static Task<IReadOnlyList<StoredTemplateRevision>> ReadRevisions(
        List<StoredTemplateRevision> source,
        string key,
        int limit)
    {
        lock (Gate)
        {
            return Task.FromResult<IReadOnlyList<StoredTemplateRevision>>(
                source
                    .Where(item => string.Equals(item.Key, key, StringComparison.Ordinal))
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .Take(Math.Clamp(limit, 1, 100))
                    .ToArray());
        }
    }
}
