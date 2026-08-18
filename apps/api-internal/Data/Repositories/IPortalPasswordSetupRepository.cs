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

public static class PortalPasswordSetupCodes
{
    public const string Consumed = "PASSWORD_SET";
    public const string TokenInvalid = "TOKEN_INVALID";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string TokenAlreadyUsed = "TOKEN_ALREADY_USED";
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
    Task<PortalPasswordSetupConsumption> ConsumeAndSetPasswordAsync(
        string tokenHash,
        Func<string, string> hashPasswordForUser,
        CancellationToken cancellationToken);
}
