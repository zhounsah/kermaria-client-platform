using System.Text.RegularExpressions;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Topologie KoXo pure : nommage des groupes primaires, des OU secondaires et
/// forme de l'identifiant unique.
/// </summary>
/// <remarks>
/// <para>
/// Extraite de <see cref="KoxoExportService"/> pour qu'un second consommateur —
/// le provisioning Billing V2 — puisse viser la meme OU que l'export sans
/// redupliquer la formule. Une topologie recopiee est une topologie qui derive :
/// deux endroits calculant le groupe secondaire finiraient par ne plus designer
/// le meme objet d'annuaire, et un quota serait pose a cote de la cible.
/// </para>
/// <para>
/// Cette classe est volontairement pure : aucune entree/sortie, aucun acces
/// annuaire, aucune dependance de configuration. Elle ne decrit que le nommage.
/// </para>
/// </remarks>
public static class KoxoDirectoryTopology
{
    /// <summary>
    /// OU commune hebergeant les identites de demonstration. Ne sert plus que de
    /// repli pour les comptes crees avant la reservation systematique d'un code
    /// de groupe (lot 5) : le cas nominal publie <c>DEMO-CLI-XXXXXX</c>.
    /// </summary>
    public const string DemoGroupReference = "CLI-DEMO";

    /// <summary>
    /// Prefixe des OU de demonstration. Il n'est pas cosmetique : KoXo ne cree
    /// un groupe secondaire dans l'annuaire que s'il est nouveau pour sa propre
    /// base. Un meme nom des deux cotes de la separation lui fait croire le
    /// groupe deja existant, et l'identite migree perd son groupe DEFINITIVEMENT
    /// — mesure en reel le 2026-08-06. Les deux branches doivent donc nommer
    /// leurs groupes secondaires differemment.
    /// </summary>
    public const string DemoGroupPrefix = "DEMO-";

    /// <summary>Groupe primaire KoXo des clients payants.</summary>
    public const string PrimaryGroupClients = "CLIENTS";

    /// <summary>
    /// Groupe primaire KoXo des comptes de demonstration. Ecrit en sequence
    /// d'echappement a dessein : la graphie doit correspondre AU BIT PRES a
    /// celle saisie dans l'IHM KoXo (<c>43 4c 49 45 4e 54 53 20 44 c3 89 4d 4f</c>
    /// en UTF-8), sans quoi la synchronisation est un no-op silencieux. Un
    /// fichier source relu dans un autre encodage ne peut pas alterer une
    /// sequence \u.
    /// </summary>
    public const string PrimaryGroupDemo = "CLIENTS D\u00C9MO";

    /// <summary>
    /// Forme imposee de <c>portal_users.koxo_unique_identifier</c>.
    /// </summary>
    /// <remarks>
    /// C'est cette valeur que KoXo reporte dans l'attribut AD
    /// <c>employeeNumber</c>, donc la seule cle de rattachement fiable entre un
    /// compte cree par KoXo et l'utilisateur portail : le nom est translittere
    /// et le <c>sAMAccountName</c> est derive par KoXo, donc ni l'un ni l'autre
    /// n'est predictible cote application.
    /// </remarks>
    private static readonly Regex UniqueIdentifierPattern =
        new("^CLI-\\d{6}$", RegexOptions.Compiled);

    public static bool IsValidUniqueIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && UniqueIdentifierPattern.IsMatch(value);

    /// <summary>
    /// Determine le profil KoXo qui prend l'identite en charge. Le decoupage
    /// suit la seule frontiere qui compte cote annuaire : les quotas et le
    /// modele de compte, qui ne s'attachent qu'a un groupe primaire.
    /// </summary>
    /// <remarks>
    /// Cloisonne aussi le rayon d'action de chaque synchronisation : avec un CSV
    /// unique et <c>DisableOrphanedAccounts</c> actif, une anomalie d'export cote
    /// demo desactivait de vrais clients payants.
    /// </remarks>
    public static string ResolvePrimaryGroup(bool isDemo)
        => isDemo ? PrimaryGroupDemo : PrimaryGroupClients;

    /// <summary>
    /// Determine l'OU cible cote KoXo, qui la cree si elle n'existe pas.
    /// </summary>
    /// <remarks>
    /// Trois cas :
    /// <list type="bullet">
    /// <item>essai en cours : le code reserve a la creation, PREFIXE — chaque
    /// essai a donc son OU propre sous <see cref="PrimaryGroupDemo"/>, et le
    /// prefixe garantit que le nom differe de celui de l'OU definitive ;</item>
    /// <item>compte converti : le meme code reserve, SANS prefixe, ce qui fait
    /// creer l'OU definitive a KoXo sans renommer la reference client ;</item>
    /// <item>client reel ordinaire : sa reference, qui nomme deja son OU.</item>
    /// </list>
    /// C'est le seul levier de la conversion cote annuaire : l'application ne
    /// deplace aucune identite elle-meme. Le changement de nom entre les deux
    /// premiers cas n'est donc pas un detail — c'est lui qui permet a KoXo de
    /// creer le groupe cible au lieu de le croire deja present.
    /// </remarks>
    /// <param name="koxoGroupReference">
    /// Code de groupe reserve a la creation d'un compte de demo. Null pour un
    /// client reel ordinaire, dont l'OU est nommee d'apres sa reference.
    /// </param>
    public static string ResolveSecondaryGroup(
        bool isDemo,
        string? koxoGroupReference,
        string customerReference)
        => isDemo
            // Repli sur l'OU commune historique pour les essais crees avant la
            // reservation systematique d'un code : ils n'en ont pas, et les
            // exclure de l'export les ferait passer pour orphelins donc
            // desactiver.
            ? DemoGroupPrefix + (koxoGroupReference ?? DemoGroupReference)
            : koxoGroupReference ?? customerReference;
}
