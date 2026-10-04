using System.Security.Cryptography;
using System.Text;
using Kermaria.ApiInternal.Data.Repositories;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record KoxoQualityLease(string CustomerId, KoxoQualityIntent Intent, string Token, int Attempt);
public sealed record KoxoQualityEvidence(string CustomerId, long Revision, string DesiredSha256,
    bool CsvVerified, bool KoxoVerified, bool AdVerified);

public interface IKoxoQualityIntentExecutor
{
    // Declenche ou relit la synchronisation. Une reception HTTP 202 ne suffit
    // pas : l'implementation doit etablir les trois preuves pour ce document.
    Task<KoxoQualityEvidence?> ExecuteAndVerifyAsync(KoxoQualityLease lease, CancellationToken cancellationToken);
}

public sealed class KoxoQualityDispatcher(IKoxoQualityIntentRepository repository, IKoxoQualityIntentExecutor executor)
{
    public static string Digest(KoxoQualityIntent intent)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(intent.DesiredJson)));

    public static bool Matches(KoxoQualityLease lease, KoxoQualityEvidence? evidence)
        => evidence is { CsvVerified: true, KoxoVerified: true, AdVerified: true }
           && evidence.CustomerId == lease.CustomerId && evidence.Revision == lease.Intent.Revision
           && string.Equals(evidence.DesiredSha256, Digest(lease.Intent), StringComparison.Ordinal);

    public async Task<bool> DispatchOneAsync(CancellationToken cancellationToken)
    {
        var lease = await repository.ClaimNextAsync(cancellationToken);
        if (lease is null) return false;
        try
        {
            var current = await repository.ReadAsync(lease.CustomerId, cancellationToken);
            if (current?.Revision != lease.Intent.Revision || current.DesiredJson != lease.Intent.DesiredJson)
            {
                await repository.FinishAsync(lease, false, "KOXO_QUALITIES_SUPERSEDED", cancellationToken);
                return true;
            }
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bounded.CancelAfter(TimeSpan.FromSeconds(60));
            var evidence = await executor.ExecuteAndVerifyAsync(lease, bounded.Token);
            var verified = Matches(lease, evidence);
            // Le depot compare encore revision + lease + expiration : une
            // modification pendant l'appel externe rend cet accuse inoperant.
            await repository.FinishAsync(lease, verified,
                verified ? "KOXO_QUALITIES_APPLIED" : "KOXO_QUALITIES_UNVERIFIED", cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Ne jamais persister les messages externes, potentiellement sensibles.
            await repository.FinishAsync(lease, false, "KOXO_QUALITIES_EXECUTION_FAILED", cancellationToken);
        }
        return true;
    }
}

public sealed class KoxoQualityWorker(IServiceScopeFactory scopes, ILogger<KoxoQualityWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<KoxoQualityDispatcher>();
                for (var count = 0; count < 10 && await dispatcher.DispatchOneAsync(stoppingToken); count++) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("KoXo quality dispatch failed; durable requests remain pending."); }
            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
