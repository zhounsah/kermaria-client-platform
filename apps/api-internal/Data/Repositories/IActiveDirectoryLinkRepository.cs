using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record AdCustomerContext(
    string CustomerId,
    string CustomerReference,
    string DisplayName);

public sealed record CustomerAdLinkUpsertResult(
    string Id,
    bool Changed);

/// <summary>
/// Un lien Active Directory ne peut pas etre attribue sans ecraser une
/// attribution existante.
/// </summary>
/// <remarks>
/// Deux formes, un seul refus : soit deux liens distincts revendiquent la meme
/// adoption (l'un porte deja cet utilisateur portail, l'autre porte deja cet
/// objet annuaire), soit l'objet annuaire appartient deja a un AUTRE
/// utilisateur portail. Dans les deux cas, poursuivre transfererait
/// silencieusement une identite d'un utilisateur a un autre. On refuse, et
/// l'arbitrage revient a un humain.
/// </remarks>
public sealed class AmbiguousAdLinkException : Exception
{
    public AmbiguousAdLinkException(
        string portalUserId,
        string? portalUserLinkId,
        string objectGuidLinkId,
        string? objectGuidLinkPortalUserId = null)
        : base(
            BuildMessage(
                portalUserId,
                portalUserLinkId,
                objectGuidLinkId,
                objectGuidLinkPortalUserId))
    {
        PortalUserId = portalUserId;
        PortalUserLinkId = portalUserLinkId;
        ObjectGuidLinkId = objectGuidLinkId;
        ObjectGuidLinkPortalUserId = objectGuidLinkPortalUserId;
    }

    /// <summary>Utilisateur portail pour lequel l'ecriture a ete demandee.</summary>
    public string PortalUserId { get; }

    /// <summary>
    /// Lien portant deja cet utilisateur portail, s'il en existe un.
    /// </summary>
    public string? PortalUserLinkId { get; }

    /// <summary>Lien portant deja l'objet annuaire demande.</summary>
    public string ObjectGuidLinkId { get; }

    /// <summary>
    /// Utilisateur portail proprietaire de ce lien, quand il en a un et qu'il
    /// differe de celui demande.
    /// </summary>
    public string? ObjectGuidLinkPortalUserId { get; }

    private static string BuildMessage(
        string portalUserId,
        string? portalUserLinkId,
        string objectGuidLinkId,
        string? objectGuidLinkPortalUserId)
        => objectGuidLinkPortalUserId is not null
            ? $"The requested directory object is already linked by "
                + $"'{objectGuidLinkId}' on behalf of portal user "
                + $"'{objectGuidLinkPortalUserId}'. Refusing to transfer it to "
                + $"portal user '{portalUserId}'."
            : $"Portal user '{portalUserId}' is already linked by "
                + $"'{portalUserLinkId}' while the requested directory object is "
                + $"already linked by '{objectGuidLinkId}'. Refusing to merge two "
                + "Active Directory links.";
}

public sealed record PortalUserAdLinkRecord(
    string Id,
    string CustomerId,
    string CustomerReference,
    string PortalUserId,
    string ObjectGuid,
    string ObjectSid,
    string SamAccountName,
    string? UserPrincipalName,
    string DisplayName,
    string DistinguishedName,
    string? AdDomain,
    string? AdProvisioningStatus,
    DateTime? AdProvisionedAtUtc,
    DateTime? LastPasswordSyncAtUtc,
    string? LastPasswordSyncStatus,
    string? KoxoExportStatus);

public interface IActiveDirectoryLinkRepository
{
    bool IsPersistent { get; }

    Task<AdCustomerContext?> GetCustomerContextAsync(
        string customerReference,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerLinksAsync(
        string customerReference,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerAdLinkSummary>> GetCustomerUserLinksAsync(
        string customerId,
        CancellationToken cancellationToken);
    Task<CustomerAdLinkUpsertResult> UpsertCustomerLinkAsync(
        string customerReference,
        string? actorUserId,
        AdDirectoryObjectSummary directoryObject,
        CancellationToken cancellationToken);
    Task<CustomerAdLinkUpsertResult> UpsertPortalUserLinkAsync(
        string customerReference,
        string portalUserId,
        string? actorUserId,
        AdDirectoryObjectSummary directoryObject,
        string? adDomain,
        string? adProvisioningStatus,
        DateTime? adProvisionedAtUtc,
        string? lastPasswordSyncStatus,
        DateTime? lastPasswordSyncAtUtc,
        string? koxoExportStatus,
        CancellationToken cancellationToken);
    Task<bool> UpdateUserPasswordSyncStatusAsync(
        string portalUserId,
        string status,
        DateTime changedAtUtc,
        CancellationToken cancellationToken);
    Task<bool> DeleteCustomerLinkAsync(
        string customerReference,
        string linkId,
        CancellationToken cancellationToken);
    Task<bool> RefreshCustomerLinkAsync(
        string targetCustomerReference,
        AdDirectoryObjectSummary directoryObject,
        CancellationToken cancellationToken);
    Task<CustomerAdLinkSummary?> FindUserLinkByEmailAsync(
        string customerReference,
        string email,
        CancellationToken cancellationToken);
    Task<PortalUserAdLinkRecord?> FindUserLinkByPortalUserIdAsync(
        string portalUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retourne <b>tous</b> les liens Active Directory rattaches a cet
    /// utilisateur portail.
    /// </summary>
    /// <remarks>
    /// Volontairement distincte de
    /// <see cref="FindUserLinkByPortalUserIdAsync"/>, qui borne a un seul
    /// resultat : quand le resultat sert a donner des droits reels, un doublon
    /// doit etre visible et bloquant, pas resolu au hasard.
    /// </remarks>
    Task<IReadOnlyList<PortalUserAdLinkRecord>> GetUserLinksByPortalUserIdAsync(
        string portalUserId,
        CancellationToken cancellationToken);
}
