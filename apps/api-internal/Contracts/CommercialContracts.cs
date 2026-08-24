using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record CommercialDocumentLine(
    string Id,
    string Label,
    string Description,
    decimal Quantity,
    string UnitLabel,
    int UnitPriceCents,
    int? TaxRateBasisPoints,
    string FiscalRegime,
    string FiscalMention,
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
    string? ServiceRequestReference,
    string? PaymentMethod);

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
    string? PaymentMethod,
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
        ServiceRequestReference,
        PaymentMethod);

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
    string? PaymentMethod,
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
        ServiceRequestReference,
        PaymentMethod);

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
    string? PaymentMethod,
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
        PaymentMethod,
        CustomerReference,
        CustomerName);

public sealed record CommercialDocumentPayload(
    string? CustomerReference,
    string? DocumentType,
    string? Title,
    string? Currency,
    string? ServiceRequestId,
    string? Disclaimer,
    string? Status);

public sealed record PaymentConfirmPayload(string? PaymentMethod);

public sealed record PaymentMethodSelectionPayload(string? PaymentMethod);

public sealed record CommercialDocumentLinePayload(
    string? Label,
    string? Description,
    decimal? Quantity,
    string? UnitLabel,
    int? UnitPriceCents,
    int? TaxRateBasisPoints,
    int? SortOrder);

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
