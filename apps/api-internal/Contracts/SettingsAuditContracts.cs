namespace Kermaria.ApiInternal.Contracts;

public sealed record SettingsAuditActionView(
    string Action,
    string Label,
    string Category,
    string Risk);

public sealed record SettingsAuditCategoryView(
    string Key,
    string Label);

public sealed record SettingsAuditEntryView(
    string OccurredAt,
    string Actor,
    string Action,
    string ActionLabel,
    string Category,
    string Risk,
    string Outcome,
    string? ReasonCode,
    string? TargetType,
    string? TargetReference,
    string CorrelationId,
    string? SourceAddress);

public sealed record SettingsAuditFilterEcho(
    string? From,
    string? To,
    string? Actor,
    string? Category,
    string? Risk,
    string? Outcome,
    string? CorrelationId,
    string? Target,
    int Limit);

public sealed record SettingsAuditView(
    IReadOnlyList<SettingsAuditEntryView> Entries,
    IReadOnlyList<SettingsAuditActionView> Actions,
    IReadOnlyList<SettingsAuditCategoryView> Categories,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Outcomes,
    SettingsAuditFilterEcho Filters,
    bool Persistent,
    bool Truncated,
    string? Warning);

public sealed record SettingsPermissionView(
    string Code,
    string Label,
    string Description,
    string Risk,
    IReadOnlyList<string> Surfaces,
    // "granted" | "open" : « open » signifie qu'aucune attribution n'existe
    // encore pour ce code, donc que l'acces est ouvert par amorcage.
    string State,
    int GrantCount);

public sealed record SettingsPermissionOverview(
    IReadOnlyList<SettingsPermissionView> Permissions,
    bool BootstrapOpen,
    string Notice);
