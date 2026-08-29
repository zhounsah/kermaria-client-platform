using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IDirectoryAuditRepository
{
    bool IsPersistent { get; }

    /// <summary>
    /// Dernieres ecritures d'annuaire tentees par API-INTERNAL
    /// (specification, 12.6).
    ///
    /// Ne renvoie que ce que cette application a demande : les ecritures faites
    /// par KoXo ne passent pas par cette table. Les presenter ici comme si elles
    /// y etaient donnerait une vue faussement complete de l'annuaire.
    /// </summary>
    Task<IReadOnlyList<DirectoryWriteEntry>> GetRecentWritesAsync(
        int limit,
        CancellationToken cancellationToken);
}
