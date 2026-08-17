using System.Net;
using System.Text;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.Provisioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kermaria.ApiInternal.SmokeTests;

/// <summary>
/// Verrouille l'application reelle d'un quota KoXo : configuration, transport,
/// lecture des issues, et la regle qui subordonne les droits annuaire au socle
/// de stockage.
/// </summary>
/// <remarks>
/// <para>
/// Aucun de ces tests ne joint SRV-21 : le transport est double par un
/// gestionnaire de messages, ce qui permet de verifier exactement ce que le
/// provider fait d'une reponse — y compris d'une reponse qui ment.
/// </para>
/// <para>
/// La resolution des cibles n'est pas rejouee ici. Le provider ne resout rien :
/// il recoit des cibles deja verifiees. C'est precisement ce qui est verrouille
/// par les tests de <c>--billing-v2-koxo-storage-target</c> et
/// <c>--billing-v2-koxo-storage-resolution</c>.
/// </para>
/// </remarks>
public static class BillingV2KoxoStorageProviderTests
{
    private const string CustomerId = "22222222-2222-2222-2222-222222222222";
    private const string PortalUserId = "44444444-4444-4444-4444-444444444444";
    private const string ObjectGuid = "55555555-5555-5555-5555-555555555555";
    private const string OtherObjectGuid =
        "66666666-6666-6666-6666-666666666666";
    private const string CorrelationId = "corr-1";

    public static async Task RunAsync()
    {
        VerifyAbsentConfigurationStaysDormant();
        VerifyHalfConfiguredEndpointIsRefused();
        VerifyPlainHttpIsRefusedUnlessExplicitlyAllowed();
        VerifyDormantProviderBlocksAnyRealTarget();

        await VerifyAppliedTargetIsAcceptedAsync();
        await VerifyNoopTargetIsAcceptedAsync();
        await VerifyBlockedReductionIsNotASuccessAsync();
        await VerifyMissingTargetIsNotASuccessAsync();
        await VerifyUnauthorizedIsNotASuccessAsync();
        await VerifyResponseAboutAnotherTargetIsRefusedAsync();
        await VerifyAppliedWithoutProofIsRefusedAsync();
        await VerifyUnknownStatusIsRefusedAsync();
        await VerifyTransportFailureIsNotASuccessAsync();
        await VerifyPartialBatchFailsGloballyAsync();
        await VerifyRequestCarriesTheExactTargetAsync();
        await VerifySharedTargetCarriesNoUserAsync();

        VerifyStorageGateGovernsDependentRights();

        Console.WriteLine(
            "Tests provider de stockage KoXo Billing V2 reussis.");
    }

    // ------------------------------------------------------------------
    // Configuration : dormante par defaut, jamais devinee.
    // ------------------------------------------------------------------

    private static void VerifyAbsentConfigurationStaysDormant()
    {
        var configuration = Resolve(new Dictionary<string, string?>());

        // Aucune adresse par defaut : un point d'entree devine ferait porter un
        // quota reel a un hote qui n'est pas celui du client.
        Ensure(
            !configuration.Configured
            && configuration.Url is null
            && configuration.BearerToken is null,
            "Sans configuration, le provider de stockage doit rester dormant.");
    }

    private static void VerifyHalfConfiguredEndpointIsRefused()
    {
        // Une configuration a moitie posee est une erreur d'exploitation, pas
        // une intention de rester dormant.
        Ensure(
            Throws(() => Resolve(new Dictionary<string, string?>
            {
                ["BILLING_V2_KOXO_STORAGE_TOKEN"] = "secret",
            }))
            && Throws(() => Resolve(new Dictionary<string, string?>
            {
                ["BILLING_V2_KOXO_STORAGE_URL"] = "https://srv-21.invalid/x/",
            })),
            "Une configuration incomplete doit echouer au demarrage, pas retomber sur le provider dormant.");
    }

    private static void VerifyPlainHttpIsRefusedUnlessExplicitlyAllowed()
    {
        var settings = new Dictionary<string, string?>
        {
            ["BILLING_V2_KOXO_STORAGE_URL"] =
                "http://srv-21.invalid:8042/internal/koxo/storage/reconcile/",
            ["BILLING_V2_KOXO_STORAGE_TOKEN"] = "secret",
        };

        Ensure(
            Throws(() => Resolve(settings)),
            "Un point d'entree en clair doit etre refuse tant qu'il n'est pas explicitement autorise.");

        settings["BILLING_V2_KOXO_STORAGE_ALLOW_INSECURE_HTTP"] = "true";
        Ensure(
            Resolve(settings).Configured,
            "L'autorisation explicite du transport en clair doit rester possible pour le reseau prive existant.");
    }

    private static void VerifyDormantProviderBlocksAnyRealTarget()
    {
        var dormant = DormantBillingV2KoxoStorageProvider.Instance;
        var empty = dormant
            .ApplyAsync([], CorrelationId, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var withTarget = dormant
            .ApplyAsync([UserTarget()], CorrelationId, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Ensure(
            empty.Succeeded
            && !withTarget.Succeeded
            && withTarget.ReasonCode
                == BillingV2KoxoStorageApplyReasons.ProviderNotConfigured,
            "Le provider dormant doit bloquer tout quota reel au lieu de rendre un succes vide.");
    }

    // ------------------------------------------------------------------
    // Lecture des issues.
    // ------------------------------------------------------------------

    private static async Task VerifyAppliedTargetIsAcceptedAsync()
    {
        var result = await ApplyAsync(
            Respond("applied", "xml_verified", UserTarget().TargetKey));

        Ensure(
            result.Succeeded
            && result.Results.Count == 1
            && result.Results[0].Outcome
                == BillingV2KoxoStorageOutcome.Applied
            && result.Results[0].Verification == "xml_verified",
            "Une application prouvee doit etre acceptee et garder son niveau de preuve.");
    }

    private static async Task VerifyNoopTargetIsAcceptedAsync()
    {
        var result = await ApplyAsync(
            Respond("noop", "xml_verified", UserTarget().TargetKey));

        // Un quota deja bon est un succes, mais reste distinguable d'une
        // application : sans cette distinction, une reconciliation qui ne
        // converge jamais serait invisible.
        Ensure(
            result.Succeeded
            && result.Results[0].Outcome == BillingV2KoxoStorageOutcome.Noop,
            "Un quota deja conforme doit reussir tout en restant distinguable d'une application.");
    }

    private static async Task VerifyBlockedReductionIsNotASuccessAsync()
    {
        var result = await ApplyAsync(
            Respond("blocked_reduction", "none", UserTarget().TargetKey));

        Ensure(
            !result.Succeeded
            && result.Results[0].Outcome
                == BillingV2KoxoStorageOutcome.BlockedReduction,
            "Une reduction refusee ne doit jamais compter comme un provisioning reussi.");
    }

    private static async Task VerifyMissingTargetIsNotASuccessAsync()
    {
        var result = await ApplyAsync(
            Respond("not_materialized", "none", UserTarget().TargetKey));

        Ensure(
            !result.Succeeded
            && result.Results[0].Outcome
                == BillingV2KoxoStorageOutcome.TargetNotFound,
            "Une fiche KoXo absente doit bloquer, jamais etre creee ni ignoree.");
    }

    private static async Task VerifyUnauthorizedIsNotASuccessAsync()
    {
        var result = await ApplyAsync(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });

        Ensure(
            !result.Succeeded
            && result.Results[0].ReasonCode
                == BillingV2KoxoStorageApplyReasons.Unauthorized,
            "Un refus d'authentification doit etre nomme, pas confondu avec une panne quelconque.");
    }

    private static async Task VerifyResponseAboutAnotherTargetIsRefusedAsync()
    {
        var result = await ApplyAsync(
            Respond("applied", "xml_verified", "user:" + OtherObjectGuid));

        // Un accuse qui ne designe pas la cible envoyee ne prouve rien sur
        // elle : le rapprochement se fait sur la cle, pas sur l'ordre.
        Ensure(
            !result.Succeeded
            && result.Results[0].ReasonCode
                == BillingV2KoxoStorageApplyReasons.ResponseMismatch,
            "Une reponse portant sur une autre cible ne doit jamais valider celle-ci.");
    }

    private static async Task VerifyAppliedWithoutProofIsRefusedAsync()
    {
        var result = await ApplyAsync(
            Respond("applied", "none", UserTarget().TargetKey));

        // Une application annoncee sans etat relu n'est pas une application.
        Ensure(
            !result.Succeeded
            && result.Results[0].ReasonCode
                == BillingV2KoxoStorageApplyReasons.ResponseMismatch,
            "Une application sans preuve relue doit etre refusee.");
    }

    private static async Task VerifyUnknownStatusIsRefusedAsync()
    {
        var result = await ApplyAsync(
            Respond("probablement_ok", "xml_verified", UserTarget().TargetKey));

        Ensure(
            !result.Succeeded
            && result.Results[0].Outcome
                == BillingV2KoxoStorageOutcome.Failed,
            "Un statut inconnu doit se lire comme un echec, jamais comme un succes optimiste.");
    }

    private static async Task VerifyTransportFailureIsNotASuccessAsync()
    {
        var result = await ApplyAsync(
            _ => throw new HttpRequestException("SRV-21 unreachable."));

        Ensure(
            !result.Succeeded
            && result.Results[0].ReasonCode
                == BillingV2KoxoStorageApplyReasons.TransportFailed,
            "Un point d'entree injoignable doit echouer, jamais laisser passer le provisioning.");
    }

    private static async Task VerifyPartialBatchFailsGloballyAsync()
    {
        var targets = new[] { UserTarget(), SharedTarget(), SecondUserTarget() };
        var handler = new StubHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            if (body.Contains("group:CLIENTS/CLI-000042", StringComparison.Ordinal))
            {
                return Json("{\"status\":\"blocked_reduction\",\"reasonCode\":\"X\",\"verification\":\"none\",\"targetKey\":\"group:CLIENTS/CLI-000042\"}");
            }

            return Json(
                "{\"status\":\"applied\",\"reasonCode\":\"X\",\"verification\":\"xml_verified\",\"targetKey\":\"user:"
                + ObjectGuid
                + "\"}");
        });

        var result = await Provider(handler).ApplyAsync(
            targets,
            CorrelationId,
            CancellationToken.None);

        // Le lot s'arrete a la premiere cible non appliquee, et la suivante est
        // rendue comme NON TENTEE : la declarer echouee affirmerait un constat
        // qui n'a pas eu lieu, l'omettre ferait croire a un lot plus petit.
        Ensure(
            !result.Succeeded
            && result.ReasonCode
                == BillingV2KoxoStorageApplyReasons.BatchIncomplete
            && result.Results.Count == 3
            && result.Results[0].Succeeded
            && !result.Results[1].Succeeded
            && result.Results[2].ReasonCode
                == BillingV2KoxoStorageApplyReasons.NotAttempted,
            "Un lot partiellement applique doit echouer globalement et rester lisible cible par cible.");
    }

    private static async Task VerifyRequestCarriesTheExactTargetAsync()
    {
        string? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request.Content!.ReadAsStringAsync()
                .GetAwaiter()
                .GetResult();
            return Json(
                "{\"status\":\"noop\",\"reasonCode\":\"X\",\"verification\":\"xml_verified\",\"targetKey\":\"user:"
                + ObjectGuid
                + "\"}");
        });

        await Provider(handler).ApplyAsync(
            [UserTarget()],
            CorrelationId,
            CancellationToken.None);

        // Le recepteur ne doit rien avoir a deviner : le login est transmis tel
        // qu'il a ete lu dans l'annuaire, et la quantite est deja en mebioctets.
        Ensure(
            captured is not null
            && captured.Contains("\"userId\":\"zachary.hounsahou\"", StringComparison.Ordinal)
            && captured.Contains("\"primaryGroup\":\"CLIENTS\"", StringComparison.Ordinal)
            && captured.Contains("\"secondaryGroup\":\"CLI-000042\"", StringComparison.Ordinal)
            && captured.Contains("\"desiredQuotaMib\":32768", StringComparison.Ordinal)
            && captured.Contains("\"targetKind\":\"user\"", StringComparison.Ordinal),
            "La requete doit porter la cible exacte, sans rien laisser a deduire au recepteur.");
    }

    private static async Task VerifySharedTargetCarriesNoUserAsync()
    {
        string? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request.Content!.ReadAsStringAsync()
                .GetAwaiter()
                .GetResult();
            return Json(
                "{\"status\":\"noop\",\"reasonCode\":\"X\",\"verification\":\"xml_verified\",\"targetKey\":\"group:CLIENTS/CLI-000042\"}");
        });

        await Provider(handler).ApplyAsync(
            [SharedTarget()],
            CorrelationId,
            CancellationToken.None);

        // Un titulaire sur une cible partagee ferait poser le quota du client
        // sur le dossier d'une personne.
        Ensure(
            captured is not null
            && !captured.Contains("\"userId\"", StringComparison.Ordinal)
            && captured.Contains(
                "\"targetKind\":\"secondary_group\"",
                StringComparison.Ordinal),
            "Une cible partagee ne doit transporter aucun titulaire.");
    }

    // ------------------------------------------------------------------
    // Subordination des droits annuaire au socle de stockage.
    // ------------------------------------------------------------------

    private static void VerifyStorageGateGovernsDependentRights()
    {
        var resolved = BillingV2KoxoStorageTargetResolution.Success(
            [UserTarget()]);
        var refused = BillingV2KoxoStorageTargetResolution.Fail(
            BillingV2KoxoStorageTargetReasons.IdentityNotMaterialized);
        var applied = BillingV2KoxoStorageApplyResult.From(
        [
            new BillingV2KoxoStorageTargetResult(
                "item-a",
                UserTarget().TargetKey,
                BillingV2KoxoStorageOutcome.Applied,
                "X",
                "xml_verified"),
        ]);
        var incomplete = BillingV2KoxoStorageApplyResult.From(
        [
            new BillingV2KoxoStorageTargetResult(
                "item-a",
                UserTarget().TargetKey,
                BillingV2KoxoStorageOutcome.BlockedReduction,
                "X",
                "none"),
        ]);

        Ensure(
            BillingV2KoxoStorageGate
                .Evaluate(0, null, null).MayContinue,
            "Un abonnement sans quota ne doit pas etre bloque par une etape qui ne le concerne pas.");

        // Un stockage non resolu ou non applique doit interdire les droits qui
        // en dependent : un acces VPN ou RDS vers un environnement personnel
        // absent ouvre une session sur un poste vide.
        Ensure(
            !BillingV2KoxoStorageGate.Evaluate(1, refused, null).MayContinue
            && !BillingV2KoxoStorageGate.Evaluate(1, resolved, null).MayContinue
            && !BillingV2KoxoStorageGate
                .Evaluate(1, resolved, incomplete).MayContinue,
            "Un stockage bloque, echoue ou non tente doit empecher les droits annuaire dependants.");

        Ensure(
            BillingV2KoxoStorageGate.Evaluate(1, resolved, applied).MayContinue,
            "Un stockage reellement applique doit laisser le provisioning se poursuivre.");
    }

    // ------------------------------------------------------------------
    // Fabriques.
    // ------------------------------------------------------------------

    private static BillingV2KoxoStorageProviderConfiguration Resolve(
        Dictionary<string, string?> settings)
        => BillingV2KoxoStorageProviderConfiguration.Resolve(
            new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build());

    private static async Task<BillingV2KoxoStorageApplyResult> ApplyAsync(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
        => await Provider(new StubHandler(responder)).ApplyAsync(
            [UserTarget()],
            CorrelationId,
            CancellationToken.None);

    private static HttpBillingV2KoxoStorageProvider Provider(
        StubHandler handler)
        => new(
            new StubHttpClientFactory(handler),
            new BillingV2KoxoStorageProviderConfiguration(
                new Uri("https://srv-21.invalid/internal/koxo/storage/reconcile/"),
                "secret",
                TimeSpan.FromSeconds(30)),
            NullLogger<HttpBillingV2KoxoStorageProvider>.Instance);

    private static Func<HttpRequestMessage, HttpResponseMessage> Respond(
        string status,
        string verification,
        string targetKey)
        => _ => Json(
            "{\"status\":\""
            + status
            + "\",\"reasonCode\":\"X\",\"verification\":\""
            + verification
            + "\",\"targetKey\":\""
            + targetKey
            + "\"}");

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static BillingV2ResolvedKoxoStorageTarget UserTarget()
        => BillingV2ResolvedKoxoStorageTarget.ForUser(
            "item-a",
            32768,
            "CLI-000123",
            Link(ObjectGuid, "zachary.hounsahou"),
            "CLIENTS",
            "CLI-000042");

    private static BillingV2ResolvedKoxoStorageTarget SecondUserTarget()
        => BillingV2ResolvedKoxoStorageTarget.ForUser(
            "item-c",
            16384,
            "CLI-000124",
            Link(OtherObjectGuid, "marie.cecile"),
            "CLIENTS",
            "CLI-000042");

    private static BillingV2ResolvedKoxoStorageTarget SharedTarget()
        => BillingV2ResolvedKoxoStorageTarget.ForSecondaryGroup(
            "item-b",
            65536,
            "CLIENTS",
            "CLI-000042");

    private static PortalUserAdLinkRecord Link(
        string objectGuid,
        string samAccountName)
        => new(
            Id: "link-1",
            CustomerId,
            CustomerReference: "CLI-000042",
            PortalUserId,
            objectGuid,
            ObjectSid: "S-1-5-21-1-2-3-1104",
            samAccountName,
            UserPrincipalName: null,
            DisplayName: "LAUMAILLE Zachary",
            DistinguishedName:
                "CN=Zachary,OU=CLI-000042,OU=KoXoAdm,DC=clients,DC=home,DC=bzh",
            AdDomain: "clients.home.bzh",
            AdProvisioningStatus: "provisioned",
            AdProvisionedAtUtc: null,
            LastPasswordSyncAtUtc: null,
            LastPasswordSyncStatus: null,
            KoxoExportStatus: "exported");

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly StubHandler _handler;

        public StubHttpClientFactory(StubHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
            => new(_handler, disposeHandler: false);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(
            Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
