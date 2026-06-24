namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockEmailLogRepository : IEmailLogRepository
{
    private readonly object _lock = new();
    private readonly List<EmailLogEntry> _entries = new();

    public bool IsPersistent => false;

    public Task<string> RecordAsync(
        string template,
        string recipient,
        string subject,
        string body,
        string status,
        string? errorMessage,
        string? relatedDocumentId,
        string correlationId,
        bool delivered,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("D");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        lock (_lock)
        {
            _entries.Insert(0, new EmailLogEntry(
                id,
                template,
                recipient,
                subject.Length <= 255 ? subject : subject[..255],
                status,
                errorMessage,
                relatedDocumentId,
                correlationId,
                now,
                delivered ? now : null));
            if (_entries.Count > 500)
            {
                _entries.RemoveRange(500, _entries.Count - 500);
            }
        }
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<EmailLogEntry>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var capped = Math.Clamp(limit, 1, 500);
        lock (_lock)
        {
            IReadOnlyList<EmailLogEntry> result =
                _entries.Take(capped).ToList();
            return Task.FromResult(result);
        }
    }
}
