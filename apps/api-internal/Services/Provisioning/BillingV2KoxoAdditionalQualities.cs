namespace Kermaria.ApiInternal.Services.Provisioning;

public interface IKoxoAdditionalQualitiesProvider
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetByIdentityAsync(
        string customerId, CancellationToken cancellationToken);
}

public sealed partial class BillingV2ProvisioningService : IKoxoAdditionalQualitiesProvider
{
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetByIdentityAsync(
        string customerId, CancellationToken cancellationToken)
    {
        if (_qualityIntents is not null)
        {
            var intent = await _qualityIntents.ReadAsync(customerId, cancellationToken);
            if (intent is not null)
            {
                var desired = KoxoQualityIntentPolicy.Deserialize(intent.DesiredJson);
                if (!KoxoQualityGroupScope.Allows(_qualityAdScope, _provisioningConfiguration, desired.Values.SelectMany(groups => groups)))
                    throw new InvalidOperationException("KOXO_QUALITY_GROUP_OUTSIDE_ALLOWED_ROOTS");
                return desired;
            }
        }
        // Avant la premiere demande KoXo, conserver le dernier etat applique
        // et suivi par l'API. Le catalogue ne doit pas etre reevalue pendant
        // l'export global : un client mal configure bloquerait les autres.
        // Les nouvelles decisions passent exclusivement par la publication
        // apres gates, et non par une interpretation opportuniste du CSV.
        var owned = await LoadOwnedMembershipsAsync(customerId, cancellationToken);
        if (_qualityIntents is not null && !KoxoQualityGroupScope.Allows(_qualityAdScope,
            _provisioningConfiguration, owned.Select(entry => entry.GroupSamAccountName)))
            throw new InvalidOperationException("KOXO_QUALITY_GROUP_OUTSIDE_ALLOWED_ROOTS");
        return KoxoAdditionalQualitiesProjection.FromManagedMemberships(
            owned.Select(entry => (entry.IdentityReference, entry.GroupSamAccountName)));
    }
}

public static class KoxoAdditionalQualitiesProjection
{
    // Etat de transition : uniquement les ajouts actifs deja appliques/suivis.
    // Pas de lecture memberOf, pas de recopie des droits d'un autre utilisateur.
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> FromManagedMemberships(
        IEnumerable<(string IdentityReference, string GroupSamAccountName)> owned)
    {
        return owned.GroupBy(entry => entry.IdentityReference, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group
                .Select(entry => entry.GroupSamAccountName).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.Ordinal);
    }
}
