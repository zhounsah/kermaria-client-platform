using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MariaDbSubscriptionRepository : ISubscriptionRepository
{
    private readonly string _connectionString;

    public MariaDbSubscriptionRepository(SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<IReadOnlyList<SubscriptionSummary>> GetByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var rows = new List<SubscriptionSummary>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BaseSelect + "\n" + """
            WHERE subscription.customer_id = @customerId
            ORDER BY subscription.updated_at DESC, subscription.id DESC;
            """;
        command.Parameters.AddWithValue("customerId", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(Read(reader));
        }

        return rows;
    }

    public async Task<IReadOnlyList<SubscriptionSummary>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var rows = new List<SubscriptionSummary>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BaseSelect + "\n" + """
            ORDER BY subscription.updated_at DESC, subscription.id DESC;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(Read(reader));
        }

        return rows;
    }

    public async Task<SubscriptionSummary?> GetByIdAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BaseSelect + "\n" + """
            WHERE subscription.id = @id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("id", subscriptionId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Read(reader);
    }

    public async Task<SubscriptionSummary?> GetByExternalIdAsync(
        string rail,
        string externalId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BaseSelect + "\n" + (
            rail == "stripe"
                ? """
                  WHERE subscription.stripe_subscription_id = @externalId
                  LIMIT 1;
                  """
                : """
                  WHERE subscription.paypal_subscription_id = @externalId
                  LIMIT 1;
                  """);
        command.Parameters.AddWithValue("externalId", externalId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Read(reader);
    }

    public async Task<SubscriptionSummary> CreatePendingAsync(
        string customerId,
        CommercialOfferSummary offer,
        string rail,
        string? paypalPlanId,
        string? paypalSubscriptionId,
        string? stripePriceId,
        string? stripeSubscriptionId,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("D");
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO subscriptions (
                id,
                customer_id,
                commercial_offer_id,
                rail,
                paypal_subscription_id,
                paypal_plan_id,
                stripe_subscription_id,
                stripe_price_id,
                status,
                public_pack_code,
                setup_fee_amount_cents,
                billing_interval_months,
                commitment_months,
                payment_mode,
                paid_cycles_count,
                commitment_ends_at,
                cancel_requested_at,
                cancel_at_term_end,
                started_at,
                next_billing_at,
                cancelled_at,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @customerId,
                @commercialOfferId,
                @rail,
                @paypalSubscriptionId,
                @paypalPlanId,
                @stripeSubscriptionId,
                @stripePriceId,
                'pending_approval',
                @publicPackCode,
                @setupFeeAmountCents,
                @billingIntervalMonths,
                @commitmentMonths,
                @paymentMode,
                0,
                NULL,
                NULL,
                0,
                NULL,
                NULL,
                NULL,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("customerId", customerId);
        command.Parameters.AddWithValue("commercialOfferId", offer.Id);
        command.Parameters.AddWithValue("rail", rail);
        command.Parameters.AddWithValue(
            "paypalSubscriptionId",
            (object?)paypalSubscriptionId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "paypalPlanId",
            (object?)paypalPlanId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "stripeSubscriptionId",
            (object?)stripeSubscriptionId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "stripePriceId",
            (object?)stripePriceId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "publicPackCode",
            DbValue(offer.PublicPackCode));
        command.Parameters.AddWithValue(
            "setupFeeAmountCents",
            offer.SetupFeeAmountCents ?? 0);
        command.Parameters.AddWithValue(
            "billingIntervalMonths",
            offer.BillingIntervalMonths ?? 1);
        command.Parameters.AddWithValue(
            "commitmentMonths",
            offer.CommitmentMonths ?? offer.BillingIntervalMonths ?? 1);
        command.Parameters.AddWithValue(
            "paymentMode",
            offer.PaymentMode ?? CommercialStatuses.PaymentModeMonthly);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException(
                "Subscription could not be reloaded after insert.");
    }

    public async Task<SubscriptionSummary> UpdateStatusAsync(
        string subscriptionId,
        string newStatus,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE subscriptions
            SET status = @status,
                updated_at = UTC_TIMESTAMP(6),
                cancel_at_term_end = CASE
                    WHEN @status IN ('cancelled', 'expired') THEN 0
                    ELSE cancel_at_term_end
                END,
                cancelled_at = CASE
                    WHEN @status = 'cancelled' AND cancelled_at IS NULL
                        THEN UTC_TIMESTAMP(6)
                    ELSE cancelled_at
                END
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("id", subscriptionId);
        command.Parameters.AddWithValue("status", newStatus);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            throw new InvalidOperationException(
                $"Subscription {subscriptionId} not found.");
        }

        return await GetByIdAsync(subscriptionId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Subscription could not be reloaded after status update.");
    }

    public async Task<SubscriptionSummary> ActivateAsync(
        string subscriptionId,
        DateTime startedAtUtc,
        DateTime nextBillingAtUtc,
        DateTime commitmentEndsAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE subscriptions
            SET status = 'active',
                started_at = COALESCE(started_at, @startedAt),
                next_billing_at = @nextBillingAt,
                commitment_ends_at = COALESCE(commitment_ends_at, @commitmentEndsAt),
                cancel_requested_at = CASE
                    WHEN status = 'pending_cancellation' THEN cancel_requested_at
                    ELSE NULL
                END,
                cancel_at_term_end = CASE
                    WHEN status = 'pending_cancellation' THEN cancel_at_term_end
                    ELSE 0
                END,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("id", subscriptionId);
        command.Parameters.AddWithValue("startedAt", startedAtUtc);
        command.Parameters.AddWithValue("nextBillingAt", nextBillingAtUtc);
        command.Parameters.AddWithValue("commitmentEndsAt", commitmentEndsAtUtc);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            throw new InvalidOperationException(
                $"Subscription {subscriptionId} not found.");
        }

        return await GetByIdAsync(subscriptionId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Subscription could not be reloaded after activation.");
    }

    public async Task<SubscriptionSummary> RecordPaymentAsync(
        string subscriptionId,
        DateTime nextBillingAtUtc,
        DateTime commitmentEndsAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE subscriptions
            SET status = CASE
                    WHEN status = 'pending_cancellation'
                        THEN 'pending_cancellation'
                    ELSE 'active'
                END,
                paid_cycles_count = COALESCE(paid_cycles_count, 0) + 1,
                next_billing_at = @nextBillingAt,
                commitment_ends_at = @commitmentEndsAt,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("id", subscriptionId);
        command.Parameters.AddWithValue("nextBillingAt", nextBillingAtUtc);
        command.Parameters.AddWithValue("commitmentEndsAt", commitmentEndsAtUtc);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            throw new InvalidOperationException(
                $"Subscription {subscriptionId} not found.");
        }

        return await GetByIdAsync(subscriptionId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Subscription could not be reloaded after payment.");
    }

    public async Task<SubscriptionSummary> RequestCancellationAsync(
        string subscriptionId,
        DateTime requestedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE subscriptions
            SET status = 'pending_cancellation',
                cancel_requested_at = COALESCE(cancel_requested_at, @requestedAt),
                cancel_at_term_end = 1,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("id", subscriptionId);
        command.Parameters.AddWithValue("requestedAt", requestedAtUtc);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            throw new InvalidOperationException(
                $"Subscription {subscriptionId} not found.");
        }

        return await GetByIdAsync(subscriptionId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Subscription could not be reloaded after cancellation request.");
    }

    private async Task<MySqlConnection> OpenAsync(
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private const string BaseSelect =
        """
        SELECT
            subscription.id,
            subscription.customer_id,
            customer.external_reference AS customer_reference,
            customer.display_name AS customer_name,
            subscription.commercial_offer_id,
            offer.name AS offer_name,
            offer.external_reference AS offer_external_reference,
            COALESCE(subscription.public_pack_code, offer.public_pack_code)
                AS public_pack_code,
            subscription.rail,
            subscription.paypal_plan_id,
            subscription.paypal_subscription_id,
            subscription.stripe_price_id,
            subscription.stripe_subscription_id,
            subscription.status,
            offer.price_amount_cents,
            COALESCE(
                subscription.setup_fee_amount_cents,
                offer.setup_fee_amount_cents,
                0
            ) AS setup_fee_amount_cents,
            COALESCE(
                subscription.billing_interval_months,
                offer.billing_interval_months,
                1
            ) AS billing_interval_months,
            COALESCE(
                subscription.commitment_months,
                offer.commitment_months,
                subscription.billing_interval_months,
                offer.billing_interval_months,
                1
            ) AS commitment_months,
            COALESCE(
                subscription.payment_mode,
                offer.payment_mode,
                'monthly'
            ) AS payment_mode,
            COALESCE(subscription.paid_cycles_count, 0) AS paid_cycles_count,
            subscription.commitment_ends_at,
            subscription.cancel_requested_at,
            COALESCE(subscription.cancel_at_term_end, 0) AS cancel_at_term_end,
            offer.currency,
            subscription.started_at,
            subscription.next_billing_at,
            subscription.cancelled_at,
            subscription.created_at,
            subscription.updated_at
        FROM subscriptions subscription
        INNER JOIN customers customer
            ON customer.id = subscription.customer_id
        INNER JOIN commercial_offers offer
            ON offer.id = subscription.commercial_offer_id
        """;

    private static SubscriptionSummary Read(MySqlDataReader reader)
        => new(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            reader.GetString("customer_reference"),
            reader.GetString("customer_name"),
            MariaDbIdentifierReader.ReadRequired(reader, "commercial_offer_id"),
            reader.GetString("offer_name"),
            ReadNullableString(reader, "offer_external_reference"),
            ReadNullableString(reader, "public_pack_code"),
            reader.GetString("rail"),
            ReadNullableString(reader, "paypal_plan_id"),
            ReadNullableString(reader, "paypal_subscription_id"),
            ReadNullableString(reader, "stripe_price_id"),
            ReadNullableString(reader, "stripe_subscription_id"),
            reader.GetString("status"),
            reader.GetInt32("price_amount_cents"),
            reader.GetInt32("setup_fee_amount_cents"),
            reader.GetInt32("billing_interval_months"),
            reader.GetInt32("commitment_months"),
            reader.GetString("payment_mode"),
            reader.GetInt32("paid_cycles_count"),
            ReadNullableIso(reader, "commitment_ends_at"),
            ReadNullableIso(reader, "cancel_requested_at"),
            reader.GetBoolean("cancel_at_term_end"),
            reader.GetString("currency"),
            ReadNullableIso(reader, "started_at"),
            ReadNullableIso(reader, "next_billing_at"),
            ReadNullableIso(reader, "cancelled_at"),
            ToIso(reader.GetDateTime("created_at")),
            ToIso(reader.GetDateTime("updated_at")));

    private static string? ReadNullableString(
        MySqlDataReader reader,
        string columnName)
        => reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(columnName);

    private static string? ReadNullableIso(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column))
            ? null
            : ToIso(reader.GetDateTime(column));

    private static string ToIso(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;
}
