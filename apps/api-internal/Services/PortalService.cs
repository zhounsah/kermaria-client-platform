using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public sealed record SubmissionResponse(
    string Reference,
    string Status,
    bool Persisted,
    string Message,
    string CorrelationId);

public interface IPortalService
{
    bool IsPersistent { get; }

    Task<PortalSummary> GetSummaryAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<ClientProfile> GetProfileAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceSummary>> GetServicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceCatalogItem>> GetServiceCatalogAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportRequestSummary>> GetSupportRequestsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<SubmissionResponse> CreateSupportRequestAsync(
        PortalSessionContext session,
        SupportRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken);
    Task<SubmissionResponse> CreateServiceRequestAsync(
        PortalSessionContext session,
        ServiceRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken);
}

public sealed class PortalService : IPortalService
{
    private readonly IPortalRepository _repository;
    private readonly ILogger<PortalService> _logger;

    public PortalService(
        IPortalRepository repository,
        ILogger<PortalService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public Task<PortalSummary> GetSummaryAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => _repository.GetSummaryAsync(session, cancellationToken);

    public Task<ClientProfile> GetProfileAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => _repository.GetProfileAsync(session, cancellationToken);

    public Task<IReadOnlyList<ServiceSummary>> GetServicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => _repository.GetServicesAsync(session, cancellationToken);

    public Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => _repository.GetInvoicesAsync(session, cancellationToken);

    public Task<IReadOnlyList<ServiceCatalogItem>> GetServiceCatalogAsync(
        CancellationToken cancellationToken)
        => _repository.GetServiceCatalogAsync(cancellationToken);

    public Task<IReadOnlyList<SupportRequestSummary>> GetSupportRequestsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
        => _repository.GetSupportRequestsAsync(session, cancellationToken);

    public async Task<SubmissionResponse> CreateSupportRequestAsync(
        PortalSessionContext session,
        SupportRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        ValidateSupportRequest(payload);
        var result = await _repository.CreateSupportRequestAsync(
            session,
            payload,
            correlationId,
            sourceAddress,
            cancellationToken);

        _logger.LogInformation(
            "Audit action {Action} outcome {Outcome} persisted {Persisted} correlation_id {CorrelationId}",
            "support_request.create",
            "success",
            result.Persisted,
            correlationId);

        return result;
    }

    public async Task<SubmissionResponse> CreateServiceRequestAsync(
        PortalSessionContext session,
        ServiceRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        ValidateServiceRequest(payload);
        var result = await _repository.CreateServiceRequestAsync(
            session,
            payload,
            correlationId,
            sourceAddress,
            cancellationToken);

        _logger.LogInformation(
            "Audit action {Action} outcome {Outcome} persisted {Persisted} correlation_id {CorrelationId}",
            "service_request.create",
            "success",
            result.Persisted,
            correlationId);

        return result;
    }

    private static void ValidateSupportRequest(SupportRequestPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.ServiceId)
            || string.IsNullOrWhiteSpace(payload.Subject)
            || string.IsNullOrWhiteSpace(payload.Description)
            || !IsValidScopedIdentifier(payload.ServiceId, allowAccountAlias: true)
            || payload.Subject.Length > 160
            || payload.Description.Length > 4000
            || payload.Priority is not ("low" or "normal" or "high"))
        {
            throw new PortalValidationException();
        }
    }

    private static void ValidateServiceRequest(ServiceRequestPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.CatalogItemId)
            || string.IsNullOrWhiteSpace(payload.Subject)
            || string.IsNullOrWhiteSpace(payload.Description)
            || !IsValidScopedIdentifier(payload.CatalogItemId)
            || payload.Subject.Length > 160
            || payload.Description.Length > 4000)
        {
            throw new PortalValidationException();
        }
    }

    private static bool IsValidScopedIdentifier(
        string? value,
        bool allowAccountAlias = false)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (allowAccountAlias
            && string.Equals(
                normalized,
                "account",
                StringComparison.Ordinal))
        {
            return true;
        }

        if (normalized.Length is < 1 or > 100)
        {
            return false;
        }

        foreach (var character in normalized)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class PortalValidationException : Exception;
