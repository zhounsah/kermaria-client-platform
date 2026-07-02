namespace Kermaria.ApiInternal.Contracts;

public sealed record StripeWebhookEventPayload(
    string? EventId,
    string? EventType,
    string? ResourceId,
    string? RawPayload);

public sealed record StripeWebhookProcessingResult(
    string EventId,
    string Status,
    string? ErrorMessage);
