namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2RuntimeConfiguration(
    bool NewSubscriptionsEnabled,
    bool AuthoritativeCheckoutEnabled,
    bool FirstRealSubscriptionApproved,
    bool ProviderOutboxEnabled,
    bool ProviderExecutorEnabled,
    bool ProvisioningEnabled,
    // Phase 3. Le reconciliateur est le seul composant Billing V2 capable
    // d'appeler Stripe SANS action utilisateur : il reste donc OFF par defaut
    // et s'active explicitement.
    bool ReconciliationWorkerEnabled = false,
    int ReconciliationIntervalSeconds =
        BillingV2RuntimeConfiguration.DefaultReconciliationIntervalSeconds,
    bool AdditionalUserProvisioningEnabled = false,
    bool FirstRealTestPricingEnabled = false,
    string? FirstRealTestCustomerId = null,
    string? FirstRealTestPresetCode = null,
    string? FirstRealTestSelectionFingerprint = null,
    int FirstRealTestDiscountBasisPoints = 0,
    long FirstRealTestExpectedTotalCents = 0,
    bool GenericSelectionEnabled = false,
    bool ServiceFulfillmentEnabled = false,
    bool SubscriptionChangesEnabled = false,
    bool StripeRecurringMutationEnabled = false,
    bool VpsLocalProvisioningEnabled = false,
    bool VpsCloudAutomationEnabled = false,
    // Capacite interne seulement. Absente/false => aucun worker refund ne peut
    // appeler Stripe, meme si une demande durable existe en base.
    bool RefundsEnabled = false)
{
    public const int DefaultReconciliationIntervalSeconds = 300;
    public const int MinimumReconciliationIntervalSeconds = 30;

    public bool AdditionalUserMutationsEnabled
        => ProvisioningEnabled || AdditionalUserProvisioningEnabled;

    public static BillingV2RuntimeConfiguration Resolve(
        IConfiguration configuration)
        => ResolveCore(configuration) with
        {
            ReconciliationWorkerEnabled = string.Equals(
                configuration["BILLING_V2_RECONCILIATION_WORKER_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            ReconciliationIntervalSeconds = ResolveInterval(
                configuration["BILLING_V2_RECONCILIATION_INTERVAL_SECONDS"]),
            AdditionalUserProvisioningEnabled = string.Equals(
                configuration["BILLING_V2_ADDITIONAL_USER_PROVISIONING_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            FirstRealTestPricingEnabled = string.Equals(
                configuration["BILLING_V2_FIRST_REAL_TEST_PRICING_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            FirstRealTestCustomerId =
                configuration["BILLING_V2_FIRST_REAL_TEST_CUSTOMER_ID"],
            FirstRealTestPresetCode =
                configuration["BILLING_V2_FIRST_REAL_TEST_PRESET_CODE"],
            FirstRealTestSelectionFingerprint =
                configuration["BILLING_V2_FIRST_REAL_TEST_SELECTION_FINGERPRINT"],
            FirstRealTestDiscountBasisPoints = ResolveNonNegativeInt(
                configuration["BILLING_V2_FIRST_REAL_TEST_DISCOUNT_BPS"]),
            FirstRealTestExpectedTotalCents = ResolveNonNegativeLong(
                configuration["BILLING_V2_FIRST_REAL_TEST_EXPECTED_TOTAL_CENTS"]),
            GenericSelectionEnabled = ReadFlag(configuration, "BILLING_V2_GENERIC_SELECTION_ENABLED"),
            ServiceFulfillmentEnabled = ReadFlag(configuration, "BILLING_V2_SERVICE_FULFILLMENT_ENABLED"),
            SubscriptionChangesEnabled = ReadFlag(configuration, "BILLING_V2_SUBSCRIPTION_CHANGES_ENABLED"),
            StripeRecurringMutationEnabled = ReadFlag(configuration, "BILLING_V2_STRIPE_RECURRING_MUTATION_ENABLED"),
            VpsLocalProvisioningEnabled = ReadFlag(configuration, "BILLING_V2_VPS_LOCAL_PROVISIONING_ENABLED"),
            VpsCloudAutomationEnabled = ReadFlag(configuration, "BILLING_V2_VPS_CLOUD_AUTOMATION_ENABLED"),
            RefundsEnabled = ReadFlag(configuration, "BILLING_V2_REFUNDS_ENABLED")
        };

    private static bool ReadFlag(IConfiguration configuration, string key)
        => string.Equals(configuration[key], "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Une fréquence absente, illisible ou trop agressive retombe sur la
    /// valeur par defaut plutot que de marteler l'API Stripe.
    /// </summary>
    public static int ResolveInterval(string? rawValue)
        => int.TryParse(rawValue, out var seconds)
           && seconds >= MinimumReconciliationIntervalSeconds
            ? seconds
            : DefaultReconciliationIntervalSeconds;

    private static int ResolveNonNegativeInt(string? rawValue)
        => int.TryParse(rawValue, out var value) && value >= 0 ? value : 0;

    private static long ResolveNonNegativeLong(string? rawValue)
        => long.TryParse(rawValue, out var value) && value >= 0 ? value : 0;

    private static BillingV2RuntimeConfiguration ResolveCore(
        IConfiguration configuration)
        => new(
            string.Equals(
                configuration["BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_PROVIDER_OUTBOX_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_PROVIDER_EXECUTOR_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase),
            string.Equals(
                configuration["BILLING_V2_PROVISIONING_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase));
}
