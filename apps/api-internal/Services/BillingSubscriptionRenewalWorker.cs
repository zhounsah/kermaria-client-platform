using System.Globalization;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public sealed class BillingSubscriptionRenewalWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SuspensionGracePeriod =
        TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingSubscriptionRenewalWorker> _logger;

    public BillingSubscriptionRenewalWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BillingSubscriptionRenewalWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        await ProcessDueSubscriptionsAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessDueSubscriptionsAsync(stoppingToken);
        }
    }

    private async Task ProcessDueSubscriptionsAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var commercial = scope.ServiceProvider.GetRequiredService<ICommercialRepository>();
        var issuing = scope.ServiceProvider.GetRequiredService<IInvoiceIssuingService>();

        if (!subscriptions.IsPersistent || !commercial.IsPersistent)
        {
            return;
        }

        IReadOnlyList<Contracts.SubscriptionSummary> allSubscriptions;
        try
        {
            allSubscriptions = await subscriptions.GetAdminSubscriptionsAsync(
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Unable to load subscriptions for billing renewal worker.");
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var dueSubscriptions = allSubscriptions.Where(subscription =>
        {
            if (!string.Equals(
                    subscription.Rail,
                    "billing",
                    StringComparison.Ordinal)
                || subscription.Status is "cancelled" or "expired"
                || string.IsNullOrWhiteSpace(subscription.NextBillingAt))
            {
                return false;
            }

            if (!TryParseIsoUtc(subscription.NextBillingAt, out var nextBillingAtUtc))
            {
                return false;
            }

            return nextBillingAtUtc <= nowUtc;
        }).ToArray();

        foreach (var subscription in dueSubscriptions)
        {
            var correlationId = $"billing-renewal-{Guid.NewGuid():N}";
            try
            {
                if (subscription.Status == "pending_cancellation"
                    && subscription.CancelAtTermEnd)
                {
                    await subscriptions.UpdateStatusAsync(
                        subscription.Id,
                        "cancelled",
                        "subscription.provisioning.term_end_cancel",
                        correlationId,
                        requestedByUserId: null,
                        cancellationToken);
                    continue;
                }

                var documents = await commercial.GetDocumentsForSubscriptionAsync(
                    subscription.Id,
                    cancellationToken);
                var openDocument = documents
                    .Where(document => document.Status != "paid")
                    .OrderByDescending(document => document.CreatedAt)
                    .FirstOrDefault();

                if (openDocument is not null)
                {
                    if (TryParseIsoUtc(openDocument.CreatedAt, out var openCreatedAtUtc)
                        && nowUtc - openCreatedAtUtc >= SuspensionGracePeriod
                        && subscription.Status != "suspended")
                    {
                        await subscriptions.UpdateStatusAsync(
                            subscription.Id,
                            "suspended",
                            "subscription.provisioning.payment_overdue",
                            correlationId,
                            requestedByUserId: null,
                            cancellationToken);
                    }

                    continue;
                }

                var documentId = await commercial.CreateBillingDocumentForSubscriptionAsync(
                    new SubscriptionBillingDocumentRequest(
                        subscription.CustomerId,
                        subscription.CommercialOfferId,
                        subscription.Id,
                        $"Renouvellement - {subscription.OfferName}",
                        Array.Empty<SubscriptionBillingDocumentLineRequest>()),
                    correlationId,
                    cancellationToken);
                var issueResult = await issuing.IssueInvoiceAsync(
                    documentId,
                    sendEmail: true,
                    correlationId,
                    cancellationToken);
                if (!issueResult.Succeeded)
                {
                    _logger.LogWarning(
                        "Renewal document {DocumentId} for subscription {SubscriptionId} issued with non-success code {Code}: {Message}",
                        documentId,
                        subscription.Id,
                        issueResult.Code,
                        issueResult.Message);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Billing renewal worker failed for subscription {SubscriptionId}.",
                    subscription.Id);
            }
        }
    }

    private static bool TryParseIsoUtc(string? value, out DateTime parsedUtc)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsedUtc = default;
            return false;
        }

        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsedUtc);
    }
}
