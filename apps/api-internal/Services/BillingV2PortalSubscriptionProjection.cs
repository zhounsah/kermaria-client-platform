using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface IBillingV2PortalSubscriptionProjection
{
    Task<IReadOnlyList<SubscriptionSummary>> GetClientSubscriptionsAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SubscriptionSummary>> GetAdminSubscriptionsAsync(
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2PortalSubscriptionProjection
    : IBillingV2PortalSubscriptionProjection
{
    public static NoOpBillingV2PortalSubscriptionProjection Instance { get; } =
        new();

    private NoOpBillingV2PortalSubscriptionProjection()
    {
    }

    public Task<IReadOnlyList<SubscriptionSummary>> GetClientSubscriptionsAsync(
        string customerId,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SubscriptionSummary>>(
            Array.Empty<SubscriptionSummary>());

    public Task<IReadOnlyList<SubscriptionSummary>> GetAdminSubscriptionsAsync(
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SubscriptionSummary>>(
            Array.Empty<SubscriptionSummary>());
}

public sealed class BillingV2PortalSubscriptionProjection
    : IBillingV2PortalSubscriptionProjection
{
    private static readonly IFiscalPolicy FiscalPolicy = new FiscalPolicy();
    private readonly string _connectionString;

    public BillingV2PortalSubscriptionProjection(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public async Task<IReadOnlyList<SubscriptionSummary>>
        GetClientSubscriptionsAsync(
            string customerId,
            CancellationToken cancellationToken)
    {
        var rows = new List<SubscriptionSummary>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(BillingV2PortalSubscriptionProjector.Project(Read(reader)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<SubscriptionSummary>>
        GetAdminSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var rows = new List<SubscriptionSummary>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = AdminSelectSql;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(BillingV2PortalSubscriptionProjector.Project(Read(reader)));
        }

        return rows;
    }

    private const string SelectSql =
        """
        SELECT
            subscription.id,
            subscription.customer_id,
            customer.external_reference AS customer_reference,
            customer.display_name AS customer_name,
            subscription.originating_preset_id,
            request.legacy_offer_id,
            preset.name AS preset_name,
            -- La colonne s'appelle `code` dans billing_v2_offer_presets ;
            -- `preset_code` est l'alias attendu par la projection.
            preset.code AS preset_code,
            offer.external_reference AS offer_external_reference,
            offer.public_pack_code,
            COALESCE(agreement.provider, checkout.provider, request.provider, 'billing')
                AS provider,
            COALESCE(
                agreement.provider_subscription_id,
                checkout.provider_subscription_id
            ) AS provider_subscription_id,
            subscription.status,
            subscription.payment_mode,
            subscription.currency,
            subscription.discount_basis_points_snapshot,
            subscription.minimum_commitment_amount_cents,
            subscription.pricing_authority,
            term.commitment_months,
            active_lock.amount_cents AS active_lock_amount_cents,
            active_lock.lock_type AS active_lock_type,
            COALESCE(totals.recurring_discount_eligible_cents, 0)
                AS recurring_discount_eligible_cents,
            COALESCE(totals.recurring_non_discountable_cents, 0)
                AS recurring_non_discountable_cents,
            COALESCE(totals.one_time_cents, 0) AS one_time_cents,
            totals.tax_rate_basis_points,
            subscription.started_at,
            subscription.renews_at,
            subscription.current_period_ends_at,
            subscription.commitment_ends_at,
            subscription.cancellation_requested_at,
            subscription.cancel_at_period_end,
            -- Compte des cycles reellement regles, source V2 uniquement.
            (
                SELECT COUNT(*)
                FROM billing_v2_billing_events settled_event
                WHERE settled_event.subscription_id = subscription.id
                  AND settled_event.settlement_status = 'settled'
            ) AS paid_cycles_count,
            -- Compteurs de places USER-ADDITIONAL reellement administrables :
            -- meme definition que la lecture produit et que la politique
            -- d'attribution, droit contractuel actif compris. Sous-requetes
            -- independantes et volontairement pas une jointure : joindre les
            -- places au calcul financier multiplierait les lignes d'items et
            -- fausserait les montants.
            (
                SELECT COUNT(*)
                FROM billing_v2_subscription_users slot
                WHERE slot.subscription_id = subscription.id
        """
        + MariaDbBillingV2AdditionalUserIdentityRepository
            .AdministrableSlotPredicate
        + """
                  AND EXISTS (
                        SELECT 1
        """
        + MariaDbBillingV2AdditionalUserIdentityRepository
            .UserSlotEntitlementSource
        + """
                          AND item.subscription_user_id = slot.id
                          AND item.subscription_id = slot.subscription_id
                  )
            ) AS additional_user_slots_count,
            (
                SELECT COUNT(*)
                FROM billing_v2_subscription_users slot
                WHERE slot.subscription_id = subscription.id
                  AND slot.identity_reference IS NOT NULL
        """
        + MariaDbBillingV2AdditionalUserIdentityRepository
            .AdministrableSlotPredicate
        + """
                  AND EXISTS (
                        SELECT 1
        """
        + MariaDbBillingV2AdditionalUserIdentityRepository
            .UserSlotEntitlementSource
        + """
                          AND item.subscription_user_id = slot.id
                          AND item.subscription_id = slot.subscription_id
                  )
            ) AS assigned_additional_users_count,
            subscription.created_at,
            subscription.updated_at
        """
        + "\n"
        + """
        FROM billing_v2_subscriptions subscription
        INNER JOIN customers customer
            ON customer.id = subscription.customer_id
        LEFT JOIN billing_v2_offer_presets preset
            ON preset.id = subscription.originating_preset_id
        LEFT JOIN billing_v2_commitment_terms term
            ON term.id = subscription.commitment_term_id
        INNER JOIN (
            SELECT selected.subscription_id,
                   selected.legacy_offer_id,
                   selected.provider,
                   selected.environment
            FROM billing_v2_authoritative_checkout_requests selected
            INNER JOIN (
                SELECT subscription_id, MAX(created_at) AS created_at
                FROM billing_v2_authoritative_checkout_requests
                GROUP BY subscription_id
            ) latest
                ON latest.subscription_id = selected.subscription_id
               AND latest.created_at = selected.created_at
        ) request
            ON request.subscription_id = subscription.id
        LEFT JOIN commercial_offers offer
            ON offer.id = request.legacy_offer_id
        LEFT JOIN (
            SELECT item.subscription_id,
                   SUM(CASE
                       WHEN component.billing_cadence = 'monthly'
                        AND component.discount_eligible_snapshot <> 0
                           THEN component.amount_cents_snapshot * item.quantity
                       ELSE 0
                   END) AS recurring_discount_eligible_cents,
                   SUM(CASE
                       WHEN component.billing_cadence = 'monthly'
                        AND component.discount_eligible_snapshot = 0
                           THEN component.amount_cents_snapshot * item.quantity
                       ELSE 0
                   END) AS recurring_non_discountable_cents,
                   SUM(CASE
                       WHEN component.billing_cadence = 'one_time'
                           THEN component.amount_cents_snapshot * item.quantity
                       ELSE 0
                   END) AS one_time_cents,
                   MAX(price.tax_rate_basis_points) AS tax_rate_basis_points
            FROM billing_v2_subscription_items item
            INNER JOIN billing_v2_subscription_item_effective_price_components component
                ON component.subscription_item_id = item.id
            INNER JOIN billing_v2_service_prices price
                ON price.id = component.service_price_id
            WHERE item.status = 'active'
              AND item.effective_from <= UTC_TIMESTAMP(6)
              AND (
                    item.effective_until IS NULL
                    OR item.effective_until > UTC_TIMESTAMP(6)
                  )
              AND component.status = 'active'
              AND component.effective_from <= UTC_TIMESTAMP(6)
              AND (component.effective_until IS NULL
                   OR component.effective_until > UTC_TIMESTAMP(6))
            GROUP BY item.subscription_id
        ) totals
            ON totals.subscription_id = subscription.id
        LEFT JOIN (
            SELECT price_lock.subscription_id,
                   price_lock.amount_cents,
                   price_lock.lock_type
            FROM billing_v2_subscription_price_locks price_lock
            INNER JOIN (
                SELECT subscription_id, MAX(effective_from) AS effective_from
                FROM billing_v2_subscription_price_locks
                WHERE status = 'active'
                  AND effective_from <= UTC_TIMESTAMP(6)
                  AND effective_until > UTC_TIMESTAMP(6)
                GROUP BY subscription_id
            ) latest_lock
                ON latest_lock.subscription_id = price_lock.subscription_id
               AND latest_lock.effective_from = price_lock.effective_from
            WHERE price_lock.status = 'active'
              AND price_lock.effective_from <= UTC_TIMESTAMP(6)
              AND price_lock.effective_until > UTC_TIMESTAMP(6)
        ) active_lock
            ON active_lock.subscription_id = subscription.id
        LEFT JOIN (
            SELECT subscription_id,
                   MAX(provider) AS provider,
                   MAX(provider_subscription_id) AS provider_subscription_id
            FROM billing_v2_payment_agreements
            WHERE provider_subscription_id IS NOT NULL
              AND status IN ('pending', 'active', 'past_due')
            GROUP BY subscription_id
        ) agreement
            ON agreement.subscription_id = subscription.id
        LEFT JOIN (
            SELECT subscription_id,
                   MAX(provider) AS provider,
                   MAX(provider_subscription_id) AS provider_subscription_id
            FROM billing_v2_provider_checkout_sessions
            WHERE provider_subscription_id IS NOT NULL
            GROUP BY subscription_id
        ) checkout
            ON checkout.subscription_id = subscription.id
        WHERE subscription.customer_id = @customer_id
          AND NOT EXISTS (
              SELECT 1
              FROM subscriptions legacy_subscription
              WHERE legacy_subscription.id = subscription.id
          )
        ORDER BY subscription.updated_at DESC, subscription.id DESC;
        """;

    private const string AdminSelectSql =
        """
        SELECT
            subscription.id,
            subscription.customer_id,
            customer.external_reference AS customer_reference,
            customer.display_name AS customer_name,
            subscription.originating_preset_id,
            request.legacy_offer_id,
            preset.name AS preset_name,
            -- La colonne s'appelle `code` dans billing_v2_offer_presets ;
            -- `preset_code` est l'alias attendu par la projection.
            preset.code AS preset_code,
            offer.external_reference AS offer_external_reference,
            offer.public_pack_code,
            COALESCE(agreement.provider, checkout.provider, request.provider, 'billing')
                AS provider,
            COALESCE(
                agreement.provider_subscription_id,
                checkout.provider_subscription_id
            ) AS provider_subscription_id,
            subscription.status,
            subscription.payment_mode,
            subscription.currency,
            subscription.discount_basis_points_snapshot,
            subscription.minimum_commitment_amount_cents,
            subscription.pricing_authority,
            term.commitment_months,
            active_lock.amount_cents AS active_lock_amount_cents,
            active_lock.lock_type AS active_lock_type,
            COALESCE(totals.recurring_discount_eligible_cents, 0)
                AS recurring_discount_eligible_cents,
            COALESCE(totals.recurring_non_discountable_cents, 0)
                AS recurring_non_discountable_cents,
            COALESCE(totals.one_time_cents, 0) AS one_time_cents,
            totals.tax_rate_basis_points,
            subscription.started_at,
            subscription.renews_at,
            subscription.current_period_ends_at,
            subscription.commitment_ends_at,
            subscription.cancellation_requested_at,
            subscription.cancel_at_period_end,
            -- Compte des cycles reellement regles, source V2 uniquement.
            (
                SELECT COUNT(*)
                FROM billing_v2_billing_events settled_event
                WHERE settled_event.subscription_id = subscription.id
                  AND settled_event.settlement_status = 'settled'
            ) AS paid_cycles_count,
            -- Compteurs de places USER-ADDITIONAL reellement administrables :
            -- meme definition que la lecture produit et que la politique
            -- d'attribution, droit contractuel actif compris. Sous-requetes
            -- independantes et volontairement pas une jointure : joindre les
            -- places au calcul financier multiplierait les lignes d'items et
            -- fausserait les montants.
            (
                SELECT COUNT(*)
                FROM billing_v2_subscription_users slot
                WHERE slot.subscription_id = subscription.id
        """
        + MariaDbBillingV2AdditionalUserIdentityRepository
            .AdministrableSlotPredicate
        + """
                  AND EXISTS (
                        SELECT 1
        """
        + MariaDbBillingV2AdditionalUserIdentityRepository
            .UserSlotEntitlementSource
        + """
                          AND item.subscription_user_id = slot.id
                          AND item.subscription_id = slot.subscription_id
                  )
            ) AS additional_user_slots_count,
            (
                SELECT COUNT(*)
                FROM billing_v2_subscription_users slot
                WHERE slot.subscription_id = subscription.id
                  AND slot.identity_reference IS NOT NULL
        """
        + MariaDbBillingV2AdditionalUserIdentityRepository
            .AdministrableSlotPredicate
        + """
                  AND EXISTS (
                        SELECT 1
        """
        + MariaDbBillingV2AdditionalUserIdentityRepository
            .UserSlotEntitlementSource
        + """
                          AND item.subscription_user_id = slot.id
                          AND item.subscription_id = slot.subscription_id
                  )
            ) AS assigned_additional_users_count,
            subscription.created_at,
            subscription.updated_at
        """
        + "\n"
        + """
        FROM billing_v2_subscriptions subscription
        INNER JOIN customers customer
            ON customer.id = subscription.customer_id
        LEFT JOIN billing_v2_offer_presets preset
            ON preset.id = subscription.originating_preset_id
        LEFT JOIN billing_v2_commitment_terms term
            ON term.id = subscription.commitment_term_id
        INNER JOIN (
            SELECT selected.subscription_id,
                   selected.legacy_offer_id,
                   selected.provider,
                   selected.environment
            FROM billing_v2_authoritative_checkout_requests selected
            INNER JOIN (
                SELECT subscription_id, MAX(created_at) AS created_at
                FROM billing_v2_authoritative_checkout_requests
                GROUP BY subscription_id
            ) latest
                ON latest.subscription_id = selected.subscription_id
               AND latest.created_at = selected.created_at
        ) request
            ON request.subscription_id = subscription.id
        LEFT JOIN commercial_offers offer
            ON offer.id = request.legacy_offer_id
        LEFT JOIN (
            SELECT item.subscription_id,
                   SUM(CASE
                       WHEN component.billing_cadence = 'monthly'
                        AND component.discount_eligible_snapshot <> 0
                           THEN component.amount_cents_snapshot * item.quantity
                       ELSE 0
                   END) AS recurring_discount_eligible_cents,
                   SUM(CASE
                       WHEN component.billing_cadence = 'monthly'
                        AND component.discount_eligible_snapshot = 0
                           THEN component.amount_cents_snapshot * item.quantity
                       ELSE 0
                   END) AS recurring_non_discountable_cents,
                   SUM(CASE
                       WHEN component.billing_cadence = 'one_time'
                           THEN component.amount_cents_snapshot * item.quantity
                       ELSE 0
                   END) AS one_time_cents,
                   MAX(price.tax_rate_basis_points) AS tax_rate_basis_points
            FROM billing_v2_subscription_items item
            INNER JOIN billing_v2_subscription_item_effective_price_components component
                ON component.subscription_item_id = item.id
            INNER JOIN billing_v2_service_prices price
                ON price.id = component.service_price_id
            WHERE item.status = 'active'
              AND item.effective_from <= UTC_TIMESTAMP(6)
              AND (
                    item.effective_until IS NULL
                  OR item.effective_until > UTC_TIMESTAMP(6)
                  )
              AND component.status = 'active'
              AND component.effective_from <= UTC_TIMESTAMP(6)
              AND (component.effective_until IS NULL
                   OR component.effective_until > UTC_TIMESTAMP(6))
            GROUP BY item.subscription_id
        ) totals
            ON totals.subscription_id = subscription.id
        LEFT JOIN (
            SELECT price_lock.subscription_id,
                   price_lock.amount_cents,
                   price_lock.lock_type
            FROM billing_v2_subscription_price_locks price_lock
            INNER JOIN (
                SELECT subscription_id, MAX(effective_from) AS effective_from
                FROM billing_v2_subscription_price_locks
                WHERE status = 'active'
                  AND effective_from <= UTC_TIMESTAMP(6)
                  AND effective_until > UTC_TIMESTAMP(6)
                GROUP BY subscription_id
            ) latest_lock
                ON latest_lock.subscription_id = price_lock.subscription_id
               AND latest_lock.effective_from = price_lock.effective_from
            WHERE price_lock.status = 'active'
              AND price_lock.effective_from <= UTC_TIMESTAMP(6)
              AND price_lock.effective_until > UTC_TIMESTAMP(6)
        ) active_lock
            ON active_lock.subscription_id = subscription.id
        LEFT JOIN (
            SELECT subscription_id,
                   MAX(provider) AS provider,
                   MAX(provider_subscription_id) AS provider_subscription_id
            FROM billing_v2_payment_agreements
            WHERE provider_subscription_id IS NOT NULL
              AND status IN ('pending', 'active', 'past_due')
            GROUP BY subscription_id
        ) agreement
            ON agreement.subscription_id = subscription.id
        LEFT JOIN (
            SELECT subscription_id,
                   MAX(provider) AS provider,
                   MAX(provider_subscription_id) AS provider_subscription_id
            FROM billing_v2_provider_checkout_sessions
            WHERE provider_subscription_id IS NOT NULL
            GROUP BY subscription_id
        ) checkout
            ON checkout.subscription_id = subscription.id
        WHERE NOT EXISTS (
              SELECT 1
              FROM subscriptions legacy_subscription
              WHERE legacy_subscription.id = subscription.id
          )
        ORDER BY subscription.updated_at DESC, subscription.id DESC;
        """;

    private static BillingV2PortalSubscriptionRow Read(MySqlDataReader reader)
        => new(
            ReadRequiredString(reader, "id"),
            ReadRequiredString(reader, "customer_id"),
            ReadRequiredString(reader, "customer_reference"),
            ReadRequiredString(reader, "customer_name"),
            ReadNullableString(reader, "originating_preset_id"),
            ReadNullableString(reader, "legacy_offer_id"),
            ReadNullableString(reader, "preset_name")
                ?? "Souscription Billing V2",
            ReadNullableString(reader, "preset_code"),
            ReadNullableString(reader, "offer_external_reference"),
            ReadNullableString(reader, "public_pack_code"),
            ReadRequiredString(reader, "provider"),
            ReadNullableString(reader, "provider_subscription_id"),
            ReadRequiredString(reader, "status"),
            ReadRequiredString(reader, "payment_mode"),
            ReadRequiredString(reader, "currency"),
            reader.GetInt32("discount_basis_points_snapshot"),
            ReadNullableInt64(reader, "minimum_commitment_amount_cents"),
            ReadRequiredString(reader, "pricing_authority"),
            reader.IsDBNull(reader.GetOrdinal("commitment_months"))
                ? 1
                : reader.GetInt32("commitment_months"),
            ReadNullableInt64(reader, "active_lock_amount_cents"),
            ReadNullableString(reader, "active_lock_type"),
            reader.GetInt64("recurring_discount_eligible_cents"),
            reader.GetInt64("recurring_non_discountable_cents"),
            reader.GetInt64("one_time_cents"),
            reader.IsDBNull(reader.GetOrdinal("tax_rate_basis_points"))
                ? (int?)null
                : reader.GetInt32("tax_rate_basis_points"),
            ReadNullableUtc(reader, "started_at"),
            ReadNullableUtc(reader, "renews_at"),
            ReadNullableUtc(reader, "current_period_ends_at"),
            ReadNullableUtc(reader, "commitment_ends_at"),
            ReadNullableUtc(reader, "cancellation_requested_at"),
            reader.GetBoolean("cancel_at_period_end"),
            reader.GetDateTime("created_at"),
            reader.GetDateTime("updated_at"),
            reader.GetInt32("paid_cycles_count"),
            reader.GetInt32("additional_user_slots_count"),
            reader.GetInt32("assigned_additional_users_count"));

    private static string ReadRequiredString(
        MySqlDataReader reader,
        string columnName)
        => MariaDbIdentifierReader.ReadRequired(reader, columnName);

    // MySqlConnector materialise les colonnes CHAR(36) en Guid : lire ces
    // identifiants avec GetString leve InvalidCastException en MariaDB reelle,
    // faute structurellement invisible aux suites en persistance mock.
    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => MariaDbIdentifierReader.ReadNullable(reader, columnName);

    private static long? ReadNullableInt64(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetInt64(columnName);

    private static DateTime? ReadNullableUtc(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : DateTime.SpecifyKind(reader.GetDateTime(columnName), DateTimeKind.Utc);
}

public sealed record BillingV2PortalSubscriptionRow(
    string Id,
    string CustomerId,
    string CustomerReference,
    string CustomerName,
    string? OriginatingPresetId,
    string? LegacyOfferId,
    string PresetName,
    string? PresetCode,
    string? OfferExternalReference,
    string? PublicPackCode,
    string Provider,
    string? ProviderSubscriptionId,
    string Status,
    string PaymentMode,
    string Currency,
    int DiscountBasisPointsSnapshot,
    long? MinimumCommitmentAmountCents,
    string PricingAuthority,
    int CommitmentMonths,
    long? ActiveLockAmountCents,
    string? ActiveLockType,
    long RecurringDiscountEligibleCents,
    long RecurringNonDiscountableCents,
    long OneTimeCents,
    int? TaxRateBasisPoints,
    DateTime? StartedAtUtc,
    DateTime? RenewsAtUtc,
    DateTime? CurrentPeriodEndsAtUtc,
    DateTime? CommitmentEndsAtUtc,
    DateTime? CancellationRequestedAtUtc,
    bool CancelAtPeriodEnd,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    // Cycles REELLEMENT regles, comptes sur les BillingEvents V2. Le compteur
    // legacy `subscriptions.paid_cycles_count` n'est pas alimente par le rail
    // V2 : le portail affichait donc 0 malgre un cycle encaisse.
    int PaidCyclesCount = 0,
    int AdditionalUserSlotsCount = 0,
    int AssignedAdditionalUsersCount = 0);

public static class BillingV2PortalSubscriptionProjector
{
    private const int BasisPointDenominator = 10000;
    private static readonly IFiscalPolicy FiscalPolicy = new FiscalPolicy();

    public static SubscriptionSummary Project(BillingV2PortalSubscriptionRow row)
    {
        var fiscal = FiscalPolicy.Resolve(row.TaxRateBasisPoints);
        var provider = NormalizeProvider(row.Provider);
        var priceAmountCents = ToInt32(ResolvePriceAmountCents(row));
        var setupFeeAmountCents = ToInt32(row.OneTimeCents);

        return new SubscriptionSummary(
            row.Id,
            row.CustomerId,
            row.CustomerReference,
            row.CustomerName,
            row.LegacyOfferId ?? row.OriginatingPresetId ?? row.Id,
            row.PresetName,
            row.OfferExternalReference ?? row.PresetCode,
            row.PublicPackCode,
            provider,
            null,
            provider == "paypal" ? row.ProviderSubscriptionId : null,
            null,
            provider == "stripe" ? row.ProviderSubscriptionId : null,
            ResolveStatus(row),
            priceAmountCents,
            setupFeeAmountCents,
            fiscal.TaxRateBasisPoints,
            fiscal.FiscalRegime,
            fiscal.FiscalMention,
            ResolveBillingIntervalMonths(row),
            Math.Max(row.CommitmentMonths, 1),
            NormalizePaymentMode(row.PaymentMode),
            row.PaidCyclesCount,
            ToIso(row.CommitmentEndsAtUtc),
            ToIso(row.CancellationRequestedAtUtc),
            row.CancelAtPeriodEnd,
            string.IsNullOrWhiteSpace(row.Currency) ? "EUR" : row.Currency,
            ToIso(row.StartedAtUtc),
            ToIso(ResolveNextBillingAt(row)),
            ResolveStatus(row) == "cancelled"
                ? ToIso(row.UpdatedAtUtc)
                : null,
            ToIso(row.CreatedAtUtc),
            ToIso(row.UpdatedAtUtc),
            "billing_v2",
            row.AdditionalUserSlotsCount,
            row.AssignedAdditionalUsersCount);
    }

    private static long ResolvePriceAmountCents(
        BillingV2PortalSubscriptionRow row)
    {
        if (row.ActiveLockAmountCents.HasValue
            && !string.Equals(
                row.PricingAuthority,
                "item_snapshots",
                StringComparison.Ordinal))
        {
            return row.ActiveLockAmountCents.Value;
        }

        var discountedEligible = MultiplyBasisPoints(
            row.RecurringDiscountEligibleCents,
            BasisPointDenominator - row.DiscountBasisPointsSnapshot);
        var recurringAfterDiscount = checked(
            discountedEligible + row.RecurringNonDiscountableCents);

        if (string.Equals(
                row.PaymentMode,
                BillingV2PaymentModes.Upfront,
                StringComparison.Ordinal))
        {
            return checked(recurringAfterDiscount * Math.Max(row.CommitmentMonths, 1));
        }

        return row.MinimumCommitmentAmountCents.HasValue
            ? Math.Max(recurringAfterDiscount, row.MinimumCommitmentAmountCents.Value)
            : recurringAfterDiscount;
    }

    private static int ResolveBillingIntervalMonths(
        BillingV2PortalSubscriptionRow row)
        => string.Equals(
            row.PaymentMode,
            BillingV2PaymentModes.Upfront,
            StringComparison.Ordinal)
                ? Math.Max(row.CommitmentMonths, 1)
                : 1;

    private static string NormalizeProvider(string provider)
        => provider switch
        {
            "paypal" => "paypal",
            "stripe" => "stripe",
            _ => "billing"
        };

    private static string NormalizePaymentMode(string paymentMode)
        => paymentMode switch
        {
            BillingV2PaymentModes.Upfront => BillingV2PaymentModes.Upfront,
            _ => BillingV2PaymentModes.Monthly
        };

    /// <summary>
    /// Prochaine facturation presentee au portail.
    ///
    /// Un contrat comptant n'en a aucune : le terme est deja paye et aucun
    /// renouvellement automatique n'est prevu. La date de fin contractuelle
    /// reste exposee par <c>commitmentEndsAt</c>, ce qui permet d'afficher
    /// « contrat actif jusqu'au ... » sans jamais annoncer un prelevement qui
    /// n'aura pas lieu. Retomber sur la fin de periode courante, comme le
    /// faisait ce calcul, transformait le terme d'un contrat prepaye en
    /// promesse de facturation.
    ///
    /// Le mensuel est inchange : il porte une vraie date de renouvellement, et
    /// la retombee sur la fin de periode couvre les lignes anterieures a la
    /// migration 061 ou <c>renews_at</c> n'etait pas encore alimente.
    /// </summary>
    private static DateTime? ResolveNextBillingAt(
        BillingV2PortalSubscriptionRow row)
        => string.Equals(
            NormalizePaymentMode(row.PaymentMode),
            BillingV2PaymentModes.Upfront,
            StringComparison.Ordinal)
            ? row.RenewsAtUtc
            : row.RenewsAtUtc ?? row.CurrentPeriodEndsAtUtc;

    /// <summary>
    /// Statut presente au portail. Un contrat comptant arrive a terme garde le
    /// statut 'active' en base : aucun renouvellement automatique n'existe pour
    /// le basculer. Le reconnaitre expire ici, en lecture, evite d'inventer une
    /// machine d'etats et de promettre un acces sans limite. L'etat 'expired'
    /// fait deja partie du contrat portail.
    ///
    /// Un contrat mensuel porte une date de renouvellement : il n'est jamais
    /// concerne par cette derivation.
    /// </summary>
    private static string ResolveStatus(BillingV2PortalSubscriptionRow row)
    {
        var normalized = NormalizeStatus(row.Status);
        if (normalized != "active"
            || row.RenewsAtUtc is not null
            || row.CommitmentEndsAtUtc is null)
        {
            return normalized;
        }

        return row.CommitmentEndsAtUtc.Value <= DateTime.UtcNow
            ? "expired"
            : normalized;
    }

    private static string NormalizeStatus(string status)
        => status switch
        {
            "active" => "active",
            "pending_approval" => "pending_approval",
            "pending_activation" => "pending_activation",
            "pending_cancellation" => "pending_cancellation",
            "cancelled" => "cancelled",
            "expired" => "expired",
            "past_due" => "suspended",
            "suspended" => "suspended",
            _ => "pending_approval"
        };

    private static long MultiplyBasisPoints(long amountCents, int basisPoints)
        => checked((amountCents * basisPoints + 5000L) / BasisPointDenominator);

    private static int ToInt32(long value)
        => checked((int)value);

    private static string? ToIso(DateTime? value)
        => value.HasValue ? ToIso(value.Value) : null;

    private static string ToIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
}
