using System.Text.Json;

namespace Kermaria.ApiInternal.Contracts;

// Modele fidele de la DSL declarative du diagnostic (specification, section 9).
// Il n'existe aucun operateur en dehors de cette liste : la base ne stocke ni
// script, ni expression, ni code interprete.

public sealed record DiagnosticConditionModel(
    string? QuestionId,
    string? Operator,
    IReadOnlyList<string>? Values);

public sealed record DiagnosticQuestionOptionModel(
    string? Value,
    string? Label,
    bool Exclusive);

public sealed record DiagnosticQuestionVisibilityModel(
    string? QuestionId,
    IReadOnlyList<string>? Values);

public sealed record DiagnosticQuestionModel(
    string? Id,
    string? Legend,
    string? SummaryLabel,
    string? Mode,
    string? Hint,
    DiagnosticQuestionVisibilityModel? When,
    IReadOnlyList<DiagnosticQuestionOptionModel>? Options);

public sealed record DiagnosticGuidanceRuleModel(
    string? Id,
    IReadOnlyList<DiagnosticConditionModel>? When,
    string? Title,
    string? Body,
    IReadOnlyList<string>? Points);

public sealed record DiagnosticBillingMappingModel(
    IReadOnlyList<DiagnosticConditionModel>? RequireAll,
    string? UsersQuestionId,
    string? StructureQuestionId,
    string? StorageQuestionId,
    string? RestoreTestQuestionId,
    IReadOnlyList<DiagnosticConditionModel>? NeedsRemoteFilesWhen,
    IReadOnlyList<DiagnosticConditionModel>? NeedsVpnWhen,
    IReadOnlyList<DiagnosticConditionModel>? NeedsWindowsDesktopWhen,
    string? IndividualDataKind,
    string? OrganisationDataKind);

public sealed record DiagnosticContextModel(
    string? Id,
    string? Label,
    string? Eyebrow,
    string? Title,
    string? Intro,
    string? ContactSubject,
    bool FormulaEligible,
    IReadOnlyList<DiagnosticQuestionModel>? Questions,
    IReadOnlyList<DiagnosticGuidanceRuleModel>? Guidance,
    DiagnosticBillingMappingModel? BillingMapping);

public sealed record DiagnosticConfigurationModel(
    int SchemaVersion,
    IReadOnlyList<DiagnosticContextModel>? Contexts);

/// <summary>
/// Etat d'une configuration. <c>Source</c> vaut <c>code</c> tant qu'aucune
/// version n'est enregistree en base, <c>database</c> ensuite.
/// </summary>
public sealed record DiagnosticConfigurationSnapshotItem(
    string State,
    int Version,
    string Source,
    string? UpdatedAt,
    JsonElement? Configuration);

public sealed record DiagnosticConfigurationAdminViewResponse(
    DiagnosticConfigurationSnapshotItem Draft,
    DiagnosticConfigurationSnapshotItem Published,
    bool DraftDiffers,
    bool Persistent);

public sealed record DiagnosticConfigurationUpdateRequest(
    JsonElement Configuration,
    int ExpectedVersion);

public sealed record DiagnosticConfigurationValidateRequest(JsonElement Configuration);

public sealed record DiagnosticConfigurationPublishRequest(
    int ExpectedDraftVersion,
    int ExpectedPublishedVersion);

public sealed record DiagnosticConfigurationMutationResponse(
    string Code,
    string Message,
    IReadOnlyList<string> Errors,
    DiagnosticConfigurationAdminViewResponse? View,
    string CorrelationId);

public sealed record DiagnosticConfigurationRevisionItemResponse(
    string State,
    int Version,
    string Outcome,
    string? ActorUserId,
    string CorrelationId,
    string CreatedAt);

/// <summary>
/// Version publiee exposee au portail public. `configuration` vaut `null`
/// tant qu'aucune version n'a ete publiee : le WebPortal retombe alors sur la
/// configuration integree a son code, jamais sur un parcours vide.
/// </summary>
public sealed record PublicDiagnosticConfigurationResponse(
    int Version,
    string Source,
    string? UpdatedAt,
    JsonElement? Configuration);
