using System.Security.Cryptography;
using System.Text;

namespace Kermaria.ApiInternal.Services;

public interface ISessionTokenService
{
    string Generate();
    string Hash(string token);
}

public sealed class SessionTokenService : ISessionTokenService
{
    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public string Hash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
