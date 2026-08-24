using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Conversion d'un compte d'essai en client reel (V1.1 Lot 4).
///
/// <para>
/// Principe retenu en conception : <b>bascule sur place</b>. Le compte garde son
/// identite AD, son mot de passe, son profil et tout son contenu ; seules les
/// restrictions de demonstration sont levees. On ne recree jamais d'identite.
/// </para>
///
/// <para>
/// Ordre volontaire : l'annuaire d'abord, la base ensuite. Si l'AD echoue a
/// mi-parcours, la bascule en base n'a pas lieu et l'operation reste rejouable
/// telle quelle (retraits et ajouts de groupes sont idempotents). L'inverse
/// laisserait un client « reel » sans acces reel.
/// </para>
///
/// <para>
/// Frontiere assumee : ce service traite l'<b>acces technique</b> (groupes AD et
/// emplacement de l'identite). L'acte <b>commercial</b> — souscription,
/// facturation — reste le parcours existant et n'est pas duplique ici.
/// </para>
/// </summary>
public interface IDemoConversionService
{
    Task<DemoConversionResult> ConvertAsync(
        string customerReference,
        DemoConversionRequest request,
        string? actorUserId,
        CancellationToken cancellationToken);
}

public sealed class DemoConversionService : IDemoConversionService
{
    private readonly IDemoAccountRepository _accounts;
    private readonly IActiveDirectoryService _activeDirectory;
    private readonly IAdGroupProvisioner _groupProvisioner;
    private readonly SubscriptionProvisioningRuntimeConfiguration
        _provisioningConfiguration;
    private readonly IServiceTopologyService _topology;
    private readonly IActiveDirectoryLinkRepository _links;
    private readonly AdRuntimeConfiguration _adConfiguration;
    private readonly DemoConversionRuntimeConfiguration _conversionConfiguration;
    private readonly ILogger<DemoConversionService> _logger;

    public DemoConversionService(
        IDemoAccountRepository accounts,
        IActiveDirectoryService activeDirectory,
        IAdGroupProvisioner groupProvisioner,
        SubscriptionProvisioningRuntimeConfiguration provisioningConfiguration,
        IServiceTopologyService topology,
        IActiveDirectoryLinkRepository links,
        AdRuntimeConfiguration adConfiguration,
        DemoConversionRuntimeConfiguration conversionConfiguration,
        ILogger<DemoConversionService> logger)
    {
        _accounts = accounts;
        _activeDirectory = activeDirectory;
        _groupProvisioner = groupProvisioner;
        _provisioningConfiguration = provisioningConfiguration;
        _topology = topology;
        _links = links;
        _adConfiguration = adConfiguration;
        _conversionConfiguration = conversionConfiguration;
        _logger = logger;
    }

    public async Task<DemoConversionResult> ConvertAsync(
        string customerReference,
        DemoConversionRequest request,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var candidate = await _accounts.FindConversionCandidateAsync(
            customerReference,
            cancellationToken);
        if (candidate is null)
        {
            throw new DemoConflictException(
                "DEMO_CONVERSION_NOT_A_DEMO",
                "Cette reference ne designe pas un compte de demonstration.");
        }

        // Idempotence : un double clic ou un rejeu ne refait pas la bascule.
        if (candidate.AlreadyConverted)
        {
            return new DemoConversionResult(
                true,
                true,
                "DEMO_CONVERSION_ALREADY_DONE",
                candidate.CustomerReference,
                Array.Empty<string>(),
                Array.Empty<string>(),
                false);
        }

        // Une vitrine n'a aucun acces reel : la convertir n'a pas de sens, et
        // l'accepter silencieusement produirait un « client » sans identite.
        if (!string.Equals(candidate.DemoKind, DemoKinds.Trial, StringComparison.Ordinal))
        {
            throw new DemoConflictException(
                "DEMO_CONVERSION_NOT_A_TRIAL",
                "Seul un compte d'essai peut etre converti en client reel.");
        }

        var realGroups = await ResolveRealGroupsAsync(
            request.ServiceCodes,
            cancellationToken);
        var demoGroups = Normalize(candidate.AdGroups);

        var removed = new List<string>();
        var granted = new List<string>();
        var identityMoved = false;

        if (_adConfiguration.WritesEnabled
            && !string.IsNullOrWhiteSpace(candidate.PortalUserId))
        {
            var link = await _links.FindUserLinkByPortalUserIdAsync(
                candidate.PortalUserId,
                cancellationToken);
            if (link is null)
            {
                // Sans identite AD, il n'y a rien a basculer cote annuaire : on
                // refuse plutot que de marquer converti un compte qui n'a jamais
                // eu d'acces reel.
                throw new DemoConflictException(
                    "DEMO_CONVERSION_NO_IDENTITY",
                    "L'identite Active Directory de ce compte d'essai est introuvable.");
            }

            var target = ToLinkSummary(link);

            foreach (var group in demoGroups)
            {
                if (await ApplyMembershipAsync(
                        target,
                        group,
                        shouldAdd: false,
                        candidate.CustomerReference,
                        cancellationToken))
                {
                    removed.Add(group);
                }
                else
                {
                    return Partial(candidate, removed, granted, identityMoved);
                }
            }

            foreach (var group in realGroups)
            {
                if (await ApplyMembershipAsync(
                        target,
                        group,
                        shouldAdd: true,
                        candidate.CustomerReference,
                        cancellationToken))
                {
                    granted.Add(group);
                }
                else
                {
                    return Partial(candidate, removed, granted, identityMoved);
                }
            }

            identityMoved = await MoveIdentityAsync(
                link,
                candidate.CustomerReference,
                cancellationToken);
            if (!identityMoved
                && _conversionConfiguration.TargetOrganizationalUnitDn is not null)
            {
                return Partial(candidate, removed, granted, false);
            }
        }

        // Rattrapage des comptes crees avant la reservation systematique : sans
        // code reserve, l'export republierait la reference DEMO-* et KoXo
        // creerait une OU a ce nom. L'ecriture est conditionnee a l'absence de
        // code, donc sans effet sur un compte deja reserve.
        var reservedGroupReference =
            await _accounts.TryReserveGroupReferenceAsync(cancellationToken);
        if (reservedGroupReference is null)
        {
            throw new DemoConflictException(
                "DEMO_GROUP_REFERENCE_UNAVAILABLE",
                "Impossible de réserver un code de groupe unique pour ce compte.");
        }

        await _accounts.SetKoxoGroupReferenceAsync(
            candidate.CustomerId,
            reservedGroupReference,
            cancellationToken);

        await _accounts.MarkConvertedAsync(
            candidate.CustomerId,
            DateTime.UtcNow,
            actorUserId,
            candidate.ProfileKey,
            cancellationToken);

        _logger.LogInformation(
            "Demo conversion for {CustomerReference}: demoGroupsRemoved={Removed} realGroupsGranted={Granted} identityMoved={Moved}",
            candidate.CustomerReference,
            removed.Count,
            granted.Count,
            identityMoved);

        return new DemoConversionResult(
            true,
            false,
            "DEMO_CONVERSION_APPLIED",
            candidate.CustomerReference,
            removed,
            granted,
            identityMoved);
    }

    /// <summary>
    /// Deplace l'identite hors de l'OU de demonstration. Sans OU cible
    /// configuree, l'etape est volontairement neutre : le cloisonnement reste
    /// alors porte par les groupes seuls.
    /// </summary>
    private async Task<bool> MoveIdentityAsync(
        PortalUserAdLinkRecord link,
        string customerReference,
        CancellationToken cancellationToken)
    {
        var targetOu = _conversionConfiguration.TargetOrganizationalUnitDn;
        if (targetOu is null)
        {
            return false;
        }

        var result = await _activeDirectory.MoveUserAsync(
            link.CustomerReference,
            link.SamAccountName,
            new MoveAdUserRequest(null, null, targetOu),
            cancellationToken);
        if (result.StatusCode >= 400)
        {
            _logger.LogWarning(
                "Demo conversion: could not move {UserSam} to {TargetOu} for {CustomerReference} ({Code}).",
                link.SamAccountName,
                targetOu,
                customerReference,
                result.Code);
            return false;
        }

        return true;
    }

    private async Task<bool> ApplyMembershipAsync(
        CustomerAdLinkSummary target,
        string group,
        bool shouldAdd,
        string customerReference,
        CancellationToken cancellationToken)
    {
        var groupDn = ResolveGroupDistinguishedName(group);
        if (groupDn is null
            && _groupProvisioner.RequiresConfiguredGroupDistinguishedNames)
        {
            _logger.LogWarning(
                "Demo conversion: no distinguished name configured for {Group} (AD_PROVISIONING_GROUP_DNS) for {CustomerReference}.",
                group,
                customerReference);
            return false;
        }

        var result = shouldAdd
            ? await _groupProvisioner.AddUserToGroupAsync(
                target,
                group,
                groupDn,
                cancellationToken)
            : await _groupProvisioner.RemoveUserFromGroupAsync(
                target,
                group,
                groupDn,
                cancellationToken);
        if (result.StatusCode >= 400)
        {
            _logger.LogWarning(
                "Demo conversion: membership change failed for {UserSam} on {Group} ({Code}).",
                target.SamAccountName,
                group,
                result.Code);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Groupes AD que la conversion doit accorder.
    /// </summary>
    /// <remarks>
    /// La resolution passe par la topologie Billing V2, seule a connaitre les
    /// regles de provisioning reellement appliquees. Un code inconnu ne rend
    /// aucun groupe : mieux vaut une conversion qui n'accorde rien de visible
    /// qu'une conversion qui accorde un acces devine.
    /// </remarks>
    private async Task<IReadOnlyList<string>> ResolveRealGroupsAsync(
        IReadOnlyList<string>? serviceCodes,
        CancellationToken cancellationToken)
    {
        if (serviceCodes is null || serviceCodes.Count == 0)
        {
            return Array.Empty<string>();
        }

        var groups = new List<string>();
        foreach (var serviceCode in serviceCodes)
        {
            if (string.IsNullOrWhiteSpace(serviceCode))
            {
                continue;
            }

            groups.AddRange(
                await _topology.ResolveServiceMappedGroupsAsync(
                    serviceCode.Trim(),
                    cancellationToken));
        }

        return Normalize(groups);
    }

    private string? ResolveGroupDistinguishedName(string groupSamAccountName)
        => _provisioningConfiguration.TryGetGroupDistinguishedName(
                groupSamAccountName,
                out var distinguishedName)
            && !string.IsNullOrWhiteSpace(distinguishedName)
                ? distinguishedName
                : null;

    private static DemoConversionResult Partial(
        DemoConversionCandidate candidate,
        IReadOnlyList<string> removed,
        IReadOnlyList<string> granted,
        bool identityMoved)
        => new(
            false,
            false,
            "DEMO_CONVERSION_PARTIAL",
            candidate.CustomerReference,
            removed,
            granted,
            identityMoved);

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string> groups)
        => groups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static CustomerAdLinkSummary ToLinkSummary(
        PortalUserAdLinkRecord link)
        => new(
            link.Id,
            link.CustomerReference,
            link.ObjectGuid,
            link.ObjectSid,
            "user",
            link.SamAccountName,
            link.UserPrincipalName,
            link.DisplayName,
            link.DistinguishedName,
            link.AdProvisionedAtUtc?.ToString("O") ?? string.Empty,
            null);
}

/// <summary>
/// OU de destination des identites converties (V1.1 Lot 4).
/// </summary>
/// <remarks>
/// Les comptes de demo vivent dans une arborescence creee par KoXo
/// (<c>OU=CLI-DEMO,...</c>) que le schema applicatif ne sait pas reconstruire :
/// la destination est donc fournie par configuration
/// (<c>DEMO_CONVERSION_TARGET_OU_DN</c>) et validee contre
/// <c>AD_ALLOWED_ROOTS</c> au moment du deplacement.
/// </remarks>
public sealed record DemoConversionRuntimeConfiguration(
    string? TargetOrganizationalUnitDn)
{
    public static DemoConversionRuntimeConfiguration Resolve(
        IConfiguration configuration)
    {
        var value = configuration["DEMO_CONVERSION_TARGET_OU_DN"];
        return new DemoConversionRuntimeConfiguration(
            string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }
}
