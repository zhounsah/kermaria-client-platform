using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.Services;

/// <summary>Issue du provisioning reel d'un compte d'essai (Lot 3).</summary>
/// <param name="ResultCode">Code lisible (inert / skipped / applied / pending_identity...).</param>
/// <param name="RealAccessApplied">Vrai si au moins un groupe GG_DEMO_* a ete ajoute.</param>
/// <param name="KoxoTriggered">Vrai si la chaine KoXo a ete declenchee (password_set).</param>
/// <param name="GroupsApplied">Groupes GG_DEMO_* effectivement ajoutes a l'identite.</param>
public sealed record DemoProvisioningOutcome(
    string ResultCode,
    bool RealAccessApplied,
    bool KoxoTriggered,
    IReadOnlyList<string> GroupsApplied);

/// <summary>Issue de la revocation d'un compte d'essai echu (Lot 3).</summary>
/// <param name="Succeeded">
/// Vrai si la revocation est complete (identite absente = rien a faire = succes,
/// ou tous les retraits/desactivation ont abouti). Faux = a reessayer.
/// </param>
public sealed record DemoRevocationOutcome(
    bool Succeeded,
    string ResultCode,
    IReadOnlyList<string> GroupsRemoved,
    bool UserDisabled);

/// <summary>
/// Applique et revoque l'acces reel cadre des comptes d'essai (usage 2).
///
/// <para>
/// Frontiere code/infra (doc V1.1 &sect;8.5) : ce service <b>applique</b> l'ajout
/// et le retrait direct des groupes <c>GG_DEMO_*</c> (AD depuis SRV-13) et
/// <b>declenche</b> la chaine privee KoXo a « mot de passe rempli ». La creation
/// de l'identite, le quota FSRM, les limites RDS et le VLAN 64 restent du ressort
/// de l'infrastructure.
/// </para>
///
/// <para>
/// Les <c>GG_DEMO_*</c> sont des groupes <b>partages</b> (OU=Groupes_TEST), hors
/// de l'OU du client : l'appartenance passe donc par
/// <see cref="IAdGroupProvisioner"/>, qui se lie au DN complet configure dans
/// <c>AD_PROVISIONING_GROUP_DNS</c>. Le chemin par-client de
/// <see cref="IActiveDirectoryService"/> n'est conserve que pour la
/// desactivation du compte, qui est bien un objet du perimetre client.
/// </para>
///
/// <para>
/// Garde-fou non negociable : un compte <c>showcase</c> n'est <b>jamais</b>
/// provisionne ni relie a KoXo, quelle que soit la configuration globale.
/// </para>
/// </summary>
public interface IDemoProvisioningService
{
    /// <param name="triggerKoxo">
    /// Faux lors d'une <b>reprise</b> : la chaine KoXo a deja ete declenchee a la
    /// creation, la rejouer a chaque passage du balayage inonderait le pipeline
    /// semi-manuel de SRV-21.
    /// </param>
    Task<DemoProvisioningOutcome> ProvisionTrialAsync(
        string customerReference,
        string portalUserId,
        string demoKind,
        string adProvisioningMode,
        IReadOnlyList<string> adGroups,
        CancellationToken cancellationToken,
        bool triggerKoxo = true);

    Task<DemoRevocationOutcome> RevokeTrialAsync(
        string customerReference,
        string portalUserId,
        IReadOnlyList<string> adGroups,
        CancellationToken cancellationToken);
}

public sealed class DemoProvisioningService : IDemoProvisioningService
{
    private const string RealScopedMode = "real_scoped";

    private readonly IActiveDirectoryService _activeDirectory;
    private readonly IAdGroupProvisioner _groupProvisioner;
    private readonly SubscriptionProvisioningRuntimeConfiguration
        _provisioningConfiguration;
    private readonly IActiveDirectoryLinkRepository _links;
    private readonly IDemoAccountRepository _accounts;
    private readonly IKoxoSyncWebhookTriggerService _koxoTrigger;
    private readonly AdRuntimeConfiguration _adConfiguration;
    private readonly ILogger<DemoProvisioningService> _logger;

    public DemoProvisioningService(
        IActiveDirectoryService activeDirectory,
        IAdGroupProvisioner groupProvisioner,
        SubscriptionProvisioningRuntimeConfiguration provisioningConfiguration,
        IActiveDirectoryLinkRepository links,
        IDemoAccountRepository accounts,
        IKoxoSyncWebhookTriggerService koxoTrigger,
        AdRuntimeConfiguration adConfiguration,
        ILogger<DemoProvisioningService> logger)
    {
        _activeDirectory = activeDirectory;
        _groupProvisioner = groupProvisioner;
        _provisioningConfiguration = provisioningConfiguration;
        _links = links;
        _accounts = accounts;
        _koxoTrigger = koxoTrigger;
        _adConfiguration = adConfiguration;
        _logger = logger;
    }

    public async Task<DemoProvisioningOutcome> ProvisionTrialAsync(
        string customerReference,
        string portalUserId,
        string demoKind,
        string adProvisioningMode,
        IReadOnlyList<string> adGroups,
        CancellationToken cancellationToken,
        bool triggerKoxo = true)
    {
        // 1) Garde-fou dur : vitrine = totalement inerte, aucun effet reel.
        if (!string.Equals(demoKind, DemoKinds.Trial, StringComparison.Ordinal))
        {
            return Inert("DEMO_PROVISIONING_INERT_SHOWCASE");
        }

        // 2) L'essai ne provisionne reellement qu'en mode real_scoped.
        if (!string.Equals(
                adProvisioningMode,
                RealScopedMode,
                StringComparison.Ordinal))
        {
            return Inert("DEMO_PROVISIONING_MODE_NOT_REAL");
        }

        // 3) Aucune ecriture AD possible -> on ne declenche rien (coherent avec
        //    le signup : la chaine KoXo est gardee par WritesEnabled).
        if (!_adConfiguration.WritesEnabled)
        {
            return Inert("DEMO_PROVISIONING_AD_WRITES_DISABLED");
        }

        var normalizedGroups = NormalizeGroups(adGroups);

        // 4) Ajout direct des groupes GG_DEMO_* si l'identite AD existe deja.
        //    A la creation, l'identite est generalement encore absente (elle est
        //    creee par la chaine KoXo, semi-manuelle SRV-21) : l'ajout est alors
        //    reporte (pending_identity) et rejoue au prochain provisioning.
        var link = await _links.FindUserLinkByPortalUserIdAsync(
            portalUserId,
            cancellationToken);

        // 4 bis) Rattrapage du lien quand l'identite a ete creee par KoXo.
        //    A l'inscription, c'est l'application qui cree le compte AD et ecrit
        //    le lien dans la foulee. Ici l'identite vient de KoXo, et RIEN
        //    n'ecrit ce lien : sans ce rattrapage, l'essai resterait
        //    indefiniment en pending_identity et n'obtiendrait jamais ses
        //    groupes. On retrouve l'identite par l'identifiant unique KoXo, que
        //    KoXo reporte dans employeeNumber.
        if (link is null)
        {
            link = await TryAdoptKoxoIdentityAsync(
                customerReference,
                portalUserId,
                cancellationToken);
        }

        var applied = new List<string>();
        var identityPending = link is null;
        if (link is not null)
        {
            var target = ToLinkSummary(link);
            foreach (var group in normalizedGroups)
            {
                // Les GG_DEMO_* sont des groupes PARTAGES (hors OU du client) :
                // on passe par le provisioner, qui se lie au DN complet configure
                // (AD_PROVISIONING_GROUP_DNS). Le chemin par-client de
                // IActiveDirectoryService les chercherait sous l'OU du client et
                // echouerait (AD_OBJECT_NOT_FOUND / AD_CROSS_CUSTOMER_FORBIDDEN).
                var groupDn = ResolveGroupDistinguishedName(group);
                if (groupDn is null
                    && _groupProvisioner.RequiresConfiguredGroupDistinguishedNames)
                {
                    _logger.LogWarning(
                        "Demo trial provisioning: no distinguished name configured for {Group} (AD_PROVISIONING_GROUP_DNS) for {CustomerReference}.",
                        group,
                        customerReference);
                    continue;
                }

                var result = await _groupProvisioner.AddUserToGroupAsync(
                    target,
                    group,
                    groupDn,
                    cancellationToken);
                if (result.StatusCode >= 400)
                {
                    _logger.LogWarning(
                        "Demo trial provisioning: could not add {UserSam} to {Group} for {CustomerReference} ({Code}).",
                        link.SamAccountName,
                        group,
                        customerReference,
                        result.Code);
                    continue;
                }

                applied.Add(group);
            }
        }

        // 5) Declenchement de la chaine KoXo (identites/metadonnees) — trial only,
        //    et jamais lors d'une reprise (deja declenchee a la creation).
        var koxoTriggered = triggerKoxo
            && await TriggerKoxoAsync(
                customerReference,
                portalUserId,
                cancellationToken);

        var resultCode = identityPending
            ? "DEMO_PROVISIONING_PENDING_IDENTITY"
            : applied.Count > 0
                ? "DEMO_PROVISIONING_APPLIED"
                : "DEMO_PROVISIONING_NO_GROUP_CHANGE";

        _logger.LogInformation(
            "Demo trial provisioning for {CustomerReference}: code={Code} groupsApplied={GroupCount} koxoTriggered={Koxo} identityPending={Pending}",
            customerReference,
            resultCode,
            applied.Count,
            koxoTriggered,
            identityPending);

        return new DemoProvisioningOutcome(
            resultCode,
            applied.Count > 0,
            koxoTriggered,
            applied);
    }

    public async Task<DemoRevocationOutcome> RevokeTrialAsync(
        string customerReference,
        string portalUserId,
        IReadOnlyList<string> adGroups,
        CancellationToken cancellationToken)
    {
        if (!_adConfiguration.WritesEnabled)
        {
            // Sans ecriture AD, rien a revoquer cote annuaire : on considere la
            // revocation « faite » pour ne pas bloquer la purge applicative.
            return new DemoRevocationOutcome(
                true,
                "DEMO_REVOCATION_AD_WRITES_DISABLED",
                Array.Empty<string>(),
                false);
        }

        var link = await _links.FindUserLinkByPortalUserIdAsync(
            portalUserId,
            cancellationToken);
        if (link is null)
        {
            // Aucune identite AD reelle -> rien a retirer, revocation reussie.
            return new DemoRevocationOutcome(
                true,
                "DEMO_REVOCATION_NO_IDENTITY",
                Array.Empty<string>(),
                false);
        }

        var removed = new List<string>();
        var allSucceeded = true;
        var target = ToLinkSummary(link);
        foreach (var group in NormalizeGroups(adGroups))
        {
            // Meme raison qu'a l'ajout : groupes partages -> bind par DN complet.
            var groupDn = ResolveGroupDistinguishedName(group);
            if (groupDn is null
                && _groupProvisioner.RequiresConfiguredGroupDistinguishedNames)
            {
                // DN manquant = revocation incomplete : on ne marque pas l'essai
                // comme revoque, le balayage suivant reessaiera.
                allSucceeded = false;
                _logger.LogWarning(
                    "Demo trial revocation: no distinguished name configured for {Group} (AD_PROVISIONING_GROUP_DNS) for {CustomerReference}.",
                    group,
                    customerReference);
                continue;
            }

            var result = await _groupProvisioner.RemoveUserFromGroupAsync(
                target,
                group,
                groupDn,
                cancellationToken);
            if (result.StatusCode >= 400)
            {
                allSucceeded = false;
                _logger.LogWarning(
                    "Demo trial revocation: could not remove {UserSam} from {Group} for {CustomerReference} ({Code}).",
                    link.SamAccountName,
                    group,
                    customerReference,
                    result.Code);
                continue;
            }

            removed.Add(group);
        }

        // Desactivation du compte AD apres retrait des groupes.
        var disableResult = await _activeDirectory.DisableUserAsync(
            link.CustomerReference,
            link.SamAccountName,
            cancellationToken);
        var userDisabled = disableResult.StatusCode < 400;
        if (!userDisabled)
        {
            allSucceeded = false;
            _logger.LogWarning(
                "Demo trial revocation: could not disable {UserSam} for {CustomerReference} ({Code}).",
                link.SamAccountName,
                customerReference,
                disableResult.Code);
        }

        return new DemoRevocationOutcome(
            allSucceeded,
            allSucceeded
                ? "DEMO_REVOCATION_APPLIED"
                : "DEMO_REVOCATION_PARTIAL",
            removed,
            userDisabled);
    }

    /// <summary>
    /// Retrouve l'identite creee par KoXo et ecrit le lien manquant.
    /// </summary>
    /// <remarks>
    /// Renvoie <c>null</c> tant que l'identite n'existe pas — la synchronisation
    /// KoXo est asynchrone et semi-manuelle, l'absence est donc un etat normal
    /// et non une erreur. Le rattrapage sera retente au balayage suivant.
    /// </remarks>
    private async Task<PortalUserAdLinkRecord?> TryAdoptKoxoIdentityAsync(
        string customerReference,
        string portalUserId,
        CancellationToken cancellationToken)
    {
        var koxoIdentifier = await _accounts.GetKoxoUniqueIdentifierAsync(
            portalUserId,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(koxoIdentifier))
        {
            return null;
        }

        var directoryObject = await _groupProvisioner
            .ResolveUserByEmployeeNumberAsync(koxoIdentifier, cancellationToken);
        if (directoryObject is null)
        {
            return null;
        }

        await _links.UpsertPortalUserLinkAsync(
            customerReference,
            portalUserId,
            actorUserId: null,
            directoryObject with { CustomerReference = customerReference },
            _adConfiguration.Domain,
            "succeeded",
            DateTime.UtcNow,
            lastPasswordSyncStatus: null,
            lastPasswordSyncAtUtc: null,
            "koxo_provisioned",
            cancellationToken);

        _logger.LogInformation(
            "Adopted KoXo-created identity {SamAccountName} ({EmployeeNumber}) for {CustomerReference}.",
            directoryObject.SamAccountName,
            koxoIdentifier,
            customerReference);

        return await _links.FindUserLinkByPortalUserIdAsync(
            portalUserId,
            cancellationToken);
    }

    private async Task<bool> TriggerKoxoAsync(
        string customerReference,
        string portalUserId,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("D");
        try
        {
            await _koxoTrigger.TriggerAsync(
                new KoxoSyncWebhookTriggerRequest(
                    $"demo-{portalUserId}",
                    portalUserId,
                    customerReference,
                    "password_set",
                    correlationId,
                    DateTime.UtcNow.ToString("O")),
                cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Best-effort : un echec KoXo ne doit pas faire echouer la creation
            // du compte. Il sera rejoue au prochain provisioning.
            _logger.LogWarning(
                exception,
                "Demo trial KoXo trigger failed for {CustomerReference}, portal_user_id {PortalUserId}, correlation_id {CorrelationId}.",
                customerReference,
                portalUserId,
                correlationId);
            return false;
        }
    }

    private static DemoProvisioningOutcome Inert(string resultCode)
        => new(resultCode, false, false, Array.Empty<string>());

    /// <summary>
    /// DN du groupe partage GG_DEMO_* tel que configure dans
    /// <c>AD_PROVISIONING_GROUP_DNS</c> (null si absent).
    /// </summary>
    private string? ResolveGroupDistinguishedName(string groupSamAccountName)
        => _provisioningConfiguration.TryGetGroupDistinguishedName(
                groupSamAccountName,
                out var distinguishedName)
            && !string.IsNullOrWhiteSpace(distinguishedName)
                ? distinguishedName
                : null;

    /// <summary>
    /// Adapte le lien d'identite du portail au contrat attendu par
    /// <see cref="IAdGroupProvisioner"/> (seuls le DN et le sAMAccountName sont
    /// exploites pour l'appartenance).
    /// </summary>
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

    private static IReadOnlyList<string> NormalizeGroups(
        IReadOnlyList<string> adGroups)
        => adGroups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
