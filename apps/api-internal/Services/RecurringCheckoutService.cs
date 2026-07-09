using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public interface IRecurringCheckoutService
{
    bool IsPersistent { get; }

    Task<CheckoutSummaryResponse> GetSummaryAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<CheckoutBucketResponse<RecurringCheckoutItemResponse>> GetRecurringAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task<CheckoutRecurringMutationResponse> AddItemAsync(
        PortalSessionContext session,
        string? offerId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<CheckoutRecurringMutationResponse> RemoveItemAsync(
        PortalSessionContext session,
        string? offerId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<CheckoutRecurringConfirmResponse> ConfirmAsync(
        PortalSessionContext session,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed partial class RecurringCheckoutService : IRecurringCheckoutService
{
    private readonly IRecurringCheckoutRepository _repository;
    private readonly ICommercialService _catalog;
    private readonly ICartService _cart;
    private readonly ISubscriptionService _subscriptions;
    private readonly ICommercialRepository _commercial;
    private readonly IInvoiceIssuingService _issuing;
    private readonly IBilledRecurringCheckoutSchemaEnsurer _schemaEnsurer;
    private readonly ILogger<RecurringCheckoutService> _logger;

    public RecurringCheckoutService(
        IRecurringCheckoutRepository repository,
        ICommercialService catalog,
        ICartService cart,
        ISubscriptionService subscriptions,
        ICommercialRepository commercial,
        IInvoiceIssuingService issuing,
        IBilledRecurringCheckoutSchemaEnsurer schemaEnsurer,
        ILogger<RecurringCheckoutService> logger)
    {
        _repository = repository;
        _catalog = catalog;
        _cart = cart;
        _subscriptions = subscriptions;
        _commercial = commercial;
        _issuing = issuing;
        _schemaEnsurer = schemaEnsurer;
        _logger = logger;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public async Task<CheckoutSummaryResponse> GetSummaryAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var cart = await _cart.GetCartAsync(session.CustomerId, cancellationToken);
        var recurring = await GetRecurringAsync(
            session.CustomerId,
            cancellationToken);
        return new CheckoutSummaryResponse(
            cart,
            recurring,
            cart.ItemCount + recurring.ItemCount,
            cart.ItemCount > 0 && recurring.ItemCount > 0);
    }

    public async Task<CheckoutBucketResponse<RecurringCheckoutItemResponse>> GetRecurringAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var items = await ResolveItemsAsync(customerId, cancellationToken);
        return BuildRecurringSummary(items);
    }

    public async Task<CheckoutRecurringMutationResponse> AddItemAsync(
        PortalSessionContext session,
        string? offerId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var offer = await GetEligibleOfferAsync(offerId, cancellationToken);
        await _repository.UpsertItemAsync(
            session.CustomerId,
            offer.Id,
            offer.CommitmentMonths ?? offer.BillingIntervalMonths ?? 1,
            offer.PaymentMode ?? CommercialStatuses.PaymentModeMonthly,
            cancellationToken);
        var recurring = await GetRecurringAsync(
            session.CustomerId,
            cancellationToken);
        return new CheckoutRecurringMutationResponse(
            recurring,
            correlationId);
    }

    public async Task<CheckoutRecurringMutationResponse> RemoveItemAsync(
        PortalSessionContext session,
        string? offerId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var normalizedOfferId = ValidateIdentifier(offerId);
        await _repository.RemoveItemAsync(
            session.CustomerId,
            normalizedOfferId,
            cancellationToken);
        var recurring = await GetRecurringAsync(
            session.CustomerId,
            cancellationToken);
        return new CheckoutRecurringMutationResponse(
            recurring,
            correlationId);
    }

    public async Task<CheckoutRecurringConfirmResponse> ConfirmAsync(
        PortalSessionContext session,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var items = await ResolveItemsAsync(session.CustomerId, cancellationToken);
        if (items.Count == 0)
        {
            throw new EmptyRecurringCheckoutException();
        }

        var createdSubscriptions = new List<SubscriptionSummary>(items.Count);
        RecurringCheckoutDocumentCreationResult creation;

        try
        {
            foreach (var item in items)
            {
                createdSubscriptions.Add(
                    await _subscriptions.CreateBilledPendingAsync(
                        session,
                        item.Offer.Id,
                        cancellationToken));
            }

            creation = await _commercial.CreateRecurringCheckoutDocumentAsync(
                new RecurringCheckoutDocumentRequest(
                    session.CustomerId,
                    session.UserId,
                    $"Facture initiale abonnements - {createdSubscriptions.Count} ligne(s)",
                    createdSubscriptions.Select(subscription =>
                        new RecurringCheckoutDocumentSubscriptionRequest(
                            subscription.Id,
                            subscription.CommercialOfferId,
                            subscription.SetupFeeAmountCents)).ToArray()),
                correlationId,
                cancellationToken);
        }
        catch
        {
            foreach (var subscription in createdSubscriptions)
            {
                try
                {
                    await _subscriptions.UpdateStatusAsync(
                        subscription.Id,
                        "cancelled",
                        "subscription.provisioning.checkout_rollback",
                        correlationId,
                        session.UserId,
                        cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogError(
                        exception,
                        "Recurring checkout rollback failed for subscription {SubscriptionId}",
                        subscription.Id);
                }
            }

            throw;
        }

        try
        {
            var issueResult = await _issuing.IssueInvoiceAsync(
                creation.DocumentId,
                sendEmail: true,
                correlationId,
                cancellationToken);
            if (!issueResult.Succeeded)
            {
                _logger.LogWarning(
                    "Recurring checkout document {DocumentId} issued with non-success code {Code}: {Message}",
                    creation.DocumentId,
                    issueResult.Code,
                    issueResult.Message);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Recurring checkout document {DocumentId} issuing threw - document persisted, awaiting admin issue",
                creation.DocumentId);
        }

        await _repository.ClearAsync(session.CustomerId, cancellationToken);

        return new CheckoutRecurringConfirmResponse(
            creation.DocumentId,
            createdSubscriptions.Count,
            creation.TotalAmountCents,
            createdSubscriptions.Select(subscription => subscription.Id).ToArray(),
            correlationId);
    }

    private async Task<List<ResolvedRecurringCheckoutItem>> ResolveItemsAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var stored = await _repository.GetItemsAsync(customerId, cancellationToken);
        if (stored.Count == 0)
        {
            return [];
        }

        var catalog = await _catalog.GetClientCatalogAsync(cancellationToken);
        var byId = catalog.ToDictionary(offer => offer.Id);
        var resolved = new List<ResolvedRecurringCheckoutItem>(stored.Count);

        foreach (var item in stored)
        {
            if (byId.TryGetValue(item.OfferId, out var offer)
                && IsRecurringEligibleOffer(offer))
            {
                resolved.Add(new ResolvedRecurringCheckoutItem(
                    offer,
                    item.CommitmentMonths,
                    item.PaymentMode));
            }
            else
            {
                await _repository.RemoveItemAsync(
                    customerId,
                    item.OfferId,
                    cancellationToken);
            }
        }

        return resolved;
    }

    private async Task<CommercialOfferSummary> GetEligibleOfferAsync(
        string? offerId,
        CancellationToken cancellationToken)
    {
        var normalizedOfferId = ValidateIdentifier(offerId);
        var catalog = await _catalog.GetClientCatalogAsync(cancellationToken);
        var offer = catalog.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, normalizedOfferId, StringComparison.Ordinal));
        if (offer is null)
        {
            throw new PortalDataNotFoundException();
        }

        if (!IsRecurringEligibleOffer(offer))
        {
            throw new RecurringOfferNotEligibleException();
        }

        return offer;
    }

    private static CheckoutBucketResponse<RecurringCheckoutItemResponse> BuildRecurringSummary(
        IReadOnlyList<ResolvedRecurringCheckoutItem> items)
    {
        var responses = new List<RecurringCheckoutItemResponse>(items.Count);
        var subtotal = 0;
        foreach (var item in items)
        {
            var offer = item.Offer;
            var setupFeeAmountCents = offer.SetupFeeAmountCents ?? 0;
            var firstChargeAmountCents = offer.PriceAmountCents + setupFeeAmountCents;
            subtotal += firstChargeAmountCents;
            responses.Add(new RecurringCheckoutItemResponse(
                offer.Id,
                offer.Name,
                offer.Description,
                offer.Category,
                offer.UnitLabel,
                offer.PublicPackCode,
                offer.PriceAmountCents,
                setupFeeAmountCents,
                firstChargeAmountCents,
                offer.BillingIntervalMonths ?? 1,
                offer.CommitmentMonths ?? offer.BillingIntervalMonths ?? item.CommitmentMonths,
                offer.PaymentMode ?? item.PaymentMode,
                "EUR"));
        }

        return new CheckoutBucketResponse<RecurringCheckoutItemResponse>(
            responses,
            responses.Count,
            subtotal,
            "EUR");
    }

    private static bool IsRecurringEligibleOffer(CommercialOfferSummary offer)
        => string.Equals(
                offer.BillingCadence,
                CommercialStatuses.CadenceMonthly,
                StringComparison.Ordinal)
            && string.Equals(
                offer.Status,
                CommercialStatuses.OfferActive,
                StringComparison.Ordinal)
            && offer.PriceAmountCents > 0;

    private static string ValidateIdentifier(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || !IdentifierPattern().IsMatch(normalized))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
}

public sealed record ResolvedRecurringCheckoutItem(
    CommercialOfferSummary Offer,
    int CommitmentMonths,
    string PaymentMode);

public sealed class RecurringOfferNotEligibleException : Exception;

public sealed class EmptyRecurringCheckoutException : Exception;
