namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Persistance de developpement uniquement. L'etat vit en memoire de processus
/// et disparait au redemarrage : l'UI l'annonce explicitement.
/// </summary>
/// <remarks>
/// Modele et revision sont poses sous le meme verrou, et l'amorce l'est en
/// bloc : c'est l'equivalent en memoire des transactions MariaDB.
/// </remarks>
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
        string payloadJson,
        string correlationId,
        string outcome,
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

            MockRevisionFailureSwitch.ThrowIfArmed();

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
            Revisions.Add(new StoredTemplateRevision(
                template.TemplateKey,
                expectedVersion + 1,
                outcome,
                template.UpdatedByUserId,
                correlationId,
                DateTime.UtcNow));
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryDeleteAsync(
        string templateKey,
        int expectedVersion,
        string? actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (!Templates.TryGetValue(templateKey, out var current)
                || current.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            MockRevisionFailureSwitch.ThrowIfArmed();

            Templates.Remove(templateKey);
            Revisions.Add(new StoredTemplateRevision(
                templateKey,
                expectedVersion,
                "deleted",
                actorUserId,
                correlationId,
                DateTime.UtcNow));
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Amorce tout ou rien : la panne simulee est levee avant toute
    /// publication, donc aucun modele ne subsiste a moitie.
    /// </summary>
    public Task<bool> TryImportAsync(
        IReadOnlyList<DemoContentTemplateImportItem> items,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (Templates.Count > 0)
            {
                return Task.FromResult(false);
            }

            MockRevisionFailureSwitch.ThrowIfArmed();

            foreach (var item in items)
            {
                var order = 0;
                var services = item.Template.Services
                    .Select(service => service with { DisplayOrder = order += 10 })
                    .ToArray();
                Templates[item.Template.TemplateKey] = item.Template with
                {
                    UpdatedAtUtc = DateTime.UtcNow,
                    Services = services,
                };
                Revisions.Add(new StoredTemplateRevision(
                    item.Template.TemplateKey,
                    item.Template.Version,
                    "imported",
                    item.Template.UpdatedByUserId,
                    correlationId,
                    DateTime.UtcNow));
            }

            return Task.FromResult(true);
        }
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

    /// <summary>Nombre de modeles stockes, pour les tests.</summary>
    public static int TemplateCount()
    {
        lock (Gate) return Templates.Count;
    }

    /// <summary>Nombre de revisions enregistrees, pour les tests.</summary>
    public static int RevisionCount()
    {
        lock (Gate) return Revisions.Count;
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
