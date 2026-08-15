using System.Data;
using System.Security.Cryptography;
using System.Text;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2AuthoritativeCheckoutRequest(
    string LegacyOfferId,
    string Provider,
    string IdempotencyKey,
    string SuccessUrl,
    string CancelUrl);

public sealed record BillingV2AuthoritativeCheckoutResult(
    bool Created,
    string SubscriptionId,
    string Provider,
    string Environment,
    string OutboxEventId,
    string IdempotencyKeyHash,
    long TotalDueNowCents,
    string ReasonCode,
    string? ApprovalUrl);

public sealed record BillingV2AuthoritativeCheckoutReadiness(
    bool Authorized,
    string ReasonCode);

public sealed record BillingV2SubscriptionPriceLockPlan(
    string LockType,
    long AmountCents,
    string Currency,
    DateTime EffectiveFromUtc,
    DateTime EffectiveUntilUtc,
    string SourceLegacyOfferId,
    string Reason);

public interface IBillingV2AuthoritativeCheckoutService
{
    Task<BillingV2AuthoritativeCheckoutResult> CreateAsync(
        PortalSessionContext session,
        BillingV2AuthoritativeCheckoutRequest request,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class BillingV2AuthoritativeCheckoutService
    : IBillingV2AuthoritativeCheckoutService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly IBillingV2CheckoutReadinessService _readiness;
    private readonly IBillingV2PricingEngine _pricing;

    public BillingV2AuthoritativeCheckoutService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration runtime,
        PayPalRuntimeConfiguration paypal,
        StripeRuntimeConfiguration stripe,
        IBillingV2CheckoutReadinessService readiness,
        IBillingV2PricingEngine pricing)
    {
        _sql = sql;
        _runtime = runtime;
        _paypal = paypal;
        _stripe = stripe;
        _readiness = readiness;
        _pricing = pricing;
    }

    public async Task<BillingV2AuthoritativeCheckoutResult> CreateAsync(
        PortalSessionContext session,
        BillingV2AuthoritativeCheckoutRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var provider = NormalizeProvider(request.Provider);
        var environment = ResolveEnvironment(provider);
        var requestFingerprintHash =
            BillingV2AuthoritativeCheckoutIdempotencyPolicy
                .ComputeRequestFingerprintHash(
                    session.CustomerId,
                    session.UserId,
                    provider,
                    environment,
                    request.LegacyOfferId);
        var gate = BillingV2AuthoritativeCheckoutGate.Evaluate(
            _runtime,
            _sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString),
            request.IdempotencyKey);
        if (!gate.Authorized)
        {
            throw new InvalidOperationException(gate.ReasonCode);
        }

        var now = DateTime.UtcNow;
        await using var readConnection =
            new MySqlConnection(_sql.ConnectionString);
        await readConnection.OpenAsync(cancellationToken);
        var earlyExisting = await ReadCheckoutRequestOrNullAsync(
            readConnection,
            transaction: null,
            session.CustomerId,
            request.IdempotencyKey,
            cancellationToken);
        if (earlyExisting is not null)
        {
            EnsureSameIdempotentRequest(
                earlyExisting,
                requestFingerprintHash);
            var earlyApprovalUrl = await ReadApprovalUrlAsync(
                readConnection,
                transaction: null,
                earlyExisting.SubscriptionId,
                earlyExisting.IdempotencyKeyHash,
                cancellationToken);
            return new BillingV2AuthoritativeCheckoutResult(
                Created: false,
                earlyExisting.SubscriptionId,
                earlyExisting.Provider,
                earlyExisting.Environment,
                earlyExisting.OutboxEventId ?? string.Empty,
                earlyExisting.IdempotencyKeyHash ?? string.Empty,
                TotalDueNowCents: 0,
                earlyExisting.ReasonCode
                    ?? "BILLING_V2_AUTHORITATIVE_CHECKOUT_IDEMPOTENT_NOOP",
                earlyApprovalUrl);
        }

        var mapping = await ReadMappingAsync(
            readConnection,
            request.LegacyOfferId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "BILLING_V2_LEGACY_OFFER_MAPPING_NOT_FOUND");
        var presetItems = await ReadPresetItemsAsync(
            readConnection,
            mapping.PresetId,
            now,
            cancellationToken);
        if (presetItems.Count == 0)
        {
            throw new InvalidOperationException(
                "BILLING_V2_PRESET_HAS_NO_ITEMS");
        }

        var pricing = CalculatePricing(mapping, presetItems, now);
        var checkoutReadiness = await _readiness.CheckAsync(
            new BillingV2CheckoutReadinessRequest(
                presetItems
                    .Select(item => item.ServicePriceId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                provider,
                environment),
            cancellationToken);
        var checkoutPlan = BillingV2CheckoutPlanner.Plan(
            checkoutReadiness,
            presetItems,
            pricing);
        var subscriptionId = Guid.NewGuid().ToString("D");
        var providerRequest = new BillingV2ProviderCheckoutCommandRequest(
            subscriptionId,
            session.CustomerId,
            session.Email,
            request.SuccessUrl,
            request.CancelUrl,
            checkoutPlan,
            checkoutReadiness,
            correlationId,
            session.UserId);
        var providerPlan = BillingV2ProviderCheckoutCommandPlanner.Plan(
            providerRequest);

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var requestId = Guid.NewGuid().ToString("D");
        await InsertCheckoutRequestAsync(
            connection,
            transaction,
            requestId,
            session,
            request,
            provider,
            environment,
            requestFingerprintHash,
            subscriptionId,
            cancellationToken);
        var existing = await ReadCheckoutRequestAsync(
            connection,
            transaction,
            session.CustomerId,
            request.IdempotencyKey,
            cancellationToken);
        if (!string.Equals(existing.Id, requestId, StringComparison.Ordinal))
        {
            EnsureSameIdempotentRequest(
                existing,
                requestFingerprintHash);
            var existingApprovalUrl = await ReadApprovalUrlAsync(
                connection,
                transaction,
                existing.SubscriptionId,
                existing.IdempotencyKeyHash,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new BillingV2AuthoritativeCheckoutResult(
                Created: false,
                existing.SubscriptionId,
                existing.Provider,
                existing.Environment,
                existing.OutboxEventId ?? string.Empty,
                existing.IdempotencyKeyHash ?? string.Empty,
                TotalDueNowCents: 0,
                existing.ReasonCode
                    ?? "BILLING_V2_AUTHORITATIVE_CHECKOUT_IDEMPOTENT_NOOP",
                existingApprovalUrl);
        }

        var itemPlan = BillingV2NewSubscriptionPlanner.Plan(
            session,
            presetItems);
        await InsertSubscriptionAsync(
            connection,
            transaction,
            subscriptionId,
            session.CustomerId,
            mapping,
            pricing,
            now,
            cancellationToken);
        foreach (var user in itemPlan.Users)
        {
            await InsertUserAsync(
                connection,
                transaction,
                subscriptionId,
                user,
                now,
                cancellationToken);
        }

        foreach (var item in itemPlan.Items)
        {
            await InsertItemAsync(
                connection,
                transaction,
                subscriptionId,
                item,
                now,
                cancellationToken);
            await InsertItemProvisioningAsync(
                connection,
                transaction,
                item,
                now,
                cancellationToken);
        }

        var priceLock = BillingV2AuthoritativeCheckoutPriceLockPolicy.Plan(
            request.LegacyOfferId,
            mapping.PaymentMode,
            mapping.CommitmentMonths,
            pricing,
            now);
        await InsertSubscriptionPriceLockAsync(
            connection,
            transaction,
            subscriptionId,
            priceLock,
            cancellationToken);

        var outboxEventId = Guid.NewGuid().ToString("D");
        await InsertOutboxEventAsync(
            connection,
            transaction,
            outboxEventId,
            providerPlan,
            cancellationToken);
        await InsertAuditAsync(
            connection,
            transaction,
            subscriptionId,
            session.UserId,
            providerPlan.PayloadText,
            cancellationToken);
        await MarkCheckoutRequestQueuedAsync(
            connection,
            transaction,
            requestId,
            outboxEventId,
            providerPlan.IdempotencyKeyHash,
            checkoutReadiness.ReasonCode,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BillingV2AuthoritativeCheckoutResult(
            Created: true,
            subscriptionId,
            provider,
            environment,
            outboxEventId,
            providerPlan.IdempotencyKeyHash,
            pricing.TotalDueNowCents,
            checkoutReadiness.ReasonCode,
            ApprovalUrl: null);
    }

    private string ResolveEnvironment(string provider)
        => provider == "stripe" ? _stripe.ModeName : _paypal.ModeName;

    private static string NormalizeProvider(string provider)
    {
        var normalized = provider.Trim().ToLowerInvariant();
        if (normalized is not ("stripe" or "paypal"))
        {
            throw new InvalidOperationException(
                "BILLING_V2_PROVIDER_UNSUPPORTED");
        }

        return normalized;
    }

    private BillingV2PricingResult CalculatePricing(
        BillingV2AuthoritativeCheckoutMapping mapping,
        IReadOnlyList<BillingV2NewSubscriptionPresetItem> presetItems,
        DateTime now)
        => _pricing.Calculate(new BillingV2PricingRequest(
            presetItems.Select(item => new BillingV2PricingItem(
                item.PresetItemId,
                item.ServiceCode,
                item.TierCode,
                item.PriceCode,
                item.AmountCents,
                item.Quantity,
                item.BillingCadence,
                item.DiscountEligible)).ToArray(),
            mapping.DiscountBasisPoints,
            mapping.PaymentMode,
            mapping.CommitmentMonths,
            MinimumCommitmentAmountCents: null,
            PriceLock: null,
            now));

    private static async Task<BillingV2AuthoritativeCheckoutMapping?>
        ReadMappingAsync(
            MySqlConnection connection,
            string legacyOfferId,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                mapping.preset_id,
                mapping.commitment_term_id,
                mapping.payment_mode,
                term.commitment_months,
                option_row.discount_basis_points
            FROM billing_v2_legacy_offer_mappings mapping
            INNER JOIN billing_v2_commitment_terms term
                ON term.id = mapping.commitment_term_id
               AND term.status = 'active'
            INNER JOIN billing_v2_commitment_payment_options option_row
                ON option_row.commitment_term_id = term.id
               AND option_row.payment_mode = mapping.payment_mode
               AND option_row.status = 'active'
            WHERE mapping.legacy_offer_id = @legacy_offer_id
              AND mapping.status = 'active'
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@legacy_offer_id", legacyOfferId);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2AuthoritativeCheckoutMapping(
            reader.GetString("preset_id"),
            reader.GetString("commitment_term_id"),
            reader.GetString("payment_mode"),
            reader.GetInt32("commitment_months"),
            reader.GetInt32("discount_basis_points"));
    }

    private static async Task<IReadOnlyList<BillingV2NewSubscriptionPresetItem>>
        ReadPresetItemsAsync(
            MySqlConnection connection,
            string presetId,
            DateTime now,
            CancellationToken cancellationToken)
    {
        var items = new List<BillingV2NewSubscriptionPresetItem>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                preset_item.id AS preset_item_id,
                preset_item.service_id,
                preset_item.tier_id,
                preset_item.scope_template,
                preset_item.quantity,
                service.code AS service_code,
                service.discount_eligible,
                tier.code AS tier_code,
                price.id AS service_price_id,
                price.price_code,
                price.amount_cents,
                price.currency,
                price.billing_cadence
            FROM billing_v2_preset_items preset_item
            INNER JOIN billing_v2_services service
                ON service.id = preset_item.service_id
               AND service.status = 'active'
            LEFT JOIN billing_v2_service_tiers tier
                ON tier.id = preset_item.tier_id
               AND tier.status = 'active'
            INNER JOIN billing_v2_service_prices price
                ON price.service_id = service.id
               AND price.tier_id <=> preset_item.tier_id
               AND price.status = 'active'
               AND price.valid_from <= @now
               AND (price.valid_until IS NULL OR price.valid_until > @now)
            WHERE preset_item.preset_id = @preset_id
            ORDER BY preset_item.display_order, preset_item.id;
            """;
        command.Parameters.AddWithValue("@preset_id", presetId);
        command.Parameters.AddWithValue("@now", now);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new BillingV2NewSubscriptionPresetItem(
                reader.GetString("preset_item_id"),
                reader.GetString("service_id"),
                reader.IsDBNull(reader.GetOrdinal("tier_id"))
                    ? null
                    : reader.GetString("tier_id"),
                reader.GetString("service_price_id"),
                reader.GetString("service_code"),
                reader.IsDBNull(reader.GetOrdinal("tier_code"))
                    ? null
                    : reader.GetString("tier_code"),
                reader.GetString("price_code"),
                reader.GetString("scope_template"),
                reader.GetInt32("quantity"),
                reader.GetInt64("amount_cents"),
                reader.GetString("currency"),
                reader.GetString("billing_cadence"),
                reader.GetBoolean("discount_eligible")));
        }

        return items;
    }

    private static async Task InsertCheckoutRequestAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string requestId,
        PortalSessionContext session,
        BillingV2AuthoritativeCheckoutRequest request,
        string provider,
        string environment,
        string requestFingerprintHash,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT IGNORE INTO billing_v2_authoritative_checkout_requests (
                id,
                customer_id,
                actor_reference,
                idempotency_key,
                request_fingerprint_hash,
                legacy_offer_id,
                provider,
                environment,
                subscription_id,
                status,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @customer_id,
                @actor_reference,
                @idempotency_key,
                @request_fingerprint_hash,
                @legacy_offer_id,
                @provider,
                @environment,
                @subscription_id,
                'pending',
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", requestId);
        command.Parameters.AddWithValue("@customer_id", session.CustomerId);
        command.Parameters.AddWithValue("@actor_reference", session.UserId);
        command.Parameters.AddWithValue(
            "@idempotency_key",
            request.IdempotencyKey);
        command.Parameters.AddWithValue(
            "@request_fingerprint_hash",
            requestFingerprintHash);
        command.Parameters.AddWithValue(
            "@legacy_offer_id",
            request.LegacyOfferId);
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@environment", environment);
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<BillingV2AuthoritativeCheckoutRequestRecord>
        ReadCheckoutRequestAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string customerId,
            string idempotencyKey,
            CancellationToken cancellationToken)
        => await ReadCheckoutRequestOrNullAsync(
            connection,
            transaction,
            customerId,
            idempotencyKey,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "BILLING_V2_CHECKOUT_REQUEST_NOT_PERSISTED");

    private static async Task<BillingV2AuthoritativeCheckoutRequestRecord?>
        ReadCheckoutRequestOrNullAsync(
            MySqlConnection connection,
            MySqlTransaction? transaction,
            string customerId,
            string idempotencyKey,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                id,
                subscription_id,
                provider,
                environment,
                request_fingerprint_hash,
                outbox_event_id,
                idempotency_key_hash,
                reason_code
            FROM billing_v2_authoritative_checkout_requests
            WHERE customer_id = @customer_id
              AND idempotency_key = @idempotency_key
            ORDER BY created_at ASC, id ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        command.Parameters.AddWithValue("@idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2AuthoritativeCheckoutRequestRecord(
            reader.GetString("id"),
            reader.GetString("subscription_id"),
            reader.GetString("provider"),
            reader.GetString("environment"),
            reader.GetString("request_fingerprint_hash"),
            reader.IsDBNull(reader.GetOrdinal("outbox_event_id"))
                ? null
                : reader.GetString("outbox_event_id"),
            reader.IsDBNull(reader.GetOrdinal("idempotency_key_hash"))
                ? null
                : reader.GetString("idempotency_key_hash"),
            reader.IsDBNull(reader.GetOrdinal("reason_code"))
                ? null
                : reader.GetString("reason_code"));
    }

    private static async Task<string?> ReadApprovalUrlAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string subscriptionId,
        string? idempotencyKeyHash,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKeyHash))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT approval_url
            FROM billing_v2_provider_checkout_sessions
            WHERE subscription_id = @subscription_id
              AND idempotency_key_hash = @idempotency_key_hash
              AND status = 'pending_approval'
              AND approval_url IS NOT NULL
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@idempotency_key_hash",
            idempotencyKeyHash);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string approvalUrl
            && !string.IsNullOrWhiteSpace(approvalUrl)
                ? approvalUrl
                : null;
    }

    private static async Task InsertSubscriptionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        string customerId,
        BillingV2AuthoritativeCheckoutMapping mapping,
        BillingV2PricingResult pricing,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_subscriptions (
                id,
                customer_id,
                originating_preset_id,
                commitment_term_id,
                status,
                payment_mode,
                currency,
                discount_basis_points_snapshot,
                minimum_commitment_amount_cents,
                billing_model,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @customer_id,
                @originating_preset_id,
                @commitment_term_id,
                'pending_approval',
                @payment_mode,
                'EUR',
                @discount_basis_points,
                @minimum_commitment_amount_cents,
                'v2',
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue("@id", subscriptionId);
        command.Parameters.AddWithValue("@customer_id", customerId);
        command.Parameters.AddWithValue(
            "@originating_preset_id",
            mapping.PresetId);
        command.Parameters.AddWithValue(
            "@commitment_term_id",
            mapping.CommitmentTermId);
        command.Parameters.AddWithValue("@payment_mode", mapping.PaymentMode);
        command.Parameters.AddWithValue(
            "@discount_basis_points",
            mapping.DiscountBasisPoints);
        command.Parameters.AddWithValue(
            "@minimum_commitment_amount_cents",
            mapping.CommitmentMonths > 1
            && mapping.PaymentMode == BillingV2PaymentModes.Monthly
                ? pricing.PayableRecurringAmountCents
                : DBNull.Value);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertUserAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        BillingV2NewSubscriptionUserPlan user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_subscription_users (
                id,
                subscription_id,
                identity_reference,
                display_name,
                email,
                is_primary,
                status,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @subscription_id,
                @identity_reference,
                @display_name,
                @email,
                @is_primary,
                'active',
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue("@id", user.Id);
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@identity_reference",
            string.IsNullOrWhiteSpace(user.IdentityReference)
                ? DBNull.Value
                : user.IdentityReference);
        command.Parameters.AddWithValue("@display_name", user.DisplayName);
        command.Parameters.AddWithValue(
            "@email",
            string.IsNullOrWhiteSpace(user.Email) ? DBNull.Value : user.Email);
        command.Parameters.AddWithValue("@is_primary", user.IsPrimary);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertItemAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        BillingV2NewSubscriptionItemPlan item,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_subscription_items (
                id,
                subscription_id,
                subscription_user_id,
                service_id,
                tier_id,
                service_price_id,
                scope_type,
                quantity,
                amount_cents_snapshot,
                currency,
                discount_eligible_snapshot,
                source,
                effective_from,
                status,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @subscription_id,
                @subscription_user_id,
                @service_id,
                @tier_id,
                @service_price_id,
                @scope_type,
                @quantity,
                @amount_cents_snapshot,
                @currency,
                @discount_eligible_snapshot,
                @source,
                @effective_from,
                'active',
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue("@id", item.Id);
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@subscription_user_id",
            item.UserId is null ? DBNull.Value : item.UserId);
        command.Parameters.AddWithValue("@service_id", item.ServiceId);
        command.Parameters.AddWithValue(
            "@tier_id",
            item.TierId is null ? DBNull.Value : item.TierId);
        command.Parameters.AddWithValue("@service_price_id", item.ServicePriceId);
        command.Parameters.AddWithValue("@scope_type", item.ScopeType);
        command.Parameters.AddWithValue("@quantity", item.Quantity);
        command.Parameters.AddWithValue(
            "@amount_cents_snapshot",
            item.AmountCentsSnapshot);
        command.Parameters.AddWithValue("@currency", item.Currency);
        command.Parameters.AddWithValue(
            "@discount_eligible_snapshot",
            item.DiscountEligibleSnapshot);
        command.Parameters.AddWithValue("@source", item.Source);
        command.Parameters.AddWithValue("@effective_from", now);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertItemProvisioningAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2NewSubscriptionItemPlan item,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_subscription_item_provisioning (
                subscription_item_id,
                provisioned_tier_id,
                provisioned_quantity,
                provisioning_status,
                created_at,
                updated_at
            ) VALUES (
                @subscription_item_id,
                @provisioned_tier_id,
                @provisioned_quantity,
                'pending',
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue("@subscription_item_id", item.Id);
        command.Parameters.AddWithValue(
            "@provisioned_tier_id",
            item.TierId is null ? DBNull.Value : item.TierId);
        command.Parameters.AddWithValue(
            "@provisioned_quantity",
            item.Quantity);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertSubscriptionPriceLockAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        BillingV2SubscriptionPriceLockPlan priceLock,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_subscription_price_locks (
                id,
                subscription_id,
                lock_type,
                amount_cents,
                currency,
                effective_from,
                effective_until,
                source_legacy_offer_id,
                reason,
                status,
                created_at
            ) VALUES (
                @id,
                @subscription_id,
                @lock_type,
                @amount_cents,
                @currency,
                @effective_from,
                @effective_until,
                @source_legacy_offer_id,
                @reason,
                'active',
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue("@lock_type", priceLock.LockType);
        command.Parameters.AddWithValue("@amount_cents", priceLock.AmountCents);
        command.Parameters.AddWithValue("@currency", priceLock.Currency);
        command.Parameters.AddWithValue(
            "@effective_from",
            priceLock.EffectiveFromUtc);
        command.Parameters.AddWithValue(
            "@effective_until",
            priceLock.EffectiveUntilUtc);
        command.Parameters.AddWithValue(
            "@source_legacy_offer_id",
            priceLock.SourceLegacyOfferId);
        command.Parameters.AddWithValue("@reason", priceLock.Reason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOutboxEventAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string outboxEventId,
        BillingV2ProviderCheckoutCommandPlan plan,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_outbox_events (
                id,
                aggregate_type,
                aggregate_id,
                event_type,
                payload_text,
                idempotency_key_hash,
                status,
                retry_count,
                available_at,
                created_at
            ) VALUES (
                @id,
                @aggregate_type,
                @aggregate_id,
                @event_type,
                @payload_text,
                @idempotency_key_hash,
                'pending',
                0,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            )
            ON DUPLICATE KEY UPDATE
                id = id;
            """;
        command.Parameters.AddWithValue("@id", outboxEventId);
        command.Parameters.AddWithValue("@aggregate_type", plan.AggregateType);
        command.Parameters.AddWithValue("@aggregate_id", plan.AggregateId);
        command.Parameters.AddWithValue("@event_type", plan.EventType);
        command.Parameters.AddWithValue("@payload_text", plan.PayloadText);
        command.Parameters.AddWithValue(
            "@idempotency_key_hash",
            plan.IdempotencyKeyHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        string actorReference,
        string detailsText,
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
                'billing_v2.authoritative_checkout_requested',
                @actor_reference,
                @details_text,
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@entity_id", subscriptionId);
        command.Parameters.AddWithValue("@actor_reference", actorReference);
        command.Parameters.AddWithValue("@details_text", detailsText);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkCheckoutRequestQueuedAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string requestId,
        string outboxEventId,
        string idempotencyKeyHash,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE billing_v2_authoritative_checkout_requests
            SET status = 'queued',
                outbox_event_id = @outbox_event_id,
                idempotency_key_hash = @idempotency_key_hash,
                reason_code = @reason_code,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", requestId);
        command.Parameters.AddWithValue("@outbox_event_id", outboxEventId);
        command.Parameters.AddWithValue(
            "@idempotency_key_hash",
            idempotencyKeyHash);
        command.Parameters.AddWithValue("@reason_code", reasonCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void EnsureSameIdempotentRequest(
        BillingV2AuthoritativeCheckoutRequestRecord existing,
        string requestFingerprintHash)
    {
        if (!BillingV2AuthoritativeCheckoutIdempotencyPolicy
            .MatchesRequestFingerprint(
                existing.RequestFingerprintHash,
                requestFingerprintHash))
        {
            throw new InvalidOperationException(
                "BILLING_V2_AUTHORITATIVE_CHECKOUT_IDEMPOTENCY_CONFLICT");
        }
    }

    private sealed record BillingV2AuthoritativeCheckoutMapping(
        string PresetId,
        string CommitmentTermId,
        string PaymentMode,
        int CommitmentMonths,
        int DiscountBasisPoints);

    private sealed record BillingV2AuthoritativeCheckoutRequestRecord(
        string Id,
        string SubscriptionId,
        string Provider,
        string Environment,
        string RequestFingerprintHash,
        string? OutboxEventId,
        string? IdempotencyKeyHash,
        string? ReasonCode);
}

public static class BillingV2AuthoritativeCheckoutIdempotencyPolicy
{
    public static string ComputeRequestFingerprintHash(
        string customerId,
        string actorReference,
        string provider,
        string environment,
        string legacyOfferId)
    {
        var fingerprint = string.Join(
            "|",
            customerId,
            provider,
            environment,
            legacyOfferId,
            actorReference);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool MatchesRequestFingerprint(
        string existingFingerprintHash,
        string requestFingerprintHash)
        => string.Equals(
            existingFingerprintHash,
            requestFingerprintHash,
            StringComparison.OrdinalIgnoreCase);
}

public static class BillingV2AuthoritativeCheckoutPriceLockPolicy
{
    public const string CheckoutReason = "v2_authoritative_checkout";

    public static BillingV2SubscriptionPriceLockPlan Plan(
        string sourceLegacyOfferId,
        string paymentMode,
        int commitmentMonths,
        BillingV2PricingResult pricing,
        DateTime nowUtc)
    {
        var months = Math.Max(1, commitmentMonths);
        if (string.Equals(
                paymentMode,
                BillingV2PaymentModes.Upfront,
                StringComparison.Ordinal))
        {
            return new BillingV2SubscriptionPriceLockPlan(
                BillingV2PriceLockTypes.UpfrontPrepaid,
                pricing.UpfrontRecurringAmountCents,
                "EUR",
                nowUtc,
                nowUtc.AddMonths(months),
                sourceLegacyOfferId,
                CheckoutReason);
        }

        return new BillingV2SubscriptionPriceLockPlan(
            BillingV2PriceLockTypes.MonthlyRecurring,
            pricing.PayableRecurringAmountCents,
            "EUR",
            nowUtc,
            nowUtc.AddMonths(months),
            sourceLegacyOfferId,
            CheckoutReason);
    }
}

public static class BillingV2AuthoritativeCheckoutGate
{
    public static BillingV2AuthoritativeCheckoutReadiness Evaluate(
        BillingV2RuntimeConfiguration runtime,
        bool persistentSqlAvailable,
        string? idempotencyKey)
    {
        if (!runtime.NewSubscriptionsEnabled)
        {
            return Blocked("BILLING_V2_NEW_SUBSCRIPTIONS_FLAG_OFF");
        }

        if (!runtime.AuthoritativeCheckoutEnabled)
        {
            return Blocked("BILLING_V2_AUTHORITATIVE_CHECKOUT_FLAG_OFF");
        }

        if (!runtime.FirstRealSubscriptionApproved)
        {
            return Blocked(
                "BILLING_V2_FIRST_REAL_SUBSCRIPTION_NOT_APPROVED");
        }

        if (!persistentSqlAvailable)
        {
            return Blocked("BILLING_V2_AUTHORITATIVE_CHECKOUT_NO_SQL");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 128)
        {
            return Blocked(
                "BILLING_V2_AUTHORITATIVE_CHECKOUT_IDEMPOTENCY_REQUIRED");
        }

        return new BillingV2AuthoritativeCheckoutReadiness(
            Authorized: true,
            "BILLING_V2_AUTHORITATIVE_CHECKOUT_LOCALLY_READY");
    }

    private static BillingV2AuthoritativeCheckoutReadiness Blocked(
        string reasonCode)
        => new(Authorized: false, reasonCode);
}
