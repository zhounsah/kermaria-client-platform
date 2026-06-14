using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockPortalNotificationStore
{
    public object SyncRoot { get; } = new();
    public List<MockPortalNotification> Notifications { get; } = [];
}

public sealed class MockPortalNotification
{
    public required string Id { get; init; }
    public required string CustomerReference { get; init; }
    public required string NotificationType { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? LinkUrl { get; init; }
    public string? ReadAt { get; set; }
    public required string CreatedAt { get; init; }
}

public sealed class MockPortalNotificationRepository
    : IPortalNotificationRepository
{
    private readonly MockPortalNotificationStore _store;

    public MockPortalNotificationRepository(MockPortalNotificationStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<IReadOnlyList<PortalNotificationSummary>>
        GetNotificationsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<PortalNotificationSummary>>(
                _store.Notifications
                    .Where(item =>
                        item.CustomerReference == session.CustomerReference)
                    .OrderByDescending(item => item.CreatedAt)
                    .Take(100)
                    .Select(ToSummary)
                    .ToArray());
        }
    }

    public Task<int> MarkAsReadAsync(
        PortalSessionContext session,
        string notificationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var notification = _store.Notifications.FirstOrDefault(item =>
                item.Id == notificationId
                && item.CustomerReference == session.CustomerReference)
                ?? throw new PortalDataNotFoundException();
            if (notification.ReadAt is not null)
            {
                return Task.FromResult(0);
            }

            notification.ReadAt = DateTime.UtcNow.ToString("O");
            return Task.FromResult(1);
        }
    }

    public Task<int> MarkAllAsReadAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var unread = _store.Notifications
                .Where(item =>
                    item.CustomerReference == session.CustomerReference
                    && item.ReadAt is null)
                .ToArray();
            var now = DateTime.UtcNow.ToString("O");
            foreach (var notification in unread)
            {
                notification.ReadAt = now;
            }

            return Task.FromResult(unread.Length);
        }
    }

    private static PortalNotificationSummary ToSummary(
        MockPortalNotification notification)
        => new(
            notification.Id,
            notification.NotificationType,
            notification.Title,
            notification.Message,
            notification.LinkUrl,
            notification.ReadAt is not null,
            notification.ReadAt,
            notification.CreatedAt);
}
