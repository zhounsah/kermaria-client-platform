namespace Kermaria.ApiInternal.Services.Email;

public sealed record EmailMessage(
    string Recipient,
    string Subject,
    string Body,
    string Template,
    string? RelatedDocumentId,
    string CorrelationId);

public sealed record EmailDeliveryResult(
    bool Succeeded,
    string Status,
    string? ErrorMessage);

public interface IEmailService
{
    string ModeName { get; }

    bool SendsEnabled { get; }

    Task<EmailDeliveryResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken);
}
