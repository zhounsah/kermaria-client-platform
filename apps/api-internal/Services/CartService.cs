using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

// V0.35 — Panier / commande groupee a la carte.
// Le panier ne regroupe que des offres one-shot (cadence one_time) actives.
// Sa confirmation materialise un unique document commercial multi-lignes
// (statut shared_with_customer) puis l'emet (BPCE), le rendant payable via
// les rails existants (Stripe / PayPal / virement). Aucune approbation admin
// prealable.
public interface ICartService
{
    bool IsPersistent { get; }

    Task<CartSummaryResponse> GetCartAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task<CartSummaryResponse> AddItemAsync(
        string customerId,
        string? offerId,
        int? quantity,
        CancellationToken cancellationToken);

    Task<CartSummaryResponse> RemoveItemAsync(
        string customerId,
        string? offerId,
        CancellationToken cancellationToken);

    Task<CartConfirmResponse> ConfirmAsync(
        string customerId,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed partial class CartService : ICartService
{
    private const int MaxQuantityPerItem = 99;
    private const int MaxDistinctItems = 50;

    private readonly ICartRepository _cart;
    private readonly ICommercialService _catalog;
    private readonly ICommercialRepository _commercial;
    private readonly IInvoiceIssuingService _issuing;
    private readonly ILogger<CartService> _logger;

    public CartService(
        ICartRepository cart,
        ICommercialService catalog,
        ICommercialRepository commercial,
        IInvoiceIssuingService issuing,
        ILogger<CartService> logger)
    {
        _cart = cart;
        _catalog = catalog;
        _commercial = commercial;
        _issuing = issuing;
        _logger = logger;
    }

    public bool IsPersistent => _cart.IsPersistent;

    public async Task<CartSummaryResponse> GetCartAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var items = await ResolveItemsAsync(customerId, cancellationToken);
        return BuildSummary(items);
    }

    public async Task<CartSummaryResponse> AddItemAsync(
        string customerId,
        string? offerId,
        int? quantity,
        CancellationToken cancellationToken)
    {
        var normalizedOfferId = ValidateIdentifier(offerId);
        var delta = quantity ?? 1;
        if (delta is < 1 or > MaxQuantityPerItem)
        {
            throw new PortalValidationException();
        }

        var catalog = await _catalog.GetClientCatalogAsync(cancellationToken);
        var offer = catalog.FirstOrDefault(candidate => candidate.Id == normalizedOfferId);
        if (offer is null)
        {
            throw new PortalDataNotFoundException();
        }

        // Le panier est strictement one-shot : les offres recurrentes relevent
        // du flux abonnement (V0.22 / V0.31), pas du panier.
        if (!string.Equals(
                offer.BillingCadence,
                CommercialStatuses.CadenceOneTime,
                StringComparison.Ordinal)
            || offer.PriceAmountCents <= 0)
        {
            throw new CartOfferNotEligibleException();
        }

        var existing = await _cart.GetItemsAsync(customerId, cancellationToken);
        var current = existing.FirstOrDefault(item => item.OfferId == normalizedOfferId);
        if (current is null && existing.Count >= MaxDistinctItems)
        {
            throw new CartOfferNotEligibleException();
        }

        var newQuantity = Math.Min(
            (current?.Quantity ?? 0) + delta,
            MaxQuantityPerItem);
        await _cart.UpsertItemAsync(
            customerId,
            normalizedOfferId,
            newQuantity,
            cancellationToken);

        var items = await ResolveItemsAsync(customerId, cancellationToken);
        return BuildSummary(items);
    }

    public async Task<CartSummaryResponse> RemoveItemAsync(
        string customerId,
        string? offerId,
        CancellationToken cancellationToken)
    {
        var normalizedOfferId = ValidateIdentifier(offerId);
        await _cart.RemoveItemAsync(customerId, normalizedOfferId, cancellationToken);
        var items = await ResolveItemsAsync(customerId, cancellationToken);
        return BuildSummary(items);
    }

    public async Task<CartConfirmResponse> ConfirmAsync(
        string customerId,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var items = await ResolveItemsAsync(customerId, cancellationToken);
        if (items.Count == 0)
        {
            throw new EmptyCartException();
        }

        var lines = new List<CartDocumentLineInput>(items.Count);
        var sortOrder = 10;
        var itemCount = 0;
        foreach (var (offer, quantity) in items)
        {
            lines.Add(new CartDocumentLineInput(
                offer.Id,
                offer.Name,
                offer.Description,
                quantity,
                offer.UnitLabel,
                offer.PriceAmountCents,
                offer.TaxRateBasisPoints,
                sortOrder));
            sortOrder += 10;
            itemCount += quantity;
        }

        var title = $"Commande à la carte — {items.Count} prestation(s)";
        var creation = await _commercial.CreateCartDocumentAsync(
            customerId,
            actorUserId,
            title,
            lines,
            correlationId,
            cancellationToken);

        // Emission immediate (BPCE mock en phase de tests) : rend le document
        // payable via les rails existants. Best-effort : si l'emission echoue,
        // le document existe deja et pourra etre emis cote admin.
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
                    "Cart document {DocumentId} issued with non-success code {Code}: {Message}",
                    creation.DocumentId,
                    issueResult.Code,
                    issueResult.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Cart document {DocumentId} issuing threw — document persisted, awaiting admin issue",
                creation.DocumentId);
        }

        await _cart.ClearAsync(customerId, cancellationToken);

        return new CartConfirmResponse(
            creation.DocumentId,
            itemCount,
            creation.TotalAmountCents,
            correlationId);
    }

    // Jointure panier <-> catalogue : ne retient que les offres encore actives
    // et one-shot ; auto-nettoie les lignes devenues indisponibles (offre
    // desactivee ou passee en recurrent entre l'ajout et la lecture).
    private async Task<List<(CommercialOfferSummary Offer, int Quantity)>>
        ResolveItemsAsync(string customerId, CancellationToken cancellationToken)
    {
        var stored = await _cart.GetItemsAsync(customerId, cancellationToken);
        if (stored.Count == 0)
        {
            return new List<(CommercialOfferSummary, int)>();
        }

        var catalog = await _catalog.GetClientCatalogAsync(cancellationToken);
        var byId = catalog.ToDictionary(offer => offer.Id);
        var resolved = new List<(CommercialOfferSummary, int)>();

        foreach (var item in stored)
        {
            if (byId.TryGetValue(item.OfferId, out var offer)
                && string.Equals(
                    offer.BillingCadence,
                    CommercialStatuses.CadenceOneTime,
                    StringComparison.Ordinal)
                && offer.PriceAmountCents > 0)
            {
                resolved.Add((offer, item.Quantity));
            }
            else
            {
                await _cart.RemoveItemAsync(
                    customerId,
                    item.OfferId,
                    cancellationToken);
            }
        }

        return resolved;
    }

    private static CartSummaryResponse BuildSummary(
        List<(CommercialOfferSummary Offer, int Quantity)> items)
    {
        var responses = new List<CartItemResponse>(items.Count);
        var subtotal = 0;
        var itemCount = 0;
        foreach (var (offer, quantity) in items)
        {
            var lineTotal = offer.PriceAmountCents * quantity;
            subtotal += lineTotal;
            itemCount += quantity;
            responses.Add(new CartItemResponse(
                offer.Id,
                offer.Name,
                offer.Description,
                offer.Category,
                offer.UnitLabel,
                offer.PriceAmountCents,
                offer.TaxRateBasisPoints,
                quantity,
                lineTotal));
        }

        return new CartSummaryResponse(responses, itemCount, subtotal, "EUR");
    }

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

public sealed class CartOfferNotEligibleException : Exception;

public sealed class EmptyCartException : Exception;
