using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Email;

namespace Kermaria.ApiInternal.Services.Provisioning;

/// <summary>
/// Regle d'attribution d'une place USER-ADDITIONAL.
/// </summary>
/// <remarks>
/// <para>
/// Volontairement pure et statique : elle est appliquee a l'etat lu <b>sous
/// verrou</b> par le depot, et c'est litteralement la meme fonction en mode
/// mock et sur MariaDB. Une regle reimplantee en SQL a cote d'une regle en C#
/// finirait par autoriser en base ce que le code refuse.
/// </para>
/// <para>
/// L'ordre des controles va du plus structurel au plus circonstanciel : un
/// appelant qui vise la mauvaise place doit l'apprendre avant d'apprendre que
/// l'adresse e-mail est prise.
/// </para>
/// </remarks>
public static class BillingV2AdditionalUserAssignmentPolicy
{
    public static string? Validate(
        BillingV2AdditionalUserSlotSnapshot snapshot,
        string expectedCustomerId,
        string expectedSubscriptionId)
    {
        if (!string.Equals(
                snapshot.SubscriptionId,
                expectedSubscriptionId,
                StringComparison.Ordinal))
        {
            return BillingV2AdditionalUserRejectionCodes
                .SlotSubscriptionMismatch;
        }

        // Le client de la session doit etre celui de l'abonnement. Aucune
        // tolerance : attribuer une place a une personne d'un autre client
        // creerait une identite dans le mauvais perimetre annuaire.
        if (!string.Equals(
                snapshot.SubscriptionCustomerId,
                expectedCustomerId,
                StringComparison.Ordinal))
        {
            return BillingV2AdditionalUserRejectionCodes.SlotCustomerMismatch;
        }

        if (string.IsNullOrWhiteSpace(snapshot.CustomerReference))
        {
            return BillingV2AdditionalUserRejectionCodes.CustomerNotFound;
        }

        if (snapshot.IsPrimary)
        {
            return BillingV2AdditionalUserRejectionCodes.SlotIsPrimary;
        }

        if (!string.Equals(
                snapshot.SlotStatus,
                BillingV2AdditionalUserIdentityConventions.ActiveSlotStatus,
                StringComparison.OrdinalIgnoreCase))
        {
            return BillingV2AdditionalUserRejectionCodes.SlotNotActive;
        }

        if (!string.Equals(
                snapshot.SubscriptionStatus,
                BillingV2AdditionalUserIdentityConventions
                    .ProvisionableSubscriptionStatus,
                StringComparison.OrdinalIgnoreCase))
        {
            return BillingV2AdditionalUserRejectionCodes
                .SubscriptionNotProvisionable;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.IdentityReference))
        {
            return BillingV2AdditionalUserRejectionCodes.SlotAlreadyAssigned;
        }

        if (snapshot.HasExistingLifecycle)
        {
            return BillingV2AdditionalUserRejectionCodes.LifecycleAlreadyExists;
        }

        // La place doit reellement etre adossee a un droit USER-ADDITIONAL
        // actif. Sans ce controle, n'importe quelle ligne de
        // billing_v2_subscription_users deviendrait un point d'entree pour
        // creer une identite reelle.
        if (!snapshot.HasActiveUserSlotEntitlement)
        {
            return BillingV2AdditionalUserRejectionCodes.SlotEntitlementMissing;
        }

        if (snapshot.IncompatibleScopedItemCount > 0)
        {
            return BillingV2AdditionalUserRejectionCodes.SlotScopeIncoherent;
        }

        if (snapshot.EmailAlreadyUsed)
        {
            return BillingV2AdditionalUserRejectionCodes.EmailAlreadyUsed;
        }

        return null;
    }
}

/// <summary>Personne reelle affectee a une place.</summary>
public sealed record BillingV2AdditionalUserAssignment(
    string CustomerId,
    string SubscriptionId,
    string SubscriptionUserId,
    string Email,
    string DisplayName,
    string? PersonalTitle,
    string? GivenName,
    string? Surname,
    DateOnly? BirthDate,
    string? Initials,
    string? Phone,
    string? ActorReference);

public sealed record BillingV2AdditionalUserOperationResult(
    bool Succeeded,
    string Code,
    string Message,
    string? PortalUserId = null,
    string? LifecycleStatus = null);

/// <summary>
/// Etapes de materialisation renvoyees par
/// <see cref="BillingV2AdditionalUserIdentityService.TryMaterializeAsync"/>.
/// </summary>
public static class BillingV2AdditionalUserMaterializationCodes
{
    public const string Ready = "IDENTITY_READY";
    public const string AwaitingPassword = "AWAITING_PASSWORD";
    public const string DirectoryNotReady = "AD_IDENTITY_NOT_READY";
    public const string DirectoryDisabled = "AD_WRITES_DISABLED";
    public const string DirectoryConflict = "AD_IDENTITY_CONFLICT";
    public const string DirectoryFailed = "AD_PROVISIONING_FAILED";
    public const string LifecycleMissing = "LIFECYCLE_MISSING";
    public const string LifecycleDisabled = "LIFECYCLE_DISABLED";

    /// <summary>
    /// Le provisioning Billing V2 est desactive : aucune operation a effet
    /// reel n'est tentee.
    /// </summary>
    /// <remarks>
    /// Refus <b>avant</b> tout point de non-retour — consommation de jeton,
    /// publication de mot de passe, appel KoXo ou AD. Un refus tardif
    /// laisserait un jeton consomme et un compte sans identite annuaire, etat
    /// dont on ne revient pas tout seul.
    /// </remarks>
    public const string ProvisioningDisabled = "BILLING_V2_PROVISIONING_DISABLED";

    /// <summary>
    /// Le relais du mot de passe vers KoXo n'est pas exploitable.
    /// </summary>
    /// <remarks>
    /// Sans lui, le mot de passe n'atteindrait jamais l'annuaire et la
    /// personne perdrait VPN, RDS et stockage sans aucune erreur visible. On
    /// refuse donc avant de consommer le jeton, qui est a usage unique.
    /// </remarks>
    public const string PasswordHandoffUnavailable = "KOXO_PASSWORD_HANDOFF_UNAVAILABLE";
}

public interface IBillingV2AdditionalUserIdentityService
{
    bool IsPersistent { get; }

    Task<BillingV2AdditionalUserOperationResult> AssignAsync(
        BillingV2AdditionalUserAssignment assignment,
        string correlationId,
        CancellationToken cancellationToken);

    Task<BillingV2AdditionalUserOperationResult> ResendInvitationAsync(
        string subscriptionUserId,
        string customerId,
        string correlationId,
        CancellationToken cancellationToken);

    Task<BillingV2AdditionalUserOperationResult> ValidateInvitationTokenAsync(
        string? token,
        CancellationToken cancellationToken);

    Task<BillingV2AdditionalUserOperationResult> SetPasswordAsync(
        string? token,
        string? password,
        CancellationToken cancellationToken);

    Task<BillingV2AdditionalUserOperationResult> TryMaterializeAsync(
        string portalUserId,
        CancellationToken cancellationToken);

    Task<BillingV2AdditionalUserOperationResult> DisableAsync(
        string subscriptionUserId,
        string customerId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Cycle de vie d'identite des utilisateurs additionnels Billing V2.
/// </summary>
/// <remarks>
/// <para>
/// Chaine cible : place vide -&gt; attribution transactionnelle (utilisateur
/// portail + <c>CLI-NNNNNN</c> + cycle de vie + jeton) -&gt; l'utilisateur
/// choisit son mot de passe -&gt; publication du mot de passe pour l'export
/// KoXo -&gt; KoXo cree l'objet annuaire -&gt; adoption par
/// <c>employeeNumber</c> -&gt; <c>ready</c>. Le stockage, le VPN et le RDS de
/// cet utilisateur se debloquent ensuite par les mecanismes de provisioning
/// existants, sans traitement particulier : l'identite est devenue ordinaire.
/// </para>
/// <para>
/// <b>Production (KoXo maitre de l'annuaire)</b> : aucun
/// <c>CreateUserAsync</c>, aucun <c>SetUserPasswordAsync</c>. Creer nous-memes
/// produirait un doublon sous un <c>sAMAccountName</c> que KoXo n'utilise pas,
/// et une ecriture LDAP du mot de passe serait ecrasee a la synchronisation
/// suivante par <c>ForcePasswords=1</c>.
/// </para>
/// <para>
/// <b>Mock</b> : il n'y a aucun KoXo derriere, donc l'application cree et
/// equipe elle-meme l'objet simule, sans quoi le parcours resterait
/// indefiniment bloque en developpement.
/// </para>
/// </remarks>
public sealed class BillingV2AdditionalUserIdentityService
    : IBillingV2AdditionalUserIdentityService
{
    private const int MinPasswordLength = 12;
    private const int MaxPasswordLength = 128;

    private readonly IBillingV2AdditionalUserIdentityRepository _repository;
    private readonly IPortalPasswordSetupRepository _passwordSetups;
    private readonly IPortalPasswordService _passwordService;
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly IActiveDirectoryLinkRepository _activeDirectoryLinks;
    private readonly IAdGroupProvisioner _adGroupProvisioner;
    private readonly IKoxoPendingPasswordStore _pendingPasswords;
    private readonly IKoxoSyncWebhookTriggerService _koxoSyncWebhook;
    private readonly IEmailDispatchService _emailDispatch;
    private readonly SignupRuntimeConfiguration _signupConfiguration;
    private readonly EmailRuntimeConfiguration _emailConfiguration;
    private readonly AdRuntimeConfiguration _adConfiguration;
    private readonly BillingV2RuntimeConfiguration _billingConfiguration;
    private readonly ILogger<BillingV2AdditionalUserIdentityService> _logger;

    public BillingV2AdditionalUserIdentityService(
        IBillingV2AdditionalUserIdentityRepository repository,
        IPortalPasswordSetupRepository passwordSetups,
        IPortalPasswordService passwordService,
        IActiveDirectoryService activeDirectoryService,
        IActiveDirectoryLinkRepository activeDirectoryLinks,
        IAdGroupProvisioner adGroupProvisioner,
        IKoxoPendingPasswordStore pendingPasswords,
        IKoxoSyncWebhookTriggerService koxoSyncWebhook,
        IEmailDispatchService emailDispatch,
        SignupRuntimeConfiguration signupConfiguration,
        EmailRuntimeConfiguration emailConfiguration,
        AdRuntimeConfiguration adConfiguration,
        BillingV2RuntimeConfiguration billingConfiguration,
        ILogger<BillingV2AdditionalUserIdentityService> logger)
    {
        _repository = repository;
        _passwordSetups = passwordSetups;
        _passwordService = passwordService;
        _activeDirectoryService = activeDirectoryService;
        _activeDirectoryLinks = activeDirectoryLinks;
        _adGroupProvisioner = adGroupProvisioner;
        _pendingPasswords = pendingPasswords;
        _koxoSyncWebhook = koxoSyncWebhook;
        _emailDispatch = emailDispatch;
        _signupConfiguration = signupConfiguration;
        _emailConfiguration = emailConfiguration;
        _adConfiguration = adConfiguration;
        _billingConfiguration = billingConfiguration;
        _logger = logger;
    }

    public bool IsPersistent => _repository.IsPersistent;

    // ------------------------------------------------------------------
    // 1. ATTRIBUTION
    // ------------------------------------------------------------------

    public async Task<BillingV2AdditionalUserOperationResult> AssignAsync(
        BillingV2AdditionalUserAssignment assignment,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var gate = GuardProvisioningEnabled();
        if (gate is not null)
        {
            return gate;
        }

        var email = NormalizeEmail(assignment.Email);
        if (email is null)
        {
            return Failure(
                BillingV2AdditionalUserRejectionCodes.InvalidIdentity,
                "L'adresse e-mail de l'utilisateur est invalide.");
        }

        var displayName = Normalize(assignment.DisplayName);
        if (displayName is null)
        {
            return Failure(
                BillingV2AdditionalUserRejectionCodes.InvalidIdentity,
                "Le nom affiche de l'utilisateur est obligatoire.");
        }

        var token = PortalSetupToken.Generate();
        var portalUserId = Guid.NewGuid().ToString("D");
        var command = new BillingV2AdditionalUserAssignmentCommand(
            assignment.CustomerId,
            assignment.SubscriptionId,
            assignment.SubscriptionUserId,
            portalUserId,
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            PortalSetupToken.Hash(token),
            DateTime.UtcNow.AddHours(
                _signupConfiguration.PasswordSetupTokenTtlHours),
            BillingV2AdditionalUserIdentityConventions.PasswordSetupPurpose,
            email,
            displayName,
            Normalize(assignment.PersonalTitle),
            Normalize(assignment.GivenName),
            Normalize(assignment.Surname),
            assignment.BirthDate,
            Normalize(assignment.Initials),
            Normalize(assignment.Phone),
            Normalize(assignment.ActorReference));

        var result = await _repository.AssignAsync(
            command,
            snapshot => BillingV2AdditionalUserAssignmentPolicy.Validate(
                snapshot,
                assignment.CustomerId,
                assignment.SubscriptionId),
            cancellationToken);

        if (!result.Succeeded)
        {
            return Failure(
                result.RejectionCode!,
                DescribeRejection(result.RejectionCode!));
        }

        // L'e-mail est hors transaction et son echec n'annule rien : le compte
        // et le jeton existent, et le lien peut etre renvoye. Annuler
        // l'attribution parce qu'un relais SMTP est indisponible consommerait
        // un CLI-NNNNNN pour rien a chaque tentative.
        await SendInvitationAsync(
            result.Created!,
            token,
            correlationId,
            cancellationToken);

        return new BillingV2AdditionalUserOperationResult(
            true,
            "ADDITIONAL_USER_ASSIGNED",
            "L'utilisateur a ete cree. Un lien de definition de mot de passe lui a ete envoye.",
            result.Created!.PortalUserId,
            result.Created!.Status);
    }

    public async Task<BillingV2AdditionalUserOperationResult>
        ResendInvitationAsync(
            string subscriptionUserId,
            string customerId,
            string correlationId,
            CancellationToken cancellationToken)
    {
        var gate = GuardProvisioningEnabled();
        if (gate is not null)
        {
            return gate;
        }

        var record = await _repository.FindBySubscriptionUserIdAsync(
            subscriptionUserId,
            cancellationToken);
        var guard = GuardOwnership(record, customerId);
        if (guard is not null)
        {
            return guard;
        }

        if (!string.Equals(
                record!.Status,
                BillingV2UserIdentityStatuses.AwaitingPassword,
                StringComparison.Ordinal))
        {
            // Renvoyer un lien a quelqu'un qui a deja pose son mot de passe lui
            // permettrait d'en changer sans authentification.
            return Failure(
                "INVALID_STATE",
                "Cet utilisateur a deja defini son mot de passe.");
        }

        var token = PortalSetupToken.Generate();
        await _passwordSetups.IssueAsync(
            new PortalPasswordSetupIssue(
                Guid.NewGuid().ToString("D"),
                record.PortalUserId,
                BillingV2AdditionalUserIdentityConventions.PasswordSetupPurpose,
                PortalSetupToken.Hash(token),
                DateTime.UtcNow.AddHours(
                    _signupConfiguration.PasswordSetupTokenTtlHours)),
            cancellationToken);

        await SendInvitationAsync(
            record,
            token,
            correlationId,
            cancellationToken);

        return new BillingV2AdditionalUserOperationResult(
            true,
            "PASSWORD_SETUP_EMAIL_SENT",
            "Un nouveau lien de definition du mot de passe a ete envoye.",
            record.PortalUserId,
            record.Status);
    }

    // ------------------------------------------------------------------
    // 2. DEFINITION DU MOT DE PASSE
    // ------------------------------------------------------------------

    public async Task<BillingV2AdditionalUserOperationResult>
        ValidateInvitationTokenAsync(
            string? token,
            CancellationToken cancellationToken)
    {
        var normalized = token?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return TokenInvalid();
        }

        var target = await _passwordSetups.FindByTokenHashAsync(
            PortalSetupToken.Hash(normalized),
            cancellationToken);
        if (target is null
            || target.IsConsumed
            || target.IsSuperseded)
        {
            return TokenInvalid();
        }

        if (target.IsExpired(DateTime.UtcNow))
        {
            return Failure(
                PortalPasswordSetupCodes.TokenExpired,
                "Ce lien de definition de mot de passe a expire.");
        }

        return new BillingV2AdditionalUserOperationResult(
            true,
            "TOKEN_VALID",
            "Lien valide. Choisissez votre mot de passe.");
    }

    public async Task<BillingV2AdditionalUserOperationResult> SetPasswordAsync(
        string? token,
        string? password,
        CancellationToken cancellationToken)
    {
        var gate = GuardProvisioningEnabled();
        if (gate is not null)
        {
            return gate;
        }

        var normalizedToken = token?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return TokenInvalid();
        }

        if (password is null
            || password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            return Failure(
                "INVALID_PASSWORD",
                $"Le mot de passe doit comporter entre {MinPasswordLength} et {MaxPasswordLength} caracteres.");
        }

        var tokenHash = PortalSetupToken.Hash(normalizedToken);

        // Lecture prealable, hors verrou, pour savoir CE QU'IL FAUDRA ecrire
        // dans la transaction. Elle n'autorise rien : la transaction relit le
        // jeton sous verrou et refuse si le relais prepare designe quelqu'un
        // d'autre.
        var target = await _passwordSetups.FindByTokenHashAsync(
            tokenHash,
            cancellationToken);
        if (target is null || !target.IsUsable(DateTime.UtcNow))
        {
            return Failure(
                ClassifyTarget(target),
                DescribeTokenFailure(ClassifyTarget(target)));
        }

        var record = await _repository.FindByPortalUserIdAsync(
            target.PortalUserId,
            cancellationToken);

        PortalPasswordHandoff? handoff = null;
        if (record is not null)
        {
            PortalPasswordSecret? secret = null;
            if (_adConfiguration.KoxoOwnsDirectory)
            {
                // Le mot de passe voyage par la colonne 14 du CSV : il est
                // scelle ici, jamais ecrit en LDAP, sinon ForcePasswords=1
                // l'ecraserait au passage suivant.
                //
                // Le scellement precede la consommation : sans lui, on
                // consommerait un jeton a usage unique pour decouvrir ensuite
                // que le secret n'atteindra jamais l'annuaire, sans second
                // lien pour recommencer.
                secret = _pendingPasswords.Seal(target.PortalUserId, password);
                if (secret is null)
                {
                    _logger.LogError(
                        "KoXo password handoff is not operational: refusing to consume a password setup token.");
                    return Failure(
                        BillingV2AdditionalUserMaterializationCodes
                            .PasswordHandoffUnavailable,
                        "La definition du mot de passe est momentanement indisponible.");
                }
            }

            handoff = new PortalPasswordHandoff(
                target.PortalUserId,
                record.Id,
                DateTime.UtcNow,
                secret);
        }

        // UNE transaction : verrou du jeton, condensat du mot de passe,
        // consommation, secret scelle, transition du cycle de vie. Le decoupage
        // precedent laissait une fenetre ou le jeton etait deja consomme alors
        // que le secret n'existait nulle part — et il n'existe en clair qu'a
        // cet instant. Aucun appel reseau ici : le declenchement KoXo suit le
        // COMMIT.
        var consumption = await _passwordSetups.ConsumeAndSetPasswordAsync(
            tokenHash,
            portalUserId => _passwordService.HashPassword(
                portalUserId,
                password),
            handoff,
            cancellationToken);
        if (!consumption.Succeeded)
        {
            if (string.Equals(
                    consumption.Code,
                    PortalPasswordSetupCodes.HandoffFailed,
                    StringComparison.Ordinal))
            {
                _logger.LogError(
                    "Password handoff transaction rolled back for portal_user_id {PortalUserId}; nothing was consumed.",
                    target.PortalUserId);
                return Failure(
                    BillingV2AdditionalUserMaterializationCodes
                        .PasswordHandoffUnavailable,
                    "La definition du mot de passe est momentanement indisponible.");
            }

            return Failure(
                consumption.Code,
                DescribeTokenFailure(consumption.Code));
        }

        var portalUserId = consumption.PortalUserId!;
        if (record is null)
        {
            // Le mot de passe est pose et le compte portail fonctionne : seule
            // la chaine annuaire est hors d'atteinte, ce qui n'est pas un echec
            // du point de vue de l'utilisateur.
            _logger.LogWarning(
                "Password set for portal_user_id {PortalUserId} without a Billing V2 identity lifecycle.",
                portalUserId);
            return new BillingV2AdditionalUserOperationResult(
                true,
                "PASSWORD_SET",
                "Mot de passe defini. Vous pouvez desormais vous connecter.",
                portalUserId);
        }

        await TriggerKoxoSyncAsync(record, "additional_user_password_set", cancellationToken);

        var materialization = await MaterializeCoreAsync(
            record with { Status = BillingV2UserIdentityStatuses.KoxoPending },
            password,
            cancellationToken);

        return new BillingV2AdditionalUserOperationResult(
            true,
            "PASSWORD_SET",
            "Mot de passe defini. Vous pouvez desormais vous connecter.",
            portalUserId,
            materialization.LifecycleStatus);
    }

    private static string ClassifyTarget(PortalPasswordSetupTarget? target)
        => target switch
        {
            null => PortalPasswordSetupCodes.TokenInvalid,
            { IsConsumed: true } => PortalPasswordSetupCodes.TokenAlreadyUsed,
            { IsSuperseded: true } => PortalPasswordSetupCodes.TokenInvalid,
            _ => PortalPasswordSetupCodes.TokenExpired
        };

    // ------------------------------------------------------------------
    // 3. MATERIALISATION ANNUAIRE
    // ------------------------------------------------------------------

    public async Task<BillingV2AdditionalUserOperationResult>
        TryMaterializeAsync(
            string portalUserId,
            CancellationToken cancellationToken)
    {
        var gate = GuardProvisioningEnabled();
        if (gate is not null)
        {
            return gate;
        }

        var record = await _repository.FindByPortalUserIdAsync(
            portalUserId,
            cancellationToken);
        if (record is null)
        {
            return Failure(
                BillingV2AdditionalUserMaterializationCodes.LifecycleMissing,
                "Aucun cycle de vie d'identite pour cet utilisateur.");
        }

        return await MaterializeCoreAsync(
            record,
            plaintextPassword: null,
            cancellationToken);
    }

    /// <summary>
    /// Fait avancer le cycle de vie d'un cran, ou constate qu'il n'avance pas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Strictement idempotente. Chaque etape est conditionnee a l'etat
    /// courant et re-verifiee : rejouee sur un cycle deja <c>ready</c>, elle ne
    /// cree ni second utilisateur, ni second identifiant, ni second lien.
    /// </para>
    /// <para>
    /// <paramref name="plaintextPassword"/> n'est fourni qu'au moment ou
    /// l'utilisateur vient de le saisir, et n'est utilise qu'en mode mock. Sur
    /// une reprise ulterieure il vaut <c>null</c> : le chemin de production
    /// n'en a pas besoin, puisque le mot de passe est applique par KoXo.
    /// </para>
    /// </remarks>
    private async Task<BillingV2AdditionalUserOperationResult>
        MaterializeCoreAsync(
            BillingV2AdditionalUserIdentityRecord record,
            string? plaintextPassword,
            CancellationToken cancellationToken)
    {
        switch (record.Status)
        {
            case BillingV2UserIdentityStatuses.Ready:
                // Idempotent : un cycle deja conclu ne doit conserver aucun
                // secret. Si l'acquittement precedent n'a pas abouti — arret
                // juste apres la conclusion — c'est ici qu'il se rattrape.
                await _pendingPasswords.AcknowledgeAsync(
                    record.PortalUserId,
                    cancellationToken);
                return Success(
                    BillingV2AdditionalUserMaterializationCodes.Ready,
                    "L'identite est prete.",
                    record);

            case BillingV2UserIdentityStatuses.Disabled:
                return Failure(
                    BillingV2AdditionalUserMaterializationCodes
                        .LifecycleDisabled,
                    "Cette place est desactivee.",
                    record);

            case BillingV2UserIdentityStatuses.AwaitingPassword:
                return Failure(
                    BillingV2AdditionalUserMaterializationCodes
                        .AwaitingPassword,
                    "L'utilisateur n'a pas encore defini son mot de passe.",
                    record);
        }

        if (!_adConfiguration.WritesEnabled)
        {
            // Annuaire desactive : le cycle reste ou il est. Aucun echec
            // enregistre, il n'y a rien qui ait echoue.
            return Failure(
                BillingV2AdditionalUserMaterializationCodes.DirectoryDisabled,
                "Les ecritures Active Directory sont desactivees.",
                record);
        }

        // Un lien deja present prime sur toute resolution : c'est le cas du
        // retry apres materialisation reussie, et republier une resolution
        // annuaire risquerait d'adopter un objet different.
        var existingLink = await _activeDirectoryLinks
            .FindUserLinkByPortalUserIdAsync(
                record.PortalUserId,
                cancellationToken);
        if (existingLink is not null)
        {
            return await FinishFromLinkAsync(
                record,
                existingLink.ObjectGuid,
                cancellationToken);
        }

        var directoryObject = _adConfiguration.KoxoOwnsDirectory
            ? await _adGroupProvisioner.ResolveUserByEmployeeNumberAsync(
                record.KoxoUniqueIdentifier,
                cancellationToken)
            : await CreateMockDirectoryObjectAsync(
                record,
                plaintextPassword,
                cancellationToken);

        if (directoryObject is null)
        {
            // Absence n'est pas echec : la synchronisation KoXo est
            // asynchrone. On relance le declencheur et on laisse le cycle en
            // koxo_pending, ce qui le maintient dans l'export.
            await TriggerKoxoSyncAsync(
                record,
                "additional_user_identity_missing",
                cancellationToken);
            return Failure(
                BillingV2AdditionalUserMaterializationCodes.DirectoryNotReady,
                "L'espace est en cours de creation. Merci de reessayer dans une minute.",
                record);
        }

        if (record.DirectoryObjectGuid is not null
            && !string.Equals(
                record.DirectoryObjectGuid,
                directoryObject.ObjectGuid,
                StringComparison.OrdinalIgnoreCase))
        {
            // Le cycle de vie designait deja un autre objet annuaire :
            // basculer dessus transfererait l'identite d'une personne a une
            // autre, avec ses droits reels.
            await _repository.MarkFailedAsync(
                record.Id,
                BillingV2AdditionalUserMaterializationCodes.DirectoryConflict,
                $"expected={record.DirectoryObjectGuid};resolved={directoryObject.ObjectGuid}",
                cancellationToken);
            return Failure(
                BillingV2AdditionalUserMaterializationCodes.DirectoryConflict,
                "L'identite annuaire resolue ne correspond pas a celle deja adoptee.",
                record);
        }

        if (!await _repository.MarkDirectoryResolvedAsync(
                record.Id,
                directoryObject.ObjectGuid,
                DateTime.UtcNow,
                cancellationToken))
        {
            return Failure(
                BillingV2AdditionalUserMaterializationCodes.DirectoryConflict,
                "Le cycle de vie n'accepte pas cette identite annuaire.",
                record);
        }

        try
        {
            await _activeDirectoryLinks.UpsertPortalUserLinkAsync(
                record.CustomerReference,
                record.PortalUserId,
                actorUserId: null,
                directoryObject,
                _adConfiguration.Domain,
                "succeeded",
                DateTime.UtcNow,
                "succeeded",
                DateTime.UtcNow,
                "koxo_pending",
                cancellationToken);
        }
        catch (AmbiguousAdLinkException exception)
        {
            // Le depot AD refuse toute adoption qui transfererait une identite
            // d'un utilisateur portail a un autre. Ce refus est definitif :
            // reessayer produirait le meme conflit, et l'arbitrage est humain.
            await _repository.MarkFailedAsync(
                record.Id,
                BillingV2AdditionalUserMaterializationCodes.DirectoryConflict,
                exception.Message,
                cancellationToken);
            return Failure(
                BillingV2AdditionalUserMaterializationCodes.DirectoryConflict,
                "Le rattachement Active Directory entrerait en conflit avec une identite existante.",
                record);
        }

        return await FinishFromLinkAsync(
            record with
            {
                Status = BillingV2UserIdentityStatuses.DirectoryReady,
                DirectoryObjectGuid = directoryObject.ObjectGuid
            },
            directoryObject.ObjectGuid,
            cancellationToken);
    }

    /// <summary>
    /// Confirme le lien annuaire par relecture, puis conclut en <c>ready</c>.
    /// </summary>
    /// <remarks>
    /// C'est cette relecture qui donne son sens a <c>directory_ready</c> :
    /// l'objet a ete resolu, mais tant que le lien n'est pas verifie present,
    /// le cycle n'est pas termine.
    /// </remarks>
    private async Task<BillingV2AdditionalUserOperationResult>
        FinishFromLinkAsync(
            BillingV2AdditionalUserIdentityRecord record,
            string linkObjectGuid,
            CancellationToken cancellationToken)
    {
        if (record.DirectoryObjectGuid is null)
        {
            if (!await _repository.MarkDirectoryResolvedAsync(
                    record.Id,
                    linkObjectGuid,
                    DateTime.UtcNow,
                    cancellationToken))
            {
                return Failure(
                    BillingV2AdditionalUserMaterializationCodes
                        .DirectoryConflict,
                    "Le cycle de vie n'accepte pas cette identite annuaire.",
                    record);
            }
        }
        else if (!string.Equals(
                     record.DirectoryObjectGuid,
                     linkObjectGuid,
                     StringComparison.OrdinalIgnoreCase))
        {
            await _repository.MarkFailedAsync(
                record.Id,
                BillingV2AdditionalUserMaterializationCodes.DirectoryConflict,
                $"expected={record.DirectoryObjectGuid};linked={linkObjectGuid}",
                cancellationToken);
            return Failure(
                BillingV2AdditionalUserMaterializationCodes.DirectoryConflict,
                "Le lien Active Directory ne correspond pas a l'identite adoptee.",
                record);
        }

        var confirmed = await _activeDirectoryLinks
            .FindUserLinkByPortalUserIdAsync(
                record.PortalUserId,
                cancellationToken);
        if (confirmed is null)
        {
            return Failure(
                BillingV2AdditionalUserMaterializationCodes.DirectoryFailed,
                "Le rattachement Active Directory n'a pas pu etre confirme.",
                record with { Status = BillingV2UserIdentityStatuses.DirectoryReady });
        }

        // Le lien annuaire vient d'etre relu en base : c'est la preuve durable
        // que KoXo a bien cree l'identite et repris le mot de passe. Seulement
        // maintenant le secret peut disparaitre. L'acquitter plus tot — au
        // premier instantane, comme le faisait la version d'origine — le
        // perdait des que l'export echouait ensuite ou que l'API redemarrait.
        //
        // Avant la conclusion, et non apres : un arret entre les deux laisse
        // un cycle `directory_ready` dont le lien existe deja, donc que
        // l'export reprend par la branche normale, sans exiger de secret. Le
        // rejeu acquitte de nouveau — l'effacement est idempotent — puis
        // conclut.
        await _pendingPasswords.AcknowledgeAsync(
            record.PortalUserId,
            cancellationToken);

        await _repository.MarkReadyAsync(
            record.Id,
            DateTime.UtcNow,
            cancellationToken);

        return Success(
            BillingV2AdditionalUserMaterializationCodes.Ready,
            "L'identite est prete.",
            record with { Status = BillingV2UserIdentityStatuses.Ready });
    }

    /// <summary>
    /// Cree l'objet annuaire simule, en mode mock uniquement.
    /// </summary>
    /// <remarks>
    /// Ce chemin n'est jamais atteint quand KoXo est maitre de l'annuaire :
    /// l'appelant a deja choisi la resolution par <c>employeeNumber</c>.
    /// </remarks>
    private async Task<AdDirectoryObjectSummary?> CreateMockDirectoryObjectAsync(
        BillingV2AdditionalUserIdentityRecord record,
        string? plaintextPassword,
        CancellationToken cancellationToken)
    {
        var samAccountName = BuildMockSamAccountName(record);
        var createResult = await _activeDirectoryService.CreateUserAsync(
            record.CustomerReference,
            new CreateAdUserRequest(
                samAccountName,
                record.DisplayName,
                GivenName: null,
                Surname: null,
                _adConfiguration.Domain is null
                    ? null
                    : $"{samAccountName}@{_adConfiguration.Domain}",
                Description: record.CustomerReference,
                PersonalTitle: null,
                Initials: null,
                record.Email,
                Phone: null,
                CompanyName: null,
                record.CustomerReference),
            cancellationToken);
        if (createResult.StatusCode >= 400 || createResult.Value is null)
        {
            return null;
        }

        if (plaintextPassword is null)
        {
            return createResult.Value;
        }

        var passwordResult = await _activeDirectoryService.SetUserPasswordAsync(
            record.CustomerReference,
            createResult.Value.SamAccountName,
            plaintextPassword,
            cancellationToken);
        return passwordResult.StatusCode >= 400 || passwordResult.Value is null
            ? createResult.Value
            : passwordResult.Value;
    }

    /// <summary>
    /// Nom de compte simule, derive de l'identifiant KoXo.
    /// </summary>
    /// <remarks>
    /// Derive de <c>CLI-NNNNNN</c> et non du nom : en mock il doit seulement
    /// etre unique et stable. Aucune prediction du nommage reel n'est tentee —
    /// c'est precisement ce que KoXo ne permet pas.
    /// </remarks>
    private static string BuildMockSamAccountName(
        BillingV2AdditionalUserIdentityRecord record)
        => record.KoxoUniqueIdentifier.Replace("-", string.Empty)
            .ToLowerInvariant();

    // ------------------------------------------------------------------
    // 4. DESACTIVATION
    // ------------------------------------------------------------------

    public async Task<BillingV2AdditionalUserOperationResult> DisableAsync(
        string subscriptionUserId,
        string customerId,
        CancellationToken cancellationToken)
    {
        var gate = GuardProvisioningEnabled();
        if (gate is not null)
        {
            return gate;
        }

        var record = await _repository.FindBySubscriptionUserIdAsync(
            subscriptionUserId,
            cancellationToken);
        var guard = GuardOwnership(record, customerId);
        if (guard is not null)
        {
            return guard;
        }

        // Etat seulement. Aucun DELETE annuaire, aucun retrait du CSV KoXo :
        // retirer une ligne du CSV desactive le compte AD correspondant, et le
        // contrat de desactivation n'est pas assez etabli pour declencher cela
        // automatiquement ici.
        await _repository.MarkDisabledAsync(
            record!.Id,
            DateTime.UtcNow,
            cancellationToken);
        return new BillingV2AdditionalUserOperationResult(
            true,
            "ADDITIONAL_USER_DISABLED",
            "La place a ete marquee desactivee.",
            record.PortalUserId,
            BillingV2UserIdentityStatuses.Disabled);
    }

    // ------------------------------------------------------------------
    // Utilitaires
    // ------------------------------------------------------------------

    /// <summary>
    /// Refuse toute operation a effet reel quand le provisioning V2 est
    /// desactive.
    /// </summary>
    /// <remarks>
    /// Le service est dormant : aucune route publique ne le raccorde encore.
    /// Ce garde-fou existe pour que le jour ou une route l'atteindra, un
    /// drapeau a <c>false</c> suffise a tout arreter — et l'arrete <b>avant</b>
    /// la premiere ecriture, pas au milieu. La seule exception est la
    /// validation en lecture d'un jeton, qui ne mute rien.
    /// </remarks>
    private BillingV2AdditionalUserOperationResult? GuardProvisioningEnabled()
        => _billingConfiguration.ProvisioningEnabled
            ? null
            : Failure(
                BillingV2AdditionalUserMaterializationCodes
                    .ProvisioningDisabled,
                "Le provisioning Billing V2 est desactive.");

    private BillingV2AdditionalUserOperationResult? GuardOwnership(
        BillingV2AdditionalUserIdentityRecord? record,
        string customerId)
    {
        if (record is null)
        {
            return Failure(
                BillingV2AdditionalUserMaterializationCodes.LifecycleMissing,
                "Aucun cycle de vie d'identite pour cette place.");
        }

        if (!string.Equals(
                record.CustomerId,
                customerId,
                StringComparison.Ordinal))
        {
            // Meme reponse que l'absence : confirmer l'existence d'une place
            // d'un autre client serait deja une fuite.
            return Failure(
                BillingV2AdditionalUserMaterializationCodes.LifecycleMissing,
                "Aucun cycle de vie d'identite pour cette place.");
        }

        return null;
    }

    private async Task SendInvitationAsync(
        BillingV2AdditionalUserIdentityRecord record,
        string token,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var url = BuildSetPasswordUrl(token);
        var delivery = await _emailDispatch.SendAccountApprovedAsync(
            record.Email,
            record.DisplayName,
            url,
            correlationId,
            cancellationToken);
        if (!delivery.Succeeded)
        {
            _logger.LogWarning(
                "Additional user invitation not delivered ({Code}) for portal_user_id {PortalUserId}, correlation_id {CorrelationId}",
                delivery.Code,
                record.PortalUserId,
                correlationId);
        }
    }

    private async Task TriggerKoxoSyncAsync(
        BillingV2AdditionalUserIdentityRecord record,
        string trigger,
        CancellationToken cancellationToken)
    {
        if (!_adConfiguration.WritesEnabled)
        {
            return;
        }

        try
        {
            await _koxoSyncWebhook.TriggerAsync(
                new KoxoSyncWebhookTriggerRequest(
                    record.SubscriptionUserId,
                    record.PortalUserId,
                    record.CustomerReference,
                    trigger,
                    Guid.NewGuid().ToString("D"),
                    DateTime.UtcNow.ToString("O")),
                cancellationToken);
        }
        catch (Exception exception)
        {
            // Rattrapage, pas condition de succes : la synchronisation
            // planifiee repassera, et le cycle reste dans l'export.
            _logger.LogWarning(
                exception,
                "KoXo sync trigger failed for portal_user_id {PortalUserId}, trigger {Trigger}",
                record.PortalUserId,
                trigger);
        }
    }

    private string BuildSetPasswordUrl(string token)
    {
        var baseUrl = _emailConfiguration.PortalPublicUrl;
        var prefix = string.IsNullOrWhiteSpace(baseUrl)
            ? string.Empty
            : baseUrl.TrimEnd('/');
        return $"{prefix}/set-password?token={Uri.EscapeDataString(token)}";
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeEmail(string? value)
    {
        var trimmed = Normalize(value)?.ToLowerInvariant();
        if (trimmed is null
            || trimmed.Length > 255
            || trimmed.Count(character => character == '@') != 1
            || trimmed.StartsWith('@')
            || trimmed.EndsWith('@')
            || !trimmed[(trimmed.IndexOf('@') + 1)..].Contains('.'))
        {
            return null;
        }

        return trimmed;
    }

    private static BillingV2AdditionalUserOperationResult TokenInvalid()
        => new(
            false,
            PortalPasswordSetupCodes.TokenInvalid,
            "Ce lien de definition de mot de passe est invalide.");

    private static BillingV2AdditionalUserOperationResult Failure(
        string code,
        string message,
        BillingV2AdditionalUserIdentityRecord? record = null)
        => new(false, code, message, record?.PortalUserId, record?.Status);

    private static BillingV2AdditionalUserOperationResult Success(
        string code,
        string message,
        BillingV2AdditionalUserIdentityRecord record)
        => new(true, code, message, record.PortalUserId, record.Status);

    private static string DescribeTokenFailure(string code)
        => code switch
        {
            PortalPasswordSetupCodes.TokenExpired =>
                "Ce lien de definition de mot de passe a expire.",
            PortalPasswordSetupCodes.TokenAlreadyUsed =>
                "Ce lien de definition de mot de passe a deja ete utilise.",
            _ => "Ce lien de definition de mot de passe est invalide."
        };

    private static string DescribeRejection(string code)
        => code switch
        {
            BillingV2AdditionalUserRejectionCodes.SlotNotFound =>
                "Cette place d'abonnement est introuvable.",
            BillingV2AdditionalUserRejectionCodes.SlotSubscriptionMismatch =>
                "Cette place n'appartient pas a l'abonnement vise.",
            BillingV2AdditionalUserRejectionCodes.SlotCustomerMismatch =>
                "Cette place n'appartient pas a votre organisation.",
            BillingV2AdditionalUserRejectionCodes.CustomerNotFound =>
                "Le client de cet abonnement est introuvable ou inactif.",
            BillingV2AdditionalUserRejectionCodes.SlotIsPrimary =>
                "La place du contact principal ne peut pas etre reattribuee.",
            BillingV2AdditionalUserRejectionCodes.SlotNotActive =>
                "Cette place n'est pas active.",
            BillingV2AdditionalUserRejectionCodes.SubscriptionNotProvisionable =>
                "L'abonnement n'est pas dans un etat permettant d'equiper un utilisateur.",
            BillingV2AdditionalUserRejectionCodes.SlotAlreadyAssigned =>
                "Cette place est deja attribuee.",
            BillingV2AdditionalUserRejectionCodes.LifecycleAlreadyExists =>
                "Cette place a deja un cycle de vie d'identite.",
            BillingV2AdditionalUserRejectionCodes.SlotEntitlementMissing =>
                "Aucun droit utilisateur additionnel actif ne couvre cette place.",
            BillingV2AdditionalUserRejectionCodes.SlotScopeIncoherent =>
                "Cette place porte un droit dont le perimetre est incoherent.",
            BillingV2AdditionalUserRejectionCodes.EmailAlreadyUsed =>
                "Cette adresse e-mail est deja utilisee.",
            _ => "L'attribution de cette place a ete refusee."
        };
}
