using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Lecture et actions d'exploitation sur les abonnements Billing V2.
/// </summary>
/// <remarks>
/// <para>
/// Remplace l'ancien <c>ISubscriptionService</c>, qui pilotait la table
/// <c>subscriptions</c>. Il n'existe plus qu'un seul systeme d'abonnement :
/// <c>billing_v2_subscriptions</c>. Les montants ne sont jamais recalcules ici
/// — ils viennent de la projection, qui applique l'autorite tarifaire V2.
/// </para>
/// <para>
/// L'annulation ne touche <b>que</b> l'axe de statut. Ni les items, ni les
/// evenements de facturation, ni les verrous de prix ne sont modifies :
/// un <c>BillingEvent</c> est immuable et reste l'autorite du montant deja
/// facture. Effacer un item pour « nettoyer » un abonnement annule reecrirait
/// l'histoire financiere.
/// </para>
/// </remarks>
public interface IBillingV2SubscriptionAdministrationService
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<SubscriptionSummary>> GetClientSubscriptionsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionSummary>> GetAdminSubscriptionsAsync(
        CancellationToken cancellationToken);

    Task<AdminSubscriptionDetail> GetAdminSubscriptionDetailAsync(
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> ClientCancelAsync(
        PortalSessionContext session,
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> AdminCancelAsync(
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionProvisioningSummary> ReconcileProvisioningAsync(
        string subscriptionId,
        string actionType,
        string correlationId,
        string? requestedByUserId,
        IReadOnlyList<string>? targetUserSamAccountNames,
        CancellationToken cancellationToken);
}

public sealed class BillingV2SubscriptionAdministrationService
    : IBillingV2SubscriptionAdministrationService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly IBillingV2PortalSubscriptionProjection _projection;
    private readonly ICommercialDocumentRepository _documents;
    private readonly IBillingV2SubscriptionProvisioningManager _provisioning;
    private readonly IBillingV2SubscriptionCancellationService _cancellation;

    public BillingV2SubscriptionAdministrationService(
        SqlRuntimeConfiguration sql,
        IBillingV2PortalSubscriptionProjection projection,
        ICommercialDocumentRepository documents,
        IBillingV2SubscriptionProvisioningManager provisioning,
        IBillingV2SubscriptionCancellationService cancellation)
    {
        _sql = sql;
        _projection = projection;
        _documents = documents;
        _provisioning = provisioning;
        _cancellation = cancellation;
    }

    public bool IsPersistent => _sql.IsPersistent;

    public Task<IReadOnlyList<SubscriptionSummary>> GetClientSubscriptionsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => _projection.GetClientSubscriptionsAsync(
            session.CustomerId,
            cancellationToken);

    public Task<IReadOnlyList<SubscriptionSummary>> GetAdminSubscriptionsAsync(
        CancellationToken cancellationToken)
        => _projection.GetAdminSubscriptionsAsync(cancellationToken);

    public async Task<AdminSubscriptionDetail> GetAdminSubscriptionDetailAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscription = await GetRequiredAdminSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        var documents = await _documents.GetDocumentsForSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        var provisioning = await _provisioning.GetSummaryAsync(
            subscription,
            cancellationToken);
        return new AdminSubscriptionDetail(subscription, documents, provisioning);
    }

    public async Task<SubscriptionSummary> ClientCancelAsync(
        PortalSessionContext session,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscriptions = await _projection.GetClientSubscriptionsAsync(
            session.CustomerId,
            cancellationToken);
        var subscription = subscriptions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, subscriptionId, StringComparison.Ordinal))
            ?? throw new PortalDataNotFoundException();

        // Cote client, un contrat deja actif s'arrete a la fin de la periode
        // payee : il a ete regle, il doit etre servi jusqu'a son terme. Le
        // client ne peut donc jamais exiger l'immediat. La convergence
        // fournisseur est portee par le service de resiliation.
        await _cancellation.RequestCancellationAsync(
            subscriptionId,
            subscription.Status,
            forceImmediate: false,
            actorReference: $"portal_user:{session.UserId}",
            cancellationToken);

        return await GetRequiredClientSubscriptionAsync(
            session.CustomerId,
            subscriptionId,
            cancellationToken);
    }

    public async Task<SubscriptionSummary> AdminCancelAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscription = await GetRequiredAdminSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        // L'administration peut couper une periode deja payee : c'est une
        // decision humaine, tracee dans le journal d'audit Billing V2.
        await _cancellation.RequestCancellationAsync(
            subscriptionId,
            subscription.Status,
            forceImmediate: true,
            actorReference: "internal_admin",
            cancellationToken);
        return await GetRequiredAdminSubscriptionAsync(
            subscriptionId,
            cancellationToken);
    }

    public async Task<SubscriptionProvisioningSummary> ReconcileProvisioningAsync(
        string subscriptionId,
        string actionType,
        string correlationId,
        string? requestedByUserId,
        IReadOnlyList<string>? targetUserSamAccountNames,
        CancellationToken cancellationToken)
    {
        var subscription = await GetRequiredAdminSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        return await _provisioning.ReconcileAsync(
            subscription,
            actionType,
            correlationId,
            requestedByUserId,
            targetUserSamAccountNames,
            cancellationToken);
    }

    // La liste d'administration reste courte a l'echelle du parc ; la relire
    // pour extraire un abonnement evite de dupliquer la requete de projection,
    // qui porte deja toute l'autorite tarifaire.
    private async Task<SubscriptionSummary> GetRequiredAdminSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscriptions = await _projection.GetAdminSubscriptionsAsync(
            cancellationToken);
        return subscriptions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, subscriptionId, StringComparison.Ordinal))
            ?? throw new PortalDataNotFoundException();
    }

    private async Task<SubscriptionSummary> GetRequiredClientSubscriptionAsync(
        string customerId,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscriptions = await _projection.GetClientSubscriptionsAsync(
            customerId,
            cancellationToken);
        return subscriptions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, subscriptionId, StringComparison.Ordinal))
            ?? throw new PortalDataNotFoundException();
    }
}
