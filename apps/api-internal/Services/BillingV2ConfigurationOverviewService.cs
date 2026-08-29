using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services;

public interface IBillingV2ConfigurationOverviewService
{
    Task<BillingV2ConfigurationOverview> GetAsync(
        string correlationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resume Billing V2 pour le Centre de configuration.
///
/// Il ne duplique aucune autorite : le catalogue reste administre dans
/// `/admin/catalog`, la readiness dans `/admin/billing-v2`. Cette vue federe ce
/// qui existe deja et le presente avec les drapeaux, pour qu'un exploitant
/// puisse lire l'etat commercial sans naviguer entre trois pages.
/// </summary>
public sealed class BillingV2ConfigurationOverviewService
    : IBillingV2ConfigurationOverviewService
{
    private readonly IBillingV2CatalogAdministrationService _catalog;
    private readonly IBillingV2AdminReadinessService _readiness;
    private readonly BillingV2RuntimeConfiguration _configuration;
    private readonly ILogger<BillingV2ConfigurationOverviewService> _logger;

    public BillingV2ConfigurationOverviewService(
        IBillingV2CatalogAdministrationService catalog,
        IBillingV2AdminReadinessService readiness,
        BillingV2RuntimeConfiguration configuration,
        ILogger<BillingV2ConfigurationOverviewService> logger)
    {
        _catalog = catalog;
        _readiness = readiness;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BillingV2ConfigurationOverview> GetAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        // Les drapeaux sont lus en memoire : ils restent affichables meme si la
        // base est indisponible, ce qui est justement le moment ou l'exploitant
        // a besoin de savoir ce qui est arme.
        var flags = BillingV2FeatureFlagRegistry.Describe(_configuration);

        BillingV2CatalogSummary? catalog = null;
        try
        {
            var snapshot = await _catalog.GetCatalogAsync(cancellationToken);
            catalog = new BillingV2CatalogSummary(
                snapshot.Source,
                snapshot.Editable,
                snapshot.Currency,
                snapshot.Services.Count,
                snapshot.Services.Count(service =>
                    string.Equals(service.Status, "active", StringComparison.Ordinal)),
                snapshot.Presets.Count,
                snapshot.Presets.Count(preset =>
                    string.Equals(preset.Status, "active", StringComparison.Ordinal)),
                snapshot.Commitments.Count);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 catalog summary unavailable correlation_id {CorrelationId}",
                correlationId);
        }

        BillingV2ReadinessSummary? readiness = null;
        try
        {
            var snapshot = await _readiness.CheckAsync(correlationId, cancellationToken);
            readiness = new BillingV2ReadinessSummary(
                snapshot.PersistentSqlAvailable,
                snapshot.SchemaReady,
                snapshot.CanRequestFirstRealSubscription,
                snapshot.ReasonCode,
                snapshot.Providers,
                snapshot.OperationalLimitations);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Billing V2 readiness unavailable correlation_id {CorrelationId}",
                correlationId);
        }

        return new BillingV2ConfigurationOverview(
            catalog,
            readiness,
            flags,
            _configuration.ReconciliationIntervalSeconds,
            correlationId);
    }
}
