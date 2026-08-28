using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Resout le contenu d'une notification portail a partir des modeles
/// administrables, avec repli sur les textes integres au code.
/// </summary>
/// <remarks>
/// Les identifiants de notification, les codes de statut et les URL de lien
/// restent des invariants code (specification, section 8.2) : seuls le titre et
/// le message sont administrables.
///
/// Les appelants sont des repositories qui ecrivent la notification dans la
/// meme transaction que la demande. <see cref="PrimeAsync"/> est donc appele
/// avant l'ouverture de la transaction, et <see cref="Resolve"/> reste
/// synchrone : aucune lecture SQL n'est declenchee pendant la transaction.
/// </remarks>
public interface IPortalNotificationContentService
{
    Task PrimeAsync(CancellationToken cancellationToken);

    PortalNotificationContent ForStatus(
        string requestType,
        string requestId,
        string status);

    PortalNotificationContent ForPublicMessage(
        string requestType,
        string requestId);
}

public sealed class PortalNotificationContentService
    : IPortalNotificationContentService
{
    private readonly ICommunicationTemplateService _templates;
    private CommunicationTemplateSnapshot _snapshot =
        CommunicationTemplateSnapshot.Empty;

    public PortalNotificationContentService(ICommunicationTemplateService templates)
        => _templates = templates;

    public async Task PrimeAsync(CancellationToken cancellationToken)
        => _snapshot = await _templates.GetSnapshotAsync(cancellationToken);

    public PortalNotificationContent ForStatus(
        string requestType,
        string requestId,
        string status)
    {
        var descriptor = PortalNotificationFactory.DescribeStatus(requestType, status);
        return Compose(descriptor, requestId, status);
    }

    public PortalNotificationContent ForPublicMessage(
        string requestType,
        string requestId)
    {
        var descriptor = PortalNotificationFactory.DescribePublicMessage(requestType);
        return Compose(descriptor, requestId, status: null);
    }

    private PortalNotificationContent Compose(
        PortalNotificationDescriptor descriptor,
        string requestId,
        string? status)
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["requestId"] = requestId,
        };

        var title = descriptor.DefaultTitle;
        var message = descriptor.DefaultMessage;

        // Un texte specifique au statut prime sur le texte generique du type.
        if (status is not null
            && TryRead(
                CommunicationTemplateRegistry.NotificationKeyForStatus(
                    descriptor.NotificationType, status),
                out var statusTitle,
                out var statusMessage))
        {
            title = statusTitle;
            message = statusMessage;
        }
        else if (TryRead(descriptor.NotificationType, out var typeTitle, out var typeMessage))
        {
            title = typeTitle;
            // Le texte generique ne doit pas ecraser un message de statut
            // integre au code plus precis que lui.
            message = status is null ? typeMessage : descriptor.DefaultMessage;
        }

        return new PortalNotificationContent(
            descriptor.NotificationType,
            CommunicationTemplateRenderer.Render(title, variables),
            CommunicationTemplateRenderer.Render(message, variables),
            PortalNotificationFactory.BuildLinkUrl(descriptor.LinkTemplate, requestId));
    }

    private bool TryRead(string key, out string title, out string message)
    {
        if (_snapshot.NotificationTemplates.TryGetValue(key, out var stored)
            && stored.Enabled)
        {
            (title, message) = (stored.Title, stored.Message);
            return true;
        }

        (title, message) = (string.Empty, string.Empty);
        return false;
    }
}
