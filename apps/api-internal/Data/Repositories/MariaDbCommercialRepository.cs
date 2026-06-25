using System.Data;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;
using System.Globalization;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbCommercialRepository : ICommercialRepository
{
    private readonly string _connectionString;

    public MariaDbCommercialRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<CommercialOfferSummary>> GetClientCatalogAsync(
        CancellationToken cancellationToken)
    {
        var offers = new List<CommercialOfferSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                name,
                description,
                category,
                unit_label,
                price_kind,
                price_amount_cents,
                currency,
                tax_rate_basis_points,
                external_reference,
                status,
                display_order,
                billing_cadence,
                paypal_plan_id,
                created_at,
                updated_at
            FROM commercial_offers
            WHERE status = 'active'
            ORDER BY display_order, name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            offers.Add(ReadOffer(reader));
        }

        return offers;
    }

    public async Task<IReadOnlyList<CommercialOfferSummary>> GetAdminCatalogAsync(
        CancellationToken cancellationToken)
    {
        var offers = new List<CommercialOfferSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                name,
                description,
                category,
                unit_label,
                price_kind,
                price_amount_cents,
                currency,
                tax_rate_basis_points,
                external_reference,
                status,
                display_order,
                billing_cadence,
                paypal_plan_id,
                created_at,
                updated_at
            FROM commercial_offers
            ORDER BY display_order, name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            offers.Add(ReadOffer(reader));
        }

        return offers;
    }

    public async Task<CommercialOfferMutationResponse> CreateOfferAsync(
        ValidatedCommercialOffer offer,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var id = Guid.NewGuid().ToString("D");
        var now = DateTime.UtcNow;
        command.CommandText =
            """
            INSERT INTO commercial_offers (
                id,
                name,
                description,
                category,
                unit_label,
                price_kind,
                price_amount_cents,
                currency,
                status,
                display_order,
                billing_cadence,
                paypal_plan_id,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @name,
                @description,
                @category,
                @unit_label,
                'ht',
                @price_amount_cents,
                'EUR',
                @status,
                @display_order,
                @billing_cadence,
                @paypal_plan_id,
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", offer.Name);
        command.Parameters.AddWithValue("@description", offer.Description);
        command.Parameters.AddWithValue("@category", offer.Category);
        command.Parameters.AddWithValue("@unit_label", offer.UnitLabel);
        command.Parameters.AddWithValue("@price_amount_cents", offer.PriceAmountCents);
        command.Parameters.AddWithValue("@status", offer.Status);
        command.Parameters.AddWithValue("@display_order", offer.DisplayOrder);
        command.Parameters.AddWithValue("@billing_cadence", offer.BillingCadence);
        command.Parameters.AddWithValue("@paypal_plan_id", DbValue(offer.PayPalPlanId));
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new CommercialOfferMutationResponse(
            id,
            offer.Status,
            true,
            correlationId);
    }

    public async Task<CommercialOfferMutationResponse> UpdateOfferAsync(
        string offerId,
        ValidatedCommercialOffer offer,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var current = await ReadOfferForUpdateAsync(
            connection,
            transaction,
            offerId,
            cancellationToken);
        var changed =
            current.Name != offer.Name
            || current.Description != offer.Description
            || current.Category != offer.Category
            || current.UnitLabel != offer.UnitLabel
            || current.PriceAmountCents != offer.PriceAmountCents
            || current.Status != offer.Status
            || current.DisplayOrder != offer.DisplayOrder
            || current.BillingCadence != offer.BillingCadence
            || current.PayPalPlanId != offer.PayPalPlanId;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE commercial_offers
                SET name = @name,
                    description = @description,
                    category = @category,
                    unit_label = @unit_label,
                    price_amount_cents = @price_amount_cents,
                    status = @status,
                    display_order = @display_order,
                    billing_cadence = @billing_cadence,
                    paypal_plan_id = @paypal_plan_id,
                    updated_at = @updated_at
                WHERE id = @id;
                """;
            command.Parameters.AddWithValue("@id", offerId);
            command.Parameters.AddWithValue("@name", offer.Name);
            command.Parameters.AddWithValue("@description", offer.Description);
            command.Parameters.AddWithValue("@category", offer.Category);
            command.Parameters.AddWithValue("@unit_label", offer.UnitLabel);
            command.Parameters.AddWithValue("@price_amount_cents", offer.PriceAmountCents);
            command.Parameters.AddWithValue("@status", offer.Status);
            command.Parameters.AddWithValue("@display_order", offer.DisplayOrder);
            command.Parameters.AddWithValue("@billing_cadence", offer.BillingCadence);
            command.Parameters.AddWithValue("@paypal_plan_id", DbValue(offer.PayPalPlanId));
            command.Parameters.AddWithValue("@updated_at", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new CommercialOfferMutationResponse(
            offerId,
            offer.Status,
            changed,
            correlationId);
    }

    public async Task<IReadOnlyList<CommercialDocumentSummary>>
        GetClientDocumentsAsync(
            PortalSessionContext session,
            CancellationToken cancellationToken)
    {
        var documents = new List<CommercialDocumentSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
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
                document.service_request_id,
                request.reference AS service_request_reference
            FROM commercial_documents document
            LEFT JOIN service_requests request
                ON request.id = document.service_request_id
            WHERE document.customer_id = @customer_id
              AND document.shared_at IS NOT NULL
            ORDER BY document.updated_at DESC, document.id DESC;
            """;
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(ReadDocumentSummary(reader));
        }

        return documents;
    }

    public async Task<CommercialDocumentDetail?> GetClientDocumentAsync(
        PortalSessionContext session,
        string documentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
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
                document.service_request_id,
                request.reference AS service_request_reference
            FROM commercial_documents document
            LEFT JOIN service_requests request
                ON request.id = document.service_request_id
            WHERE document.id = @document_id
              AND document.customer_id = @customer_id
              AND document.shared_at IS NOT NULL
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@document_id", documentId);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var summary = ReadDocumentSummary(reader);
        await reader.DisposeAsync();

        return new CommercialDocumentDetail(
            summary.Id,
            summary.DocumentType,
            summary.Status,
            summary.Title,
            summary.InternalReference,
            summary.Currency,
            summary.SubtotalAmountCents,
            summary.TaxAmountCents,
            summary.TotalAmountCents,
            summary.Disclaimer,
            summary.CreatedAt,
            summary.UpdatedAt,
            summary.SharedAt,
            summary.ServiceRequestId,
            summary.ServiceRequestReference,
            await GetLinesAsync(connection, documentId, cancellationToken));
    }

    public async Task<IReadOnlyList<AdminCommercialDocumentSummary>>
        GetAdminDocumentsAsync(
            CancellationToken cancellationToken)
    {
        var documents = new List<AdminCommercialDocumentSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
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
                document.service_request_id,
                request.reference AS service_request_reference,
                customer.id AS customer_id,
                customer.external_reference AS customer_reference,
                customer.display_name AS customer_name
            FROM commercial_documents document
            INNER JOIN customers customer
                ON customer.id = document.customer_id
            LEFT JOIN service_requests request
                ON request.id = document.service_request_id
            ORDER BY document.updated_at DESC, document.id DESC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(ReadAdminDocumentSummary(reader));
        }

        return documents;
    }

    public async Task<AdminCommercialDocumentDetail?> GetAdminDocumentAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
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
                document.service_request_id,
                request.reference AS service_request_reference,
                customer.id AS customer_id,
                customer.external_reference AS customer_reference,
                customer.display_name AS customer_name,
                author.display_name AS created_by_display_name
            FROM commercial_documents document
            INNER JOIN customers customer
                ON customer.id = document.customer_id
            INNER JOIN portal_users author
                ON author.id = document.created_by_user_id
            LEFT JOIN service_requests request
                ON request.id = document.service_request_id
            WHERE document.id = @document_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@document_id", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var summary = ReadAdminDocumentSummary(reader);
        var createdByDisplayName =
            ReadNullableString(reader, "created_by_display_name")
            ?? "Administration interne";
        await reader.DisposeAsync();

        return new AdminCommercialDocumentDetail(
            summary.Id,
            summary.DocumentType,
            summary.Status,
            summary.Title,
            summary.InternalReference,
            summary.Currency,
            summary.SubtotalAmountCents,
            summary.TaxAmountCents,
            summary.TotalAmountCents,
            summary.Disclaimer,
            summary.CreatedAt,
            summary.UpdatedAt,
            summary.SharedAt,
            summary.ServiceRequestId,
            summary.ServiceRequestReference,
            summary.CustomerReference,
            summary.CustomerName,
            createdByDisplayName,
            await GetLinesAsync(connection, documentId, cancellationToken));
    }

    public async Task<CommercialDocumentMutationResponse> CreateDocumentAsync(
        PortalSessionContext actor,
        ValidatedCommercialDocument document,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var customerId = await ResolveCustomerIdAsync(
            connection,
            transaction,
            document.CustomerReference!,
            cancellationToken);
        _ = await ResolveServiceRequestReferenceAsync(
            connection,
            transaction,
            customerId,
            document.ServiceRequestId,
            cancellationToken);
        var documentId = Guid.NewGuid().ToString("D");
        var now = DateTime.UtcNow;
        var reference = CreateReference();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO commercial_documents (
                    id,
                    customer_id,
                    service_request_id,
                    document_type,
                    status,
                    title,
                    internal_reference,
                    currency,
                    subtotal_amount_cents,
                    tax_amount_cents,
                    total_amount_cents,
                    disclaimer,
                    created_by_user_id,
                    created_at,
                    updated_at,
                    shared_at,
                    cancelled_at
                ) VALUES (
                    @id,
                    @customer_id,
                    @service_request_id,
                    @document_type,
                    @status,
                    @title,
                    @internal_reference,
                    @currency,
                    0,
                    0,
                    0,
                    @disclaimer,
                    @created_by_user_id,
                    @created_at,
                    @updated_at,
                    NULL,
                    NULL
                );
                """;
            command.Parameters.AddWithValue("@id", documentId);
            command.Parameters.AddWithValue("@customer_id", customerId);
            command.Parameters.AddWithValue(
                "@service_request_id",
                DbValue(document.ServiceRequestId));
            command.Parameters.AddWithValue("@document_type", document.DocumentType);
            command.Parameters.AddWithValue("@status", document.Status);
            command.Parameters.AddWithValue("@title", document.Title);
            command.Parameters.AddWithValue("@internal_reference", reference);
            command.Parameters.AddWithValue("@currency", document.Currency);
            command.Parameters.AddWithValue("@disclaimer", document.Disclaimer);
            command.Parameters.AddWithValue("@created_by_user_id", actor.UserId);
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new CommercialDocumentMutationResponse(
            documentId,
            reference,
            document.Status,
            true,
            correlationId);
    }

    public async Task<CommercialDocumentMutationResponse> UpdateDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        ValidatedCommercialDocument document,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var current = await ReadDocumentForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        if (current.Status != CommercialStatuses.Draft)
        {
            throw new PortalValidationException();
        }

        if (document.CustomerReference is not null)
        {
            var customerId = await ResolveCustomerIdAsync(
                connection,
                transaction,
                document.CustomerReference,
                cancellationToken);
            if (!string.Equals(customerId, current.CustomerId, StringComparison.Ordinal))
            {
                throw new PortalValidationException();
            }
        }

        _ = await ResolveServiceRequestReferenceAsync(
            connection,
            transaction,
            current.CustomerId,
            document.ServiceRequestId,
            cancellationToken);
        var changed =
            current.DocumentType != document.DocumentType
            || current.Status != document.Status
            || current.Title != document.Title
            || current.Disclaimer != document.Disclaimer
            || current.ServiceRequestId != document.ServiceRequestId;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE commercial_documents
                SET service_request_id = @service_request_id,
                    document_type = @document_type,
                    status = @status,
                    title = @title,
                    disclaimer = @disclaimer,
                    updated_at = @updated_at
                WHERE id = @id;
                """;
            command.Parameters.AddWithValue("@id", documentId);
            command.Parameters.AddWithValue(
                "@service_request_id",
                DbValue(document.ServiceRequestId));
            command.Parameters.AddWithValue("@document_type", document.DocumentType);
            command.Parameters.AddWithValue("@status", document.Status);
            command.Parameters.AddWithValue("@title", document.Title);
            command.Parameters.AddWithValue("@disclaimer", document.Disclaimer);
            command.Parameters.AddWithValue("@updated_at", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new CommercialDocumentMutationResponse(
            documentId,
            current.InternalReference,
            document.Status,
            changed,
            correlationId);
    }

    public async Task<CommercialDocumentLineMutationResponse> AddLineAsync(
        PortalSessionContext actor,
        string documentId,
        ValidatedCommercialDocumentLine line,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        _ = await ReadDraftDocumentForLineMutationAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        var resolved = await ResolveLineInputAsync(
            connection,
            transaction,
            line,
            cancellationToken);
        var lineId = Guid.NewGuid().ToString("D");
        var now = DateTime.UtcNow;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO commercial_document_lines (
                    id,
                    document_id,
                    offer_id,
                    label,
                    description,
                    quantity,
                    unit_label,
                    unit_price_cents,
                    tax_rate_basis_points,
                    line_total_cents,
                    sort_order,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @document_id,
                    @offer_id,
                    @label,
                    @description,
                    @quantity,
                    @unit_label,
                    @unit_price_cents,
                    @tax_rate_basis_points,
                    @line_total_cents,
                    @sort_order,
                    @created_at,
                    @updated_at
                );
                """;
            command.Parameters.AddWithValue("@id", lineId);
            command.Parameters.AddWithValue("@document_id", documentId);
            command.Parameters.AddWithValue("@offer_id", DbValue(resolved.OfferId));
            command.Parameters.AddWithValue("@label", resolved.Label);
            command.Parameters.AddWithValue("@description", resolved.Description);
            command.Parameters.AddWithValue("@quantity", resolved.Quantity);
            command.Parameters.AddWithValue("@unit_label", resolved.UnitLabel);
            command.Parameters.AddWithValue("@unit_price_cents", resolved.UnitPriceCents);
            command.Parameters.AddWithValue(
                "@tax_rate_basis_points",
                DbValue(resolved.TaxRateBasisPoints));
            command.Parameters.AddWithValue("@line_total_cents", resolved.LineTotalCents);
            command.Parameters.AddWithValue("@sort_order", resolved.SortOrder);
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await RecalculateDocumentTotalsAsync(
            connection,
            transaction,
            documentId,
            now,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CommercialDocumentLineMutationResponse(
            lineId,
            documentId,
            true,
            correlationId);
    }

    public async Task<CommercialDocumentLineMutationResponse> UpdateLineAsync(
        PortalSessionContext actor,
        string documentId,
        string lineId,
        ValidatedCommercialDocumentLine line,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        _ = await ReadDraftDocumentForLineMutationAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        var current = await ReadLineForUpdateAsync(
            connection,
            transaction,
            documentId,
            lineId,
            cancellationToken);
        var resolved = await ResolveLineInputAsync(
            connection,
            transaction,
            line,
            cancellationToken);
        var changed =
            current.OfferId != resolved.OfferId
            || current.Label != resolved.Label
            || current.Description != resolved.Description
            || current.Quantity != resolved.Quantity
            || current.UnitLabel != resolved.UnitLabel
            || current.UnitPriceCents != resolved.UnitPriceCents
            || current.TaxRateBasisPoints != resolved.TaxRateBasisPoints
            || current.LineTotalCents != resolved.LineTotalCents
            || current.SortOrder != resolved.SortOrder;
        var now = DateTime.UtcNow;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE commercial_document_lines
                SET offer_id = @offer_id,
                    label = @label,
                    description = @description,
                    quantity = @quantity,
                    unit_label = @unit_label,
                    unit_price_cents = @unit_price_cents,
                    tax_rate_basis_points = @tax_rate_basis_points,
                    line_total_cents = @line_total_cents,
                    sort_order = @sort_order,
                    updated_at = @updated_at
                WHERE id = @id
                  AND document_id = @document_id;
                """;
            command.Parameters.AddWithValue("@id", lineId);
            command.Parameters.AddWithValue("@document_id", documentId);
            command.Parameters.AddWithValue("@offer_id", DbValue(resolved.OfferId));
            command.Parameters.AddWithValue("@label", resolved.Label);
            command.Parameters.AddWithValue("@description", resolved.Description);
            command.Parameters.AddWithValue("@quantity", resolved.Quantity);
            command.Parameters.AddWithValue("@unit_label", resolved.UnitLabel);
            command.Parameters.AddWithValue("@unit_price_cents", resolved.UnitPriceCents);
            command.Parameters.AddWithValue(
                "@tax_rate_basis_points",
                DbValue(resolved.TaxRateBasisPoints));
            command.Parameters.AddWithValue("@line_total_cents", resolved.LineTotalCents);
            command.Parameters.AddWithValue("@sort_order", resolved.SortOrder);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await RecalculateDocumentTotalsAsync(
            connection,
            transaction,
            documentId,
            now,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CommercialDocumentLineMutationResponse(
            lineId,
            documentId,
            changed,
            correlationId);
    }

    public async Task<CommercialDocumentMutationResponse> ShareDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var current = await ReadDocumentForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        if (current.Status == CommercialStatuses.Cancelled)
        {
            throw new PortalValidationException();
        }

        if (current.Status == CommercialStatuses.SharedWithCustomer)
        {
            await transaction.CommitAsync(cancellationToken);
            return new CommercialDocumentMutationResponse(
                documentId,
                current.InternalReference,
                current.Status,
                false,
                correlationId);
        }

        var now = DateTime.UtcNow;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE commercial_documents
                SET status = @status,
                    shared_at = COALESCE(shared_at, @shared_at),
                    updated_at = @updated_at
                WHERE id = @id;
                """;
            command.Parameters.AddWithValue("@id", documentId);
            command.Parameters.AddWithValue(
                "@status",
                CommercialStatuses.SharedWithCustomer);
            command.Parameters.AddWithValue("@shared_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new CommercialDocumentMutationResponse(
            documentId,
            current.InternalReference,
            CommercialStatuses.SharedWithCustomer,
            true,
            correlationId);
    }

    public async Task<CommercialDocumentMutationResponse> CancelDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var current = await ReadDocumentForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        if (current.Status == CommercialStatuses.Cancelled)
        {
            await transaction.CommitAsync(cancellationToken);
            return new CommercialDocumentMutationResponse(
                documentId,
                current.InternalReference,
                current.Status,
                false,
                correlationId);
        }

        var now = DateTime.UtcNow;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE commercial_documents
                SET status = @status,
                    cancelled_at = COALESCE(cancelled_at, @cancelled_at),
                    updated_at = @updated_at
                WHERE id = @id;
                """;
            command.Parameters.AddWithValue("@id", documentId);
            command.Parameters.AddWithValue("@status", CommercialStatuses.Cancelled);
            command.Parameters.AddWithValue("@cancelled_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new CommercialDocumentMutationResponse(
            documentId,
            current.InternalReference,
            CommercialStatuses.Cancelled,
            true,
            correlationId);
    }

    private static CommercialOfferSummary ReadOffer(MySqlDataReader reader)
        => new(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("name"),
            reader.GetString("description"),
            reader.GetString("category"),
            reader.GetString("unit_label"),
            reader.GetString("price_kind"),
            reader.GetInt32("price_amount_cents"),
            reader.GetString("currency"),
            reader.IsDBNull(reader.GetOrdinal("tax_rate_basis_points"))
                ? null
                : reader.GetInt32("tax_rate_basis_points"),
            reader.IsDBNull(reader.GetOrdinal("external_reference"))
                ? null
                : reader.GetString("external_reference"),
            reader.GetString("status"),
            reader.GetInt32("display_order"),
            reader.GetString("billing_cadence"),
            ReadNullableString(reader, "paypal_plan_id"),
            ToUtcIso(reader.GetDateTime("created_at")),
            ToUtcIso(reader.GetDateTime("updated_at")));

    private static CommercialDocumentSummary ReadDocumentSummary(
        MySqlDataReader reader)
        => new(
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
            ReadNullableDateTimeIso(reader, "shared_at"),
            ReadNullableString(reader, "service_request_id"),
            ReadNullableString(reader, "service_request_reference"));

    private static AdminCommercialDocumentSummary ReadAdminDocumentSummary(
        MySqlDataReader reader)
    {
        var summary = ReadDocumentSummary(reader);
        var customerId = MariaDbIdentifierReader.ReadRequired(reader, "customer_id");
        var customerReference =
            ReadNullableString(reader, "customer_reference") ?? customerId;
        var customerName =
            ReadNullableString(reader, "customer_name") ?? customerReference;
        return new AdminCommercialDocumentSummary(
            summary.Id,
            summary.DocumentType,
            summary.Status,
            summary.Title,
            summary.InternalReference,
            summary.Currency,
            summary.SubtotalAmountCents,
            summary.TaxAmountCents,
            summary.TotalAmountCents,
            summary.Disclaimer,
            summary.CreatedAt,
            summary.UpdatedAt,
            summary.SharedAt,
            summary.ServiceRequestId,
            summary.ServiceRequestReference,
            customerReference,
            customerName);
    }

    private static async Task<IReadOnlyList<CommercialDocumentLine>> GetLinesAsync(
        MySqlConnection connection,
        string documentId,
        CancellationToken cancellationToken)
    {
        var lines = new List<CommercialDocumentLine>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                offer_id,
                label,
                description,
                quantity,
                unit_label,
                unit_price_cents,
                tax_rate_basis_points,
                line_total_cents,
                sort_order,
                created_at,
                updated_at
            FROM commercial_document_lines
            WHERE document_id = @document_id
            ORDER BY sort_order, created_at, id;
            """;
        command.Parameters.AddWithValue("@document_id", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(new CommercialDocumentLine(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                ReadNullableString(reader, "offer_id"),
                reader.GetString("label"),
                reader.GetString("description"),
                reader.GetDecimal("quantity"),
                reader.GetString("unit_label"),
                reader.GetInt32("unit_price_cents"),
                reader.IsDBNull(reader.GetOrdinal("tax_rate_basis_points"))
                    ? null
                    : reader.GetInt32("tax_rate_basis_points"),
                reader.GetInt32("line_total_cents"),
                reader.GetInt32("sort_order"),
                ToUtcIso(reader.GetDateTime("created_at")),
                ToUtcIso(reader.GetDateTime("updated_at"))));
        }

        return lines;
    }

    private static async Task<OfferRow> ReadOfferForUpdateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string offerId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                name,
                description,
                category,
                unit_label,
                price_amount_cents,
                status,
                display_order,
                billing_cadence,
                paypal_plan_id
            FROM commercial_offers
            WHERE id = @id
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("@id", offerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new PortalDataNotFoundException();
        }

        return new OfferRow(
            reader.GetString("name"),
            reader.GetString("description"),
            reader.GetString("category"),
            reader.GetString("unit_label"),
            reader.GetInt32("price_amount_cents"),
            reader.GetString("status"),
            reader.GetInt32("display_order"),
            reader.GetString("billing_cadence"),
            ReadNullableString(reader, "paypal_plan_id"));
    }

    private static async Task<string> ResolveCustomerIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string customerReference,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id
            FROM customers
            WHERE external_reference = @customer_reference
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_reference", customerReference);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is null or DBNull
            ? throw new PortalValidationException()
            : MariaDbIdentifierReader.ConvertRequiredValue(result, "customers.id");
    }

    private static async Task<string?> ResolveServiceRequestReferenceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string customerId,
        string? serviceRequestId,
        CancellationToken cancellationToken)
    {
        if (serviceRequestId is null)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT reference
            FROM service_requests
            WHERE id = @id
              AND customer_id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", serviceRequestId);
        command.Parameters.AddWithValue("@customer_id", customerId);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is null or DBNull
            ? throw new PortalValidationException()
            : Convert.ToString(result)!;
    }

    private static async Task<DocumentRow> ReadDocumentForUpdateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string documentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                customer_id,
                service_request_id,
                document_type,
                status,
                title,
                internal_reference,
                disclaimer
            FROM commercial_documents
            WHERE id = @id
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("@id", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new PortalDataNotFoundException();
        }

        return new DocumentRow(
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            ReadNullableString(reader, "service_request_id"),
            reader.GetString("document_type"),
            reader.GetString("status"),
            reader.GetString("title"),
            reader.GetString("internal_reference"),
            reader.GetString("disclaimer"));
    }

    private static async Task<DocumentRow> ReadDraftDocumentForLineMutationAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string documentId,
        CancellationToken cancellationToken)
    {
        var document = await ReadDocumentForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        if (document.Status != CommercialStatuses.Draft)
        {
            throw new PortalValidationException();
        }

        return document;
    }

    private static async Task<LineRow> ReadLineForUpdateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string documentId,
        string lineId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                offer_id,
                label,
                description,
                quantity,
                unit_label,
                unit_price_cents,
                tax_rate_basis_points,
                line_total_cents,
                sort_order
            FROM commercial_document_lines
            WHERE id = @id
              AND document_id = @document_id
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("@id", lineId);
        command.Parameters.AddWithValue("@document_id", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new PortalDataNotFoundException();
        }

        return new LineRow(
            ReadNullableString(reader, "offer_id"),
            reader.GetString("label"),
            reader.GetString("description"),
            reader.GetDecimal("quantity"),
            reader.GetString("unit_label"),
            reader.GetInt32("unit_price_cents"),
            reader.IsDBNull(reader.GetOrdinal("tax_rate_basis_points"))
                ? null
                : reader.GetInt32("tax_rate_basis_points"),
            reader.GetInt32("line_total_cents"),
            reader.GetInt32("sort_order"));
    }

    private static async Task<ResolvedLineInput> ResolveLineInputAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ValidatedCommercialDocumentLine line,
        CancellationToken cancellationToken)
    {
        OfferDefaults? offer = null;
        if (line.OfferId is not null)
        {
            offer = await ResolveOfferDefaultsAsync(
                connection,
                transaction,
                line.OfferId,
                cancellationToken);
        }

        var label = line.Label ?? offer?.Name
            ?? throw new PortalValidationException();
        var unitLabel = line.UnitLabel ?? offer?.UnitLabel
            ?? throw new PortalValidationException();
        var unitPriceCents = line.UnitPriceCents ?? offer?.PriceAmountCents
            ?? throw new PortalValidationException();
        if (unitPriceCents < 0)
        {
            throw new PortalValidationException();
        }

        var description = string.IsNullOrWhiteSpace(line.Description)
            ? offer?.Description ?? string.Empty
            : line.Description;
        var lineTotalCents = (int)decimal.Round(
            line.Quantity * unitPriceCents,
            0,
            MidpointRounding.AwayFromZero);

        return new ResolvedLineInput(
            offer?.Id,
            label,
            description,
            line.Quantity,
            unitLabel,
            unitPriceCents,
            line.TaxRateBasisPoints,
            lineTotalCents,
            line.SortOrder);
    }

    private static async Task<OfferDefaults> ResolveOfferDefaultsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string offerId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, name, description, unit_label, price_amount_cents
            FROM commercial_offers
            WHERE id = @id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", offerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new PortalValidationException();
        }

        return new OfferDefaults(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("name"),
            reader.GetString("description"),
            reader.GetString("unit_label"),
            reader.GetInt32("price_amount_cents"));
    }

    private static async Task RecalculateDocumentTotalsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string documentId,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE commercial_documents document
            LEFT JOIN (
                SELECT
                    document_id,
                    COALESCE(SUM(line_total_cents), 0) AS subtotal_amount_cents,
                    COALESCE(SUM(
                        CASE
                            WHEN tax_rate_basis_points IS NULL THEN 0
                            ELSE ROUND(
                                line_total_cents * (tax_rate_basis_points / 10000),
                                0
                            )
                        END
                    ), 0) AS tax_amount_cents
                FROM commercial_document_lines
                WHERE document_id = @document_id
                GROUP BY document_id
            ) totals
                ON totals.document_id = document.id
            SET document.subtotal_amount_cents =
                    COALESCE(totals.subtotal_amount_cents, 0),
                document.tax_amount_cents =
                    COALESCE(totals.tax_amount_cents, 0),
                document.total_amount_cents =
                    COALESCE(totals.subtotal_amount_cents, 0)
                    + COALESCE(totals.tax_amount_cents, 0),
                document.updated_at = @updated_at
            WHERE document.id = @document_id;
            """;
        command.Parameters.AddWithValue("@document_id", documentId);
        command.Parameters.AddWithValue("@updated_at", updatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<MySqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string? ReadNullableString(
    MySqlDataReader reader,
    string columnName)
    {
    var ordinal = reader.GetOrdinal(columnName);

    if (reader.IsDBNull(ordinal))
    {
        return null;
    }

    var value = reader.GetValue(ordinal);

    return value switch
    {
        string stringValue => stringValue,
        Guid guidValue => guidValue.ToString("D"),
        IFormattable formattableValue => formattableValue.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
    }

    private static string? ReadNullableDateTimeIso(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : ToUtcIso(reader.GetDateTime(columnName));

    private static string ToUtcIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;

    private static object DbValue(int? value)
        => value is null ? DBNull.Value : value.Value;

    private static string CreateReference()
        => $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..17]
            .Insert(0, "COM-")
            .ToUpperInvariant();

    private sealed record OfferRow(
        string Name,
        string Description,
        string Category,
        string UnitLabel,
        int PriceAmountCents,
        string Status,
        int DisplayOrder,
        string BillingCadence,
        string? PayPalPlanId);

    private sealed record DocumentRow(
        string CustomerId,
        string? ServiceRequestId,
        string DocumentType,
        string Status,
        string Title,
        string InternalReference,
        string Disclaimer);

    private sealed record LineRow(
        string? OfferId,
        string Label,
        string Description,
        decimal Quantity,
        string UnitLabel,
        int UnitPriceCents,
        int? TaxRateBasisPoints,
        int LineTotalCents,
        int SortOrder);

    private sealed record OfferDefaults(
        string Id,
        string Name,
        string Description,
        string UnitLabel,
        int PriceAmountCents);

    private sealed record ResolvedLineInput(
        string? OfferId,
        string Label,
        string Description,
        decimal Quantity,
        string UnitLabel,
        int UnitPriceCents,
        int? TaxRateBasisPoints,
        int LineTotalCents,
        int SortOrder);

    public async Task<DocumentForIssuing?> GetDocumentForIssuingAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                d.id            AS document_id,
                d.title,
                d.internal_reference,
                d.currency,
                d.total_amount_cents,
                d.status,
                c.id            AS customer_id,
                c.external_reference,
                c.display_name,
                c.billing_email,
                c.address,
                c.city,
                c.country
            FROM commercial_documents d
            JOIN customers c ON c.id = d.customer_id
            WHERE d.id = @documentId
            """;
        cmd.Parameters.AddWithValue("documentId", documentId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var doc = new
        {
            DocumentId = MariaDbIdentifierReader.ReadRequired(reader, "document_id"),
            CustomerId = MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            CustomerExternalReference = reader.GetString("external_reference"),
            CustomerDisplayName = reader.GetString("display_name"),
            CustomerBillingEmail = ReadNullableString(reader, "billing_email"),
            CustomerAddress = ReadNullableString(reader, "address"),
            CustomerCity = ReadNullableString(reader, "city"),
            CustomerCountry = ReadNullableString(reader, "country"),
            DocumentTitle = reader.GetString("title"),
            InternalReference = reader.GetString("internal_reference"),
            Currency = reader.GetString("currency"),
            TotalAmountCents = reader.GetInt32("total_amount_cents"),
            Status = reader.GetString("status")
        };
        await reader.CloseAsync();

        var lines = await GetLinesAsync(
            connection, documentId, cancellationToken);

        return new DocumentForIssuing(
            doc.DocumentId,
            doc.CustomerId,
            doc.CustomerExternalReference,
            doc.CustomerDisplayName,
            doc.CustomerBillingEmail,
            doc.CustomerAddress,
            doc.CustomerCity,
            doc.CustomerCountry,
            doc.DocumentTitle,
            doc.InternalReference,
            doc.Currency,
            doc.TotalAmountCents,
            doc.Status,
            lines);
    }

    public async Task MarkDocumentIssuedAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE commercial_documents SET
                status = 'issued',
                updated_at = NOW(6)
            WHERE id = @documentId
            """;
        cmd.Parameters.AddWithValue("documentId", documentId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkDocumentPaidAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE commercial_documents SET
                status = 'paid',
                updated_at = NOW(6)
            WHERE id = @documentId
            """;
        cmd.Parameters.AddWithValue("documentId", documentId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string> CreateBillingDocumentFromOfferAsync(
        string customerId,
        string offerId,
        string subscriptionId,
        string title,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var systemUserId = await ResolveSystemActorAsync(
            connection,
            transaction,
            cancellationToken);

        var offer = await ReadOfferDetailsAsync(
            connection,
            transaction,
            offerId,
            cancellationToken);

        var documentId = Guid.NewGuid().ToString("D");
        var now = DateTime.UtcNow;
        var reference = CreateReference();

        await using (var documentCommand = connection.CreateCommand())
        {
            documentCommand.Transaction = transaction;
            documentCommand.CommandText =
                """
                INSERT INTO commercial_documents (
                    id,
                    customer_id,
                    service_request_id,
                    subscription_id,
                    document_type,
                    status,
                    title,
                    internal_reference,
                    currency,
                    subtotal_amount_cents,
                    tax_amount_cents,
                    total_amount_cents,
                    disclaimer,
                    created_by_user_id,
                    created_at,
                    updated_at,
                    shared_at,
                    cancelled_at
                ) VALUES (
                    @id,
                    @customerId,
                    NULL,
                    @subscriptionId,
                    'informational_invoice',
                    'shared_with_customer',
                    @title,
                    @reference,
                    0,
                    0,
                    0,
                    @disclaimer,
                    @createdBy,
                    @createdAt,
                    @updatedAt,
                    @sharedAt,
                    NULL
                );
                """;
            documentCommand.Parameters.AddWithValue("@id", documentId);
            documentCommand.Parameters.AddWithValue("@customerId", customerId);
            documentCommand.Parameters.AddWithValue("@subscriptionId", subscriptionId);
            documentCommand.Parameters.AddWithValue("@title", title);
            documentCommand.Parameters.AddWithValue("@reference", reference);
            documentCommand.Parameters.AddWithValue(
                "@disclaimer",
                CommercialStatuses.DefaultDisclaimer);
            documentCommand.Parameters.AddWithValue("@createdBy", systemUserId);
            documentCommand.Parameters.AddWithValue("@createdAt", now);
            documentCommand.Parameters.AddWithValue("@updatedAt", now);
            documentCommand.Parameters.AddWithValue("@sharedAt", now);
            await documentCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var lineId = Guid.NewGuid().ToString("D");
        await using (var lineCommand = connection.CreateCommand())
        {
            lineCommand.Transaction = transaction;
            lineCommand.CommandText =
                """
                INSERT INTO commercial_document_lines (
                    id,
                    document_id,
                    offer_id,
                    label,
                    description,
                    quantity,
                    unit_label,
                    unit_price_cents,
                    tax_rate_basis_points,
                    line_total_cents,
                    sort_order,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @documentId,
                    @offerId,
                    @label,
                    @description,
                    1.00,
                    @unitLabel,
                    @unitPriceCents,
                    @taxRate,
                    @lineTotal,
                    10,
                    @createdAt,
                    @updatedAt
                );
                """;
            lineCommand.Parameters.AddWithValue("@id", lineId);
            lineCommand.Parameters.AddWithValue("@documentId", documentId);
            lineCommand.Parameters.AddWithValue("@offerId", offerId);
            lineCommand.Parameters.AddWithValue("@label", offer.Name);
            lineCommand.Parameters.AddWithValue("@description", offer.Description);
            lineCommand.Parameters.AddWithValue("@unitLabel", offer.UnitLabel);
            lineCommand.Parameters.AddWithValue(
                "@unitPriceCents",
                offer.PriceAmountCents);
            lineCommand.Parameters.AddWithValue(
                "@taxRate",
                offer.TaxRateBasisPoints is null
                    ? DBNull.Value
                    : (object)offer.TaxRateBasisPoints.Value);
            lineCommand.Parameters.AddWithValue(
                "@lineTotal",
                offer.PriceAmountCents);
            lineCommand.Parameters.AddWithValue("@createdAt", now);
            lineCommand.Parameters.AddWithValue("@updatedAt", now);
            await lineCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await RecalculateDocumentTotalsAsync(
            connection,
            transaction,
            documentId,
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return documentId;
    }

    public async Task<IReadOnlyList<CommercialDocumentSummary>>
        GetDocumentsForSubscriptionAsync(
            string subscriptionId,
            CancellationToken cancellationToken)
    {
        var documents = new List<CommercialDocumentSummary>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
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
                document.service_request_id,
                NULL AS service_request_reference
            FROM commercial_documents document
            WHERE document.subscription_id = @subscriptionId
            ORDER BY document.created_at DESC, document.id DESC;
            """;
        command.Parameters.AddWithValue("subscriptionId", subscriptionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(ReadDocumentSummary(reader));
        }

        return documents;
    }

    private static async Task<string> ResolveSystemActorAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id FROM portal_users
            WHERE role = 'internal_admin'
              AND status = 'active'
            ORDER BY created_at
            LIMIT 1;
            """;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
        {
            throw new InvalidOperationException(
                "No active internal_admin user available to act as system actor.");
        }

        return MariaDbIdentifierReader.ConvertRequiredValue(
            result,
            "portal_users.id");
    }

    private sealed record OfferDetailsRow(
        string Name,
        string Description,
        string UnitLabel,
        int PriceAmountCents,
        int? TaxRateBasisPoints);

    private static async Task<OfferDetailsRow> ReadOfferDetailsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string offerId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT name, description, unit_label, price_amount_cents,
                tax_rate_basis_points
            FROM commercial_offers
            WHERE id = @id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", offerId);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"Offer {offerId} not found.");
        }

        return new OfferDetailsRow(
            reader.GetString("name"),
            reader.GetString("description"),
            reader.GetString("unit_label"),
            reader.GetInt32("price_amount_cents"),
            reader.IsDBNull(reader.GetOrdinal("tax_rate_basis_points"))
                ? null
                : reader.GetInt32("tax_rate_basis_points"));
    }
}
