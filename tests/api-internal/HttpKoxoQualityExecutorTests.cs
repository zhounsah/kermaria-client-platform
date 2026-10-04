using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Provisioning;

namespace Kermaria.ApiInternal.SmokeTests;

internal static class HttpKoxoQualityExecutorTests
{
    public static async Task RunAsync()
    {
        await Case(HttpStatusCode.OK, csv:true, xml:true, ad:true, expectSuccess:true, expectTrigger:0);
        await Case(HttpStatusCode.OK, csv:false, xml:true, ad:true, expectSuccess:false, expectTrigger:1);
        await Case(HttpStatusCode.OK, csv:true, xml:false, ad:true, expectSuccess:false, expectTrigger:0);
        await Case(HttpStatusCode.OK, csv:true, xml:true, ad:false, expectSuccess:false, expectTrigger:0);
        await Case(HttpStatusCode.Conflict, csv:true, xml:true, ad:true, expectSuccess:false, expectTrigger:0);
        await Case(HttpStatusCode.NotFound, csv:true, xml:true, ad:true, expectSuccess:false, expectTrigger:0, expectError:true);
        await Case(HttpStatusCode.Accepted, csv:true, xml:true, ad:true, expectSuccess:false, expectTrigger:0, expectError:true);
        await Case(HttpStatusCode.OK, csv:true, xml:true, ad:true, expectSuccess:false, expectTrigger:0, wrongRevision:true, expectError:true);
        Console.WriteLine("HTTP KoXo quality executor tests passed.");
    }

    private static async Task Case(HttpStatusCode status, bool csv, bool xml, bool ad,
        bool expectSuccess, int expectTrigger, bool wrongRevision=false, bool expectError=false)
    {
        const string identity="00000000-0000-0000-0000-000000000001";
        const string customer="00000000-0000-0000-0000-000000000002";
        const string reference="DEV-CLI-PROBE";
        const string group="GG_VPN_E2E_DEV";
        const string groupDn="CN=GG_VPN_E2E_DEV,OU=CLIENTS DEV,DC=clients,DC=home,DC=bzh";
        const string userDn="CN=probe,OU=DEV-CLI-PROBE,OU=CLIENTS DEV,DC=clients,DC=home,DC=bzh";
        var intent=new KoxoQualityIntent(1, 0, KoxoQualityIntentPolicy.Serialize(new Dictionary<string,IReadOnlyList<string>>{[identity]=[group]}));
        var lease=new KoxoQualityLease(customer,intent,"lease",1);
        var candidate=new KoxoExportCandidate(identity,reference,"CLI-D999903","monsieur","Probe","Test","1990-01-01","probe@example.invalid",CustomerId:customer);
        var user=new AdDirectoryObjectSummary(identity,"sid-user","user","probe",null,"Probe",userDn,reference,false);
        var link=new PortalUserAdLinkRecord("link",customer,reference,identity,identity,"sid-user","probe",null,"Probe",userDn,null,null,null,null,null,null);
        var groups=new AdDirectoryObjectSummary[]{new(customer,"sid-group","group",group,null,group,groupDn,reference,false)};
        var repo=Proxy<IKoxoRepository>((method,_)=>method.Name=="ListExportCandidatesAsync"
            ? Task.FromResult<IReadOnlyList<KoxoExportCandidate>>([candidate]) : throw new InvalidOperationException(method.Name));
        var links=Proxy<IActiveDirectoryLinkRepository>((method,_)=>method.Name=="GetUserLinksByPortalUserIdAsync"
            ? Task.FromResult<IReadOnlyList<PortalUserAdLinkRecord>>([link]) : throw new InvalidOperationException(method.Name));
        var resolver=Proxy<IAdGroupProvisioner>((method,_)=>method.Name=="ResolveUserByEmployeeNumberAsync"
            ? Task.FromResult<AdDirectoryObjectSummary?>(user) : throw new InvalidOperationException("Unexpected AD write: "+method.Name));
        var directory=Proxy<IActiveDirectoryService>((method,_)=>method.Name switch {
            "get_ModeName"=>"read_only",
            "GetUserEffectiveGroupsAsync"=>Task.FromResult(new AdServiceResult<IReadOnlyList<AdDirectoryObjectSummary>>(200,"OK","",ad?groups:[])),
            _=>throw new InvalidOperationException("Unexpected AD operation: "+method.Name)
        });
        var triggers=0;
        var trigger=Proxy<IKoxoSyncWebhookTriggerService>((method,_)=>{triggers++;return Task.CompletedTask;});
        using var client=new HttpClient(new Handler(async request=>{
            Check(request.RequestUri!.AbsolutePath=="/internal/koxo/qualities/proof/", "Dedicated proof route required");
            Check(request.Headers.Authorization?.Scheme=="Bearer", "Proof must authenticate");
            var body=await request.Content!.ReadAsStringAsync();
            Check(body.Contains("CLI-D999903") && body.Contains("GG_VPN_E2E_DEV"), "Proof must bind requested target");
            return new HttpResponseMessage(status) { Content=JsonContent.Create(new {
                protocolVersion=1,customerId=customer,revision=wrongRevision?2:1,desiredSha256=KoxoQualityDispatcher.Digest(intent),
                targets=new[]{new{portalUserId=identity,uniqueId="CLI-D999903",userId="probe",csvVerified=csv,koxoVerified=xml}}
            })};
        }));
        var factory=Proxy<IHttpClientFactory>((_,_)=>client);
        var configuration=new SubscriptionProvisioningRuntimeConfiguration(
            new Dictionary<string,IReadOnlyList<string>>(),new Dictionary<string,string>(),new Dictionary<string,string>{{group,groupDn}},1,1);
        var executor=new HttpKoxoQualityIntentExecutor(repo,links,directory,resolver,configuration,
            new(new Uri("https://koxo.example.invalid/internal/koxo/sync/"),"FAKE-TEST-TOKEN",TimeSpan.FromSeconds(10),false),trigger,factory,
            new(AdIntegrationMode.ControlledWrite,"clients.home.bzh",null,null,["OU=CLIENTS DEV,DC=clients,DC=home,DC=bzh"],false,null,null,1000,1000,100,true));
        try {
            var result=await executor.ExecuteAndVerifyAsync(lease,default);
            Check(!expectError,"Expected proof refusal");
            Check(KoxoQualityDispatcher.Matches(lease,result)==expectSuccess,"Unexpected verified result");
        } catch (Exception error) when (expectError && error is HttpRequestException or InvalidOperationException) { }
        Check(triggers==expectTrigger,"Wrong sync trigger count");
    }

    private static T Proxy<T>(Func<MethodInfo,object?[]?,object?> action) where T:class
    { var instance=DispatchProxy.Create<T,QualityExecutorTestProxy>();((QualityExecutorTestProxy)(object)instance).Action=action;return instance; }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request); }
}

public class QualityExecutorTestProxy:DispatchProxy
{
    public Func<MethodInfo,object?[]?,object?> Action {get;set;}=null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Action(method!,args);
}
