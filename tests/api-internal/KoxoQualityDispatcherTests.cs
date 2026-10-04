using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

internal static class KoxoQualityDispatcherTests
{
    public static async Task RunAsync()
    {
        var intent = new KoxoQualityIntent(2, 1, "{}");
        var lease = new KoxoQualityLease("customer-a", intent, "lease-a", 1);
        var valid = new KoxoQualityEvidence(lease.CustomerId, 2, KoxoQualityDispatcher.Digest(intent), true, true, true);
        foreach (var evidence in new KoxoQualityEvidence?[] {
            null, valid with { CsvVerified=false }, valid with { KoxoVerified=false }, valid with { AdVerified=false },
            valid with { CustomerId="customer-b" }, valid with { Revision=1 }, valid with { DesiredSha256="wrong" } })
        {
            var repository = new Repository(lease);
            await new KoxoQualityDispatcher(repository, new Executor((_, _) => Task.FromResult(evidence))).DispatchOneAsync(default);
            Check(repository.Acknowledged == 0 && repository.LastCode == "KOXO_QUALITIES_UNVERIFIED", "Partial or foreign proof must remain pending");
        }
        var success = new Repository(lease);
        await new KoxoQualityDispatcher(success, new Executor((_, _) => Task.FromResult<KoxoQualityEvidence?>(valid))).DispatchOneAsync(default);
        Check(success.Acknowledged == 1, "Exact triple proof is acknowledged");

        var failure = new Repository(lease);
        await new KoxoQualityDispatcher(failure, new Executor((_, _) => throw new InvalidOperationException("sensitive error"))).DispatchOneAsync(default);
        Check(failure.LastCode == "KOXO_QUALITIES_EXECUTION_FAILED" && failure.Acknowledged == 0, "Failure is retryable and sanitized");

        var stale = new Repository(lease) { Current = intent with { Revision=3 } };
        var unused = new Executor((_, _) => throw new InvalidOperationException("Must not execute"));
        await new KoxoQualityDispatcher(stale, unused).DispatchOneAsync(default);
        Check(unused.Calls == 0 && stale.Acknowledged == 0, "Superseded lease must not trigger external work");

        var concurrent = new Repository(lease);
        await new KoxoQualityDispatcher(concurrent, new Executor((_, _) => {
            concurrent.Current = intent with { Revision=3 };
            return Task.FromResult<KoxoQualityEvidence?>(valid);
        })).DispatchOneAsync(default);
        Check(concurrent.Acknowledged == 0 && concurrent.Current.Revision == 3, "Late proof must not complete a newer revision");

        var expired = new Repository(lease) { LeaseValid=false };
        await new KoxoQualityDispatcher(expired, new Executor((_, _) => Task.FromResult<KoxoQualityEvidence?>(valid))).DispatchOneAsync(default);
        Check(expired.Acknowledged == 0, "Expired lease cannot acknowledge");

        using var cancellation = new CancellationTokenSource();
        var cancelled = new Repository(lease);
        try
        {
            await new KoxoQualityDispatcher(cancelled, new Executor((_, token) => {
                cancellation.Cancel(); token.ThrowIfCancellationRequested();
                return Task.FromResult<KoxoQualityEvidence?>(valid);
            })).DispatchOneAsync(cancellation.Token);
            throw new InvalidOperationException("Cancellation swallowed");
        }
        catch (OperationCanceledException) { }
        Check(cancelled.LastCode is null && cancelled.Acknowledged == 0, "Shutdown leaves lease to expire, without false proof");
        Check(!await new KoxoQualityDispatcher(success, new Executor((_, _) => throw new Exception())).DispatchOneAsync(default),
            "An empty queue does not invoke the executor");
        Console.WriteLine("KoXo quality dispatcher tests passed.");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private sealed class Executor(Func<KoxoQualityLease, CancellationToken, Task<KoxoQualityEvidence?>> action) : IKoxoQualityIntentExecutor
    {
        public int Calls { get; private set; }
        public Task<KoxoQualityEvidence?> ExecuteAndVerifyAsync(KoxoQualityLease lease, CancellationToken token)
        { Calls++; return action(lease, token); }
    }

    private sealed class Repository(KoxoQualityLease lease) : IKoxoQualityIntentRepository
    {
        public KoxoQualityIntent Current { get; set; } = lease.Intent;
        public int Acknowledged { get; private set; }
        public bool LeaseValid { get; set; } = true;
        public string? LastCode { get; private set; }
        private bool _claimed;
        public Task<KoxoQualityIntent?> ReadAsync(string customerId, CancellationToken token) => Task.FromResult<KoxoQualityIntent?>(Current);
        public Task<KoxoQualityLease?> ClaimNextAsync(CancellationToken token)
        { var next = _claimed ? null : lease; _claimed=true; return Task.FromResult(next); }
        public Task<bool> FinishAsync(KoxoQualityLease claim, bool verified, string resultCode, CancellationToken token)
        {
            LastCode=resultCode;
            if (!LeaseValid || claim.Token != lease.Token || claim.Intent.Revision != Current.Revision) return Task.FromResult(false);
            if (verified) Acknowledged++;
            return Task.FromResult(true);
        }
        public Task<KoxoQualityIntentPublication> PublishAsync(string customerId, long expectedRevision,
            IReadOnlyDictionary<string, IReadOnlyList<string>> qualities, CancellationToken token) => throw new NotSupportedException();
    }
}
