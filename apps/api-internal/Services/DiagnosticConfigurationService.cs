using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Kermaria.ApiInternal.Services;

public interface IDiagnosticConfigurationService
{
    bool IsPersistent { get; }

    Task<PublicDiagnosticConfigurationResponse> GetPublishedAsync(
        CancellationToken cancellationToken);

    Task<DiagnosticConfigurationAdminViewResponse> GetAdminViewAsync(
        CancellationToken cancellationToken);

    DiagnosticConfigurationMutationResponse Validate(
        DiagnosticConfigurationValidateRequest request,
        string correlationId);

    Task<DiagnosticConfigurationMutationResponse> SaveDraftAsync(
        DiagnosticConfigurationUpdateRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<DiagnosticConfigurationMutationResponse> PublishAsync(
        DiagnosticConfigurationPublishRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DiagnosticConfigurationRevisionItemResponse>> GetRevisionsAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Diagnostic administrable (specification, section 9). Le service ne rend rien
/// et ne calcule aucun prix : il valide une DSL fermee, la persiste en deux
/// etats et expose la version publiee. Le moteur d'interpretation reste unique
/// et vit dans le WebPortal, partage entre le parcours public et le simulateur
/// d'administration.
/// </summary>
public sealed class DiagnosticConfigurationService : IDiagnosticConfigurationService
{
    public const string DraftState = "draft";
    public const string PublishedState = "published";

    /// <summary>
    /// Duree de vie du cache de la version publiee : assez courte pour qu'une
    /// publication soit visible sans redemarrage, assez longue pour qu'une
    /// rafale de visites ne relise pas la base a chaque page.
    /// </summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private static readonly object CacheGate = new();
    private static StoredDiagnosticConfiguration? _cachedPublished;
    private static DateTime _cachedAtUtc = DateTime.MinValue;
    private static bool _cacheValid;

    private readonly IDiagnosticConfigurationRepository _repository;
    private readonly ILogger<DiagnosticConfigurationService> _logger;

    public DiagnosticConfigurationService(
        IDiagnosticConfigurationRepository repository,
        ILogger<DiagnosticConfigurationService> logger)
        => (_repository, _logger) = (repository, logger);

    public bool IsPersistent => _repository.IsPersistent;

    public static void Invalidate()
    {
        lock (CacheGate)
        {
            _cacheValid = false;
            _cachedPublished = null;
        }
    }

    public async Task<PublicDiagnosticConfigurationResponse> GetPublishedAsync(
        CancellationToken cancellationToken)
    {
        lock (CacheGate)
        {
            if (_cacheValid && DateTime.UtcNow - _cachedAtUtc < CacheTtl)
            {
                return ToPublic(_cachedPublished);
            }
        }

        StoredDiagnosticConfiguration? stored;
        try
        {
            stored = await _repository.GetAsync(PublishedState, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Repli explicite : sans base, le WebPortal retombe sur la
            // configuration integree a son code. Le parcours public reste
            // complet plutot que vide.
            _logger.LogWarning(
                exception,
                "Diagnostic configuration unavailable; falling back to built-in definitions.");
            return ToPublic(null);
        }

        lock (CacheGate)
        {
            _cachedPublished = stored;
            _cachedAtUtc = DateTime.UtcNow;
            _cacheValid = true;
        }

        return ToPublic(stored);
    }

    public async Task<DiagnosticConfigurationAdminViewResponse> GetAdminViewAsync(
        CancellationToken cancellationToken)
    {
        var draft = await _repository.GetAsync(DraftState, cancellationToken);
        var published = await _repository.GetAsync(PublishedState, cancellationToken);
        return BuildView(draft, published, _repository.IsPersistent);
    }

    public DiagnosticConfigurationMutationResponse Validate(
        DiagnosticConfigurationValidateRequest request,
        string correlationId)
    {
        var validation = DiagnosticConfigurationRegistry.Validate(request.Configuration);
        return validation.IsValid
            ? new DiagnosticConfigurationMutationResponse(
                "DIAGNOSTIC_VALID",
                "La configuration est valide.",
                [],
                null,
                correlationId)
            : new DiagnosticConfigurationMutationResponse(
                "DIAGNOSTIC_INVALID",
                "La configuration comporte des erreurs.",
                validation.Errors,
                null,
                correlationId);
    }

    public async Task<DiagnosticConfigurationMutationResponse> SaveDraftAsync(
        DiagnosticConfigurationUpdateRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var validation = DiagnosticConfigurationRegistry.Validate(request.Configuration);
        if (!validation.IsValid)
        {
            return new DiagnosticConfigurationMutationResponse(
                "DIAGNOSTIC_INVALID",
                "La configuration comporte des erreurs.",
                validation.Errors,
                await GetAdminViewAsync(cancellationToken),
                correlationId);
        }

        var expected = Math.Max(request.ExpectedVersion, 0);
        var draft = new StoredDiagnosticConfiguration(
            DraftState,
            validation.CanonicalJson!,
            expected + 1,
            DateTime.UtcNow,
            actorUserId);

        if (!await _repository.TrySaveDraftAsync(
                draft,
                expected,
                "draft_saved",
                correlationId,
                cancellationToken))
        {
            return new DiagnosticConfigurationMutationResponse(
                "DIAGNOSTIC_VERSION_CONFLICT",
                "Le brouillon a ete modifie entre-temps. Rechargez avant d'enregistrer.",
                [],
                await GetAdminViewAsync(cancellationToken),
                correlationId);
        }

        return new DiagnosticConfigurationMutationResponse(
            "DIAGNOSTIC_DRAFT_SAVED",
            "Brouillon enregistre.",
            [],
            await GetAdminViewAsync(cancellationToken),
            correlationId);
    }

    public async Task<DiagnosticConfigurationMutationResponse> PublishAsync(
        DiagnosticConfigurationPublishRequest request,
        string actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var draft = await _repository.GetAsync(DraftState, cancellationToken);
        if (draft is null)
        {
            return new DiagnosticConfigurationMutationResponse(
                "DIAGNOSTIC_NO_DRAFT",
                "Aucun brouillon a publier.",
                [],
                await GetAdminViewAsync(cancellationToken),
                correlationId);
        }

        if (draft.Version != request.ExpectedDraftVersion)
        {
            return new DiagnosticConfigurationMutationResponse(
                "DIAGNOSTIC_VERSION_CONFLICT",
                "Le brouillon a ete modifie entre-temps. Rechargez avant de publier.",
                [],
                await GetAdminViewAsync(cancellationToken),
                correlationId);
        }

        // Revalidation avant publication : un brouillon enregistre sous une
        // version anterieure du registre ne doit pas atteindre le public parce
        // qu'il avait ete accepte autrefois.
        DiagnosticConfigurationValidation validation;
        try
        {
            using var document = JsonDocument.Parse(draft.PayloadJson);
            validation = DiagnosticConfigurationRegistry.Validate(document.RootElement);
        }
        catch (JsonException)
        {
            validation = new DiagnosticConfigurationValidation(
                null,
                ["Le brouillon enregistre n'est pas un JSON valide."]);
        }

        if (!validation.IsValid)
        {
            return new DiagnosticConfigurationMutationResponse(
                "DIAGNOSTIC_INVALID",
                "Le brouillon n'est plus valide et ne peut pas etre publie.",
                validation.Errors,
                await GetAdminViewAsync(cancellationToken),
                correlationId);
        }

        var expectedPublished = Math.Max(request.ExpectedPublishedVersion, 0);
        var published = new StoredDiagnosticConfiguration(
            PublishedState,
            validation.CanonicalJson!,
            expectedPublished + 1,
            DateTime.UtcNow,
            actorUserId);

        if (!await _repository.TryPublishAsync(
                published,
                expectedPublished,
                draft.Version,
                correlationId,
                cancellationToken))
        {
            return new DiagnosticConfigurationMutationResponse(
                "DIAGNOSTIC_VERSION_CONFLICT",
                "La configuration a ete modifiee entre-temps. Rechargez avant de publier.",
                [],
                await GetAdminViewAsync(cancellationToken),
                correlationId);
        }

        Invalidate();
        return new DiagnosticConfigurationMutationResponse(
            "DIAGNOSTIC_PUBLISHED",
            "Configuration publiee.",
            [],
            await GetAdminViewAsync(cancellationToken),
            correlationId);
    }

    public async Task<IReadOnlyList<DiagnosticConfigurationRevisionItemResponse>>
        GetRevisionsAsync(CancellationToken cancellationToken)
    {
        var revisions = await _repository.GetRevisionsAsync(50, cancellationToken);
        return revisions
            .Select(item => new DiagnosticConfigurationRevisionItemResponse(
                item.Key,
                item.Version,
                item.Outcome,
                item.ActorUserId,
                item.CorrelationId,
                FormatUtc(item.CreatedAtUtc)))
            .ToArray();
    }

    private static DiagnosticConfigurationAdminViewResponse BuildView(
        StoredDiagnosticConfiguration? draft,
        StoredDiagnosticConfiguration? published,
        bool persistent)
        => new(
            ToSnapshot(DraftState, draft),
            ToSnapshot(PublishedState, published),
            // Les charges sont canoniques : une comparaison textuelle suffit et
            // n'annonce jamais une difference qui n'existe pas.
            !string.Equals(
                draft?.PayloadJson,
                published?.PayloadJson,
                StringComparison.Ordinal),
            persistent);

    private static DiagnosticConfigurationSnapshotItem ToSnapshot(
        string state,
        StoredDiagnosticConfiguration? stored)
        => new(
            state,
            stored?.Version ?? 0,
            stored is null ? "code" : "database",
            stored is null ? null : FormatUtc(stored.UpdatedAtUtc),
            ParseOrNull(stored?.PayloadJson));

    private static PublicDiagnosticConfigurationResponse ToPublic(
        StoredDiagnosticConfiguration? stored)
        => new(
            stored?.Version ?? 0,
            stored is null ? "code" : "database",
            stored is null ? null : FormatUtc(stored.UpdatedAtUtc),
            ParseOrNull(stored?.PayloadJson));

    /// <summary>
    /// Le document est clone : un <c>JsonElement</c> survivant a son
    /// <c>JsonDocument</c> leverait a la serialisation de la reponse.
    /// </summary>
    private static JsonElement? ParseOrNull(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FormatUtc(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            .ToString("yyyy-MM-ddTHH:mm:ssZ");
}
