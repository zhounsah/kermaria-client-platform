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
