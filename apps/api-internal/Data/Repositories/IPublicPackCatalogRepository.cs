using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IPublicPackCatalogRepository
{
    bool IsPersistent { get; }

    Task<PublicPackCatalogContent?> GetAsync(CancellationToken cancellationToken);

    Task<PublicPackCatalogMutationResponse> UpsertAsync(
        ValidatedPublicPackCatalogContent content,
        string correlationId,
        CancellationToken cancellationToken);
}
