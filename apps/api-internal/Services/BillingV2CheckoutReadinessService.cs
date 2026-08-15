namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2CheckoutReadinessRequest(
    IReadOnlyList<string> RequiredServicePriceIds,
    string Provider,
    string Environment);

public sealed record BillingV2CheckoutReadinessDecision(
    bool Authorized,
    string ReasonCode,
    BillingV2LaunchReadinessSnapshot LaunchReadiness,
    BillingV2ProviderPriceMappingStatus ProviderMappings,
    BillingV2DocumentReadinessStatus DocumentReadiness);

public sealed record BillingV2DocumentReadinessStatus(
    bool Ready,
    string ReasonCode,
    string Message)
{
    public static BillingV2DocumentReadinessStatus ReadyForCheckout { get; } =
        new(
            true,
            "BILLING_V2_DOCUMENT_ISSUER_READY",
            "Billing V2 document issuing is ready.");

    public static BillingV2DocumentReadinessStatus NotReady { get; } =
        new(
            false,
            "BILLING_V2_BPCE_INVOICE_AUTOMATION_NOT_READY",
            "Billing V2 checkout cannot be authorized until a tested V2 document/invoice issuing path exists.");
}

public interface IBillingV2CheckoutReadinessService
{
    Task<BillingV2CheckoutReadinessDecision> CheckAsync(
        BillingV2CheckoutReadinessRequest request,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2CheckoutReadinessService
    : IBillingV2CheckoutReadinessService
{
    public static NoOpBillingV2CheckoutReadinessService Instance { get; }
        = new();

    private NoOpBillingV2CheckoutReadinessService()
    {
    }

    public Task<BillingV2CheckoutReadinessDecision> CheckAsync(
        BillingV2CheckoutReadinessRequest request,
        CancellationToken cancellationToken)
    {
        var launchReadiness = BillingV2LaunchReadinessGate.Evaluate(
            realCustomerSubscriptionCount: 0,
            demoSubscriptionCount: 0);
        var providerMappings = BillingV2ProviderPriceMappingGate.Evaluate(
            request.RequiredServicePriceIds,
            Array.Empty<BillingV2ProviderPriceMapping>(),
            request.Provider,
            request.Environment);
        return Task.FromResult(new BillingV2CheckoutReadinessDecision(
            Authorized: false,
            "BILLING_V2_CHECKOUT_FLAG_OFF",
            launchReadiness,
            providerMappings,
            BillingV2DocumentReadinessStatus.NotReady));
    }
}

public sealed class BillingV2CheckoutReadinessService
    : IBillingV2CheckoutReadinessService
{
    private readonly BillingV2RuntimeConfiguration _configuration;
    private readonly IBillingV2LaunchReadinessService _launchReadiness;
    private readonly IBillingV2ProviderAgreementService _providerAgreements;
    private readonly IBillingV2DocumentReadinessService _documentReadiness;
    private readonly ILogger<BillingV2CheckoutReadinessService> _logger;

    public BillingV2CheckoutReadinessService(
        BillingV2RuntimeConfiguration configuration,
        IBillingV2LaunchReadinessService launchReadiness,
        IBillingV2ProviderAgreementService providerAgreements,
        IBillingV2DocumentReadinessService documentReadiness,
        ILogger<BillingV2CheckoutReadinessService> logger)
    {
        _configuration = configuration;
        _launchReadiness = launchReadiness;
        _providerAgreements = providerAgreements;
        _documentReadiness = documentReadiness;
        _logger = logger;
    }

    public async Task<BillingV2CheckoutReadinessDecision> CheckAsync(
        BillingV2CheckoutReadinessRequest request,
        CancellationToken cancellationToken)
    {
        var launchReadiness = await _launchReadiness.CheckAsync(
            cancellationToken);
        var providerMappings =
            await _providerAgreements.VerifyPriceMappingsReadyAsync(
                request.RequiredServicePriceIds,
                request.Provider,
                request.Environment,
                cancellationToken);
        var documentReadiness = await _documentReadiness.CheckAsync(
            cancellationToken);
        var decision = BillingV2CheckoutReadinessGate.Evaluate(
            _configuration,
            launchReadiness,
            providerMappings,
            documentReadiness);

        if (!decision.Authorized)
        {
            _logger.LogWarning(
                "Billing V2 authoritative checkout blocked: {ReasonCode}. Legacy checkout remains authoritative.",
                decision.ReasonCode);
        }

        return decision;
    }
}

public static class BillingV2CheckoutReadinessGate
{
    public static BillingV2CheckoutReadinessDecision Evaluate(
        BillingV2RuntimeConfiguration configuration,
        BillingV2LaunchReadinessSnapshot launchReadiness,
        BillingV2ProviderPriceMappingStatus providerMappings,
        BillingV2DocumentReadinessStatus? documentReadiness = null)
    {
        documentReadiness ??= BillingV2DocumentReadinessStatus.NotReady;

        if (!configuration.AuthoritativeCheckoutEnabled)
        {
            return Blocked(
                "BILLING_V2_CHECKOUT_FLAG_OFF",
                launchReadiness,
                providerMappings,
                documentReadiness);
        }

        if (!configuration.FirstRealSubscriptionApproved)
        {
            return Blocked(
                "BILLING_V2_FIRST_REAL_SUBSCRIPTION_NOT_APPROVED",
                launchReadiness,
                providerMappings,
                documentReadiness);
        }

        if (!configuration.ProviderOutboxEnabled)
        {
            return Blocked(
                "BILLING_V2_PROVIDER_OUTBOX_FLAG_OFF",
                launchReadiness,
                providerMappings,
                documentReadiness);
        }

        if (!configuration.ProviderExecutorEnabled)
        {
            return Blocked(
                "BILLING_V2_PROVIDER_EXECUTOR_FLAG_OFF",
                launchReadiness,
                providerMappings,
                documentReadiness);
        }

        if (!launchReadiness.NoRealCustomerSubscriptions)
        {
            return Blocked(
                "BILLING_V2_REAL_LEGACY_SUBSCRIPTIONS_PRESENT",
                launchReadiness,
                providerMappings,
                documentReadiness);
        }

        if (!launchReadiness.VerifiedAgainstPersistentSql)
        {
            return Blocked(
                "BILLING_V2_LAUNCH_READINESS_UNVERIFIED",
                launchReadiness,
                providerMappings,
                documentReadiness);
        }

        if (!providerMappings.Ready)
        {
            return Blocked(
                "BILLING_V2_PROVIDER_PRICE_MAPPING_INCOMPLETE",
                launchReadiness,
                providerMappings,
                documentReadiness);
        }

        if (!documentReadiness.Ready)
        {
            return Blocked(
                documentReadiness.ReasonCode,
                launchReadiness,
                providerMappings,
                documentReadiness);
        }

        return new BillingV2CheckoutReadinessDecision(
            Authorized: true,
            "BILLING_V2_AUTHORITATIVE_CHECKOUT_READY",
            launchReadiness,
            providerMappings,
            documentReadiness);
    }

    private static BillingV2CheckoutReadinessDecision Blocked(
        string reasonCode,
        BillingV2LaunchReadinessSnapshot launchReadiness,
        BillingV2ProviderPriceMappingStatus providerMappings,
        BillingV2DocumentReadinessStatus documentReadiness)
        => new(
            Authorized: false,
            reasonCode,
            launchReadiness,
            providerMappings,
            documentReadiness);
}
