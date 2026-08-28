namespace Kermaria.ApiInternal.Contracts;

/// <summary>Etat exploitable d'un domaine de configuration, sans valeur secrete.</summary>
public sealed record ConfigurationStatusDomain(
    string Key,
    string Label,
    string State,
    IReadOnlyList<ConfigurationStatusFact> Facts,
    string? Warning = null);

public sealed record ConfigurationStatusFact(string Label, string Value, bool Sensitive = false);

public sealed record ConfigurationStatusSnapshot(IReadOnlyList<ConfigurationStatusDomain> Domains);
