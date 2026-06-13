using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Services.ActiveDirectory;

public sealed record AdOperationResult(
    int StatusCode,
    string Code,
    string Message);

public interface IActiveDirectoryService
{
    AdHealthResponse GetHealth();
    AdOperationResult ChangePassword(ChangePasswordRequest? request);
    AdOperationResult CreateUser(CreateUserRequest? request);
    AdOperationResult AddUserToGroup(GroupMembershipRequest? request);
    AdOperationResult RemoveUserFromGroup(GroupMembershipRequest? request);
}
