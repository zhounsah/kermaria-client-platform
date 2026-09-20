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
        CatalogScopeIsNormalizedBeforeCartPersistence();
        CustomerReadinessProjectionIsActionable();
        ConfigurationReadinessDistinguishesVpsShapes();
        ExplicitFeeDeduplicationOnly();
        FingerprintIsStableAndSensitive();
        CartItemCurrentRolesAreCompositionDerived();
        QuoteStatusIsServerDerived();
        PresetDefinitionRetainsSelectionAndQuantityPolicy();
        RequiredPresetItemsAreInjectedIntoFormulaComposition();
        DirectCartMergeIsDeterministic();
        CartMutationResultHttpMappingIsCentralized();
        CheckoutRecoveryIsReadOnlyAndStateful();
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

    private static void CustomerReadinessProjectionIsActionable()
    {
        var commitment = new BillingV2CartIssue(
            "CART_COMMITMENT_REQUIRED", "error", null, null,
            "Un engagement explicite est requis pour le récurrent.", true);
        var personalStorageScope = BillingV2CartPolicy.EvaluateScopes([
            new("base", "BASE-SERVICE", "subscription", "subscription", null),
            new("storage", "STORAGE-PERSONAL", "primary_user", "user", null)
        ]);
        var deferredBinding = personalStorageScope.Issues.Single(issue =>
            issue.Code == "CART_SUBJECT_BINDING_DEFERRED"
            && issue.CartItemId == "storage"
            && issue.ServiceCode == "STORAGE-PERSONAL");
        var publicIssuesBeforeCommitment = personalStorageScope.Issues
            .Append(commitment)
            .Select(BillingV2CartPolicy.ProjectCustomerIssue)
            .Where(issue => issue.Blocking && issue.CustomerMessage is not null)
            .ToArray();

        Ensure(personalStorageScope.CommercialReady,
            "Un stockage personnel sans binding reste commercialement valide lorsque ce binding est différable.");
        Ensure(!deferredBinding.Blocking
            && BillingV2CartPolicy.ProjectCustomerIssue(deferredBinding).CustomerMessage is null,
            "Un binding différé ne produit pas de faux avertissement dans le panier.");
        Ensure(publicIssuesBeforeCommitment.Length == 1
            && publicIssuesBeforeCommitment[0].Code == "CART_COMMITMENT_REQUIRED",
            "BASE-SERVICE et un stockage personnel 16 Go sans binding ne laissent visible que le choix d'engagement.");
        var commitmentProjection = BillingV2CartPolicy.ProjectCustomerIssue(commitment);
        Ensure(commitmentProjection.Blocking
            && commitmentProjection.CustomerMessage == "Choisissez une durée d’engagement avant de poursuivre.",
            "Un vrai blocker expose une action client précise sans code interne.");
        Ensure(personalStorageScope.Issues
            .Select(BillingV2CartPolicy.ProjectCustomerIssue)
            .All(issue => !issue.Blocking || issue.CustomerMessage is null),
            "Après sélection d'un engagement valide, le binding différé ne laisse aucun blocker public.");

        var invalidScope = BillingV2CartPolicy.ProjectCustomerIssue(new BillingV2CartIssue(
            "CART_SCOPE_INVALID", "error", "storage", "STORAGE-PERSONAL",
            "Le scope ne correspond pas au service catalogue.", true));
        Ensure(invalidScope.CustomerMessage == "La portée d’un service ne correspond plus à cette configuration. Retirez-le puis choisissez une configuration compatible.",
            "Une incohérence réelle de scope reste bloquante mais ne retombe plus sur un message générique.");
    }

    private static void CatalogScopeIsNormalizedBeforeCartPersistence()
    {
        Ensure(BillingV2CatalogScopeTemplatePolicy.TryMapToCartTemplate("subscription",
                out var subscription)
            && subscription == BillingV2CartScopeTemplates.Subscription,
            "Le scope catalogue subscription conserve son scope Cart canonique.");
        Ensure(BillingV2CatalogScopeTemplatePolicy.TryMapToCartTemplate("user",
                out var primaryUser)
            && primaryUser == BillingV2CartScopeTemplates.PrimaryUser,
            "Le scope catalogue user devient primary_user avant toute ecriture Cart.");
        Ensure(BillingV2CatalogScopeTemplatePolicy.TryMapToCartTemplate("additional_user",
                out var additionalUser)
            && additionalUser == BillingV2CartScopeTemplates.AdditionalUser,
            "Les scopes catalogue deja canoniques restent traduisibles sans perte.");
        Ensure(!BillingV2CatalogScopeTemplatePolicy.TryMapToCartTemplate("unknown_scope",
                out _),
            "Un scope catalogue non deterministe est refuse avant INSERT Cart.");

        var directPersonalStorage = BillingV2CartPolicy.EvaluateScopes([
            new("storage", "STORAGE-PERSONAL", primaryUser, "user", null)
        ]);
        Ensure(directPersonalStorage.CommercialReady
            && directPersonalStorage.Issues.Count(issue => issue.Code == "CART_SCOPE_INVALID") == 0
            && directPersonalStorage.Issues.Single(issue => issue.Code == "CART_SUBJECT_BINDING_DEFERRED").Blocking == false,
            "Un ajout direct user normalise en primary_user conserve un binding differe non bloquant.");
    }

    private static void FingerprintIsStableAndSensitive()
    {
        var now = DateTime.UnixEpoch;
        var cart = new BillingV2Cart("cart", null, "hash", "open", "EUR", null,
            null, "monthly", null, null, 3, now, now, now, now.AddDays(30), null,
        [
            new("item", "cart", "service", "SERVICE", null, null, 1, "subscription",
                null, null, null, null, BillingV2CartItemOrigins.Direct, false,
                false, false, true, true, true, true, null, 1, 10000,
                null, null, 1, now, now)
        ]);
        var line = Line("item", "PRICE", null);
        var first = BillingV2CartPolicy.CompositionFingerprint(cart, [line]);
        var second = BillingV2CartPolicy.CompositionFingerprint(cart, [line]);
        var changed = BillingV2CartPolicy.CompositionFingerprint(cart with { Version = 4 }, [line]);
        Ensure(first == second && first != changed,
            "L'empreinte est stable et liee a la version de composition.");

        var sameEconomicCompositionDifferentOrigin = cart with
        {
            Items = [CartItem("item", "service", null, "subscription",
                BillingV2CartItemOrigins.Preset)]
        };
        Ensure(first == BillingV2CartPolicy.CompositionFingerprint(
                sameEconomicCompositionDifferentOrigin, [line]),
            "La provenance historique ne participe pas au pricing ni a l'empreinte d'etat du meme Cart.");
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

    private static void PresetDefinitionRetainsSelectionAndQuantityPolicy()
    {
        var option = new BillingV2CartPresetDefinitionItem(
            "preset-item", "USER-ADDITIONAL", null, "additional_user", 1,
            RequiredItem: false, CustomerEditable: true, SelectedByDefault: false,
            MinimumQuantity: 1, MaximumQuantity: 10, DisplayOrder: 80);
        Ensure(!option.RequiredItem && !option.SelectedByDefault
            && option.DefaultQuantity == 1 && option.MinimumQuantity == 1
            && option.MaximumQuantity == 10,
            "Une option de quantite preserve son absence initiale et ses bornes autoritaires.");
    }

    private static void RequiredPresetItemsAreInjectedIntoFormulaComposition()
    {
        var definitions = new[]
        {
            PresetItem("base", "BASE-SERVICE", null, "subscription", 1,
                required: true, editable: false, selectedByDefault: true, displayOrder: 10),
            PresetItem("storage-128", "STORAGE-PERSONAL", "128", "primary_user", 1,
                required: false, editable: true, selectedByDefault: false, displayOrder: 20),
            PresetItem("backup-128", "BACKUP-PERSONAL", "128", "primary_user", 1,
                required: false, editable: true, selectedByDefault: false, displayOrder: 30),
            PresetItem("vpn-plus", "VPN-ACCESS", "PLUS", "primary_user", 1,
                required: false, editable: true, selectedByDefault: false, displayOrder: 40),
            PresetItem("users", "USER-ADDITIONAL", null, "additional_user", 1,
                required: false, editable: true, selectedByDefault: false, minimum: 1, maximum: 10,
                displayOrder: 50),
            PresetItem("support", "SUPPORT-PLUS", null, "subscription", 1,
                required: false, editable: true, selectedByDefault: false, displayOrder: 60),
            PresetItem("rds", "RDS-ACCESS", null, "primary_user", 1,
                required: false, editable: true, selectedByDefault: false, displayOrder: 70)
        };
        // BASE-SERVICE n'est volontairement pas une intention navigateur.
        // C'est exactement le cas formule qui avait produit un Cart incomplet.
        var selected = new[]
        {
            new BillingV2PublicSelectionComponent("STORAGE-PERSONAL", "128", 1),
            new BillingV2PublicSelectionComponent("BACKUP-PERSONAL", "128", 1),
            new BillingV2PublicSelectionComponent("VPN-ACCESS", "PLUS", 1),
            new BillingV2PublicSelectionComponent("USER-ADDITIONAL", null, 2),
            new BillingV2PublicSelectionComponent("SUPPORT-PLUS", null, 1)
        };

        var composition = BillingV2CartPolicy.ResolvePresetComposition(definitions, selected);
        Ensure(composition.IsValid
            && composition.Items.Count(item => item.ServiceCode == "BASE-SERVICE") == 1
            && composition.Items.Any(item => item.ServiceCode == "USER-ADDITIONAL" && item.Quantity == 2)
            && composition.Items.Any(item => item.ServiceCode == "STORAGE-PERSONAL" && item.TierCode == "128")
            && composition.Items.Any(item => item.ServiceCode == "BACKUP-PERSONAL" && item.TierCode == "128")
            && !composition.Items.Any(item => item.ServiceCode == "RDS-ACCESS"),
            "La composition formule injecte exactement le socle required, preserve les choix et laisse les options OFF absentes.");

        var retry = BillingV2CartPolicy.ResolvePresetComposition(definitions, selected);
        Ensure(retry.IsValid && retry.Items.SequenceEqual(composition.Items)
            && retry.Items.Count(item => item.ServiceCode == "BASE-SERVICE") == 1,
            "Le meme import formule produit une composition stable sans second socle.");
    }

    private static void CartMutationResultHttpMappingIsCentralized()
    {
        Ensure(BillingV2CartMutationResults.HttpStatusCode(new("CART_PRESET_INITIALIZED")) == 200
            && BillingV2CartMutationResults.HttpStatusCode(new("CART_OK")) == 200
            && BillingV2CartMutationResults.HttpStatusCode(new("CART_QUOTED")) == 200
            && BillingV2CartMutationResults.HttpStatusCode(new("CART_FORMULA_SELECTION_IMPORTED")) == 200
            && BillingV2CartMutationResults.HttpStatusCode(new("CART_ITEM_ALREADY_PRESENT")) == 200,
            "Les initialisations, reprises idempotentes et quotes Cart sont des succes HTTP.");
        Ensure(BillingV2CartMutationResults.HttpStatusCode(new("CART_PRESET_CONFLICT")) == 409
            && BillingV2CartMutationResults.HttpStatusCode(new("CART_VERSION_CONFLICT")) == 409
            && BillingV2CartMutationResults.HttpStatusCode(new("CART_MERGE_REQUIRES_REVIEW")) == 409
            && BillingV2CartMutationResults.HttpStatusCode(new("CART_ITEM_TIER_CONFLICT")) == 409
            && BillingV2CartMutationResults.HttpStatusCode(new("CART_STRUCTURAL_ITEM_REQUIRED")) == 409,
            "Les conflits Cart ont une classification HTTP unique.");
        Ensure(BillingV2CartMutationResults.HttpStatusCode(new("CART_NOT_FOUND")) == 404
            && BillingV2CartMutationResults.HttpStatusCode(new("CART_PRESET_INVALID")) == 400,
            "Les not-found et erreurs de validation restent distingues au niveau endpoint.");
    }

    private static void CheckoutRecoveryIsReadOnlyAndStateful()
    {
        Ensure(!BillingV2CartCheckoutRecoveryPolicy.RequiresExplicitCartReference(0)
            && !BillingV2CartCheckoutRecoveryPolicy.RequiresExplicitCartReference(1)
            && BillingV2CartCheckoutRecoveryPolicy.RequiresExplicitCartReference(2),
            "Sans cartId, la reprise doit refuser deux checkouts possibles au lieu de choisir le plus recent.");

        var queued = BillingV2CartCheckoutRecoveryPolicy.Resolve(
            "pending_approval", "pending", null, null, false);
        Ensure(queued.Status == "pending_provider" && queued.Retryable,
            "Un checkout commit mais sans session provider est repris comme preparation asynchrone.");

        var approval = BillingV2CartCheckoutRecoveryPolicy.Resolve(
            "pending_approval", "processed", "pending_approval", null, true);
        Ensure(approval.Status == "approval_required" && approval.ExposeApprovalUrl,
            "Une URL d'approbation arrivee apres le commit est reprise sans recreer de session.");

        var pending = BillingV2CartCheckoutRecoveryPolicy.Resolve(
            "pending_approval", "processed", "approved", "pending", false);
        Ensure(pending.Status == "payment_pending" && !pending.Retryable,
            "Le paiement pending conserve la meme souscription pendant les refresh.");

        var confirmed = BillingV2CartCheckoutRecoveryPolicy.Resolve(
            "active", "processed", "completed", "succeeded", false);
        Ensure(confirmed.Status == "confirmed" && !confirmed.ExposeApprovalUrl,
            "La confirmation provider devient un etat de reprise stable.");

        var failed = BillingV2CartCheckoutRecoveryPolicy.Resolve(
            "pending_approval", "failed", null, null, false);
        var failedOnRefresh = BillingV2CartCheckoutRecoveryPolicy.Resolve(
            "pending_approval", "failed", null, null, false);
        Ensure(failed.Status == "failed" && failed.Retryable
            && failed == failedOnRefresh,
            "Un echec provider reste observable et strictement stable au refresh sans creer une seconde souscription.");
    }

    private static void DirectCartMergeIsDeterministic()
    {
        var storage64 = CartItem("storage-64", "STORAGE", "64", "primary_user");
        var baseService = CartItem("base", "BASE", null, "subscription",
            origin: BillingV2CartItemOrigins.Structural, structural: true);
        var directAdd = BillingV2CartPolicy.ResolveDirectAddition(
            [storage64, baseService], "VPN", "PLUS", "primary_user", null);
        var sameConfiguration = BillingV2CartPolicy.ResolveDirectAddition(
            [storage64], "STORAGE", "64", "primary_user", null);
        var otherTier = BillingV2CartPolicy.ResolveDirectAddition(
            [storage64], "STORAGE", "128", "primary_user", null);
        Ensure(directAdd == BillingV2CartDirectAddResolution.Add
            && sameConfiguration == BillingV2CartDirectAddResolution.AlreadyPresent
            && otherTier == BillingV2CartDirectAddResolution.TierConflict,
            "La fusion directe ajoute seulement une nouvelle configuration, reste idempotente a palier identique et refuse un palier concurrent.");
    }

    private static void CartItemCurrentRolesAreCompositionDerived()
    {
        var releasedDependency = BillingV2CartPolicy.ResolveCurrentItemRole(
            BillingV2CartItemOrigins.Dependency, false, false, false, false, false);
        Ensure(releasedDependency.CanRemove && !releasedDependency.CanEdit
            && !releasedDependency.CountsAsCommercialSelection,
            "Une ancienne dependance liberee devient retirable sans etre comptee comme selection explicite.");

        var directRequiredByPreset = BillingV2CartPolicy.ResolveCurrentItemRole(
            BillingV2CartItemOrigins.Direct, false, true, false, true, true);
        Ensure(directRequiredByPreset.IsRequiredByPreset
            && !directRequiredByPreset.CanRemove
            && directRequiredByPreset.IsExplicitCommercialSelection,
            "Un item direct qui satisfait un required preset garde sa provenance et devient protege par son role courant.");

        var presetSatisfyingDependency = BillingV2CartPolicy.ResolveCurrentItemRole(
            BillingV2CartItemOrigins.Preset, false, false, true, true, true);
        Ensure(presetSatisfyingDependency.IsRequiredByDependency
            && presetSatisfyingDependency.CountsAsCommercialSelection,
            "Une selection de preset explicite reste commerciale meme lorsqu'elle satisfait une dependance.");

        var structuralHistoricallyDirect = BillingV2CartPolicy.ResolveCurrentItemRole(
            BillingV2CartItemOrigins.Direct, true, false, false, false, true);
        Ensure(!structuralHistoricallyDirect.CanRemove
            && !structuralHistoricallyDirect.CountsAsCommercialSelection
            && structuralHistoricallyDirect.DisplayReason == "included",
            "Le socle courant prevaut sur la provenance directe pour les droits et le compteur.");
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
        => new(item, "SERVICE", null, "Service", null, price, price, BillingV2BillingCadences.OneTime,
            690, 1, 690, false, key);

    private static BillingV2CartItem CartItem(string id, string serviceId, string? tierId,
        string scope, string origin = BillingV2CartItemOrigins.Direct, bool structural = false)
        => new(id, "cart", serviceId, serviceId, tierId, tierId, 1, scope, null,
            null, null, origin == BillingV2CartItemOrigins.Direct, origin, structural,
            false, false, origin is BillingV2CartItemOrigins.Direct or BillingV2CartItemOrigins.Preset,
            !structural, !structural, !structural, null, 1, 10000, null, null, 1,
            DateTime.UnixEpoch, DateTime.UnixEpoch);

    private static BillingV2CartPresetCompositionItem PresetItem(string id,
        string serviceCode, string? tierCode, string scope, int quantity,
        bool required, bool editable, bool selectedByDefault, int minimum = 1,
        int maximum = 1, int displayOrder = 1)
        => new(id, $"service:{serviceCode}", serviceCode,
            tierCode is null ? null : $"tier:{serviceCode}:{tierCode}", tierCode,
            scope, quantity, required, editable, selectedByDefault, minimum, maximum,
            displayOrder);

    private static BillingV2CartDependencyResolution Resolve(string root,
        IReadOnlyCollection<BillingV2CartDependencyRule> rules)
        => BillingV2CartPolicy.ResolveDependencies(
            [new(root, "TIER", "primary_user", "current_customer", 1)], rules);

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
