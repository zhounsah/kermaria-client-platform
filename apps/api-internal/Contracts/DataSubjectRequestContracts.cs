namespace Kermaria.ApiInternal.Contracts;

public sealed record DataSubjectRequestCreatePayload(string? RequestType, string? Details);
public sealed record DataSubjectRequestMessagePayload(
    string? Body, string? Status, bool ExtendDeadline = false);
public sealed record DataSubjectRequestSummary(
    string Id, string Reference, string RequestType, string Status,
    string CreatedAt, string DueAt, string UpdatedAt,
    string? CustomerId = null);
public sealed record DataSubjectRequestMessage(
    string Id, string AuthorRole, string Body, string CreatedAt);
public sealed record DataSubjectRequestDetail(
    string Id, string Reference, string RequestType, string Details,
    string Status, string CreatedAt, string DueAt, string UpdatedAt,
    string CustomerId, string UserId,
    IReadOnlyList<DataSubjectRequestMessage> Messages,
    string? ResponseFileName = null,
    string? DeadlineExtendedAt = null);
