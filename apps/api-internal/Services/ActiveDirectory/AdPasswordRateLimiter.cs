using System.Collections.Concurrent;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.ActiveDirectory;

public enum AdPasswordRateLimitDecision
{
    Allowed,
    Locked
}

public interface IAdPasswordRateLimiter
{
    AdPasswordRateLimitDecision CheckUser(string userId, DateTime nowUtc);
    void RegisterFailure(string userId, DateTime nowUtc);
    void Reset(string userId);
}

public sealed class AdPasswordRateLimiter : IAdPasswordRateLimiter
{
    private readonly AdPasswordRuntimeConfiguration _configuration;
    private readonly ConcurrentDictionary<string, RateLimitState> _states = new(
        StringComparer.OrdinalIgnoreCase);

    public AdPasswordRateLimiter(AdPasswordRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AdPasswordRateLimitDecision CheckUser(
        string userId,
        DateTime nowUtc)
    {
        if (!_states.TryGetValue(userId, out var state))
        {
            return AdPasswordRateLimitDecision.Allowed;
        }

        lock (state)
        {
            if (state.LockedUntilUtc is not null
                && state.LockedUntilUtc > nowUtc)
            {
                return AdPasswordRateLimitDecision.Locked;
            }

            PruneExpired(state, nowUtc);
            return AdPasswordRateLimitDecision.Allowed;
        }
    }

    public void RegisterFailure(string userId, DateTime nowUtc)
    {
        var state = _states.GetOrAdd(userId, _ => new RateLimitState());
        lock (state)
        {
            PruneExpired(state, nowUtc);
            state.FailureTimestamps.Add(nowUtc);
            if (state.FailureTimestamps.Count >= _configuration.MaxFailuresPer15Minutes)
            {
                state.LockedUntilUtc = nowUtc.Add(_configuration.LockoutDuration);
                state.FailureTimestamps.Clear();
            }
        }
    }

    public void Reset(string userId)
    {
        _states.TryRemove(userId, out _);
    }

    private void PruneExpired(RateLimitState state, DateTime nowUtc)
    {
        var cutoff = nowUtc.Subtract(_configuration.FailureWindow);
        state.FailureTimestamps.RemoveAll(timestamp => timestamp < cutoff);
        if (state.LockedUntilUtc is not null
            && state.LockedUntilUtc <= nowUtc)
        {
            state.LockedUntilUtc = null;
        }
    }

    private sealed class RateLimitState
    {
        public List<DateTime> FailureTimestamps { get; } = new();
        public DateTime? LockedUntilUtc { get; set; }
    }
}
