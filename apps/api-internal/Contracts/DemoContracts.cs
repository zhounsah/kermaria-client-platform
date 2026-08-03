namespace Kermaria.ApiInternal.Contracts;

/// <summary>Valeurs canoniques des comptes de demonstration (V1.1).</summary>
public static class DemoKinds
{
    public const string Showcase = "showcase";
    public const string Trial = "trial";

    public static bool IsValid(string? value)
        => value is Showcase or Trial;
}

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
public sealed record DemoLifecycleSweepResult(
    int RevokedCount,
    int PurgedCount,
    IReadOnlyList<string> SkippedReferences,
    IReadOnlyList<string> RevokeFailures);

/// <summary>Reponse retournee apres creation d'un compte de demo.</summary>
public sealed record DemoAccountCreatedResponse(
    string CustomerReference,
    string Email,
    string Kind,
    string? ExpiresAt);
