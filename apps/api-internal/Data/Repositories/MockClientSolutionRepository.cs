using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockClientSolutionStore
{
    public object SyncRoot { get; } = new();

    public StoredClientSolutionPortalSettings? Settings { get; set; }

    public Dictionary<string, StoredClientSolution> Solutions { get; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, StoredClientSolutionLogoContent> Logos { get; } =
        new(StringComparer.Ordinal);
}

public sealed class MockClientSolutionRepository : IClientSolutionRepository
{
    private readonly MockClientSolutionStore _store;

    public MockClientSolutionRepository(MockClientSolutionStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<StoredClientSolutionPortalSettings?> GetSettingsAsync(
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(_store.Settings);
        }
    }

    public Task<ClientSolutionPortalMutationResponse> UpsertSettingsAsync(
        ValidatedClientSolutionPortalSettings settings,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var current = _store.Settings;
            var now = DateTime.UtcNow.ToString("O");
            _store.Settings = new StoredClientSolutionPortalSettings(
                settings.Eyebrow,
                settings.Title,
                settings.Description,
                settings.FooterNote,
                now);

            var changed = current is null
                || current.Eyebrow != settings.Eyebrow
                || current.Title != settings.Title
                || current.Description != settings.Description
                || current.FooterNote != settings.FooterNote;

            return Task.FromResult(
                new ClientSolutionPortalMutationResponse(
                    changed,
                    now,
                    correlationId));
        }
    }

    public Task<IReadOnlyList<StoredClientSolution>> GetSolutionsAsync(
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<StoredClientSolution>>(
                _store.Solutions.Values
                    .OrderBy(solution => solution.DisplayOrder)
                    .ThenBy(solution => solution.Title, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    public Task<StoredClientSolution?> GetSolutionAsync(
        string id,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Solutions.TryGetValue(id, out var solution)
                    ? solution
                    : null);
        }
    }

    public Task<bool> SlugExistsAsync(
        string slug,
        string? excludedId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Solutions.Values.Any(solution =>
                    string.Equals(solution.Slug, slug, StringComparison.Ordinal)
                    && !string.Equals(
                        solution.Id,
                        excludedId,
                        StringComparison.Ordinal)));
        }
    }

    public Task<ClientSolutionMutationResponse> CreateSolutionAsync(
        ValidatedClientSolution solution,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var now = DateTime.UtcNow.ToString("O");
            _store.Solutions[solution.Id] = ToStored(solution, null, now, now);

            return Task.FromResult(
                new ClientSolutionMutationResponse(
                    solution.Id,
                    Changed: true,
                    now,
                    correlationId));
        }
    }

    public Task<ClientSolutionMutationResponse> UpdateSolutionAsync(
        ValidatedClientSolution solution,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var now = DateTime.UtcNow.ToString("O");
            _store.Solutions.TryGetValue(solution.Id, out var current);
            _store.Solutions[solution.Id] = ToStored(
                solution,
                current?.Logo,
                current?.CreatedAt ?? now,
                now);

            var changed = current is null
                || current.Slug != solution.Slug
                || current.Title != solution.Title
                || current.Tagline != solution.Tagline
                || current.TargetUrl != solution.TargetUrl
                || current.OpensInNewTab != solution.OpensInNewTab
                || current.Status != solution.Status
                || current.DisplayOrder != solution.DisplayOrder;

            return Task.FromResult(
                new ClientSolutionMutationResponse(
                    solution.Id,
                    changed,
                    now,
                    correlationId));
        }
    }

    public Task<ClientSolutionMutationResponse> DeleteSolutionAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var removed = _store.Solutions.Remove(id);
            _store.Logos.Remove(id);

            return Task.FromResult(
                new ClientSolutionMutationResponse(
                    id,
                    removed,
                    DateTime.UtcNow.ToString("O"),
                    correlationId));
        }
    }

    public Task<StoredClientSolutionLogoContent?> GetLogoAsync(
        string id,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Logos.TryGetValue(id, out var logo) ? logo : null);
        }
    }

    public Task<ClientSolutionMutationResponse> SaveLogoAsync(
        string id,
        StoredClientSolutionLogoContent logo,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            if (!_store.Solutions.TryGetValue(id, out var current))
            {
                return Task.FromResult(
                    new ClientSolutionMutationResponse(
                        id,
                        Changed: false,
                        DateTime.UtcNow.ToString("O"),
                        correlationId));
            }

            var now = DateTime.UtcNow.ToString("O");
            _store.Logos[id] = logo;
            _store.Solutions[id] = current with
            {
                Logo = new StoredClientSolutionLogo(
                    logo.ContentType,
                    logo.OriginalName,
                    logo.Bytes.Length,
                    now),
                UpdatedAt = now
            };

            return Task.FromResult(
                new ClientSolutionMutationResponse(
                    id,
                    Changed: true,
                    now,
                    correlationId));
        }
    }

    public Task<ClientSolutionMutationResponse> DeleteLogoAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var removed = _store.Logos.Remove(id);
            var now = DateTime.UtcNow.ToString("O");
            if (_store.Solutions.TryGetValue(id, out var current))
            {
                _store.Solutions[id] = current with
                {
                    Logo = null,
                    UpdatedAt = now
                };
            }

            return Task.FromResult(
                new ClientSolutionMutationResponse(
                    id,
                    removed,
                    now,
                    correlationId));
        }
    }

    private static StoredClientSolution ToStored(
        ValidatedClientSolution solution,
        StoredClientSolutionLogo? logo,
        string createdAt,
        string updatedAt)
        => new(
            solution.Id,
            solution.Slug,
            solution.Title,
            solution.Tagline,
            solution.TargetUrl,
            solution.OpensInNewTab,
            solution.Status,
            solution.DisplayOrder,
            logo,
            createdAt,
            updatedAt);
}
