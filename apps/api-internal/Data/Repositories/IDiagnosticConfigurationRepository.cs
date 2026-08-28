namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record StoredDiagnosticConfiguration(
    string State,
    string PayloadJson,
    int Version,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId = null);

/// <summary>
/// Persistance du diagnostic administrable (migration 075). Deux lignes au
/// plus : <c>draft</c> et <c>published</c>. Le parcours public ne lit jamais
/// le brouillon.
/// </summary>
public interface IDiagnosticConfigurationRepository
{
    bool IsPersistent { get; }

    Task<StoredDiagnosticConfiguration?> GetAsync(
        string state,
        CancellationToken cancellationToken);

    Task<bool> TrySaveDraftAsync(
        StoredDiagnosticConfiguration draft,
        int expectedVersion,
        string outcome,
        string correlationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publication atomique : la version publiee et la trace d'historique sont
    /// ecrites dans une seule transaction, apres verification que le brouillon
    /// n'a pas bouge. Le public voit donc l'ancienne version complete ou la
    /// nouvelle version complete, jamais un etat intermediaire.
    /// </summary>
    Task<bool> TryPublishAsync(
        StoredDiagnosticConfiguration published,
        int expectedPublishedVersion,
        int expectedDraftVersion,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredTemplateRevision>> GetRevisionsAsync(
        int limit,
        CancellationToken cancellationToken);
}
