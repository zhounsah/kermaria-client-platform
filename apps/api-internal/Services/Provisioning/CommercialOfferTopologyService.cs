using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record CatalogTechnicalServiceDefinition(
    string TechnicalServiceReference,
    string Label,
    IReadOnlyList<string> GroupSamAccountNames);

public interface ICommercialOfferTopologyService
{
    Task<IReadOnlyList<string>> ResolveMappedGroupsAsync(
        SubscriptionSummary subscription,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ResolveTechnicalServiceReferencesAsync(
        SubscriptionSummary subscription,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ResolveServiceMappedGroupsAsync(
        string technicalServiceReference,
        CancellationToken cancellationToken);

    Task<string> ResolveServiceLabelAsync(
        string technicalServiceReference,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogTechnicalServiceDefinition>> GetTechnicalServicesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetManagedGroupSamAccountNamesAsync(
        CancellationToken cancellationToken);
}

public sealed class CommercialOfferTopologyService
    : ICommercialOfferTopologyService
{
    private readonly ICommercialRepository _commercialRepository;
    private Task<CatalogTopologySnapshot>? _snapshotTask;

    public CommercialOfferTopologyService(ICommercialRepository commercialRepository)
    {
        _commercialRepository = commercialRepository;
    }

    public async Task<IReadOnlyList<string>> ResolveMappedGroupsAsync(
        SubscriptionSummary subscription,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        if (!snapshot.OffersByExternalReference.TryGetValue(
                Normalize(subscription.OfferExternalReference),
                out var offer))
        {
            return Array.Empty<string>();
        }

        var groups = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in offer.ProvisioningGroupSamAccountNames)
        {
            groups.Add(group);
        }

        foreach (var technicalServiceReference in ResolveTechnicalRefsForOffer(offer))
        {
            foreach (var group in ResolveServiceGroups(snapshot, technicalServiceReference))
            {
                groups.Add(group);
            }
        }

        return groups.ToArray();
    }

    public async Task<IReadOnlyList<string>> ResolveTechnicalServiceReferencesAsync(
        SubscriptionSummary subscription,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.OffersByExternalReference.TryGetValue(
            Normalize(subscription.OfferExternalReference),
            out var offer)
            ? ResolveTechnicalRefsForOffer(offer)
            : Array.Empty<string>();
    }

    public async Task<IReadOnlyList<string>> ResolveServiceMappedGroupsAsync(
        string technicalServiceReference,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return ResolveServiceGroups(snapshot, technicalServiceReference);
    }

    public async Task<string> ResolveServiceLabelAsync(
        string technicalServiceReference,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        var normalizedReference = Normalize(technicalServiceReference);
        if (normalizedReference.Length == 0)
        {
            return "Service";
        }

        if (snapshot.OffersByExternalReference.TryGetValue(
                normalizedReference,
                out var offer)
            && !string.IsNullOrWhiteSpace(offer.Name))
        {
            return offer.Name;
        }

        return CreateFallbackLabel(normalizedReference);
    }

    public async Task<IReadOnlyList<CatalogTechnicalServiceDefinition>>
        GetTechnicalServicesAsync(
            CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.TechnicalServices;
    }

    public async Task<IReadOnlyList<string>> GetManagedGroupSamAccountNamesAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.ManagedGroupSamAccountNames;
    }

    private Task<CatalogTopologySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken)
        => _snapshotTask ??= LoadSnapshotAsync(cancellationToken);

    private async Task<CatalogTopologySnapshot> LoadSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var offers = await _commercialRepository.GetAdminCatalogAsync(
            cancellationToken);
        var offersByExternalReference = offers
            .Where(offer => !string.IsNullOrWhiteSpace(offer.ExternalReference))
            .GroupBy(
                offer => Normalize(offer.ExternalReference),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(offer => offer.UpdatedAt, StringComparer.Ordinal)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var technicalServiceReferences = new SortedSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var offer in offers)
        {
            foreach (var technicalServiceReference in ResolveTechnicalRefsForOffer(offer))
            {
                technicalServiceReferences.Add(technicalServiceReference);
            }
        }

        var technicalServices = technicalServiceReferences
            .Select(reference => new CatalogTechnicalServiceDefinition(
                reference,
                offersByExternalReference.TryGetValue(reference, out var offer)
                    ? offer.Name
                    : CreateFallbackLabel(reference),
                ResolveServiceGroups(
                    new CatalogTopologySnapshot(
                        offersByExternalReference,
                        Array.Empty<CatalogTechnicalServiceDefinition>(),
                        Array.Empty<string>()),
                    reference)))
            .OrderBy(service => service.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var managedGroups = technicalServices
            .SelectMany(service => service.GroupSamAccountNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CatalogTopologySnapshot(
            offersByExternalReference,
            technicalServices,
            managedGroups);
    }

    private static IReadOnlyList<string> ResolveTechnicalRefsForOffer(
        CommercialOfferSummary offer)
    {
        var explicitReferences = offer.TechnicalServiceReferences
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (explicitReferences.Length > 0)
        {
            return explicitReferences;
        }

        if (!string.IsNullOrWhiteSpace(offer.ExternalReference)
            && offer.ProvisioningGroupSamAccountNames.Count > 0)
        {
            return [offer.ExternalReference.Trim()];
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> ResolveServiceGroups(
        CatalogTopologySnapshot snapshot,
        string technicalServiceReference)
    {
        var normalizedReference = Normalize(technicalServiceReference);
        if (normalizedReference.Length == 0
            || !snapshot.OffersByExternalReference.TryGetValue(
                normalizedReference,
                out var offer))
        {
            return Array.Empty<string>();
        }

        return offer.ProvisioningGroupSamAccountNames
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

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

internal sealed record CatalogTopologySnapshot(
    IReadOnlyDictionary<string, CommercialOfferSummary> OffersByExternalReference,
    IReadOnlyList<CatalogTechnicalServiceDefinition> TechnicalServices,
    IReadOnlyList<string> ManagedGroupSamAccountNames);
