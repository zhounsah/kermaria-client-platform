using System.Collections.Concurrent;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed class MockDataSubjectRequestStore
{
    public ConcurrentDictionary<string, DataSubjectRequestDetail> Requests { get; } = new();
    public ConcurrentDictionary<string, (string FileName, string ContentType, byte[] Data)> Files { get; } = new();
    public ConcurrentDictionary<string, string> CustomerReferences { get; } = new();
}

public sealed class DataSubjectRequestService
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "access", "rectification", "erasure", "portability", "objection", "restriction"
    };
    private static readonly HashSet<string> AdminStatuses = new(StringComparer.Ordinal)
    {
        "in_progress", "waiting_for_customer", "response_ready", "closed", "refused"
    };

    private readonly SqlRuntimeConfiguration _sql;
    private readonly MockDataSubjectRequestStore _mock;
    private readonly MockPortalNotificationStore _mockNotifications;
    private volatile bool _schemaReady;

    public DataSubjectRequestService(SqlRuntimeConfiguration sql, MockDataSubjectRequestStore mock,
        MockPortalNotificationStore mockNotifications)
    {
        _sql = sql;
        _mock = mock;
        _mockNotifications = mockNotifications;
    }

    public bool IsPersistent => _sql.IsPersistent;

    public async Task<DataSubjectRequestDetail> CreateAsync(
        PortalSessionContext session, DataSubjectRequestCreatePayload? payload,
        CancellationToken cancellationToken)
    {
        var type = payload?.RequestType?.Trim().ToLowerInvariant();
        var details = payload?.Details?.Trim();
        if (type is null || !Types.Contains(type) || details is null
            || details.Length is < 10 or > 5000)
            throw new PortalValidationException();

        var now = DateTime.UtcNow;
        var id = Guid.NewGuid().ToString("D");
        var detail = new DataSubjectRequestDetail(
            id, "DON-" + id[..8].ToUpperInvariant(), type, details,
            "received", now.ToString("O"), now.AddMonths(1).ToString("O"),
            now.ToString("O"), session.CustomerId, session.UserId, []);
        if (!IsPersistent)
        {
            _mock.Requests[id] = detail;
            _mock.CustomerReferences[id] = session.CustomerReference;
            return detail;
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO data_subject_requests
                (id, reference, customer_id, user_id, request_type, details,
                 status, due_at, created_at, updated_at)
            VALUES (@id, @reference, @customer_id, @user_id, @type, @details,
                    'received', @due_at, @created_at, @created_at);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@reference", detail.Reference);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);
        command.Parameters.AddWithValue("@user_id", session.UserId);
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@details", details);
        command.Parameters.AddWithValue("@due_at", now.AddMonths(1));
        command.Parameters.AddWithValue("@created_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return detail;
    }

    public async Task<IReadOnlyList<DataSubjectRequestSummary>> ListClientAsync(
        PortalSessionContext session, CancellationToken cancellationToken)
        => await ListAsync(session.UserId, cancellationToken);

    public async Task<IReadOnlyList<DataSubjectRequestSummary>> ListAdminAsync(
        CancellationToken cancellationToken)
        => await ListAsync(null, cancellationToken);

    private async Task<IReadOnlyList<DataSubjectRequestSummary>> ListAsync(
        string? userId, CancellationToken cancellationToken)
    {
        if (!IsPersistent)
            return _mock.Requests.Values
                .Where(item => userId is null || item.UserId == userId)
                .OrderByDescending(item => item.CreatedAt)
                .Select(ToSummary).ToArray();

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, reference, customer_id, request_type, status,
                   created_at, due_at, updated_at
            FROM data_subject_requests
            WHERE (@user_id IS NULL OR user_id = @user_id)
            ORDER BY created_at DESC LIMIT 200;
            """;
        command.Parameters.AddWithValue("@user_id", (object?)userId ?? DBNull.Value);
        var items = new List<DataSubjectRequestSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(new DataSubjectRequestSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("reference"), reader.GetString("request_type"),
                reader.GetString("status"), Utc(reader.GetDateTime("created_at")),
                Utc(reader.GetDateTime("due_at")), Utc(reader.GetDateTime("updated_at")),
                MariaDbIdentifierReader.ReadRequired(reader, "customer_id")));
        return items;
    }

    public Task<DataSubjectRequestDetail?> GetClientAsync(
        PortalSessionContext session, string id, CancellationToken cancellationToken)
        => GetAsync(id, session.UserId, cancellationToken);

    public Task<DataSubjectRequestDetail?> GetAdminAsync(
        string id, CancellationToken cancellationToken)
        => GetAsync(id, null, cancellationToken);

    private async Task<DataSubjectRequestDetail?> GetAsync(
        string id, string? userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out _)) return null;
        if (!IsPersistent)
        {
            return _mock.Requests.TryGetValue(id, out var item)
                   && (userId is null || item.UserId == userId)
                ? item : null;
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, reference, customer_id, user_id, request_type, details,
                   response_file_name, deadline_extended_at,
                   status, created_at, due_at, updated_at
            FROM data_subject_requests
            WHERE id = @id AND (@user_id IS NULL OR user_id = @user_id)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@user_id", (object?)userId ?? DBNull.Value);
        DataSubjectRequestDetail? detail;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            detail = new DataSubjectRequestDetail(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("reference"), reader.GetString("request_type"),
                reader.GetString("details"), reader.GetString("status"),
                Utc(reader.GetDateTime("created_at")),
                Utc(reader.GetDateTime("due_at")),
                Utc(reader.GetDateTime("updated_at")),
                MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
                MariaDbIdentifierReader.ReadRequired(reader, "user_id"), [],
                reader.IsDBNull(reader.GetOrdinal("response_file_name"))
                    ? null : reader.GetString("response_file_name"),
                reader.IsDBNull(reader.GetOrdinal("deadline_extended_at"))
                    ? null : Utc(reader.GetDateTime("deadline_extended_at")));
        }

        await using var messages = connection.CreateCommand();
        messages.CommandText = """
            SELECT id, author_role, body, created_at
            FROM data_subject_request_messages
            WHERE request_id = @id ORDER BY created_at, id;
            """;
        messages.Parameters.AddWithValue("@id", id);
        var history = new List<DataSubjectRequestMessage>();
        await using var messageReader = await messages.ExecuteReaderAsync(cancellationToken);
        while (await messageReader.ReadAsync(cancellationToken))
            history.Add(new DataSubjectRequestMessage(
                MariaDbIdentifierReader.ReadRequired(messageReader, "id"),
                messageReader.GetString("author_role"),
                messageReader.GetString("body"),
                Utc(messageReader.GetDateTime("created_at"))));
        return detail with { Messages = history };
    }

    public async Task<DataSubjectRequestDetail?> ReplyAsync(
        string id, PortalSessionContext? client,
        DataSubjectRequestMessagePayload? payload,
        CancellationToken cancellationToken)
    {
        var body = payload?.Body?.Trim();
        var isAdmin = client is null;
        var extendDeadline = isAdmin && payload?.ExtendDeadline == true;
        var status = isAdmin ? payload?.Status?.Trim() : "in_progress";
        if (body is null || body.Length is < 3 or > 5000
            || (isAdmin && (status is null || !AdminStatuses.Contains(status)))
            || (!isAdmin && payload?.ExtendDeadline == true))
            throw new PortalValidationException();
        var current = isAdmin
            ? await GetAdminAsync(id, cancellationToken)
            : await GetClientAsync(client!, id, cancellationToken);
        if (current is null) return null;
        if (client is not null && current.Status is "closed" or "refused")
            throw new PortalValidationException();

        var now = DateTime.UtcNow;
        if (extendDeadline && (body.Length < 10 || current.DeadlineExtendedAt is not null
            || status != "in_progress"
            || DateTime.Parse(current.DueAt).ToUniversalTime() < now))
            throw new PortalValidationException();
        var extendedDueAt = DateTime.Parse(current.CreatedAt).ToUniversalTime().AddMonths(3);
        var message = new DataSubjectRequestMessage(
            Guid.NewGuid().ToString("D"), isAdmin ? "team" : "client",
            body, now.ToString("O"));
        if (!IsPersistent)
        {
            var updated = current with
            {
                Status = status!, UpdatedAt = now.ToString("O"),
                DueAt = extendDeadline ? extendedDueAt.ToString("O") : current.DueAt,
                DeadlineExtendedAt = extendDeadline ? now.ToString("O") : current.DeadlineExtendedAt,
                Messages = [.. current.Messages, message]
            };
            _mock.Requests[id] = updated;
            if (isAdmin) AddMockNotification(updated, "Une réponse est disponible dans votre espace client.");
            return updated;
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE data_subject_requests
                SET status = @status, updated_at = @now,
                    due_at = CASE WHEN @extend = 1 THEN @extended_due_at ELSE due_at END,
                    deadline_extended_at = CASE WHEN @extend = 1 THEN @now ELSE deadline_extended_at END
                WHERE id = @id AND customer_id = @customer_id
                  AND (@is_admin = 1 OR status NOT IN ('closed', 'refused'))
                  AND (@extend = 0 OR (deadline_extended_at IS NULL AND due_at >= @now));
                """;
            update.Parameters.AddWithValue("@status", status);
            update.Parameters.AddWithValue("@now", now);
            update.Parameters.AddWithValue("@id", id);
            update.Parameters.AddWithValue("@customer_id", current.CustomerId);
            update.Parameters.AddWithValue("@is_admin", isAdmin ? 1 : 0);
            update.Parameters.AddWithValue("@extend", extendDeadline ? 1 : 0);
            update.Parameters.AddWithValue("@extended_due_at", extendedDueAt);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (extendDeadline) throw new PortalValidationException();
                return null;
            }
        }
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO data_subject_request_messages
                    (id, request_id, author_role, body, created_at)
                VALUES (@id, @request_id, @role, @body, @now);
                """;
            insert.Parameters.AddWithValue("@id", message.Id);
            insert.Parameters.AddWithValue("@request_id", id);
            insert.Parameters.AddWithValue("@role", message.AuthorRole);
            insert.Parameters.AddWithValue("@body", body);
            insert.Parameters.AddWithValue("@now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        if (isAdmin)
        {
            await using var notification = connection.CreateCommand();
            notification.Transaction = transaction;
            notification.CommandText = """
                INSERT INTO portal_notifications
                    (id, customer_id, user_id, request_type, request_id, notification_type,
                     title, message, link_url, created_at)
                VALUES (@id, @customer_id, @user_id, NULL, NULL, 'data_request_update',
                        'Mise à jour de votre demande de données',
                        'Une réponse est disponible dans votre espace client.',
                        @link_url, @now);
                """;
            notification.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            notification.Parameters.AddWithValue("@customer_id", current.CustomerId);
            notification.Parameters.AddWithValue("@user_id", current.UserId);
            notification.Parameters.AddWithValue("@link_url", "/profile/donnees/" + Uri.EscapeDataString(id));
            notification.Parameters.AddWithValue("@now", now);
            await notification.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, client?.UserId, cancellationToken);
    }

    public async Task<DataSubjectRequestDetail?> UploadResponseFileAsync(
        string id, string fileName, string contentType, byte[] data,
        CancellationToken cancellationToken)
    {
        fileName = Path.GetFileName(fileName).Trim();
        if (fileName.Length is < 1 or > 180 || fileName.Any(char.IsControl)
            || data.Length is < 4 or > 10 * 1024 * 1024
            || !AllowedResponseFile(fileName, contentType, data))
            throw new PortalValidationException();
        var current = await GetAdminAsync(id, cancellationToken);
        if (current is null) return null;
        var now = DateTime.UtcNow;
        if (!IsPersistent)
        {
            _mock.Files[id] = (fileName, contentType, data);
            var updated = current with { ResponseFileName = fileName,
                Status = "response_ready", UpdatedAt = now.ToString("O") };
            _mock.Requests[id] = updated;
            AddMockNotification(updated, "Un document est à consulter dans votre espace client.");
            return updated;
        }
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE data_subject_requests
                SET response_file_name = @name, response_content_type = @type,
                    response_file_data = @data, status = 'response_ready', updated_at = @now
                WHERE id = @id;
                """;
            update.Parameters.AddWithValue("@name", fileName);
            update.Parameters.AddWithValue("@type", contentType);
            update.Parameters.AddWithValue("@data", data);
            update.Parameters.AddWithValue("@now", now);
            update.Parameters.AddWithValue("@id", id);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var notification = connection.CreateCommand())
        {
            notification.Transaction = transaction;
            notification.CommandText = """
                INSERT INTO portal_notifications
                    (id, customer_id, user_id, request_type, request_id,
                     notification_type, title, message, link_url, created_at)
                VALUES (@id, @customer_id, @user_id, NULL, NULL,
                        'data_request_update', 'Document disponible pour votre demande de données',
                        'Un document est à consulter dans votre espace client.', @url, @now);
                """;
            notification.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            notification.Parameters.AddWithValue("@customer_id", current.CustomerId);
            notification.Parameters.AddWithValue("@user_id", current.UserId);
            notification.Parameters.AddWithValue("@url", "/profile/donnees/" + Uri.EscapeDataString(id));
            notification.Parameters.AddWithValue("@now", now);
            await notification.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetAdminAsync(id, cancellationToken);
    }

    public async Task<(string FileName, string ContentType, byte[] Data)?> GetResponseFileAsync(
        PortalSessionContext session, string id, CancellationToken cancellationToken)
    {
        var request = await GetClientAsync(session, id, cancellationToken);
        if (request?.ResponseFileName is null) return null;
        if (!IsPersistent)
            return _mock.Files.TryGetValue(id, out var file) ? file : null;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT response_file_name, response_content_type, response_file_data
            FROM data_subject_requests
            WHERE id = @id AND user_id = @user_id
              AND response_file_data IS NOT NULL LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@user_id", session.UserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetString("response_file_name"),
               reader.GetString("response_content_type"),
               (byte[])reader["response_file_data"])
            : null;
    }

    private static bool AllowedResponseFile(string fileName, string contentType, byte[] data)
        => (Path.GetExtension(fileName).ToLowerInvariant(), contentType) switch
        {
            (".pdf", "application/pdf") => data.AsSpan(0, 4).SequenceEqual("%PDF"u8),
            (".zip", "application/zip") => data.AsSpan(0, 4).SequenceEqual(new byte[] { 80, 75, 3, 4 }),
            (".csv", "text/csv") => true,
            (".json", "application/json") => data[0] is (byte)'{' or (byte)'[',
            _ => false
        };

    private void AddMockNotification(DataSubjectRequestDetail request, string message)
    {
        if (!_mock.CustomerReferences.TryGetValue(request.Id, out var customerReference))
            return;
        lock (_mockNotifications.SyncRoot)
        {
            _mockNotifications.Notifications.Add(new MockPortalNotification
            {
                Id = Guid.NewGuid().ToString("D"),
                CustomerReference = customerReference,
                UserId = request.UserId,
                NotificationType = "data_request_update",
                Title = "Mise à jour de votre demande de données",
                Message = message,
                LinkUrl = "/profile/donnees/" + Uri.EscapeDataString(request.Id),
                CreatedAt = DateTime.UtcNow.ToString("O")
            });
        }
    }

    private async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        if (!_schemaReady)
        {
            await using var schema = connection.CreateCommand();
            schema.CommandText = """
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN ('data_subject_requests', 'data_subject_request_messages');
            """;
            if (Convert.ToInt32(await schema.ExecuteScalarAsync(cancellationToken)) != 2)
            {
                await connection.DisposeAsync();
                throw new SiteFeatureSchemaUnavailableException("les demandes de données (migration 099)");
            }
            _schemaReady = true;
        }
        return connection;
    }

    private static DataSubjectRequestSummary ToSummary(DataSubjectRequestDetail detail)
        => new(detail.Id, detail.Reference, detail.RequestType, detail.Status,
            detail.CreatedAt, detail.DueAt, detail.UpdatedAt, detail.CustomerId);

    private static string Utc(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}
