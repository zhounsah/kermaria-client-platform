using Kermaria.ApiInternal.Contracts;
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

public sealed class ClientServiceCatalogService : IClientServiceCatalogService
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ICommercialRepository _commercialRepository;
    private readonly ICommercialOfferTopologyService _topologyService;

    public ClientServiceCatalogService(
        ISubscriptionRepository subscriptions,
        ICommercialRepository commercialRepository,
        ICommercialOfferTopologyService topologyService)
    {
        _subscriptions = subscriptions;
        _commercialRepository = commercialRepository;
        _topologyService = topologyService;
    }

    public async Task<IReadOnlyList<ServiceSummary>> GetServicesAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        var catalog = await _commercialRepository.GetAdminCatalogAsync(
            cancellationToken);
        var offersById = catalog.ToDictionary(
            offer => offer.Id,
            StringComparer.Ordinal);
        var offersByExternalReference = catalog
            .Where(offer => !string.IsNullOrWhiteSpace(offer.ExternalReference))
            .GroupBy(
                offer => offer.ExternalReference!.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(offer => offer.UpdatedAt, StringComparer.Ordinal)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var buckets = new Dictionary<string, List<ServiceEntitlementSource>>(
            StringComparer.OrdinalIgnoreCase);

        var subscriptions = await _subscriptions.GetByCustomerAsync(
            session.CustomerId,
            cancellationToken);
        foreach (var subscription in subscriptions)
        {
            var technicalServiceReferences =
                await _topologyService.ResolveTechnicalServiceReferencesAsync(
                    subscription,
                    cancellationToken);
            var serviceStatus = MapSubscriptionStatus(subscription.Status);
            if (technicalServiceReferences.Count == 0 || serviceStatus is null)
            {
                continue;
            }

            foreach (var technicalServiceReference in technicalServiceReferences)
            {
                AddSource(
                    buckets,
                    technicalServiceReference,
                    new ServiceEntitlementSource(
                        "subscription",
                        subscription.Id,
                        subscription.OfferName,
                        serviceStatus,
                        subscription.StartedAt ?? subscription.CreatedAt,
                        serviceStatus == "pending"
                            ? CreatePendingSubscriptionMessage(subscription.Status)
                            : null));
            }
        }

        var documentSummaries = await _commercialRepository.GetClientDocumentsAsync(
            session,
            cancellationToken);
        foreach (var summary in documentSummaries.Where(summary =>
                     summary.Status != "cancelled"))
        {
            var detail = await _commercialRepository.GetClientDocumentAsync(
                session,
                summary.Id,
                cancellationToken);
            if (detail is null)
            {
                continue;
            }

            var serviceStatus = MapDocumentStatus(detail.Status);
            if (serviceStatus is null)
            {
                continue;
            }

            foreach (var line in detail.Lines)
            {
                if (line.OfferId is null
                    || !offersById.TryGetValue(line.OfferId, out var offer))
                {
                    continue;
                }

                foreach (var technicalServiceReference in ResolveTechnicalRefsForOffer(offer))
                {
                    AddSource(
                        buckets,
                        technicalServiceReference,
                        new ServiceEntitlementSource(
                            "document",
                            detail.Id,
                            detail.Title,
                            serviceStatus,
                            detail.UpdatedAt,
                            serviceStatus == "pending"
                                ? "Option enregistrée, en attente de règlement ou d'activation."
                                : null));
                }
            }
        }

        return buckets
            .Select(bucket => BuildServiceSummary(
                bucket.Key,
                bucket.Value,
                offersByExternalReference))
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
        string technicalServiceReference,
        ServiceEntitlementSource source)
    {
        var normalizedReference = technicalServiceReference.Trim();
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

    private static IReadOnlyList<string> ResolveTechnicalRefsForOffer(
        CommercialOfferSummary offer)
    {
        if (offer.TechnicalServiceReferences.Count > 0)
        {
            return offer.TechnicalServiceReferences;
        }

        if (!string.IsNullOrWhiteSpace(offer.ExternalReference)
            && offer.ProvisioningGroupSamAccountNames.Count > 0)
        {
            return [offer.ExternalReference];
        }

        return Array.Empty<string>();
    }

    private static ServiceSummary BuildServiceSummary(
        string technicalServiceReference,
        IReadOnlyList<ServiceEntitlementSource> sources,
        IReadOnlyDictionary<string, CommercialOfferSummary> offersByExternalReference)
    {
        offersByExternalReference.TryGetValue(
            technicalServiceReference,
            out var catalogOffer);
        var status = ResolvePortalStatus(sources);
        var startedAt = sources
            .Select(source => source.StartedAt)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
        var sourceLabels = sources
            .Select(source => source.SourceLabel)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ServiceSummary(
            technicalServiceReference,
            technicalServiceReference,
            catalogOffer?.Name ?? CreateFallbackLabel(technicalServiceReference),
            InferServiceType(catalogOffer, technicalServiceReference),
            status,
            catalogOffer?.Description
                ?? $"Service dérivé du catalogue pour {technicalServiceReference}.",
            startedAt,
            sourceLabels.Length == 0
                ? "Aucun rattachement commercial détaillé."
                : $"Couvert via : {string.Join(", ", sourceLabels)}",
            sources.Any(source => source.Kind == "document")
                ? "Option rattachée à vos achats"
                : "Inclus dans vos souscriptions",
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

        if (sources.Any(source => source.Status == "pending"))
        {
            return "pending";
        }

        return "suspended";
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

    private static string? MapDocumentStatus(string documentStatus)
        => documentStatus switch
        {
            "paid" => "active",
            "draft" or "pending_review" or "shared_with_customer" or "issued" =>
                "pending",
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
        CommercialOfferSummary? offer,
        string technicalServiceReference)
    {
        var haystack = string.Join(
            ' ',
            new[]
            {
                technicalServiceReference,
                offer?.Name,
                offer?.Category,
                offer?.Description
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

    private static string CreateFallbackLabel(string technicalServiceReference)
    {
        var tokens = technicalServiceReference
            .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.ToLowerInvariant())
            .Select(token => token.Length switch
            {
                0 => token,
                1 => token.ToUpperInvariant(),
                _ => char.ToUpperInvariant(token[0]) + token[1..]
            });
        return string.Join(" ", tokens);
    }
}

internal sealed record ServiceEntitlementSource(
    string Kind,
    string SourceId,
    string SourceLabel,
    string Status,
    string? StartedAt,
    string? NextStep);
