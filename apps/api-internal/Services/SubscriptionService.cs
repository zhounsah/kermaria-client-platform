using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

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
}

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repository;

    public SubscriptionService(ISubscriptionRepository repository)
    {
        _repository = repository;
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
}
