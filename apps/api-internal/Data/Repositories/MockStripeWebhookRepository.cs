namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockStripeWebhookStore
{
    public object SyncRoot { get; } = new();
    public Dictionary<string, StripeWebhookEventRecord> Events { get; } = new();
}

public sealed class MockStripeWebhookRepository : IStripeWebhookRepository
{
    private static readonly HashSet<string> ProcessedInvoiceSuccessEventTypes =
    [
        "invoice.paid",
        "invoice.payment_succeeded"
    ];

    private readonly MockStripeWebhookStore _store;

    public MockStripeWebhookRepository(MockStripeWebhookStore store)
    {
        _store = store;
    }

    public Task<StripeWebhookEventRecord?> GetByEventIdAsync(
        string eventId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Events.TryGetValue(eventId, out var record);
            return Task.FromResult(record);
        }
    }

    public Task<bool> HasProcessedInvoiceSuccessEventAsync(
        string resourceId,
        CancellationToken cancellationToken)
    {
        var normalizedResourceId = WebhookResourceIdNormalizer.Normalize(resourceId);
        if (normalizedResourceId is null)
        {
            return Task.FromResult(false);
        }

        lock (_store.SyncRoot)
        {
            var exists = _store.Events.Values.Any(record =>
                string.Equals(
                    record.ResourceId,
                    normalizedResourceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    record.Status,
                    "processed",
                    StringComparison.Ordinal)
                && ProcessedInvoiceSuccessEventTypes.Contains(record.EventType));
            return Task.FromResult(exists);
        }
    }

    public Task<string> InsertReceivedAsync(
        string eventId,
        string eventType,
        string? resourceId,
        string rawPayload,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            if (_store.Events.ContainsKey(eventId))
            {
                throw new InvalidOperationException(
                    $"Event {eventId} already exists.");
            }

            var id = Guid.NewGuid().ToString("D");
            _store.Events[eventId] = new StripeWebhookEventRecord(
                id,
                eventId,
                eventType,
                WebhookResourceIdNormalizer.Normalize(resourceId),
                "received");
            return Task.FromResult(id);
        }
    }

    public Task MarkProcessedAsync(
        string eventId,
        CancellationToken cancellationToken)
        => UpdateStatus(eventId, "processed");

    public Task MarkFailedAsync(
        string eventId,
        string errorMessage,
        CancellationToken cancellationToken)
        => UpdateStatus(eventId, "failed");

    public Task MarkIgnoredAsync(
        string eventId,
        CancellationToken cancellationToken)
        => UpdateStatus(eventId, "ignored");

    private Task UpdateStatus(string eventId, string status)
    {
        lock (_store.SyncRoot)
        {
            if (_store.Events.TryGetValue(eventId, out var existing))
            {
                _store.Events[eventId] = existing with { Status = status };
            }
        }

        return Task.CompletedTask;
    }
}
