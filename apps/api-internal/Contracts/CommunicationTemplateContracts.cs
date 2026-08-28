namespace Kermaria.ApiInternal.Contracts;

public sealed record CommunicationTemplateVariable(string Name, string Description);

public sealed record CommunicationTemplateRevisionItem(
    string Key,
    int Version,
    string Outcome,
    string? ActorUserId,
    string CorrelationId,
    string CreatedAt);

public sealed record EmailTemplateItem(
    string Key,
    string DisplayName,
    string Description,
    string Subject,
    string Body,
    bool Enabled,
    string Source,
    bool Customized,
    int Version,
    string? UpdatedAt,
    string DefaultSubject,
    string DefaultBody,
    bool TestSendSupported,
    IReadOnlyList<CommunicationTemplateVariable> Variables);

public sealed record NotificationTemplateItem(
    string Key,
    string DisplayName,
    string Description,
    string Title,
    string Message,
    bool Enabled,
    string Source,
    bool Customized,
    int Version,
    string? UpdatedAt,
    string DefaultTitle,
    string DefaultMessage,
    IReadOnlyList<CommunicationTemplateVariable> Variables);

public sealed record SystemSnippetItem(
    string Key,
    string DisplayName,
    string Description,
    string Body,
    string Source,
    bool Customized,
    int Version,
    string? UpdatedAt,
    string DefaultBody,
    int MaxLength);

public sealed record CommunicationTemplateCollection(
    IReadOnlyList<EmailTemplateItem> EmailTemplates,
    IReadOnlyList<NotificationTemplateItem> NotificationTemplates,
    IReadOnlyList<SystemSnippetItem> Snippets,
    bool Persistent);

public sealed record EmailTemplateUpdateRequest(
    string Subject,
    string Body,
    bool Enabled,
    int ExpectedVersion);

public sealed record NotificationTemplateUpdateRequest(
    string Title,
    string Message,
    bool Enabled,
    int ExpectedVersion);

public sealed record SystemSnippetUpdateRequest(
    string Body,
    int ExpectedVersion);

public sealed record CommunicationTemplateRestoreRequest(int ExpectedVersion);

public sealed record EmailTemplatePreviewRequest(string Subject, string Body);

public sealed record EmailTemplatePreviewResponse(
    string Code,
    string Message,
    string? Subject,
    string? Body,
    string CorrelationId);

public sealed record EmailTemplateTestRequest(string Recipient);

public sealed record EmailTemplateMutationResponse(
    string Code,
    string Message,
    EmailTemplateItem? Template,
    string CorrelationId);

public sealed record NotificationTemplateMutationResponse(
    string Code,
    string Message,
    NotificationTemplateItem? Template,
    string CorrelationId);

public sealed record SystemSnippetMutationResponse(
    string Code,
    string Message,
    SystemSnippetItem? Snippet,
    string CorrelationId);

public sealed record CommunicationTemplateSimpleResponse(
    string Code,
    string Message,
    string CorrelationId);

/// <summary>Textes systeme exposes au portail public, sans donnee sensible.</summary>
public sealed record PublicSystemSnippets(IReadOnlyDictionary<string, string> Snippets);
