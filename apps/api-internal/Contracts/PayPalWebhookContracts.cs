namespace Kermaria.ApiInternal.Contracts;

public sealed record PayPalWebhookEventPayload(
    string? EventId,
    string? EventType,
    string? ResourceId,
    string? RawPayload);

public sealed record PayPalWebhookProcessingResult(
    string EventId,
    string Status,
    string? ErrorMessage);
