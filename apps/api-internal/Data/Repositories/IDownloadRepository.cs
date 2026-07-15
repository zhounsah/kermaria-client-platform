using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IDownloadRepository
{
    bool IsPersistent { get; }

    Task SeedDefaultCategoriesAsync(
        IReadOnlyList<ValidatedDownloadCategory> categories,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredDownloadCategory>> GetCategoriesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredDownloadResource>> GetResourcesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredDownloadVisibilityRule>> GetVisibilityRulesAsync(
        CancellationToken cancellationToken);

    Task<DownloadCategoryMutationResponse> CreateCategoryAsync(
        ValidatedDownloadCategory category,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadCategoryMutationResponse> UpdateCategoryAsync(
        ValidatedDownloadCategory category,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadCategoryMutationResponse> DeleteCategoryAsync(
        string categoryId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadResourceMutationResponse> CreateResourceAsync(
        ValidatedDownloadResource resource,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadResourceMutationResponse> UpdateResourceAsync(
        ValidatedDownloadResource resource,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadResourceMutationResponse> DeleteResourceAsync(
        string resourceId,
        string correlationId,
        CancellationToken cancellationToken);
}
