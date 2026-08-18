namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Codes de refus de l'attribution d'une place USER-ADDITIONAL.
/// </summary>
/// <remarks>
/// Chaque refus nomme la regle violee. Aucun code fourre-tout : un appelant qui
/// ne sait pas pourquoi il a ete refuse finira par reessayer en boucle.
/// </remarks>
public static class BillingV2AdditionalUserRejectionCodes
{
    public const string SlotNotFound = "SLOT_NOT_FOUND";
    public const string SlotSubscriptionMismatch = "SLOT_SUBSCRIPTION_MISMATCH";
    public const string SlotCustomerMismatch = "SLOT_CUSTOMER_MISMATCH";
    public const string CustomerNotFound = "CUSTOMER_NOT_FOUND";
    public const string SlotIsPrimary = "SLOT_IS_PRIMARY";
    public const string SlotNotActive = "SLOT_NOT_ACTIVE";
    public const string SubscriptionNotProvisionable =
        "SUBSCRIPTION_NOT_PROVISIONABLE";
    public const string SlotAlreadyAssigned = "SLOT_ALREADY_ASSIGNED";
    public const string SlotEntitlementMissing = "SLOT_ENTITLEMENT_MISSING";
    public const string SlotScopeIncoherent = "SLOT_SCOPE_INCOHERENT";
    public const string EmailAlreadyUsed = "EMAIL_ALREADY_USED";
    public const string LifecycleAlreadyExists = "LIFECYCLE_ALREADY_EXISTS";
    public const string InvalidIdentity = "INVALID_IDENTITY";
}

/// <summary>
/// Conventions de nommage propres a ce cycle de vie.
/// </summary>
public static class BillingV2AdditionalUserIdentityConventions
{
    /// <summary>
    /// Statut d'abonnement autorisant la materialisation d'une place.
    /// </summary>
    /// <remarks>
    /// Aligne sur la projection de provisioning, qui ne considere que
    /// <c>sub.status = 'active'</c>. Deux definitions differentes de
    /// « abonnement provisionnable » finiraient par equiper un utilisateur que
    /// le moteur de provisioning refuse ensuite de servir.
    /// </remarks>
    public const string ProvisionableSubscriptionStatus = "active";

    /// <summary>Statut contractuel exige de la place.</summary>
    public const string ActiveSlotStatus = "active";

    /// <summary>
    /// <c>purpose</c> des jetons de mot de passe emis par ce cycle de vie.
    /// </summary>
    public const string PasswordSetupPurpose = "billing_v2_additional_user";

    /// <summary>
    /// Sujet d'identite des utilisateurs portail crees ici.
    /// </summary>
    /// <remarks>
    /// Le depot d'inscription pose <c>signup-{id}</c> et celui des comptes de
    /// demonstration <c>demo-{id}</c> : le prefixe dit d'ou vient l'identite.
    /// Reutiliser <c>signup-</c> pour une place d'abonnement laisserait croire
    /// a une demande d'inscription qui n'a jamais existe.
    /// </remarks>
    public static string BuildSubject(string portalUserId)
        => $"billing-v2-user-{portalUserId}";
}
