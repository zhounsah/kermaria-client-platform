using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record KoxoExportCandidate(
    string PortalUserId,
    string CustomerReference,
    string? KoxoUniqueIdentifier,
    string? PersonalTitle,
    string? GivenName,
    string? Surname,
    string? BirthDate,
    string Email);

public sealed record KoxoRunInsert(
    string Id,
    string Source,
    string Status,
    int? SchemaVersion,
    int UserCount,
    int InvalidUserCount,
    string CorrelationId,
    string? SourceAddress,
    string SummaryMessage,
    DateTime? GeneratedAtUtc,
    string? PreviewJson,
    string? ValidationErrorsJson);

public interface IKoxoRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<KoxoExportCandidate>> ListExportCandidatesAsync(
        CancellationToken cancellationToken);

    Task InsertRunAsync(
        KoxoRunInsert run,
        CancellationToken cancellationToken);

    Task<KoxoRunSummary?> GetLatestRunAsync(
        CancellationToken cancellationToken);

    Task<KoxoRunSummary?> GetLatestRunBySourceAsync(
        string source,
        CancellationToken cancellationToken);
}
