namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Ce qu'il faut savoir d'un utilisateur portail pour viser sa fiche KoXo.
/// </summary>
/// <param name="KoxoUniqueIdentifier">
/// <c>CLI-NNNNNN</c>, reporte par KoXo dans l'attribut AD
/// <c>employeeNumber</c>. Nullable : un utilisateur peut exister sans qu'aucun
/// identifiant ne lui ait encore ete alloue, et c'est un blocage, pas une
/// valeur a fabriquer.
/// </param>
public sealed record BillingV2KoxoPortalUserRecord(
    string PortalUserId,
    string CustomerId,
    string CustomerReference,
    string? KoxoUniqueIdentifier,
    bool IsDemo,
    string? KoxoGroupReference);

/// <summary>
/// Ce qu'il faut savoir d'un client pour nommer son OU de groupe secondaire.
/// </summary>
public sealed record BillingV2KoxoCustomerRecord(
    string CustomerId,
    string CustomerReference,
    bool IsDemo,
    string? KoxoGroupReference);

/// <summary>
/// Lectures ciblees, en lecture seule, pour resoudre une cible de stockage
/// KoXo.
/// </summary>
/// <remarks>
/// <para>
/// Volontairement distinct de <see cref="IKoxoRepository"/>. Celui-ci porte la
/// politique de population du CSV global — quels comptes partent en
/// synchronisation, avec quelles preconditions d'etat civil, et avec quelle
/// tolerance pour les essais de demonstration. Cette politique n'a rien a dire
/// sur « ou poser le quota de cet abonnement » : s'en servir ferait dependre le
/// ciblage d'un stockage de regles d'export sans rapport, et un changement de
/// l'une casserait l'autre en silence.
/// </para>
/// <para>
/// Aucune methode d'ecriture n'est declaree ici, et aucune ne doit l'etre : la
/// resolution d'une cible ne cree ni identite, ni lien, ni OU.
/// </para>
/// </remarks>
public interface IBillingV2KoxoTargetingRepository
{
    bool IsPersistent { get; }

    /// <summary>
    /// Lit un utilisateur portail precis d'un client precis.
    /// </summary>
    /// <remarks>
    /// La requete est bornee par les deux identifiants exacts : un utilisateur
    /// d'un autre client ne remonte pas, et aucune recherche approximative
    /// n'est proposee. Un utilisateur inexistant et un utilisateur appartenant
    /// a un autre client sont donc indistinguables ici, ce qui est voulu :
    /// distinguer les deux reviendrait a confirmer l'existence d'une ligne
    /// d'un autre client.
    /// </remarks>
    Task<BillingV2KoxoPortalUserRecord?> FindPortalUserAsync(
        string customerId,
        string portalUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lit un client precis par son identifiant exact.
    /// </summary>
    Task<BillingV2KoxoCustomerRecord?> FindCustomerAsync(
        string customerId,
        CancellationToken cancellationToken);
}
