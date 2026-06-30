using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Services.ActiveDirectory;

public sealed record AdServiceResult<T>(
    int StatusCode,
    string Code,
    string Message,
    T? Value = default,
    bool Changed = false);

public interface IActiveDirectoryService
{
    string ModeName { get; }

    Task<AdStatusResponse> GetStatusAsync(CancellationToken cancellationToken);
    Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchUsersAsync(
        string? query,
        string? customerReference,
        CancellationToken cancellationToken);
    Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchGroupsAsync(
        string? query,
        string? customerReference,
        CancellationToken cancellationToken);
    Task<AdServiceResult<AdDirectoryObjectSummary>> ResolveObjectForLinkAsync(
        string customerReference,
        string? distinguishedName,
        CancellationToken cancellationToken);
    Task<AdServiceResult<AdDirectoryObjectSummary>> CreateUserAsync(
        string customerReference,
        CreateAdUserRequest? request,
        CancellationToken cancellationToken);
    Task<AdServiceResult<AdDirectoryObjectSummary>> CreateGroupAsync(
        string customerReference,
        CreateAdGroupRequest? request,
        CancellationToken cancellationToken);
    Task<AdServiceResult<AdDirectoryObjectSummary>> AddGroupMemberAsync(
        string customerReference,
        string? groupSamAccountName,
        string? userSamAccountName,
        CancellationToken cancellationToken);
    Task<AdServiceResult<AdDirectoryObjectSummary>> RemoveGroupMemberAsync(
        string customerReference,
        string? groupSamAccountName,
        string? userSamAccountName,
        CancellationToken cancellationToken);
    Task<AdServiceResult<AdDirectoryObjectSummary>> DisableUserAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken);
    Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserToDisabledAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken);
    Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> GetUserEffectiveGroupsAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken);
    Task<AdServiceResult<AdDirectoryObjectSummary>> RenameUserAsync(
        string customerReference,
        string? currentSamAccountName,
        RenameAdUserRequest? request,
        CancellationToken cancellationToken);
    Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserAsync(
        string customerReference,
        string? samAccountName,
        MoveAdUserRequest? request,
        CancellationToken cancellationToken);
    Task<AdServiceResult<AdDirectoryObjectSummary>> ChangeUserPasswordAsync(
        string customerReference,
        string? samAccountName,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken);
}
