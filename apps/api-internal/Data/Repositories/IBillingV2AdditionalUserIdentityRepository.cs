namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Etats du cycle de vie d'identite d'une place d'abonnement.
/// </summary>
/// <remarks>
/// Ces valeurs sont celles de la contrainte CHECK de la migration 065 : toute
/// divergence serait rejetee par la base, pas silencieusement acceptee.
/// </remarks>
public static class BillingV2UserIdentityStatuses
{
    public const string AwaitingPassword = "awaiting_password";
    public const string KoxoPending = "koxo_pending";
    public const string DirectoryReady = "directory_ready";
    public const string Ready = "ready";
    public const string Failed = "failed";
    public const string Disabled = "disabled";

    public static readonly IReadOnlyList<string> All =
    [
        AwaitingPassword,
        KoxoPending,
        DirectoryReady,
        Ready,
        Failed,
        Disabled
    ];

    public static bool IsKnown(string? status)
        => status is not null && All.Contains(status, StringComparer.Ordinal);
}

/// <summary>
/// Etat d'une place d'abonnement, lu sous verrou dans la transaction
/// d'attribution.
/// </summary>
/// <remarks>
/// Contient tout ce dont la validation a besoin. La validation elle-meme
/// n'est pas dans le depot : elle est passee en rappel, pour que le mode mock
/// et MariaDB executent litteralement la meme regle.
/// </remarks>
public sealed record BillingV2AdditionalUserSlotSnapshot(
    string SubscriptionUserId,
    string SubscriptionId,
    string SubscriptionCustomerId,
    string SubscriptionStatus,
    bool IsPrimary,
    string SlotStatus,
    string? IdentityReference,
    string? CustomerReference,
    bool HasActiveUserSlotEntitlement,
    int IncompatibleScopedItemCount,
    bool HasExistingLifecycle,
    bool EmailAlreadyUsed);

/// <summary>
/// Place USER-ADDITIONAL telle qu'elle est lue pour l'espace client.
/// </summary>
/// <remarks>
/// Lecture <b>produit</b> : elle ne porte ni identifiant KoXo, ni objectGUID,
/// ni code d'echec. Ces informations n'ont aucun usage cote client et leur
/// seule presence dans une projection finit toujours par remonter jusqu'a
/// l'ecran.
/// </remarks>
public sealed record BillingV2AdditionalUserSlotView(
    string SubscriptionUserId,
    string? DisplayName,
    string? Email,
    bool IsAssigned,
    string? LifecycleStatus);

public sealed record BillingV2AdditionalUserAssignmentCommand(
    string CustomerId,
    string SubscriptionId,
    string SubscriptionUserId,
    string PortalUserId,
    string LifecycleId,
    string PasswordSetupId,
    string PasswordSetupTokenHash,
    DateTime PasswordSetupExpiresAtUtc,
    string PasswordSetupPurpose,
    string NormalizedEmail,
    string DisplayName,
    string? PersonalTitle,
    string? GivenName,
    string? Surname,
    DateOnly? BirthDate,
    string? Initials,
    string? Phone,
    string? ActorReference);

public sealed record BillingV2AdditionalUserIdentityRecord(
    string Id,
    string SubscriptionUserId,
    string SubscriptionId,
    string CustomerId,
    string CustomerReference,
    string PortalUserId,
    string KoxoUniqueIdentifier,
    string Status,
    string? FailureCode,
    string? DirectoryObjectGuid,
    string Email,
    string DisplayName);

/// <summary>
/// Resultat de l'attribution : soit un refus motive, soit le cycle de vie cree.
/// </summary>
public sealed record BillingV2AdditionalUserAssignmentResult(
    string? RejectionCode,
    BillingV2AdditionalUserIdentityRecord? Created)
{
    public bool Succeeded => RejectionCode is null && Created is not null;

    public static BillingV2AdditionalUserAssignmentResult Reject(string code)
        => new(code, null);

    public static BillingV2AdditionalUserAssignmentResult Success(
        BillingV2AdditionalUserIdentityRecord record)
        => new(null, record);
}

/// <summary>
/// Cycle de vie d'identite des places USER-ADDITIONAL.
/// </summary>
public interface IBillingV2AdditionalUserIdentityRepository
{
    bool IsPersistent { get; }

    /// <summary>
    /// Attribue une personne reelle a une place, en une seule transaction.
    /// </summary>
    /// <param name="validate">
    /// Regle metier appliquee a l'etat lu <b>sous verrou</b>. Retourner un code
    /// annule la transaction entiere.
    /// </param>
    /// <remarks>
    /// <para>
    /// La transaction couvre : verrou de la place, allocation
    /// <c>CLI-NNNNNN</c>, creation de l'utilisateur portail, mise a jour de la
    /// place, creation du cycle de vie, emission du jeton de mot de passe et
    /// audit Billing V2. Un echec a n'importe quelle etape ne laisse rien
    /// derriere : ni identifiant KoXo consomme sans utilisateur, ni utilisateur
    /// sans cycle de vie.
    /// </para>
    /// <para>
    /// Le rappel de validation n'est pas une commodite : il garantit que la
    /// regle testee hors base est exactement celle appliquee en base, plutot
    /// qu'une reimplantation parallele en SQL.
    /// </para>
    /// </remarks>
    Task<BillingV2AdditionalUserAssignmentResult> AssignAsync(
        BillingV2AdditionalUserAssignmentCommand command,
        Func<BillingV2AdditionalUserSlotSnapshot, string?> validate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Places USER-ADDITIONAL d'un abonnement, <b>places vides comprises</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Une place n'est USER-ADDITIONAL que si un droit contractuel actif la
    /// couvre : item de perimetre utilisateur, service actif et regle de
    /// provisioning <c>contractual_entitlement</c> ciblant <c>user_slot</c>.
    /// <c>is_primary = 0</c> ne prouve rien a lui seul — une place secondaire
    /// peut exister sans qu'aucun utilisateur additionnel n'ait ete vendu, et
    /// la presenter comme attribuable proposerait une action que l'attribution
    /// refuserait ensuite en <c>SLOT_ENTITLEMENT_MISSING</c>.
    /// </para>
    /// <para>
    /// <paramref name="customerId"/> n'est pas un filtre de confort : une
    /// place appartenant a un autre client ne doit pas seulement etre masquee,
    /// elle doit etre indistinguable d'une place inexistante.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<BillingV2AdditionalUserSlotView>>
        ListAdditionalUserSlotsAsync(
            string customerId,
            string subscriptionId,
            CancellationToken cancellationToken);

    Task<BillingV2AdditionalUserIdentityRecord?> FindByPortalUserIdAsync(
        string portalUserId,
        CancellationToken cancellationToken);

    Task<BillingV2AdditionalUserIdentityRecord?> FindBySubscriptionUserIdAsync(
        string subscriptionUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cycles qui attendent uniquement la convergence KoXo/annuaire.
    /// </summary>
    Task<IReadOnlyList<BillingV2AdditionalUserIdentityRecord>>
        ListMaterializationCandidatesAsync(
            int limit,
            CancellationToken cancellationToken);

    Task TouchMaterializationAttemptAsync(
        string id,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>awaiting_password</c> (ou reprise) -&gt; <c>koxo_pending</c>.
    /// </summary>
    /// <remarks>
    /// Seul cet etat autorise l'export KoXo sans <c>customer_ad_links</c> : la
    /// transition n'est donc franchie qu'une fois le mot de passe reellement
    /// pose, jamais a l'attribution.
    /// </remarks>
    Task<bool> MarkKoxoPendingAsync(
        string id,
        DateTime passwordSetAtUtc,
        DateTime koxoTriggeredAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>koxo_pending</c> -&gt; <c>directory_ready</c>, en fixant l'objectGUID
    /// adopte.
    /// </summary>
    /// <remarks>
    /// Refuse si un autre objectGUID est deja enregistre : une identite
    /// annuaire deja adoptee ne se remplace pas silencieusement par une autre.
    /// </remarks>
    Task<bool> MarkDirectoryResolvedAsync(
        string id,
        string directoryObjectGuid,
        DateTime resolvedAtUtc,
        CancellationToken cancellationToken);

    /// <summary><c>directory_ready</c> -&gt; <c>ready</c>.</summary>
    Task<bool> MarkReadyAsync(
        string id,
        DateTime linkedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>Marque un echec explicite, sans quitter <c>ready</c>.</summary>
    Task<bool> MarkFailedAsync(
        string id,
        string failureCode,
        string? failureDetail,
        CancellationToken cancellationToken);

    /// <summary>
    /// Bascule le cycle de vie en <c>disabled</c>.
    /// </summary>
    /// <remarks>
    /// Ce lot n'ajoute aucune suppression annuaire ni KoXo : la desactivation
    /// n'est ici qu'un etat, pose explicitement, qui retire la place du chemin
    /// de materialisation. Le contrat de suppression reelle n'est pas etabli.
    /// </remarks>
    Task<bool> MarkDisabledAsync(
        string id,
        DateTime disabledAtUtc,
        CancellationToken cancellationToken);
}
