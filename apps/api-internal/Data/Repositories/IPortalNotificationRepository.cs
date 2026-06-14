using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public interface IPortalNotificationRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<PortalNotificationSummary>> GetNotificationsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<int> MarkAsReadAsync(
        PortalSessionContext session,
        string notificationId,
        CancellationToken cancellationToken);

    Task<int> MarkAllAsReadAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
}
