using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public static class RequestTypes
{
    public const string Support = "support";
    public const string Service = "service";
}

public static class RequestWorkflowStatuses
{
    public static readonly IReadOnlySet<string> Support =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "open",
            "in_progress",
            "waiting_for_customer",
            "resolved",
            "closed",
            "cancelled"
        };

    public static readonly IReadOnlySet<string> Service =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "received",
            "under_review",
            "accepted",
            "rejected",
            "cancelled",
            "completed"
        };

    public static bool IsValid(string requestType, string status)
        => requestType switch
        {
            RequestTypes.Support => Support.Contains(status),
            RequestTypes.Service => Service.Contains(status),
            _ => false
        };
}

public interface IRequestWorkflowService
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<SupportRequestSummary>> GetClientSupportRequestsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceRequestSummary>> GetClientServiceRequestsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
    Task<PortalSupportRequestDetail> GetClientSupportRequestAsync(
        PortalSessionContext session,
        string requestId,
        CancellationToken cancellationToken);
    Task<PortalServiceRequestDetail> GetClientServiceRequestAsync(
        PortalSessionContext session,
        string requestId,
        CancellationToken cancellationToken);
    Task<AdminActivityOverview> GetAdminActivityAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminSupportRequestSummary>> GetAdminSupportRequestsAsync(
        AdminRequestListQuery query,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminServiceRequestSummary>> GetAdminServiceRequestsAsync(
        AdminRequestListQuery query,
        CancellationToken cancellationToken);
    Task<AdminSupportRequestDetail> GetAdminSupportRequestAsync(
        string requestId,
        CancellationToken cancellationToken);
    Task<AdminServiceRequestDetail> GetAdminServiceRequestAsync(
        string requestId,
        CancellationToken cancellationToken);
    Task<RequestMutationResponse> UpdateStatusAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        RequestStatusPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
    Task<RequestMutationResponse> AddInternalNoteAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        RequestTextPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
    Task<RequestMutationResponse> AddPublicMessageAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        RequestTextPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
    Task<RequestMutationResponse> AddClientPublicMessageAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        RequestTextPayload payload,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class RequestWorkflowService : IRequestWorkflowService
{
    private const int MinimumTextLength = 3;
    private const int MaximumTextLength = 2000;
    private readonly IRequestWorkflowRepository _repository;

    public RequestWorkflowService(IRequestWorkflowRepository repository)
    {
        _repository = repository;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public Task<IReadOnlyList<SupportRequestSummary>>
        GetClientSupportRequestsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
        => _repository.GetClientSupportRequestsAsync(
            session,
            cancellationToken);

    public Task<IReadOnlyList<ServiceRequestSummary>>
        GetClientServiceRequestsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
        => _repository.GetClientServiceRequestsAsync(
            session,
            cancellationToken);

    public async Task<PortalSupportRequestDetail> GetClientSupportRequestAsync(
        PortalSessionContext session,
        string requestId,
        CancellationToken cancellationToken)
        => await _repository.GetClientSupportRequestAsync(
                session,
                ValidateIdentifier(requestId),
                cancellationToken)
            ?? throw new PortalDataNotFoundException();

    public async Task<PortalServiceRequestDetail> GetClientServiceRequestAsync(
        PortalSessionContext session,
        string requestId,
        CancellationToken cancellationToken)
        => await _repository.GetClientServiceRequestAsync(
                session,
                ValidateIdentifier(requestId),
                cancellationToken)
            ?? throw new PortalDataNotFoundException();

    public Task<AdminActivityOverview> GetAdminActivityAsync(
        CancellationToken cancellationToken)
        => _repository.GetAdminActivityAsync(cancellationToken);

    public Task<IReadOnlyList<AdminSupportRequestSummary>>
        GetAdminSupportRequestsAsync(
            AdminRequestListQuery query,
            CancellationToken cancellationToken)
        => _repository.GetAdminSupportRequestsAsync(
            ValidateAdminQuery(RequestTypes.Support, query),
            cancellationToken);

    public Task<IReadOnlyList<AdminServiceRequestSummary>>
        GetAdminServiceRequestsAsync(
            AdminRequestListQuery query,
            CancellationToken cancellationToken)
        => _repository.GetAdminServiceRequestsAsync(
            ValidateAdminQuery(RequestTypes.Service, query),
            cancellationToken);

    public async Task<AdminSupportRequestDetail> GetAdminSupportRequestAsync(
        string requestId,
        CancellationToken cancellationToken)
        => await _repository.GetAdminSupportRequestAsync(
                ValidateIdentifier(requestId),
                cancellationToken)
            ?? throw new PortalDataNotFoundException();

    public async Task<AdminServiceRequestDetail> GetAdminServiceRequestAsync(
        string requestId,
        CancellationToken cancellationToken)
        => await _repository.GetAdminServiceRequestAsync(
                ValidateIdentifier(requestId),
                cancellationToken)
            ?? throw new PortalDataNotFoundException();

    public Task<RequestMutationResponse> UpdateStatusAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        RequestStatusPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var status = payload.Status?.Trim();
        if (string.IsNullOrWhiteSpace(status)
            || !RequestWorkflowStatuses.IsValid(requestType, status))
        {
            throw new PortalValidationException();
        }

        return _repository.UpdateStatusAsync(
            actor,
            requestType,
            ValidateIdentifier(requestId),
            status,
            correlationId,
            cancellationToken);
    }

    public Task<RequestMutationResponse> AddInternalNoteAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        RequestTextPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.AddInternalNoteAsync(
            actor,
            ValidateRequestType(requestType),
            ValidateIdentifier(requestId),
            ValidateText(payload.Text),
            correlationId,
            cancellationToken);

    public Task<RequestMutationResponse> AddPublicMessageAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        RequestTextPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.AddPublicMessageAsync(
            actor,
            ValidateRequestType(requestType),
            ValidateIdentifier(requestId),
            ValidateText(payload.Text),
            correlationId,
            cancellationToken);

    public Task<RequestMutationResponse> AddClientPublicMessageAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        RequestTextPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
        => _repository.AddClientPublicMessageAsync(
            actor,
            ValidateRequestType(requestType),
            ValidateIdentifier(requestId),
            ValidateText(payload.Text),
            correlationId,
            cancellationToken);

    private static string ValidateIdentifier(string value)
    {
        var identifier = value.Trim();
        if (identifier.Length is < 1 or > 100)
        {
            throw new PortalValidationException();
        }

        return identifier;
    }

    private static string ValidateRequestType(string requestType)
        => requestType is RequestTypes.Support or RequestTypes.Service
            ? requestType
            : throw new PortalValidationException();

    private static string ValidateText(string? value)
    {
        var text = value?.Trim();
        if (text is null
            || text.Length < MinimumTextLength
            || text.Length > MaximumTextLength)
        {
            throw new PortalValidationException();
        }

        return text;
    }

    private static AdminRequestListQuery ValidateAdminQuery(
        string requestType,
        AdminRequestListQuery query)
    {
        var status = string.IsNullOrWhiteSpace(query.Status)
            ? null
            : query.Status.Trim();
        if (status is not null
            && !RequestWorkflowStatuses.IsValid(requestType, status))
        {
            throw new PortalValidationException();
        }

        var priority = string.IsNullOrWhiteSpace(query.Priority)
            ? null
            : query.Priority.Trim();
        if (requestType == RequestTypes.Support
            && priority is not null
            && priority is not ("low" or "normal" or "high"))
        {
            throw new PortalValidationException();
        }

        var order = query.Order.Trim().ToLowerInvariant();
        if (order is not ("newest" or "oldest" or "status"))
        {
            throw new PortalValidationException();
        }

        var attention = string.IsNullOrWhiteSpace(query.Attention)
            ? null
            : query.Attention.Trim().ToLowerInvariant();
        if (attention is not null
            && attention is not ("to_handle" or "client_reply"))
        {
            throw new PortalValidationException();
        }

        return new AdminRequestListQuery(status, priority, order, attention);
    }
}
