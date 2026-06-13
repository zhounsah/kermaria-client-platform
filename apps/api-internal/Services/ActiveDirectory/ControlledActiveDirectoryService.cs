using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.ActiveDirectory;

public sealed class ControlledActiveDirectoryService : IActiveDirectoryService
{
    private static readonly HashSet<string> ForbiddenAdministrativeGroups =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Domain Admins",
            "Enterprise Admins",
            "Schema Admins",
            "Administrators",
            "Account Operators",
            "Backup Operators",
            "Server Operators"
        };

    private readonly AdRuntimeConfiguration _configuration;

    public ControlledActiveDirectoryService(
        AdRuntimeConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AdHealthResponse GetHealth()
    {
        var status = !_configuration.ConfigurationValid
            ? "configuration_invalid"
            : _configuration.Mode == AdIntegrationMode.Enabled
                ? "validation_required"
                : "configuration_valid";

        return new AdHealthResponse(
            _configuration.ModeName,
            status,
            _configuration.ConfigurationValid,
            false);
    }

    public AdOperationResult ChangePassword(ChangePasswordRequest? request)
    {
        var configurationError = ValidateConfiguration();

        if (configurationError is not null)
        {
            return configurationError;
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.CurrentPassword)
            || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return InvalidRequest();
        }

        if (!_configuration.IsTargetInAllowedOu(
                request.TargetDistinguishedName))
        {
            return new AdOperationResult(
                StatusCodes.Status403Forbidden,
                "AD_TARGET_OUTSIDE_ALLOWED_OU",
                "La cible demandée n'est pas autorisée.");
        }

        return RealOperationNotEnabled();
    }

    public AdOperationResult CreateUser(CreateUserRequest? request)
    {
        var configurationError = ValidateConfiguration();

        if (configurationError is not null)
        {
            return configurationError;
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.AccountName)
            || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return InvalidRequest();
        }

        return RealOperationNotEnabled();
    }

    public AdOperationResult AddUserToGroup(GroupMembershipRequest? request)
        => ValidateGroupOperation(request);

    public AdOperationResult RemoveUserFromGroup(GroupMembershipRequest? request)
        => ValidateGroupOperation(request);

    private AdOperationResult ValidateGroupOperation(
        GroupMembershipRequest? request)
    {
        var configurationError = ValidateConfiguration();

        if (configurationError is not null)
        {
            return configurationError;
        }

        if (request is null
            || !_configuration.IsTargetInAllowedOu(
                request.TargetDistinguishedName)
            || string.IsNullOrWhiteSpace(request.GroupName)
            || ForbiddenAdministrativeGroups.Contains(request.GroupName)
            || !_configuration.IsGroupAllowed(request.GroupName))
        {
            return new AdOperationResult(
                StatusCodes.Status403Forbidden,
                "AD_SCOPE_NOT_ALLOWED",
                "La cible ou le groupe demandé n'est pas autorisé.");
        }

        return RealOperationNotEnabled();
    }

    private AdOperationResult? ValidateConfiguration()
    {
        return _configuration.ConfigurationValid
            ? null
            : new AdOperationResult(
                StatusCodes.Status503ServiceUnavailable,
                "AD_CONFIGURATION_INVALID",
                "La configuration Active Directory n'est pas valide.");
    }

    private static AdOperationResult InvalidRequest()
        => new(
            StatusCodes.Status400BadRequest,
            "INVALID_REQUEST",
            "La demande Active Directory est incomplète ou invalide.");

    private static AdOperationResult RealOperationNotEnabled()
        => new(
            StatusCodes.Status501NotImplemented,
            "AD_REAL_CHANGE_NOT_ENABLED",
            "L'opération Active Directory réelle n'est pas activée.");
}
