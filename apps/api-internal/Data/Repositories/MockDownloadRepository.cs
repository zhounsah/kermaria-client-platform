using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockDownloadStore
{
    public object SyncRoot { get; } = new();

    public Dictionary<string, StoredDownloadCategory> Categories { get; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, StoredDownloadResource> Resources { get; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, StoredDownloadVisibilityRule> Rules { get; } =
        new(StringComparer.Ordinal);
}

public sealed class MockDownloadRepository : IDownloadRepository
{
    private readonly MockDownloadStore _store;

    public MockDownloadRepository(MockDownloadStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task SeedDefaultCategoriesAsync(
        IReadOnlyList<ValidatedDownloadCategory> categories,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            foreach (var category in categories)
            {
                if (_store.Categories.Values.Any(existing =>
                        string.Equals(
                            existing.Slug,
                            category.Slug,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var now = DateTime.UtcNow.ToString("O");
                _store.Categories[category.Id] = new StoredDownloadCategory(
                    category.Id,
                    category.Slug,
                    category.Title,
                    category.Description,
                    category.Status,
                    category.DisplayOrder,
                    now,
                    now);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredDownloadCategory>> GetCategoriesAsync(
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<StoredDownloadCategory>>(
                _store.Categories.Values
                    .OrderBy(category => category.DisplayOrder)
                    .ThenBy(category => category.Title, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    public Task<IReadOnlyList<StoredDownloadResource>> GetResourcesAsync(
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<StoredDownloadResource>>(
                _store.Resources.Values
                    .OrderBy(resource => resource.DisplayOrder)
                    .ThenBy(resource => resource.Title, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    public Task<IReadOnlyList<StoredDownloadVisibilityRule>> GetVisibilityRulesAsync(
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<StoredDownloadVisibilityRule>>(
                _store.Rules.Values
                    .OrderBy(rule => rule.ResourceId, StringComparer.Ordinal)
                    .ThenBy(rule => rule.TargetType, StringComparer.Ordinal)
                    .ThenBy(rule => rule.TargetValue, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    public Task<DownloadCategoryMutationResponse> CreateCategoryAsync(
        ValidatedDownloadCategory category,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var now = DateTime.UtcNow.ToString("O");
            _store.Categories[category.Id] = new StoredDownloadCategory(
                category.Id,
                category.Slug,
                category.Title,
                category.Description,
                category.Status,
                category.DisplayOrder,
                now,
                now);
            return Task.FromResult(
                new DownloadCategoryMutationResponse(
                    category.Id,
                    Changed: true,
                    now,
                    correlationId));
        }
    }

    public Task<DownloadCategoryMutationResponse> UpdateCategoryAsync(
        ValidatedDownloadCategory category,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Categories.TryGetValue(category.Id, out var current);
            var now = DateTime.UtcNow.ToString("O");
            var next = new StoredDownloadCategory(
                category.Id,
                category.Slug,
                category.Title,
                category.Description,
                category.Status,
                category.DisplayOrder,
                current?.CreatedAt ?? now,
                now);
            var changed = current is null || !Equals(current with
            {
                UpdatedAt = now
            }, next);
            _store.Categories[category.Id] = next;
            return Task.FromResult(
                new DownloadCategoryMutationResponse(
                    category.Id,
                    changed,
                    now,
                    correlationId));
        }
    }

    public Task<DownloadCategoryMutationResponse> DeleteCategoryAsync(
        string categoryId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Categories.Remove(categoryId);
            return Task.FromResult(
                new DownloadCategoryMutationResponse(
                    categoryId,
                    Changed: true,
                    DateTime.UtcNow.ToString("O"),
                    correlationId));
        }
    }

    public Task<DownloadResourceMutationResponse> CreateResourceAsync(
        ValidatedDownloadResource resource,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var now = DateTime.UtcNow.ToString("O");
            _store.Resources[resource.Id] = ToStored(resource, now, now);
            ReplaceRules(resource);
            return Task.FromResult(
                new DownloadResourceMutationResponse(
                    resource.Id,
                    Changed: true,
                    now,
                    correlationId));
        }
    }

    public Task<DownloadResourceMutationResponse> UpdateResourceAsync(
        ValidatedDownloadResource resource,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Resources.TryGetValue(resource.Id, out var current);
            var currentRules = _store.Rules.Values
                .Where(rule => rule.ResourceId == resource.Id)
                .ToArray();
            var now = DateTime.UtcNow.ToString("O");
            var next = ToStored(
                resource,
                current?.CreatedAt ?? now,
                now);
            var changed = current is null
                || !Equals(current with { UpdatedAt = now }, next)
                || !RulesEquivalent(currentRules, resource.Rules);

            _store.Resources[resource.Id] = next;
            ReplaceRules(resource);

            return Task.FromResult(
                new DownloadResourceMutationResponse(
                    resource.Id,
                    changed,
                    now,
                    correlationId));
        }
    }

    public Task<DownloadResourceMutationResponse> DeleteResourceAsync(
        string resourceId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Resources.Remove(resourceId);
            foreach (var ruleId in _store.Rules.Values
                         .Where(rule => rule.ResourceId == resourceId)
                         .Select(rule => rule.Id)
                         .ToArray())
            {
                _store.Rules.Remove(ruleId);
            }

            return Task.FromResult(
                new DownloadResourceMutationResponse(
                    resourceId,
                    Changed: true,
                    DateTime.UtcNow.ToString("O"),
                    correlationId));
        }
    }

    private void ReplaceRules(ValidatedDownloadResource resource)
    {
        foreach (var ruleId in _store.Rules.Values
                     .Where(rule => rule.ResourceId == resource.Id)
                     .Select(rule => rule.Id)
                     .ToArray())
        {
            _store.Rules.Remove(ruleId);
        }

        foreach (var rule in resource.Rules)
        {
            _store.Rules[rule.Id] = new StoredDownloadVisibilityRule(
                rule.Id,
                rule.ResourceId,
                rule.TargetType,
                rule.TargetValue);
        }
    }

    private static StoredDownloadResource ToStored(
        ValidatedDownloadResource resource,
        string createdAt,
        string updatedAt)
        => new(
            resource.Id,
            resource.CategoryId,
            resource.Title,
            resource.ShortDescription,
            resource.ResourceType,
            resource.SourceKind,
            resource.VisibilityMode,
            resource.Status,
            resource.ExternalUrl,
            resource.VersionLabel,
            resource.InstallationInstructions,
            resource.DisplayOrder,
            resource.InternalFile,
            createdAt,
            updatedAt);

    private static bool RulesEquivalent(
        IReadOnlyList<StoredDownloadVisibilityRule> currentRules,
        IReadOnlyList<ValidatedDownloadVisibilityRule> nextRules)
    {
        var current = currentRules
            .Select(rule => $"{rule.TargetType}:{rule.TargetValue}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var next = nextRules
            .Select(rule => $"{rule.TargetType}:{rule.TargetValue}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return current.SequenceEqual(next, StringComparer.Ordinal);
    }
}
