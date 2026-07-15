using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.Services;

public sealed record StoredDownloadCategory(
    string Id,
    string Slug,
    string Title,
    string? Description,
    string Status,
    int DisplayOrder,
    string CreatedAt,
    string UpdatedAt);

public sealed record StoredDownloadVisibilityRule(
    string Id,
    string ResourceId,
    string TargetType,
    string TargetValue);

public sealed record StoredDownloadResource(
    string Id,
    string CategoryId,
    string Title,
    string ShortDescription,
    string ResourceType,
    string SourceKind,
    string VisibilityMode,
    string Status,
    string? ExternalUrl,
    string? VersionLabel,
    string? InstallationInstructions,
    int DisplayOrder,
    StoredDownloadFileMetadata? InternalFile,
    string CreatedAt,
    string UpdatedAt);

public sealed record ValidatedDownloadCategory(
    string Id,
    string Slug,
    string Title,
    string? Description,
    string Status,
    int DisplayOrder);

public sealed record ValidatedDownloadVisibilityRule(
    string Id,
    string ResourceId,
    string TargetType,
    string TargetValue);

public sealed record ValidatedDownloadResource(
    string Id,
    string CategoryId,
    string Title,
    string ShortDescription,
    string ResourceType,
    string SourceKind,
    string VisibilityMode,
    string Status,
    string? ExternalUrl,
    string? VersionLabel,
    string? InstallationInstructions,
    int DisplayOrder,
    StoredDownloadFileMetadata? InternalFile,
    IReadOnlyList<ValidatedDownloadVisibilityRule> Rules);

public sealed record DownloadDeliveryResult(
    string SourceKind,
    string? ExternalUrl,
    DownloadFileReadResult? File);

public sealed class DownloadConflictException : Exception
{
    public DownloadConflictException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

internal sealed record DownloadCategorySeed(
    string Slug,
    string Title,
    string? Description,
    int DisplayOrder);

internal sealed record DownloadAccessScope(
    IReadOnlySet<string> PublicPackCodes,
    IReadOnlySet<string> OfferExternalReferences,
    IReadOnlySet<string> ServiceTypes,
    IReadOnlySet<string> ProvisioningGroups);

internal sealed record DownloadState(
    IReadOnlyList<StoredDownloadCategory> Categories,
    IReadOnlyList<StoredDownloadResource> Resources,
    IReadOnlyDictionary<string, StoredDownloadCategory> CategoriesById,
    IReadOnlyDictionary<string, IReadOnlyList<StoredDownloadVisibilityRule>>
        RulesByResourceId);

public interface IDownloadService
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<PortalDownloadCategory>> GetPortalDownloadsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken);

    Task<DownloadDeliveryResult> ResolvePortalDownloadAsync(
        PortalSessionContext session,
        string id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadCategory>> GetAdminCategoriesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadResource>> GetAdminDownloadsAsync(
        CancellationToken cancellationToken);

    Task<DownloadResource> GetAdminDownloadAsync(
        string id,
        CancellationToken cancellationToken);

    Task<DownloadCategoryMutationResponse> CreateCategoryAsync(
        DownloadCategoryPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadCategoryMutationResponse> UpdateCategoryAsync(
        string id,
        DownloadCategoryPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadCategoryMutationResponse> DeleteCategoryAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadResourceMutationResponse> CreateResourceAsync(
        DownloadResourcePayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadResourceMutationResponse> UpdateResourceAsync(
        string id,
        DownloadResourcePayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadResourceMutationResponse> DeleteResourceAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadResourceMutationResponse> UploadResourceFileAsync(
        string id,
        string originalName,
        string? contentType,
        Stream stream,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DownloadResourceMutationResponse> DeleteResourceFileAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class DownloadService : IDownloadService
{
    private const int MaxSlugLength = 80;
    private const int MaxCategoryTitleLength = 120;
    private const int MaxCategoryDescriptionLength = 280;
    private const int MaxResourceTitleLength = 140;
    private const int MaxShortDescriptionLength = 320;
    private const int MaxVersionLength = 80;
    private const int MaxInstructionsLength = 4000;
    private const int MaxExternalUrlLength = 2048;
    private const int MaxRuleValueLength = 160;

    private static readonly IReadOnlySet<string> KnownPublicPackCodes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "pack-dossier-securise",
            "pack-acces-distance",
            "pack-bureau-windows-distance",
            "pack-pro-association"
        };

    private static readonly IReadOnlySet<string> KnownServiceTypes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "personal_hosting",
            "storage",
            "backup",
            "vpn",
            "rds",
            "support",
            "cloud",
            "documentation",
            "monitoring",
            "user",
            "other"
        };

    private static readonly IReadOnlyList<DownloadCategorySeed> DefaultCategories =
    [
        new("logiciels", "Logiciels", null, 10),
        new("scripts", "Scripts", null, 20),
        new("fichiers-rdp", "Fichiers RDP", null, 30),
        new("documentation", "Documentation", null, 40),
        new("outils-complementaires", "Outils complémentaires", null, 50)
    ];

    private readonly IDownloadRepository _repository;
    private readonly IDownloadStorageService _storage;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IClientServiceCatalogService _serviceCatalogService;
    private readonly ICommercialOfferTopologyService _topologyService;
    private readonly IDownloadSchemaEnsurer _schemaEnsurer;
    private readonly ILogger<DownloadService> _logger;

    public DownloadService(
        IDownloadRepository repository,
        IDownloadStorageService storage,
        ISubscriptionRepository subscriptions,
        IClientServiceCatalogService serviceCatalogService,
        ICommercialOfferTopologyService topologyService,
        IDownloadSchemaEnsurer schemaEnsurer,
        ILogger<DownloadService> logger)
    {
        _repository = repository;
        _storage = storage;
        _subscriptions = subscriptions;
        _serviceCatalogService = serviceCatalogService;
        _topologyService = topologyService;
        _schemaEnsurer = schemaEnsurer;
        _logger = logger;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public async Task<IReadOnlyList<PortalDownloadCategory>> GetPortalDownloadsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        var accessScope = await BuildAccessScopeAsync(session, cancellationToken);
        var activeCategories = state.Categories
            .Where(category => category.Status == DownloadStatuses.Active)
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Title, StringComparer.Ordinal)
            .ToArray();

        var visibleResources = state.Resources
            .Where(resource =>
                resource.Status == DownloadStatuses.Active
                && state.CategoriesById.TryGetValue(
                    resource.CategoryId,
                    out var category)
                && category.Status == DownloadStatuses.Active
                && IsVisible(
                    resource,
                    state.RulesByResourceId.GetValueOrDefault(resource.Id)
                        ?? Array.Empty<StoredDownloadVisibilityRule>(),
                    accessScope))
            .GroupBy(resource => resource.CategoryId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(resource => resource.DisplayOrder)
                    .ThenBy(resource => resource.Title, StringComparer.Ordinal)
                    .Select(resource => ToPortalItem(resource))
                    .ToArray(),
                StringComparer.Ordinal);

        return activeCategories
            .Where(category =>
                visibleResources.TryGetValue(category.Id, out var items)
                && items.Length > 0)
            .Select(category => new PortalDownloadCategory(
                category.Id,
                category.Slug,
                category.Title,
                category.Description,
                visibleResources[category.Id]))
            .ToArray();
    }

    public async Task<DownloadDeliveryResult> ResolvePortalDownloadAsync(
        PortalSessionContext session,
        string id,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeIdentifier(id);
        var state = await LoadStateAsync(cancellationToken);
        var resource = state.Resources.FirstOrDefault(entry =>
            entry.Id == normalizedId)
            ?? throw new PortalDataNotFoundException();

        if (!state.CategoriesById.TryGetValue(resource.CategoryId, out var category)
            || category.Status != DownloadStatuses.Active
            || resource.Status != DownloadStatuses.Active)
        {
            throw new PortalDataNotFoundException();
        }

        var accessScope = await BuildAccessScopeAsync(session, cancellationToken);
        var rules = state.RulesByResourceId.GetValueOrDefault(resource.Id)
            ?? Array.Empty<StoredDownloadVisibilityRule>();
        if (!IsVisible(resource, rules, accessScope))
        {
            throw new PortalDataNotFoundException();
        }

        if (resource.SourceKind == DownloadSourceKinds.ExternalUrl)
        {
            var url = NormalizeOptionalAbsoluteUrl(resource.ExternalUrl);
            return url is null
                ? throw new PortalDataNotFoundException()
                : new DownloadDeliveryResult(
                    resource.SourceKind,
                    url,
                    null);
        }

        if (resource.InternalFile is null)
        {
            throw new PortalDataNotFoundException();
        }

        try
        {
            return new DownloadDeliveryResult(
                resource.SourceKind,
                null,
                await _storage.OpenReadAsync(
                    resource.InternalFile,
                    cancellationToken));
        }
        catch (FileNotFoundException)
        {
            throw new PortalDataNotFoundException();
        }
    }

    public async Task<IReadOnlyList<DownloadCategory>> GetAdminCategoriesAsync(
        CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        var resourceCounts = state.Resources
            .GroupBy(resource => resource.CategoryId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);

        return state.Categories
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Title, StringComparer.Ordinal)
            .Select(category => ToAdminCategory(
                category,
                resourceCounts.GetValueOrDefault(category.Id)))
            .ToArray();
    }

    public async Task<IReadOnlyList<DownloadResource>> GetAdminDownloadsAsync(
        CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);

        return state.Resources
            .OrderBy(resource =>
                state.CategoriesById.GetValueOrDefault(resource.CategoryId)
                    ?.DisplayOrder ?? int.MaxValue)
            .ThenBy(resource => resource.DisplayOrder)
            .ThenBy(resource => resource.Title, StringComparer.Ordinal)
            .Select(resource => ToAdminResource(
                resource,
                state.CategoriesById.GetValueOrDefault(resource.CategoryId),
                state.RulesByResourceId.GetValueOrDefault(resource.Id)
                    ?? Array.Empty<StoredDownloadVisibilityRule>()))
            .ToArray();
    }

    public async Task<DownloadResource> GetAdminDownloadAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeIdentifier(id);
        var state = await LoadStateAsync(cancellationToken);
        var resource = state.Resources.FirstOrDefault(entry =>
            entry.Id == normalizedId)
            ?? throw new PortalDataNotFoundException();

        return ToAdminResource(
            resource,
            state.CategoriesById.GetValueOrDefault(resource.CategoryId),
            state.RulesByResourceId.GetValueOrDefault(resource.Id)
                ?? Array.Empty<StoredDownloadVisibilityRule>());
    }

    public async Task<DownloadCategoryMutationResponse> CreateCategoryAsync(
        DownloadCategoryPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        var category = ValidateCategory(
            Guid.NewGuid().ToString("D"),
            payload,
            current: null,
            state.Categories);

        return await _repository.CreateCategoryAsync(
            category,
            correlationId,
            cancellationToken);
    }

    public async Task<DownloadCategoryMutationResponse> UpdateCategoryAsync(
        string id,
        DownloadCategoryPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeIdentifier(id);
        var state = await LoadStateAsync(cancellationToken);
        var current = state.Categories.FirstOrDefault(category =>
            category.Id == normalizedId)
            ?? throw new PortalDataNotFoundException();
        var validated = ValidateCategory(
            current.Id,
            payload,
            current,
            state.Categories);

        return await _repository.UpdateCategoryAsync(
            validated,
            correlationId,
            cancellationToken);
    }

    public async Task<DownloadCategoryMutationResponse> DeleteCategoryAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeIdentifier(id);
        var state = await LoadStateAsync(cancellationToken);
        if (!state.Categories.Any(category => category.Id == normalizedId))
        {
            throw new PortalDataNotFoundException();
        }

        if (state.Resources.Any(resource => resource.CategoryId == normalizedId))
        {
            throw new DownloadConflictException(
                "DOWNLOAD_CATEGORY_NOT_EMPTY",
                "Cette catégorie contient encore des téléchargements.");
        }

        return await _repository.DeleteCategoryAsync(
            normalizedId,
            correlationId,
            cancellationToken);
    }

    public async Task<DownloadResourceMutationResponse> CreateResourceAsync(
        DownloadResourcePayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        var resourceId = Guid.NewGuid().ToString("D");
        var validated = ValidateResource(
            resourceId,
            payload,
            current: null,
            currentRules: Array.Empty<StoredDownloadVisibilityRule>(),
            categories: state.Categories,
            currentFile: null);

        return await _repository.CreateResourceAsync(
            validated,
            correlationId,
            cancellationToken);
    }

    public async Task<DownloadResourceMutationResponse> UpdateResourceAsync(
        string id,
        DownloadResourcePayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeIdentifier(id);
        var state = await LoadStateAsync(cancellationToken);
        var current = state.Resources.FirstOrDefault(resource =>
            resource.Id == normalizedId)
            ?? throw new PortalDataNotFoundException();
        var currentRules = state.RulesByResourceId.GetValueOrDefault(normalizedId)
            ?? Array.Empty<StoredDownloadVisibilityRule>();
        var validated = ValidateResource(
            normalizedId,
            payload,
            current,
            currentRules,
            state.Categories,
            current.InternalFile);

        var previousFile = current.InternalFile;
        var result = await _repository.UpdateResourceAsync(
            validated,
            correlationId,
            cancellationToken);

        if (previousFile is not null
            && (validated.SourceKind != DownloadSourceKinds.InternalFile
                || validated.InternalFile?.StorageKey != previousFile.StorageKey))
        {
            await DeleteStoredFileSafelyAsync(previousFile, cancellationToken);
        }

        return result;
    }

    public async Task<DownloadResourceMutationResponse> DeleteResourceAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeIdentifier(id);
        var state = await LoadStateAsync(cancellationToken);
        var current = state.Resources.FirstOrDefault(resource =>
            resource.Id == normalizedId)
            ?? throw new PortalDataNotFoundException();

        var result = await _repository.DeleteResourceAsync(
            normalizedId,
            correlationId,
            cancellationToken);
        await DeleteStoredFileSafelyAsync(current.InternalFile, cancellationToken);
        return result;
    }

    public async Task<DownloadResourceMutationResponse> UploadResourceFileAsync(
        string id,
        string originalName,
        string? contentType,
        Stream stream,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeIdentifier(id);
        var state = await LoadStateAsync(cancellationToken);
        var current = state.Resources.FirstOrDefault(resource =>
            resource.Id == normalizedId)
            ?? throw new PortalDataNotFoundException();

        if (current.SourceKind != DownloadSourceKinds.InternalFile)
        {
            throw new PortalValidationException();
        }

        var currentRules = state.RulesByResourceId.GetValueOrDefault(normalizedId)
            ?? Array.Empty<StoredDownloadVisibilityRule>();
        var newFile = await _storage.SaveAsync(
            originalName,
            contentType,
            stream,
            cancellationToken);

        try
        {
            var validated = BuildValidatedResource(
                current,
                currentRules,
                state.Categories,
                newFile,
                statusOverride: null);
            var result = await _repository.UpdateResourceAsync(
                validated,
                correlationId,
                cancellationToken);
            await DeleteStoredFileSafelyAsync(current.InternalFile, cancellationToken);
            return result;
        }
        catch
        {
            await DeleteStoredFileSafelyAsync(newFile, cancellationToken);
            throw;
        }
    }

    public async Task<DownloadResourceMutationResponse> DeleteResourceFileAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeIdentifier(id);
        var state = await LoadStateAsync(cancellationToken);
        var current = state.Resources.FirstOrDefault(resource =>
            resource.Id == normalizedId)
            ?? throw new PortalDataNotFoundException();
        if (current.InternalFile is null)
        {
            return new DownloadResourceMutationResponse(
                current.Id,
                Changed: false,
                current.UpdatedAt,
                correlationId);
        }

        var currentRules = state.RulesByResourceId.GetValueOrDefault(normalizedId)
            ?? Array.Empty<StoredDownloadVisibilityRule>();
        var statusOverride = current.SourceKind == DownloadSourceKinds.InternalFile
            ? DownloadStatuses.Inactive
            : null;
        var validated = BuildValidatedResource(
            current,
            currentRules,
            state.Categories,
            fileOverride: null,
            statusOverride);
        var result = await _repository.UpdateResourceAsync(
            validated,
            correlationId,
            cancellationToken);
        await DeleteStoredFileSafelyAsync(current.InternalFile, cancellationToken);
        return result;
    }

    private async Task<DownloadState> LoadStateAsync(
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        await EnsureDefaultCategoriesAsync(cancellationToken);

        var categories = await _repository.GetCategoriesAsync(cancellationToken);
        var resources = await _repository.GetResourcesAsync(cancellationToken);
        var rules = await _repository.GetVisibilityRulesAsync(cancellationToken);

        return new DownloadState(
            categories,
            resources,
            categories.ToDictionary(category => category.Id, StringComparer.Ordinal),
            rules.GroupBy(rule => rule.ResourceId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<StoredDownloadVisibilityRule>)group
                        .OrderBy(rule => rule.TargetType, StringComparer.Ordinal)
                        .ThenBy(rule => rule.TargetValue, StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.Ordinal));
    }

    private async Task EnsureDefaultCategoriesAsync(
        CancellationToken cancellationToken)
    {
        var categories = await _repository.GetCategoriesAsync(cancellationToken);
        var knownSlugs = categories
            .Select(category => category.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = DefaultCategories
            .Where(category => !knownSlugs.Contains(category.Slug))
            .Select(category => new ValidatedDownloadCategory(
                Guid.NewGuid().ToString("D"),
                category.Slug,
                category.Title,
                category.Description,
                DownloadStatuses.Active,
                category.DisplayOrder))
            .ToArray();

        if (missing.Length > 0)
        {
            await _repository.SeedDefaultCategoriesAsync(
                missing,
                cancellationToken);
        }
    }

    private async Task<DownloadAccessScope> BuildAccessScopeAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        var subscriptions = await _subscriptions.GetByCustomerAsync(
            session.CustomerId,
            cancellationToken);
        var services = await _serviceCatalogService.GetServicesAsync(
            session,
            cancellationToken);

        var activeSubscriptions = subscriptions
            .Where(subscription =>
                subscription.Status == "active")
            .ToArray();
        var publicPackCodes = activeSubscriptions
            .Select(subscription => subscription.PublicPackCode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
        var offerExternalReferences = activeSubscriptions
            .Select(subscription => subscription.OfferExternalReference)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
        var serviceTypes = services
            .Where(service => service.Status == "active")
            .Select(service => service.Type)
            .ToHashSet(StringComparer.Ordinal);
        var provisioningGroups =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var subscription in activeSubscriptions)
        {
            foreach (var group in await _topologyService.ResolveMappedGroupsAsync(
                         subscription,
                         cancellationToken))
            {
                provisioningGroups.Add(group);
            }
        }

        return new DownloadAccessScope(
            publicPackCodes,
            offerExternalReferences,
            serviceTypes,
            provisioningGroups);
    }

    private static DownloadCategory ToAdminCategory(
        StoredDownloadCategory category,
        int resourceCount)
        => new(
            category.Id,
            category.Slug,
            category.Title,
            category.Description,
            category.Status,
            category.DisplayOrder,
            resourceCount,
            category.CreatedAt,
            category.UpdatedAt);

    private static DownloadResource ToAdminResource(
        StoredDownloadResource resource,
        StoredDownloadCategory? category,
        IReadOnlyList<StoredDownloadVisibilityRule> rules)
        => new(
            resource.Id,
            resource.CategoryId,
            category?.Title ?? "Catégorie introuvable",
            resource.Title,
            resource.ShortDescription,
            resource.ResourceType,
            resource.SourceKind,
            resource.VisibilityMode,
            resource.Status,
            resource.ExternalUrl,
            resource.VersionLabel,
            resource.InstallationInstructions,
            resource.DisplayOrder,
            resource.InternalFile is not null,
            resource.InternalFile?.OriginalName,
            resource.InternalFile?.ContentType,
            resource.InternalFile?.SizeBytes,
            resource.InternalFile?.Extension,
            resource.CreatedAt,
            resource.UpdatedAt,
            rules.Select(rule => new DownloadVisibilityRule(
                rule.Id,
                rule.ResourceId,
                rule.TargetType,
                rule.TargetValue))
                .ToArray());

    private static PortalDownloadItem ToPortalItem(StoredDownloadResource resource)
        => new(
            resource.Id,
            resource.Title,
            resource.ShortDescription,
            resource.ResourceType,
            resource.VersionLabel,
            resource.UpdatedAt,
            resource.InstallationInstructions);

    private static bool IsVisible(
        StoredDownloadResource resource,
        IReadOnlyList<StoredDownloadVisibilityRule> rules,
        DownloadAccessScope accessScope)
    {
        if (resource.VisibilityMode == DownloadVisibilityModes.AllClients)
        {
            return true;
        }

        if (resource.VisibilityMode != DownloadVisibilityModes.Targeted
            || rules.Count == 0)
        {
            return false;
        }

        return rules.Any(rule => rule.TargetType switch
        {
            var targetType when
                targetType == DownloadVisibilityTargetTypes.PublicPackCode =>
                    accessScope.PublicPackCodes.Contains(rule.TargetValue),
            var targetType when
                targetType == DownloadVisibilityTargetTypes
                    .OfferExternalReference =>
                    accessScope.OfferExternalReferences.Contains(
                        rule.TargetValue),
            var targetType when
                targetType == DownloadVisibilityTargetTypes.ServiceType =>
                    accessScope.ServiceTypes.Contains(rule.TargetValue),
            var targetType when
                targetType == DownloadVisibilityTargetTypes.ProvisioningGroup =>
                    accessScope.ProvisioningGroups.Contains(rule.TargetValue),
            _ => false
        });
    }

    private ValidatedDownloadCategory ValidateCategory(
        string id,
        DownloadCategoryPayload payload,
        StoredDownloadCategory? current,
        IReadOnlyList<StoredDownloadCategory> categories)
    {
        var normalizedId = NormalizeIdentifier(id);
        var slug = NormalizeSlug(payload.Slug ?? current?.Slug);
        var title = NormalizeRequiredText(
            payload.Title ?? current?.Title,
            minimumLength: 2,
            maximumLength: MaxCategoryTitleLength);
        var description = NormalizeOptionalText(
            payload.Description ?? current?.Description,
            MaxCategoryDescriptionLength);
        var status = NormalizeStatus(payload.Status ?? current?.Status);
        var displayOrder = NormalizeDisplayOrder(
            payload.DisplayOrder ?? current?.DisplayOrder);

        var conflicting = categories.FirstOrDefault(category =>
            category.Id != normalizedId
            && string.Equals(
                category.Slug,
                slug,
                StringComparison.OrdinalIgnoreCase));
        if (conflicting is not null)
        {
            throw new DownloadConflictException(
                "DOWNLOAD_CATEGORY_SLUG_EXISTS",
                "Une catégorie utilise déjà ce slug.");
        }

        return new ValidatedDownloadCategory(
            normalizedId,
            slug,
            title,
            description,
            status,
            displayOrder);
    }

    private ValidatedDownloadResource ValidateResource(
        string id,
        DownloadResourcePayload payload,
        StoredDownloadResource? current,
        IReadOnlyList<StoredDownloadVisibilityRule> currentRules,
        IReadOnlyList<StoredDownloadCategory> categories,
        StoredDownloadFileMetadata? currentFile)
    {
        var normalizedId = NormalizeIdentifier(id);
        var categoryId = NormalizeIdentifier(
            payload.CategoryId ?? current?.CategoryId);
        if (!categories.Any(category => category.Id == categoryId))
        {
            throw new PortalValidationException();
        }

        var title = NormalizeRequiredText(
            payload.Title ?? current?.Title,
            minimumLength: 2,
            maximumLength: MaxResourceTitleLength);
        var shortDescription = NormalizeRequiredText(
            payload.ShortDescription ?? current?.ShortDescription,
            minimumLength: 2,
            maximumLength: MaxShortDescriptionLength);
        var resourceType = NormalizeResourceType(
            payload.ResourceType ?? current?.ResourceType);
        var sourceKind = NormalizeSourceKind(
            payload.SourceKind ?? current?.SourceKind);
        var visibilityMode = NormalizeVisibilityMode(
            payload.VisibilityMode ?? current?.VisibilityMode);
        var status = NormalizeStatus(payload.Status ?? current?.Status);
        var versionLabel = NormalizeOptionalText(
            payload.VersionLabel ?? current?.VersionLabel,
            MaxVersionLength);
        var instructions = NormalizeOptionalText(
            payload.InstallationInstructions ?? current?.InstallationInstructions,
            MaxInstructionsLength);
        var displayOrder = NormalizeDisplayOrder(
            payload.DisplayOrder ?? current?.DisplayOrder);
        var externalUrl = sourceKind == DownloadSourceKinds.ExternalUrl
            ? NormalizeOptionalAbsoluteUrl(
                payload.ExternalUrl ?? current?.ExternalUrl,
                allowBlank: true)
            : null;
        var effectiveFile = sourceKind == DownloadSourceKinds.InternalFile
            ? currentFile
            : null;
        var rules = NormalizeRules(
            normalizedId,
            payload.VisibilityRules is null
                ? currentRules.Select(rule => new DownloadVisibilityRulePayload(
                    rule.TargetType,
                    rule.TargetValue))
                : payload.VisibilityRules,
            visibilityMode);

        return ValidateResourceInvariants(
            new ValidatedDownloadResource(
                normalizedId,
                categoryId,
                title,
                shortDescription,
                resourceType,
                sourceKind,
                visibilityMode,
                status,
                externalUrl,
                versionLabel,
                instructions,
                displayOrder,
                effectiveFile,
                rules));
    }

    private ValidatedDownloadResource BuildValidatedResource(
        StoredDownloadResource current,
        IReadOnlyList<StoredDownloadVisibilityRule> currentRules,
        IReadOnlyList<StoredDownloadCategory> categories,
        StoredDownloadFileMetadata? fileOverride,
        string? statusOverride)
    {
        return ValidateResource(
            current.Id,
            new DownloadResourcePayload(
                current.CategoryId,
                current.Title,
                current.ShortDescription,
                current.ResourceType,
                current.SourceKind,
                current.VisibilityMode,
                statusOverride ?? current.Status,
                current.ExternalUrl,
                current.VersionLabel,
                current.InstallationInstructions,
                current.DisplayOrder,
                currentRules.Select(rule => new DownloadVisibilityRulePayload(
                    rule.TargetType,
                    rule.TargetValue))
                    .ToArray()),
            current,
            currentRules,
            categories,
            fileOverride);
    }

    private static ValidatedDownloadResource ValidateResourceInvariants(
        ValidatedDownloadResource resource)
    {
        if (resource.Status == DownloadStatuses.Active)
        {
            if (resource.SourceKind == DownloadSourceKinds.InternalFile
                && resource.InternalFile is null)
            {
                throw new PortalValidationException();
            }

            if (resource.SourceKind == DownloadSourceKinds.ExternalUrl
                && string.IsNullOrWhiteSpace(resource.ExternalUrl))
            {
                throw new PortalValidationException();
            }

            if (resource.VisibilityMode == DownloadVisibilityModes.Targeted
                && resource.Rules.Count == 0)
            {
                throw new PortalValidationException();
            }
        }

        return resource;
    }

    private static IReadOnlyList<ValidatedDownloadVisibilityRule> NormalizeRules(
        string resourceId,
        IEnumerable<DownloadVisibilityRulePayload>? payloads,
        string visibilityMode)
    {
        if (payloads is null)
        {
            return Array.Empty<ValidatedDownloadVisibilityRule>();
        }

        var rules = new Dictionary<string, ValidatedDownloadVisibilityRule>(
            StringComparer.Ordinal);
        foreach (var payload in payloads)
        {
            var targetType = NormalizeRuleTargetType(payload.TargetType);
            var targetValue = NormalizeRuleTargetValue(targetType, payload.TargetValue);
            var dedupeKey = $"{targetType}:{targetValue}";
            rules[dedupeKey] = new ValidatedDownloadVisibilityRule(
                Guid.NewGuid().ToString("D"),
                resourceId,
                targetType,
                targetValue);
        }

        return visibilityMode == DownloadVisibilityModes.Targeted
            ? rules.Values
                .OrderBy(rule => rule.TargetType, StringComparer.Ordinal)
                .ThenBy(rule => rule.TargetValue, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<ValidatedDownloadVisibilityRule>();
    }

    private static string NormalizeIdentifier(string? value)
    {
        var normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized)
            && normalized.Length <= 100
            && normalized.All(character =>
                char.IsLetterOrDigit(character) || character == '-')
            ? normalized
            : throw new PortalValidationException();
    }

    private static string NormalizeSlug(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > MaxSlugLength)
        {
            throw new PortalValidationException();
        }

        var parts = normalized.Split(
            '-',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0
            || parts.Any(part =>
                !part.All(character => char.IsLetterOrDigit(character))))
        {
            throw new PortalValidationException();
        }

        return string.Join('-', parts);
    }

    private static string NormalizeRequiredText(
        string? value,
        int minimumLength,
        int maximumLength)
    {
        var normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized)
            && normalized.Length >= minimumLength
            && normalized.Length <= maximumLength
            ? normalized
            : throw new PortalValidationException();
    }

    private static string? NormalizeOptionalText(string? value, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maximumLength
            ? normalized
            : throw new PortalValidationException();
    }

    private static string NormalizeStatus(string? value)
    {
        var normalized = NormalizeRequiredText(value, 1, 30);
        return DownloadStatuses.IsKnown(normalized)
            ? normalized
            : throw new PortalValidationException();
    }

    private static string NormalizeResourceType(string? value)
    {
        var normalized = NormalizeRequiredText(value, 1, 30);
        return DownloadResourceTypes.IsKnown(normalized)
            ? normalized
            : throw new PortalValidationException();
    }

    private static string NormalizeSourceKind(string? value)
    {
        var normalized = NormalizeRequiredText(value, 1, 30);
        return DownloadSourceKinds.IsKnown(normalized)
            ? normalized
            : throw new PortalValidationException();
    }

    private static string NormalizeVisibilityMode(string? value)
    {
        var normalized = NormalizeRequiredText(value, 1, 30);
        return DownloadVisibilityModes.IsKnown(normalized)
            ? normalized
            : throw new PortalValidationException();
    }

    private static int NormalizeDisplayOrder(int? value)
        => value is >= 0 and <= 9999
            ? value.Value
            : throw new PortalValidationException();

    private static string? NormalizeOptionalAbsoluteUrl(
        string? value,
        bool allowBlank = false)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return allowBlank ? null : throw new PortalValidationException();
        }

        if (normalized.Length > MaxExternalUrlLength
            || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new PortalValidationException();
        }

        return uri.ToString();
    }

    private static string NormalizeRuleTargetType(string? value)
    {
        var normalized = NormalizeRequiredText(value, 1, 40);
        return DownloadVisibilityTargetTypes.IsKnown(normalized)
            ? normalized
            : throw new PortalValidationException();
    }

    private static string NormalizeRuleTargetValue(
        string targetType,
        string? value)
    {
        var normalized = NormalizeRequiredText(value, 1, MaxRuleValueLength);
        return targetType switch
        {
            var current when
                current == DownloadVisibilityTargetTypes.PublicPackCode =>
                    KnownPublicPackCodes.Contains(normalized)
                        ? normalized
                        : throw new PortalValidationException(),
            var current when
                current == DownloadVisibilityTargetTypes.ServiceType =>
                    KnownServiceTypes.Contains(normalized)
                        ? normalized
                        : throw new PortalValidationException(),
            _ => normalized
        };
    }

    private async Task DeleteStoredFileSafelyAsync(
        StoredDownloadFileMetadata? file,
        CancellationToken cancellationToken)
    {
        try
        {
            await _storage.DeleteAsync(file, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to delete stored download file {StorageKey}",
                file?.StorageKey);
        }
    }
}
