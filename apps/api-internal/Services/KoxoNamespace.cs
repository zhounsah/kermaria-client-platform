using System.Globalization;
using System.Text.RegularExpressions;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Espace de nommage KoXo d'une instance : forme des identifiants uniques
/// (reportes par KoXo dans <c>employeeNumber</c>), prefixe des references
/// client (qui nomment les OU secondaires) et groupes primaires (profils KoXo).
/// </summary>
/// <remarks>
/// <para>
/// DEV et PROD partagent le domaine <c>clients.home.bzh</c>. Deux instances qui
/// tireraient leurs identifiants et leurs references dans le meme espace se
/// les disputeraient : le premier compte DEV recevrait <c>CLI-000001</c>, deja
/// porte par un compte PROD, et une synchronisation KoXo alimentee par la DEV
/// reecrirait ce compte. Le namespace rend ces deux espaces disjoints par
/// construction, pas par convention.
/// </para>
/// <para>
/// Il est tout ou rien : soit les quatre valeurs sont celles de la production
/// (defaut, comportement historique au bit pres), soit les quatre en different.
/// Un namespace partiel — identifiants distincts mais groupe primaire
/// <c>CLIENTS</c>, par exemple — ferait publier des identites DEV dans le profil
/// PROD, et il est refuse au demarrage.
/// </para>
/// <para>
/// Fixe une fois au demarrage (<see cref="Initialize"/>), avant toute requete :
/// chaque point qui alloue, valide ou aiguille une identite lit
/// <see cref="Current"/>, donc aucun ne peut rester sur l'ancien espace.
/// </para>
/// </remarks>
public sealed record KoxoNamespace(
    string IdentifierPrefix,
    string CustomerReferencePrefix,
    string PrimaryGroupClients,
    string PrimaryGroupDemo)
{
    /// <summary>Nombre de chiffres de la partie sequentielle d'un identifiant.</summary>
    public const int IdentifierDigits = 6;

    /// <summary>Longueur de <c>portal_users.koxo_unique_identifier</c> (VARCHAR(32)).</summary>
    public const int IdentifierMaxLength = 32;

    /// <summary>
    /// Longueur de <c>customers.koxo_group_reference</c> (VARCHAR(32)), la plus
    /// courte des colonnes qui recoivent une reference client.
    /// </summary>
    public const int CustomerReferenceMaxLength = 32;

    /// <summary>Partie aleatoire d'une reference client.</summary>
    public const int CustomerReferenceRandomLength = 6;

    public const string IdentifierPrefixVariable = "KOXO_IDENTIFIER_PREFIX";
    public const string CustomerReferencePrefixVariable = "CUSTOMER_REFERENCE_PREFIX";
    public const string PrimaryGroupClientsVariable = "KOXO_PRIMARY_GROUP_CLIENTS";
    public const string PrimaryGroupDemoVariable = "KOXO_PRIMARY_GROUP_DEMO";

    /// <summary>
    /// Espace historique de la production. Toute instance sans configuration
    /// explicite y reste : <c>CLI-000001</c>, <c>CLI-XXXXXX</c>, <c>CLIENTS</c>,
    /// <c>CLIENTS DÉMO</c>.
    /// </summary>
    public static KoxoNamespace Production { get; } = new(
        "CLI-",
        "CLI-",
        KoxoDirectoryTopology.PrimaryGroupClients,
        KoxoDirectoryTopology.PrimaryGroupDemo);

    private static readonly object InitializationGate = new();
    private static KoxoNamespace? _initialized;
    private static readonly AsyncLocal<KoxoNamespace?> ScopedOverride = new();

    /// <summary>
    /// Namespace de l'instance. Production tant que <see cref="Initialize"/>
    /// n'a pas ete appele.
    /// </summary>
    public static KoxoNamespace Current
        => ScopedOverride.Value ?? _initialized ?? Production;

    public bool IsProduction => this == Production;

    /// <summary>
    /// Fixe le namespace de l'instance. Idempotent pour une meme valeur ; une
    /// valeur differente leve, car deux namespaces successifs dans un meme
    /// processus produiraient des identifiants des deux espaces.
    /// </summary>
    public static void Initialize(KoxoNamespace value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (InitializationGate)
        {
            if (_initialized is not null && _initialized != value)
            {
                throw new InvalidOperationException(
                    "Le namespace KoXo est deja fixe pour ce processus.");
            }

            _initialized = value;
        }
    }

    /// <summary>
    /// Reservee aux tests : substitue un namespace pour le flux asynchrone
    /// courant, sans toucher a celui de l'instance. Internal, visible du seul
    /// projet de tests (InternalsVisibleTo) ; le code applicatif ne l'appelle
    /// jamais.
    /// </summary>
    internal static IDisposable BeginTestScope(KoxoNamespace value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var previous = ScopedOverride.Value;
        ScopedOverride.Value = value;
        return new Scope(previous);
    }

    /// <summary>Identifiant unique KoXo de rang <paramref name="sequence"/>.</summary>
    public string FormatIdentifier(long sequence)
        => IdentifierPrefix + sequence.ToString(
            "D" + IdentifierDigits.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);

    /// <summary>
    /// Vrai si la valeur appartient a CE namespace : prefixe exact puis six
    /// chiffres. Un identifiant de l'autre espace est donc refuse, dans les deux
    /// sens — la DEV rejette <c>CLI-000001</c>, la PROD rejette
    /// <c>CLI-D000001</c>.
    /// </summary>
    public bool IsValidUniqueIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != IdentifierPrefix.Length + IdentifierDigits
            || !value.StartsWith(IdentifierPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = IdentifierPrefix.Length; index < value.Length; index++)
        {
            if (value[index] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    public string ResolvePrimaryGroup(bool isDemo)
        => isDemo ? PrimaryGroupDemo : PrimaryGroupClients;

    private sealed class Scope(KoxoNamespace? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ScopedOverride.Value = previous;
            _disposed = true;
        }
    }
}

public static class KoxoNamespaceResolver
{
    // Lettres, un tiret, puis au plus trois lettres : « CLI- », « CLI-D ».
    // Aucun chiffre, pour qu'un prefixe suivi de six chiffres ne puisse jamais
    // reproduire un identifiant d'un autre prefixe.
    private static readonly Regex IdentifierPrefixPattern = new(
        "^[A-Z]{2,8}-[A-Z]{0,3}$",
        RegexOptions.CultureInvariant);

    // Un a trois segments « LETTRES- » : « CLI- », « DEV-CLI- ».
    private static readonly Regex CustomerReferencePrefixPattern = new(
        "^(?:[A-Z]{2,8}-){1,3}$",
        RegexOptions.CultureInvariant);

    // Nom de profil KoXo, recopie tel quel dans le CSV et l'IHM KoXo. Les
    // separateurs CSV et DN, les guillemets et les controles sont exclus.
    private static readonly Regex PrimaryGroupPattern = new(
        "^[\\p{L}\\p{N}][\\p{L}\\p{N} _-]{0,62}[\\p{L}\\p{N}]$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Resout le namespace depuis la configuration. Sans aucune des quatre
    /// cles, rend <see cref="KoxoNamespace.Production"/>. Leve sur toute valeur
    /// invalide, partielle ou melangeant les deux espaces.
    /// </summary>
    public static KoxoNamespace Resolve(IConfiguration configuration)
    {
        var candidate = ReadCandidate(configuration);
        var errors = Validate(candidate);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Namespace KoXo invalide : " + string.Join(" ; ", errors));
        }

        return candidate;
    }

    /// <summary>
    /// Lit les quatre cles sans rien valider ; une cle absente prend la valeur
    /// de production. Sert au garde-fou DEV/PROD, qui doit pouvoir signaler un
    /// namespace invalide au lieu de lever.
    /// </summary>
    public static KoxoNamespace ReadCandidate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var production = KoxoNamespace.Production;
        return new KoxoNamespace(
            Read(configuration, KoxoNamespace.IdentifierPrefixVariable)
                ?? production.IdentifierPrefix,
            Read(configuration, KoxoNamespace.CustomerReferencePrefixVariable)
                ?? production.CustomerReferencePrefix,
            Read(configuration, KoxoNamespace.PrimaryGroupClientsVariable)
                ?? production.PrimaryGroupClients,
            Read(configuration, KoxoNamespace.PrimaryGroupDemoVariable)
                ?? production.PrimaryGroupDemo);
    }

    /// <summary>Regles du namespace, sans lever : sert aussi au garde-fou DEV/PROD.</summary>
    public static IReadOnlyList<string> Validate(KoxoNamespace candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var errors = new List<string>();
        var production = KoxoNamespace.Production;

        if (!IdentifierPrefixPattern.IsMatch(candidate.IdentifierPrefix)
            || candidate.IdentifierPrefix.Length + KoxoNamespace.IdentifierDigits
                > KoxoNamespace.IdentifierMaxLength)
        {
            errors.Add($"{KoxoNamespace.IdentifierPrefixVariable} doit suivre la forme LETTRES-[LETTRES] (ex. CLI-D)");
        }

        if (!CustomerReferencePrefixPattern.IsMatch(candidate.CustomerReferencePrefix)
            || candidate.CustomerReferencePrefix.Length
                + KoxoNamespace.CustomerReferenceRandomLength
                > KoxoNamespace.CustomerReferenceMaxLength)
        {
            errors.Add($"{KoxoNamespace.CustomerReferencePrefixVariable} doit suivre la forme LETTRES- repetee (ex. DEV-CLI-)");
        }

        foreach (var (variable, value) in new[]
        {
            (KoxoNamespace.PrimaryGroupClientsVariable, candidate.PrimaryGroupClients),
            (KoxoNamespace.PrimaryGroupDemoVariable, candidate.PrimaryGroupDemo)
        })
        {
            if (!PrimaryGroupPattern.IsMatch(value))
            {
                errors.Add($"{variable} doit etre un nom de profil KoXo simple (lettres, chiffres, espace, tiret)");
            }
        }

        if (string.Equals(
                candidate.PrimaryGroupClients,
                candidate.PrimaryGroupDemo,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("les groupes primaires clients et demonstration doivent differer");
        }

        if (candidate.IsProduction)
        {
            return errors;
        }

        // Tout ou rien : chaque valeur hors production doit quitter l'espace de
        // production, faute de quoi une partie des identites DEV y retomberait.
        if (candidate.IdentifierPrefix == production.IdentifierPrefix)
        {
            errors.Add($"namespace partiel : {KoxoNamespace.IdentifierPrefixVariable} reste celui de la production");
        }

        if (candidate.CustomerReferencePrefix == production.CustomerReferencePrefix)
        {
            errors.Add($"namespace partiel : {KoxoNamespace.CustomerReferencePrefixVariable} reste celui de la production");
        }

        // Une reference de production precede toujours de « CLI- » ; une OU de
        // demonstration, de « DEMO- ». Une reference hors production qui
        // commencerait ainsi se confondrait a l'oeil avec l'une ou l'autre, et
        // « DEMO-CLI-XXXXXX » designerait litteralement une OU de demo PROD.
        if (candidate.CustomerReferencePrefix.StartsWith(
                production.CustomerReferencePrefix,
                StringComparison.Ordinal)
            || candidate.CustomerReferencePrefix.StartsWith(
                KoxoDirectoryTopology.DemoGroupPrefix,
                StringComparison.Ordinal))
        {
            errors.Add($"{KoxoNamespace.CustomerReferencePrefixVariable} ne peut commencer ni par {production.CustomerReferencePrefix} ni par {KoxoDirectoryTopology.DemoGroupPrefix}");
        }

        foreach (var (variable, value) in new[]
        {
            (KoxoNamespace.PrimaryGroupClientsVariable, candidate.PrimaryGroupClients),
            (KoxoNamespace.PrimaryGroupDemoVariable, candidate.PrimaryGroupDemo)
        })
        {
            if (string.Equals(value, production.PrimaryGroupClients, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, production.PrimaryGroupDemo, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"namespace partiel : {variable} designe un profil KoXo de production");
            }
        }

        return errors;
    }

    private static string? Read(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
