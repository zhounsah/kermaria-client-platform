using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.ActiveDirectory;

public sealed class MockActiveDirectoryService : IActiveDirectoryService
{
    private readonly AdRuntimeConfiguration _configuration;

    public MockActiveDirectoryService(AdRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AdHealthResponse GetHealth()
        => new(
            _configuration.ModeName,
            "mock",
            true,
            false);

    public AdOperationResult ChangePassword(ChangePasswordRequest? request)
        => new(
            StatusCodes.Status501NotImplemented,
            "AD_REAL_CHANGE_NOT_ENABLED",
            "Le changement de mot de passe Active Directory n'est pas activé.");

    public AdOperationResult CreateUser(CreateUserRequest? request)
        => Simulated();

    public AdOperationResult AddUserToGroup(GroupMembershipRequest? request)
        => Simulated();

    public AdOperationResult RemoveUserFromGroup(GroupMembershipRequest? request)
        => Simulated();

    private static AdOperationResult Simulated()
        => new(
            StatusCodes.Status200OK,
            "AD_MOCK_SIMULATED",
            "Action Active Directory simulée. Aucune modification n'a été effectuée.");
}
