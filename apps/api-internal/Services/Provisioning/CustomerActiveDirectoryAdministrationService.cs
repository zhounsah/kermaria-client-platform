using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.ActiveDirectory;

namespace Kermaria.ApiInternal.Services.Provisioning;

public interface ICustomerActiveDirectoryAdministrationService
{
    Task<AdminCustomerAdWorkspace> GetWorkspaceAsync(
        string customerReference,
        string? subscriptionId,
        CancellationToken cancellationToken);

    Task<CustomerAdProvisioningMutationResponse> ApplyServiceActionAsync(
        string customerReference,
        string technicalServiceReference,
        CustomerAdProvisioningMutationRequest? request,
        string correlationId,
        string? requestedByUserId,
        CancellationToken cancellationToken);

    Task<CustomerAdProvisioningMutationResponse> ApplyGroupActionAsync(
        string customerReference,
        string groupSamAccountName,
        CustomerAdProvisioningMutationRequest? request,
        string correlationId,
        string? requestedByUserId,
        CancellationToken cancellationToken);
}

public sealed class CustomerActiveDirectoryAdministrationService
    : ICustomerActiveDirectoryAdministrationService
{
    private readonly IActiveDirectoryLinkRepository _links;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ISubscriptionProvisioningManager _provisioningManager;
    private readonly IActiveDirectoryService _activeDirectory;
    private readonly ICommercialOfferTopologyService _topologyService;
    private readonly IAdGroupProvisioner _groupProvisioner;
    private readonly SubscriptionProvisioningRuntimeConfiguration _configuration;
    private readonly ILogger<CustomerActiveDirectoryAdministrationService> _logger;

    public CustomerActiveDirectoryAdministrationService(
        IActiveDirectoryLinkRepository links,
        ISubscriptionRepository subscriptions,
        ISubscriptionProvisioningManager provisioningManager,
        IActiveDirectoryService activeDirectory,
        ICommercialOfferTopologyService topologyService,
        IAdGroupProvisioner groupProvisioner,
        SubscriptionProvisioningRuntimeConfiguration configuration,
        ILogger<CustomerActiveDirectoryAdministrationService> logger)
    {
        _links = links;
        _subscriptions = subscriptions;
        _provisioningManager = provisioningManager;
        _activeDirectory = activeDirectory;
        _topologyService = topologyService;
        _groupProvisioner = groupProvisioner;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AdminCustomerAdWorkspace> GetWorkspaceAsync(
        string customerReference,
        string? subscriptionId,
        CancellationToken cancellationToken)
    {
        var context = await BuildContextAsync(
            customerReference,
            subscriptionId,
            cancellationToken);
        return BuildWorkspace(context);
    }

    public async Task<CustomerAdProvisioningMutationResponse> ApplyServiceActionAsync(
        string customerReference,
        string technicalServiceReference,
        CustomerAdProvisioningMutationRequest? request,
        string correlationId,
        string? requestedByUserId,
        CancellationToken cancellationToken)
    {
        var normalizedServiceReference = NormalizeTechnicalServiceReference(
            technicalServiceReference);
        var context = await BuildContextAsync(
            customerReference,
            request?.SubscriptionId,
            cancellationToken);
        var serviceDefinition = context.TechnicalServices.FirstOrDefault(service =>
            string.Equals(
                service.TechnicalServiceReference,
                normalizedServiceReference,
                StringComparison.OrdinalIgnoreCase));
        if (serviceDefinition is null || serviceDefinition.GroupSamAccountNames.Count == 0)
        {
            return await BuildFailureResponseAsync(
                context,
                correlationId,
                "PROVISIONING_SERVICE_NOT_CONFIGURED",
                $"Le service {normalizedServiceReference} n'a aucun groupe AD mappé.",
                cancellationToken);
        }

        return await ApplyGroupSetActionAsync(
            context,
            request,
            correlationId,
            requestedByUserId,
            serviceDefinition.GroupSamAccountNames,
            serviceDefinition.Label,
            normalizedServiceReference,
            "service",
            cancellationToken);
    }

    public async Task<CustomerAdProvisioningMutationResponse> ApplyGroupActionAsync(
        string customerReference,
        string groupSamAccountName,
        CustomerAdProvisioningMutationRequest? request,
        string correlationId,
        string? requestedByUserId,
        CancellationToken cancellationToken)
    {
        var normalizedGroupSamAccountName = NormalizeSamAccountName(
            groupSamAccountName);
        var context = await BuildContextAsync(
            customerReference,
            request?.SubscriptionId,
            cancellationToken);

        return await ApplyGroupSetActionAsync(
            context,
            request,
            correlationId,
            requestedByUserId,
            new[] { normalizedGroupSamAccountName },
            normalizedGroupSamAccountName,
            normalizedGroupSamAccountName,
            "group",
            cancellationToken);
    }

    private async Task<CustomerAdProvisioningMutationResponse> ApplyGroupSetActionAsync(
        CustomerAdWorkspaceContext context,
        CustomerAdProvisioningMutationRequest? request,
        string correlationId,
        string? requestedByUserId,
        IReadOnlyList<string> groupSamAccountNames,
        string label,
        string targetReference,
        string targetKind,
        CancellationToken cancellationToken)
    {
        var operation = NormalizeOperation(request?.Operation);
        var targetUsers = ResolveTargetUsers(
            context.UserLinks,
            request?.TargetUserSamAccountNames);
        if (!targetUsers.Succeeded)
        {
            return await BuildFailureResponseAsync(
                context,
                correlationId,
                targetUsers.Code!,
                targetUsers.Message!,
                cancellationToken);
        }

        var coverageSatisfied = groupSamAccountNames.All(group =>
            context.CoveredGroups.Contains(group));
        var diagnostics = BuildBlockingDiagnostics(
            context,
            groupSamAccountNames,
            targetUsers.Users!);
        if (diagnostics.Count > 0)
        {
            return await BuildFailureResponseAsync(
                context,
                correlationId,
                diagnostics[0].Code,
                diagnostics[0].Message,
                cancellationToken);
        }

        var isOverride = request?.IsOverride ?? false;
        if (!coverageSatisfied && !isOverride)
        {
            var noun = targetKind == "service"
                ? $"Le service {label}"
                : $"Le groupe de sécurité {label}";
            return await BuildFailureResponseAsync(
                context,
                correlationId,
                "PROVISIONING_OVERRIDE_REQUIRED",
                $"{noun} n'est pas couvert par une souscription active ou déjà payée. Utilisez l'override administrateur pour forcer l'action.",
                cancellationToken);
        }

        var execution = await ExecuteManualActionAsync(
            targetUsers.Users!,
            groupSamAccountNames,
            operation,
            cancellationToken);
        if (!execution.Succeeded)
        {
            return await BuildFailureResponseAsync(
                context,
                correlationId,
                execution.Code,
                execution.Message,
                cancellationToken);
        }

        _logger.LogInformation(
            "Customer AD {TargetKind} {TargetReference} {Operation} completed for customer {CustomerReference} users={UserCount} override={IsOverride} changed={Changed} actor={ActorUserId}",
            targetKind,
            targetReference,
            operation,
            context.Customer.CustomerReference,
            targetUsers.Users!.Count,
            isOverride,
            execution.Changed,
            requestedByUserId);

        var refreshedWorkspace = await GetWorkspaceAsync(
            context.Customer.CustomerReference,
            request?.SubscriptionId,
            cancellationToken);
        var actionLabel = targetKind == "service"
            ? $"Service {label}"
            : $"Groupe de sécurité {label}";
        var verb = operation == "activate" ? "activé" : "retiré";
        var suffix = isOverride ? " avec override administrateur" : string.Empty;
        var userCount = targetUsers.Users!.Count;

        return new CustomerAdProvisioningMutationResponse(
            targetKind == "service"
                ? operation == "activate"
                    ? "CUSTOMER_AD_SERVICE_ACTIVATED"
                    : "CUSTOMER_AD_SERVICE_REMOVED"
                : operation == "activate"
                    ? "CUSTOMER_AD_GROUP_ACTIVATED"
                    : "CUSTOMER_AD_GROUP_REMOVED",
            $"{actionLabel} {verb} pour {userCount} utilisateur(s){suffix}.",
            execution.Changed,
            correlationId,
            refreshedWorkspace);
    }

    private async Task<CustomerAdProvisioningMutationResponse> BuildFailureResponseAsync(
        CustomerAdWorkspaceContext context,
        string correlationId,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        var refreshedWorkspace = await GetWorkspaceAsync(
            context.Customer.CustomerReference,
            context.SubscriptionContext?.Id,
            cancellationToken);
        return new CustomerAdProvisioningMutationResponse(
            code,
            message,
            false,
            correlationId,
            refreshedWorkspace);
    }

    private async Task<CustomerAdWorkspaceContext> BuildContextAsync(
        string customerReference,
        string? subscriptionId,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerReference = NormalizeCustomerReference(
            customerReference);
        var customer = await _links.GetCustomerContextAsync(
                normalizedCustomerReference,
                cancellationToken)
            ?? throw new PortalDataNotFoundException();
        var adStatus = await _activeDirectory.GetStatusAsync(cancellationToken);
        var links = await _links.GetCustomerLinksAsync(
            customer.CustomerReference,
            cancellationToken);
        var userLinks = await _links.GetCustomerUserLinksAsync(
            customer.CustomerId,
            cancellationToken);
        var subscriptions = await _subscriptions.GetByCustomerAsync(
            customer.CustomerId,
            cancellationToken);

        SubscriptionSummary? scopedSubscription = null;
        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            scopedSubscription = subscriptions.FirstOrDefault(subscription =>
                subscription.Id.Equals(
                    subscriptionId.Trim(),
                    StringComparison.Ordinal))
                ?? throw new PortalDataNotFoundException();
        }

        var provisioningSummaries = new Dictionary<string, SubscriptionProvisioningSummary>(
            StringComparer.Ordinal);
        var mappedGroupsBySubscriptionId =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var subscription in subscriptions)
        {
            mappedGroupsBySubscriptionId[subscription.Id] =
                await _topologyService.ResolveMappedGroupsAsync(
                    subscription,
                    cancellationToken);
            provisioningSummaries[subscription.Id] =
                await _provisioningManager.GetSummaryAsync(
                    subscription,
                    cancellationToken);
        }

        var technicalServices = await _topologyService.GetTechnicalServicesAsync(
            cancellationToken);

        var effectiveGroupDiagnostics = new List<AdProvisioningDiagnostic>();
        var effectiveGroupsByUserSam =
            await LoadEffectiveGroupsByUserSamAsync(
                customer.CustomerReference,
                userLinks,
                adStatus,
                effectiveGroupDiagnostics,
                cancellationToken);

        var displaySubscriptions = scopedSubscription is null
            ? subscriptions
            : new[] { scopedSubscription };
        var coveredGroups = subscriptions
            .Where(subscription => IsManualCoverageStatus(subscription.Status))
            .SelectMany(subscription => mappedGroupsBySubscriptionId[subscription.Id])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var displayGroups = displaySubscriptions
            .SelectMany(subscription => mappedGroupsBySubscriptionId[subscription.Id])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CustomerAdWorkspaceContext(
            customer,
            adStatus,
            links,
            userLinks,
            subscriptions,
            displaySubscriptions,
            provisioningSummaries,
            effectiveGroupsByUserSam,
            effectiveGroupDiagnostics,
            mappedGroupsBySubscriptionId,
            technicalServices,
            coveredGroups,
            displayGroups,
            scopedSubscription is null
                ? null
                : BuildSubscriptionContext(
                    scopedSubscription,
                    mappedGroupsBySubscriptionId,
                    technicalServices));
    }

    private AdminCustomerAdWorkspace BuildWorkspace(
        CustomerAdWorkspaceContext context)
    {
        var subscriptionContexts = context.Subscriptions
            .Select(subscription => BuildSubscriptionContext(
                subscription,
                context.MappedGroupsBySubscriptionId,
                context.TechnicalServices))
            .OrderByDescending(subscription => subscription.Status, StringComparer.Ordinal)
            .ThenBy(subscription => subscription.OfferName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var serviceDefinitions = context.TechnicalServices
            .Where(service => service.GroupSamAccountNames.Count > 0
                && service.GroupSamAccountNames.All(group => context.DisplayGroups.Contains(
                    group,
                    StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        var services = serviceDefinitions
            .Select(service => BuildServiceSummary(context, service))
            .OrderBy(service => service.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var groups = context.DisplayGroups
            .Select(groupSamAccountName => BuildGroupSummary(context, groupSamAccountName))
            .OrderBy(group => group.GroupSamAccountName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var diagnostics = context.EffectiveGroupDiagnostics
            .Concat(services.SelectMany(service => service.Diagnostics))
            .Concat(groups.SelectMany(group => group.Diagnostics))
            .GroupBy(CreateDiagnosticKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ToArray();

        return new AdminCustomerAdWorkspace(
            context.Customer.CustomerReference,
            context.Customer.DisplayName,
            context.AdStatus,
            context.Links,
            context.UserLinks
                .Select(user => new SubscriptionProvisioningTargetUserSummary(
                    user.SamAccountName,
                    user.DisplayName,
                    user.UserPrincipalName))
                .ToArray(),
            context.SubscriptionContext,
            subscriptionContexts,
            context.DisplayGroups,
            ResolveProvisioningStatus(context),
            ResolveLastResultCode(context),
            services,
            groups,
            diagnostics);
    }

    private static AdminCustomerAdSubscriptionContext BuildSubscriptionContext(
        SubscriptionSummary subscription,
        IReadOnlyDictionary<string, IReadOnlyList<string>> mappedGroupsBySubscriptionId,
        IReadOnlyList<CatalogTechnicalServiceDefinition> technicalServices)
    {
        var mappedGroups = mappedGroupsBySubscriptionId[subscription.Id];
        var coveredServices = technicalServices
            .Where(service => service.GroupSamAccountNames.Count > 0
                && service.GroupSamAccountNames.All(group => mappedGroups.Contains(
                    group,
                    StringComparer.OrdinalIgnoreCase)))
            .Select(service => service.TechnicalServiceReference)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AdminCustomerAdSubscriptionContext(
            subscription.Id,
            subscription.OfferName,
            subscription.OfferExternalReference,
            subscription.PublicPackCode,
            subscription.Status,
            mappedGroups,
            coveredServices);
    }

    private ProvisionableServiceSummary BuildServiceSummary(
        CustomerAdWorkspaceContext context,
        CatalogTechnicalServiceDefinition serviceDefinition)
    {
        var technicalServiceReference = serviceDefinition.TechnicalServiceReference;
        var groups = serviceDefinition.GroupSamAccountNames;
        var diagnostics = BuildBlockingDiagnostics(
            context,
            groups,
            context.UserLinks);
        var status = ResolveCurrentStatus(
            context,
            groups,
            diagnostics.Count > 0);
        var subscriptionIds = context.Subscriptions
            .Where(subscription =>
            {
                var mappedGroups = context.MappedGroupsBySubscriptionId[subscription.Id];
                return groups.All(group => mappedGroups.Contains(
                    group,
                    StringComparer.OrdinalIgnoreCase));
            })
            .Select(subscription => subscription.Id)
            .ToArray();
        var coveredSubscriptionIds = context.Subscriptions
            .Where(subscription =>
                IsManualCoverageStatus(subscription.Status)
                && groups.All(group => context.MappedGroupsBySubscriptionId[
                    subscription.Id].Contains(
                        group,
                        StringComparer.OrdinalIgnoreCase)))
            .Select(subscription => subscription.Id)
            .ToArray();
        var isCovered = groups.Count > 0
            && groups.All(group => context.CoveredGroups.Contains(group));

        return new ProvisionableServiceSummary(
            technicalServiceReference,
            serviceDefinition.Label,
            groups,
            subscriptionIds,
            coveredSubscriptionIds,
            isCovered,
            isCovered && diagnostics.Count == 0,
            !isCovered,
            status,
            diagnostics);
    }

    private ProvisionableGroupSummary BuildGroupSummary(
        CustomerAdWorkspaceContext context,
        string groupSamAccountName)
    {
        var diagnostics = BuildBlockingDiagnostics(
            context,
            new[] { groupSamAccountName },
            context.UserLinks);
        var relatedServices = context.TechnicalServices
            .Where(service => service.GroupSamAccountNames.Contains(
                groupSamAccountName,
                StringComparer.OrdinalIgnoreCase))
            .Select(service => service.TechnicalServiceReference)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var subscriptionIds = context.Subscriptions
            .Where(subscription => context.MappedGroupsBySubscriptionId[
                subscription.Id].Contains(
                    groupSamAccountName,
                    StringComparer.OrdinalIgnoreCase))
            .Select(subscription => subscription.Id)
            .ToArray();
        var coveredSubscriptionIds = context.Subscriptions
            .Where(subscription =>
                IsManualCoverageStatus(subscription.Status)
                && context.MappedGroupsBySubscriptionId[subscription.Id].Contains(
                        groupSamAccountName,
                        StringComparer.OrdinalIgnoreCase))
            .Select(subscription => subscription.Id)
            .ToArray();
        var isCovered = context.CoveredGroups.Contains(groupSamAccountName);

        return new ProvisionableGroupSummary(
            groupSamAccountName,
            groupSamAccountName,
            relatedServices,
            subscriptionIds,
            coveredSubscriptionIds,
            isCovered,
            isCovered && diagnostics.Count == 0,
            !isCovered,
            ResolveCurrentStatus(
                context,
                new[] { groupSamAccountName },
                diagnostics.Count > 0),
            diagnostics);
    }

    private string ResolveCurrentStatus(
        CustomerAdWorkspaceContext context,
        IReadOnlyList<string> groupSamAccountNames,
        bool blocked)
    {
        if (blocked)
        {
            return "blocked";
        }

        if (context.UserLinks.Count == 0 || groupSamAccountNames.Count == 0)
        {
            return "blocked";
        }

        var fullyProvisionedUserCount = context.UserLinks.Count(user =>
            context.EffectiveGroupsByUserSam.TryGetValue(
                user.SamAccountName,
                out var groups)
            && groupSamAccountNames.All(groups.Contains));
        if (fullyProvisionedUserCount == context.UserLinks.Count)
        {
            return "active";
        }

        if (fullyProvisionedUserCount == 0)
        {
            return "inactive";
        }

        return "partial";
    }

    private List<AdProvisioningDiagnostic> BuildBlockingDiagnostics(
        CustomerAdWorkspaceContext context,
        IReadOnlyList<string> groupSamAccountNames,
        IReadOnlyList<CustomerAdLinkSummary> targetUsers)
    {
        var diagnostics = new List<AdProvisioningDiagnostic>();

        if (!context.AdStatus.WritesEnabled)
        {
            diagnostics.Add(new AdProvisioningDiagnostic(
                "AD_READ_ONLY",
                "Les écritures Active Directory sont désactivées par la configuration actuelle.",
                "none",
                context.AdStatus.AllowedRoots,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
            return diagnostics;
        }

        if (targetUsers.Count == 0)
        {
            diagnostics.Add(new AdProvisioningDiagnostic(
                "PROVISIONING_NO_TARGET_USERS",
                "Aucun utilisateur lié n'est disponible pour exécuter cette action.",
                "none",
                context.AdStatus.AllowedRoots,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
            return diagnostics;
        }

        var missingGroupMappings = new List<string>();
        var outOfScopeGroupDns = new List<string>();
        foreach (var groupSamAccountName in groupSamAccountNames)
        {
            if (!_configuration.TryGetGroupDistinguishedName(
                    groupSamAccountName,
                    out var distinguishedName))
            {
                if (_groupProvisioner.RequiresConfiguredGroupDistinguishedNames)
                {
                    missingGroupMappings.Add(groupSamAccountName);
                }

                continue;
            }

            if (!context.AdStatus.AllowedRoots.Any()
                || !context.AdStatus.AllowedRoots.Any(allowedRoot =>
                    distinguishedName.Equals(
                        allowedRoot,
                        StringComparison.OrdinalIgnoreCase)
                    || distinguishedName.EndsWith(
                        $",{allowedRoot}",
                        StringComparison.OrdinalIgnoreCase)))
            {
                outOfScopeGroupDns.Add(distinguishedName);
            }
        }

        if (missingGroupMappings.Count > 0)
        {
            diagnostics.Add(new AdProvisioningDiagnostic(
                "PROVISIONING_GROUP_NOT_CONFIGURED",
                $"Le ou les groupes de sécurité suivants n'ont pas de DN configuré : {string.Join(", ", missingGroupMappings)}.",
                "group",
                context.AdStatus.AllowedRoots,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
        }

        var outOfScopeUserDns = targetUsers
            .Where(user => !context.AdStatus.AllowedRoots.Any()
                || !context.AdStatus.AllowedRoots.Any(allowedRoot =>
                    user.DistinguishedName.Equals(
                        allowedRoot,
                        StringComparison.OrdinalIgnoreCase)
                    || user.DistinguishedName.EndsWith(
                        $",{allowedRoot}",
                        StringComparison.OrdinalIgnoreCase)))
            .Select(user => user.DistinguishedName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (outOfScopeUserDns.Length > 0 || outOfScopeGroupDns.Count > 0)
        {
            var targetType = outOfScopeUserDns.Length > 0 && outOfScopeGroupDns.Count > 0
                ? "user_and_group"
                : outOfScopeUserDns.Length > 0
                    ? "user"
                    : "group";
            var message = targetType switch
            {
                "user_and_group" =>
                    "Le provisionning est bloqué : au moins un utilisateur lié et un groupe cible sont hors du périmètre AD autorisé.",
                "user" =>
                    "Le provisionning est bloqué : au moins un utilisateur lié est hors du périmètre AD autorisé.",
                _ =>
                    "Le provisionning est bloqué : au moins un groupe cible est hors du périmètre AD autorisé."
            };

            diagnostics.Add(new AdProvisioningDiagnostic(
                "AD_TARGET_OUTSIDE_ALLOWED_ROOTS",
                message,
                targetType,
                context.AdStatus.AllowedRoots,
                outOfScopeUserDns,
                outOfScopeGroupDns,
                targetUsers
                    .Where(user => outOfScopeUserDns.Contains(
                        user.DistinguishedName,
                        StringComparer.OrdinalIgnoreCase))
                    .Select(user => user.SamAccountName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()));
        }

        diagnostics.AddRange(context.EffectiveGroupDiagnostics);
        return diagnostics;
    }

    private async Task<Dictionary<string, HashSet<string>>> LoadEffectiveGroupsByUserSamAsync(
        string customerReference,
        IReadOnlyList<CustomerAdLinkSummary> userLinks,
        AdStatusResponse adStatus,
        List<AdProvisioningDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var groupsByUserSam =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (userLinks.Count == 0)
        {
            return groupsByUserSam;
        }

        if (!adStatus.ReadsEnabled)
        {
            diagnostics.Add(new AdProvisioningDiagnostic(
                "AD_READS_UNAVAILABLE",
                "Les lectures Active Directory sont désactivées : l'état détaillé des services ne peut pas être calculé.",
                "none",
                adStatus.AllowedRoots,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
            return groupsByUserSam;
        }

        foreach (var user in userLinks)
        {
            var result = await _activeDirectory.GetUserEffectiveGroupsAsync(
                customerReference,
                user.SamAccountName,
                cancellationToken);
            if (result.StatusCode >= 400 || result.Value is null)
            {
                diagnostics.Add(new AdProvisioningDiagnostic(
                    result.Code,
                    $"Impossible de lire les groupes effectifs de {user.SamAccountName} : {result.Message}",
                    "user",
                    adStatus.AllowedRoots,
                    new[] { user.DistinguishedName },
                    Array.Empty<string>(),
                    new[] { user.SamAccountName }));
                continue;
            }

            groupsByUserSam[user.SamAccountName] = result.Value
                .Select(group => group.SamAccountName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return groupsByUserSam;
    }

    private async Task<ManualGroupExecutionResult> ExecuteManualActionAsync(
        IReadOnlyList<CustomerAdLinkSummary> targetUsers,
        IReadOnlyList<string> groupSamAccountNames,
        string operation,
        CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var user in targetUsers)
        {
            foreach (var groupSamAccountName in groupSamAccountNames)
            {
                _configuration.TryGetGroupDistinguishedName(
                    groupSamAccountName,
                    out var distinguishedName);
                var result = operation == "activate"
                    ? await _groupProvisioner.AddUserToGroupAsync(
                        user,
                        groupSamAccountName,
                        distinguishedName,
                        cancellationToken)
                    : await _groupProvisioner.RemoveUserFromGroupAsync(
                        user,
                        groupSamAccountName,
                        distinguishedName,
                        cancellationToken);
                changed |= result.Changed;
                if (result.StatusCode >= 400)
                {
                    return new ManualGroupExecutionResult(
                        false,
                        changed,
                        result.Code,
                        result.Message);
                }
            }
        }

        return new ManualGroupExecutionResult(
            true,
            changed,
            operation == "activate"
                ? "AD_GROUP_MEMBER_ADDED"
                : "AD_GROUP_MEMBER_REMOVED",
            operation == "activate"
                ? "Activation manuelle effectuée."
                : "Retrait manuel effectué.");
    }

    private TargetUserResolution ResolveTargetUsers(
        IReadOnlyList<CustomerAdLinkSummary> userLinks,
        IReadOnlyList<string>? requestedSamAccountNames)
    {
        if (requestedSamAccountNames is null || requestedSamAccountNames.Count == 0)
        {
            return new TargetUserResolution(
                true,
                userLinks,
                null,
                null);
        }

        var requestedUsers = requestedSamAccountNames
            .Select(NormalizeSamAccountName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var matchedUsers = userLinks
            .Where(user => requestedUsers.Contains(
                user.SamAccountName,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (matchedUsers.Length != requestedUsers.Length)
        {
            return new TargetUserResolution(
                false,
                null,
                "INVALID_REQUEST",
                "La sélection d'utilisateurs AD contient au moins un compte non lié à ce client.");
        }

        return new TargetUserResolution(
            true,
            matchedUsers,
            null,
            null);
    }

    private static string ResolveProvisioningStatus(
        CustomerAdWorkspaceContext context)
    {
        var statuses = context.DisplaySubscriptions
            .Select(subscription => context.ProvisioningSummaries[subscription.Id].Status)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return statuses.Length switch
        {
            0 => "not_required",
            1 => statuses[0],
            _ => "mixed"
        };
    }

    private static string? ResolveLastResultCode(CustomerAdWorkspaceContext context)
    {
        return context.DisplaySubscriptions
            .Select(subscription => context.ProvisioningSummaries[subscription.Id])
            .OrderByDescending(
                summary => summary.RecentActions.FirstOrDefault()?.RequestedAt,
                StringComparer.Ordinal)
            .Select(summary =>
                !string.IsNullOrWhiteSpace(summary.LastResultCode)
                    ? summary.LastResultCode
                    : summary.RecentActions
                        .Select(action => action.ResultCode)
                        .FirstOrDefault(resultCode =>
                            !string.IsNullOrWhiteSpace(resultCode)))
            .FirstOrDefault(resultCode => !string.IsNullOrWhiteSpace(resultCode));
    }

    private static string CreateDiagnosticKey(AdProvisioningDiagnostic diagnostic)
    {
        return string.Join(
            "|",
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.TargetType,
            string.Join(",", diagnostic.AffectedUserDistinguishedNames),
            string.Join(",", diagnostic.AffectedGroupDistinguishedNames));
    }

    private static bool IsManualCoverageStatus(string status)
        => status is "active" or "pending_activation" or "pending_cancellation";

    private static string NormalizeCustomerReference(string customerReference)
        => ActiveDirectoryInputValidator.NormalizeCustomerReference(
            customerReference)
            ?? throw new PortalValidationException();

    private static string NormalizeSamAccountName(string samAccountName)
        => ActiveDirectoryInputValidator.NormalizeSamAccountName(
            samAccountName)
            ?? throw new PortalValidationException();

    private static string NormalizeTechnicalServiceReference(
        string technicalServiceReference)
    {
        var normalized = technicalServiceReference.Trim();
        return normalized.Length > 0
               && normalized.Length <= 100
               && normalized.All(character =>
                   char.IsLetterOrDigit(character)
                   || character is '-' or '_' or '.')
            ? normalized
            : throw new PortalValidationException();
    }

    private static string NormalizeOperation(string? operation)
    {
        return operation?.Trim().ToLowerInvariant() switch
        {
            "activate" => "activate",
            "remove" => "remove",
            _ => throw new PortalValidationException()
        };
    }
}

internal sealed record CustomerAdWorkspaceContext(
    AdCustomerContext Customer,
    AdStatusResponse AdStatus,
    IReadOnlyList<CustomerAdLinkSummary> Links,
    IReadOnlyList<CustomerAdLinkSummary> UserLinks,
    IReadOnlyList<SubscriptionSummary> Subscriptions,
    IReadOnlyList<SubscriptionSummary> DisplaySubscriptions,
    IReadOnlyDictionary<string, SubscriptionProvisioningSummary> ProvisioningSummaries,
    IReadOnlyDictionary<string, HashSet<string>> EffectiveGroupsByUserSam,
    IReadOnlyList<AdProvisioningDiagnostic> EffectiveGroupDiagnostics,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MappedGroupsBySubscriptionId,
    IReadOnlyList<CatalogTechnicalServiceDefinition> TechnicalServices,
    IReadOnlySet<string> CoveredGroups,
    IReadOnlyList<string> DisplayGroups,
    AdminCustomerAdSubscriptionContext? SubscriptionContext);

internal sealed record TargetUserResolution(
    bool Succeeded,
    IReadOnlyList<CustomerAdLinkSummary>? Users,
    string? Code,
    string? Message);

internal sealed record ManualGroupExecutionResult(
    bool Succeeded,
    bool Changed,
    string Code,
    string Message);
