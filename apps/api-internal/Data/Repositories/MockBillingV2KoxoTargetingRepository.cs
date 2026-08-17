namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Repli non persistant : ne connait aucun client et aucun utilisateur.
/// </summary>
/// <remarks>
/// Volontairement vide plutot que peuple de donnees fictives. Un quota KoXo se
/// pose sur un objet d'annuaire reel ; fabriquer ici un client et un
/// <c>employeeNumber</c> plausibles ferait aboutir une resolution qui ne
/// correspond a rien, et la suite mock declarerait « cible resolue » sans qu'
/// aucune fiche n'existe. Renvoyer <c>null</c> laisse le service echouer de
/// maniere explicite, ce qui est le comportement correct hors base reelle.
/// </remarks>
public sealed class MockBillingV2KoxoTargetingRepository
    : IBillingV2KoxoTargetingRepository
{
    public bool IsPersistent => false;

    public Task<BillingV2KoxoPortalUserRecord?> FindPortalUserAsync(
        string customerId,
        string portalUserId,
        CancellationToken cancellationToken)
        => Task.FromResult<BillingV2KoxoPortalUserRecord?>(null);

    public Task<BillingV2KoxoCustomerRecord?> FindCustomerAsync(
        string customerId,
        CancellationToken cancellationToken)
        => Task.FromResult<BillingV2KoxoCustomerRecord?>(null);
}
