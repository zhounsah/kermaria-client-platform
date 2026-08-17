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
/// Etat desire d'un seul <c>billing_v2_subscription_user</c>.
/// </summary>
/// <remarks>
/// Le plan V2 n'expose plus aucun ensemble de groupes AD au niveau client :
/// un groupe n'existe que porte par l'utilisateur qui l'a achete. C'est cette
/// structure, et non un controle a l'execution, qui rend impossible d'appliquer
/// le droit de A a B.
/// </remarks>
public sealed record BillingV2UserDesiredState(
    string SubscriptionUserId,
    string IdentityReference,
    IReadOnlyList<string> DesiredAdGroups,
    IReadOnlyList<BillingV2NextcloudQuotaPlan> UserStoragePlans);

/// <summary>
/// Ressources de scope abonnement, volontairement separees des utilisateurs.
/// </summary>
/// <remarks>
/// Aucun groupe AD n'y figure : la semantique utilisateur d'un droit AD achete
/// au niveau abonnement n'est pas definie dans le modele actuel, donc le
/// planificateur le classe non resolu plutot que de le distribuer a tous les
/// utilisateurs du client.
/// </remarks>
public sealed record BillingV2SubscriptionDesiredState(
    IReadOnlyList<BillingV2NextcloudQuotaPlan> SharedStoragePlans)
{
    public static BillingV2SubscriptionDesiredState Empty { get; }
        = new(Array.Empty<BillingV2NextcloudQuotaPlan>());
}

public sealed record BillingV2ProvisioningPlan(
    IReadOnlyList<BillingV2UserDesiredState> Users,
    BillingV2SubscriptionDesiredState SubscriptionResources,
    IReadOnlyList<string> UnresolvedRuleReferences)
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
    /// Tous les plans de quota, personnels et partages.
    /// </summary>
    /// <remarks>
    /// Le chemin reel du quota de stockage passe par KoXo puis le systeme de
    /// fichiers, pas par un appel direct : aucun de ces plans n'est executable
    /// aujourd'hui et leur seule presence doit continuer a bloquer l'execution.
    /// </remarks>
    public IReadOnlyList<BillingV2NextcloudQuotaPlan> NextcloudQuotas => Users
        .SelectMany(user => user.UserStoragePlans)
        .Concat(SubscriptionResources.SharedStoragePlans)
        .ToArray();

    public static BillingV2ProvisioningPlan Empty { get; }
        = new(
            Array.Empty<BillingV2UserDesiredState>(),
            BillingV2SubscriptionDesiredState.Empty,
            Array.Empty<string>());
}

public sealed record BillingV2NextcloudQuotaPlan(
    string SubscriptionItemId,
    string? SubscriptionUserId,
    string TargetType,
    string? IdentityReference,
    long QuotaValue,
    string Unit);

public sealed record BillingV2NextcloudQuotaReadiness(
    bool CanApplyQuotas,
    string ReasonCode);

public interface IBillingV2NextcloudQuotaProvider
{
    BillingV2NextcloudQuotaReadiness CheckReadiness(
        IReadOnlyList<BillingV2NextcloudQuotaPlan> quotas);
}

public sealed class DormantBillingV2NextcloudQuotaProvider
    : IBillingV2NextcloudQuotaProvider
{
    public static DormantBillingV2NextcloudQuotaProvider Instance { get; }
        = new();

    private DormantBillingV2NextcloudQuotaProvider()
    {
    }

    public BillingV2NextcloudQuotaReadiness CheckReadiness(
        IReadOnlyList<BillingV2NextcloudQuotaPlan> quotas)
        => quotas.Count == 0
            ? new BillingV2NextcloudQuotaReadiness(
                CanApplyQuotas: true,
                "BILLING_V2_NEXTCLOUD_QUOTA_NOOP")
            : new BillingV2NextcloudQuotaReadiness(
                CanApplyQuotas: false,
                "BILLING_V2_NEXTCLOUD_QUOTA_PROVIDER_NOT_CONFIGURED");
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
}

public sealed class BillingV2ProvisioningService : IBillingV2ProvisioningService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _billingV2;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IActiveDirectoryLinkRepository _activeDirectoryLinks;
    private readonly IProvisioningService _provisioningService;
    private readonly IBillingV2NextcloudQuotaProvider _nextcloudQuotaProvider;
    private readonly SubscriptionProvisioningRuntimeConfiguration
        _provisioningConfiguration;
    private readonly ILogger<BillingV2ProvisioningService> _logger;

    public BillingV2ProvisioningService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration billingV2,
        ISubscriptionRepository subscriptions,
        IActiveDirectoryLinkRepository activeDirectoryLinks,
        IProvisioningService provisioningService,
        IBillingV2NextcloudQuotaProvider nextcloudQuotaProvider,
        SubscriptionProvisioningRuntimeConfiguration provisioningConfiguration,
        ILogger<BillingV2ProvisioningService> logger)
    {
        _sql = sql;
        _billingV2 = billingV2;
        _subscriptions = subscriptions;
        _activeDirectoryLinks = activeDirectoryLinks;
        _provisioningService = provisioningService;
        _nextcloudQuotaProvider = nextcloudQuotaProvider;
        _provisioningConfiguration = provisioningConfiguration;
        _logger = logger;
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

        if (plan.NextcloudQuotas.Count > 0)
        {
            var nextcloudReadiness =
                _nextcloudQuotaProvider.CheckReadiness(plan.NextcloudQuotas);
            if (nextcloudReadiness.CanApplyQuotas)
            {
                _logger.LogWarning(
                    "Billing V2 Nextcloud quota provider unexpectedly reported ready for customer {CustomerId}, but quota execution is not wired in this release. Legacy provisioning remains authoritative.",
                    customerId);
                return null;
            }

            _logger.LogWarning(
                "Billing V2 provisioning gate denied for customer {CustomerId}: Nextcloud quota plans exist but no trusted runtime quota provider is configured ({ReasonCode}). Legacy provisioning remains authoritative.",
                customerId,
                nextcloudReadiness.ReasonCode);
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
        var resolution = await ResolveTargetsAsync(
            customerId,
            plan.Users,
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
        var targetUsers = await _activeDirectoryLinks.GetCustomerUserLinksAsync(
            customerId,
            cancellationToken);
        if (targetUsers.Count == 0)
        {
            _logger.LogWarning(
                "Billing V2 provisioning skipped for subscription {SubscriptionId}: no Active Directory user link is available. No external action was executed.",
                subscriptionId);
            return null;
        }

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

        if (plan.NextcloudQuotas.Count > 0)
        {
            var nextcloudReadiness =
                _nextcloudQuotaProvider.CheckReadiness(plan.NextcloudQuotas);
            _logger.LogWarning(
                "Billing V2 provisioning gate denied for subscription {SubscriptionId}: Nextcloud quota plans exist but no trusted runtime quota provider is configured ({ReasonCode}). No external action was executed.",
                subscriptionId,
                nextcloudReadiness.ReasonCode);
            return null;
        }

        if (plan.Users.Count == 0)
        {
            _logger.LogWarning(
                "Billing V2 provisioning skipped for subscription {SubscriptionId}: no user-scoped desired state is available. No external action was executed.",
                subscriptionId);
            return null;
        }

        var resolution = await ResolveTargetsAsync(
            customerId,
            plan.Users,
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

        return await ExecutePerUserAsync(
            BillingV2ProvisioningExecutionPlanner.BuildPerUserRequests(
                decision,
                resolution.Targets,
                groupDistinguishedNames),
            cancellationToken);
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
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string customerId
            && !string.IsNullOrWhiteSpace(customerId)
                ? customerId
                : null;
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
        var userOrder = new List<string>();
        var identityByUserId = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var groupsByUserId = new Dictionary<string, SortedSet<string>>(
            StringComparer.Ordinal);
        var quotasByUserId =
            new Dictionary<string, List<BillingV2NextcloudQuotaPlan>>(
                StringComparer.Ordinal);
        var sharedQuotas = new List<BillingV2NextcloudQuotaPlan>();
        var unresolved = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleType)
                || string.IsNullOrWhiteSpace(rule.TargetType))
            {
                unresolved.Add(CreateRuleReference(rule));
                continue;
            }

            if (string.Equals(
                    rule.ScopeType,
                    UserScope,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!TryRegisterUser(
                        rule,
                        userOrder,
                        identityByUserId,
                        groupsByUserId,
                        quotasByUserId,
                        out var subscriptionUserId,
                        out var identityReference))
                {
                    unresolved.Add(CreateRuleReference(rule));
                    continue;
                }

                if (IsAdGroupTarget(rule))
                {
                    if (string.IsNullOrWhiteSpace(rule.TargetReference))
                    {
                        unresolved.Add(CreateRuleReference(rule));
                        continue;
                    }

                    groupsByUserId[subscriptionUserId]
                        .Add(rule.TargetReference.Trim());
                    continue;
                }

                if (IsStorageQuotaTarget(rule))
                {
                    var value = ResolveValue(rule);
                    if (value is null)
                    {
                        unresolved.Add(CreateRuleReference(rule));
                        continue;
                    }

                    quotasByUserId[subscriptionUserId].Add(
                        new BillingV2NextcloudQuotaPlan(
                            rule.SubscriptionItemId,
                            subscriptionUserId,
                            rule.TargetType,
                            identityReference,
                            value.Value,
                            rule.TierUnit ?? "GiB"));
                    continue;
                }

                unresolved.Add(CreateRuleReference(rule));
                continue;
            }

            if (string.Equals(
                    rule.ScopeType,
                    SubscriptionScope,
                    StringComparison.OrdinalIgnoreCase))
            {
                // Un item de scope abonnement ne doit pas porter d'utilisateur :
                // l'incoherence de scope est une anomalie, pas un detail.
                if (!string.IsNullOrWhiteSpace(rule.SubscriptionUserId))
                {
                    unresolved.Add(CreateRuleReference(rule));
                    continue;
                }

                // Un droit AD achete au niveau abonnement n'a pas de titulaire
                // defini dans le modele actuel. Le distribuer a tous les
                // utilisateurs du client serait exactement la fuite corrigee
                // ici, donc il reste non resolu.
                if (IsAdGroupTarget(rule))
                {
                    unresolved.Add(CreateRuleReference(rule));
                    continue;
                }

                if (IsStorageQuotaTarget(rule))
                {
                    var value = ResolveValue(rule);
                    if (value is null)
                    {
                        unresolved.Add(CreateRuleReference(rule));
                        continue;
                    }

                    sharedQuotas.Add(new BillingV2NextcloudQuotaPlan(
                        rule.SubscriptionItemId,
                        SubscriptionUserId: null,
                        rule.TargetType,
                        IdentityReference: null,
                        value.Value,
                        rule.TierUnit ?? "GiB"));
                    continue;
                }

                unresolved.Add(CreateRuleReference(rule));
                continue;
            }

            unresolved.Add(CreateRuleReference(rule));
        }

        var users = userOrder
            .Select(subscriptionUserId => new BillingV2UserDesiredState(
                subscriptionUserId,
                identityByUserId[subscriptionUserId],
                groupsByUserId[subscriptionUserId].ToArray(),
                quotasByUserId[subscriptionUserId]))
            .ToArray();

        return new BillingV2ProvisioningPlan(
            users,
            new BillingV2SubscriptionDesiredState(sharedQuotas),
            unresolved.ToArray());
    }

    private static bool TryRegisterUser(
        BillingV2ProvisioningRuleProjection rule,
        List<string> userOrder,
        Dictionary<string, string> identityByUserId,
        Dictionary<string, SortedSet<string>> groupsByUserId,
        Dictionary<string, List<BillingV2NextcloudQuotaPlan>> quotasByUserId,
        out string subscriptionUserId,
        out string identityReference)
    {
        subscriptionUserId = string.Empty;
        identityReference = string.Empty;

        // Un item de scope utilisateur sans utilisateur n'a pas de titulaire :
        // il ne doit jamais retomber sur les utilisateurs du client.
        if (string.IsNullOrWhiteSpace(rule.SubscriptionUserId))
        {
            return false;
        }

        // Sans identite, il n'existe aucun compte a qui appliquer le droit.
        // C'est le cas de tout utilisateur supplementaire aujourd'hui.
        if (string.IsNullOrWhiteSpace(rule.IdentityReference))
        {
            return false;
        }

        if (!string.Equals(
                rule.SubscriptionUserStatus,
                "active",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        subscriptionUserId = rule.SubscriptionUserId.Trim();
        identityReference = rule.IdentityReference.Trim();

        if (identityByUserId.TryGetValue(subscriptionUserId, out var known))
        {
            if (!string.Equals(known, identityReference, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        identityByUserId[subscriptionUserId] = identityReference;
        groupsByUserId[subscriptionUserId] =
            new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        quotasByUserId[subscriptionUserId] =
            new List<BillingV2NextcloudQuotaPlan>();
        userOrder.Add(subscriptionUserId);
        return true;
    }

    private static bool IsAdGroupTarget(
        BillingV2ProvisioningRuleProjection rule)
        => string.Equals(
            rule.TargetType,
            "ad_group",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsStorageQuotaTarget(
        BillingV2ProvisioningRuleProjection rule)
        => rule.TargetType.StartsWith(
            "nextcloud_",
            StringComparison.OrdinalIgnoreCase);

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
