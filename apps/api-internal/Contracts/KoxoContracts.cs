using System.Text.Json.Serialization;

namespace Kermaria.ApiInternal.Contracts;

public sealed record KoxoExportUser(
    string Civilite,
    string Nom,
    string Prenom,
    string DateNaissance,
    string IdentifiantUnique,
    string GroupeSecondaire,
    string Email,
    // N'alimente AUCUNE colonne du CSV : le groupe primaire est porte par le
    // profil KoXo (le XML), pas par le fichier. Ce champ sert uniquement a
    // AIGUILLER chaque identite vers le bon profil, donc vers le bon CSV. Sans
    // lui, un seul fichier melangerait payants et demos, et le modele KoXo — qui
    // ne s'associe qu'a un unique groupe primaire — appliquerait le quota des uns
    // aux autres.
    string GroupePrimaire,
    // Alimente la colonne 14 du CSV, que KoXo applique a l'annuaire. Omis du
    // JSON quand il n'y a rien a publier : KoXo conserve alors le mot de passe
    // qu'il connait deja, au lieu de le remplacer par une valeur vide.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? MotDePasse = null);

public sealed record KoxoInvalidUser(
    string? IdentifiantUnique,
    string PortalUserId,
    IReadOnlyList<string> Fields);

public sealed record KoxoExportPayload(
    int SchemaVersion,
    string GeneratedAt,
    int UserCount,
    IReadOnlyList<KoxoExportUser> Users);

public sealed record KoxoValidationFailurePayload(
    string Error,
    string Message,
    IReadOnlyList<KoxoInvalidUser> InvalidUsers);

public sealed record KoxoRunSummary(
    string CreatedAt,
    string Source,
    string Status,
    int? SchemaVersion,
    int UserCount,
    int InvalidUserCount,
    string CorrelationId,
    string? SourceAddress,
    string SummaryMessage,
    string? GeneratedAt);

public sealed record KoxoAdminDashboard(
    int ExportableUserCount,
    int InvalidUserCount,
    string? LastApiCallAt,
    string? LastRequestedStatus,
    int SchemaVersion,
    KoxoExportPayload? Preview,
    IReadOnlyList<KoxoInvalidUser> ValidationErrors,
    KoxoRunSummary? LastRun);
