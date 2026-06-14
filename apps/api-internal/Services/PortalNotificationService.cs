using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public sealed record PortalNotificationContent(
    string NotificationType,
    string Title,
    string Message,
    string LinkUrl);

public static class PortalNotificationFactory
{
    public static PortalNotificationContent ForStatus(
        string requestType,
        string requestId,
        string status)
    {
        var encodedId = Uri.EscapeDataString(requestId);
        return requestType switch
        {
            RequestTypes.Support => new(
                "support_status_changed",
                "Mise à jour de votre demande support",
                SupportStatusMessage(status),
                $"/support/{encodedId}"),
            RequestTypes.Service => new(
                "service_status_changed",
                "Mise à jour de votre demande de service",
                ServiceStatusMessage(status),
                $"/request-service/{encodedId}"),
            _ => throw new PortalValidationException()
        };
    }

    public static PortalNotificationContent ForPublicMessage(
        string requestType,
        string requestId)
    {
        var encodedId = Uri.EscapeDataString(requestId);
        return requestType switch
        {
            RequestTypes.Support => new(
                "support_public_message",
                "Nouveau message sur votre demande support",
                "Un nouveau message est disponible sur votre demande.",
                $"/support/{encodedId}"),
            RequestTypes.Service => new(
                "service_public_message",
                "Nouveau message sur votre demande de service",
                "Un nouveau message est disponible sur votre demande de service.",
                $"/request-service/{encodedId}"),
            _ => throw new PortalValidationException()
        };
    }

    private static string SupportStatusMessage(string status)
        => status switch
        {
            "open" => "Votre demande support est ouverte.",
            "in_progress" =>
                "Votre demande support est en cours de traitement.",
            "waiting_for_customer" =>
                "Votre demande support est en attente de votre retour.",
            "resolved" =>
                "Votre demande support a été indiquée comme résolue.",
            "closed" => "Votre demande support a été clôturée.",
            "cancelled" => "Votre demande support a été annulée.",
            _ => "Le statut de votre demande support a été mis à jour."
        };

    private static string ServiceStatusMessage(string status)
        => status switch
        {
            "received" => "Votre demande de service a été reçue.",
            "under_review" =>
                "Votre demande de service est en cours d'étude.",
            "accepted" =>
                "Votre demande de service a été acceptée. Elle sera traitée manuellement.",
            "rejected" =>
                "Votre demande de service n'a pas été retenue.",
            "cancelled" =>
                "Votre demande de service a été annulée.",
            "completed" =>
                "Le traitement manuel de votre demande de service est terminé.",
            _ => "Le statut de votre demande de service a été mis à jour."
        };
}

public interface IPortalNotificationService
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<PortalNotificationSummary>> GetNotificationsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<NotificationReadResponse> MarkAsReadAsync(
        PortalSessionContext session,
        string notificationId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<NotificationReadResponse> MarkAllAsReadAsync(
        PortalSessionContext session,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class PortalNotificationService : IPortalNotificationService
{
    private readonly IPortalNotificationRepository _repository;

    public PortalNotificationService(IPortalNotificationRepository repository)
    {
        _repository = repository;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public Task<IReadOnlyList<PortalNotificationSummary>>
        GetNotificationsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
        => _repository.GetNotificationsAsync(session, cancellationToken);

    public async Task<NotificationReadResponse> MarkAsReadAsync(
        PortalSessionContext session,
        string notificationId,
        string correlationId,
        CancellationToken cancellationToken)
        => new(
            await _repository.MarkAsReadAsync(
                session,
                ValidateIdentifier(notificationId),
                cancellationToken),
            correlationId);

    public async Task<NotificationReadResponse> MarkAllAsReadAsync(
        PortalSessionContext session,
        string correlationId,
        CancellationToken cancellationToken)
        => new(
            await _repository.MarkAllAsReadAsync(
                session,
                cancellationToken),
            correlationId);

    private static string ValidateIdentifier(string value)
    {
        var identifier = value.Trim();
        if (identifier.Length is < 1 or > 100)
        {
            throw new PortalValidationException();
        }

        return identifier;
    }
}
