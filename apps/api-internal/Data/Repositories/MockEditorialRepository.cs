using System.Text.Json;
using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockEditorialStore
{
    public object SyncRoot { get; } = new();

    public Dictionary<string, EditorialCategory> Categories { get; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, EditorialContentDetail> Contents { get; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, List<EditorialRevisionDetail>> Revisions { get; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, EditorialRedirect> Redirects { get; } =
        new(StringComparer.Ordinal);
}

public sealed class MockEditorialRepository : IEditorialRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web);

    private readonly MockEditorialStore _store;

    public MockEditorialRepository(MockEditorialStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<bool> HasAdminPermissionAsync(
        string userId,
        string permissionCode,
        CancellationToken cancellationToken)
        => Task.FromResult(true);

    public Task<IReadOnlyList<EditorialCategory>> GetCategoriesAsync(
        string? contentType,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<EditorialCategory>>(
                _store.Categories.Values
                    .Where(category => contentType is null
                        || category.ContentType == contentType)
                    .OrderBy(category => category.SortOrder)
                    .ThenBy(category => category.Name, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    public Task<EditorialCategory> UpsertCategoryAsync(
        EditorialCategory category,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Categories[category.Id] = category;
            return Task.FromResult(category);
        }
    }

    public Task<IReadOnlyList<EditorialContentSummary>> GetContentListAsync(
        string? contentType,
        string? status,
        string? query,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var normalizedQuery = query?.Trim().ToLowerInvariant();
            return Task.FromResult<IReadOnlyList<EditorialContentSummary>>(
                _store.Contents.Values
                    .Where(content => contentType is null
                        || content.ContentType == contentType)
                    .Where(content => status is null || content.Status == status)
                    .Where(content => normalizedQuery is null
                        || content.Title.ToLowerInvariant().Contains(normalizedQuery)
                        || (content.Summary ?? string.Empty)
                            .ToLowerInvariant()
                            .Contains(normalizedQuery)
                        || content.BodyMarkdown
                            .ToLowerInvariant()
                            .Contains(normalizedQuery))
                    .OrderBy(content => content.SortOrder)
                    .ThenByDescending(content => content.UpdatedAt)
                    .Select(ToSummary)
                    .ToArray());
        }
    }

    public Task<EditorialContentDetail?> GetContentAsync(
        string id,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Contents.TryGetValue(id, out var content)
                    ? content
                    : null);
        }
    }

    public Task<EditorialContentDetail?> GetContentBySlugAsync(
        string contentType,
        string slug,
        bool publicOnly,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Contents.Values.FirstOrDefault(content =>
                    content.ContentType == contentType
                    && content.Slug == slug
                    && (!publicOnly || IsPublic(content))));
        }
    }

    public Task<IReadOnlyList<EditorialContentDetail>> GetFaqByScopeAsync(
        string scope,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<EditorialContentDetail>>(
                _store.Contents.Values
                    .Where(content => content.ContentType == EditorialContentTypes.Faq)
                    .Where(IsPublic)
                    .Where(content => content.FaqScopes.Contains(scope))
                    .OrderBy(content => content.SortOrder)
                    .ThenBy(content => content.Title, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    public Task<EditorialRedirect?> GetRedirectAsync(
        string oldPath,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Redirects.TryGetValue(oldPath, out var redirect)
                    ? redirect
                    : null);
        }
    }

    public Task<EditorialContentDetail> UpsertContentAsync(
        EditorialContentDetail content,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Contents[content.Id] = content;
            return Task.FromResult(content);
        }
    }

    public Task<IReadOnlyList<EditorialRevisionSummary>> GetRevisionsAsync(
        string contentId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<EditorialRevisionSummary>>(
                _store.Revisions.TryGetValue(contentId, out var revisions)
                    ? revisions
                        .OrderByDescending(revision => revision.VersionNumber)
                        .Select(revision => (EditorialRevisionSummary)revision)
                        .ToArray()
                    : []);
        }
    }

    public Task<EditorialRevisionDetail?> GetRevisionAsync(
        string revisionId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Revisions.Values
                    .SelectMany(revisions => revisions)
                    .FirstOrDefault(revision => revision.Id == revisionId));
        }
    }

    public Task AddRevisionAsync(
        string contentId,
        string action,
        EditorialContentDetail snapshot,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            if (!_store.Revisions.TryGetValue(contentId, out var revisions))
            {
                revisions = [];
                _store.Revisions[contentId] = revisions;
            }

            var createdAt = DateTime.UtcNow.ToString("O");
            revisions.Add(new EditorialRevisionDetail(
                Guid.NewGuid().ToString("D"),
                contentId,
                revisions.Count + 1,
                action,
                createdAt,
                actorUserId,
                Clone(snapshot)));
        }

        return Task.CompletedTask;
    }

    public Task AddRedirectAsync(
        string contentId,
        string contentType,
        string oldPath,
        string newPath,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            if (oldPath == newPath || _store.Redirects.ContainsKey(oldPath))
            {
                return Task.CompletedTask;
            }

            _store.Redirects[oldPath] = new EditorialRedirect(
                Guid.NewGuid().ToString("D"),
                contentType,
                oldPath,
                newPath,
                DateTime.UtcNow.ToString("O"));
        }

        return Task.CompletedTask;
    }

    private static bool IsPublic(EditorialContentDetail content)
        => content.Status == EditorialContentStatuses.Published
            && content.PublishedAt is not null
            && !content.NoIndex;

    private static EditorialContentSummary ToSummary(EditorialContentDetail content)
        => content;

    private static EditorialContentDetail Clone(EditorialContentDetail content)
        => JsonSerializer.Deserialize<EditorialContentDetail>(
            JsonSerializer.Serialize(content, JsonOptions),
            JsonOptions) ?? content;
}
