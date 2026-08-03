using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Implementation non persistante : le mode mock ne materialise pas de comptes
/// de demo (creation/liste/purge sont inertes).
/// </summary>
public sealed class MockDemoAccountRepository : IDemoAccountRepository
{
    public Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task CreateDemoAccountAsync(
        DemoAccountCreationSpec spec,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> CustomerReferenceTakenAsync(
        string reference,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task SetKoxoGroupReferenceAsync(
        string customerId,
        string groupReference,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<DemoAccountSummary>> ListDemoAccountsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DemoAccountSummary>>(
            Array.Empty<DemoAccountSummary>());

    public Task<IReadOnlyList<DemoExpiredTrial>> ListExpiredTrialsToRevokeAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DemoExpiredTrial>>(
            Array.Empty<DemoExpiredTrial>());

    public Task MarkTrialProvisionedAsync(
        string customerId,
        DateTime provisionedAtUtc,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task MarkTrialRevokedAsync(
        string customerId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<DemoTrialProvisioningTarget>>
        ListTrialsForProvisioningRetryAsync(
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DemoTrialProvisioningTarget>>(
            Array.Empty<DemoTrialProvisioningTarget>());

    public Task<DemoConversionCandidate?> FindConversionCandidateAsync(
        string customerReference,
        CancellationToken cancellationToken = default)
        => Task.FromResult<DemoConversionCandidate?>(null);

    public Task MarkConvertedAsync(
        string customerId,
        DateTime convertedAtUtc,
        string? actorUserId,
        string? sourceProfileKey,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<DemoPurgeResult> PurgeExpiredDemoCustomersAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult(
            new DemoPurgeResult(0, Array.Empty<string>()));
}
