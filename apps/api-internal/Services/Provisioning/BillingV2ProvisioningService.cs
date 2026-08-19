using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record BillingV2ProvisioningReadinessState(
    bool GlobalFlagEnabled,
    bool ClientReady,
    bool AddOnlyMode,
    bool CompleteMaterialization,
    bool RequiredRulesResolved,
    bool ShadowSucceeded,
    bool ShadowMatchesLegacy,
    bool HasUnresolvedMismatch,
    bool TargetGroupsResolved);

public sealed record BillingV2ProvisioningGateDecision(
    bool Authorized,
    bool AddOnlyMode,
    string ReasonCode)
{
    public static BillingV2ProvisioningGateDecision Allow(bool addOnlyMode)
        => new(true, addOnlyMode, "BILLING_V2_PROVISIONING_READY");

    public static BillingV2ProvisioningGateDecision Deny(string reasonCode)
        => new(false, AddOnlyMode: true, reasonCode);
}

/// <summary>
/// Une ligne de la projection SQL du provisioning V2 : un item d'abonnement
/// actif croise avec zero ou une regle de provisioning.
/// </summary>
/// <remarks>
/// <para>
/// Les champs de scope (<see cref="ScopeType"/>,
/// <see cref="SubscriptionUserId"/>, <see cref="IdentityReference"/>) ne sont
/// pas decoratifs : un droit achete pour un <c>billing_v2_subscription_user</c>
/// donne ne doit jamais pouvoir etre applique a un autre utilisateur du meme
/// client. Perdre ces colonnes a la lecture, comme c'etait le cas avant cette
/// correction, reintroduit mecaniquement cette fuite de droits.
/// </para>
/// </remarks>
public sealed record BillingV2ProvisioningRuleProjection(
    string SubscriptionId,
    string SubscriptionItemId,
    string ServiceCode,
    string? TierCode,
    string RuleType,
    string TargetType,
    string? TargetReference,
    string ValueSource,
    string? StaticValue,
    long? TierNumericValue,
    string? TierUnit,
    int Quantity,
    string ScopeType,
    string? SubscriptionUserId,
    string? IdentityReference,
    bool SubscriptionUserIsPrimary,
    string? SubscriptionUserStatus);

/// <summary>
/// Regle explicitement comprise par le planificateur mais qui n'emet aucune
/// ecriture externe.
/// </summary>
/// <remarks>
/// <para>
/// Un droit contractuel, une prestation humaine ou une couverture heritee
/// d'une politique globale n'a pas d'objet a creer : la reconnaitre est une
/// conclusion, pas une action. La distinction avec un blocage est essentielle,
/// sinon la seule presence d'un socle ou d'un support dans l'abonnement
/// empecherait a jamais le provisioning des droits qui, eux, sont reels.
/// </para>
/// <para>
/// Ce type n'existe que pour les regles EXPLICITES. L'absence de regle reste
/// une anomalie : voir
/// <see cref="BillingV2ProvisioningBlockerReasons.RuleMissing"/>.
/// </para>
/// </remarks>
public sealed record BillingV2AcknowledgedEntitlement(
    string SubscriptionItemId,
    string? SubscriptionUserId,
    string ServiceCode,
    string RuleType,
    string TargetType,
    string? TargetReference,
    string ScopeType);

/// <summary>
/// Quota de stockage desire, exprime dans l'unite canonique du catalogue.
/// </summary>
/// <remarks>
/// Aucune conversion n'est faite ici. La valeur reste celle du catalogue
/// (<c>GiB</c>) et la traduction vers l'unite attendue par KoXo/FSRM
/// (<c>FolderQuota</c> en MiB) appartient au provider reel, pas au plan : un
/// plan converti trop tot deviendrait faux si l'unite du catalogue changeait.
/// </remarks>
public sealed record BillingV2StorageQuotaPlan(
    string SubscriptionItemId,
    string? SubscriptionUserId,
    string TargetType,
    string? IdentityReference,
    long QuotaValue,
    string Unit,
    string ScopeType);

/// <summary>
/// Raison precise pour laquelle une ligne du catalogue n'est pas executable.
/// </summary>
public sealed record BillingV2ProvisioningBlocker(
    string RuleReference,
    string ReasonCode);

public static class BillingV2ProvisioningBlockerReasons
{
    /// <summary>
    /// Aucune regle explicite, ou item non materialise. Ce n'est jamais un
    /// noop : une brique vendue sans regle est une lacune de catalogue.
    /// </summary>
    public const string RuleMissing =
        "BILLING_V2_PROVISIONING_RULE_MISSING";

    public const string RuleTypeUnknown =
        "BILLING_V2_PROVISIONING_RULE_TYPE_UNKNOWN";

    public const string TargetTypeUnknown =
        "BILLING_V2_PROVISIONING_TARGET_TYPE_UNKNOWN";

    /// <summary>
    /// Le scope de l'item contredit le scope impose par la regle.
    /// </summary>
    public const string ScopeIncoherent =
        "BILLING_V2_PROVISIONING_SCOPE_INCOHERENT";

    /// <summary>
    /// La regle est comprise mais aucune identite ne la porte. Aucun repli
    /// vers un autre utilisateur du client n'est acceptable.
    /// </summary>
    public const string IdentityRequired =
        "BILLING_V2_PROVISIONING_IDENTITY_REQUIRED";

    /// <summary>
    /// Un droit aval a ete achete pour un utilisateur qui n'a pas de stockage
    /// personnel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le stockage personnel n'est pas une ressource parmi d'autres : c'est
    /// l'environnement de travail de l'utilisateur, donc la marque qu'il est
    /// techniquement equipe. Accorder un acces VPN ou RDS a quelqu'un qui n'en a
    /// pas reviendrait a ouvrir un acces vers un poste de travail inexistant.
    /// </para>
    /// <para>
    /// Ce n'est PAS lui qui cree l'identite annuaire. Pour un client payant
    /// ordinaire, l'export KoXo exige deja un <c>customer_ad_links</c>, donc le
    /// compte preexiste ; seul un essai de demonstration part sans lien, et
    /// c'est alors KoXo qui cree le compte, adopte ensuite par
    /// <c>employeeNumber</c>. La condition posee ici est une regle d'ordre
    /// voulue, pas une dependance technique de creation.
    /// </para>
    /// </remarks>
    public const string PersonalStorageRequired =
        "BILLING_V2_PROVISIONING_PERSONAL_STORAGE_REQUIRED";

    /// <summary>
    /// Deux stockages personnels concurrents pour un meme utilisateur.
    /// </summary>
    /// <remarks>
    /// L'environnement utilisateur est unique : deux quotas contradictoires ne
    /// se departagent pas, ils se refusent.
    /// </remarks>
    public const string PersonalStorageConflict =
        "BILLING_V2_PROVISIONING_PERSONAL_STORAGE_CONFLICT";

    public const string UserNotActive =
        "BILLING_V2_PROVISIONING_USER_NOT_ACTIVE";

    public const string UserIdentityConflict =
        "BILLING_V2_PROVISIONING_USER_IDENTITY_CONFLICT";

    public const string TargetReferenceMissing =
        "BILLING_V2_PROVISIONING_TARGET_REFERENCE_MISSING";

    public const string ValueUnresolved =
        "BILLING_V2_PROVISIONING_VALUE_UNRESOLVED";

    /// <summary>
    /// Le catalogue exprime le tier dans une unite differente de celle que le
    /// provisioning de stockage sait interpreter.
    /// </summary>
    public const string UnitUnexpected =
        "BILLING_V2_PROVISIONING_UNIT_UNEXPECTED";
}

/// <summary>
/// Etat desire d'un seul <c>billing_v2_subscription_user</c>.
/// </summary>
/// <remarks>
/// <para>
/// Le plan V2 n'expose plus aucun ensemble de groupes AD au niveau client :
/// un groupe n'existe que porte par l'utilisateur qui l'a achete. C'est cette
/// structure, et non un controle a l'execution, qui rend impossible d'appliquer
/// le droit de A a B. La meme regle vaut pour le stockage personnel : un quota
/// n'existe que dans l'etat desire de son titulaire.
/// </para>
/// <para>
/// Les membres ne sont pas paralleles : <see cref="PersonalStorage"/> est le
/// socle technique de l'utilisateur, c'est-a-dire son environnement de travail
/// et le quota qui le borne ; <see cref="DesiredAdGroups"/> ne contient que des
/// acces optionnels situes en aval, qui supposent une identite annuaire deja
/// resolue.
/// </para>
/// </remarks>
public sealed record BillingV2UserDesiredState(
    string SubscriptionUserId,
    string IdentityReference,
    IReadOnlyList<string> DesiredAdGroups,
    BillingV2StorageQuotaPlan? PersonalStorage,
    IReadOnlyList<BillingV2AcknowledgedEntitlement> UserInheritedCoverages,
    IReadOnlyList<BillingV2AcknowledgedEntitlement> UserEntitlements)
{
    /// <summary>
    /// Plans de stockage personnels de cet utilisateur, au plus un.
    /// </summary>
    public IReadOnlyList<BillingV2StorageQuotaPlan> UserStoragePlans
        => PersonalStorage is null
            ? Array.Empty<BillingV2StorageQuotaPlan>()
            : [PersonalStorage];
}

/// <summary>
/// Ressources de scope abonnement, volontairement separees des utilisateurs.
/// </summary>
/// <remarks>
/// <para>
/// Aucun groupe AD n'y figure : la semantique utilisateur d'un droit AD achete
/// au niveau abonnement n'est pas definie dans le modele actuel, donc le
/// planificateur le classe non resolu plutot que de le distribuer a tous les
/// utilisateurs du client. Le stockage partage suit la meme logique en sens
/// inverse : il appartient au groupe secondaire du client et ne doit jamais
/// etre recopie dans l'etat desire d'un utilisateur.
/// </para>
/// <para>
/// <see cref="UnassignedUserSlots"/> porte les places d'utilisateur payees mais
/// pas encore attribuees a une personne. Elles sont rattachees a l'abonnement
/// et non a un utilisateur, precisement parce qu'aucune identite ne les porte
/// encore.
/// </para>
/// </remarks>
public sealed record BillingV2SubscriptionDesiredState(
    IReadOnlyList<BillingV2StorageQuotaPlan> SharedStoragePlans,
    IReadOnlyList<BillingV2AcknowledgedEntitlement> InheritedCoverages,
    IReadOnlyList<BillingV2AcknowledgedEntitlement> Entitlements,
    IReadOnlyList<BillingV2AcknowledgedEntitlement> UnassignedUserSlots)
{
    public static BillingV2SubscriptionDesiredState Empty { get; }
        = new(
            Array.Empty<BillingV2StorageQuotaPlan>(),
            Array.Empty<BillingV2AcknowledgedEntitlement>(),
            Array.Empty<BillingV2AcknowledgedEntitlement>(),
            Array.Empty<BillingV2AcknowledgedEntitlement>());
}

public sealed record BillingV2ProvisioningPlan(
    IReadOnlyList<BillingV2UserDesiredState> Users,
    BillingV2SubscriptionDesiredState SubscriptionResources,
    IReadOnlyList<BillingV2ProvisioningBlocker> Blockers)
{
    /// <summary>
    /// Enveloppe informative des groupes AD du client, tous utilisateurs
    /// confondus.
    /// </summary>
    /// <remarks>
    /// Sert uniquement a verifier que chaque groupe a un DN configure et a
    /// comparer le perimetre V2 au perimetre legacy. Ne jamais passer cette
    /// union comme droit desire a une execution : elle n'a pas de titulaire.
    /// </remarks>
    public IReadOnlyList<string> AllDesiredAdGroups => Users
        .SelectMany(user => user.DesiredAdGroups)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Utilisateurs dont l'execution exige une identite Active Directory deja
    /// resolue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deux identites distinctes coexistent et ne doivent pas etre confondues :
    /// l'identite de facturation
    /// (<c>billing_v2_subscription_users.identity_reference</c>, soit
    /// <c>portal_users.id</c>), exigee de toute ressource utilisateur ; et
    /// l'identite annuaire resolue (<c>customer_ad_links</c>, <c>objectGUID</c>,
    /// <c>objectSID</c>), qui n'existe qu'une fois le compte cree.
    /// </para>
    /// <para>
    /// Seuls les acces situes en aval — VPN, RDS — exigent la seconde, parce
    /// qu'eux seuls ecrivent dans l'annuaire. Un utilisateur qui n'a que son
    /// environnement personnel ne declenche aucune ecriture AD dans cette
    /// version, et reclamer son lien ici bloquerait aussi l'essai de
    /// demonstration, dont le compte n'est cree qu'ensuite, par KoXo. La rigueur
    /// n'est pas relachee pour autant : appliquer reellement un quota passe par
    /// <see cref="BillingV2KoxoStorageTargetResolver"/>, qui exige une identite
    /// entierement materialisee.
    /// </para>
    /// </remarks>
    public IReadOnlyList<BillingV2UserDesiredState> UsersRequiringAdIdentity
        => Users
            .Where(user => user.DesiredAdGroups.Count > 0)
            .ToArray();

    /// <summary>
    /// Tous les plans de quota, personnels et partages.
    /// </summary>
    /// <remarks>
    /// Le chemin reel du quota passe par KoXo, qui ecrit le quota dans la fiche
    /// XML puis fait appliquer la limite sur le serveur de fichiers. Ces plans
    /// sont desormais executables, mais uniquement apres resolution stricte de
    /// leur cible : leur presence conditionne le provisioning, elle ne l'ouvre
    /// pas. Un quota non resolu ou non applique bloque tout le lot, acces
    /// annuaire compris.
    /// </remarks>
    public IReadOnlyList<BillingV2StorageQuotaPlan> StorageQuotaPlans => Users
        .SelectMany(user => user.UserStoragePlans)
        .Concat(SubscriptionResources.SharedStoragePlans)
        .ToArray();

    /// <summary>
    /// References des lignes non executables, quelle qu'en soit la raison.
    /// </summary>
    /// <remarks>
    /// Derivee de <see cref="Blockers"/> et non alimentee separement : toute
    /// nouvelle raison de blocage refuse donc automatiquement le provisioning,
    /// sans qu'il faille penser a la cabler dans la gate.
    /// </remarks>
    public IReadOnlyList<string> UnresolvedRuleReferences => Blockers
        .Select(blocker => blocker.RuleReference)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static BillingV2ProvisioningPlan Empty { get; }
        = new(
            Array.Empty<BillingV2UserDesiredState>(),
            BillingV2SubscriptionDesiredState.Empty,
            Array.Empty<BillingV2ProvisioningBlocker>());
}

public sealed record BillingV2KoxoStorageReadiness(
    bool CanApplyQuotas,
    string ReasonCode);

/// <summary>
/// Application reelle d'un quota de stockage.
/// </summary>
/// <remarks>
/// L'implementation reelle passera par KoXo (fiche utilisateur ou groupe
/// secondaire, puis reparation de type <c>Storage</c>), jamais par un appel
/// direct au serveur de fichiers ni par le service qui expose ce stockage aux
/// utilisateurs. Cette interface ne connait donc que l'intention, pas le
/// transport.
/// </remarks>
public interface IBillingV2KoxoStorageProvider
{
    BillingV2KoxoStorageReadiness CheckReadiness(
        IReadOnlyList<BillingV2StorageQuotaPlan> quotas);

    /// <summary>
    /// Reconcilie des cibles DEJA resolues, sans jamais en resoudre aucune.
    /// </summary>
    /// <remarks>
    /// Le provider ne connait ni <c>portal_users</c>, ni l'annuaire, ni la
    /// topologie : il recoit des objets KoXo nommes et verifies. Refaire la
    /// resolution ici creerait un second chemin d'identification, donc une
    /// seconde facon de se tromper de titulaire.
    /// </remarks>
    Task<BillingV2KoxoStorageApplyResult> ApplyAsync(
        IReadOnlyList<BillingV2ResolvedKoxoStorageTarget> targets,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class DormantBillingV2KoxoStorageProvider
    : IBillingV2KoxoStorageProvider
{
    public static DormantBillingV2KoxoStorageProvider Instance { get; }
        = new();

    private DormantBillingV2KoxoStorageProvider()
    {
    }

    public BillingV2KoxoStorageReadiness CheckReadiness(
        IReadOnlyList<BillingV2StorageQuotaPlan> quotas)
        => quotas.Count == 0
            ? new BillingV2KoxoStorageReadiness(
                CanApplyQuotas: true,
                "BILLING_V2_KOXO_STORAGE_NOOP")
            : new BillingV2KoxoStorageReadiness(
                CanApplyQuotas: false,
                BillingV2KoxoStorageApplyReasons.ProviderNotConfigured);

    /// <summary>
    /// Refuse tout, sauf un lot vide.
    /// </summary>
    /// <remarks>
    /// C'est l'implementation retenue quand aucun point d'entree KoXo n'est
    /// configure. Elle echoue au lieu de rendre un succes vide : un lot de
    /// quotas declare applique alors que rien n'a ete fait laisserait
    /// l'abonnement passer pour provisionne.
    /// </remarks>
    public Task<BillingV2KoxoStorageApplyResult> ApplyAsync(
        IReadOnlyList<BillingV2ResolvedKoxoStorageTarget> targets,
        string correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(targets.Count == 0
            ? BillingV2KoxoStorageApplyResult.Noop()
            : BillingV2KoxoStorageApplyResult.Fail(
                BillingV2KoxoStorageApplyReasons.ProviderNotConfigured));
}

/// <summary>
/// Un etat desire d'utilisateur rattache a l'identite Active Directory reelle
/// qui le porte.
/// </summary>
public sealed record BillingV2ResolvedProvisioningTarget(
    BillingV2UserDesiredState DesiredState,
    CustomerAdLinkSummary AdLink);

public sealed record BillingV2ProvisioningTargetResolution(
    bool Resolved,
    string ReasonCode,
    IReadOnlyList<BillingV2ResolvedProvisioningTarget> Targets)
{
    public static BillingV2ProvisioningTargetResolution Fail(string reasonCode)
        => new(
            false,
            reasonCode,
            Array.Empty<BillingV2ResolvedProvisioningTarget>());

    public static BillingV2ProvisioningTargetResolution Success(
        IReadOnlyList<BillingV2ResolvedProvisioningTarget> targets)
        => new(true, "BILLING_V2_PROVISIONING_IDENTITY_RESOLVED", targets);
}

public interface IBillingV2ProvisioningService
{
    Task<ProvisioningExecutionResult?> TryReconcileAsync(
        SubscriptionProvisioningContext context,
        CancellationToken cancellationToken);

    Task<ProvisioningExecutionResult?> TryReconcileActivatedSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<BillingV2ProvisioningReadinessReviewResult> ReviewClientReadinessAsync(
        string customerId,
        string reviewedByReference,
        CancellationToken cancellationToken);
}

public sealed class NoOpBillingV2ProvisioningService
    : IBillingV2ProvisioningService
{
    public static NoOpBillingV2ProvisioningService Instance { get; } = new();

    private NoOpBillingV2ProvisioningService()
    {
    }

    public Task<ProvisioningExecutionResult?> TryReconcileAsync(
        SubscriptionProvisioningContext context,
        CancellationToken cancellationToken)
        => Task.FromResult<ProvisioningExecutionResult?>(null);

    public Task<ProvisioningExecutionResult?> TryReconcileActivatedSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
        => Task.FromResult<ProvisioningExecutionResult?>(null);

    public Task<BillingV2ProvisioningReadinessReviewResult> ReviewClientReadinessAsync(
        string customerId,
        string reviewedByReference,
        CancellationToken cancellationToken)
        => Task.FromResult(BillingV2ProvisioningReadinessReviewResult.PersistenceUnavailable);
}

public sealed partial class BillingV2ProvisioningService : IBillingV2ProvisioningService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _billingV2;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IActiveDirectoryLinkRepository _activeDirectoryLinks;
    private readonly IProvisioningService _provisioningService;
    private readonly IBillingV2KoxoStorageProvider _koxoStorageProvider;
    private readonly IBillingV2KoxoStorageTargetResolutionService
        _koxoStorageTargets;
    private readonly SubscriptionProvisioningRuntimeConfiguration
        _provisioningConfiguration;
    private readonly ILogger<BillingV2ProvisioningService> _logger;

    public BillingV2ProvisioningService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration billingV2,
        ISubscriptionRepository subscriptions,
        IActiveDirectoryLinkRepository activeDirectoryLinks,
        IProvisioningService provisioningService,
        IBillingV2KoxoStorageProvider koxoStorageProvider,
        IBillingV2KoxoStorageTargetResolutionService koxoStorageTargets,
        SubscriptionProvisioningRuntimeConfiguration provisioningConfiguration,
        ILogger<BillingV2ProvisioningService> logger)
    {
        _sql = sql;
        _billingV2 = billingV2;
        _subscriptions = subscriptions;
        _activeDirectoryLinks = activeDirectoryLinks;
        _provisioningService = provisioningService;
        _koxoStorageProvider = koxoStorageProvider;
        _koxoStorageTargets = koxoStorageTargets;
        _provisioningConfiguration = provisioningConfiguration;
        _logger = logger;
    }

    /// <summary>
    /// Reconcilie le socle de stockage avant toute ecriture dans l'annuaire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// L'ordre n'est pas cosmetique : un acces VPN ou RDS accorde a un
    /// utilisateur dont l'environnement personnel n'existe pas ouvre une session
    /// vers un poste vide. Le stockage passe donc d'abord, et un stockage
    /// bloque ou echoue interrompt le provisioning avant la moindre modification
    /// de groupe.
    /// </para>
    /// <para>
    /// La resolution des cibles reste entierement en dehors du provider : ce
    /// service resout, le provider applique. Un provider qui resoudrait
    /// lui-meme ouvrirait un second chemin d'identification du titulaire.
    /// </para>
    /// </remarks>
    private async Task<bool> TryReconcileStorageAsync(
        string customerId,
        BillingV2ProvisioningPlan plan,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        var planCount = plan.StorageQuotaPlans.Count;
        if (planCount == 0)
        {
            return BillingV2KoxoStorageGate
                .Evaluate(planCount, resolution: null, applied: null)
                .MayContinue;
        }

        var resolution = await _koxoStorageTargets.ResolveAsync(
            customerId,
            plan.StorageQuotaPlans,
            cancellationToken);
        if (!BillingV2KoxoStorageGate
                .Evaluate(planCount, resolution, applied: null)
                .MayContinue
            && !resolution.Resolved)
        {
            _logger.LogWarning(
                "Billing V2 provisioning denied for customer {CustomerId} subscription {SubscriptionId}: KoXo storage targets could not be resolved ({ReasonCode}). No storage quota and no Active Directory change was applied.",
                customerId,
                subscriptionId,
                resolution.ReasonCode);
            return false;
        }

        var correlationId = Guid.NewGuid().ToString("D");
        var applied = await _koxoStorageProvider.ApplyAsync(
            resolution.Targets,
            correlationId,
            cancellationToken);
        await PersistStorageStatusesAsync(applied, cancellationToken);
        var gate = BillingV2KoxoStorageGate.Evaluate(
            planCount,
            resolution,
            applied);
        if (!gate.MayContinue)
        {
            // Le detail par cible est journalise : un lot partiellement
            // applique doit rester lisible, sinon il se lit comme un echec
            // total alors qu'une partie du stockage a bien change.
            foreach (var result in applied.Results.Where(
                result => !result.Succeeded))
            {
                _logger.LogWarning(
                    "Billing V2 KoXo storage reconcile refused for subscription {SubscriptionId} item {SubscriptionItemId}: {Outcome} ({ReasonCode}).",
                    subscriptionId,
                    result.SubscriptionItemId,
                    result.Outcome,
                    result.ReasonCode);
            }

            _logger.LogWarning(
                "Billing V2 provisioning denied for customer {CustomerId} subscription {SubscriptionId}: KoXo storage reconciliation did not complete ({ReasonCode}). Dependent Active Directory rights were not applied.",
                customerId,
                subscriptionId,
                applied.ReasonCode);
            return false;
        }

        _logger.LogInformation(
            "Billing V2 KoXo storage reconciled for customer {CustomerId} subscription {SubscriptionId}: {TargetCount} target(s), correlation {CorrelationId}.",
            customerId,
            subscriptionId,
            applied.Results.Count,
            correlationId);
        return true;
    }

    public async Task<ProvisioningExecutionResult?> TryReconcileAsync(
        SubscriptionProvisioningContext context,
        CancellationToken cancellationToken)
    {
        if (!_billingV2.ProvisioningEnabled
            || !_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return null;
        }

        var customerId = context.Subscription.CustomerId;
        var activeLegacySubscriptions =
            (await _subscriptions.GetByCustomerAsync(
                customerId,
                cancellationToken))
            .Where(subscription => string.Equals(
                subscription.Status,
                "active",
                StringComparison.Ordinal))
            .ToArray();
        if (activeLegacySubscriptions.Length == 0)
        {
            return null;
        }

        var readiness = await LoadReadinessAsync(
            customerId,
            cancellationToken);
        var plan = await LoadProvisioningPlanAsync(
            customerId,
            activeLegacySubscriptions.Select(subscription => subscription.Id).ToArray(),
            cancellationToken);
        var materializedIds = await LoadMaterializedActiveSubscriptionIdsAsync(
            customerId,
            cancellationToken);
        var missingSubscriptions = activeLegacySubscriptions
            .Select(subscription => subscription.Id)
            .Except(materializedIds, StringComparer.Ordinal)
            .ToArray();
        var desiredAdGroupEnvelope = plan.AllDesiredAdGroups;
        var targetGroupsResolved = desiredAdGroupEnvelope.All(group =>
            context.GroupDistinguishedNamesBySamAccountName.TryGetValue(
                group,
                out var distinguishedName)
            && !string.IsNullOrWhiteSpace(distinguishedName));

        // Le shadow legacy reste une condition necessaire de plus, jamais la
        // source des droits : il raisonne au niveau client et ne peut donc pas
        // dire quel utilisateur porte quel groupe.
        var sameAdRights = desiredAdGroupEnvelope.SequenceEqual(
            context.ReconciledGroups,
            StringComparer.OrdinalIgnoreCase);
        var decision = BillingV2ProvisioningReadinessGate.Evaluate(
            new BillingV2ProvisioningReadinessState(
                GlobalFlagEnabled: _billingV2.ProvisioningEnabled,
                ClientReady: readiness.ClientReady,
                AddOnlyMode: readiness.AddOnlyMode,
                CompleteMaterialization: missingSubscriptions.Length == 0
                    && plan.UnresolvedRuleReferences.Count == 0,
                RequiredRulesResolved: plan.UnresolvedRuleReferences.Count == 0,
                ShadowSucceeded: readiness.ShadowSucceeded,
                ShadowMatchesLegacy: readiness.ShadowMatchesLegacy
                    && sameAdRights,
                HasUnresolvedMismatch: readiness.HasUnresolvedMismatch,
                TargetGroupsResolved: targetGroupsResolved));
        if (!decision.Authorized)
        {
            _logger.LogWarning(
                "Billing V2 provisioning gate denied for customer {CustomerId} subscription {SubscriptionId}: {ReasonCode}. Legacy provisioning remains authoritative.",
                customerId,
                context.Subscription.Id,
                decision.ReasonCode);
            return null;
        }

        if (!decision.AddOnlyMode)
        {
            _logger.LogWarning(
                "Billing V2 provisioning is limited to add-only mode for customer {CustomerId} subscription {SubscriptionId}. Legacy provisioning remains authoritative.",
                customerId,
                context.Subscription.Id);
            return null;
        }

        // Le socle de stockage passe avant tout acces dependant, et son echec
        // arrete le provisioning : la reconciliation refuse d'elle-meme si
        // aucun provider n'est configure.
        if (!await TryReconcileStorageAsync(
                customerId,
                plan,
                context.Subscription.Id,
                cancellationToken))
        {
            return null;
        }

        if (plan.Users.Count == 0)
        {
            _logger.LogWarning(
                "Billing V2 provisioning skipped for customer {CustomerId}: no user-scoped desired state is available. Legacy provisioning remains authoritative.",
                customerId);
            return null;
        }

        var customerUserLinks =
            await _activeDirectoryLinks.GetCustomerUserLinksAsync(
                customerId,
                cancellationToken);

        // Seuls les acces AD reclament une identite annuaire deja resolue. Un
        // utilisateur qui n'a que son environnement personnel n'en a pas encore
        // et ne doit pas faire echouer la resolution des autres.
        var resolution = await ResolveTargetsAsync(
            customerId,
            plan.UsersRequiringAdIdentity,
            customerUserLinks,
            cancellationToken);
        if (!resolution.Resolved)
        {
            _logger.LogWarning(
                "Billing V2 provisioning denied for customer {CustomerId} subscription {SubscriptionId}: {ReasonCode}. Legacy provisioning remains authoritative.",
                customerId,
                context.Subscription.Id,
                resolution.ReasonCode);
            return null;
        }

        // Une action manuelle peut avoir restreint la selection a certains
        // comptes : V2 la respecte et ne l'elargit jamais. L'appartenance a
        // cette selection se juge sur objectGUID, pas sur sAMAccountName.
        var allowedObjectGuids = context.TargetUsers
            .Select(user => BillingV2ProvisioningIdentityResolver
                .NormalizeObjectGuid(user.ObjectGuid))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var targets = resolution.Targets
            .Where(target => BillingV2ProvisioningIdentityResolver
                .NormalizeObjectGuid(target.AdLink.ObjectGuid) is string guid
                && allowedObjectGuids.Contains(guid))
            .ToArray();
        if (targets.Length == 0)
        {
            _logger.LogWarning(
                "Billing V2 provisioning skipped for customer {CustomerId} subscription {SubscriptionId}: no Billing V2 user matches the requested target selection. Legacy provisioning remains authoritative.",
                customerId,
                context.Subscription.Id);
            return null;
        }

        return await ExecutePerUserAsync(
            BillingV2ProvisioningExecutionPlanner.BuildPerUserRequests(
                decision,
                targets,
                context.GroupDistinguishedNamesBySamAccountName),
            cancellationToken);
    }

    public async Task<ProvisioningExecutionResult?> TryReconcileActivatedSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        if (!_billingV2.ProvisioningEnabled
            || !_sql.IsPersistent
            || string.IsNullOrWhiteSpace(_sql.ConnectionString))
        {
            return null;
        }

        var customerId = await LoadActiveSubscriptionCustomerIdAsync(
            subscriptionId,
            cancellationToken);
        if (customerId is null)
        {
            return null;
        }

        var activeV2SubscriptionIds =
            await LoadMaterializedActiveSubscriptionIdsAsync(
                customerId,
                cancellationToken);
        if (!activeV2SubscriptionIds.Contains(subscriptionId))
        {
            return null;
        }

        var readiness = await LoadReadinessAsync(
            customerId,
            cancellationToken);
        var plan = await LoadProvisioningPlanAsync(
            customerId,
            activeV2SubscriptionIds.ToArray(),
            cancellationToken);
        // Le referentiel de liens est charge ici, mais son eventuelle vacuite ne
        // peut pas conclure avant la porte de stockage : celle-ci refuse plus
        // tot et plus explicitement. Un lien absent n'est d'ailleurs pas
        // toujours une anomalie — pour un essai de demonstration, le compte est
        // cree par KoXo apres l'export, donc l'absence est un etat transitoire
        // normal.
        var targetUsers = await _activeDirectoryLinks.GetCustomerUserLinksAsync(
            customerId,
            cancellationToken);

        var targetGroupsResolved = plan.AllDesiredAdGroups.All(group =>
            _provisioningConfiguration.GroupDistinguishedNamesBySamAccountName
                .TryGetValue(group, out var distinguishedName)
            && !string.IsNullOrWhiteSpace(distinguishedName));
        var decision = BillingV2ProvisioningReadinessGate.Evaluate(
            new BillingV2ProvisioningReadinessState(
                GlobalFlagEnabled: _billingV2.ProvisioningEnabled,
                ClientReady: readiness.ClientReady,
                AddOnlyMode: readiness.AddOnlyMode,
                CompleteMaterialization: activeV2SubscriptionIds.Count > 0
                    && plan.UnresolvedRuleReferences.Count == 0,
                RequiredRulesResolved: plan.UnresolvedRuleReferences.Count == 0,
                ShadowSucceeded: readiness.ShadowSucceeded,
                ShadowMatchesLegacy: readiness.ShadowMatchesLegacy,
                HasUnresolvedMismatch: readiness.HasUnresolvedMismatch,
                TargetGroupsResolved: targetGroupsResolved));
        if (!decision.Authorized)
        {
            _logger.LogWarning(
                "Billing V2 provisioning gate denied after provider activation for subscription {SubscriptionId}: {ReasonCode}. No external action was executed.",
                subscriptionId,
                decision.ReasonCode);
            return null;
        }

        if (!decision.AddOnlyMode)
        {
            _logger.LogWarning(
                "Billing V2 provisioning after provider activation is limited to add-only mode for subscription {SubscriptionId}. No external action was executed.",
                subscriptionId);
            return null;
        }

        await MarkAcknowledgedEntitlementsAsync(plan, cancellationToken);

        if (!await TryReconcileStorageAsync(
                customerId,
                plan,
                subscriptionId,
                cancellationToken))
        {
            return null;
        }

        if (plan.Users.Count == 0)
        {
            _logger.LogWarning(
                "Billing V2 provisioning skipped for subscription {SubscriptionId}: no user-scoped desired state is available. No external action was executed.",
                subscriptionId);
            return null;
        }

        // Rien a executer sur l'annuaire : le declarer traite ici masquerait le
        // fait que le socle KoXo, lui, reste non applique.
        if (plan.UsersRequiringAdIdentity.Count == 0)
        {
            _logger.LogWarning(
                "Billing V2 provisioning skipped for subscription {SubscriptionId}: no user requires an Active Directory access. No external action was executed.",
                subscriptionId);
            return null;
        }

        if (targetUsers.Count == 0)
        {
            _logger.LogWarning(
                "Billing V2 provisioning skipped for subscription {SubscriptionId}: an Active Directory access is required but no user link is available. No external action was executed.",
                subscriptionId);
            return null;
        }

        var resolution = await ResolveTargetsAsync(
            customerId,
            plan.UsersRequiringAdIdentity,
            targetUsers,
            cancellationToken);
        if (!resolution.Resolved)
        {
            _logger.LogWarning(
                "Billing V2 provisioning denied after provider activation for subscription {SubscriptionId}: {ReasonCode}. No external action was executed.",
                subscriptionId,
                resolution.ReasonCode);
            return null;
        }

        // La configuration runtime declare des DN non nullables et la requete
        // d'execution les accepte nullables : la projection explicite evite la
        // variance CS8620 sans relacher le contrat d'aucun des deux cotes.
        var groupDistinguishedNames = _provisioningConfiguration
            .GroupDistinguishedNamesBySamAccountName
            .ToDictionary(
                entry => entry.Key,
                entry => (string?)entry.Value,
                StringComparer.OrdinalIgnoreCase);

        var execution = await ExecutePerUserAsync(
            BillingV2ProvisioningExecutionPlanner.BuildPerUserRequests(
                decision,
                resolution.Targets,
                groupDistinguishedNames),
            cancellationToken);
        await PersistAdStatusesAsync(customerId, execution, cancellationToken);
        return execution;
    }

    /// <summary>
    /// Rattache chaque etat desire d'utilisateur a une et une seule identite
    /// Active Directory du client, ou echoue.
    /// </summary>
    /// <remarks>
    /// La resolution est volontairement stricte : aucune identite choisie par
    /// defaut, aucun repli sur « tous les utilisateurs du client ». Un doute
    /// sur le titulaire d'un droit doit interrompre le provisioning, pas le
    /// faire porter au mauvais compte.
    /// </remarks>
    private async Task<BillingV2ProvisioningTargetResolution> ResolveTargetsAsync(
        string customerId,
        IReadOnlyList<BillingV2UserDesiredState> users,
        IReadOnlyList<CustomerAdLinkSummary> customerUserLinks,
        CancellationToken cancellationToken)
    {
        var linksByIdentityReference =
            new Dictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>(
                StringComparer.Ordinal);
        foreach (var identityReference in users
            .Select(user => user.IdentityReference)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal))
        {
            linksByIdentityReference[identityReference] =
                await _activeDirectoryLinks.GetUserLinksByPortalUserIdAsync(
                    identityReference,
                    cancellationToken);
        }

        return BillingV2ProvisioningIdentityResolver.Resolve(
            customerId,
            users,
            linksByIdentityReference,
            customerUserLinks);
    }

    /// <summary>
    /// Execute une reconciliation par utilisateur, chacune bornee a sa seule
    /// identite et a ses seuls groupes.
    /// </summary>
    /// <remarks>
    /// Le moteur AD (<see cref="ProvisioningService"/>) et son provisionneur
    /// restent inchanges : ils decident l'appartenance au niveau du groupe et
    /// l'appliquent a tous les <c>TargetUsers</c> recus. C'est donc l'appelant
    /// qui doit garantir qu'une requete ne porte qu'un utilisateur.
    /// </remarks>
    private async Task<ProvisioningExecutionResult> ExecutePerUserAsync(
        IReadOnlyList<ProvisioningExecutionRequest> requests,
        CancellationToken cancellationToken)
    {
        var results = new List<ProvisioningExecutionResult>(requests.Count);
        foreach (var request in requests)
        {
            var result = await _provisioningService.ReconcileAsync(
                request,
                cancellationToken);
            results.Add(result);
            if (!result.Succeeded)
            {
                break;
            }
        }

        return BillingV2ProvisioningResultAggregator.Combine(results);
    }

    private async Task<string?> LoadActiveSubscriptionCustomerIdAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT customer_id
            FROM billing_v2_subscriptions
            WHERE id = @subscription_id
              AND status = 'active'
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@subscription_id", subscriptionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MariaDbIdentifierReader.ReadRequired(reader, "customer_id");
    }

    private async Task<BillingV2ProvisioningDbReadiness> LoadReadinessAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                ready_for_v2_provisioning,
                add_only_mode,
                last_shadow_status,
                last_shadow_matches_legacy,
                unresolved_mismatch_count
            FROM billing_v2_provisioning_client_readiness
            WHERE customer_id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return BillingV2ProvisioningDbReadiness.NotReady;
        }

        var status = reader.IsDBNull(reader.GetOrdinal("last_shadow_status"))
            ? null
            : reader.GetString("last_shadow_status");
        return new BillingV2ProvisioningDbReadiness(
            ClientReady: reader.GetBoolean("ready_for_v2_provisioning"),
            AddOnlyMode: reader.GetBoolean("add_only_mode"),
            ShadowSucceeded: string.Equals(
                status,
                "success",
                StringComparison.OrdinalIgnoreCase),
            ShadowMatchesLegacy:
                !reader.IsDBNull(reader.GetOrdinal("last_shadow_matches_legacy"))
                && reader.GetBoolean("last_shadow_matches_legacy"),
            HasUnresolvedMismatch:
                reader.GetInt32("unresolved_mismatch_count") > 0);
    }

    private async Task<IReadOnlySet<string>> LoadMaterializedActiveSubscriptionIdsAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id
            FROM billing_v2_subscriptions
            WHERE customer_id = @customer_id
              AND status = 'active';
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(MariaDbIdentifierReader.ReadRequired(reader, "id"));
        }

        return ids;
    }

    private async Task<BillingV2ProvisioningPlan> LoadProvisioningPlanAsync(
        string customerId,
        IReadOnlyList<string> activeLegacySubscriptionIds,
        CancellationToken cancellationToken)
    {
        var rules = new List<BillingV2ProvisioningRuleProjection>();
        var legacyIdSet = activeLegacySubscriptionIds
            .ToHashSet(StringComparer.Ordinal);
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                sub.id AS subscription_id,
                item.id AS subscription_item_id,
                item.scope_type,
                item.subscription_user_id,
                subscription_user.identity_reference,
                subscription_user.is_primary,
                subscription_user.status AS subscription_user_status,
                service.code AS service_code,
                tier.code AS tier_code,
                rule.rule_type,
                rule.target_type,
                rule.target_reference,
                rule.value_source,
                rule.static_value,
                tier.numeric_value,
                tier.unit,
                provisioned.provisioned_quantity,
                provisioned.subscription_item_id AS provisioning_item_id
            FROM billing_v2_subscriptions sub
            INNER JOIN billing_v2_subscription_items item
                ON item.subscription_id = sub.id
               AND item.status = 'active'
               AND item.effective_from <= UTC_TIMESTAMP(6)
               AND (item.effective_until IS NULL
                    OR item.effective_until > UTC_TIMESTAMP(6))
            LEFT JOIN billing_v2_subscription_users subscription_user
                ON subscription_user.id = item.subscription_user_id
            LEFT JOIN billing_v2_subscription_item_provisioning provisioned
                ON provisioned.subscription_item_id = item.id
            INNER JOIN billing_v2_services service
                ON service.id = item.service_id
               AND service.status = 'active'
            LEFT JOIN billing_v2_service_tiers tier
                ON tier.id = COALESCE(provisioned.provisioned_tier_id, item.tier_id)
               AND tier.status = 'active'
            LEFT JOIN billing_v2_provisioning_rules rule
                ON rule.service_id = service.id
               AND rule.status = 'active'
               AND (rule.tier_id IS NULL OR rule.tier_id = tier.id)
            WHERE sub.customer_id = @customer_id
              AND sub.status = 'active'
            ORDER BY sub.id, item.id, rule.display_order;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var subscriptionId = MariaDbIdentifierReader.ReadRequired(
                reader,
                "subscription_id");
            if (!legacyIdSet.Contains(subscriptionId))
            {
                continue;
            }

            var subscriptionItemId = MariaDbIdentifierReader.ReadRequired(
                reader,
                "subscription_item_id");
            var serviceCode = reader.GetString("service_code");
            var tierCode = reader.IsDBNull(reader.GetOrdinal("tier_code"))
                ? null
                : reader.GetString("tier_code");
            var scopeType = reader.GetString("scope_type");
            var subscriptionUserId = MariaDbIdentifierReader.ReadNullable(
                reader,
                "subscription_user_id");
            var identityReference =
                reader.IsDBNull(reader.GetOrdinal("identity_reference"))
                    ? null
                    : reader.GetString("identity_reference");
            var subscriptionUserIsPrimary =
                !reader.IsDBNull(reader.GetOrdinal("is_primary"))
                && reader.GetBoolean("is_primary");
            var subscriptionUserStatus =
                reader.IsDBNull(reader.GetOrdinal("subscription_user_status"))
                    ? null
                    : reader.GetString("subscription_user_status");

            var provisioningItemId = MariaDbIdentifierReader.ReadNullable(
                reader,
                "provisioning_item_id");
            if (string.IsNullOrWhiteSpace(provisioningItemId))
            {
                rules.Add(new BillingV2ProvisioningRuleProjection(
                    subscriptionId,
                    subscriptionItemId,
                    serviceCode,
                    tierCode,
                    RuleType: string.Empty,
                    TargetType: string.Empty,
                    TargetReference: null,
                    ValueSource: string.Empty,
                    StaticValue: null,
                    TierNumericValue: null,
                    TierUnit: null,
                    Quantity: 0,
                    scopeType,
                    subscriptionUserId,
                    identityReference,
                    subscriptionUserIsPrimary,
                    subscriptionUserStatus));
                continue;
            }

            rules.Add(new BillingV2ProvisioningRuleProjection(
                subscriptionId,
                subscriptionItemId,
                serviceCode,
                tierCode,
                reader.IsDBNull(reader.GetOrdinal("rule_type"))
                    ? string.Empty
                    : reader.GetString("rule_type"),
                reader.IsDBNull(reader.GetOrdinal("target_type"))
                    ? string.Empty
                    : reader.GetString("target_type"),
                reader.IsDBNull(reader.GetOrdinal("target_reference"))
                    ? null
                    : reader.GetString("target_reference"),
                reader.IsDBNull(reader.GetOrdinal("value_source"))
                    ? string.Empty
                    : reader.GetString("value_source"),
                reader.IsDBNull(reader.GetOrdinal("static_value"))
                    ? null
                    : reader.GetString("static_value"),
                reader.IsDBNull(reader.GetOrdinal("numeric_value"))
                    ? null
                    : reader.GetInt64("numeric_value"),
                reader.IsDBNull(reader.GetOrdinal("unit"))
                    ? null
                    : reader.GetString("unit"),
                reader.GetInt32("provisioned_quantity"),
                scopeType,
                subscriptionUserId,
                identityReference,
                subscriptionUserIsPrimary,
                subscriptionUserStatus));
        }

        return BillingV2ProvisioningPlanner.Plan(rules);
    }

    private sealed record BillingV2ProvisioningDbReadiness(
        bool ClientReady,
        bool AddOnlyMode,
        bool ShadowSucceeded,
        bool ShadowMatchesLegacy,
        bool HasUnresolvedMismatch)
    {
        public static BillingV2ProvisioningDbReadiness NotReady { get; }
            = new(
                ClientReady: false,
                AddOnlyMode: true,
                ShadowSucceeded: false,
                ShadowMatchesLegacy: false,
                HasUnresolvedMismatch: false);
    }
}

public static class BillingV2ProvisioningReadinessGate
{
    public static BillingV2ProvisioningGateDecision Evaluate(
        BillingV2ProvisioningReadinessState state)
    {
        if (!state.GlobalFlagEnabled)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_FLAG_OFF");
        }

        if (!state.CompleteMaterialization)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_INCOMPLETE_MATERIALIZATION");
        }

        if (!state.RequiredRulesResolved)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_RULES_UNRESOLVED");
        }

        if (!state.ShadowSucceeded || !state.ShadowMatchesLegacy)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_SHADOW_NOT_MATCHING");
        }

        if (state.HasUnresolvedMismatch)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_UNRESOLVED_MISMATCH");
        }

        if (!state.TargetGroupsResolved)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_TARGETS_UNRESOLVED");
        }

        if (!state.ClientReady)
        {
            return BillingV2ProvisioningGateDecision.Deny(
                "BILLING_V2_PROVISIONING_CLIENT_NOT_READY");
        }

        return BillingV2ProvisioningGateDecision.Allow(state.AddOnlyMode);
    }
}

public static class BillingV2ProvisioningExecutionPolicy
{
    public static IReadOnlyList<string> ResolveManagedGroupsForExecution(
        BillingV2ProvisioningGateDecision decision,
        IReadOnlyList<string> desiredGroups,
        IReadOnlyList<string> managedGroups)
        => decision.AddOnlyMode
            ? desiredGroups
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : managedGroups
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToArray();
}

public static class BillingV2ProvisioningIdentityResolver
{
    /// <summary>
    /// Resout <c>billing_v2_subscription_users.identity_reference</c> vers une
    /// identite Active Directory unique et appartenant bien a ce client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// L'invariant attendu est <c>identity_reference = portal_users.id</c> :
    /// c'est ce que le planificateur de checkout ecrit pour l'utilisateur
    /// principal. Aucune contrainte de schema ne le garantit et un utilisateur
    /// supplementaire n'a aujourd'hui aucune identite du tout, donc chaque
    /// etape reste verifiee ici plutot que supposee.
    /// </para>
    /// <para>
    /// La cle d'identite finale est <c>objectGUID</c>, jamais
    /// <c>sAMAccountName</c> : dans une foret multi-domaines un
    /// <c>sAMAccountName</c> n'est unique que dans son domaine, alors que
    /// <c>objectGUID</c> est immuable et unique dans toute la foret. La colonne
    /// <c>customer_ad_links.object_guid</c> porte d'ailleurs un index UNIQUE.
    /// Le <c>sAMAccountName</c> reste une propriete descriptive, utilisee pour
    /// l'execution et les journaux.
    /// </para>
    /// <para>
    /// Toute anomalie est un echec : zero lien, plusieurs liens, un lien d'un
    /// autre client, un lien sans <c>objectGUID</c> ou avec un
    /// <c>objectGUID</c> malforme, un <c>objectGUID</c> absent du referentiel
    /// de liens du client, deux instantanes incoherents du meme objet, ou deux
    /// utilisateurs d'abonnement pointant la meme identite AD.
    /// </para>
    /// </remarks>
    public static BillingV2ProvisioningTargetResolution Resolve(
        string customerId,
        IReadOnlyList<BillingV2UserDesiredState> users,
        IReadOnlyDictionary<string, IReadOnlyList<PortalUserAdLinkRecord>>
            linksByIdentityReference,
        IReadOnlyList<CustomerAdLinkSummary> customerUserLinks)
    {
        var targets = new List<BillingV2ResolvedProvisioningTarget>();
        foreach (var user in users.OrderBy(
            candidate => candidate.SubscriptionUserId,
            StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(user.IdentityReference))
            {
                return BillingV2ProvisioningTargetResolution.Fail(
                    "BILLING_V2_PROVISIONING_IDENTITY_MISSING");
            }

            if (!linksByIdentityReference.TryGetValue(
                    user.IdentityReference,
                    out var portalLinks)
                || portalLinks.Count == 0)
            {
                return BillingV2ProvisioningTargetResolution.Fail(
                    "BILLING_V2_PROVISIONING_IDENTITY_NOT_LINKED");
            }

            if (portalLinks.Count > 1)
            {
                return BillingV2ProvisioningTargetResolution.Fail(
                    "BILLING_V2_PROVISIONING_IDENTITY_AMBIGUOUS");
            }

            var portalLink = portalLinks[0];
            if (!string.Equals(
                    portalLink.CustomerId,
                    customerId,
                    StringComparison.Ordinal))
            {
                return BillingV2ProvisioningTargetResolution.Fail(
                    "BILLING_V2_PROVISIONING_IDENTITY_CUSTOMER_MISMATCH");
            }

            if (string.IsNullOrWhiteSpace(portalLink.ObjectGuid))
            {
                return BillingV2ProvisioningTargetResolution.Fail(
                    "BILLING_V2_PROVISIONING_IDENTITY_GUID_MISSING");
            }

            var expectedObjectGuid = NormalizeObjectGuid(portalLink.ObjectGuid);
            if (expectedObjectGuid is null)
            {
                return BillingV2ProvisioningTargetResolution.Fail(
                    "BILLING_V2_PROVISIONING_IDENTITY_GUID_INVALID");
            }

            // Correlation forte : objectGUID exact. Le sAMAccountName n'est
            // unique que dans un domaine, donc il ne peut pas designer une
            // identite dans une foret multi-domaines.
            var matching = customerUserLinks
                .Where(candidate => string.Equals(
                    NormalizeObjectGuid(candidate.ObjectGuid),
                    expectedObjectGuid,
                    StringComparison.Ordinal))
                .ToArray();
            if (matching.Length == 0)
            {
                return BillingV2ProvisioningTargetResolution.Fail(
                    "BILLING_V2_PROVISIONING_IDENTITY_NOT_LINKED");
            }

            if (matching.Length > 1)
            {
                return BillingV2ProvisioningTargetResolution.Fail(
                    "BILLING_V2_PROVISIONING_IDENTITY_AMBIGUOUS");
            }

            var adLink = matching[0];
            if (!ObjectSidsAreCoherent(portalLink.ObjectSid, adLink.ObjectSid))
            {
                return BillingV2ProvisioningTargetResolution.Fail(
                    "BILLING_V2_PROVISIONING_IDENTITY_SID_MISMATCH");
            }

            targets.Add(new BillingV2ResolvedProvisioningTarget(user, adLink));
        }

        var distinctAccounts = targets
            .Select(target => NormalizeObjectGuid(target.AdLink.ObjectGuid))
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctAccounts != targets.Count)
        {
            return BillingV2ProvisioningTargetResolution.Fail(
                "BILLING_V2_PROVISIONING_IDENTITY_AMBIGUOUS");
        }

        return BillingV2ProvisioningTargetResolution.Success(targets);
    }

    /// <summary>
    /// Ramene un <c>objectGUID</c> a sa forme canonique, ou <c>null</c> s'il
    /// est inexploitable comme cle d'identite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>customer_ad_links.object_guid</c> est un <c>CHAR(36)</c> que
    /// MySqlConnector materialise en <see cref="Guid"/>, donc la persistance
    /// reelle fournit toujours une valeur parsable. La comparaison reste
    /// tolerante aux accolades et a la casse pour ne pas dependre de la forme
    /// d'ecriture.
    /// </para>
    /// <para>
    /// Une valeur non vide mais non parsable est rejetee : une chaine opaque
    /// ne doit jamais servir de cle d'identite, meme si la meme chaine se
    /// retrouve des deux cotes de la comparaison. Un candidat inexploitable ne
    /// peut donc jamais etre selectionne, puisqu'il ne s'egale a aucune forme
    /// canonique.
    /// </para>
    /// </remarks>
    public static string? NormalizeObjectGuid(string? objectGuid)
    {
        if (string.IsNullOrWhiteSpace(objectGuid))
        {
            return null;
        }

        var trimmed = objectGuid.Trim().Trim('{', '}');
        return Guid.TryParse(trimmed, out var parsed)
            ? parsed.ToString("D")
            : null;
    }

    /// <summary>
    /// Verifie que les deux lectures de <c>customer_ad_links</c> decrivent le
    /// meme etat de l'objet annuaire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// C'est un controle de coherence entre deux instantanes, pas une
    /// affirmation sur la stabilite du SID. Un <c>objectSid</c> change
    /// legitimement lorsqu'un utilisateur est deplace entre deux domaines
    /// d'une meme foret : l'objet obtient un SID du nouveau domaine et
    /// l'ancien peut etre conserve dans <c>sIDHistory</c>. Seul
    /// <c>objectGUID</c> reste stable pendant toute la vie de l'objet, y
    /// compris apres renommage ou deplacement, d'ou son role de cle.
    /// </para>
    /// <para>
    /// Les deux valeurs comparees ici proviennent de la meme ligne, lue par
    /// deux requetes distinctes. Une divergence signale donc une lecture prise
    /// de part et d'autre d'un rafraichissement du lien, c'est-a-dire un etat
    /// dont on ne sait pas lequel des deux est a jour. Le refus est temporaire
    /// et non definitif : une fois les deux sources rafraichies, un cycle
    /// ulterieur resout normalement.
    /// </para>
    /// </remarks>
    private static bool ObjectSidsAreCoherent(
        string? portalLinkObjectSid,
        string? customerLinkObjectSid)
    {
        if (string.IsNullOrWhiteSpace(portalLinkObjectSid)
            || string.IsNullOrWhiteSpace(customerLinkObjectSid))
        {
            return false;
        }

        return string.Equals(
            portalLinkObjectSid.Trim(),
            customerLinkObjectSid.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}

public static class BillingV2ProvisioningExecutionPlanner
{
    /// <summary>
    /// Construit une requete de reconciliation par utilisateur.
    /// </summary>
    /// <remarks>
    /// Chaque requete ne porte qu'un <c>TargetUsers</c> et que les groupes de
    /// cet utilisateur. Les groupes geres sont bornes aux groupes desires du
    /// meme utilisateur : le moteur AD ne peut donc emettre que des ajouts, et
    /// jamais un retrait ni un ajout croise.
    /// </remarks>
    public static IReadOnlyList<ProvisioningExecutionRequest> BuildPerUserRequests(
        BillingV2ProvisioningGateDecision decision,
        IReadOnlyList<BillingV2ResolvedProvisioningTarget> targets,
        IReadOnlyDictionary<string, string?>
            groupDistinguishedNamesBySamAccountName)
    {
        var requests = new List<ProvisioningExecutionRequest>();
        foreach (var target in targets
            .OrderBy(
                candidate => candidate.AdLink.SamAccountName,
                StringComparer.OrdinalIgnoreCase)
            // Deux comptes de domaines differents peuvent porter le meme
            // sAMAccountName : l'objectGUID garde l'ordre deterministe.
            .ThenBy(
                candidate => candidate.AdLink.ObjectGuid,
                StringComparer.Ordinal))
        {
            var desiredGroups = target.DesiredState.DesiredAdGroups
                .Where(group => !string.IsNullOrWhiteSpace(group))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (desiredGroups.Length == 0)
            {
                // Cet utilisateur n'a achete aucun droit AD : pas de requete,
                // donc aucune operation ne le nommera.
                continue;
            }

            requests.Add(new ProvisioningExecutionRequest(
                [target.AdLink],
                desiredGroups,
                BillingV2ProvisioningExecutionPolicy
                    .ResolveManagedGroupsForExecution(
                        decision,
                        desiredGroups,
                        desiredGroups),
                groupDistinguishedNamesBySamAccountName));
        }

        return requests;
    }
}

public static class BillingV2ProvisioningResultAggregator
{
    public static ProvisioningExecutionResult Combine(
        IReadOnlyList<ProvisioningExecutionResult> results)
    {
        if (results.Count == 0)
        {
            return new ProvisioningExecutionResult(
                true,
                false,
                "PROVISIONING_MAPPING_EMPTY",
                Array.Empty<ProvisioningOperationResult>());
        }

        var operations = new List<ProvisioningOperationResult>();
        var changed = false;
        foreach (var result in results)
        {
            operations.AddRange(result.Operations);
            changed |= result.Changed;
            if (!result.Succeeded)
            {
                return new ProvisioningExecutionResult(
                    false,
                    changed,
                    result.ResultCode,
                    operations);
            }
        }

        return new ProvisioningExecutionResult(
            true,
            changed,
            changed
                ? "PROVISIONING_APPLIED"
                : "PROVISIONING_UNCHANGED",
            operations);
    }
}

/// <summary>
/// Nature d'une regle une fois le couple <c>rule_type</c> / <c>target_type</c>
/// reconnu.
/// </summary>
public enum BillingV2ProvisioningRuleKind
{
    /// <summary>Couple inconnu : rien ne doit en etre deduit.</summary>
    Unknown = 0,

    /// <summary>Appartenance a un groupe Active Directory.</summary>
    AdGroupMembership,

    /// <summary>
    /// Stockage personnel : socle technique de l'environnement utilisateur.
    /// </summary>
    UserStorageQuota,

    /// <summary>Quota de stockage porte par le groupe secondaire du client.</summary>
    SharedStorageQuota,

    /// <summary>Droit reconnu qui n'emet aucune ecriture externe.</summary>
    AcknowledgedEntitlement,
}

/// <summary>
/// Scope impose par la regle, independamment de celui declare sur l'item.
/// </summary>
public enum BillingV2ProvisioningRuleScope
{
    User,
    Subscription,
    Any,
}

/// <summary>
/// Vocabulaire des regles de provisioning V2 et classification stricte.
/// </summary>
/// <remarks>
/// <para>
/// La classification est une liste blanche : seul un couple explicitement
/// enumere ici est compris. Un <c>rule_type</c> ou un <c>target_type</c>
/// inconnu ne recoit aucune interpretation par defaut, faute de quoi une faute
/// de frappe dans le catalogue deviendrait silencieusement un noop.
/// </para>
/// <para>
/// Le scope fait partie de la classification. Un quota personnel achete au
/// niveau abonnement, ou un quota de groupe secondaire attache a un
/// utilisateur, decrit une intention que le modele ne sait pas honorer : la
/// regle est reconnue, l'item est refuse.
/// </para>
/// </remarks>
public static class BillingV2ProvisioningRuleSemantics
{
    public const string AdGroupMembershipRule = "ad_group_membership";
    public const string InfrastructureActionRule = "infrastructure_action";
    public const string InheritedCoverageRule = "inherited_coverage";
    public const string PlatformEntitlementRule = "platform_entitlement";
    public const string ContractualEntitlementRule = "contractual_entitlement";
    public const string ServiceDeliveryRule = "service_delivery";

    public const string AdGroupTarget = "ad_group";
    public const string KoxoUserStorageTarget = "koxo_user_storage";
    public const string KoxoSecondaryGroupStorageTarget =
        "koxo_secondary_group_storage";
    public const string BackupPolicyTarget = "backup_policy";
    public const string PlatformTarget = "platform";
    public const string MonitoringTarget = "monitoring";
    public const string SupportLevelTarget = "support_level";
    public const string OnboardingTarget = "onboarding";

    /// <summary>
    /// Droit commercial a un utilisateur d'abonnement supplementaire.
    /// </summary>
    /// <remarks>
    /// Ce n'est pas un mecanisme de creation d'identite : la creation du compte
    /// annuaire appartient a la chaine KoXo, et il ne doit exister qu'un seul
    /// proprietaire de cette operation. Le droit commercial autorise un
    /// utilisateur de plus, il ne le materialise pas.
    /// </remarks>
    public const string UserSlotTarget = "user_slot";

    /// <summary>
    /// Unite dans laquelle le catalogue exprime les tiers de stockage.
    /// </summary>
    /// <remarks>
    /// Le tier ne porte qu'un nombre : sans unite verifiee, 64 pourrait aussi
    /// bien valoir 64 Mio que 64 Tio. La conversion vers l'unite de KoXo est
    /// laissee au provider reel.
    /// </remarks>
    public const string ExpectedStorageUnit = "GiB";

    private static readonly HashSet<string> KnownRuleTypes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        AdGroupMembershipRule,
        InfrastructureActionRule,
        InheritedCoverageRule,
        PlatformEntitlementRule,
        ContractualEntitlementRule,
        ServiceDeliveryRule,
    };

    public static bool IsKnownRuleType(string? ruleType)
        => !string.IsNullOrWhiteSpace(ruleType)
            && KnownRuleTypes.Contains(ruleType.Trim());

    public static bool TryClassify(
        string? ruleType,
        string? targetType,
        out BillingV2ProvisioningRuleKind kind,
        out BillingV2ProvisioningRuleScope scope)
    {
        kind = BillingV2ProvisioningRuleKind.Unknown;
        scope = BillingV2ProvisioningRuleScope.Any;

        if (string.IsNullOrWhiteSpace(ruleType)
            || string.IsNullOrWhiteSpace(targetType))
        {
            return false;
        }

        var rule = ruleType.Trim();
        var target = targetType.Trim();

        if (Matches(rule, AdGroupMembershipRule)
            && Matches(target, AdGroupTarget))
        {
            kind = BillingV2ProvisioningRuleKind.AdGroupMembership;
            scope = BillingV2ProvisioningRuleScope.User;
            return true;
        }

        if (Matches(rule, InfrastructureActionRule)
            && Matches(target, KoxoUserStorageTarget))
        {
            kind = BillingV2ProvisioningRuleKind.UserStorageQuota;
            scope = BillingV2ProvisioningRuleScope.User;
            return true;
        }

        if (Matches(rule, InfrastructureActionRule)
            && Matches(target, KoxoSecondaryGroupStorageTarget))
        {
            kind = BillingV2ProvisioningRuleKind.SharedStorageQuota;
            scope = BillingV2ProvisioningRuleScope.Subscription;
            return true;
        }

        // La sauvegarde est portee par une politique globale deja en place :
        // un nouveau dossier place dans le perimetre sauvegarde est couvert
        // sans objet dedie. Le scope reste libre parce que la couverture vaut
        // pour un dossier personnel comme pour un dossier de groupe.
        if (Matches(rule, InheritedCoverageRule)
            && Matches(target, BackupPolicyTarget))
        {
            kind = BillingV2ProvisioningRuleKind.AcknowledgedEntitlement;
            scope = BillingV2ProvisioningRuleScope.Any;
            return true;
        }

        if (Matches(rule, PlatformEntitlementRule)
            && (Matches(target, PlatformTarget)
                || Matches(target, MonitoringTarget)))
        {
            kind = BillingV2ProvisioningRuleKind.AcknowledgedEntitlement;
            scope = BillingV2ProvisioningRuleScope.Subscription;
            return true;
        }

        if (Matches(rule, ContractualEntitlementRule)
            && Matches(target, SupportLevelTarget))
        {
            kind = BillingV2ProvisioningRuleKind.AcknowledgedEntitlement;
            scope = BillingV2ProvisioningRuleScope.Subscription;
            return true;
        }

        // Le droit a un utilisateur supplementaire est commercial : il autorise
        // un billing_v2_subscription_user de plus, il ne cree rien. Son
        // equipement technique passe par le stockage personnel de cet
        // utilisateur, comme pour tout autre utilisateur.
        if (Matches(rule, ContractualEntitlementRule)
            && Matches(target, UserSlotTarget))
        {
            kind = BillingV2ProvisioningRuleKind.AcknowledgedEntitlement;
            scope = BillingV2ProvisioningRuleScope.Any;
            return true;
        }

        if (Matches(rule, ServiceDeliveryRule)
            && Matches(target, OnboardingTarget))
        {
            kind = BillingV2ProvisioningRuleKind.AcknowledgedEntitlement;
            scope = BillingV2ProvisioningRuleScope.Subscription;
            return true;
        }

        return false;
    }

    private static bool Matches(string value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}

public static class BillingV2ProvisioningPlanner
{
    private const string UserScope = "user";
    private const string SubscriptionScope = "subscription";

    /// <summary>
    /// Transforme les lignes de projection en etats desires, un par
    /// utilisateur d'abonnement.
    /// </summary>
    /// <remarks>
    /// Aucune ligne n'est ignoree en silence : tout ce qui n'est pas
    /// explicitement resolu tombe dans
    /// <see cref="BillingV2ProvisioningPlan.UnresolvedRuleReferences"/>, ce que
    /// la gate de readiness traduit en refus.
    /// </remarks>
    public static BillingV2ProvisioningPlan Plan(
        IReadOnlyList<BillingV2ProvisioningRuleProjection> rules)
    {
        var state = new PlanningState();

        foreach (var rule in rules)
        {
            Classify(rule, state);
        }

        EnforcePersonalStoragePrerequisite(state);

        var users = state.UserOrder
            .Select(subscriptionUserId => new BillingV2UserDesiredState(
                subscriptionUserId,
                state.IdentityByUserId[subscriptionUserId],
                state.GroupsByUserId[subscriptionUserId].ToArray(),
                state.PersonalStorageByUserId.GetValueOrDefault(
                    subscriptionUserId),
                state.CoveragesByUserId[subscriptionUserId],
                state.EntitlementsByUserId[subscriptionUserId]))
            .ToArray();

        return new BillingV2ProvisioningPlan(
            users,
            new BillingV2SubscriptionDesiredState(
                state.SharedQuotas,
                state.SubscriptionCoverages,
                state.SubscriptionEntitlements,
                state.UnassignedUserSlots),
            state.Blockers
                .OrderBy(blocker => blocker.RuleReference, StringComparer.OrdinalIgnoreCase)
                .ThenBy(blocker => blocker.ReasonCode, StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>
    /// Refuse tout acces aval accorde a un utilisateur sans stockage personnel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// L'ordre voulu est : stockage personnel achete, environnement utilisateur
    /// provisionne, puis seulement acces optionnels. Un acces VPN ou RDS sans
    /// environnement ouvrirait une porte vers un poste inexistant. Le compte
    /// annuaire, lui, n'est pas produit par ce stockage : il preexiste pour un
    /// client payant ordinaire, et c'est KoXo qui le cree pour un essai de
    /// demonstration.
    /// </para>
    /// <para>
    /// Le controle est un post-passage parce que l'ordre des lignes de
    /// projection n'est pas garanti : l'acces peut arriver avant le stockage
    /// qui le rend legitime.
    /// </para>
    /// <para>
    /// Le refus est volontairement grossier dans cette version. Le moteur ne
    /// sait pas encore representer une sequence partielle (environnement pret,
    /// acces en attente), donc tant qu'il ne le sait pas, il refuse.
    /// </para>
    /// </remarks>
    private static void EnforcePersonalStoragePrerequisite(PlanningState state)
    {
        foreach (var subscriptionUserId in state.UserOrder)
        {
            if (state.GroupsByUserId[subscriptionUserId].Count == 0
                || state.PersonalStorageByUserId.ContainsKey(subscriptionUserId))
            {
                continue;
            }

            foreach (var reference in state
                .AdAccessReferencesByUserId[subscriptionUserId])
            {
                state.Blockers.Add(new BillingV2ProvisioningBlocker(
                    reference,
                    BillingV2ProvisioningBlockerReasons.PersonalStorageRequired));
            }
        }
    }

    /// <summary>
    /// Classe une ligne de projection, ou l'inscrit comme bloquante.
    /// </summary>
    /// <remarks>
    /// Aucune sortie silencieuse : chaque chemin se termine soit par un ajout a
    /// un etat desire, soit par un blocage motive.
    /// </remarks>
    private static void Classify(
        BillingV2ProvisioningRuleProjection rule,
        PlanningState state)
    {
        // Item actif sans regle explicite, ou item non materialise : le
        // catalogue ne dit pas quoi faire, ce qui n'autorise pas a ne rien
        // faire.
        if (string.IsNullOrWhiteSpace(rule.RuleType)
            || string.IsNullOrWhiteSpace(rule.TargetType))
        {
            state.Block(rule, BillingV2ProvisioningBlockerReasons.RuleMissing);
            return;
        }

        if (!BillingV2ProvisioningRuleSemantics.IsKnownRuleType(rule.RuleType))
        {
            state.Block(rule, BillingV2ProvisioningBlockerReasons.RuleTypeUnknown);
            return;
        }

        if (!BillingV2ProvisioningRuleSemantics.TryClassify(
                rule.RuleType,
                rule.TargetType,
                out var kind,
                out var requiredScope))
        {
            state.Block(
                rule,
                BillingV2ProvisioningBlockerReasons.TargetTypeUnknown);
            return;
        }

        var itemIsUserScoped = string.Equals(
            rule.ScopeType,
            UserScope,
            StringComparison.OrdinalIgnoreCase);
        var itemIsSubscriptionScoped = string.Equals(
            rule.ScopeType,
            SubscriptionScope,
            StringComparison.OrdinalIgnoreCase);

        if (!itemIsUserScoped && !itemIsSubscriptionScoped)
        {
            state.Block(
                rule,
                BillingV2ProvisioningBlockerReasons.ScopeIncoherent);
            return;
        }

        // Le scope impose par la regle prime sur celui declare par l'item : un
        // quota de groupe secondaire attache a un utilisateur, ou l'inverse,
        // decrit une intention que le modele ne sait pas honorer.
        if (requiredScope == BillingV2ProvisioningRuleScope.User
                && !itemIsUserScoped
            || requiredScope == BillingV2ProvisioningRuleScope.Subscription
                && !itemIsSubscriptionScoped)
        {
            state.Block(
                rule,
                BillingV2ProvisioningBlockerReasons.ScopeIncoherent);
            return;
        }

        if (itemIsSubscriptionScoped)
        {
            // Un item de scope abonnement ne doit pas porter d'utilisateur :
            // l'incoherence de scope est une anomalie, pas un detail.
            if (!string.IsNullOrWhiteSpace(rule.SubscriptionUserId))
            {
                state.Block(
                    rule,
                    BillingV2ProvisioningBlockerReasons.ScopeIncoherent);
                return;
            }

            ClassifySubscriptionScoped(rule, kind, state);
            return;
        }

        ClassifyUserScoped(rule, kind, state);
    }

    private static void ClassifySubscriptionScoped(
        BillingV2ProvisioningRuleProjection rule,
        BillingV2ProvisioningRuleKind kind,
        PlanningState state)
    {
        switch (kind)
        {
            case BillingV2ProvisioningRuleKind.SharedStorageQuota:
                if (!TryBuildQuota(
                        rule,
                        subscriptionUserId: null,
                        identityReference: null,
                        SubscriptionScope,
                        state,
                        out var sharedQuota))
                {
                    return;
                }

                state.SharedQuotas.Add(sharedQuota);
                return;

            case BillingV2ProvisioningRuleKind.AcknowledgedEntitlement:
                var entitlement = CreateEntitlement(rule, SubscriptionScope);
                if (IsInheritedCoverage(rule))
                {
                    state.SubscriptionCoverages.Add(entitlement);
                    return;
                }

                state.SubscriptionEntitlements.Add(entitlement);
                return;

            default:
                // AD, stockage personnel et identite ont un titulaire par
                // nature. Les rattacher a l'abonnement reviendrait a choisir
                // arbitrairement un utilisateur du client.
                state.Block(
                    rule,
                    BillingV2ProvisioningBlockerReasons.ScopeIncoherent);
                return;
        }
    }

    private static void ClassifyUserScoped(
        BillingV2ProvisioningRuleProjection rule,
        BillingV2ProvisioningRuleKind kind,
        PlanningState state)
    {
        // Un item de scope utilisateur sans utilisateur n'a pas de titulaire :
        // il ne doit jamais retomber sur les utilisateurs du client.
        if (string.IsNullOrWhiteSpace(rule.SubscriptionUserId))
        {
            state.Block(
                rule,
                BillingV2ProvisioningBlockerReasons.ScopeIncoherent);
            return;
        }

        var subscriptionUserId = rule.SubscriptionUserId.Trim();

        if (!string.Equals(
                rule.SubscriptionUserStatus,
                "active",
                StringComparison.OrdinalIgnoreCase))
        {
            state.Block(
                rule,
                BillingV2ProvisioningBlockerReasons.UserNotActive);
            return;
        }

        // Une place d'utilisateur est un droit commercial, pas une ressource.
        // Tant qu'aucune personne ne lui est affectee, elle ne demande aucune
        // ecriture : la representer sans identite est donc legitime, et la
        // faire echouer immobiliserait tout l'abonnement pour une place que le
        // client n'a simplement pas encore attribuee.
        if (kind == BillingV2ProvisioningRuleKind.AcknowledgedEntitlement
            && string.Equals(
                rule.TargetType?.Trim(),
                BillingV2ProvisioningRuleSemantics.UserSlotTarget,
                StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(rule.IdentityReference))
        {
            state.UnassignedUserSlots.Add(CreateEntitlement(rule, UserScope));
            return;
        }

        // Toute autre regle de scope utilisateur decrit une ressource reelle :
        // sans identite de facturation, il n'existe aucun titulaire a qui
        // l'appliquer, et aucun repli vers un autre utilisateur du client n'est
        // acceptable. C'est en attachant STORAGE-PERSONAL, VPN ou RDS a une
        // place vide que l'absence d'identite redevient bloquante.
        if (string.IsNullOrWhiteSpace(rule.IdentityReference))
        {
            state.Block(
                rule,
                BillingV2ProvisioningBlockerReasons.IdentityRequired);
            return;
        }

        var identityReference = rule.IdentityReference.Trim();
        if (!state.TryRegisterUser(subscriptionUserId, identityReference))
        {
            state.Block(
                rule,
                BillingV2ProvisioningBlockerReasons.UserIdentityConflict);
            return;
        }

        switch (kind)
        {
            case BillingV2ProvisioningRuleKind.AdGroupMembership:
                if (string.IsNullOrWhiteSpace(rule.TargetReference))
                {
                    state.Block(
                        rule,
                        BillingV2ProvisioningBlockerReasons
                            .TargetReferenceMissing);
                    return;
                }

                state.GroupsByUserId[subscriptionUserId]
                    .Add(rule.TargetReference.Trim());
                state.AdAccessReferencesByUserId[subscriptionUserId]
                    .Add(CreateRuleReference(rule));
                return;

            case BillingV2ProvisioningRuleKind.UserStorageQuota:
                if (!TryBuildQuota(
                        rule,
                        subscriptionUserId,
                        identityReference,
                        UserScope,
                        state,
                        out var userQuota))
                {
                    return;
                }

                // Un environnement utilisateur est unique : deux quotas
                // personnels contradictoires ne se departagent pas.
                if (state.PersonalStorageByUserId.ContainsKey(subscriptionUserId))
                {
                    state.Block(
                        rule,
                        BillingV2ProvisioningBlockerReasons
                            .PersonalStorageConflict);
                    return;
                }

                state.PersonalStorageByUserId[subscriptionUserId] = userQuota;
                return;

            case BillingV2ProvisioningRuleKind.AcknowledgedEntitlement:
                var entitlement = CreateEntitlement(rule, UserScope);
                if (IsInheritedCoverage(rule))
                {
                    state.CoveragesByUserId[subscriptionUserId].Add(entitlement);
                    return;
                }

                state.EntitlementsByUserId[subscriptionUserId].Add(entitlement);
                return;

            default:
                state.Block(
                    rule,
                    BillingV2ProvisioningBlockerReasons.ScopeIncoherent);
                return;
        }
    }

    private static bool TryBuildQuota(
        BillingV2ProvisioningRuleProjection rule,
        string? subscriptionUserId,
        string? identityReference,
        string scopeType,
        PlanningState state,
        out BillingV2StorageQuotaPlan quota)
    {
        quota = null!;

        var value = ResolveValue(rule);
        if (value is null)
        {
            state.Block(
                rule,
                BillingV2ProvisioningBlockerReasons.ValueUnresolved);
            return false;
        }

        // Le catalogue est la seule source de l'unite : aucune valeur par
        // defaut n'est fabriquee ici, sinon un tier sans unite deviendrait
        // silencieusement un quota en GiB.
        if (!string.Equals(
                rule.TierUnit?.Trim(),
                BillingV2ProvisioningRuleSemantics.ExpectedStorageUnit,
                StringComparison.OrdinalIgnoreCase))
        {
            state.Block(
                rule,
                BillingV2ProvisioningBlockerReasons.UnitUnexpected);
            return false;
        }

        quota = new BillingV2StorageQuotaPlan(
            rule.SubscriptionItemId,
            subscriptionUserId,
            rule.TargetType.Trim(),
            identityReference,
            value.Value,
            BillingV2ProvisioningRuleSemantics.ExpectedStorageUnit,
            scopeType);
        return true;
    }

    private static BillingV2AcknowledgedEntitlement CreateEntitlement(
        BillingV2ProvisioningRuleProjection rule,
        string scopeType)
        => new(
            rule.SubscriptionItemId,
            string.IsNullOrWhiteSpace(rule.SubscriptionUserId)
                ? null
                : rule.SubscriptionUserId.Trim(),
            rule.ServiceCode,
            rule.RuleType.Trim(),
            rule.TargetType.Trim(),
            rule.TargetReference,
            scopeType);

    private static bool IsInheritedCoverage(
        BillingV2ProvisioningRuleProjection rule)
        => string.Equals(
            rule.RuleType?.Trim(),
            BillingV2ProvisioningRuleSemantics.InheritedCoverageRule,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Accumulateur du planificateur.
    /// </summary>
    private sealed class PlanningState
    {
        public List<string> UserOrder { get; } = [];

        public Dictionary<string, string> IdentityByUserId { get; }
            = new(StringComparer.Ordinal);

        public Dictionary<string, SortedSet<string>> GroupsByUserId { get; }
            = new(StringComparer.Ordinal);

        /// <summary>
        /// Socle technique de chaque utilisateur, au plus un par utilisateur.
        /// </summary>
        public Dictionary<string, BillingV2StorageQuotaPlan>
            PersonalStorageByUserId
        { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// References des lignes d'acces AD, pour pouvoir les bloquer
        /// nominativement si le socle manque.
        /// </summary>
        public Dictionary<string, List<string>> AdAccessReferencesByUserId
        { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<BillingV2AcknowledgedEntitlement>>
            CoveragesByUserId
        { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<BillingV2AcknowledgedEntitlement>>
            EntitlementsByUserId
        { get; } = new(StringComparer.Ordinal);

        public List<BillingV2StorageQuotaPlan> SharedQuotas { get; } = [];

        public List<BillingV2AcknowledgedEntitlement> SubscriptionCoverages
        { get; } = [];

        public List<BillingV2AcknowledgedEntitlement> SubscriptionEntitlements
        { get; } = [];

        /// <summary>
        /// Places d'utilisateur payees mais pas encore attribuees.
        /// </summary>
        public List<BillingV2AcknowledgedEntitlement> UnassignedUserSlots
        { get; } = [];

        public List<BillingV2ProvisioningBlocker> Blockers { get; } = [];

        public void Block(
            BillingV2ProvisioningRuleProjection rule,
            string reasonCode)
            => Blockers.Add(new BillingV2ProvisioningBlocker(
                CreateRuleReference(rule),
                reasonCode));

        public bool TryRegisterUser(
            string subscriptionUserId,
            string identityReference)
        {
            if (IdentityByUserId.TryGetValue(subscriptionUserId, out var known))
            {
                return string.Equals(
                    known,
                    identityReference,
                    StringComparison.Ordinal);
            }

            IdentityByUserId[subscriptionUserId] = identityReference;
            GroupsByUserId[subscriptionUserId] =
                new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            AdAccessReferencesByUserId[subscriptionUserId] = [];
            CoveragesByUserId[subscriptionUserId] = [];
            EntitlementsByUserId[subscriptionUserId] = [];
            UserOrder.Add(subscriptionUserId);
            return true;
        }
    }

    private static long? ResolveValue(BillingV2ProvisioningRuleProjection rule)
    {
        if (string.Equals(
                rule.ValueSource,
                "tier_numeric_value",
                StringComparison.OrdinalIgnoreCase))
        {
            return rule.TierNumericValue;
        }

        if (string.Equals(
                rule.ValueSource,
                "static",
                StringComparison.OrdinalIgnoreCase)
            && long.TryParse(rule.StaticValue, out var staticValue))
        {
            return staticValue;
        }

        return null;
    }

    private static string CreateRuleReference(
        BillingV2ProvisioningRuleProjection rule)
        => $"{rule.ServiceCode}:{rule.TierCode ?? "no-tier"}:{rule.SubscriptionItemId}";
}
