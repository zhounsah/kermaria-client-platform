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

    public string ModeName => _configuration.ModeName;

    public Task<AdStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(new AdStatusResponse(
            _configuration.ModeName,
            "disabled",
            true,
            false,
            false,
            null,
            null,
            _configuration.AllowedRoots,
            _configuration.ConnectTimeoutMs,
            _configuration.QueryTimeoutMs,
            _configuration.MaxResults));

    public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchUsersAsync(
        string? query,
        string? customerReference,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledListResult());

    public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchGroupsAsync(
        string? query,
        string? customerReference,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledListResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> ResolveObjectForLinkAsync(
        string customerReference,
        string? distinguishedName,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> CreateUserAsync(
        string customerReference,
        CreateAdUserRequest? request,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> CreateGroupAsync(
        string customerReference,
        CreateAdGroupRequest? request,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> AddGroupMemberAsync(
        string customerReference,
        string? groupSamAccountName,
        string? userSamAccountName,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> RemoveGroupMemberAsync(
        string customerReference,
        string? groupSamAccountName,
        string? userSamAccountName,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> DisableUserAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserToDisabledAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> GetUserEffectiveGroupsAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledListResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> RenameUserAsync(
        string customerReference,
        string? currentSamAccountName,
        RenameAdUserRequest? request,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserAsync(
        string customerReference,
        string? samAccountName,
        MoveAdUserRequest? request,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> ChangeUserPasswordAsync(
        string customerReference,
        string? samAccountName,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    public Task<AdServiceResult<AdDirectoryObjectSummary>> SetUserPasswordAsync(
        string customerReference,
        string? samAccountName,
        string? newPassword,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledObjectResult());

    private static AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>> DisabledListResult()
        => new(
            StatusCodes.Status501NotImplemented,
            "AD_INTEGRATION_DISABLED",
            "Active Directory integration is disabled.",
            Array.Empty<AdDirectoryObjectSummary>());

    private static AdServiceResult<AdDirectoryObjectSummary> DisabledObjectResult()
        => new(
            StatusCodes.Status501NotImplemented,
            "AD_INTEGRATION_DISABLED",
            "Active Directory integration is disabled.");
}
