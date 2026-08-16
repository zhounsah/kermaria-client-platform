using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record BillingV2ProvisioningShadowRule(
    string LegacyServiceReference,
    string MappingKind,
    string? V2ServiceCode,
    string? V2TierCode,
    string TargetType,
    string? TargetReference);

public sealed record BillingV2ProvisioningShadowComparison(
    bool Enabled,
    bool Succeeded,
    IReadOnlyList<string> CurrentSubscriptionGroups,
    IReadOnlyList<string> ReconciledGroups,
    IReadOnlyList<string> MissingGroups,
    IReadOnlyList<string> ExtraGroups,
    IReadOnlyList<string> UnsupportedLegacyServiceReferences)
{
    public static BillingV2ProvisioningShadowComparison Disabled { get; } =
        new(
            Enabled: false,
            Succeeded: true,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

    public bool MatchesLegacy => MissingGroups.Count == 0 && ExtraGroups.Count == 0;
}

public interface IBillingV2ProvisioningShadowService
{
    Task<BillingV2ProvisioningShadowComparison> CompareAsync(
        SubscriptionSummary subscription,
        IReadOnlyList<string> legacyCurrentSubscriptionGroups,
        IReadOnlyList<string> legacyReconciledGroups,
        CancellationToken cancellationToken);
}

public sealed record BillingV2ClientServiceCatalogShadowComparison(
    bool Enabled,
    bool Succeeded,
    IReadOnlyList<string> LegacyServiceReferences,
    IReadOnlyList<string> MappedV2ServiceCodes,
    IReadOnlyList<string> IgnoredLegacyEntitlementReferences,
    IReadOnlyList<string> UnsupportedLegacyServiceReferences)
{
    public static BillingV2ClientServiceCatalogShadowComparison Disabled { get; }
        = new(
            Enabled: false,
            Succeeded: true,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

    public bool IsCovered => UnsupportedLegacyServiceReferences.Count == 0;
}

public interface IBillingV2ClientServiceCatalogShadowService
{
    Task<BillingV2ClientServiceCatalogShadowComparison> CompareAsync(
        PortalSessionContext session,
        IReadOnlyList<ServiceSummary> legacyServices,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2ProvisioningShadowService
    : IBillingV2ProvisioningShadowService
{
    public static NoOpBillingV2ProvisioningShadowService Instance { get; } = new();

    private NoOpBillingV2ProvisioningShadowService()
    {
    }

    public Task<BillingV2ProvisioningShadowComparison> CompareAsync(
        SubscriptionSummary subscription,
        IReadOnlyList<string> legacyCurrentSubscriptionGroups,
        IReadOnlyList<string> legacyReconciledGroups,
        CancellationToken cancellationToken)
        => Task.FromResult(BillingV2ProvisioningShadowComparison.Disabled);
}

public sealed class NoOpBillingV2ClientServiceCatalogShadowService
    : IBillingV2ClientServiceCatalogShadowService
{
    public static NoOpBillingV2ClientServiceCatalogShadowService Instance { get; }
        = new();

    private NoOpBillingV2ClientServiceCatalogShadowService()
    {
    }

    public Task<BillingV2ClientServiceCatalogShadowComparison> CompareAsync(
        PortalSessionContext session,
        IReadOnlyList<ServiceSummary> legacyServices,
        CancellationToken cancellationToken)
        => Task.FromResult(
            BillingV2ClientServiceCatalogShadowComparison.Disabled);
}

public sealed class BillingV2ProvisioningShadowService
    : IBillingV2ProvisioningShadowService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _billingV2;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ICommercialOfferTopologyService _topology;
    private readonly ILogger<BillingV2ProvisioningShadowService> _logger;

    public BillingV2ProvisioningShadowService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration billingV2,
        ISubscriptionRepository subscriptions,
        ICommercialOfferTopologyService topology,
        ILogger<BillingV2ProvisioningShadowService> logger)
    {
        _sql = sql;
        _billingV2 = billingV2;
        _subscriptions = subscriptions;
        _topology = topology;
        _logger = logger;
    }

    public async Task<BillingV2ProvisioningShadowComparison> CompareAsync(
        SubscriptionSummary subscription,
        IReadOnlyList<string> legacyCurrentSubscriptionGroups,
        IReadOnlyList<string> legacyReconciledGroups,
        CancellationToken cancellationToken)
    {
        if (!_billingV2.ProvisioningShadowModeEnabled
            || !_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return BillingV2ProvisioningShadowComparison.Disabled;
        }

        try
        {
            var rules = await LoadRulesAsync(cancellationToken);
            var currentTechnicalReferences =
                await _topology.ResolveTechnicalServiceReferencesAsync(
                    subscription,
                    cancellationToken);
            var currentGroups =
                BillingV2ProvisioningShadowCalculator.ResolveAdGroups(
                    currentTechnicalReferences,
                    rules);

            var activeSubscriptions =
                (await _subscriptions.GetByCustomerAsync(
                    subscription.CustomerId,
                    cancellationToken))
                .Where(candidate => string.Equals(
                    candidate.Status,
                    "active",
                    StringComparison.Ordinal))
                .ToArray();

            var reconciledGroups = new SortedSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var unsupported = new SortedSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var activeSubscription in activeSubscriptions)
            {
                var technicalReferences =
                    await _topology.ResolveTechnicalServiceReferencesAsync(
                        activeSubscription,
                        cancellationToken);
                foreach (var group in BillingV2ProvisioningShadowCalculator
                    .ResolveAdGroups(technicalReferences, rules))
                {
                    reconciledGroups.Add(group);
                }

                foreach (var reference in BillingV2ProvisioningShadowCalculator
                    .ResolveUnsupportedLegacyServiceReferences(
                        technicalReferences,
                        rules))
                {
                    unsupported.Add(reference);
                }
            }

            var missing = legacyReconciledGroups
                .Except(reconciledGroups, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var extra = reconciledGroups
                .Except(legacyReconciledGroups, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new BillingV2ProvisioningShadowComparison(
                Enabled: true,
                Succeeded: true,
                currentGroups,
                reconciledGroups.ToArray(),
                missing,
                extra,
                unsupported.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 shadow provisioning comparison failed for subscription {SubscriptionId}. Legacy provisioning remains authoritative.",
                subscription.Id);
            return new BillingV2ProvisioningShadowComparison(
                Enabled: true,
                Succeeded: false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }
    }

    private async Task<IReadOnlyList<BillingV2ProvisioningShadowRule>>
        LoadRulesAsync(CancellationToken cancellationToken)
    {
        var rules = new List<BillingV2ProvisioningShadowRule>();
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                mapping.legacy_service_reference,
                mapping.mapping_kind,
                mapping.v2_service_code,
                mapping.v2_tier_code,
                rule.target_type,
                rule.target_reference
            FROM billing_v2_legacy_service_mappings mapping
            LEFT JOIN billing_v2_services service
              ON service.code = mapping.v2_service_code
             AND service.status = 'active'
            LEFT JOIN billing_v2_service_tiers tier
              ON tier.service_id = service.id
             AND tier.code = mapping.v2_tier_code
            LEFT JOIN billing_v2_provisioning_rules rule
              ON rule.service_id = service.id
             AND rule.status = 'active'
             AND rule.target_type = 'ad_group'
             AND (
                 rule.tier_id IS NULL
                 OR rule.tier_id = tier.id
             )
            ORDER BY mapping.legacy_service_reference, rule.display_order;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(new BillingV2ProvisioningShadowRule(
                reader.GetString("legacy_service_reference"),
                reader.GetString("mapping_kind"),
                reader.IsDBNull(reader.GetOrdinal("v2_service_code"))
                    ? null
                    : reader.GetString("v2_service_code"),
                reader.IsDBNull(reader.GetOrdinal("v2_tier_code"))
                    ? null
                    : reader.GetString("v2_tier_code"),
                reader.IsDBNull(reader.GetOrdinal("target_type"))
                    ? string.Empty
                    : reader.GetString("target_type"),
                reader.IsDBNull(reader.GetOrdinal("target_reference"))
                    ? null
                    : reader.GetString("target_reference")));
        }

        return rules;
    }
}

public sealed class BillingV2ClientServiceCatalogShadowService
    : IBillingV2ClientServiceCatalogShadowService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _billingV2;
    private readonly ILogger<BillingV2ClientServiceCatalogShadowService> _logger;

    public BillingV2ClientServiceCatalogShadowService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration billingV2,
        ILogger<BillingV2ClientServiceCatalogShadowService> logger)
    {
        _sql = sql;
        _billingV2 = billingV2;
        _logger = logger;
    }

    public async Task<BillingV2ClientServiceCatalogShadowComparison> CompareAsync(
        PortalSessionContext session,
        IReadOnlyList<ServiceSummary> legacyServices,
        CancellationToken cancellationToken)
    {
        if (!_billingV2.ProvisioningShadowModeEnabled
            || !_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return BillingV2ClientServiceCatalogShadowComparison.Disabled;
        }

        try
        {
            var rules = await LoadRulesAsync(cancellationToken);
            var comparison =
                BillingV2ClientServiceCatalogShadowCalculator.Compare(
                    legacyServices.Select(service => service.Id).ToArray(),
                    rules);
            return comparison;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 client service catalog shadow failed for customer {CustomerId}. Legacy service catalog remains authoritative.",
                session.CustomerId);
            return new BillingV2ClientServiceCatalogShadowComparison(
                Enabled: true,
                Succeeded: false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }
    }

    private async Task<IReadOnlyList<BillingV2ProvisioningShadowRule>>
        LoadRulesAsync(CancellationToken cancellationToken)
    {
        var rules = new List<BillingV2ProvisioningShadowRule>();
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                legacy_service_reference,
                mapping_kind,
                v2_service_code,
                v2_tier_code
            FROM billing_v2_legacy_service_mappings
            ORDER BY legacy_service_reference;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(new BillingV2ProvisioningShadowRule(
                reader.GetString("legacy_service_reference"),
                reader.GetString("mapping_kind"),
                reader.IsDBNull(reader.GetOrdinal("v2_service_code"))
                    ? null
                    : reader.GetString("v2_service_code"),
                reader.IsDBNull(reader.GetOrdinal("v2_tier_code"))
                    ? null
                    : reader.GetString("v2_tier_code"),
                string.Empty,
                null));
        }

        return rules;
    }
}

public static class BillingV2ProvisioningShadowCalculator
{
    public static IReadOnlyList<string> ResolveAdGroups(
        IReadOnlyList<string> legacyTechnicalServiceReferences,
        IReadOnlyList<BillingV2ProvisioningShadowRule> rules)
    {
        var groups = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in NormalizeReferences(
            legacyTechnicalServiceReferences))
        {
            foreach (var rule in rules.Where(rule => string.Equals(
                rule.LegacyServiceReference,
                reference,
                StringComparison.OrdinalIgnoreCase)))
            {
                if (string.Equals(
                        rule.TargetType,
                        "ad_group",
                        StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(rule.TargetReference))
                {
                    groups.Add(rule.TargetReference.Trim());
                }
            }
        }

        return groups.ToArray();
    }

    public static IReadOnlyList<string> ResolveUnsupportedLegacyServiceReferences(
        IReadOnlyList<string> legacyTechnicalServiceReferences,
        IReadOnlyList<BillingV2ProvisioningShadowRule> rules)
    {
        var supported = rules
            .Select(rule => rule.LegacyServiceReference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return NormalizeReferences(legacyTechnicalServiceReferences)
            .Where(reference => !supported.Contains(reference))
            .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeReferences(
        IReadOnlyList<string> legacyTechnicalServiceReferences)
        => legacyTechnicalServiceReferences
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public static class BillingV2ClientServiceCatalogShadowCalculator
{
    public static BillingV2ClientServiceCatalogShadowComparison Compare(
        IReadOnlyList<string> legacyServiceReferences,
        IReadOnlyList<BillingV2ProvisioningShadowRule> rules)
    {
        var normalizedReferences = legacyServiceReferences
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var supportedReferences = rules
            .Select(rule => rule.LegacyServiceReference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mappedServiceCodes = new SortedSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var ignoredEntitlements = new SortedSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var reference in normalizedReferences)
        {
            foreach (var rule in rules.Where(rule => string.Equals(
                         rule.LegacyServiceReference,
                         reference,
                         StringComparison.OrdinalIgnoreCase)))
            {
                if (string.Equals(
                        rule.MappingKind,
                        "legacy_one_time_entitlement",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ignoredEntitlements.Add(reference);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(rule.V2ServiceCode))
                {
                    mappedServiceCodes.Add(rule.V2ServiceCode.Trim());
                }
            }
        }

        var unsupported = normalizedReferences
            .Where(reference => !supportedReferences.Contains(reference))
            .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new BillingV2ClientServiceCatalogShadowComparison(
            Enabled: true,
            Succeeded: true,
            normalizedReferences,
            mappedServiceCodes.ToArray(),
            ignoredEntitlements.ToArray(),
            unsupported);
    }
}
