using System.Data;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbPortalRepository : IPortalRepository
{
    private readonly string _connectionString;

    public MariaDbPortalRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<PortalSummary> GetSummaryAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                c.external_reference,
                COALESCE(u.display_name, c.display_name) AS contact_name,
                (
                    SELECT COUNT(*)
                    FROM customer_services s
                    WHERE s.customer_id = c.id AND s.status = 'active'
                ) AS active_service_count,
                (
                    SELECT COUNT(*)
                    FROM invoices i
                    WHERE i.customer_id = c.id AND i.status = 'pending'
                ) AS pending_invoice_count,
                (
                    SELECT COALESCE(SUM(i.total_amount), 0)
                    FROM invoices i
                    WHERE i.customer_id = c.id AND i.status = 'pending'
                ) AS pending_invoice_total,
                (
                    SELECT COUNT(*)
                    FROM support_requests sr
                    WHERE sr.customer_id = c.id AND sr.status <> 'closed'
                ) AS open_support_count,
                GREATEST(
                    c.updated_at,
                    COALESCE((SELECT MAX(s.updated_at) FROM customer_services s WHERE s.customer_id = c.id), c.updated_at),
                    COALESCE((SELECT MAX(i.updated_at) FROM invoices i WHERE i.customer_id = c.id), c.updated_at),
                    COALESCE((SELECT MAX(sr.updated_at) FROM support_requests sr WHERE sr.customer_id = c.id), c.updated_at)
                ) AS last_updated_at
            FROM customers c
            LEFT JOIN portal_users u
                ON u.id = @user_id AND u.customer_id = c.id
            WHERE c.id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@user_id", session.UserId);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new PortalDataNotFoundException();
        }

        return new PortalSummary(
            reader.GetString("external_reference"),
            reader.GetString("contact_name"),
            reader.GetInt32("active_service_count"),
            reader.GetInt32("pending_invoice_count"),
            reader.GetDecimal("pending_invoice_total"),
            reader.GetInt32("open_support_count"),
            ToUtcIso(reader.GetDateTime("last_updated_at")));
    }

    public async Task<ClientProfile> GetProfileAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                c.display_name,
                c.external_reference,
                COALESCE(u.display_name, c.display_name) AS contact_name,
                COALESCE(u.email, c.billing_email, '') AS email,
                COALESCE(c.phone, '') AS phone,
                COALESCE(c.address, '') AS address,
                COALESCE(c.city, '') AS city,
                COALESCE(c.country, '') AS country,
                c.status
            FROM customers c
            LEFT JOIN portal_users u
                ON u.id = @user_id AND u.customer_id = c.id
            WHERE c.id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@user_id", session.UserId);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new PortalDataNotFoundException();
        }

        return new ClientProfile(
            reader.GetString("display_name"),
            reader.GetString("external_reference"),
            reader.GetString("contact_name"),
            reader.GetString("email"),
            reader.GetString("phone"),
            reader.GetString("address"),
            reader.GetString("city"),
            reader.GetString("country"),
            reader.GetString("status"));
    }

    public async Task<IReadOnlyList<ServiceSummary>> GetServicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        var services = new List<ServiceSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
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
            ORDER BY created_at;
            """;
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            services.Add(new ServiceSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("external_reference"),
                reader.GetString("name"),
                reader.GetString("service_type"),
                reader.GetString("status"),
                reader.GetString("description"),
                reader.IsDBNull("started_at")
                    ? null
                    : reader.GetDateTime("started_at").ToString("yyyy-MM-dd"),
                reader.GetString("scope"),
                reader.GetString("commercial_terms"),
                reader.IsDBNull("next_step")
                    ? null
                    : reader.GetString("next_step")));
        }

        return services;
    }

    public async Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        var invoices = new List<InvoiceSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
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
            ORDER BY issued_at DESC;
            """;
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            invoices.Add(new InvoiceSummary(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("invoice_number"),
                reader.GetString("status"),
                reader.GetDateTime("issued_at").ToString("yyyy-MM-dd"),
                reader.IsDBNull("due_at")
                    ? reader.GetDateTime("issued_at").ToString("yyyy-MM-dd")
                    : reader.GetDateTime("due_at").ToString("yyyy-MM-dd"),
                reader.GetString("period_label"),
                reader.GetDecimal("total_amount"),
                reader.GetString("currency")));
        }

        return invoices;
    }

    public async Task<IReadOnlyList<ServiceCatalogItem>> GetServiceCatalogAsync(
        CancellationToken cancellationToken)
    {
        var items = new List<ServiceCatalogItem>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                TRIM(CAST(id AS CHAR(64))) AS id,
                name,
                category,
                description,
                scope,
                commercial_terms
            FROM service_catalog
            WHERE is_active = TRUE
            ORDER BY sort_order, name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ServiceCatalogItem(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                reader.GetString("name"),
                reader.GetString("category"),
                reader.GetString("description"),
                reader.GetString("scope"),
                reader.GetString("commercial_terms")));
        }

        return items;
    }

    public async Task<IReadOnlyList<SupportRequestSummary>> GetSupportRequestsAsync(
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
                COALESCE(s.name, 'Compte client') AS service_name,
                sr.created_at,
                sr.updated_at
            FROM support_requests sr
            LEFT JOIN customer_services s
                ON s.id = sr.service_id
                AND s.customer_id = sr.customer_id
            WHERE sr.customer_id = @customer_id
            ORDER BY sr.created_at DESC;
            """;
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

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

    public async Task<SubmissionResponse> CreateSupportRequestAsync(
        PortalSessionContext session,
        SupportRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var customerId = session.CustomerId;
        var serviceId = payload.ServiceId == "account"
            ? null
            : await ResolveServiceIdAsync(
                connection,
                transaction,
                customerId,
                payload.ServiceId!,
                cancellationToken);
        var id = Guid.NewGuid().ToString("D");
        var reference = CreateReference("SUP");
        var now = DateTime.UtcNow;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO support_requests (
                    id,
                    customer_id,
                    created_by_user_id,
                    service_id,
                    reference,
                    subject,
                    description,
                    priority,
                    category,
                    status,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @customer_id,
                    @created_by_user_id,
                    @service_id,
                    @reference,
                    @subject,
                    @description,
                    @priority,
                    'support',
                    'open',
                    @created_at,
                    @updated_at
                );
                """;
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@customer_id", customerId);
            command.Parameters.AddWithValue(
                "@created_by_user_id",
                session.UserId);
            command.Parameters.AddWithValue(
                "@service_id",
                serviceId is null ? DBNull.Value : serviceId);
            command.Parameters.AddWithValue("@reference", reference);
            command.Parameters.AddWithValue("@subject", payload.Subject);
            command.Parameters.AddWithValue("@description", payload.Description);
            command.Parameters.AddWithValue("@priority", payload.Priority);
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            new AuditEvent(
                correlationId,
                "support_request.create",
                "success",
                TargetType: "support_request",
                TargetReference: reference,
                CustomerId: customerId,
                ActorUserId: session.UserId,
                SourceAddress: sourceAddress),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SubmissionResponse(
            reference,
            "received",
            true,
            "Demande enregistrée. Aucun traitement immédiat n'est garanti.",
            correlationId);
    }

    public async Task<SubmissionResponse> CreateServiceRequestAsync(
        PortalSessionContext session,
        ServiceRequestPayload payload,
        string correlationId,
        string? sourceAddress,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var customerId = session.CustomerId;
        var catalogItemId = await ResolveCatalogItemIdAsync(
            connection,
            transaction,
            payload.CatalogItemId!,
            cancellationToken);
        var id = Guid.NewGuid().ToString("D");
        var reference = CreateReference("SRV");
        var now = DateTime.UtcNow;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO service_requests (
                    id,
                    customer_id,
                    created_by_user_id,
                    catalog_item_id,
                    reference,
                    timeline,
                    context,
                    status,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @customer_id,
                    @created_by_user_id,
                    @catalog_item_id,
                    @reference,
                    @timeline,
                    @context,
                    'received',
                    @created_at,
                    @updated_at
                );
                """;
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@customer_id", customerId);
            command.Parameters.AddWithValue(
                "@created_by_user_id",
                session.UserId);
            command.Parameters.AddWithValue("@catalog_item_id", catalogItemId);
            command.Parameters.AddWithValue("@reference", reference);
            command.Parameters.AddWithValue("@timeline", "exploration");
            command.Parameters.AddWithValue(
                "@context",
                BuildServiceRequestContext(payload));
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            new AuditEvent(
                correlationId,
                "service_request.create",
                "success",
                TargetType: "service_request",
                TargetReference: reference,
                CustomerId: customerId,
                ActorUserId: session.UserId,
                SourceAddress: sourceAddress),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SubmissionResponse(
            reference,
            "received",
            true,
            "Demande enregistrée pour étude. Aucun devis ni paiement n'a été créé.",
            correlationId);
    }

    public async Task AppendAuditAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await InsertAuditAsync(
            connection,
            transaction: null,
            auditEvent,
            cancellationToken);
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<string> ResolveServiceIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string customerId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id
            FROM customer_services
            WHERE id = @id AND customer_id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", serviceId);
        command.Parameters.AddWithValue("@customer_id", customerId);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is null or DBNull
            ? throw new PortalAccessDeniedException()
            : MariaDbIdentifierReader.ConvertRequiredValue(
                result,
                "customer_services.id");
    }

    private static async Task<string> ResolveCatalogItemIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string catalogItemId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT TRIM(CAST(id AS CHAR(64))) AS id
            FROM service_catalog
            WHERE id = @id AND is_active = TRUE
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", catalogItemId);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is null or DBNull
            ? throw new PortalValidationException()
            : MariaDbIdentifierReader.ConvertRequiredValue(
                result,
                "service_catalog.id");
    }

    private static async Task InsertAuditAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO audit_logs (
                id,
                occurred_at,
                correlation_id,
                actor_user_id,
                actor_service,
                customer_id,
                action,
                target_type,
                target_reference,
                outcome,
                reason_code,
                source_address
            ) VALUES (
                @id,
                @occurred_at,
                @correlation_id,
                @actor_user_id,
                'API-INTERNAL',
                @customer_id,
                @action,
                @target_type,
                @target_reference,
                @outcome,
                @reason_code,
                @source_address
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@occurred_at", DateTime.UtcNow);
        command.Parameters.AddWithValue(
            "@correlation_id",
            auditEvent.CorrelationId);
        command.Parameters.AddWithValue(
            "@actor_user_id",
            DbValue(auditEvent.ActorUserId));
        command.Parameters.AddWithValue(
            "@customer_id",
            DbValue(auditEvent.CustomerId));
        command.Parameters.AddWithValue("@action", auditEvent.Action);
        command.Parameters.AddWithValue(
            "@target_type",
            DbValue(auditEvent.TargetType));
        command.Parameters.AddWithValue(
            "@target_reference",
            DbValue(auditEvent.TargetReference));
        command.Parameters.AddWithValue("@outcome", auditEvent.Outcome);
        command.Parameters.AddWithValue(
            "@reason_code",
            DbValue(auditEvent.ReasonCode));
        command.Parameters.AddWithValue(
            "@source_address",
            DbValue(auditEvent.SourceAddress));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;

    private static string BuildServiceRequestContext(
        ServiceRequestPayload payload)
        => $"Sujet : {payload.Subject!.Trim()}{Environment.NewLine}{Environment.NewLine}{payload.Description!.Trim()}";

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

    private static string CreateReference(string prefix)
        => $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..25]
            .ToUpperInvariant();
}

public sealed class PortalDataNotFoundException : Exception;
