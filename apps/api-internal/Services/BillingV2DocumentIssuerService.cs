using System.Data;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2DocumentIssueResult(
    bool Succeeded,
    string ReasonCode,
    string? CommercialDocumentId,
    BpceIssuedInvoiceInfo? Invoice);

public interface IBillingV2DocumentIssuerService
{
    Task<BillingV2DocumentIssueResult> EnsureInitialInvoiceAsync(
        string subscriptionId,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2DocumentIssuerService
    : IBillingV2DocumentIssuerService
{
    public static NoOpBillingV2DocumentIssuerService Instance { get; } = new();

    private NoOpBillingV2DocumentIssuerService()
    {
    }

    public Task<BillingV2DocumentIssueResult> EnsureInitialInvoiceAsync(
        string subscriptionId,
        string correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(new BillingV2DocumentIssueResult(
            false,
            "BILLING_V2_DOCUMENT_ISSUER_DISABLED",
            CommercialDocumentId: null,
            Invoice: null));
}

public sealed class BillingV2DocumentIssuerService
    : IBillingV2DocumentIssuerService
{
    public const string InitialSubscriptionDocumentKind =
        "initial_subscription_invoice";

    private const string CommercialDocumentOrigin = "billing_v2";

    private readonly SqlRuntimeConfiguration _sql;
    private readonly IInvoiceIssuingService _invoiceIssuing;
    private readonly ILogger<BillingV2DocumentIssuerService> _logger;

    public BillingV2DocumentIssuerService(
        SqlRuntimeConfiguration sql,
        IInvoiceIssuingService invoiceIssuing,
        ILogger<BillingV2DocumentIssuerService> logger)
    {
        _sql = sql;
        _invoiceIssuing = invoiceIssuing;
        _logger = logger;
    }

    public async Task<BillingV2DocumentIssueResult> EnsureInitialInvoiceAsync(
        string subscriptionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return new BillingV2DocumentIssueResult(
                false,
                "BILLING_V2_DOCUMENT_ISSUER_NO_PERSISTENT_SQL",
                CommercialDocumentId: null,
                Invoice: null);
        }

        string documentId;
        await using (var connection = new MySqlConnection(_sql.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            var existing = await ReadExistingDocumentIdAsync(
                connection,
                transaction,
                subscriptionId,
                cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                documentId = existing;
            }
            else
            {
                var source = await LoadSourceAsync(
                    connection,
                    transaction,
                    subscriptionId,
                    cancellationToken);
                if (source is null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new BillingV2DocumentIssueResult(
                        false,
                        "BILLING_V2_DOCUMENT_SUBSCRIPTION_NOT_FOUND",
                        CommercialDocumentId: null,
                        Invoice: null);
                }

                if (!string.Equals(
                    source.Subscription.Status,
                    "active",
                    StringComparison.Ordinal))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new BillingV2DocumentIssueResult(
                        false,
                        "BILLING_V2_DOCUMENT_SUBSCRIPTION_NOT_ACTIVE",
                        CommercialDocumentId: null,
                        Invoice: null);
                }

                var plan = BillingV2DocumentSnapshotPlanner.Plan(source);
                if (plan.TotalAmountCents <= 0)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new BillingV2DocumentIssueResult(
                        false,
                        "BILLING_V2_DOCUMENT_EMPTY",
                        CommercialDocumentId: null,
                        Invoice: null);
                }

                documentId = await CreateDocumentAsync(
                    connection,
                    transaction,
                    source,
                    plan,
                    correlationId,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }

        var issue = await _invoiceIssuing.IssueInvoiceAsync(
            documentId,
            sendEmail: false,
            correlationId,
            cancellationToken);
        if (!issue.Succeeded
            && !string.Equals(
                issue.Code,
                "INVOICE_ALREADY_ISSUED",
                StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Billing V2 document {DocumentId} invoice issuing failed: {Code}.",
                documentId,
                issue.Code);
            return new BillingV2DocumentIssueResult(
                false,
                issue.Code,
                documentId,
                issue.Invoice);
        }

        await UpdateDocumentStatusAsync(
            documentId,
            status: "issued",
            reasonCode: issue.Code,
            cancellationToken);

        var payment = await _invoiceIssuing.ConfirmPaymentAsync(
            documentId,
            correlationId,
            paymentMethod: "billing_v2_provider",
            cancellationToken);
        if (!payment.Succeeded)
        {
            return new BillingV2DocumentIssueResult(
                false,
                payment.Code,
                documentId,
                payment.Invoice ?? issue.Invoice);
        }

        await UpdateDocumentStatusAsync(
            documentId,
            status: "paid",
            reasonCode: payment.Code,
            cancellationToken);
        return new BillingV2DocumentIssueResult(
            true,
            payment.Code,
            documentId,
            payment.Invoice);
    }

    private static async Task<string?> ReadExistingDocumentIdAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT commercial_document_id
            FROM billing_v2_subscription_documents
            WHERE subscription_id = @subscription_id
              AND document_kind = @document_kind
            ORDER BY created_at ASC
            LIMIT 1
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@document_kind",
            InitialSubscriptionDocumentKind);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar);
    }

    private static async Task<BillingV2DocumentSource?> LoadSourceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        BillingV2DocumentSubscriptionSource? subscription = null;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT
                    subscription.id,
                    subscription.customer_id,
                    subscription.status,
                    subscription.payment_mode,
                    subscription.currency,
                    subscription.discount_basis_points_snapshot,
                    subscription.minimum_commitment_amount_cents,
                    subscription.created_at,
                    term.commitment_months,
                    customer.external_reference,
                    customer.display_name
                FROM billing_v2_subscriptions subscription
                INNER JOIN billing_v2_commitment_terms term
                    ON term.id = subscription.commitment_term_id
                INNER JOIN customers customer
                    ON customer.id = subscription.customer_id
                WHERE subscription.id = @subscription_id
                FOR UPDATE;
                """;
            command.Parameters.AddWithValue("@subscription_id", subscriptionId);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            subscription = new BillingV2DocumentSubscriptionSource(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
                reader.GetString("status"),
                reader.GetString("payment_mode"),
                reader.GetString("currency"),
                reader.GetInt32("discount_basis_points_snapshot"),
                reader.IsDBNull(
                    reader.GetOrdinal("minimum_commitment_amount_cents"))
                    ? null
                    : reader.GetInt64("minimum_commitment_amount_cents"),
                reader.GetDateTime("created_at"),
                reader.GetInt32("commitment_months"),
                reader.GetString("external_reference"),
                reader.GetString("display_name"));
        }

        var items = new List<BillingV2DocumentSourceItem>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT
                    item.id AS subscription_item_id,
                    item.service_price_id,
                    service.code AS service_code,
                    service.name AS service_name,
                    tier.code AS tier_code,
                    tier.name AS tier_name,
                    price.price_code,
                    price.billing_cadence,
                    price.tax_rate_basis_points,
                    item.quantity,
                    item.amount_cents_snapshot,
                    item.currency,
                    item.discount_eligible_snapshot
                FROM billing_v2_subscription_items item
                INNER JOIN billing_v2_services service
                    ON service.id = item.service_id
                LEFT JOIN billing_v2_service_tiers tier
                    ON tier.id = item.tier_id
                INNER JOIN billing_v2_service_prices price
                    ON price.id = item.service_price_id
                WHERE item.subscription_id = @subscription_id
                  AND item.status = 'active'
                  AND item.effective_from <= UTC_TIMESTAMP(6)
                  AND (item.effective_until IS NULL
                       OR item.effective_until > UTC_TIMESTAMP(6))
                ORDER BY service.display_order, tier.display_order, item.id;
                """;
            command.Parameters.AddWithValue("@subscription_id", subscriptionId);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new BillingV2DocumentSourceItem(
                    MariaDbIdentifierReader.ReadRequired(
                        reader,
                        "subscription_item_id"),
                    MariaDbIdentifierReader.ReadRequired(
                        reader,
                        "service_price_id"),
                    reader.GetString("service_code"),
                    reader.GetString("service_name"),
                    ReadNullableString(reader, "tier_code"),
                    ReadNullableString(reader, "tier_name"),
                    reader.GetString("price_code"),
                    reader.GetString("billing_cadence"),
                    reader.IsDBNull(reader.GetOrdinal("tax_rate_basis_points"))
                        ? null
                        : reader.GetInt32("tax_rate_basis_points"),
                    reader.GetInt32("quantity"),
                    reader.GetInt64("amount_cents_snapshot"),
                    reader.GetString("currency"),
                    reader.GetBoolean("discount_eligible_snapshot")));
            }
        }

        var priceLock = await LoadActivePriceLockAsync(
            connection,
            transaction,
            subscriptionId,
            cancellationToken);
        return new BillingV2DocumentSource(subscription, items, priceLock);
    }

    private static async Task<BillingV2DocumentPriceLock?> LoadActivePriceLockAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT lock_type, amount_cents, currency, effective_from, effective_until
            FROM billing_v2_subscription_price_locks
            WHERE subscription_id = @subscription_id
              AND status = 'active'
              AND effective_from <= UTC_TIMESTAMP(6)
              AND effective_until > UTC_TIMESTAMP(6)
            ORDER BY created_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2DocumentPriceLock(
            reader.GetString("lock_type"),
            reader.GetInt64("amount_cents"),
            reader.GetString("currency"),
            reader.GetDateTime("effective_from"),
            reader.GetDateTime("effective_until"));
    }

    private static async Task<string> CreateDocumentAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2DocumentSource source,
        BillingV2DocumentPlan plan,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var documentId = Guid.NewGuid().ToString("D");
        var subscriptionDocumentId = Guid.NewGuid().ToString("D");
        var systemActorId = await ResolveSystemActorAsync(
            connection,
            transaction,
            cancellationToken);
        var reference = CreateReference(source.Subscription.CustomerReference);
        var now = DateTime.UtcNow;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO commercial_documents (
                    id,
                    customer_id,
                    service_request_id,
                    subscription_id,
                    origin,
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
                    NULL,
                    NULL,
                    @origin,
                    'informational_invoice',
                    'shared_with_customer',
                    @title,
                    @reference,
                    @currency,
                    @subtotal_amount_cents,
                    @tax_amount_cents,
                    @total_amount_cents,
                    @disclaimer,
                    @created_by_user_id,
                    @created_at,
                    @updated_at,
                    @shared_at,
                    NULL
                );
                """;
            command.Parameters.AddWithValue("@id", documentId);
            command.Parameters.AddWithValue(
                "@customer_id",
                source.Subscription.CustomerId);
            command.Parameters.AddWithValue("@origin", CommercialDocumentOrigin);
            command.Parameters.AddWithValue("@title", plan.Title);
            command.Parameters.AddWithValue("@reference", reference);
            command.Parameters.AddWithValue("@currency", plan.Currency);
            command.Parameters.AddWithValue(
                "@subtotal_amount_cents",
                checked((int)plan.SubtotalAmountCents));
            command.Parameters.AddWithValue(
                "@tax_amount_cents",
                checked((int)plan.TaxAmountCents));
            command.Parameters.AddWithValue(
                "@total_amount_cents",
                checked((int)plan.TotalAmountCents));
            command.Parameters.AddWithValue(
                "@disclaimer",
                CommercialStatuses.DefaultDisclaimer);
            command.Parameters.AddWithValue(
                "@created_by_user_id",
                systemActorId);
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            command.Parameters.AddWithValue("@shared_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO billing_v2_subscription_documents (
                    id,
                    subscription_id,
                    commercial_document_id,
                    document_kind,
                    period_start,
                    period_end,
                    subtotal_amount_cents,
                    discount_amount_cents,
                    tax_amount_cents,
                    total_amount_cents,
                    currency,
                    status,
                    reason_code,
                    created_at,
                    updated_at
                ) VALUES (
                    @id,
                    @subscription_id,
                    @commercial_document_id,
                    @document_kind,
                    @period_start,
                    @period_end,
                    @subtotal_amount_cents,
                    @discount_amount_cents,
                    @tax_amount_cents,
                    @total_amount_cents,
                    @currency,
                    'created',
                    @reason_code,
                    @created_at,
                    @updated_at
                );
                """;
            command.Parameters.AddWithValue("@id", subscriptionDocumentId);
            command.Parameters.AddWithValue(
                "@subscription_id",
                source.Subscription.SubscriptionId);
            command.Parameters.AddWithValue(
                "@commercial_document_id",
                documentId);
            command.Parameters.AddWithValue(
                "@document_kind",
                InitialSubscriptionDocumentKind);
            command.Parameters.AddWithValue("@period_start", plan.PeriodStart);
            command.Parameters.AddWithValue("@period_end", plan.PeriodEnd);
            command.Parameters.AddWithValue(
                "@subtotal_amount_cents",
                plan.SubtotalAmountCents);
            command.Parameters.AddWithValue(
                "@discount_amount_cents",
                plan.DiscountAmountCents);
            command.Parameters.AddWithValue("@tax_amount_cents", plan.TaxAmountCents);
            command.Parameters.AddWithValue(
                "@total_amount_cents",
                plan.TotalAmountCents);
            command.Parameters.AddWithValue("@currency", plan.Currency);
            command.Parameters.AddWithValue(
                "@reason_code",
                "BILLING_V2_DOCUMENT_CREATED_FROM_SUBSCRIPTION_SNAPSHOT");
            command.Parameters.AddWithValue("@created_at", now);
            command.Parameters.AddWithValue("@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var line in plan.Lines)
        {
            var commercialLineId = Guid.NewGuid().ToString("D");
            await InsertCommercialLineAsync(
                connection,
                transaction,
                documentId,
                commercialLineId,
                line,
                now,
                cancellationToken);
            await InsertV2LineSnapshotAsync(
                connection,
                transaction,
                subscriptionDocumentId,
                commercialLineId,
                line,
                now,
                cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            source.Subscription.SubscriptionId,
            documentId,
            correlationId,
            cancellationToken);
        return documentId;
    }

    private static async Task InsertCommercialLineAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string documentId,
        string commercialLineId,
        BillingV2DocumentLinePlan line,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
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
                NULL,
                @label,
                @description,
                1.00,
                @unit_label,
                @unit_price_cents,
                @tax_rate_basis_points,
                @line_total_cents,
                @sort_order,
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue("@id", commercialLineId);
        command.Parameters.AddWithValue("@document_id", documentId);
        command.Parameters.AddWithValue("@label", line.Label);
        command.Parameters.AddWithValue("@description", line.Description);
        command.Parameters.AddWithValue("@unit_label", line.UnitLabel);
        command.Parameters.AddWithValue(
            "@unit_price_cents",
            checked((int)line.NetLineAmountCents));
        command.Parameters.AddWithValue(
            "@tax_rate_basis_points",
            line.TaxRateBasisPoints is null
                ? DBNull.Value
                : line.TaxRateBasisPoints);
        command.Parameters.AddWithValue(
            "@line_total_cents",
            checked((int)line.NetLineAmountCents));
        command.Parameters.AddWithValue("@sort_order", line.SortOrder);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertV2LineSnapshotAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionDocumentId,
        string commercialLineId,
        BillingV2DocumentLinePlan line,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_document_line_snapshots (
                id,
                subscription_document_id,
                commercial_document_line_id,
                subscription_item_id,
                service_price_id,
                service_code,
                tier_code,
                label,
                purchased_quantity,
                gross_unit_amount_cents,
                gross_line_amount_cents,
                discount_amount_cents,
                net_line_amount_cents,
                tax_rate_basis_points,
                tax_amount_cents,
                final_line_amount_cents,
                currency,
                created_at
            ) VALUES (
                @id,
                @subscription_document_id,
                @commercial_document_line_id,
                @subscription_item_id,
                @service_price_id,
                @service_code,
                @tier_code,
                @label,
                @purchased_quantity,
                @gross_unit_amount_cents,
                @gross_line_amount_cents,
                @discount_amount_cents,
                @net_line_amount_cents,
                @tax_rate_basis_points,
                @tax_amount_cents,
                @final_line_amount_cents,
                @currency,
                @created_at
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue(
            "@subscription_document_id",
            subscriptionDocumentId);
        command.Parameters.AddWithValue(
            "@commercial_document_line_id",
            commercialLineId);
        command.Parameters.AddWithValue(
            "@subscription_item_id",
            line.SubscriptionItemId);
        command.Parameters.AddWithValue("@service_price_id", line.ServicePriceId);
        command.Parameters.AddWithValue("@service_code", line.ServiceCode);
        command.Parameters.AddWithValue(
            "@tier_code",
            string.IsNullOrWhiteSpace(line.TierCode)
                ? DBNull.Value
                : line.TierCode);
        command.Parameters.AddWithValue("@label", line.Label);
        command.Parameters.AddWithValue("@purchased_quantity", line.Quantity);
        command.Parameters.AddWithValue(
            "@gross_unit_amount_cents",
            line.GrossUnitAmountCents);
        command.Parameters.AddWithValue(
            "@gross_line_amount_cents",
            line.GrossLineAmountCents);
        command.Parameters.AddWithValue(
            "@discount_amount_cents",
            line.DiscountAmountCents);
        command.Parameters.AddWithValue(
            "@net_line_amount_cents",
            line.NetLineAmountCents);
        command.Parameters.AddWithValue(
            "@tax_rate_basis_points",
            line.TaxRateBasisPoints is null
                ? DBNull.Value
                : line.TaxRateBasisPoints);
        command.Parameters.AddWithValue("@tax_amount_cents", line.TaxAmountCents);
        command.Parameters.AddWithValue(
            "@final_line_amount_cents",
            line.FinalLineAmountCents);
        command.Parameters.AddWithValue("@currency", line.Currency);
        command.Parameters.AddWithValue("@created_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateDocumentStatusAsync(
        string documentId,
        string status,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE billing_v2_subscription_documents
            SET status = @status,
                reason_code = @reason_code,
                updated_at = UTC_TIMESTAMP(6)
            WHERE commercial_document_id = @commercial_document_id;
            """;
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@reason_code", reasonCode);
        command.Parameters.AddWithValue(
            "@commercial_document_id",
            documentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_audit_log (
                id,
                entity_type,
                entity_id,
                action,
                actor_reference,
                details_text,
                created_at
            ) VALUES (
                @id,
                'billing_v2_subscription',
                @entity_id,
                'document_created',
                'system:billing_v2',
                @details_text,
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@entity_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@details_text",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                commercialDocumentId = documentId,
                documentKind = InitialSubscriptionDocumentKind,
                correlationId
            }));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> ResolveSystemActorAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id
            FROM portal_users
            WHERE role = 'internal_admin'
              AND status = 'active'
            ORDER BY created_at ASC
            LIMIT 1;
            """;
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull
            ? throw new InvalidOperationException(
                "No active internal_admin user available to issue Billing V2 document.")
            : Convert.ToString(scalar)!;
    }

    private static string CreateReference(string customerReference)
        => $"BV2-{DateTime.UtcNow:yyyyMMddHHmmss}-{customerReference}";

    private static string? ReadNullableString(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}

public sealed record BillingV2DocumentSubscriptionSource(
    string SubscriptionId,
    string CustomerId,
    string Status,
    string PaymentMode,
    string Currency,
    int DiscountBasisPoints,
    long? MinimumCommitmentAmountCents,
    DateTime CreatedAtUtc,
    int CommitmentMonths,
    string CustomerReference,
    string CustomerName);

public sealed record BillingV2DocumentSourceItem(
    string SubscriptionItemId,
    string ServicePriceId,
    string ServiceCode,
    string ServiceName,
    string? TierCode,
    string? TierName,
    string ServicePriceCode,
    string BillingCadence,
    int? TaxRateBasisPoints,
    int Quantity,
    long AmountCentsSnapshot,
    string Currency,
    bool DiscountEligible);

public sealed record BillingV2DocumentPriceLock(
    string LockType,
    long AmountCents,
    string Currency,
    DateTime EffectiveFromUtc,
    DateTime EffectiveUntilUtc);

public sealed record BillingV2DocumentSource(
    BillingV2DocumentSubscriptionSource Subscription,
    IReadOnlyList<BillingV2DocumentSourceItem> Items,
    BillingV2DocumentPriceLock? PriceLock);

public sealed record BillingV2DocumentPlan(
    string Title,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string Currency,
    long SubtotalAmountCents,
    long DiscountAmountCents,
    long TaxAmountCents,
    long TotalAmountCents,
    IReadOnlyList<BillingV2DocumentLinePlan> Lines);

public sealed record BillingV2DocumentLinePlan(
    string SubscriptionItemId,
    string ServicePriceId,
    string ServiceCode,
    string? TierCode,
    string Label,
    string Description,
    decimal Quantity,
    string UnitLabel,
    long GrossUnitAmountCents,
    long GrossLineAmountCents,
    long DiscountAmountCents,
    long NetLineAmountCents,
    int? TaxRateBasisPoints,
    long TaxAmountCents,
    long FinalLineAmountCents,
    string Currency,
    int SortOrder);

public static class BillingV2DocumentSnapshotPlanner
{
    private const int BasisPointDenominator = 10000;

    public static BillingV2DocumentPlan Plan(BillingV2DocumentSource source)
    {
        if (source.Items.Count == 0)
        {
            throw new InvalidOperationException(
                "Billing V2 document cannot be planned without subscription items.");
        }

        var subscription = source.Subscription;
        var periodStart = subscription.CreatedAtUtc.Date;
        var periodEnd = string.Equals(
            subscription.PaymentMode,
            BillingV2PaymentModes.Upfront,
            StringComparison.Ordinal)
            ? periodStart.AddMonths(Math.Max(1, subscription.CommitmentMonths))
            : periodStart.AddMonths(1);
        var oneTimeItems = source.Items
            .Where(item => string.Equals(
                item.BillingCadence,
                BillingV2BillingCadences.OneTime,
                StringComparison.Ordinal))
            .ToArray();
        var recurringItems = source.Items
            .Where(item => string.Equals(
                item.BillingCadence,
                BillingV2BillingCadences.Monthly,
                StringComparison.Ordinal))
            .ToArray();

        var recurringGross = recurringItems.Sum(GrossLineAmount);
        var oneTimeGross = oneTimeItems.Sum(GrossLineAmount);
        var recurringTarget = ResolveRecurringTarget(source, recurringGross);
        var recurringAllocations = Allocate(
            recurringTarget,
            recurringItems.Select(GrossLineAmount).ToArray());

        var lines = new List<BillingV2DocumentLinePlan>();
        var sortOrder = 10;
        for (var index = 0; index < recurringItems.Length; index++)
        {
            lines.Add(CreateLine(
                recurringItems[index],
                recurringAllocations[index],
                recurringMultiplier: string.Equals(
                    subscription.PaymentMode,
                    BillingV2PaymentModes.Upfront,
                    StringComparison.Ordinal)
                    ? Math.Max(1, subscription.CommitmentMonths)
                    : 1,
                periodStart,
                periodEnd,
                sortOrder));
            sortOrder += 10;
        }

        foreach (var item in oneTimeItems)
        {
            lines.Add(CreateLine(
                item,
                GrossLineAmount(item),
                recurringMultiplier: 1,
                periodStart,
                periodEnd,
                sortOrder));
            sortOrder += 10;
        }

        var subtotal = lines.Sum(line => line.NetLineAmountCents);
        var tax = lines.Sum(line => line.TaxAmountCents);
        return new BillingV2DocumentPlan(
            $"Facture abonnement Billing V2 - {subscription.CustomerName}",
            periodStart,
            periodEnd,
            subscription.Currency,
            subtotal,
            recurringGross - recurringTarget,
            tax,
            checked(subtotal + tax),
            lines);

        long ResolveRecurringTarget(
            BillingV2DocumentSource documentSource,
            long fallbackGross)
        {
            if (documentSource.PriceLock is { } priceLock)
            {
                return priceLock.AmountCents;
            }

            var discountEligibleGross = recurringItems
                .Where(item => item.DiscountEligible)
                .Sum(GrossLineAmount);
            var nonDiscountableGross = recurringItems
                .Where(item => !item.DiscountEligible)
                .Sum(GrossLineAmount);
            var discounted = ApplyBasisPoints(
                discountEligibleGross,
                BasisPointDenominator - subscription.DiscountBasisPoints)
                + nonDiscountableGross;

            if (string.Equals(
                subscription.PaymentMode,
                BillingV2PaymentModes.Upfront,
                StringComparison.Ordinal))
            {
                return checked(discounted * Math.Max(1, subscription.CommitmentMonths));
            }

            return subscription.MinimumCommitmentAmountCents.HasValue
                ? Math.Max(discounted, subscription.MinimumCommitmentAmountCents.Value)
                : discounted;
        }

        BillingV2DocumentLinePlan CreateLine(
            BillingV2DocumentSourceItem item,
            long netLineAmountCents,
            int recurringMultiplier,
            DateTime start,
            DateTime end,
            int order)
        {
            var grossLine = checked(GrossLineAmount(item) * recurringMultiplier);
            var tax = item.TaxRateBasisPoints.HasValue
                ? ApplyBasisPoints(netLineAmountCents, item.TaxRateBasisPoints.Value)
                : 0;
            var tier = string.IsNullOrWhiteSpace(item.TierName)
                ? string.Empty
                : $" - {item.TierName}";
            return new BillingV2DocumentLinePlan(
                item.SubscriptionItemId,
                item.ServicePriceId,
                item.ServiceCode,
                item.TierCode,
                $"{item.ServiceName}{tier}",
                $"Droit contractuel Billing V2 du {start:yyyy-MM-dd} au {end:yyyy-MM-dd}.",
                item.Quantity,
                item.BillingCadence == BillingV2BillingCadences.Monthly
                    ? "periode"
                    : "forfait",
                item.AmountCentsSnapshot,
                grossLine,
                grossLine - netLineAmountCents,
                netLineAmountCents,
                item.TaxRateBasisPoints,
                tax,
                checked(netLineAmountCents + tax),
                item.Currency,
                order);
        }
    }

    private static long GrossLineAmount(BillingV2DocumentSourceItem item)
        => checked(item.AmountCentsSnapshot * item.Quantity);

    private static long ApplyBasisPoints(long amountCents, int basisPoints)
        => (long)decimal.Round(
            amountCents * (basisPoints / (decimal)BasisPointDenominator),
            0,
            MidpointRounding.AwayFromZero);

    private static IReadOnlyList<long> Allocate(
        long totalCents,
        IReadOnlyList<long> weights)
    {
        if (weights.Count == 0)
        {
            return Array.Empty<long>();
        }

        var sum = weights.Sum();
        if (sum == 0)
        {
            return Enumerable.Repeat(0L, weights.Count).ToArray();
        }

        var allocations = new long[weights.Count];
        var allocated = 0L;
        for (var i = 0; i < weights.Count; i++)
        {
            if (i == weights.Count - 1)
            {
                allocations[i] = checked(totalCents - allocated);
                break;
            }

            allocations[i] = (long)decimal.Round(
                totalCents * (weights[i] / (decimal)sum),
                0,
                MidpointRounding.AwayFromZero);
            allocated = checked(allocated + allocations[i]);
        }

        return allocations;
    }
}
