namespace Kermaria.ApiInternal.Contracts;

public sealed record KoxoExportUser(
    string Civilite,
    string Nom,
    string Prenom,
    string DateNaissance,
    string IdentifiantUnique,
    string GroupeSecondaire,
    string Email);

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
