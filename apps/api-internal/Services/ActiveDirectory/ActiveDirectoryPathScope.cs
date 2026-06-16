namespace Kermaria.ApiInternal.Services.ActiveDirectory;

public sealed class ActiveDirectoryPathScope
{
    private readonly string _clientsOuDn;
    private readonly string[] _clientsOuParts;

    public ActiveDirectoryPathScope(string clientsOuDn)
    {
        _clientsOuDn = NormalizeDistinguishedName(clientsOuDn)
            ?? throw new InvalidOperationException(
                "The Active Directory root OU is unavailable.");
        _clientsOuParts = SplitDn(_clientsOuDn);
    }

    public string ClientsOuDn => _clientsOuDn;

    public string BuildCustomerOuDn(string customerReference)
        => $"OU={EscapeRdnValue(customerReference)},OU=10_Customers,{_clientsOuDn}";

    public string BuildUsersOuDn(string customerReference)
        => $"OU=Users,{BuildCustomerOuDn(customerReference)}";

    public string BuildGroupsOuDn(string customerReference)
        => $"OU=Groups,{BuildCustomerOuDn(customerReference)}";

    public string BuildDisabledOuDn(string customerReference)
        => $"OU=Disabled,{BuildCustomerOuDn(customerReference)}";

    public string? NormalizeDistinguishedName(string? distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return null;
        }

        var parts = SplitDn(distinguishedName);
        if (parts.Length == 0)
        {
            return null;
        }

        return string.Join(
            ",",
            parts.Select(NormalizeDnPart));
    }

    public bool IsWithinAllowedRoot(string? distinguishedName)
    {
        var normalized = NormalizeDistinguishedName(distinguishedName);
        if (normalized is null)
        {
            return false;
        }

        return normalized.Equals(
                _clientsOuDn,
                StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(
                $",{_clientsOuDn}",
                StringComparison.OrdinalIgnoreCase);
    }

    public string? ExtractCustomerReference(string? distinguishedName)
    {
        var normalized = NormalizeDistinguishedName(distinguishedName);
        if (normalized is null || !IsWithinAllowedRoot(normalized))
        {
            return null;
        }

        var parts = SplitDn(normalized);
        if (parts.Length <= _clientsOuParts.Length)
        {
            return null;
        }

        var prefixLength = parts.Length - _clientsOuParts.Length;
        if (prefixLength < 2)
        {
            return null;
        }

        var customersContainerPart = parts[prefixLength - 1];
        if (!string.Equals(
                TryReadOuValue(customersContainerPart),
                "10_Customers",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var customerPart = parts[prefixLength - 2];
        return TryReadOuValue(customerPart);
    }

    public string BuildUserDn(
        string customerReference,
        string samAccountName)
        => $"CN={EscapeRdnValue(samAccountName)},{BuildUsersOuDn(customerReference)}";

    public string BuildGroupDn(
        string customerReference,
        string samAccountName)
        => $"CN={EscapeRdnValue(samAccountName)},{BuildGroupsOuDn(customerReference)}";

    public static string EscapeRdnValue(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("+", "\\+", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("<", "\\<", StringComparison.Ordinal)
            .Replace(">", "\\>", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace("=", "\\=", StringComparison.Ordinal);

        if (escaped.StartsWith(' ') || escaped.StartsWith('#'))
        {
            escaped = "\\" + escaped;
        }

        if (escaped.EndsWith(' '))
        {
            escaped = escaped[..^1] + "\\ ";
        }

        return escaped;
    }

    public static string EscapeLdapFilterValue(string value)
    {
        return value
            .Replace("\\", "\\5c", StringComparison.Ordinal)
            .Replace("*", "\\2a", StringComparison.Ordinal)
            .Replace("(", "\\28", StringComparison.Ordinal)
            .Replace(")", "\\29", StringComparison.Ordinal)
            .Replace("\0", "\\00", StringComparison.Ordinal);
    }

    private static string NormalizeDnPart(string part)
    {
        var separatorIndex = FindUnescapedSeparator(part, '=');
        if (separatorIndex <= 0)
        {
            return part.Trim();
        }

        var key = part[..separatorIndex].Trim().ToUpperInvariant();
        var value = part[(separatorIndex + 1)..].Trim();
        return $"{key}={value}";
    }

    private static string? TryReadOuValue(string part)
    {
        var separatorIndex = FindUnescapedSeparator(part, '=');
        if (separatorIndex <= 0)
        {
            return null;
        }

        var key = part[..separatorIndex].Trim();
        if (!key.Equals("OU", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return part[(separatorIndex + 1)..].Trim();
    }

    private static string[] SplitDn(string distinguishedName)
    {
        var parts = new List<string>();
        var buffer = new System.Text.StringBuilder();
        var escaped = false;

        foreach (var character in distinguishedName)
        {
            if (escaped)
            {
                buffer.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                buffer.Append(character);
                escaped = true;
                continue;
            }

            if (character == ',')
            {
                var part = buffer.ToString().Trim();
                if (part.Length > 0)
                {
                    parts.Add(part);
                }

                buffer.Clear();
                continue;
            }

            buffer.Append(character);
        }

        var lastPart = buffer.ToString().Trim();
        if (lastPart.Length > 0)
        {
            parts.Add(lastPart);
        }

        return parts.ToArray();
    }

    private static int FindUnescapedSeparator(string value, char separator)
    {
        var escaped = false;

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == separator)
            {
                return index;
            }
        }

        return -1;
    }
}
