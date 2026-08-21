using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed record BillingV2SubscriptionChangeRequest(
    string SubscriptionId,
    string SubscriptionItemId,
    string NewTierId,
    string ChangeKind,
    string IdempotencyKey,
    string? ActorReference);

public sealed record BillingV2SubscriptionChangeResult(
    string ChangeId,
    string Status,
    DateTime EffectiveAtUtc,
    long ProrationAmountCents,
    bool Existing);

public interface IBillingV2SubscriptionChangeService
{
    Task<BillingV2SubscriptionChangeResult> RequestAsync(
        BillingV2SubscriptionChangeRequest request,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<string> ApplySettledUpgradeAsync(
        string changeId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<int> ApplyDueDowngradesAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>Point d'observation optionnel des frontieres transactionnelles.</summary>
public interface IBillingV2SubscriptionChangeCheckpoint
{
    Task ReachedAsync(string checkpoint, CancellationToken cancellationToken);
}

/// <summary>
/// Change contractuel V2.1. L'upgrade persiste seulement une intention et son
/// prorata avant settlement ; le successeur ne peut etre cree qu'apres la
/// preuve d'encaissement. Le downgrade ne cree aucun evenement financier et
/// attend strictement la frontiere du cycle.
/// </summary>
public sealed class BillingV2SubscriptionChangeService : IBillingV2SubscriptionChangeService
{
    private readonly SqlRuntimeConfiguration _sql;
    private readonly BillingV2RuntimeConfiguration _runtime;
    private readonly IBillingV2PricingEngine _pricing;
    private readonly IBillingV2SubscriptionChangeCheckpoint? _checkpoint;

    public BillingV2SubscriptionChangeService(SqlRuntimeConfiguration sql, BillingV2RuntimeConfiguration runtime, IBillingV2PricingEngine pricing, IBillingV2SubscriptionChangeCheckpoint? checkpoint = null)
        => (_sql, _runtime, _pricing, _checkpoint) = (sql, runtime, pricing, checkpoint);

    public async Task<BillingV2SubscriptionChangeResult> RequestAsync(BillingV2SubscriptionChangeRequest request, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (!_runtime.SubscriptionChangesEnabled)
            throw new InvalidOperationException("BILLING_V2_SUBSCRIPTION_CHANGES_DISABLED");
        if (!_sql.IsPersistent || string.IsNullOrWhiteSpace(_sql.ConnectionString))
            throw new InvalidOperationException("BILLING_V2_SUBSCRIPTION_CHANGES_NO_PERSISTENT_SQL");
        if (request.ChangeKind is not (BillingV2SubscriptionChangePolicy.Upgrade or BillingV2SubscriptionChangePolicy.Downgrade))
            throw new InvalidOperationException("BILLING_V2_CHANGE_KIND_UNKNOWN");

        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var canonical = $"billing_v2.change|{request.SubscriptionId}|{request.SubscriptionItemId}|{request.NewTierId}|{request.ChangeKind}|{request.IdempotencyKey}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        var prior = await ReadChangeByHashAsync(connection, transaction, hash, cancellationToken);
        if (prior is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return prior with { Existing = true };
        }
        var subscription = await ReadSubscriptionAsync(connection, transaction, request.SubscriptionId, cancellationToken)
            ?? throw new InvalidOperationException("BILLING_V2_CHANGE_SUBSCRIPTION_NOT_FOUND");
        prior = await ReadChangeByHashAsync(connection, transaction, hash, cancellationToken);
        if (prior is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return prior with { Existing = true };
        }
        if (await HasPendingChangeAsync(connection, transaction, request.SubscriptionId, cancellationToken))
            throw new InvalidOperationException("BILLING_V2_CHANGE_ALREADY_PENDING");
        var item = await ReadItemAsync(connection, transaction, request.SubscriptionItemId, request.SubscriptionId, cancellationToken)
            ?? throw new InvalidOperationException("BILLING_V2_CHANGE_ITEM_NOT_FOUND");
        var components = await ReadTierComponentsAsync(connection, transaction, item.ServiceId, request.NewTierId, nowUtc, cancellationToken);
        if (!components.Any(component => component.BillingCadence == "monthly"))
            throw new InvalidOperationException("BILLING_V2_CHANGE_MONTHLY_COMPONENT_REQUIRED");
        if (components.Any(component => component.BillingCadence == "one_time"
            && component.ChargeTrigger == BillingV2ComponentizedPricingPolicy.SubscriptionChange))
            throw new InvalidOperationException("BILLING_V2_SUBSCRIPTION_CHANGE_ONE_TIME_NOT_SUPPORTED");

        var effectiveAt = BillingV2SubscriptionChangePolicy.ResolveEffectiveAt(request.ChangeKind, nowUtc, subscription.NextBoundaryUtc);
        var changeId = Guid.NewGuid().ToString("D");
        var inserted = await InsertChangeAsync(connection, transaction, changeId, request, canonical, hash, subscription.Version, effectiveAt, nowUtc, cancellationToken);
        if (!inserted)
        {
            var existing = await ReadChangeByHashAsync(connection, transaction, hash, cancellationToken)
                ?? throw new InvalidOperationException("BILLING_V2_CHANGE_IDEMPOTENCY_LOOKUP_FAILED");
            await transaction.CommitAsync(cancellationToken);
            return existing with { Existing = true };
        }
        if (_checkpoint is not null) await _checkpoint.ReachedAsync("after_change", cancellationToken);

        var successorComponents = BillingV2SubscriptionChangePolicy.ComponentsForSuccessor(request.ChangeKind, components);
        var proration = 0L;
        if (request.ChangeKind == BillingV2SubscriptionChangePolicy.Upgrade)
        {
            var oldMonthly = await ReadMonthlyAmountAsync(connection, transaction, item.Id, cancellationToken);
            var newMonthly = successorComponents.Single(component => component.BillingCadence == "monthly").AmountCents;
            proration = _pricing.CalculateMonthlyProration(oldMonthly, newMonthly, subscription.PeriodStartUtc, subscription.NextBoundaryUtc, effectiveAt).NetAmountCents;
            if (proration < 0) throw new InvalidOperationException("BILLING_V2_CHANGE_UPGRADE_NEGATIVE_PRORATION");
        }
        await InsertChangeSnapshotAsync(connection, transaction, changeId, item, request.NewTierId, successorComponents, cancellationToken);
        if (request.ChangeKind == BillingV2SubscriptionChangePolicy.Upgrade && proration > 0)
        {
            var monthly = successorComponents.Single(component => component.BillingCadence == "monthly");
            await InsertUpgradeEventAsync(connection, transaction, changeId, request.SubscriptionId, subscription.CustomerId, item, request.NewTierId, monthly, proration, subscription.PeriodStartUtc, subscription.NextBoundaryUtc, nowUtc, cancellationToken);
            if (_checkpoint is not null) await _checkpoint.ReachedAsync("after_upgrade_event", cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new BillingV2SubscriptionChangeResult(changeId, "pending", effectiveAt, proration, false);
    }

    public async Task<string> ApplySettledUpgradeAsync(string changeId, DateTime nowUtc, CancellationToken cancellationToken)
        => await ApplyAsync(changeId, nowUtc, requireSettled: true, cancellationToken);

    public async Task<int> ApplyDueDowngradesAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (!_runtime.SubscriptionChangesEnabled) return 0;
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM billing_v2_subscription_changes WHERE change_kind='downgrade' AND status='pending' AND effective_at <= @now ORDER BY effective_at, id";
        command.Parameters.AddWithValue("@now", nowUtc);
        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) ids.Add(Convert.ToString(reader.GetValue(0))!);
        var applied = 0;
        foreach (var id in ids)
            if (await ApplyAsync(id, nowUtc, requireSettled: false, cancellationToken) == "BILLING_V2_CHANGE_APPLIED") applied++;
        return applied;
    }

    private async Task<string> ApplyAsync(string changeId, DateTime nowUtc, bool requireSettled, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var row = await ReadApplyRowAsync(connection, transaction, changeId, cancellationToken);
        if (row is null) { await transaction.RollbackAsync(cancellationToken); return "BILLING_V2_CHANGE_NOT_FOUND"; }
        if (row.Status == "applied") { await transaction.CommitAsync(cancellationToken); return "BILLING_V2_CHANGE_ALREADY_APPLIED"; }
        if (requireSettled && row.HasCharge && !row.Settled) { await transaction.RollbackAsync(cancellationToken); return "BILLING_V2_CHANGE_SETTLEMENT_REQUIRED"; }
        if (!requireSettled && row.EffectiveAtUtc > nowUtc) { await transaction.RollbackAsync(cancellationToken); return "BILLING_V2_CHANGE_NOT_DUE"; }

        var version = await BillingV2FinancialCoreStore.LockSubscriptionAsync(
            connection, transaction, row.SubscriptionId, cancellationToken);
        if (version != row.BaseSubscriptionVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return "BILLING_V2_CHANGE_SUBSCRIPTION_CAS_CONFLICT";
        }

        var closed = await CloseItemAsync(connection, transaction, row.ItemId, row.EffectiveAtUtc, nowUtc, cancellationToken);
        if (!closed) { await transaction.RollbackAsync(cancellationToken); return "BILLING_V2_CHANGE_ITEM_CAS_CONFLICT"; }
        var successorId = Guid.NewGuid().ToString("D");
        await InsertSuccessorAsync(connection, transaction, successorId, row, nowUtc, cancellationToken);
        await InsertSuccessorComponentsAsync(connection, transaction, successorId, row.ChangeId, row.EffectiveAtUtc, nowUtc, cancellationToken);
        if (_checkpoint is not null) await _checkpoint.ReachedAsync("after_successor", cancellationToken);
        var advanced = await BillingV2FinancialCoreStore.TryAdvanceSubscriptionAsync(
            connection, transaction, row.SubscriptionId, row.BaseSubscriptionVersion, "active", nowUtc, cancellationToken);
        if (!advanced.IsValid)
        {
            await transaction.RollbackAsync(cancellationToken);
            return "BILLING_V2_CHANGE_SUBSCRIPTION_CAS_CONFLICT";
        }
        await MarkAppliedAsync(connection, transaction, changeId, nowUtc, cancellationToken);
        if (_runtime.StripeRecurringMutationEnabled)
        {
            await QueueStripeRecurringMutationAsync(
                connection, transaction, row, successorId, nowUtc, cancellationToken);
            if (_checkpoint is not null) await _checkpoint.ReachedAsync("after_outbox", cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return "BILLING_V2_CHANGE_APPLIED";
    }

    // SQL helpers deliberately project the component resolver view. Legacy rows
    // remain readable but are never backfilled; their first change creates a
    // new componentized successor containing only determinable current prices.
    private sealed record SubscriptionRow(string CustomerId, long Version, DateTime PeriodStartUtc, DateTime NextBoundaryUtc);
    private sealed record ItemRow(string Id, string ServiceId, string? UserId, string ScopeType, int Quantity, string Currency);
    private sealed record ApplyRow(string ChangeId, string Status, bool Settled, bool HasCharge, long BaseSubscriptionVersion, string ItemId, string SubscriptionId, string ServiceId, string? UserId, string ScopeType, int Quantity, string Currency, string NewTierId, DateTime EffectiveAtUtc);

    private static async Task<SubscriptionRow?> ReadSubscriptionAsync(MySqlConnection c, MySqlTransaction t, string id, CancellationToken ct) { await using var q=c.CreateCommand(); q.Transaction=t; q.CommandText="SELECT customer_id,version,current_period_started_at,COALESCE(renews_at,current_period_ends_at) AS boundary FROM billing_v2_subscriptions WHERE id=@id FOR UPDATE"; q.Parameters.AddWithValue("@id",id); await using var r=await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new SubscriptionRow(MariaDbIdentifierReader.ReadRequired(r,"customer_id"),r.GetInt64(1),r.GetDateTime(2),r.GetDateTime(3)) : null; }
    private static async Task<bool> HasPendingChangeAsync(MySqlConnection c,MySqlTransaction t,string subscriptionId,CancellationToken ct){await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT EXISTS(SELECT 1 FROM billing_v2_subscription_changes WHERE subscription_id=@id AND status='pending')";q.Parameters.AddWithValue("@id",subscriptionId);return Convert.ToInt32(await q.ExecuteScalarAsync(ct))!=0;}
    private static Task InsertUpgradeEventAsync(MySqlConnection c,MySqlTransaction t,string changeId,string subscriptionId,string customerId,ItemRow item,string newTier,BillingV2PriceComponentSnapshot monthly,long amount,DateTime start,DateTime end,DateTime now,CancellationToken ct){var line=new BillingV2BillingEventLineDraft(0,"subscription_change",null,"Prorata upgrade",1,amount,amount,0,amount,0,amount,item.Currency);var draft=new BillingV2BillingEventDraft(BillingV2BillingEventTypes.UpgradeCharge,BillingV2BillingEventDirections.Debit,item.Currency,amount,0,amount,0,amount,BillingV2BillingEventFactory.PricingEngineVersion,$"billing_v2.change.upgrade|{changeId}",[line]);return BillingV2FinancialCoreStore.InsertFinalizedBillingEventAsync(c,t,Guid.NewGuid().ToString("D"),customerId,subscriptionId,changeId,draft,BillingV2PaymentModes.Monthly,1,0,start,end,now,now.AddDays(7),[new BillingV2BillingEventLineSource(item.ServiceId,newTier,monthly.ServicePriceId,"monthly")],ct);}
    private static async Task<ItemRow?> ReadItemAsync(MySqlConnection c, MySqlTransaction t, string id, string subscriptionId, CancellationToken ct) { await using var q=c.CreateCommand(); q.Transaction=t; q.CommandText="SELECT id,service_id,subscription_user_id,scope_type,quantity,currency FROM billing_v2_subscription_items WHERE id=@id AND subscription_id=@subscription_id AND status='active' FOR UPDATE"; q.Parameters.AddWithValue("@id",id); q.Parameters.AddWithValue("@subscription_id",subscriptionId); await using var r=await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new ItemRow(MariaDbIdentifierReader.ReadRequired(r,"id"),MariaDbIdentifierReader.ReadRequired(r,"service_id"),MariaDbIdentifierReader.ReadNullable(r,"subscription_user_id"),r.GetString(3),r.GetInt32(4),r.GetString(5)) : null; }
    private static async Task<IReadOnlyList<BillingV2PriceComponentSnapshot>> ReadTierComponentsAsync(MySqlConnection c, MySqlTransaction t,string serviceId,string tierId,DateTime now,CancellationToken ct) { var x=new List<BillingV2PriceComponentSnapshot>(); await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT price.id,price.billing_cadence,price.charge_trigger,price.amount_cents,price.currency,service.discount_eligible,price.price_version FROM billing_v2_service_prices price INNER JOIN billing_v2_services service ON service.id=price.service_id WHERE price.service_id=@s AND price.tier_id=@t AND price.status='active' AND price.valid_from<=@n AND (price.valid_until IS NULL OR price.valid_until>@n) ORDER BY price.billing_cadence,price.price_version DESC,price.id";q.Parameters.AddWithValue("@s",serviceId);q.Parameters.AddWithValue("@t",tierId);q.Parameters.AddWithValue("@n",now);await using var r=await q.ExecuteReaderAsync(ct);var order=0;while(await r.ReadAsync(ct))x.Add(new BillingV2PriceComponentSnapshot(MariaDbIdentifierReader.ReadRequired(r,"id"),r.GetString(1),r.GetString(2),r.GetInt64(3),r.GetString(4),r.GetBoolean(5),order++));return x; }
    private static async Task<long> ReadMonthlyAmountAsync(MySqlConnection c,MySqlTransaction t,string itemId,CancellationToken ct){await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT amount_cents_snapshot FROM billing_v2_subscription_item_effective_price_components WHERE subscription_item_id=@id AND billing_cadence='monthly' AND status='active' ORDER BY display_order LIMIT 1";q.Parameters.AddWithValue("@id",itemId);var v=await q.ExecuteScalarAsync(ct);if(v is null or DBNull)throw new InvalidOperationException("BILLING_V2_CHANGE_OLD_MONTHLY_COMPONENT_REQUIRED");return Convert.ToInt64(v);}
    private static async Task<bool> InsertChangeAsync(MySqlConnection c,MySqlTransaction t,string id,BillingV2SubscriptionChangeRequest r,string canonical,string hash,long version,DateTime effective,DateTime now,CancellationToken ct){await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="INSERT IGNORE INTO billing_v2_subscription_changes (id,subscription_id,client_request_id,idempotency_key_canonical,idempotency_key_hash,base_subscription_version,change_kind,billing_effect,requested_at,effective_at,status,requested_by_reference,created_at) VALUES (@id,@s,@k,@c,@h,@v,@kind,@effect,@n,@e,'pending',@a,@n)";q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@s",r.SubscriptionId);q.Parameters.AddWithValue("@k",r.IdempotencyKey);q.Parameters.AddWithValue("@c",canonical);q.Parameters.AddWithValue("@h",hash);q.Parameters.AddWithValue("@v",version);q.Parameters.AddWithValue("@kind",r.ChangeKind);q.Parameters.AddWithValue("@effect",r.ChangeKind==BillingV2SubscriptionChangePolicy.Upgrade?"proration_charge":"none");q.Parameters.AddWithValue("@n",now);q.Parameters.AddWithValue("@e",effective);q.Parameters.AddWithValue("@a",r.ActorReference??(object)DBNull.Value);return await q.ExecuteNonQueryAsync(ct)==1;}
    private static async Task<BillingV2SubscriptionChangeResult?> ReadChangeByHashAsync(MySqlConnection c,MySqlTransaction t,string hash,CancellationToken ct){await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT id AS change_id,status,effective_at FROM billing_v2_subscription_changes WHERE idempotency_key_hash=@h";q.Parameters.AddWithValue("@h",hash);await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?new BillingV2SubscriptionChangeResult(MariaDbIdentifierReader.ReadRequired(r,"change_id"),r.GetString(1),r.GetDateTime(2),0,true):null;}
    private static async Task InsertChangeSnapshotAsync(MySqlConnection c,MySqlTransaction t,string changeId,ItemRow item,string newTier,IReadOnlyList<BillingV2PriceComponentSnapshot> components,CancellationToken ct){var itemId=Guid.NewGuid().ToString("D");await using(var q=c.CreateCommand()){q.Transaction=t;q.CommandText="INSERT INTO billing_v2_subscription_change_items (id,change_id,subscription_item_id,action_type,service_id,subscription_user_id,old_tier_id,new_tier_id,old_quantity,new_quantity,created_at) SELECT @id,@c,@i,'replace',@s,@u,tier_id,@n,quantity,quantity,UTC_TIMESTAMP(6) FROM billing_v2_subscription_items WHERE id=@i";q.Parameters.AddWithValue("@id",itemId);q.Parameters.AddWithValue("@c",changeId);q.Parameters.AddWithValue("@i",item.Id);q.Parameters.AddWithValue("@s",item.ServiceId);q.Parameters.AddWithValue("@u",item.UserId??(object)DBNull.Value);q.Parameters.AddWithValue("@n",newTier);await q.ExecuteNonQueryAsync(ct);}foreach(var component in components){await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="INSERT INTO billing_v2_subscription_change_item_components (id,subscription_change_item_id,service_price_id,billing_cadence,charge_trigger,amount_cents_snapshot,currency,discount_eligible_snapshot,display_order) VALUES (@id,@ci,@p,@cad,@tr,@a,@cu,@d,@o)";q.Parameters.AddWithValue("@id",Guid.NewGuid().ToString("D"));q.Parameters.AddWithValue("@ci",itemId);q.Parameters.AddWithValue("@p",component.ServicePriceId);q.Parameters.AddWithValue("@cad",component.BillingCadence);q.Parameters.AddWithValue("@tr",component.ChargeTrigger);q.Parameters.AddWithValue("@a",component.AmountCents);q.Parameters.AddWithValue("@cu",component.Currency);q.Parameters.AddWithValue("@d",component.DiscountEligible);q.Parameters.AddWithValue("@o",component.DisplayOrder);await q.ExecuteNonQueryAsync(ct);}}
    private static async Task<ApplyRow?> ReadApplyRowAsync(MySqlConnection c,MySqlTransaction t,string id,CancellationToken ct){await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="SELECT ch.id AS change_id,ch.status,EXISTS(SELECT 1 FROM billing_v2_billing_events e WHERE e.subscription_change_id=ch.id AND e.settlement_status='settled'),EXISTS(SELECT 1 FROM billing_v2_billing_events e WHERE e.subscription_change_id=ch.id),ch.base_subscription_version,i.id AS item_id,i.subscription_id,i.service_id,i.subscription_user_id,i.scope_type,i.quantity,i.currency,ci.new_tier_id,ch.effective_at FROM billing_v2_subscription_changes ch INNER JOIN billing_v2_subscription_change_items ci ON ci.change_id=ch.id INNER JOIN billing_v2_subscription_items i ON i.id=ci.subscription_item_id WHERE ch.id=@id FOR UPDATE";q.Parameters.AddWithValue("@id",id);await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?new ApplyRow(MariaDbIdentifierReader.ReadRequired(r,"change_id"),r.GetString(1),r.GetBoolean(2),r.GetBoolean(3),r.GetInt64(4),MariaDbIdentifierReader.ReadRequired(r,"item_id"),MariaDbIdentifierReader.ReadRequired(r,"subscription_id"),MariaDbIdentifierReader.ReadRequired(r,"service_id"),MariaDbIdentifierReader.ReadNullable(r,"subscription_user_id"),r.GetString(9),r.GetInt32(10),r.GetString(11),MariaDbIdentifierReader.ReadRequired(r,"new_tier_id"),r.GetDateTime(13)):null;}
    private static async Task<bool> CloseItemAsync(MySqlConnection c,MySqlTransaction t,string id,DateTime effective,DateTime now,CancellationToken ct){await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="UPDATE billing_v2_subscription_items SET effective_until=@e,status='superseded',updated_at=@n WHERE id=@id AND status='active' AND (effective_until IS NULL OR effective_until>@e)";q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@e",effective);q.Parameters.AddWithValue("@n",now);return await q.ExecuteNonQueryAsync(ct)==1;}
    private static async Task InsertSuccessorAsync(MySqlConnection c,MySqlTransaction t,string successor,ApplyRow row,DateTime now,CancellationToken ct){await using(var q=c.CreateCommand()){q.Transaction=t;q.CommandText="INSERT INTO billing_v2_subscription_items (id,subscription_id,subscription_user_id,service_id,tier_id,service_price_id,scope_type,quantity,amount_cents_snapshot,currency,discount_eligible_snapshot,pricing_representation,source,effective_from,status,created_at,updated_at) SELECT @id,@s,@u,@service,ci.new_tier_id,cc.service_price_id,@scope,@qty,cc.amount_cents_snapshot,@currency,cc.discount_eligible_snapshot,'componentized','subscription_change',@effective,'active',@now,@now FROM billing_v2_subscription_change_items ci INNER JOIN billing_v2_subscription_change_item_components cc ON cc.subscription_change_item_id=ci.id WHERE ci.change_id=@change AND cc.billing_cadence='monthly' ORDER BY cc.display_order LIMIT 1";q.Parameters.AddWithValue("@id",successor);q.Parameters.AddWithValue("@s",row.SubscriptionId);q.Parameters.AddWithValue("@u",row.UserId??(object)DBNull.Value);q.Parameters.AddWithValue("@service",row.ServiceId);q.Parameters.AddWithValue("@scope",row.ScopeType);q.Parameters.AddWithValue("@qty",row.Quantity);q.Parameters.AddWithValue("@currency",row.Currency);q.Parameters.AddWithValue("@effective",row.EffectiveAtUtc);q.Parameters.AddWithValue("@now",now);q.Parameters.AddWithValue("@change",row.ChangeId);if(await q.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("BILLING_V2_CHANGE_SUCCESSOR_INSERT_FAILED");} }
    private static async Task InsertSuccessorComponentsAsync(MySqlConnection c,MySqlTransaction t,string successor,string changeId,DateTime effective,DateTime now,CancellationToken ct){await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="INSERT INTO billing_v2_subscription_item_price_components (id,subscription_item_id,service_price_id,billing_cadence,charge_trigger,amount_cents_snapshot,currency,discount_eligible_snapshot,effective_from,display_order,status,created_at) SELECT UUID(),@successor,cc.service_price_id,cc.billing_cadence,cc.charge_trigger,cc.amount_cents_snapshot,cc.currency,cc.discount_eligible_snapshot,@effective,cc.display_order,'active',@now FROM billing_v2_subscription_change_items ci INNER JOIN billing_v2_subscription_change_item_components cc ON cc.subscription_change_item_id=ci.id WHERE ci.change_id=@change";q.Parameters.AddWithValue("@successor",successor);q.Parameters.AddWithValue("@effective",effective);q.Parameters.AddWithValue("@now",now);q.Parameters.AddWithValue("@change",changeId);if(await q.ExecuteNonQueryAsync(ct)<1)throw new InvalidOperationException("BILLING_V2_CHANGE_SUCCESSOR_COMPONENTS_MISSING");}
    private static async Task MarkAppliedAsync(MySqlConnection c,MySqlTransaction t,string id,DateTime now,CancellationToken ct){await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="UPDATE billing_v2_subscription_changes SET status='applied',applied_at=@n WHERE id=@id AND status='pending'";q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@n",now);if(await q.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("BILLING_V2_CHANGE_STATUS_CAS_CONFLICT");}
    private static async Task QueueStripeRecurringMutationAsync(MySqlConnection c,MySqlTransaction t,ApplyRow row,string successorId,DateTime now,CancellationToken ct){await using var amount=c.CreateCommand();amount.Transaction=t;amount.CommandText="SELECT amount_cents_snapshot,currency FROM billing_v2_subscription_item_price_components WHERE subscription_item_id=@item AND billing_cadence='monthly' AND status='active' ORDER BY display_order LIMIT 1";amount.Parameters.AddWithValue("@item",successorId);await using var reader=await amount.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))throw new InvalidOperationException("BILLING_V2_STRIPE_RECURRING_COMPONENT_MISSING");var cents=reader.GetInt64(0);var currency=reader.GetString(1);await reader.CloseAsync();var canonical=$"billing_v2.stripe.recurring_mutation|{row.ChangeId}|{successorId}|{cents}|{currency}";var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();var payload=JsonSerializer.Serialize(new { change_id=row.ChangeId, subscription_id=row.SubscriptionId, successor_item_id=successorId, amount_cents=cents, currency, quantity=row.Quantity });await using var q=c.CreateCommand();q.Transaction=t;q.CommandText="INSERT IGNORE INTO billing_v2_outbox_events (id,aggregate_type,aggregate_id,event_type,payload_text,idempotency_key_hash,status,available_at,created_at) VALUES (@id,'billing_v2_subscription_change',@change,'billing_v2.stripe.recurring_mutation_requested',@payload,@hash,'pending',@now,@now)";q.Parameters.AddWithValue("@id",Guid.NewGuid().ToString("D"));q.Parameters.AddWithValue("@change",row.ChangeId);q.Parameters.AddWithValue("@payload",payload);q.Parameters.AddWithValue("@hash",hash);q.Parameters.AddWithValue("@now",now);await q.ExecuteNonQueryAsync(ct);}
}
