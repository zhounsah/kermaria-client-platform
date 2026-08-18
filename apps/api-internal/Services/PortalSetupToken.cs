using System.Security.Cryptography;
using System.Text;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Jeton a usage unique envoye par e-mail (verification d'adresse, definition
/// de mot de passe).
/// </summary>
/// <remarks>
/// <para>
/// Extrait de <see cref="SignupService"/>, ou il etait prive : le cycle de vie
/// des utilisateurs additionnels Billing V2 a exactement le meme besoin, et
/// deux generateurs de jetons qui divergeraient — longueur, encodage, fonction
/// de hachage — produiraient deux niveaux de securite differents pour un meme
/// type de lien.
/// </para>
/// <para>
/// 32 octets tires du CSPRNG, soit 256 bits : le jeton est la seule
/// authentification du porteur du lien. L'encodage Base64 URL-safe evite les
/// caracteres qu'un client de messagerie ou un proxy reecrit.
/// </para>
/// <para>
/// <b>Seul le condensat est persistable.</b> Le jeton en clair n'existe que
/// dans le lien envoye : une base compromise ne doit pas rendre les liens
/// rejouables.
/// </para>
/// </remarks>
public static class PortalSetupToken
{
    public static string Generate()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
