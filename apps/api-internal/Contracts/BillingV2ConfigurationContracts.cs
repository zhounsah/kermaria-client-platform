namespace Kermaria.ApiInternal.Contracts;

/// <summary>
/// Un drapeau Billing V2 presente avec son contexte : ce que l'exploitant doit
/// savoir avant de decider, pas seulement son etat.
/// </summary>
public sealed record BillingV2FeatureFlagItem(
    string Key,
    string EnvironmentVariable,
    string Label,
    string Description,
    bool Enabled,
    string Risk,
    IReadOnlyList<string> Dependencies,
    // Dependances actuellement fermees alors que le drapeau est ouvert : la
    // fonction est annoncee active mais ne peut rien produire.
    IReadOnlyList<string> UnsatisfiedDependencies,
    bool RestartRequired,
    string Classification,
    string Source);

public sealed record BillingV2CatalogSummary(
    string Source,
    bool Editable,
    string Currency,
    int ServiceCount,
    int ActiveServiceCount,
    int PresetCount,
    int ActivePresetCount,
    int CommitmentCount);

public sealed record BillingV2ReadinessSummary(
    bool PersistentSqlAvailable,
    bool SchemaReady,
    bool CanRequestFirstRealSubscription,
    string ReasonCode,
    IReadOnlyList<BillingV2AdminProviderReadiness> Providers,
    IReadOnlyList<BillingV2AdminOperationalLimitation> Limitations);

public sealed record BillingV2ConfigurationOverview(
    BillingV2CatalogSummary? Catalog,
    BillingV2ReadinessSummary? Readiness,
    IReadOnlyList<BillingV2FeatureFlagItem> Flags,
    int ReconciliationIntervalSeconds,
    string CorrelationId);
