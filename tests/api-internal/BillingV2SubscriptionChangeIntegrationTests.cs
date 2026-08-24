using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>Integration MariaDB reelle du changement VPS M -> L.</summary>
public static class BillingV2SubscriptionChangeIntegrationTests
{
    public static async Task RunSubscriptionChangeOneTimeRefusalAsync()
    {
        var cs=Environment.GetEnvironmentVariable("BILLING_V2_TEST_MARIADB_CONNECTION")??throw new InvalidOperationException("BILLING_V2_TEST_MARIADB_CONNECTION absent");
        var sql=new SqlRuntimeConfiguration(PortalPersistenceMode.MariaDb,"mariadb",cs,"test",true);
        var runtime=new BillingV2RuntimeConfiguration(false,false,false,false,false,false,SubscriptionChangesEnabled:true,StripeRecurringMutationEnabled:true);
        await using var db=new MySqlConnection(cs); await db.OpenAsync();
        var service=new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine());
        await Exec(db,"UPDATE billing_v2_service_prices SET status='retired' WHERE price_code LIKE 'INTEGRATION-CHANGE-FEE-%'");

        // An initial setup on the target tier remains a supported catalogue component:
        // it is excluded from a tier change, rather than being mistaken for a change fee.
        var supported=await SeedUpgradeFixtureAsync(db,DateTime.UtcNow.AddMinutes(-2));
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_service_prices WHERE tier_id=@tier AND billing_cadence='one_time' AND charge_trigger='initial_subscription' AND status='active'",("@tier",supported.Ids.L)),"initial setup remains catalogued");
        await service.RequestAsync(new BillingV2SubscriptionChangeRequest(supported.SubscriptionId,supported.ItemId,supported.Ids.L,BillingV2SubscriptionChangePolicy.Upgrade,"no-change-fee-"+supported.Suffix,"integration"),supported.Now,CancellationToken.None);
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE subscription_id=@id",("@id",supported.SubscriptionId)),"upgrade without change fee remains supported");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_change_item_components component INNER JOIN billing_v2_subscription_change_items item ON item.id=component.subscription_change_item_id INNER JOIN billing_v2_subscription_changes change_row ON change_row.id=item.change_id WHERE change_row.subscription_id=@id AND component.billing_cadence='one_time'",("@id",supported.SubscriptionId)),"initial setup is excluded from successor snapshot");

        var zeroCharge=await SeedUpgradeFixtureAsync(db,DateTime.UtcNow.AddMinutes(-2));
        await Exec(db,"UPDATE billing_v2_subscription_item_price_components SET amount_cents_snapshot=@amount WHERE subscription_item_id=@item AND billing_cadence='monthly'",("@amount",zeroCharge.Ids.AmountL),("@item",zeroCharge.ItemId));
        var zeroChange=await service.RequestAsync(new BillingV2SubscriptionChangeRequest(zeroCharge.SubscriptionId,zeroCharge.ItemId,zeroCharge.Ids.L,BillingV2SubscriptionChangePolicy.Upgrade,"zero-proration-"+zeroCharge.Suffix,"integration"),zeroCharge.Now,CancellationToken.None);
        Equal(0,zeroChange.ProrationAmountCents,"zero-proration upgrade has no financial charge");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_billing_events WHERE subscription_change_id=@id",("@id",zeroChange.ChangeId)),"zero-proration upgrade has no BillingEvent");
        if(await service.ApplySettledUpgradeAsync(zeroChange.ChangeId,zeroCharge.Now,CancellationToken.None)!="BILLING_V2_CHANGE_APPLIED")throw new InvalidOperationException("zero-proration upgrade did not apply without settlement");

        var upgrade=await SeedUpgradeFixtureAsync(db,DateTime.UtcNow.AddMinutes(-2));
        await InsertSubscriptionChangeOneTimePriceAsync(db,upgrade.Ids.Service,upgrade.Ids.L,upgrade.Now,upgrade.Suffix);
        var upgradeRequest=new BillingV2SubscriptionChangeRequest(upgrade.SubscriptionId,upgrade.ItemId,upgrade.Ids.L,BillingV2SubscriptionChangePolicy.Upgrade,"unsupported-upgrade-"+upgrade.Suffix,"integration");
        await AssertSubscriptionChangeOneTimeRefusedAsync(service,db,upgradeRequest,upgrade.Now,upgrade.SubscriptionId,upgrade.ItemId,upgrade.Ids.M,1,"upgrade");
        await AssertSubscriptionChangeOneTimeRefusedAsync(service,db,upgradeRequest,upgrade.Now,upgrade.SubscriptionId,upgrade.ItemId,upgrade.Ids.M,1,"upgrade replay");

        var downgrade=await SeedDueDowngradeFixtureAsync(db,DateTime.UtcNow.AddMinutes(-2));
        await InsertSubscriptionChangeOneTimePriceAsync(db,downgrade.Ids.Service,downgrade.Ids.S,downgrade.RequestedAt,downgrade.Suffix);
        var downgradeRequest=new BillingV2SubscriptionChangeRequest(downgrade.SubscriptionId,downgrade.ItemId,downgrade.Ids.S,BillingV2SubscriptionChangePolicy.Downgrade,"unsupported-downgrade-"+downgrade.Suffix,"integration");
        await AssertSubscriptionChangeOneTimeRefusedAsync(service,db,downgradeRequest,downgrade.RequestedAt,downgrade.SubscriptionId,downgrade.ItemId,downgrade.Ids.L,downgrade.Version,"downgrade");
        await Exec(db,"UPDATE billing_v2_service_prices SET status='retired' WHERE price_code LIKE 'INTEGRATION-CHANGE-FEE-%'");
    }

    public static async Task RunStripeIndeterminateAsync()
    {
        var cs=Environment.GetEnvironmentVariable("BILLING_V2_TEST_MARIADB_CONNECTION")??throw new InvalidOperationException("BILLING_V2_TEST_MARIADB_CONNECTION absent"); var sql=new SqlRuntimeConfiguration(PortalPersistenceMode.MariaDb,"mariadb",cs,"test",true); var runtime=new BillingV2RuntimeConfiguration(false,false,false,false,false,false,SubscriptionChangesEnabled:true,StripeRecurringMutationEnabled:true); await using var db=new MySqlConnection(cs);await db.OpenAsync();
        var s1=await SeedRecurringMutationAsync(db,sql,runtime,IndeterminateMode.TimeoutBefore); var d1=new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Test),s1.Gateway); try{await d1.DispatchPendingAsync(CancellationToken.None);throw new InvalidOperationException("S1 timeout not raised");}catch(HttpRequestException){} Equal(0,s1.Gateway.MutationEffectiveCount,"S1 no provider apply before timeout"); await ExpireLeaseAsync(db,s1.OutboxId); Equal(1,await d1.DispatchPendingAsync(CancellationToken.None),"S1 retry dispatch"); Equal(1,s1.Gateway.MutationEffectiveCount,"S1 one effective mutation"); Equal(1,s1.Gateway.Keys.Count,"S1 same idempotency key");
        var s2=await SeedRecurringMutationAsync(db,sql,runtime,IndeterminateMode.TimeoutAfter); var d2=new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Test),s2.Gateway); try{await d2.DispatchPendingAsync(CancellationToken.None);throw new InvalidOperationException("S2 timeout not raised");}catch(HttpRequestException){} Equal(1,s2.Gateway.MutationEffectiveCount,"S2 provider applied once"); await ExpireLeaseAsync(db,s2.OutboxId); Equal(1,await d2.DispatchPendingAsync(CancellationToken.None),"S2 refetch dispatch"); Equal(1,s2.Gateway.MutationEffectiveCount,"S2 no second provider mutation"); Equal(1,s2.Gateway.PostCalls,"S2 refetch before second post");
        var s3=await SeedRecurringMutationAsync(db,sql,runtime,IndeterminateMode.Mismatch); var d3=new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Test),s3.Gateway); Equal(0,await d3.DispatchPendingAsync(CancellationToken.None),"S3 mismatch not confirmed"); Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE id=@id AND status='failed' AND last_error='BILLING_V2_STRIPE_RECURRING_MUTATION_REFETCH_MISMATCH_MANUAL_REVIEW_REQUIRED'",("@id",s3.OutboxId)),"S3 mismatch is terminal and observable");
        var s4=await SeedRecurringMutationAsync(db,sql,runtime,IndeterminateMode.Ambiguous); var d4=new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Test),s4.Gateway); Equal(0,await d4.DispatchPendingAsync(CancellationToken.None),"S4 ambiguous refused"); Equal(0,s4.Gateway.MutationEffectiveCount,"S4 no arbitrary mutation");
        var bounded=await SeedRecurringMutationAsync(db,sql,runtime,IndeterminateMode.PersistentRetryable); var boundedDispatcher=new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Test),bounded.Gateway); for(var attempt=0;attempt<5;attempt++){Equal(0,await boundedDispatcher.DispatchPendingAsync(CancellationToken.None),"bounded retry attempt "+attempt);if(attempt<4)await ExpireLeaseAsync(db,bounded.OutboxId);} Equal(5,bounded.Gateway.PostCalls,"bounded retry makes five attempts at most"); Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE id=@id AND status='failed' AND retry_count=4 AND last_error='BILLING_V2_STRIPE_RECURRING_MUTATION_INDETERMINATE_MANUAL_REVIEW_REQUIRED'",("@id",bounded.OutboxId)),"bounded retry reaches observable terminal state");
        var s6=await SeedRecurringMutationAsync(db,sql,runtime,IndeterminateMode.Normal); var d6a=new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Test),s6.Gateway); var d6b=new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Test),s6.Gateway); await Task.WhenAll(Task.Run(()=>d6a.DispatchPendingAsync(CancellationToken.None)),Task.Run(()=>d6b.DispatchPendingAsync(CancellationToken.None))); Equal(1,s6.Gateway.MutationEffectiveCount,"S6 one concurrent effective mutation");
        var live=new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Live,"sk_live_never_used"),s6.Gateway); Equal(0,await live.DispatchPendingAsync(CancellationToken.None),"S8 live refused"); Equal(1,s6.Gateway.MutationEffectiveCount,"S8 no live provider mutation");
        VerifyRecurringMutationHttpContract();
    }

    public static async Task RunCrashConcurrencyAsync()
    {
        var cs = Environment.GetEnvironmentVariable("BILLING_V2_TEST_MARIADB_CONNECTION")
            ?? throw new InvalidOperationException("BILLING_V2_TEST_MARIADB_CONNECTION absent");
        var sql = new SqlRuntimeConfiguration(PortalPersistenceMode.MariaDb, "mariadb", cs, "test", true);
        var runtime = new BillingV2RuntimeConfiguration(false,false,false,false,false,false,
            SubscriptionChangesEnabled:true, StripeRecurringMutationEnabled:true);
        await using var db = new MySqlConnection(cs); await db.OpenAsync();
        var c1 = await SeedUpgradeFixtureAsync(db,DateTime.UtcNow.AddMinutes(-4));
        var c1Request = new BillingV2SubscriptionChangeRequest(c1.SubscriptionId,c1.ItemId,c1.Ids.L,BillingV2SubscriptionChangePolicy.Upgrade,"c1-"+c1.Suffix,"integration");
        try { await new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine(),new ThrowAtCheckpoint("after_change")).RequestAsync(c1Request,c1.Now,CancellationToken.None); throw new InvalidOperationException("C1 fault not raised"); } catch (InjectedCrashException) { }
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE subscription_id=@id",("@id",c1.SubscriptionId)),"C1 no durable change");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_billing_events WHERE subscription_id=@id",("@id",c1.SubscriptionId)),"C1 no durable event");
        var c2Service = new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine());
        var c2 = await c2Service.RequestAsync(c1Request,c1.Now,CancellationToken.None);
        var c2Replay = await c2Service.RequestAsync(c1Request,c1.Now,CancellationToken.None); if(!c2Replay.Existing)throw new InvalidOperationException("C2 replay not idempotent");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE subscription_id=@id",("@id",c1.SubscriptionId)),"C2 one durable change");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_billing_events WHERE subscription_change_id=@id",("@id",c2.ChangeId)),"C2 one durable event");
        await SettleAsync(db,c2.ChangeId,c1.Now);
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE id=@id AND status='active'",("@id",c1.ItemId)),"C3 M active after durable settlement");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@id AND tier_id=@tier AND status='active'",("@id",c1.SubscriptionId),("@tier",c1.Ids.L)),"C3 L absent before restart");
        if(await new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine()).ApplySettledUpgradeAsync(c2.ChangeId,c1.Now,CancellationToken.None)!="BILLING_V2_CHANGE_APPLIED")throw new InvalidOperationException("C3 resume failed");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested' AND status='pending'",("@id",c2.ChangeId)),"C5 durable pending outbox");
        var c6Outbox=await ScalarString(db,"SELECT id FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",c2.ChangeId)); var c6Provider="sub-c6-"+c1.Suffix;
        await Exec(db,"INSERT INTO billing_v2_provider_checkout_sessions (id,subscription_id,provider,environment,provider_subscription_id,status,idempotency_key_hash,outbox_event_id,created_at,updated_at) VALUES (@id,@s,'stripe','test',@provider,'completed',@hash,@outbox,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))",("@id",Guid.NewGuid().ToString("D")),("@s",c1.SubscriptionId),("@provider",c6Provider),("@hash",Guid.NewGuid().ToString("N")),("@outbox",c6Outbox));
        await Exec(db,"UPDATE billing_v2_outbox_events SET status='processing',available_at=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE id=@id",("@id",c6Outbox));
        var c6Stripe=new FakeStripeGateway(c6Provider,"si-c6",c1.Ids.AmountM,"EUR",1); var c6Dispatcher=new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Test),c6Stripe);
        Equal(1,await c6Dispatcher.DispatchPendingAsync(CancellationToken.None),"C6 expired processing reclaimed"); Equal(1,c6Stripe.MutationEffectiveCount,"C6 one provider mutation");

        var fixture = await SeedUpgradeFixtureAsync(db, DateTime.UtcNow.AddMinutes(-2));
        var request = new BillingV2SubscriptionChangeRequest(fixture.SubscriptionId,fixture.ItemId,fixture.Ids.L,BillingV2SubscriptionChangePolicy.Upgrade,"c4-"+fixture.Suffix,"integration");
        var crashing = new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine(),new ThrowAtCheckpoint("after_successor"));
        var change = await crashing.RequestAsync(request,fixture.Now,CancellationToken.None);
        await SettleAsync(db,change.ChangeId,fixture.Now);
        try { await crashing.ApplySettledUpgradeAsync(change.ChangeId,fixture.Now,CancellationToken.None); throw new InvalidOperationException("C4 fault not raised"); }
        catch (InjectedCrashException) { }
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE id=@id AND status='active'",("@id",fixture.ItemId)),"C4 rollback keeps M active");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@id AND tier_id=@tier AND status='active'",("@id",fixture.SubscriptionId),("@tier",fixture.Ids.L)),"C4 rollback has no L");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",change.ChangeId)),"C4 rollback has no outbox");
        var resumed = new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine());
        if(await resumed.ApplySettledUpgradeAsync(change.ChangeId,fixture.Now,CancellationToken.None)!="BILLING_V2_CHANGE_APPLIED")throw new InvalidOperationException("C4 resume failed");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@id AND tier_id=@tier AND status='active'",("@id",fixture.SubscriptionId),("@tier",fixture.Ids.L)),"C4 one resumed L");
        Equal(2,await ScalarLong(db,"SELECT version FROM billing_v2_subscriptions WHERE id=@id",("@id",fixture.SubscriptionId)),"C4 version once");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",change.ChangeId)),"C4 one resumed outbox");

        var c7 = await SeedDueDowngradeFixtureAsync(db,DateTime.UtcNow.AddMinutes(-3));
        var c7Request = new BillingV2SubscriptionChangeRequest(c7.SubscriptionId,c7.ItemId,c7.Ids.S,BillingV2SubscriptionChangePolicy.Downgrade,"c7-"+c7.Suffix,"integration");
        var c7Pending = await new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine()).RequestAsync(c7Request,c7.RequestedAt,CancellationToken.None);
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE id=@id AND status='pending' AND effective_at=@effective",("@id",c7Pending.ChangeId),("@effective",c7.EffectiveAt)),"C7 pending before worker");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE id=@id AND tier_id=@tier AND status='active'",("@id",c7.ItemId),("@tier",c7.Ids.L)),"C7 L active before crash");
        var c7Crash = new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine(),new ThrowAtCheckpoint("after_successor"));
        try { await c7Crash.ApplyDueDowngradesAsync(c7.EffectiveAt.AddMinutes(1),CancellationToken.None); throw new InvalidOperationException("C7 fault not raised"); } catch (InjectedCrashException) { }
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE id=@id AND status='active' AND effective_until IS NULL",("@id",c7.ItemId)),"C7 rollback keeps L unchanged");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@id AND tier_id=@tier",("@id",c7.SubscriptionId),("@tier",c7.Ids.S)),"C7 rollback has no S");
        Equal(c7.Version,await ScalarLong(db,"SELECT version FROM billing_v2_subscriptions WHERE id=@id",("@id",c7.SubscriptionId)),"C7 rollback version");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE id=@id AND status='pending'",("@id",c7Pending.ChangeId)),"C7 remains pending");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",c7Pending.ChangeId)),"C7 rollback no outbox");
        var c7Resume = new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine());
        Equal(1,await c7Resume.ApplyDueDowngradesAsync(c7.EffectiveAt.AddHours(3),CancellationToken.None),"C7 late replay applies once");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE id=@id AND status='superseded' AND effective_until=@effective",("@id",c7.ItemId),("@effective",c7.EffectiveAt)),"C7 L bounded at contractual boundary");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@id AND tier_id=@tier AND status='active' AND effective_from=@effective",("@id",c7.SubscriptionId),("@tier",c7.Ids.S),("@effective",c7.EffectiveAt)),"C7 one historic S");
        Equal(c7.Version+1,await ScalarLong(db,"SELECT version FROM billing_v2_subscriptions WHERE id=@id",("@id",c7.SubscriptionId)),"C7 version once");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",c7Pending.ChangeId)),"C7 one outbox");
        Equal(0,await c7Resume.ApplyDueDowngradesAsync(c7.EffectiveAt.AddHours(4),CancellationToken.None),"C7 second replay no-op");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@id AND tier_id=@tier",("@id",c7.SubscriptionId),("@tier",c7.Ids.S)),"C7 S remains unique");

        var concurrent = await SeedUpgradeFixtureAsync(db,DateTime.UtcNow.AddMinutes(-1));
        var concurrentRequest = new BillingV2SubscriptionChangeRequest(concurrent.SubscriptionId,concurrent.ItemId,concurrent.Ids.L,BillingV2SubscriptionChangePolicy.Upgrade,"same-"+concurrent.Suffix,"integration");
        var first = new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine()); var second = new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine());
        var results = await Task.WhenAll(Task.Run(()=>first.RequestAsync(concurrentRequest,concurrent.Now,CancellationToken.None)),Task.Run(()=>second.RequestAsync(concurrentRequest,concurrent.Now,CancellationToken.None)));
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE subscription_id=@id",("@id",concurrent.SubscriptionId)),"same request one change");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_billing_events WHERE subscription_id=@id AND event_type='upgrade_charge'",("@id",concurrent.SubscriptionId)),"same request one event");
        if(!results.Any(x=>x.Existing))throw new InvalidOperationException("same request must return existing result");
    }

    public static async Task RunDeferredDowngradeAsync()
    {
        var cs = Environment.GetEnvironmentVariable("BILLING_V2_TEST_MARIADB_CONNECTION")
            ?? throw new InvalidOperationException("BILLING_V2_TEST_MARIADB_CONNECTION absent");
        var sql = new SqlRuntimeConfiguration(PortalPersistenceMode.MariaDb, "mariadb", cs, "test", true);
        var runtime = new BillingV2RuntimeConfiguration(false,false,false,false,false,false,
            SubscriptionChangesEnabled:true, StripeRecurringMutationEnabled:true);
        var changes = new BillingV2SubscriptionChangeService(sql, runtime, new BillingV2PricingEngine());
        var requestedAt = new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);
        var boundary = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var lateWorker = boundary.AddHours(3).AddMinutes(17);
        await using var db = new MySqlConnection(cs); await db.OpenAsync();
        var suffix = Guid.NewGuid().ToString("N"); var subscription = Guid.NewGuid().ToString("D"); var itemL = Guid.NewGuid().ToString("D"); var customer = Guid.NewGuid().ToString("D"); var term = Guid.NewGuid().ToString("D");
        var ids = await ReadIds(db);
        await Exec(db,"INSERT INTO billing_v2_commitment_terms (id,code,name,commitment_months,discount_basis_points,status,created_at,updated_at) VALUES (@id,@code,'Integration',12,0,'active',@n,@n)",("@id",term),("@code","INT-"+suffix),("@n",requestedAt));
        await Exec(db,"INSERT INTO billing_v2_subscriptions (id,customer_id,commitment_term_id,status,payment_mode,currency,billing_model,started_at,billing_anchor_at,current_period_started_at,current_period_ends_at,renews_at,discount_basis_points_snapshot,version,created_at,updated_at) VALUES (@s,@c,@term,'active','monthly','EUR','v2',@start,@start,@start,@boundary,@boundary,0,23,@n,@n)",("@s",subscription),("@c",customer),("@term",term),("@start",new DateTime(2026,8,1,0,0,0,DateTimeKind.Utc)),("@boundary",boundary),("@n",requestedAt));
        await Exec(db,"INSERT INTO billing_v2_subscription_items (id,subscription_id,service_id,tier_id,service_price_id,scope_type,quantity,amount_cents_snapshot,currency,discount_eligible_snapshot,pricing_representation,source,effective_from,status,created_at,updated_at) VALUES (@i,@s,@service,@tier,@price,'subscription',1,@amount,'EUR',1,'componentized','integration',@start,'active',@n,@n)",("@i",itemL),("@s",subscription),("@service",ids.Service),("@tier",ids.L),("@price",ids.MonthlyL),("@amount",ids.AmountL),("@start",new DateTime(2026,8,1,0,0,0,DateTimeKind.Utc)),("@n",requestedAt));
        await Component(db,itemL,ids.MonthlyL,"monthly","initial_subscription",ids.AmountL,requestedAt);
        var seedOutbox = Guid.NewGuid().ToString("D"); var providerSubscription = "sub-downgrade-"+suffix;
        await Exec(db,"INSERT INTO billing_v2_outbox_events (id,aggregate_type,aggregate_id,event_type,payload_text,idempotency_key_hash,status,available_at,created_at) VALUES (@id,'billing_v2_subscription',@s,'integration.seed','{}',@hash,'processed',@n,@n)",("@id",seedOutbox),("@s",subscription),("@hash",Guid.NewGuid().ToString("N")),("@n",requestedAt));
        await Exec(db,"INSERT INTO billing_v2_provider_checkout_sessions (id,subscription_id,provider,environment,provider_subscription_id,status,idempotency_key_hash,outbox_event_id,created_at,updated_at) VALUES (@id,@s,'stripe','test',@provider,'completed',@hash,@outbox,@n,@n)",("@id",Guid.NewGuid().ToString("D")),("@s",subscription),("@provider",providerSubscription),("@hash",Guid.NewGuid().ToString("N")),("@outbox",seedOutbox),("@n",requestedAt));
        var stripe = new FakeStripeGateway(providerSubscription,"si-recurring",ids.AmountL,"EUR",1);
        var dispatcher = new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Test),stripe);

        var request = new BillingV2SubscriptionChangeRequest(subscription,itemL,ids.S,BillingV2SubscriptionChangePolicy.Downgrade,"l-to-s-"+suffix,"integration");
        var change = await changes.RequestAsync(request,requestedAt,CancellationToken.None);
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE id=@id AND change_kind='downgrade' AND status='pending' AND effective_at=@boundary AND base_subscription_version=23",("@id",change.ChangeId),("@boundary",boundary)),"pending L to S change");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_change_items WHERE change_id=@id AND old_tier_id=@l AND new_tier_id=@s",("@id",change.ChangeId),("@l",ids.L),("@s",ids.S)),"downgrade snapshots");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE id=@id AND tier_id=@l AND status='active'",("@id",itemL),("@l",ids.L)),"L before boundary");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@id AND tier_id=@s AND status='active'",("@id",subscription),("@s",ids.S)),"S absent before boundary");
        Equal(23,await ScalarLong(db,"SELECT version FROM billing_v2_subscriptions WHERE id=@id",("@id",subscription)),"version before boundary");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_billing_events WHERE subscription_change_id=@id OR event_type IN ('downgrade_credit','credit_note')",("@id",change.ChangeId)),"no downgrade credit");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",change.ChangeId)),"no Stripe mutation before boundary");
        Equal(ids.AmountL,stripe.AmountCents,"Stripe remains L before boundary");
        Equal(0,await changes.ApplyDueDowngradesAsync(boundary.AddTicks(-1),CancellationToken.None),"not due one tick before boundary");
        Equal(1,await changes.ApplyDueDowngradesAsync(lateWorker,CancellationToken.None),"applied late at boundary");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE id=@id AND tier_id=@l AND status='superseded' AND effective_until=@boundary",("@id",itemL),("@l",ids.L),("@boundary",boundary)),"L bounded at historic boundary");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@id AND tier_id=@s AND status='active' AND effective_from=@boundary AND pricing_representation='componentized'",("@id",subscription),("@s",ids.S),("@boundary",boundary)),"S successor at historic boundary");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@id AND status='active'",("@id",subscription)),"one active entitlement at boundary");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_item_price_components c INNER JOIN billing_v2_subscription_items i ON i.id=c.subscription_item_id WHERE i.subscription_id=@id AND i.tier_id=@s AND c.billing_cadence='monthly' AND c.amount_cents_snapshot=@amount",("@id",subscription),("@s",ids.S),("@amount",ids.AmountS)),"S monthly snapshot");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_item_price_components c INNER JOIN billing_v2_subscription_items i ON i.id=c.subscription_item_id WHERE i.subscription_id=@id AND i.tier_id=@s AND c.billing_cadence='one_time'",("@id",subscription),("@s",ids.S)),"no S setup");
        Equal(24,await ScalarLong(db,"SELECT version FROM billing_v2_subscriptions WHERE id=@id",("@id",subscription)),"version advances once at boundary");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",change.ChangeId)),"one S recurring outbox");
        await Exec(db,"UPDATE billing_v2_outbox_events SET available_at=UTC_TIMESTAMP(6) WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",change.ChangeId));
        Equal(1,await dispatcher.DispatchPendingAsync(CancellationToken.None),"S Stripe dispatch"); Equal(ids.AmountS,stripe.AmountCents,"Stripe refetch confirmed S"); Equal(1,stripe.Quantity,"S Stripe quantity"); Equal(1,stripe.MutationEffectiveCount,"one S Stripe mutation");

        var rail = new BillingV2StripeRailService(sql,new StripeRuntimeConfiguration(StripeMode.Test),stripe,new FixedBillingV2Clock(lateWorker),NullLogger<BillingV2StripeRailService>.Instance);
        var renewals = new BillingV2RenewalService(sql,new FixedBillingV2Clock(lateWorker),stripe,rail,NullLogger<BillingV2RenewalService>.Instance);
        var renewal = await renewals.EnsureRenewalChargeAsync(subscription,3,CancellationToken.None);
        if(!renewal.Created) throw new InvalidOperationException(renewal.ReasonCode);
        Equal(ids.AmountS,renewal.ExpectedAmountCents,"renewal uses S snapshot");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_billing_event_lines WHERE billing_event_id=@id AND tier_code='S'",("@id",renewal.BillingEventId!)),"renewal line S only");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_billing_event_lines WHERE billing_event_id=@id AND (tier_code='L' OR service_price_id=@setup)",("@id",renewal.BillingEventId!),("@setup",ids.SetupM)),"renewal excludes L and setup");
        var replay=await changes.RequestAsync(request,lateWorker,CancellationToken.None);if(!replay.Existing)throw new InvalidOperationException("downgrade replay not idempotent");
        Equal(0,await changes.ApplyDueDowngradesAsync(lateWorker,CancellationToken.None),"boundary worker replay no-op"); Equal(0,await dispatcher.DispatchPendingAsync(CancellationToken.None),"Stripe replay no-op");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE subscription_id=@id",("@id",subscription)),"one downgrade change after replay"); Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@id AND tier_id=@s",("@id",subscription),("@s",ids.S)),"one S successor after replay"); Equal(24,await ScalarLong(db,"SELECT version FROM billing_v2_subscriptions WHERE id=@id",("@id",subscription)),"version stable after replay"); Equal(1,stripe.MutationEffectiveCount,"one Stripe mutation after replay");
    }

    public static async Task RunAsync()
    {
        var cs = Environment.GetEnvironmentVariable("BILLING_V2_TEST_MARIADB_CONNECTION")
            ?? throw new InvalidOperationException("BILLING_V2_TEST_MARIADB_CONNECTION absent");
        var sql = new SqlRuntimeConfiguration(PortalPersistenceMode.MariaDb, "mariadb", cs, "test", true);
        var runtime = new BillingV2RuntimeConfiguration(false,false,false,false,false,false,
            SubscriptionChangesEnabled:true, StripeRecurringMutationEnabled:true);
        var service = new BillingV2SubscriptionChangeService(sql, runtime, new BillingV2PricingEngine());
        var now = DateTime.UtcNow.AddMinutes(-2);
        await using var db = new MySqlConnection(cs); await db.OpenAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var subscription = Guid.NewGuid().ToString("D"); var item = Guid.NewGuid().ToString("D"); var customer = Guid.NewGuid().ToString("D");
        var ids = await ReadIds(db);
        await Exec(db, "INSERT INTO billing_v2_subscriptions (id,customer_id,status,payment_mode,currency,started_at,commitment_started_at,current_period_started_at,current_period_ends_at,renews_at,discount_basis_points_snapshot,billing_model,version,created_at,updated_at) VALUES (@s,@c,'active','monthly','EUR',@n,@n,@n,@e,@e,0,'v2',7,@n,@n)", ("@s",subscription),("@c",customer),("@n",now),("@e",now.AddMonths(1)));
        await Exec(db, "INSERT INTO billing_v2_subscription_items (id,subscription_id,service_id,tier_id,service_price_id,scope_type,quantity,amount_cents_snapshot,currency,discount_eligible_snapshot,pricing_representation,source,effective_from,status,created_at,updated_at) VALUES (@i,@s,@service,@tier,@price,'subscription',1,@amount,'EUR',1,'componentized','integration',@n,'active',@n,@n)", ("@i",item),("@s",subscription),("@service",ids.Service),("@tier",ids.M),("@price",ids.MonthlyM),("@amount",ids.AmountM),("@n",now));
        await Component(db,item,ids.MonthlyM,"monthly","initial_subscription",ids.AmountM,now);
        var setupComponent = await Component(db,item,ids.SetupM,"one_time","initial_subscription",ids.SetupAmount,now);
        await using (var transaction = await db.BeginTransactionAsync())
        {
            var setupLine = new BillingV2BillingEventLineDraft(0,"VPS-CLOUD","M","Setup VPS",1,ids.SetupAmount,ids.SetupAmount,0,ids.SetupAmount,0,ids.SetupAmount,"EUR");
            var setupEvent = new BillingV2BillingEventDraft(BillingV2BillingEventTypes.InitialCharge,BillingV2BillingEventDirections.Debit,"EUR",ids.SetupAmount,0,ids.SetupAmount,0,ids.SetupAmount,BillingV2BillingEventFactory.PricingEngineVersion,$"integration-setup-{suffix}",[setupLine]);
            await BillingV2FinancialCoreStore.InsertFinalizedBillingEventAsync(db,transaction,Guid.NewGuid().ToString("D"),customer,subscription,null,setupEvent,"monthly",1,0,now,now.AddMonths(1),now,now.AddDays(7),[new BillingV2BillingEventLineSource(ids.Service,ids.M,ids.SetupM,"one_time",item,setupComponent)],CancellationToken.None);
            await transaction.CommitAsync();
        }
        Equal(1, await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@s",("@s",subscription)),"initial entitlement");
        Equal(2, await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_item_price_components WHERE subscription_item_id=@i",("@i",item)),"initial components");
        Equal(1, await Count(db,"SELECT COUNT(*) FROM billing_v2_one_time_component_consumptions WHERE subscription_item_price_component_id=@id AND status='consumed'",("@id",setupComponent)),"setup consumed once");
        var request = new BillingV2SubscriptionChangeRequest(subscription,item,ids.L,BillingV2SubscriptionChangePolicy.Upgrade,"m-to-l-"+suffix,"integration");
        var change = await service.RequestAsync(request, now, CancellationToken.None);
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE subscription_id=@s",("@s",subscription)),"change count");
        Equal(7,await ScalarLong(db,"SELECT base_subscription_version FROM billing_v2_subscription_changes WHERE id=@id",("@id",change.ChangeId)),"base version");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_change_items WHERE change_id=@id AND old_tier_id=@m AND new_tier_id=@l AND old_quantity=1 AND new_quantity=1",("@id",change.ChangeId),("@m",ids.M),("@l",ids.L)),"before/after snapshot");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_billing_events WHERE subscription_change_id=@id AND event_type='upgrade_charge'",("@id",change.ChangeId)),"upgrade event");
        Equal(new BillingV2PricingEngine().CalculateMonthlyProration(ids.AmountM,ids.AmountL,now,now.AddMonths(1),now).NetAmountCents,await ScalarLong(db,"SELECT net_amount_cents FROM billing_v2_billing_events WHERE subscription_change_id=@id",("@id",change.ChangeId)),"pricing-engine proration");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@s AND tier_id=@tier AND status='active'",("@s",subscription),("@tier",ids.M)),"M active before settlement");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@s AND tier_id=@tier AND status='active'",("@s",subscription),("@tier",ids.L)),"L absent before settlement");
        var upgradeEvent = await ScalarString(db,"SELECT id FROM billing_v2_billing_events WHERE subscription_change_id=@id",("@id",change.ChangeId));
        await using (var transaction = await db.BeginTransactionAsync()) { await BillingV2FinancialCoreStore.ApplySettlementAsync(db,transaction,upgradeEvent,"settled","integration",now,CancellationToken.None); await transaction.CommitAsync(); }
        var applied=await service.ApplySettledUpgradeAsync(change.ChangeId,now,CancellationToken.None);
        if(applied!="BILLING_V2_CHANGE_APPLIED")throw new InvalidOperationException(applied);
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@s AND tier_id=@tier AND status='active'",("@s",subscription),("@tier",ids.L)),"one L successor");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE id=@i AND tier_id=@tier AND status='superseded' AND effective_until=@effective",("@i",item),("@tier",ids.M),("@effective",change.EffectiveAtUtc)),"M bounded at effective_at");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE subscription_id=@s AND status='active'",("@s",subscription)),"one active VPS entitlement");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_item_price_components c INNER JOIN billing_v2_subscription_items i ON i.id=c.subscription_item_id WHERE i.subscription_id=@s AND i.tier_id=@tier AND c.billing_cadence='one_time'",("@s",subscription),("@tier",ids.L)),"no L setup");
        Equal(8,await ScalarLong(db,"SELECT version FROM billing_v2_subscriptions WHERE id=@s",("@s",subscription)),"version advances once");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",change.ChangeId)),"one recurring outbox");
        var mutationOutbox = await ScalarString(db,"SELECT id FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",change.ChangeId));
        var providerSubscription = "sub-integration-" + suffix;
        await Exec(db,"INSERT INTO billing_v2_provider_checkout_sessions (id,subscription_id,provider,environment,provider_subscription_id,status,idempotency_key_hash,outbox_event_id,created_at,updated_at) VALUES (@id,@s,'stripe','test',@provider,'completed',@hash,@outbox,@n,@n)",("@id",Guid.NewGuid().ToString("D")),("@s",subscription),("@provider",providerSubscription),("@hash",Guid.NewGuid().ToString("N")),("@outbox",mutationOutbox),("@n",now));
        var stripe = new FakeStripeGateway(providerSubscription,"si-recurring",ids.AmountM,"EUR",1);
        var dispatcher = new BillingV2StripeRecurringMutationDispatcher(sql,runtime,new StripeRuntimeConfiguration(StripeMode.Test),stripe);
        Equal(1,await dispatcher.DispatchPendingAsync(CancellationToken.None),"recurring outbox dispatched");
        Equal(1,stripe.MutationEffectiveCount,"one Stripe mutation"); Equal(ids.AmountL,stripe.AmountCents,"Stripe amount L"); Equal(1,stripe.Quantity,"Stripe quantity");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested' AND status='processed'",("@id",change.ChangeId)),"recurring confirmed");
        var replay=await service.RequestAsync(request,now,CancellationToken.None); if(!replay.Existing)throw new InvalidOperationException("replay not idempotent");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE subscription_id=@s",("@s",subscription)),"replay change count");
        Equal(0,await dispatcher.DispatchPendingAsync(CancellationToken.None),"recurring replay no-op"); Equal(1,stripe.MutationEffectiveCount,"Stripe replay no-op");
    }
    private sealed record DueDowngradeFixture(string SubscriptionId,string ItemId,Ids Ids,DateTime RequestedAt,DateTime EffectiveAt,long Version,string Suffix);
    private sealed record RecurringScenario(string OutboxId,IndeterminateFakeStripeGateway Gateway);
    private static async Task<RecurringScenario> SeedRecurringMutationAsync(MySqlConnection db,SqlRuntimeConfiguration sql,BillingV2RuntimeConfiguration runtime,IndeterminateMode mode){var fixture=await SeedUpgradeFixtureAsync(db,DateTime.UtcNow.AddMinutes(-2));var service=new BillingV2SubscriptionChangeService(sql,runtime,new BillingV2PricingEngine());var change=await service.RequestAsync(new BillingV2SubscriptionChangeRequest(fixture.SubscriptionId,fixture.ItemId,fixture.Ids.L,BillingV2SubscriptionChangePolicy.Upgrade,"stripe-"+fixture.Suffix,"integration"),fixture.Now,CancellationToken.None);await SettleAsync(db,change.ChangeId,fixture.Now);if(await service.ApplySettledUpgradeAsync(change.ChangeId,fixture.Now,CancellationToken.None)!="BILLING_V2_CHANGE_APPLIED")throw new InvalidOperationException("stripe scenario apply failed");var outbox=await ScalarString(db,"SELECT id FROM billing_v2_outbox_events WHERE aggregate_id=@id AND event_type='billing_v2.stripe.recurring_mutation_requested'",("@id",change.ChangeId));var provider="sub-stripe-"+fixture.Suffix;await Exec(db,"INSERT INTO billing_v2_provider_checkout_sessions (id,subscription_id,provider,environment,provider_subscription_id,status,idempotency_key_hash,outbox_event_id,created_at,updated_at) VALUES (@id,@s,'stripe','test',@provider,'completed',@hash,@outbox,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))",("@id",Guid.NewGuid().ToString("D")),("@s",fixture.SubscriptionId),("@provider",provider),("@hash",Guid.NewGuid().ToString("N")),("@outbox",outbox));return new(outbox,new IndeterminateFakeStripeGateway(provider,"si-"+fixture.Suffix,fixture.Ids.AmountM,"EUR",1,mode));}
    private static Task ExpireLeaseAsync(MySqlConnection db,string outboxId)=>Exec(db,"UPDATE billing_v2_outbox_events SET status='processing',available_at=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE id=@id",("@id",outboxId));
    private static void VerifyRecurringMutationHttpContract(){var source=File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(),"apps","api-internal","Services","BillingV2StripeGateway.cs"));var start=source.IndexOf("UpdateRecurringAmountAsync",StringComparison.Ordinal);var method=source[start..source.IndexOf("public async Task<BillingV2StripeInvoiceSnapshot",start,StringComparison.Ordinal)];if(!method.Contains("[\"proration_behavior\"] = \"none\"",StringComparison.Ordinal)||method.Contains("payment_behavior",StringComparison.Ordinal)||!method.Contains("using var responseToDispose = response",StringComparison.Ordinal))throw new InvalidOperationException("S7 Stripe recurring mutation HTTP contract invalid");}
    private static async Task<DueDowngradeFixture> SeedDueDowngradeFixtureAsync(MySqlConnection db,DateTime requestedAt){var ids=await ReadIds(db);var suffix=Guid.NewGuid().ToString("N");var subscription=Guid.NewGuid().ToString("D");var item=Guid.NewGuid().ToString("D");var effective=requestedAt.AddMinutes(1);const long version=41;await Exec(db,"INSERT INTO billing_v2_subscriptions (id,customer_id,status,payment_mode,currency,started_at,current_period_started_at,current_period_ends_at,renews_at,discount_basis_points_snapshot,billing_model,version,created_at,updated_at) VALUES (@s,@c,'active','monthly','EUR',@n,@n,@e,@e,0,'v2',@v,@n,@n)",("@s",subscription),("@c",Guid.NewGuid().ToString("D")),("@n",requestedAt),("@e",effective),("@v",version));await Exec(db,"INSERT INTO billing_v2_subscription_items (id,subscription_id,service_id,tier_id,service_price_id,scope_type,quantity,amount_cents_snapshot,currency,discount_eligible_snapshot,pricing_representation,source,effective_from,status,created_at,updated_at) VALUES (@i,@s,@service,@tier,@price,'subscription',1,@amount,'EUR',1,'componentized','integration',@n,'active',@n,@n)",("@i",item),("@s",subscription),("@service",ids.Service),("@tier",ids.L),("@price",ids.MonthlyL),("@amount",ids.AmountL),("@n",requestedAt));await Component(db,item,ids.MonthlyL,"monthly","initial_subscription",ids.AmountL,requestedAt);return new(subscription,item,ids,requestedAt,effective,version,suffix);}
    private static async Task InsertSubscriptionChangeOneTimePriceAsync(MySqlConnection db,string serviceId,string tierId,DateTime validFrom,string suffix)
        => await Exec(db,"INSERT INTO billing_v2_service_prices (id,service_id,tier_id,price_code,price_version,amount_cents,currency,billing_cadence,charge_trigger,valid_from,status) VALUES (@id,@service,@tier,@code,1,499,'EUR','one_time','subscription_change',@valid,'active')",("@id",Guid.NewGuid().ToString("D")),("@service",serviceId),("@tier",tierId),("@code","INTEGRATION-CHANGE-FEE-"+suffix),("@valid",validFrom.AddMinutes(-1)));
    private static async Task AssertSubscriptionChangeOneTimeRefusedAsync(BillingV2SubscriptionChangeService service,MySqlConnection db,BillingV2SubscriptionChangeRequest request,DateTime now,string subscriptionId,string itemId,string currentTier,long version,string name)
    {
        var outboxBefore=await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE event_type='billing_v2.stripe.recurring_mutation_requested'");
        try { await service.RequestAsync(request,now,CancellationToken.None); throw new InvalidOperationException(name+" one-time change fee was accepted"); }
        catch (InvalidOperationException exception) when (exception.Message=="BILLING_V2_SUBSCRIPTION_CHANGE_ONE_TIME_NOT_SUPPORTED") { }
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_changes WHERE subscription_id=@id",("@id",subscriptionId)),name+" no change");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_billing_events WHERE subscription_id=@id",("@id",subscriptionId)),name+" no financial event");
        Equal(1,await Count(db,"SELECT COUNT(*) FROM billing_v2_subscription_items WHERE id=@id AND tier_id=@tier AND status='active' AND effective_until IS NULL",("@id",itemId),("@tier",currentTier)),name+" original entitlement unchanged");
        Equal(version,await ScalarLong(db,"SELECT version FROM billing_v2_subscriptions WHERE id=@id",("@id",subscriptionId)),name+" version unchanged");
        Equal(outboxBefore,await Count(db,"SELECT COUNT(*) FROM billing_v2_outbox_events WHERE event_type='billing_v2.stripe.recurring_mutation_requested'"),name+" no outbox");
        Equal(0,await Count(db,"SELECT COUNT(*) FROM billing_v2_one_time_component_consumptions consumption INNER JOIN billing_v2_subscription_item_price_components component ON component.id=consumption.subscription_item_price_component_id WHERE component.subscription_item_id=@item",("@item",itemId)),name+" no one-time consumption");
    }
    private sealed record UpgradeFixture(string SubscriptionId,string ItemId,Ids Ids,DateTime Now,string Suffix);
    private static async Task<UpgradeFixture> SeedUpgradeFixtureAsync(MySqlConnection db,DateTime now){var ids=await ReadIds(db);var suffix=Guid.NewGuid().ToString("N");var subscription=Guid.NewGuid().ToString("D");var item=Guid.NewGuid().ToString("D");await Exec(db,"INSERT INTO billing_v2_subscriptions (id,customer_id,status,payment_mode,currency,started_at,current_period_started_at,current_period_ends_at,renews_at,discount_basis_points_snapshot,billing_model,version,created_at,updated_at) VALUES (@s,@c,'active','monthly','EUR',@n,@n,@e,@e,0,'v2',1,@n,@n)",("@s",subscription),("@c",Guid.NewGuid().ToString("D")),("@n",now),("@e",now.AddMonths(1)));await Exec(db,"INSERT INTO billing_v2_subscription_items (id,subscription_id,service_id,tier_id,service_price_id,scope_type,quantity,amount_cents_snapshot,currency,discount_eligible_snapshot,pricing_representation,source,effective_from,status,created_at,updated_at) VALUES (@i,@s,@service,@tier,@price,'subscription',1,@amount,'EUR',1,'componentized','integration',@n,'active',@n,@n)",("@i",item),("@s",subscription),("@service",ids.Service),("@tier",ids.M),("@price",ids.MonthlyM),("@amount",ids.AmountM),("@n",now));await Component(db,item,ids.MonthlyM,"monthly","initial_subscription",ids.AmountM,now);return new(subscription,item,ids,now,suffix);}
    private static async Task SettleAsync(MySqlConnection db,string changeId,DateTime now){var id=await ScalarString(db,"SELECT id FROM billing_v2_billing_events WHERE subscription_change_id=@id",("@id",changeId));await using var tx=await db.BeginTransactionAsync();await BillingV2FinancialCoreStore.ApplySettlementAsync(db,tx,id,"settled","integration",now,CancellationToken.None);await tx.CommitAsync();}
    private sealed class InjectedCrashException : Exception { }
    private sealed class ThrowAtCheckpoint(string checkpoint) : IBillingV2SubscriptionChangeCheckpoint { public Task ReachedAsync(string value,CancellationToken ct){if(value==checkpoint)throw new InjectedCrashException();return Task.CompletedTask;} }
    private enum IndeterminateMode { Normal, TimeoutBefore, TimeoutAfter, Mismatch, Ambiguous, PersistentRetryable }
    private sealed class IndeterminateFakeStripeGateway(string subscriptionId,string itemId,long amountCents,string currency,int quantity,IndeterminateMode mode) : IBillingV2StripeGateway
    { private bool _first=true; public HashSet<string> Keys { get; }=[]; public long AmountCents {get;private set;}=amountCents; public int Quantity {get;private set;}=quantity; public int MutationEffectiveCount {get;private set;} public int PostCalls {get;private set;} public bool CanExecute=>true;
      public Task<BillingV2StripeCreateResult> CreateCheckoutSessionAsync(BillingV2StripeCheckoutRequest r,CancellationToken ct)=>throw new NotSupportedException(); public Task<BillingV2StripeSessionSnapshot?> GetCheckoutSessionAsync(string id,CancellationToken ct)=>Task.FromResult<BillingV2StripeSessionSnapshot?>(null); public Task<BillingV2StripeSessionSnapshot?> FindCheckoutSessionAsync(BillingV2StripeSessionLocator l,CancellationToken ct)=>Task.FromResult<BillingV2StripeSessionSnapshot?>(null); public Task<BillingV2StripeSubscriptionSnapshot?> GetSubscriptionAsync(string id,CancellationToken ct)=>Task.FromResult<BillingV2StripeSubscriptionSnapshot?>(id==subscriptionId?new BillingV2StripeSubscriptionSnapshot(subscriptionId,"active",null,null,new Dictionary<string,string>(),[new BillingV2StripeSubscriptionItemSnapshot(itemId,null,true,AmountCents,currency,Quantity)]):null);
      public Task<BillingV2StripeRecurringMutationResult> UpdateRecurringAmountAsync(BillingV2StripeRecurringMutationRequest r,CancellationToken ct){Keys.Add(r.IdempotencyKey);if(mode==IndeterminateMode.Ambiguous)return Task.FromResult(new BillingV2StripeRecurringMutationResult(false,"BILLING_V2_STRIPE_RECURRING_ITEM_AMBIGUOUS",null,false));if(mode==IndeterminateMode.Mismatch)return Task.FromResult(new BillingV2StripeRecurringMutationResult(false,"BILLING_V2_STRIPE_RECURRING_MUTATION_REFETCH_MISMATCH",null,true));if(mode==IndeterminateMode.PersistentRetryable){PostCalls++;return Task.FromResult(new BillingV2StripeRecurringMutationResult(false,"BILLING_V2_STRIPE_RECURRING_MUTATION_INDETERMINATE",null,true));}if(mode==IndeterminateMode.TimeoutBefore&&_first){_first=false;PostCalls++;throw new HttpRequestException();}if(mode==IndeterminateMode.TimeoutAfter&&_first){_first=false;PostCalls++;AmountCents=r.AmountCents;Quantity=r.Quantity;MutationEffectiveCount++;throw new HttpRequestException();}if(AmountCents==r.AmountCents&&Quantity==r.Quantity)return Task.FromResult(new BillingV2StripeRecurringMutationResult(true,"BILLING_V2_STRIPE_RECURRING_MUTATION_CONFIRMED_AFTER_REFETCH",subscriptionId,false));PostCalls++;AmountCents=r.AmountCents;Quantity=r.Quantity;MutationEffectiveCount++;return Task.FromResult(new BillingV2StripeRecurringMutationResult(true,"BILLING_V2_STRIPE_RECURRING_MUTATION_CONFIRMED",subscriptionId,false));}
      public Task<BillingV2StripeInvoiceSnapshot?> GetInvoiceAsync(string id,CancellationToken ct)=>Task.FromResult<BillingV2StripeInvoiceSnapshot?>(null); public Task<BillingV2StripeInvoiceSnapshot?> GetLatestInvoiceForSubscriptionAsync(string id,CancellationToken ct)=>Task.FromResult<BillingV2StripeInvoiceSnapshot?>(null); }
    private sealed record Ids(string Service,string M,string L,string S,string MonthlyM,string SetupM,string MonthlyL,string MonthlyS,long AmountM,long SetupAmount,long AmountL,long AmountS);
    private static async Task<Ids> ReadIds(MySqlConnection db){await using var q=db.CreateCommand();q.CommandText="SELECT s.id,tier_m.id,tier_l.id,tier_s.id,pm.id,ps.id,pl.id,px.id,pm.amount_cents,ps.amount_cents,pl.amount_cents,px.amount_cents FROM billing_v2_services s JOIN billing_v2_service_tiers tier_m ON tier_m.service_id=s.id AND tier_m.code='M' JOIN billing_v2_service_tiers tier_l ON tier_l.service_id=s.id AND tier_l.code='L' JOIN billing_v2_service_tiers tier_s ON tier_s.service_id=s.id AND tier_s.code='S' JOIN billing_v2_service_prices pm ON pm.service_id=s.id AND pm.tier_id=tier_m.id AND pm.billing_cadence='monthly' JOIN billing_v2_service_prices ps ON ps.service_id=s.id AND ps.tier_id=tier_m.id AND ps.billing_cadence='one_time' JOIN billing_v2_service_prices pl ON pl.service_id=s.id AND pl.tier_id=tier_l.id AND pl.billing_cadence='monthly' JOIN billing_v2_service_prices px ON px.service_id=s.id AND px.tier_id=tier_s.id AND px.billing_cadence='monthly' WHERE s.code='VPS-CLOUD' LIMIT 1";await using var r=await q.ExecuteReaderAsync();if(!await r.ReadAsync())throw new InvalidOperationException("VPS seed absent");return new(Convert.ToString(r.GetValue(0))!,Convert.ToString(r.GetValue(1))!,Convert.ToString(r.GetValue(2))!,Convert.ToString(r.GetValue(3))!,Convert.ToString(r.GetValue(4))!,Convert.ToString(r.GetValue(5))!,Convert.ToString(r.GetValue(6))!,Convert.ToString(r.GetValue(7))!,r.GetInt64(8),r.GetInt64(9),r.GetInt64(10),r.GetInt64(11));}
    private static async Task<string> Component(MySqlConnection db,string item,string price,string cadence,string trigger,long amount,DateTime now){var id=Guid.NewGuid().ToString("D");await Exec(db,"INSERT INTO billing_v2_subscription_item_price_components (id,subscription_item_id,service_price_id,billing_cadence,charge_trigger,amount_cents_snapshot,currency,discount_eligible_snapshot,effective_from,display_order,status,created_at) VALUES (@id,@item,@price,@cad,@trigger,@amount,'EUR',1,@n,0,'active',@n)",("@id",id),("@item",item),("@price",price),("@cad",cadence),("@trigger",trigger),("@amount",amount),("@n",now));return id;}
    private static async Task Exec(MySqlConnection db,string sql,params (string,object)[] p){await using var q=db.CreateCommand();q.CommandText=sql;foreach(var x in p)q.Parameters.AddWithValue(x.Item1,x.Item2);await q.ExecuteNonQueryAsync();}
    private static async Task<long> ScalarLong(MySqlConnection db,string sql,params (string,object)[] p){await using var q=db.CreateCommand();q.CommandText=sql;foreach(var x in p)q.Parameters.AddWithValue(x.Item1,x.Item2);return Convert.ToInt64(await q.ExecuteScalarAsync());}
    private static async Task<string> ScalarString(MySqlConnection db,string sql,params (string,object)[] p){await using var q=db.CreateCommand();q.CommandText=sql;foreach(var x in p)q.Parameters.AddWithValue(x.Item1,x.Item2);return Convert.ToString(await q.ExecuteScalarAsync())!;}
    private static async Task<int> Count(MySqlConnection db,string sql,params (string,object)[] p)=>(int)await ScalarLong(db,sql,p);
    private static void Equal(long expected,long actual,string name){if(expected!=actual)throw new InvalidOperationException($"{name}: {actual} != {expected}");}

    private sealed class FakeStripeGateway(string subscriptionId,string itemId,long amountCents,string currency,int quantity) : IBillingV2StripeGateway
    {
        private readonly HashSet<string> _keys=[]; public bool CanExecute=>true; public long AmountCents { get; private set; }=amountCents; public int Quantity { get; private set; }=quantity; public int MutationEffectiveCount { get; private set; }
        public Task<BillingV2StripeCreateResult> CreateCheckoutSessionAsync(BillingV2StripeCheckoutRequest r,CancellationToken ct)=>throw new NotSupportedException();
        public Task<BillingV2StripeSessionSnapshot?> GetCheckoutSessionAsync(string id,CancellationToken ct)=>Task.FromResult<BillingV2StripeSessionSnapshot?>(null);
        public Task<BillingV2StripeSessionSnapshot?> FindCheckoutSessionAsync(BillingV2StripeSessionLocator l,CancellationToken ct)=>Task.FromResult<BillingV2StripeSessionSnapshot?>(null);
        public Task<BillingV2StripeSubscriptionSnapshot?> GetSubscriptionAsync(string id,CancellationToken ct)=>Task.FromResult<BillingV2StripeSubscriptionSnapshot?>(id==subscriptionId?new BillingV2StripeSubscriptionSnapshot(subscriptionId,"active",null,null,new Dictionary<string,string>(),[new BillingV2StripeSubscriptionItemSnapshot(itemId,null,true,AmountCents,currency,Quantity)]):null);
        public async Task<BillingV2StripeRecurringMutationResult> UpdateRecurringAmountAsync(BillingV2StripeRecurringMutationRequest r,CancellationToken ct){if(r.ProviderSubscriptionId!=subscriptionId)return new(false,"fake subscription missing",null,false);if(_keys.Add(r.IdempotencyKey)){AmountCents=r.AmountCents;Quantity=r.Quantity;MutationEffectiveCount++;}var fetched=await GetSubscriptionAsync(subscriptionId,ct);var actual=fetched!.Items!.Single();return actual.UnitAmountCents==r.AmountCents&&actual.Currency==r.Currency&&actual.Quantity==r.Quantity?new(true,"confirmed",subscriptionId,false):new(false,"fake refetch mismatch",subscriptionId,false);}
        public Task<BillingV2StripeInvoiceSnapshot?> GetInvoiceAsync(string id,CancellationToken ct)=>Task.FromResult<BillingV2StripeInvoiceSnapshot?>(null);
        public Task<BillingV2StripeInvoiceSnapshot?> GetLatestInvoiceForSubscriptionAsync(string id,CancellationToken ct)=>Task.FromResult<BillingV2StripeInvoiceSnapshot?>(null);
    }
}
