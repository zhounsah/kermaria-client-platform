using System.Security.Cryptography;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Fabrique les references client <c>CLI-XXXXXX</c>.
/// </summary>
/// <remarks>
/// Cette reference nomme aussi l'OU du client dans l'annuaire (KoXo cree
/// <c>OU=CLI-XXXXXX</c> d'apres le champ « GroupeSecondaire » de l'export), et
/// sert donc aussi bien a l'inscription qu'a la reservation du code de groupe
/// d'un compte de demonstration.
///
/// L'alphabet exclut I, L, O, 0 et 1 : ces references sont lues et recopiees a
/// la main (support, annuaire), ou ces caracteres se confondent.
/// </remarks>
public static class CustomerReferenceGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int Length = 6;

    public const string Prefix = "CLI-";

    public static string Generate()
    {
        Span<char> buffer = stackalloc char[Length];
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = Alphabet[
                RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"{Prefix}{new string(buffer)}";
    }
}
