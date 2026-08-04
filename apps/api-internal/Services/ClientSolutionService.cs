using System.Globalization;
using System.Text;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services;

public sealed record StoredClientSolutionLogo(
    string ContentType,
    string OriginalName,
    int SizeBytes,
    string UpdatedAt);

public sealed record StoredClientSolutionLogoContent(
    byte[] Bytes,
    string ContentType,
    string OriginalName,
    string UpdatedAt);

public sealed record StoredClientSolution(
    string Id,
    string Slug,
    string Title,
    string? Tagline,
    string TargetUrl,
    bool OpensInNewTab,
    string Status,
    int DisplayOrder,
    StoredClientSolutionLogo? Logo,
    string CreatedAt,
    string UpdatedAt);

public sealed record StoredClientSolutionPortalSettings(
    string? Eyebrow,
    string Title,
    string? Description,
    string? FooterNote,
    string UpdatedAt);

public sealed record ValidatedClientSolution(
    string Id,
    string Slug,
    string Title,
    string? Tagline,
    string TargetUrl,
    bool OpensInNewTab,
    string Status,
    int DisplayOrder);

public sealed record ValidatedClientSolutionPortalSettings(
    string? Eyebrow,
    string Title,
    string? Description,
    string? FooterNote);

public sealed class ClientSolutionConflictException : Exception
{
    public ClientSolutionConflictException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public interface IClientSolutionService
{
    bool IsPersistent { get; }

    Task<PublicClientSolutionPortal> GetPublicPortalAsync(
        CancellationToken cancellationToken);

    Task<StoredClientSolutionLogoContent> GetPublicLogoAsync(
        string id,
        CancellationToken cancellationToken);

    Task<AdminClientSolutionPortal> GetAdminPortalAsync(
        CancellationToken cancellationToken);

    Task<ClientSolution> GetAdminSolutionAsync(
        string id,
        CancellationToken cancellationToken);

    Task<ClientSolutionPortalMutationResponse> UpdateSettingsAsync(
        ClientSolutionPortalSettingsPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<ClientSolutionMutationResponse> CreateSolutionAsync(
        ClientSolutionPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<ClientSolutionMutationResponse> UpdateSolutionAsync(
        string id,
        ClientSolutionPayload payload,
        string correlationId,
        CancellationToken cancellationToken);

    Task<ClientSolutionMutationResponse> DeleteSolutionAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);

    Task<ClientSolutionMutationResponse> UploadLogoAsync(
        string id,
        string originalName,
        string? contentType,
        Stream stream,
        string correlationId,
        CancellationToken cancellationToken);

    Task<ClientSolutionMutationResponse> DeleteLogoAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class ClientSolutionService : IClientSolutionService
{
    private const int MaxSlugLength = 80;
    private const int MaxTitleLength = 120;
    private const int MaxTaglineLength = 280;
    private const int MaxTargetUrlLength = 2048;
    private const int MaxDisplayOrder = 9999;
    private const int MaxSettingsEyebrowLength = 120;
    private const int MaxSettingsTitleLength = 160;
    private const int MaxSettingsDescriptionLength = 600;
    private const int MaxSettingsFooterNoteLength = 600;
    private const int MaxLogoOriginalNameLength = 180;

    private static readonly ValidatedClientSolutionPortalSettings DefaultSettings =
        new(
            "Portail de services",
            "Accéder à mes solutions",
            "Retrouvez ici les accès directs aux services mis à votre disposition. "
            + "Cliquez sur une tuile pour ouvrir le service correspondant.",
            null);

    private readonly IClientSolutionRepository _repository;
    private readonly IClientSolutionSchemaEnsurer _schemaEnsurer;

    public ClientSolutionService(
        IClientSolutionRepository repository,
        IClientSolutionSchemaEnsurer schemaEnsurer)
    {
        _repository = repository;
        _schemaEnsurer = schemaEnsurer;
    }

    public bool IsPersistent => _repository.IsPersistent;

    public async Task<PublicClientSolutionPortal> GetPublicPortalAsync(
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var settings = await LoadSettingsAsync(cancellationToken);
        var solutions = await _repository.GetSolutionsAsync(cancellationToken);

        return new PublicClientSolutionPortal(
            ToSettingsContract(settings),
            solutions
                .Where(solution =>
                    solution.Status == ClientSolutionStatuses.Published)
                .OrderBy(solution => solution.DisplayOrder)
                .ThenBy(solution => solution.Title, StringComparer.Ordinal)
                .Select(solution => new PublicClientSolution(
                    solution.Id,
                    solution.Slug,
                    solution.Title,
                    solution.Tagline,
                    solution.TargetUrl,
                    solution.OpensInNewTab,
                    solution.Logo is not null,
                    solution.Logo?.UpdatedAt,
                    solution.DisplayOrder))
                .ToArray());
    }

    public async Task<StoredClientSolutionLogoContent> GetPublicLogoAsync(
        string id,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var normalizedId = NormalizeIdentifier(id);
        var solution = await _repository.GetSolutionAsync(
            normalizedId,
            cancellationToken);
        if (solution is null
            || solution.Status != ClientSolutionStatuses.Published)
        {
            throw new PortalDataNotFoundException();
        }

        return await _repository.GetLogoAsync(normalizedId, cancellationToken)
            ?? throw new PortalDataNotFoundException();
    }

    public async Task<AdminClientSolutionPortal> GetAdminPortalAsync(
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var settings = await LoadSettingsAsync(cancellationToken);
        var solutions = await _repository.GetSolutionsAsync(cancellationToken);

        return new AdminClientSolutionPortal(
            ToSettingsContract(settings),
            solutions
                .OrderBy(solution => solution.DisplayOrder)
                .ThenBy(solution => solution.Title, StringComparer.Ordinal)
                .Select(ToContract)
                .ToArray());
    }

    public async Task<ClientSolution> GetAdminSolutionAsync(
        string id,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var solution = await _repository.GetSolutionAsync(
            NormalizeIdentifier(id),
            cancellationToken);

        return solution is null
            ? throw new PortalDataNotFoundException()
            : ToContract(solution);
    }

    public async Task<ClientSolutionPortalMutationResponse> UpdateSettingsAsync(
        ClientSolutionPortalSettingsPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        return await _repository.UpsertSettingsAsync(
            ValidateSettings(payload),
            correlationId,
            cancellationToken);
    }

    public async Task<ClientSolutionMutationResponse> CreateSolutionAsync(
        ClientSolutionPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var solution = ValidateSolution(
            Guid.NewGuid().ToString("D"),
            payload);
        await EnsureSlugAvailableAsync(
            solution.Slug,
            excludedId: null,
            cancellationToken);

        return await _repository.CreateSolutionAsync(
            solution,
            correlationId,
            cancellationToken);
    }

    public async Task<ClientSolutionMutationResponse> UpdateSolutionAsync(
        string id,
        ClientSolutionPayload payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var normalizedId = NormalizeIdentifier(id);
        var current = await _repository.GetSolutionAsync(
            normalizedId,
            cancellationToken)
            ?? throw new PortalDataNotFoundException();
        var solution = ValidateSolution(current.Id, payload);
        await EnsureSlugAvailableAsync(
            solution.Slug,
            current.Id,
            cancellationToken);

        return await _repository.UpdateSolutionAsync(
            solution,
            correlationId,
            cancellationToken);
    }

    public async Task<ClientSolutionMutationResponse> DeleteSolutionAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var normalizedId = NormalizeIdentifier(id);
        _ = await _repository.GetSolutionAsync(normalizedId, cancellationToken)
            ?? throw new PortalDataNotFoundException();

        return await _repository.DeleteSolutionAsync(
            normalizedId,
            correlationId,
            cancellationToken);
    }

    public async Task<ClientSolutionMutationResponse> UploadLogoAsync(
        string id,
        string originalName,
        string? contentType,
        Stream stream,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var normalizedId = NormalizeIdentifier(id);
        _ = await _repository.GetSolutionAsync(normalizedId, cancellationToken)
            ?? throw new PortalDataNotFoundException();

        var normalizedContentType = NormalizeLogoContentType(contentType);
        if (!ClientSolutionLogoContentTypes.IsKnown(normalizedContentType))
        {
            throw new ClientSolutionConflictException(
                "CLIENT_SOLUTION_LOGO_TYPE_REJECTED",
                "Le logo doit être un fichier PNG, JPEG, WebP ou SVG.");
        }

        var bytes = await ReadLimitedAsync(stream, cancellationToken);
        if (bytes.Length == 0)
        {
            throw new PortalValidationException();
        }

        return await _repository.SaveLogoAsync(
            normalizedId,
            new StoredClientSolutionLogoContent(
                bytes,
                normalizedContentType,
                NormalizeLogoOriginalName(originalName),
                DateTime.UtcNow.ToString("O")),
            correlationId,
            cancellationToken);
    }

    public async Task<ClientSolutionMutationResponse> DeleteLogoAsync(
        string id,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await _schemaEnsurer.EnsureAsync(cancellationToken);
        var normalizedId = NormalizeIdentifier(id);
        _ = await _repository.GetSolutionAsync(normalizedId, cancellationToken)
            ?? throw new PortalDataNotFoundException();

        return await _repository.DeleteLogoAsync(
            normalizedId,
            correlationId,
            cancellationToken);
    }

    private async Task<StoredClientSolutionPortalSettings> LoadSettingsAsync(
        CancellationToken cancellationToken)
        => await _repository.GetSettingsAsync(cancellationToken)
            ?? new StoredClientSolutionPortalSettings(
                DefaultSettings.Eyebrow,
                DefaultSettings.Title,
                DefaultSettings.Description,
                DefaultSettings.FooterNote,
                DateTime.UnixEpoch.ToString("O"));

    private async Task EnsureSlugAvailableAsync(
        string slug,
        string? excludedId,
        CancellationToken cancellationToken)
    {
        if (await _repository.SlugExistsAsync(slug, excludedId, cancellationToken))
        {
            throw new ClientSolutionConflictException(
                "CLIENT_SOLUTION_SLUG_TAKEN",
                "Une autre solution utilise déjà cet identifiant d'URL.");
        }
    }

    private static ClientSolutionPortalSettings ToSettingsContract(
        StoredClientSolutionPortalSettings settings)
        => new(
            settings.Eyebrow,
            settings.Title,
            settings.Description,
            settings.FooterNote,
            settings.UpdatedAt);

    private static ClientSolution ToContract(StoredClientSolution solution)
        => new(
            solution.Id,
            solution.Slug,
            solution.Title,
            solution.Tagline,
            solution.TargetUrl,
            solution.OpensInNewTab,
            solution.Status,
            solution.DisplayOrder,
            solution.Logo is not null,
            solution.Logo?.OriginalName,
            solution.Logo?.ContentType,
            solution.Logo?.SizeBytes,
            solution.Logo?.UpdatedAt,
            solution.CreatedAt,
            solution.UpdatedAt);

    private static ValidatedClientSolutionPortalSettings ValidateSettings(
        ClientSolutionPortalSettingsPayload payload)
    {
        var title = Trim(payload.Title);
        if (title is null
            || title.Length < 2
            || title.Length > MaxSettingsTitleLength)
        {
            throw new PortalValidationException();
        }

        var eyebrow = Trim(payload.Eyebrow);
        var description = Trim(payload.Description);
        var footerNote = Trim(payload.FooterNote);
        if (eyebrow is { Length: > MaxSettingsEyebrowLength }
            || description is { Length: > MaxSettingsDescriptionLength }
            || footerNote is { Length: > MaxSettingsFooterNoteLength })
        {
            throw new PortalValidationException();
        }

        return new ValidatedClientSolutionPortalSettings(
            eyebrow,
            title,
            description,
            footerNote);
    }

    private static ValidatedClientSolution ValidateSolution(
        string id,
        ClientSolutionPayload payload)
    {
        var title = Trim(payload.Title);
        if (title is null || title.Length < 2 || title.Length > MaxTitleLength)
        {
            throw new PortalValidationException();
        }

        var slug = NormalizeSlug(Trim(payload.Slug) ?? title);
        if (slug.Length is < 2 or > MaxSlugLength)
        {
            throw new PortalValidationException();
        }

        var tagline = Trim(payload.Tagline);
        if (tagline is { Length: > MaxTaglineLength })
        {
            throw new PortalValidationException();
        }

        var targetUrl = Trim(payload.TargetUrl);
        if (targetUrl is null
            || targetUrl.Length > MaxTargetUrlLength
            || !IsAbsoluteWebUrl(targetUrl))
        {
            throw new PortalValidationException();
        }

        var status = Trim(payload.Status) ?? ClientSolutionStatuses.Draft;
        if (!ClientSolutionStatuses.IsKnown(status))
        {
            throw new PortalValidationException();
        }

        var displayOrder = payload.DisplayOrder ?? 0;
        if (displayOrder is < 0 or > MaxDisplayOrder)
        {
            throw new PortalValidationException();
        }

        return new ValidatedClientSolution(
            id,
            slug,
            title,
            tagline,
            targetUrl,
            payload.OpensInNewTab ?? true,
            status,
            displayOrder);
    }

    private static bool IsAbsoluteWebUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo);

    private static string NormalizeSlug(string value)
    {
        var normalized = value
            .Normalize(NormalizationForm.FormD)
            .Where(character =>
                CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
            .ToArray();

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in new string(normalized).ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length <= MaxSlugLength ? slug : slug[..MaxSlugLength].Trim('-');
    }

    private static string NormalizeIdentifier(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= 100
            && normalized.All(character =>
                character == '-'
                || (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z'))
            ? normalized
            : throw new PortalValidationException();
    }

    private static string NormalizeLogoContentType(string? contentType)
    {
        var normalized = contentType?.Split(';')[0].Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;
    }

    private static string NormalizeLogoOriginalName(string value)
    {
        var fileName = Path.GetFileName(value ?? string.Empty).Trim();
        var sanitized = new string(
            fileName
                .Where(character =>
                    !char.IsControl(character)
                    && character != '"'
                    && character != '\r'
                    && character != '\n')
                .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "logo";
        }

        return sanitized.Length <= MaxLogoOriginalNameLength
            ? sanitized
            : sanitized[..MaxLogoOriginalNameLength];
    }

    private static async Task<byte[]> ReadLimitedAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > ClientSolutionLogoContentTypes.MaxSizeBytes)
            {
                throw new ClientSolutionConflictException(
                    "CLIENT_SOLUTION_LOGO_TOO_LARGE",
                    "Le logo ne doit pas dépasser 512 Ko.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
