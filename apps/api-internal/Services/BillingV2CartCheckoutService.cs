using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

/// <summary>
/// Entrée de checkout du panier. Elle ne calcule pas de prix et ne crée pas de
/// rail financier : elle reconstitue exclusivement une composition depuis le
/// Cart et le quote que API-INTERNAL a déjà persistés, puis délègue l'écriture
/// financière à <see cref="IBillingV2AuthoritativeCheckoutService"/>.
/// </summary>
public interface IBillingV2CartCheckoutService
{
    Task<BillingV2CartCheckoutResult> CheckoutAsync(
        PortalSessionContext session,
        BillingV2CartCheckoutCommand command,
        string successUrl,
        string cancelUrl,
        string correlationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lit un checkout deja ancre pour son proprietaire. Cette lecture ne
    /// reessaie ni l'outbox ni le provider : elle rend uniquement le prochain
    /// etat public a afficher apres un refresh ou une reponse HTTP perdue.
    /// </summary>
    Task<BillingV2CartCheckoutStatusResult> GetStatusAsync(
        PortalSessionContext session,
        string? cartId,
        CancellationToken cancellationToken);
}

public sealed record BillingV2CartCheckoutCommand(
    string CartId,
    int ExpectedCartVersion,
    int AcceptedQuoteVersion,
    string AcceptedCompositionFingerprint);

public sealed record BillingV2CartCheckoutResult(
    string Code,
    string? SubscriptionId = null,
    string? Provider = null,
    string? ApprovalUrl = null,
    string? CartStatus = null,
    int? CartVersion = null);

public sealed record BillingV2CartCheckoutStatusResult(
    string Code,
    string? CartId = null,
    string? SubscriptionId = null,
    string? SubscriptionStatus = null,
    string? CheckoutStatus = null,
    string? Provider = null,
    string? ApprovalUrl = null,
    bool Retryable = false);

/// <summary>
/// Le navigateur ne choisit pas un provider. Le rail est une décision serveur
/// transitoirement limitée à Stripe, conformément au catalogue Checkout V2.
/// La primitive demeure séparée de l'UI afin que le routage puisse évoluer
/// sans changer le contrat Cart.
/// </summary>
internal static class BillingV2CartCheckoutProviderPolicy
{
    public static string Resolve(BillingV2RuntimeConfiguration runtime)
        => string.Equals(runtime.CartCheckoutProvider, "stripe", StringComparison.Ordinal)
            ? "stripe"
            : throw new InvalidOperationException(
                "BILLING_V2_CART_PROVIDER_NOT_AUTHORIZED");
}

/// <summary>
/// Traduction sans effet de bord de l'etat persiste du pipeline provider vers
/// une vue client. Elle ne remplace pas les machines a etats financieres : la
/// projection est uniquement une reprise apres response perdue ou refresh.
/// </summary>
public sealed record BillingV2CartCheckoutRecoveryState(
    string Status,
    bool Retryable,
    bool ExposeApprovalUrl);

public static class BillingV2CartCheckoutRecoveryPolicy
{
    /// <summary>
    /// Sans référence Cart, une reprise n'est sûre que lorsqu'elle désigne une
    /// seule opération du client. Le tri chronologique ne constitue jamais une
    /// preuve d'intention utilisateur.
    /// </summary>
    public static bool RequiresExplicitCartReference(int matchingCheckoutCount)
        => matchingCheckoutCount > 1;

    public static BillingV2CartCheckoutRecoveryState Resolve(
        string? subscriptionStatus,
        string? outboxStatus,
        string? providerCheckoutStatus,
        string? paymentStatus,
        bool hasSafeApprovalUrl)
    {
        if (paymentStatus is "succeeded"
            || subscriptionStatus is "active" or "pending_activation")
        {
            return new("confirmed", false, false);
        }
        if (paymentStatus is "failed" or "cancelled"
            || providerCheckoutStatus is "failed" or "cancelled"
            || outboxStatus == "failed")
        {
            return new("failed", true, false);
        }
        if (hasSafeApprovalUrl && providerCheckoutStatus == "pending_approval")
        {
            return new("approval_required", false, true);
        }
        if (paymentStatus is "pending" or "processing"
            || providerCheckoutStatus is "approved" or "completed")
        {
            return new("payment_pending", false, false);
        }
        return new("pending_provider", outboxStatus is "pending" or "processing", false);
    }
}

public sealed class BillingV2CartCheckoutService : IBillingV2CartCheckoutService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly IBillingV2AuthoritativeCheckoutService _checkout;
    private readonly ILogger<BillingV2CartCheckoutService> _logger;

    public BillingV2CartCheckoutService(
        SqlRuntimeConfiguration sql,
        BillingV2RuntimeConfiguration runtime,
        IBillingV2AuthoritativeCheckoutService checkout,
        ILogger<BillingV2CartCheckoutService> logger)
    {
        _sql = sql;
        _runtime = runtime;
        _checkout = checkout;
        _logger = logger;
    }

    public async Task<BillingV2CartCheckoutResult> CheckoutAsync(
        PortalSessionContext session,
        BillingV2CartCheckoutCommand command,
        string successUrl,
        string cancelUrl,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!IsValid(command)) return new("CART_CHECKOUT_REQUEST_INVALID");
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
            return new("CART_CHECKOUT_STORAGE_UNAVAILABLE");

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var cart = await ReadCartAsync(connection, session.CustomerId, command.CartId, cancellationToken);
        if (cart is null) return new("CART_NOT_FOUND");
        if (cart.Status == BillingV2CartStatuses.CheckedOut)
        {
            return string.IsNullOrWhiteSpace(cart.CheckedOutSubscriptionId)
                ? new("CART_CHECKOUT_LINK_CORRUPT")
                : new("CART_CHECKOUT_COMPLETED", cart.CheckedOutSubscriptionId,
                    CartStatus: cart.Status, CartVersion: cart.Version);
        }
        if (cart.Status == BillingV2CartStatuses.Expired || cart.ExpiresAtUtc <= DateTime.UtcNow)
            return new("CART_EXPIRED", CartStatus: BillingV2CartStatuses.Expired, CartVersion: cart.Version);
        if (cart.Status != BillingV2CartStatuses.Open) return new("CART_IMMUTABLE", CartStatus: cart.Status, CartVersion: cart.Version);
        if (cart.Version != command.ExpectedCartVersion)
            return new("CART_VERSION_CONFLICT", CartStatus: cart.Status, CartVersion: cart.Version);
        if (string.IsNullOrWhiteSpace(cart.CommitmentTermId)) return new("CART_COMMITMENT_REQUIRED");
        if (string.IsNullOrWhiteSpace(cart.PaymentMode)) return new("CART_PAYMENT_MODE_REQUIRED");

        var quote = await ReadAcceptedQuoteAsync(connection, command, cancellationToken);
        if (quote is null) return new("CART_QUOTE_CHANGED", CartStatus: cart.Status, CartVersion: cart.Version);
        if (quote.ExpiresAtUtc <= DateTime.UtcNow) return new("CART_QUOTE_EXPIRED", CartStatus: cart.Status, CartVersion: cart.Version);
        if (!string.Equals(quote.CommercialReadiness, BillingV2CartCommercialReadiness.Ready, StringComparison.Ordinal)
            || HasBlockingIssue(quote))
        {
            return new("CART_CHECKOUT_BLOCKED", CartStatus: cart.Status, CartVersion: cart.Version);
        }

        var items = await ReadItemsAsync(connection, command.CartId, cancellationToken);
        if (items.Count == 0) return new("CART_EMPTY", CartStatus: cart.Status, CartVersion: cart.Version);
        var composition = BuildComposition(cart, quote, items);
        if (composition is null) return new("CART_QUOTE_CHANGED", CartStatus: cart.Status, CartVersion: cart.Version);

        // La clé est dérivée uniquement d'identifiants déjà contrôlés par le
        // serveur. Elle rend les retries de l'UI, les timeouts et les doubles
        // clics identiques à l'intention financière existante.
        var idempotencyKey = "cart-checkout:" + BillingV2CheckoutSelectionFingerprint.ForSelection(
            $"{cart.Id}|{cart.Version}|{quote.QuoteVersion}|{quote.CompositionFingerprint}");
        string provider;
        try
        {
            provider = BillingV2CartCheckoutProviderPolicy.Resolve(_runtime);
        }
        catch (InvalidOperationException)
        {
            return new("CART_PROVIDER_NOT_AUTHORIZED", CartStatus: cart.Status, CartVersion: cart.Version);
        }
        try
        {
            var result = await _checkout.CreateAsync(
                session,
                new BillingV2AuthoritativeCheckoutRequest(
                    Selection: null,
                    Provider: provider,
                    IdempotencyKey: idempotencyKey,
                    SuccessUrl: successUrl,
                    CancelUrl: cancelUrl,
                    CompositionOverride: composition,
                    CartCheckoutLink: new BillingV2CartCheckoutLink(
                        cart.Id, cart.Version, quote.RecurringTotalCents, quote.TotalDueNowCents)),
                correlationId,
                cancellationToken);
            _logger.LogInformation(
                "Billing V2 Cart checkout completed. cart_id={CartId} customer_id={CustomerId} subscription_id={SubscriptionId} provider={Provider} result={Result}",
                cart.Id, session.CustomerId, result.SubscriptionId, result.Provider,
                result.Created ? "created" : "idempotent");
            return new("CART_CHECKOUT_CREATED", result.SubscriptionId, result.Provider,
                result.ApprovalUrl, BillingV2CartStatuses.CheckedOut, cart.Version + 1);
        }
        catch (InvalidOperationException exception) when (IsExpectedCartFailure(exception.Message))
        {
            var code = exception.Message == "BILLING_V2_CART_QUOTE_CHANGED"
                ? "CART_QUOTE_CHANGED"
                : exception.Message;
            _logger.LogInformation(
                "Billing V2 Cart checkout refused. cart_id={CartId} customer_id={CustomerId} result={Result}",
                cart.Id, session.CustomerId, code);
            return new(code, CartStatus: cart.Status, CartVersion: cart.Version);
        }
    }

    public async Task<BillingV2CartCheckoutStatusResult> GetStatusAsync(
        PortalSessionContext session,
        string? cartId,
        CancellationToken cancellationToken)
    {
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
            return new("CART_CHECKOUT_STORAGE_UNAVAILABLE");
        if (!string.IsNullOrWhiteSpace(cartId) && !Guid.TryParse(cartId, out _))
            return new("CART_NOT_FOUND");

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var lookup = await ReadCheckoutStatusAsync(
            connection, session.CustomerId, cartId, cancellationToken);
        if (string.IsNullOrWhiteSpace(cartId)
            && BillingV2CartCheckoutRecoveryPolicy.RequiresExplicitCartReference(
                lookup.MatchingCheckoutCount))
        {
            return new("CART_CHECKOUT_AMBIGUOUS");
        }
        var record = lookup.Record;
        if (record is null) return new("CART_CHECKOUT_NOT_FOUND");
        if (!string.Equals(record.CartStatus, BillingV2CartStatuses.CheckedOut, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(record.SubscriptionId))
        {
            return new("CART_CHECKOUT_NOT_FOUND");
        }

        var publicStatus = BillingV2CartCheckoutRecoveryPolicy.Resolve(
            record.SubscriptionStatus,
            record.OutboxStatus,
            record.ProviderCheckoutStatus,
            record.PaymentStatus,
            !string.IsNullOrWhiteSpace(record.ApprovalUrl));
        return new(
            "CART_CHECKOUT_STATUS_FOUND",
            record.CartId,
            record.SubscriptionId,
            record.SubscriptionStatus,
            publicStatus.Status,
            record.Provider,
            publicStatus.ExposeApprovalUrl ? record.ApprovalUrl : null,
            publicStatus.Retryable);
    }

    private static bool IsValid(BillingV2CartCheckoutCommand command)
        => Guid.TryParse(command.CartId, out _)
            && command.ExpectedCartVersion >= 0
            && command.AcceptedQuoteVersion > 0
            && command.AcceptedCompositionFingerprint is { Length: 64 }
            && command.AcceptedCompositionFingerprint.All(Uri.IsHexDigit);

    private static bool HasBlockingIssue(BillingV2CartQuote quote)
        => quote.DependencyIssues.Concat(quote.ScopeIssues).Concat(quote.ConfigurationIssues)
            .Any(issue => issue.Blocking);

    private static bool IsExpectedCartFailure(string code)
        => code.StartsWith("BILLING_V2_CART_", StringComparison.Ordinal)
           || code is "BILLING_V2_AUTHORITATIVE_CHECKOUT_DISABLED"
               or "BILLING_V2_AUTHORITATIVE_CHECKOUT_STORAGE_UNAVAILABLE";

    private static async Task<CartRecord?> ReadCartAsync(
        MySqlConnection connection, string customerId, string cartId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT cart.id, cart.status, cart.version, cart.currency, cart.commitment_term_id, cart.payment_mode,
                   cart.source_preset_id, cart.expires_at, cart.checked_out_subscription_id,
                   term.commitment_months, payment_option.discount_basis_points
            FROM billing_v2_carts cart
            LEFT JOIN billing_v2_commitment_terms term ON term.id = cart.commitment_term_id AND term.status = 'active'
            LEFT JOIN billing_v2_commitment_payment_options payment_option
              ON payment_option.commitment_term_id = term.id
             AND payment_option.payment_mode = cart.payment_mode
             AND payment_option.status = 'active'
            WHERE cart.id = @cart_id AND cart.customer_id = @customer_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@cart_id", cartId);
        command.Parameters.AddWithValue("@customer_id", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new CartRecord(
            MariaDbIdentifierReader.ReadRequired(reader, "id"), reader.GetString("status"), reader.GetInt32("version"),
            reader.GetString("currency"), MariaDbIdentifierReader.ReadNullable(reader, "commitment_term_id"),
            reader.IsDBNull(reader.GetOrdinal("payment_mode")) ? null : reader.GetString("payment_mode"),
            MariaDbIdentifierReader.ReadNullable(reader, "source_preset_id"), reader.GetDateTime("expires_at"),
            MariaDbIdentifierReader.ReadNullable(reader, "checked_out_subscription_id"),
            reader.IsDBNull(reader.GetOrdinal("commitment_months")) ? null : reader.GetInt32("commitment_months"),
            reader.IsDBNull(reader.GetOrdinal("discount_basis_points")) ? null : reader.GetInt32("discount_basis_points"));
    }

    private static async Task<CartCheckoutStatusLookup> ReadCheckoutStatusAsync(
        MySqlConnection connection,
        string customerId,
        string? cartId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        // The optional cart filter is structural SQL, never user SQL: it avoids
        // binding a typeless NULL through MariaDB solely to express the recovery
        // case where the client lost the original checkout response.
        var cartFilter = string.IsNullOrWhiteSpace(cartId)
            ? string.Empty
            : "AND cart.id = @cart_id";
        command.CommandText = $"""
            SELECT
                cart.id AS cart_id,
                cart.status AS cart_status,
                cart.checked_out_subscription_id AS subscription_id,
                subscription.status AS subscription_status,
                request.provider AS provider,
                request.status AS request_status,
                outbox.status AS outbox_status,
                checkout.status AS provider_checkout_status,
                checkout.approval_url AS approval_url,
                payment.status AS payment_status
            FROM billing_v2_carts cart
            LEFT JOIN billing_v2_subscriptions subscription
              ON subscription.id = cart.checked_out_subscription_id
             AND subscription.customer_id = cart.customer_id
            LEFT JOIN billing_v2_authoritative_checkout_requests request
              ON request.id = (
                  SELECT candidate.id
                  FROM billing_v2_authoritative_checkout_requests candidate
                  WHERE candidate.subscription_id = cart.checked_out_subscription_id
                  ORDER BY candidate.created_at DESC, candidate.id DESC
                  LIMIT 1
              )
            LEFT JOIN billing_v2_outbox_events outbox
              ON outbox.id = request.outbox_event_id
            LEFT JOIN billing_v2_provider_checkout_sessions checkout
              ON checkout.idempotency_key_hash = request.idempotency_key_hash
             AND checkout.subscription_id = cart.checked_out_subscription_id
            LEFT JOIN billing_v2_payment_attempts payment
              ON payment.id = (
                  SELECT candidate.id
                  FROM billing_v2_payment_attempts candidate
                  WHERE candidate.billing_event_id = request.billing_event_id
                  ORDER BY candidate.created_at DESC, candidate.id DESC
                  LIMIT 1
              )
            WHERE cart.customer_id = @customer_id
              AND cart.status = 'checked_out'
              {cartFilter}
            ORDER BY cart.updated_at DESC, cart.id DESC
            LIMIT 2;
            """;
        command.Parameters.AddWithValue("@customer_id", customerId);
        if (!string.IsNullOrWhiteSpace(cartId))
            command.Parameters.AddWithValue("@cart_id", cartId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new(null, 0);
        var record = new CartCheckoutStatusRecord(
            MariaDbIdentifierReader.ReadRequired(reader, "cart_id"),
            reader.GetString("cart_status"),
            MariaDbIdentifierReader.ReadNullable(reader, "subscription_id"),
            ReadNullableString(reader, "subscription_status"),
            ReadNullableString(reader, "provider"),
            ReadNullableString(reader, "request_status"),
            ReadNullableString(reader, "outbox_status"),
            ReadNullableString(reader, "provider_checkout_status"),
            ReadSafeApprovalUrl(reader),
            ReadNullableString(reader, "payment_status"));
        var matchingCheckoutCount = await reader.ReadAsync(cancellationToken) ? 2 : 1;
        return new(record, matchingCheckoutCount);
    }

    private static string? ReadSafeApprovalUrl(MySqlDataReader reader)
    {
        var value = ReadNullableString(reader, "approval_url");
        var provider = ReadNullableString(reader, "provider");
        return string.Equals(provider, "stripe", StringComparison.Ordinal)
            ? BillingV2StripeApprovalUrlRecoveryPolicy.NormalizeTrustedApprovalUrl(value)
            : null;
    }

    private static string? ReadNullableString(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static async Task<BillingV2CartQuote?> ReadAcceptedQuoteAsync(
        MySqlConnection connection, BillingV2CartCheckoutCommand command, CancellationToken cancellationToken)
    {
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText = """
            SELECT quote_json
            FROM billing_v2_cart_quotes
            WHERE cart_id = @cart_id AND quote_version = @quote_version
              AND cart_version = @cart_version
              AND composition_fingerprint = @fingerprint
            LIMIT 1;
            """;
        dbCommand.Parameters.AddWithValue("@cart_id", command.CartId);
        dbCommand.Parameters.AddWithValue("@quote_version", command.AcceptedQuoteVersion);
        dbCommand.Parameters.AddWithValue("@cart_version", command.ExpectedCartVersion);
        dbCommand.Parameters.AddWithValue("@fingerprint", command.AcceptedCompositionFingerprint);
        var value = await dbCommand.ExecuteScalarAsync(cancellationToken);
        if (value is not string json || string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<BillingV2CartQuote>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<CartItemRecord>> ReadItemsAsync(
        MySqlConnection connection, string cartId, CancellationToken cancellationToken)
    {
        var items = new List<CartItemRecord>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT item.id, item.service_id, item.tier_id, item.quantity, item.scope_template,
                   service.code AS service_code, tier.code AS tier_code
            FROM billing_v2_cart_items item
            JOIN billing_v2_services service ON service.id = item.service_id
            LEFT JOIN billing_v2_service_tiers tier ON tier.id = item.tier_id
            WHERE item.cart_id = @cart_id
            ORDER BY item.display_order, item.id;
            """;
        command.Parameters.AddWithValue("@cart_id", cartId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new CartItemRecord(
                MariaDbIdentifierReader.ReadRequired(reader, "id"),
                MariaDbIdentifierReader.ReadRequired(reader, "service_id"),
                MariaDbIdentifierReader.ReadNullable(reader, "tier_id"), reader.GetInt32("quantity"),
                reader.GetString("scope_template"), reader.GetString("service_code"),
                reader.IsDBNull(reader.GetOrdinal("tier_code")) ? null : reader.GetString("tier_code")));
        }
        return items;
    }

    private static BillingV2AuthoritativeCheckoutComposition? BuildComposition(
        CartRecord cart, BillingV2CartQuote quote, IReadOnlyList<CartItemRecord> items)
    {
        if (!string.Equals(quote.CartId, cart.Id, StringComparison.Ordinal)
            || quote.CartVersion != cart.Version
            || !string.Equals(quote.Currency, cart.Currency, StringComparison.Ordinal)
            || !string.Equals(quote.QuoteStatus, BillingV2CartQuoteStatuses.Current, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(cart.PaymentMode)
            || string.IsNullOrWhiteSpace(cart.CommitmentTermId)) return null;

        var itemsById = items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var compositionItems = new List<BillingV2NewSubscriptionPresetItem>();
        foreach (var line in quote.Lines)
        {
            if (!itemsById.TryGetValue(line.CartItemId, out var item)
                || !string.Equals(line.ServiceCode, item.ServiceCode, StringComparison.Ordinal)
                || !string.Equals(line.TierCode, item.TierCode, StringComparison.Ordinal)
                || line.Quantity != item.Quantity
                || string.IsNullOrWhiteSpace(line.ServicePriceId)) return null;
            compositionItems.Add(new BillingV2NewSubscriptionPresetItem(
                $"cart:{item.Id}:{line.ServicePriceId}", item.ServiceId, item.TierId,
                line.ServicePriceId, item.ServiceCode, item.TierCode, line.PriceCode,
                item.ScopeTemplate, item.Quantity, line.UnitAmountCents, quote.Currency,
                line.BillingCadence, line.DiscountEligible));
        }
        if (compositionItems.Count == 0
            || items.Any(item => !quote.Lines.Any(line => line.CartItemId == item.Id))) return null;

        // Le quote mémorise déjà le taux de remise après lecture du terme et
        // du mode de paiement. Sa source reste le PricingEngine Cart, jamais
        // une valeur envoyée par le navigateur.
        if (cart.CommitmentMonths is null || cart.DiscountBasisPoints is null) return null;
        var canonical = string.Join("|", compositionItems.OrderBy(item => item.PresetItemId, StringComparer.Ordinal)
            .Select(item => $"{item.ServiceId}:{item.TierId}:{item.ServicePriceId}:{item.Quantity}:{item.ScopeTemplate}"));
        // L'empreinte de composition Cart reste strictement économique pour le
        // quote. L'intention financière y ajoute l'identité du Cart : deux
        // Carts distincts mais économiquement identiques restent bien deux
        // commandes possibles, sans que les origins Cart n'affectent le prix.
        var checkoutSelectionFingerprint = BillingV2CheckoutSelectionFingerprint.ForSelection(
            $"cart:{cart.Id}|quote:{quote.CompositionFingerprint}");
        return new BillingV2AuthoritativeCheckoutComposition(
            cart.SourcePresetId, cart.CommitmentTermId, cart.PaymentMode!, cart.CommitmentMonths.Value,
            cart.DiscountBasisPoints.Value,
            compositionItems, canonical, checkoutSelectionFingerprint);
    }

    private sealed record CartRecord(string Id, string Status, int Version, string Currency,
        string? CommitmentTermId, string? PaymentMode, string? SourcePresetId,
        DateTime ExpiresAtUtc, string? CheckedOutSubscriptionId, int? CommitmentMonths,
        int? DiscountBasisPoints);

    private sealed record CartItemRecord(string Id, string ServiceId, string? TierId,
        int Quantity, string ScopeTemplate, string ServiceCode, string? TierCode);

    private sealed record CartCheckoutStatusRecord(
        string CartId,
        string CartStatus,
        string? SubscriptionId,
        string? SubscriptionStatus,
        string? Provider,
        string? RequestStatus,
        string? OutboxStatus,
        string? ProviderCheckoutStatus,
        string? ApprovalUrl,
        string? PaymentStatus);

    private sealed record CartCheckoutStatusLookup(
        CartCheckoutStatusRecord? Record,
        int MatchingCheckoutCount);
}
