namespace Kermaria.ApiInternal.Services.ActiveDirectory;

public static class ActiveDirectoryInputValidator
{
    public static string? NormalizeCustomerReference(string? value)
        => NormalizeByRegex(value, "^[A-Za-z0-9-]{1,100}$");

    public static string? NormalizeSamAccountName(string? value)
        => NormalizeByRegex(value, "^[A-Za-z0-9._-]{1,64}$");

    public static string? NormalizeQuery(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        return normalized.Length <= 100 ? normalized : null;
    }

    public static bool TryNormalizeUserPrincipalName(
        string? value,
        string? allowedDomain,
        out string? normalizedValue)
    {
        normalizedValue = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim();
        if (normalized.Length > 255
            || normalized.Any(char.IsWhiteSpace)
            || normalized.Any(char.IsControl))
        {
            return false;
        }

        var separatorIndex = normalized.IndexOf('@');
        if (separatorIndex <= 0
            || separatorIndex != normalized.LastIndexOf('@')
            || separatorIndex >= normalized.Length - 1)
        {
            return false;
        }

        var localPart = normalized[..separatorIndex];
        var domainPart = normalized[(separatorIndex + 1)..];
        if (string.IsNullOrWhiteSpace(localPart)
            || string.IsNullOrWhiteSpace(domainPart)
            || string.IsNullOrWhiteSpace(allowedDomain)
            || !domainPart.Equals(
                allowedDomain.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalizedValue = $"{localPart}@{domainPart.ToLowerInvariant()}";
        return true;
    }

    private static string? NormalizeByRegex(string? value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return System.Text.RegularExpressions.Regex.IsMatch(
            normalized,
            pattern)
            ? normalized
            : null;
    }
}
