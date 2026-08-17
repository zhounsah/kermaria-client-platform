using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services.Provisioning;

/// <summary>
/// Nature de l'objet KoXo qui portera reellement le quota.
/// </summary>
/// <remarks>
/// Les deux cibles n'ont ni la meme cle, ni le meme mode de creation : une
/// fiche utilisateur est retrouvee par <c>employeeNumber</c> et doit deja
/// exister, alors qu'une OU de groupe secondaire est nommee par la topologie et
/// creee par KoXo si elle manque. Les confondre reviendrait a poser un quota
/// personnel sur le dossier partage du client, ou l'inverse.
/// </remarks>
public enum BillingV2KoxoStorageTargetKind
{
    /// <summary>Fiche utilisateur KoXo, identifiee par son employeeNumber.</summary>
    User,

    /// <summary>Groupe secondaire du client, identifie par le nom de son OU.</summary>
    SecondaryGroup,
}

/// <summary>
/// Une intention de quota rattachee a l'objet KoXo reel qui la portera.
/// </summary>
/// <remarks>
/// <para>
/// C'est la traduction du plan (<see cref="BillingV2StorageQuotaPlan"/>) vers
/// l'unite et les cles de KoXo. Le plan reste exprime dans l'unite du catalogue
/// (<c>GiB</c>) ; la cible porte des mebioctets, parce que c'est ce que la fiche
/// KoXo et le quota FSRM attendent.
/// </para>
/// <para>
/// Construire cette cible n'applique rien et n'ecrit nulle part : aucun XML
/// n'est touche, aucun <c>KoXoAdm.exe</c> n'est lance, aucun appel n'est emis
/// vers SRV-21. C'est une description, verifiee avant toute execution.
/// </para>
/// </remarks>
public sealed record BillingV2ResolvedKoxoStorageTarget(
    string SubscriptionItemId,
    BillingV2KoxoStorageTargetKind Kind,
    long QuotaMebibytes,
    string? EmployeeNumber,
    PortalUserAdLinkRecord? AdLink,
    string? PrimaryGroup,
    string? SecondaryGroup)
{
    /// <summary>
    /// Cle d'unicite de la cible, pour interdire deux quotas concurrents sur le
    /// meme objet d'annuaire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// L'identite d'une fiche utilisateur est son <c>objectGUID</c> et non son
    /// <c>employeeNumber</c> : dans une foret multi-domaines seul le premier est
    /// unique et immuable.
    /// </para>
    /// <para>
    /// Un groupe secondaire, lui, n'est identifie que par le couple complet :
    /// <c>CLIENTS/X</c> et <c>CLIENTS DEMO/X</c> sont deux OU distinctes, dans
    /// deux branches que la separation des groupes primaires cloisonne
    /// justement. Ne retenir que le nom secondaire declarerait une collision la
    /// ou il n'y en a pas, et refuserait deux quotas legitimes.
    /// </para>
    /// </remarks>
    public string TargetKey => Kind switch
    {
        BillingV2KoxoStorageTargetKind.User
            => $"user:{AdLink?.ObjectGuid}",
        _ => $"group:{PrimaryGroup}/{SecondaryGroup}",
    };

    public static BillingV2ResolvedKoxoStorageTarget ForUser(
        string subscriptionItemId,
        long quotaMebibytes,
        string employeeNumber,
        PortalUserAdLinkRecord adLink)
        => new(
            subscriptionItemId,
            BillingV2KoxoStorageTargetKind.User,
            quotaMebibytes,
            employeeNumber,
            adLink,
            PrimaryGroup: null,
            SecondaryGroup: null);

    public static BillingV2ResolvedKoxoStorageTarget ForSecondaryGroup(
        string subscriptionItemId,
        long quotaMebibytes,
        string primaryGroup,
        string secondaryGroup)
        => new(
            subscriptionItemId,
            BillingV2KoxoStorageTargetKind.SecondaryGroup,
            quotaMebibytes,
            EmployeeNumber: null,
            AdLink: null,
            primaryGroup,
            secondaryGroup);
}

public sealed record BillingV2KoxoStorageTargetResolution(
    bool Resolved,
    string ReasonCode,
    IReadOnlyList<BillingV2ResolvedKoxoStorageTarget> Targets)
{
    public static BillingV2KoxoStorageTargetResolution Fail(string reasonCode)
        => new(
            false,
            reasonCode,
            Array.Empty<BillingV2ResolvedKoxoStorageTarget>());

    public static BillingV2KoxoStorageTargetResolution Success(
        IReadOnlyList<BillingV2ResolvedKoxoStorageTarget> targets)
        => new(
            true,
            BillingV2KoxoStorageTargetReasons.Resolved,
            targets);
}

public static class BillingV2KoxoStorageTargetReasons
{
    public const string Resolved =
        "BILLING_V2_KOXO_STORAGE_TARGET_RESOLVED";

    public const string TargetTypeUnknown =
        "BILLING_V2_KOXO_STORAGE_TARGET_TYPE_UNKNOWN";

    public const string ScopeIncoherent =
        "BILLING_V2_KOXO_STORAGE_SCOPE_INCOHERENT";

    public const string UnitUnexpected =
        "BILLING_V2_KOXO_STORAGE_UNIT_UNEXPECTED";

    public const string QuotaInvalid =
        "BILLING_V2_KOXO_STORAGE_QUOTA_INVALID";

    public const string QuotaOverflow =
        "BILLING_V2_KOXO_STORAGE_QUOTA_OVERFLOW";

    /// <summary>
    /// Aucun instantane d'identite n'a ete fourni pour cet utilisateur.
    /// </summary>
    /// <remarks>
    /// Un utilisateur non materialise est bloquant. Le fournisseur de quota ne
    /// cree aucune identite : il n'est pas le proprietaire de cette operation et
    /// improviser une creation ici produirait un compte hors du pipeline KoXo.
    /// </remarks>
    public const string IdentityNotMaterialized =
        "BILLING_V2_KOXO_STORAGE_IDENTITY_NOT_MATERIALIZED";

    public const string EmployeeNumberInvalid =
        "BILLING_V2_KOXO_STORAGE_EMPLOYEE_NUMBER_INVALID";

    public const string IdentityNotLinked =
        "BILLING_V2_KOXO_STORAGE_IDENTITY_NOT_LINKED";

    /// <summary>
    /// Plusieurs liens, ou deux quotas visant le meme objet d'annuaire.
    /// </summary>
    public const string IdentityAmbiguous =
        "BILLING_V2_KOXO_STORAGE_IDENTITY_AMBIGUOUS";

    public const string IdentityCustomerMismatch =
        "BILLING_V2_KOXO_STORAGE_IDENTITY_CUSTOMER_MISMATCH";

    /// <summary>
    /// L'instantane fourni ne decrit pas l'utilisateur demande par le quota.
    /// </summary>
    /// <remarks>
    /// La cle du dictionnaire n'est pas une preuve : c'est l'appelant qui la
    /// choisit. Sans cette verification, une couche d'alimentation qui range un
    /// instantane sous la mauvaise cle ferait porter le quota de A par
    /// l'identite de B, et toutes les verifications suivantes reussiraient
    /// puisque l'instantane de B est parfaitement coherent avec lui-meme.
    /// </remarks>
    public const string IdentitySnapshotMismatch =
        "BILLING_V2_KOXO_STORAGE_IDENTITY_SNAPSHOT_MISMATCH";

    /// <summary>
    /// Le lien annuaire retenu appartient a un autre utilisateur portail.
    /// </summary>
    /// <remarks>
    /// Meme raisonnement un cran plus bas : le triplet GUID/SID/sAMAccountName
    /// prouve que le lien et l'objet d'annuaire decrivent le meme compte, pas
    /// que ce compte est celui du <c>portal_users.id</c> qui a paye le quota.
    /// Deux utilisateurs d'un meme client passeraient donc tous les autres
    /// controles.
    /// </remarks>
    public const string IdentityPortalUserMismatch =
        "BILLING_V2_KOXO_STORAGE_IDENTITY_PORTAL_USER_MISMATCH";

    public const string IdentityGuidInvalid =
        "BILLING_V2_KOXO_STORAGE_IDENTITY_GUID_INVALID";

    /// <summary>
    /// L'annuaire ne rend aucune identite pour cet <c>employeeNumber</c>.
    /// </summary>
    /// <remarks>
    /// Couvre aussi le cas de plusieurs correspondances :
    /// <see cref="IAdGroupProvisioner.ResolveUserByEmployeeNumberAsync"/> traite
    /// une correspondance multiple comme une absence, precisement pour qu'aucun
    /// arbitrage implicite ne designe la mauvaise identite.
    /// </remarks>
    public const string DirectoryObjectNotFound =
        "BILLING_V2_KOXO_STORAGE_DIRECTORY_OBJECT_NOT_FOUND";

    public const string DirectoryObjectMismatch =
        "BILLING_V2_KOXO_STORAGE_DIRECTORY_OBJECT_MISMATCH";

    public const string SecondaryGroupUnknown =
        "BILLING_V2_KOXO_STORAGE_SECONDARY_GROUP_UNKNOWN";

    /// <summary>
    /// L'instantane de groupe secondaire decrit un autre client.
    /// </summary>
    /// <remarks>
    /// Le stockage partage est la seule cible qui n'a aucune identite pour la
    /// contredire : rien, dans le nom d'une OU, ne rappelle a quel abonnement
    /// elle appartient. Sans ce controle, une couche d'alimentation qui se
    /// tromperait de client poserait le quota de A sur le dossier partage de B,
    /// et toutes les autres verifications passeraient.
    /// </remarks>
    public const string SecondaryGroupCustomerMismatch =
        "BILLING_V2_KOXO_STORAGE_SECONDARY_GROUP_CUSTOMER_MISMATCH";
}

/// <summary>
/// Tout ce qui est connu d'une identite utilisateur au moment de resoudre son
/// quota, deja lu, sans aucune entree/sortie restante.
/// </summary>
/// <remarks>
/// <para>
/// Les lectures sont faites par l'appelant et passees ici pour que la resolution
/// reste une fonction pure, testable sans annuaire ni base. La chaine reelle est
/// <c>portal_users.id</c> → <c>portal_users.koxo_unique_identifier</c>
/// (<c>CLI-NNNNNN</c>) → attribut AD <c>employeeNumber</c> → objet d'annuaire.
/// </para>
/// <para>
/// <see cref="PortalUserLinks"/> vient volontairement de
/// <see cref="IActiveDirectoryLinkRepository.GetUserLinksByPortalUserIdAsync"/>,
/// qui rend TOUS les liens : un doublon doit etre visible et bloquant, jamais
/// tranche au hasard.
/// </para>
/// </remarks>
public sealed record BillingV2KoxoUserIdentitySnapshot(
    string IdentityReference,
    string? KoxoUniqueIdentifier,
    IReadOnlyList<PortalUserAdLinkRecord> PortalUserLinks,
    AdDirectoryObjectSummary? DirectoryObjectByEmployeeNumber);

/// <summary>
/// Etat du client necessaire pour nommer son OU de groupe secondaire.
/// </summary>
/// <param name="CustomerId">
/// Client auquel cet instantane appartient. Present pour que le resolver puisse
/// verifier qu'il decrit bien le client de l'abonnement en cours, et non un
/// autre : contrairement a une fiche utilisateur, une OU partagee n'a aucune
/// autre attache qui pourrait dementir une erreur d'alimentation.
/// </param>
public sealed record BillingV2KoxoSecondaryGroupSnapshot(
    string CustomerId,
    bool IsDemo,
    string? KoxoGroupReference,
    string CustomerReference);

/// <summary>
/// Sens d'une evolution de quota.
/// </summary>
public enum BillingV2KoxoQuotaTransition
{
    Unchanged,
    Increase,
    Decrease,
}

/// <summary>
/// Politique d'application d'un quota deja resolu.
/// </summary>
/// <remarks>
/// <para>
/// Une reduction n'est pas l'inverse d'une augmentation : abaisser un quota
/// sous l'occupation reelle d'un dossier bloque immediatement l'utilisateur, et
/// aucune donnee n'a ete supprimee pour autant. Tant que la plateforme ne sait
/// pas mesurer l'occupation puis accompagner la baisse, la reduction se refuse.
/// </para>
/// <para>
/// Cette politique n'est pas encore branchee : aucun quota n'est applique dans
/// cette version. Elle existe pour que l'execution de la phase suivante n'ait
/// pas a redecouvrir la regle.
/// </para>
/// </remarks>
public static class BillingV2KoxoQuotaPolicy
{
    public const string DecreaseRefused =
        "BILLING_V2_KOXO_STORAGE_QUOTA_DECREASE_REFUSED";

    public static BillingV2KoxoQuotaTransition Classify(
        long currentMebibytes,
        long desiredMebibytes)
    {
        if (desiredMebibytes > currentMebibytes)
        {
            return BillingV2KoxoQuotaTransition.Increase;
        }

        return desiredMebibytes < currentMebibytes
            ? BillingV2KoxoQuotaTransition.Decrease
            : BillingV2KoxoQuotaTransition.Unchanged;
    }

    public static bool IsApplicable(BillingV2KoxoQuotaTransition transition)
        => transition != BillingV2KoxoQuotaTransition.Decrease;
}

/// <summary>
/// Traduit des plans de quota en cibles KoXo, ou echoue.
/// </summary>
/// <remarks>
/// <para>
/// Fonction pure et fermee par defaut : aucune entree/sortie, aucun appel
/// annuaire, aucune ecriture. La moindre incoherence refuse la resolution
/// ENTIERE plutot que d'appliquer le sous-ensemble qu'elle a compris — un quota
/// pose sur une identite mal rattachee ne se rattrape pas apres coup.
/// </para>
/// <para>
/// Aucun rapprochement approximatif n'est tolere : ni par nom, ni par
/// <c>displayName</c>, ni par <c>sAMAccountName</c> devine. KoXo translittere le
/// nom et derive lui-meme le <c>sAMAccountName</c>, donc aucune de ces valeurs
/// n'est predictible cote application. Le rattachement passe par
/// <c>employeeNumber</c>, et la coherence est verifiee sur le triplet
/// <c>objectGUID</c> / <c>objectSID</c> / <c>sAMAccountName</c>.
/// </para>
/// </remarks>
public static class BillingV2KoxoStorageTargetResolver
{
    /// <summary>
    /// Facteur de conversion de l'unite du catalogue vers celle de KoXo.
    /// </summary>
    /// <remarks>
    /// Le catalogue exprime les tiers en <c>GiB</c> ; la fiche KoXo et le quota
    /// FSRM raisonnent en mebioctets. La conversion est faite ici et nulle part
    /// ailleurs, en <c>checked</c> : un depassement silencieux transformerait un
    /// grand quota en petit quota, donc en blocage utilisateur.
    /// </remarks>
    public const long MebibytesPerGibibyte = 1024;

    public static BillingV2KoxoStorageTargetResolution Resolve(
        string customerId,
        IReadOnlyList<BillingV2StorageQuotaPlan> quotas,
        IReadOnlyDictionary<string, BillingV2KoxoUserIdentitySnapshot>
            identitiesByReference,
        BillingV2KoxoSecondaryGroupSnapshot? secondaryGroup)
    {
        var targets = new List<BillingV2ResolvedKoxoStorageTarget>();
        foreach (var quota in quotas.OrderBy(
            plan => plan.SubscriptionItemId,
            StringComparer.Ordinal))
        {
            if (!TryConvertToMebibytes(quota, out var mebibytes, out var quotaReason))
            {
                return BillingV2KoxoStorageTargetResolution.Fail(quotaReason);
            }

            var targetType = quota.TargetType?.Trim();
            if (Matches(
                targetType,
                BillingV2ProvisioningRuleSemantics.KoxoUserStorageTarget))
            {
                if (!TryResolveUser(
                        customerId,
                        quota,
                        mebibytes,
                        identitiesByReference,
                        out var userTarget,
                        out var userReason))
                {
                    return BillingV2KoxoStorageTargetResolution.Fail(userReason);
                }

                targets.Add(userTarget);
                continue;
            }

            if (Matches(
                targetType,
                BillingV2ProvisioningRuleSemantics
                    .KoxoSecondaryGroupStorageTarget))
            {
                if (!TryResolveSecondaryGroup(
                        customerId,
                        quota,
                        mebibytes,
                        secondaryGroup,
                        out var groupTarget,
                        out var groupReason))
                {
                    return BillingV2KoxoStorageTargetResolution.Fail(groupReason);
                }

                targets.Add(groupTarget);
                continue;
            }

            return BillingV2KoxoStorageTargetResolution.Fail(
                BillingV2KoxoStorageTargetReasons.TargetTypeUnknown);
        }

        // Deux quotas visant le meme objet ne se departagent pas : le dernier
        // ecrit gagnerait, ce qui rendrait le resultat dependant de l'ordre de
        // lecture. Le controle est global, donc il attrape aussi deux
        // utilisateurs d'abonnement pointant la meme identite annuaire.
        var distinctTargets = targets
            .Select(target => target.TargetKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (distinctTargets != targets.Count)
        {
            return BillingV2KoxoStorageTargetResolution.Fail(
                BillingV2KoxoStorageTargetReasons.IdentityAmbiguous);
        }

        return BillingV2KoxoStorageTargetResolution.Success(targets);
    }

    private static bool TryConvertToMebibytes(
        BillingV2StorageQuotaPlan quota,
        out long mebibytes,
        out string reasonCode)
    {
        mebibytes = 0;
        reasonCode = BillingV2KoxoStorageTargetReasons.Resolved;

        // Le plan porte l'unite du catalogue : la relire ici plutot que de la
        // supposer evite qu'un changement d'unite en amont devienne une erreur
        // de facteur 1024 en aval.
        if (!Matches(
            quota.Unit,
            BillingV2ProvisioningRuleSemantics.ExpectedStorageUnit))
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.UnitUnexpected;
            return false;
        }

        if (quota.QuotaValue <= 0)
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.QuotaInvalid;
            return false;
        }

        try
        {
            mebibytes = checked(quota.QuotaValue * MebibytesPerGibibyte);
        }
        catch (OverflowException)
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.QuotaOverflow;
            return false;
        }

        return true;
    }

    private static bool TryResolveUser(
        string customerId,
        BillingV2StorageQuotaPlan quota,
        long mebibytes,
        IReadOnlyDictionary<string, BillingV2KoxoUserIdentitySnapshot>
            identitiesByReference,
        out BillingV2ResolvedKoxoStorageTarget target,
        out string reasonCode)
    {
        target = null!;
        reasonCode = BillingV2KoxoStorageTargetReasons.Resolved;

        if (!IsUserScoped(quota.ScopeType)
            || string.IsNullOrWhiteSpace(quota.SubscriptionUserId)
            || string.IsNullOrWhiteSpace(quota.IdentityReference))
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.ScopeIncoherent;
            return false;
        }

        var identityReference = quota.IdentityReference.Trim();

        // Un utilisateur non materialise est bloquant : le fournisseur de quota
        // ne cree pas d'identite, sinon deux chemins de creation coexisteraient
        // et le compte KoXo ne serait plus la seule source.
        if (!identitiesByReference.TryGetValue(identityReference, out var snapshot))
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.IdentityNotMaterialized;
            return false;
        }

        // La cle du dictionnaire vient de l'appelant : elle ne prouve rien. Un
        // instantane range sous la mauvaise cle serait parfaitement coherent
        // avec lui-meme et passerait tous les controles suivants, en faisant
        // porter le quota de A par l'identite de B.
        if (!string.Equals(
                snapshot.IdentityReference,
                identityReference,
                StringComparison.Ordinal))
        {
            reasonCode =
                BillingV2KoxoStorageTargetReasons.IdentitySnapshotMismatch;
            return false;
        }

        var employeeNumber = snapshot.KoxoUniqueIdentifier?.Trim();
        if (!KoxoDirectoryTopology.IsValidUniqueIdentifier(employeeNumber))
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.EmployeeNumberInvalid;
            return false;
        }

        if (snapshot.PortalUserLinks.Count == 0)
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.IdentityNotLinked;
            return false;
        }

        if (snapshot.PortalUserLinks.Count > 1)
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.IdentityAmbiguous;
            return false;
        }

        var link = snapshot.PortalUserLinks[0];
        if (!string.Equals(link.CustomerId, customerId, StringComparison.Ordinal))
        {
            reasonCode =
                BillingV2KoxoStorageTargetReasons.IdentityCustomerMismatch;
            return false;
        }

        // Le triplet verifie plus bas prouve que le lien et l'objet d'annuaire
        // decrivent le meme compte — pas que ce compte est celui du
        // portal_users.id qui a paye le quota. Deux utilisateurs d'un meme
        // client passeraient donc tout le reste.
        if (!string.Equals(
                link.PortalUserId,
                identityReference,
                StringComparison.Ordinal))
        {
            reasonCode =
                BillingV2KoxoStorageTargetReasons.IdentityPortalUserMismatch;
            return false;
        }

        var linkedObjectGuid = BillingV2ProvisioningIdentityResolver
            .NormalizeObjectGuid(link.ObjectGuid);
        if (linkedObjectGuid is null)
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.IdentityGuidInvalid;
            return false;
        }

        var directoryObject = snapshot.DirectoryObjectByEmployeeNumber;
        if (directoryObject is null)
        {
            reasonCode =
                BillingV2KoxoStorageTargetReasons.DirectoryObjectNotFound;
            return false;
        }

        // Le lien enregistre et l'objet retrouve par employeeNumber doivent
        // decrire le meme compte, sur les trois attributs a la fois. Un seul
        // ecart signale soit un lien perime, soit un employeeNumber recycle sur
        // un autre compte : dans les deux cas, appliquer le quota le poserait
        // sur la mauvaise identite.
        var directoryObjectGuid = BillingV2ProvisioningIdentityResolver
            .NormalizeObjectGuid(directoryObject.ObjectGuid);
        if (directoryObjectGuid is null)
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.IdentityGuidInvalid;
            return false;
        }

        if (!string.Equals(
                directoryObjectGuid,
                linkedObjectGuid,
                StringComparison.Ordinal)
            || !EqualsTrimmed(directoryObject.ObjectSid, link.ObjectSid)
            || !EqualsTrimmed(
                directoryObject.SamAccountName,
                link.SamAccountName))
        {
            reasonCode =
                BillingV2KoxoStorageTargetReasons.DirectoryObjectMismatch;
            return false;
        }

        target = BillingV2ResolvedKoxoStorageTarget.ForUser(
            quota.SubscriptionItemId,
            mebibytes,
            employeeNumber!,
            link);
        return true;
    }

    private static bool TryResolveSecondaryGroup(
        string customerId,
        BillingV2StorageQuotaPlan quota,
        long mebibytes,
        BillingV2KoxoSecondaryGroupSnapshot? secondaryGroup,
        out BillingV2ResolvedKoxoStorageTarget target,
        out string reasonCode)
    {
        target = null!;
        reasonCode = BillingV2KoxoStorageTargetReasons.Resolved;

        // Le stockage partage appartient au client, pas a une personne : un
        // titulaire sur cette ligne signale un plan mal construit.
        if (!IsSubscriptionScoped(quota.ScopeType)
            || !string.IsNullOrWhiteSpace(quota.SubscriptionUserId)
            || !string.IsNullOrWhiteSpace(quota.IdentityReference))
        {
            reasonCode = BillingV2KoxoStorageTargetReasons.ScopeIncoherent;
            return false;
        }

        if (secondaryGroup is null
            || string.IsNullOrWhiteSpace(secondaryGroup.CustomerReference))
        {
            reasonCode =
                BillingV2KoxoStorageTargetReasons.SecondaryGroupUnknown;
            return false;
        }

        // L'instantane doit decrire le client de l'abonnement en cours. C'est
        // la seule attache dont dispose une OU partagee : le nom du groupe
        // secondaire, lui, ne dementirait jamais une erreur d'alimentation.
        if (!string.Equals(
                secondaryGroup.CustomerId,
                customerId,
                StringComparison.Ordinal))
        {
            reasonCode = BillingV2KoxoStorageTargetReasons
                .SecondaryGroupCustomerMismatch;
            return false;
        }

        // Le nom est celui de la topologie d'export, jamais recompose ici :
        // c'est la seule garantie que le quota vise l'OU dans laquelle KoXo a
        // reellement place les identites du client.
        var groupName = KoxoDirectoryTopology.ResolveSecondaryGroup(
            secondaryGroup.IsDemo,
            NormalizeOptional(secondaryGroup.KoxoGroupReference),
            secondaryGroup.CustomerReference.Trim());
        if (string.IsNullOrWhiteSpace(groupName))
        {
            reasonCode =
                BillingV2KoxoStorageTargetReasons.SecondaryGroupUnknown;
            return false;
        }

        target = BillingV2ResolvedKoxoStorageTarget.ForSecondaryGroup(
            quota.SubscriptionItemId,
            mebibytes,
            KoxoDirectoryTopology.ResolvePrimaryGroup(secondaryGroup.IsDemo),
            groupName);
        return true;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool IsUserScoped(string? scopeType)
        => Matches(scopeType, "user");

    private static bool IsSubscriptionScoped(string? scopeType)
        => Matches(scopeType, "subscription");

    private static bool EqualsTrimmed(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(
                left.Trim(),
                right.Trim(),
                StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string? value, string expected)
        => !string.IsNullOrWhiteSpace(value)
            && string.Equals(
                value.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase);
}
