using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

internal static class KoxoAdditionalQualitiesTests
{
    public static void Run()
    {
        KoxoQualityIntentTests.Run();
        var owned = new[] { ("alice", "GG_VPN"), ("alice", "gg_vpn"), ("alice", "GG_RDS"), ("bob", "GG_RDS") };
        var result = KoxoAdditionalQualitiesProjection.FromManagedMemberships(owned);
        Check(result["alice"].SequenceEqual(["GG_RDS", "GG_VPN"]), "Keep the approved baseline, with no duplicate or untracked rights");
        Check(result["bob"].SequenceEqual(["GG_RDS"]), "Rights remain identity scoped");
        result = KoxoAdditionalQualitiesProjection.FromManagedMemberships([]);
        Check(result.Count == 0, "No managed active membership means no baseline grant");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}

internal sealed class TestKoxoQualitiesProvider : IKoxoAdditionalQualitiesProvider
{
    public int Calls { get; private set; }
    public bool ThrowOnCall { get; set; }
    public string[] Groups { get; set; } = ["GG_VPN", "GG_RDS", "gg_vpn"];
    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetByIdentityAsync(
        string customerId, CancellationToken cancellationToken)
    {
        Calls++;
        if (ThrowOnCall) throw new InvalidOperationException("Production must not call this provider");
        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            new Dictionary<string, IReadOnlyList<string>> { ["portal-quality"] = Groups });
    }
}
