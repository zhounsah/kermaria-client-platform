namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockPayPalWebhookStore
{
    public object SyncRoot { get; } = new();
    public Dictionary<string, PayPalWebhookEventRecord> Events { get; } = new();
}

public sealed class MockPayPalWebhookRepository : IPayPalWebhookRepository
{
    private readonly MockPayPalWebhookStore _store;

    public MockPayPalWebhookRepository(MockPayPalWebhookStore store)
    {
        _store = store;
    }

    public Task<PayPalWebhookEventRecord?> GetByEventIdAsync(
        string eventId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Events.TryGetValue(eventId, out var record);
            return Task.FromResult(record);
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
            _store.Events[eventId] = new PayPalWebhookEventRecord(
                id,
                eventId,
                eventType,
                resourceId,
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
