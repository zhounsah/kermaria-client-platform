namespace Kermaria.ApiInternal.Contracts;

/// <summary>
/// Un fait observable d'une integration.
/// </summary>
/// <param name="Kind">
/// <c>value</c> : donnee non sensible affichable telle quelle.
/// <c>state</c> : etat derive (Active / Configure / Non configure).
/// <c>secret</c> : le secret n'est jamais transporte, seule sa presence l'est.
/// </param>
public sealed record IntegrationFact(
    string Label,
    string Value,
    string Kind = "value");

/// <summary>
/// Operation de test proposee pour une integration. Une operation absente
/// porte la raison de son absence : l'administration doit comprendre qu'il n'y
/// a pas de test plutot que de croire qu'il a reussi.
/// </summary>
public sealed record IntegrationOperation(
    string Key,
    string Label,
    string Description,
    bool Available,
    string? UnavailableReason);

public sealed record IntegrationView(
    string Key,
    string Label,
    string Mode,
    bool Configured,
    // "healthy" | "warning" | "critical" | "info" — meme vocabulaire que le
    // panneau d'etat du Centre de configuration.
    string State,
    string? Warning,
    // Rappel du risque quand le mode engage des operations reelles.
    string? RiskNote,
    IReadOnlyList<IntegrationFact> Facts,
    IReadOnlyList<IntegrationOperation> Operations,
    string? LastSuccessAt,
    string? LastErrorAt,
    string? LastErrorSummary);

public sealed record IntegrationsOverview(
    IReadOnlyList<IntegrationView> Integrations,
    string CheckedAt);

public sealed record IntegrationTestRequest(string? Recipient);

public sealed record IntegrationTestResponse(
    string Code,
    string Message,
    string CorrelationId);
