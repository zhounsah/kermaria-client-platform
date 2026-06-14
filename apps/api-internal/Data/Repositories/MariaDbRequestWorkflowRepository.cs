using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbRequestWorkflowRepository
    : IRequestWorkflowRepository
{
    private const int DefaultLimit = 100;
    private readonly string _connectionString;

    public MariaDbRequestWorkflowRepository(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<SupportRequestSummary>>
        GetClientSupportRequestsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
    {
        var requests = new List<SupportRequestSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                sr.id,
                sr.reference,
                sr.subject,
                sr.status,
                sr.priority,
                COALESCE(service.name, 'Compte client') AS service_name,
                sr.created_at,
                sr.updated_at
            FROM support_requests sr
            LEFT JOIN customer_services service
                ON service.id = sr.service_id
                AND service.customer_id = sr.customer_id
            WHERE sr.customer_id = @customer_id
            ORDER BY sr.updated_at DESC
            LIMIT 100;
            """;
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            requests.Add(new SupportRequestSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("reference"),
                reader.GetString("subject"),
                reader.GetString("status"),
                reader.GetString("priority"),
                reader.GetString("service_name"),
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("updated_at"))));
        }

        return requests;
    }

    public async Task<IReadOnlyList<ServiceRequestSummary>>
        GetClientServiceRequestsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
    {
        var requests = new List<ServiceRequestSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                request.id,
                request.reference,
                catalog.name AS catalog_item_name,
                request.context,
                request.status,
                request.created_at,
                request.updated_at
            FROM service_requests request
            INNER JOIN service_catalog catalog
                ON catalog.id = request.catalog_item_id
            WHERE request.customer_id = @customer_id
            ORDER BY request.updated_at DESC
            LIMIT 100;
            """;
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            requests.Add(new ServiceRequestSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("reference"),
                reader.GetString("catalog_item_name"),
                ExtractSubject(reader.GetString("context")),
                reader.GetString("status"),
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("updated_at"))));
        }

        return requests;
    }

    public async Task<PortalSupportRequestDetail?>
        GetClientSupportRequestAsync(
            PortalSessionContext session,
            string requestId,
            CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                sr.id,
                sr.reference,
                sr.subject,
                sr.description,
                sr.status,
                sr.priority,
                COALESCE(service.name, 'Compte client') AS service_name,
                sr.created_at,
                sr.updated_at
            FROM support_requests sr
            LEFT JOIN customer_services service
                ON service.id = sr.service_id
                AND service.customer_id = sr.customer_id
            WHERE sr.id = @request_id
              AND sr.customer_id = @customer_id;
            """;
        command.Parameters.AddWithValue("@request_id", requestId);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var detail = new PortalSupportRequestDetail(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("reference"),
            reader.GetString("subject"),
            reader.GetString("description"),
            reader.GetString("status"),
            reader.GetString("priority"),
            reader.GetString("service_name"),
            ToUtcIso(reader.GetDateTime("created_at")),
            ToUtcIso(reader.GetDateTime("updated_at")),
            [],
            []);
        await reader.DisposeAsync();

        return detail with
        {
            Events = await GetEventsAsync(
                connection,
                RequestTypes.Support,
                requestId,
                clientVisibleOnly: true,
                cancellationToken),
            PublicMessages = await GetPublicMessagesAsync(
                connection,
                RequestTypes.Support,
                requestId,
                session.UserId,
                adminView: false,
                cancellationToken)
        };
    }

    public async Task<PortalServiceRequestDetail?>
        GetClientServiceRequestAsync(
            PortalSessionContext session,
            string requestId,
            CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                request.id,
                request.reference,
                catalog.name AS catalog_item_name,
                request.context,
                request.status,
                request.created_at,
                request.updated_at
            FROM service_requests request
            INNER JOIN service_catalog catalog
                ON catalog.id = request.catalog_item_id
            WHERE request.id = @request_id
              AND request.customer_id = @customer_id;
            """;
        command.Parameters.AddWithValue("@request_id", requestId);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var context = reader.GetString("context");
        var detail = new PortalServiceRequestDetail(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("reference"),
            reader.GetString("catalog_item_name"),
            ExtractSubject(context),
            ExtractDescription(context),
            reader.GetString("status"),
            ToUtcIso(reader.GetDateTime("created_at")),
            ToUtcIso(reader.GetDateTime("updated_at")),
            [],
            []);
        await reader.DisposeAsync();

        return detail with
        {
            Events = await GetEventsAsync(
                connection,
                RequestTypes.Service,
                requestId,
                clientVisibleOnly: true,
                cancellationToken),
            PublicMessages = await GetPublicMessagesAsync(
                connection,
                RequestTypes.Service,
                requestId,
                session.UserId,
                adminView: false,
                cancellationToken)
        };
    }

    public async Task<IReadOnlyList<AdminSupportRequestSummary>>
        GetAdminSupportRequestsAsync(
            AdminRequestListQuery query,
            CancellationToken cancellationToken)
    {
        var requests = new List<AdminSupportRequestSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                sr.id,
                sr.reference,
                customer.external_reference AS customer_reference,
                customer.display_name AS customer_name,
                COALESCE(service.name, 'Compte client') AS service_name,
                sr.priority,
                sr.status,
                sr.subject,
                sr.created_at,
                sr.updated_at
            FROM support_requests sr
            INNER JOIN customers customer ON customer.id = sr.customer_id
            LEFT JOIN customer_services service
                ON service.id = sr.service_id
                AND service.customer_id = sr.customer_id
            WHERE (@status IS NULL OR sr.status = @status)
              AND (@priority IS NULL OR sr.priority = @priority)
            ORDER BY {OrderClause(query.Order, "sr")}
            LIMIT {DefaultLimit};
            """;
        command.Parameters.AddWithValue("@status", DbValue(query.Status));
        command.Parameters.AddWithValue("@priority", DbValue(query.Priority));

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            requests.Add(new AdminSupportRequestSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("reference"),
                reader.GetString("customer_reference"),
                reader.GetString("customer_name"),
                reader.GetString("service_name"),
                reader.GetString("priority"),
                reader.GetString("status"),
                reader.GetString("subject"),
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("updated_at"))));
        }

        return requests;
    }

    public async Task<IReadOnlyList<AdminServiceRequestSummary>>
        GetAdminServiceRequestsAsync(
            AdminRequestListQuery query,
            CancellationToken cancellationToken)
    {
        var requests = new List<AdminServiceRequestSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                request.id,
                request.reference,
                customer.external_reference AS customer_reference,
                customer.display_name AS customer_name,
                catalog.name AS catalog_item_name,
                request.context,
                request.status,
                request.created_at,
                request.updated_at
            FROM service_requests request
            INNER JOIN customers customer ON customer.id = request.customer_id
            INNER JOIN service_catalog catalog
                ON catalog.id = request.catalog_item_id
            WHERE (@status IS NULL OR request.status = @status)
            ORDER BY {OrderClause(query.Order, "request")}
            LIMIT {DefaultLimit};
            """;
        command.Parameters.AddWithValue("@status", DbValue(query.Status));

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var context = reader.GetString("context");
            requests.Add(new AdminServiceRequestSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("reference"),
                reader.GetString("customer_reference"),
                reader.GetString("customer_name"),
                reader.GetString("catalog_item_name"),
                ExtractSubject(context),
                Truncate(ExtractDescription(context), 240),
                reader.GetString("status"),
                true,
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("updated_at"))));
        }

        return requests;
    }

    public async Task<AdminSupportRequestDetail?> GetAdminSupportRequestAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                sr.id,
                sr.reference,
                customer.external_reference AS customer_reference,
                customer.display_name AS customer_name,
                COALESCE(service.name, 'Compte client') AS service_name,
                sr.priority,
                sr.status,
                sr.subject,
                sr.description,
                sr.created_at,
                sr.updated_at
            FROM support_requests sr
            INNER JOIN customers customer ON customer.id = sr.customer_id
            LEFT JOIN customer_services service
                ON service.id = sr.service_id
                AND service.customer_id = sr.customer_id
            WHERE sr.id = @request_id;
            """;
        command.Parameters.AddWithValue("@request_id", requestId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var detail = new AdminSupportRequestDetail(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("reference"),
            reader.GetString("customer_reference"),
            reader.GetString("customer_name"),
            reader.GetString("service_name"),
            reader.GetString("priority"),
            reader.GetString("status"),
            reader.GetString("subject"),
            reader.GetString("description"),
            ToUtcIso(reader.GetDateTime("created_at")),
            ToUtcIso(reader.GetDateTime("updated_at")),
            [],
            [],
            []);
        await reader.DisposeAsync();

        return detail with
        {
            Events = await GetEventsAsync(
                connection,
                RequestTypes.Support,
                requestId,
                clientVisibleOnly: false,
                cancellationToken),
            InternalNotes = await GetInternalNotesAsync(
                connection,
                RequestTypes.Support,
                requestId,
                cancellationToken),
            PublicMessages = await GetPublicMessagesAsync(
                connection,
                RequestTypes.Support,
                requestId,
                viewerUserId: null,
                adminView: true,
                cancellationToken)
        };
    }

    public async Task<AdminServiceRequestDetail?> GetAdminServiceRequestAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                request.id,
                request.reference,
                customer.external_reference AS customer_reference,
                customer.display_name AS customer_name,
                catalog.name AS catalog_item_name,
                request.context,
                request.status,
                request.created_at,
                request.updated_at
            FROM service_requests request
            INNER JOIN customers customer ON customer.id = request.customer_id
            INNER JOIN service_catalog catalog
                ON catalog.id = request.catalog_item_id
            WHERE request.id = @request_id;
            """;
        command.Parameters.AddWithValue("@request_id", requestId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var context = reader.GetString("context");
        var detail = new AdminServiceRequestDetail(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("reference"),
            reader.GetString("customer_reference"),
            reader.GetString("customer_name"),
            reader.GetString("catalog_item_name"),
            reader.GetString("status"),
            ExtractSubject(context),
            ExtractDescription(context),
            true,
            ToUtcIso(reader.GetDateTime("created_at")),
            ToUtcIso(reader.GetDateTime("updated_at")),
            [],
            [],
            []);
        await reader.DisposeAsync();

        return detail with
        {
            Events = await GetEventsAsync(
                connection,
                RequestTypes.Service,
                requestId,
                clientVisibleOnly: false,
                cancellationToken),
            InternalNotes = await GetInternalNotesAsync(
                connection,
                RequestTypes.Service,
                requestId,
                cancellationToken),
            PublicMessages = await GetPublicMessagesAsync(
                connection,
                RequestTypes.Service,
                requestId,
                viewerUserId: null,
                adminView: true,
                cancellationToken)
        };
    }

    public async Task<RequestMutationResponse> UpdateStatusAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string status,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);
        var current = await ReadMutationTargetAsync(
            connection,
            transaction,
            requestType,
            requestId,
            cancellationToken);

        if (current.Status == status)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RequestMutationResponse(
                requestId,
                current.Reference,
                status,
                false,
                correlationId);
        }

        var now = DateTime.UtcNow;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = requestType == RequestTypes.Support
                ? """
                  UPDATE support_requests
                  SET status = @status,
                      closed_at = CASE
                          WHEN @status IN ('closed', 'cancelled')
                              THEN @updated_at
                          ELSE NULL
                      END,
                      updated_at = @updated_at
                  WHERE id = @request_id;
                  """
                : """
                  UPDATE service_requests
                  SET status = @status,
                      updated_at = @updated_at
                  WHERE id = @request_id;
                  """;
            update.Parameters.AddWithValue("@status", status);
            update.Parameters.AddWithValue("@updated_at", now);
            update.Parameters.AddWithValue("@request_id", requestId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            requestType,
            requestId,
            actor.UserId,
            "status_changed",
            current.Status,
            status,
            correlationId,
            cancellationToken);
        await InsertNotificationAsync(
            connection,
            transaction,
            current.CustomerId,
            requestType,
            requestId,
            PortalNotificationFactory.ForStatus(
                requestType,
                requestId,
                status),
            now,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RequestMutationResponse(
            requestId,
            current.Reference,
            status,
            true,
            correlationId);
    }

    public Task<RequestMutationResponse> AddInternalNoteAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string note,
        string correlationId,
        CancellationToken cancellationToken)
        => AddTextAsync(
            actor,
            requestType,
            requestId,
            note,
            correlationId,
            "request_internal_notes",
            "note_text",
            "internal_note_added",
            requiredCustomerId: null,
            createClientNotification: false,
            cancellationToken);

    public Task<RequestMutationResponse> AddPublicMessageAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
        => AddTextAsync(
            actor,
            requestType,
            requestId,
            message,
            correlationId,
            "request_public_messages",
            "message_text",
            "public_message_added",
            requiredCustomerId: null,
            createClientNotification: true,
            cancellationToken);

    public Task<RequestMutationResponse> AddClientPublicMessageAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
        => AddTextAsync(
            actor,
            requestType,
            requestId,
            message,
            correlationId,
            "request_public_messages",
            "message_text",
            "public_message_added",
            requiredCustomerId: actor.CustomerId,
            createClientNotification: false,
            cancellationToken);

    private async Task<RequestMutationResponse> AddTextAsync(
        PortalSessionContext actor,
        string requestType,
        string requestId,
        string text,
        string correlationId,
        string tableName,
        string textColumn,
        string eventType,
        string? requiredCustomerId,
        bool createClientNotification,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);
        var current = await ReadMutationTargetAsync(
            connection,
            transaction,
            requestType,
            requestId,
            cancellationToken);
        if (requiredCustomerId is not null
            && !string.Equals(
                current.CustomerId,
                requiredCustomerId,
                StringComparison.Ordinal))
        {
            throw new PortalDataNotFoundException();
        }

        var now = DateTime.UtcNow;
        await TouchRequestAsync(
            connection,
            transaction,
            requestType,
            requestId,
            now,
            cancellationToken);

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                $"""
                INSERT INTO {tableName} (
                    id,
                    request_type,
                    request_id,
                    author_user_id,
                    {textColumn},
                    created_at
                ) VALUES (
                    @id,
                    @request_type,
                    @request_id,
                    @author_user_id,
                    @text,
                    @created_at
                );
                """;
            insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            insert.Parameters.AddWithValue("@request_type", requestType);
            insert.Parameters.AddWithValue("@request_id", requestId);
            insert.Parameters.AddWithValue("@author_user_id", actor.UserId);
            insert.Parameters.AddWithValue("@text", text);
            insert.Parameters.AddWithValue("@created_at", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            requestType,
            requestId,
            actor.UserId,
            eventType,
            null,
            null,
            correlationId,
            cancellationToken);
        if (eventType == "public_message_added" && createClientNotification)
        {
            await InsertNotificationAsync(
                connection,
                transaction,
                current.CustomerId,
                requestType,
                requestId,
                PortalNotificationFactory.ForPublicMessage(
                    requestType,
                    requestId),
                now,
                cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return new RequestMutationResponse(
            requestId,
            current.Reference,
            current.Status,
            true,
            correlationId);
    }

    private static async Task<RequestTarget> ReadMutationTargetAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string requestType,
        string requestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = requestType == RequestTypes.Support
            ? """
              SELECT customer_id, reference, status
              FROM support_requests
              WHERE id = @request_id
              FOR UPDATE;
              """
            : """
              SELECT customer_id, reference, status
              FROM service_requests
              WHERE id = @request_id
              FOR UPDATE;
              """;
        command.Parameters.AddWithValue("@request_id", requestId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new PortalDataNotFoundException();
        }

        return new RequestTarget(
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            reader.GetString("reference"),
            reader.GetString("status"));
    }

    private static async Task TouchRequestAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string requestType,
        string requestId,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = requestType == RequestTypes.Support
            ? """
              UPDATE support_requests
              SET updated_at = @updated_at
              WHERE id = @request_id;
              """
            : """
              UPDATE service_requests
              SET updated_at = @updated_at
              WHERE id = @request_id;
              """;
        command.Parameters.AddWithValue("@updated_at", updatedAt);
        command.Parameters.AddWithValue("@request_id", requestId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertNotificationAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string customerId,
        string requestType,
        string requestId,
        PortalNotificationContent content,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO portal_notifications (
                id,
                customer_id,
                request_type,
                request_id,
                notification_type,
                title,
                message,
                link_url,
                read_at,
                created_at
            ) VALUES (
                @id,
                @customer_id,
                @request_type,
                @request_id,
                @notification_type,
                @title,
                @message,
                @link_url,
                NULL,
                @created_at
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@customer_id", customerId);
        command.Parameters.AddWithValue("@request_type", requestType);
        command.Parameters.AddWithValue("@request_id", requestId);
        command.Parameters.AddWithValue(
            "@notification_type",
            content.NotificationType);
        command.Parameters.AddWithValue("@title", content.Title);
        command.Parameters.AddWithValue("@message", content.Message);
        command.Parameters.AddWithValue("@link_url", content.LinkUrl);
        command.Parameters.AddWithValue("@created_at", createdAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertEventAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string requestType,
        string requestId,
        string? actorUserId,
        string eventType,
        string? oldStatus,
        string? newStatus,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO request_events (
                id,
                request_type,
                request_id,
                actor_user_id,
                event_type,
                old_status,
                new_status,
                correlation_id,
                created_at
            ) VALUES (
                @id,
                @request_type,
                @request_id,
                @actor_user_id,
                @event_type,
                @old_status,
                @new_status,
                @correlation_id,
                @created_at
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@request_type", requestType);
        command.Parameters.AddWithValue("@request_id", requestId);
        command.Parameters.AddWithValue("@actor_user_id", DbValue(actorUserId));
        command.Parameters.AddWithValue("@event_type", eventType);
        command.Parameters.AddWithValue("@old_status", DbValue(oldStatus));
        command.Parameters.AddWithValue("@new_status", DbValue(newStatus));
        command.Parameters.AddWithValue("@correlation_id", correlationId);
        command.Parameters.AddWithValue("@created_at", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<RequestEventSummary>>
        GetEventsAsync(
            MySqlConnection connection,
            string requestType,
            string requestId,
            bool clientVisibleOnly,
            CancellationToken cancellationToken)
    {
        var events = new List<RequestEventSummary>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT event_type, old_status, new_status, created_at
            FROM request_events
            WHERE request_type = @request_type
              AND request_id = @request_id
              AND (
                  @client_visible_only = FALSE
                  OR event_type IN ('created', 'status_changed')
              )
            ORDER BY created_at, id;
            """;
        command.Parameters.AddWithValue("@request_type", requestType);
        command.Parameters.AddWithValue("@request_id", requestId);
        command.Parameters.AddWithValue(
            "@client_visible_only",
            clientVisibleOnly);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new RequestEventSummary(
                reader.GetString("event_type"),
                ReadNullableString(reader, "old_status"),
                ReadNullableString(reader, "new_status"),
                ToUtcIso(reader.GetDateTime("created_at"))));
        }

        return events;
    }

    private static async Task<IReadOnlyList<InternalRequestNote>>
        GetInternalNotesAsync(
            MySqlConnection connection,
            string requestType,
            string requestId,
            CancellationToken cancellationToken)
    {
        var notes = new List<InternalRequestNote>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                note.id,
                note.note_text,
                author.display_name,
                note.created_at
            FROM request_internal_notes note
            INNER JOIN portal_users author ON author.id = note.author_user_id
            WHERE note.request_type = @request_type
              AND note.request_id = @request_id
            ORDER BY note.created_at DESC, note.id DESC;
            """;
        command.Parameters.AddWithValue("@request_type", requestType);
        command.Parameters.AddWithValue("@request_id", requestId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notes.Add(new InternalRequestNote(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("note_text"),
                reader.GetString("display_name"),
                ToUtcIso(reader.GetDateTime("created_at"))));
        }

        return notes;
    }

    private static async Task<IReadOnlyList<PublicRequestMessage>>
        GetPublicMessagesAsync(
            MySqlConnection connection,
            string requestType,
            string requestId,
            string? viewerUserId,
            bool adminView,
            CancellationToken cancellationToken)
    {
        var messages = new List<PublicRequestMessage>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                message.id,
                message.message_text,
                message.author_user_id,
                author.display_name,
                author.role,
                message.created_at
            FROM request_public_messages message
            INNER JOIN portal_users author
                ON author.id = message.author_user_id
            WHERE message.request_type = @request_type
              AND message.request_id = @request_id
            ORDER BY message.created_at, message.id;
            """;
        command.Parameters.AddWithValue("@request_type", requestType);
        command.Parameters.AddWithValue("@request_id", requestId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var authorUserId = MariaDbIdentifierReader.ReadRequired(
                reader,
                "author_user_id");
            var authorType = reader.GetString("role") == PortalRoles.InternalAdmin
                ? "admin"
                : "client";
            var authorLabel = authorType == "admin"
                ? "Équipe Kermaria"
                : adminView
                    ? reader.GetString("display_name")
                    : string.Equals(
                        authorUserId,
                        viewerUserId,
                        StringComparison.Ordinal)
                        ? "Vous"
                        : "Votre organisation";
            messages.Add(new PublicRequestMessage(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("message_text"),
                authorLabel,
                authorType,
                ToUtcIso(reader.GetDateTime("created_at"))));
        }

        return messages;
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string OrderClause(string order, string alias)
        => order switch
        {
            "oldest" => $"{alias}.created_at ASC",
            "status" => $"{alias}.status ASC, {alias}.updated_at DESC",
            _ => $"{alias}.updated_at DESC"
        };

    private static string ExtractSubject(string context)
    {
        var firstLine = context
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .FirstOrDefault()
            ?? "Demande de service";
        return firstLine.StartsWith("Sujet :", StringComparison.OrdinalIgnoreCase)
            ? firstLine["Sujet :".Length..].Trim()
            : Truncate(firstLine, 160);
    }

    private static string ExtractDescription(string context)
    {
        var lines = context.Split(["\r\n", "\n"], StringSplitOptions.None);
        return string.Join(
            Environment.NewLine,
            lines.Skip(1).SkipWhile(string.IsNullOrWhiteSpace)).Trim();
    }

    private static string Truncate(string value, int maximumLength)
    {
        var trimmed = value.Trim();
        return trimmed[..Math.Min(trimmed.Length, maximumLength)];
    }

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;

    private sealed record RequestTarget(
        string CustomerId,
        string Reference,
        string Status);
}
