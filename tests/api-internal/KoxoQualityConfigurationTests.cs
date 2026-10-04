using Kermaria.ApiInternal.Data.Configuration;
using Microsoft.Extensions.Configuration;
using Kermaria.ApiInternal.Services.Provisioning;
using Kermaria.ApiInternal.Services.ActiveDirectory;

namespace Kermaria.ApiInternal.SmokeTests;

internal static class KoxoQualityConfigurationTests
{
    public static void Run()
    {
        var values = new Dictionary<string,string?> { ["APP_ENV"]="Development" };
        var sql = new SqlRuntimeConfiguration(PortalPersistenceMode.MariaDb,"mariadb","Server=test.invalid;Database=kermaria_dev;", "test",true);
        var webhook = new KoxoSyncWebhookRuntimeConfiguration(new Uri("http://test.invalid:8043/internal/koxo/sync/"),"FAKE",TimeSpan.FromSeconds(10),true);
        KoxoQualityRuntimeConfiguration Resolve(SqlRuntimeConfiguration? target=null, KoxoSyncWebhookRuntimeConfiguration? receiver=null, bool provisioning=true, bool controlledAd=true)
            => KoxoQualityRuntimeConfiguration.Resolve(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),target??sql,receiver??webhook,provisioning,controlledAd);
        Check(!Resolve().Enabled,"Default must remain disabled");
        values[KoxoQualityRuntimeConfiguration.Variable]="true";
        Check(Resolve().Enabled,"Complete DEV configuration accepted");
        values["BILLING_V2_KOXO_EMPTY_QUALITY_GROUP"]="GG_NO_ACCESS_E2E_DEV";
        Check(Resolve().EmptyGroup=="GG_NO_ACCESS_E2E_DEV","Neutral transport group can be configured in DEV");
        foreach(var invalid in new[]{"GG_NO_ACCESS_E2E_DEV,GG_ADMIN","GG_NO_ACCESS_E2E_DEV\n","GG_PROD"}) {
            values["BILLING_V2_KOXO_EMPTY_QUALITY_GROUP"]=invalid;
            Reject(()=>Resolve(),"EMPTY_GROUP_INVALID");
        }
        values.Remove("BILLING_V2_KOXO_EMPTY_QUALITY_GROUP");
        foreach(var environment in new[]{"Production","Staging",""}) {
            values["APP_ENV"]=environment; Reject(()=>Resolve(),"DEV_ONLY");
        }
        values["APP_ENV"]="Development";
        Reject(()=>Resolve(sql with {Mode=PortalPersistenceMode.Mock}),"MARIADB_REQUIRED");
        Reject(()=>Resolve(sql with {ConnectionString="Server=test.invalid;Database=kermaria;"}),"WRONG_DATABASE");
        Reject(()=>Resolve(receiver:webhook with {Url=new Uri("http://test.invalid:8042/internal/koxo/sync/")}),"DEV_RECEIVER_REQUIRED");
        Reject(()=>Resolve(receiver:webhook with {BearerToken=null}),"DEV_RECEIVER_REQUIRED");
        Reject(()=>Resolve(provisioning:false),"PROVISIONING_DISABLED");
        Reject(()=>Resolve(controlledAd:false),"CONTROLLED_AD_REQUIRED");
        values[KoxoQualityRuntimeConfiguration.Variable]="maybe"; Reject(()=>Resolve(),"ENABLED_INVALID");
        values[KoxoQualityRuntimeConfiguration.Variable]="false"; values["APP_ENV"]="Production";
        Check(!Resolve().Enabled,"Disabled flag leaves production unchanged");
        const string devRoot="OU=CLIENTS DEV,DC=clients,DC=home,DC=bzh";
        var adScope=new AdRuntimeConfiguration(AdIntegrationMode.ControlledWrite,"clients.home.bzh",null,null,[devRoot],false,null,null,1000,1000,100,true);
        var groupConfig=new SubscriptionProvisioningRuntimeConfiguration(new Dictionary<string,IReadOnlyList<string>>(),new Dictionary<string,string>(),
            new Dictionary<string,string>{{"GG_DEV","CN=GG_DEV,"+devRoot},{"GG_PROD","CN=GG_PROD,OU=CLIENTS,DC=clients,DC=home,DC=bzh"}},1,1);
        Check(KoxoQualityGroupScope.Allows(adScope,groupConfig,["GG_DEV"]),"Configured DEV group allowed");
        Check(!KoxoQualityGroupScope.Allows(adScope,groupConfig,["GG_PROD"]),"A configured DN outside allowed roots is still forbidden");
        Check(!KoxoQualityGroupScope.Allows(adScope,groupConfig,["GG_UNKNOWN"]),"Unknown group forbidden");
        foreach(var mode in new[]{AdIntegrationMode.Disabled,AdIntegrationMode.ReadOnly,AdIntegrationMode.Mock})
            Check(!KoxoQualityGroupScope.Allows(adScope with {Mode=mode},groupConfig,["GG_DEV"]),"KoXo must not bypass AD mode");
        Check(!KoxoQualityGroupScope.Allows(null,groupConfig,[]),"Missing scope cannot silently authorize a revocation");
        var pathScope=new ActiveDirectoryPathScope(devRoot);
        Check(pathScope.BuildSearchRootDn("user","DEV-CLI-PROBE",true)=="OU=DEV-CLI-PROBE,"+devRoot,
            "KoXo users live directly under their customer OU");
        Check(pathScope.BuildSearchRootDn("group","DEV-CLI-PROBE",true)=="OU=DEV-CLI-PROBE,"+devRoot,
            "KoXo group lookup remains bounded to the customer OU");
        Check(pathScope.BuildSearchRootDn("user","DEV-CLI-PROBE",false)=="OU=Users,OU=DEV-CLI-PROBE,"+devRoot,
            "Non-KoXo hierarchy remains unchanged");
        Check(pathScope.ExtractCustomerReference("CN=probe,"+pathScope.BuildSearchRootDn("user","DEV-CLI-PROBE",true))=="DEV-CLI-PROBE",
            "Customer isolation still resolves the same customer");
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Reject(Func<KoxoQualityRuntimeConfiguration> action,string code)
    {try{action();}catch(InvalidOperationException error)when(error.Message.EndsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected rejection "+code);}
}
