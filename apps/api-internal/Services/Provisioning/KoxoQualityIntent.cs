using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record KoxoQualityIntent(long Revision, long AppliedRevision, string DesiredJson)
{
    public bool Pending => AppliedRevision != Revision;
}

public sealed record KoxoQualityIntentPublication(bool Accepted, bool Changed, KoxoQualityIntent? Intent);

public static class KoxoQualityIntentPolicy
{
    public static IReadOnlyList<string> TransportGroups(IReadOnlyList<string> desired, string? emptyGroup)
        => desired.Count == 0 && !string.IsNullOrWhiteSpace(emptyGroup) ? [emptyGroup] : desired;

    public static ProvisioningExecutionResult ToExecutionResult(KoxoQualityIntentPublication publication)
    {
        if (!publication.Accepted || publication.Intent is null)
            return new(false, false, "KOXO_QUALITIES_REVISION_CONFLICT", []);
        return publication.Intent.Pending
            ? new(false, false, "KOXO_QUALITIES_PENDING", [])
            : new(true, false, "KOXO_QUALITIES_ALREADY_VERIFIED", []);
    }

    public const int MaximumIdentities = 128;
    public const int MaximumDocumentBytes = 65536;
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Deserialize(string json)
    {
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)
            ?? throw new InvalidOperationException("KOXO_QUALITY_INTENT_INVALID");
        var result = parsed.ToDictionary(entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value, StringComparer.Ordinal);
        if (Serialize(result) != json)
            throw new InvalidOperationException("KOXO_QUALITY_INTENT_NON_CANONICAL");
        return result;
    }

    public static string Serialize(IReadOnlyDictionary<string, IReadOnlyList<string>> byIdentity)
    {
        if (byIdentity.Count > MaximumIdentities)
            throw new ArgumentException("KOXO_QUALITY_CAPACITY_EXCEEDED");
        var canonical = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (identity, groups) in byIdentity)
        {
            if (!Guid.TryParseExact(identity, "D", out var id))
                throw new ArgumentException("KOXO_QUALITY_IDENTITY_INVALID");
            if (groups.Any(group => string.IsNullOrWhiteSpace(group)
                || group != group.Trim()
                || !Regex.IsMatch(group, @"\A[\p{L}\p{N}_. -]{1,256}\z")))
                throw new ArgumentException("KOXO_QUALITY_GROUP_INVALID");
            if (!canonical.TryAdd(id.ToString("D"), groups.Select(group => group.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))
                throw new ArgumentException("KOXO_QUALITY_DUPLICATE_IDENTITY");
        }
        var json = JsonSerializer.Serialize(canonical);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes)
            throw new ArgumentException("KOXO_QUALITY_CAPACITY_EXCEEDED");
        return json;
    }

    public static KoxoQualityIntentPublication Publish(
        KoxoQualityIntent? current, long expectedRevision, string canonicalJson)
    {
        if (expectedRevision < 0 || expectedRevision != (current?.Revision ?? 0))
            return new(false, false, current);
        if (current?.DesiredJson == canonicalJson)
            return new(true, false, current);
        return new(true, true, new(checked(expectedRevision + 1), current?.AppliedRevision ?? 0, canonicalJson));
    }

    // Un accuse 202 de reception n'est pas une preuve. Le caller doit avoir
    // verifie les qualites KoXo ET les memberships AD de cette revision.
    public static bool MayAcknowledge(KoxoQualityIntent current, long observedRevision)
        => observedRevision > 0 && current.Revision == observedRevision;
}
