namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Service de fond (SRV-13) du cycle de vie des comptes de demonstration (Lot 3).
/// Detecte les echeances <c>demo_expires_at</c> au demarrage puis periodiquement,
/// revoque l'acces reel des essais echus (retrait GG_DEMO_* + desactivation AD)
/// et purge les comptes echus. Double d'une tache planifiee Windows (filet de
/// securite) qui invoque le meme balayage via <c>--run-demo-expiration</c>.
/// </summary>
public sealed class DemoAccountExpirationWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DemoAccountExpirationWorker> _logger;

    public DemoAccountExpirationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<DemoAccountExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        await RunSweepAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSweepAsync(stoppingToken);
        }
    }

    private async Task RunSweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IDemoAccountService>();
        if (!service.IsPersistent)
        {
            return;
        }

        try
        {
            var result = await service.RunExpirationSweepAsync(cancellationToken);
            if (result.RevokedCount > 0
                || result.PurgedCount > 0
                || result.SkippedReferences.Count > 0
                || result.RevokeFailures.Count > 0)
            {
                _logger.LogInformation(
                    "Demo expiration sweep: revoked={Revoked} purged={Purged} skipped={Skipped} revokeFailures={Failures}",
                    result.RevokedCount,
                    result.PurgedCount,
                    result.SkippedReferences.Count,
                    result.RevokeFailures.Count);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Demo expiration sweep failed; will retry on next tick.");
        }
    }
}
