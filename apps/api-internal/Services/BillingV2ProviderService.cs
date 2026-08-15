using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2PaymentAgreementPlan(
    string Provider,
    string Environment,
    string ProviderSubscriptionId,
    string Status);

public sealed record BillingV2ProviderPriceMapping(
    string ServicePriceId,
    string Provider,
    string Environment,
    string ProviderExternalId);

public sealed record BillingV2ProviderPriceMappingStatus(
    bool Ready,
    IReadOnlyList<string> MissingServicePriceIds,
    IReadOnlyList<string> AmbiguousServicePriceIds,
    IReadOnlyList<BillingV2ProviderPriceMapping> ResolvedMappings);

public interface IBillingV2ProviderAgreementService
{
    Task RecordFromLegacySubscriptionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        SubscriptionSummary legacySubscription,
        DateTime now,
        CancellationToken cancellationToken);

    Task<BillingV2ProviderPriceMappingStatus> VerifyPriceMappingsReadyAsync(
        IReadOnlyList<string> servicePriceIds,
        string provider,
        string environment,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2ProviderAgreementService
    : IBillingV2ProviderAgreementService
{
    public static NoOpBillingV2ProviderAgreementService Instance { get; }
        = new();

    private NoOpBillingV2ProviderAgreementService()
    {
    }

    public Task RecordFromLegacySubscriptionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        SubscriptionSummary legacySubscription,
        DateTime now,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<BillingV2ProviderPriceMappingStatus> VerifyPriceMappingsReadyAsync(
        IReadOnlyList<string> servicePriceIds,
        string provider,
        string environment,
        CancellationToken cancellationToken)
        => Task.FromResult(
            BillingV2ProviderPriceMappingGate.Evaluate(
                servicePriceIds,
                Array.Empty<BillingV2ProviderPriceMapping>(),
                provider,
                environment));
}

public sealed class BillingV2ProviderAgreementService
    : IBillingV2ProviderAgreementService
{
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly SqlRuntimeConfiguration _sql;

    public BillingV2ProviderAgreementService(
        SqlRuntimeConfiguration sql,
        PayPalRuntimeConfiguration paypal,
        StripeRuntimeConfiguration stripe)
    {
        _sql = sql;
        _paypal = paypal;
        _stripe = stripe;
    }

    public async Task RecordFromLegacySubscriptionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        SubscriptionSummary legacySubscription,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var agreement = BillingV2ProviderAgreementPlanner.PlanFromLegacy(
            legacySubscription,
            _paypal,
            _stripe);
        if (agreement is null)
        {
            return;
        }

        await InsertPaymentAgreementAsync(
            connection,
            transaction,
            subscriptionId,
            agreement,
            now,
            cancellationToken);
    }

    public async Task<BillingV2ProviderPriceMappingStatus>
        VerifyPriceMappingsReadyAsync(
            IReadOnlyList<string> servicePriceIds,
            string provider,
            string environment,
            CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return BillingV2ProviderPriceMappingGate.Evaluate(
                servicePriceIds,
                Array.Empty<BillingV2ProviderPriceMapping>(),
                provider,
                environment);
        }

        var mappings = new List<BillingV2ProviderPriceMapping>();
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                service_price_id,
                provider,
                environment,
                CASE
                    WHEN provider = 'stripe' THEN external_price_id
                    WHEN provider = 'paypal' THEN external_plan_id
                    ELSE COALESCE(external_price_id, external_plan_id)
                END AS provider_external_id
            FROM billing_v2_provider_price_mappings
            WHERE provider = @provider
              AND environment = @environment
              AND status = 'active';
            """;
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@environment", environment);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            mappings.Add(new BillingV2ProviderPriceMapping(
                reader.GetString("service_price_id"),
                reader.GetString("provider"),
                reader.GetString("environment"),
                reader.GetString("provider_external_id")));
        }

        return BillingV2ProviderPriceMappingGate.Evaluate(
            servicePriceIds,
            mappings,
            provider,
            environment);
    }

    private static async Task InsertPaymentAgreementAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        BillingV2PaymentAgreementPlan agreement,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO billing_v2_payment_agreements (
                id,
                subscription_id,
                provider,
                environment,
                provider_subscription_id,
                status,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @subscription_id,
                @provider,
                @environment,
                @provider_subscription_id,
                @status,
                @created_at,
                @updated_at
            )
            ON DUPLICATE KEY UPDATE
                provider_subscription_id = provider_subscription_id,
                updated_at = updated_at;
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue("@provider", agreement.Provider);
        command.Parameters.AddWithValue("@environment", agreement.Environment);
        command.Parameters.AddWithValue(
            "@provider_subscription_id",
            agreement.ProviderSubscriptionId);
        command.Parameters.AddWithValue("@status", agreement.Status);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsurePaymentAgreementIdempotencyAsync(
            connection,
            transaction,
            subscriptionId,
            agreement,
            cancellationToken);
    }

    private static async Task EnsurePaymentAgreementIdempotencyAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string subscriptionId,
        BillingV2PaymentAgreementPlan agreement,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT subscription_id, provider_subscription_id
            FROM billing_v2_payment_agreements
            WHERE provider = @provider
              AND environment = @environment
              AND (
                    subscription_id = @subscription_id
                 OR provider_subscription_id = @provider_subscription_id
              );
            """;
        command.Parameters.AddWithValue("@provider", agreement.Provider);
        command.Parameters.AddWithValue("@environment", agreement.Environment);
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        command.Parameters.AddWithValue(
            "@provider_subscription_id",
            agreement.ProviderSubscriptionId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var existingSubscriptionId = reader.GetString("subscription_id");
            var providerSubscriptionIdOrdinal = reader.GetOrdinal(
                "provider_subscription_id");
            var existingProviderSubscriptionId = reader.IsDBNull(
                    providerSubscriptionIdOrdinal)
                ? null
                : reader.GetString(providerSubscriptionIdOrdinal);
            if (!string.Equals(
                    existingSubscriptionId,
                    subscriptionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    existingProviderSubscriptionId,
                    agreement.ProviderSubscriptionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Un abonnement fournisseur V2 est deja associe a un autre contrat local.");
            }
        }
    }
}

public static class BillingV2ProviderAgreementPlanner
{
    public static BillingV2PaymentAgreementPlan? PlanFromLegacy(
        SubscriptionSummary legacySubscription,
        PayPalRuntimeConfiguration paypal,
        StripeRuntimeConfiguration stripe)
    {
        if (string.Equals(
                legacySubscription.Rail,
                "paypal",
                StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(
                legacySubscription.PayPalSubscriptionId)
                ? null
                : new BillingV2PaymentAgreementPlan(
                    "paypal",
                    paypal.ModeName,
                    legacySubscription.PayPalSubscriptionId,
                    "pending");
        }

        if (string.Equals(
                legacySubscription.Rail,
                "stripe",
                StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(
                legacySubscription.StripeSubscriptionId)
                ? null
                : new BillingV2PaymentAgreementPlan(
                    "stripe",
                    stripe.ModeName,
                    legacySubscription.StripeSubscriptionId,
                    "pending");
        }

        return null;
    }
}

public static class BillingV2ProviderPriceMappingGate
{
    public static BillingV2ProviderPriceMappingStatus Evaluate(
        IReadOnlyList<string> requiredServicePriceIds,
        IReadOnlyList<BillingV2ProviderPriceMapping> mappings,
        string provider,
        string environment)
    {
        var required = requiredServicePriceIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var activeMappings = mappings
            .Where(mapping =>
                string.Equals(
                    mapping.Provider,
                    provider,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    mapping.Environment,
                    environment,
                    StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(mapping.ProviderExternalId))
            .GroupBy(
                mapping => mapping.ServicePriceId,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(mapping => mapping.ProviderExternalId.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        var missing = required
            .Where(id => !activeMappings.ContainsKey(id))
            .ToArray();
        var ambiguous = activeMappings
            .Where(pair => required.Contains(pair.Key, StringComparer.Ordinal)
                && pair.Value.Length != 1)
            .Select(pair => pair.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var resolved = required
            .Where(id => activeMappings.TryGetValue(id, out var values)
                && values.Length == 1)
            .Select(id => new BillingV2ProviderPriceMapping(
                id,
                provider,
                environment,
                activeMappings[id][0]))
            .ToArray();

        return new BillingV2ProviderPriceMappingStatus(
            missing.Length == 0 && ambiguous.Length == 0,
            missing,
            ambiguous,
            resolved);
    }
}
