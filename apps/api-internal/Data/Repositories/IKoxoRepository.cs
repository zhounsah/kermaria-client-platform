using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <param name="IsDemo">
/// Compte encore en demonstration : son identite doit rester dans l'OU de
/// demonstration commune, quel que soit le code de groupe deja reserve.
/// </param>
/// <param name="KoxoGroupReference">
/// Code de groupe reserve a la creation d'un compte de demo, publie seulement
/// apres conversion. Null pour un client reel ordinaire, dont l'OU est nommee
/// d'apres sa reference.
/// </param>
public sealed record KoxoExportCandidate(
    string PortalUserId,
    string CustomerReference,
    string? KoxoUniqueIdentifier,
    string? PersonalTitle,
    string? GivenName,
    string? Surname,
    string? BirthDate,
    string Email,
    bool IsDemo = false,
    string? KoxoGroupReference = null,
    /// <summary>
    /// Vrai quand cette ligne n'est presente que par l'exception Billing V2,
    /// sans <c>customer_ad_links</c>.
    /// </summary>
    /// <remarks>
    /// KoXo va donc <b>creer</b> l'objet annuaire a partir de cette ligne, et
    /// la colonne 14 est le seul endroit ou le mot de passe peut lui parvenir.
    /// Exporter cette ligne sans mot de passe creerait un compte dont personne
    /// ne connaitrait le secret, et une synchronisation ulterieure ne le
    /// rattraperait pas : le compte existerait deja.
    /// </remarks>
    bool RequiresPendingPassword = false);

public sealed record KoxoRunInsert(
    string Id,
    string Source,
    string Status,
    int? SchemaVersion,
    int UserCount,
    int InvalidUserCount,
    string CorrelationId,
    string? SourceAddress,
    string SummaryMessage,
    DateTime? GeneratedAtUtc,
    string? PreviewJson,
    string? ValidationErrorsJson);

public interface IKoxoRepository
{
    bool IsPersistent { get; }

    Task<IReadOnlyList<KoxoExportCandidate>> ListExportCandidatesAsync(
        CancellationToken cancellationToken);

    Task InsertRunAsync(
        KoxoRunInsert run,
        CancellationToken cancellationToken);

    Task<KoxoRunSummary?> GetLatestRunAsync(
        CancellationToken cancellationToken);

    Task<KoxoRunSummary?> GetLatestRunBySourceAsync(
        string source,
        CancellationToken cancellationToken);
}
