namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record PayPalWebhookEventRecord(
    string Id,
    string EventId,
    string EventType,
    string? ResourceId,
    string Status);

public interface IPayPalWebhookRepository
{
    Task<PayPalWebhookEventRecord?> GetByEventIdAsync(
        string eventId,
        CancellationToken cancellationToken);

    Task<string> InsertReceivedAsync(
        string eventId,
        string eventType,
        string? resourceId,
        string rawPayload,
        CancellationToken cancellationToken);

    Task MarkProcessedAsync(
        string eventId,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        string eventId,
        string errorMessage,
        CancellationToken cancellationToken);

    Task MarkIgnoredAsync(
        string eventId,
        CancellationToken cancellationToken);
}
