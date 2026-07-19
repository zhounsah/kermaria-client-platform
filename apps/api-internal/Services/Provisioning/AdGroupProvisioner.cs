using System.DirectoryServices;
using System.Runtime.InteropServices;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed record AdGroupProvisionerResult(
    int StatusCode,
    string Code,
    string Message,
    bool Changed);

public interface IAdGroupProvisioner
{
    string ModeName { get; }
    bool RequiresConfiguredGroupDistinguishedNames { get; }

    Task<AdGroupProvisionerResult> AddUserToGroupAsync(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        string? groupDistinguishedName,
        CancellationToken cancellationToken);

    Task<AdGroupProvisionerResult> RemoveUserFromGroupAsync(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        string? groupDistinguishedName,
        CancellationToken cancellationToken);
}

public sealed class DisabledAdGroupProvisioner : IAdGroupProvisioner
{
    public string ModeName => "disabled";

    public bool RequiresConfiguredGroupDistinguishedNames => false;

    public Task<AdGroupProvisionerResult> AddUserToGroupAsync(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        string? groupDistinguishedName,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledResult());

    public Task<AdGroupProvisionerResult> RemoveUserFromGroupAsync(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        string? groupDistinguishedName,
        CancellationToken cancellationToken)
        => Task.FromResult(DisabledResult());

    private static AdGroupProvisionerResult DisabledResult()
        => new(
            StatusCodes.Status501NotImplemented,
            "AD_INTEGRATION_DISABLED",
            "Active Directory provisioning is disabled.",
            false);
}

public sealed class MockAdGroupProvisioner : IAdGroupProvisioner
{
    private readonly MockAdGroupMembershipStore _memberships;

    public MockAdGroupProvisioner(MockAdGroupMembershipStore memberships)
    {
        _memberships = memberships;
    }

    public string ModeName => "mock";

    public bool RequiresConfiguredGroupDistinguishedNames => false;

    public Task<AdGroupProvisionerResult> AddUserToGroupAsync(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        string? groupDistinguishedName,
        CancellationToken cancellationToken)
        => Task.FromResult(UpdateMembership(
            user,
            groupSamAccountName,
            shouldAdd: true));

    public Task<AdGroupProvisionerResult> RemoveUserFromGroupAsync(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        string? groupDistinguishedName,
        CancellationToken cancellationToken)
        => Task.FromResult(UpdateMembership(
            user,
            groupSamAccountName,
            shouldAdd: false));

    private AdGroupProvisionerResult UpdateMembership(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        bool shouldAdd)
    {
        if (shouldAdd)
        {
            if (!_memberships.AddMembership(
                    groupSamAccountName,
                    user.SamAccountName))
            {
                return new AdGroupProvisionerResult(
                    StatusCodes.Status200OK,
                    "AD_GROUP_MEMBER_ALREADY_PRESENT",
                    "Active Directory group membership already exists in mock mode.",
                    false);
            }

            return new AdGroupProvisionerResult(
                StatusCodes.Status200OK,
                "AD_GROUP_MEMBER_ADDED",
                "Active Directory group membership added in mock mode.",
                true);
        }

        if (!_memberships.RemoveMembership(
                groupSamAccountName,
                user.SamAccountName))
        {
            return new AdGroupProvisionerResult(
                StatusCodes.Status200OK,
                "AD_GROUP_MEMBER_ALREADY_ABSENT",
                "Active Directory group membership already absent in mock mode.",
                false);
        }

        return new AdGroupProvisionerResult(
            StatusCodes.Status200OK,
            "AD_GROUP_MEMBER_REMOVED",
            "Active Directory group membership removed in mock mode.",
            true);
    }
}

public sealed class LdapAdGroupProvisioner : IAdGroupProvisioner
{
    private const int AdsGroupTypeGlobal = 0x00000002;
    private const int AdsGroupTypeDomainLocal = 0x00000004;
    private const int AdsGroupTypeUniversal = 0x00000008;

    private readonly AdRuntimeConfiguration _configuration;
    private readonly ILogger<LdapAdGroupProvisioner> _logger;

    public LdapAdGroupProvisioner(
        AdRuntimeConfiguration configuration,
        ILogger<LdapAdGroupProvisioner> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string ModeName => _configuration.ModeName;

    public bool RequiresConfiguredGroupDistinguishedNames => true;

    public Task<AdGroupProvisionerResult> AddUserToGroupAsync(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        string? groupDistinguishedName,
        CancellationToken cancellationToken)
        => Task.FromResult(UpdateMembership(
            user,
            groupSamAccountName,
            groupDistinguishedName,
            shouldAdd: true));

    public Task<AdGroupProvisionerResult> RemoveUserFromGroupAsync(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        string? groupDistinguishedName,
        CancellationToken cancellationToken)
        => Task.FromResult(UpdateMembership(
            user,
            groupSamAccountName,
            groupDistinguishedName,
            shouldAdd: false));

    private AdGroupProvisionerResult UpdateMembership(
        CustomerAdLinkSummary user,
        string groupSamAccountName,
        string? groupDistinguishedName,
        bool shouldAdd)
    {
        if (!_configuration.ConfigurationValid)
        {
            return new AdGroupProvisionerResult(
                StatusCodes.Status503ServiceUnavailable,
                "AD_CONFIGURATION_INVALID",
                "Active Directory configuration is invalid.",
                false);
        }

        if (!_configuration.WritesEnabled)
        {
            return new AdGroupProvisionerResult(
                StatusCodes.Status403Forbidden,
                "AD_READ_ONLY",
                "Active Directory writes are disabled in read-only mode.",
                false);
        }

        var normalizedGroupDn = _configuration.NormalizeDistinguishedName(
            groupDistinguishedName);
        var normalizedUserDn = _configuration.NormalizeDistinguishedName(
            user.DistinguishedName);

        if (normalizedGroupDn is null)
        {
            return new AdGroupProvisionerResult(
                StatusCodes.Status400BadRequest,
                "PROVISIONING_GROUP_NOT_CONFIGURED",
                $"No distinguished name is configured for group {groupSamAccountName}.",
                false);
        }

        if (normalizedUserDn is null)
        {
            return new AdGroupProvisionerResult(
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                "The linked Active Directory user distinguished name is invalid.",
                false);
        }

        if (!_configuration.IsWithinAllowedRoots(normalizedGroupDn)
            || !_configuration.IsWithinAllowedRoots(normalizedUserDn))
        {
            return new AdGroupProvisionerResult(
                StatusCodes.Status403Forbidden,
                "AD_TARGET_OUTSIDE_ALLOWED_ROOTS",
                "The Active Directory target is outside the configured allowed roots.",
                false);
        }

        try
        {
            using var group = BindEntry(normalizedGroupDn);
            group.RefreshCache(["member", "groupType"]);
            var members = group.Properties["member"];
            var groupScope = ResolveGroupScope(group);
            var groupDomain = _configuration.ResolveDomainForDistinguishedName(
                normalizedGroupDn);
            var userDomain = _configuration.ResolveDomainForDistinguishedName(
                normalizedUserDn);
            var isCrossDomainMembership =
                !string.IsNullOrWhiteSpace(groupDomain)
                && !string.IsNullOrWhiteSpace(userDomain)
                && !groupDomain.Equals(
                    userDomain,
                    StringComparison.OrdinalIgnoreCase);
            var alreadyPresent = members.Cast<object>()
                .Any(member => string.Equals(
                    member?.ToString(),
                    normalizedUserDn,
                    StringComparison.OrdinalIgnoreCase));

            if (shouldAdd)
            {
                if (alreadyPresent)
                {
                    return new AdGroupProvisionerResult(
                        StatusCodes.Status200OK,
                        "AD_GROUP_MEMBER_ALREADY_PRESENT",
                        "Active Directory group membership already exists.",
                        false);
                }

                // Global groups cannot hold direct members from another domain.
                if (isCrossDomainMembership
                    && groupScope == AdSecurityGroupScope.Global)
                {
                    return new AdGroupProvisionerResult(
                        StatusCodes.Status409Conflict,
                        "AD_GROUP_SCOPE_INCOMPATIBLE",
                        "Cross-domain provisioning requires the target group to be universal or domain-local.",
                        false);
                }

                members.Add(normalizedUserDn);
                group.CommitChanges();
                return new AdGroupProvisionerResult(
                    StatusCodes.Status200OK,
                    "AD_GROUP_MEMBER_ADDED",
                    "Active Directory group membership added.",
                    true);
            }

            if (!alreadyPresent)
            {
                return new AdGroupProvisionerResult(
                    StatusCodes.Status200OK,
                    "AD_GROUP_MEMBER_ALREADY_ABSENT",
                    "Active Directory group membership already absent.",
                    false);
            }

            members.Remove(normalizedUserDn);
            group.CommitChanges();
            return new AdGroupProvisionerResult(
                StatusCodes.Status200OK,
                "AD_GROUP_MEMBER_REMOVED",
                "Active Directory group membership removed.",
                true);
        }
        catch (DirectoryServicesCOMException exception)
            when (IsNoSuchObject(exception))
        {
            return new AdGroupProvisionerResult(
                StatusCodes.Status404NotFound,
                "AD_OBJECT_NOT_FOUND",
                "The requested Active Directory object could not be found.",
                false);
        }
        catch (DirectoryServicesCOMException exception)
            when (IsAccessDenied(exception))
        {
            return new AdGroupProvisionerResult(
                StatusCodes.Status403Forbidden,
                "AD_ACCESS_DENIED",
                "The Active Directory operation was refused.",
                false);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Active Directory provisioning access denied for user {UserSamAccountName} group {GroupSamAccountName}",
                user.SamAccountName,
                groupSamAccountName);
            return new AdGroupProvisionerResult(
                StatusCodes.Status403Forbidden,
                "AD_ACCESS_DENIED",
                "The Active Directory operation was refused.",
                false);
        }
        catch (Exception exception) when (IsDirectoryFailure(exception))
        {
            _logger.LogWarning(
                exception,
                "Active Directory provisioning failed for user {UserSamAccountName} group {GroupSamAccountName}",
                user.SamAccountName,
                groupSamAccountName);
            return new AdGroupProvisionerResult(
                StatusCodes.Status503ServiceUnavailable,
                "AD_UNAVAILABLE",
                "Active Directory is temporarily unavailable.",
                false);
        }
    }

    private DirectoryEntry BindEntry(string distinguishedName)
    {
        var ldapPath = _configuration.BuildLdapPath(distinguishedName);
        if (_configuration.UseCurrentWindowsCredentials)
        {
            var entry = new DirectoryEntry(ldapPath);
            entry.AuthenticationType = AuthenticationTypes.Secure
                | AuthenticationTypes.Sealing
                | AuthenticationTypes.Signing;
            return entry;
        }

        return new DirectoryEntry(
            ldapPath,
            _configuration.ServiceAccountUsername,
            _configuration.ServiceAccountPassword,
            AuthenticationTypes.Secure
            | AuthenticationTypes.Sealing
            | AuthenticationTypes.Signing);
    }

    private static AdSecurityGroupScope ResolveGroupScope(
        DirectoryEntry group)
    {
        if (group.Properties["groupType"].Value is null)
        {
            return AdSecurityGroupScope.Unknown;
        }

        var groupType = Convert.ToInt32(
            group.Properties["groupType"].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        if ((groupType & AdsGroupTypeUniversal) == AdsGroupTypeUniversal)
        {
            return AdSecurityGroupScope.Universal;
        }

        if ((groupType & AdsGroupTypeDomainLocal) == AdsGroupTypeDomainLocal)
        {
            return AdSecurityGroupScope.DomainLocal;
        }

        if ((groupType & AdsGroupTypeGlobal) == AdsGroupTypeGlobal)
        {
            return AdSecurityGroupScope.Global;
        }

        return AdSecurityGroupScope.Unknown;
    }

    private static bool IsDirectoryFailure(Exception exception)
        => exception is DirectoryServicesCOMException
            or COMException
            or InvalidOperationException
            or UnauthorizedAccessException;

    private static bool IsNoSuchObject(DirectoryServicesCOMException exception)
        => exception.ErrorCode == unchecked((int)0x80072030);

    private static bool IsAccessDenied(DirectoryServicesCOMException exception)
        => exception.ErrorCode == unchecked((int)0x80072098)
            || exception.ErrorCode == unchecked((int)0x80070005);
}

internal enum AdSecurityGroupScope
{
    Unknown = 0,
    DomainLocal = 1,
    Global = 2,
    Universal = 3
}
