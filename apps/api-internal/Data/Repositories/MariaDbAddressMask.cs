using System.Net;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Masque une adresse source avant de la remonter a une interface
/// d'administration.
///
/// Le masque est partage volontairement : une deuxieme implementation
/// finirait par diverger, et c'est exactement le genre de divergence qui
/// n'est pas visible tant qu'une adresse complete n'a pas deja fuite.
/// </summary>
public static class MariaDbAddressMask
{
    public static string? Apply(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (IPAddress.TryParse(value, out var address))
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4)
            {
                bytes[3] = 0;
                return new IPAddress(bytes).ToString();
            }

            for (var index = 8; index < bytes.Length; index++)
            {
                bytes[index] = 0;
            }

            return new IPAddress(bytes).ToString();
        }

        return "masquée";
    }
}
