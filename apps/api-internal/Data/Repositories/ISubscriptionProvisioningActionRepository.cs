using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record SubscriptionProvisioningActionCreateRequest(
    string SubscriptionId,
    string? CustomerId,
    string? RequestedByUserId,
    string ActionType,
    string TargetReference,
    string CorrelationId,
    string? IdempotencyKeyHash,
    string? DetailsJson);

public interface ISubscriptionProvisioningActionRepository
{
    bool IsPersistent { get; }

    Task<string> CreateRequestedAsync(
        SubscriptionProvisioningActionCreateRequest request,
        CancellationToken cancellationToken);

    Task MarkStartedAsync(
        string actionId,
        CancellationToken cancellationToken);

    Task MarkCompletedAsync(
        string actionId,
        string status,
        string? resultCode,
        bool changed,
        string? detailsJson,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionProvisioningActionSummary>>
        GetRecentBySubscriptionAsync(
            string subscriptionId,
            int limit,
            CancellationToken cancellationToken);
}
