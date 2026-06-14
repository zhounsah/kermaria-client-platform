namespace Kermaria.ApiInternal.Contracts;

public sealed record AdminOverview(
    int CustomerCount,
    int ActiveUserCount,
    int ActiveSessionCount,
    int OpenSupportRequestCount,
    int RecentServiceRequestCount,
    IReadOnlyList<AdminAuditLogEntry> RecentAudits,
    string AdMode,
    bool AdOperationsEnabled);

public sealed record AdminCustomerSummary(
    string CustomerReference,
    string DisplayName,
    string Status,
    int ServiceCount,
    int OpenSupportRequestCount,
    string CreatedAt,
    string LastActivityAt);

public sealed record AdminSupportRequestSummary(
    string Id,
    string Reference,
    string CustomerReference,
    string CustomerName,
    string ServiceName,
    string Priority,
    string Status,
    string Subject,
    string CreatedAt,
    string UpdatedAt);

public sealed record AdminServiceRequestSummary(
    string Id,
    string Reference,
    string CustomerReference,
    string CustomerName,
    string CatalogItemName,
    string Subject,
    string DescriptionPreview,
    string Status,
    bool Persisted,
    string CreatedAt,
    string UpdatedAt);

public sealed record AdminSupportRequestDetail(
    string Id,
    string Reference,
    string CustomerReference,
    string CustomerName,
    string ServiceName,
    string Priority,
    string Status,
    string Subject,
    string Description,
    string CreatedAt,
    string UpdatedAt,
    IReadOnlyList<RequestEventSummary> Events,
    IReadOnlyList<InternalRequestNote> InternalNotes,
    IReadOnlyList<PublicRequestMessage> PublicMessages);

public sealed record AdminServiceRequestDetail(
    string Id,
    string Reference,
    string CustomerReference,
    string CustomerName,
    string CatalogItemName,
    string Status,
    string Subject,
    string Description,
    bool Persisted,
    string CreatedAt,
    string UpdatedAt,
    IReadOnlyList<RequestEventSummary> Events,
    IReadOnlyList<InternalRequestNote> InternalNotes,
    IReadOnlyList<PublicRequestMessage> PublicMessages);

public sealed record InternalRequestNote(
    string Id,
    string Note,
    string AuthorDisplayName,
    string CreatedAt);

public sealed record AdminRequestListQuery(
    string? Status,
    string? Priority,
    string Order = "newest");

public sealed record AdminSessionSummary(
    string UserDisplayName,
    string UserEmail,
    string Role,
    string? CustomerReference,
    string CreatedAt,
    string ExpiresAt,
    string? LastSeenAt,
    string? SourceAddress,
    string? UserAgent,
    string Status);

public sealed record AdminAuditLogEntry(
    string OccurredAt,
    string Actor,
    string Action,
    string Outcome,
    string? ReasonCode,
    string? CustomerReference,
    string CorrelationId,
    string? SourceAddress);

public sealed record RevokeOtherSessionsResponse(int RevokedCount);
