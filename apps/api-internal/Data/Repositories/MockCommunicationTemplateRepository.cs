namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Persistance de developpement uniquement. L'etat vit en memoire de processus
/// et disparait au redemarrage : l'UI l'annonce explicitement.
/// </summary>
/// <remarks>
/// Le modele et sa revision sont poses sous le meme verrou : soit les deux,
/// soit aucun. C'est l'equivalent en memoire de la transaction MariaDB.
/// </remarks>
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

    public Task<bool> TrySaveEmailTemplateAsync(
        StoredEmailTemplate template,
        string displayName,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (!MatchesVersion(Emails.TryGetValue(template.Key, out var current), current?.Version, expectedVersion))
            {
                return Task.FromResult(false);
            }

            MockRevisionFailureSwitch.ThrowIfArmed();

            Emails[template.Key] = template;
            EmailRevisions.Add(new StoredTemplateRevision(
                template.Key,
                template.Version,
                outcome,
                template.UpdatedByUserId,
                correlationId,
                DateTime.UtcNow));
            return Task.FromResult(true);
        }
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

    public Task<bool> TrySaveNotificationTemplateAsync(
        StoredNotificationTemplate template,
        string displayName,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (!MatchesVersion(Notifications.TryGetValue(template.Key, out var current), current?.Version, expectedVersion))
            {
                return Task.FromResult(false);
            }

            MockRevisionFailureSwitch.ThrowIfArmed();

            Notifications[template.Key] = template;
            NotificationRevisions.Add(new StoredTemplateRevision(
                template.Key,
                template.Version,
                outcome,
                template.UpdatedByUserId,
                correlationId,
                DateTime.UtcNow));
            return Task.FromResult(true);
        }
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

    public Task<bool> TrySaveSnippetAsync(
        StoredSystemSnippet snippet,
        string displayName,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (!MatchesVersion(Snippets.TryGetValue(snippet.Key, out var current), current?.Version, expectedVersion))
            {
                return Task.FromResult(false);
            }

            MockRevisionFailureSwitch.ThrowIfArmed();

            Snippets[snippet.Key] = snippet;
            SnippetRevisions.Add(new StoredTemplateRevision(
                snippet.Key,
                snippet.Version,
                outcome,
                snippet.UpdatedByUserId,
                correlationId,
                DateTime.UtcNow));
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<StoredTemplateRevision>> GetSnippetRevisionsAsync(
        string snippetKey,
        int limit,
        CancellationToken cancellationToken)
        => ReadRevisions(SnippetRevisions, snippetKey, limit);

    /// <summary>Etat brut d'un modele d'e-mail, pour les tests.</summary>
    public static StoredEmailTemplate? PeekEmail(string key)
    {
        lock (Gate) return Emails.GetValueOrDefault(key);
    }

    /// <summary>Nombre de revisions d'e-mail enregistrees, pour les tests.</summary>
    public static int EmailRevisionCount(string key)
    {
        lock (Gate)
        {
            return EmailRevisions.Count(
                item => string.Equals(item.Key, key, StringComparison.Ordinal));
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Emails.Clear();
            Notifications.Clear();
            Snippets.Clear();
            EmailRevisions.Clear();
            NotificationRevisions.Clear();
            SnippetRevisions.Clear();
        }
    }

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
