using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.ActiveDirectory;

public sealed class LdapActiveDirectoryService : IActiveDirectoryService
{
    private readonly AdRuntimeConfiguration _configuration;
    private readonly ActiveDirectoryPathScope _scope;
    private readonly ILogger<LdapActiveDirectoryService> _logger;

    public LdapActiveDirectoryService(
        AdRuntimeConfiguration configuration,
        ILogger<LdapActiveDirectoryService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _scope = new ActiveDirectoryPathScope(
            configuration.ClientsOuDn
            ?? "OU=TEST_SITE_WEB,DC=home,DC=bzh");
    }

    public string ModeName => _configuration.ModeName;

    public Task<AdStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        if (!_configuration.ConfigurationValid)
        {
            return Task.FromResult(new AdStatusResponse(
                _configuration.ModeName,
                "configuration_invalid",
                false,
                _configuration.ReadsEnabled,
                _configuration.WritesEnabled,
                _configuration.Domain,
                _scope.ClientsOuDn,
                _configuration.AllowedRoots,
                _configuration.ConnectTimeoutMs,
                _configuration.QueryTimeoutMs,
                _configuration.MaxResults));
        }

        try
        {
            using var root = BindEntry(_scope.ClientsOuDn);
            root.RefreshCache(["distinguishedName"]);

            return Task.FromResult(new AdStatusResponse(
                _configuration.ModeName,
                "ready",
                true,
                true,
                _configuration.WritesEnabled,
                _configuration.Domain,
                _scope.ClientsOuDn,
                _configuration.AllowedRoots,
                _configuration.ConnectTimeoutMs,
                _configuration.QueryTimeoutMs,
                _configuration.MaxResults));
        }
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            _logger.LogWarning(
                "Active Directory status probe failed without exposing bind details exception_type {ExceptionType}",
                exception.GetType().Name);
            return Task.FromResult(new AdStatusResponse(
                _configuration.ModeName,
                "unreachable",
                true,
                true,
                _configuration.WritesEnabled,
                _configuration.Domain,
                _scope.ClientsOuDn,
                _configuration.AllowedRoots,
                _configuration.ConnectTimeoutMs,
                _configuration.QueryTimeoutMs,
                _configuration.MaxResults));
        }
    }

    public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchUsersAsync(
        string? query,
        string? customerReference,
        CancellationToken cancellationToken)
        => Task.FromResult(SearchDirectory(
            "user",
            query,
            customerReference));

    public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> SearchGroupsAsync(
        string? query,
        string? customerReference,
        CancellationToken cancellationToken)
        => Task.FromResult(SearchDirectory(
            "group",
            query,
            customerReference));

    public Task<AdServiceResult<AdDirectoryObjectSummary>> ResolveObjectForLinkAsync(
        string customerReference,
        string? distinguishedName,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerReference =
            ActiveDirectoryInputValidator.NormalizeCustomerReference(
                customerReference);
        var normalizedDn = _scope.NormalizeDistinguishedName(distinguishedName);

        if (normalizedCustomerReference is null || normalizedDn is null)
        {
            return Task.FromResult(InvalidObject());
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
        if (!_configuration.WritesEnabled)
        {
            return Task.FromResult(ReadOnlyResult());
        }

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
            return Task.FromResult(InvalidObject());
        }

        var distinguishedName = _scope.BuildUserDn(
            normalizedCustomerReference,
            samAccountName);
        var defaultUserPrincipalName = $"{samAccountName}@{_configuration.Domain}";
        if (!ActiveDirectoryInputValidator.TryNormalizeUserPrincipalName(
                request!.UserPrincipalName,
                _configuration.Domain,
                out var normalizedUserPrincipalName))
        {
            return Task.FromResult(InvalidObject());
        }

        var userPrincipalName = normalizedUserPrincipalName
            ?? defaultUserPrincipalName;

        return Task.FromResult(ExecuteWrite(() =>
        {
            using var parent = BindEntry(
                _scope.BuildUsersOuDn(normalizedCustomerReference));
            using var user = parent.Children.Add(
                $"CN={ActiveDirectoryPathScope.EscapeRdnValue(samAccountName)}",
                "user");
            user.Properties["sAMAccountName"].Value = samAccountName;
            user.Properties["displayName"].Value = displayName;
            user.Properties["userPrincipalName"].Value = userPrincipalName;
            user.Properties["userAccountControl"].Value = 514;

            if (!string.IsNullOrWhiteSpace(request.GivenName))
            {
                user.Properties["givenName"].Value = request.GivenName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Surname))
            {
                user.Properties["sn"].Value = request.Surname.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                user.Properties["description"].Value = request.Description.Trim();
            }

            user.CommitChanges();
            using var created = BindEntry(distinguishedName);
            created.RefreshCache();
            return MapEntry(created);
        },
        "AD_USER_CREATED",
        "Active Directory user created.",
        StatusCodes.Status201Created));
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> CreateGroupAsync(
        string customerReference,
        CreateAdGroupRequest? request,
        CancellationToken cancellationToken)
    {
        if (!_configuration.WritesEnabled)
        {
            return Task.FromResult(ReadOnlyResult());
        }

        var normalizedCustomerReference =
            ActiveDirectoryInputValidator.NormalizeCustomerReference(
                customerReference);
        var samAccountName =
            ActiveDirectoryInputValidator.NormalizeSamAccountName(
                request?.SamAccountName);

        if (normalizedCustomerReference is null || samAccountName is null)
        {
            return Task.FromResult(InvalidObject());
        }

        var distinguishedName = _scope.BuildGroupDn(
            normalizedCustomerReference,
            samAccountName);
        var displayName = string.IsNullOrWhiteSpace(request!.DisplayName)
            ? samAccountName
            : request.DisplayName.Trim();

        return Task.FromResult(ExecuteWrite(() =>
        {
            using var parent = BindEntry(
                _scope.BuildGroupsOuDn(normalizedCustomerReference));
            using var group = parent.Children.Add(
                $"CN={ActiveDirectoryPathScope.EscapeRdnValue(samAccountName)}",
                "group");
            group.Properties["sAMAccountName"].Value = samAccountName;
            group.Properties["displayName"].Value = displayName;
            group.Properties["groupType"].Value = -2147483646;

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                group.Properties["description"].Value = request.Description.Trim();
            }

            group.CommitChanges();
            using var created = BindEntry(distinguishedName);
            created.RefreshCache();
            return MapEntry(created);
        },
        "AD_GROUP_CREATED",
        "Active Directory group created.",
        StatusCodes.Status201Created));
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> AddGroupMemberAsync(
        string customerReference,
        string? groupSamAccountName,
        string? userSamAccountName,
        CancellationToken cancellationToken)
    {
        if (!_configuration.WritesEnabled)
        {
            return Task.FromResult(ReadOnlyResult());
        }

        return Task.FromResult(UpdateMembership(
            customerReference,
            groupSamAccountName,
            userSamAccountName,
            shouldAdd: true,
            "AD_GROUP_MEMBER_ADDED",
            "Active Directory group membership updated."));
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> RemoveGroupMemberAsync(
        string customerReference,
        string? groupSamAccountName,
        string? userSamAccountName,
        CancellationToken cancellationToken)
    {
        if (!_configuration.WritesEnabled)
        {
            return Task.FromResult(ReadOnlyResult());
        }

        return Task.FromResult(UpdateMembership(
            customerReference,
            groupSamAccountName,
            userSamAccountName,
            shouldAdd: false,
            "AD_GROUP_MEMBER_REMOVED",
            "Active Directory group membership removed."));
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> DisableUserAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken)
    {
        if (!_configuration.WritesEnabled)
        {
            return Task.FromResult(ReadOnlyResult());
        }

        var resolvedUser = ResolveBySam(customerReference, samAccountName, "user");
        if (resolvedUser.Value is null)
        {
            return Task.FromResult(resolvedUser);
        }

        if (resolvedUser.Value.IsDisabled)
        {
            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status200OK,
                "AD_USER_ALREADY_DISABLED",
                "Active Directory user is already disabled.",
                resolvedUser.Value,
                false));
        }

        return Task.FromResult(ExecuteWrite(() =>
        {
            using var user = BindEntry(resolvedUser.Value.DistinguishedName);
            user.RefreshCache(["userAccountControl"]);
            var currentUac = Convert.ToInt32(
                user.Properties["userAccountControl"].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            user.Properties["userAccountControl"].Value = currentUac | 0x2;
            user.CommitChanges();
            user.RefreshCache();
            return MapEntry(user);
        },
        "AD_USER_DISABLED",
        "Active Directory user disabled."));
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserToDisabledAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken)
    {
        if (!_configuration.WritesEnabled)
        {
            return Task.FromResult(ReadOnlyResult());
        }

        var resolvedUser = ResolveBySam(customerReference, samAccountName, "user");
        if (resolvedUser.Value is null)
        {
            return Task.FromResult(resolvedUser);
        }

        if (!resolvedUser.Value.IsDisabled)
        {
            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status409Conflict,
                "AD_USER_MUST_BE_DISABLED",
                "The user must be disabled before being moved to the Disabled OU."));
        }

        var targetParentDn = _scope.BuildDisabledOuDn(
            resolvedUser.Value.CustomerReference);
        var currentParentDn = resolvedUser.Value.DistinguishedName[
            (resolvedUser.Value.DistinguishedName.IndexOf(',') + 1)..];
        if (currentParentDn.Equals(
                targetParentDn,
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status200OK,
                "AD_USER_ALREADY_IN_DISABLED_OU",
                "Active Directory user is already in the Disabled OU.",
                resolvedUser.Value,
                false));
        }

        var targetDn = $"CN={ActiveDirectoryPathScope.EscapeRdnValue(resolvedUser.Value.SamAccountName)},{targetParentDn}";
        return Task.FromResult(ExecuteWrite(() =>
        {
            using var user = BindEntry(resolvedUser.Value.DistinguishedName);
            using var targetParent = BindEntry(targetParentDn);
            user.MoveTo(
                targetParent,
                $"CN={ActiveDirectoryPathScope.EscapeRdnValue(resolvedUser.Value.SamAccountName)}");
            user.CommitChanges();
            using var moved = BindEntry(targetDn);
            moved.RefreshCache();
            return MapEntry(moved);
        },
        "AD_USER_MOVED_TO_DISABLED",
        "Active Directory user moved to the Disabled OU."));
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> RenameUserAsync(
        string customerReference,
        string? currentSamAccountName,
        RenameAdUserRequest? request,
        CancellationToken cancellationToken)
    {
        if (!_configuration.WritesEnabled)
        {
            return Task.FromResult(ReadOnlyResult());
        }

        var resolvedUser = ResolveBySam(customerReference, currentSamAccountName, "user");
        if (resolvedUser.Value is null)
        {
            return Task.FromResult(resolvedUser);
        }

        var newSam = ActiveDirectoryInputValidator.NormalizeSamAccountName(
            request?.NewSamAccountName);
        var newDisplayName = request?.NewDisplayName?.Trim();
        if (newSam is null || string.IsNullOrWhiteSpace(newDisplayName))
        {
            return Task.FromResult(InvalidObject());
        }

        if (!ActiveDirectoryInputValidator.TryNormalizeUserPrincipalName(
                request!.NewUserPrincipalName,
                _configuration.Domain,
                out var newUpn))
        {
            return Task.FromResult(InvalidObject());
        }

        var resolvedSummary = resolvedUser.Value;
        var resolvedUpn = newUpn
            ?? $"{newSam}@{_configuration.Domain}";

        if (resolvedSummary.SamAccountName.Equals(newSam, StringComparison.OrdinalIgnoreCase)
            && string.Equals(resolvedSummary.DisplayName, newDisplayName, StringComparison.Ordinal)
            && string.Equals(
                resolvedSummary.UserPrincipalName,
                resolvedUpn,
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status200OK,
                "AD_USER_RENAME_NOOP",
                "Active Directory user already matches the requested attributes.",
                resolvedSummary,
                false));
        }

        var currentParentDn = resolvedSummary.DistinguishedName[
            (resolvedSummary.DistinguishedName.IndexOf(',') + 1)..];
        var newCnRdn = $"CN={ActiveDirectoryPathScope.EscapeRdnValue(newSam)}";
        var newDn = $"{newCnRdn},{currentParentDn}";

        return Task.FromResult(ExecuteWrite(() =>
        {
            using var user = BindEntry(resolvedSummary.DistinguishedName);
            if (!resolvedSummary.SamAccountName.Equals(
                    newSam,
                    StringComparison.OrdinalIgnoreCase))
            {
                using var parent = BindEntry(currentParentDn);
                user.MoveTo(parent, newCnRdn);
                user.CommitChanges();
            }

            user.Properties["sAMAccountName"].Value = newSam;
            user.Properties["displayName"].Value = newDisplayName;
            user.Properties["userPrincipalName"].Value = resolvedUpn;
            user.CommitChanges();

            using var refreshed = BindEntry(newDn);
            refreshed.RefreshCache();
            return MapEntry(refreshed);
        },
        "AD_USER_RENAMED",
        "Active Directory user renamed."));
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> MoveUserAsync(
        string customerReference,
        string? samAccountName,
        MoveAdUserRequest? request,
        CancellationToken cancellationToken)
    {
        if (!_configuration.WritesEnabled)
        {
            return Task.FromResult(ReadOnlyResult());
        }

        var resolvedUser = ResolveBySam(customerReference, samAccountName, "user");
        if (resolvedUser.Value is null)
        {
            return Task.FromResult(resolvedUser);
        }

        var targetCustomerReference =
            ActiveDirectoryInputValidator.NormalizeCustomerReference(
                request?.TargetCustomerReference);
        var targetContainer = ActiveDirectoryInputValidator.NormalizeMoveContainer(
            request?.TargetContainer);
        if (targetCustomerReference is null || targetContainer is null)
        {
            return Task.FromResult(InvalidObject());
        }

        var resolvedSummary = resolvedUser.Value;
        var targetParentDn = targetContainer == "Users"
            ? _scope.BuildUsersOuDn(targetCustomerReference)
            : _scope.BuildDisabledOuDn(targetCustomerReference);
        var currentParentDn = resolvedSummary.DistinguishedName[
            (resolvedSummary.DistinguishedName.IndexOf(',') + 1)..];
        if (currentParentDn.Equals(targetParentDn, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status200OK,
                "AD_USER_MOVE_NOOP",
                "Active Directory user is already at the requested location.",
                resolvedSummary,
                false));
        }

        var targetDn = $"CN={ActiveDirectoryPathScope.EscapeRdnValue(resolvedSummary.SamAccountName)},{targetParentDn}";

        return Task.FromResult(ExecuteWrite(() =>
        {
            using var user = BindEntry(resolvedSummary.DistinguishedName);
            using var targetParent = BindEntry(targetParentDn);
            user.MoveTo(targetParent);
            user.CommitChanges();
            using var moved = BindEntry(targetDn);
            moved.RefreshCache();
            return MapEntry(moved);
        },
        "AD_USER_MOVED",
        "Active Directory user moved."));
    }

    public Task<AdServiceResult<AdDirectoryObjectSummary>> ChangeUserPasswordAsync(
        string customerReference,
        string? samAccountName,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken)
    {
        if (!_configuration.WritesEnabled)
        {
            return Task.FromResult(ReadOnlyResult());
        }

        var resolvedUser = ResolveBySam(customerReference, samAccountName, "user");
        if (resolvedUser.Value is null)
        {
            return Task.FromResult(resolvedUser);
        }

        if (string.IsNullOrEmpty(currentPassword)
            || string.IsNullOrEmpty(newPassword))
        {
            return Task.FromResult(InvalidObject());
        }

        if (currentPassword.Length > 1024 || newPassword.Length > 1024)
        {
            return Task.FromResult(InvalidObject());
        }

        if (resolvedUser.Value.IsDisabled)
        {
            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status403Forbidden,
                "AD_USER_DISABLED",
                "L'utilisateur Active Directory est desactive.",
                resolvedUser.Value,
                false));
        }

        try
        {
            using var user = BindEntry(resolvedUser.Value.DistinguishedName);
            user.Invoke(
                "ChangePassword",
                new object[] { currentPassword, newPassword });
            user.CommitChanges();
            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status200OK,
                "AD_PASSWORD_CHANGED",
                "Active Directory password changed.",
                resolvedUser.Value,
                true));
        }
        catch (System.Reflection.TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            _logger.LogWarning(
                "Active Directory password change refused without exposing details exception_type {ExceptionType}",
                exception.InnerException.GetType().Name);
            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status400BadRequest,
                "AD_PASSWORD_CHANGE_FAILED",
                "Le mot de passe ne respecte pas la politique du domaine ou le mot de passe actuel est incorrect.",
                resolvedUser.Value,
                false));
        }
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            _logger.LogWarning(
                "Active Directory password change failed without exposing target details exception_type {ExceptionType}",
                exception.GetType().Name);
            return Task.FromResult(new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status503ServiceUnavailable,
                "AD_UNAVAILABLE",
                "Active Directory is temporarily unavailable.",
                resolvedUser.Value,
                false));
        }
    }

    public Task<AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>> GetUserEffectiveGroupsAsync(
        string customerReference,
        string? samAccountName,
        CancellationToken cancellationToken)
    {
        var resolvedUser = ResolveBySam(customerReference, samAccountName, "user");
        if (resolvedUser.Value is null)
        {
            return Task.FromResult(
                new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(
                    resolvedUser.StatusCode,
                    resolvedUser.Code,
                    resolvedUser.Message,
                    Array.Empty<AdDirectoryObjectSummary>()));
        }

        try
        {
            using var root = BindEntry(_scope.ClientsOuDn);
            var escapedUserDn = ActiveDirectoryPathScope.EscapeLdapFilterValue(
                resolvedUser.Value.DistinguishedName);
            // LDAP_MATCHING_RULE_IN_CHAIN (1.2.840.113556.1.4.1941) returns
            // direct AND transitive group memberships.
            var filter =
                $"(&(objectClass=group)(member:1.2.840.113556.1.4.1941:={escapedUserDn}))";
            using var searcher = CreateSearcher(root, filter);
            using var results = searcher.FindAll();
            var groups = results
                .Cast<SearchResult>()
                .Select(result => MapEffectiveGroupResult(
                    result,
                    resolvedUser.Value.CustomerReference))
                .OrderBy(group => group.SamAccountName, StringComparer.OrdinalIgnoreCase)
                .Take(_configuration.MaxResults)
                .ToArray();

            return Task.FromResult(
                new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(
                    StatusCodes.Status200OK,
                    "AD_USER_GROUPS_FOUND",
                    "Active Directory user effective groups resolved.",
                    groups));
        }
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            _logger.LogWarning(
                "Active Directory effective groups lookup failed without exposing target details exception_type {ExceptionType}",
                exception.GetType().Name);
            return Task.FromResult(
                new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(
                    StatusCodes.Status503ServiceUnavailable,
                    "AD_UNAVAILABLE",
                    "Active Directory is temporarily unavailable.",
                    Array.Empty<AdDirectoryObjectSummary>()));
        }
    }

    private AdDirectoryObjectSummary MapEffectiveGroupResult(
        SearchResult result,
        string fallbackCustomerReference)
    {
        var distinguishedName = _scope.NormalizeDistinguishedName(
            result.Properties["distinguishedName"].Count > 0
                ? result.Properties["distinguishedName"][0]?.ToString()
                : null)
            ?? throw new InvalidOperationException(
                "The Active Directory group distinguishedName is unavailable.");
        string samAccountName =
            (result.Properties["sAMAccountName"].Count > 0
                ? result.Properties["sAMAccountName"][0]?.ToString()
                : null)
            ?? throw new InvalidOperationException(
                "The Active Directory group sAMAccountName is unavailable.");
        string displayName =
            (result.Properties["displayName"].Count > 0
                ? result.Properties["displayName"][0]?.ToString()
                : null)
            ?? samAccountName;
        var objectGuid =
            result.Properties["objectGUID"].Count > 0
            && result.Properties["objectGUID"][0] is byte[] guidBytes
                ? new Guid(guidBytes).ToString("D")
                : $"ad-effective-group-{samAccountName.ToLowerInvariant()}";
        var objectSid =
            result.Properties["objectSid"].Count > 0
            && result.Properties["objectSid"][0] is byte[] sidBytes
                ? new SecurityIdentifier(sidBytes, 0).ToString()
                : "S-1-0-0";
        var customerReference =
            _scope.ExtractCustomerReference(distinguishedName)
            ?? fallbackCustomerReference;

        return new AdDirectoryObjectSummary(
            objectGuid,
            objectSid,
            "group",
            samAccountName,
            null,
            displayName,
            distinguishedName,
            customerReference,
            false);
    }

    private AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>> SearchDirectory(
        string objectType,
        string? query,
        string? customerReference)
    {
        var readinessFailure = ValidateReadiness();
        if (readinessFailure is not null)
        {
            return new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(
                readinessFailure.StatusCode,
                readinessFailure.Code,
                readinessFailure.Message,
                Array.Empty<AdDirectoryObjectSummary>());
        }

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

        var rootDn = BuildSearchRootDistinguishedName(
            objectType,
            normalizedCustomerReference);
        var filter = BuildSearchFilter(objectType, normalizedQuery);
        try
        {
            using var root = BindEntry(rootDn);
            using var searcher = CreateSearcher(root, filter);
            using var results = searcher.FindAll();
            var rawCount = results.Count;
            var objects = results
                .Cast<SearchResult>()
                .Select(result => MapEntry(result.GetDirectoryEntry()))
                .Where(entry =>
                    normalizedCustomerReference is null
                    || entry.CustomerReference.Equals(
                        normalizedCustomerReference,
                        StringComparison.OrdinalIgnoreCase))
                .Take(_configuration.MaxResults)
                .ToArray();

            _logger.LogInformation(
                "Active Directory search object_type {ObjectType} root_dn {RootDn} filter {Filter} raw_count {RawCount} filtered_count {FilteredCount}",
                objectType,
                rootDn,
                filter,
                rawCount,
                objects.Length);

            return new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(
                StatusCodes.Status200OK,
                objectType == "user"
                    ? "AD_USERS_FOUND"
                    : "AD_GROUPS_FOUND",
                "Active Directory search completed.",
                objects);
        }
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            _logger.LogWarning(
                "Active Directory search failed without exposing query details exception_type {ExceptionType}",
                exception.GetType().Name);
            return new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(
                StatusCodes.Status503ServiceUnavailable,
                "AD_UNAVAILABLE",
                "Active Directory is temporarily unavailable.",
                Array.Empty<AdDirectoryObjectSummary>());
        }
    }

    private AdServiceResult<AdDirectoryObjectSummary> ResolveByDn(
        string customerReference,
        string normalizedDn)
    {
        var readinessFailure = ValidateReadiness();
        if (readinessFailure is not null)
        {
            return readinessFailure;
        }

        if (!_scope.IsWithinAllowedRoot(normalizedDn))
        {
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status403Forbidden,
                "AD_TARGET_OUTSIDE_ALLOWED_OU",
                "The requested Active Directory target is outside the allowed OU.");
        }

        try
        {
            using var entry = BindEntry(normalizedDn);
            entry.RefreshCache();
            var directoryObject = MapEntry(entry);
            if (!directoryObject.CustomerReference.Equals(
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
                "Active Directory object resolved.",
                directoryObject);
        }
        catch (DirectoryServicesCOMException exception)
            when (IsNoSuchObject(exception))
        {
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status404NotFound,
                "AD_OBJECT_NOT_FOUND",
                "The requested Active Directory object was not found.");
        }
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            _logger.LogWarning(
                "Active Directory object lookup failed without exposing DN details exception_type {ExceptionType}",
                exception.GetType().Name);
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status503ServiceUnavailable,
                "AD_UNAVAILABLE",
                "Active Directory is temporarily unavailable.");
        }
    }

    private AdServiceResult<AdDirectoryObjectSummary> ResolveBySam(
        string customerReference,
        string? samAccountName,
        string objectType)
    {
        var readinessFailure = ValidateReadiness();
        if (readinessFailure is not null)
        {
            return readinessFailure;
        }

        var normalizedCustomerReference =
            ActiveDirectoryInputValidator.NormalizeCustomerReference(
                customerReference);
        var normalizedSamAccountName =
            ActiveDirectoryInputValidator.NormalizeSamAccountName(
                samAccountName);
        if (normalizedCustomerReference is null
            || normalizedSamAccountName is null)
        {
            return InvalidObject();
        }

        try
        {
            using var scopedRoot = BindEntry(
                BuildSearchRootDistinguishedName(
                    objectType,
                    normalizedCustomerReference));
            using var scopedSearcher = CreateSearcher(
                scopedRoot,
                BuildExactSamFilter(objectType, normalizedSamAccountName));
            using var scopedResults = scopedSearcher.FindAll();
            if (scopedResults.Count > 0)
            {
                using var directoryEntry = scopedResults[0].GetDirectoryEntry();
                var directoryObject = MapEntry(directoryEntry);
                if (!directoryObject.CustomerReference.Equals(
                        normalizedCustomerReference,
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
                    "Active Directory object resolved.",
                    directoryObject);
            }

            using var root = BindEntry(_scope.ClientsOuDn);
            using var searcher = CreateSearcher(
                root,
                BuildExactSamFilter(objectType, normalizedSamAccountName));
            using var results = searcher.FindAll();
            foreach (SearchResult result in results)
            {
                using var candidateEntry = result.GetDirectoryEntry();
                var directoryObject = MapEntry(candidateEntry);
                if (!directoryObject.CustomerReference.Equals(
                        normalizedCustomerReference,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new AdServiceResult<AdDirectoryObjectSummary>(
                        StatusCodes.Status403Forbidden,
                        "AD_CROSS_CUSTOMER_FORBIDDEN",
                        "Cross-customer Active Directory operations are not allowed.");
                }
            }

            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status404NotFound,
                "AD_OBJECT_NOT_FOUND",
                "The requested Active Directory object was not found.");
        }
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            _logger.LogWarning(
                "Active Directory SAM lookup failed without exposing account names exception_type {ExceptionType}",
                exception.GetType().Name);
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status503ServiceUnavailable,
                "AD_UNAVAILABLE",
                "Active Directory is temporarily unavailable.");
        }
    }

    private AdServiceResult<AdDirectoryObjectSummary> UpdateMembership(
        string customerReference,
        string? groupSamAccountName,
        string? userSamAccountName,
        bool shouldAdd,
        string successCode,
        string successMessage)
    {
        var resolvedGroup = ResolveBySam(customerReference, groupSamAccountName, "group");
        if (resolvedGroup.Value is null)
        {
            return resolvedGroup;
        }

        var resolvedUser = ResolveBySam(customerReference, userSamAccountName, "user");
        if (resolvedUser.Value is null)
        {
            return resolvedUser;
        }

        return ExecuteWrite(() =>
        {
            using var group = BindEntry(resolvedGroup.Value.DistinguishedName);
            var memberDn = resolvedUser.Value.DistinguishedName;
            var members = group.Properties["member"];
            var alreadyPresent = members.Cast<object>()
                .Select(value => value?.ToString())
                .Any(value => string.Equals(
                    value,
                    memberDn,
                    StringComparison.OrdinalIgnoreCase));

            if (shouldAdd && alreadyPresent)
            {
                group.RefreshCache();
                return new AdServiceResult<AdDirectoryObjectSummary>(
                    StatusCodes.Status200OK,
                    "AD_GROUP_MEMBER_ALREADY_PRESENT",
                    "Active Directory group membership is already present.",
                    MapEntry(group),
                    false);
            }

            if (!shouldAdd && !alreadyPresent)
            {
                group.RefreshCache();
                return new AdServiceResult<AdDirectoryObjectSummary>(
                    StatusCodes.Status200OK,
                    "AD_GROUP_MEMBER_ALREADY_ABSENT",
                    "Active Directory group membership is already absent.",
                    MapEntry(group),
                    false);
            }

            if (shouldAdd)
            {
                members.Add(memberDn);
                group.CommitChanges();
            }
            else
            {
                members.Remove(memberDn);
                group.CommitChanges();
            }

            group.RefreshCache();
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status200OK,
                successCode,
                successMessage,
                MapEntry(group),
                true);
        });
    }

    private AdServiceResult<AdDirectoryObjectSummary> ExecuteWrite(
        Func<AdServiceResult<AdDirectoryObjectSummary>> writeAction)
    {
        var readinessFailure = ValidateReadiness();
        if (readinessFailure is not null)
        {
            return readinessFailure;
        }

        try
        {
            return writeAction();
        }
        catch (DirectoryServicesCOMException exception)
            when (IsAlreadyExists(exception))
        {
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status409Conflict,
                "AD_OBJECT_ALREADY_EXISTS",
                "The requested Active Directory object already exists.");
        }
        catch (DirectoryServicesCOMException exception)
            when (IsNoSuchObject(exception))
        {
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status404NotFound,
                "AD_OBJECT_NOT_FOUND",
                "The requested Active Directory object was not found.");
        }
        catch (DirectoryServicesCOMException exception)
            when (IsAccessDenied(exception))
        {
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status403Forbidden,
                "AD_SCOPE_NOT_ALLOWED",
                "The requested Active Directory operation is outside the allowed scope.");
        }
        catch (DirectoryServicesCOMException exception)
            when (IsConstraintViolation(exception))
        {
            // AD utilise CONSTRAINT_VIOLATION pour les conflits de
            // sAMAccountName en doublon (contrainte d'unicite domaine)
            // en plus de LDAP_ALREADY_EXISTS sur le DN cible.
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status409Conflict,
                "AD_OBJECT_ALREADY_EXISTS",
                "The requested Active Directory object name or attribute is already in use.");
        }
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            var hresult = exception is DirectoryServicesCOMException dsex
                ? dsex.ErrorCode
                : exception is COMException comex
                    ? comex.ErrorCode
                    : 0;
            _logger.LogWarning(
                "Active Directory write failed without exposing target details exception_type {ExceptionType} hresult 0x{Hresult:X8}",
                exception.GetType().Name,
                hresult);
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status503ServiceUnavailable,
                "AD_UNAVAILABLE",
                "Active Directory is temporarily unavailable.");
        }
    }

    private AdServiceResult<AdDirectoryObjectSummary> ExecuteWrite(
        Func<AdDirectoryObjectSummary> writeAction,
        string successCode,
        string successMessage,
        int successStatusCode = StatusCodes.Status200OK)
    {
        return ExecuteWrite(() => new AdServiceResult<AdDirectoryObjectSummary>(
                successStatusCode,
                successCode,
                successMessage,
                writeAction(),
                true));
    }

    private AdServiceResult<AdDirectoryObjectSummary>? ValidateReadiness()
    {
        if (!_configuration.ConfigurationValid)
        {
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status503ServiceUnavailable,
                "AD_CONFIGURATION_INVALID",
                "Active Directory configuration is invalid.");
        }

        if (!_configuration.ReadsEnabled)
        {
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status501NotImplemented,
                "AD_INTEGRATION_DISABLED",
                "Active Directory integration is disabled.");
        }

        return null;
    }

    private DirectoryEntry BindEntry(string distinguishedName)
    {
        return new DirectoryEntry(
            $"LDAP://{_configuration.Domain}/{distinguishedName}",
            _configuration.ServiceAccountUsername,
            _configuration.ServiceAccountPassword,
            AuthenticationTypes.Secure
            | AuthenticationTypes.Sealing
            | AuthenticationTypes.Signing);
    }

    private DirectorySearcher CreateSearcher(
        DirectoryEntry root,
        string filter)
    {
        var searcher = new DirectorySearcher(root)
        {
            Filter = filter,
            SearchScope = SearchScope.Subtree,
            SizeLimit = _configuration.MaxResults,
            PageSize = _configuration.MaxResults,
            ClientTimeout = TimeSpan.FromMilliseconds(
                _configuration.QueryTimeoutMs),
            ServerTimeLimit = TimeSpan.FromMilliseconds(
                _configuration.QueryTimeoutMs)
        };
        searcher.PropertiesToLoad.Add("distinguishedName");
        searcher.PropertiesToLoad.Add("objectGUID");
        searcher.PropertiesToLoad.Add("objectSid");
        searcher.PropertiesToLoad.Add("objectClass");
        searcher.PropertiesToLoad.Add("sAMAccountName");
        searcher.PropertiesToLoad.Add("userPrincipalName");
        searcher.PropertiesToLoad.Add("displayName");
        searcher.PropertiesToLoad.Add("userAccountControl");
        return searcher;
    }

    private AdDirectoryObjectSummary MapEntry(DirectoryEntry entry)
    {
        var distinguishedName = _scope.NormalizeDistinguishedName(
            entry.Properties["distinguishedName"].Value?.ToString())
            ?? throw new InvalidOperationException(
                "The Active Directory object distinguishedName is unavailable.");
        var customerReference = _scope.ExtractCustomerReference(distinguishedName)
            ?? throw new InvalidOperationException(
                "The Active Directory object customer scope is unavailable.");
        var isGroup = entry.SchemaClassName.Equals(
            "group",
            StringComparison.OrdinalIgnoreCase);
        var samAccountName =
            entry.Properties["sAMAccountName"].Value?.ToString()
            ?? throw new InvalidOperationException(
                "The Active Directory object sAMAccountName is unavailable.");
        var displayName =
            entry.Properties["displayName"].Value?.ToString()
            ?? samAccountName;
        return new AdDirectoryObjectSummary(
            entry.Guid.ToString("D"),
            entry.Properties["objectSid"].Value is byte[] sidBytes
                ? new SecurityIdentifier(sidBytes, 0).ToString()
                : "S-1-0-0",
            isGroup ? "group" : "user",
            samAccountName,
            entry.Properties["userPrincipalName"].Value?.ToString(),
            displayName,
            distinguishedName,
            customerReference,
            !isGroup && entry.Properties["userAccountControl"].Value is not null
                && (Convert.ToInt32(
                        entry.Properties["userAccountControl"].Value,
                        System.Globalization.CultureInfo.InvariantCulture)
                    & 0x2) == 0x2);
    }

    private static string BuildSearchFilter(string objectType, string query)
    {
        var escapedQuery = ActiveDirectoryPathScope.EscapeLdapFilterValue(query);
        var containsExpression = string.IsNullOrEmpty(query)
            ? string.Empty
            : $"(|(sAMAccountName=*{escapedQuery}*)(displayName=*{escapedQuery}*)(userPrincipalName=*{escapedQuery}*))";

        return objectType == "user"
            ? $"(&(objectCategory=person)(objectClass=user){containsExpression})"
            : $"(&(objectClass=group){containsExpression})";
    }

    private static string BuildExactSamFilter(
        string objectType,
        string samAccountName)
    {
        var escapedSam = ActiveDirectoryPathScope.EscapeLdapFilterValue(
            samAccountName);
        return objectType == "user"
            ? $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={escapedSam}))"
            : $"(&(objectClass=group)(sAMAccountName={escapedSam}))";
    }

    private string BuildSearchRootDistinguishedName(
        string objectType,
        string? customerReference)
    {
        if (string.IsNullOrWhiteSpace(customerReference))
        {
            return _scope.ClientsOuDn;
        }

        return objectType == "user"
            ? _scope.BuildUsersOuDn(customerReference)
            : _scope.BuildGroupsOuDn(customerReference);
    }

    private static bool IsDirectoryFailure(Exception exception)
        => exception is DirectoryServicesCOMException
            or COMException
            or InvalidOperationException
            or UnauthorizedAccessException;

    private static bool IsAlreadyExists(DirectoryServicesCOMException exception)
        => exception.ErrorCode == unchecked((int)0x80072035);

    private static bool IsNoSuchObject(DirectoryServicesCOMException exception)
        => exception.ErrorCode == unchecked((int)0x80072030);

    private static bool IsAccessDenied(DirectoryServicesCOMException exception)
        => exception.ErrorCode == unchecked((int)0x80072098)
            || exception.ErrorCode == unchecked((int)0x80070005);

    private static bool IsConstraintViolation(DirectoryServicesCOMException exception)
        => exception.ErrorCode == unchecked((int)0x8007202F);

    private static AdServiceResult<AdDirectoryObjectSummary> InvalidObject()
        => new(
            StatusCodes.Status400BadRequest,
            "INVALID_REQUEST",
            "The requested Active Directory payload is invalid.");

    private static AdServiceResult<AdDirectoryObjectSummary> ReadOnlyResult()
        => new(
            StatusCodes.Status403Forbidden,
            "AD_READ_ONLY",
            "Active Directory writes are disabled in read-only mode.");
}
