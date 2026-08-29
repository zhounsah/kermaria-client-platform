using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface IDirectoryOverviewService
{
    Task<DirectoryOverview> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Vue annuaire et KoXo (specification, section 12.5 et 12.6).
///
/// Elle existe pour lever une ambiguite, pas pour ajouter un reglage :
/// <c>controlled_write</c> melangeait « KoXo fait autorite » et « API-INTERNAL
/// sait ecrire en LDAP ». Ces deux choses sont ici separees, ligne par ligne :
/// qui a le mandat d'une part, ce que cette application s'autorise d'autre part.
///
/// Tout y est en lecture. Rendre le mode annuaire modifiable depuis une page web
/// permettrait d'elargir la portee d'ecriture sur un annuaire de production
/// depuis un navigateur — exactement ce que le bornage par racines autorisees
/// existe pour empecher.
/// </summary>
public sealed class DirectoryOverviewService : IDirectoryOverviewService
{
    private const int WriteLimit = 50;

    private readonly AdRuntimeConfiguration _ad;
    private readonly KoxoSyncWebhookRuntimeConfiguration _koxo;
    private readonly IDirectoryAuditRepository _writes;
    private readonly ILogger<DirectoryOverviewService> _logger;

    public DirectoryOverviewService(
        AdRuntimeConfiguration ad,
        KoxoSyncWebhookRuntimeConfiguration koxo,
        IDirectoryAuditRepository writes,
        ILogger<DirectoryOverviewService> logger)
    {
        _ad = ad;
        _koxo = koxo;
        _writes = writes;
        _logger = logger;
    }

    public async Task<DirectoryOverview> GetAsync(
        CancellationToken cancellationToken)
    {
        var entries = Array.Empty<DirectoryWriteEntry>();
        var writesNotice = _writes.IsPersistent
            ? "Ces ecritures sont celles demandees par API-INTERNAL. Les identites "
                + "creees ou renommees par KoXo n'y figurent pas : elles ne passent "
                + "pas par cette application."
            : "Persistance non durable : aucune ecriture d'annuaire n'est conservee.";

        if (_writes.IsPersistent)
        {
            try
            {
                entries = [.. await _writes.GetRecentWritesAsync(
                    WriteLimit,
                    cancellationToken)];
            }
            catch (Exception exception) when (
                exception is MySqlException or InvalidOperationException)
            {
                _logger.LogWarning(
                    exception,
                    "Directory write history unavailable");
                writesNotice =
                    "L'historique des ecritures d'annuaire est momentanement indisponible.";
            }
        }

        return new DirectoryOverview(
            _ad.ModeName,
            _ad.ConfigurationValid,
            ResolveState(),
            ResolveWarning(),
            BuildAuthorities(),
            BuildPolicies(),
            _ad.AllowedRoots,
            entries,
            _writes.IsPersistent,
            writesNotice);
    }

    /// <summary>
    /// Table des autorites (specification, 12.1). Elle est derivee du mode
    /// courant et non recopiee : une table figee finirait par decrire un autre
    /// deploiement que celui qui tourne.
    /// </summary>
    private IReadOnlyList<DirectoryAuthorityItem> BuildAuthorities()
    {
        var koxoOwns = _ad.KoxoOwnsDirectory;
        var identity = _ad.IdentityAuthority;

        return
        [
            new(
                "Creation d'un utilisateur",
                identity,
                koxoOwns
                    ? "API-INTERNAL ne cree pas d'identite : il adopte celle que KoXo a creee, par employeeNumber."
                    : "Aucun KoXo en face : l'identite est creee localement, sinon le parcours de mot de passe resterait bloque."),
            new(
                "Creation d'une OU client",
                identity,
                "KoXo cree l'OU cible si elle n'existe pas."),
            new(
                "Mot de passe d'annuaire",
                _ad.PasswordAuthority,
                koxoOwns
                    ? "Aucune reinitialisation directe par API-INTERNAL."
                    : "Mode local uniquement."),
            new(
                "Lecture et resolution d'identite",
                "API-INTERNAL",
                _ad.ReadsEnabled
                    ? "Rattachement par l'attribut employeeNumber, seule cle stable."
                    : "Lecture desactivee dans ce mode."),
            new(
                "Appartenance aux groupes de services",
                _ad.WritesEnabled ? "API-INTERNAL" : "Aucune",
                _ad.GroupMembershipWritePolicy),
            new(
                "Cycle de vie de l'utilisateur",
                koxoOwns ? "KoXo" : _ad.WritesEnabled ? "API-INTERNAL / mode local" : "Aucune",
                _ad.UserLifecycleWritePolicy),
            new(
                "Suppression d'un utilisateur",
                "Aucune",
                "Interdite a API-INTERNAL, quel que soit le mode."),
            new(
                "Renommage et attributs d'identite",
                identity,
                "Le CSV KoXo fait autorite sur la population ; les groupes restent pilotes par l'API."),
        ];
    }

    private IReadOnlyList<DirectoryPolicyItem> BuildPolicies()
    {
        var policies = new List<DirectoryPolicyItem>
        {
            new(
                "AD_INTEGRATION_MODE",
                "Mode d'integration",
                _ad.ModeName,
                "restart_required",
                true,
                false),
            new(
                "directory_access",
                "Acces de l'API a l'annuaire",
                _ad.DirectoryAccess,
                "restart_required",
                true,
                false),
            new(
                "identity_authority",
                "Autorite des identites",
                _ad.IdentityAuthority,
                "restart_required",
                true,
                false),
            new(
                "password_authority",
                "Autorite des mots de passe",
                _ad.PasswordAuthority,
                "restart_required",
                true,
                false),
            new(
                "group_membership_write_policy",
                "Ecriture des groupes de services",
                _ad.GroupMembershipWritePolicy,
                "restart_required",
                true,
                false),
            new(
                "user_lifecycle_write_policy",
                "Ecriture du cycle de vie",
                _ad.UserLifecycleWritePolicy,
                "restart_required",
                true,
                false),
            new(
                "manual_admin_write_policy",
                "Ecriture administrateur manuelle",
                "Interdite",
                "code_invariant",
                true,
                false),
            new(
                "AD_DOMAIN",
                "Domaine",
                _ad.Domain ?? "Non configure",
                "restart_required",
                true,
                false),
            new(
                "AD_CLIENTS_OU_DN",
                "OU clients",
                _ad.ClientsOuDn ?? "Non configuree",
                "restart_required",
                true,
                false),
            new(
                "AD_REQUIRED_OU_ROOT",
                "Racine imposee",
                _ad.RequiredOuRoot ?? "Non configuree",
                "restart_required",
                true,
                false),
            new(
                "AD_SERVICE_ACCOUNT_USERNAME",
                "Compte LDAP",
                _ad.ServiceAccountUsername ?? "Non configure",
                "restart_required",
                true,
                false),
            new(
                "AD_SERVICE_ACCOUNT_PASSWORD",
                "Secret du compte LDAP",
                Present(_ad.ServiceAccountPassword),
                "secret",
                true,
                true),
            new(
                "AD_USE_CURRENT_WINDOWS_CREDENTIALS",
                "Identite Windows du service",
                _ad.UseCurrentWindowsCredentials ? "Oui" : "Non",
                "restart_required",
                true,
                false),
            new(
                "KOXO_SYNC_WEBHOOK_URL",
                "Webhook KoXo",
                _koxo.Url is null
                    ? "Non configure"
                    : $"{_koxo.Url.Scheme}://{_koxo.Url.Host}:{_koxo.Url.Port}{_koxo.Url.AbsolutePath}",
                "restart_required",
                true,
                false),
            new(
                "KOXO_SYNC_WEBHOOK_TOKEN",
                "Jeton du webhook KoXo",
                Present(_koxo.BearerToken),
                "secret",
                true,
                true),
        };

        return policies;
    }

    private string ResolveState()
    {
        if (!_ad.ConfigurationValid)
        {
            return "critical";
        }

        // Se lier sous l'identite du service Windows ignore le compte de
        // service configure, qui est le seul a porter la delegation. Le
        // symptome observe est un refus d'acces qui ressemble a une delegation
        // manquante alors qu'elle est correctement posee.
        if (_ad.UseCurrentWindowsCredentials && _ad.ReadsEnabled)
        {
            return "warning";
        }

        if (_ad.WritesEnabled && !_koxo.Enabled)
        {
            return "warning";
        }

        return _ad.Mode == AdIntegrationMode.Disabled ? "info" : "healthy";
    }

    private string? ResolveWarning()
    {
        if (!_ad.ConfigurationValid)
        {
            return "La configuration d'annuaire est invalide : les ecritures restent refusees.";
        }

        if (_ad.UseCurrentWindowsCredentials && _ad.ReadsEnabled)
        {
            return "AD_USE_CURRENT_WINDOWS_CREDENTIALS est a true : le compte de service "
                + "configure est ignore et la liaison se fait sous l'identite du service "
                + "Windows, qui n'a aucune delegation. Le refus qui en resulte ressemble "
                + "a une delegation manquante.";
        }

        if (_ad.WritesEnabled && !_koxo.Enabled)
        {
            return "Les ecritures de groupes sont autorisees mais le webhook KoXo n'est pas "
                + "configure : aucune identite ne sera creee en face.";
        }

        return null;
    }

    private static string Present(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Non configure" : "Configure";
}
