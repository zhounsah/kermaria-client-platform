using System.Security.Cryptography;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.ActiveDirectory;

public sealed class MockActiveDirectoryService : IActiveDirectoryService
{
    private readonly AdRuntimeConfiguration _configuration;
    private readonly ActiveDirectoryPathScope _scope;
    private readonly Dictionary<string, MockDirectoryObject> _objectsByDn =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _groupMembers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();

    public MockActiveDirectoryService(AdRuntimeConfiguration configuration)
    {
        _configuration = configuration;
        _scope = new ActiveDirectoryPathScope(
            configuration.ClientsOuDn
            ?? "OU=TEST_SITE_WEB,DC=home,DC=bzh");
        SeedFixtures();
    }

    public string ModeName => _configuration.ModeName;

    public Task<AdStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(new AdStatusResponse(
            _configuration.ModeName,
            "mock",
            true,
            true,
            true,
            _configuration.Domain ?? "home.bzh",
            _scope.ClientsOuDn,
            _configuration.ConnectTimeoutMs,
            _configuration.QueryTimeoutMs,
            _configuration.MaxResults));

    public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchUsersAsync(
        string? query,
        string? customerReference,
        CancellationToken cancellationToken)
        => Task.FromResult(SearchObjects("user", query, customerReference));

    public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchGroupsAsync(
        string? query,
        string? customerReference,
        CancellationToken cancellationToken)
        => Task.FromResult(SearchObjects("group", query, customerReference));

    public Task<AdServiceResult<AdDirectoryObjectSummary>> ResolveObjectForLinkAsync(
        string customerReference,
        string? distinguishedName,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerReference =
            ActiveDirectoryInputValidator.NormalizeCustomerReference(
                customerReference);
        var normalizedDn = _scope.NormalizeDistinguishedName(
            distinguishedName);

        if (normalizedCustomerReference is null || normalizedDn is null)
        {
            return Task.FromResult(InvalidObject("INVALID_REQUEST"));
        }

        return Task.FromResult(ResolveByDn(
            normalizedCustomerReference,
            normalizedDn));
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> CreateUserAsync(
        string customerReference,
        CreateAdUserRequest? request,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerReference =
            ActiveDirectoryInputValidator.NormalizeCustomerReference(
                customerReference);
        var samAccountName =
            ActiveDirectoryInputValidator.NormalizeSamAccountName(
                request?.SamAccountName);
        var displayName = request?.DisplayName?.Trim();

        if (normalizedCustomerReference is null
            || samAccountName is null
            || string.IsNullOrWhiteSpace(displayName))
        {
            return Task.FromResult(InvalidObject("INVALID_REQUEST"));
        }

        if (!ActiveDirectoryInputValidator.TryNormalizeUserPrincipalName(
                request!.UserPrincipalName,
                _configuration.Domain ?? "home.bzh",
                out var normalizedUserPrincipalName))
        {
            return Task.FromResult(InvalidObject("INVALID_REQUEST"));
        }

        lock (_syncRoot)
        {
            var userDn = _scope.BuildUserDn(
                normalizedCustomerReference,
                samAccountName);
            if (_objectsByDn.ContainsKey(userDn))
            {
                return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                    StatusCodes.Status409Conflict,
                    "AD_OBJECT_ALREADY_EXISTS",
                    "The requested Active Directory object already exists."));
            }

            var user = BuildObject(
                objectType: "user",
                customerReference: normalizedCustomerReference,
                samAccountName: samAccountName,
                distinguishedName: userDn,
                displayName: displayName,
                userPrincipalName: normalizedUserPrincipalName
                    ?? $"{samAccountName}@{_configuration.Domain ?? "home.bzh"}",
                isDisabled: true);
            _objectsByDn[userDn] = user;

            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status201Created,
                "AD_USER_CREATED",
                "Active Directory user created in mock mode.",
                ToSummary(user),
                true));
        }
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> CreateGroupAsync(
        string customerReference,
        CreateAdGroupRequest? request,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerReference =
            ActiveDirectoryInputValidator.NormalizeCustomerReference(
                customerReference);
        var samAccountName =
            ActiveDirectoryInputValidator.NormalizeSamAccountName(
                request?.SamAccountName);

        if (normalizedCustomerReference is null || samAccountName is null)
        {
            return Task.FromResult(InvalidObject("INVALID_REQUEST"));
        }

        lock (_syncRoot)
        {
            var groupDn = _scope.BuildGroupDn(
                normalizedCustomerReference,
                samAccountName);
            if (_objectsByDn.ContainsKey(groupDn))
            {
                return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                    StatusCodes.Status409Conflict,
                    "AD_OBJECT_ALREADY_EXISTS",
                    "The requested Active Directory object already exists."));
            }

            var group = BuildObject(
                objectType: "group",
                customerReference: normalizedCustomerReference,
                samAccountName: samAccountName,
                distinguishedName: groupDn,
                displayName: string.IsNullOrWhiteSpace(request!.DisplayName)
                    ? samAccountName
                    : request.DisplayName.Trim(),
                userPrincipalName: null,
                isDisabled: false);
            _objectsByDn[groupDn] = group;
            _groupMembers[groupDn] = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status201Created,
                "AD_GROUP_CREATED",
                "Active Directory group created in mock mode.",
                ToSummary(group),
                true));
        }
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> AddGroupMemberAsync(
        string customerReference,
        string? groupSamAccountName,
        string? userSamAccountName,
        CancellationToken cancellationToken)
        => Task.FromResult(UpdateGroupMembership(
            customerReference,
            groupSamAccountName,
            userSamAccountName,
            shouldAdd: true));

    public Task<AdServiceResult<AdDirectoryObjectSummary>> RemoveGroupMemberAsync(
        string customerReference,
        string? groupSamAccountName,
        string? userSamAccountName,
        CancellationToken cancellationToken)
        => Task.FromResult(UpdateGroupMembership(
            customerReference,
            groupSamAccountName,
            userSamAccountName,
            shouldAdd: false));

    public Task<AdServiceResult<AdDirectoryObjectSummary>> DisableUserAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken)
    {
        var resolved = ResolveBySam(customerReference, samAccountName, "user");
        if (resolved.Value is null)
        {
            return Task.FromResult(resolved);
        }

        lock (_syncRoot)
        {
            var current = _objectsByDn[resolved.Value.DistinguishedName];
            if (current.IsDisabled)
            {
                return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                    StatusCodes.Status200OK,
                    "AD_USER_ALREADY_DISABLED",
                    "Active Directory user is already disabled.",
                    resolved.Value,
                    false));
            }

            current = current with { IsDisabled = true };
            _objectsByDn[current.DistinguishedName] = current;

            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status200OK,
                "AD_USER_DISABLED",
                "Active Directory user disabled in mock mode.",
                ToSummary(current),
                true));
        }
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserToDisabledAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken)
    {
        var resolved = ResolveBySam(customerReference, samAccountName, "user");
        if (resolved.Value is null)
        {
            return Task.FromResult(resolved);
        }

        if (!resolved.Value.IsDisabled)
        {
            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status409Conflict,
                "AD_USER_MUST_BE_DISABLED",
                "The user must be disabled before being moved to the Disabled OU."));
        }

        lock (_syncRoot)
        {
            var current = _objectsByDn[resolved.Value.DistinguishedName];
            var targetDn = $"CN={ActiveDirectoryPathScope.EscapeRdnValue(current.SamAccountName)},{_scope.BuildDisabledOuDn(current.CustomerReference)}";
            if (current.DistinguishedName.Equals(
                    targetDn,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                    StatusCodes.Status200OK,
                    "AD_USER_ALREADY_IN_DISABLED_OU",
                    "Active Directory user is already in the Disabled OU.",
                    resolved.Value,
                    false));
            }

            _objectsByDn.Remove(current.DistinguishedName);
            current = current with { DistinguishedName = targetDn };
            _objectsByDn[targetDn] = current;

            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status200OK,
                "AD_USER_MOVED_TO_DISABLED",
                "Active Directory user moved to the Disabled OU in mock mode.",
                ToSummary(current),
                true));
        }
    }

    private AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>> SearchObjects(
        string objectType,
        string? query,
        string? customerReference)
    {
        var normalizedQuery = ActiveDirectoryInputValidator.NormalizeQuery(query);
        if (normalizedQuery is null)
        {
            return new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                "The requested Active Directory search is invalid.",
                Array.Empty<AdDirectoryObjectSummary>());
        }

        var normalizedCustomerReference = string.IsNullOrWhiteSpace(
                customerReference)
            ? null
            : ActiveDirectoryInputValidator.NormalizeCustomerReference(
                customerReference);
        if (customerReference is not null && normalizedCustomerReference is null)
        {
            return new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                "The requested Active Directory search is invalid.",
                Array.Empty<AdDirectoryObjectSummary>());
        }

        IReadOnlyList<AdDirectoryObjectSummary> results;
        lock (_syncRoot)
        {
            results = _objectsByDn.Values
                .Where(objectState =>
                    objectState.ObjectType == objectType
                    && (normalizedCustomerReference is null
                        || objectState.CustomerReference.Equals(
                            normalizedCustomerReference,
                            StringComparison.OrdinalIgnoreCase))
                    && MatchesQuery(objectState, normalizedQuery))
                .OrderBy(objectState => objectState.SamAccountName)
                .Take(_configuration.MaxResults)
                .Select(ToSummary)
                .ToArray();
        }

        return new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(
            StatusCodes.Status200OK,
            objectType == "user"
                ? "AD_USERS_FOUND"
                : "AD_GROUPS_FOUND",
            "Active Directory search completed in mock mode.",
            results,
            false);
    }

    private AdServiceResult<AdDirectoryObjectSummary> ResolveByDn(
        string customerReference,
        string normalizedDn)
    {
        if (!_scope.IsWithinAllowedRoot(normalizedDn))
        {
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status403Forbidden,
                "AD_TARGET_OUTSIDE_ALLOWED_OU",
                "The requested Active Directory target is outside the allowed OU.");
        }

        lock (_syncRoot)
        {
            if (!_objectsByDn.TryGetValue(normalizedDn, out var objectState))
            {
                return new AdServiceResult<AdDirectoryObjectSummary>(
                    StatusCodes.Status404NotFound,
                    "AD_OBJECT_NOT_FOUND",
                    "The requested Active Directory object was not found.");
            }

            if (!objectState.CustomerReference.Equals(
                    customerReference,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new AdServiceResult<AdDirectoryObjectSummary>(
                    StatusCodes.Status403Forbidden,
                    "AD_CROSS_CUSTOMER_FORBIDDEN",
                    "Cross-customer Active Directory operations are not allowed.");
            }

            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status200OK,
                "AD_OBJECT_FOUND",
                "Active Directory object resolved in mock mode.",
                ToSummary(objectState));
        }
    }

    private AdServiceResult<AdDirectoryObjectSummary> ResolveBySam(
        string customerReference,
        string? samAccountName,
        string objectType)
    {
        var normalizedCustomerReference =
            ActiveDirectoryInputValidator.NormalizeCustomerReference(
                customerReference);
        var normalizedSamAccountName =
            ActiveDirectoryInputValidator.NormalizeSamAccountName(
                samAccountName);

        if (normalizedCustomerReference is null
            || normalizedSamAccountName is null)
        {
            return InvalidObject("INVALID_REQUEST");
        }

        lock (_syncRoot)
        {
            var objectState = _objectsByDn.Values.FirstOrDefault(candidate =>
                candidate.ObjectType == objectType
                && candidate.CustomerReference.Equals(
                    normalizedCustomerReference,
                    StringComparison.OrdinalIgnoreCase)
                && candidate.SamAccountName.Equals(
                    normalizedSamAccountName,
                    StringComparison.OrdinalIgnoreCase));
            if (objectState is not null)
            {
                return new AdServiceResult<AdDirectoryObjectSummary>(
                    StatusCodes.Status200OK,
                    "AD_OBJECT_FOUND",
                    "Active Directory object resolved in mock mode.",
                    ToSummary(objectState));
            }

            var crossCustomerObject = _objectsByDn.Values.FirstOrDefault(candidate =>
                candidate.ObjectType == objectType
                && candidate.SamAccountName.Equals(
                    normalizedSamAccountName,
                    StringComparison.OrdinalIgnoreCase));
            if (crossCustomerObject is null)
            {
                return new AdServiceResult<AdDirectoryObjectSummary>(
                    StatusCodes.Status404NotFound,
                    "AD_OBJECT_NOT_FOUND",
                    "The requested Active Directory object was not found.");
            }

            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status403Forbidden,
                "AD_CROSS_CUSTOMER_FORBIDDEN",
                "Cross-customer Active Directory operations are not allowed.");
        }
    }

    private AdServiceResult<AdDirectoryObjectSummary> UpdateGroupMembership(
        string customerReference,
        string? groupSamAccountName,
        string? userSamAccountName,
        bool shouldAdd)
    {
        var resolvedGroup = ResolveBySam(
            customerReference,
            groupSamAccountName,
            "group");
        if (resolvedGroup.Value is null)
        {
            return resolvedGroup;
        }

        var resolvedUser = ResolveBySam(
            customerReference,
            userSamAccountName,
            "user");
        if (resolvedUser.Value is null)
        {
            return resolvedUser;
        }

        lock (_syncRoot)
        {
            var members = _groupMembers.TryGetValue(
                    resolvedGroup.Value.DistinguishedName,
                    out var existingMembers)
                ? existingMembers
                : _groupMembers[resolvedGroup.Value.DistinguishedName] =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var changed = shouldAdd
                ? members.Add(resolvedUser.Value.DistinguishedName)
                : members.Remove(resolvedUser.Value.DistinguishedName);
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status200OK,
                shouldAdd
                    ? changed
                        ? "AD_GROUP_MEMBER_ADDED"
                        : "AD_GROUP_MEMBER_ALREADY_PRESENT"
                    : changed
                        ? "AD_GROUP_MEMBER_REMOVED"
                        : "AD_GROUP_MEMBER_ALREADY_ABSENT",
                shouldAdd
                    ? "Active Directory membership updated in mock mode."
                    : "Active Directory membership removed in mock mode.",
                resolvedGroup.Value,
                changed);
        }
    }

    private static bool MatchesQuery(
        MockDirectoryObject objectState,
        string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return true;
        }

        return objectState.SamAccountName.Contains(
                query,
                StringComparison.OrdinalIgnoreCase)
            || objectState.DisplayName.Contains(
                query,
                StringComparison.OrdinalIgnoreCase)
            || (objectState.UserPrincipalName?.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase)
                ?? false);
    }

    private void SeedFixtures()
    {
        foreach (var customerReference in new[]
        {
            "CLI-DEMO-0042",
            "CLI-DEMO-0100",
            "CLI-DEMO-0200"
        })
        {
            AddSeedGroup(customerReference, $"{customerReference}_PORTAL_USERS");
            AddSeedGroup(customerReference, $"{customerReference}_RDS_USERS");
            AddSeedGroup(customerReference, $"{customerReference}_VPN_USERS");
            AddSeedGroup(customerReference, $"{customerReference}_BACKUP_USERS");
        }

        AddSeedUser(
            "CLI-DEMO-0042",
            "test.web.0042.admin",
            "Test Web 0042 Admin");
        AddSeedUser(
            "CLI-DEMO-0042",
            "test.web.0042.user",
            "Test Web 0042 User");
        AddSeedUser(
            "CLI-DEMO-0100",
            "test.web.0100.user",
            "Test Web 0100 User");
        AddSeedUser(
            "CLI-DEMO-0200",
            "test.web.0200.user",
            "Test Web 0200 User");
    }

    private void AddSeedUser(
        string customerReference,
        string samAccountName,
        string displayName)
    {
        var user = BuildObject(
            objectType: "user",
            customerReference: customerReference,
            samAccountName: samAccountName,
            distinguishedName: _scope.BuildUserDn(customerReference, samAccountName),
            displayName: displayName,
            userPrincipalName: $"{samAccountName}@{_configuration.Domain ?? "home.bzh"}",
            isDisabled: true);
        _objectsByDn[user.DistinguishedName] = user;
    }

    private void AddSeedGroup(
        string customerReference,
        string suffix)
    {
        var samAccountName = $"KERMARIA_{suffix}";
        var group = BuildObject(
            objectType: "group",
            customerReference: customerReference,
            samAccountName: samAccountName,
            distinguishedName: _scope.BuildGroupDn(customerReference, samAccountName),
            displayName: samAccountName,
            userPrincipalName: null,
            isDisabled: false);
        _objectsByDn[group.DistinguishedName] = group;
        _groupMembers[group.DistinguishedName] = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
    }

    private static MockDirectoryObject BuildObject(
        string objectType,
        string customerReference,
        string samAccountName,
        string distinguishedName,
        string displayName,
        string? userPrincipalName,
        bool isDisabled)
    {
        var seed = $"{objectType}|{customerReference}|{samAccountName}";
        return new MockDirectoryObject(
            CreateDeterministicGuid(seed),
            CreateDeterministicSid(seed),
            objectType,
            samAccountName,
            userPrincipalName,
            displayName,
            distinguishedName,
            customerReference,
            isDisabled);
    }

    private static AdDirectoryObjectSummary ToSummary(
        MockDirectoryObject objectState)
        => new(
            objectState.ObjectGuid,
            objectState.ObjectSid,
            objectState.ObjectType,
            objectState.SamAccountName,
            objectState.UserPrincipalName,
            objectState.DisplayName,
            objectState.DistinguishedName,
            objectState.CustomerReference,
            objectState.IsDisabled);

    private static AdServiceResult<AdDirectoryObjectSummary> InvalidObject(
        string code)
        => new(
            StatusCodes.Status400BadRequest,
            code,
            "The requested Active Directory payload is invalid.");

    private static string CreateDeterministicGuid(string seed)
    {
        var hash = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return new Guid(hash).ToString("D");
    }

    private static string CreateDeterministicSid(string seed)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        var parts = new[]
        {
            BitConverter.ToUInt32(hash, 0),
            BitConverter.ToUInt32(hash, 4),
            BitConverter.ToUInt32(hash, 8),
            BitConverter.ToUInt32(hash, 12)
        };
        return $"S-1-5-21-{parts[0]}-{parts[1]}-{parts[2]}-{parts[3]}";
    }

    private sealed record MockDirectoryObject(
        string ObjectGuid,
        string ObjectSid,
        string ObjectType,
        string SamAccountName,
        string? UserPrincipalName,
        string DisplayName,
        string DistinguishedName,
        string CustomerReference,
        bool IsDisabled);
}
