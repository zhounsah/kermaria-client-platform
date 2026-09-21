using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface IBillingV2CartService
{
    Task<BillingV2CartMutationResult> GetOrCreateCurrentAsync(BillingV2CartOwner owner, string currency, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> GetCurrentAsync(BillingV2CartOwner owner, string currency, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> GetAsync(BillingV2CartOwner owner, string cartId, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> ImportFormulaSelectionAsync(BillingV2CartOwner owner,
        BillingV2PublicSelection selection, string currency, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> AddItemAsync(BillingV2CartOwner owner, string cartId, int expectedVersion, BillingV2CartItemCommand command, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> UpdateItemAsync(BillingV2CartOwner owner, string cartId, string itemId, int expectedVersion, BillingV2CartItemCommand command, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> RemoveItemAsync(BillingV2CartOwner owner, string cartId, string itemId, int expectedVersion, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> SetCommitmentAsync(BillingV2CartOwner owner, string cartId, int expectedVersion, string? commitmentCode, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> SetPaymentModeAsync(BillingV2CartOwner owner, string cartId, int expectedVersion, string? paymentMode, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> QuoteAsync(BillingV2CartOwner owner, string cartId, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> ExpireAsync(BillingV2CartOwner owner, string cartId, int expectedVersion, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> ClaimAsync(string anonymousToken, string customerId, int expectedVersion, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> ClaimCurrentAsync(string anonymousToken, string customerId, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> ProjectLegacySelectionAsync(BillingV2CartOwner owner, string cartId, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> InitializeFromPresetAsync(BillingV2CartOwner owner, string presetCode,
        string currency, int? expectedVersion, bool replace, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> AddPresetItemAsync(BillingV2CartOwner owner, string cartId,
        int expectedVersion, string presetItemId, CancellationToken cancellationToken);
}

public sealed record BillingV2CartItemCommand(
    string ServiceCode,
    string? TierCode,
    int Quantity,
    string? ScopeTemplate,
    string? SubjectBinding,
    string? SourcePresetId,
    string? SourcePresetItemId,
    string? ConfigurationKind,
    string? ConfigurationReference,
    string Origin);

/// <summary>
/// Persistance de l'intention commerciale. Cette classe est volontairement
/// independante du checkout authoritative : aucune methode ne
/// touche une table subscription/payment/event/outbox/provisioning.
/// </summary>
public sealed class BillingV2CartService : IBillingV2CartService
{
    private const int CurrentTransactionMaxAttempts = 3;

    private static readonly string[] RequiredTables =
    [
        "billing_v2_carts", "billing_v2_cart_items", "billing_v2_cart_quotes",
        "billing_v2_services", "billing_v2_service_tiers", "billing_v2_service_prices",
        "billing_v2_service_dependencies", "billing_v2_commitment_terms",
        "billing_v2_commitment_payment_options", "billing_v2_offer_presets",
        "billing_v2_preset_items"
    ];

    private readonly SqlRuntimeConfiguration _sql;
    private readonly IBillingV2PublicCatalogService _catalogService;
    private readonly IBillingV2PricingEngine _pricing;
    private readonly ILogger<BillingV2CartService> _logger;

    public BillingV2CartService(SqlRuntimeConfiguration sql,
        IBillingV2PublicCatalogService catalogService,
        IBillingV2PricingEngine pricing,
        ILogger<BillingV2CartService> logger)
    {
        _sql = sql;
        _catalogService = catalogService;
        _pricing = pricing;
        _logger = logger;
    }

    public async Task<BillingV2CartMutationResult> GetOrCreateCurrentAsync(
        BillingV2CartOwner owner, string currency, CancellationToken cancellationToken)
    {
        if (!owner.IsValid || !IsCurrency(currency)) return new("CART_OWNER_OR_CURRENCY_INVALID");
        var normalizedCurrency = currency.ToUpperInvariant();
        var deadlockRetryCount = 0;
        for (var attempt = 1; attempt <= CurrentTransactionMaxAttempts; attempt++)
        {
            try
            {
                var result = await GetOrCreateCurrentOnceAsync(owner, normalizedCurrency, cancellationToken);
                _logger.LogInformation(
                    "Billing V2 Cart command completed. command=current owner_type={OwnerType} cart_id={CartId} result={Result} retry_deadlock_count={RetryDeadlockCount}",
                    owner.IsAuthenticated ? "customer" : "anonymous", result.Cart?.Id, result.Code, deadlockRetryCount);
                return result;
            }
            catch (MySqlException exception) when (IsCurrentTransactionRetryable(exception)
                && attempt < CurrentTransactionMaxAttempts)
            {
                if (IsDeadlock(exception)) deadlockRetryCount++;
                _logger.LogWarning(exception,
                    "Billing V2 Cart current transaction will retry. command=current owner_type={OwnerType} currency={Currency} retry_deadlock_count={RetryDeadlockCount}",
                    owner.IsAuthenticated ? "customer" : "anonymous", normalizedCurrency, deadlockRetryCount);
            }
            catch (MySqlException exception) when (IsCurrentTransactionRetryable(exception))
            {
                _logger.LogError(exception,
                    "Billing V2 Cart current transaction exhausted its retries. command=current owner_type={OwnerType} currency={Currency} retry_deadlock_count={RetryDeadlockCount}",
                    owner.IsAuthenticated ? "customer" : "anonymous", normalizedCurrency, deadlockRetryCount);
                throw;
            }
        }

        throw new InvalidOperationException("CART_CURRENT_RETRY_EXHAUSTED");
    }

    private async Task<BillingV2CartMutationResult> GetOrCreateCurrentOnceAsync(
        BillingV2CartOwner owner, string currency, CancellationToken cancellationToken)
    {
        await using var connection = await OpenReadyAsync(cancellationToken);
        var now = DateTime.UtcNow;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // Le cleanup est borne a l'unique owner/devise vise. Un current ne doit
        // jamais verrouiller les Carts expires d'autres visiteurs.
        await ExpireInactiveCurrentAsync(connection, transaction, owner, currency, now, cancellationToken);

        // Une lecture non verrouillante evite de poser un gap lock avant une
        // creation. La contrainte unique de slots arbitre la course finale.
        var cart = await ReadCurrentAsync(connection, transaction, owner, currency, false, cancellationToken);
        if (cart is null)
        {
            var id = Guid.NewGuid().ToString("D");
            try
            {
                await InsertOpenCartAsync(connection, transaction, owner, id, currency, now, cancellationToken);
                cart = await ReadCartAsync(connection, transaction, owner, id, false, cancellationToken);
            }
            catch (MySqlException exception) when (IsDuplicateKey(exception))
            {
                // Un concurrent a cree le meme Cart owner/devise. La lecture
                // verrouillante est un current read InnoDB et voit son gagnant.
                cart = await ReadCurrentAsync(connection, transaction, owner, currency, true, cancellationToken);
            }
        }
        else
        {
            cart = await ReadCurrentAsync(connection, transaction, owner, currency, true, cancellationToken);
        }

        if (cart is null)
            throw new InvalidOperationException("CART_CURRENT_CONCURRENT_READ_UNAVAILABLE");
        // Une creation initialise deja l'activite a now; ce touch est
        // intentionnellement idempotent et couvre aussi le Cart gagnant relu
        // apres une collision de cle unique.
        await TouchCartActivityAsync(connection, transaction, cart.Id, now, cancellationToken);
        cart = await ReadCartAsync(connection, transaction, owner, cart.Id, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new("CART_OK", cart);
    }

    public async Task<BillingV2CartMutationResult> GetAsync(BillingV2CartOwner owner,
        string cartId, CancellationToken cancellationToken)
    {
        if (!owner.IsValid || !Guid.TryParse(cartId, out _)) return new("CART_NOT_FOUND");
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExpireInactiveCartAsync(connection, transaction, owner, cartId, DateTime.UtcNow, cancellationToken);
        var cart = await ReadCartAsync(connection, transaction, owner, cartId, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return cart is null ? new("CART_NOT_FOUND") : new("CART_OK", cart);
    }

    /// <summary>
    /// Lecture du Cart courant sans creation, renouvellement d'activite ou
    /// ecriture d'expiration. Elle alimente notamment le header public et la
    /// page panier : consulter le site ne materialise jamais un panier vide.
    /// </summary>
    public async Task<BillingV2CartMutationResult> GetCurrentAsync(BillingV2CartOwner owner,
        string currency, CancellationToken cancellationToken)
    {
        if (!owner.IsValid || !IsCurrency(currency)) return new("CART_NOT_FOUND");
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var cart = await ReadCurrentAsync(connection, transaction, owner,
            currency.ToUpperInvariant(), false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (cart is null) return new("CART_NOT_FOUND");
        return cart.ExpiresAtUtc <= DateTime.UtcNow
            ? new("CART_EXPIRED", cart with { Status = BillingV2CartStatuses.Expired })
            : new("CART_OK", cart);
    }

    /// <summary>
    /// Frontiere explicite entre une formule configuree localement et le
    /// panier. Cette commande n'est appelee qu'apres le CTA public : ouvrir
    /// ou modifier <c>/formules/[code]</c> ne peut donc ni creer ni toucher un
    /// Cart. La selection navigateur ne contient que des codes catalogue ; la
    /// politique legacy, le preset et les bornes de metadata sont tous relus
    /// cote serveur avant la moindre ecriture.
    /// </summary>
    public async Task<BillingV2CartMutationResult> ImportFormulaSelectionAsync(
        BillingV2CartOwner owner, BillingV2PublicSelection selection, string currency,
        CancellationToken cancellationToken)
    {
        if (!owner.IsValid || !IsCurrency(currency)
            || string.IsNullOrWhiteSpace(selection.PresetCode))
            return new("CART_FORMULA_SELECTION_INVALID");

        var normalizedCurrency = currency.ToUpperInvariant();
        var catalog = await _catalogService.GetCatalogAsync(cancellationToken);
        if (!string.Equals(catalog.Currency, normalizedCurrency, StringComparison.Ordinal))
            return new("CART_FORMULA_SELECTION_INVALID");
        var resolution = BillingV2PublicSelectionPolicy.Resolve(catalog, selection);
        if (!resolution.Resolved)
            return new("CART_FORMULA_SELECTION_INVALID");

        // La creation du Cart est volontairement apres la validation pure de
        // la formule : une selection invalide ne doit pas laisser un Cart
        // anonyme inaccessible lorsque le BFF n'a pas encore pose son cookie.
        var current = await GetOrCreateCurrentAsync(owner, normalizedCurrency, cancellationToken);
        if (current.Cart is null) return current;

        var imported = await MutateAsync(owner, current.Cart.Id, current.Cart.Version,
            async (connection, transaction, cart, now, token) =>
            {
                var preset = await ReadPresetAsync(connection, transaction,
                    selection.PresetCode!.Trim(), token);
                if (preset is null) return "CART_FORMULA_SELECTION_INVALID";

                var configuredItems = ResolveFormulaPresetItems(preset, resolution.Components);
                if (configuredItems is null) return "CART_FORMULA_SELECTION_INVALID";

                // Le Cart est multi-origines. Une formule peut donc rejoindre
                // une composition directe, mais uniquement par union
                // deterministe : une ligne identique est reutilisee, une
                // ligne absente est ajoutee, toute concurrence de palier ou
                // de quantite exige une decision explicite dans /panier.
                var importPlan = PlanFormulaImport(cart, preset, configuredItems, selection);
                if (!importPlan.CanImport)
                    return "CART_MERGE_REQUIRES_REVIEW";

                var commitmentId = await ResolveFormulaCommitmentIdAsync(connection, transaction,
                    selection, token);
                if (commitmentId is null) return "CART_FORMULA_SELECTION_INVALID";

                var displayOrder = cart.Items.Count == 0 ? 1 : cart.Items.Max(item => item.DisplayOrder) + 1;
                foreach (var item in importPlan.ItemsToInsert)
                {
                    await InsertPresetItemAsync(connection, transaction, cart.Id, item,
                        displayOrder++, now, token);
                }
                // Une ligne déjà présente peut satisfaire une sélection de
                // formule sans perdre sa provenance historique (par exemple
                // origin=direct ou origin=dependency). Le lien de définition
                // porte l'autorisation courante du preset ; il ne réécrit
                // jamais origin.
                foreach (var link in importPlan.ExistingPresetLinks)
                {
                    await AttachPresetDefinitionAsync(connection, transaction, cart.Id,
                        link.CartItemId, link.PresetItemId, token);
                }

                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                // source_preset_id reste la provenance qui a initialise un
                // Cart vide. Les items portent toujours leur propre
                // source_preset_item_id, ce qui laisse le Cart extensible par
                // de futures origines directes sans le reduire a un preset.
                update.CommandText = """
                    UPDATE billing_v2_carts
                    SET source_preset_id = COALESCE(source_preset_id, @preset_id),
                        commitment_term_id = @commitment_id,
                        payment_mode = @payment_mode
                    WHERE id = @cart_id;
                    """;
                update.Parameters.AddWithValue("@preset_id", preset.Id);
                update.Parameters.AddWithValue("@commitment_id", commitmentId);
                update.Parameters.AddWithValue("@payment_mode", selection.PaymentMode);
                update.Parameters.AddWithValue("@cart_id", cart.Id);
                await update.ExecuteNonQueryAsync(token);

                var importedCart = await ReadCartAsync(connection, transaction, owner, cart.Id,
                    false, token);
                if (importedCart is null) return "CART_FORMULA_SELECTION_INVALID";

                var dependencyResult = await ResolveImportDependenciesAsync(connection, transaction,
                    owner, importedCart, now, token);
                if (dependencyResult != "CART_ITEM_ADDED") return dependencyResult;

                importedCart = await ReadCartAsync(connection, transaction, owner, cart.Id,
                    false, token);
                if (importedCart is null
                    || !HasAllRequiredPresetItems(preset, importedCart)
                    || HasDuplicateStructuralItems(importedCart)
                    || (await ReadDependencyIssuesAsync(connection, transaction, importedCart, token))
                    .Any(issue => issue.Blocking)
                    || !await HasValidImportedScopeAndPricingAsync(connection, transaction,
                        importedCart, token))
                    return "CART_FORMULA_SELECTION_INVALID";
                return "CART_FORMULA_SELECTION_IMPORTED";
            }, cancellationToken);

        if (imported.Code != "CART_FORMULA_SELECTION_IMPORTED" || imported.Cart is null)
            return imported;

        // Le devis Cart est volontairement recalcule apres l'import. Le devis
        // formule n'est jamais une reservation ni une autorite de prix.
        var quoted = await QuoteAsync(owner, imported.Cart.Id, cancellationToken);
        return quoted.Quote is null
            ? imported
            : new BillingV2CartMutationResult("CART_FORMULA_SELECTION_IMPORTED",
                quoted.Cart, quoted.Quote);
    }

    /// <summary>
    /// Initialise transactionnellement un Cart depuis un preset public. Le
    /// navigateur ne fournit que le code du preset : les services, paliers,
    /// scopes, quantites et identifiants de lignes sont relus en base.
    /// Cette methode ne connait ni checkout ni subscription.
    /// </summary>
    public async Task<BillingV2CartMutationResult> InitializeFromPresetAsync(
        BillingV2CartOwner owner, string presetCode, string currency, int? expectedVersion,
        bool replace, CancellationToken cancellationToken)
    {
        if (!owner.IsValid || !IsCurrency(currency) || string.IsNullOrWhiteSpace(presetCode))
            return new("CART_PRESET_INVALID");
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        await ExpireInactiveCurrentAsync(connection, transaction, owner, currency.ToUpperInvariant(), now, cancellationToken);
        var preset = await ReadPresetAsync(connection, transaction, presetCode.Trim(), cancellationToken);
        if (preset is null) return new("CART_PRESET_INVALID");

        var cart = await ReadCurrentAsync(connection, transaction, owner, currency.ToUpperInvariant(), true, cancellationToken);
        if (cart is null)
        {
            var id = Guid.NewGuid().ToString("D");
            await InsertOpenCartAsync(connection, transaction, owner, id, currency.ToUpperInvariant(), now, cancellationToken);
            cart = await ReadCartAsync(connection, transaction, owner, id, true, cancellationToken);
        }
        if (cart is null) return new("CART_NOT_FOUND");

        if (string.Equals(cart.SourcePresetId, preset.Id, StringComparison.Ordinal))
        {
            await TouchCartActivityAsync(connection, transaction, cart.Id, now, cancellationToken);
            var current = await ReadCartAsync(connection, transaction, owner, cart.Id, false, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new("CART_PRESET_INITIALIZED", current);
        }
        if (cart.SourcePresetId is not null || cart.Items.Count > 0)
        {
            if (!replace)
                return new("CART_PRESET_CONFLICT", cart, null, cart.SourcePresetId);
            if (expectedVersion is null || cart.Version != expectedVersion.Value)
                return new("CART_VERSION_CONFLICT", cart);
            await using var clear = connection.CreateCommand();
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM billing_v2_cart_items WHERE cart_id = @cart_id;";
            clear.Parameters.AddWithValue("@cart_id", cart.Id);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }
        else if (expectedVersion is not null && cart.Version != expectedVersion.Value)
        {
            return new("CART_VERSION_CONFLICT", cart);
        }

        await using (var setPreset = connection.CreateCommand())
        {
            setPreset.Transaction = transaction;
            setPreset.CommandText = "UPDATE billing_v2_carts SET source_preset_id = @preset WHERE id = @cart_id;";
            setPreset.Parameters.AddWithValue("@preset", preset.Id);
            setPreset.Parameters.AddWithValue("@cart_id", cart.Id);
            await setPreset.ExecuteNonQueryAsync(cancellationToken);
        }
        var displayOrder = 1;
        foreach (var item in preset.Items.Where(item => item.SelectedByDefault))
        {
            await InsertPresetItemAsync(connection, transaction, cart.Id, item, displayOrder++, now, cancellationToken);
        }
        await using (var version = connection.CreateCommand())
        {
            version.Transaction = transaction;
            version.CommandText = """
                UPDATE billing_v2_carts SET version = version + 1, updated_at = @now,
                    last_activity_at = @now, expires_at = @expires WHERE id = @id;
                """;
            version.Parameters.AddWithValue("@now", now);
            version.Parameters.AddWithValue("@expires", now.AddDays(30));
            version.Parameters.AddWithValue("@id", cart.Id);
            await version.ExecuteNonQueryAsync(cancellationToken);
        }
        var initialized = await ReadCartAsync(connection, transaction, owner, cart.Id, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new("CART_PRESET_INITIALIZED", initialized);
    }

    /// <summary>
    /// Ajoute une option uniquement par son identifiant de définition. Les
    /// métadonnées de service/tier/scope/quantité ne proviennent jamais du
    /// navigateur et le preset du Cart est vérifié dans la même transaction.
    /// </summary>
    public Task<BillingV2CartMutationResult> AddPresetItemAsync(BillingV2CartOwner owner,
        string cartId, int expectedVersion, string presetItemId, CancellationToken cancellationToken)
        => MutateAsync(owner, cartId, expectedVersion, async (connection, transaction, cart, now, token) =>
        {
            if (cart.SourcePresetId is null || !Guid.TryParse(presetItemId, out _))
                return "CART_PRESET_ITEM_INVALID";
            var definition = await ReadPresetItemAsync(connection, transaction, cart.SourcePresetId,
                presetItemId, token);
            if (definition is null || !definition.CustomerEditable)
                return "CART_PRESET_ITEM_INVALID";
            if (cart.Items.Any(item => item.SourcePresetItemId == presetItemId))
                return "CART_PRESET_ITEM_ALREADY_SELECTED";
            var root = await AlignTierToExistingRequirementAsync(connection, transaction, cart,
                new DependencyNode(definition.ServiceId, definition.TierId, definition.ScopeTemplate, null, definition.Quantity), token);
            if (root.Code != "CART_ITEM_OK") return root.Code;
            var resolvedDefinition = definition with { TierId = root.Node!.TierId };
            var existingEquivalent = cart.Items.FirstOrDefault(item =>
                SameFormulaComposition(item, resolvedDefinition));
            if (existingEquivalent is not null)
            {
                await AttachPresetDefinitionAsync(connection, transaction, cart.Id,
                    existingEquivalent.Id, resolvedDefinition.PresetItemId, token);
                return "CART_PRESET_ITEM_ADDED";
            }
            if (cart.Items.Any(item => item.ServiceCode == definition.ServiceCode
                    && item.ScopeTemplate == definition.ScopeTemplate))
                return "CART_PRESET_ITEM_CONFLICT";
            await InsertPresetItemAsync(connection, transaction, cart.Id,
                resolvedDefinition, cart.Items.Count + 1, now, token);
            return await AddDeterministicDependenciesAsync(connection, transaction, cart,
                root.Node!, cart.Items.Count + 2, now, token);
        }, cancellationToken);

    public Task<BillingV2CartMutationResult> AddItemAsync(BillingV2CartOwner owner,
        string cartId, int expectedVersion, BillingV2CartItemCommand command,
        CancellationToken cancellationToken)
        => MutateAsync(owner, cartId, expectedVersion, async (connection, transaction, cart, now, token) =>
        {
            if (!string.Equals(command.Origin, BillingV2CartItemOrigins.Direct,
                    StringComparison.Ordinal))
                return "CART_ORIGIN_INVALID";
            var resolved = await ResolveItemAsync(connection, transaction, cart, command, token);
            if (resolved.Code != "CART_ITEM_OK") return resolved.Code;
            var merge = BillingV2CartPolicy.ResolveDirectAddition(cart.Items,
                resolved.ServiceId!, resolved.TierId, resolved.ScopeTemplate!, command.SubjectBinding);
            if (merge == BillingV2CartDirectAddResolution.AlreadyPresent)
                return "CART_ITEM_ALREADY_PRESENT";
            if (merge == BillingV2CartDirectAddResolution.TierConflict)
                return "CART_ITEM_TIER_CONFLICT";

            var displayOrder = cart.Items.Count == 0
                ? 1
                : cart.Items.Max(item => item.DisplayOrder) + 1;
            await InsertDirectItemAsync(connection, transaction, cart.Id, resolved,
                command, displayOrder++, now, token);

            // `mandatory_for_subscription` porte le socle global Billing V2.
            // Le navigateur n'envoie jamais ce composant : il est compose une
            // unique fois ici, y compris lorsqu'un Cart a ete initialise par
            // une formule et reçoit ensuite une ligne directe.
            var structural = await EnsureMandatoryStructuralItemsAsync(connection,
                transaction, cart, displayOrder, now, token);
            if (structural.Code != "CART_ITEM_ADDED") return structural.Code;
            // Une dependance ne devient automatique que lorsque le catalogue
            // donne une resolution unique. Sinon elle reste une issue
            // structuree dans le quote, jamais un choix arbitraire du client.
            return await AddDeterministicDependenciesAsync(connection, transaction, cart,
                new DependencyNode(resolved.ServiceId!, resolved.TierId,
                    resolved.ScopeTemplate!, command.SubjectBinding, command.Quantity),
                structural.NextDisplayOrder, now, token);
        }, cancellationToken);

    public Task<BillingV2CartMutationResult> UpdateItemAsync(BillingV2CartOwner owner,
        string cartId, string itemId, int expectedVersion, BillingV2CartItemCommand command,
        CancellationToken cancellationToken)
        => MutateAsync(owner, cartId, expectedVersion, async (connection, transaction, cart, now, token) =>
        {
            var existing = cart.Items.FirstOrDefault(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
            if (existing is null)
                return "CART_ITEM_NOT_FOUND";
            if (!existing.CanEdit)
                return "CART_PRESET_ITEM_IMMUTABLE";
            if (cart.SourcePresetId is not null && existing.SourcePresetItemId is not null)
            {
                var definition = await ReadPresetItemAsync(connection, transaction, cart.SourcePresetId,
                    existing.SourcePresetItemId, token);
                if (definition is null || !string.Equals(command.ServiceCode, definition.ServiceCode, StringComparison.Ordinal)
                    || !string.Equals(command.ScopeTemplate, definition.ScopeTemplate, StringComparison.Ordinal)
                    || command.Quantity < definition.MinimumQuantity || command.Quantity > definition.MaximumQuantity)
                    return "CART_PRESET_ITEM_INVALID";
                // Le navigateur ne choisit pas l'origine de la mutation : une
                // ligne issue d'un preset reste contrainte par toutes les
                // definitions soeurs autorisees du meme preset.
                command = command with
                {
                    Origin = BillingV2CartItemOrigins.Preset,
                    SourcePresetId = cart.SourcePresetId,
                    SourcePresetItemId = existing.SourcePresetItemId
                };
            }
            else
            {
                command = command with
                {
                    Origin = BillingV2CartItemOrigins.Direct,
                    SourcePresetId = null,
                    SourcePresetItemId = null
                };
            }
            var resolved = await ResolveItemAsync(connection, transaction, cart, command, token);
            if (resolved.Code != "CART_ITEM_OK") return resolved.Code;
            var merge = BillingV2CartPolicy.ResolveDirectAddition(
                cart.Items.Where(item => item.Id != existing.Id), resolved.ServiceId!,
                resolved.TierId, resolved.ScopeTemplate!, command.SubjectBinding);
            if (merge == BillingV2CartDirectAddResolution.AlreadyPresent)
                return "CART_ITEM_ALREADY_PRESENT";
            if (merge == BillingV2CartDirectAddResolution.TierConflict)
                return "CART_ITEM_TIER_CONFLICT";
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE billing_v2_cart_items
                SET service_id = @service_id, tier_id = @tier_id, quantity = @quantity,
                    scope_template = @scope, subject_binding = @subject,
                    configuration_kind = @configuration_kind,
                    configuration_reference = @configuration_reference, updated_at = @now
                WHERE id = @id AND cart_id = @cart_id;
                """;
            update.Parameters.AddWithValue("@service_id", resolved.ServiceId!);
            update.Parameters.AddWithValue("@tier_id", (object?)resolved.TierId ?? DBNull.Value);
            update.Parameters.AddWithValue("@quantity", command.Quantity);
            update.Parameters.AddWithValue("@scope", resolved.ScopeTemplate!);
            update.Parameters.AddWithValue("@subject", (object?)command.SubjectBinding ?? DBNull.Value);
            update.Parameters.AddWithValue("@configuration_kind", (object?)command.ConfigurationKind ?? DBNull.Value);
            update.Parameters.AddWithValue("@configuration_reference", (object?)command.ConfigurationReference ?? DBNull.Value);
            update.Parameters.AddWithValue("@now", now);
            update.Parameters.AddWithValue("@id", itemId);
            update.Parameters.AddWithValue("@cart_id", cart.Id);
            await update.ExecuteNonQueryAsync(token);
            var synchronized = await SynchronizeSameNumericDependentsAsync(connection, transaction, cart,
                existing with { TierId = resolved.TierId, TierCode = command.TierCode }, token);
            if (!synchronized) return "CART_DEPENDENCY_IMPOSSIBLE";
            var updatedCart = await ReadCartAsync(connection, transaction, owner, cart.Id, false, token);
            if (updatedCart is null || (await ReadDependencyIssuesAsync(connection, transaction,
                    updatedCart, token)).Any(issue => issue.Blocking))
                return "CART_DEPENDENCY_REQUIRED";
            return "CART_ITEM_UPDATED";
        }, cancellationToken);

    public Task<BillingV2CartMutationResult> RemoveItemAsync(BillingV2CartOwner owner,
        string cartId, string itemId, int expectedVersion, CancellationToken cancellationToken)
        => MutateAsync(owner, cartId, expectedVersion, async (connection, transaction, cart, _, token) =>
        {
            var existing = cart.Items.FirstOrDefault(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
            if (existing is null) return "CART_ITEM_NOT_FOUND";
            if (existing.IsStructural) return "CART_STRUCTURAL_ITEM_REQUIRED";
            if (existing.IsRequiredByPreset) return "CART_PRESET_ITEM_REQUIRED";
            if (existing.IsRequiredByDependency) return "CART_DEPENDENCY_REQUIRED";
            if (!existing.CanRemove) return "CART_PRESET_ITEM_REQUIRED";
            var before = await ReadDependencyIssuesAsync(connection, transaction, cart, token);
            var withoutItem = cart with { Items = cart.Items.Where(item => item.Id != existing.Id).ToArray() };
            var after = await ReadDependencyIssuesAsync(connection, transaction, withoutItem, token);
            if (after.Any(issue => issue.Blocking && !before.Any(current =>
                    current.Code == issue.Code && current.CartItemId == issue.CartItemId)))
                return "CART_DEPENDENCY_REQUIRED";
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM billing_v2_cart_items WHERE id = @id AND cart_id = @cart_id;";
            command.Parameters.AddWithValue("@id", itemId);
            command.Parameters.AddWithValue("@cart_id", cart.Id);
            return await command.ExecuteNonQueryAsync(token) == 1 ? "CART_ITEM_REMOVED" : "CART_ITEM_NOT_FOUND";
        }, cancellationToken);

    public Task<BillingV2CartMutationResult> SetCommitmentAsync(BillingV2CartOwner owner,
        string cartId, int expectedVersion, string? commitmentCode, CancellationToken cancellationToken)
        => MutateAsync(owner, cartId, expectedVersion, async (connection, transaction, cart, _, token) =>
        {
            string? commitmentId = null;
            if (!string.IsNullOrWhiteSpace(commitmentCode))
            {
                commitmentId = await LookupIdAsync(connection, transaction,
                    "SELECT id FROM billing_v2_commitment_terms WHERE code = @code AND status = 'active';",
                    commitmentCode.Trim(), token);
                if (commitmentId is null) return "CART_COMMITMENT_INVALID";
                // L'engagement et le mode de paiement forment un couple
                // global. Changer l'un ne peut pas laisser l'autre dans un
                // état que le pricing ou le futur checkout rejetterait plus
                // tard. Aucun choix de repli n'est fait silencieusement.
                if (!string.IsNullOrWhiteSpace(cart.PaymentMode))
                {
                    await using var paymentCompatibility = connection.CreateCommand();
                    paymentCompatibility.Transaction = transaction;
                    paymentCompatibility.CommandText = """
                        SELECT COUNT(*)
                        FROM billing_v2_commitment_payment_options
                        WHERE commitment_term_id = @id AND payment_mode = @mode
                          AND status = 'active';
                        """;
                    paymentCompatibility.Parameters.AddWithValue("@id", commitmentId);
                    paymentCompatibility.Parameters.AddWithValue("@mode", cart.PaymentMode);
                    if (Convert.ToInt32(await paymentCompatibility.ExecuteScalarAsync(token)) != 1)
                        return "CART_PAYMENT_MODE_INCOMPATIBLE";
                }
            }
            await UpdateCartFieldAsync(connection, transaction, cart.Id, "commitment_term_id", commitmentId, token);
            return "CART_COMMITMENT_UPDATED";
        }, cancellationToken);

    public Task<BillingV2CartMutationResult> SetPaymentModeAsync(BillingV2CartOwner owner,
        string cartId, int expectedVersion, string? paymentMode, CancellationToken cancellationToken)
        => MutateAsync(owner, cartId, expectedVersion, async (connection, transaction, cart, _, token) =>
        {
            var mode = paymentMode?.Trim().ToLowerInvariant();
            if (mode is not null && mode is not BillingV2PaymentModes.Monthly and not BillingV2PaymentModes.Upfront)
                return "CART_PAYMENT_MODE_INVALID";
            if (cart.CommitmentTermId is not null && mode is not null)
            {
                await using var check = connection.CreateCommand();
                check.Transaction = transaction;
                check.CommandText = """
                    SELECT COUNT(*) FROM billing_v2_commitment_payment_options
                    WHERE commitment_term_id = @id AND payment_mode = @mode AND status = 'active';
                    """;
                check.Parameters.AddWithValue("@id", cart.CommitmentTermId);
                check.Parameters.AddWithValue("@mode", mode);
                if (Convert.ToInt32(await check.ExecuteScalarAsync(token)) != 1)
                    return "CART_PAYMENT_MODE_INCOMPATIBLE";
            }
            await UpdateCartFieldAsync(connection, transaction, cart.Id, "payment_mode", mode, token);
            return "CART_PAYMENT_MODE_UPDATED";
        }, cancellationToken);

    public async Task<BillingV2CartMutationResult> QuoteAsync(BillingV2CartOwner owner,
        string cartId, CancellationToken cancellationToken)
    {
        var result = await GetAsync(owner, cartId, cancellationToken);
        if (result.Cart is null) return result;
        if (result.Cart.Status != BillingV2CartStatuses.Open) return new("CART_IMMUTABLE", result.Cart);
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var cart = await ReadCartAsync(connection, transaction, owner, cartId, true, cancellationToken);
        if (cart is null) return new("CART_NOT_FOUND");
        var quote = await BuildQuoteAsync(connection, transaction, cart, cancellationToken);
        await StoreQuoteAsync(connection, transaction, quote, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new("CART_QUOTED", cart, quote);
    }

    public Task<BillingV2CartMutationResult> ExpireAsync(BillingV2CartOwner owner,
        string cartId, int expectedVersion, CancellationToken cancellationToken)
        => MutateAsync(owner, cartId, expectedVersion, async (connection, transaction, cart, _, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE billing_v2_carts
                SET status = 'expired', open_customer_slot = NULL,
                    open_anonymous_slot = NULL, version = version + 1,
                    updated_at = UTC_TIMESTAMP(6)
                WHERE id = @id;
                """;
            command.Parameters.AddWithValue("@id", cart.Id);
            await command.ExecuteNonQueryAsync(token);
            return "CART_EXPIRED";
        }, cancellationToken, touch: false);

    /// <summary>
    /// Adaptateur transitoire, volontairement à sens unique : il relit un
    /// Cart déjà autorisé et produit la sélection historique consommée par le
    /// checkout legacy. Il ne crée aucun état financier et ne remplace pas le
    /// futur checkout Cart de Phase 4.
    /// </summary>
    public async Task<BillingV2CartMutationResult> ProjectLegacySelectionAsync(
        BillingV2CartOwner owner, string cartId, CancellationToken cancellationToken)
    {
        var result = await GetAsync(owner, cartId, cancellationToken);
        var cart = result.Cart;
        if (cart is null) return result;
        if (cart.SourcePresetId is null) return new("CART_LEGACY_PROJECTION_UNAVAILABLE", cart);
        var byCode = cart.Items.GroupBy(item => item.ServiceCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        BillingV2CartItem? One(string code) => byCode.TryGetValue(code, out var items) && items.Length == 1 ? items[0] : null;
        var personal = One(BillingV2PublicCatalogCodes.StoragePersonal);
        if (personal?.TierCode is null || string.IsNullOrWhiteSpace(cart.CommitmentCode)
            || string.IsNullOrWhiteSpace(cart.PaymentMode))
            return new("CART_LEGACY_PROJECTION_UNAVAILABLE", cart);
        var selection = new BillingV2PublicSelection(
            PresetCode: await ReadPresetCodeAsync(cart.SourcePresetId, cancellationToken),
            CommitmentCode: cart.CommitmentCode,
            PaymentMode: cart.PaymentMode,
            StoragePersonalTierCode: personal.TierCode,
            BackupPersonal: One(BillingV2PublicCatalogCodes.BackupPersonal) is not null,
            StorageSharedTierCode: One(BillingV2PublicCatalogCodes.StorageShared)?.TierCode,
            BackupShared: One(BillingV2PublicCatalogCodes.BackupShared) is not null,
            VpnTierCode: One(BillingV2PublicCatalogCodes.VpnAccess)?.TierCode,
            RemoteDesktop: One(BillingV2PublicCatalogCodes.RemoteDesktop) is not null,
            AdditionalUsers: One(BillingV2PublicCatalogCodes.AdditionalUser)?.Quantity ?? 0,
            SupportPlus: One(BillingV2PublicCatalogCodes.SupportPlus) is not null,
            Components: null);
        return new("CART_LEGACY_SELECTION_PROJECTED", cart, null, null, selection);
    }

    public async Task<BillingV2CartMutationResult> ClaimAsync(string anonymousToken,
        string customerId, int expectedVersion, CancellationToken cancellationToken)
    {
        var anonymous = new BillingV2CartOwner(null, anonymousToken);
        if (!anonymous.IsValid || !Guid.TryParse(customerId, out _)) return new("CART_CLAIM_INVALID");
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExpireInactiveOwnedCartsAsync(connection, transaction, anonymous, DateTime.UtcNow, cancellationToken);
        var cart = await ReadAnyCurrentAsync(connection, transaction, anonymous, true, cancellationToken);
        if (cart is null) return new("CART_NOT_FOUND");
        if (cart.Version != expectedVersion) return new("CART_VERSION_CONFLICT", cart);
        var customerOwner = new BillingV2CartOwner(customerId, null);
        await ExpireInactiveCurrentAsync(connection, transaction, customerOwner, cart.Currency, DateTime.UtcNow, cancellationToken);
        var existing = await ReadCurrentAsync(connection, transaction, customerOwner, cart.Currency, true, cancellationToken);
        if (existing is not null) return new("CART_CLAIM_CONFLICT", cart);
        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE billing_v2_carts SET customer_id = @customer, anonymous_session_hash = NULL,
                open_customer_slot = 1, open_anonymous_slot = NULL,
                version = version + 1, updated_at = @now, last_activity_at = @now,
                expires_at = @expires WHERE id = @id AND version = @version;
            """;
        var now = DateTime.UtcNow;
        update.Parameters.AddWithValue("@customer", customerId);
        update.Parameters.AddWithValue("@now", now);
        update.Parameters.AddWithValue("@expires", now.AddDays(30));
        update.Parameters.AddWithValue("@id", cart.Id);
        update.Parameters.AddWithValue("@version", expectedVersion);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) return new("CART_VERSION_CONFLICT", cart);
        var claimed = await ReadCartAsync(connection, transaction, customerOwner, cart.Id, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new("CART_CLAIMED", claimed);
    }

    /// <summary>Claim de reprise après login : la version anonyme est lue et verrouillée côté serveur.</summary>
    public async Task<BillingV2CartMutationResult> ClaimCurrentAsync(string anonymousToken,
        string customerId, CancellationToken cancellationToken)
    {
        var anonymous = new BillingV2CartOwner(null, anonymousToken);
        if (!anonymous.IsValid || !Guid.TryParse(customerId, out _)) return new("CART_CLAIM_INVALID");
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        await ExpireInactiveOwnedCartsAsync(connection, transaction, anonymous, now, cancellationToken);
        var cart = await ReadAnyCurrentAsync(connection, transaction, anonymous, true, cancellationToken);
        if (cart is null)
        {
            // Conserver l'expiration ciblée éventuellement effectuée ci-dessus :
            // l'absence de Cart à claim est un succès de reprise, pas un rollback implicite.
            await transaction.CommitAsync(cancellationToken);
            return new("CART_NOTHING_TO_CLAIM");
        }
        var customerOwner = new BillingV2CartOwner(customerId, null);
        await ExpireInactiveCurrentAsync(connection, transaction, customerOwner, cart.Currency, now, cancellationToken);
        if (await ReadCurrentAsync(connection, transaction, customerOwner, cart.Currency, true, cancellationToken) is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new("CART_CLAIM_CONFLICT", cart);
        }
        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE billing_v2_carts SET customer_id = @customer, anonymous_session_hash = NULL,
                open_customer_slot = 1, open_anonymous_slot = NULL, version = version + 1,
                updated_at = @now, last_activity_at = @now, expires_at = @expires
            WHERE id = @id AND version = @version;
            """;
        update.Parameters.AddWithValue("@customer", customerId);
        update.Parameters.AddWithValue("@now", now);
        update.Parameters.AddWithValue("@expires", now.AddDays(30));
        update.Parameters.AddWithValue("@id", cart.Id);
        update.Parameters.AddWithValue("@version", cart.Version);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) return new("CART_VERSION_CONFLICT", cart);
        var claimed = await ReadCartAsync(connection, transaction, customerOwner, cart.Id, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new("CART_CLAIMED", claimed);
    }

    private async Task<BillingV2CartMutationResult> MutateAsync(BillingV2CartOwner owner,
        string cartId, int expectedVersion,
        Func<MySqlConnection, MySqlTransaction, BillingV2Cart, DateTime, CancellationToken, Task<string>> mutation,
        CancellationToken cancellationToken, bool touch = true)
    {
        if (!owner.IsValid || !Guid.TryParse(cartId, out _)) return new("CART_NOT_FOUND");
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        await ExpireInactiveCartAsync(connection, transaction, owner, cartId, now, cancellationToken);
        var cart = await ReadCartAsync(connection, transaction, owner, cartId, true, cancellationToken);
        if (cart is null) return new("CART_NOT_FOUND");
        if (cart.Status != BillingV2CartStatuses.Open) return new("CART_IMMUTABLE", cart);
        if (cart.Version != expectedVersion) return new("CART_VERSION_CONFLICT", cart);
        var code = await mutation(connection, transaction, cart, now, cancellationToken);
        if (BillingV2CartMutationResults.Classify(code)
            != BillingV2CartMutationOutcome.Success)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(code, cart);
        }
        if (touch)
        {
            await using var version = connection.CreateCommand();
            version.Transaction = transaction;
            version.CommandText = """
                UPDATE billing_v2_carts SET version = version + 1, updated_at = @now,
                    last_activity_at = @now, expires_at = @expires WHERE id = @id AND version = @version;
                """;
            version.Parameters.AddWithValue("@now", now);
            version.Parameters.AddWithValue("@expires", now.AddDays(30));
            version.Parameters.AddWithValue("@id", cart.Id);
            version.Parameters.AddWithValue("@version", expectedVersion);
            if (await version.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new("CART_VERSION_CONFLICT", cart);
            }
        }
        var mutated = await ReadCartAsync(connection, transaction, owner, cartId, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(code, mutated);
    }

    private static IReadOnlyList<BillingV2CartPresetCompositionItem>? ResolveFormulaPresetItems(
        PresetDefinition preset,
        IReadOnlyList<BillingV2PublicSelectionComponent> components)
    {
        var composition = BillingV2CartPolicy.ResolvePresetComposition(preset.Items, components);
        return composition.IsValid ? composition.Items : null;
    }

    private static FormulaImportPlan PlanFormulaImport(BillingV2Cart cart,
        PresetDefinition preset, IReadOnlyList<BillingV2CartPresetCompositionItem> configuredItems,
        BillingV2PublicSelection selection)
    {
        // `source_preset_id` identifie seulement la premiere formule qui a
        // initialise le Cart. Un autre preset n'est pas fusionne en silence :
        // il peut modifier les composants structurels et doit passer par la
        // revue explicite du panier.
        if (cart.SourcePresetId is not null
            && !string.Equals(cart.SourcePresetId, preset.Id, StringComparison.Ordinal))
        {
            return FormulaImportPlan.Review;
        }
        if (cart.CommitmentCode is not null
            && !string.Equals(cart.CommitmentCode, selection.CommitmentCode,
                StringComparison.Ordinal))
        {
            return FormulaImportPlan.Review;
        }
        if (cart.PaymentMode is not null
            && !string.Equals(cart.PaymentMode, selection.PaymentMode,
                StringComparison.Ordinal))
        {
            return FormulaImportPlan.Review;
        }

        var inserts = new List<BillingV2CartPresetCompositionItem>();
        var links = new List<ExistingPresetLink>();
        foreach (var definition in configuredItems)
        {
            var existingPresetItems = cart.Items.Where(item => string.Equals(
                item.SourcePresetItemId, definition.PresetItemId,
                StringComparison.Ordinal)).ToArray();
            if (existingPresetItems.Length > 0)
            {
                // Une meme ligne de preset ne peut jamais etre rejouee avec
                // une autre valeur via un refresh du configurateur.
                if (existingPresetItems.Length != 1
                    || !SameFormulaComposition(existingPresetItems[0], definition))
                    return FormulaImportPlan.Review;
                continue;
            }

            // Base structurelle et service ajoute directement peuvent deja
            // satisfaire la meme intention commerciale. Cette equivalence est
            // une identite metier (service/tier/quantite/scope/sans binding),
            // jamais un rapprochement par libelle ou montant.
            var equivalent = cart.Items.FirstOrDefault(item => SameFormulaComposition(item, definition));
            if (equivalent is not null)
            {
                if (equivalent.SourcePresetItemId is null)
                    links.Add(new(equivalent.Id, definition.PresetItemId));
                continue;
            }

            // Un service deja present dans le meme scope mais avec une autre
            // configuration est ambigue : ne jamais ecraser son palier ni sa
            // quantite au nom de l'import de formule.
            if (cart.Items.Any(item => string.Equals(item.ServiceId, definition.ServiceId,
                    StringComparison.Ordinal)
                    && string.Equals(item.ScopeTemplate, definition.ScopeTemplate,
                        StringComparison.Ordinal)))
            {
                return FormulaImportPlan.Review;
            }
            inserts.Add(definition);
        }

        return new(true, inserts, links);
    }

    private static bool SameFormulaComposition(BillingV2CartItem item,
        BillingV2CartPresetCompositionItem definition)
        => string.Equals(item.ServiceId, definition.ServiceId, StringComparison.Ordinal)
            && string.Equals(item.TierId, definition.TierId, StringComparison.Ordinal)
            && item.Quantity == definition.Quantity
            && string.Equals(item.ScopeTemplate, definition.ScopeTemplate,
                StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(item.SubjectBinding);

    private sealed record FormulaImportPlan(
        bool CanImport,
        IReadOnlyList<BillingV2CartPresetCompositionItem> ItemsToInsert,
        IReadOnlyList<ExistingPresetLink> ExistingPresetLinks)
    {
        public static readonly FormulaImportPlan Review = new(false, [], []);
    }

    private sealed record ExistingPresetLink(string CartItemId, string PresetItemId);

    /// <summary>
    /// L'import ajoute d'abord la composition formule normalisee, puis laisse
    /// le resolver transactionnel traiter les dependances deterministes. Une
    /// relecture entre chaque racine donne au resolver la composition deja
    /// enrichie et empeche l'insertion de dependances convergentes en double.
    /// </summary>
    private async Task<string> ResolveImportDependenciesAsync(MySqlConnection connection,
        MySqlTransaction transaction, BillingV2CartOwner owner, BillingV2Cart cart,
        DateTime now, CancellationToken cancellationToken)
    {
        var roots = cart.Items.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id,
            StringComparer.Ordinal).ToArray();
        var current = cart;
        foreach (var root in roots)
        {
            var result = await AddDeterministicDependenciesAsync(connection, transaction, current,
                new DependencyNode(root.ServiceId, root.TierId, root.ScopeTemplate,
                    root.SubjectBinding, root.Quantity),
                current.Items.Count == 0 ? 1 : current.Items.Max(item => item.DisplayOrder) + 1,
                now, cancellationToken);
            if (result != "CART_ITEM_ADDED") return result;
            current = await ReadCartAsync(connection, transaction, owner, cart.Id, false,
                cancellationToken) ?? cart;
        }
        return "CART_ITEM_ADDED";
    }

    private static bool HasAllRequiredPresetItems(PresetDefinition preset, BillingV2Cart cart)
        => preset.Items.Where(item => item.RequiredItem).All(required =>
            cart.Items.Any(item => string.Equals(item.SourcePresetItemId,
                required.PresetItemId, StringComparison.Ordinal))
            || cart.Items.Any(item => SameFormulaComposition(item, required)));

    private static bool HasDuplicateStructuralItems(BillingV2Cart cart)
        => cart.Items.GroupBy(item => string.Join("|", item.ServiceId, item.TierId ?? "-",
                item.ScopeTemplate, item.SubjectBinding ?? "-"), StringComparer.Ordinal)
            .Any(group => group.Count() > 1);

    /// <summary>
    /// La politique de selection a deja valide les choix explicites. Cette
    /// seconde passe couvre les lignes required injectees et les dependances
    /// ajoutees dans cette transaction, avant le commit du Cart.
    /// </summary>
    private async Task<bool> HasValidImportedScopeAndPricingAsync(MySqlConnection connection,
        MySqlTransaction transaction, BillingV2Cart cart, CancellationToken cancellationToken)
    {
        var preview = await BuildQuoteAsync(connection, transaction, cart, cancellationToken);
        return !preview.ScopeIssues.Any(issue => issue.Blocking)
            && !preview.ConfigurationIssues.Any(issue => issue.Blocking
                && issue.Code is "CART_SERVICE_UNAVAILABLE" or "CART_TIER_UNAVAILABLE"
                    or "CART_PRICE_UNAVAILABLE");
    }

    private static async Task<string?> ResolveFormulaCommitmentIdAsync(
        MySqlConnection connection, MySqlTransaction transaction,
        BillingV2PublicSelection selection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(selection.CommitmentCode)
            || selection.PaymentMode is not BillingV2PaymentModes.Monthly
                and not BillingV2PaymentModes.Upfront)
            return null;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT term.id
            FROM billing_v2_commitment_terms term
            JOIN billing_v2_commitment_payment_options payment
              ON payment.commitment_term_id = term.id
             AND payment.payment_mode = @payment_mode
             AND payment.status = 'active'
            WHERE term.code = @code AND term.status = 'active'
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@code", selection.CommitmentCode.Trim());
        command.Parameters.AddWithValue("@payment_mode", selection.PaymentMode);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Identifier(value);
    }

    private async Task<BillingV2CartQuote> BuildQuoteAsync(MySqlConnection connection,
        MySqlTransaction transaction, BillingV2Cart cart, CancellationToken cancellationToken)
    {
        var catalog = await _catalogService.GetCatalogAsync(cancellationToken);
        var priceLines = new List<BillingV2CartQuoteLine>();
        var configurationCandidates = new List<BillingV2CartConfigurationCandidate>();
        var scopeCandidates = new List<BillingV2CartScopeCandidate>();
        var issues = new List<BillingV2CartIssue>();
        foreach (var item in cart.Items)
        {
            var service = catalog.Services.FirstOrDefault(candidate => candidate.Code == item.ServiceCode);
            if (service is null) { issues.Add(Issue("CART_SERVICE_UNAVAILABLE", item, true)); continue; }
            var tier = item.TierCode is null
                ? null
                : service.Tiers.FirstOrDefault(candidate => candidate.Code == item.TierCode);
            var components = tier is null && item.TierCode is null
                ? service.FlatComponents
                : tier?.Components;
            if (components is null) { issues.Add(Issue("CART_TIER_UNAVAILABLE", item, true)); continue; }
            var metadata = await ReadServiceMetadataAsync(connection, transaction, item.ServiceId, cancellationToken);
            if (metadata is null) { issues.Add(Issue("CART_SERVICE_UNAVAILABLE", item, true)); continue; }
            configurationCandidates.Add(new(item.Id, item.ServiceCode, metadata.ConfigurationPolicy,
                item.ConfigurationReference, metadata.PriceStableWithoutConfiguration));
            scopeCandidates.Add(new(item.Id, item.ServiceCode, item.ScopeTemplate,
                metadata.DefaultScopeType, item.SubjectBinding));
            var initialComponents = components.Where(component =>
                component.AppliesToInitialSubscription).ToArray();
            // Une ligne d'un autre prix/devise ne peut pas etre ignorée : elle
            // ferait perdre un frais ou une récurrence du catalogue. La quote
            // est donc fail-closed dès que toutes les composantes initiales
            // ne relèvent pas de la devise unique du Cart.
            if (initialComponents.Length == 0 || initialComponents.Any(component =>
                    !string.Equals(component.Currency, cart.Currency,
                        StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Issue("CART_PRICE_UNAVAILABLE", item, true));
                continue;
            }
            foreach (var component in initialComponents)
            {
                if (string.IsNullOrWhiteSpace(component.ServicePriceId)) { issues.Add(Issue("CART_PRICE_UNAVAILABLE", item, true)); continue; }
                priceLines.Add(new(item.Id, item.ServiceCode, item.TierCode,
                    service.Name, tier?.Label, component.ServicePriceId,
                    component.PriceCode ?? component.ServicePriceId, component.BillingCadence,
                    component.AmountCents, item.Quantity, checked(component.AmountCents * item.Quantity),
                    component.DiscountEligible,
                    await ReadFeeDeduplicationKeyAsync(connection, transaction, component.ServicePriceId, cancellationToken)));
            }
        }
        var dependencyIssues = await ReadDependencyIssuesAsync(connection, transaction, cart, cancellationToken);
        var readiness = BillingV2CartPolicy.EvaluateConfiguration(configurationCandidates);
        var scopeReadiness = BillingV2CartPolicy.EvaluateScopes(scopeCandidates);
        var retained = BillingV2CartPolicy.DeduplicateExplicitFees(priceLines, cart.Currency);
        var commitment = await ReadCommitmentAsync(connection, transaction, cart, cancellationToken);
        if (commitment.Code is null && retained.Any(line => line.BillingCadence == BillingV2BillingCadences.Monthly))
            issues.Add(new("CART_COMMITMENT_REQUIRED", "error", null, null, "Un engagement explicite est requis pour le recurrent.", true));
        var pricing = _pricing.Calculate(new BillingV2PricingRequest(
            retained.Select(line => new BillingV2PricingItem(line.CartItemId, line.ServiceCode,
                line.TierCode, line.PriceCode, line.UnitAmountCents, line.Quantity,
                line.BillingCadence, line.DiscountEligible)).ToArray(), commitment.DiscountBasisPoints,
            cart.PaymentMode ?? BillingV2PaymentModes.Monthly, commitment.Months, null, null, DateTime.UtcNow));
        var allBlocking = issues.Concat(dependencyIssues).Concat(readiness.Issues)
            .Concat(scopeReadiness.Issues).Any(issue => issue.Blocking);
        var now = DateTime.UtcNow;
        return new(cart.Id, cart.Version, await NextQuoteVersionAsync(connection, transaction, cart.Id, cancellationToken),
            BillingV2CartPolicy.CompositionFingerprint(cart, retained), cart.Currency,
            pricing.RecurringSubtotalCents, pricing.RecurringDiscountCents,
            pricing.PayableRecurringAmountCents, pricing.OneTimeSubtotalCents,
            pricing.TotalDueNowCents, now, now.AddMinutes(30), BillingV2CartQuoteStatuses.Current, retained,
            dependencyIssues.Select(BillingV2CartPolicy.ProjectCustomerIssue).ToArray(),
            scopeReadiness.Issues.Select(BillingV2CartPolicy.ProjectCustomerIssue).ToArray(),
            readiness.Issues.Concat(issues.Where(issue => issue.Code.StartsWith("CART_", StringComparison.Ordinal)))
                .Select(BillingV2CartPolicy.ProjectCustomerIssue).ToArray(),
            allBlocking ? BillingV2CartCommercialReadiness.Blocked : readiness.Commercial,
            readiness.Configuration,
            scopeReadiness.ProvisioningReadiness == BillingV2CartProvisioningReadiness.Ready
                && readiness.Configuration != BillingV2CartConfigurationReadiness.Blocked
                ? (readiness.Configuration == BillingV2CartConfigurationReadiness.Deferred
                    ? BillingV2CartProvisioningReadiness.Deferred
                    : BillingV2CartProvisioningReadiness.Ready)
                : BillingV2CartProvisioningReadiness.Blocked);
    }

    private static BillingV2CartIssue Issue(string code, BillingV2CartItem item, bool blocking)
        => new(code, blocking ? "error" : "info", item.Id, item.ServiceCode, code, blocking);

    private async Task<IReadOnlyList<BillingV2CartIssue>> ReadDependencyIssuesAsync(
        MySqlConnection connection, MySqlTransaction transaction, BillingV2Cart cart, CancellationToken cancellationToken)
    {
        var issues = new List<BillingV2CartIssue>();
        foreach (var item in cart.Items)
        {
            var dependencies = new List<(string RequiredId, string RequiredCode, string ScopeRelation, string TierRelation)>();
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = """
                SELECT required.id, required.code, dependency.scope_relation, dependency.tier_relation
                FROM billing_v2_service_dependencies dependency
                JOIN billing_v2_services required ON required.id = dependency.required_service_id
                WHERE dependency.service_id = @service_id AND dependency.status = 'active';
                """;
            command.Parameters.AddWithValue("@service_id", item.ServiceId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                dependencies.Add((Identifier(reader, 0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            }
            await reader.DisposeAsync();
            foreach (var dependency in dependencies)
            {
                var candidate = cart.Items.FirstOrDefault(value => value.ServiceCode == dependency.RequiredCode
                    && SameScope(item, value, dependency.ScopeRelation));
                var satisfied = candidate is not null && await DependencyTierMatchesAsync(
                    connection, transaction, item, candidate, dependency.TierRelation, cancellationToken);
                if (!satisfied)
                {
                    var resolution = candidate is null
                        ? await ResolveDeterministicTierAsync(connection, transaction,
                            dependency.RequiredId, item.TierId, dependency.TierRelation, cancellationToken)
                        : (Code: "CART_DEPENDENCY_REQUIRED", TierId: (string?)null);
                    var code = resolution.Code is "CART_DEPENDENCY_AMBIGUOUS" or "CART_DEPENDENCY_IMPOSSIBLE"
                        ? resolution.Code
                        : "CART_DEPENDENCY_REQUIRED";
                    issues.Add(new(code, "error", item.Id, item.ServiceCode,
                        $"Le service requis {dependency.RequiredCode} est absent ou incompatible.", true));
                }
            }
        }
        return issues;
    }

    private static async Task<bool> DependencyTierMatchesAsync(MySqlConnection connection,
        MySqlTransaction transaction, BillingV2CartItem dependent,
        BillingV2CartItem required, string relation, CancellationToken cancellationToken)
    {
        if (relation == "any") return true;
        if (dependent.TierId is null || required.TierId is null) return false;
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            SELECT dependent.numeric_value, required.numeric_value
            FROM billing_v2_service_tiers dependent
            JOIN billing_v2_service_tiers required
            WHERE dependent.id = @dependent AND required.id = @required;
            """;
        command.Parameters.AddWithValue("@dependent", dependent.TierId);
        command.Parameters.AddWithValue("@required", required.TierId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1)) return false;
        var dependentValue = reader.GetInt64(0); var requiredValue = reader.GetInt64(1);
        return relation switch
        {
            "same_numeric_value" => dependentValue == requiredValue,
            "dependent_gte_required" => dependentValue >= requiredValue,
            _ => false
        };
    }

    private static bool SameScope(BillingV2CartItem dependent, BillingV2CartItem required,
        string relation)
    {
        if (relation != "same_scope") return true;
        if (dependent.ScopeTemplate != required.ScopeTemplate) return false;
        // Deux targets individuelles non encore liees peuvent rester
        // differables ensemble. Des bindings explicites doivent en revanche
        // etre exactement coherents : une egalite sur le seul scope serait une
        // fuite de droit entre deux utilisateurs.
        return string.IsNullOrWhiteSpace(dependent.SubjectBinding)
            || string.IsNullOrWhiteSpace(required.SubjectBinding)
            || string.Equals(dependent.SubjectBinding, required.SubjectBinding,
                StringComparison.Ordinal);
    }

    private static bool SameScope(DependencyNode dependent, DependencyNode required,
        string relation)
        => SameScope(ToCartItem(dependent), ToCartItem(required), relation);

    private async Task<ResolvedItem> ResolveItemAsync(MySqlConnection connection,
        MySqlTransaction transaction, BillingV2Cart cart, BillingV2CartItemCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ServiceCode) || command.Quantity is < 1 or > 10000)
            return new("CART_ITEM_INVALID");
        // Les bornes de quantite existantes sont portees par les lignes de
        // preset. Le catalogue direct ne possede pas encore de policy de
        // volume par service : accepter 1..10000 reviendrait a inventer une
        // regle commerciale. Le direct est donc strictement unitaire tant
        // qu'une metadata catalogue explicite ne le rend pas quantifiable.
        if (string.Equals(command.Origin, BillingV2CartItemOrigins.Direct,
                StringComparison.Ordinal)
            && command.Quantity != 1)
        {
            return new("CART_DIRECT_QUANTITY_NOT_CONFIGURED");
        }
        await using var lookup = connection.CreateCommand(); lookup.Transaction = transaction;
        lookup.CommandText = """
            SELECT id, default_scope_type, pricing_model, public_visible,
                   self_service_orderable, public_ordering_mode, configuration_policy
            FROM billing_v2_services WHERE code = @code AND status = 'active';
            """;
        lookup.Parameters.AddWithValue("@code", command.ServiceCode.Trim());
        await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new("CART_SERVICE_INVALID");
        var serviceId = Identifier(reader, 0); var scope = reader.GetString(1); var tiered = reader.GetString(2) == "tiered";
        var visible = reader.GetBoolean(3); var selfService = reader.GetBoolean(4); var mode = reader.GetString(5);
        var configurationPolicy = reader.GetString(6);
        // Les verifications suivantes lancent d'autres commandes sur la meme
        // transaction. Fermer explicitement le reader avant tout lookup afin
        // de rester compatible avec MySqlConnector sans lecteurs multiples.
        await reader.DisposeAsync();
        if (!visible) return new("CART_SERVICE_NOT_PUBLIC");
        if (command.Origin == "direct"
            && (mode != BillingV2PublicOrderingModes.Direct || !selfService))
            return new("CART_DIRECT_NOT_ELIGIBLE");
        // Un tunnel technique existant (notamment lorsqu'une configuration
        // durable est requise) ne devient jamais une commande Cart generique
        // par le seul fait d'etre marque `direct`. Cette policy est lue en
        // base pour chaque commande, jamais deduite par le navigateur.
        if (command.Origin == "direct" && !string.Equals(configurationPolicy,
                BillingV2CartConfigurationPolicies.NotRequired,
                StringComparison.Ordinal))
            return new("CART_DIRECT_CONFIGURATION_REQUIRED");
        if (command.Origin is not "direct" and not "preset") return new("CART_ORIGIN_INVALID");
        string? tierId = null;
        if (tiered != !string.IsNullOrWhiteSpace(command.TierCode)) return new("CART_TIER_REQUIRED_OR_FORBIDDEN");
        if (command.TierCode is not null)
        {
            tierId = await LookupIdAsync(connection, transaction,
                "SELECT id FROM billing_v2_service_tiers WHERE service_id = @service_id AND code = @code AND status = 'active' AND public_selectable = 1;",
                command.TierCode.Trim(), cancellationToken, serviceId);
            if (tierId is null) return new("CART_TIER_INVALID");
        }
        if (command.Origin == BillingV2CartItemOrigins.Direct
            && !await HasCurrentInitialPriceAsync(connection, transaction,
                serviceId, tierId, cart.Currency, cancellationToken))
        {
            return new("CART_DIRECT_NOT_ELIGIBLE");
        }
        // Le catalogue et le Cart n'emploient pas le meme vocabulaire de
        // portee. Un ajout direct ne peut donc jamais persister la valeur
        // catalogue brute, ni faire confiance a un scope fourni par le
        // navigateur : sa portee est derivee exclusivement du catalogue.
        string scopeTemplate;
        if (command.Origin == BillingV2CartItemOrigins.Direct)
        {
            if (!BillingV2CatalogScopeTemplatePolicy.TryMapToCartTemplate(scope,
                    out scopeTemplate))
            {
                return new("CART_SCOPE_UNSUPPORTED");
            }
        }
        else
        {
            scopeTemplate = command.ScopeTemplate ?? scope;
        }
        if (command.Origin == "preset"
            && (mode != BillingV2PublicOrderingModes.OfferComponent
                || !await PresetOriginMatchesAsync(connection, transaction,
                    command, serviceId, tierId, scopeTemplate, cancellationToken)))
        {
            return new("CART_PRESET_COMPONENT_INVALID");
        }
        return new("CART_ITEM_OK", serviceId, tierId, scopeTemplate);
    }

    /// <summary>
    /// L'ajout direct ne doit pas creer une intention commercialisable dont
    /// aucun prix actif ne peut etre resolu. Le montant n'est pas lu ici pour
    /// etre expose : le quote passera ensuite par BillingV2PricingEngine.
    /// </summary>
    private static async Task<bool> HasCurrentInitialPriceAsync(
        MySqlConnection connection, MySqlTransaction transaction, string serviceId,
        string? tierId, string currency, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1
                FROM billing_v2_service_prices price
                WHERE price.service_id = @service_id
                  AND price.tier_id <=> @tier_id
                  AND price.currency = @currency
                  AND price.status = 'active'
                  AND price.charge_trigger = 'initial_subscription'
                  AND price.valid_from <= UTC_TIMESTAMP(6)
                  AND (price.valid_until IS NULL OR price.valid_until > UTC_TIMESTAMP(6))
            );
            """;
        command.Parameters.AddWithValue("@service_id", serviceId);
        command.Parameters.AddWithValue("@tier_id", (object?)tierId ?? DBNull.Value);
        command.Parameters.AddWithValue("@currency", currency);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task<bool> PresetOriginMatchesAsync(MySqlConnection connection,
        MySqlTransaction transaction, BillingV2CartItemCommand command,
        string serviceId, string? tierId, string scopeTemplate,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.SourcePresetId, out _)
            || !Guid.TryParse(command.SourcePresetItemId, out _)) return false;
        await using var check = connection.CreateCommand(); check.Transaction = transaction;
        check.CommandText = """
            SELECT COUNT(*)
            FROM billing_v2_offer_presets preset
            JOIN billing_v2_preset_items item ON item.preset_id = preset.id
            WHERE preset.id = @preset_id AND preset.status = 'active' AND preset.is_public = 1
              -- source_preset_item_id ancre une option commerciale. Un
              -- changement de palier reste possible seulement lorsqu'une
              -- ligne soeur du meme preset autorise explicitement ce palier.
              AND item.id = @preset_item_id
              AND EXISTS (
                  SELECT 1
                  FROM billing_v2_preset_items allowed
                  WHERE allowed.preset_id = preset.id
                    AND allowed.service_id = @service_id
                    AND allowed.scope_template = @scope
                    AND allowed.tier_id <=> @tier_id
              );
            """;
        check.Parameters.AddWithValue("@preset_id", command.SourcePresetId!);
        check.Parameters.AddWithValue("@preset_item_id", command.SourcePresetItemId!);
        check.Parameters.AddWithValue("@service_id", serviceId);
        check.Parameters.AddWithValue("@tier_id", (object?)tierId ?? DBNull.Value);
        check.Parameters.AddWithValue("@scope", scopeTemplate);
        return Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    /// <summary>Les contraintes de preset sont réévaluées côté serveur sur toute mutation.</summary>
    private static async Task<bool> IsPresetItemMutableAsync(MySqlConnection connection,
        MySqlTransaction transaction, string? presetItemId, bool removing,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(presetItemId)) return true;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT required_item, customer_editable FROM billing_v2_preset_items WHERE id = @id;";
        command.Parameters.AddWithValue("@id", presetItemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return false;
        var required = reader.GetBoolean(0);
        var editable = reader.GetBoolean(1);
        return editable && (!removing || !required);
    }

    private static async Task<string> AddDeterministicDependenciesAsync(MySqlConnection connection,
        MySqlTransaction transaction, BillingV2Cart cart, DependencyNode root,
        int displayOrder, DateTime now, CancellationToken cancellationToken)
    {
        const int maxDepth = 16;
        var known = cart.Items.Select(item => new DependencyNode(item.ServiceId, item.TierId,
            item.ScopeTemplate, item.SubjectBinding, item.Quantity)).ToList();
        known.Add(root);
        var work = new Queue<(DependencyNode Node, IReadOnlySet<string> Lineage, int Depth)>();
        work.Enqueue((root, new HashSet<string>(StringComparer.Ordinal) { root.ServiceId }, 0));

        while (work.Count > 0)
        {
            var current = work.Dequeue();
            if (current.Depth >= maxDepth) return "CART_DEPENDENCY_DEPTH_EXCEEDED";
            var dependencies = await ReadDependenciesAsync(connection, transaction,
                current.Node.ServiceId, cancellationToken);
            foreach (var dependency in dependencies)
            {
                if (current.Lineage.Contains(dependency.ServiceId)) return "CART_DEPENDENCY_CYCLE";
                var existing = known.FirstOrDefault(item => item.ServiceId == dependency.ServiceId
                    && SameScope(current.Node, item, dependency.ScopeRelation));
                if (existing is not null)
                {
                    if (!await DependencyTierMatchesAsync(connection, transaction,
                            ToCartItem(current.Node), ToCartItem(existing), dependency.TierRelation, cancellationToken))
                        return "CART_DEPENDENCY_IMPOSSIBLE";
                    continue;
                }
                var requiredTier = await ResolveDeterministicTierAsync(connection,
                    transaction, dependency.ServiceId, current.Node.TierId, dependency.TierRelation, cancellationToken);
                if (requiredTier.Code is "CART_DEPENDENCY_AMBIGUOUS" or "CART_DEPENDENCY_IMPOSSIBLE") continue;
                var next = new DependencyNode(dependency.ServiceId, requiredTier.TierId,
                    dependency.ScopeRelation == "same_scope" ? current.Node.ScopeTemplate : dependency.DefaultScope,
                    dependency.ScopeRelation == "same_scope" ? current.Node.SubjectBinding : null,
                    current.Node.Quantity);
                await InsertDependencyItemAsync(connection, transaction, cart.Id, next, displayOrder++, now, cancellationToken);
                known.Add(next);
                var lineage = new HashSet<string>(current.Lineage, StringComparer.Ordinal) { next.ServiceId };
                work.Enqueue((next, lineage, current.Depth + 1));
            }
        }
        return "CART_ITEM_ADDED";
    }

    private static async Task<(string Code, DependencyNode? Node)> AlignTierToExistingRequirementAsync(
        MySqlConnection connection, MySqlTransaction transaction, BillingV2Cart cart,
        DependencyNode node, CancellationToken token)
    {
        foreach (var dependency in await ReadDependenciesAsync(connection, transaction, node.ServiceId, token))
        {
            var required = cart.Items.FirstOrDefault(item => item.ServiceId == dependency.ServiceId
                && SameScope(node, new DependencyNode(item.ServiceId, item.TierId, item.ScopeTemplate, item.SubjectBinding, item.Quantity), dependency.ScopeRelation));
            if (required is null || dependency.TierRelation != "same_numeric_value") continue;
            var tier = await ResolveDeterministicTierAsync(connection, transaction, node.ServiceId, required.TierId, "same_numeric_value", token);
            if (tier.Code != "CART_DEPENDENCY_RESOLVED") return (tier.Code, null);
            node = node with { TierId = tier.TierId };
        }
        return ("CART_ITEM_OK", node);
    }

    private static async Task<bool> SynchronizeSameNumericDependentsAsync(MySqlConnection connection,
        MySqlTransaction transaction, BillingV2Cart cart, BillingV2CartItem required, CancellationToken token)
    {
        foreach (var dependent in cart.Items.Where(item => item.Id != required.Id))
        {
            var rules = await ReadDependenciesAsync(connection, transaction, dependent.ServiceId, token);
            if (!rules.Any(rule => rule.ServiceId == required.ServiceId && rule.TierRelation == "same_numeric_value"
                    && SameScope(new DependencyNode(dependent.ServiceId, dependent.TierId, dependent.ScopeTemplate, dependent.SubjectBinding, dependent.Quantity),
                        new DependencyNode(required.ServiceId, required.TierId, required.ScopeTemplate, required.SubjectBinding, required.Quantity), rule.ScopeRelation))) continue;
            var tier = await ResolveDeterministicTierAsync(connection, transaction, dependent.ServiceId, required.TierId, "same_numeric_value", token);
            if (tier.Code != "CART_DEPENDENCY_RESOLVED") return false;
            await using var update = connection.CreateCommand(); update.Transaction = transaction;
            update.CommandText = "UPDATE billing_v2_cart_items SET tier_id = @tier, updated_at = UTC_TIMESTAMP(6) WHERE id = @id AND cart_id = @cart;";
            update.Parameters.AddWithValue("@tier", tier.TierId!); update.Parameters.AddWithValue("@id", dependent.Id); update.Parameters.AddWithValue("@cart", cart.Id);
            if (await update.ExecuteNonQueryAsync(token) != 1) return false;
        }
        return true;
    }

    private static async Task<IReadOnlyList<DependencyDefinition>> ReadDependenciesAsync(
        MySqlConnection connection, MySqlTransaction transaction, string serviceId, CancellationToken cancellationToken)
    {
        var result = new List<DependencyDefinition>();
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            SELECT required.id, required.default_scope_type, dependency.scope_relation, dependency.tier_relation
            FROM billing_v2_service_dependencies dependency
            JOIN billing_v2_services required ON required.id = dependency.required_service_id
            WHERE dependency.service_id = @service_id AND dependency.status = 'active' AND required.status = 'active';
            """;
        command.Parameters.AddWithValue("@service_id", serviceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(Identifier(reader, 0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        return result;
    }

    private static async Task InsertDirectItemAsync(MySqlConnection connection,
        MySqlTransaction transaction, string cartId, ResolvedItem resolved,
        BillingV2CartItemCommand command, int displayOrder, DateTime now,
        CancellationToken cancellationToken)
    {
        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO billing_v2_cart_items
                (id, cart_id, service_id, tier_id, quantity, scope_template,
                 subject_binding, source_preset_item_id, origin,
                 configuration_kind, configuration_reference, display_order,
                 created_at, updated_at)
            VALUES (@id, @cart_id, @service_id, @tier_id, @quantity, @scope,
                    @subject, NULL, @origin, @configuration_kind,
                    @configuration_reference, @display_order, @now, @now);
            """;
        insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        insert.Parameters.AddWithValue("@cart_id", cartId);
        insert.Parameters.AddWithValue("@service_id", resolved.ServiceId!);
        insert.Parameters.AddWithValue("@tier_id", (object?)resolved.TierId ?? DBNull.Value);
        insert.Parameters.AddWithValue("@quantity", command.Quantity);
        insert.Parameters.AddWithValue("@scope", resolved.ScopeTemplate!);
        insert.Parameters.AddWithValue("@subject", (object?)command.SubjectBinding ?? DBNull.Value);
        insert.Parameters.AddWithValue("@origin", BillingV2CartItemOrigins.Direct);
        insert.Parameters.AddWithValue("@configuration_kind", (object?)command.ConfigurationKind ?? DBNull.Value);
        insert.Parameters.AddWithValue("@configuration_reference", (object?)command.ConfigurationReference ?? DBNull.Value);
        insert.Parameters.AddWithValue("@display_order", displayOrder);
        insert.Parameters.AddWithValue("@now", now);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Les services marques <c>mandatory_for_subscription</c> sont une policy
    /// globale Billing V2, pas une particularite de preset. Un ajout direct
    /// compose le socle serveur une seule fois par service/scope. Un socle
    /// tiered ne serait pas deterministe : il bloque plutot qu'etre devine.
    /// </summary>
    private static async Task<(string Code, int NextDisplayOrder)> EnsureMandatoryStructuralItemsAsync(
        MySqlConnection connection, MySqlTransaction transaction, BillingV2Cart cart,
        int displayOrder, DateTime now, CancellationToken cancellationToken)
    {
        var mandatory = new List<(string ServiceId, string ScopeTemplate, string PricingModel)>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT id, default_scope_type, pricing_model
                FROM billing_v2_services
                WHERE mandatory_for_subscription = 1 AND status = 'active'
                ORDER BY display_order, id;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                mandatory.Add((Identifier(reader, 0), reader.GetString(1), reader.GetString(2)));
        }

        foreach (var item in mandatory)
        {
            if (cart.Items.Any(existing => string.Equals(existing.ServiceId, item.ServiceId,
                    StringComparison.Ordinal)
                    && string.Equals(existing.ScopeTemplate, item.ScopeTemplate,
                        StringComparison.Ordinal)))
                continue;
            if (!string.Equals(item.PricingModel, "fixed", StringComparison.Ordinal))
                return ("CART_STRUCTURAL_ITEM_INVALID", displayOrder);

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO billing_v2_cart_items
                    (id, cart_id, service_id, tier_id, quantity, scope_template,
                     origin, display_order, created_at, updated_at)
                VALUES (@id, @cart_id, @service_id, NULL, 1, @scope,
                        @origin, @display_order, @now, @now);
                """;
            insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            insert.Parameters.AddWithValue("@cart_id", cart.Id);
            insert.Parameters.AddWithValue("@service_id", item.ServiceId);
            insert.Parameters.AddWithValue("@scope", item.ScopeTemplate);
            insert.Parameters.AddWithValue("@origin", BillingV2CartItemOrigins.Structural);
            insert.Parameters.AddWithValue("@display_order", displayOrder++);
            insert.Parameters.AddWithValue("@now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        return ("CART_ITEM_ADDED", displayOrder);
    }

    private static async Task InsertDependencyItemAsync(MySqlConnection connection, MySqlTransaction transaction,
        string cartId, DependencyNode item, int displayOrder, DateTime now, CancellationToken cancellationToken)
    {
        await using var insert = connection.CreateCommand(); insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO billing_v2_cart_items (id, cart_id, service_id, tier_id, quantity, scope_template,
                subject_binding, origin, display_order, created_at, updated_at)
            VALUES (@id, @cart_id, @service_id, @tier_id, @quantity, @scope, @subject, @origin, @display_order, @now, @now);
            """;
        insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D")); insert.Parameters.AddWithValue("@cart_id", cartId);
        insert.Parameters.AddWithValue("@service_id", item.ServiceId); insert.Parameters.AddWithValue("@tier_id", (object?)item.TierId ?? DBNull.Value);
        insert.Parameters.AddWithValue("@quantity", item.Quantity); insert.Parameters.AddWithValue("@scope", item.ScopeTemplate);
        insert.Parameters.AddWithValue("@subject", (object?)item.SubjectBinding ?? DBNull.Value);
        insert.Parameters.AddWithValue("@origin", BillingV2CartItemOrigins.Dependency);
        insert.Parameters.AddWithValue("@display_order", displayOrder); insert.Parameters.AddWithValue("@now", now);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(string Code, string? TierId)> ResolveDeterministicTierAsync(
        MySqlConnection connection, MySqlTransaction transaction, string requiredServiceId,
        string? dependentTierId, string relation, CancellationToken cancellationToken)
    {
        if (relation == "any")
        {
            await using var noTier = connection.CreateCommand(); noTier.Transaction = transaction;
            noTier.CommandText = """
                SELECT id FROM billing_v2_service_tiers
                WHERE service_id = @service_id AND status = 'active'
                ORDER BY display_order, id;
                """; noTier.Parameters.AddWithValue("@service_id", requiredServiceId);
            var tiers = new List<string>(); await using var tierReader = await noTier.ExecuteReaderAsync(cancellationToken);
            while (await tierReader.ReadAsync(cancellationToken)) tiers.Add(Identifier(tierReader, 0));
            if (tiers.Count == 0)
                return ("CART_DEPENDENCY_RESOLVED", null);
            return tiers.Count == 1
                ? ("CART_DEPENDENCY_RESOLVED", tiers[0])
                : ("CART_DEPENDENCY_AMBIGUOUS", null);
        }
        if (relation != "same_numeric_value" || dependentTierId is null)
            return ("CART_DEPENDENCY_AMBIGUOUS", null);
        await using var exact = connection.CreateCommand(); exact.Transaction = transaction;
        exact.CommandText = """
            SELECT required.id
            FROM billing_v2_service_tiers required
            JOIN billing_v2_service_tiers dependent ON dependent.id = @dependent_tier_id
            WHERE required.service_id = @required_service_id AND required.status = 'active'
              AND required.numeric_value = dependent.numeric_value;
            """;
        exact.Parameters.AddWithValue("@dependent_tier_id", dependentTierId);
        exact.Parameters.AddWithValue("@required_service_id", requiredServiceId);
        var found = new List<string>(); await using var reader = await exact.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) found.Add(Identifier(reader, 0));
        return found.Count switch
        {
            1 => ("CART_DEPENDENCY_RESOLVED", found[0]),
            0 => ("CART_DEPENDENCY_IMPOSSIBLE", null),
            _ => ("CART_DEPENDENCY_AMBIGUOUS", null)
        };
    }

    private async Task<BillingV2Cart?> ReadCurrentAsync(MySqlConnection connection, MySqlTransaction? transaction,
        BillingV2CartOwner owner, string currency, bool forUpdate, CancellationToken cancellationToken)
    {
        var query = owner.IsAuthenticated
            ? "SELECT id FROM billing_v2_carts WHERE customer_id = @owner AND open_customer_slot = 1 AND status = 'open' AND currency = @currency"
            : "SELECT id FROM billing_v2_carts WHERE anonymous_session_hash = @owner AND open_anonymous_slot = 1 AND status = 'open' AND currency = @currency";
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = query + (forUpdate ? " FOR UPDATE;" : ";");
        command.Parameters.AddWithValue("@owner", owner.IsAuthenticated ? owner.CustomerId! : owner.AnonymousTokenHash!);
        command.Parameters.AddWithValue("@currency", currency);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return id is null ? null : await ReadCartAsync(connection, transaction, owner, Identifier(id), forUpdate, cancellationToken);
    }

    private static async Task InsertOpenCartAsync(MySqlConnection connection, MySqlTransaction transaction,
        BillingV2CartOwner owner, string id, string currency, DateTime now, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO billing_v2_carts
                (id, customer_id, anonymous_session_hash, open_customer_slot,
                 open_anonymous_slot, status, currency, version,
                 created_at, updated_at, last_activity_at, expires_at)
            VALUES (@id, @customer, @anonymous, @customer_slot,
                    @anonymous_slot, 'open', @currency, 1,
                    @now, @now, @now, @expires);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@customer", (object?)owner.CustomerId ?? DBNull.Value);
        command.Parameters.AddWithValue("@anonymous", (object?)owner.AnonymousTokenHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@customer_slot", owner.IsAuthenticated ? 1 : DBNull.Value);
        command.Parameters.AddWithValue("@anonymous_slot", owner.IsAuthenticated ? DBNull.Value : 1);
        command.Parameters.AddWithValue("@currency", currency);
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.AddWithValue("@expires", now.AddDays(30));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<PresetDefinition?> ReadPresetAsync(MySqlConnection connection,
        MySqlTransaction transaction, string code, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT preset.id, item.id, service.id, service.code, tier.id, tier.code, item.scope_template,
                   item.quantity, item.required_item, item.customer_editable, item.selected_by_default,
                   item.minimum_quantity, item.maximum_quantity, item.display_order
            FROM billing_v2_offer_presets preset
            JOIN billing_v2_preset_items item ON item.preset_id = preset.id
            JOIN billing_v2_services service ON service.id = item.service_id
              AND service.status = 'active'
              -- Un socle requis peut etre volontairement absent de la
              -- vitrine comme service isole. Il reste toutefois une partie
              -- obligatoire de la formule et doit etre compose cote serveur.
              AND (service.public_visible = 1 OR item.required_item = 1)
            LEFT JOIN billing_v2_service_tiers tier ON tier.id = item.tier_id
              AND tier.status = 'active'
            WHERE preset.code = @code AND preset.status = 'active' AND preset.is_public = 1
            ORDER BY item.display_order, item.id;
            """;
        command.Parameters.AddWithValue("@code", code);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        string? presetId = null;
        var items = new List<BillingV2CartPresetCompositionItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            presetId ??= Identifier(reader, 0);
            items.Add(new(Identifier(reader, 1), Identifier(reader, 2), reader.GetString(3),
                NullableIdentifier(reader, 4), reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6), reader.GetInt32(7), reader.GetBoolean(8), reader.GetBoolean(9),
                reader.GetBoolean(10), reader.GetInt32(11), reader.GetInt32(12), reader.GetInt32(13)));
        }
        // L'invariant est aussi porte par 091, mais cette relecture reste
        // indispensable : une base ancienne ou une ecriture hors admin ne
        // doit jamais initialiser un Cart sans un composant requis.
        return presetId is null || items.Count == 0 || items.Any(item => item.RequiredItem && !item.SelectedByDefault)
            ? null
            : new(presetId, items);
    }

    private static async Task InsertPresetItemAsync(MySqlConnection connection, MySqlTransaction transaction,
        string cartId, BillingV2CartPresetCompositionItem item, int displayOrder, DateTime now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO billing_v2_cart_items
                (id, cart_id, service_id, tier_id, quantity, scope_template,
                 source_preset_item_id, origin, display_order, created_at, updated_at)
            VALUES (@id, @cart_id, @service_id, @tier_id, @quantity, @scope,
                    @preset_item_id, @origin, @display_order, @now, @now);
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("@cart_id", cartId);
        command.Parameters.AddWithValue("@service_id", item.ServiceId);
        command.Parameters.AddWithValue("@tier_id", (object?)item.TierId ?? DBNull.Value);
        command.Parameters.AddWithValue("@quantity", item.Quantity);
        command.Parameters.AddWithValue("@scope", item.ScopeTemplate);
        command.Parameters.AddWithValue("@preset_item_id", item.PresetItemId);
        command.Parameters.AddWithValue("@origin", BillingV2CartItemOrigins.Preset);
        command.Parameters.AddWithValue("@display_order", displayOrder);
        command.Parameters.AddWithValue("@now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Associe une ligne déjà présente à la définition qui l'autorise dans le
    /// preset courant. Cette association décrit une contribution de formule
    /// actuelle ; <c>origin</c> reste la provenance historique immuable de la
    /// ligne, y compris lorsqu'elle avait auparavant été créée comme
    /// dépendance.
    /// </summary>
    private static async Task AttachPresetDefinitionAsync(MySqlConnection connection,
        MySqlTransaction transaction, string cartId, string cartItemId, string presetItemId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE billing_v2_cart_items
            SET source_preset_item_id = COALESCE(source_preset_item_id, @preset_item_id),
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @item_id AND cart_id = @cart_id;
            """;
        command.Parameters.AddWithValue("@preset_item_id", presetItemId);
        command.Parameters.AddWithValue("@item_id", cartItemId);
        command.Parameters.AddWithValue("@cart_id", cartId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<BillingV2CartPresetCompositionItem?> ReadPresetItemAsync(MySqlConnection connection,
        MySqlTransaction transaction, string presetId, string presetItemId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT item.id, service.id, service.code, tier.id, tier.code, item.scope_template,
                   item.quantity, item.required_item, item.customer_editable, item.selected_by_default,
                   item.minimum_quantity, item.maximum_quantity, item.display_order
            FROM billing_v2_preset_items item
            JOIN billing_v2_services service ON service.id = item.service_id AND service.status = 'active'
            LEFT JOIN billing_v2_service_tiers tier ON tier.id = item.tier_id AND tier.status = 'active'
            WHERE item.id = @item_id AND item.preset_id = @preset_id;
            """;
        command.Parameters.AddWithValue("@item_id", presetItemId);
        command.Parameters.AddWithValue("@preset_id", presetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var item = new BillingV2CartPresetCompositionItem(Identifier(reader, 0), Identifier(reader, 1), reader.GetString(2), NullableIdentifier(reader, 3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetInt32(6),
                reader.GetBoolean(7), reader.GetBoolean(8), reader.GetBoolean(9), reader.GetInt32(10),
                reader.GetInt32(11), reader.GetInt32(12));
        return item.RequiredItem && !item.SelectedByDefault ? null : item;
    }

    private async Task<BillingV2Cart?> ReadAnyCurrentAsync(MySqlConnection connection, MySqlTransaction? transaction,
        BillingV2CartOwner owner, bool forUpdate, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = owner.IsAuthenticated
            ? "SELECT id FROM billing_v2_carts WHERE customer_id = @owner AND open_customer_slot = 1 AND status = 'open' ORDER BY created_at LIMIT 1"
            : "SELECT id FROM billing_v2_carts WHERE anonymous_session_hash = @owner AND open_anonymous_slot = 1 AND status = 'open' ORDER BY created_at LIMIT 1";
        if (forUpdate) command.CommandText += " FOR UPDATE;";
        command.Parameters.AddWithValue("@owner", owner.IsAuthenticated ? owner.CustomerId! : owner.AnonymousTokenHash!);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return id is null ? null : await ReadCartAsync(connection, transaction, owner, Identifier(id), forUpdate, cancellationToken);
    }

    private static async Task<string?> LookupIdAsync(MySqlConnection connection, MySqlTransaction transaction,
        string sql, string code, CancellationToken token, string? serviceId = null)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        command.Parameters.AddWithValue("@code", code);
        if (serviceId is not null) command.Parameters.AddWithValue("@service_id", serviceId);
        var value = await command.ExecuteScalarAsync(token); return value is null ? null : Identifier(value);
    }

    private static async Task UpdateCartFieldAsync(MySqlConnection connection, MySqlTransaction transaction,
        string cartId, string field, string? value, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = $"UPDATE billing_v2_carts SET {field} = @value WHERE id = @id;";
        command.Parameters.AddWithValue("@value", (object?)value ?? DBNull.Value); command.Parameters.AddWithValue("@id", cartId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<BillingV2Cart?> ReadCartAsync(MySqlConnection connection, MySqlTransaction? transaction,
        BillingV2CartOwner owner, string cartId, bool forUpdate, CancellationToken cancellationToken)
    {
        if (transaction is null)
            throw new InvalidOperationException("CART_READ_REQUIRES_TRANSACTION");
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            SELECT cart.id, cart.customer_id, cart.anonymous_session_hash, cart.status, cart.currency,
                   cart.commitment_term_id, term.code, cart.payment_mode, cart.source_preset_id, preset.code,
                   cart.version, cart.created_at, cart.updated_at, cart.last_activity_at, cart.expires_at,
                   cart.checked_out_subscription_id
            FROM billing_v2_carts cart LEFT JOIN billing_v2_commitment_terms term ON term.id = cart.commitment_term_id
            LEFT JOIN billing_v2_offer_presets preset ON preset.id = cart.source_preset_id
            WHERE cart.id = @id AND ((@customer IS NOT NULL AND cart.customer_id = @customer)
              OR (@anonymous IS NOT NULL AND cart.anonymous_session_hash = @anonymous))
            """ + (forUpdate ? " FOR UPDATE;" : ";");
        command.Parameters.AddWithValue("@id", cartId); command.Parameters.AddWithValue("@customer", (object?)owner.CustomerId ?? DBNull.Value);
        command.Parameters.AddWithValue("@anonymous", (object?)owner.AnonymousTokenHash ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var cart = new BillingV2Cart(Identifier(reader, 0), NullableIdentifier(reader, 1), reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3), reader.GetString(4), NullableIdentifier(reader, 5), reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7), NullableIdentifier(reader, 8),
            reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetInt32(10), reader.GetDateTime(11),
            reader.GetDateTime(12), reader.GetDateTime(13), reader.GetDateTime(14), NullableIdentifier(reader, 15), []);
        await reader.DisposeAsync();
        var definition = cart.SourcePresetId is null
            ? null
            : await ReadPresetDefinitionAsync(connection, transaction, cart.SourcePresetId, cancellationToken);
        var items = await ReadItemsAsync(connection, transaction, cart.Id, cancellationToken);
        var projected = await ProjectItemRolesAsync(connection, transaction,
            cart with { Items = items, PresetDefinition = definition }, cancellationToken);
        return cart with {
            Items = projected,
            PresetDefinition = definition
        };
    }

    private static async Task<IReadOnlyList<BillingV2CartPresetDefinitionItem>> ReadPresetDefinitionAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string presetId, CancellationToken cancellationToken)
    {
        var items = new List<BillingV2CartPresetDefinitionItem>();
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            SELECT item.id, service.code, tier.code, item.scope_template, item.quantity,
                   item.required_item, item.customer_editable, item.selected_by_default,
                   item.minimum_quantity, item.maximum_quantity, item.display_order
            FROM billing_v2_preset_items item
            JOIN billing_v2_services service ON service.id = item.service_id
            LEFT JOIN billing_v2_service_tiers tier ON tier.id = item.tier_id
            WHERE item.preset_id = @preset_id
            ORDER BY item.display_order, item.id;
            """;
        command.Parameters.AddWithValue("@preset_id", presetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(new(
            Identifier(reader, 0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3), reader.GetInt32(4), reader.GetBoolean(5), reader.GetBoolean(6),
            reader.GetBoolean(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10)));
        return items;
    }

    private static async Task<IReadOnlyList<BillingV2CartItem>> ReadItemsAsync(MySqlConnection connection,
        MySqlTransaction? transaction, string cartId, CancellationToken cancellationToken)
    {
        var result = new List<BillingV2CartItem>(); await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            SELECT item.id, item.cart_id, item.service_id, service.code, item.tier_id, tier.code,
                   item.quantity, item.scope_template, item.subject_binding, item.source_preset_item_id,
                   preset_item.required_item, preset_item.customer_editable,
                   item.origin, service.mandatory_for_subscription, service.public_visible,
                   service.self_service_orderable, service.public_ordering_mode,
                   service.configuration_policy,
                   preset_item.minimum_quantity, preset_item.maximum_quantity,
                   item.configuration_kind, item.configuration_reference, item.display_order, item.created_at, item.updated_at
            FROM billing_v2_cart_items item JOIN billing_v2_services service ON service.id = item.service_id
            LEFT JOIN billing_v2_service_tiers tier ON tier.id = item.tier_id
            LEFT JOIN billing_v2_preset_items preset_item ON preset_item.id = item.source_preset_item_id
            WHERE item.cart_id = @cart_id ORDER BY item.display_order, item.id;
            """; command.Parameters.AddWithValue("@cart_id", cartId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var sourcePresetItemId = NullableIdentifier(reader, 9);
            var isStructural = reader.GetBoolean(13);
            var origin = reader.IsDBNull(12)
                ? sourcePresetItemId is not null
                    ? BillingV2CartItemOrigins.Preset
                    : BillingV2CartItemOrigins.Legacy
                : reader.GetString(12);
            var directEditable = reader.GetBoolean(14) && reader.GetBoolean(15)
                && string.Equals(reader.GetString(16), BillingV2PublicOrderingModes.Direct,
                    StringComparison.Ordinal)
                && string.Equals(reader.GetString(17),
                    BillingV2CartConfigurationPolicies.NotRequired,
                    StringComparison.Ordinal);
            bool? editable = sourcePresetItemId is not null
                ? (reader.IsDBNull(11) ? null : reader.GetBoolean(11))
                : directEditable;
            var bounds = sourcePresetItemId is null
                ? (Minimum: 1, Maximum: 1)
                : (Minimum: reader.IsDBNull(18) ? 1 : reader.GetInt32(18),
                    Maximum: reader.IsDBNull(19) ? 10000 : reader.GetInt32(19));
            result.Add(new(Identifier(reader, 0), Identifier(reader, 1), Identifier(reader, 2), reader.GetString(3),
                NullableIdentifier(reader, 4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetInt32(6), reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8), sourcePresetItemId,
                reader.IsDBNull(10) ? null : reader.GetBoolean(10), editable, origin,
                isStructural, false, false, false, false, false, false, null,
                bounds.Minimum, bounds.Maximum,
                reader.IsDBNull(20) ? null : reader.GetString(20), reader.IsDBNull(21) ? null : reader.GetString(21),
                reader.GetInt32(22), reader.GetDateTime(23), reader.GetDateTime(24)));
        }
        return result;
    }

    /// <summary>
    /// Projette les rôles courants depuis la composition réelle et les policies
    /// actives. <c>origin</c> reste immuable : il décrit uniquement la première
    /// provenance connue d'une ligne et ne décide jamais seul d'un droit de
    /// retrait, d'édition, de comptage ou d'affichage.
    /// </summary>
    private async Task<IReadOnlyList<BillingV2CartItem>> ProjectItemRolesAsync(
        MySqlConnection connection, MySqlTransaction transaction, BillingV2Cart cart,
        CancellationToken cancellationToken)
    {
        var requiredByPreset = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in cart.PresetDefinition?.Where(item => item.RequiredItem) ?? [])
        {
            var candidate = cart.Items.OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault(item => string.Equals(item.SourcePresetItemId,
                        definition.PresetItemId, StringComparison.Ordinal)
                    || MatchesCurrentPresetDefinition(item, definition));
            if (candidate is not null) requiredByPreset.Add(candidate.Id);
        }

        var requiredByDependency = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dependent in cart.Items)
        {
            foreach (var dependency in await ReadDependenciesAsync(connection, transaction,
                         dependent.ServiceId, cancellationToken))
            {
                var candidates = cart.Items.Where(candidate =>
                        string.Equals(candidate.ServiceId, dependency.ServiceId,
                            StringComparison.Ordinal)
                        && SameScope(new DependencyNode(dependent.ServiceId,
                                dependent.TierId, dependent.ScopeTemplate,
                                dependent.SubjectBinding, dependent.Quantity),
                            new DependencyNode(candidate.ServiceId, candidate.TierId,
                                candidate.ScopeTemplate, candidate.SubjectBinding,
                                candidate.Quantity), dependency.ScopeRelation))
                    .OrderBy(candidate => candidate.DisplayOrder)
                    .ThenBy(candidate => candidate.Id, StringComparer.Ordinal);
                foreach (var candidate in candidates)
                {
                    if (await DependencyTierMatchesAsync(connection, transaction,
                            dependent, candidate, dependency.TierRelation,
                            cancellationToken))
                    {
                        requiredByDependency.Add(candidate.Id);
                        break;
                    }
                }
            }
        }

        return cart.Items.Select(item =>
        {
            var structural = item.IsStructural;
            var presetRequired = requiredByPreset.Contains(item.Id);
            var dependencyRequired = requiredByDependency.Contains(item.Id);
            var role = BillingV2CartPolicy.ResolveCurrentItemRole(item.Origin,
                structural, presetRequired, dependencyRequired,
                item.SourcePresetItemId is not null, item.CustomerEditable == true);
            return item with
            {
                IsRequiredByPreset = role.IsRequiredByPreset,
                IsRequiredByDependency = role.IsRequiredByDependency,
                IsExplicitCommercialSelection = role.IsExplicitCommercialSelection,
                CanEdit = role.CanEdit,
                CanRemove = role.CanRemove,
                CountsAsCommercialSelection = role.CountsAsCommercialSelection,
                DisplayReason = role.DisplayReason
            };
        }).ToArray();
    }

    private static bool MatchesCurrentPresetDefinition(BillingV2CartItem item,
        BillingV2CartPresetDefinitionItem definition)
        => string.Equals(item.ServiceCode, definition.ServiceCode, StringComparison.Ordinal)
            && string.Equals(item.TierCode, definition.TierCode, StringComparison.Ordinal)
            && item.Quantity == definition.DefaultQuantity
            && string.Equals(item.ScopeTemplate, definition.ScopeTemplate,
                StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(item.SubjectBinding);

    private async Task<ServiceMetadata?> ReadServiceMetadataAsync(MySqlConnection connection, MySqlTransaction transaction, string serviceId, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT configuration_policy, configuration_price_stable_without_reference, default_scope_type FROM billing_v2_services WHERE id = @id;";
        command.Parameters.AddWithValue("@id", serviceId); await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new(reader.GetString(0), reader.GetBoolean(1), reader.GetString(2)) : null;
    }

    private static async Task<string?> ReadFeeDeduplicationKeyAsync(MySqlConnection connection, MySqlTransaction transaction, string priceId, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT fee_deduplication_key FROM billing_v2_service_prices WHERE id = @id;"; command.Parameters.AddWithValue("@id", priceId);
        var value = await command.ExecuteScalarAsync(token); return value is null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    private static async Task<(string? Code, int Months, int DiscountBasisPoints)> ReadCommitmentAsync(MySqlConnection connection, MySqlTransaction transaction, BillingV2Cart cart, CancellationToken token)
    {
        if (cart.CommitmentTermId is null) return (null, 1, 0);
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            SELECT term.code, term.commitment_months, option_row.discount_basis_points
            FROM billing_v2_commitment_terms term JOIN billing_v2_commitment_payment_options option_row
              ON option_row.commitment_term_id = term.id AND option_row.payment_mode = @mode AND option_row.status = 'active'
            WHERE term.id = @id AND term.status = 'active';
            """; command.Parameters.AddWithValue("@id", cart.CommitmentTermId); command.Parameters.AddWithValue("@mode", cart.PaymentMode ?? BillingV2PaymentModes.Monthly);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? (reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)) : (null, 1, 0);
    }

    private static async Task<int> NextQuoteVersionAsync(MySqlConnection connection, MySqlTransaction transaction, string cartId, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(quote_version), 0) + 1 FROM billing_v2_cart_quotes WHERE cart_id = @id;"; command.Parameters.AddWithValue("@id", cartId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(token));
    }

    private static async Task StoreQuoteAsync(MySqlConnection connection, MySqlTransaction transaction, BillingV2CartQuote quote, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO billing_v2_cart_quotes (id, cart_id, cart_version, quote_version, composition_fingerprint, currency, quote_json, calculated_at, expires_at)
            VALUES (@id, @cart_id, @cart_version, @quote_version, @fingerprint, @currency, @json, @calculated, @expires);
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("@cart_id", quote.CartId);
        command.Parameters.AddWithValue("@cart_version", quote.CartVersion); command.Parameters.AddWithValue("@quote_version", quote.QuoteVersion);
        command.Parameters.AddWithValue("@fingerprint", quote.CompositionFingerprint); command.Parameters.AddWithValue("@currency", quote.Currency);
        command.Parameters.AddWithValue("@json", JsonSerializer.Serialize(quote)); command.Parameters.AddWithValue("@calculated", quote.CalculatedAtUtc); command.Parameters.AddWithValue("@expires", quote.ExpiresAtUtc);
        await command.ExecuteNonQueryAsync(token);
    }

    private static Task ExpireInactiveCurrentAsync(MySqlConnection connection, MySqlTransaction transaction,
        BillingV2CartOwner owner, string currency, DateTime now, CancellationToken token)
        => ExpireInactiveOwnedCartsAsync(connection, transaction, owner, now, token, currency);

    /// <summary>
    /// Cleanup borne a un proprietaire, et facultativement a une devise. Il ne
    /// doit jamais balayer les Carts open de tous les visiteurs dans le chemin
    /// transactionnel d'une commande. Les slots sont liberes dans la meme
    /// ecriture que l'expiration.
    /// </summary>
    private static async Task ExpireInactiveOwnedCartsAsync(MySqlConnection connection, MySqlTransaction transaction,
        BillingV2CartOwner owner, DateTime now, CancellationToken token, string? currency = null)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        var ownerColumn = owner.IsAuthenticated ? "customer_id" : "anonymous_session_hash";
        var openSlot = owner.IsAuthenticated ? "open_customer_slot" : "open_anonymous_slot";
        command.CommandText = $"""
            UPDATE billing_v2_carts
            SET status = 'expired', open_customer_slot = NULL,
                open_anonymous_slot = NULL, updated_at = @now
            WHERE status = 'open' AND expires_at <= @now
              AND {ownerColumn} = @owner AND {openSlot} = 1
              {(currency is null ? string.Empty : "AND currency = @currency")};
            """;
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.AddWithValue("@owner", owner.IsAuthenticated ? owner.CustomerId! : owner.AnonymousTokenHash!);
        if (currency is not null) command.Parameters.AddWithValue("@currency", currency);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task ExpireInactiveCartAsync(MySqlConnection connection, MySqlTransaction transaction,
        BillingV2CartOwner owner, string cartId, DateTime now, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        var ownerColumn = owner.IsAuthenticated ? "customer_id" : "anonymous_session_hash";
        command.CommandText = $"""
            UPDATE billing_v2_carts
            SET status = 'expired', open_customer_slot = NULL,
                open_anonymous_slot = NULL, updated_at = @now
            WHERE id = @id AND status = 'open' AND expires_at <= @now
              AND {ownerColumn} = @owner;
            """;
        command.Parameters.AddWithValue("@id", cartId);
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.AddWithValue("@owner", owner.IsAuthenticated ? owner.CustomerId! : owner.AnonymousTokenHash!);
        await command.ExecuteNonQueryAsync(token);
    }

    private static bool IsDeadlock(MySqlException exception)
        => exception.Number == 1213
            || string.Equals(exception.SqlState, "40001", StringComparison.Ordinal);

    private static bool IsDuplicateKey(MySqlException exception)
        => exception.Number == 1062;

    private static bool IsCurrentTransactionRetryable(MySqlException exception)
        // Les collisions 1062 du current sont traitees localement par la
        // relecture du gagnant. Seuls les deadlocks/serialization failures
        // justifient de rejouer toute la transaction.
        => IsDeadlock(exception);

    private static async Task TouchCartActivityAsync(MySqlConnection connection,
        MySqlTransaction transaction, string cartId, DateTime now, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE billing_v2_carts
            SET updated_at = @now, last_activity_at = @now, expires_at = @expires
            WHERE id = @id AND status = 'open';
            """;
        command.Parameters.AddWithValue("@id", cartId);
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.AddWithValue("@expires", now.AddDays(30));
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task<MySqlConnection> OpenReadyAsync(CancellationToken token)
    {
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString)) throw new InvalidOperationException("BILLING_V2_CART_STORAGE_UNAVAILABLE");
        var connection = new MySqlConnection(_sql.ConnectionString); await connection.OpenAsync(token);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('billing_v2_carts','billing_v2_cart_items','billing_v2_cart_quotes','billing_v2_services','billing_v2_service_tiers','billing_v2_service_prices','billing_v2_service_dependencies','billing_v2_commitment_terms','billing_v2_commitment_payment_options','billing_v2_offer_presets','billing_v2_preset_items');";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(token)) != RequiredTables.Length) { await connection.DisposeAsync(); throw new InvalidOperationException("BILLING_V2_CART_SCHEMA_UNAVAILABLE"); }
        command.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND ((table_name = 'billing_v2_preset_items' AND column_name IN ('selected_by_default','minimum_quantity','maximum_quantity')) OR (table_name = 'billing_v2_cart_items' AND column_name = 'origin'));";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(token)) != 4) { await connection.DisposeAsync(); throw new InvalidOperationException("BILLING_V2_CART_SCHEMA_UNAVAILABLE"); }
        return connection;
    }

    private async Task<string?> ReadPresetCodeAsync(string presetId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT code FROM billing_v2_offer_presets WHERE id = @id AND status = 'active' AND is_public = 1;";
        command.Parameters.AddWithValue("@id", presetId);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static bool IsCurrency(string value) => value.Length == 3 && value.All(char.IsAsciiLetter);
    private static string Identifier(object value) => value switch { Guid id => id.ToString("D"), byte[] bytes when bytes.Length == 16 => new Guid(bytes).ToString("D"), _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty };
    private static string Identifier(MySqlDataReader reader, int ordinal) => Identifier(reader.GetValue(ordinal));
    private static string? NullableIdentifier(MySqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Identifier(reader, ordinal);
    private sealed record ResolvedItem(string Code, string? ServiceId = null, string? TierId = null, string? ScopeTemplate = null);
    private sealed record ServiceMetadata(string ConfigurationPolicy, bool PriceStableWithoutConfiguration, string DefaultScopeType);
    private sealed record DependencyDefinition(string ServiceId, string DefaultScope, string ScopeRelation, string TierRelation);
    private sealed record PresetDefinition(string Id, IReadOnlyList<BillingV2CartPresetCompositionItem> Items);
    private sealed record DependencyNode(string ServiceId, string? TierId, string ScopeTemplate, string? SubjectBinding, int Quantity);

    private static BillingV2CartItem ToCartItem(DependencyNode node)
        => new(node.ServiceId, "dependency", node.ServiceId, "dependency", node.TierId,
            null, node.Quantity, node.ScopeTemplate, node.SubjectBinding, null, null, false,
            BillingV2CartItemOrigins.Dependency, false, false, false, false, false, false,
            false, null, 1, 1, null, null, 0,
            DateTime.UnixEpoch, DateTime.UnixEpoch);
}
