using System.Security.Cryptography;
using System.Text;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Politique pure du panier. Elle ne sait ni creer une souscription, ni creer
/// un evenement de billing, ni appeler un provider. Elle est volontairement
/// testable sans MariaDB afin de verrouiller les cas de configuration differee.
/// </summary>
public static class BillingV2CartPolicy
{
    private static readonly IReadOnlySet<string> ValidScopes = new HashSet<string>(StringComparer.Ordinal)
    {
        "subscription", "primary_user", "additional_user"
    };

    public static bool IsOpenCartLogicallyExpired(string status, DateTime expiresAtUtc,
        DateTime nowUtc)
        => string.Equals(status, BillingV2CartStatuses.Open, StringComparison.Ordinal)
            && expiresAtUtc <= nowUtc;

    public static (string Commercial, string Configuration,
        IReadOnlyList<BillingV2CartIssue> Issues) EvaluateConfiguration(
        IReadOnlyList<BillingV2CartConfigurationCandidate> items)
    {
        var issues = new List<BillingV2CartIssue>();
        var mixed = items.Count > 1;
        var configuration = BillingV2CartConfigurationReadiness.NotRequired;

        foreach (var item in items)
        {
            if (string.Equals(item.ConfigurationPolicy,
                    BillingV2CartConfigurationPolicies.NotRequired,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.ConfigurationReference))
            {
                configuration = BillingV2CartConfigurationReadiness.Complete;
                continue;
            }

            var deferrable = string.Equals(item.ConfigurationPolicy,
                BillingV2CartConfigurationPolicies.DeferrableInMixedCart,
                StringComparison.Ordinal)
                && mixed
                && item.PriceStableWithoutConfiguration;

            if (deferrable)
            {
                configuration = BillingV2CartConfigurationReadiness.Deferred;
                issues.Add(new BillingV2CartIssue(
                    "CART_CONFIGURATION_DEFERRED", "info", item.CartItemId,
                    item.ServiceCode,
                    "La configuration technique est differee sans incidence tarifaire.",
                    false));
                continue;
            }

            configuration = BillingV2CartConfigurationReadiness.Blocked;
            issues.Add(new BillingV2CartIssue(
                item.PriceStableWithoutConfiguration
                    ? "CART_CONFIGURATION_REQUIRED"
                    : "CART_CONFIGURATION_AFFECTS_PRICE",
                "error", item.CartItemId, item.ServiceCode,
                "La configuration requise doit etre complete avant le checkout.",
                true));
        }

        return (issues.Any(issue => issue.Blocking)
                ? BillingV2CartCommercialReadiness.Blocked
                : BillingV2CartCommercialReadiness.Ready,
            configuration,
            issues);
    }

    public static string CompositionFingerprint(
        BillingV2Cart cart,
        IEnumerable<BillingV2CartQuoteLine> lines)
    {
        var canonical = string.Join("|",
            "billing-v2-cart-v1",
            cart.Id,
            cart.Version,
            cart.Currency,
            cart.CommitmentTermId ?? "-",
            cart.PaymentMode ?? "-",
            string.Join(";", cart.Items.OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => string.Join("/", item.Id, item.ServiceId,
                    item.TierId ?? "-", item.Quantity, item.ScopeTemplate,
                    item.SubjectBinding ?? "-", item.ConfigurationKind ?? "-",
                    item.ConfigurationReference ?? "-"))),
            string.Join(";", lines.OrderBy(line => line.ServicePriceId,
                    StringComparer.Ordinal)
                .Select(line => string.Join("/", line.ServicePriceId,
                    line.Quantity, line.FeeDeduplicationKey ?? "-"))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Une cle de dedoublonnage designe une charge ponctuelle unique pour le
    /// contexte <c>devise + initial_subscription + cle</c>. Elle ne peut pas
    /// exprimer une tarification a l'unite : un composant qui doit etre facture
    /// par quantite ne porte pas de cle. Ni montant, ni libelle, ni ordre ne
    /// participent a la decision.
    /// </summary>
    public static IReadOnlyList<BillingV2CartQuoteLine> DeduplicateExplicitFees(
        IEnumerable<BillingV2CartQuoteLine> lines, string currency)
    {
        var result = new List<BillingV2CartQuoteLine>();
        foreach (var line in lines.Where(line =>
                     !string.Equals(line.BillingCadence, BillingV2BillingCadences.OneTime,
                         StringComparison.Ordinal)
                     || string.IsNullOrWhiteSpace(line.FeeDeduplicationKey))
                 .OrderBy(line => line.ServicePriceId, StringComparer.Ordinal))
        {
            result.Add(line);
        }

        var fees = lines.Where(line =>
                string.Equals(line.BillingCadence, BillingV2BillingCadences.OneTime,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(line.FeeDeduplicationKey))
            .GroupBy(line => line.FeeDeduplicationContext
                ?? FeeDeduplicationContext(currency, line.FeeDeduplicationKey!), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        foreach (var group in fees)
        {
            var selected = group.OrderBy(line => line.ServicePriceId, StringComparer.Ordinal).First();
            result.Add(selected with
            {
                Quantity = 1,
                AmountCents = selected.UnitAmountCents,
                FeeDeduplicationContext = group.Key
            });
        }
        return result.OrderBy(line => line.ServicePriceId, StringComparer.Ordinal).ToArray();
    }

    public static string FeeDeduplicationContext(string currency, string key)
        => string.Join("|", currency.ToUpperInvariant(), "initial_subscription", key);

    /// <summary>
    /// Le statut d'un snapshot ne vient jamais du navigateur : la version de
    /// composition et son horodatage sont compares cote serveur au moment ou
    /// une future etape contractuelle voudra le consommer.
    /// </summary>
    public static string ResolveQuoteStatus(int currentCartVersion, int quotedCartVersion,
        DateTime quoteExpiresAtUtc, DateTime nowUtc)
    {
        if (quoteExpiresAtUtc <= nowUtc) return BillingV2CartQuoteStatuses.Expired;
        return currentCartVersion == quotedCartVersion
            ? BillingV2CartQuoteStatuses.Current
            : BillingV2CartQuoteStatuses.Stale;
    }

    public static BillingV2CartScopeEvaluation EvaluateScopes(
        IReadOnlyList<BillingV2CartScopeCandidate> items)
    {
        var issues = new List<BillingV2CartIssue>();
        var provisioning = BillingV2CartProvisioningReadiness.Ready;
        foreach (var item in items)
        {
            if (!ValidScopes.Contains(item.ScopeTemplate))
            {
                issues.Add(new("CART_SCOPE_INVALID", "error", item.CartItemId,
                    item.ServiceCode, "Le scope Billing V2 est inconnu.", true));
                provisioning = BillingV2CartProvisioningReadiness.Blocked;
                continue;
            }
            if (!IsScopeCompatible(item.DefaultScopeType, item.ScopeTemplate))
            {
                issues.Add(new("CART_SCOPE_INVALID", "error", item.CartItemId,
                    item.ServiceCode, "Le scope ne correspond pas au service catalogue.", true));
                provisioning = BillingV2CartProvisioningReadiness.Blocked;
                continue;
            }
            if (item.ScopeTemplate == "subscription")
            {
                if (!string.IsNullOrWhiteSpace(item.SubjectBinding))
                {
                    issues.Add(new("CART_SUBJECT_BINDING_INVALID", "error", item.CartItemId,
                        item.ServiceCode, "Un service porte par l'abonnement ne cible pas un sujet individuel.", true));
                    provisioning = BillingV2CartProvisioningReadiness.Blocked;
                }
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.SubjectBinding))
            {
                if (item.BindingRequiredBeforeCheckout)
                {
                    issues.Add(new("CART_SUBJECT_BINDING_REQUIRED", "error", item.CartItemId,
                        item.ServiceCode, "Un sujet doit etre choisi avant le checkout.", true));
                    provisioning = BillingV2CartProvisioningReadiness.Blocked;
                    continue;
                }
                issues.Add(new("CART_SUBJECT_BINDING_DEFERRED", "info", item.CartItemId,
                    item.ServiceCode, "Le sujet sera resolu avant le provisioning.", false));
                if (provisioning == BillingV2CartProvisioningReadiness.Ready)
                    provisioning = BillingV2CartProvisioningReadiness.Deferred;
                continue;
            }
            if (!IsSafeSubjectBinding(item.SubjectBinding))
            {
                issues.Add(new("CART_SUBJECT_BINDING_INVALID", "error", item.CartItemId,
                    item.ServiceCode, "La reference de sujet est invalide.", true));
                provisioning = BillingV2CartProvisioningReadiness.Blocked;
            }
        }
        return new(!issues.Any(issue => issue.Blocking), provisioning, issues);
    }

    private static bool IsSafeSubjectBinding(string value)
        => value.Length is >= 1 and <= 255
            && value.All(character => char.IsLetterOrDigit(character)
                || character is '-' or '_' or ':' or '.');

    private static bool IsScopeCompatible(string catalogScope, string cartScope)
        => catalogScope switch
        {
            "subscription" => cartScope == "subscription",
            // Le catalogue V2 exprime le portage individuel par `user`; le
            // panier affine ensuite ce portage entre titulaire principal et
            // utilisateur additionnel sans inventer un nouveau service.
            "user" => cartScope is "primary_user" or "additional_user",
            "primary_user" => cartScope == "primary_user",
            "additional_user" => cartScope == "additional_user",
            _ => false
        };

    /// <summary>
    /// Reference pure du parcours de dependances. Le service MariaDB applique
    /// la meme discipline dans sa transaction ; cette forme sans I/O verrouille
    /// les cycles, les branches convergentes et le point fixe en test.
    /// </summary>
    public static BillingV2CartDependencyResolution ResolveDependencies(
        IReadOnlyCollection<BillingV2CartDependencyNode> initial,
        IReadOnlyCollection<BillingV2CartDependencyRule> rules,
        int maximumDepth = 16)
    {
        var nodes = initial.ToDictionary(node => node.Key, StringComparer.Ordinal);
        var issues = new List<BillingV2CartIssue>();
        var queue = new Queue<(BillingV2CartDependencyNode Node, IReadOnlySet<string> Lineage, int Depth)>();
        foreach (var node in initial)
            queue.Enqueue((node, new HashSet<string>(StringComparer.Ordinal) { node.ServiceCode }, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Depth >= maximumDepth)
            {
                issues.Add(DependencyIssue("CART_DEPENDENCY_DEPTH_EXCEEDED", current.Node, true));
                continue;
            }
            foreach (var rule in rules.Where(rule => rule.ServiceCode == current.Node.ServiceCode))
            {
                if (current.Lineage.Contains(rule.RequiredServiceCode))
                {
                    issues.Add(DependencyIssue("CART_DEPENDENCY_CYCLE", current.Node, true));
                    continue;
                }
                var candidate = new BillingV2CartDependencyNode(rule.RequiredServiceCode,
                    rule.ResolveTierCode(current.Node), rule.ResolveScope(current.Node),
                    rule.ResolveBinding(current.Node), current.Node.Quantity);
                if (rule.Resolution is BillingV2CartDependencyResolutionKind.Ambiguous
                    or BillingV2CartDependencyResolutionKind.Impossible)
                {
                    issues.Add(DependencyIssue(rule.Resolution == BillingV2CartDependencyResolutionKind.Ambiguous
                        ? "CART_DEPENDENCY_AMBIGUOUS" : "CART_DEPENDENCY_IMPOSSIBLE", current.Node, true));
                    continue;
                }
                if (nodes.TryGetValue(candidate.Key, out var existing))
                {
                    if (!existing.IsCompatibleWith(candidate))
                        issues.Add(DependencyIssue("CART_DEPENDENCY_IMPOSSIBLE", current.Node, true));
                    continue;
                }
                nodes[candidate.Key] = candidate;
                var lineage = new HashSet<string>(current.Lineage, StringComparer.Ordinal)
                {
                    candidate.ServiceCode
                };
                queue.Enqueue((candidate, lineage, current.Depth + 1));
            }
        }
        return new(nodes.Values.OrderBy(node => node.Key, StringComparer.Ordinal).ToArray(), issues);
    }

    /// <summary>
    /// Construit la composition d'une formule depuis la definition de preset
    /// et les seuls choix effectifs du client. Les lignes requises sont une
    /// partie structurelle du preset : elles ne doivent jamais dependre d'un
    /// champ ou d'un composant transmis par le navigateur.
    ///
    /// Les options selectionnees par defaut mais facultatives ne sont pas
    /// injectees ici. Elles doivent etre presentes dans la selection validee,
    /// ce qui preserve exactement les choix effectues dans le configurateur.
    /// </summary>
    public static BillingV2CartPresetCompositionResolution ResolvePresetComposition(
        IReadOnlyList<BillingV2CartPresetCompositionItem> presetItems,
        IReadOnlyList<BillingV2PublicSelectionComponent> selectedComponents)
    {
        var selected = new Dictionary<string, BillingV2CartPresetCompositionItem>(StringComparer.Ordinal);
        foreach (var component in selectedComponents)
        {
            var candidates = presetItems
                .Where(item => string.Equals(item.ServiceCode, component.ServiceCode,
                    StringComparison.Ordinal)
                    && string.Equals(item.TierCode, component.TierCode,
                        StringComparison.Ordinal))
                .ToArray();
            // Une formule legacy ne porte pas de binding concret. Si le
            // preset declare deux cibles possibles, un choix arbitraire serait
            // une escalation de la selection navigateur.
            if (candidates.Length != 1)
                return BillingV2CartPresetCompositionResolution.Invalid;

            var item = candidates[0];
            if (component.Quantity < item.MinimumQuantity
                || component.Quantity > item.MaximumQuantity
                || (!item.SelectedByDefault && !item.CustomerEditable)
                || !selected.TryAdd(item.PresetItemId, item with { Quantity = component.Quantity }))
                return BillingV2CartPresetCompositionResolution.Invalid;
        }

        foreach (var required in presetItems.Where(item => item.RequiredItem)
                     .OrderBy(item => item.DisplayOrder)
                     .ThenBy(item => item.PresetItemId, StringComparer.Ordinal))
        {
            // Une definition required elle-meme invalide ne peut jamais
            // produire un Cart partiel : le service annulera la transaction.
            if (required.Quantity < required.MinimumQuantity
                || required.Quantity > required.MaximumQuantity)
                return BillingV2CartPresetCompositionResolution.Invalid;
            selected.TryAdd(required.PresetItemId, required);
        }

        var composition = selected.Values
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.PresetItemId, StringComparer.Ordinal)
            .ToArray();
        return composition.Any(item => item.RequiredItem
                    && !selected.ContainsKey(item.PresetItemId))
            ? BillingV2CartPresetCompositionResolution.Invalid
            : new("CART_PRESET_COMPOSITION_VALID", composition);
    }

    private static BillingV2CartIssue DependencyIssue(string code,
        BillingV2CartDependencyNode node, bool blocking)
        => new(code, "error", node.ServiceCode, node.ServiceCode, code, blocking);
}

public sealed record BillingV2CartConfigurationCandidate(
    string CartItemId,
    string ServiceCode,
    string ConfigurationPolicy,
    string? ConfigurationReference,
    bool PriceStableWithoutConfiguration);

public sealed record BillingV2CartScopeCandidate(
    string CartItemId,
    string ServiceCode,
    string ScopeTemplate,
    string DefaultScopeType,
    string? SubjectBinding,
    bool BindingRequiredBeforeCheckout = false);

public sealed record BillingV2CartScopeEvaluation(
    bool CommercialReady,
    string ProvisioningReadiness,
    IReadOnlyList<BillingV2CartIssue> Issues);

/// <summary>
/// Representation serveur d'une ligne de preset utilisable pour composer un
/// Cart. Les identifiants service/tier restent internes ; le navigateur ne les
/// fournit jamais dans une commande formule vers Cart.
/// </summary>
public sealed record BillingV2CartPresetCompositionItem(
    string PresetItemId,
    string ServiceId,
    string ServiceCode,
    string? TierId,
    string? TierCode,
    string ScopeTemplate,
    int Quantity,
    bool RequiredItem,
    bool CustomerEditable,
    bool SelectedByDefault,
    int MinimumQuantity,
    int MaximumQuantity,
    int DisplayOrder);

public sealed record BillingV2CartPresetCompositionResolution(
    string Code,
    IReadOnlyList<BillingV2CartPresetCompositionItem> Items)
{
    public static readonly BillingV2CartPresetCompositionResolution Invalid = new(
        "CART_PRESET_COMPOSITION_INVALID", []);

    public bool IsValid => string.Equals(Code, "CART_PRESET_COMPOSITION_VALID",
        StringComparison.Ordinal);
}

public enum BillingV2CartDependencyResolutionKind
{
    Deterministic,
    Ambiguous,
    Impossible
}

public sealed record BillingV2CartDependencyNode(
    string ServiceCode,
    string? TierCode,
    string ScopeTemplate,
    string? SubjectBinding,
    int Quantity)
{
    public string Key => string.Join("/", ServiceCode, TierCode ?? "-", ScopeTemplate, SubjectBinding ?? "-");

    public bool IsCompatibleWith(BillingV2CartDependencyNode other)
        => TierCode == other.TierCode && ScopeTemplate == other.ScopeTemplate
            && SubjectBinding == other.SubjectBinding && Quantity == other.Quantity;
}

public sealed record BillingV2CartDependencyRule(
    string ServiceCode,
    string RequiredServiceCode,
    BillingV2CartDependencyResolutionKind Resolution = BillingV2CartDependencyResolutionKind.Deterministic,
    string? RequiredTierCode = null,
    string? RequiredScopeTemplate = null,
    string? RequiredSubjectBinding = null)
{
    public string? ResolveTierCode(BillingV2CartDependencyNode source)
        => RequiredTierCode ?? source.TierCode;

    public string ResolveScope(BillingV2CartDependencyNode source)
        => RequiredScopeTemplate ?? source.ScopeTemplate;

    public string? ResolveBinding(BillingV2CartDependencyNode source)
        => RequiredSubjectBinding ?? source.SubjectBinding;
}

public sealed record BillingV2CartDependencyResolution(
    IReadOnlyList<BillingV2CartDependencyNode> Nodes,
    IReadOnlyList<BillingV2CartIssue> Issues);
