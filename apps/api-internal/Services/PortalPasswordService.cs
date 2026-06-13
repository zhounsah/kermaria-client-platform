using Kermaria.ApiInternal.Data.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Kermaria.ApiInternal.Services;

public interface IPortalPasswordService
{
    string DummyHash { get; }
    string HashPassword(string userId, string password);
    PasswordVerificationResult Verify(
        string userId,
        string passwordHash,
        string password);
}

public sealed class PortalPasswordService : IPortalPasswordService
{
    private readonly PasswordHasher<PortalPasswordSubject> _hasher = new();

    public PortalPasswordService()
    {
        DummyHash = HashPassword(
            "authentication-dummy",
            Convert.ToBase64String(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
    }

    public string DummyHash { get; }

    public string HashPassword(string userId, string password)
        => _hasher.HashPassword(new PortalPasswordSubject(userId), password);

    public PasswordVerificationResult Verify(
        string userId,
        string passwordHash,
        string password)
        => _hasher.VerifyHashedPassword(
            new PortalPasswordSubject(userId),
            passwordHash,
            password);

    private sealed record PortalPasswordSubject(string UserId);
}
