using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IEditorialRepository
{
    bool IsPersistent { get; }

    Task<bool> HasAdminPermissionAsync(
        string userId,
        string permissionCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Nombre d'attributions explicites par code de permission. Un code absent
    /// du resultat n'a aucune attribution : l'acces y reste ouvert par
    /// amorcage, et c'est precisement ce que la page de permissions doit
    /// rendre visible.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetAdminPermissionGrantCountsAsync(
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EditorialCategory>> GetCategoriesAsync(
        string? contentType,
        CancellationToken cancellationToken);

    Task<EditorialCategory> UpsertCategoryAsync(
        EditorialCategory category,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EditorialContentSummary>> GetContentListAsync(
        string? contentType,
        string? status,
        string? query,
        CancellationToken cancellationToken);

    Task<EditorialContentDetail?> GetContentAsync(
        string id,
        CancellationToken cancellationToken);

    Task<EditorialContentDetail?> GetContentBySlugAsync(
        string contentType,
        string slug,
        bool publicOnly,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EditorialContentDetail>> GetFaqByScopeAsync(
        string scope,
        CancellationToken cancellationToken);

    Task<EditorialRedirect?> GetRedirectAsync(
        string oldPath,
        CancellationToken cancellationToken);

    Task<EditorialContentDetail> UpsertContentAsync(
        EditorialContentDetail content,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EditorialRevisionSummary>> GetRevisionsAsync(
        string contentId,
        CancellationToken cancellationToken);

    Task<EditorialRevisionDetail?> GetRevisionAsync(
        string revisionId,
        CancellationToken cancellationToken);

    Task AddRevisionAsync(
        string contentId,
        string action,
        EditorialContentDetail snapshot,
        string? actorUserId,
        CancellationToken cancellationToken);

    Task AddRedirectAsync(
        string contentId,
        string contentType,
        string oldPath,
        string newPath,
        string? actorUserId,
        CancellationToken cancellationToken);
}
