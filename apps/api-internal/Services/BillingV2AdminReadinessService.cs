using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface IBillingV2AdminReadinessService
{
    Task<BillingV2AdminReadinessSnapshot> CheckAsync(
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class BillingV2AdminReadinessService
    : IBillingV2AdminReadinessService
{
    private static readonly string[] RequiredSchemaTables =
    [
        "billing_v2_services",
        "billing_v2_service_tiers",
        "billing_v2_service_prices",
        "billing_v2_offer_presets",
        "billing_v2_preset_items",
        "billing_v2_provider_price_mappings",
        "billing_v2_subscriptions",
        "billing_v2_subscription_items",
        "billing_v2_subscription_item_provisioning",
        "billing_v2_provider_checkout_sessions",
        "billing_v2_provider_events",
        "billing_v2_authoritative_checkout_requests",
        "billing_v2_subscription_documents",
        "billing_v2_document_line_snapshots",
        // Coeur financier (Phase 1) et cycle de vie (Phases 2.5 / 3).
        "billing_v2_subscription_changes",
        "billing_v2_billing_events",
        "billing_v2_billing_event_lines",
        "billing_v2_payment_attempts",
        "billing_v2_document_issuance_attempts"
    ];

    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly PayPalRuntimeConfiguration _paypal;
    private readonly StripeRuntimeConfiguration _stripe;
    private readonly IBillingV2LaunchReadinessService _launchReadiness;
    private readonly IBillingV2ProviderAgreementService _providerAgreements;
    private readonly IBillingV2DocumentReadinessService _documentReadiness;
    private readonly ILogger<BillingV2AdminReadinessService> _logger;

    public BillingV2AdminReadinessService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration runtime,
        PayPalRuntimeConfiguration paypal,
        StripeRuntimeConfiguration stripe,
        IBillingV2LaunchReadinessService launchReadiness,
        IBillingV2ProviderAgreementService providerAgreements,
        IBillingV2DocumentReadinessService documentReadiness,
        ILogger<BillingV2AdminReadinessService> logger)
    {
        _sql = sql;
        _runtime = runtime;
        _paypal = paypal;
        _stripe = stripe;
        _launchReadiness = launchReadiness;
        _providerAgreements = providerAgreements;
        _documentReadiness = documentReadiness;
        _logger = logger;
    }

    public async Task<BillingV2AdminReadinessSnapshot> CheckAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        var persistentSqlAvailable =
            _sql.IsPersistent && !string.IsNullOrWhiteSpace(_sql.ConnectionString);
        var runtimeFlags = ToRuntimeFlags(_runtime);
        var launch = await _launchReadiness.CheckAsync(cancellationToken);
        var launchSummary = BillingV2AdminReadinessMapper.ToAdminLaunchReadiness(
            launch);
        var documentReadiness = await _documentReadiness.CheckAsync(
            cancellationToken);

        if (!persistentSqlAvailable)
        {
            return CreateSnapshot(
                persistentSqlAvailable,
                schemaReady: false,
                RequiredSchemaTables,
                runtimeFlags,
                launchSummary,
                providers: [],
                documentReadiness,
                correlationId,
                blockedIssuanceCount: 0,
                BuildLifecycleInputs(
                    persistentSqlAvailable,
                    schemaReady: false,
                    stripeMappingsReady: false,
                    documentReadiness));
        }

        try
        {
            var missingTables = await LoadMissingSchemaTablesAsync(
                cancellationToken);
            if (missingTables.Count > 0)
            {
                return CreateSnapshot(
                    persistentSqlAvailable,
                    schemaReady: false,
                    missingTables,
                    runtimeFlags,
                    launchSummary,
                    providers: [],
                    documentReadiness,
                    correlationId,
                    blockedIssuanceCount: 0,
                    BuildLifecycleInputs(
                        persistentSqlAvailable,
                        schemaReady: false,
                        stripeMappingsReady: false,
                        documentReadiness));
            }

            var servicePriceIds = await LoadActiveServicePriceIdsAsync(
                cancellationToken);
            var providers = new[]
            {
                await CheckProviderAsync(
                    "stripe",
                    _stripe.ModeName,
                    _stripe.IsConfigured,
                    servicePriceIds,
                    cancellationToken),
                await CheckProviderAsync(
                    "paypal",
                    _paypal.ModeName,
                    _paypal.IsConfigured,
                    servicePriceIds,
                    cancellationToken)
            };

            // Phase 3, point 9. Un dossier BPCE bloque ne doit pas rester
            // silencieux : il remonte dans le meme instantane que le reste,
            // la ou un exploitant regarde deja.
            var blockedIssuances = await CountBlockedIssuancesAsync(
                cancellationToken);
            if (blockedIssuances > 0)
            {
                _logger.LogWarning(
                    "Billing V2 has {Count} document issuance attempt(s) awaiting manual review. No second invoice will be created automatically.",
                    blockedIssuances);
            }

            return CreateSnapshot(
                persistentSqlAvailable,
                schemaReady: true,
                missingTables: [],
                runtimeFlags,
                launchSummary,
                providers,
                documentReadiness,
                correlationId,
                blockedIssuances,
                BuildLifecycleInputs(
                    persistentSqlAvailable,
                    schemaReady: true,
                    providers.Any(provider => string.Equals(
                        provider.Provider,
                        "stripe",
                        StringComparison.Ordinal)
                        && provider.PriceMappingsReady),
                    documentReadiness));
        }
        catch (MySqlException exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 admin readiness snapshot failed. No data was modified.");
            return CreateSnapshot(
                persistentSqlAvailable,
                schemaReady: false,
                RequiredSchemaTables,
                runtimeFlags,
                launchSummary,
                providers: [],
                documentReadiness,
                correlationId,
                blockedIssuanceCount: 0,
                BuildLifecycleInputs(
                    persistentSqlAvailable,
                    schemaReady: false,
                    stripeMappingsReady: false,
                    documentReadiness));
        }
    }

    /// <summary>
    /// Assemble les entrees de la matrice de readiness Phase 3 depuis l'etat
    /// reellement observe : schema, drapeaux runtime, providers configures.
    /// </summary>
    private BillingV2LifecycleReadinessInputs BuildLifecycleInputs(
        bool persistentSqlAvailable,
        bool schemaReady,
        bool stripeMappingsReady,
        BillingV2DocumentReadinessStatus documentReadiness)
        => new(
            persistentSqlAvailable,
            FinancialCoreSchemaReady: schemaReady,
            RenewalSchemaReady: schemaReady,
            _runtime.AuthoritativeCheckoutEnabled,
            _runtime.ProviderExecutorEnabled,
            _stripe.IsConfigured,
            stripeMappingsReady,
            // "Activable", pas "actif" : le worker peut rester eteint, il doit
            // seulement pouvoir etre allume sans changement de code.
            ReconciliationWorkerActivatable: persistentSqlAvailable
                && _runtime.AuthoritativeCheckoutEnabled,
            documentReadiness.Ready,
            BillingV2DocumentIssuancePolicy
                .InvoiceLookupByExternalReferenceSupported,
            _runtime.ProvisioningEnabled,
            _paypal.IsConfigured);

    private async Task<BillingV2AdminProviderReadiness> CheckProviderAsync(
        string provider,
        string environment,
        bool providerConfigured,
        IReadOnlyList<string> servicePriceIds,
        CancellationToken cancellationToken)
    {
        var mappings = await _providerAgreements.VerifyPriceMappingsReadyAsync(
            servicePriceIds,
            provider,
            environment,
            cancellationToken);
        return new BillingV2AdminProviderReadiness(
            provider,
            environment,
            providerConfigured,
            mappings.Ready,
            servicePriceIds.Count,
            mappings.ResolvedMappings.Count,
            mappings.MissingServicePriceIds,
            mappings.AmbiguousServicePriceIds,
            providerConfigured && mappings.Ready);
    }

    /// <summary>
    /// Emissions documentaires en attente d'une decision humaine. Elles ne se
    /// debloqueront pas seules : tant que l'API BPCE ne sait pas rechercher
    /// une facture, recreer risquerait un second numero fiscal.
    /// </summary>
    private async Task<int> CountBlockedIssuancesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM billing_v2_document_issuance_attempts
            WHERE status IN ('reconciliation_required', 'failed');
            """;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private async Task<IReadOnlyList<string>> LoadMissingSchemaTablesAsync(
        CancellationToken cancellationToken)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN (
                'billing_v2_services',
                'billing_v2_service_tiers',
                'billing_v2_service_prices',
                'billing_v2_offer_presets',
                'billing_v2_preset_items',
                'billing_v2_provider_price_mappings',
                'billing_v2_subscriptions',
                'billing_v2_subscription_items',
                'billing_v2_subscription_item_provisioning',
                'billing_v2_provider_checkout_sessions',
                'billing_v2_provider_events',
                'billing_v2_authoritative_checkout_requests',
                'billing_v2_subscription_documents',
                'billing_v2_document_line_snapshots',
                'billing_v2_subscription_changes',
                'billing_v2_billing_events',
                'billing_v2_billing_event_lines',
                'billing_v2_payment_attempts',
                'billing_v2_document_issuance_attempts'
              );
            """;
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            existing.Add(reader.GetString("table_name"));
        }

        return RequiredSchemaTables
            .Where(table => !existing.Contains(table))
            .OrderBy(table => table, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> LoadActiveServicePriceIdsAsync(
        CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id
            FROM billing_v2_service_prices
            WHERE status = 'active'
              AND valid_from <= UTC_TIMESTAMP(6)
              AND (valid_until IS NULL OR valid_until > UTC_TIMESTAMP(6))
            ORDER BY id;
            """;
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(MariaDbIdentifierReader.ReadRequired(reader, "id"));
        }

        return ids;
    }

    private static BillingV2AdminReadinessSnapshot CreateSnapshot(
        bool persistentSqlAvailable,
        bool schemaReady,
        IReadOnlyList<string> missingTables,
        BillingV2AdminRuntimeFlags runtimeFlags,
        BillingV2AdminLaunchReadiness launchReadiness,
        IReadOnlyList<BillingV2AdminProviderReadiness> providers,
        BillingV2DocumentReadinessStatus documentReadiness,
        string correlationId,
        int blockedIssuanceCount,
        BillingV2LifecycleReadinessInputs lifecycleInputs)
    {
        var operationalLimitations =
            BillingV2AdminOperationalLimitations.Create(
                documentReadiness,
                BillingV2LifecycleReadinessGate.Evaluate(lifecycleInputs),
                blockedIssuanceCount);
        var reason = BillingV2AdminReadinessGate.ResolveReasonCode(
            persistentSqlAvailable,
            schemaReady,
            runtimeFlags,
            launchReadiness,
            providers,
            operationalLimitations);
        return new BillingV2AdminReadinessSnapshot(
            persistentSqlAvailable,
            schemaReady,
            missingTables,
            runtimeFlags,
            launchReadiness,
            providers,
            operationalLimitations,
            reason == "BILLING_V2_ADMIN_READY_FOR_FIRST_SUBSCRIPTION",
            reason,
            correlationId);
    }

    private static BillingV2AdminRuntimeFlags ToRuntimeFlags(
        BillingV2RuntimeConfiguration runtime)
        => new(
            runtime.CatalogShadowModeEnabled,
            runtime.ProvisioningShadowModeEnabled,
            runtime.NewSubscriptionsEnabled,
            runtime.AuthoritativeCheckoutEnabled,
            runtime.FirstRealSubscriptionApproved,
            runtime.ProviderOutboxEnabled,
            runtime.ProviderExecutorEnabled,
            runtime.ProvisioningEnabled);
}

public static class BillingV2AdminOperationalLimitations
{
    public static IReadOnlyList<BillingV2AdminOperationalLimitation> Default { get; } =
    [
        new(
            "BILLING_V2_CANCELLATION_AUTOMATION_NOT_READY",
            "human_review",
            "Les routes de résiliation automatisée restent legacy-only ; une souscription Billing V2 doit être annulée par une procédure dédiée ou une décision humaine."),
        new(
            "BILLING_V2_BPCE_INVOICE_AUTOMATION_NOT_READY",
            "hard_blocker",
            "L'émission de facture BPCE reste branchée sur les documents commerciaux legacy ; aucune facture BPCE V2 automatique n'est produite par le checkout V2."),
        new(
            "BILLING_V2_KOXO_STORAGE_PROVIDER_NOT_READY",
            "human_review",
            "Les quotas de stockage V2 peuvent etre calcules, mais aucun provider fiable de modification reelle de quota KoXo n'est cable.")
    ];

    public static IReadOnlyList<BillingV2AdminOperationalLimitation> Create(
        BillingV2DocumentReadinessStatus documentReadiness,
        IReadOnlyList<BillingV2ReadinessComponent>? lifecycle = null,
        int blockedIssuanceCount = 0)
    {
        var withoutDocumentLimitation = Default
            .Where(limitation => !string.Equals(
                limitation.Code,
                BillingV2DocumentReadinessStatus.NotReady.ReasonCode,
                StringComparison.Ordinal))
            .ToList();

        if (!documentReadiness.Ready)
        {
            withoutDocumentLimitation.Insert(
                1,
                new BillingV2AdminOperationalLimitation(
                    documentReadiness.ReasonCode,
                    "hard_blocker",
                    documentReadiness.Message));
        }

        if (blockedIssuanceCount > 0)
        {
            withoutDocumentLimitation.Add(
                new BillingV2AdminOperationalLimitation(
                    "BILLING_V2_DOCUMENT_ISSUANCE_AWAITING_REVIEW",
                    "human_review",
                    $"{blockedIssuanceCount} emission(s) documentaire(s) en attente de revue humaine. Aucune seconde facture n'est creee automatiquement : verifier chez BPCE avant de debloquer."));
        }

        if (lifecycle is null)
        {
            return withoutDocumentLimitation;
        }

        // Phase 3. Un composant requis mais non pret devient un blocage dur ;
        // un composant MANUAL reste une limite exploitable, pas un blocage.
        // C'est ce qui laisse PayPal explicitement NOT READY sans empecher
        // Stripe de fonctionner : PayPal n'est pas dans les composants requis.
        foreach (var component in BillingV2LifecycleReadinessGate
                     .StripeLaunchBlockers(lifecycle))
        {
            withoutDocumentLimitation.Add(
                new BillingV2AdminOperationalLimitation(
                    component.ReasonCode,
                    "hard_blocker",
                    component.Message));
        }

        foreach (var component in lifecycle.Where(entry => string.Equals(
                     entry.State,
                     BillingV2ReadinessStates.Manual,
                     StringComparison.Ordinal)))
        {
            withoutDocumentLimitation.Add(
                new BillingV2AdminOperationalLimitation(
                    component.ReasonCode,
                    "human_review",
                    component.Message));
        }

        return withoutDocumentLimitation;
    }
}

public static class BillingV2AdminReadinessMapper
{
    public static BillingV2AdminLaunchReadiness ToAdminLaunchReadiness(
        BillingV2LaunchReadinessSnapshot snapshot)
        => new(
            snapshot.RealCustomerSubscriptionCount,
            snapshot.DemoSubscriptionCount,
            snapshot.NoRealCustomerSubscriptions,
            snapshot.VerifiedAgainstPersistentSql)
        {
            BlockingRealSubscriptions = snapshot.BlockingRealSubscriptions
                .Select(subscription =>
                    new BillingV2AdminBlockingLegacySubscription(
                        subscription.SubscriptionId,
                        subscription.Status,
                        subscription.CustomerId,
                        subscription.CustomerReference,
                        subscription.CustomerName,
                        subscription.CommercialOfferId,
                        subscription.CreatedAt.ToString("O"),
                        subscription.UpdatedAt.ToString("O")))
                .ToArray()
        };
}

public static class BillingV2AdminReadinessGate
{
    public static string ResolveReasonCode(
        bool persistentSqlAvailable,
        bool schemaReady,
        BillingV2AdminRuntimeFlags runtimeFlags,
        BillingV2AdminLaunchReadiness launchReadiness,
        IReadOnlyList<BillingV2AdminProviderReadiness> providers,
        IReadOnlyList<BillingV2AdminOperationalLimitation>? operationalLimitations = null)
    {
        operationalLimitations ??= BillingV2AdminOperationalLimitations.Default;

        if (!persistentSqlAvailable)
        {
            return "BILLING_V2_ADMIN_NO_PERSISTENT_SQL";
        }

        if (!schemaReady)
        {
            return "BILLING_V2_ADMIN_SCHEMA_INCOMPLETE";
        }

        if (!runtimeFlags.NewSubscriptionsEnabled
            || !runtimeFlags.AuthoritativeCheckoutEnabled
            || !runtimeFlags.FirstRealSubscriptionApproved
            || !runtimeFlags.ProviderOutboxEnabled
            || !runtimeFlags.ProviderExecutorEnabled)
        {
            return "BILLING_V2_ADMIN_FLAGS_CLOSED";
        }

        if (!launchReadiness.NoRealCustomerSubscriptions)
        {
            return "BILLING_V2_ADMIN_REAL_LEGACY_SUBSCRIPTIONS_PRESENT";
        }

        if (!launchReadiness.VerifiedAgainstPersistentSql)
        {
            return "BILLING_V2_ADMIN_LAUNCH_READINESS_UNVERIFIED";
        }

        if (!providers.Any(provider => provider.ReadyForCheckout))
        {
            return "BILLING_V2_ADMIN_NO_PROVIDER_READY";
        }

        var hardBlocker = operationalLimitations.FirstOrDefault(limitation =>
            string.Equals(
                limitation.Severity,
                "hard_blocker",
                StringComparison.OrdinalIgnoreCase));
        if (hardBlocker is not null)
        {
            return hardBlocker.Code;
        }

        return "BILLING_V2_ADMIN_READY_FOR_FIRST_SUBSCRIPTION";
    }
}
