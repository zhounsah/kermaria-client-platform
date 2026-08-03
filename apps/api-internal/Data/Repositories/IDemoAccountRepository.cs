using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>Un service seme sur un compte de demo (ligne <c>customer_services</c>).</summary>
public sealed record DemoServiceSeed(
    string ServiceType,
    string Name,
    string Description,
    string Scope,
    string CommercialTerms);

/// <summary>
/// Trial echu a revoquer : identite portail + reference client + groupes AD
/// GG_DEMO_* a retirer (issus du profil applique). Alimente le balayage
/// d'expiration (Lot 3).
/// </summary>
public sealed record DemoExpiredTrial(
    string CustomerId,
    string CustomerReference,
    string PortalUserId,
    IReadOnlyList<string> AdGroups);

/// <summary>
/// Compte de demo candidat a la conversion en client reel (Lot 4).
/// </summary>
/// <param name="AlreadyConverted">
/// Vrai si <c>demo_converted_at</c> est deja renseigne : la conversion a deja
/// eu lieu, il ne faut pas la rejouer.
/// </param>
public sealed record DemoConversionCandidate(
    string CustomerId,
    string CustomerReference,
    string PortalUserId,
    string DemoKind,
    string? ProfileKey,
    IReadOnlyList<string> AdGroups,
    bool AlreadyConverted);

/// <summary>Specification complete d'un compte de demo a materialiser.</summary>
public sealed record DemoAccountCreationSpec(
    string CustomerId,
    string ExternalReference,
    string DisplayName,
    string CustomerType,
    string DemoProfileId,
    string DemoKind,
    DateTime? DemoExpiresAtUtc,
    string? DemoCreatedByUserId,
    string PortalUserId,
    string Email,
    string PasswordHash,
    string UserDisplayName,
    IReadOnlyList<DemoServiceSeed> Services);

/// <summary>
/// Cycle de vie des comptes de demonstration/essai (V1.1).
/// </summary>
public interface IDemoAccountRepository
{
    /// <summary>Indique si un utilisateur portail existe deja pour cet e-mail.</summary>
    Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>Materialise un compte de demo (customer + portal_user + services).</summary>
    Task CreateDemoAccountAsync(
        DemoAccountCreationSpec spec,
        CancellationToken cancellationToken = default);

    /// <summary>Liste les comptes de demo pour la vue admin dediee.</summary>
    Task<IReadOnlyList<DemoAccountSummary>> ListDemoAccountsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Liste les comptes de demo <c>trial</c> echus, en acces reel cadre
    /// (<c>ad_provisioning_mode = real_scoped</c>) et non encore revoques
    /// (<c>demo_revoked_at IS NULL</c>), avec les groupes GG_DEMO_* du profil.
    /// </summary>
    Task<IReadOnlyList<DemoExpiredTrial>> ListExpiredTrialsToRevokeAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Horodate le provisioning reel d'un trial (declenchement KoXo/AD).</summary>
    Task MarkTrialProvisionedAsync(
        string customerId,
        DateTime provisionedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Horodate la revocation d'un trial echu (idempotence du balayage).</summary>
    Task MarkTrialRevokedAsync(
        string customerId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrouve un compte de demo par sa reference externe, avec les groupes
    /// <c>GG_DEMO_*</c> de son profil. Renvoie <c>null</c> si la reference ne
    /// designe pas un compte de demo.
    /// </summary>
    Task<DemoConversionCandidate?> FindConversionCandidateAsync(
        string customerReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bascule le compte en client reel : <c>is_demo = FALSE</c>, marqueurs de
    /// demo remis a NULL (ce qui le sort du balayage d'expiration et de la
    /// purge), provenance conservee dans <c>demo_source_profile_key</c>.
    /// </summary>
    Task MarkConvertedAsync(
        string customerId,
        DateTime convertedAtUtc,
        string? actorUserId,
        string? sourceProfileKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Purge par lot les comptes <c>is_demo</c> dont l'echeance
    /// <c>demo_expires_at</c> est passee.
    /// </summary>
    /// <remarks>
    /// V1.1 : la purge supprime l'identite et le contenu deja gere
    /// (<c>customer_services</c>). Un compte qui porte du contenu metier
    /// non encore couvert par la cascade (factures, demandes, abonnements,
    /// docs commerciaux, notifications...) est <b>saute</b> et remonte dans
    /// <see cref="DemoPurgeResult.SkippedCustomerReferences"/> plutot que de
    /// declencher une erreur de cle etrangere. La cascade sera etendue au fil
    /// de l'ajout de contenu (Lots suivants).
    /// </remarks>
    Task<DemoPurgeResult> PurgeExpiredDemoCustomersAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>Resultat d'une passe de purge des comptes de demo.</summary>
/// <param name="PurgedCustomerCount">Nombre de comptes demo supprimes.</param>
/// <param name="SkippedCustomerReferences">
/// References externes des comptes demo echus mais conserves parce qu'ils
/// portaient encore du contenu metier non couvert par la cascade actuelle.
/// </param>
public sealed record DemoPurgeResult(
    int PurgedCustomerCount,
    IReadOnlyList<string> SkippedCustomerReferences);
