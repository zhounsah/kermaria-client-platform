using System.Globalization;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.Services;

public sealed record SubscriptionLookup(
    CommercialOfferSummary Offer,
    string Rail,
    string ExternalPlanId);

public interface ISubscriptionService
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<SubscriptionSummary>> GetClientSubscriptionsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionSummary>> GetAdminSubscriptionsAsync(
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionLookup> ResolveSubscribableOfferAsync(
        string offerId,
        string rail,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> CreatePendingAsync(
        PortalSessionContext session,
        string offerId,
        string rail,
        string externalSubscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> CreateBilledPendingAsync(
        PortalSessionContext session,
        string offerId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> MarkAsPendingActivationAsync(
        PortalSessionContext session,
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<AdminSubscriptionDetail> GetAdminSubscriptionDetailAsync(
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> ActivateAsync(
        string subscriptionId,
        DateTime startedAtUtc,
        DateTime nextBillingAtUtc,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> RecordPaymentAsync(
        string subscriptionId,
        DateTime paidAtUtc,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> UpdateStatusAsync(
        string subscriptionId,
        string newStatus,
        string provisioningActionType,
        string correlationId,
        string? requestedByUserId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> ClientCancelAsync(
        PortalSessionContext session,
        string subscriptionId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SubscriptionSummary> AdminCancelAsync(
        string subscriptionId,
        string correlationId,
        string? requestedByUserId,
        CancellationToken cancellationToken);

    Task<SubscriptionProvisioningSummary> ReconcileProvisioningAsync(
        string subscriptionId,
        string actionType,
        string correlationId,
        string? requestedByUserId,
        IReadOnlyList<string>? targetUserSamAccountNames,
        CancellationToken cancellationToken);
}

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    private readonly IBillingCatalog _billingCatalog;
    private readonly ICommercialRepository _commercialRepository;
    private readonly IBillingV2NewSubscriptionService _billingV2Subscriptions;
    private readonly IBillingV2PortalSubscriptionProjection _billingV2PortalSubscriptions;
    private readonly ISubscriptionProvisioningManager _provisioningManager;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        ISubscriptionRepository repository,
        IBillingCatalog billingCatalog,
        ICommercialRepository commercialRepository,
        IBillingV2NewSubscriptionService billingV2Subscriptions,
        IBillingV2PortalSubscriptionProjection billingV2PortalSubscriptions,
        ISubscriptionProvisioningManager provisioningManager,
        ILogger<SubscriptionService> logger)
    {
        _repository = repository;
        _billingCatalog = billingCatalog;
        _commercialRepository = commercialRepository;
        _billingV2Subscriptions = billingV2Subscriptions;
        _billingV2PortalSubscriptions = billingV2PortalSubscriptions;
        _provisioningManager = provisioningManager;
        _logger = logger;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public Task<IReadOnlyList<SubscriptionSummary>> GetClientSubscriptionsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => GetClientSubscriptionsMergedAsync(session, cancellationToken);

    public Task<IReadOnlyList<SubscriptionSummary>> GetAdminSubscriptionsAsync(
        CancellationToken cancellationToken)
        => _repository.GetAllAsync(cancellationToken);

    private async Task<IReadOnlyList<SubscriptionSummary>>
        GetClientSubscriptionsMergedAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
    {
        var legacy = await _repository.GetByCustomerAsync(
            session.CustomerId,
            cancellationToken);
        var billingV2 = await _billingV2PortalSubscriptions
            .GetClientSubscriptionsAsync(
                session.CustomerId,
                cancellationToken);
        if (billingV2.Count == 0)
        {
            return legacy;
        }

        return legacy
            .Concat(billingV2)
            .OrderByDescending(subscription => ParseUpdatedAt(subscription))
            .ThenByDescending(subscription => subscription.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<SubscriptionSummary> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
        => await GetRequiredSubscriptionAsync(
            subscriptionId,
            cancellationToken);

    public async Task<SubscriptionLookup> ResolveSubscribableOfferAsync(
        string offerId,
        string rail,
        CancellationToken cancellationToken)
    {
        var resolved = await _billingCatalog.ResolveSubscribableOfferAsync(
            offerId,
            rail,
            cancellationToken);
        return new SubscriptionLookup(
            resolved.Offer,
            rail,
            resolved.ProviderExternalId ?? string.Empty);
    }

    public async Task<SubscriptionSummary> CreatePendingAsync(
        PortalSessionContext session,
        string offerId,
        string rail,
        string externalSubscriptionId,
        CancellationToken cancellationToken)
    {
        var lookup = await ResolveSubscribableOfferAsync(
            offerId,
            rail,
            cancellationToken);
        var subscription = await _repository.CreatePendingAsync(
            session.CustomerId,
            lookup.Offer,
            rail,
            "pending_approval",
            rail == "stripe" ? null : lookup.ExternalPlanId,
            rail == "stripe" ? null : externalSubscriptionId,
            rail == "stripe" ? lookup.ExternalPlanId : null,
            rail == "stripe" ? externalSubscriptionId : null,
            cancellationToken);
        await EnsurePriceLockAsync(
            subscription,
            lookup.Offer,
            "legacy_subscription_created",
            cancellationToken);
        await _billingV2Subscriptions.CreateForNewSubscriptionAsync(
            session,
            subscription,
            cancellationToken);
        return subscription;
    }

    public async Task<SubscriptionSummary> CreateBilledPendingAsync(
        PortalSessionContext session,
        string offerId,
        CancellationToken cancellationToken)
    {
        var lookup = await ResolveSubscribableOfferAsync(
            offerId,
            "billing",
            cancellationToken);
        var subscription = await _repository.CreatePendingAsync(
            session.CustomerId,
            lookup.Offer,
            "billing",
            "pending_payment",
            null,
            null,
            null,
            null,
            cancellationToken);
        await EnsurePriceLockAsync(
            subscription,
            lookup.Offer,
            "legacy_subscription_created",
            cancellationToken);
        await _billingV2Subscriptions.CreateForNewSubscriptionAsync(
            session,
            subscription,
            cancellationToken);
        return subscription;
    }

    public async Task<SubscriptionSummary> MarkAsPendingActivationAsync(
        PortalSessionContext session,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var current = await GetRequiredSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        if (!string.Equals(
                current.CustomerId,
                session.CustomerId,
                StringComparison.Ordinal))
        {
            throw new PortalDataNotFoundException();
        }

        if (current.Status is "active" or "pending_activation")
        {
            return current;
        }

        if (current.Status != "pending_approval")
        {
            throw new PortalValidationException();
        }

        var updated = await _repository.UpdateStatusAsync(
            subscriptionId,
            "pending_activation",
            cancellationToken);
        await _billingV2Subscriptions.SyncFromLegacySubscriptionAsync(
            updated,
            cancellationToken);
        return updated;
    }

    public async Task<AdminSubscriptionDetail> GetAdminSubscriptionDetailAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscription = await GetRequiredSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        var documents = await _commercialRepository
            .GetDocumentsForSubscriptionAsync(
                subscriptionId,
                cancellationToken);
        var provisioning = await _provisioningManager.GetSummaryAsync(
            subscription,
            cancellationToken);
        return new AdminSubscriptionDetail(
            subscription,
            documents,
            provisioning);
    }

    public async Task<SubscriptionSummary> ActivateAsync(
        string subscriptionId,
        DateTime startedAtUtc,
        DateTime nextBillingAtUtc,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var current = await GetRequiredSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        if (string.Equals(
                current.Status,
                "active",
                StringComparison.Ordinal))
        {
            return current;
        }

        var intervalMonths = ResolveBillingIntervalMonths(current);
        var commitmentMonths = ResolveCommitmentMonths(current);
        var effectiveStartedAt = ParseIsoUtc(current.StartedAt) ?? startedAtUtc;
        var effectiveNextBillingAt =
            ParseIsoUtc(current.NextBillingAt)
            ?? effectiveStartedAt.AddMonths(intervalMonths);
        var effectiveCommitmentEndsAt =
            ParseIsoUtc(current.CommitmentEndsAt)
            ?? effectiveStartedAt.AddMonths(commitmentMonths);

        var updated = await _repository.ActivateAsync(
            subscriptionId,
            effectiveStartedAt,
            effectiveNextBillingAt,
            effectiveCommitmentEndsAt,
            cancellationToken);
        await EnsurePriceLockAsync(
            updated,
            "legacy_subscription_activated",
            cancellationToken);
        await _billingV2Subscriptions.SyncFromLegacySubscriptionAsync(
            updated,
            cancellationToken);
        await TryReconcileProvisioningAsync(
            updated,
            "subscription.provisioning.activate",
            correlationId,
            requestedByUserId: null,
            cancellationToken);
        return updated;
    }

    private async Task EnsurePriceLockAsync(
        SubscriptionSummary subscription,
        CommercialOfferSummary offer,
        string reason,
        CancellationToken cancellationToken)
    {
        await _commercialRepository.EnsureSubscriptionPriceLockAsync(
            subscription.Id,
            offer.Id,
            offer.PriceAmountCents,
            offer.TaxRateBasisPoints,
            offer.Currency,
            reason,
            cancellationToken);
    }

    private async Task EnsurePriceLockAsync(
        SubscriptionSummary subscription,
        string reason,
        CancellationToken cancellationToken)
    {
        await _commercialRepository.EnsureSubscriptionPriceLockAsync(
            subscription.Id,
            subscription.CommercialOfferId,
            subscription.PriceAmountCents,
            subscription.TaxRateBasisPoints,
            subscription.Currency,
            reason,
            cancellationToken);
    }

    public async Task<SubscriptionSummary> RecordPaymentAsync(
        string subscriptionId,
        DateTime paidAtUtc,
        CancellationToken cancellationToken)
    {
        var current = await GetRequiredSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        var intervalMonths = ResolveBillingIntervalMonths(current);
        var commitmentMonths = ResolveCommitmentMonths(current);
        var cycleStart = current.PaidCyclesCount == 0
            ? ParseIsoUtc(current.StartedAt) ?? paidAtUtc
            : ParseIsoUtc(current.NextBillingAt)
                ?? ParseIsoUtc(current.StartedAt)
                ?? paidAtUtc;
        var nextBillingAt = cycleStart.AddMonths(intervalMonths);
        var currentCommitmentEndsAt =
            ParseIsoUtc(current.CommitmentEndsAt)
            ?? (ParseIsoUtc(current.StartedAt) ?? paidAtUtc).AddMonths(
                commitmentMonths);

        var nextCommitmentEndsAt = cycleStart >= currentCommitmentEndsAt
            ? currentCommitmentEndsAt.AddMonths(commitmentMonths)
            : currentCommitmentEndsAt;

        var updated = await _repository.RecordPaymentAsync(
            subscriptionId,
            nextBillingAt,
            nextCommitmentEndsAt,
            cancellationToken);
        await _billingV2Subscriptions.SyncFromLegacySubscriptionAsync(
            updated,
            cancellationToken);
        return updated;
    }

    public async Task<SubscriptionSummary> UpdateStatusAsync(
        string subscriptionId,
        string newStatus,
        string provisioningActionType,
        string correlationId,
        string? requestedByUserId,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedStatus(newStatus))
        {
            throw new PortalValidationException();
        }

        var current = await GetRequiredSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        var updated = string.Equals(
            current.Status,
            newStatus,
            StringComparison.Ordinal)
            ? current
            : await _repository.UpdateStatusAsync(
                subscriptionId,
                newStatus,
                cancellationToken);
        await _billingV2Subscriptions.SyncFromLegacySubscriptionAsync(
            updated,
            cancellationToken);

        if (ShouldReconcileProvisioning(newStatus))
        {
            await TryReconcileProvisioningAsync(
                updated,
                provisioningActionType,
                correlationId,
                requestedByUserId,
                cancellationToken);
        }

        return updated;
    }

    public async Task<SubscriptionSummary> ClientCancelAsync(
        PortalSessionContext session,
        string subscriptionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var current = await GetRequiredSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        if (!string.Equals(
                current.CustomerId,
                session.CustomerId,
                StringComparison.Ordinal))
        {
            throw new PortalDataNotFoundException();
        }

        return await CancelAsync(
            current,
            correlationId,
            session.UserId,
            "subscription.provisioning.client_cancel",
            cancellationToken);
    }

    public async Task<SubscriptionSummary> AdminCancelAsync(
        string subscriptionId,
        string correlationId,
        string? requestedByUserId,
        CancellationToken cancellationToken)
    {
        var current = await GetRequiredSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        return await CancelAsync(
            current,
            correlationId,
            requestedByUserId,
            "subscription.provisioning.admin_cancel",
            cancellationToken);
    }

    public async Task<SubscriptionProvisioningSummary> ReconcileProvisioningAsync(
        string subscriptionId,
        string actionType,
        string correlationId,
        string? requestedByUserId,
        IReadOnlyList<string>? targetUserSamAccountNames,
        CancellationToken cancellationToken)
    {
        var subscription = await GetRequiredSubscriptionAsync(
            subscriptionId,
            cancellationToken);
        return await _provisioningManager.ReconcileAsync(
            subscription,
            actionType,
            correlationId,
            requestedByUserId,
            targetUserSamAccountNames,
            cancellationToken);
    }

    private async Task<SubscriptionSummary> CancelAsync(
        SubscriptionSummary current,
        string correlationId,
        string? requestedByUserId,
        string provisioningActionType,
        CancellationToken cancellationToken)
    {
        if (current.Status is "cancelled" or "expired")
        {
            return current;
        }

        if (current.Status == "pending_cancellation")
        {
            return current;
        }

        var nextBillingAt = ParseIsoUtc(current.NextBillingAt);
        if (current.Status is "active" or "suspended"
            && nextBillingAt is not null
            && nextBillingAt > DateTime.UtcNow)
        {
            var updated = await _repository.RequestCancellationAsync(
                current.Id,
                DateTime.UtcNow,
                cancellationToken);
            await _billingV2Subscriptions.SyncFromLegacySubscriptionAsync(
                updated,
                cancellationToken);
            return updated;
        }

        return await UpdateStatusAsync(
            current.Id,
            "cancelled",
            provisioningActionType,
            correlationId,
            requestedByUserId,
            cancellationToken);
    }

    private async Task<SubscriptionSummary> GetRequiredSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
        => await _repository.GetByIdAsync(subscriptionId, cancellationToken)
            ?? throw new PortalDataNotFoundException();

    private async Task TryReconcileProvisioningAsync(
        SubscriptionSummary subscription,
        string actionType,
        string correlationId,
        string? requestedByUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _provisioningManager.ReconcileAsync(
                subscription,
                actionType,
                correlationId,
                requestedByUserId,
                targetUserSamAccountNames: null,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Subscription provisioning failed after transition for subscription {SubscriptionId} action {ActionType}",
                subscription.Id,
                actionType);
        }
    }

    private static bool ShouldReconcileProvisioning(string status)
        => status is "active" or "suspended" or "cancelled" or "expired";

    private static bool IsSupportedStatus(string status)
        => status is "pending_approval"
            or "pending_payment"
            or "pending_activation"
            or "pending_cancellation"
            or "active"
            or "suspended"
            or "cancelled"
            or "expired";

    private static int ResolveBillingIntervalMonths(SubscriptionSummary subscription)
        => Math.Clamp(subscription.BillingIntervalMonths, 1, 12);

    private static int ResolveCommitmentMonths(SubscriptionSummary subscription)
        => Math.Clamp(
            subscription.CommitmentMonths > 0
                ? subscription.CommitmentMonths
                : subscription.BillingIntervalMonths,
            1,
            12);

    private static DateTimeOffset ParseUpdatedAt(SubscriptionSummary subscription)
        => DateTimeOffset.TryParse(
            subscription.UpdatedAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
                ? parsed
                : DateTimeOffset.MinValue;

    private static DateTime? ParseIsoUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.SpecifyKind(
            DateTime.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            DateTimeKind.Utc);
    }
}
