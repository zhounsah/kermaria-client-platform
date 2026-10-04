using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

internal static class KoxoQualityIntentTests
{
    public static void Run()
    {
        const string alice = "00000000-0000-0000-0000-000000000001";
        const string bob = "00000000-0000-0000-0000-000000000002";
        Check(KoxoQualityIntentPolicy.TransportGroups([], "GG_NO_ACCESS_E2E_DEV").SequenceEqual(["GG_NO_ACCESS_E2E_DEV"]),
            "A configured neutral group represents an empty desired access set");
        Check(KoxoQualityIntentPolicy.TransportGroups(["GG_VPN_E2E_DEV"], "GG_NO_ACCESS_E2E_DEV").SequenceEqual(["GG_VPN_E2E_DEV"]),
            "Neutral group must disappear when real access is desired again");
        Check(KoxoQualityIntentPolicy.TransportGroups([], null).Count == 0, "Default transport remains unchanged");
        var first = KoxoQualityIntentPolicy.Serialize(new Dictionary<string, IReadOnlyList<string>>
        {
            [bob] = [], [alice] = ["gg_vpn", "GG_RDS", "GG_VPN"]
        });
        var reordered = KoxoQualityIntentPolicy.Serialize(new Dictionary<string, IReadOnlyList<string>>
        {
            [alice] = ["GG_VPN", "gg_rds"], [bob] = []
        });
        Check(first == reordered, "Semantic replay must be canonical");
        var restored = KoxoQualityIntentPolicy.Deserialize(first);
        Check(restored[alice].SequenceEqual(["GG_RDS", "GG_VPN"]) && restored[bob].Count == 0,
            "Durable export preserves identity boundaries and explicit empty lists");
        var created = KoxoQualityIntentPolicy.Publish(null, 0, first);
        Check(created.Accepted && created.Changed && created.Intent is { Revision: 1, AppliedRevision: 0, Pending: true },
            "Publishing an intent must never claim application");
        var replay = KoxoQualityIntentPolicy.Publish(created.Intent, 1, reordered);
        Check(replay.Accepted && !replay.Changed && replay.Intent == created.Intent, "Replay preserves pending revision");
        Check(!KoxoQualityIntentPolicy.Publish(created.Intent, 0, first).Accepted, "Stale writer rejected even on replay");
        var applied = created.Intent! with { AppliedRevision = 1 };
        Check(!applied.Pending, "Matching acknowledgement completes only current revision");
        Check(!KoxoQualityIntentPolicy.ToExecutionResult(created).Succeeded, "A publication is not proof");
        Check(KoxoQualityIntentPolicy.ToExecutionResult(new(true,false,applied)) is {Succeeded:true,Changed:false,ResultCode:"KOXO_QUALITIES_ALREADY_VERIFIED"},
            "Replay of an acknowledged revision must not remain pending forever");
        Check(!KoxoQualityIntentPolicy.ToExecutionResult(new(false,false,applied)).Succeeded,"Conflict cannot reuse a past success");
        var revoked = KoxoQualityIntentPolicy.Serialize(new Dictionary<string, IReadOnlyList<string>> { [alice] = [], [bob] = [] });
        var removal = KoxoQualityIntentPolicy.Publish(applied, 1, revoked);
        Check(removal.Intent is { Revision: 2, AppliedRevision: 1, Pending: true }, "Removal requires new external proof");
        Check(!KoxoQualityIntentPolicy.MayAcknowledge(removal.Intent!, 1), "Late success cannot acknowledge a revocation");
        Check(KoxoQualityIntentPolicy.MayAcknowledge(removal.Intent!, 2), "Current revision may be acknowledged");
        Check(!KoxoQualityIntentPolicy.Publish(removal.Intent, 1, first).Accepted, "Old addition cannot replace new removal");
        foreach (var invalid in new[] { "GG_VPN,GG_ADMIN", "GG_VPN\r\n", " ", "GG_VPN;" })
            Reject(() => KoxoQualityIntentPolicy.Serialize(new Dictionary<string, IReadOnlyList<string>> { [alice] = [invalid] }));
        Reject(() => KoxoQualityIntentPolicy.Serialize(new Dictionary<string, IReadOnlyList<string>> { ["guessed-user"] = [] }));
        var tooMany = Enumerable.Range(0, 129).ToDictionary(_ => Guid.NewGuid().ToString("D"), _ => (IReadOnlyList<string>)Array.Empty<string>());
        Reject(() => KoxoQualityIntentPolicy.Serialize(tooMany));
        var oversized = Enumerable.Range(0, 400).Select(index => new string('A', 240) + index).ToArray();
        Reject(() => KoxoQualityIntentPolicy.Serialize(new Dictionary<string, IReadOnlyList<string>> { [alice] = oversized }));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new InvalidOperationException("Invalid quality intent accepted");
    }
}
