using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Infrastructure;

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
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionService _subscriptions;
    private readonly ICommercialRepository _commercial;
    private readonly IInvoiceIssuingService _issuing;
    private readonly IAuditService _audit;
    private readonly ILogger<StripeWebhookService> _logger;

    public StripeWebhookService(
        IStripeWebhookRepository events,
        ISubscriptionRepository subscriptionRepository,
        ISubscriptionService subscriptions,
        ICommercialRepository commercial,
        IInvoiceIssuingService issuing,
        IAuditService audit,
        ILogger<StripeWebhookService> logger)
    {
        _events = events;
        _subscriptionRepository = subscriptionRepository;
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
            if (existing.Status is not "failed")
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

            _logger.LogInformation(
                "Retrying Stripe webhook event {EventId} after prior status {Status}",
                eventId,
                existing.Status);
        }
        else
        {
            await _events.InsertReceivedAsync(
                eventId,
                eventType,
                payload.ResourceId,
                rawPayload,
                cancellationToken);
        }

        try
        {
            var status = await DispatchAsync(
                eventType,
                payload.ResourceId,
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
        string? resourceId,
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
            case "invoice.payment_succeeded":
                return await HandleInvoicePaymentSucceededAsync(
                    resourceId,
                    eventType,
                    rawPayload,
                    correlationId,
                    cancellationToken);

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(
                    rawPayload,
                    correlationId,
                    cancellationToken);
                return "processed";

            default:
                _logger.LogInformation(
                    "Stripe webhook event {EventType} not handled - marked ignored",
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
            return "ignored";
        }

        var documentId = ReadDataObjectMetadataString(rawPayload, "document_id");
        if (string.IsNullOrWhiteSpace(documentId))
        {
            _logger.LogWarning(
                "Stripe payment_intent.succeeded event {PaymentIntentId} ignored because metadata.document_id is missing",
                ReadDataObjectString(rawPayload, "id") ?? "<unknown>");
            return "ignored";
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

    private async Task<string> HandleInvoicePaymentSucceededAsync(
        string? resourceId,
        string eventType,
        string rawPayload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var invoiceId = WebhookResourceIdNormalizer.Normalize(resourceId)
            ?? ReadDataObjectString(rawPayload, "id");
        if (!string.IsNullOrWhiteSpace(invoiceId)
            && await _events.HasProcessedInvoiceSuccessEventAsync(
                invoiceId,
                cancellationToken))
        {
            _logger.LogInformation(
                "Stripe invoice success event {EventType} ignored because invoice {InvoiceId} is already processed",
                eventType,
                invoiceId);
            return "ignored";
        }

        var stripeSubscriptionId = ReadStripeInvoiceSubscriptionId(rawPayload);
        if (string.IsNullOrWhiteSpace(stripeSubscriptionId))
        {
            throw new InvalidOperationException(
                $"{eventType} payload has no subscription id.");
        }

        var subscription = await _subscriptionRepository.GetByExternalIdAsync(
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
                now,
                correlationId,
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

        var extraLines = subscription.PaidCyclesCount == 0
            && subscription.SetupFeeAmountCents > 0
            ? new[]
            {
                new SubscriptionBillingDocumentLineRequest(
                    null,
                    "Mise en service",
                    $"Mise en service du {subscription.OfferName}",
                    1m,
                    "forfait",
                    subscription.SetupFeeAmountCents,
                    null,
                    5)
            }
            : Array.Empty<SubscriptionBillingDocumentLineRequest>();
        var title = $"Echeance {KermariaTimeZone.Now:yyyy-MM} - {subscription.OfferName}";

        var documentId = await _commercial.CreateBillingDocumentForSubscriptionAsync(
            new SubscriptionBillingDocumentRequest(
                subscription.CustomerId,
                subscription.CommercialOfferId,
                subscription.Id,
                title,
                extraLines),
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

        subscription = await _subscriptions.RecordPaymentAsync(
            subscription.Id,
            DateTime.UtcNow,
            cancellationToken);

        await _audit.RecordAsync(
            new AuditEvent(
                correlationId,
                "subscription.payment_received",
                "success",
                TargetType: "subscription",
                TargetReference: subscription.Id,
                CustomerId: subscription.CustomerId),
            cancellationToken);

        return "processed";
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

        var subscription = await _subscriptionRepository.GetByExternalIdAsync(
            "stripe",
            stripeSubscriptionId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"No local subscription matches Stripe id {stripeSubscriptionId}.");

        var updated = await _subscriptions.UpdateStatusAsync(
            subscription.Id,
            "cancelled",
            "subscription.provisioning.cancelled",
            correlationId,
            requestedByUserId: null,
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

    private static string? ReadStripeInvoiceSubscriptionId(string rawPayload)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (!document.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("object", out var dataObject))
            {
                return null;
            }

            if (dataObject.TryGetProperty("subscription", out var directSubscription)
                && directSubscription.ValueKind == JsonValueKind.String)
            {
                return directSubscription.GetString();
            }

            if (dataObject.TryGetProperty("parent", out var parent)
                && parent.ValueKind == JsonValueKind.Object
                && parent.TryGetProperty(
                    "subscription_details",
                    out var subscriptionDetails)
                && subscriptionDetails.ValueKind == JsonValueKind.Object
                && subscriptionDetails.TryGetProperty(
                    "subscription",
                    out var nestedSubscription)
                && nestedSubscription.ValueKind == JsonValueKind.String)
            {
                return nestedSubscription.GetString();
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
