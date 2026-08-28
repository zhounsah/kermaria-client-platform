namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Persistance de developpement uniquement. L'etat vit en memoire de processus
/// et disparait au redemarrage : l'UI l'annonce explicitement.
/// </summary>
public sealed class MockDiagnosticConfigurationRepository
    : IDiagnosticConfigurationRepository
{
    private static readonly Dictionary<string, StoredDiagnosticConfiguration> Entries =
        new(StringComparer.Ordinal);
    private static readonly List<StoredTemplateRevision> Revisions = [];
    private static readonly object Gate = new();

    public bool IsPersistent => false;

    public Task<StoredDiagnosticConfiguration?> GetAsync(
        string state,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            return Task.FromResult(
                Entries.TryGetValue(state, out var entry) ? entry : null);
        }
    }

    public Task<bool> TrySaveDraftAsync(
        StoredDiagnosticConfiguration draft,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            if (!MatchesVersion(draft.State, expectedVersion))
            {
                return Task.FromResult(false);
            }

            Entries[draft.State] = draft;
            AddRevision(draft, outcome, correlationId);
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryPublishAsync(
        StoredDiagnosticConfiguration published,
        int expectedPublishedVersion,
        int expectedDraftVersion,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            var draftVersion = Entries.TryGetValue("draft", out var draft)
                ? draft.Version
                : 0;
            if (draftVersion != expectedDraftVersion
                || !MatchesVersion(published.State, expectedPublishedVersion))
            {
                return Task.FromResult(false);
            }

            Entries[published.State] = published;
            AddRevision(published, "published", correlationId);
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<StoredTemplateRevision>> GetRevisionsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            return Task.FromResult<IReadOnlyList<StoredTemplateRevision>>(
                Revisions
                    .AsEnumerable()
                    .Reverse()
                    .Take(Math.Clamp(limit, 1, 100))
                    .ToArray());
        }
    }

    private static bool MatchesVersion(string state, int expectedVersion)
        => Entries.TryGetValue(state, out var current)
            ? current.Version == expectedVersion
            : expectedVersion == 0;

    private static void AddRevision(
        StoredDiagnosticConfiguration entry,
        string outcome,
        string correlationId)
        => Revisions.Add(new StoredTemplateRevision(
            entry.State,
            entry.Version,
            outcome,
            entry.UpdatedByUserId,
            correlationId,
            DateTime.UtcNow));
}
