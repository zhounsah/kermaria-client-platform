namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed class BillingV2AdditionalUserIdentityConvergenceWorker : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingV2AdditionalUserIdentityConvergenceWorker> _logger;

    public BillingV2AdditionalUserIdentityConvergenceWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BillingV2AdditionalUserIdentityConvergenceWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Billing V2 USER-ADDITIONAL convergence worker started: batch={BatchSize}, interval_seconds={IntervalSeconds}.",
            BatchSize,
            PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var convergence = scope.ServiceProvider.GetRequiredService<
                    IBillingV2AdditionalUserIdentityConvergenceService>();
                var completed = await convergence.ConvergePendingAsync(
                    BatchSize,
                    stoppingToken);
                if (completed > 0)
                {
                    _logger.LogInformation(
                        "Billing V2 USER-ADDITIONAL convergence completed {Count} lifecycle(s).",
                        completed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Billing V2 additional-user convergence pass failed; the next pass will retry.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
