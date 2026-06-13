using System.Text.RegularExpressions;

namespace Kermaria.ApiInternal.Infrastructure;

public static partial class CorrelationIdProvider
{
    public static string Resolve(string? candidate)
    {
        var normalized = candidate?.Trim();

        return normalized is not null && ValidCorrelationId().IsMatch(normalized)
            ? normalized
            : Guid.NewGuid().ToString("D");
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidCorrelationId();
}
