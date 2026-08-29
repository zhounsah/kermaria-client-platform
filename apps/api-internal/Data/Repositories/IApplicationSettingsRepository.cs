namespace Kermaria.ApiInternal.Data.Repositories;

public sealed record StoredApplicationSetting(
    string Key,
    string Category,
    string ValueJson,
    string ValueType,
    int Version,
    DateTime UpdatedAtUtc,
    string? UpdatedByUserId = null);

public interface IApplicationSettingsRepository
{
    bool IsPersistent { get; }
    Task<IReadOnlyList<StoredApplicationSetting>> GetAllAsync(CancellationToken cancellationToken);
    Task<StoredApplicationSetting?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Applique la valeur et historise le changement en une seule unite de
    /// travail.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Les deux ecritures etaient auparavant separees : un reglage pouvait
    /// changer sans laisser de trace si l'historisation echouait juste apres.
    /// Un parametre de securite modifie sans revision est indistinguable d'un
    /// parametre jamais modifie — c'est exactement ce qu'un audit doit pouvoir
    /// trancher.
    /// </para>
    /// <para>
    /// La valeur remplacee est relue <b>sous verrou</b> et non fournie par
    /// l'appelant : une lecture prealable hors transaction decrirait un etat
    /// deja perime, et la revision consignerait un « avant » qui n'a jamais
    /// existe.
    /// </para>
    /// <para>
    /// Retourne <c>false</c> sur conflit de version. Une indisponibilite de la
    /// persistance leve : elle ne doit pas se confondre avec un conflit, qui
    /// invite a recharger la page.
    /// </para>
    /// </remarks>
    Task<bool> TryApplyAsync(
        StoredApplicationSetting setting,
        int expectedVersion,
        string correlationId,
        CancellationToken cancellationToken);
}
