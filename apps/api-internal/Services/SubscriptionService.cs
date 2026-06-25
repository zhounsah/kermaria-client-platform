using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public sealed record SubscriptionLookup(
    CommercialOfferSummary Offer,
    string PayPalPlanId);

public interface ISubscriptionService
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<SubscriptionSummary>> GetClientSubscriptionsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionSummary>> GetAdminSubscriptionsAsync(
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionLookup> ResolveSubscribableOfferAsync(
        string offerId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> CreatePendingAsync(
        PortalSessionContext session,
        string offerId,
        string paypalSubscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> MarkAsPendingActivationAsync(
        PortalSessionContext session,
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<AdminSubscriptionDetail> GetAdminSubscriptionDetailAsync(
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> AdminCancelAsync(
        string subscriptionId,
        CancellationToken cancellationToken);
}

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly ICommercialRepository _commercialRepository;

    public SubscriptionService(
        ISubscriptionRepository repository,
        ICommercialRepository commercialRepository)
    {
        _repository = repository;
        _commercialRepository = commercialRepository;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public Task<IReadOnlyList<SubscriptionSummary>> GetClientSubscriptionsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => _repository.GetByCustomerAsync(session.CustomerId, cancellationToken);

    public Task<IReadOnlyList<SubscriptionSummary>> GetAdminSubscriptionsAsync(
        CancellationToken cancellationToken)
        => _repository.GetAllAsync(cancellationToken);

    public async Task<SubscriptionSummary> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
        => await _repository.GetByIdAsync(subscriptionId, cancellationToken)
            ?? throw new PortalDataNotFoundException();

    public async Task<SubscriptionLookup> ResolveSubscribableOfferAsync(
        string offerId,
        CancellationToken cancellationToken)
    {
        var catalog = await _commercialRepository.GetClientCatalogAsync(
            cancellationToken);
        var offer = catalog.FirstOrDefault(
            candidate => string.Equals(
                candidate.Id,
                offerId,
                StringComparison.Ordinal))
            ?? throw new PortalDataNotFoundException();

        if (!string.Equals(
                offer.BillingCadence,
                CommercialStatuses.CadenceMonthly,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(offer.PayPalPlanId))
        {
            throw new PortalValidationException();
        }

        return new SubscriptionLookup(offer, offer.PayPalPlanId);
    }

    public async Task<SubscriptionSummary> CreatePendingAsync(
        PortalSessionContext session,
        string offerId,
        string paypalSubscriptionId,
        CancellationToken cancellationToken)
    {
        var lookup = await ResolveSubscribableOfferAsync(
            offerId,
            cancellationToken);
        return await _repository.CreatePendingAsync(
            session.CustomerId,
            lookup.Offer.Id,
            lookup.PayPalPlanId,
            paypalSubscriptionId,
            cancellationToken);
    }

    public async Task<SubscriptionSummary> MarkAsPendingActivationAsync(
        PortalSessionContext session,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var current = await _repository.GetByIdAsync(
            subscriptionId,
            cancellationToken)
            ?? throw new PortalDataNotFoundException();
        if (!string.Equals(
                current.CustomerId,
                session.CustomerId,
                StringComparison.Ordinal))
        {
            throw new PortalDataNotFoundException();
        }

        if (current.Status is "active" or "pending_activation")
        {
            return current;
        }

        if (current.Status != "pending_approval")
        {
            throw new PortalValidationException();
        }

        return await _repository.UpdateStatusAsync(
            subscriptionId,
            "pending_activation",
            cancellationToken);
    }

    public async Task<AdminSubscriptionDetail> GetAdminSubscriptionDetailAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscription = await _repository.GetByIdAsync(
            subscriptionId,
            cancellationToken)
            ?? throw new PortalDataNotFoundException();
        var documents = await _commercialRepository
            .GetDocumentsForSubscriptionAsync(
                subscriptionId,
                cancellationToken);
        return new AdminSubscriptionDetail(subscription, documents);
    }

    public async Task<SubscriptionSummary> AdminCancelAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var current = await _repository.GetByIdAsync(
            subscriptionId,
            cancellationToken)
            ?? throw new PortalDataNotFoundException();

        if (current.Status is "cancelled" or "expired")
        {
            return current;
        }

        return await _repository.UpdateStatusAsync(
            subscriptionId,
            "cancelled",
            cancellationToken);
    }
}
