using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Email;

public sealed class DisabledEmailService : IEmailService
{
    private readonly EmailRuntimeConfiguration _configuration;

    public DisabledEmailService(EmailRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ModeName => _configuration.ModeName;

    public bool SendsEnabled => false;

    public Task<EmailDeliveryResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
        => Task.FromResult(new EmailDeliveryResult(
            false,
            "disabled",
            "Email channel is disabled."));
}
