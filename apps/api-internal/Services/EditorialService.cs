using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public interface IEditorialService
{
    bool IsPersistent { get; }

    Task EnsurePermissionAsync(
        PortalSessionContext actor,
        string permissionCode,
        CancellationToken cancellationToken);

    Task<EditorialListResponse> GetAdminListAsync(
        string? contentType,
        string? status,
        string? query,
        CancellationToken cancellationToken);

    Task<EditorialContentDetail> GetAdminContentAsync(
        string id,
        CancellationToken cancellationToken);

    Task<EditorialContentDetail> GetPublicBySlugAsync(
        string contentType,
        string slug,
        CancellationToken cancellationToken);

    Task<EditorialListResponse> GetPublicWikiHomeAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EditorialContentDetail>> SearchPublicWikiAsync(
        string query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EditorialContentDetail>> GetPublicFaqAsync(
        string scope,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EditorialContentSummary>> GetPublicSitemapAsync(
        CancellationToken cancellationToken);

    Task<EditorialRedirect?> GetRedirectAsync(
        string oldPath,
        CancellationToken cancellationToken);

    Task<EditorialMutationResponse> UpsertContentAsync(
        string? id,
        EditorialContentPayload payload,
        PortalSessionContext actor,
        string correlationId,
        CancellationToken cancellationToken);

    Task<EditorialMutationResponse> PublishAsync(
        string id,
        PortalSessionContext actor,
        string correlationId,
        CancellationToken cancellationToken);

    Task<EditorialMutationResponse> ArchiveAsync(
        string id,
        PortalSessionContext actor,
        string correlationId,
        CancellationToken cancellationToken);

    Task<EditorialCategory> UpsertCategoryAsync(
        string? id,
        EditorialCategoryPayload payload,
        PortalSessionContext actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EditorialRevisionSummary>> GetRevisionsAsync(
        string contentId,
        CancellationToken cancellationToken);

    Task<EditorialRevisionDetail> GetRevisionAsync(
        string revisionId,
        CancellationToken cancellationToken);

    Task<EditorialMutationResponse> RestoreRevisionAsync(
        string revisionId,
        PortalSessionContext actor,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class EditorialService : IEditorialService
{
    private const int MaxTitleLength = 220;
    private const int MaxSlugLength = 120;
    private const int MaxSummaryLength = 600;
    private const int MaxMarkdownLength = 160_000;
    private static readonly string[] ReservedSeoSlugs =
    [
        "access-denied",
        "admin",
        "api",
        "backups",
        "cgv",
        "commercial-documents",
        "configurer",
        "contact",
        "dashboard",
        "decouvrir-espace-client",
        "diagnostic",
        "downloads",
        "invoices",
        "login",
        "mentions-legales",
        "notifications",
        "offres",
        "panier",
        "password",
        "politique-confidentialite",
        "portfolio",
        "profile",
        "request-service",
        "services",
        "set-password",
        "signup",
        "solutions",
        "souscrire",
        "support",
        "wiki"
    ];

    private readonly IEditorialRepository _repository;

    public EditorialService(IEditorialRepository repository)
    {
        _repository = repository;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public async Task EnsurePermissionAsync(
        PortalSessionContext actor,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        if (!await _repository.HasAdminPermissionAsync(
                actor.UserId,
                permissionCode,
                cancellationToken))
        {
            throw new PortalAccessDeniedException();
        }
    }

    public async Task<EditorialListResponse> GetAdminListAsync(
        string? contentType,
        string? status,
        string? query,
        CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeOptionalContentType(contentType);
        var normalizedStatus = NormalizeOptionalStatus(status);
        return new EditorialListResponse(
            await _repository.GetContentListAsync(
                normalizedType,
                normalizedStatus,
                string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
                cancellationToken),
            await _repository.GetCategoriesAsync(normalizedType, cancellationToken));
    }

    public async Task<EditorialContentDetail> GetAdminContentAsync(
        string id,
        CancellationToken cancellationToken)
        => await _repository.GetContentAsync(id, cancellationToken)
            ?? throw new PortalDataNotFoundException();

    public async Task<EditorialContentDetail> GetPublicBySlugAsync(
        string contentType,
        string slug,
        CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeContentType(contentType);
        var normalizedSlug = NormalizeSlug(slug);
        var content = await _repository.GetContentBySlugAsync(
            normalizedType,
            normalizedSlug,
            publicOnly: true,
            cancellationToken);
        if (content is null || content.Status != EditorialContentStatuses.Published)
        {
            throw new PortalDataNotFoundException();
        }

        return content;
    }

    public async Task<EditorialListResponse> GetPublicWikiHomeAsync(
        CancellationToken cancellationToken)
        => new(
            await _repository.GetContentListAsync(
                EditorialContentTypes.WikiArticle,
                EditorialContentStatuses.Published,
                null,
                cancellationToken),
            await _repository.GetCategoriesAsync(
                EditorialContentTypes.WikiArticle,
                cancellationToken));

    public async Task<IReadOnlyList<EditorialContentDetail>> SearchPublicWikiAsync(
        string query,
        CancellationToken cancellationToken)
    {
        if (query.Trim().Length < 2)
        {
            return [];
        }

        var summaries = await _repository.GetContentListAsync(
            EditorialContentTypes.WikiArticle,
            EditorialContentStatuses.Published,
            query,
            cancellationToken);
        var details = new List<EditorialContentDetail>();
        foreach (var summary in summaries)
        {
            var content = await _repository.GetContentAsync(
                summary.Id,
                cancellationToken);
            if (content is not null && IsPublic(content))
            {
                details.Add(content);
            }
        }

        return details;
    }

    public Task<IReadOnlyList<EditorialContentDetail>> GetPublicFaqAsync(
        string scope,
        CancellationToken cancellationToken)
        => _repository.GetFaqByScopeAsync(NormalizeScope(scope), cancellationToken);

    public async Task<IReadOnlyList<EditorialContentSummary>> GetPublicSitemapAsync(
        CancellationToken cancellationToken)
        => (await _repository.GetContentListAsync(
                null,
                EditorialContentStatuses.Published,
                null,
                cancellationToken))
            .Where(content => !content.NoIndex && content.PublicPath is not null)
            .ToArray();

    public Task<EditorialRedirect?> GetRedirectAsync(
        string oldPath,
        CancellationToken cancellationToken)
        => _repository.GetRedirectAsync(NormalizePath(oldPath), cancellationToken);

    public async Task<EditorialMutationResponse> UpsertContentAsync(
        string? id,
        EditorialContentPayload payload,
        PortalSessionContext actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var current = string.IsNullOrWhiteSpace(id)
            ? null
            : await _repository.GetContentAsync(id, cancellationToken);
        var contentType = current?.ContentType
            ?? NormalizeContentType(payload.ContentType);
        var now = DateTime.UtcNow.ToString("O");
        var normalized = ValidatePayload(
            current?.Id ?? Guid.NewGuid().ToString("D"),
            contentType,
            payload,
            current,
            actor.UserId,
            now);
        var changed = current is null || HasChanged(current, normalized);

        if (current is not null && changed)
        {
            await _repository.AddRevisionAsync(
                current.Id,
                ResolveRevisionAction(current, normalized),
                current,
                actor.UserId,
                cancellationToken);
        }

        if (current is not null
            && current.Slug != normalized.Slug
            && current.Status == EditorialContentStatuses.Published)
        {
            var oldPath = BuildPublicPath(current.ContentType, current.Slug);
            var newPath = BuildPublicPath(normalized.ContentType, normalized.Slug);
            if (oldPath is not null && newPath is not null)
            {
                await _repository.AddRedirectAsync(
                    current.Id,
                    current.ContentType,
                    oldPath,
                    newPath,
                    actor.UserId,
                    cancellationToken);
            }
        }

        await _repository.UpsertContentAsync(normalized, cancellationToken);
        return new EditorialMutationResponse(
            normalized.Id,
            changed,
            normalized.UpdatedAt,
            correlationId);
    }

    public async Task<EditorialMutationResponse> PublishAsync(
        string id,
        PortalSessionContext actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var current = await GetAdminContentAsync(id, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var next = current with
        {
            Status = EditorialContentStatuses.Published,
            PublishedAt = current.PublishedAt ?? now,
            UpdatedAt = now,
            UpdatedByUserId = actor.UserId
        };
        await _repository.AddRevisionAsync(
            id,
            "publish",
            current,
            actor.UserId,
            cancellationToken);
        await _repository.UpsertContentAsync(next, cancellationToken);
        return new EditorialMutationResponse(id, true, now, correlationId);
    }

    public async Task<EditorialMutationResponse> ArchiveAsync(
        string id,
        PortalSessionContext actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var current = await GetAdminContentAsync(id, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var next = current with
        {
            Status = EditorialContentStatuses.Archived,
            UpdatedAt = now,
            UpdatedByUserId = actor.UserId
        };
        await _repository.AddRevisionAsync(
            id,
            "archive",
            current,
            actor.UserId,
            cancellationToken);
        await _repository.UpsertContentAsync(next, cancellationToken);
        return new EditorialMutationResponse(id, true, now, correlationId);
    }

    public async Task<EditorialCategory> UpsertCategoryAsync(
        string? id,
        EditorialCategoryPayload payload,
        PortalSessionContext actor,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.ToString("O");
        var category = new EditorialCategory(
            string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("D") : id,
            NormalizeContentType(payload.ContentType),
            NormalizeTitle(payload.Name, maxLength: 160),
            NormalizeSlug(payload.Slug),
            NormalizeOptional(payload.Description, MaxSummaryLength),
            payload.SortOrder ?? 0,
            now,
            now);
        return await _repository.UpsertCategoryAsync(category, cancellationToken);
    }

    public Task<IReadOnlyList<EditorialRevisionSummary>> GetRevisionsAsync(
        string contentId,
        CancellationToken cancellationToken)
        => _repository.GetRevisionsAsync(contentId, cancellationToken);

    public async Task<EditorialRevisionDetail> GetRevisionAsync(
        string revisionId,
        CancellationToken cancellationToken)
        => await _repository.GetRevisionAsync(revisionId, cancellationToken)
            ?? throw new PortalDataNotFoundException();

    public async Task<EditorialMutationResponse> RestoreRevisionAsync(
        string revisionId,
        PortalSessionContext actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var revision = await GetRevisionAsync(revisionId, cancellationToken);
        var current = await GetAdminContentAsync(
            revision.ContentId,
            cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        await _repository.AddRevisionAsync(
            current.Id,
            "restore_before",
            current,
            actor.UserId,
            cancellationToken);
        var restored = revision.Snapshot with
        {
            UpdatedAt = now,
            UpdatedByUserId = actor.UserId
        };
        await _repository.UpsertContentAsync(restored, cancellationToken);
        return new EditorialMutationResponse(
            restored.Id,
            true,
            restored.UpdatedAt,
            correlationId);
    }

    private static EditorialContentDetail ValidatePayload(
        string id,
        string contentType,
        EditorialContentPayload payload,
        EditorialContentDetail? current,
        string actorUserId,
        string now)
    {
        var slug = NormalizeSlug(payload.Slug);
        if (contentType == EditorialContentTypes.SeoPage
            && ReservedSeoSlugs.Contains(slug, StringComparer.Ordinal))
        {
            throw new PortalValidationException();
        }

        var status = NormalizeStatus(payload.Status);
        var body = payload.BodyMarkdown?.Trim() ?? string.Empty;
        if (body.Length > MaxMarkdownLength)
        {
            throw new PortalValidationException();
        }

        var faqScopes = contentType == EditorialContentTypes.Faq
            ? (payload.FaqScopes ?? [])
                .Select(NormalizeScope)
                .Distinct(StringComparer.Ordinal)
                .Take(20)
                .ToArray()
            : [];
        var publishedAt =
            status == EditorialContentStatuses.Published
                ? current?.PublishedAt ?? now
                : current?.PublishedAt;

        return new EditorialContentDetail(
            id,
            contentType,
            NormalizeTitle(payload.Title),
            slug,
            NormalizeOptional(payload.Summary, MaxSummaryLength),
            body,
            string.IsNullOrWhiteSpace(payload.CategoryId)
                ? null
                : payload.CategoryId.Trim(),
            current?.CategoryName,
            status,
            NormalizeOptional(payload.SeoTitle, MaxTitleLength),
            NormalizeOptional(payload.SeoDescription, 320),
            NormalizeUrl(payload.CanonicalUrl),
            payload.NoIndex ?? false,
            payload.SortOrder ?? current?.SortOrder ?? 0,
            faqScopes,
            publishedAt,
            current?.CreatedAt ?? now,
            now,
            current?.CreatedByUserId ?? actorUserId,
            actorUserId,
            BuildPublicPath(contentType, slug));
    }

    private static bool HasChanged(
        EditorialContentDetail current,
        EditorialContentDetail next)
        => current.Title != next.Title
            || current.Slug != next.Slug
            || current.Summary != next.Summary
            || current.BodyMarkdown != next.BodyMarkdown
            || current.CategoryId != next.CategoryId
            || current.Status != next.Status
            || current.SeoTitle != next.SeoTitle
            || current.SeoDescription != next.SeoDescription
            || current.CanonicalUrl != next.CanonicalUrl
            || current.NoIndex != next.NoIndex
            || current.SortOrder != next.SortOrder
            || !current.FaqScopes.SequenceEqual(next.FaqScopes);

    private static string ResolveRevisionAction(
        EditorialContentDetail current,
        EditorialContentDetail next)
    {
        if (current.Slug != next.Slug)
        {
            return "slug_change";
        }

        if (current.Status != next.Status)
        {
            return next.Status;
        }

        return "update";
    }

    private static bool IsPublic(EditorialContentDetail content)
        => content.Status == EditorialContentStatuses.Published
            && content.PublishedAt is not null
            && !content.NoIndex;

    private static string? NormalizeOptionalContentType(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : NormalizeContentType(value);

    private static string? NormalizeOptionalStatus(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : NormalizeStatus(value);

    private static string NormalizeContentType(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (!EditorialContentTypes.IsKnown(normalized))
        {
            throw new PortalValidationException();
        }

        return normalized!;
    }

    private static string NormalizeStatus(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (!EditorialContentStatuses.IsKnown(normalized))
        {
            throw new PortalValidationException();
        }

        return normalized!;
    }

    private static string NormalizeTitle(string? value, int maxLength = MaxTitleLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > maxLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string NormalizeSlug(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > MaxSlugLength
            || !System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string NormalizeScope(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > 80
            || !System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string NormalizePath(string value)
    {
        var normalized = value.Trim();
        if (!normalized.StartsWith('/')
            || normalized.Contains('\\')
            || normalized.Contains("//", StringComparison.Ordinal))
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new PortalValidationException();
        }

        return normalized;
    }

    private static string? NormalizeUrl(string? value)
    {
        var normalized = NormalizeOptional(value, 2048);
        if (normalized is null)
        {
            return null;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length > 0
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new PortalValidationException();
        }

        return uri.ToString();
    }

    private static string? BuildPublicPath(string contentType, string slug)
        => contentType switch
        {
            EditorialContentTypes.WikiArticle => $"/article/{slug}",
            EditorialContentTypes.SeoPage => $"/{slug}",
            _ => null
        };
}
