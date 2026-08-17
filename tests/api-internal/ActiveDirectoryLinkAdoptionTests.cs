using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Regles d'adoption d'une identite Active Directory, sur la persistance mock.
///
/// Ce que ces tests couvrent est la DECISION : qui a le droit de reprendre un
/// objet annuaire deja connu. Ce qu'ils ne peuvent pas couvrir est l'ECRITURE
/// SQL elle-meme (colonne oubliee dans un UPDATE, verrouillage, rollback) :
/// pour cela, voir
/// <see cref="ActiveDirectoryLinkRepositorySchemaTests"/>, qui exige une
/// MariaDB reelle.
/// </summary>
public static class ActiveDirectoryLinkAdoptionTests
{
    private const string CustomerReference = "CLI-DEMO-0042";

    public static async Task RunAsync()
    {
        await VerifyForeignOwnerIsNeverTransferredAsync();
        await VerifySameOwnerKeepsAdoptingInPlaceAsync();
        await VerifyUnownedObjectStaysAdoptableAsync();
    }

    /// <summary>
    /// Q possede l'objet B, P n'a aucun lien : l'upsert de P sur B doit etre
    /// refuse, et Q rester proprietaire.
    /// </summary>
    private static async Task VerifyForeignOwnerIsNeverTransferredAsync()
    {
        var repository = new MockActiveDirectoryLinkRepository();
        var contested = Guid.NewGuid().ToString("D");
        var owner = NewPortalUserId();
        var claimant = NewPortalUserId();

        var ownerLink = await UpsertAsync(
            repository, owner, contested, "proprietaire");

        var refused = false;
        try
        {
            await UpsertAsync(repository, claimant, contested, "revendicateur");
        }
        catch (AmbiguousAdLinkException exception)
        {
            refused = true;
            Ensure(
                string.Equals(
                    exception.ObjectGuidLinkPortalUserId,
                    owner,
                    StringComparison.Ordinal),
                "Le refus doit nommer l'utilisateur portail proprietaire.");
            Ensure(
                string.Equals(
                    exception.ObjectGuidLinkId,
                    ownerLink.Id,
                    StringComparison.Ordinal),
                "Le refus doit nommer le lien deja en place.");
            Ensure(
                exception.PortalUserLinkId is null,
                "Le revendicateur n'a aucun lien : rien ne doit etre nomme de "
                + "son cote.");
        }

        Ensure(
            refused,
            "Un objet annuaire deja rattache a un autre utilisateur portail ne "
            + "doit jamais etre transfere par un upsert.");

        var ownerAfter = await repository.FindUserLinkByPortalUserIdAsync(
            owner, CancellationToken.None);
        Ensure(
            ownerAfter is not null
            && string.Equals(ownerAfter.Id, ownerLink.Id, StringComparison.Ordinal)
            && string.Equals(
                ownerAfter.ObjectGuid, contested, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                ownerAfter.SamAccountName,
                "proprietaire",
                StringComparison.Ordinal),
            "Le proprietaire doit conserver son lien, inchange.");

        var claimantAfter = await repository.FindUserLinkByPortalUserIdAsync(
            claimant, CancellationToken.None);
        Ensure(
            claimantAfter is null,
            "Le revendicateur ne doit obtenir aucun lien.");
    }

    /// <summary>
    /// Le meme utilisateur portail reste libre d'adopter une identite recreee :
    /// c'est le scenario legitime, il ne doit pas etre pris dans le refus.
    /// </summary>
    private static async Task VerifySameOwnerKeepsAdoptingInPlaceAsync()
    {
        var repository = new MockActiveDirectoryLinkRepository();
        var portalUserId = NewPortalUserId();
        var oldGuid = Guid.NewGuid().ToString("D");
        var newGuid = Guid.NewGuid().ToString("D");

        var created = await UpsertAsync(
            repository, portalUserId, oldGuid, "ancien");
        var adopted = await UpsertAsync(
            repository, portalUserId, newGuid, "nouveau");

        Ensure(
            string.Equals(created.Id, adopted.Id, StringComparison.Ordinal),
            "L'adoption par le meme utilisateur doit reconduire le meme lien.");

        var stored = await repository.FindUserLinkByPortalUserIdAsync(
            portalUserId, CancellationToken.None);
        Ensure(
            stored is not null
            && string.Equals(
                stored.ObjectGuid, newGuid, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                stored.SamAccountName, "nouveau", StringComparison.Ordinal),
            "Le lien doit porter la nouvelle identite.");

        // Reposer le MEME objet sur le MEME utilisateur reste legitime.
        var again = await UpsertAsync(
            repository, portalUserId, newGuid, "nouveau");
        Ensure(
            string.Equals(again.Id, adopted.Id, StringComparison.Ordinal),
            "Reposer le meme objet sur le meme utilisateur doit rester permis.");
    }

    /// <summary>
    /// Un objet qu'aucun utilisateur portail ne porte reste adoptable : le
    /// refus doit viser le proprietaire, pas la simple existence du lien.
    /// </summary>
    private static async Task VerifyUnownedObjectStaysAdoptableAsync()
    {
        var repository = new MockActiveDirectoryLinkRepository();
        var guid = Guid.NewGuid().ToString("D");
        var portalUserId = NewPortalUserId();

        // Lien de niveau client : aucun portal_user_id ne le porte.
        await repository.UpsertCustomerLinkAsync(
            CustomerReference,
            actorUserId: null,
            DirectoryObject(guid, "sans-proprietaire"),
            CancellationToken.None);

        var adopted = await UpsertAsync(
            repository, portalUserId, guid, "sans-proprietaire");

        var stored = await repository.FindUserLinkByPortalUserIdAsync(
            portalUserId, CancellationToken.None);
        Ensure(
            stored is not null
            && string.Equals(stored.Id, adopted.Id, StringComparison.Ordinal)
            && string.Equals(
                stored.ObjectGuid, guid, StringComparison.OrdinalIgnoreCase),
            "Un objet sans proprietaire doit rester adoptable.");
    }

    private static Task<CustomerAdLinkUpsertResult> UpsertAsync(
        MockActiveDirectoryLinkRepository repository,
        string portalUserId,
        string objectGuid,
        string samAccountName)
        => repository.UpsertPortalUserLinkAsync(
            CustomerReference,
            portalUserId,
            actorUserId: null,
            DirectoryObject(objectGuid, samAccountName),
            adDomain: "clients.home.bzh",
            adProvisioningStatus: "succeeded",
            adProvisionedAtUtc: DateTime.UtcNow,
            lastPasswordSyncStatus: null,
            lastPasswordSyncAtUtc: null,
            koxoExportStatus: "exported",
            CancellationToken.None);

    private static AdDirectoryObjectSummary DirectoryObject(
        string objectGuid,
        string samAccountName)
        => new(
            objectGuid,
            $"S-1-5-21-{samAccountName}",
            "user",
            samAccountName,
            $"{samAccountName}@clients.home.bzh",
            samAccountName,
            $"CN={samAccountName},OU={CustomerReference},OU=KoXoAdm,"
            + "DC=clients,DC=home,DC=bzh",
            CustomerReference,
            IsDisabled: false);

    // Le mock partage son etat entre instances : chaque scenario prend des
    // identifiants neufs pour ne pas heriter du precedent.
    private static string NewPortalUserId() => Guid.NewGuid().ToString("D");

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
