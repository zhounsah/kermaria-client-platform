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

// Coordonnees modifiables par le client depuis son espace. L'organisation, la
// reference client, l'e-mail (identifiant de connexion) et le statut ne sont
// volontairement pas exposes ici : ils restent pilotes par le back-office.
public sealed record ClientProfileUpdate(
    string ContactName,
    string Phone,
    string Address,
    string City,
    string Country);

public sealed record ClientProfileUpdateResult(
    string Code,
    string Message,
    ClientProfile Profile,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

public sealed record PortalSummary(
    string CustomerReference,
    string ContactName,
    int ActiveServiceCount,
    int PendingInvoiceCount,
    decimal PendingInvoiceTotal,
    int OpenSupportRequestCount,
    int ActiveServiceRequestCount,
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

public sealed record ClientVpsSpecifications(
    long? VcpuCount,
    long? RamGib,
    long? DiskGib);

/// <summary>
/// Projection operationnelle client d'un VPS achete. Cette forme est
/// intentionnellement distincte de la revue administrative : aucune cible
/// d'infrastructure, note operationnelle ou identifiant fournisseur n'est
/// expose ici.
/// </summary>
public sealed record ClientVpsSummary(
    string Id,
    string ServiceCode,
    string ServiceName,
    string TierCode,
    string TierLabel,
    string Hostname,
    string ProvisioningStatus,
    string? PublicIpAddress,
    DateTime? ProvisioningStartedAt,
    DateTime? ActivatedAt);

public sealed record ClientVpsDetail(
    string Id,
    string ServiceCode,
    string ServiceName,
    string TierCode,
    string TierLabel,
    string Hostname,
    string OperatingSystem,
    string Usage,
    string ManagementMode,
    string InternetExposure,
    string ProvisioningStatus,
    string? PublicIpAddress,
    DateTime? ProvisioningStartedAt,
    DateTime? ActivatedAt,
    ClientVpsSpecifications Specifications);

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

public sealed record ServiceRequestSummary(
    string Id,
    string Reference,
    string CatalogItemName,
    string Subject,
    string Status,
    string CreatedAt,
    string UpdatedAt);

public sealed record PortalSupportRequestDetail(
    string Id,
    string Reference,
    string Subject,
    string Description,
    string Status,
    string Priority,
    string ServiceName,
    string CreatedAt,
    string UpdatedAt,
    IReadOnlyList<RequestEventSummary> Events,
    IReadOnlyList<PublicRequestMessage> PublicMessages);

public sealed record PortalServiceRequestDetail(
    string Id,
    string Reference,
    string CatalogItemName,
    string Subject,
    string Description,
    string Status,
    string CreatedAt,
    string UpdatedAt,
    IReadOnlyList<RequestEventSummary> Events,
    IReadOnlyList<PublicRequestMessage> PublicMessages);

public sealed record RequestEventSummary(
    string EventType,
    string? OldStatus,
    string? NewStatus,
    string OccurredAt);

public sealed record PublicRequestMessage(
    string Id,
    string Message,
    string AuthorLabel,
    string AuthorType,
    string CreatedAt);

public sealed record PortalNotificationSummary(
    string Id,
    string NotificationType,
    string Title,
    string Message,
    string? LinkUrl,
    bool IsRead,
    string? ReadAt,
    string CreatedAt);

public sealed record NotificationReadResponse(
    int UpdatedCount,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

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

public sealed record RequestStatusPayload(string? Status);

public sealed record RequestTextPayload(string? Text);

/// <param name="FormuleCode">
/// Code d'une formule Billing V2 (<c>billing_v2_offer_presets.code</c>) quand le
/// visiteur arrive depuis une fiche. Purement contextuel : il est repris dans
/// le message et n'engage rien.
/// </param>
public sealed record ContactMessagePayload(
    string? Name,
    string? Email,
    string? Subject,
    string? Message,
    string? FormuleCode);

public sealed record RequestMutationResponse(
    string Id,
    string Reference,
    string Status,
    bool Changed,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);

public sealed record MockSubmissionResponse(
    string Reference,
    string Status,
    bool Persisted,
    string Message,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
