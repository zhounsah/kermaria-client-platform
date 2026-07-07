using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

// V0.35 — Panier / commande groupee a la carte.
// Le panier client regroupe des offres one-shot ; sa confirmation
// materialise un unique document commercial multi-lignes regle via les
// rails existants (Stripe / PayPal / virement).

public sealed record CartItemResponse(
    string OfferId,
    string Name,
    string Description,
    string Category,
    string UnitLabel,
    int UnitPriceCents,
    int? TaxRateBasisPoints,
    int Quantity,
    int LineTotalCents);

public sealed record CartSummaryResponse(
    IReadOnlyList<CartItemResponse> Items,
    int ItemCount,
    int SubtotalCents,
    string Currency);

public sealed record CartAddRequest(string? OfferId, int? Quantity);

public sealed record CartRemoveRequest(string? OfferId);

public sealed record CartMutationResponse(
    CartSummaryResponse Cart,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

public sealed record CartConfirmResponse(
    string DocumentId,
    int ItemCount,
    int TotalAmountCents,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
