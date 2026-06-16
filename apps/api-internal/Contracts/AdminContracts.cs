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

public sealed record AdminActivityOverview(
    int SupportToHandleCount,
    int ServiceToHandleCount,
    int RecentClientReplyCount,
    int WaitingForCustomerCount,
    int ActiveRequestCount,
    IReadOnlyList<AdminActivityItem> RecentActivities);

public sealed record AdminActivityItem(
    string RequestType,
    string RequestId,
    string Reference,
    string CustomerReference,
    string CustomerName,
    string Subject,
    string Status,
    string AuthorType,
    string AuthorLabel,
    string OccurredAt);

public sealed record AdminCustomerSummary(
    string CustomerReference,
    string DisplayName,
    string Status,
    int ServiceCount,
    int OpenSupportRequestCount,
    string CreatedAt,
    string LastActivityAt);

public sealed record AdminCustomerDetail(
    ClientProfile Identity,
    string CreatedAt,
    string LastActivityAt,
    int PortalUserCount,
    int ActivePortalUserCount,
    int ActiveSessionCount,
    int ActiveServiceCount,
    int PendingInvoiceCount,
    int OpenSupportRequestCount,
    int ActiveServiceRequestCount,
    int SharedCommercialDocumentCount,
    IReadOnlyList<ServiceSummary> Services,
    IReadOnlyList<InvoiceSummary> Invoices,
    IReadOnlyList<AdminSupportRequestSummary> SupportRequests,
    IReadOnlyList<AdminServiceRequestSummary> ServiceRequests,
    IReadOnlyList<AdminCommercialDocumentSummary> CommercialDocuments,
    IReadOnlyList<AdminActivityItem> RecentActivity,
    IReadOnlyList<AdminAuditLogEntry> RecentAuditLogs);

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
    string UpdatedAt,
    bool HasRecentClientReply,
    bool RequiresAttention);

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
    string UpdatedAt,
    bool HasRecentClientReply,
    bool RequiresAttention);

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
    string Order = "newest",
    string? Attention = null);

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
