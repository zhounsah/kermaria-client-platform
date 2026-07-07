using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public interface IPayPalWebhookService
{
    Task<PayPalWebhookProcessingResult> ProcessAsync(
        PayPalWebhookEventPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class PayPalWebhookService : IPayPalWebhookService
{
    private readonly IPayPalWebhookRepository _events;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionService _subscriptions;
    private readonly ICommercialRepository _commercial;
    private readonly IInvoiceIssuingService _issuing;
    private readonly IAuditService _audit;
    private readonly ILogger<PayPalWebhookService> _logger;

    public PayPalWebhookService(
        IPayPalWebhookRepository events,
        ISubscriptionRepository subscriptionRepository,
        ISubscriptionService subscriptions,
        ICommercialRepository commercial,
        IInvoiceIssuingService issuing,
        IAuditService audit,
        ILogger<PayPalWebhookService> logger)
    {
        _events = events;
        _subscriptionRepository = subscriptionRepository;
        _subscriptions = subscriptions;
        _commercial = commercial;
        _issuing = issuing;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PayPalWebhookProcessingResult> ProcessAsync(
        PayPalWebhookEventPayload payload,
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
                "Duplicate PayPal webhook event {EventId} ignored (status={Status})",
                eventId,
                existing.Status);
            return new PayPalWebhookProcessingResult(
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
                eventId,
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

            return new PayPalWebhookProcessingResult(
                eventId,
                status,
                ErrorMessage: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "PayPal webhook event {EventId} ({EventType}) failed",
                eventId,
                eventType);
            await _events.MarkFailedAsync(
                eventId,
                Truncate(ex.Message, 4000),
                cancellationToken);
            return new PayPalWebhookProcessingResult(
                eventId,
                "failed",
                ErrorMessage: ex.Message);
        }
    }

    private async Task<string> DispatchAsync(
        string eventId,
        string eventType,
        string? resourceId,
        string rawPayload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case "BILLING.SUBSCRIPTION.ACTIVATED":
                await HandleSubscriptionActivatedAsync(
                    resourceId,
                    correlationId,
                    cancellationToken);
                return "processed";

            case "BILLING.SUBSCRIPTION.SUSPENDED":
                await HandleSubscriptionStatusAsync(
                    resourceId,
                    "suspended",
                    "subscription.suspended",
                    correlationId,
                    cancellationToken);
                return "processed";

            case "BILLING.SUBSCRIPTION.CANCELLED":
                await HandleSubscriptionStatusAsync(
                    resourceId,
                    "cancelled",
                    "subscription.cancelled",
                    correlationId,
                    cancellationToken);
                return "processed";

            case "BILLING.SUBSCRIPTION.EXPIRED":
                await HandleSubscriptionStatusAsync(
                    resourceId,
                    "expired",
                    "subscription.expired",
                    correlationId,
                    cancellationToken);
                return "processed";

            case "PAYMENT.SALE.COMPLETED":
                await HandlePaymentCompletedAsync(
                    rawPayload,
                    correlationId,
                    cancellationToken);
                return "processed";

            default:
                _logger.LogInformation(
                    "PayPal webhook event {EventType} not handled - marked ignored",
                    eventType);
                return "ignored";
        }
    }

    private async Task HandleSubscriptionActivatedAsync(
        string? paypalSubscriptionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var subscription = await ResolveSubscriptionAsync(
            paypalSubscriptionId,
            cancellationToken);
        var now = DateTime.UtcNow;
        var activated = await _subscriptions.ActivateAsync(
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
                TargetReference: activated.Id,
                CustomerId: activated.CustomerId),
            cancellationToken);
    }

    private async Task HandleSubscriptionStatusAsync(
        string? paypalSubscriptionId,
        string newStatus,
        string auditAction,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var subscription = await ResolveSubscriptionAsync(
            paypalSubscriptionId,
            cancellationToken);
        var updated = await _subscriptions.UpdateStatusAsync(
            subscription.Id,
            newStatus,
            $"subscription.provisioning.{newStatus}",
            correlationId,
            requestedByUserId: null,
            cancellationToken);
        await _audit.RecordAsync(
            new AuditEvent(
                correlationId,
                auditAction,
                "success",
                TargetType: "subscription",
                TargetReference: updated.Id,
                CustomerId: updated.CustomerId),
            cancellationToken);
    }

    private async Task HandlePaymentCompletedAsync(
        string rawPayload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var paypalSubscriptionId = ExtractBillingAgreementId(rawPayload);
        if (string.IsNullOrWhiteSpace(paypalSubscriptionId))
        {
            throw new InvalidOperationException(
                "PAYMENT.SALE.COMPLETED payload has no billing_agreement_id.");
        }

        var subscription = await _subscriptionRepository.GetByExternalIdAsync(
            "paypal",
            paypalSubscriptionId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"No local subscription matches PayPal id {paypalSubscriptionId}.");

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
        var title = $"Echeance {DateTime.UtcNow:yyyy-MM} - {subscription.OfferName}";

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
            "paypal",
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
    }

    private async Task<SubscriptionSummary> ResolveSubscriptionAsync(
        string? paypalSubscriptionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(paypalSubscriptionId))
        {
            throw new InvalidOperationException(
                "PayPal subscription id is required.");
        }

        return await _subscriptionRepository.GetByExternalIdAsync(
            "paypal",
            paypalSubscriptionId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"No local subscription matches PayPal id {paypalSubscriptionId}.");
    }

    private static string? ExtractBillingAgreementId(string rawPayload)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (document.RootElement.TryGetProperty(
                    "resource", out var resource)
                && resource.TryGetProperty(
                    "billing_agreement_id", out var agreement)
                && agreement.ValueKind == JsonValueKind.String)
            {
                return agreement.GetString();
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
