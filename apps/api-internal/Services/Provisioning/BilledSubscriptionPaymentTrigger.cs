using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services.Provisioning;

public interface IBilledSubscriptionPaymentTrigger
{
    Task OnDocumentPaidAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class BilledSubscriptionPaymentTrigger
    : IBilledSubscriptionPaymentTrigger
{
    private readonly ICommercialRepository _commercial;
    private readonly ISubscriptionService _subscriptions;
    private readonly ILogger<BilledSubscriptionPaymentTrigger> _logger;

    public BilledSubscriptionPaymentTrigger(
        ICommercialRepository commercial,
        ISubscriptionService subscriptions,
        ILogger<BilledSubscriptionPaymentTrigger> logger)
    {
        _commercial = commercial;
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public async Task OnDocumentPaidAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> subscriptionIds;
        try
        {
            subscriptionIds = await _commercial.GetLinkedSubscriptionIdsForDocumentAsync(
                documentId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to load linked subscriptions for paid document {DocumentId}",
                documentId);
            return;
        }

        foreach (var subscriptionId in subscriptionIds.Distinct(StringComparer.Ordinal))
        {
            try
            {
                var subscription = await _subscriptions.GetSubscriptionAsync(
                    subscriptionId,
                    cancellationToken);
                if (!string.Equals(
                        subscription.Rail,
                        "billing",
                        StringComparison.Ordinal)
                    || subscription.Status is "cancelled" or "expired")
                {
                    continue;
                }

                if (subscription.Status is "pending_payment" or "pending_activation")
                {
                    if (subscription.Status == "pending_payment")
                    {
                        await _subscriptions.UpdateStatusAsync(
                            subscription.Id,
                            "pending_activation",
                            "subscription.provisioning.pending_payment_paid",
                            correlationId,
                            requestedByUserId: null,
                            cancellationToken);
                    }

                    await _subscriptions.ActivateAsync(
                        subscription.Id,
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        correlationId,
                        cancellationToken);
                    await _subscriptions.RecordPaymentAsync(
                        subscription.Id,
                        DateTime.UtcNow,
                        cancellationToken);
                    continue;
                }

                var updated = await _subscriptions.RecordPaymentAsync(
                    subscription.Id,
                    DateTime.UtcNow,
                    cancellationToken);
                if (string.Equals(
                        subscription.Status,
                        "suspended",
                        StringComparison.Ordinal)
                    && string.Equals(
                        updated.Status,
                        "active",
                        StringComparison.Ordinal))
                {
                    await _subscriptions.ReconcileProvisioningAsync(
                        subscription.Id,
                        "subscription.provisioning.reactivated_after_payment",
                        correlationId,
                        requestedByUserId: null,
                        targetUserSamAccountNames: null,
                        cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Billed subscription payment trigger failed for subscription {SubscriptionId} document {DocumentId}",
                    subscriptionId,
                    documentId);
            }
        }
    }
}
