using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services.Provisioning;

// Enregistre avant le worker. Un schema absent empeche le demarrage de la
// fonctionnalite ; aucune creation/migration automatique dans une requete.
public sealed class KoxoQualitySchemaGate(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IKoxoQualityIntentRepository>();
        _ = await repository.ReadAsync(Guid.Empty.ToString("D"), cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
