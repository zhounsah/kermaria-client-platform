using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record ClientProfile(
    string CompanyName,
    string CustomerReference,
    string ContactName,
    string Email,
    string Phone,
    string Address,
    string City,
    string Country,
    string AccountStatus);

public sealed record PortalSummary(
    string CustomerReference,
    string ContactName,
    int ActiveServiceCount,
    int PendingInvoiceCount,
    decimal PendingInvoiceTotal,
    int OpenSupportRequestCount,
    string LastUpdatedAt);

public sealed record ServiceSummary(
    string Id,
    string Reference,
    string Name,
    string Type,
    string Status,
    string Description,
    string? StartedAt,
    string Scope,
    string CommercialTerms,
    string? NextStep = null);

public sealed record InvoiceSummary(
    string Id,
    string Number,
    string Status,
    string IssuedAt,
    string DueAt,
    string Period,
    decimal TotalAmount,
    string Currency);

public sealed record SupportRequestSummary(
    string Id,
    string Reference,
    string Subject,
    string Status,
    string Priority,
    string ServiceName,
    string CreatedAt,
    string UpdatedAt);

public sealed record ServiceCatalogItem(
    string Id,
    string Name,
    string Category,
    string Description,
    string Scope,
    string CommercialTerms);

public sealed record SupportRequestPayload(
    string? ServiceId,
    string? Priority,
    string? Subject,
    string? Description);

public sealed record ServiceRequestPayload(
    string? CatalogItemId,
    string? Subject,
    string? Description);

public sealed record MockSubmissionResponse(
    string Reference,
    string Status,
    bool Persisted,
    string Message,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
