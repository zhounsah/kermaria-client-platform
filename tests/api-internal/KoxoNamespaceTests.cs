using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Services;
using Microsoft.Extensions.Configuration;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Namespace KoXo : la production reste au bit pres, et un namespace DEV ne
/// peut produire aucune valeur de l'espace de production.
/// </summary>
public static class KoxoNamespaceTests
{
    private static readonly Regex ProductionReference = new(
        "^CLI-[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex DevReference = new(
        "^DEV-CLI-[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$",
        RegexOptions.CultureInvariant);

    public static void Run()
    {
        VerifyProductionDefault();
        var dev = VerifyDevNamespace();
        VerifyNamespacesAreDisjoint(dev);
        VerifyCustomerReferences(dev);
        VerifyInstanceScope(dev);
        VerifyResolverRefusals();
        VerifyInitializationIsFinal(dev);
    }

    internal static KoxoNamespace DevNamespace()
        => KoxoNamespaceResolver.Resolve(Configuration(new()
        {
            [KoxoNamespace.IdentifierPrefixVariable] = "CLI-D",
            [KoxoNamespace.CustomerReferencePrefixVariable] = "DEV-CLI-",
            [KoxoNamespace.PrimaryGroupClientsVariable] = "CLIENTS DEV",
            [KoxoNamespace.PrimaryGroupDemoVariable] = "CLIENTS DEV DEMO"
        }));

    private static void VerifyProductionDefault()
    {
        var production = KoxoNamespace.Production;
        Ensure(KoxoNamespaceResolver.Resolve(Configuration(new())) == production,
            "Sans configuration, le namespace doit etre celui de la production.");
        Ensure(KoxoNamespaceResolver.Resolve(Configuration(new()
            {
                [KoxoNamespace.IdentifierPrefixVariable] = "CLI-",
                [KoxoNamespace.CustomerReferencePrefixVariable] = "CLI-",
                [KoxoNamespace.PrimaryGroupClientsVariable] = "CLIENTS",
                [KoxoNamespace.PrimaryGroupDemoVariable] = "CLIENTS DÉMO"
            })) == production,
            "Les valeurs de production explicites doivent redonner exactement la production.");
        Ensure(KoxoNamespace.Current == production,
            "Le processus de test non initialise doit rester en production.");

        Ensure(production.FormatIdentifier(1) == "CLI-000001"
            && production.FormatIdentifier(2) == "CLI-000002"
            && production.FormatIdentifier(42) == "CLI-000042"
            && production.FormatIdentifier(999999) == "CLI-999999",
            "Les identifiants de production doivent rester CLI-000001, CLI-000002...");
        Ensure(KoxoDirectoryTopology.IsValidUniqueIdentifier("CLI-000001")
            && !KoxoDirectoryTopology.IsValidUniqueIdentifier("CLI-00001")
            && !KoxoDirectoryTopology.IsValidUniqueIdentifier("CLI-0000001")
            && !KoxoDirectoryTopology.IsValidUniqueIdentifier("CLI-D000001")
            && !KoxoDirectoryTopology.IsValidUniqueIdentifier("CLI-00000A")
            && !KoxoDirectoryTopology.IsValidUniqueIdentifier(" CLI-000001")
            && !KoxoDirectoryTopology.IsValidUniqueIdentifier(null),
            "La validation de production doit rester ^CLI-\\d{6}$.");
        Ensure(KoxoDirectoryTopology.ResolvePrimaryGroup(false) == "CLIENTS"
            && KoxoDirectoryTopology.ResolvePrimaryGroup(true) == "CLIENTS DÉMO",
            "Les groupes primaires de production doivent rester CLIENTS et CLIENTS DÉMO.");
        for (var index = 0; index < 200; index++)
        {
            Ensure(ProductionReference.IsMatch(CustomerReferenceGenerator.Generate()),
                "Les references client de production doivent rester CLI-XXXXXX.");
        }
    }

    private static KoxoNamespace VerifyDevNamespace()
    {
        var dev = DevNamespace();
        Ensure(!dev.IsProduction
            && dev.FormatIdentifier(1) == "CLI-D000001"
            && dev.FormatIdentifier(2) == "CLI-D000002"
            && dev.IsValidUniqueIdentifier("CLI-D000001")
            && !dev.IsValidUniqueIdentifier("CLI-000001")
            && !dev.IsValidUniqueIdentifier("CLI-D00001")
            && !dev.IsValidUniqueIdentifier("CLI-X000001")
            && !dev.IsValidUniqueIdentifier("cli-d000001")
            && dev.ResolvePrimaryGroup(false) == "CLIENTS DEV"
            && dev.ResolvePrimaryGroup(true) == "CLIENTS DEV DEMO",
            "Le namespace DEV doit produire CLI-D000001 et viser le profil CLIENTS DEV.");
        Ensure(dev.FormatIdentifier(999999).Length <= KoxoNamespace.IdentifierMaxLength,
            "Un identifiant DEV doit tenir dans portal_users.koxo_unique_identifier (32).");
        return dev;
    }

    private static void VerifyNamespacesAreDisjoint(KoxoNamespace dev)
    {
        var production = KoxoNamespace.Production;
        // Tout l'espace des six chiffres : aucune valeur DEV n'est une valeur
        // PROD, et chaque namespace refuse les identifiants de l'autre.
        for (var sequence = 0; sequence <= 999999; sequence++)
        {
            var devIdentifier = dev.FormatIdentifier(sequence);
            var productionIdentifier = production.FormatIdentifier(sequence);
            if (devIdentifier == productionIdentifier
                || production.IsValidUniqueIdentifier(devIdentifier)
                || dev.IsValidUniqueIdentifier(productionIdentifier)
                || !dev.IsValidUniqueIdentifier(devIdentifier)
                || !production.IsValidUniqueIdentifier(productionIdentifier))
            {
                throw new InvalidOperationException(
                    $"Collision de namespace KoXo au rang {sequence}.");
            }
        }
    }

    private static void VerifyCustomerReferences(KoxoNamespace dev)
    {
        var production = KoxoNamespace.Production;
        for (var index = 0; index < 200; index++)
        {
            var reference = CustomerReferenceGenerator.Generate(dev);
            Ensure(DevReference.IsMatch(reference)
                && !ProductionReference.IsMatch(reference)
                && !reference.StartsWith(production.CustomerReferencePrefix, StringComparison.Ordinal)
                && !reference.StartsWith(KoxoDirectoryTopology.DemoGroupPrefix, StringComparison.Ordinal)
                && reference.Length <= KoxoNamespace.CustomerReferenceMaxLength,
                "Une reference DEV doit etre DEV-CLI-XXXXXX, hors de l'espace de production.");

            // Les OU KoXo derivees : OU client et OU d'essai.
            var clientOu = KoxoDirectoryTopology.ResolveSecondaryGroup(false, null, reference);
            var demoOu = KoxoDirectoryTopology.ResolveSecondaryGroup(true, reference, reference);
            Ensure(clientOu == reference
                && demoOu == "DEMO-" + reference
                && !ProductionReference.IsMatch(clientOu)
                && !demoOu.StartsWith("DEMO-CLI-", StringComparison.Ordinal),
                "Les OU DEV ne peuvent pas porter le nom d'une OU de production.");
        }
    }

    private static void VerifyInstanceScope(KoxoNamespace dev)
    {
        using (KoxoNamespace.BeginTestScope(dev))
        {
            Ensure(KoxoNamespace.Current == dev
                && KoxoDirectoryTopology.IsValidUniqueIdentifier("CLI-D000001")
                && !KoxoDirectoryTopology.IsValidUniqueIdentifier("CLI-000001")
                && KoxoDirectoryTopology.ResolvePrimaryGroup(false) == "CLIENTS DEV"
                && DevReference.IsMatch(CustomerReferenceGenerator.Generate()),
                "Les points statiques doivent suivre le namespace de l'instance.");
        }

        Ensure(KoxoNamespace.Current == KoxoNamespace.Production,
            "La portee de test doit rendre le namespace precedent.");
    }

    private static void VerifyResolverRefusals()
    {
        void EnsureRefused(string fragment, Dictionary<string, string?> values)
        {
            try
            {
                KoxoNamespaceResolver.Resolve(Configuration(values));
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains(fragment, StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException($"Le namespace aurait du etre refuse : {fragment}.");
        }

        Dictionary<string, string?> Dev(string key, string value)
        {
            var values = new Dictionary<string, string?>
            {
                [KoxoNamespace.IdentifierPrefixVariable] = "CLI-D",
                [KoxoNamespace.CustomerReferencePrefixVariable] = "DEV-CLI-",
                [KoxoNamespace.PrimaryGroupClientsVariable] = "CLIENTS DEV",
                [KoxoNamespace.PrimaryGroupDemoVariable] = "CLIENTS DEV DEMO"
            };
            values[key] = value;
            return values;
        }

        // Namespace partiel : une seule cle hors production.
        EnsureRefused("namespace partiel", new()
        {
            [KoxoNamespace.IdentifierPrefixVariable] = "CLI-D"
        });
        EnsureRefused("namespace partiel", Dev(KoxoNamespace.PrimaryGroupClientsVariable, "clients"));
        EnsureRefused("namespace partiel", Dev(KoxoNamespace.PrimaryGroupDemoVariable, "CLIENTS DÉMO"));
        EnsureRefused("namespace partiel", Dev(KoxoNamespace.CustomerReferencePrefixVariable, "CLI-"));
        EnsureRefused("ne peut commencer", Dev(KoxoNamespace.CustomerReferencePrefixVariable, "CLI-DEV-"));
        EnsureRefused("ne peut commencer", Dev(KoxoNamespace.CustomerReferencePrefixVariable, "DEMO-CLI-"));
        EnsureRefused(KoxoNamespace.IdentifierPrefixVariable, Dev(KoxoNamespace.IdentifierPrefixVariable, "CLI-1"));
        EnsureRefused(KoxoNamespace.IdentifierPrefixVariable, Dev(KoxoNamespace.IdentifierPrefixVariable, "cli-d"));
        EnsureRefused(KoxoNamespace.CustomerReferencePrefixVariable, Dev(KoxoNamespace.CustomerReferencePrefixVariable, "DEV-CLI"));
        EnsureRefused("doivent differer", Dev(KoxoNamespace.PrimaryGroupDemoVariable, "CLIENTS DEV"));
        EnsureRefused(KoxoNamespace.PrimaryGroupClientsVariable, Dev(KoxoNamespace.PrimaryGroupClientsVariable, "CLIENTS;DEV"));
    }

    private static void VerifyInitializationIsFinal(KoxoNamespace dev)
    {
        // Le processus de test tourne en production : la fixer explicitement ne
        // change rien, et une seconde valeur doit etre refusee.
        KoxoNamespace.Initialize(KoxoNamespace.Production);
        KoxoNamespace.Initialize(KoxoNamespace.Production);
        try
        {
            KoxoNamespace.Initialize(dev);
        }
        catch (InvalidOperationException)
        {
            Ensure(KoxoNamespace.Current == KoxoNamespace.Production,
                "Un namespace refuse ne doit rien changer.");
            return;
        }

        throw new InvalidOperationException(
            "Un second namespace dans le meme processus doit etre refuse.");
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
