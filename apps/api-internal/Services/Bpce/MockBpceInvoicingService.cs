using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Bpce;

public sealed class MockBpceInvoicingService : IBpceInvoicingService
{
    private const string MockSenderId = "MOCK-SENDER-EI-ZACHARY";

    private static readonly BpceSenderInfo MockSender = new(
        Id: MockSenderId,
        Name: "Zachary HOUNSA-HOUNKPA EI (mock)",
        ProfileName: "Profil de facturation principal",
        Siren: "000000000",
        Siret: "00000000000000",
        Email: "mock-billing@example.invalid",
        Country: "FR",
        Locale: "fr",
        IsDefault: true,
        IsArchived: false);

    private readonly BpceRuntimeConfiguration _configuration;

    public MockBpceInvoicingService(BpceRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ModeName => _configuration.ModeName;

    public Task<BpceStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(new BpceStatusResponse(
            _configuration.ModeName,
            "mock",
            true,
            true,
            _configuration.BaseUrl,
            _configuration.RequestTimeoutMs));

    public Task<BpceServiceResult<IReadOnlyList<BpceSenderInfo>>> ListSendersAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(new BpceServiceResult<IReadOnlyList<BpceSenderInfo>>(
            StatusCodes.Status200OK,
            "BPCE_SENDERS_FOUND",
            "Mock BPCE senders returned.",
            new[] { MockSender }));

    public Task<BpceServiceResult<BpceSenderInfo>> GetSenderAsync(
        string senderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(senderId))
        {
            return Task.FromResult(new BpceServiceResult<BpceSenderInfo>(
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                "Sender identifier is required."));
        }

        if (!string.Equals(
                senderId.Trim(),
                MockSenderId,
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new BpceServiceResult<BpceSenderInfo>(
                StatusCodes.Status404NotFound,
                "BPCE_SENDER_NOT_FOUND",
                "Mock BPCE sender not found."));
        }

        return Task.FromResult(new BpceServiceResult<BpceSenderInfo>(
            StatusCodes.Status200OK,
            "BPCE_SENDER_FOUND",
            "Mock BPCE sender returned.",
            MockSender));
    }
}
