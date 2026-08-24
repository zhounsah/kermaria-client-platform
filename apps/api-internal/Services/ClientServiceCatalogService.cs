using Kermaria.ApiInternal;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.Services;

public interface IClientServiceCatalogService
{
    Task<IReadOnlyList<ServiceSummary>> GetServicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<bool> IsKnownServiceIdAsync(
        PortalSessionContext session,
        string serviceId,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetActiveServiceTypesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);
}

/// <summary>
/// Services visibles par un client, projetes depuis Billing V2 seul.
/// </summary>
/// <remarks>
/// <para>
/// La source unique est <see cref="IBillingV2ClientServiceEntitlementProjection"/> :
/// un droit vient d'un item d'abonnement V2 actif, jamais d'une ligne de
/// document commercial. Un document est une trace de facturation, pas un titre
/// d'acces : le laisser ouvrir un service revenait a accorder un droit sur la
/// foi d'un devis.
/// </para>
/// <para>
/// Les libelles et categories viennent de <see cref="IServiceTopologyService"/>,
/// c'est-a-dire de <c>billing_v2_services</c>. Aucune offre commerciale
/// n'intervient.
/// </para>
/// </remarks>
public sealed class ClientServiceCatalogService : IClientServiceCatalogService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly IServiceTopologyService _topologyService;
    private readonly IBillingV2ClientServiceEntitlementProjection _entitlements;

    public ClientServiceCatalogService(
        SqlRuntimeConfiguration sql,
        IServiceTopologyService topologyService,
        IBillingV2ClientServiceEntitlementProjection entitlements)
    {
        _sql = sql;
        _topologyService = topologyService;
        _entitlements = entitlements;
    }

    public async Task<IReadOnlyList<ServiceSummary>> GetServicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent)
        {
            return MockPortalData.Services;
        }

        var buckets = new Dictionary<string, List<ServiceEntitlementSource>>(
            StringComparer.OrdinalIgnoreCase);
        var entitlements = await _entitlements.GetClientEntitlementsAsync(
            session.CustomerId,
            cancellationToken);
        foreach (var entitlement in entitlements)
        {
            var serviceStatus = MapSubscriptionStatus(
                entitlement.SubscriptionStatus);
            if (serviceStatus is null)
            {
                continue;
            }

            AddSource(
                buckets,
                entitlement.ServiceCode,
                new ServiceEntitlementSource(
                    entitlement.SubscriptionId,
                    entitlement.SubscriptionLabel,
                    serviceStatus,
                    entitlement.StartedAt ?? entitlement.CreatedAt,
                    serviceStatus == "pending"
                        ? CreatePendingSubscriptionMessage(
                            entitlement.SubscriptionStatus)
                        : null));
        }

        var definitions = await _topologyService.GetTechnicalServicesAsync(
            cancellationToken);
        var definitionsByCode = definitions.ToDictionary(
            service => service.TechnicalServiceReference,
            service => service,
            StringComparer.OrdinalIgnoreCase);

        var services = new List<ServiceSummary>(buckets.Count);
        foreach (var bucket in buckets)
        {
            definitionsByCode.TryGetValue(bucket.Key, out var definition);
            var label = definition?.Label
                ?? await _topologyService.ResolveServiceLabelAsync(
                    bucket.Key,
                    cancellationToken);
            services.Add(BuildServiceSummary(
                bucket.Key,
                label,
                definition,
                bucket.Value));
        }

        return services
            .OrderBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<bool> IsKnownServiceIdAsync(
        PortalSessionContext session,
        string serviceId,
        CancellationToken cancellationToken)
    {
        var services = await GetServicesAsync(session, cancellationToken);
        return services.Any(service =>
            string.Equals(service.Id, serviceId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlySet<string>> GetActiveServiceTypesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        var services = await GetServicesAsync(session, cancellationToken);
        return services
            .Where(service => service.Status == "active")
            .Select(service => service.Type)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddSource(
        IDictionary<string, List<ServiceEntitlementSource>> buckets,
        string serviceCode,
        ServiceEntitlementSource source)
    {
        var normalizedReference = serviceCode.Trim();
        if (normalizedReference.Length == 0)
        {
            return;
        }

        if (!buckets.TryGetValue(normalizedReference, out var bucket))
        {
            bucket = [];
            buckets[normalizedReference] = bucket;
        }

        bucket.Add(source);
    }

    private static ServiceSummary BuildServiceSummary(
        string serviceCode,
        string label,
        CatalogTechnicalServiceDefinition? definition,
        IReadOnlyList<ServiceEntitlementSource> sources)
    {
        var status = ResolvePortalStatus(sources);
        var startedAt = sources
            .Select(source => source.StartedAt)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
        var sourceLabels = sources
            .Select(source => source.SourceLabel)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ServiceSummary(
            serviceCode,
            serviceCode,
            label,
            InferServiceType(definition, label, serviceCode),
            status,
            definition?.Description ?? $"Service du catalogue : {label}.",
            startedAt,
            sourceLabels.Length == 0
                ? "Aucun rattachement commercial détaillé."
                : $"Couvert via : {string.Join(", ", sourceLabels)}",
            "Inclus dans vos souscriptions",
            status == "pending"
                ? sources.Select(source => source.NextStep)
                    .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                : null);
    }

    private static string ResolvePortalStatus(
        IEnumerable<ServiceEntitlementSource> sources)
    {
        if (sources.Any(source => source.Status == "active"))
        {
            return "active";
        }

        return sources.Any(source => source.Status == "pending")
            ? "pending"
            : "suspended";
    }

    private static string? MapSubscriptionStatus(string subscriptionStatus)
        => subscriptionStatus switch
        {
            "active" or "pending_cancellation" => "active",
            "pending_approval" or "pending_payment" or "pending_activation" =>
                "pending",
            "suspended" or "cancelled" or "expired" => "suspended",
            _ => null
        };

    private static string CreatePendingSubscriptionMessage(string subscriptionStatus)
        => subscriptionStatus switch
        {
            "pending_payment" => "Souscription en attente de paiement confirmé.",
            "pending_activation" =>
                "Souscription payée, en attente d'activation ou de provisionning.",
            "pending_approval" => "Souscription en attente d'approbation.",
            _ => "Souscription en attente de finalisation."
        };

    private static string InferServiceType(
        CatalogTechnicalServiceDefinition? definition,
        string label,
        string serviceCode)
    {
        var haystack = string.Join(
            ' ',
            new[]
            {
                serviceCode,
                label,
                definition?.Category,
                definition?.Description
            }.Where(value => !string.IsNullOrWhiteSpace(value)))
            .ToLowerInvariant();

        if (haystack.Contains("vpn", StringComparison.Ordinal))
        {
            return "vpn";
        }

        if (haystack.Contains("rds", StringComparison.Ordinal)
            || haystack.Contains("bureau windows", StringComparison.Ordinal))
        {
            return "rds";
        }

        if (haystack.Contains("sauveg", StringComparison.Ordinal))
        {
            return "backup";
        }

        if (haystack.Contains("nextcloud", StringComparison.Ordinal)
            || haystack.Contains("cloud", StringComparison.Ordinal))
        {
            return "cloud";
        }

        if (haystack.Contains("document", StringComparison.Ordinal))
        {
            return "documentation";
        }

        if (haystack.Contains("supervision", StringComparison.Ordinal)
            || haystack.Contains("monitor", StringComparison.Ordinal))
        {
            return "monitoring";
        }

        if (haystack.Contains("utilisateur", StringComparison.Ordinal))
        {
            return "user";
        }

        if (haystack.Contains("support", StringComparison.Ordinal))
        {
            return "support";
        }

        if (haystack.Contains("stock", StringComparison.Ordinal)
            || haystack.Contains("hébergement", StringComparison.Ordinal)
            || haystack.Contains("dossier", StringComparison.Ordinal))
        {
            return "personal_hosting";
        }

        return "other";
    }
}

internal sealed record ServiceEntitlementSource(
    string SourceId,
    string SourceLabel,
    string Status,
    string? StartedAt,
    string? NextStep);
