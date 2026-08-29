namespace Kermaria.ApiInternal.Contracts;

/// <summary>
/// Une version datee de la mention fiscale d'un regime.
/// </summary>
public sealed record FiscalMentionVersionItem(
    string Id,
    string Regime,
    string Mention,
    string EffectiveFrom,
    string CreatedAt,
    string? CreatedByUserId,
    bool Active,
    bool Scheduled);

public sealed record FiscalPolicyRegimeView(
    string Regime,
    string Label,
    string Description,
    // Mention integree au code : c'est le repli, et la valeur appliquee tant
    // qu'aucune version n'a ete enregistree.
    string DefaultMention,
    string ActiveMention,
    string? ActiveEffectiveFrom,
    // "code" ou "database" : l'administration doit voir d'ou vient le texte
    // reellement applique aujourd'hui.
    string ActiveSource,
    int Version,
    IReadOnlyList<FiscalMentionVersionItem> Versions);

public sealed record FiscalPolicyAdminView(
    IReadOnlyList<FiscalPolicyRegimeView> Regimes,
    bool Persistent);

public sealed record FiscalMentionCreateRequest(
    string? Regime,
    string? Mention,
    // Date d'effet en ISO 8601 UTC. Une date passee est refusee : une mention
    // ne doit jamais modifier un document deja emis.
    string? EffectiveFrom,
    int ExpectedVersion);

public sealed record FiscalPolicyMutationResponse(
    string Code,
    string Message,
    FiscalPolicyAdminView? View,
    string CorrelationId);
