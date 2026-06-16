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
        var userPrincipalName = string.IsNullOrWhiteSpace(
                request!.UserPrincipalName)
            ? $"{samAccountName}@{_configuration.Domain}"
            : request.UserPrincipalName.Trim();

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

        try
        {
            using var root = BindEntry(
                BuildSearchRootDistinguishedName(
                    objectType,
                    normalizedCustomerReference));
            using var searcher = CreateSearcher(root, BuildSearchFilter(objectType, normalizedQuery));
            using var results = searcher.FindAll();
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

            if (shouldAdd && !alreadyPresent)
            {
                members.Add(memberDn);
                group.CommitChanges();
            }
            else if (!shouldAdd && alreadyPresent)
            {
                members.Remove(memberDn);
                group.CommitChanges();
            }

            group.RefreshCache();
            return MapEntry(group);
        },
        successCode,
        successMessage);
    }

    private AdServiceResult<AdDirectoryObjectSummary> ExecuteWrite(
        Func<AdDirectoryObjectSummary> writeAction,
        string successCode,
        string successMessage,
        int successStatusCode = StatusCodes.Status200OK)
    {
        var readinessFailure = ValidateReadiness();
        if (readinessFailure is not null)
        {
            return readinessFailure;
        }

        try
        {
            return new AdServiceResult<AdDirectoryObjectSummary>(
                successStatusCode,
                successCode,
                successMessage,
                writeAction(),
                true);
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
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            _logger.LogWarning(
                "Active Directory write failed without exposing target details exception_type {ExceptionType}",
                exception.GetType().Name);
            return new AdServiceResult<AdDirectoryObjectSummary>(
                StatusCodes.Status503ServiceUnavailable,
                "AD_UNAVAILABLE",
                "Active Directory is temporarily unavailable.");
        }
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
