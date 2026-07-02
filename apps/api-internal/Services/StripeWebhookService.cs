using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public interface IStripeWebhookService
{
    Task<StripeWebhookProcessingResult> ProcessAsync(
        StripeWebhookEventPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class StripeWebhookService : IStripeWebhookService
{
    private readonly IStripeWebhookRepository _events;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ICommercialRepository _commercial;
    private readonly IInvoiceIssuingService _issuing;
    private readonly IAuditService _audit;
    private readonly ILogger<StripeWebhookService> _logger;

    public StripeWebhookService(
        IStripeWebhookRepository events,
        ISubscriptionRepository subscriptions,
        ICommercialRepository commercial,
        IInvoiceIssuingService issuing,
        IAuditService audit,
        ILogger<StripeWebhookService> logger)
    {
        _events = events;
        _subscriptions = subscriptions;
        _commercial = commercial;
        _issuing = issuing;
        _audit = audit;
        _logger = logger;
    }

    public async Task<StripeWebhookProcessingResult> ProcessAsync(
        StripeWebhookEventPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var eventId = payload.EventId?.Trim();
        var eventType = payload.EventType?.Trim();
        var rawPayload = payload.RawPayload ?? "{}";

        if (string.IsNullOrWhiteSpace(eventId)
            || string.IsNullOrWhiteSpace(eventType))
        {
            throw new PortalValidationException();
        }

        var existing = await _events.GetByEventIdAsync(eventId, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Duplicate Stripe webhook event {EventId} ignored (status={Status})",
                eventId,
                existing.Status);
            return new StripeWebhookProcessingResult(
                eventId,
                existing.Status,
                ErrorMessage: null);
        }

        await _events.InsertReceivedAsync(
            eventId,
            eventType,
            payload.ResourceId,
            rawPayload,
            cancellationToken);

        try
        {
            var status = await DispatchAsync(
                eventType,
                rawPayload,
                correlationId,
                cancellationToken);

            switch (status)
            {
                case "processed":
                    await _events.MarkProcessedAsync(eventId, cancellationToken);
                    break;
                case "ignored":
                    await _events.MarkIgnoredAsync(eventId, cancellationToken);
                    break;
                default:
                    await _events.MarkProcessedAsync(eventId, cancellationToken);
                    status = "processed";
                    break;
            }

            return new StripeWebhookProcessingResult(
                eventId,
                status,
                ErrorMessage: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Stripe webhook event {EventId} ({EventType}) failed",
                eventId,
                eventType);
            await _events.MarkFailedAsync(
                eventId,
                Truncate(ex.Message, 4000),
                cancellationToken);
            return new StripeWebhookProcessingResult(
                eventId,
                "failed",
                ErrorMessage: ex.Message);
        }
    }

    private async Task<string> DispatchAsync(
        string eventType,
        string rawPayload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case "payment_intent.succeeded":
                return await HandlePaymentIntentSucceededAsync(
                    rawPayload,
                    correlationId,
                    cancellationToken);

            case "invoice.paid":
                await HandleInvoicePaidAsync(
                    rawPayload,
                    correlationId,
                    cancellationToken);
                return "processed";

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(
                    rawPayload,
                    correlationId,
                    cancellationToken);
                return "processed";

            default:
                _logger.LogInformation(
                    "Stripe webhook event {EventType} not handled — marked ignored",
                    eventType);
                return "ignored";
        }
    }

    private async Task<string> HandlePaymentIntentSucceededAsync(
        string rawPayload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var invoiceId = ReadDataObjectString(rawPayload, "invoice");
        if (!string.IsNullOrWhiteSpace(invoiceId))
        {
            // This PaymentIntent belongs to a subscription invoice — the
            // "invoice.paid" event already handles confirmation for it.
            return "ignored";
        }

        var documentId = ReadDataObjectMetadataString(rawPayload, "document_id");
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new InvalidOperationException(
                "payment_intent.succeeded payload has no metadata.document_id.");
        }

        var confirmResult = await _issuing.ConfirmPaymentAsync(
            documentId,
            correlationId,
            "stripe",
            cancellationToken);
        if (!confirmResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Stripe payment confirm failed: {confirmResult.Code} {confirmResult.Message}");
        }

        return "processed";
    }

    private async Task HandleInvoicePaidAsync(
        string rawPayload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var stripeSubscriptionId = ReadDataObjectString(rawPayload, "subscription");
        if (string.IsNullOrWhiteSpace(stripeSubscriptionId))
        {
            throw new InvalidOperationException(
                "invoice.paid payload has no subscription id.");
        }

        var subscription = await _subscriptions.GetByExternalIdAsync(
            "stripe",
            stripeSubscriptionId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"No local subscription matches Stripe id {stripeSubscriptionId}.");

        if (subscription.Status is "pending_approval" or "pending_activation")
        {
            var now = DateTime.UtcNow;
            subscription = await _subscriptions.ActivateAsync(
                subscription.Id,
                now,
                now.AddMonths(1),
                cancellationToken);
            await _audit.RecordAsync(
                new AuditEvent(
                    correlationId,
                    "subscription.activated",
                    "success",
                    TargetType: "subscription",
                    TargetReference: subscription.Id,
                    CustomerId: subscription.CustomerId),
                cancellationToken);
        }

        var title =
            $"Échéance mensuelle {DateTime.UtcNow:yyyy-MM} — {subscription.OfferName}";

        var documentId = await _commercial.CreateBillingDocumentFromOfferAsync(
            subscription.CustomerId,
            subscription.CommercialOfferId,
            subscription.Id,
            title,
            correlationId,
            cancellationToken);

        var issueResult = await _issuing.IssueInvoiceAsync(
            documentId,
            sendEmail: false,
            correlationId,
            cancellationToken);
        if (!issueResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"BPCE issue failed: {issueResult.Code} {issueResult.Message}");
        }

        var confirmResult = await _issuing.ConfirmPaymentAsync(
            documentId,
            correlationId,
            "stripe",
            cancellationToken);
        if (!confirmResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"BPCE confirm failed: {confirmResult.Code} {confirmResult.Message}");
        }

        await _audit.RecordAsync(
            new AuditEvent(
                correlationId,
                "subscription.payment_received",
                "success",
                TargetType: "subscription",
                TargetReference: subscription.Id,
                CustomerId: subscription.CustomerId),
            cancellationToken);
    }

    private async Task HandleSubscriptionDeletedAsync(
        string rawPayload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var stripeSubscriptionId = ReadDataObjectString(rawPayload, "id");
        if (string.IsNullOrWhiteSpace(stripeSubscriptionId))
        {
            throw new InvalidOperationException(
                "customer.subscription.deleted payload has no id.");
        }

        var subscription = await _subscriptions.GetByExternalIdAsync(
            "stripe",
            stripeSubscriptionId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"No local subscription matches Stripe id {stripeSubscriptionId}.");

        var updated = await _subscriptions.UpdateStatusAsync(
            subscription.Id,
            "cancelled",
            cancellationToken);
        await _audit.RecordAsync(
            new AuditEvent(
                correlationId,
                "subscription.cancelled",
                "success",
                TargetType: "subscription",
                TargetReference: updated.Id,
                CustomerId: updated.CustomerId),
            cancellationToken);
    }

    private static string? ReadDataObjectString(string rawPayload, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (document.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("object", out var dataObject)
                && dataObject.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? ReadDataObjectMetadataString(
        string rawPayload,
        string metadataKey)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (document.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("object", out var dataObject)
                && dataObject.TryGetProperty("metadata", out var metadata)
                && metadata.TryGetProperty(metadataKey, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string Truncate(string value, int maximumLength)
        => value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
