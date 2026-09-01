using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;
using System.Data;
using System.Net;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2VpsTechnicalReviewSummary(
    string TechnicalRequestId,
    string CustomerReference,
    string CustomerName,
    string ServiceCode,
    string TierCode,
    int RevisionNumber,
    string Hostname,
    string OperatingSystem,
    string Usage,
    string ManagementMode,
    string InternetExposure,
    string Comment,
    string SettlementStatus,
    string TechnicalStatus,
    string ProvisioningStatus,
    string? InfrastructureTarget,
    string? InstanceReference,
    string? PublicIpAddress,
    string? OperationalNotes,
    DateTime CreatedAt,
    DateTime? SettledAt,
    DateTime? TechnicalReviewPendingAt,
    string? ApprovalType,
    DateTime? ApprovedAt,
    DateTime? ProvisioningStartedAt,
    DateTime? ActivatedAt,
    bool ReadyToProvision);

public sealed record BillingV2VpsTechnicalRequestStatus(
    string TechnicalRequestId,
    string SettlementStatus,
    string TechnicalStatus,
    string ProvisioningStatus,
    DateTime UpdatedAt);

public sealed record BillingV2VpsManualProvisioningInput(
    string? InfrastructureTarget,
    string? InstanceReference,
    string? PublicIpAddress,
    string? OperationalNotes);

public interface IBillingV2VpsTechnicalReviewService
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<BillingV2VpsTechnicalReviewSummary>>
        GetAdminReviewsAsync(CancellationToken cancellationToken);

    Task<BillingV2VpsTechnicalRequestStatus?> GetClientStatusAsync(
        PortalSessionContext session,
        string technicalRequestId,
        CancellationToken cancellationToken);

    Task<BillingV2VpsTechnicalReviewSummary> ApproveAsync(
        string technicalRequestId,
        string approvedByUserId,
        CancellationToken cancellationToken);

    Task<BillingV2VpsTechnicalReviewSummary> StartManualProvisioningAsync(
        string technicalRequestId,
        BillingV2VpsManualProvisioningInput input,
        string startedByUserId,
        CancellationToken cancellationToken);

    Task<BillingV2VpsTechnicalReviewSummary> MarkProvisioningActiveAsync(
        string technicalRequestId,
        string activatedByUserId,
        CancellationToken cancellationToken);

    Task<bool> IsVpsTechnicalSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Revue humaine des commandes VPS reglees. Le passage en attente est effectue
/// dans la transaction du settlement, via <see cref="BillingV2VpsTechnicalReviewSettlement"/>.
/// Cette surface ne cree aucun paiement et ne declenche aucun provisioning.
/// </summary>
public sealed class BillingV2VpsTechnicalReviewService
    : IBillingV2VpsTechnicalReviewService
{
    private const string PendingReview = "pending_review";
    private const string Approved = "approved";
    private const string Settled = "settled";
    private const string ProvisioningPending = "pending";
    private const string ProvisioningInProgress = "provisioning";
    private readonly SqlRuntimeConfiguration _sql;

    public BillingV2VpsTechnicalReviewService(SqlRuntimeConfiguration sql)
    {
        _sql = sql;
    }

    public bool IsPersistent => _sql.IsPersistent;

    public async Task<IReadOnlyList<BillingV2VpsTechnicalReviewSummary>>
        GetAdminReviewsAsync(CancellationToken cancellationToken)
    {
        if (!IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return Array.Empty<BillingV2VpsTechnicalReviewSummary>();
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureProvisioningSchemaReadyAsync(connection, cancellationToken);
        var rows = new List<BillingV2VpsTechnicalReviewSummary>();
        await using var command = connection.CreateCommand();
        command.CommandText = AdminSelectSql;
        command.Parameters.AddWithValue("@technical_request_id", DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(ReadSummary(reader));
        }

        return rows;
    }

    public async Task<BillingV2VpsTechnicalRequestStatus?> GetClientStatusAsync(
        PortalSessionContext session,
        string technicalRequestId,
        CancellationToken cancellationToken)
    {
        if (!IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return null;
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureProvisioningSchemaReadyAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT request_row.id,
                   COALESCE(event_row.settlement_status, 'pending') AS settlement_status,
                   request_row.technical_status,
                   request_row.provisioning_status,
                   request_row.updated_at
            FROM billing_v2_vps_technical_requests request_row
            LEFT JOIN billing_v2_vps_technical_request_checkouts checkout_link
                ON checkout_link.technical_request_id = request_row.id
               AND checkout_link.technical_request_revision_number = request_row.current_revision
            LEFT JOIN billing_v2_billing_events event_row
                ON event_row.id = checkout_link.billing_event_id
            WHERE request_row.id = @technical_request_id
              AND request_row.customer_id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@technical_request_id", technicalRequestId);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2VpsTechnicalRequestStatus(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            reader.GetString("settlement_status"),
            reader.GetString("technical_status"),
            reader.GetString("provisioning_status"),
            reader.GetDateTime("updated_at"));
    }

    public async Task<BillingV2VpsTechnicalReviewSummary> ApproveAsync(
        string technicalRequestId,
        string approvedByUserId,
        CancellationToken cancellationToken)
    {
        EnsurePersistent();
        await using var connection = new MySqlConnection(_sql.ConnectionString!);
        await connection.OpenAsync(cancellationToken);
        await EnsureProvisioningSchemaReadyAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var state = await ReadApprovalStateForUpdateAsync(
            connection,
            transaction,
            technicalRequestId,
            cancellationToken);
        if (state is null)
        {
            throw new PortalDataNotFoundException();
        }
        if (!string.Equals(state.SettlementStatus, Settled, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "BILLING_V2_VPS_TECHNICAL_APPROVAL_PAYMENT_NOT_SETTLED");
        }
        if (!string.Equals(state.TechnicalStatus, PendingReview, StringComparison.Ordinal)
            && !string.Equals(state.TechnicalStatus, Approved, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "BILLING_V2_VPS_TECHNICAL_APPROVAL_STATE_INVALID");
        }

        if (string.Equals(state.TechnicalStatus, PendingReview, StringComparison.Ordinal))
        {
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE billing_v2_vps_technical_requests
                SET technical_status = 'approved',
                    approval_type = 'human',
                    approved_at = @approved_at,
                    approved_by_user_id = @approved_by_user_id,
                    updated_at = @approved_at
                WHERE id = @technical_request_id
                  AND technical_status = 'pending_review';
                """;
            update.Parameters.AddWithValue("@technical_request_id", technicalRequestId);
            update.Parameters.AddWithValue("@approved_at", DateTime.UtcNow);
            update.Parameters.AddWithValue("@approved_by_user_id", approvedByUserId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException(
                    "BILLING_V2_VPS_TECHNICAL_APPROVAL_CONFLICT");
            }
        }

        var result = await ReadAdminSummaryAsync(
            connection,
            transaction,
            technicalRequestId,
            cancellationToken)
            ?? throw new PortalDataNotFoundException();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<BillingV2VpsTechnicalReviewSummary> StartManualProvisioningAsync(
        string technicalRequestId,
        BillingV2VpsManualProvisioningInput input,
        string startedByUserId,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeManualProvisioningInput(input);
        EnsurePersistent();
        await using var connection = new MySqlConnection(_sql.ConnectionString!);
        await connection.OpenAsync(cancellationToken);
        await EnsureProvisioningSchemaReadyAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var state = await ReadProvisioningStateForUpdateAsync(
            connection, transaction, technicalRequestId, cancellationToken);
        EnsureProvisioningPrerequisites(state, ProvisioningPending, "START");

        var now = DateTime.UtcNow;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE billing_v2_vps_technical_requests
                SET provisioning_status = 'provisioning',
                    infrastructure_target = @infrastructure_target,
                    instance_reference = @instance_reference,
                    public_ip_address = @public_ip_address,
                    operational_notes = @operational_notes,
                    provisioning_started_at = @now,
                    provisioning_started_by_user_id = @started_by_user_id,
                    updated_at = @now
                WHERE id = @technical_request_id
                  AND provisioning_status = 'pending';
                """;
            update.Parameters.AddWithValue("@technical_request_id", technicalRequestId);
            update.Parameters.AddWithValue("@infrastructure_target", normalized.InfrastructureTarget);
            update.Parameters.AddWithValue("@instance_reference", normalized.InstanceReference);
            update.Parameters.AddWithValue("@public_ip_address", DbNullable(normalized.PublicIpAddress));
            update.Parameters.AddWithValue("@operational_notes", DbNullable(normalized.OperationalNotes));
            update.Parameters.AddWithValue("@started_by_user_id", startedByUserId);
            update.Parameters.AddWithValue("@now", now);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException("BILLING_V2_VPS_MANUAL_PROVISIONING_START_CONFLICT");
            }
        }

        var result = await ReadAdminSummaryAsync(connection, transaction, technicalRequestId, cancellationToken)
            ?? throw new PortalDataNotFoundException();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<BillingV2VpsTechnicalReviewSummary> MarkProvisioningActiveAsync(
        string technicalRequestId,
        string activatedByUserId,
        CancellationToken cancellationToken)
    {
        EnsurePersistent();
        await using var connection = new MySqlConnection(_sql.ConnectionString!);
        await connection.OpenAsync(cancellationToken);
        await EnsureProvisioningSchemaReadyAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var state = await ReadProvisioningStateForUpdateAsync(
            connection, transaction, technicalRequestId, cancellationToken);
        EnsureProvisioningPrerequisites(state, ProvisioningInProgress, "ACTIVATE");

        var now = DateTime.UtcNow;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE billing_v2_vps_technical_requests
                SET provisioning_status = 'active',
                    activated_at = @now,
                    activated_by_user_id = @activated_by_user_id,
                    updated_at = @now
                WHERE id = @technical_request_id
                  AND provisioning_status = 'provisioning';
                """;
            update.Parameters.AddWithValue("@technical_request_id", technicalRequestId);
            update.Parameters.AddWithValue("@activated_by_user_id", activatedByUserId);
            update.Parameters.AddWithValue("@now", now);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException("BILLING_V2_VPS_MANUAL_PROVISIONING_ACTIVATE_CONFLICT");
            }
        }

        var result = await ReadAdminSummaryAsync(connection, transaction, technicalRequestId, cancellationToken)
            ?? throw new PortalDataNotFoundException();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<bool> IsVpsTechnicalSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        if (!IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString)
            || string.IsNullOrWhiteSpace(subscriptionId))
        {
            return false;
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT EXISTS(
                SELECT 1
                FROM billing_v2_vps_technical_request_checkouts
                WHERE subscription_id = @subscription_id
            );
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private void EnsurePersistent()
    {
        if (!IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            throw new InvalidOperationException(
                "BILLING_V2_VPS_TECHNICAL_REVIEW_STORAGE_UNAVAILABLE");
        }
    }

    private static async Task EnsureProvisioningSchemaReadyAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'billing_v2_vps_technical_requests'
              AND column_name IN (
                  'provisioning_status', 'infrastructure_target', 'instance_reference',
                  'public_ip_address', 'operational_notes', 'provisioning_started_at',
                  'provisioning_started_by_user_id', 'activated_at', 'activated_by_user_id');
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 9)
        {
            throw new InvalidOperationException("BILLING_V2_VPS_MANUAL_PROVISIONING_SCHEMA_UNAVAILABLE");
        }
    }

    private static async Task<ApprovalState?> ReadApprovalStateForUpdateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string technicalRequestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT request_row.technical_status, event_row.settlement_status,
                   request_row.provisioning_status
            FROM billing_v2_vps_technical_requests request_row
            INNER JOIN billing_v2_vps_technical_request_checkouts checkout_link
                ON checkout_link.technical_request_id = request_row.id
               AND checkout_link.technical_request_revision_number = request_row.current_revision
            INNER JOIN billing_v2_billing_events event_row
                ON event_row.id = checkout_link.billing_event_id
            WHERE request_row.id = @technical_request_id
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("@technical_request_id", technicalRequestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ApprovalState(
                reader.GetString("technical_status"),
                reader.GetString("settlement_status"),
                reader.GetString("provisioning_status"))
            : null;
    }

    private static async Task<ProvisioningState?> ReadProvisioningStateForUpdateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string technicalRequestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT request_row.technical_status,
                   request_row.provisioning_status,
                   event_row.settlement_status
            FROM billing_v2_vps_technical_requests request_row
            INNER JOIN billing_v2_vps_technical_request_checkouts checkout_link
                ON checkout_link.technical_request_id = request_row.id
               AND checkout_link.technical_request_revision_number = request_row.current_revision
            INNER JOIN billing_v2_billing_events event_row
                ON event_row.id = checkout_link.billing_event_id
            WHERE request_row.id = @technical_request_id
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("@technical_request_id", technicalRequestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProvisioningState(
                reader.GetString("technical_status"),
                reader.GetString("provisioning_status"),
                reader.GetString("settlement_status"))
            : null;
    }

    private static async Task<BillingV2VpsTechnicalReviewSummary?> ReadAdminSummaryAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string technicalRequestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = AdminSelectSql;
        command.Parameters.AddWithValue("@technical_request_id", technicalRequestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSummary(reader) : null;
    }

    private static BillingV2VpsTechnicalReviewSummary ReadSummary(MySqlDataReader reader)
        => new(
            MariaDbIdentifierReader.ReadRequired(reader, "technical_request_id"),
            reader.GetString("customer_reference"),
            reader.GetString("customer_name"),
            reader.GetString("service_code"),
            reader.GetString("tier_code"),
            reader.GetInt32("revision_number"),
            reader.GetString("hostname"),
            reader.GetString("operating_system"),
            reader.GetString("usage_description"),
            reader.GetString("management_mode"),
            reader.GetString("internet_exposure"),
            reader.GetString("comment_text"),
            reader.GetString("settlement_status"),
            reader.GetString("technical_status"),
            reader.GetString("provisioning_status"),
            ReadNullableString(reader, "infrastructure_target"),
            ReadNullableString(reader, "instance_reference"),
            ReadNullableString(reader, "public_ip_address"),
            ReadNullableString(reader, "operational_notes"),
            reader.GetDateTime("created_at"),
            reader.IsDBNull(reader.GetOrdinal("settled_at"))
                ? null : reader.GetDateTime("settled_at"),
            reader.IsDBNull(reader.GetOrdinal("technical_review_pending_at"))
                ? null : reader.GetDateTime("technical_review_pending_at"),
            reader.IsDBNull(reader.GetOrdinal("approval_type"))
                ? null : reader.GetString("approval_type"),
            reader.IsDBNull(reader.GetOrdinal("approved_at"))
                ? null : reader.GetDateTime("approved_at"),
            ReadNullableDateTime(reader, "provisioning_started_at"),
            ReadNullableDateTime(reader, "activated_at"),
            string.Equals(reader.GetString("technical_status"), Approved, StringComparison.Ordinal)
                && string.Equals(reader.GetString("provisioning_status"), ProvisioningPending, StringComparison.Ordinal));

    private const string AdminSelectSql =
        """
        SELECT request_row.id AS technical_request_id,
               customer.external_reference AS customer_reference,
               customer.display_name AS customer_name,
               request_row.service_code,
               request_row.tier_code,
               checkout_link.technical_request_revision_number AS revision_number,
               revision.hostname,
               revision.operating_system,
               revision.usage_description,
               revision.management_mode,
               revision.internet_exposure,
               revision.comment_text,
               event_row.settlement_status,
               request_row.technical_status,
               request_row.provisioning_status,
               request_row.infrastructure_target,
               request_row.instance_reference,
               request_row.public_ip_address,
               request_row.operational_notes,
               request_row.created_at,
               event_row.settled_at,
               request_row.technical_review_pending_at,
               request_row.approval_type,
               request_row.approved_at,
               request_row.provisioning_started_at,
               request_row.activated_at
        FROM billing_v2_vps_technical_requests request_row
        INNER JOIN customers customer
            ON customer.id = request_row.customer_id
        INNER JOIN billing_v2_vps_technical_request_checkouts checkout_link
            ON checkout_link.technical_request_id = request_row.id
           AND checkout_link.technical_request_revision_number = request_row.current_revision
        INNER JOIN billing_v2_billing_events event_row
            ON event_row.id = checkout_link.billing_event_id
        INNER JOIN billing_v2_vps_technical_request_revisions revision
            ON revision.technical_request_id = request_row.id
           AND revision.revision_number = checkout_link.technical_request_revision_number
        WHERE event_row.settlement_status = 'settled'
          AND request_row.technical_status IN ('pending_review', 'approved')
          AND (
              @technical_request_id IS NULL
              OR request_row.id = @technical_request_id
          )
        ORDER BY CASE request_row.technical_status
                    WHEN 'pending_review' THEN 0
                    ELSE 1
                 END,
                 request_row.technical_review_pending_at ASC,
                 request_row.id ASC
        """;

    private static void EnsureProvisioningPrerequisites(
        ProvisioningState? state,
        string requiredProvisioningStatus,
        string action)
    {
        if (state is null)
        {
            throw new PortalDataNotFoundException();
        }
        if (!string.Equals(state.SettlementStatus, Settled, StringComparison.Ordinal)
            || !string.Equals(state.TechnicalStatus, Approved, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"BILLING_V2_VPS_MANUAL_PROVISIONING_{action}_NOT_AUTHORIZED");
        }
        if (!string.Equals(state.ProvisioningStatus, requiredProvisioningStatus, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"BILLING_V2_VPS_MANUAL_PROVISIONING_{action}_STATE_INVALID");
        }
    }

    private static ManualProvisioningInput NormalizeManualProvisioningInput(
        BillingV2VpsManualProvisioningInput input)
    {
        var normalized = new ManualProvisioningInput(
            Trim(input.InfrastructureTarget),
            Trim(input.InstanceReference),
            Trim(input.PublicIpAddress),
            Trim(input.OperationalNotes));
        if (normalized.InfrastructureTarget.Length is < 1 or > 255
            || normalized.InstanceReference.Length is < 1 or > 255
            || normalized.PublicIpAddress.Length > 45
            || normalized.OperationalNotes.Length > 2000
            || (normalized.PublicIpAddress.Length > 0
                && !IPAddress.TryParse(normalized.PublicIpAddress, out _))
            || ContainsSecret(normalized.AllValues()))
        {
            throw new PortalValidationException();
        }
        return normalized;
    }

    private static bool ContainsSecret(IEnumerable<string> values)
        => values.Any(value => value.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase)
            || value.Contains("mot de passe", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("private key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("clé privée", StringComparison.OrdinalIgnoreCase)
            || value.Contains("api key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("ssh-rsa", StringComparison.OrdinalIgnoreCase));

    private static object DbNullable(string value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static string? ReadNullableString(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(column);

    private static DateTime? ReadNullableDateTime(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetDateTime(column);

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;

    private sealed record ApprovalState(
        string TechnicalStatus,
        string SettlementStatus,
        string ProvisioningStatus);
    private sealed record ProvisioningState(
        string TechnicalStatus,
        string ProvisioningStatus,
        string SettlementStatus);
    private sealed record ManualProvisioningInput(
        string InfrastructureTarget,
        string InstanceReference,
        string PublicIpAddress,
        string OperationalNotes)
    {
        public IEnumerable<string> AllValues()
            => [InfrastructureTarget, InstanceReference, PublicIpAddress, OperationalNotes];
    }
}

/// <summary>
/// Pont transactionnel entre la preuve de settlement Billing V2 et la revue
/// VPS. Un retour navigateur ne l'appelle jamais : seule la relecture Stripe
/// qui vient de passer <c>settlement_status</c> a <c>settled</c> y arrive.
/// </summary>
public static class BillingV2VpsTechnicalReviewSettlement
{
    public static async Task QueuePendingReviewAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string billingEventId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_vps_technical_requests request_row
            INNER JOIN billing_v2_vps_technical_request_checkouts checkout_link
                ON checkout_link.technical_request_id = request_row.id
            INNER JOIN billing_v2_billing_events event_row
                ON event_row.id = checkout_link.billing_event_id
            SET request_row.technical_status = 'pending_review',
                request_row.technical_review_pending_at = COALESCE(
                    request_row.technical_review_pending_at,
                    @now),
                request_row.updated_at = @now
            WHERE checkout_link.billing_event_id = @billing_event_id
              AND event_row.settlement_status = 'settled'
              AND request_row.technical_status = 'draft';
            """;
        command.Parameters.AddWithValue("@billing_event_id", billingEventId);
        command.Parameters.AddWithValue("@now", nowUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
