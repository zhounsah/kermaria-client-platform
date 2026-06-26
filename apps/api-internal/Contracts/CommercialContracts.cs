using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record CommercialOfferSummary(
    string Id,
    string Name,
    string Description,
    string Category,
    string UnitLabel,
    string PriceKind,
    int PriceAmountCents,
    string Currency,
    int? TaxRateBasisPoints,
    string? ExternalReference,
    string Status,
    int DisplayOrder,
    string BillingCadence,
    [property: JsonPropertyName("paypalPlanIdSandbox")] string? PayPalPlanIdSandbox,
    [property: JsonPropertyName("paypalPlanIdLive")] string? PayPalPlanIdLive,
    string CreatedAt,
    string UpdatedAt);

public sealed record CommercialDocumentLine(
    string Id,
    string? OfferId,
    string Label,
    string Description,
    decimal Quantity,
    string UnitLabel,
    int UnitPriceCents,
    int? TaxRateBasisPoints,
    int LineTotalCents,
    int SortOrder,
    string CreatedAt,
    string UpdatedAt);

public record CommercialDocumentSummary(
    string Id,
    string DocumentType,
    string Status,
    string Title,
    string InternalReference,
    string Currency,
    int SubtotalAmountCents,
    int TaxAmountCents,
    int TotalAmountCents,
    string Disclaimer,
    string CreatedAt,
    string UpdatedAt,
    string? SharedAt,
    string? ServiceRequestId,
    string? ServiceRequestReference);

public record CommercialDocumentDetail(
    string Id,
    string DocumentType,
    string Status,
    string Title,
    string InternalReference,
    string Currency,
    int SubtotalAmountCents,
    int TaxAmountCents,
    int TotalAmountCents,
    string Disclaimer,
    string CreatedAt,
    string UpdatedAt,
    string? SharedAt,
    string? ServiceRequestId,
    string? ServiceRequestReference,
    IReadOnlyList<CommercialDocumentLine> Lines)
    : CommercialDocumentSummary(
        Id,
        DocumentType,
        Status,
        Title,
        InternalReference,
        Currency,
        SubtotalAmountCents,
        TaxAmountCents,
        TotalAmountCents,
        Disclaimer,
        CreatedAt,
        UpdatedAt,
        SharedAt,
        ServiceRequestId,
        ServiceRequestReference);

public record AdminCommercialDocumentSummary(
    string Id,
    string DocumentType,
    string Status,
    string Title,
    string InternalReference,
    string Currency,
    int SubtotalAmountCents,
    int TaxAmountCents,
    int TotalAmountCents,
    string Disclaimer,
    string CreatedAt,
    string UpdatedAt,
    string? SharedAt,
    string? ServiceRequestId,
    string? ServiceRequestReference,
    string CustomerReference,
    string CustomerName)
    : CommercialDocumentSummary(
        Id,
        DocumentType,
        Status,
        Title,
        InternalReference,
        Currency,
        SubtotalAmountCents,
        TaxAmountCents,
        TotalAmountCents,
        Disclaimer,
        CreatedAt,
        UpdatedAt,
        SharedAt,
        ServiceRequestId,
        ServiceRequestReference);

public record AdminCommercialDocumentDetail(
    string Id,
    string DocumentType,
    string Status,
    string Title,
    string InternalReference,
    string Currency,
    int SubtotalAmountCents,
    int TaxAmountCents,
    int TotalAmountCents,
    string Disclaimer,
    string CreatedAt,
    string UpdatedAt,
    string? SharedAt,
    string? ServiceRequestId,
    string? ServiceRequestReference,
    string CustomerReference,
    string CustomerName,
    string CreatedByDisplayName,
    IReadOnlyList<CommercialDocumentLine> Lines)
    : AdminCommercialDocumentSummary(
        Id,
        DocumentType,
        Status,
        Title,
        InternalReference,
        Currency,
        SubtotalAmountCents,
        TaxAmountCents,
        TotalAmountCents,
        Disclaimer,
        CreatedAt,
        UpdatedAt,
        SharedAt,
        ServiceRequestId,
        ServiceRequestReference,
        CustomerReference,
        CustomerName);

public sealed record CommercialOfferPayload(
    string? Name,
    string? Description,
    string? Category,
    string? UnitLabel,
    int? PriceAmountCents,
    string? Status,
    int? DisplayOrder,
    string? BillingCadence,
    [property: JsonPropertyName("paypalPlanIdSandbox")] string? PayPalPlanIdSandbox,
    [property: JsonPropertyName("paypalPlanIdLive")] string? PayPalPlanIdLive);

public sealed record CommercialDocumentPayload(
    string? CustomerReference,
    string? DocumentType,
    string? Title,
    string? Currency,
    string? ServiceRequestId,
    string? Disclaimer,
    string? Status);

public sealed record CommercialDocumentLinePayload(
    string? OfferId,
    string? Label,
    string? Description,
    decimal? Quantity,
    string? UnitLabel,
    int? UnitPriceCents,
    int? TaxRateBasisPoints,
    int? SortOrder);

public sealed record CommercialOfferMutationResponse(
    string Id,
    string Status,
    bool Changed,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

public sealed record CommercialDocumentMutationResponse(
    string Id,
    string InternalReference,
    string Status,
    bool Changed,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

public sealed record CommercialDocumentLineMutationResponse(
    string Id,
    string DocumentId,
    bool Changed,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
