using Kermaria.ApiInternal.Data.Configuration;
using Microsoft.Extensions.Logging;

namespace Kermaria.ApiInternal.Services.Email;

public sealed class MockEmailService : IEmailService
{
    private readonly EmailRuntimeConfiguration _configuration;
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(
        EmailRuntimeConfiguration configuration,
        ILogger<MockEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string ModeName => _configuration.ModeName;

    public bool SendsEnabled => false;

    public Task<EmailDeliveryResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "EMAIL[mock] template {Template} -> {Recipient} subject {Subject} document {DocumentId} correlation_id {CorrelationId}",
            message.Template,
            message.Recipient,
            message.Subject,
            message.RelatedDocumentId ?? "<none>",
            message.CorrelationId);
        return Task.FromResult(new EmailDeliveryResult(
            true,
            "mock_sent",
            null));
    }
}
