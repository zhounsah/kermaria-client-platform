using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Email;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Instantane immuable des communications administrables. Il est recharge a la
/// demande et invalide apres chaque mutation : les reglages restent reellement
/// dynamiques sans relire la base a chaque e-mail (specification, section 24).
/// </summary>
public sealed record CommunicationTemplateSnapshot(
    IReadOnlyDictionary<string, StoredEmailTemplate> EmailTemplates,
    IReadOnlyDictionary<string, StoredNotificationTemplate> NotificationTemplates,
    IReadOnlyDictionary<string, StoredSystemSnippet> Snippets,
    DateTime LoadedAtUtc)
{
    public static readonly CommunicationTemplateSnapshot Empty = new(
        new Dictionary<string, StoredEmailTemplate>(StringComparer.Ordinal),
        new Dictionary<string, StoredNotificationTemplate>(StringComparer.Ordinal),
        new Dictionary<string, StoredSystemSnippet>(StringComparer.Ordinal),
        DateTime.MinValue);
}

public interface ICommunicationTemplateService
{
    bool IsPersistent { get; }

    /// <summary>Charge l'instantane si necessaire et le retourne.</summary>
    Task<CommunicationTemplateSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken);

    Task<(string Subject, string Body)> RenderEmailAsync(
        string templateKey,
        IReadOnlyDictionary<string, string?> variables,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> GetPublicSnippetsAsync(
        CancellationToken cancellationToken);

    Task<CommunicationTemplateCollection> GetAdminCollectionAsync(
        CancellationToken cancellationToken);

    Task<EmailTemplateMutationResponse> UpdateEmailTemplateAsync(
        string key,
        EmailTemplateUpdateRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<EmailTemplateMutationResponse> RestoreEmailTemplateAsync(
        string key,
        int expectedVersion,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    EmailTemplatePreviewResponse PreviewEmailTemplate(
        string key,
        EmailTemplatePreviewRequest request,
        string correlationId);

    Task<NotificationTemplateMutationResponse> UpdateNotificationTemplateAsync(
        string key,
        NotificationTemplateUpdateRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<NotificationTemplateMutationResponse> RestoreNotificationTemplateAsync(
        string key,
        int expectedVersion,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SystemSnippetMutationResponse> UpdateSnippetAsync(
        string key,
        SystemSnippetUpdateRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<SystemSnippetMutationResponse> RestoreSnippetAsync(
        string key,
        int expectedVersion,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CommunicationTemplateRevisionItem>> GetRevisionsAsync(
        string scope,
        string key,
        CancellationToken cancellationToken);
}

public sealed class CommunicationTemplateService : ICommunicationTemplateService
{
    /// <summary>
    /// Duree de vie du cache. Assez courte pour qu'une correction de texte
    /// prenne effet sans redemarrage, assez longue pour qu'un envoi en rafale
    /// ne relise pas la base a chaque message.
    /// </summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private static readonly object CacheGate = new();
    private static CommunicationTemplateSnapshot? _cached;

    private readonly ICommunicationTemplateRepository _repository;
    private readonly ILogger<CommunicationTemplateService> _logger;

    public CommunicationTemplateService(
        ICommunicationTemplateRepository repository,
        ILogger<CommunicationTemplateService> logger)
        => (_repository, _logger) = (repository, logger);

    public bool IsPersistent => _repository.IsPersistent;

    public static void Invalidate()
    {
        lock (CacheGate)
        {
            _cached = null;
        }
    }

    public async Task<CommunicationTemplateSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken)
    {
        lock (CacheGate)
        {
            if (_cached is not null
                && DateTime.UtcNow - _cached.LoadedAtUtc < CacheTtl)
            {
                return _cached;
            }
        }

        CommunicationTemplateSnapshot snapshot;
        try
        {
            var emails = await _repository.GetEmailTemplatesAsync(cancellationToken);
            var notifications =
                await _repository.GetNotificationTemplatesAsync(cancellationToken);
            var snippets = await _repository.GetSnippetsAsync(cancellationToken);
            snapshot = new CommunicationTemplateSnapshot(
                emails.ToDictionary(item => item.Key, StringComparer.Ordinal),
                notifications.ToDictionary(item => item.Key, StringComparer.Ordinal),
                snippets.ToDictionary(item => item.Key, StringComparer.Ordinal),
                DateTime.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Repli explicite : sans base, les gabarits integres au code
            // prennent le relais. Un e-mail critique doit partir meme quand la
            // personnalisation est indisponible.
            _logger.LogWarning(
                exception,
                "Communication templates unavailable; falling back to built-in templates.");
            return CommunicationTemplateSnapshot.Empty;
        }

        lock (CacheGate)
        {
            _cached = snapshot;
        }

        return snapshot;
    }

    public async Task<(string Subject, string Body)> RenderEmailAsync(
        string templateKey,
        IReadOnlyDictionary<string, string?> variables,
        CancellationToken cancellationToken)
    {
        var (defaultSubject, defaultBody) = EmailTemplates.Default(templateKey);
        var snapshot = await GetSnapshotAsync(cancellationToken);
        if (!snapshot.EmailTemplates.TryGetValue(templateKey, out var stored)
            || !stored.Enabled)
        {
            // Un modele desactive revient au texte integre plutot que de
            // supprimer un e-mail transactionnel attendu par le destinataire.
            return (
                CommunicationTemplateRenderer.Render(defaultSubject, variables),
                CommunicationTemplateRenderer.Render(defaultBody, variables));
        }

        return (
            CommunicationTemplateRenderer.Render(stored.Subject, variables),
            CommunicationTemplateRenderer.Render(stored.Body, variables));
    }

    public async Task<IReadOnlyDictionary<string, string>> GetPublicSnippetsAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return CommunicationTemplateRegistry.SystemSnippetDefinitions.ToDictionary(
            definition => definition.Key,
            definition => snapshot.Snippets.TryGetValue(definition.Key, out var stored)
                ? stored.Body
                : definition.DefaultBody,
            StringComparer.Ordinal);
    }

    public async Task<CommunicationTemplateCollection> GetAdminCollectionAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return new CommunicationTemplateCollection(
            CommunicationTemplateRegistry.EmailTemplateDefinitions
                .Select(definition => ToItem(
                    definition,
                    snapshot.EmailTemplates.GetValueOrDefault(definition.Key)))
                .ToArray(),
            CommunicationTemplateRegistry.NotificationTemplateDefinitions
                .Select(definition => ToItem(
                    definition,
                    snapshot.NotificationTemplates.GetValueOrDefault(definition.Key)))
                .ToArray(),
            CommunicationTemplateRegistry.SystemSnippetDefinitions
                .Select(definition => ToItem(
                    definition,
                    snapshot.Snippets.GetValueOrDefault(definition.Key)))
                .ToArray(),
            IsPersistent);
    }

    public Task<EmailTemplateMutationResponse> UpdateEmailTemplateAsync(
        string key,
        EmailTemplateUpdateRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
        => SaveEmailAsync(
            key,
            request.Subject,
            request.Body,
            request.Enabled,
            request.ExpectedVersion,
            actorUserId,
            correlationId,
            "updated",
            cancellationToken);

    public Task<EmailTemplateMutationResponse> RestoreEmailTemplateAsync(
        string key,
        int expectedVersion,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var definition = CommunicationTemplateRegistry.FindEmail(key);
        if (definition is null)
        {
            return Task.FromResult(new EmailTemplateMutationResponse(
                "TEMPLATE_UNKNOWN_KEY",
                "Ce modèle n'appartient pas au registre autorisé.",
                null,
                correlationId));
        }

        var (subject, body) = EmailTemplates.Default(key);
        return SaveEmailAsync(
            key,
            subject,
            body,
            Enabled: true,
            expectedVersion,
            actorUserId,
            correlationId,
            "restored",
            cancellationToken);
    }

    public EmailTemplatePreviewResponse PreviewEmailTemplate(
        string key,
        EmailTemplatePreviewRequest request,
        string correlationId)
    {
        var definition = CommunicationTemplateRegistry.FindEmail(key);
        if (definition is null)
        {
            return new EmailTemplatePreviewResponse(
                "TEMPLATE_UNKNOWN_KEY",
                "Ce modèle n'appartient pas au registre autorisé.",
                null,
                null,
                correlationId);
        }

        if (!TryValidateEmail(definition, request.Subject, request.Body,
                out var subject, out var body, out var failure))
        {
            return new EmailTemplatePreviewResponse(
                failure.Code, failure.Message, null, null, correlationId);
        }

        var sample = definition.Variables.ToDictionary(
            variable => variable.Name,
            variable => (string?)$"[{variable.Name}]",
            StringComparer.Ordinal);
        return new EmailTemplatePreviewResponse(
            "TEMPLATE_PREVIEW",
            "Aperçu généré avec des valeurs d'exemple.",
            CommunicationTemplateRenderer.Render(subject, sample),
            CommunicationTemplateRenderer.Render(body, sample),
            correlationId);
    }

    public async Task<NotificationTemplateMutationResponse> UpdateNotificationTemplateAsync(
        string key,
        NotificationTemplateUpdateRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
        => await SaveNotificationAsync(
            key,
            request.Title,
            request.Message,
            request.Enabled,
            request.ExpectedVersion,
            actorUserId,
            correlationId,
            "updated",
            cancellationToken);

    public async Task<NotificationTemplateMutationResponse> RestoreNotificationTemplateAsync(
        string key,
        int expectedVersion,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var definition = CommunicationTemplateRegistry.FindNotification(key);
        if (definition is null)
        {
            return new NotificationTemplateMutationResponse(
                "TEMPLATE_UNKNOWN_KEY",
                "Ce modèle n'appartient pas au registre autorisé.",
                null,
                correlationId);
        }

        return await SaveNotificationAsync(
            key,
            definition.DefaultTitle,
            definition.DefaultMessage,
            Enabled: true,
            expectedVersion,
            actorUserId,
            correlationId,
            "restored",
            cancellationToken);
    }

    public async Task<SystemSnippetMutationResponse> UpdateSnippetAsync(
        string key,
        SystemSnippetUpdateRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
        => await SaveSnippetAsync(
            key,
            request.Body,
            request.ExpectedVersion,
            actorUserId,
            correlationId,
            "updated",
            cancellationToken);

    public async Task<SystemSnippetMutationResponse> RestoreSnippetAsync(
        string key,
        int expectedVersion,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var definition = CommunicationTemplateRegistry.FindSnippet(key);
        if (definition is null)
        {
            return new SystemSnippetMutationResponse(
                "TEMPLATE_UNKNOWN_KEY",
                "Ce texte n'appartient pas au registre autorisé.",
                null,
                correlationId);
        }

        return await SaveSnippetAsync(
            key,
            definition.DefaultBody,
            expectedVersion,
            actorUserId,
            correlationId,
            "restored",
            cancellationToken);
    }

    public async Task<IReadOnlyList<CommunicationTemplateRevisionItem>> GetRevisionsAsync(
        string scope,
        string key,
        CancellationToken cancellationToken)
    {
        var revisions = scope switch
        {
            "email" when CommunicationTemplateRegistry.FindEmail(key) is not null
                => await _repository.GetEmailRevisionsAsync(key, 25, cancellationToken),
            "notification" when CommunicationTemplateRegistry.FindNotification(key) is not null
                => await _repository.GetNotificationRevisionsAsync(key, 25, cancellationToken),
            "snippet" when CommunicationTemplateRegistry.FindSnippet(key) is not null
                => await _repository.GetSnippetRevisionsAsync(key, 25, cancellationToken),
            _ => []
        };

        return revisions
            .Select(item => new CommunicationTemplateRevisionItem(
                item.Key,
                item.Version,
                item.Outcome,
                item.ActorUserId,
                item.CorrelationId,
                DateTime.SpecifyKind(item.CreatedAtUtc, DateTimeKind.Utc).ToString("O")))
            .ToArray();
    }

    private async Task<EmailTemplateMutationResponse> SaveEmailAsync(
        string key,
        string rawSubject,
        string rawBody,
        bool Enabled,
        int expectedVersion,
        string actorUserId,
        string correlationId,
        string outcome,
        CancellationToken cancellationToken)
    {
        var definition = CommunicationTemplateRegistry.FindEmail(key);
        if (definition is null)
        {
            return new EmailTemplateMutationResponse(
                "TEMPLATE_UNKNOWN_KEY",
                "Ce modèle n'appartient pas au registre autorisé.",
                null,
                correlationId);
        }

        if (!TryValidateEmail(definition, rawSubject, rawBody,
                out var subject, out var body, out var failure))
        {
            return new EmailTemplateMutationResponse(
                failure.Code, failure.Message, null, correlationId);
        }

        var next = new StoredEmailTemplate(
            key,
            subject,
            body,
            Enabled,
            expectedVersion + 1,
            DateTime.UtcNow,
            actorUserId);
        bool saved;
        try
        {
            saved = await _repository.TrySaveEmailTemplateAsync(
                next, definition.DisplayName, expectedVersion, outcome,
                correlationId, cancellationToken);
        }
        catch (Exception exception) when (
            exception is MySqlException or InvalidOperationException)
        {
            _logger.LogError(exception, "Ecriture impossible du modele {TemplateKey}.", key);
            return new EmailTemplateMutationResponse(
                "TEMPLATE_STORAGE_UNAVAILABLE",
                "Le modèle n'a pas pu être enregistré : rien n'a été modifié.",
                null,
                correlationId);
        }

        if (!saved)
        {
            return new EmailTemplateMutationResponse(
                "TEMPLATE_VERSION_CONFLICT",
                "Ce modèle a été modifié par un autre administrateur. Rechargez la page.",
                null,
                correlationId);
        }

        Invalidate();
        return new EmailTemplateMutationResponse(
            "TEMPLATE_UPDATED",
            outcome == "restored"
                ? "Modèle restauré à sa valeur par défaut."
                : "Modèle enregistré.",
            ToItem(definition, next),
            correlationId);
    }

    private async Task<NotificationTemplateMutationResponse> SaveNotificationAsync(
        string key,
        string rawTitle,
        string rawMessage,
        bool Enabled,
        int expectedVersion,
        string actorUserId,
        string correlationId,
        string outcome,
        CancellationToken cancellationToken)
    {
        var definition = CommunicationTemplateRegistry.FindNotification(key);
        if (definition is null)
        {
            return new NotificationTemplateMutationResponse(
                "TEMPLATE_UNKNOWN_KEY",
                "Ce modèle n'appartient pas au registre autorisé.",
                null,
                correlationId);
        }

        var allowed = definition.Variables.Select(item => item.Name).ToArray();
        if (!TryNormalize(rawTitle, CommunicationTemplateRegistry.MaxNotificationTitleLength,
                allowMultiline: false, allowed, out var title, out var failure)
            || !TryNormalize(rawMessage, CommunicationTemplateRegistry.MaxNotificationMessageLength,
                allowMultiline: false, allowed, out var message, out failure))
        {
            return new NotificationTemplateMutationResponse(
                failure.Code, failure.Message, null, correlationId);
        }

        var next = new StoredNotificationTemplate(
            key, title, message, Enabled, expectedVersion + 1, DateTime.UtcNow, actorUserId);
        bool saved;
        try
        {
            saved = await _repository.TrySaveNotificationTemplateAsync(
                next, definition.DisplayName, expectedVersion, outcome,
                correlationId, cancellationToken);
        }
        catch (Exception exception) when (
            exception is MySqlException or InvalidOperationException)
        {
            _logger.LogError(exception, "Ecriture impossible de la notification {TemplateKey}.", key);
            return new NotificationTemplateMutationResponse(
                "TEMPLATE_STORAGE_UNAVAILABLE",
                "La notification n'a pas pu être enregistrée : rien n'a été modifié.",
                null,
                correlationId);
        }

        if (!saved)
        {
            return new NotificationTemplateMutationResponse(
                "TEMPLATE_VERSION_CONFLICT",
                "Ce modèle a été modifié par un autre administrateur. Rechargez la page.",
                null,
                correlationId);
        }

        Invalidate();
        return new NotificationTemplateMutationResponse(
            "TEMPLATE_UPDATED",
            outcome == "restored"
                ? "Notification restaurée à sa valeur par défaut."
                : "Notification enregistrée.",
            ToItem(definition, next),
            correlationId);
    }

    private async Task<SystemSnippetMutationResponse> SaveSnippetAsync(
        string key,
        string rawBody,
        int expectedVersion,
        string actorUserId,
        string correlationId,
        string outcome,
        CancellationToken cancellationToken)
    {
        var definition = CommunicationTemplateRegistry.FindSnippet(key);
        if (definition is null)
        {
            return new SystemSnippetMutationResponse(
                "TEMPLATE_UNKNOWN_KEY",
                "Ce texte n'appartient pas au registre autorisé.",
                null,
                correlationId);
        }

        // Un snippet n'accepte aucune variable : c'est un texte fixe.
        if (!TryNormalize(rawBody, definition.MaxLength, allowMultiline: true,
                [], out var body, out var failure))
        {
            return new SystemSnippetMutationResponse(
                failure.Code, failure.Message, null, correlationId);
        }

        var next = new StoredSystemSnippet(
            key, body, expectedVersion + 1, DateTime.UtcNow, actorUserId);
        bool saved;
        try
        {
            saved = await _repository.TrySaveSnippetAsync(
                next, definition.DisplayName, expectedVersion, outcome,
                correlationId, cancellationToken);
        }
        catch (Exception exception) when (
            exception is MySqlException or InvalidOperationException)
        {
            _logger.LogError(exception, "Ecriture impossible du texte {SnippetKey}.", key);
            return new SystemSnippetMutationResponse(
                "TEMPLATE_STORAGE_UNAVAILABLE",
                "Le texte n'a pas pu être enregistré : rien n'a été modifié.",
                null,
                correlationId);
        }

        if (!saved)
        {
            return new SystemSnippetMutationResponse(
                "TEMPLATE_VERSION_CONFLICT",
                "Ce texte a été modifié par un autre administrateur. Rechargez la page.",
                null,
                correlationId);
        }

        Invalidate();
        return new SystemSnippetMutationResponse(
            "TEMPLATE_UPDATED",
            outcome == "restored"
                ? "Texte restauré à sa valeur par défaut."
                : "Texte enregistré.",
            ToItem(definition, next),
            correlationId);
    }

    private static bool TryValidateEmail(
        EmailTemplateDefinition definition,
        string rawSubject,
        string rawBody,
        out string subject,
        out string body,
        out (string Code, string Message) failure)
    {
        var allowed = definition.Variables.Select(item => item.Name).ToArray();
        body = string.Empty;
        return TryNormalize(rawSubject, CommunicationTemplateRegistry.MaxSubjectLength,
                   allowMultiline: false, allowed, out subject, out failure)
            && TryNormalize(rawBody, CommunicationTemplateRegistry.MaxBodyLength,
                   allowMultiline: true, allowed, out body, out failure);
    }

    /// <summary>
    /// Normalisation commune : bornes de longueur, caracteres de controle
    /// interdits et whitelist fermee de variables. Une variable inconnue fait
    /// echouer la sauvegarde, elle n'est jamais acceptee silencieusement.
    /// </summary>
    private static bool TryNormalize(
        string? raw,
        int maxLength,
        bool allowMultiline,
        IReadOnlyCollection<string> allowedVariables,
        out string value,
        out (string Code, string Message) failure)
    {
        value = (raw ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        failure = default;

        if (value.Length == 0)
        {
            failure = ("TEMPLATE_EMPTY", "Le contenu ne peut pas être vide.");
            return false;
        }

        if (value.Length > maxLength)
        {
            failure = (
                "TEMPLATE_TOO_LONG",
                $"Le contenu dépasse la longueur maximale de {maxLength} caractères.");
            return false;
        }

        foreach (var character in value)
        {
            if (character == '\n' && allowMultiline)
            {
                continue;
            }

            if (char.IsControl(character))
            {
                failure = (
                    "TEMPLATE_INVALID_CHARACTER",
                    allowMultiline
                        ? "Le contenu comporte un caractère de contrôle interdit."
                        : "Ce champ doit tenir sur une seule ligne, sans caractère de contrôle.");
                return false;
            }
        }

        if (!CommunicationTemplateRenderer.UsesOnlyAllowedVariables(value, allowedVariables))
        {
            failure = (
                "TEMPLATE_UNKNOWN_VARIABLE",
                allowedVariables.Count == 0
                    ? "Ce texte n'accepte aucune variable."
                    : "Le contenu utilise une variable inconnue. Variables autorisées : "
                      + string.Join(", ", allowedVariables.Select(name => "{{" + name + "}}"))
                      + ".");
            return false;
        }

        return true;
    }

    private static EmailTemplateItem ToItem(
        EmailTemplateDefinition definition,
        StoredEmailTemplate? stored)
    {
        var (defaultSubject, defaultBody) = EmailTemplates.Default(definition.Key);
        return new EmailTemplateItem(
            definition.Key,
            definition.DisplayName,
            definition.Description,
            stored?.Subject ?? defaultSubject,
            stored?.Body ?? defaultBody,
            stored?.Enabled ?? true,
            stored is null ? "code" : "database",
            stored is not null
                && (!string.Equals(stored.Subject, defaultSubject, StringComparison.Ordinal)
                    || !string.Equals(stored.Body, defaultBody, StringComparison.Ordinal)),
            stored?.Version ?? 0,
            stored is null
                ? null
                : DateTime.SpecifyKind(stored.UpdatedAtUtc, DateTimeKind.Utc).ToString("O"),
            defaultSubject,
            defaultBody,
            definition.TestSendSupported,
            definition.Variables);
    }

    private static NotificationTemplateItem ToItem(
        NotificationTemplateDefinition definition,
        StoredNotificationTemplate? stored)
        => new(
            definition.Key,
            definition.DisplayName,
            definition.Description,
            stored?.Title ?? definition.DefaultTitle,
            stored?.Message ?? definition.DefaultMessage,
            stored?.Enabled ?? true,
            stored is null ? "code" : "database",
            stored is not null
                && (!string.Equals(stored.Title, definition.DefaultTitle, StringComparison.Ordinal)
                    || !string.Equals(stored.Message, definition.DefaultMessage, StringComparison.Ordinal)),
            stored?.Version ?? 0,
            stored is null
                ? null
                : DateTime.SpecifyKind(stored.UpdatedAtUtc, DateTimeKind.Utc).ToString("O"),
            definition.DefaultTitle,
            definition.DefaultMessage,
            definition.Variables);

    private static SystemSnippetItem ToItem(
        SystemSnippetDefinition definition,
        StoredSystemSnippet? stored)
        => new(
            definition.Key,
            definition.DisplayName,
            definition.Description,
            stored?.Body ?? definition.DefaultBody,
            stored is null ? "code" : "database",
            stored is not null
                && !string.Equals(stored.Body, definition.DefaultBody, StringComparison.Ordinal),
            stored?.Version ?? 0,
            stored is null
                ? null
                : DateTime.SpecifyKind(stored.UpdatedAtUtc, DateTimeKind.Utc).ToString("O"),
            definition.DefaultBody,
            definition.MaxLength);
}
