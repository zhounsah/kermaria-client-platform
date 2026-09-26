using System.Security.Cryptography;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Fabrique les references client (<c>CLI-XXXXXX</c> en production), dans le
/// namespace de l'instance (<see cref="KoxoNamespace.Current"/>).
/// </summary>
/// <remarks>
/// Cette reference nomme aussi l'OU du client dans l'annuaire (KoXo cree
/// <c>OU=CLI-XXXXXX</c> d'apres le champ « GroupeSecondaire » de l'export), et
/// sert donc aussi bien a l'inscription qu'a la reservation du code de groupe
/// d'un compte de demonstration.
///
/// L'alphabet exclut I, L, O, 0 et 1 : ces references sont lues et recopiees a
/// la main (support, annuaire), ou ces caracteres se confondent.
///
/// Le prefixe vient du namespace : une instance DEV produit par exemple
/// <c>DEV-CLI-XXXXXX</c>, donc des OU qui ne peuvent pas porter le nom d'une OU
/// de production dans le domaine partage.
/// </remarks>
public static class CustomerReferenceGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int Length = KoxoNamespace.CustomerReferenceRandomLength;

    public static string Generate() => Generate(KoxoNamespace.Current);

    public static string Generate(KoxoNamespace koxoNamespace)
    {
        ArgumentNullException.ThrowIfNull(koxoNamespace);
        Span<char> buffer = stackalloc char[Length];
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = Alphabet[
                RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return koxoNamespace.CustomerReferencePrefix + new string(buffer);
    }
}
