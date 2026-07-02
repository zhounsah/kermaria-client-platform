using System.Net;
using System.Net.Mail;
using Kermaria.ApiInternal.Data.Configuration;
using Microsoft.Extensions.Logging;

namespace Kermaria.ApiInternal.Services.Email;

public sealed class LiveEmailService : IEmailService
{
    private readonly EmailRuntimeConfiguration _configuration;
    private readonly ILogger<LiveEmailService> _logger;

    public LiveEmailService(
        EmailRuntimeConfiguration configuration,
        ILogger<LiveEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string ModeName => _configuration.ModeName;

    public bool SendsEnabled => _configuration.SendsEnabled;

    public async Task<EmailDeliveryResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration.SmtpHost)
            || string.IsNullOrWhiteSpace(_configuration.FromAddress))
        {
            return new EmailDeliveryResult(
                false,
                "config_invalid",
                "SMTP host or from address is not configured.");
        }

        // V0.30 partiel : allowlist des destinataires. Fail-closed par
        // défaut ; aucun appel SMTP n'est réalisé si le destinataire n'est
        // pas explicitement autorisé.
        if (!_configuration.IsRecipientAllowed(message.Recipient))
        {
            _logger.LogWarning(
                "Live email blocked by allowlist for template {Template} recipient {Recipient} correlation_id {CorrelationId}",
                message.Template,
                message.Recipient,
                message.CorrelationId);
            return new EmailDeliveryResult(
                false,
                "blocked_allowlist",
                "Recipient is not in EMAIL_LIVE_ALLOWLIST.");
        }

        using var smtp = new SmtpClient(
            _configuration.SmtpHost,
            _configuration.SmtpPort)
        {
            EnableSsl = _configuration.SmtpUseStartTls,
            Timeout = _configuration.RequestTimeoutMs,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(_configuration.SmtpUsername))
        {
            smtp.Credentials = new NetworkCredential(
                _configuration.SmtpUsername,
                _configuration.SmtpPassword ?? string.Empty);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(
                _configuration.FromAddress,
                _configuration.FromDisplayName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
        };
        mail.To.Add(new MailAddress(message.Recipient));
        mail.Headers.Add("X-Correlation-Id", message.CorrelationId);
        mail.Headers.Add("X-Template", message.Template);

        try
        {
            await smtp.SendMailAsync(mail, cancellationToken);
            return new EmailDeliveryResult(true, "sent", null);
        }
        catch (SmtpException ex)
        {
            _logger.LogWarning(
                ex,
                "SMTP delivery failed for template {Template} recipient {Recipient} status_code {StatusCode}",
                message.Template,
                message.Recipient,
                ex.StatusCode);
            return new EmailDeliveryResult(
                false,
                "smtp_error",
                $"{ex.StatusCode}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Email delivery failed for template {Template} recipient {Recipient}",
                message.Template,
                message.Recipient);
            return new EmailDeliveryResult(
                false,
                "delivery_error",
                ex.Message);
        }
    }
}
