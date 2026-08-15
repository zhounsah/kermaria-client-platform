using System.Data;
using System.Globalization;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2NewSubscriptionPlan(
    IReadOnlyList<BillingV2NewSubscriptionUserPlan> Users,
    IReadOnlyList<BillingV2NewSubscriptionItemPlan> Items);

public sealed record BillingV2NewSubscriptionUserPlan(
    string Id,
    string? IdentityReference,
    string DisplayName,
    string? Email,
    bool IsPrimary);

public sealed record BillingV2NewSubscriptionPresetItem(
    string PresetItemId,
    string ServiceId,
    string? TierId,
    string ServicePriceId,
    string ServiceCode,
    string? TierCode,
    string PriceCode,
    string ScopeTemplate,
    int Quantity,
    long AmountCents,
    string Currency,
    string BillingCadence,
    bool DiscountEligible);

public sealed record BillingV2NewSubscriptionItemPlan(
    string Id,
    string? UserId,
    string ServiceId,
    string? TierId,
    string ServicePriceId,
    string ScopeType,
    int Quantity,
    long AmountCentsSnapshot,
    string Currency,
    bool DiscountEligibleSnapshot,
    string Source);

public interface IBillingV2NewSubscriptionService
{
    Task CreateForNewSubscriptionAsync(
        PortalSessionContext session,
        SubscriptionSummary legacySubscription,
        CancellationToken cancellationToken);

    Task SyncFromLegacySubscriptionAsync(
        SubscriptionSummary legacySubscription,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2NewSubscriptionService
    : IBillingV2NewSubscriptionService
{
    public static NoOpBillingV2NewSubscriptionService Instance { get; } = new();

    private NoOpBillingV2NewSubscriptionService()
    {
    }

    public Task CreateForNewSubscriptionAsync(
        PortalSessionContext session,
        SubscriptionSummary legacySubscription,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task SyncFromLegacySubscriptionAsync(
        SubscriptionSummary legacySubscription,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class BillingV2NewSubscriptionService
    : IBillingV2NewSubscriptionService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _configuration;
    private readonly IBillingV2PricingEngine _pricing;
    private readonly IBillingV2ProviderAgreementService _providerAgreements;
    private readonly ILogger<BillingV2NewSubscriptionService> _logger;

    public BillingV2NewSubscriptionService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration configuration,
        IBillingV2PricingEngine pricing,
        IBillingV2ProviderAgreementService providerAgreements,
        ILogger<BillingV2NewSubscriptionService> logger)
    {
        _sql = sql;
        _configuration = configuration;
        _pricing = pricing;
        _providerAgreements = providerAgreements;
        _logger = logger;
    }

    public async Task CreateForNewSubscriptionAsync(
        PortalSessionContext session,
        SubscriptionSummary legacySubscription,
        CancellationToken cancellationToken)
    {
        if (!_configuration.NewSubscriptionsEnabled
            || !_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return;
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        if (await SubscriptionExistsAsync(
                connection,
                transaction,
                legacySubscription.Id,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var now = DateTime.UtcNow;
        var mapping = await ReadMappingAsync(
            connection,
            transaction,
            legacySubscription,
            cancellationToken);
        if (mapping is null)
        {
            _logger.LogWarning(
                "Billing V2 new subscription skipped for legacy subscription {SubscriptionId}: missing active legacy mapping for offer {OfferId}.",
                legacySubscription.Id,
                legacySubscription.CommercialOfferId);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var presetItems = await ReadPresetItemsAsync(
            connection,
            transaction,
            mapping.PresetId,
            now,
            cancellationToken);
        if (presetItems.Count == 0)
        {
            throw new InvalidOperationException(
                $"Billing V2 preset {mapping.PresetId} has no active billable items.");
        }

        var plan = BillingV2NewSubscriptionPlanner.Plan(
            session,
            presetItems);
        var pricingResult = _pricing.Calculate(new BillingV2PricingRequest(
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
            AsOfUtc: now));
        var minimumCommitmentAmountCents =
            string.Equals(
                mapping.PaymentMode,
                BillingV2PaymentModes.Monthly,
                StringComparison.Ordinal)
            && mapping.CommitmentMonths > 1
                ? _pricing.CalculateMinimumCommitmentAmount(
                    pricingResult.DiscountedRecurringAmountCents)
                : (long?)null;

        await InsertSubscriptionAsync(
            connection,
            transaction,
            legacySubscription,
            mapping,
            minimumCommitmentAmountCents,
            now,
            cancellationToken);
        foreach (var user in plan.Users)
        {
            await InsertUserAsync(
                connection,
                transaction,
                legacySubscription.Id,
                user,
                now,
                cancellationToken);
        }

        foreach (var item in plan.Items)
        {
            await InsertItemAsync(
                connection,
                transaction,
                legacySubscription.Id,
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

        await _providerAgreements.RecordFromLegacySubscriptionAsync(
            connection,
            transaction,
            legacySubscription.Id,
            legacySubscription,
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SyncFromLegacySubscriptionAsync(
        SubscriptionSummary legacySubscription,
        CancellationToken cancellationToken)
    {
        if (!_configuration.NewSubscriptionsEnabled
            || !_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return;
        }

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE billing_v2_subscriptions
            SET status = @status,
                started_at = COALESCE(started_at, @started_at),
                current_period_ends_at = @current_period_ends_at,
                renews_at = @renews_at,
                commitment_ends_at = @commitment_ends_at,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @subscription_id;
            """;
        command.Parameters.AddWithValue(
            "@subscription_id",
            legacySubscription.Id);
        command.Parameters.AddWithValue("@status", legacySubscription.Status);
        command.Parameters.AddWithValue(
            "@started_at",
            DbNullableDateTime(legacySubscription.StartedAt));
        command.Parameters.AddWithValue(
            "@current_period_ends_at",
            DbNullableDateTime(legacySubscription.NextBillingAt));
        command.Parameters.AddWithValue(
            "@renews_at",
            DbNullableDateTime(legacySubscription.NextBillingAt));
        command.Parameters.AddWithValue(
            "@commitment_ends_at",
            DbNullableDateTime(legacySubscription.CommitmentEndsAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static object DbNullableDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return DBNull.Value;
        }

        return parsed.UtcDateTime;
    }

    private static async Task<bool> SubscriptionExistsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT 1
            FROM billing_v2_subscriptions
            WHERE id = @subscription_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private static async Task<BillingV2NewSubscriptionMapping?>
        ReadMappingAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            SubscriptionSummary legacySubscription,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
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
            INNER JOIN billing_v2_offer_presets preset
                ON preset.id = mapping.preset_id
               AND preset.status = 'active'
            WHERE mapping.legacy_offer_id = @legacy_offer_id
              AND mapping.status = 'active'
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(
            "@legacy_offer_id",
            legacySubscription.CommercialOfferId);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2NewSubscriptionMapping(
            reader.GetString("preset_id"),
            reader.GetString("commitment_term_id"),
            reader.GetString("payment_mode"),
            reader.GetInt32("commitment_months"),
            reader.GetInt32("discount_basis_points"));
    }

    private static async Task<IReadOnlyList<BillingV2NewSubscriptionPresetItem>>
        ReadPresetItemsAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string presetId,
            DateTime now,
            CancellationToken cancellationToken)
    {
        var items = new List<BillingV2NewSubscriptionPresetItem>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
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
               AND (
                   price.tier_id <=> preset_item.tier_id
               )
               AND price.status = 'active'
               AND price.valid_from <= @now
               AND (
                   price.valid_until IS NULL
                   OR price.valid_until > @now
               )
            WHERE preset_item.preset_id = @preset_id
              AND price.id = (
                  SELECT latest_price.id
                  FROM billing_v2_service_prices latest_price
                  WHERE latest_price.service_id = service.id
                    AND latest_price.tier_id <=> preset_item.tier_id
                    AND latest_price.status = 'active'
                    AND latest_price.valid_from <= @now
                    AND (
                        latest_price.valid_until IS NULL
                        OR latest_price.valid_until > @now
                    )
                  ORDER BY latest_price.valid_from DESC,
                           latest_price.price_version DESC,
                           latest_price.id DESC
                  LIMIT 1
              )
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

    private static async Task InsertSubscriptionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        SubscriptionSummary legacySubscription,
        BillingV2NewSubscriptionMapping mapping,
        long? minimumCommitmentAmountCents,
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
                @status,
                @payment_mode,
                @currency,
                @discount_basis_points,
                @minimum_commitment_amount_cents,
                'v2',
                @created_at,
                @updated_at
            );
            """;
        command.Parameters.AddWithValue("@id", legacySubscription.Id);
        command.Parameters.AddWithValue(
            "@customer_id",
            legacySubscription.CustomerId);
        command.Parameters.AddWithValue(
            "@originating_preset_id",
            mapping.PresetId);
        command.Parameters.AddWithValue(
            "@commitment_term_id",
            mapping.CommitmentTermId);
        command.Parameters.AddWithValue("@status", legacySubscription.Status);
        command.Parameters.AddWithValue("@payment_mode", mapping.PaymentMode);
        command.Parameters.AddWithValue("@currency", legacySubscription.Currency);
        command.Parameters.AddWithValue(
            "@discount_basis_points",
            mapping.DiscountBasisPoints);
        command.Parameters.AddWithValue(
            "@minimum_commitment_amount_cents",
            minimumCommitmentAmountCents is null
                ? DBNull.Value
                : minimumCommitmentAmountCents);
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
        command.Parameters.AddWithValue(
            "@service_price_id",
            item.ServicePriceId);
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
        command.Parameters.AddWithValue(
            "@subscription_item_id",
            item.Id);
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

    private sealed record BillingV2NewSubscriptionMapping(
        string PresetId,
        string CommitmentTermId,
        string PaymentMode,
        int CommitmentMonths,
        int DiscountBasisPoints);
}

public static class BillingV2NewSubscriptionPlanner
{
    public static BillingV2NewSubscriptionPlan Plan(
        PortalSessionContext session,
        IReadOnlyList<BillingV2NewSubscriptionPresetItem> presetItems)
    {
        var users = new List<BillingV2NewSubscriptionUserPlan>();
        var items = new List<BillingV2NewSubscriptionItemPlan>();
        BillingV2NewSubscriptionUserPlan? primaryUser = null;
        var additionalUserIndex = 0;

        foreach (var presetItem in presetItems)
        {
            var user = ResolveUserForScope(
                session,
                presetItem.ScopeTemplate,
                users,
                ref primaryUser,
                ref additionalUserIndex);
            items.Add(new BillingV2NewSubscriptionItemPlan(
                Guid.NewGuid().ToString("D"),
                user?.Id,
                presetItem.ServiceId,
                presetItem.TierId,
                presetItem.ServicePriceId,
                user is null ? "subscription" : "user",
                presetItem.Quantity,
                presetItem.AmountCents,
                presetItem.Currency,
                presetItem.DiscountEligible,
                "preset"));
        }

        return new BillingV2NewSubscriptionPlan(users, items);
    }

    private static BillingV2NewSubscriptionUserPlan? ResolveUserForScope(
        PortalSessionContext session,
        string scopeTemplate,
        List<BillingV2NewSubscriptionUserPlan> users,
        ref BillingV2NewSubscriptionUserPlan? primaryUser,
        ref int additionalUserIndex)
    {
        if (string.Equals(
                scopeTemplate,
                "subscription",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(
                scopeTemplate,
                "primary_user",
                StringComparison.OrdinalIgnoreCase))
        {
            primaryUser ??= new BillingV2NewSubscriptionUserPlan(
                Guid.NewGuid().ToString("D"),
                session.UserId,
                session.DisplayName,
                session.Email,
                IsPrimary: true);
            if (!users.Contains(primaryUser))
            {
                users.Add(primaryUser);
            }

            return primaryUser;
        }

        if (string.Equals(
                scopeTemplate,
                "additional_user",
                StringComparison.OrdinalIgnoreCase))
        {
            additionalUserIndex++;
            var user = new BillingV2NewSubscriptionUserPlan(
                Guid.NewGuid().ToString("D"),
                IdentityReference: null,
                $"Utilisateur additionnel {additionalUserIndex}",
                Email: null,
                IsPrimary: false);
            users.Add(user);
            return user;
        }

        throw new ArgumentException(
            $"Unsupported Billing V2 preset item scope '{scopeTemplate}'.",
            nameof(scopeTemplate));
    }
}
