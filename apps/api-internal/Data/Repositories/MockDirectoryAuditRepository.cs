using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Journal d'ecritures d'annuaire en memoire.
///
/// Sans persistance, aucune ecriture n'est conservee. Renvoyer une liste vide
/// est la seule reponse honnete : fabriquer des entrees ferait croire que
/// l'annuaire a ete touche. La page annonce elle-meme que la persistance n'est
/// pas durable.
/// </summary>
public sealed class MockDirectoryAuditRepository : IDirectoryAuditRepository
{
    public bool IsPersistent => false;

    public Task<IReadOnlyList<DirectoryWriteEntry>> GetRecentWritesAsync(
        int limit,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<DirectoryWriteEntry>>([]);
}
