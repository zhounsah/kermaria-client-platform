namespace Kermaria.ApiInternal.Services;

public sealed record LegacySubscriptionPriceLockBackfillSubscription(
    string Id,
    string OfferId,
    string Status);

public sealed record LegacySubscriptionHistoricalBillingLine(
    string SubscriptionId,
    string OfferId,
    int UnitPriceCents,
    int? TaxRateBasisPoints,
    string Currency,
    DateTime DocumentCreatedAt,
    int SortOrder,
    DateTime LineCreatedAt,
    string LineId);

public sealed record LegacySubscriptionPriceLockBackfillCandidate(
    string SubscriptionId,
    string OfferId,
    int UnitPriceCents,
    int? TaxRateBasisPoints,
    string Currency,
    string Reason);

public sealed record LegacySubscriptionPriceLockReviewRequired(
    string SubscriptionId,
    string OfferId,
    string Reason);

public sealed record LegacySubscriptionPriceLockBackfillPlan(
    IReadOnlyList<LegacySubscriptionPriceLockBackfillCandidate> Locks,
    IReadOnlyList<LegacySubscriptionPriceLockReviewRequired> ReviewRequired);

public static class LegacySubscriptionPriceLockBackfillPlanner
{
    private static readonly HashSet<string> EligibleStatuses =
        new(StringComparer.Ordinal)
        {
            "pending_approval",
            "pending_payment",
            "pending_activation",
            "pending_cancellation",
            "active",
            "suspended"
        };

    public static LegacySubscriptionPriceLockBackfillPlan Plan(
        IReadOnlyList<LegacySubscriptionPriceLockBackfillSubscription>
            subscriptions,
        IReadOnlyList<LegacySubscriptionHistoricalBillingLine> historicalLines,
        IReadOnlySet<string> subscriptionsWithActiveLock)
    {
        var locks = new List<LegacySubscriptionPriceLockBackfillCandidate>();
        var reviewRequired =
            new List<LegacySubscriptionPriceLockReviewRequired>();

        foreach (var subscription in subscriptions
            .Where(subscription => EligibleStatuses.Contains(subscription.Status))
            .Where(subscription => !subscriptionsWithActiveLock.Contains(
                subscription.Id))
            .OrderBy(subscription => subscription.Id, StringComparer.Ordinal))
        {
            var firstLine = historicalLines
                .Where(line => string.Equals(
                    line.SubscriptionId,
                    subscription.Id,
                    StringComparison.Ordinal)
                    && string.Equals(
                        line.OfferId,
                        subscription.OfferId,
                        StringComparison.Ordinal)
                    && line.UnitPriceCents > 0)
                .OrderBy(line => line.DocumentCreatedAt)
                .ThenBy(line => line.SortOrder)
                .ThenBy(line => line.LineCreatedAt)
                .ThenBy(line => line.LineId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (firstLine is null)
            {
                reviewRequired.Add(new LegacySubscriptionPriceLockReviewRequired(
                    subscription.Id,
                    subscription.OfferId,
                    "missing_reliable_historical_price"));
                continue;
            }

            locks.Add(new LegacySubscriptionPriceLockBackfillCandidate(
                subscription.Id,
                subscription.OfferId,
                firstLine.UnitPriceCents,
                firstLine.TaxRateBasisPoints,
                firstLine.Currency,
                "legacy_subscription_backfill"));
        }

        return new LegacySubscriptionPriceLockBackfillPlan(
            locks,
            reviewRequired);
    }
}
