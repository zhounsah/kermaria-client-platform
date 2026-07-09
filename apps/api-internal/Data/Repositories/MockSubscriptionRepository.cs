using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockSubscriptionStore
{
    public object SyncRoot { get; } = new();

    public List<SubscriptionSummary> Subscriptions { get; } = new();
}

public sealed class MockSubscriptionRepository : ISubscriptionRepository
{
    private readonly MockSubscriptionStore _store;

    public MockSubscriptionRepository(MockSubscriptionStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<IReadOnlyList<SubscriptionSummary>> GetByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<SubscriptionSummary>>(
                _store.Subscriptions
                    .Where(subscription => subscription.CustomerId == customerId)
                    .OrderByDescending(subscription => subscription.UpdatedAt)
                    .ToArray());
        }
    }

    public Task<IReadOnlyList<SubscriptionSummary>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<SubscriptionSummary>>(
                _store.Subscriptions
                    .OrderByDescending(subscription => subscription.UpdatedAt)
                    .ToArray());
        }
    }

    public Task<SubscriptionSummary?> GetByIdAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Subscriptions.FirstOrDefault(
                    subscription => subscription.Id == subscriptionId));
        }
    }

    public Task<SubscriptionSummary?> GetByExternalIdAsync(
        string rail,
        string externalId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(
                _store.Subscriptions.FirstOrDefault(
                    subscription => rail == "stripe"
                        ? subscription.StripeSubscriptionId == externalId
                        : subscription.PayPalSubscriptionId == externalId));
        }
    }

    public Task<SubscriptionSummary> CreatePendingAsync(
        string customerId,
        CommercialOfferSummary offer,
        string rail,
        string initialStatus,
        string? paypalPlanId,
        string? paypalSubscriptionId,
        string? stripePriceId,
        string? stripeSubscriptionId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var now = DateTime.UtcNow.ToString("O");
            var summary = new SubscriptionSummary(
                Guid.NewGuid().ToString("D"),
                customerId,
                customerId,
                customerId,
                offer.Id,
                offer.Name,
                offer.ExternalReference,
                offer.PublicPackCode,
                rail,
                paypalPlanId,
                paypalSubscriptionId,
                stripePriceId,
                stripeSubscriptionId,
                initialStatus,
                offer.PriceAmountCents,
                offer.SetupFeeAmountCents ?? 0,
                offer.BillingIntervalMonths ?? 1,
                offer.CommitmentMonths ?? offer.BillingIntervalMonths ?? 1,
                offer.PaymentMode ?? CommercialStatuses.PaymentModeMonthly,
                0,
                null,
                null,
                false,
                "EUR",
                null,
                null,
                null,
                now,
                now);
            _store.Subscriptions.Add(summary);
            return Task.FromResult(summary);
        }
    }

    public Task<SubscriptionSummary> UpdateStatusAsync(
        string subscriptionId,
        string newStatus,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var index = _store.Subscriptions.FindIndex(
                subscription => subscription.Id == subscriptionId);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Subscription {subscriptionId} not found.");
            }

            var current = _store.Subscriptions[index];
            var now = DateTime.UtcNow.ToString("O");
            var cancelledAt = newStatus == "cancelled"
                ? current.CancelledAt ?? now
                : current.CancelledAt;
            var updated = current with
            {
                Status = newStatus,
                UpdatedAt = now,
                CancelledAt = cancelledAt,
                CancelAtTermEnd = newStatus is "cancelled" or "expired"
                    ? false
                    : current.CancelAtTermEnd
            };
            _store.Subscriptions[index] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<SubscriptionSummary> ActivateAsync(
        string subscriptionId,
        DateTime startedAtUtc,
        DateTime nextBillingAtUtc,
        DateTime commitmentEndsAtUtc,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var index = _store.Subscriptions.FindIndex(
                subscription => subscription.Id == subscriptionId);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Subscription {subscriptionId} not found.");
            }

            var current = _store.Subscriptions[index];
            var now = DateTime.UtcNow.ToString("O");
            var updated = current with
            {
                Status = "active",
                StartedAt = current.StartedAt ?? startedAtUtc.ToString("O"),
                NextBillingAt = nextBillingAtUtc.ToString("O"),
                CommitmentEndsAt = current.CommitmentEndsAt
                    ?? commitmentEndsAtUtc.ToString("O"),
                UpdatedAt = now,
                CancelRequestedAt = current.Status == "pending_cancellation"
                    ? current.CancelRequestedAt
                    : null,
                CancelAtTermEnd = current.Status == "pending_cancellation"
                    && current.CancelAtTermEnd
            };
            _store.Subscriptions[index] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<SubscriptionSummary> RecordPaymentAsync(
        string subscriptionId,
        DateTime nextBillingAtUtc,
        DateTime commitmentEndsAtUtc,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var index = _store.Subscriptions.FindIndex(
                subscription => subscription.Id == subscriptionId);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Subscription {subscriptionId} not found.");
            }

            var current = _store.Subscriptions[index];
            var now = DateTime.UtcNow.ToString("O");
            var updated = current with
            {
                Status = current.Status == "pending_cancellation"
                    ? "pending_cancellation"
                    : "active",
                PaidCyclesCount = current.PaidCyclesCount + 1,
                NextBillingAt = nextBillingAtUtc.ToString("O"),
                CommitmentEndsAt = commitmentEndsAtUtc.ToString("O"),
                UpdatedAt = now
            };
            _store.Subscriptions[index] = updated;
            return Task.FromResult(updated);
        }
    }

    public Task<SubscriptionSummary> RequestCancellationAsync(
        string subscriptionId,
        DateTime requestedAtUtc,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var index = _store.Subscriptions.FindIndex(
                subscription => subscription.Id == subscriptionId);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Subscription {subscriptionId} not found.");
            }

            var current = _store.Subscriptions[index];
            var now = DateTime.UtcNow.ToString("O");
            var updated = current with
            {
                Status = "pending_cancellation",
                CancelRequestedAt = current.CancelRequestedAt
                    ?? requestedAtUtc.ToString("O"),
                CancelAtTermEnd = true,
                UpdatedAt = now
            };
            _store.Subscriptions[index] = updated;
            return Task.FromResult(updated);
        }
    }
}
