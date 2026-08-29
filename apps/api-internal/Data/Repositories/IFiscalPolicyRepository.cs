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
    /// Le numero de version du regime ne correspond plus a celui que
    /// l'administrateur avait sous les yeux.
    /// </summary>
    VersionConflict,

    /// <summary>
    /// Une version du meme regime prend deja effet exactement au meme instant :
    /// la version applicable serait indeterminee.
    /// </summary>
    EffectiveDateTaken
}

/// <summary>
/// Mentions et versions lues ensemble, dans la meme unite de lecture.
/// </summary>
/// <remarks>
/// Assemblees par deux lectures separees, elles peuvent decrire deux instants
/// differents : l'administrateur recoit alors les mentions d'avant une mutation
/// concurrente avec le numero de version d'apres. Son ecran est coherent en
/// apparence, son prochain envoi part sur une version qu'il n'a jamais vue, et
/// le controle optimiste le laisse passer.
/// </remarks>
public sealed record FiscalPolicyAdminSnapshot(
    IReadOnlyList<StoredFiscalMention> Mentions,
    IReadOnlyDictionary<string, int> Versions);

public interface IFiscalPolicyRepository
{
    bool IsPersistent { get; }

    /// <summary>
    /// Mentions et versions dans une seule lecture coherente.
    /// </summary>
    Task<FiscalPolicyAdminSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredFiscalMention>> ListAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Numero de version courant de chaque regime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ce numero <b>ne redescend jamais</b>. Le decompte des mentions faisait
    /// office de version, mais une suppression le fait diminuer : apres
    /// « ajout, ajout, annulation, ajout », il retrouve sa valeur d'avant et un
    /// <c>expectedVersion</c> devenu obsolete redevient acceptable. L'ecran
    /// d'un administrateur qui n'a jamais vu la version intermediaire passe
    /// alors sans conflit — sur un texte qui s'imprime sur des factures.
    /// </para>
    /// <para>
    /// Un regime absent de la table n'a jamais ete versionne : sa version est
    /// alors le nombre de mentions qu'il porte.
    /// </para>
    /// </remarks>
    Task<IReadOnlyDictionary<string, int>> GetRegimeVersionsAsync(
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
    /// La verification et l'ecriture partagent donc la meme transaction et le
    /// meme verrou : celui de la ligne de version du regime. Verrouiller une
    /// ligne <b>presente</b> plutot qu'un intervalle vide est volontaire — le
    /// verrou d'intervalle n'existe qu'en REPEATABLE READ, et l'isolation du
    /// serveur n'est pas une garantie de cette application.
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
    /// <remarks>
    /// Incremente le numero de version du regime, dans la meme transaction. Une
    /// suppression est une modification comme une autre : ne pas la compter
    /// laisserait un <c>expectedVersion</c> anterieur redevenir valide.
    /// </remarks>
    Task<bool> TryDeleteScheduledAsync(
        string id,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
