using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Provisioning;
using MySqlConnector;

namespace Kermaria.ApiInternal.SmokeTests;

internal static class KoxoQualityMariaDbTests
{
    public const string TestHost = "KERMARIA-SRV-06.home.bzh";
    public const string TestDatabase = "kermaria_koxo_quality_test_dev";

    public static bool IsApprovedTarget(string? connection, string? optIn)
    {
        if (optIn != "true" || string.IsNullOrWhiteSpace(connection)) return false;
        try
        {
            var target = new MySqlConnectionStringBuilder(connection);
            return target.Database == TestDatabase &&
                ((string.Equals(target.Server, TestHost, StringComparison.OrdinalIgnoreCase) && target.Port == 3306)
                || (target.Server == "127.0.0.1" && target.Port == 33398));
        }
        catch (ArgumentException) { return false; }
    }

    public static void VerifyTargetGuard()
    {
        var sample = $"Server={TestHost};Database={TestDatabase};User ID=fake;Password=fake;";
        Check(IsApprovedTarget(sample, "true"), "Exact approved target accepted");
        Check(!IsApprovedTarget(sample, null), "Missing opt-in refused");
        Check(!IsApprovedTarget(sample.Replace(TestDatabase,"kermaria_dev"), "true"), "Operational DEV refused");
        Check(!IsApprovedTarget(sample.Replace(TestDatabase,"kermaria"), "true"), "Production refused");
        Check(!IsApprovedTarget(sample.Replace(TestHost,"another-host"), "true"), "Wrong host refused");
        Check(IsApprovedTarget($"Server=127.0.0.1;Port=33398;Database={TestDatabase};", "true"), "Dedicated local instance accepted");
        Check(!IsApprovedTarget($"Server=127.0.0.1;Port=3306;Database={TestDatabase};", "true"), "Default local server refused");
    }

    public static async Task<int> RunAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("BILLING_V2_TEST_MARIADB_CONNECTION");
        if (!IsApprovedTarget(connectionString, Environment.GetEnvironmentVariable("RUN_KOXO_QUALITY_SQL_TESTS")))
        {
            Console.WriteLine("NOT RUN: exact dedicated target and explicit opt-in required. No SQL connection opened.");
            return 2;
        }
        // Migration incorporee a CE build : un lancement sur SRV-13 ne lit
        // ni un autre checkout ni une migration modifiee apres compilation.
        await using var migrationStream = typeof(KoxoQualityMariaDbTests).Assembly
            .GetManifestResourceStream("KoxoQualityIntentMigration.sql")
            ?? throw new InvalidOperationException("Embedded migration missing");
        using var migrationReader = new StreamReader(migrationStream);
        var migration = await migrationReader.ReadToEndAsync();
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        Check((string?)await Scalar(connection,"SELECT DATABASE();") == TestDatabase,"Connected database mismatch");
        if (new MySqlConnectionStringBuilder(connectionString!).Server == "127.0.0.1")
        {
            var expectedDirectory=Environment.GetEnvironmentVariable("KOXO_QUALITY_TEST_EXPECTED_DATADIR");
            Check(!string.IsNullOrWhiteSpace(expectedDirectory),"Local data directory proof required");
            var actualDirectory=(string)(await Scalar(connection,"SELECT @@datadir;"))!;
            Check(string.Equals(Path.GetFullPath(actualDirectory).TrimEnd('\\','/'),
                Path.GetFullPath(expectedDirectory!).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase),"Wrong local instance data directory");
        }
        Check(Convert.ToInt32(await Scalar(connection,"SELECT GET_LOCK('koxo-quality-test-harness',0);")) == 1,"Test harness already running");
        try
        {
            Check(Convert.ToInt32(await Scalar(connection,"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE();")) == 0,
                "Dedicated test database must be empty. No existing table is changed or deleted.");
            var repository = new MariaDbKoxoQualityIntentRepository(new(
                PortalPersistenceMode.MariaDb,"mariadb",connectionString,"dedicated-test",true));
            var customerA=Guid.NewGuid().ToString("D"); var customerB=Guid.NewGuid().ToString("D");
            var alice=Guid.NewGuid().ToString("D"); var bob=Guid.NewGuid().ToString("D");
            await ExpectInvalid(() => repository.ReadAsync(customerA,default),"KOXO_QUALITY_INTENT_SCHEMA_MISSING");
            Check(Convert.ToInt32(await Scalar(connection,"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE();")) == 0,
                "Repository must never create its own schema");
            await Execute(connection,"CREATE TABLE customers(id CHAR(36) PRIMARY KEY) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");
            await Execute(connection,"CREATE TABLE portal_users(id CHAR(36) PRIMARY KEY, customer_id CHAR(36) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");
            await Execute(connection,migration);
            await Execute(connection,"INSERT INTO customers(id) VALUES (@a),(@b);",("@a",customerA),("@b",customerB));
            await Execute(connection,"INSERT INTO portal_users(id,customer_id) VALUES (@a,@ca),(@b,@cb);",("@a",alice),("@b",bob),("@ca",customerA),("@cb",customerB));
            IReadOnlyDictionary<string,IReadOnlyList<string>> For(string id, params string[] groups)
                => new Dictionary<string,IReadOnlyList<string>>{[id]=groups};

            var concurrent=await Task.WhenAll(
                repository.PublishAsync(customerA,0,For(alice,"GG_VPN_TEST"),default),
                repository.PublishAsync(customerA,0,For(alice,"GG_RDS_TEST"),default));
            Check(concurrent.Count(result=>result.Accepted)==1,"Exactly one initial publication wins");
            var initial=(await repository.ReadAsync(customerA,default))!;
            Check(initial is {Revision:1,AppliedRevision:0,Pending:true},"Initial publication is pending");
            await ExpectInvalid(() => repository.PublishAsync(customerA,1,For(bob,"GG_TEST"),default),"KOXO_QUALITY_IDENTITY_CUSTOMER_MISMATCH");
            Check((await repository.ReadAsync(customerA,default))==initial,"Foreign identity rejection must roll back");
            var replay=await repository.PublishAsync(customerA,1,KoxoQualityIntentPolicy.Deserialize(initial.DesiredJson),default);
            Check(replay.Accepted && !replay.Changed,"Replay must not create a new revision");

            var claims=await Task.WhenAll(repository.ClaimNextAsync(default),repository.ClaimNextAsync(default));
            Check(claims.Count(lease=>lease is not null)==1,"Only one worker holds a lease");
            var old=claims.Single(lease=>lease is not null)!;
            var removal=await repository.PublishAsync(customerA,1,For(alice),default);
            Check(removal.Accepted && removal.Intent!.Revision==2,"Removal is a new revision");
            Check(!await repository.FinishAsync(old,true,"KOXO_QUALITIES_APPLIED",default),"Old success cannot acknowledge removal");
            var removalLease=(await repository.ClaimNextAsync(default))!;
            Check(removalLease.Intent.Revision==2,"New revision is immediately eligible");
            Check(await repository.FinishAsync(removalLease,false,"KOXO_QUALITIES_UNVERIFIED",default),"Failure releases the lease");
            Check(await repository.ClaimNextAsync(default) is null,"Backoff prevents immediate retry");
            await Execute(connection,"UPDATE koxo_quality_intents SET next_attempt_at=UTC_TIMESTAMP(6) WHERE customer_id=@id;",("@id",customerA));
            var expiring=(await repository.ClaimNextAsync(default))!;
            await Execute(connection,"UPDATE koxo_quality_intents SET lease_expires_at=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE customer_id=@id;",("@id",customerA));
            var retry=(await repository.ClaimNextAsync(default))!;
            Check(retry.Token!=expiring.Token && retry.Attempt==expiring.Attempt+1,"Crash recovery gets a new lease");
            Check(!await repository.FinishAsync(expiring,true,"KOXO_QUALITIES_APPLIED",default),"Expired worker cannot acknowledge");
            Check(await repository.FinishAsync(retry,true,"KOXO_QUALITIES_APPLIED",default),"Current verified revision acknowledged");
            Check((await repository.ReadAsync(customerA,default)) is {Revision:2,AppliedRevision:2,Pending:false},"Removal converged");
            Check(await repository.ClaimNextAsync(default) is null,"Completed revision is not retried");

            await repository.PublishAsync(customerB,0,For(bob,"GG_OTHER_TEST"),default);
            var other=(await repository.ClaimNextAsync(default))!;
            Check(other.CustomerId==customerB,"Second client has independent work");
            Check(!await repository.FinishAsync(other with {CustomerId=customerA},true,"KOXO_QUALITIES_APPLIED",default),"Lease cannot cross customers");
            Check((await repository.ReadAsync(customerA,default))!.AppliedRevision==2,"Other client is untouched");
            Console.WriteLine("KoXo quality MariaDB transactions PASS. Dedicated fixture tables retained for inspection; no operational database touched.");
            return 0;
        }
        finally { await Scalar(connection,"SELECT RELEASE_LOCK('koxo-quality-test-harness');"); }
    }

    private static async Task<object?> Scalar(MySqlConnection connection,string sql)
    { await using var command=connection.CreateCommand();command.CommandText=sql;return await command.ExecuteScalarAsync(); }
    private static async Task Execute(MySqlConnection connection,string sql,params (string Name,object Value)[] parameters)
    { await using var command=connection.CreateCommand();command.CommandText=sql;foreach(var (name,value) in parameters)command.Parameters.AddWithValue(name,value);await command.ExecuteNonQueryAsync(); }
    private static async Task ExpectInvalid<T>(Func<Task<T>> action,string code)
    { try{await action();}catch(InvalidOperationException error)when(error.Message==code){return;}throw new InvalidOperationException("Expected bounded rejection: "+code); }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
