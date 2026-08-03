namespace Kermaria.ApiInternal.Contracts;

/// <summary>Valeurs canoniques des comptes de demonstration (V1.1).</summary>
public static class DemoKinds
{
    public const string Showcase = "showcase";
    public const string Trial = "trial";

    public static bool IsValid(string? value)
        => value is Showcase or Trial;
}

/// <summary>Demande de conversion d'un compte d'essai en client reel (Lot 4).</summary>
/// <param name="OfferExternalReference">
/// Offre reelle dont les groupes AD remplacent les <c>GG_DEMO_*</c>. Facultative :
/// sans elle, la conversion se contente de retirer l'acces de demonstration.
/// </param>
public sealed record DemoConversionRequest(string? OfferExternalReference);

/// <summary>Issue d'une conversion essai -> reel.</summary>
/// <param name="Converted">
/// Vrai si le compte est desormais un client reel (y compris s'il l'etait deja :
/// la conversion est idempotente).
/// </param>
/// <param name="AlreadyConverted">Vrai si la conversion avait deja ete faite.</param>
public sealed record DemoConversionResult(
    bool Converted,
    bool AlreadyConverted,
    string ResultCode,
    string CustomerReference,
    IReadOnlyList<string> DemoGroupsRemoved,
    IReadOnlyList<string> RealGroupsGranted,
    bool IdentityMoved);

/// <summary>Matrice de capacites (axe B) d'un profil de demo.</summary>
public sealed record DemoCapabilities(
    string EmailMode,
    string BpceMode,
    string PaymentMode,
    string AdProvisioningMode,
    IReadOnlyList<string> AdGroups,
    int? StorageQuotaGo,
    string RdsSessionMode);

/// <summary>Profil de demo expose par l'API (sans identifiant interne).</summary>
public sealed record DemoProfileSummary(
    string Key,
    string Label,
    string Kind,
    string? ContentTemplateKey,
    int LifetimeDays,
    string Status,
    DemoCapabilities Capabilities);

/// <summary>Charge utile de creation/mise a jour d'un profil de demo.</summary>
public sealed record DemoProfilePayload(
    string? Key,
    string? Label,
    string? Kind,
    string? ContentTemplateKey,
    string? EmailMode,
    string? BpceMode,
    string? PaymentMode,
    string? AdProvisioningMode,
    IReadOnlyList<string>? AdGroups,
    int? StorageQuotaGo,
    string? RdsSessionMode,
    int? LifetimeDays,
    string? Status);

/// <summary>Template de contenu (axe A) disponible pour un compte de demo.</summary>
public sealed record DemoContentTemplateSummary(
    string Key,
    string Label,
    IReadOnlyList<string> ServiceNames);

/// <summary>Demande de creation d'un compte de demo depuis l'admin.</summary>
/// <remarks>
/// <see cref="SelectedServiceNames"/> pilote la composition a la carte : si null,
/// tous les services du template sont semes ; si fourni, seuls les services du
/// template dont le nom figure dans la liste sont retenus (liste vide = aucun
/// service).
/// </remarks>
public sealed record DemoAccountCreateRequest(
    string? ProfileKey,
    string? DisplayName,
    string? Email,
    string? InitialPassword,
    string? UserDisplayName,
    int? LifetimeDaysOverride,
    IReadOnlyList<string>? SelectedServiceNames);

/// <summary>Conflit fonctionnel sur un compte de demo (ex. e-mail deja utilise).</summary>
public sealed class DemoConflictException : Exception
{
    public DemoConflictException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Resume d'un compte de demo pour la vue admin dediee.</summary>
/// <remarks>
/// <see cref="RevokedAt"/> est renseigne quand le balayage d'expiration a revoque
/// l'acces reel (retrait des groupes GG_DEMO_* + desactivation) avant purge.
/// </remarks>
public sealed record DemoAccountSummary(
    string CustomerReference,
    string DisplayName,
    string Kind,
    string? ProfileKey,
    int ServiceCount,
    string CreatedAt,
    string? ExpiresAt,
    string? RevokedAt);

/// <summary>Resultat d'un balayage du cycle de vie des comptes de demo (Lot 3).</summary>
/// <param name="RevokedCount">Trials echus dont l'acces reel a ete revoque.</param>
/// <param name="PurgedCount">Comptes de demo echus supprimes.</param>
/// <param name="SkippedReferences">
/// Comptes echus conserves (contenu metier hors cascade de purge).
/// </param>
/// <param name="RevokeFailures">
/// References des trials dont la revocation AD a echoue (a reessayer au prochain
/// passage) ; le compte n'est ni marque revoque ni purge.
/// </param>
/// <param name="ReprovisionedCount">
/// Essais dont l'acces reel a ete applique lors de cette passe, l'identite AD
/// n'ayant pas encore existe au moment de la creation.
/// </param>
public sealed record DemoLifecycleSweepResult(
    int RevokedCount,
    int PurgedCount,
    IReadOnlyList<string> SkippedReferences,
    IReadOnlyList<string> RevokeFailures,
    int ReprovisionedCount);

/// <summary>Reponse retournee apres creation d'un compte de demo.</summary>
public sealed record DemoAccountCreatedResponse(
    string CustomerReference,
    string Email,
    string Kind,
    string? ExpiresAt);
