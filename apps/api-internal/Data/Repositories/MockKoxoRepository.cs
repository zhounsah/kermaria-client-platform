using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockKoxoRepository : IKoxoRepository
{
    private readonly List<KoxoRunSummary> _runs = [];

    public bool IsPersistent => false;

    public Task<IReadOnlyList<KoxoExportCandidate>> ListExportCandidatesAsync(
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<KoxoExportCandidate>>([]);

    public Task InsertRunAsync(
        KoxoRunInsert run,
        CancellationToken cancellationToken)
    {
        _runs.Insert(0, new KoxoRunSummary(
            DateTime.UtcNow.ToString("O"),
            run.Source,
            run.Status,
            run.SchemaVersion,
            run.UserCount,
            run.InvalidUserCount,
            run.CorrelationId,
            run.SourceAddress,
            run.SummaryMessage,
            run.GeneratedAtUtc?.ToString("O")));
        return Task.CompletedTask;
    }

    public Task<KoxoRunSummary?> GetLatestRunAsync(CancellationToken cancellationToken)
        => Task.FromResult(_runs.FirstOrDefault());

    public Task<KoxoRunSummary?> GetLatestRunBySourceAsync(
        string source,
        CancellationToken cancellationToken)
        => Task.FromResult(
            _runs.FirstOrDefault(run =>
                string.Equals(run.Source, source, StringComparison.Ordinal)));
}
