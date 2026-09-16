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

// Schema v2 du pré-diagnostic. Les identifiants et structures sont fermes;
// aucun de ces objets ne contient d'expression executable ou de prix.
public sealed record PreDiagnosticProfileModel(
    string? Id, string? Label, string? Description, bool Active, int Order);
public sealed record PreDiagnosticScoreEffectModel(
    string? Mode, int Value, IReadOnlyList<DiagnosticConditionModel>? When);
public sealed record PreDiagnosticOptionModel(
    string? Value, string? Label, bool Active, int Order,
    IReadOnlyList<PreDiagnosticScoreEffectModel>? Effects);
public sealed record PreDiagnosticCategoryModel(
    string? Id, string? Label, bool Active, int Order,
    IReadOnlyDictionary<string, int>? Weights);
public sealed record PreDiagnosticQuestionModel(
    string? Id, string? CategoryId, string? Label, string? Hint,
    IReadOnlyList<string>? Profiles, bool Required, bool Active, int Order,
    IReadOnlyList<DiagnosticConditionModel>? When,
    IReadOnlyList<PreDiagnosticOptionModel>? Options);
public sealed record PreDiagnosticLevelModel(
    string? Id, string? Label, int MinimumScore, int Order, string? Description);
public sealed record PreDiagnosticPriorityModel(
    string? CategoryId, int Threshold, string? Title, string? Body, int Order);
public sealed record PreDiagnosticPositiveModel(
    string? CategoryId, int Threshold, string? Text, int Order);
public sealed record PreDiagnosticContextModel(
    string? Id, string? Label, string? Text, bool Active, int Order,
    bool AllowsSelfService);
public sealed record PreDiagnosticCommercialProfileModel(
    string? Id, string? Label, bool Active, IReadOnlyList<string>? Intents,
    IReadOnlyList<string>? Scopes);
public sealed record PreDiagnosticCommercialOptionModel(
    string? Value, string? Label, bool Active, int Order,
    IReadOnlyList<string>? Profiles, IReadOnlyList<string>? Contexts,
    // The shared TypeScript contract deliberately reuses the option shape for
    // health and commercial questions. Commercial options must not carry score
    // effects, but retaining the (empty) array here lets the v2 document stay
    // strictly deserializable on both sides.
    IReadOnlyList<PreDiagnosticScoreEffectModel>? Effects);
public sealed record PreDiagnosticCommercialQuestionModel(
    string? Id, string? Label, string? Hint, IReadOnlyList<string>? Profiles,
    IReadOnlyList<string>? Contexts, bool Active, int Order,
    string? DynamicOptions,
    IReadOnlyList<PreDiagnosticCommercialOptionModel>? Options);
public sealed record PreDiagnosticCatalogBindingModel(
    string? ProfileId, IReadOnlyList<string>? RequiredServiceCodes,
    string? StorageServiceCode);
public sealed record PreDiagnosticCommerceModel(
    int MinimumStorageGb, int MaximumStorageGb, int MinimumUsers, int MaximumUsers,
    int MaximumSites, IReadOnlyList<string>? CompatibleScopes,
    IReadOnlyList<string>? HumanReviewIntents,
    IReadOnlyList<PreDiagnosticCommercialQuestionModel>? Questions,
    IReadOnlyList<PreDiagnosticCommercialProfileModel>? Profiles,
    IReadOnlyList<PreDiagnosticCatalogBindingModel>? CatalogBindings);
public sealed record PreDiagnosticConfigurationModel(
    int SchemaVersion,
    IReadOnlyList<PreDiagnosticProfileModel>? Profiles,
    IReadOnlyList<PreDiagnosticCategoryModel>? Categories,
    IReadOnlyList<PreDiagnosticQuestionModel>? Questions,
    IReadOnlyList<PreDiagnosticLevelModel>? Levels,
    int MaximumPriorities,
    IReadOnlyList<PreDiagnosticPriorityModel>? Priorities,
    IReadOnlyList<PreDiagnosticPositiveModel>? Positives,
    IReadOnlyList<PreDiagnosticContextModel>? Contexts,
    PreDiagnosticCommerceModel? Commerce);

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
