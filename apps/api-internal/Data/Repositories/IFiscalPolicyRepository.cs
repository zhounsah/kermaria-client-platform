using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record StoredFiscalMention(
    string Id,
    string Regime,
    string Mention,
    DateTime EffectiveFromUtc,
    DateTime CreatedAtUtc,
    string? CreatedByUserId);

public interface IFiscalPolicyRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<StoredFiscalMention>> ListAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Ajoute une version. Retourne `false` si une version du meme regime prend
    /// deja effet exactement au meme instant : la version applicable serait
    /// alors indeterminee.
    /// </summary>
    Task<bool> TryAddAsync(
        StoredFiscalMention mention,
        string correlationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Supprime une version qui n'a pas encore pris effet. Une version deja
    /// appliquee n'est jamais supprimable : elle documente ce qui a ete imprime
    /// sur de vrais documents.
    /// </summary>
    Task<bool> TryDeleteScheduledAsync(
        string id,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
