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

    /// <summary>
    /// Document d'un cycle de renouvellement (Phase 3).
    ///
    /// Construit UNIQUEMENT depuis les snapshots du BillingEvent : ni
    /// catalogue, ni recalcul. Idempotent par (abonnement, cycle) et par
    /// BillingEvent - un rejeu ne peut pas produire une seconde facture.
    /// </summary>
    Task<BillingV2DocumentIssueResult> EnsureCycleInvoiceAsync(
        string subscriptionId,
        string billingEventId,
        int cycleSequence,
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

    public Task<BillingV2DocumentIssueResult> EnsureCycleInvoiceAsync(
        string subscriptionId,
        string billingEventId,
        int cycleSequence,
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

    /// <summary>Un document par cycle de renouvellement.</summary>
    public const string RenewalSubscriptionDocumentKind =
        "renewal_subscription_invoice";

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

        return await IssueAndConfirmAsync(
            documentId,
            correlationId,
            cancellationToken);
    }

    /// <summary>
    /// Emission + confirmation de paiement, partagees par la charge initiale
    /// et les cycles de renouvellement. Un seul chemin, donc une seule
    /// politique d'idempotence BPCE.
    /// </summary>
    private async Task<BillingV2DocumentIssueResult> IssueAndConfirmAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        // Phase 2.5. L'intention d'emission est persistee AVANT tout appel
        // reseau BPCE, avec une reference stable derivee du document. Sans
        // cela, un succes BPCE suivi d'un timeout avant l'ecriture locale
        // laissait le retry recreer une facture -- et valider un second numero
        // fiscal.
        var issuanceGate = await EnsureIssuanceIntentAsync(
            documentId,
            cancellationToken);
        if (!issuanceGate.CanCallProvider)
        {
            _logger.LogWarning(
                "Billing V2 document {DocumentId} issuance blocked: {ReasonCode}. Manual review required: {Manual}.",
                documentId,
                issuanceGate.ReasonCode,
                issuanceGate.RequiresManualReview);
            return new BillingV2DocumentIssueResult(
                false,
                issuanceGate.ReasonCode,
                documentId,
                Invoice: null);
        }

        var issue = await _invoiceIssuing.IssueInvoiceAsync(
            documentId,
            sendEmail: false,
            correlationId,
            cancellationToken);
        await ResolveIssuanceIntentAsync(
            documentId,
            issue.Succeeded
            || string.Equals(
                issue.Code,
                "INVOICE_ALREADY_ISSUED",
                StringComparison.Ordinal),
            issue.Code,
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

    public async Task<BillingV2DocumentIssueResult> EnsureCycleInvoiceAsync(
        string subscriptionId,
        string billingEventId,
        int cycleSequence,
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

            // Idempotence : un document deja rattache a CE BillingEvent est le
            // document du cycle. On ne recree rien.
            var existing = await ReadCycleDocumentIdAsync(
                connection,
                transaction,
                billingEventId,
                cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                documentId = existing;
            }
            else
            {
                var source = await LoadCycleSourceAsync(
                    connection,
                    transaction,
                    billingEventId,
                    cancellationToken);
                if (source is null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new BillingV2DocumentIssueResult(
                        false,
                        "BILLING_V2_DOCUMENT_BILLING_EVENT_NOT_FOUND",
                        CommercialDocumentId: null,
                        Invoice: null);
                }

                // Le document n'est emis qu'apres un encaissement PROUVE.
                if (!string.Equals(
                        source.SettlementStatus,
                        BillingV2SettlementStatuses.Settled,
                        StringComparison.Ordinal))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new BillingV2DocumentIssueResult(
                        false,
                        "BILLING_V2_DOCUMENT_CYCLE_NOT_SETTLED",
                        CommercialDocumentId: null,
                        Invoice: null);
                }

                try
                {
                    documentId = await CreateCycleDocumentAsync(
                        connection,
                        transaction,
                        subscriptionId,
                        cycleSequence,
                        source,
                        correlationId,
                        cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (MySqlException exception) when (exception.Number == 1062)
                {
                    // Course perdue avec un autre rejeu : l'unicite sur le
                    // BillingEvent a fait son travail. On reprend le document
                    // du gagnant plutot que d'en produire un second.
                    await transaction.RollbackAsync(cancellationToken);
                    var winner = await ReadCycleDocumentIdAsync(
                        connection,
                        transaction: null,
                        billingEventId,
                        cancellationToken);
                    if (winner is null)
                    {
                        throw;
                    }

                    documentId = winner;
                }
            }
        }

        return await IssueAndConfirmAsync(
            documentId,
            correlationId,
            cancellationToken);
    }

    private static async Task<string?> ReadCycleDocumentIdAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string billingEventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT commercial_document_id
            FROM billing_v2_subscription_documents
            WHERE billing_event_id = @billing_event_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@billing_event_id", billingEventId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar);
    }

    private sealed record CycleDocumentLine(
        string ServiceCode,
        string? TierCode,
        string Description,
        int Quantity,
        long UnitAmountCents,
        long GrossAmountCents,
        long DiscountAmountCents,
        long NetAmountCents,
        long TaxAmountCents,
        long TotalAmountCents,
        string Currency,
        string ServicePriceId,
        int DisplayOrder);

    private sealed record CycleDocumentSource(
        string BillingEventId,
        string SubscriptionId,
        string CustomerId,
        string CustomerReference,
        string CustomerName,
        string SettlementStatus,
        string Currency,
        DateTime PeriodStartUtc,
        DateTime PeriodEndUtc,
        long DiscountAmountCents,
        long TaxAmountCents,
        long TotalAmountCents,
        IReadOnlyList<CycleDocumentLine> Lines);

    /// <summary>
    /// Lit le document a produire DEPUIS le BillingEvent et ses lignes.
    /// Aucun montant n'est recalcule : la facture reproduit exactement ce qui
    /// a ete finalise puis encaisse.
    /// </summary>
    private static async Task<CycleDocumentSource?> LoadCycleSourceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string billingEventId,
        CancellationToken cancellationToken)
    {
        CycleDocumentSource? source;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT
                    event_row.id,
                    event_row.subscription_id,
                    event_row.customer_id,
                    event_row.settlement_status,
                    event_row.currency,
                    event_row.period_start,
                    event_row.period_end,
                    event_row.discount_amount_cents,
                    event_row.tax_amount_cents,
                    event_row.total_amount_cents,
                    customer.external_reference,
                    customer.display_name
                FROM billing_v2_billing_events event_row
                INNER JOIN customers customer
                    ON customer.id = event_row.customer_id
                WHERE event_row.id = @billing_event_id
                FOR UPDATE;
                """;
            command.Parameters.AddWithValue("@billing_event_id", billingEventId);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            source = new CycleDocumentSource(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                MariaDbIdentifierReader.ReadRequired(reader, "subscription_id"),
                MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
                reader.GetString("external_reference"),
                reader.GetString("display_name"),
                reader.GetString("settlement_status"),
                reader.GetString("currency"),
                DateTime.SpecifyKind(
                    reader.GetDateTime("period_start"),
                    DateTimeKind.Utc),
                DateTime.SpecifyKind(
                    reader.GetDateTime("period_end"),
                    DateTimeKind.Utc),
                reader.GetInt64("discount_amount_cents"),
                reader.GetInt64("tax_amount_cents"),
                reader.GetInt64("total_amount_cents"),
                Array.Empty<CycleDocumentLine>());
        }

        var lines = new List<CycleDocumentLine>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT service_code, tier_code, description, quantity,
                       unit_amount_cents, gross_amount_cents,
                       discount_allocated_amount_cents, net_amount_cents,
                       tax_amount_cents, total_amount_cents, currency,
                       service_price_id, display_order
                FROM billing_v2_billing_event_lines
                WHERE billing_event_id = @billing_event_id
                ORDER BY display_order;
                """;
            command.Parameters.AddWithValue("@billing_event_id", billingEventId);
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(new CycleDocumentLine(
                    reader.GetString("service_code"),
                    reader.IsDBNull(reader.GetOrdinal("tier_code"))
                        ? null
                        : reader.GetString("tier_code"),
                    reader.GetString("description"),
                    reader.GetInt32("quantity"),
                    reader.GetInt64("unit_amount_cents"),
                    reader.GetInt64("gross_amount_cents"),
                    reader.GetInt64("discount_allocated_amount_cents"),
                    reader.GetInt64("net_amount_cents"),
                    reader.GetInt64("tax_amount_cents"),
                    reader.GetInt64("total_amount_cents"),
                    reader.GetString("currency"),
                    MariaDbIdentifierReader.ReadRequired(
                        reader,
                        "service_price_id"),
                    reader.GetInt32("display_order")));
            }
        }

        return lines.Count == 0 ? null : source with { Lines = lines };
    }

    private static async Task<string> CreateCycleDocumentAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        int cycleSequence,
        CycleDocumentSource source,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var documentId = Guid.NewGuid().ToString("D");
        var subscriptionDocumentId = Guid.NewGuid().ToString("D");
        var systemActorId = await ResolveSystemActorAsync(
            connection,
            transaction,
            cancellationToken);
        var now = DateTime.UtcNow;
        var periodStart = BillingV2BillingCalendar
            .CivilDate(source.PeriodStartUtc)
            .ToDateTime(TimeOnly.MinValue);
        var periodEnd = BillingV2BillingCalendar
            .CivilDate(source.PeriodEndUtc)
            .ToDateTime(TimeOnly.MinValue);
        var subtotal = source.Lines.Sum(line => line.NetAmountCents);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO commercial_documents (
                    id, customer_id, service_request_id, subscription_id,
                    origin, document_type, status, title, internal_reference,
                    currency, subtotal_amount_cents, tax_amount_cents,
                    total_amount_cents, disclaimer, created_by_user_id,
                    created_at, updated_at, shared_at, cancelled_at
                ) VALUES (
                    @id, @customer_id, NULL, NULL,
                    @origin, 'informational_invoice', 'shared_with_customer',
                    @title, @reference,
                    @currency, @subtotal, @tax,
                    @total, @disclaimer, @created_by_user_id,
                    @now, @now, @now, NULL
                );
                """;
            command.Parameters.AddWithValue("@id", documentId);
            command.Parameters.AddWithValue("@customer_id", source.CustomerId);
            command.Parameters.AddWithValue("@origin", CommercialDocumentOrigin);
            command.Parameters.AddWithValue(
                "@title",
                $"Facture de renouvellement Billing V2 (cycle {cycleSequence}) - {source.CustomerName}");
            command.Parameters.AddWithValue(
                "@reference",
                $"BV2-C{cycleSequence:D3}-{now:yyyyMMddHHmmss}-{source.CustomerReference}");
            command.Parameters.AddWithValue("@currency", source.Currency);
            command.Parameters.AddWithValue(
                "@subtotal",
                checked((int)subtotal));
            command.Parameters.AddWithValue(
                "@tax",
                checked((int)source.TaxAmountCents));
            command.Parameters.AddWithValue(
                "@total",
                checked((int)source.TotalAmountCents));
            command.Parameters.AddWithValue(
                "@disclaimer",
                CommercialStatuses.DefaultDisclaimer);
            command.Parameters.AddWithValue(
                "@created_by_user_id",
                systemActorId);
            command.Parameters.AddWithValue("@now", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO billing_v2_subscription_documents (
                    id, subscription_id, commercial_document_id,
                    billing_event_id, document_kind, cycle_sequence,
                    period_start, period_end,
                    subtotal_amount_cents, discount_amount_cents,
                    tax_amount_cents, total_amount_cents, currency,
                    status, reason_code, created_at, updated_at
                ) VALUES (
                    @id, @subscription_id, @commercial_document_id,
                    @billing_event_id, @document_kind, @cycle_sequence,
                    @period_start, @period_end,
                    @subtotal, @discount, @tax, @total, @currency,
                    'created', @reason_code, @now, @now
                );
                """;
            command.Parameters.AddWithValue("@id", subscriptionDocumentId);
            command.Parameters.AddWithValue(
                "@subscription_id",
                subscriptionId);
            command.Parameters.AddWithValue(
                "@commercial_document_id",
                documentId);
            command.Parameters.AddWithValue(
                "@billing_event_id",
                source.BillingEventId);
            command.Parameters.AddWithValue(
                "@document_kind",
                RenewalSubscriptionDocumentKind);
            command.Parameters.AddWithValue("@cycle_sequence", cycleSequence);
            command.Parameters.AddWithValue("@period_start", periodStart);
            command.Parameters.AddWithValue("@period_end", periodEnd);
            command.Parameters.AddWithValue("@subtotal", subtotal);
            command.Parameters.AddWithValue(
                "@discount",
                source.DiscountAmountCents);
            command.Parameters.AddWithValue("@tax", source.TaxAmountCents);
            command.Parameters.AddWithValue("@total", source.TotalAmountCents);
            command.Parameters.AddWithValue("@currency", source.Currency);
            command.Parameters.AddWithValue(
                "@reason_code",
                "BILLING_V2_DOCUMENT_CREATED_FROM_BILLING_EVENT_SNAPSHOT");
            command.Parameters.AddWithValue("@now", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var line in source.Lines)
        {
            var commercialLineId = Guid.NewGuid().ToString("D");
            await InsertCommercialLineAsync(
                connection,
                transaction,
                documentId,
                commercialLineId,
                new BillingV2DocumentLinePlan(
                    SubscriptionItemId: string.Empty,
                    line.ServicePriceId,
                    line.ServiceCode,
                    line.TierCode,
                    line.TierCode is null
                        ? line.ServiceCode
                        : $"{line.ServiceCode} {line.TierCode}",
                    line.Description,
                    line.Quantity,
                    "periode",
                    line.UnitAmountCents,
                    line.GrossAmountCents,
                    line.DiscountAmountCents,
                    line.NetAmountCents,
                    TaxRateBasisPoints: null,
                    line.TaxAmountCents,
                    line.TotalAmountCents,
                    line.Currency,
                    (line.DisplayOrder + 1) * 10),
                now,
                cancellationToken);
        }

        await InsertAuditAsync(
            connection,
            transaction,
            subscriptionId,
            documentId,
            correlationId,
            cancellationToken);
        return documentId;
    }

    /// <summary>
    /// Cree ou reprend l'intention d'emission, et decide si l'appel BPCE est
    /// autorise. Un appel dont le sort est indetermine laisse l'intention en
    /// `in_flight` : la reprise passera alors par
    /// <see cref="BillingV2DocumentIssuancePolicy.ResolveIndeterminate"/>, qui
    /// echoue en ferme tant que l'API BPCE ne sait pas rechercher une facture
    /// par reference externe.
    /// </summary>
    private async Task<BillingV2DocumentIssuanceDecision>
        EnsureIssuanceIntentAsync(
            string documentId,
            CancellationToken cancellationToken)
    {
        var reference = BillingV2DocumentIssuancePolicy
            .BuildExternalReference(documentId);
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                """
                INSERT IGNORE INTO billing_v2_document_issuance_attempts (
                    id, commercial_document_id, billing_event_id,
                    external_reference,
                    status, attempt_count, created_at, updated_at
                )
                SELECT
                    @id,
                    @document_id,
                    -- Rattachement explicite au BillingEvent du document :
                    -- une emission doit etre tracable jusqu'a la charge
                    -- qu'elle materialise, cycle initial compris.
                    (
                        SELECT doc.billing_event_id
                        FROM billing_v2_subscription_documents doc
                        WHERE doc.commercial_document_id = @document_id
                        LIMIT 1
                    ),
                    @reference,
                    'created', 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6);
                """;
            insert.Parameters.AddWithValue(
                "@id",
                Guid.NewGuid().ToString("D"));
            insert.Parameters.AddWithValue("@document_id", documentId);
            insert.Parameters.AddWithValue("@reference", reference);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        var attempt = await ReadIssuanceAttemptAsync(
            connection,
            documentId,
            cancellationToken);
        var decision = BillingV2DocumentIssuancePolicy.Evaluate(attempt);

        // Une intention laissee `in_flight` par un appel precedent signifie
        // que l'on ignore si BPCE a cree la facture. On ne recree pas.
        if (decision.CanCallProvider
            && attempt is not null
            && string.Equals(
                attempt.Status,
                BillingV2DocumentIssuanceStatuses.InFlight,
                StringComparison.Ordinal))
        {
            var indeterminate = BillingV2DocumentIssuancePolicy
                .ResolveIndeterminate(
                    BillingV2DocumentIssuancePolicy
                        .InvoiceLookupByExternalReferenceSupported,
                    lookupFoundExistingInvoice: false);
            if (!indeterminate.CanCallProvider)
            {
                await MarkIssuanceAsync(
                    connection,
                    documentId,
                    BillingV2DocumentIssuanceStatuses.ReconciliationRequired,
                    indeterminate.ReasonCode,
                    cancellationToken);
                return indeterminate;
            }
        }

        if (!decision.CanCallProvider)
        {
            return decision;
        }

        await MarkIssuanceAsync(
            connection,
            documentId,
            BillingV2DocumentIssuanceStatuses.InFlight,
            reasonCode: null,
            cancellationToken,
            incrementAttempt: true);
        return decision;
    }

    private async Task ResolveIssuanceIntentAsync(
        string documentId,
        bool succeeded,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await MarkIssuanceAsync(
            connection,
            documentId,
            succeeded
                ? BillingV2DocumentIssuanceStatuses.Succeeded
                : BillingV2DocumentIssuanceStatuses.Failed,
            reasonCode,
            cancellationToken);
    }

    private static async Task<BillingV2DocumentIssuanceAttempt?>
        ReadIssuanceAttemptAsync(
            MySqlConnection connection,
            string documentId,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, commercial_document_id, external_reference,
                   status, provider_invoice_id, attempt_count
            FROM billing_v2_document_issuance_attempts
            WHERE commercial_document_id = @document_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@document_id", documentId);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2DocumentIssuanceAttempt(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(
                reader,
                "commercial_document_id"),
            reader.GetString("external_reference"),
            reader.GetString("status"),
            reader.IsDBNull(reader.GetOrdinal("provider_invoice_id"))
                ? null
                : reader.GetString("provider_invoice_id"),
            reader.GetInt32("attempt_count"));
    }

    private static async Task MarkIssuanceAsync(
        MySqlConnection connection,
        string documentId,
        string status,
        string? reasonCode,
        CancellationToken cancellationToken,
        bool incrementAttempt = false)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             UPDATE billing_v2_document_issuance_attempts
             SET status = @status,
                 reason_code = COALESCE(@reason_code, reason_code),
                 attempt_count = attempt_count
                     + {(incrementAttempt ? "1" : "0")},
                 attempted_at = CASE
                     WHEN @status = 'in_flight' THEN UTC_TIMESTAMP(6)
                     ELSE attempted_at
                 END,
                 resolved_at = CASE
                     WHEN @status IN ('succeeded', 'failed',
                                      'reconciliation_required')
                         THEN UTC_TIMESTAMP(6)
                     ELSE resolved_at
                 END,
                 updated_at = UTC_TIMESTAMP(6)
             WHERE commercial_document_id = @document_id;
             """;
        command.Parameters.AddWithValue("@document_id", documentId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue(
            "@reason_code",
            reasonCode is null ? DBNull.Value : reasonCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
                    billing_event_id,
                    cycle_sequence,
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
                )
                SELECT
                    @id,
                    @subscription_id,
                    @commercial_document_id,
                    -- Le document initial est rattache a SON BillingEvent et
                    -- porte explicitement le cycle 1 : sans cela l'unicite par
                    -- cycle reposait sur un NULL, que MariaDB laisse passer en
                    -- plusieurs exemplaires dans un index UNIQUE.
                    (
                        SELECT initial_event.id
                        FROM billing_v2_billing_events initial_event
                        WHERE initial_event.subscription_id = @subscription_id
                          AND initial_event.cycle_sequence = 1
                        ORDER BY initial_event.created_at
                        LIMIT 1
                    ),
                    1,
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
                    @updated_at;
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

        // L'axe documentaire du BillingEvent doit suivre l'etat reel du
        // document. Sans cette propagation il restait a `none` meme apres
        // emission, y compris pour un renouvellement.
        await using var eventCommand = connection.CreateCommand();
        eventCommand.CommandText =
            """
            UPDATE billing_v2_billing_events event_row
            INNER JOIN billing_v2_subscription_documents doc
                ON doc.billing_event_id = event_row.id
            SET event_row.document_status = @document_status
            WHERE doc.commercial_document_id = @commercial_document_id;
            """;
        eventCommand.Parameters.AddWithValue(
            "@document_status",
            status switch
            {
                "issued" or "paid" => "issued",
                "failed" => "failed",
                _ => "pending"
            });
        eventCommand.Parameters.AddWithValue(
            "@commercial_document_id",
            documentId);
        await eventCommand.ExecuteNonQueryAsync(cancellationToken);
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
        // Jour civil Paris, pas `.Date` sur l'instant UTC : un abonnement cree
        // apres minuit Paris mais avant minuit UTC aurait sinon une periode
        // datee de la veille, alors que la facture BPCE porte le jour Paris.
        var contractPeriod = BillingV2BillingCalendar.ResolvePeriod(
            subscription.CreatedAtUtc,
            subscription.PaymentMode,
            subscription.CommitmentMonths);
        var periodStart = contractPeriod.CivilStart.ToDateTime(TimeOnly.MinValue);
        var periodEnd = contractPeriod.CivilEnd.ToDateTime(TimeOnly.MinValue);
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
