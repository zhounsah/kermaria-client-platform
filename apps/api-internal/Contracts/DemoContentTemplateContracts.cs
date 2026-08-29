namespace Kermaria.ApiInternal.Contracts;

/// <summary>Un service semé par un modele de demonstration.</summary>
public sealed record DemoContentTemplateServiceItem(
    // Contraint par le registre ferme des types de service : l'administration
    // ne peut pas inventer un type que le code ne sait pas provisionner.
    string ServiceType,
    string Name,
    string Description,
    string Scope);

public sealed record DemoContentTemplateItem(
    string TemplateKey,
    string Label,
    string Description,
    bool Enabled,
    int DisplayOrder,
    // 0 pour un modele encore porte par le code : il n'existe pas en base et sa
    // premiere ecriture se fait donc avec `expectedVersion = 0`.
    int Version,
    // "code" ou "database" : l'administration doit voir d'ou vient le modele
    // reellement applique.
    string Source,
    bool Editable,
    string? UpdatedAt,
    string? UpdatedByUserId,
    IReadOnlyList<DemoContentTemplateServiceItem> Services,
    // Profils de demonstration qui referencent ce modele : supprimer un modele
    // reference laisserait un compte de demo sans aucun service.
    IReadOnlyList<string> UsedByProfileKeys);

public sealed record DemoContentTemplateRevisionItem(
    string TemplateKey,
    int Version,
    string Outcome,
    string? ActorUserId,
    string CorrelationId,
    string CreatedAt);

/// <summary>
/// Destination AD des identites converties (specification, section 15.2). Elle
/// est sensible : une OU hors des racines autorisees deplacerait de vraies
/// identites hors du perimetre borne. Elle reste donc en lecture seule ici et se
/// regle sur la machine, avant un redemarrage.
/// </summary>
public sealed record DemoConversionTargetView(
    string EnvironmentVariable,
    string? TargetOrganizationalUnitDn,
    bool Configured,
    // Faux quand la valeur configuree sort des racines autorisees : la
    // conversion serait alors refusee au moment du deplacement.
    bool WithinAllowedRoots,
    IReadOnlyList<string> AllowedRoots,
    string AdIntegrationMode,
    string Classification,
    bool RestartRequired);

public sealed record DemoContentTemplateAdminView(
    IReadOnlyList<DemoContentTemplateItem> Templates,
    IReadOnlyList<string> KnownServiceTypes,
    IReadOnlyList<DemoContentTemplateRevisionItem> Revisions,
    // "code" tant que la table est vide ou illisible : le registre C# fait
    // alors autorite et rien n'est administrable.
    string Authority,
    bool Persistent,
    string CommercialTermsLabel,
    DemoConversionTargetView Conversion);

public sealed record DemoContentTemplateServicePayload(
    string? ServiceType,
    string? Name,
    string? Description,
    string? Scope);

public sealed record DemoContentTemplateSavePayload(
    string? TemplateKey,
    string? Label,
    string? Description,
    bool Enabled,
    int? DisplayOrder,
    int ExpectedVersion,
    IReadOnlyList<DemoContentTemplateServicePayload>? Services);

public sealed record DemoContentTemplateMutationResponse(
    string Code,
    string Message,
    DemoContentTemplateAdminView? View,
    string CorrelationId);
