namespace Kermaria.ApiInternal.Contracts;

/// <summary>
/// Autorite fonctionnelle d'une operation d'annuaire (specification, 12.1).
/// L'autorite n'est pas une capacite technique : elle dit qui a le mandat, pas
/// qui saurait le faire.
/// </summary>
public sealed record DirectoryAuthorityItem(
    string Operation,
    string Authority,
    string Note);

public sealed record DirectoryPolicyItem(
    string Key,
    string Label,
    string Value,
    string Classification,
    bool RestartRequired,
    bool Sensitive);

public sealed record DirectoryWriteEntry(
    string OccurredAt,
    string Operation,
    // "api_internal" : la seule valeur possible aujourd'hui. Les ecritures KoXo
    // ne passent pas par cette table ; les inventer ici serait faux.
    string Engine,
    string? Actor,
    string Workflow,
    string? CustomerReference,
    string TargetReference,
    string Status,
    string? ResultCode,
    bool? Changed,
    string CorrelationId);

public sealed record DirectoryOverview(
    string Mode,
    bool ConfigurationValid,
    string State,
    string? Warning,
    IReadOnlyList<DirectoryAuthorityItem> Authorities,
    IReadOnlyList<DirectoryPolicyItem> Policies,
    IReadOnlyList<string> AllowedRoots,
    IReadOnlyList<DirectoryWriteEntry> Writes,
    bool WritesPersistent,
    string WritesNotice);
