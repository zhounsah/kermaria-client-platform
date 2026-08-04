using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IClientSolutionRepository
{
    bool IsPersistent { get; }

    Task<StoredClientSolutionPortalSettings?> GetSettingsAsync(
        CancellationToken cancellationToken);

    Task<ClientSolutionPortalMutationResponse> UpsertSettingsAsync(
        ValidatedClientSolutionPortalSettings settings,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredClientSolution>> GetSolutionsAsync(
        CancellationToken cancellationToken);

    Task<StoredClientSolution?> GetSolutionAsync(
        string id,
        CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(
        string slug,
        string? excludedId,
        CancellationToken cancellationToken);

    Task<ClientSolutionMutationResponse> CreateSolutionAsync(
        ValidatedClientSolution solution,
        string correlationId,
        CancellationToken cancellationToken);

    Task<ClientSolutionMutationResponse> UpdateSolutionAsync(
        ValidatedClientSolution solution,
        string correlationId,
        CancellationToken cancellationToken);

    Task<ClientSolutionMutationResponse> DeleteSolutionAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);

    Task<StoredClientSolutionLogoContent?> GetLogoAsync(
        string id,
        CancellationToken cancellationToken);

    Task<ClientSolutionMutationResponse> SaveLogoAsync(
        string id,
        StoredClientSolutionLogoContent logo,
        string correlationId,
        CancellationToken cancellationToken);

    Task<ClientSolutionMutationResponse> DeleteLogoAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);
}
