namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Fait converger les amorcages d'identite AD des comptes principaux.
/// </summary>
/// <remarks>
/// La creation par KoXo est asynchrone : l'adoption par employeeNumber et
/// l'ecriture du lien ne peuvent donc pas etre garanties dans la requete qui
/// pose le mot de passe ou verifie l'adresse. Ce passage periodique les reprend,
/// sans jamais creer ni transferer d'identite hors des regles du service.
/// </remarks>
public sealed class PrimaryIdentityBootstrapConvergenceWorker : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PrimaryIdentityBootstrapConvergenceWorker> _logger;

    public PrimaryIdentityBootstrapConvergenceWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PrimaryIdentityBootstrapConvergenceWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Primary identity bootstrap convergence worker started: batch={BatchSize}, interval_seconds={IntervalSeconds}.",
            BatchSize,
            PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var signup = scope.ServiceProvider.GetRequiredService<ISignupService>();
                var completed = await signup.ConvergePendingPrimaryIdentitiesAsync(
                    BatchSize,
                    stoppingToken);
                if (completed > 0)
                {
                    _logger.LogInformation(
                        "Primary identity bootstrap convergence completed {Count} identity(ies).",
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
                    "Primary identity bootstrap convergence pass failed; the next pass will retry.");
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
