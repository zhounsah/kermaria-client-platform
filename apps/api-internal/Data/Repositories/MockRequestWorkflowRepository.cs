using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockRequestWorkflowStore
{
    public object SyncRoot { get; } = new();

    public List<MockSupportRequest> SupportRequests { get; } =
        MockPortalData.SupportRequests
            .Select(request => new MockSupportRequest(
                request.Id,
                request.Reference,
                MockPortalData.Profile.CustomerReference,
                MockPortalData.Profile.CompanyName,
                request.ServiceName,
                request.Priority,
                request.Status,
                request.Subject,
                "Description fictive sans donnée sensible.",
                request.CreatedAt,
                request.UpdatedAt))
            .ToList();

    public List<MockServiceRequest> ServiceRequests { get; } =
    [
        new(
            "service-request-mock-001",
            "SRV-MOCK-ADMIN-001",
            MockPortalData.Profile.CustomerReference,
            MockPortalData.Profile.CompanyName,
            "VPN privé",
            "Qualification d'un accès",
            "Demande fictive en lecture seule.",
            "received",
            "2026-06-12T10:00:00Z",
            "2026-06-12T10:00:00Z")
    ];

    public Dictionary<string, List<MockEvent>> Events { get; } = new();
    public Dictionary<string, List<InternalRequestNote>> InternalNotes { get; } =
        new();
    public Dictionary<string, List<MockPublicMessage>> PublicMessages
        { get; } = new();

    public MockRequestWorkflowStore()
    {
        foreach (var request in SupportRequests)
        {
            Events[Key(RequestTypes.Support, request.Id)] =
            [
                new(
                    "created",
                    null,
                    request.Status,
                    request.CreatedAt)
            ];
        }

        foreach (var request in ServiceRequests)
        {
            Events[Key(RequestTypes.Service, request.Id)] =
            [
                new(
                    "created",
                    null,
                    request.Status,
                    request.CreatedAt)
            ];
        }
    }

    public static string Key(string requestType, string requestId)
        => $"{requestType}:{requestId}";
}

public sealed class MockRequestWorkflowRepository
    : IRequestWorkflowRepository
{
    private readonly MockRequestWorkflowStore _store;
    private readonly MockPortalNotificationStore _notificationStore;

    public MockRequestWorkflowRepository(
        MockRequestWorkflowStore store,
        MockPortalNotificationStore notificationStore)
    {
        _store = store;
        _notificationStore = notificationStore;
    }

    public bool IsPersistent => false;

    public Task<IReadOnlyList<SupportRequestSummary>>
        GetClientSupportRequestsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<SupportRequestSummary>>(
                _store.SupportRequests
                    .Where(request =>
                        request.CustomerReference == session.CustomerReference)
                    .OrderByDescending(request => request.UpdatedAt)
                    .Select(ToClientSummary)
                    .ToArray());
        }
    }

    public Task<IReadOnlyList<ServiceRequestSummary>>
        GetClientServiceRequestsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequestSummary>>(
                _store.ServiceRequests
                    .Where(request =>
                        request.CustomerReference == session.CustomerReference)
                    .OrderByDescending(request => request.UpdatedAt)
                    .Select(request => new ServiceRequestSummary(
                        request.Id,
                        request.Reference,
                        request.CatalogItemName,
                        request.Subject,
                        request.Status,
                        request.CreatedAt,
                        request.UpdatedAt))
                    .ToArray());
        }
    }

    public Task<PortalSupportRequestDetail?> GetClientSupportRequestAsync(
        PortalSessionContext session,
        string requestId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var request = _store.SupportRequests.FirstOrDefault(candidate =>
                candidate.Id == requestId
                && candidate.CustomerReference == session.CustomerReference);
            return Task.FromResult(
                request is null
                    ? null
                    : new PortalSupportRequestDetail(
                        request.Id,
                        request.Reference,
                        request.Subject,
                        request.Description,
                        request.Status,
                        request.Priority,
                        request.ServiceName,
                        request.CreatedAt,
                        request.UpdatedAt,
                        ClientEvents(RequestTypes.Support, request.Id),
                        PublicMessages(
                            RequestTypes.Support,
                            request.Id,
                            session,
                            adminView: false)));
        }
    }

    public Task<PortalServiceRequestDetail?> GetClientServiceRequestAsync(
        PortalSessionContext session,
        string requestId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var request = _store.ServiceRequests.FirstOrDefault(candidate =>
                candidate.Id == requestId
                && candidate.CustomerReference == session.CustomerReference);
            return Task.FromResult(
                request is null
                    ? null
                    : new PortalServiceRequestDetail(
                        request.Id,
                        request.Reference,
                        request.CatalogItemName,
                        request.Subject,
                        request.Description,
                        request.Status,
                        request.CreatedAt,
                        request.UpdatedAt,
                        ClientEvents(RequestTypes.Service, request.Id),
                        PublicMessages(
                            RequestTypes.Service,
                            request.Id,
                            session,
                            adminView: false)));
        }
    }

    public Task<IReadOnlyList<AdminSupportRequestSummary>>
        GetAdminSupportRequestsAsync(
            AdminRequestListQuery query,
            CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            IEnumerable<MockSupportRequest> requests = _store.SupportRequests;
            if (query.Status is not null)
            {
                requests = requests.Where(request =>
                    request.Status == query.Status);
            }

            if (query.Priority is not null)
            {
                requests = requests.Where(request =>
                    request.Priority == query.Priority);
            }

            requests = Order(requests, query.Order);
            return Task.FromResult<IReadOnlyList<AdminSupportRequestSummary>>(
                requests.Select(request => new AdminSupportRequestSummary(
                    request.Id,
                    request.Reference,
                    request.CustomerReference,
                    request.CustomerName,
                    request.ServiceName,
                    request.Priority,
                    request.Status,
                    request.Subject,
                    request.CreatedAt,
                    request.UpdatedAt)).ToArray());
        }
    }

    public Task<IReadOnlyList<AdminServiceRequestSummary>>
        GetAdminServiceRequestsAsync(
            AdminRequestListQuery query,
            CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            IEnumerable<MockServiceRequest> requests = _store.ServiceRequests;
            if (query.Status is not null)
            {
                requests = requests.Where(request =>
                    request.Status == query.Status);
            }

            requests = Order(requests, query.Order);
            return Task.FromResult<IReadOnlyList<AdminServiceRequestSummary>>(
                requests.Select(request => new AdminServiceRequestSummary(
                    request.Id,
                    request.Reference,
                    request.CustomerReference,
                    request.CustomerName,
                    request.CatalogItemName,
                    request.Subject,
                    request.Description[..Math.Min(
                        request.Description.Length,
                        240)],
                    request.Status,
                    false,
                    request.CreatedAt,
                    request.UpdatedAt)).ToArray());
        }
    }

    public Task<AdminSupportRequestDetail?> GetAdminSupportRequestAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var request = _store.SupportRequests.FirstOrDefault(
                candidate => candidate.Id == requestId);
            return Task.FromResult(
                request is null
                    ? null
                    : new AdminSupportRequestDetail(
                        request.Id,
                        request.Reference,
                        request.CustomerReference,
                        request.CustomerName,
                        request.ServiceName,
                        request.Priority,
                        request.Status,
                        request.Subject,
                        request.Description,
                        request.CreatedAt,
                        request.UpdatedAt,
                        Events(RequestTypes.Support, request.Id),
                        InternalNotes(RequestTypes.Support, request.Id),
                        PublicMessages(
                            RequestTypes.Support,
                            request.Id,
                            viewer: null,
                            adminView: true)));
        }
    }

    public Task<AdminServiceRequestDetail?> GetAdminServiceRequestAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var request = _store.ServiceRequests.FirstOrDefault(
                candidate => candidate.Id == requestId);
            return Task.FromResult(
                request is null
                    ? null
                    : new AdminServiceRequestDetail(
                        request.Id,
                        request.Reference,
                        request.CustomerReference,
                        request.CustomerName,
                        request.CatalogItemName,
                        request.Status,
                        request.Subject,
                        request.Description,
                        false,
                        request.CreatedAt,
                        request.UpdatedAt,
                        Events(RequestTypes.Service, request.Id),
                        InternalNotes(RequestTypes.Service, request.Id),
                        PublicMessages(
                            RequestTypes.Service,
                            request.Id,
                            viewer: null,
                            adminView: true)));
        }
    }

    public Task<RequestMutationResponse> UpdateStatusAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string status,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var target = FindTarget(requestType, requestId);
            if (target.Status == status)
            {
                return Task.FromResult(new RequestMutationResponse(
                    requestId,
                    target.Reference,
                    status,
                    false,
                    correlationId));
            }

            var previousStatus = target.Status;
            var now = DateTime.UtcNow.ToString("O");
            target.SetStatus(status, now);
            EventsFor(requestType, requestId).Add(new MockEvent(
                "status_changed",
                previousStatus,
                status,
                now));
            AddNotification(
                target,
                PortalNotificationFactory.ForStatus(
                    requestType,
                    requestId,
                    status),
                now);

            return Task.FromResult(new RequestMutationResponse(
                requestId,
                target.Reference,
                status,
                true,
                correlationId));
        }
    }

    public Task<RequestMutationResponse> AddInternalNoteAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string note,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var target = FindTarget(requestType, requestId);
            var now = DateTime.UtcNow.ToString("O");
            target.SetStatus(target.Status, now);
            var notes = InternalNotesFor(requestType, requestId);
            notes.Add(new InternalRequestNote(
                Guid.NewGuid().ToString("D"),
                note,
                actor.DisplayName,
                now));
            EventsFor(requestType, requestId).Add(new MockEvent(
                "internal_note_added",
                null,
                null,
                now));

            return Task.FromResult(new RequestMutationResponse(
                requestId,
                target.Reference,
                target.Status,
                true,
                correlationId));
        }
    }

    public Task<RequestMutationResponse> AddPublicMessageAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var target = FindTarget(requestType, requestId);
            var now = DateTime.UtcNow.ToString("O");
            target.SetStatus(target.Status, now);
            var messages = PublicMessagesFor(requestType, requestId);
            messages.Add(new MockPublicMessage(
                Guid.NewGuid().ToString("D"),
                message,
                actor.UserId,
                actor.DisplayName,
                "admin",
                now));
            EventsFor(requestType, requestId).Add(new MockEvent(
                "public_message_added",
                null,
                null,
                now));
            AddNotification(
                target,
                PortalNotificationFactory.ForPublicMessage(
                    requestType,
                    requestId),
                now);

            return Task.FromResult(new RequestMutationResponse(
                requestId,
                target.Reference,
                target.Status,
                true,
                correlationId));
        }
    }

    public Task<RequestMutationResponse> AddClientPublicMessageAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var target = FindTarget(requestType, requestId);
            if (target.CustomerReference != actor.CustomerReference)
            {
                throw new PortalDataNotFoundException();
            }

            var now = DateTime.UtcNow.ToString("O");
            target.SetStatus(target.Status, now);
            PublicMessagesFor(requestType, requestId).Add(
                new MockPublicMessage(
                    Guid.NewGuid().ToString("D"),
                    message,
                    actor.UserId,
                    actor.DisplayName,
                    "client",
                    now));
            EventsFor(requestType, requestId).Add(new MockEvent(
                "public_message_added",
                null,
                null,
                now));

            return Task.FromResult(new RequestMutationResponse(
                requestId,
                target.Reference,
                target.Status,
                true,
                correlationId));
        }
    }

    private IRequestTarget FindTarget(string requestType, string requestId)
        => requestType switch
        {
            RequestTypes.Support => (IRequestTarget?)
                _store.SupportRequests.FirstOrDefault(
                    request => request.Id == requestId),
            RequestTypes.Service => (IRequestTarget?)
                _store.ServiceRequests.FirstOrDefault(
                    request => request.Id == requestId),
            _ => null
        } ?? throw new PortalDataNotFoundException();

    private IReadOnlyList<RequestEventSummary> ClientEvents(
        string requestType,
        string requestId)
        => Events(requestType, requestId)
            .Where(item =>
                item.EventType is "created" or "status_changed")
            .ToArray();

    private void AddNotification(
        IRequestTarget target,
        PortalNotificationContent content,
        string createdAt)
    {
        lock (_notificationStore.SyncRoot)
        {
            _notificationStore.Notifications.Add(new MockPortalNotification
            {
                Id = Guid.NewGuid().ToString("D"),
                CustomerReference = target.CustomerReference,
                NotificationType = content.NotificationType,
                Title = content.Title,
                Message = content.Message,
                LinkUrl = content.LinkUrl,
                CreatedAt = createdAt
            });
        }
    }

    private IReadOnlyList<RequestEventSummary> Events(
        string requestType,
        string requestId)
        => EventsFor(requestType, requestId)
            .Select(item => new RequestEventSummary(
                item.EventType,
                item.OldStatus,
                item.NewStatus,
                item.OccurredAt))
            .ToArray();

    private IReadOnlyList<InternalRequestNote> InternalNotes(
        string requestType,
        string requestId)
        => InternalNotesFor(requestType, requestId).ToArray();

    private IReadOnlyList<PublicRequestMessage> PublicMessages(
        string requestType,
        string requestId,
        PortalSessionContext? viewer,
        bool adminView)
        => PublicMessagesFor(requestType, requestId)
            .Select(message => new PublicRequestMessage(
                message.Id,
                message.Message,
                message.AuthorType == "admin"
                    ? "Équipe Kermaria"
                    : adminView
                        ? message.AuthorDisplayName
                        : message.AuthorUserId == viewer?.UserId
                            ? "Vous"
                            : "Votre organisation",
                message.AuthorType,
                message.CreatedAt))
            .ToArray();

    private List<MockEvent> EventsFor(string requestType, string requestId)
    {
        var key = MockRequestWorkflowStore.Key(requestType, requestId);
        if (!_store.Events.TryGetValue(key, out var events))
        {
            events = [];
            _store.Events[key] = events;
        }

        return events;
    }

    private List<InternalRequestNote> InternalNotesFor(
        string requestType,
        string requestId)
    {
        var key = MockRequestWorkflowStore.Key(requestType, requestId);
        if (!_store.InternalNotes.TryGetValue(key, out var notes))
        {
            notes = [];
            _store.InternalNotes[key] = notes;
        }

        return notes;
    }

    private List<MockPublicMessage> PublicMessagesFor(
        string requestType,
        string requestId)
    {
        var key = MockRequestWorkflowStore.Key(requestType, requestId);
        if (!_store.PublicMessages.TryGetValue(key, out var messages))
        {
            messages = [];
            _store.PublicMessages[key] = messages;
        }

        return messages;
    }

    private static SupportRequestSummary ToClientSummary(
        MockSupportRequest request)
        => new(
            request.Id,
            request.Reference,
            request.Subject,
            request.Status,
            request.Priority,
            request.ServiceName,
            request.CreatedAt,
            request.UpdatedAt);

    private static IEnumerable<T> Order<T>(
        IEnumerable<T> requests,
        string order)
        where T : IRequestTarget
        => order switch
        {
            "oldest" => requests.OrderBy(request => request.CreatedAt),
            "status" => requests
                .OrderBy(request => request.Status)
                .ThenByDescending(request => request.UpdatedAt),
            _ => requests.OrderByDescending(request => request.UpdatedAt)
        };
}

public interface IRequestTarget
{
    string Id { get; }
    string Reference { get; }
    string CustomerReference { get; }
    string Status { get; }
    string CreatedAt { get; }
    string UpdatedAt { get; }
    void SetStatus(string status, string updatedAt);
}

public sealed record MockSupportRequest(
    string Id,
    string Reference,
    string CustomerReference,
    string CustomerName,
    string ServiceName,
    string Priority,
    string InitialStatus,
    string Subject,
    string Description,
    string CreatedAt,
    string InitialUpdatedAt) : IRequestTarget
{
    public string Status { get; private set; } = InitialStatus;
    public string UpdatedAt { get; private set; } = InitialUpdatedAt;

    public void SetStatus(string status, string updatedAt)
    {
        Status = status;
        UpdatedAt = updatedAt;
    }
}

public sealed record MockServiceRequest(
    string Id,
    string Reference,
    string CustomerReference,
    string CustomerName,
    string CatalogItemName,
    string Subject,
    string Description,
    string InitialStatus,
    string CreatedAt,
    string InitialUpdatedAt) : IRequestTarget
{
    public string Status { get; private set; } = InitialStatus;
    public string UpdatedAt { get; private set; } = InitialUpdatedAt;

    public void SetStatus(string status, string updatedAt)
    {
        Status = status;
        UpdatedAt = updatedAt;
    }
}

public sealed record MockEvent(
    string EventType,
    string? OldStatus,
    string? NewStatus,
    string OccurredAt);

public sealed record MockPublicMessage(
    string Id,
    string Message,
    string AuthorUserId,
    string AuthorDisplayName,
    string AuthorType,
    string CreatedAt);
