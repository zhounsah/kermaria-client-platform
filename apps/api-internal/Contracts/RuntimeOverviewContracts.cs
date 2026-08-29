namespace Kermaria.ApiInternal.Contracts;

/// <summary>
/// Une ligne de la vue runtime consolidee (specification, section 17.5).
/// </summary>
/// <param name="Value">
/// Valeur non sensible, ou etat derive. Un secret n'apparait jamais : seule sa
/// presence est publiee.
/// </param>
/// <param name="Source">
/// <c>environment</c>, <c>json</c>, <c>default</c> ou <c>database</c>. Le but
/// est de rendre l'exploitation comprehensible, pas d'exposer le contenu brut du
/// fichier de configuration.
/// </param>
public sealed record RuntimeParameterItem(
    string Key,
    string Label,
    string Value,
    string Source,
    string Classification,
    bool RestartRequired,
    bool Sensitive,
    string? LastChangedAt);

public sealed record RuntimeSectionView(
    string Key,
    string Label,
    string State,
    string? Warning,
    IReadOnlyList<RuntimeParameterItem> Parameters);

public sealed record RuntimeOverview(
    string Environment,
    string Version,
    string? ConfigurationPath,
    bool ConfigurationFilePresent,
    string StartedAt,
    long UptimeSeconds,
    IReadOnlyList<RuntimeSectionView> Sections);
