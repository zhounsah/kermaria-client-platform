using System.DirectoryServices;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
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

    /// <summary>
    /// Retrouve dans l'annuaire l'identite portant cet <c>employeeNumber</c>,
    /// ou <c>null</c> si elle n'existe pas.
    /// </summary>
    /// <remarks>
    /// KoXo inscrit l'identifiant unique du CSV (<c>CLI-NNNNNN</c>) dans
    /// <c>employeeNumber</c>. C'est la seule cle de rattachement fiable entre un
    /// compte cree par KoXo et l'utilisateur portail : le nom subit une
    /// translitteration (accents supprimes) et le sAMAccountName est derive par
    /// KoXo, donc ni l'un ni l'autre n'est predictible cote application.
    ///
    /// La recherche est bornee aux racines autorisees, et une correspondance
    /// multiple est traitee comme une absence : rattacher la mauvaise identite
    /// donnerait des droits reels au mauvais compte.
    /// </remarks>
    Task<AdDirectoryObjectSummary?> ResolveUserByEmployeeNumberAsync(
        string employeeNumber,
        CancellationToken cancellationToken);
}

public sealed class DisabledAdGroupProvisioner : IAdGroupProvisioner
{
    public string ModeName => "disabled";

    public bool RequiresConfiguredGroupDistinguishedNames => false;

    public Task<AdDirectoryObjectSummary?> ResolveUserByEmployeeNumberAsync(
        string employeeNumber,
        CancellationToken cancellationToken)
        => Task.FromResult<AdDirectoryObjectSummary?>(null);

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

    // Le mock ne simule pas d'annuaire peuple : aucune identite a rattacher.
    public Task<AdDirectoryObjectSummary?> ResolveUserByEmployeeNumberAsync(
        string employeeNumber,
        CancellationToken cancellationToken)
        => Task.FromResult<AdDirectoryObjectSummary?>(null);

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
    private const int AdsAccountDisabled = 0x00000002;
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

    public Task<AdDirectoryObjectSummary?> ResolveUserByEmployeeNumberAsync(
        string employeeNumber,
        CancellationToken cancellationToken)
    {
        if (!_configuration.ConfigurationValid
            || string.IsNullOrWhiteSpace(employeeNumber))
        {
            return Task.FromResult<AdDirectoryObjectSummary?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var matches = new List<AdDirectoryObjectSummary>();
            foreach (var root in _configuration.AllowedRoots)
            {
                matches.AddRange(SearchRoot(root, employeeNumber));
                if (matches.Count > 1)
                {
                    break;
                }
            }

            if (matches.Count != 1)
            {
                if (matches.Count > 1)
                {
                    // Rattacher la mauvaise identite donnerait des droits reels
                    // au mauvais compte : on refuse plutot que de choisir.
                    _logger.LogWarning(
                        "Several Active Directory identities carry employeeNumber {EmployeeNumber}; refusing to link.",
                        employeeNumber);
                }

                return Task.FromResult<AdDirectoryObjectSummary?>(null);
            }

            return Task.FromResult<AdDirectoryObjectSummary?>(matches[0]);
        }
        catch (COMException exception)
        {
            _logger.LogWarning(
                exception,
                "Active Directory lookup by employeeNumber {EmployeeNumber} failed.",
                employeeNumber);
            return Task.FromResult<AdDirectoryObjectSummary?>(null);
        }
    }

    private IEnumerable<AdDirectoryObjectSummary> SearchRoot(
        string rootDistinguishedName,
        string employeeNumber)
    {
        using var rootEntry = BindEntry(rootDistinguishedName);
        using var searcher = new DirectorySearcher(rootEntry)
        {
            Filter =
                "(&(objectClass=user)(objectCategory=person)(employeeNumber="
                + EscapeLdapValue(employeeNumber)
                + "))",
            SearchScope = SearchScope.Subtree,
            SizeLimit = 2,
            PageSize = 2
        };
        searcher.PropertiesToLoad.Add("sAMAccountName");
        searcher.PropertiesToLoad.Add("displayName");
        searcher.PropertiesToLoad.Add("userPrincipalName");
        searcher.PropertiesToLoad.Add("distinguishedName");
        searcher.PropertiesToLoad.Add("objectGUID");
        searcher.PropertiesToLoad.Add("objectSid");
        searcher.PropertiesToLoad.Add("userAccountControl");

        using var results = searcher.FindAll();
        var summaries = new List<AdDirectoryObjectSummary>();
        foreach (SearchResult result in results)
        {
            var distinguishedName = ReadSingle(result, "distinguishedName");
            // Ceinture et bretelles : la racine de recherche est deja autorisee,
            // mais une reference LDAP pourrait renvoyer un objet hors perimetre.
            if (distinguishedName is null
                || !_configuration.IsWithinAllowedRoots(distinguishedName))
            {
                continue;
            }

            var samAccountName = ReadSingle(result, "sAMAccountName");
            if (samAccountName is null)
            {
                continue;
            }

            var objectGuid =
                result.Properties["objectGUID"].Count > 0
                && result.Properties["objectGUID"][0] is byte[] guidBytes
                    ? new Guid(guidBytes).ToString("D")
                    : null;
            var objectSid =
                result.Properties["objectSid"].Count > 0
                && result.Properties["objectSid"][0] is byte[] sidBytes
                    ? new SecurityIdentifier(sidBytes, 0).ToString()
                    : null;
            if (objectGuid is null || objectSid is null)
            {
                continue;
            }

            var userAccountControl =
                result.Properties["userAccountControl"].Count > 0
                    ? Convert.ToInt32(
                        result.Properties["userAccountControl"][0],
                        CultureInfo.InvariantCulture)
                    : 0;

            summaries.Add(new AdDirectoryObjectSummary(
                objectGuid,
                objectSid,
                "user",
                samAccountName,
                ReadSingle(result, "userPrincipalName"),
                ReadSingle(result, "displayName") ?? samAccountName,
                distinguishedName,
                string.Empty,
                (userAccountControl & AdsAccountDisabled) != 0));
        }

        return summaries;
    }

    private static string? ReadSingle(SearchResult result, string propertyName)
        => result.Properties[propertyName].Count > 0
            ? result.Properties[propertyName][0]?.ToString()
            : null;

    /// <summary>Echappe les caracteres speciaux d'un filtre LDAP (RFC 4515).</summary>
    private static string EscapeLdapValue(string value)
        => value
            .Replace("\\", "\\5c", StringComparison.Ordinal)
            .Replace("*", "\\2a", StringComparison.Ordinal)
            .Replace("(", "\\28", StringComparison.Ordinal)
            .Replace(")", "\\29", StringComparison.Ordinal)
            .Replace("\0", "\\00", StringComparison.Ordinal);

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
