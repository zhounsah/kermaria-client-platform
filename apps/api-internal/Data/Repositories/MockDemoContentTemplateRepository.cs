namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Persistance de developpement uniquement. L'etat vit en memoire de processus
/// et disparait au redemarrage : l'UI l'annonce explicitement.
/// </summary>
public sealed class MockDemoContentTemplateRepository : IDemoContentTemplateRepository
{
    private static readonly Dictionary<string, StoredDemoContentTemplate> Templates =
        new(StringComparer.Ordinal);
    private static readonly List<StoredTemplateRevision> Revisions = [];
    private static readonly object Gate = new();

    public bool IsPersistent => false;

    public Task<IReadOnlyList<StoredDemoContentTemplate>> ListAsync(
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            return Task.FromResult<IReadOnlyList<StoredDemoContentTemplate>>(
                Templates.Values
                    .OrderBy(item => item.DisplayOrder)
                    .ThenBy(item => item.TemplateKey, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    public Task<bool> TrySaveAsync(
        StoredDemoContentTemplate template,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            var exists = Templates.TryGetValue(template.TemplateKey, out var current);
            var storedVersion = exists ? current!.Version : 0;
            if (storedVersion != expectedVersion)
            {
                return Task.FromResult(false);
            }

            var order = 0;
            var services = template.Services
                .Select(service => service with { DisplayOrder = order += 10 })
                .ToArray();
            Templates[template.TemplateKey] = template with
            {
                Version = expectedVersion + 1,
                UpdatedAtUtc = DateTime.UtcNow,
                Services = services,
            };
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryDeleteAsync(
        string templateKey,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (!Templates.TryGetValue(templateKey, out var current)
                || current.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            Templates.Remove(templateKey);
            return Task.FromResult(true);
        }
    }

    public Task AddRevisionAsync(
        string templateKey,
        int version,
        string payloadJson,
        string? actorUserId,
        string correlationId,
        string outcome,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            Revisions.Add(new StoredTemplateRevision(
                templateKey,
                version,
                outcome,
                actorUserId,
                correlationId,
                DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredTemplateRevision>> GetRevisionsAsync(
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            return Task.FromResult<IReadOnlyList<StoredTemplateRevision>>(
                Revisions
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .Take(100)
                    .ToArray());
        }
    }

    /// <summary>Reinitialise l'etat entre deux scenarios de test.</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            Templates.Clear();
            Revisions.Clear();
        }
    }
}
