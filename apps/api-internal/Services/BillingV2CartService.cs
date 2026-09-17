using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public interface IBillingV2CartService
{
    Task<BillingV2CartMutationResult> GetOrCreateCurrentAsync(BillingV2CartOwner owner, string currency, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> GetAsync(BillingV2CartOwner owner, string cartId, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> AddItemAsync(BillingV2CartOwner owner, string cartId, int expectedVersion, BillingV2CartItemCommand command, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> UpdateItemAsync(BillingV2CartOwner owner, string cartId, string itemId, int expectedVersion, BillingV2CartItemCommand command, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> RemoveItemAsync(BillingV2CartOwner owner, string cartId, string itemId, int expectedVersion, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> SetCommitmentAsync(BillingV2CartOwner owner, string cartId, int expectedVersion, string? commitmentCode, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> SetPaymentModeAsync(BillingV2CartOwner owner, string cartId, int expectedVersion, string? paymentMode, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> QuoteAsync(BillingV2CartOwner owner, string cartId, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> ExpireAsync(BillingV2CartOwner owner, string cartId, int expectedVersion, CancellationToken cancellationToken);
    Task<BillingV2CartMutationResult> ClaimAsync(string anonymousToken, string customerId, int expectedVersion, CancellationToken cancellationToken);
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
        await using var connection = await OpenReadyAsync(cancellationToken);
        var now = DateTime.UtcNow;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExpireInactiveAsync(connection, transaction, now, cancellationToken);
        var cart = await ReadCurrentAsync(connection, transaction, owner, currency, true, cancellationToken);
        if (cart is null)
        {
            var id = Guid.NewGuid().ToString("D");
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
            command.Parameters.AddWithValue("@currency", currency.ToUpperInvariant());
            command.Parameters.AddWithValue("@now", now);
            command.Parameters.AddWithValue("@expires", now.AddDays(30));
            await command.ExecuteNonQueryAsync(cancellationToken);
            cart = await ReadCartAsync(connection, transaction, owner, id, false, cancellationToken);
        }
        else
        {
            await TouchCartActivityAsync(connection, transaction, cart.Id, now, cancellationToken);
            cart = await ReadCartAsync(connection, transaction, owner, cart.Id, false, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new("CART_OK", cart);
    }

    public async Task<BillingV2CartMutationResult> GetAsync(BillingV2CartOwner owner,
        string cartId, CancellationToken cancellationToken)
    {
        if (!owner.IsValid || !Guid.TryParse(cartId, out _)) return new("CART_NOT_FOUND");
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExpireInactiveAsync(connection, transaction, DateTime.UtcNow, cancellationToken);
        var cart = await ReadCartAsync(connection, transaction, owner, cartId, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return cart is null ? new("CART_NOT_FOUND") : new("CART_OK", cart);
    }

    public Task<BillingV2CartMutationResult> AddItemAsync(BillingV2CartOwner owner,
        string cartId, int expectedVersion, BillingV2CartItemCommand command,
        CancellationToken cancellationToken)
        => MutateAsync(owner, cartId, expectedVersion, async (connection, transaction, cart, now, token) =>
        {
            var resolved = await ResolveItemAsync(connection, transaction, cart, command, token);
            if (resolved.Code != "CART_ITEM_OK") return resolved.Code;
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO billing_v2_cart_items
                    (id, cart_id, service_id, tier_id, quantity, scope_template,
                     subject_binding, source_preset_item_id, configuration_kind,
                     configuration_reference, display_order, created_at, updated_at)
                VALUES (@id, @cart_id, @service_id, @tier_id, @quantity, @scope,
                        @subject, @preset_item, @configuration_kind,
                        @configuration_reference, @display_order, @now, @now);
                """;
            insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
            insert.Parameters.AddWithValue("@cart_id", cart.Id);
            insert.Parameters.AddWithValue("@service_id", resolved.ServiceId!);
            insert.Parameters.AddWithValue("@tier_id", (object?)resolved.TierId ?? DBNull.Value);
            insert.Parameters.AddWithValue("@quantity", command.Quantity);
            insert.Parameters.AddWithValue("@scope", resolved.ScopeTemplate!);
            insert.Parameters.AddWithValue("@subject", (object?)command.SubjectBinding ?? DBNull.Value);
            insert.Parameters.AddWithValue("@preset_item", (object?)command.SourcePresetItemId ?? DBNull.Value);
            insert.Parameters.AddWithValue("@configuration_kind", (object?)command.ConfigurationKind ?? DBNull.Value);
            insert.Parameters.AddWithValue("@configuration_reference", (object?)command.ConfigurationReference ?? DBNull.Value);
            insert.Parameters.AddWithValue("@display_order", cart.Items.Count + 1);
            insert.Parameters.AddWithValue("@now", now);
            await insert.ExecuteNonQueryAsync(token);
            // Une dependance ne devient automatique que lorsque le catalogue
            // donne une resolution unique. Sinon elle reste une issue
            // structuree dans le quote, jamais un choix arbitraire du client.
            return await AddDeterministicDependenciesAsync(connection, transaction, cart,
                new DependencyNode(resolved.ServiceId!, resolved.TierId,
                    resolved.ScopeTemplate!, command.SubjectBinding, command.Quantity),
                cart.Items.Count + 2, now, token);
        }, cancellationToken);

    public Task<BillingV2CartMutationResult> UpdateItemAsync(BillingV2CartOwner owner,
        string cartId, string itemId, int expectedVersion, BillingV2CartItemCommand command,
        CancellationToken cancellationToken)
        => MutateAsync(owner, cartId, expectedVersion, async (connection, transaction, cart, now, token) =>
        {
            if (!cart.Items.Any(item => string.Equals(item.Id, itemId, StringComparison.Ordinal)))
                return "CART_ITEM_NOT_FOUND";
            var resolved = await ResolveItemAsync(connection, transaction, cart, command, token);
            if (resolved.Code != "CART_ITEM_OK") return resolved.Code;
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
            return "CART_ITEM_UPDATED";
        }, cancellationToken);

    public Task<BillingV2CartMutationResult> RemoveItemAsync(BillingV2CartOwner owner,
        string cartId, string itemId, int expectedVersion, CancellationToken cancellationToken)
        => MutateAsync(owner, cartId, expectedVersion, async (connection, transaction, cart, _, token) =>
        {
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

    public async Task<BillingV2CartMutationResult> ClaimAsync(string anonymousToken,
        string customerId, int expectedVersion, CancellationToken cancellationToken)
    {
        var anonymous = new BillingV2CartOwner(null, anonymousToken);
        if (!anonymous.IsValid || !Guid.TryParse(customerId, out _)) return new("CART_CLAIM_INVALID");
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExpireInactiveAsync(connection, transaction, DateTime.UtcNow, cancellationToken);
        var cart = await ReadAnyCurrentAsync(connection, transaction, anonymous, true, cancellationToken);
        if (cart is null) return new("CART_NOT_FOUND");
        if (cart.Version != expectedVersion) return new("CART_VERSION_CONFLICT", cart);
        var customerOwner = new BillingV2CartOwner(customerId, null);
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

    private async Task<BillingV2CartMutationResult> MutateAsync(BillingV2CartOwner owner,
        string cartId, int expectedVersion,
        Func<MySqlConnection, MySqlTransaction, BillingV2Cart, DateTime, CancellationToken, Task<string>> mutation,
        CancellationToken cancellationToken, bool touch = true)
    {
        if (!owner.IsValid || !Guid.TryParse(cartId, out _)) return new("CART_NOT_FOUND");
        await using var connection = await OpenReadyAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        await ExpireInactiveAsync(connection, transaction, now, cancellationToken);
        var cart = await ReadCartAsync(connection, transaction, owner, cartId, true, cancellationToken);
        if (cart is null) return new("CART_NOT_FOUND");
        if (cart.Status != BillingV2CartStatuses.Open) return new("CART_IMMUTABLE", cart);
        if (cart.Version != expectedVersion) return new("CART_VERSION_CONFLICT", cart);
        var code = await mutation(connection, transaction, cart, now, cancellationToken);
        if (!code.EndsWith("ADDED", StringComparison.Ordinal) && !code.EndsWith("UPDATED", StringComparison.Ordinal)
            && code is not "CART_ITEM_REMOVED" and not "CART_EXPIRED")
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
            var components = item.TierCode is null ? service.FlatComponents : service.Tiers
                .FirstOrDefault(tier => tier.Code == item.TierCode)?.Components;
            if (components is null) { issues.Add(Issue("CART_TIER_UNAVAILABLE", item, true)); continue; }
            var metadata = await ReadServiceMetadataAsync(connection, transaction, item.ServiceId, cancellationToken);
            if (metadata is null) { issues.Add(Issue("CART_SERVICE_UNAVAILABLE", item, true)); continue; }
            configurationCandidates.Add(new(item.Id, item.ServiceCode, metadata.ConfigurationPolicy,
                item.ConfigurationReference, metadata.PriceStableWithoutConfiguration));
            scopeCandidates.Add(new(item.Id, item.ServiceCode, item.ScopeTemplate,
                metadata.DefaultScopeType, item.SubjectBinding));
            foreach (var component in components.Where(component => component.AppliesToInitialSubscription))
            {
                if (string.IsNullOrWhiteSpace(component.ServicePriceId)) { issues.Add(Issue("CART_PRICE_UNAVAILABLE", item, true)); continue; }
                priceLines.Add(new(item.Id, item.ServiceCode, item.TierCode, component.ServicePriceId,
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
            dependencyIssues, scopeReadiness.Issues,
            readiness.Issues.Concat(issues.Where(issue => issue.Code.StartsWith("CART_", StringComparison.Ordinal))).ToArray(),
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
        await using var lookup = connection.CreateCommand(); lookup.Transaction = transaction;
        lookup.CommandText = """
            SELECT id, default_scope_type, pricing_model, public_visible,
                   self_service_orderable, public_ordering_mode
            FROM billing_v2_services WHERE code = @code AND status = 'active';
            """;
        lookup.Parameters.AddWithValue("@code", command.ServiceCode.Trim());
        await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new("CART_SERVICE_INVALID");
        var serviceId = Identifier(reader, 0); var scope = reader.GetString(1); var tiered = reader.GetString(2) == "tiered";
        var visible = reader.GetBoolean(3); var selfService = reader.GetBoolean(4); var mode = reader.GetString(5);
        if (!visible) return new("CART_SERVICE_NOT_PUBLIC");
        if (command.Origin == "direct" && (mode != BillingV2PublicOrderingModes.Direct || !selfService)) return new("CART_DIRECT_NOT_ELIGIBLE");
        if (command.Origin == "preset")
        {
            if (mode != BillingV2PublicOrderingModes.OfferComponent
                || !await PresetOriginMatchesAsync(connection, transaction,
                    command, serviceId, cancellationToken))
            {
                return new("CART_PRESET_COMPONENT_INVALID");
            }
        }
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
        return new("CART_ITEM_OK", serviceId, tierId, command.ScopeTemplate ?? scope);
    }

    private static async Task<bool> PresetOriginMatchesAsync(MySqlConnection connection,
        MySqlTransaction transaction, BillingV2CartItemCommand command,
        string serviceId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.SourcePresetId, out _)
            || !Guid.TryParse(command.SourcePresetItemId, out _)) return false;
        await using var check = connection.CreateCommand(); check.Transaction = transaction;
        check.CommandText = """
            SELECT COUNT(*)
            FROM billing_v2_offer_presets preset
            JOIN billing_v2_preset_items item ON item.preset_id = preset.id
            WHERE preset.id = @preset_id AND preset.status = 'active' AND preset.is_public = 1
              AND item.id = @preset_item_id AND item.service_id = @service_id;
            """;
        check.Parameters.AddWithValue("@preset_id", command.SourcePresetId!);
        check.Parameters.AddWithValue("@preset_item_id", command.SourcePresetItemId!);
        check.Parameters.AddWithValue("@service_id", serviceId);
        return Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) == 1;
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

    private static async Task InsertDependencyItemAsync(MySqlConnection connection, MySqlTransaction transaction,
        string cartId, DependencyNode item, int displayOrder, DateTime now, CancellationToken cancellationToken)
    {
        await using var insert = connection.CreateCommand(); insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO billing_v2_cart_items (id, cart_id, service_id, tier_id, quantity, scope_template,
                subject_binding, display_order, created_at, updated_at)
            VALUES (@id, @cart_id, @service_id, @tier_id, @quantity, @scope, @subject, @display_order, @now, @now);
            """;
        insert.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D")); insert.Parameters.AddWithValue("@cart_id", cartId);
        insert.Parameters.AddWithValue("@service_id", item.ServiceId); insert.Parameters.AddWithValue("@tier_id", (object?)item.TierId ?? DBNull.Value);
        insert.Parameters.AddWithValue("@quantity", item.Quantity); insert.Parameters.AddWithValue("@scope", item.ScopeTemplate);
        insert.Parameters.AddWithValue("@subject", (object?)item.SubjectBinding ?? DBNull.Value); insert.Parameters.AddWithValue("@display_order", displayOrder); insert.Parameters.AddWithValue("@now", now);
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
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            SELECT cart.id, cart.customer_id, cart.anonymous_session_hash, cart.status, cart.currency,
                   cart.commitment_term_id, term.code, cart.payment_mode, cart.source_preset_id,
                   cart.version, cart.created_at, cart.updated_at, cart.last_activity_at, cart.expires_at,
                   cart.checked_out_subscription_id
            FROM billing_v2_carts cart LEFT JOIN billing_v2_commitment_terms term ON term.id = cart.commitment_term_id
            WHERE cart.id = @id AND ((@customer IS NOT NULL AND cart.customer_id = @customer)
              OR (@anonymous IS NOT NULL AND cart.anonymous_session_hash = @anonymous))
            """ + (forUpdate ? " FOR UPDATE;" : ";");
        command.Parameters.AddWithValue("@id", cartId); command.Parameters.AddWithValue("@customer", (object?)owner.CustomerId ?? DBNull.Value);
        command.Parameters.AddWithValue("@anonymous", (object?)owner.AnonymousTokenHash ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var cart = new BillingV2Cart(Identifier(reader, 0), NullableIdentifier(reader, 1), reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3), reader.GetString(4), NullableIdentifier(reader, 5), reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7), NullableIdentifier(reader, 8), reader.GetInt32(9), reader.GetDateTime(10),
            reader.GetDateTime(11), reader.GetDateTime(12), reader.GetDateTime(13), NullableIdentifier(reader, 14), []);
        await reader.DisposeAsync();
        return cart with { Items = await ReadItemsAsync(connection, transaction, cart.Id, cancellationToken) };
    }

    private static async Task<IReadOnlyList<BillingV2CartItem>> ReadItemsAsync(MySqlConnection connection,
        MySqlTransaction? transaction, string cartId, CancellationToken cancellationToken)
    {
        var result = new List<BillingV2CartItem>(); await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            SELECT item.id, item.cart_id, item.service_id, service.code, item.tier_id, tier.code,
                   item.quantity, item.scope_template, item.subject_binding, item.source_preset_item_id,
                   item.configuration_kind, item.configuration_reference, item.display_order, item.created_at, item.updated_at
            FROM billing_v2_cart_items item JOIN billing_v2_services service ON service.id = item.service_id
            LEFT JOIN billing_v2_service_tiers tier ON tier.id = item.tier_id
            WHERE item.cart_id = @cart_id ORDER BY item.display_order, item.id;
            """; command.Parameters.AddWithValue("@cart_id", cartId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(Identifier(reader, 0), Identifier(reader, 1), Identifier(reader, 2), reader.GetString(3),
            NullableIdentifier(reader, 4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetInt32(6), reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8), NullableIdentifier(reader, 9), reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetInt32(12), reader.GetDateTime(13), reader.GetDateTime(14)));
        return result;
    }

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

    private static async Task ExpireInactiveAsync(MySqlConnection connection, MySqlTransaction transaction, DateTime now, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            UPDATE billing_v2_carts
            SET status = 'expired', open_customer_slot = NULL,
                open_anonymous_slot = NULL, updated_at = @now
            WHERE status = 'open' AND expires_at <= @now;
            """;
        command.Parameters.AddWithValue("@now", now);
        await command.ExecuteNonQueryAsync(token);
    }

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
        return connection;
    }

    private static bool IsCurrency(string value) => value.Length == 3 && value.All(char.IsAsciiLetter);
    private static string Identifier(object value) => value switch { Guid id => id.ToString("D"), byte[] bytes when bytes.Length == 16 => new Guid(bytes).ToString("D"), _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty };
    private static string Identifier(MySqlDataReader reader, int ordinal) => Identifier(reader.GetValue(ordinal));
    private static string? NullableIdentifier(MySqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Identifier(reader, ordinal);
    private sealed record ResolvedItem(string Code, string? ServiceId = null, string? TierId = null, string? ScopeTemplate = null);
    private sealed record ServiceMetadata(string ConfigurationPolicy, bool PriceStableWithoutConfiguration, string DefaultScopeType);
    private sealed record DependencyDefinition(string ServiceId, string DefaultScope, string ScopeRelation, string TierRelation);
    private sealed record DependencyNode(string ServiceId, string? TierId, string ScopeTemplate, string? SubjectBinding, int Quantity);

    private static BillingV2CartItem ToCartItem(DependencyNode node)
        => new(node.ServiceId, "dependency", node.ServiceId, "dependency", node.TierId,
            null, node.Quantity, node.ScopeTemplate, node.SubjectBinding, null, null,
            null, 0, DateTime.UnixEpoch, DateTime.UnixEpoch);
}
