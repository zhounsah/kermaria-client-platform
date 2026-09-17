using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Tests sans I/O du noyau panier. Les scenarios SQL (unicite, concurrence,
/// rollback et ownership) restent opt-in MariaDB : ils ne sont jamais pointes
/// vers une base de recette ou production par les smoke tests.
/// </summary>
public static class BillingV2CartPolicyTests
{
    public static Task RunAsync()
    {
        AnonymousOwnerIsHashed();
        CurrentCartExpiryPolicyIsExplicit();
        RecursiveDependenciesReachFixedPoint();
        DependencyCyclesAndAmbiguitiesAreBlocked();
        DependencyConvergenceDoesNotDuplicateItems();
        ScopeAndBindingReadinessIsAuthoritative();
        ConfigurationReadinessDistinguishesVpsShapes();
        ExplicitFeeDeduplicationOnly();
        FingerprintIsStableAndSensitive();
        QuoteStatusIsServerDerived();
        CartModelNeverContainsFinancialAuthority();
        CartModuleHasNoFinancialOrProviderDependency();
        return Task.CompletedTask;
    }

    private static void AnonymousOwnerIsHashed()
    {
        var owner = new BillingV2CartOwner(null, "opaque-browser-secret");
        Ensure(owner.IsValid && owner.AnonymousTokenHash is { Length: 64 }
            && owner.AnonymousTokenHash != "opaque-browser-secret",
            "Le token anonyme n'est jamais la valeur persistee.");
    }

    private static void CurrentCartExpiryPolicyIsExplicit()
    {
        var now = DateTime.UtcNow;
        Ensure(BillingV2CartPolicy.IsOpenCartLogicallyExpired(
                BillingV2CartStatuses.Open, now.AddTicks(-1), now)
            && !BillingV2CartPolicy.IsOpenCartLogicallyExpired(
                BillingV2CartStatuses.Open, now.AddTicks(1), now)
            && !BillingV2CartPolicy.IsOpenCartLogicallyExpired(
                BillingV2CartStatuses.Expired, now.AddTicks(-1), now),
            "Un Cart open expire est ecarte avant la recherche du Cart courant.");
    }

    private static void ConfigurationReadinessDistinguishesVpsShapes()
    {
        var vpsOnly = BillingV2CartPolicy.EvaluateConfiguration(
        [
            new("vps", "VPS-LOCAL", BillingV2CartConfigurationPolicies.DeferrableInMixedCart,
                null, true)
        ]);
        Ensure(vpsOnly.Commercial == BillingV2CartCommercialReadiness.Blocked
            && vpsOnly.Configuration == BillingV2CartConfigurationReadiness.Blocked,
            "Un service configurable seul reste bloque sans configuration.");

        var mixedDeferred = BillingV2CartPolicy.EvaluateConfiguration(
        [
            new("vps", "VPS-LOCAL", BillingV2CartConfigurationPolicies.DeferrableInMixedCart,
                null, true),
            new("storage", "STORAGE-PERSONAL", BillingV2CartConfigurationPolicies.NotRequired,
                null, true)
        ]);
        Ensure(mixedDeferred.Commercial == BillingV2CartCommercialReadiness.Ready
            && mixedDeferred.Configuration == BillingV2CartConfigurationReadiness.Deferred,
            "Une configuration differee est autorisee seulement dans un panier mixte a prix fixe.");

        var priceAffecting = BillingV2CartPolicy.EvaluateConfiguration(
        [
            new("vps", "VPS-LOCAL", BillingV2CartConfigurationPolicies.DeferrableInMixedCart,
                null, false),
            new("storage", "STORAGE-PERSONAL", BillingV2CartConfigurationPolicies.NotRequired,
                null, true)
        ]);
        Ensure(priceAffecting.Commercial == BillingV2CartCommercialReadiness.Blocked
            && priceAffecting.Issues.Any(issue => issue.Code == "CART_CONFIGURATION_AFFECTS_PRICE"),
            "Une information pouvant modifier le prix interdit le checkout futur.");
    }

    private static void ExplicitFeeDeduplicationOnly()
    {
        var sameKey = Line("setup-a", "SETUP-A", "installation");
        var duplicate = Line("setup-b", "SETUP-B", "installation");
        var noKeyA = Line("setup-c", "SETUP-C", null);
        var noKeyB = Line("setup-d", "SETUP-D", null);
        var retained = BillingV2CartPolicy.DeduplicateExplicitFees(
            [sameKey, duplicate, noKeyA, noKeyB], "EUR");
        Ensure(retained.Count == 3
            && retained.Any(line => line.ServicePriceId == "SETUP-A")
            && retained.Count(line => line.FeeDeduplicationKey is null) == 2,
            "Aucun dedoublonnage implicite ne depend du montant ou du libelle.");

        var recurringSameKey = sameKey with { BillingCadence = BillingV2BillingCadences.Monthly };
        Ensure(BillingV2CartPolicy.DeduplicateExplicitFees([recurringSameKey, recurringSameKey with { ServicePriceId = "MONTHLY-B" }], "EUR").Count == 2,
            "Une cle de frais ne dedoublonne jamais du recurrent.");
    }

    private static void RecursiveDependenciesReachFixedPoint()
    {
        var result = Resolve("A", [
            new("A", "B"), new("B", "C"), new("C", "D")
        ]);
        Ensure(result.Nodes.Select(node => node.ServiceCode).SequenceEqual(["A", "B", "C", "D"])
            && result.Issues.Count == 0,
            "A vers B vers C vers D atteint le point fixe deterministe.");
        var second = BillingV2CartPolicy.ResolveDependencies(result.Nodes,
            [new("A", "B"), new("B", "C"), new("C", "D")]);
        Ensure(second.Nodes.Count == result.Nodes.Count && second.Issues.Count == 0,
            "Une seconde resolution est stable et n'ajoute pas de doublon.");
    }

    private static void DependencyCyclesAndAmbiguitiesAreBlocked()
    {
        var cycle = Resolve("A", [new("A", "B"), new("B", "A")]);
        Ensure(cycle.Issues.Any(issue => issue.Code == "CART_DEPENDENCY_CYCLE"),
            "Le cycle A vers B vers A est explicite.");
        var ambiguous = Resolve("A", [new("A", "B"), new("B", "C", BillingV2CartDependencyResolutionKind.Ambiguous)]);
        Ensure(ambiguous.Issues.Any(issue => issue.Code == "CART_DEPENDENCY_AMBIGUOUS"),
            "Une ambiguite de niveau deux reste structuree.");
        var impossible = Resolve("A", [new("A", "B"), new("B", "C", BillingV2CartDependencyResolutionKind.Impossible)]);
        Ensure(impossible.Issues.Any(issue => issue.Code == "CART_DEPENDENCY_IMPOSSIBLE"),
            "Une dependance impossible de niveau deux bloque la composition.");
    }

    private static void DependencyConvergenceDoesNotDuplicateItems()
    {
        var result = Resolve("A", [new("A", "B"), new("A", "C"), new("B", "D"), new("C", "D")]);
        Ensure(result.Nodes.Count(node => node.ServiceCode == "D") == 1 && result.Nodes.Count == 4,
            "Deux branches convergentes ajoutent un unique item D.");

        var coherent = BillingV2CartPolicy.ResolveDependencies([
            new("A", "TIER", "primary_user", "current_customer", 1),
            new("B", "TIER", "primary_user", "current_customer", 1)
        ], [new("A", "B")]);
        Ensure(coherent.Nodes.Count(node => node.ServiceCode == "B") == 1,
            "Une dependance same_scope avec le meme binding est deja satisfaite.");

        var incompatible = BillingV2CartPolicy.ResolveDependencies([
            new("A", "TIER", "primary_user", "current_customer", 1),
            new("B", "TIER", "primary_user", "other_customer", 1)
        ], [new("A", "B")]);
        Ensure(incompatible.Nodes.Count(node => node.ServiceCode == "B") == 2,
            "Un binding incompatible ne satisfait jamais une dependance same_scope.");
    }

    private static void ScopeAndBindingReadinessIsAuthoritative()
    {
        var valid = BillingV2CartPolicy.EvaluateScopes([
            new("subscription", "BASE", "subscription", "subscription", null),
            new("primary", "STORAGE", "primary_user", "user", "current_customer"),
            new("primary-deferred", "BACKUP", "primary_user", "user", null),
            new("additional", "USER", "additional_user", "user", null)
        ]);
        Ensure(valid.CommercialReady && valid.ProvisioningReadiness == BillingV2CartProvisioningReadiness.Deferred
            && valid.Issues.Any(issue => issue.Code == "CART_SUBJECT_BINDING_DEFERRED"),
            "Un binding differe peut rester commercialement valide.");
        var invalid = BillingV2CartPolicy.EvaluateScopes([
            new("bad-scope", "BASE", "unexpected", "subscription", null),
            new("bad-binding", "BASE", "subscription", "subscription", "user-1"),
            new("required", "USER", "additional_user", "user", null, true)
        ]);
        Ensure(!invalid.CommercialReady
            && invalid.Issues.Any(issue => issue.Code == "CART_SCOPE_INVALID")
            && invalid.Issues.Any(issue => issue.Code == "CART_SUBJECT_BINDING_INVALID"),
            "Les incompatibilites scope/binding bloquent cote serveur.");
        Ensure(invalid.Issues.Any(issue => issue.Code == "CART_SUBJECT_BINDING_REQUIRED"),
            "Le modele peut rendre un binding commercialement obligatoire.");
    }

    private static void FingerprintIsStableAndSensitive()
    {
        var now = DateTime.UnixEpoch;
        var cart = new BillingV2Cart("cart", null, "hash", "open", "EUR", null,
            null, "monthly", null, 3, now, now, now, now.AddDays(30), null,
        [
            new("item", "cart", "service", "SERVICE", null, null, 1, "subscription",
                null, null, null, null, 1, now, now)
        ]);
        var line = Line("item", "PRICE", null);
        var first = BillingV2CartPolicy.CompositionFingerprint(cart, [line]);
        var second = BillingV2CartPolicy.CompositionFingerprint(cart, [line]);
        var changed = BillingV2CartPolicy.CompositionFingerprint(cart with { Version = 4 }, [line]);
        Ensure(first == second && first != changed,
            "L'empreinte est stable et liee a la version de composition.");
    }

    private static void QuoteStatusIsServerDerived()
    {
        var now = DateTime.UtcNow;
        Ensure(BillingV2CartPolicy.ResolveQuoteStatus(2, 2, now.AddMinutes(30), now)
                == BillingV2CartQuoteStatuses.Current
            && BillingV2CartPolicy.ResolveQuoteStatus(3, 2, now.AddMinutes(30), now)
                == BillingV2CartQuoteStatuses.Stale
            && BillingV2CartPolicy.ResolveQuoteStatus(2, 2, now, now)
                == BillingV2CartQuoteStatuses.Expired,
            "Le statut du quote est derive de la version et du TTL serveur.");
    }

    private static void CartModelNeverContainsFinancialAuthority()
    {
        Ensure(typeof(BillingV2CartItem).GetProperties().All(property =>
                !property.Name.Contains("Amount", StringComparison.OrdinalIgnoreCase)
                && !property.Name.Contains("Price", StringComparison.OrdinalIgnoreCase)),
            "Le CartItem ne peut pas porter un prix navigateur.");
        var anonymousHash = typeof(BillingV2Cart).GetProperty(nameof(BillingV2Cart.AnonymousSessionHash));
        Ensure(anonymousHash?.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), true).Any() == true,
            "Le condensat de possession anonyme reste interne et n'est pas projete dans l'API.");
    }

    private static void CartModuleHasNoFinancialOrProviderDependency()
    {
        var forbidden = new[] { "AuthoritativeCheckout", "ProviderCheckout", "BillingEvent", "Provisioning" };
        var dependencies = typeof(BillingV2CartService)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .Concat(typeof(BillingV2CartService).GetFields(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Select(field => field.FieldType.Name));
        Ensure(!dependencies.Any(dependency => forbidden.Any(word =>
                dependency.Contains(word, StringComparison.Ordinal))),
            "Le module Cart ne depend ni du checkout, ni d'un provider, ni des events, ni du provisioning.");
    }

    private static BillingV2CartQuoteLine Line(string item, string price, string? key)
        => new(item, "SERVICE", null, price, price, BillingV2BillingCadences.OneTime,
            690, 1, 690, false, key);

    private static BillingV2CartDependencyResolution Resolve(string root,
        IReadOnlyCollection<BillingV2CartDependencyRule> rules)
        => BillingV2CartPolicy.ResolveDependencies(
            [new(root, "TIER", "primary_user", "current_customer", 1)], rules);

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
