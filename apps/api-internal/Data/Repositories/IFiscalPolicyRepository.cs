using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record StoredFiscalMention(
    string Id,
    string Regime,
    string Mention,
    DateTime EffectiveFromUtc,
    DateTime CreatedAtUtc,
    string? CreatedByUserId);

/// <summary>
/// Issue d'un ajout de mention.
/// </summary>
public enum FiscalMentionAddOutcome
{
    Added,

    /// <summary>
    /// Le nombre de versions du regime ne correspond plus a celui que
    /// l'administrateur avait sous les yeux.
    /// </summary>
    VersionConflict,

    /// <summary>
    /// Une version du meme regime prend deja effet exactement au meme instant :
    /// la version applicable serait indeterminee.
    /// </summary>
    EffectiveDateTaken
}

public interface IFiscalPolicyRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<StoredFiscalMention>> ListAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Verifie la version attendue et ajoute la mention dans la meme unite de
    /// travail.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le decompte servait auparavant de version, mais il etait lu sur une
    /// connexion distincte, avant l'insertion. Deux administrateurs partant du
    /// meme ecran lisaient donc tous les deux la meme version et inseraient
    /// tous les deux : la mention appliquee devenait celle de la date d'effet
    /// la plus proche, sans que ni l'un ni l'autre ne voie de conflit. Sur un
    /// texte qui s'imprime sur des factures, c'est un resultat faux et
    /// silencieux.
    /// </para>
    /// <para>
    /// La verification et l'ecriture doivent donc partager la meme transaction
    /// et le meme verrou sur le regime.
    /// </para>
    /// </remarks>
    Task<FiscalMentionAddOutcome> TryAddAsync(
        StoredFiscalMention mention,
        int expectedVersion,
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
