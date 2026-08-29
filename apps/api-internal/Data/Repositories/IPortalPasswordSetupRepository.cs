namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>Emission d'un jeton de definition de mot de passe.</summary>
public sealed record PortalPasswordSetupIssue(
    string Id,
    string PortalUserId,
    string Purpose,
    string TokenHash,
    DateTime ExpiresAtUtc);

/// <summary>Jeton retrouve, sans consommation.</summary>
public sealed record PortalPasswordSetupTarget(
    string Id,
    string PortalUserId,
    string Purpose,
    DateTime ExpiresAtUtc,
    bool IsConsumed,
    bool IsSuperseded)
{
    public bool IsExpired(DateTime nowUtc) => ExpiresAtUtc <= nowUtc;

    public bool IsUsable(DateTime nowUtc)
        => !IsConsumed && !IsSuperseded && !IsExpired(nowUtc);
}

/// <summary>
/// Secret scelle, pret a etre ecrit sans jamais transiter en clair.
/// </summary>
/// <remarks>
/// Le scellement est fait par le magasin de mots de passe en attente, qui seul
/// detient la cle. Ce qui traverse ensuite les couches n'est plus qu'un chiffre
/// authentifie : ni le depot, ni la transaction, ni un journal ne voient le mot
/// de passe.
/// </remarks>
public sealed record PortalPasswordSecret(
    string Ciphertext,
    string KeyId,
    DateTime ExpiresAtUtc);

/// <summary>
/// Ce qui doit etre ecrit <b>dans la meme transaction</b> que la consommation
/// du jeton.
/// </summary>
/// <remarks>
/// <para>
/// Sans cela, le chemin comportait une fenetre irrattrapable : le jeton etait
/// consomme et le mot de passe pose dans une premiere transaction, puis le
/// secret destine a KoXo publie dans une seconde. Un arret entre les deux
/// laissait un jeton mort — il est a usage unique — et aucun moyen de
/// retrouver le mot de passe, qui n'existe en clair qu'a cet instant. La
/// personne se connectait au portail mais n'avait jamais ni VPN, ni RDS, ni
/// stockage, sans aucune erreur visible.
/// </para>
/// <para>
/// <see cref="Secret"/> est nul quand KoXo n'est pas maitre de l'annuaire :
/// il n'y a alors aucun relais a alimenter, mais la transition du cycle de vie
/// reste due.
/// </para>
/// </remarks>
public sealed record PortalPasswordHandoff(
    string PortalUserId,
    string LifecycleId,
    DateTime AtUtc,
    PortalPasswordSecret? Secret);

/// <summary>
/// Point d'attache du secret scelle, reserve aux depots <b>mock</b>.
/// </summary>
/// <remarks>
/// En persistance reelle le secret est ecrit par le depot SQL a l'interieur de
/// la transaction. En mock il n'y a pas de transaction : c'est le magasin en
/// memoire qui joue ce role, et l'attache n'a lieu qu'au moment ou la
/// transaction simulee reussit.
/// </remarks>
public interface IKoxoPendingPasswordSealSink
{
    void AttachSealed(string portalUserId, PortalPasswordSecret secret);

    /// <summary>
    /// Defait un attachement, comme le ferait un ROLLBACK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Indispensable des lors que l'unite de travail simulee peut echouer
    /// <b>apres</b> l'attache. Sans cela le mock laisse un secret en attente
    /// alors que le mot de passe portail a ete repris : KoXo appliquerait plus
    /// tard a l'annuaire un mot de passe que le portail ne connait pas — la
    /// divergence exacte que la transaction reelle interdit.
    /// </para>
    /// <para>
    /// L'etat precedent est restaure, pas simplement efface : un secret en
    /// attente d'un changement anterieur doit survivre a l'annulation du
    /// suivant, comme le ferait le ROLLBACK d'un
    /// <c>INSERT ... ON DUPLICATE KEY UPDATE</c>.
    /// </para>
    /// </remarks>
    void DiscardSealed(string portalUserId, PortalPasswordSecret secret);
}

/// <summary>
/// Transition du cycle de vie a effectuer dans la meme transaction, reservee
/// aux depots <b>mock</b>.
/// </summary>
public interface IBillingV2UserIdentityTransitionSink
{
    bool TryMarkKoxoPending(
        string lifecycleId,
        string portalUserId,
        DateTime atUtc);
}

public static class PortalPasswordSetupCodes
{
    public const string Consumed = "PASSWORD_SET";
    public const string TokenInvalid = "TOKEN_INVALID";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string TokenAlreadyUsed = "TOKEN_ALREADY_USED";

    /// <summary>
    /// Le relais du mot de passe ou la transition du cycle de vie a echoue :
    /// rien n'a ete conserve, le jeton reste utilisable.
    /// </summary>
    public const string HandoffFailed = "PASSWORD_HANDOFF_FAILED";
}

/// <summary>
/// Resultat de la consommation atomique d'un jeton.
/// </summary>
public sealed record PortalPasswordSetupConsumption(
    string Code,
    string? PortalUserId)
{
    public bool Succeeded => string.Equals(
        Code,
        PortalPasswordSetupCodes.Consumed,
        StringComparison.Ordinal);
}

/// <summary>
/// Jetons de definition de mot de passe rattaches a un utilisateur portail.
/// </summary>
/// <remarks>
/// <para>
/// Volontairement independant de <c>signup_pending</c>. Dans le parcours
/// d'inscription, le jeton, l'etat commercial de la demande et l'etat du
/// provisioning annuaire cohabitent dans une meme ligne : le jeton n'y est donc
/// pas reutilisable pour un utilisateur qui n'est pas issu d'une inscription,
/// et detourner cette table pour une place d'abonnement y injecterait une
/// demande d'inscription qui n'a jamais existe.
/// </para>
/// <para>
/// Cette table ne connait qu'un utilisateur portail et un <c>purpose</c>
/// descriptif. Elle ne porte aucun etat KoXo : la materialisation annuaire est
/// suivie par <c>billing_v2_user_identity_provisioning</c>, qui est un tout
/// autre sujet.
/// </para>
/// </remarks>
public interface IPortalPasswordSetupRepository
{
    bool IsPersistent { get; }

    /// <summary>
    /// Emet un jeton et <b>invalide immediatement</b> les jetons encore ouverts
    /// du meme utilisateur pour le meme <paramref name="issue"/>.Purpose.
    /// </summary>
    /// <remarks>
    /// Le renouvellement d'un lien doit rendre le precedent inutilisable :
    /// laisser deux liens valides multiplierait les fenetres d'usage d'un
    /// secret a usage unique.
    /// </remarks>
    Task IssueAsync(
        PortalPasswordSetupIssue issue,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrouve un jeton par son condensat, <b>sans</b> le consommer.
    /// </summary>
    /// <remarks>
    /// Reserve a la validation d'un lien avant d'afficher le formulaire :
    /// consommer ici ferait disparaitre le jeton avant que l'utilisateur ait
    /// saisi quoi que ce soit.
    /// </remarks>
    Task<PortalPasswordSetupTarget?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Consomme le jeton et ecrit le mot de passe, en une seule transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La consommation est un UPDATE conditionnel dont on exige exactement une
    /// ligne affectee : deux requetes concurrentes portant le meme jeton ne
    /// peuvent pas reussir toutes les deux, et aucune verification prealable
    /// separee ne peut etre contournee par une course.
    /// </para>
    /// <para>
    /// Le condensat du mot de passe est calcule par
    /// <paramref name="hashPasswordForUser"/> a l'interieur de la transaction :
    /// il depend de l'identifiant de l'utilisateur, qui n'est connu qu'apres
    /// resolution du jeton.
    /// </para>
    /// </remarks>
    /// <param name="handoff">
    /// Ce qui doit etre ecrit dans la <b>meme</b> transaction : le secret
    /// scelle destine a KoXo et la transition du cycle de vie. Nul pour un
    /// utilisateur sans cycle de vie Billing V2, ou le comportement se limite
    /// a la consommation et au mot de passe.
    /// <para>
    /// Aucun appel reseau ici : le declenchement KoXo est fait par l'appelant
    /// <b>apres</b> le COMMIT. Tenir une transaction ouverte pendant un appel
    /// sortant exposerait la base a la latence d'un tiers.
    /// </para>
    /// </param>
    /// <param name="expectedPurpose">
    /// <c>purpose</c> exige du jeton, verifie <b>sous le meme verrou</b> que la
    /// consommation.
    /// <para>
    /// Le controler avant, sur une lecture separee, ne prouverait rien : entre
    /// la lecture et la consommation, rien n'empeche que le jeton ait change.
    /// Et un jeton emis pour un autre parcours consomme ici poserait un mot de
    /// passe dans un flux dont ni les regles de validation ni les invariants
    /// n'ont ete verifies.
    /// </para>
    /// </param>
    Task<PortalPasswordSetupConsumption> ConsumeAndSetPasswordAsync(
        string tokenHash,
        string expectedPurpose,
        Func<string, string> hashPasswordForUser,
        PortalPasswordHandoff? handoff,
        CancellationToken cancellationToken);
}
