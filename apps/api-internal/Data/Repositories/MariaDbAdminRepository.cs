using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbAdminRepository : IAdminRepository
{
    private const int DefaultLimit = 100;
    private readonly string _connectionString;

    private sealed record CustomerAdminSnapshot(
        string CustomerId,
        ClientProfile Identity,
        string CreatedAt,
        string LastActivityAt,
        int PortalUserCount,
        int ActivePortalUserCount,
        int ActiveSessionCount,
        int ActiveServiceCount,
        int PendingInvoiceCount,
        int OpenSupportRequestCount,
        int ActiveServiceRequestCount,
        int SharedCommercialDocumentCount);

    public MariaDbAdminRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<AdminOverview> GetOverviewAsync(
        string adMode,
        bool adOperationsEnabled,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                (SELECT COUNT(*) FROM customers) AS customer_count,
                (
                    SELECT COUNT(*)
                    FROM portal_users
                    WHERE status = 'active'
                ) AS active_user_count,
                (
                    SELECT COUNT(*)
                    FROM portal_sessions
                    WHERE revoked_at IS NULL
                      AND expires_at > UTC_TIMESTAMP(6)
                ) AS active_session_count,
                (
                    SELECT COUNT(*)
                    FROM support_requests
                    WHERE status <> 'closed'
                ) AS open_support_count,
                (
                    SELECT COUNT(*)
                    FROM service_requests
                    WHERE created_at >= UTC_TIMESTAMP(6) - INTERVAL 30 DAY
                ) AS recent_service_request_count;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Admin overview is unavailable.");
        }

        var customerCount = reader.GetInt32("customer_count");
        var activeUserCount = reader.GetInt32("active_user_count");
        var activeSessionCount = reader.GetInt32("active_session_count");
        var openSupportCount = reader.GetInt32("open_support_count");
        var recentServiceRequestCount =
            reader.GetInt32("recent_service_request_count");
        await reader.DisposeAsync();

        return new AdminOverview(
            customerCount,
            activeUserCount,
            activeSessionCount,
            openSupportCount,
            recentServiceRequestCount,
            await GetAuditLogsAsync(10, cancellationToken),
            adMode,
            adOperationsEnabled);
    }

    public async Task<IReadOnlyList<AdminCustomerSummary>> GetCustomersAsync(
        CancellationToken cancellationToken)
    {
        var customers = new List<AdminCustomerSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                c.external_reference,
                c.display_name,
                c.status,
                (
                    SELECT COUNT(*)
                    FROM customer_services s
                    WHERE s.customer_id = c.id
                ) AS service_count,
                (
                    SELECT COUNT(*)
                    FROM support_requests sr
                    WHERE sr.customer_id = c.id
                      AND sr.status <> 'closed'
                ) AS open_support_count,
                c.created_at,
                GREATEST(
                    c.updated_at,
                    COALESCE((
                        SELECT MAX(sr.updated_at)
                        FROM support_requests sr
                        WHERE sr.customer_id = c.id
                    ), c.updated_at),
                    COALESCE((
                        SELECT MAX(r.created_at)
                        FROM service_requests r
                        WHERE r.customer_id = c.id
                    ), c.updated_at)
                ) AS last_activity_at
            FROM customers c
            ORDER BY c.display_name
            LIMIT 100;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(new AdminCustomerSummary(
                reader.GetString("external_reference"),
                reader.GetString("display_name"),
                reader.GetString("status"),
                reader.GetInt32("service_count"),
                reader.GetInt32("open_support_count"),
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("last_activity_at"))));
        }

        return customers;
    }

    public async Task<AdminCustomerDetail?> GetCustomerAsync(
        string customerReference,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var snapshot = await GetCustomerSnapshotAsync(
            connection,
            customerReference,
            cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        var services = await GetCustomerServicesAsync(
            connection,
            snapshot.CustomerId,
            cancellationToken);
        var invoices = await GetCustomerInvoicesAsync(
            connection,
            snapshot.CustomerId,
            cancellationToken);
        var supportRequests = await GetCustomerSupportRequestsAsync(
            connection,
            snapshot.CustomerId,
            customerReference,
            snapshot.Identity.CompanyName,
            cancellationToken);
        var serviceRequests = await GetCustomerServiceRequestsAsync(
            connection,
            snapshot.CustomerId,
            customerReference,
            snapshot.Identity.CompanyName,
            cancellationToken);
        var commercialDocuments = await GetCustomerCommercialDocumentsAsync(
            connection,
            snapshot.CustomerId,
            customerReference,
            snapshot.Identity.CompanyName,
            cancellationToken);
        var recentActivity = await GetCustomerRecentActivityAsync(
            connection,
            snapshot.CustomerId,
            cancellationToken);
        var recentAuditLogs = await GetCustomerAuditLogsAsync(
            connection,
            snapshot.CustomerId,
            cancellationToken);

        return new AdminCustomerDetail(
            snapshot.Identity,
            snapshot.CreatedAt,
            snapshot.LastActivityAt,
            snapshot.PortalUserCount,
            snapshot.ActivePortalUserCount,
            snapshot.ActiveSessionCount,
            snapshot.ActiveServiceCount,
            snapshot.PendingInvoiceCount,
            snapshot.OpenSupportRequestCount,
            snapshot.ActiveServiceRequestCount,
            snapshot.SharedCommercialDocumentCount,
            services,
            invoices,
            supportRequests,
            serviceRequests,
            commercialDocuments,
            recentActivity,
            recentAuditLogs);
    }

    public async Task<IReadOnlyList<AdminSupportRequestSummary>>
        GetSupportRequestsAsync(CancellationToken cancellationToken)
    {
        var requests = new List<AdminSupportRequestSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                sr.id,
                sr.reference,
                c.external_reference AS customer_reference,
                c.display_name AS customer_name,
                COALESCE(s.name, 'Compte client') AS service_name,
                sr.priority,
                sr.status,
                sr.subject,
                sr.created_at,
                sr.updated_at
            FROM support_requests sr
            INNER JOIN customers c ON c.id = sr.customer_id
            LEFT JOIN customer_services s
                ON s.id = sr.service_id
                AND s.customer_id = sr.customer_id
            ORDER BY sr.created_at DESC
            LIMIT 100;
            """;

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
                ToUtcIso(reader.GetDateTime("updated_at")),
                false,
                false));
        }

        return requests;
    }

    public async Task<IReadOnlyList<AdminServiceRequestSummary>>
        GetServiceRequestsAsync(CancellationToken cancellationToken)
    {
        var requests = new List<AdminServiceRequestSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                r.id,
                r.reference,
                c.external_reference AS customer_reference,
                c.display_name AS customer_name,
                catalog.name AS catalog_item_name,
                r.context,
                r.status,
                r.created_at
            FROM service_requests r
            INNER JOIN customers c ON c.id = r.customer_id
            INNER JOIN service_catalog catalog
                ON catalog.id = r.catalog_item_id
            ORDER BY r.created_at DESC
            LIMIT 100;
            """;

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
                Truncate(ExtractDescription(context), 240) ?? string.Empty,
                reader.GetString("status"),
                true,
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("created_at")),
                false,
                false));
        }

        return requests;
    }

    public async Task<IReadOnlyList<AdminSessionSummary>> GetSessionsAsync(
        CancellationToken cancellationToken)
    {
        var sessions = new List<AdminSessionSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                u.display_name,
                u.email,
                u.role,
                c.external_reference AS customer_reference,
                s.created_at,
                s.expires_at,
                s.revoked_at,
                s.last_seen_at,
                s.ip_address,
                s.user_agent
            FROM portal_sessions s
            INNER JOIN portal_users u ON u.id = s.user_id
            LEFT JOIN customers c ON c.id = u.customer_id
            ORDER BY s.created_at DESC
            LIMIT 100;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        var now = DateTime.UtcNow;
        while (await reader.ReadAsync(cancellationToken))
        {
            var expiresAt = AsUtc(reader.GetDateTime("expires_at"));
            var revokedAt = ReadNullableUtc(reader, "revoked_at");
            var role = reader.GetString("role");
            sessions.Add(new AdminSessionSummary(
                reader.GetString("display_name"),
                reader.GetString("email"),
                role,
                role == PortalRoles.ClientUser
                    ? reader.GetString("customer_reference")
                    : null,
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(expiresAt),
                ToUtcIso(ReadNullableUtc(reader, "last_seen_at")),
                MaskAddress(ReadNullableString(reader, "ip_address")),
                Truncate(ReadNullableString(reader, "user_agent"), 120),
                revokedAt is not null
                    ? "revoked"
                    : expiresAt <= now
                        ? "expired"
                        : "active"));
        }

        return sessions;
    }

    public async Task<IReadOnlyList<AdminAuditLogEntry>> GetAuditLogsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var audits = new List<AdminAuditLogEntry>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                a.occurred_at,
                COALESCE(u.display_name, a.actor_service) AS actor_name,
                a.action,
                a.outcome,
                a.reason_code,
                c.external_reference AS customer_reference,
                a.correlation_id,
                a.source_address
            FROM audit_logs a
            LEFT JOIN portal_users u ON u.id = a.actor_user_id
            LEFT JOIN customers c ON c.id = a.customer_id
            ORDER BY a.occurred_at DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue(
            "@limit",
            Math.Clamp(limit, 1, DefaultLimit));

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            audits.Add(new AdminAuditLogEntry(
                ToUtcIso(reader.GetDateTime("occurred_at")),
                reader.GetString("actor_name"),
                reader.GetString("action"),
                reader.GetString("outcome"),
                ReadNullableString(reader, "reason_code"),
                ReadNullableString(reader, "customer_reference"),
                reader.GetString("correlation_id"),
                MaskAddress(ReadNullableString(reader, "source_address"))));
        }

        return audits;
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<CustomerAdminSnapshot?> GetCustomerSnapshotAsync(
        MySqlConnection connection,
        string customerReference,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                c.id,
                c.display_name,
                c.external_reference,
                COALESCE((
                    SELECT u.display_name
                    FROM portal_users u
                    WHERE u.customer_id = c.id
                      AND u.role = 'client_user'
                    ORDER BY
                        COALESCE(u.last_login_at, u.created_at) DESC,
                        u.id DESC
                    LIMIT 1
                ), c.display_name) AS contact_name,
                COALESCE((
                    SELECT u.email
                    FROM portal_users u
                    WHERE u.customer_id = c.id
                      AND u.role = 'client_user'
                    ORDER BY
                        COALESCE(u.last_login_at, u.created_at) DESC,
                        u.id DESC
                    LIMIT 1
                ), c.billing_email, '') AS contact_email,
                COALESCE(c.phone, '') AS phone,
                COALESCE(c.address, '') AS address,
                COALESCE(c.city, '') AS city,
                COALESCE(c.country, '') AS country,
                c.status,
                c.created_at,
                GREATEST(
                    c.updated_at,
                    COALESCE((
                        SELECT MAX(service.updated_at)
                        FROM customer_services service
                        WHERE service.customer_id = c.id
                    ), c.updated_at),
                    COALESCE((
                        SELECT MAX(invoice.updated_at)
                        FROM invoices invoice
                        WHERE invoice.customer_id = c.id
                    ), c.updated_at),
                    COALESCE((
                        SELECT MAX(support.updated_at)
                        FROM support_requests support
                        WHERE support.customer_id = c.id
                    ), c.updated_at),
                    COALESCE((
                        SELECT MAX(service_request.updated_at)
                        FROM service_requests service_request
                        WHERE service_request.customer_id = c.id
                    ), c.updated_at),
                    COALESCE((
                        SELECT MAX(document.updated_at)
                        FROM commercial_documents document
                        WHERE document.customer_id = c.id
                    ), c.updated_at),
                    COALESCE((
                        SELECT MAX(audit.occurred_at)
                        FROM audit_logs audit
                        WHERE audit.customer_id = c.id
                    ), c.updated_at)
                ) AS last_activity_at,
                (
                    SELECT COUNT(*)
                    FROM portal_users portal_user
                    WHERE portal_user.customer_id = c.id
                ) AS portal_user_count,
                (
                    SELECT COUNT(*)
                    FROM portal_users portal_user
                    WHERE portal_user.customer_id = c.id
                      AND portal_user.status = 'active'
                ) AS active_portal_user_count,
                (
                    SELECT COUNT(*)
                    FROM portal_sessions session
                    INNER JOIN portal_users portal_user
                        ON portal_user.id = session.user_id
                    WHERE portal_user.customer_id = c.id
                      AND session.revoked_at IS NULL
                      AND session.expires_at > UTC_TIMESTAMP(6)
                ) AS active_session_count,
                (
                    SELECT COUNT(*)
                    FROM customer_services service
                    WHERE service.customer_id = c.id
                      AND service.status = 'active'
                ) AS active_service_count,
                (
                    SELECT COUNT(*)
                    FROM invoices invoice
                    WHERE invoice.customer_id = c.id
                      AND invoice.status = 'pending'
                ) AS pending_invoice_count,
                (
                    SELECT COUNT(*)
                    FROM support_requests support
                    WHERE support.customer_id = c.id
                      AND support.status <> 'closed'
                ) AS open_support_request_count,
                (
                    SELECT COUNT(*)
                    FROM service_requests service_request
                    WHERE service_request.customer_id = c.id
                      AND service_request.status IN (
                          'received',
                          'under_review',
                          'accepted'
                      )
                ) AS active_service_request_count,
                (
                    SELECT COUNT(*)
                    FROM commercial_documents document
                    WHERE document.customer_id = c.id
                      AND document.status = 'shared_with_customer'
                ) AS shared_commercial_document_count
            FROM customers c
            WHERE c.external_reference = @customer_reference
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_reference", customerReference);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CustomerAdminSnapshot(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            new ClientProfile(
                reader.GetString("display_name"),
                reader.GetString("external_reference"),
                reader.GetString("contact_name"),
                reader.GetString("contact_email"),
                reader.GetString("phone"),
                reader.GetString("address"),
                reader.GetString("city"),
                reader.GetString("country"),
                reader.GetString("status")),
            ToUtcIso(reader.GetDateTime("created_at")),
            ToUtcIso(reader.GetDateTime("last_activity_at")),
            reader.GetInt32("portal_user_count"),
            reader.GetInt32("active_portal_user_count"),
            reader.GetInt32("active_session_count"),
            reader.GetInt32("active_service_count"),
            reader.GetInt32("pending_invoice_count"),
            reader.GetInt32("open_support_request_count"),
            reader.GetInt32("active_service_request_count"),
            reader.GetInt32("shared_commercial_document_count"));
    }

    private static async Task<IReadOnlyList<ServiceSummary>> GetCustomerServicesAsync(
        MySqlConnection connection,
        string customerId,
        CancellationToken cancellationToken)
    {
        var services = new List<ServiceSummary>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                external_reference,
                name,
                service_type,
                status,
                description,
                started_at,
                scope,
                commercial_terms,
                next_step
            FROM customer_services
            WHERE customer_id = @customer_id
            ORDER BY updated_at DESC, id DESC
            LIMIT 10;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            services.Add(new ServiceSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("external_reference"),
                reader.GetString("name"),
                reader.GetString("service_type"),
                reader.GetString("status"),
                reader.GetString("description"),
                reader.IsDBNull(reader.GetOrdinal("started_at"))
                    ? null
                    : reader.GetDateTime("started_at").ToString("yyyy-MM-dd"),
                reader.GetString("scope"),
                reader.GetString("commercial_terms"),
                ReadNullableString(reader, "next_step")));
        }

        return services;
    }

    private static async Task<IReadOnlyList<InvoiceSummary>> GetCustomerInvoicesAsync(
        MySqlConnection connection,
        string customerId,
        CancellationToken cancellationToken)
    {
        var invoices = new List<InvoiceSummary>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                invoice_number,
                status,
                issued_at,
                due_at,
                period_label,
                total_amount,
                currency
            FROM invoices
            WHERE customer_id = @customer_id
            ORDER BY issued_at DESC, id DESC
            LIMIT 10;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            invoices.Add(new InvoiceSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("invoice_number"),
                reader.GetString("status"),
                reader.GetDateTime("issued_at").ToString("yyyy-MM-dd"),
                reader.IsDBNull(reader.GetOrdinal("due_at"))
                    ? reader.GetDateTime("issued_at").ToString("yyyy-MM-dd")
                    : reader.GetDateTime("due_at").ToString("yyyy-MM-dd"),
                reader.GetString("period_label"),
                reader.GetDecimal("total_amount"),
                reader.GetString("currency")));
        }

        return invoices;
    }

    private static async Task<IReadOnlyList<AdminSupportRequestSummary>>
        GetCustomerSupportRequestsAsync(
            MySqlConnection connection,
            string customerId,
            string customerReference,
            string customerName,
            CancellationToken cancellationToken)
    {
        var requests = new List<AdminSupportRequestSummary>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                sr.id,
                sr.reference,
                COALESCE(service.name, 'Compte client') AS service_name,
                sr.priority,
                sr.status,
                sr.subject,
                sr.created_at,
                sr.updated_at,
                IF(latest_author.role = 'client_user', 1, 0)
                    AS has_recent_client_reply
            FROM support_requests sr
            LEFT JOIN customer_services service
                ON service.id = sr.service_id
                AND service.customer_id = sr.customer_id
            LEFT JOIN portal_users latest_author
                ON latest_author.id = (
                    SELECT message.author_user_id
                    FROM request_public_messages message
                    WHERE message.request_type = 'support'
                      AND message.request_id = sr.id
                    ORDER BY message.created_at DESC, message.id DESC
                    LIMIT 1
                )
            WHERE sr.customer_id = @customer_id
            ORDER BY sr.updated_at DESC, sr.id DESC
            LIMIT 10;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var hasRecentClientReply =
                reader.GetBoolean("has_recent_client_reply");
            var status = reader.GetString("status");
            requests.Add(new AdminSupportRequestSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("reference"),
                customerReference,
                customerName,
                reader.GetString("service_name"),
                reader.GetString("priority"),
                status,
                reader.GetString("subject"),
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("updated_at")),
                hasRecentClientReply,
                hasRecentClientReply
                    || status is "open" or "in_progress"));
        }

        return requests;
    }

    private static async Task<IReadOnlyList<AdminServiceRequestSummary>>
        GetCustomerServiceRequestsAsync(
            MySqlConnection connection,
            string customerId,
            string customerReference,
            string customerName,
            CancellationToken cancellationToken)
    {
        var requests = new List<AdminServiceRequestSummary>();
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
                request.updated_at,
                IF(latest_author.role = 'client_user', 1, 0)
                    AS has_recent_client_reply
            FROM service_requests request
            INNER JOIN service_catalog catalog
                ON catalog.id = request.catalog_item_id
            LEFT JOIN portal_users latest_author
                ON latest_author.id = (
                    SELECT message.author_user_id
                    FROM request_public_messages message
                    WHERE message.request_type = 'service'
                      AND message.request_id = request.id
                    ORDER BY message.created_at DESC, message.id DESC
                    LIMIT 1
                )
            WHERE request.customer_id = @customer_id
            ORDER BY request.updated_at DESC, request.id DESC
            LIMIT 10;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var context = reader.GetString("context");
            var hasRecentClientReply =
                reader.GetBoolean("has_recent_client_reply");
            var status = reader.GetString("status");
            requests.Add(new AdminServiceRequestSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("reference"),
                customerReference,
                customerName,
                reader.GetString("catalog_item_name"),
                ExtractSubject(context),
                Truncate(ExtractDescription(context), 240) ?? string.Empty,
                status,
                true,
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("updated_at")),
                hasRecentClientReply,
                hasRecentClientReply
                    || status is "received" or "under_review"));
        }

        return requests;
    }

    private static async Task<IReadOnlyList<AdminCommercialDocumentSummary>>
        GetCustomerCommercialDocumentsAsync(
            MySqlConnection connection,
            string customerId,
            string customerReference,
            string customerName,
            CancellationToken cancellationToken)
    {
        var documents = new List<AdminCommercialDocumentSummary>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                document.id,
                document.document_type,
                document.status,
                document.title,
                document.internal_reference,
                document.currency,
                document.subtotal_amount_cents,
                document.tax_amount_cents,
                document.total_amount_cents,
                document.disclaimer,
                document.created_at,
                document.updated_at,
                document.shared_at,
                document.payment_method,
                document.service_request_id,
                request.reference AS service_request_reference
            FROM commercial_documents document
            LEFT JOIN service_requests request
                ON request.id = document.service_request_id
            WHERE document.customer_id = @customer_id
            ORDER BY document.updated_at DESC, document.id DESC
            LIMIT 10;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(new AdminCommercialDocumentSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("document_type"),
                reader.GetString("status"),
                reader.GetString("title"),
                reader.GetString("internal_reference"),
                reader.GetString("currency"),
                reader.GetInt32("subtotal_amount_cents"),
                reader.GetInt32("tax_amount_cents"),
                reader.GetInt32("total_amount_cents"),
                reader.GetString("disclaimer"),
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("updated_at")),
                ToUtcIso(ReadNullableUtc(reader, "shared_at")),
                ReadNullableIdentifier(reader, "service_request_id"),
                ReadNullableString(reader, "service_request_reference"),
                ReadNullableString(reader, "payment_method"),
                customerReference,
                customerName));
        }

        return documents;
    }

    private static async Task<IReadOnlyList<AdminActivityItem>>
        GetCustomerRecentActivityAsync(
            MySqlConnection connection,
            string customerId,
            CancellationToken cancellationToken)
    {
        var activities = new List<AdminActivityItem>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT *
            FROM (
                SELECT
                    message.id AS message_id,
                    'support' AS request_type,
                    support.id AS request_id,
                    support.reference,
                    customer.external_reference AS customer_reference,
                    customer.display_name AS customer_name,
                    support.subject AS subject_source,
                    support.status,
                    author.role AS author_role,
                    author.display_name AS author_display_name,
                    message.created_at AS occurred_at
                FROM request_public_messages message
                INNER JOIN portal_users author
                    ON author.id = message.author_user_id
                INNER JOIN support_requests support
                    ON message.request_type = 'support'
                    AND support.id = message.request_id
                INNER JOIN customers customer
                    ON customer.id = support.customer_id
                WHERE support.customer_id = @customer_id

                UNION ALL

                SELECT
                    message.id AS message_id,
                    'service' AS request_type,
                    service.id AS request_id,
                    service.reference,
                    customer.external_reference AS customer_reference,
                    customer.display_name AS customer_name,
                    service.context AS subject_source,
                    service.status,
                    author.role AS author_role,
                    author.display_name AS author_display_name,
                    message.created_at AS occurred_at
                FROM request_public_messages message
                INNER JOIN portal_users author
                    ON author.id = message.author_user_id
                INNER JOIN service_requests service
                    ON message.request_type = 'service'
                    AND service.id = message.request_id
                INNER JOIN customers customer
                    ON customer.id = service.customer_id
                WHERE service.customer_id = @customer_id
            ) activity
            ORDER BY occurred_at DESC, message_id DESC
            LIMIT 10;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var requestType = reader.GetString("request_type");
            var authorType =
                reader.GetString("author_role") == PortalRoles.InternalAdmin
                    ? "admin"
                    : "client";
            activities.Add(new AdminActivityItem(
                requestType,
                MariaDbIdentifierReader.ReadRequired(reader, "request_id"),
                reader.GetString("reference"),
                reader.GetString("customer_reference"),
                reader.GetString("customer_name"),
                requestType == "service"
                    ? ExtractSubject(reader.GetString("subject_source"))
                    : reader.GetString("subject_source"),
                reader.GetString("status"),
                authorType,
                authorType == "admin"
                    ? "Équipe Kermaria"
                    : reader.GetString("author_display_name"),
                ToUtcIso(reader.GetDateTime("occurred_at"))));
        }

        return activities;
    }

    private async Task<IReadOnlyList<AdminAuditLogEntry>> GetCustomerAuditLogsAsync(
        MySqlConnection connection,
        string customerId,
        CancellationToken cancellationToken)
    {
        var audits = new List<AdminAuditLogEntry>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                a.occurred_at,
                COALESCE(u.display_name, a.actor_service) AS actor_name,
                a.action,
                a.outcome,
                a.reason_code,
                c.external_reference AS customer_reference,
                a.correlation_id,
                a.source_address
            FROM audit_logs a
            LEFT JOIN portal_users u ON u.id = a.actor_user_id
            LEFT JOIN customers c ON c.id = a.customer_id
            WHERE a.customer_id = @customer_id
            ORDER BY a.occurred_at DESC
            LIMIT 10;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            audits.Add(new AdminAuditLogEntry(
                ToUtcIso(reader.GetDateTime("occurred_at")),
                reader.GetString("actor_name"),
                reader.GetString("action"),
                reader.GetString("outcome"),
                ReadNullableString(reader, "reason_code"),
                ReadNullableString(reader, "customer_reference"),
                reader.GetString("correlation_id"),
                MaskAddress(ReadNullableString(reader, "source_address"))));
        }

        return audits;
    }

    private static string ExtractSubject(string context)
    {
        var firstLine = context
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .FirstOrDefault()
            ?? "Demande de service";
        return firstLine.StartsWith("Sujet :", StringComparison.OrdinalIgnoreCase)
            ? firstLine["Sujet :".Length..].Trim()
            : Truncate(firstLine.Trim(), 160) ?? "Demande de service";
    }

    private static string ExtractDescription(string context)
    {
        var separatorIndex = context.IndexOf(
            $"{Environment.NewLine}{Environment.NewLine}",
            StringComparison.Ordinal);
        if (separatorIndex >= 0)
        {
            return context[
                (separatorIndex + (Environment.NewLine.Length * 2))..].Trim();
        }

        var lines = context.Split(["\r\n", "\n"], StringSplitOptions.None);
        return string.Join(" ", lines.Skip(1)).Trim();
    }

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);

    private static string? ReadNullableIdentifier(
        MySqlDataReader reader,
        string columnName)
        => MariaDbIdentifierReader.ReadNullable(reader, columnName);

    private static DateTime? ReadNullableUtc(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : AsUtc(reader.GetDateTime(columnName));

    private static DateTime AsUtc(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string ToUtcIso(DateTime value)
        => AsUtc(value).ToString("O");

    private static string? ToUtcIso(DateTime? value)
        => value is null ? null : ToUtcIso(value.Value);

    private static string? Truncate(string? value, int maximumLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, maximumLength)];

    private static string? MaskAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (System.Net.IPAddress.TryParse(value, out var address))
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4)
            {
                bytes[3] = 0;
                return new System.Net.IPAddress(bytes).ToString();
            }

            for (var index = 8; index < bytes.Length; index++)
            {
                bytes[index] = 0;
            }

            return new System.Net.IPAddress(bytes).ToString();
        }

        return "masquée";
    }
}
