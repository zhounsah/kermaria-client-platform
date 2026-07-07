using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record MockSubscriptionProvisioningActionRecord(
    string Id,
    string SubscriptionId,
    string? CustomerId,
    string? RequestedByUserId,
    string ActionType,
    string TargetReference,
    string CorrelationId,
    string? IdempotencyKeyHash,
    string Status,
    string? ResultCode,
    bool? Changed,
    string RequestedAt,
    string? StartedAt,
    string? CompletedAt,
    string? DetailsJson);

public sealed class MockSubscriptionProvisioningActionStore
{
    public object SyncRoot { get; } = new();

    public List<MockSubscriptionProvisioningActionRecord> Actions { get; } = [];
}

public sealed class MockSubscriptionProvisioningActionRepository
    : ISubscriptionProvisioningActionRepository
{
    private readonly MockSubscriptionProvisioningActionStore _store;

    public MockSubscriptionProvisioningActionRepository(
        MockSubscriptionProvisioningActionStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<string> CreateRequestedAsync(
        SubscriptionProvisioningActionCreateRequest request,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var id = Guid.NewGuid().ToString("D");
            _store.Actions.Add(new MockSubscriptionProvisioningActionRecord(
                id,
                request.SubscriptionId,
                request.CustomerId,
                request.RequestedByUserId,
                request.ActionType,
                request.TargetReference,
                request.CorrelationId,
                request.IdempotencyKeyHash,
                "requested",
                null,
                null,
                DateTime.UtcNow.ToString("O"),
                null,
                null,
                request.DetailsJson));
            return Task.FromResult(id);
        }
    }

    public Task MarkStartedAsync(
        string actionId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var index = FindIndex(actionId);
            if (index < 0)
            {
                return Task.CompletedTask;
            }

            var current = _store.Actions[index];
            _store.Actions[index] = current with
            {
                Status = "running",
                StartedAt = current.StartedAt ?? DateTime.UtcNow.ToString("O")
            };
            return Task.CompletedTask;
        }
    }

    public Task MarkCompletedAsync(
        string actionId,
        string status,
        string? resultCode,
        bool changed,
        string? detailsJson,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var index = FindIndex(actionId);
            if (index < 0)
            {
                return Task.CompletedTask;
            }

            var current = _store.Actions[index];
            _store.Actions[index] = current with
            {
                Status = status,
                ResultCode = resultCode,
                Changed = changed,
                StartedAt = current.StartedAt ?? DateTime.UtcNow.ToString("O"),
                CompletedAt = DateTime.UtcNow.ToString("O"),
                DetailsJson = detailsJson
            };
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<SubscriptionProvisioningActionSummary>>
        GetRecentBySubscriptionAsync(
            string subscriptionId,
            int limit,
            CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<SubscriptionProvisioningActionSummary>>(
                _store.Actions
                    .Where(action => action.SubscriptionId == subscriptionId)
                    .OrderByDescending(action => action.RequestedAt)
                    .Take(limit)
                    .Select(action => new SubscriptionProvisioningActionSummary(
                        action.Id,
                        action.ActionType,
                        action.Status,
                        action.ResultCode,
                        action.Changed ?? false,
                        action.CorrelationId,
                        action.TargetReference,
                        action.RequestedAt,
                        action.StartedAt,
                        action.CompletedAt))
                    .ToArray());
        }
    }

    private int FindIndex(string actionId)
        => _store.Actions.FindIndex(action => action.Id == actionId);
}
