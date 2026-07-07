namespace Kermaria.ApiInternal.Data.Configuration;

public sealed record SubscriptionProvisioningRuntimeConfiguration(
    IReadOnlyDictionary<string, IReadOnlyList<string>>
        GroupsByOfferExternalReference,
    IReadOnlyDictionary<string, string> GroupDistinguishedNamesBySamAccountName,
    int MaxAttempts,
    int RetryDelayMs)
{
    public IReadOnlyList<string> ResolveMappedGroups(
        string? offerExternalReference)
    {
        if (string.IsNullOrWhiteSpace(offerExternalReference))
        {
            return Array.Empty<string>();
        }

        return GroupsByOfferExternalReference.TryGetValue(
            offerExternalReference.Trim(),
            out var groups)
            ? groups
            : Array.Empty<string>();
    }

    public IReadOnlyList<string> ManagedGroupSamAccountNames =>
        GroupsByOfferExternalReference.Values
            .SelectMany(groups => groups)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public bool TryGetGroupDistinguishedName(
        string groupSamAccountName,
        out string distinguishedName)
        => GroupDistinguishedNamesBySamAccountName.TryGetValue(
            groupSamAccountName,
            out distinguishedName!);
}

public static class SubscriptionProvisioningConfigurationResolver
{
    private const string GroupsSectionName = "SUBSCRIPTION_PROVISIONING_GROUPS";
    private const string GroupDnsSectionName = "AD_PROVISIONING_GROUP_DNS";

    private static readonly IReadOnlyDictionary<string, string[]>
        DefaultGroupsByOfferExternalReference =
            new Dictionary<string, string[]>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["ACCES-RDS"] = ["GG_RDS"],
                ["ACCES-VPN"] = ["GG_VPN"],
                ["PACK-ACCES-1M-MENS"] = ["GG_VPN"],
                ["PACK-ACCES-6M-MENS"] = ["GG_VPN"],
                ["PACK-ACCES-6M-COMPT"] = ["GG_VPN"],
                ["PACK-ACCES-12M-MENS"] = ["GG_VPN"],
                ["PACK-ACCES-12M-COMPT"] = ["GG_VPN"],
                ["PACK-BUREAU-1M-MENS"] = ["GG_RDS", "GG_VPN"],
                ["PACK-BUREAU-6M-MENS"] = ["GG_RDS", "GG_VPN"],
                ["PACK-BUREAU-6M-COMPT"] = ["GG_RDS", "GG_VPN"],
                ["PACK-BUREAU-12M-MENS"] = ["GG_RDS", "GG_VPN"],
                ["PACK-BUREAU-12M-COMPT"] = ["GG_RDS", "GG_VPN"],
                ["PACK-PRO-1M-MENS"] = ["GG_VPN"],
                ["PACK-PRO-6M-MENS"] = ["GG_VPN"],
                ["PACK-PRO-6M-COMPT"] = ["GG_VPN"],
                ["PACK-PRO-12M-MENS"] = ["GG_VPN"],
                ["PACK-PRO-12M-COMPT"] = ["GG_VPN"],
                ["RADIO"] = ["GG_Radio"]
            };

    public static SubscriptionProvisioningRuntimeConfiguration Resolve(
        IConfiguration configuration)
    {
        var groupsByOfferExternalReference =
            new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var entry in DefaultGroupsByOfferExternalReference)
        {
            groupsByOfferExternalReference[entry.Key] = entry.Value;
        }

        foreach (var child in ReadSectionEntries(
            configuration,
            GroupsSectionName))
        {
            var groups = SplitGroups(child.Value);
            if (groups.Count == 0)
            {
                continue;
            }

            groupsByOfferExternalReference[child.Key] = groups;
        }

        var groupDnsBySamAccountName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in ReadSectionEntries(
            configuration,
            GroupDnsSectionName))
        {
            var distinguishedName =
                AdConfigurationResolver.NormalizeDn(child.Value);
            if (distinguishedName is null)
            {
                continue;
            }

            groupDnsBySamAccountName[child.Key] = distinguishedName;
        }

        return new SubscriptionProvisioningRuntimeConfiguration(
            groupsByOfferExternalReference,
            groupDnsBySamAccountName,
            ParseBoundedInt(
                configuration["SUBSCRIPTION_PROVISIONING_MAX_ATTEMPTS"],
                fallback: 3,
                minimum: 1,
                maximum: 5),
            ParseBoundedInt(
                configuration["SUBSCRIPTION_PROVISIONING_RETRY_DELAY_MS"],
                fallback: 200,
                minimum: 0,
                maximum: 5000));
    }

    private static IReadOnlyList<string> SplitGroups(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(
                [',', ';', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int ParseBoundedInt(
        string? value,
        int fallback,
        int minimum,
        int maximum)
    {
        if (!int.TryParse(value, out var parsed))
        {
            return fallback;
        }

        return Math.Clamp(parsed, minimum, maximum);
    }

    private static IReadOnlyList<KeyValuePair<string, string?>> ReadSectionEntries(
        IConfiguration configuration,
        string sectionName)
    {
        var entries = new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var child in configuration
            .GetSection(sectionName)
            .GetChildren())
        {
            entries[child.Key] = child.Value;
        }

        foreach (var pair in configuration.AsEnumerable())
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            if (TryExtractSectionKey(
                    pair.Key,
                    sectionName,
                    out var entryKey))
            {
                entries[entryKey] = pair.Value;
            }
        }

        return entries.ToArray();
    }

    private static bool TryExtractSectionKey(
        string configurationKey,
        string sectionName,
        out string entryKey)
    {
        foreach (var separator in new[] { ':', '_' })
        {
            var prefix = separator == ':'
                ? $"{sectionName}:"
                : $"{sectionName}__";
            if (!configurationKey.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entryKey = configurationKey[prefix.Length..].Trim();
            return entryKey.Length > 0;
        }

        entryKey = string.Empty;
        return false;
    }
}
