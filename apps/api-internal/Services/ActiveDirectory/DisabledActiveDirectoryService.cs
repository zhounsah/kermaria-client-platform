using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.ActiveDirectory;

public sealed class DisabledActiveDirectoryService : IActiveDirectoryService
{
    private readonly AdRuntimeConfiguration _configuration;

    public DisabledActiveDirectoryService(AdRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AdHealthResponse GetHealth()
        => new(
            _configuration.ModeName,
            "disabled",
            true,
            false);

    public AdOperationResult ChangePassword(ChangePasswordRequest? request)
        => Disabled();

    public AdOperationResult CreateUser(CreateUserRequest? request)
        => Disabled();

    public AdOperationResult AddUserToGroup(GroupMembershipRequest? request)
        => Disabled();

    public AdOperationResult RemoveUserFromGroup(GroupMembershipRequest? request)
        => Disabled();

    private static AdOperationResult Disabled()
        => new(
            StatusCodes.Status501NotImplemented,
            "AD_INTEGRATION_DISABLED",
            "L'intégration Active Directory n'est pas activée.");
}
