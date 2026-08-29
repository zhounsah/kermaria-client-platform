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

/// <summary>
/// Un modele du code et sa representation historisable, pour l'amorce.
/// </summary>
public sealed record DemoContentTemplateImportItem(
    StoredDemoContentTemplate Template,
    string PayloadJson);

public interface IDemoContentTemplateRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<StoredDemoContentTemplate>> ListAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Cree ou remplace un modele, ses services et sa revision en une seule
    /// unite de travail. `expectedVersion` vaut 0 pour une creation ; toute
    /// autre valeur doit correspondre a la version stockee, sinon l'ecriture
    /// est refusee.
    /// </summary>
    /// <remarks>
    /// La revision fait partie de l'ecriture, pas de son epilogue. Un modele
    /// de demonstration modifie sans trace rend impossible de repondre a
    /// « qui a change ce que voient les prospects, et quand ».
    /// </remarks>
    Task<bool> TrySaveAsync(
        StoredDemoContentTemplate template,
        int expectedVersion,
        string payloadJson,
        string correlationId,
        string outcome,
        CancellationToken cancellationToken);

    /// <summary>
    /// Supprime un modele et consigne la suppression dans la meme transaction.
    /// </summary>
    Task<bool> TryDeleteAsync(
        string templateKey,
        int expectedVersion,
        string? actorUserId,
        string correlationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Recopie les modeles du code, tout ou rien.
    /// </summary>
    /// <remarks>
    /// <para>
    /// L'amorce echouait auparavant modele par modele : une panne au troisieme
    /// laissait une table a moitie peuplee, que le controle « la table est-elle
    /// vide ? » considerait ensuite comme deja administree. Les modeles
    /// manquants devenaient alors invisibles et definitivement perdus pour
    /// l'amorce.
    /// </para>
    /// <para>
    /// La vacuite de la table est verifiee <b>dans</b> la transaction :
    /// verifiee avant, deux amorces simultanees passeraient toutes les deux.
    /// </para>
    /// </remarks>
    Task<bool> TryImportAsync(
        IReadOnlyList<DemoContentTemplateImportItem> items,
        string correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredTemplateRevision>> GetRevisionsAsync(
        CancellationToken cancellationToken);
}
