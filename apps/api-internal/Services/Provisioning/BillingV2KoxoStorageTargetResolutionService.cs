using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services.Provisioning;

public interface IBillingV2KoxoStorageTargetResolutionService
{
    Task<BillingV2KoxoStorageTargetResolution> ResolveAsync(
        string customerId,
        IReadOnlyList<BillingV2StorageQuotaPlan> quotas,
        CancellationToken cancellationToken);
}

/// <summary>
/// Alimente le resolver de cibles KoXo a partir des donnees reelles, en lecture
/// seule.
/// </summary>
/// <remarks>
/// <para>
/// Cette couche fait exactement une chose : transformer des plans de quota en
/// instantanes verifies, puis appeler
/// <see cref="BillingV2KoxoStorageTargetResolver"/>. Elle n'applique aucun
/// quota, ne cree aucune identite, n'ecrit ni en base, ni dans l'annuaire, ni
/// vers KoXo. Toutes ses dependances ne sont sollicitees que par des methodes
/// de lecture.
/// </para>
/// <para>
/// Le decoupage est volontaire : le resolver reste une fonction pure, testable
/// sans base ni annuaire, et c'est ici — et seulement ici — que vivent les
/// entrees/sorties. Melanger les deux rendrait les invariants d'identite
/// intestables autrement qu'avec une infrastructure complete.
/// </para>
/// <para>
/// La resolution est globale : une seule ligne douteuse refuse tout le lot.
/// Rendre le sous-ensemble compris laisserait croire l'abonnement provisionne
/// alors qu'une partie des quotas ne l'est pas.
/// </para>
/// </remarks>
public sealed class BillingV2KoxoStorageTargetResolutionService
    : IBillingV2KoxoStorageTargetResolutionService
{
    private readonly IBillingV2KoxoTargetingRepository _targeting;
    private readonly IActiveDirectoryLinkRepository _links;
    private readonly IAdGroupProvisioner _directory;

    public BillingV2KoxoStorageTargetResolutionService(
        IBillingV2KoxoTargetingRepository targeting,
        IActiveDirectoryLinkRepository links,
        IAdGroupProvisioner directory)
    {
        _targeting = targeting;
        _links = links;
        _directory = directory;
    }

    public async Task<BillingV2KoxoStorageTargetResolution> ResolveAsync(
        string customerId,
        IReadOnlyList<BillingV2StorageQuotaPlan> quotas,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return BillingV2KoxoStorageTargetResolution.Fail(
                BillingV2KoxoStorageTargetReasons.CustomerNotFound);
        }

        if (quotas.Count == 0)
        {
            return BillingV2KoxoStorageTargetResolver.Resolve(
                customerId,
                quotas,
                new Dictionary<string, BillingV2KoxoUserIdentitySnapshot>(
                    StringComparer.Ordinal),
                secondaryGroup: null);
        }

        // Les references sont dedupliquees avant toute lecture : deux plans du
        // meme utilisateur ne doivent pas produire deux interrogations de la
        // base et deux recherches LDAP identiques.
        var identityReferences = quotas
            .Where(quota => RequiresUserIdentity(quota))
            .Select(quota => quota.IdentityReference?.Trim())
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();

        var snapshots =
            new Dictionary<string, BillingV2KoxoUserIdentitySnapshot>(
                StringComparer.Ordinal);
        foreach (var identityReference in identityReferences)
        {
            var attempt = await BuildUserSnapshotAsync(
                customerId,
                identityReference,
                cancellationToken);
            if (attempt.ReasonCode is string failure)
            {
                return BillingV2KoxoStorageTargetResolution.Fail(failure);
            }

            snapshots[identityReference] = attempt.Snapshot!;
        }

        BillingV2KoxoSecondaryGroupSnapshot? secondaryGroup = null;
        if (quotas.Any(RequiresSecondaryGroup))
        {
            var customer = await _targeting.FindCustomerAsync(
                customerId,
                cancellationToken);
            if (customer is null)
            {
                return BillingV2KoxoStorageTargetResolution.Fail(
                    BillingV2KoxoStorageTargetReasons.CustomerNotFound);
            }

            // Le nommage reste entierement dans KoxoDirectoryTopology : ce
            // service ne transporte que l'etat brut du client.
            secondaryGroup = new BillingV2KoxoSecondaryGroupSnapshot(
                customer.CustomerId,
                customer.IsDemo,
                customer.KoxoGroupReference,
                customer.CustomerReference);
        }

        return BillingV2KoxoStorageTargetResolver.Resolve(
            customerId,
            quotas,
            snapshots,
            secondaryGroup);
    }

    /// <summary>
    /// Construit l'instantane d'une identite, ou dit pourquoi il est refuse.
    /// </summary>
    /// <remarks>
    /// La chaine suivie est celle de la production :
    /// <c>portal_users.id</c> → <c>koxo_unique_identifier</c> →
    /// <c>employeeNumber</c> → objet d'annuaire. Aucun maillon n'est devine :
    /// le <c>sAMAccountName</c> n'est jamais predit, il est lu.
    /// </remarks>
    private async Task<UserSnapshotAttempt> BuildUserSnapshotAsync(
        string customerId,
        string identityReference,
        CancellationToken cancellationToken)
    {
        var portalUser = await _targeting.FindPortalUserAsync(
            customerId,
            identityReference,
            cancellationToken);
        if (portalUser is null)
        {
            return UserSnapshotAttempt.Fail(
                BillingV2KoxoStorageTargetReasons.PortalUserNotFound);
        }

        // Defense en profondeur : la requete est deja bornee par le client,
        // mais le service ne prend pas cette borne pour acquise.
        if (!string.Equals(
                portalUser.CustomerId,
                customerId,
                StringComparison.Ordinal)
            || !string.Equals(
                portalUser.PortalUserId,
                identityReference,
                StringComparison.Ordinal))
        {
            return UserSnapshotAttempt.Fail(
                BillingV2KoxoStorageTargetReasons.PortalUserCustomerMismatch);
        }

        // Tous les liens, jamais un seul : un doublon doit rester visible pour
        // que le resolver le refuse, au lieu d'etre masque par un Find.
        var links = await _links.GetUserLinksByPortalUserIdAsync(
            identityReference,
            cancellationToken);

        // Les references client portees par les objets lus doivent designer le
        // meme client. Une reference absente n'est pas une contradiction : elle
        // n'apporte simplement rien.
        foreach (var link in links)
        {
            if (!ReferencesAgree(link.CustomerReference, portalUser.CustomerReference))
            {
                return UserSnapshotAttempt.Fail(
                    BillingV2KoxoStorageTargetReasons.CustomerReferenceMismatch);
            }
        }

        // L'identifiant unique est la seule cle de rattachement : sans lui, il
        // n'y a rien a chercher dans l'annuaire, et rien a inventer non plus.
        var employeeNumber = portalUser.KoxoUniqueIdentifier?.Trim();
        if (!KoxoDirectoryTopology.IsValidUniqueIdentifier(employeeNumber))
        {
            return UserSnapshotAttempt.Fail(
                BillingV2KoxoStorageTargetReasons.EmployeeNumberInvalid);
        }

        var directoryObject = await _directory.ResolveUserByEmployeeNumberAsync(
            employeeNumber!,
            cancellationToken);
        if (directoryObject is not null
            && !ReferencesAgree(
                directoryObject.CustomerReference,
                portalUser.CustomerReference))
        {
            return UserSnapshotAttempt.Fail(
                BillingV2KoxoStorageTargetReasons.CustomerReferenceMismatch);
        }

        return UserSnapshotAttempt.Success(
            new BillingV2KoxoUserIdentitySnapshot(
                portalUser.PortalUserId,
                employeeNumber,
                links,
                directoryObject));
    }

    /// <summary>
    /// Deux references client sont d'accord, ou l'une d'elles est absente.
    /// </summary>
    /// <remarks>
    /// Le chemin LDAP actuel laisse
    /// <see cref="AdDirectoryObjectSummary.CustomerReference"/> vide sur une
    /// recherche par <c>employeeNumber</c> : la valeur n'est pas fausse, elle
    /// n'est pas renseignee. La traiter comme un desaccord interdirait toute
    /// resolution. Aucune reference n'est reconstruite a partir du DN : un
    /// decoupage de chaine sur un nom distingue serait une devinette.
    /// </remarks>
    private static bool ReferencesAgree(string? candidate, string expected)
        => string.IsNullOrWhiteSpace(candidate)
            || string.Equals(
                candidate.Trim(),
                expected.Trim(),
                StringComparison.Ordinal);

    private static bool RequiresUserIdentity(BillingV2StorageQuotaPlan quota)
        => string.Equals(
            quota.TargetType?.Trim(),
            BillingV2ProvisioningRuleSemantics.KoxoUserStorageTarget,
            StringComparison.OrdinalIgnoreCase);

    private static bool RequiresSecondaryGroup(BillingV2StorageQuotaPlan quota)
        => string.Equals(
            quota.TargetType?.Trim(),
            BillingV2ProvisioningRuleSemantics.KoxoSecondaryGroupStorageTarget,
            StringComparison.OrdinalIgnoreCase);

    private sealed record UserSnapshotAttempt(
        BillingV2KoxoUserIdentitySnapshot? Snapshot,
        string? ReasonCode)
    {
        public static UserSnapshotAttempt Fail(string reasonCode)
            => new(null, reasonCode);

        public static UserSnapshotAttempt Success(
            BillingV2KoxoUserIdentitySnapshot snapshot)
            => new(snapshot, null);
    }
}
