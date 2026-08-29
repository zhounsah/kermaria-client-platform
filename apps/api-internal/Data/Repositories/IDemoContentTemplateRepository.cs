namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record StoredDemoTemplateService(
    string ServiceType,
    string Name,
    string Description,
    string Scope,
    int DisplayOrder);

public sealed record StoredDemoContentTemplate(
    string TemplateKey,
    string Label,
    string Description,
    bool Enabled,
    int DisplayOrder,
    int Version,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId,
    IReadOnlyList<StoredDemoTemplateService> Services);

public interface IDemoContentTemplateRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<StoredDemoContentTemplate>> ListAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Cree ou remplace un modele et ses services. `expectedVersion` vaut 0
    /// pour une creation ; toute autre valeur doit correspondre a la version
    /// stockee, sinon l'ecriture est refusee.
    /// </summary>
    Task<bool> TrySaveAsync(
        StoredDemoContentTemplate template,
        int expectedVersion,
        CancellationToken cancellationToken);

    Task<bool> TryDeleteAsync(
        string templateKey,
        int expectedVersion,
        CancellationToken cancellationToken);

    Task AddRevisionAsync(
        string templateKey,
        int version,
        string payloadJson,
        string? actorUserId,
        string correlationId,
        string outcome,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredTemplateRevision>> GetRevisionsAsync(
        CancellationToken cancellationToken);
}
