namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record StoredEmailTemplate(
    string Key,
    string Subject,
    string Body,
    bool Enabled,
    int Version,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId = null);

public sealed record StoredNotificationTemplate(
    string Key,
    string Title,
    string Message,
    bool Enabled,
    int Version,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId = null);

public sealed record StoredSystemSnippet(
    string Key,
    string Body,
    int Version,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId = null);

public sealed record StoredTemplateRevision(
    string Key,
    int Version,
    string Outcome,
    string? ActorUserId,
    string CorrelationId,
    DateTime CreatedAtUtc);

/// <summary>
/// Persistance des communications administrables. Les tables sont
/// specialisees (migration 074) : elles ne transitent jamais par
/// <c>application_settings</c>.
/// </summary>
/// <remarks>
/// Chaque enregistrement porte sa propre revision : le modele et sa trace sont
/// ecrits dans la meme unite de travail. Separes, un modele d'e-mail pouvait
/// changer sans que l'historique le dise — et c'est l'historique qui sert a
/// savoir qui a change un message envoye a de vrais clients.
///
/// <c>false</c> signifie conflit de version. Une indisponibilite de la
/// persistance leve, pour ne pas se confondre avec un conflit.
/// </remarks>
public interface ICommunicationTemplateRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<StoredEmailTemplate>> GetEmailTemplatesAsync(
        CancellationToken cancellationToken);

    Task<bool> TrySaveEmailTemplateAsync(
        StoredEmailTemplate template,
        string displayName,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredTemplateRevision>> GetEmailRevisionsAsync(
        string templateKey,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredNotificationTemplate>> GetNotificationTemplatesAsync(
        CancellationToken cancellationToken);

    Task<bool> TrySaveNotificationTemplateAsync(
        StoredNotificationTemplate template,
        string displayName,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredTemplateRevision>> GetNotificationRevisionsAsync(
        string templateKey,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredSystemSnippet>> GetSnippetsAsync(
        CancellationToken cancellationToken);

    Task<bool> TrySaveSnippetAsync(
        StoredSystemSnippet snippet,
        string displayName,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredTemplateRevision>> GetSnippetRevisionsAsync(
        string snippetKey,
        int limit,
        CancellationToken cancellationToken);
}
