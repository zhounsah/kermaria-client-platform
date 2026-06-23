using System.Diagnostics;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

const string correlationHeader = "X-Correlation-Id";
const string dataSourceHeader = "X-Data-Source";
const string sessionHeader = "X-Portal-Session";
const string serviceAuthHeader = "X-Service-Auth";
const string testCorrelationId = "v0.8-smoke-test";
const string mockEmail = "portal.test@example.invalid";
const string mockPassword = "NOT_A_REAL_PASSWORD_V07";
const string mockAdminEmail = "admin.test@example.invalid";
const string mockAdminPassword = "NOT_A_REAL_ADMIN_PASSWORD_V08";

var dotnetExecutable = "dotnet";
var apiAssembly = string.Empty;
RuntimeConfigurationContracts? runtimeConfiguration = null;

return await RunAsync(args);

async Task<int> RunAsync(string[] arguments)
{
    if (arguments.Length is < 1 or > 2)
    {
        Console.Error.WriteLine(
            "Usage: smoke-tests [dotnet-executable] <api-internal-dll>");
        return 2;
    }

    dotnetExecutable = arguments.Length == 2
        ? Path.GetFullPath(arguments[0])
        : "dotnet";
    var sourceApiAssembly = Path.GetFullPath(arguments[^1]);

    if ((arguments.Length == 2 && !File.Exists(dotnetExecutable))
        || !File.Exists(sourceApiAssembly))
    {
        Console.Error.WriteLine(
            "Le runtime .NET ou l'assembly API est introuvable.");
        return 2;
    }

    using var apiRuntime = SmokeTestRuntimeHelpers.CreateIsolatedApiRuntime(
        sourceApiAssembly);
    apiAssembly = apiRuntime.AssemblyPath;
    runtimeConfiguration =
        SmokeTestRuntimeHelpers.LoadRuntimeConfigurationContracts(apiAssembly);

    try
    {
        VerifyIdentifierMapping();
        VerifyActiveDirectoryPathScope();
        VerifyChildProcessEnvironmentGuardrails();
        await RunMockTestsAsync();
        await RunMockActiveDirectoryModeTestsAsync();
        await RunMockBpceIssuingTestsAsync();
        await RunReadOnlyActiveDirectoryModeTestsAsync();
        await RunUnavailableReadinessTestAsync();
        await RunProductionConfigurationValidationTestsAsync();
        await RunServiceAuthenticationGuardTestsAsync();
        await RunDisabledAccountTestAsync();
        await RunExpiredSessionTestAsync();
        await RunLockoutResetTestAsync();

        if (IsMariaDbTestRequested())
        {
            await RunMariaDbReadTestsAsync();
        }

        Console.WriteLine("Smoke tests API-INTERNAL V0.20 reussis.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine("Smoke tests API-INTERNAL V0.20 en echec.");
        Console.Error.WriteLine(exception.ToString());
        return 1;
    }
}

void VerifyActiveDirectoryPathScope()
{
    var scopeType = Assembly.LoadFrom(apiAssembly).GetType(
        "Kermaria.ApiInternal.Services.ActiveDirectory.ActiveDirectoryPathScope")
        ?? throw new InvalidOperationException(
            "Le type ActiveDirectoryPathScope est introuvable.");
    var scope = Activator.CreateInstance(
            scopeType,
            "OU=TEST_SITE_WEB,DC=home,DC=bzh")
        ?? throw new InvalidOperationException(
            "Le scope Active Directory ne peut pas etre instancie.");
    var extractCustomerReference = scopeType.GetMethod(
            "ExtractCustomerReference")
        ?? throw new InvalidOperationException(
            "La methode ExtractCustomerReference est introuvable.");

    var userCustomerReference = extractCustomerReference.Invoke(
        scope,
        ["CN=test2,OU=Users,OU=CLI-DEMO-0060,OU=10_Customers,OU=TEST_SITE_WEB,DC=home,DC=bzh"]) as string;
    var groupCustomerReference = extractCustomerReference.Invoke(
        scope,
        ["CN=testgroupe1,OU=Groups,OU=CLI-DEMO-0060,OU=10_Customers,OU=TEST_SITE_WEB,DC=home,DC=bzh"]) as string;
    var disabledCustomerReference = extractCustomerReference.Invoke(
        scope,
        ["CN=test3,OU=Disabled,OU=CLI-DEMO-0060,OU=10_Customers,OU=TEST_SITE_WEB,DC=home,DC=bzh"]) as string;

    Ensure(
        string.Equals(
            userCustomerReference,
            "CLI-DEMO-0060",
            StringComparison.Ordinal)
        && string.Equals(
            groupCustomerReference,
            "CLI-DEMO-0060",
            StringComparison.Ordinal)
        && string.Equals(
            disabledCustomerReference,
            "CLI-DEMO-0060",
            StringComparison.Ordinal),
        "Le scope AD doit extraire la reference client reelle plutot que l'OU 10_Customers.");
}

async Task RunMockTestsAsync()
{
    var mockBaseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    using var api = StartApi(
        mockBaseUrl,
        startInfo =>
        {
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
            startInfo.Environment["AD_INTEGRATION_MODE"] = "disabled";
            startInfo.Environment["DEMO_PORTAL_EMAIL"] = mockEmail;
            startInfo.Environment["DEMO_PORTAL_PASSWORD"] = mockPassword;
            startInfo.Environment["DEMO_PORTAL_STATUS"] = "active";
            startInfo.Environment["DEMO_INTERNAL_ADMIN_EMAIL"] =
                mockAdminEmail;
            startInfo.Environment["DEMO_INTERNAL_ADMIN_PASSWORD"] =
                mockAdminPassword;
            startInfo.Environment["SESSION_DURATION_MINUTES"] = "60";
            startInfo.Environment["LOGIN_MAX_FAILURES"] = "5";
            startInfo.Environment["LOGIN_LOCKOUT_MINUTES"] = "10";
            foreach (var variable in new[]
            {
                "SQL_PROVIDER",
                "SQL_HOST",
                "SQL_PORT",
                "SQL_DATABASE",
                "SQL_USERNAME",
                "SQL_PASSWORD"
            })
            {
                startInfo.Environment.Remove(variable);
            }
        });
    using var handler = new HttpClientHandler { UseProxy = false };
    using var client = new HttpClient(handler);

    try
    {
        using var healthResponse = await WaitForHealthAsync(
            client,
            api.Process,
            mockBaseUrl,
            api.Logs);

        Ensure(
            healthResponse.IsSuccessStatusCode,
            "Le health check de l'API n'a pas répondu avec succès.");
        Ensure(
            healthResponse.Headers.Contains(correlationHeader),
            "Le health check ne génère pas de X-Correlation-Id.");

        using var liveResponse = await client.GetAsync(
            $"{mockBaseUrl}/health/live");
        using var livePayload = JsonDocument.Parse(
            await liveResponse.Content.ReadAsStringAsync());
        Ensure(
            liveResponse.StatusCode == HttpStatusCode.OK
            && livePayload.RootElement.GetProperty("status").GetString()
                == "healthy"
            && livePayload.RootElement.GetProperty("check").GetString()
                == "live",
            "Le health check live mock est invalide.");

        using var readyResponse = await client.GetAsync(
            $"{mockBaseUrl}/health/ready");
        var readyBody = await readyResponse.Content.ReadAsStringAsync();
        using var readyPayload = JsonDocument.Parse(readyBody);
        Ensure(
            readyResponse.StatusCode == HttpStatusCode.OK
            && readyPayload.RootElement.GetProperty("checks")
                .GetProperty("mariadb").GetString() == "not_configured"
            && readyPayload.RootElement.GetProperty("checks")
                .GetProperty("ad").GetString() == "disabled",
            "Le health check ready mock est invalide.");
        Ensure(
            !readyBody.Contains(mockPassword, StringComparison.Ordinal)
            && !readyBody.Contains(
                mockAdminPassword,
                StringComparison.Ordinal),
            "Le health check ready ne doit contenir aucun secret.");

        using var readyAliasResponse = await client.GetAsync(
            $"{mockBaseUrl}/ready");
        using var readyAliasPayload = JsonDocument.Parse(
            await readyAliasResponse.Content.ReadAsStringAsync());
        Ensure(
            readyAliasResponse.StatusCode == HttpStatusCode.OK
            && readyAliasPayload.RootElement.GetProperty("check").GetString()
                == "ready",
            "L'alias /ready mock est invalide.");

        using var unauthenticatedResponse = await client.GetAsync(
            $"{mockBaseUrl}/internal/portal/services");
        Ensure(
            unauthenticatedResponse.StatusCode == HttpStatusCode.Unauthorized,
            "Une lecture portail sans session devait être refusée.");

        using var unauthenticatedAdminResponse = await client.GetAsync(
            $"{mockBaseUrl}/internal/admin/overview");
        Ensure(
            unauthenticatedAdminResponse.StatusCode
                == HttpStatusCode.Unauthorized,
            "Une lecture admin sans session devait être refusée.");

        using var invalidSessionRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/auth/session",
            "invalid-session-token");
        using var invalidSessionResponse = await client.SendAsync(
            invalidSessionRequest);
        Ensure(
            invalidSessionResponse.StatusCode == HttpStatusCode.Unauthorized,
            "Une session inconnue devait être refusée.");

        const string invalidLoginPassword =
            "NOT_A_REAL_INVALID_PASSWORD_SENTINEL";
        using var invalidLoginRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/auth/sessions")
        {
            Content = JsonContent.Create(new
            {
                email = mockEmail,
                password = invalidLoginPassword
            })
        };
        using var invalidLoginResponse = await client.SendAsync(
            invalidLoginRequest);
        using var invalidLoginPayload = JsonDocument.Parse(
            await invalidLoginResponse.Content.ReadAsStringAsync());
        Ensure(
            invalidLoginResponse.StatusCode == HttpStatusCode.Unauthorized,
            "Un login invalide devait retourner HTTP 401.");
        Ensure(
            invalidLoginPayload.RootElement.GetProperty("code").GetString()
                == "INVALID_CREDENTIALS",
            "Le login invalide ne retourne pas un message générique.");

        using var loginRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/auth/sessions")
        {
            Content = JsonContent.Create(new
            {
                email = mockEmail,
                password = mockPassword
            })
        };
        loginRequest.Headers.Add(correlationHeader, testCorrelationId);
        using var loginResponse = await client.SendAsync(loginRequest);
        using var loginPayload = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());
        Ensure(
            loginResponse.StatusCode == HttpStatusCode.OK,
            "Le login mock valide devait retourner HTTP 200.");
        var sessionToken = loginPayload.RootElement
            .GetProperty("sessionToken")
            .GetString();
        Ensure(
            !string.IsNullOrWhiteSpace(sessionToken),
            "Le login mock ne retourne pas de token interne.");

        using var sessionRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/auth/session",
            sessionToken!);
        using var sessionResponse = await client.SendAsync(sessionRequest);
        using var sessionPayload = JsonDocument.Parse(
            await sessionResponse.Content.ReadAsStringAsync());
        Ensure(
            sessionResponse.StatusCode == HttpStatusCode.OK,
            "La session créée ne peut pas être relue.");
        Ensure(
            sessionPayload.RootElement
                .GetProperty("user")
                .GetProperty("customerReference")
                .GetString() == MockCustomerReference(),
            "La session ne contient pas la référence client attendue.");
        Ensure(
            sessionPayload.RootElement
                .GetProperty("user")
                .GetProperty("role")
                .GetString() == "client_user",
            "La session client ne contient pas le rôle attendu.");

        using var clientAdminRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/overview",
            sessionToken!);
        using var clientAdminResponse = await client.SendAsync(
            clientAdminRequest);
        Ensure(
            clientAdminResponse.StatusCode == HttpStatusCode.Forbidden,
            "Un client_user ne doit pas accéder aux routes admin.");

        using var adminLoginResponse = await client.PostAsJsonAsync(
            $"{mockBaseUrl}/internal/auth/sessions",
            new
            {
                email = mockAdminEmail,
                password = mockAdminPassword
            });
        using var adminLoginPayload = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());
        Ensure(
            adminLoginResponse.StatusCode == HttpStatusCode.OK,
            "Le login internal_admin mock devait réussir.");
        Ensure(
            adminLoginPayload.RootElement
                .GetProperty("user")
                .GetProperty("role")
                .GetString() == "internal_admin",
            "Le compte admin ne retourne pas le rôle attendu.");
        Ensure(
            adminLoginPayload.RootElement
                .GetProperty("user")
                .GetProperty("customerReference")
                .ValueKind == JsonValueKind.Null,
            "Une session admin ne doit pas exposer de référence client.");
        var adminSessionToken = adminLoginPayload.RootElement
            .GetProperty("sessionToken")
            .GetString()
            ?? throw new InvalidOperationException(
                "Le login admin ne retourne aucun token interne.");

        foreach (var endpoint in new[]
        {
            "/internal/admin/overview",
            "/internal/admin/activity",
            "/internal/admin/customers",
            $"/internal/admin/customers/{MockCustomerReference()}",
            "/internal/admin/support-requests",
            "/internal/admin/service-requests",
            "/internal/admin/sessions",
            "/internal/admin/audit-logs"
        })
        {
            using var adminRequest = CreateSessionRequest(
                HttpMethod.Get,
                $"{mockBaseUrl}{endpoint}",
                adminSessionToken);
            using var adminResponse = await client.SendAsync(adminRequest);
            var adminResponseText =
                await adminResponse.Content.ReadAsStringAsync();
            Ensure(
                adminResponse.StatusCode == HttpStatusCode.OK,
                $"La route admin {endpoint} devait répondre HTTP 200.");
            Ensure(
                !adminResponseText.Contains(
                    "sessionToken",
                    StringComparison.OrdinalIgnoreCase)
                && !adminResponseText.Contains(
                    "passwordHash",
                    StringComparison.OrdinalIgnoreCase),
                $"La route admin {endpoint} expose une donnée d'authentification.");
        }

        using var adminCustomerDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/customers/{MockCustomerReference()}",
            adminSessionToken);
        using var adminCustomerDetailResponse = await client.SendAsync(
            adminCustomerDetailRequest);
        var adminCustomerDetailText =
            await adminCustomerDetailResponse.Content.ReadAsStringAsync();
        Ensure(
            adminCustomerDetailResponse.StatusCode == HttpStatusCode.OK,
            "La fiche client admin mock devait répondre HTTP 200.");
        Ensure(
            adminCustomerDetailText.Contains(
                "commercialDocuments",
                StringComparison.Ordinal)
            && adminCustomerDetailText.Contains(
                "recentAuditLogs",
                StringComparison.Ordinal),
            "La fiche client admin mock ne contient pas les sections attendues.");

        using var adminSupportListRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/support-requests",
            adminSessionToken);
        using var adminSupportListResponse = await client.SendAsync(
            adminSupportListRequest);
        using var adminSupportListPayload = JsonDocument.Parse(
            await adminSupportListResponse.Content.ReadAsStringAsync());
        var workflowSupportId = adminSupportListPayload.RootElement[0]
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException(
                "Le workflow support mock ne retourne aucun identifiant.");

        using var supportStatusRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{mockBaseUrl}/internal/admin/support-requests/{workflowSupportId}/status",
            adminSessionToken);
        supportStatusRequest.Content = JsonContent.Create(
            new { status = "waiting_for_customer" });
        using var supportStatusResponse = await client.SendAsync(
            supportStatusRequest);
        using var supportStatusPayload = JsonDocument.Parse(
            await supportStatusResponse.Content.ReadAsStringAsync());
        Ensure(
            supportStatusResponse.StatusCode == HttpStatusCode.OK
            && supportStatusPayload.RootElement
                .GetProperty("changed")
                .GetBoolean(),
            "Un admin doit pouvoir changer le statut support.");

        using var supportNoOpRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{mockBaseUrl}/internal/admin/support-requests/{workflowSupportId}/status",
            adminSessionToken);
        supportNoOpRequest.Content = JsonContent.Create(
            new { status = "waiting_for_customer" });
        using var supportNoOpResponse = await client.SendAsync(
            supportNoOpRequest);
        using var supportNoOpPayload = JsonDocument.Parse(
            await supportNoOpResponse.Content.ReadAsStringAsync());
        Ensure(
            !supportNoOpPayload.RootElement.GetProperty("changed").GetBoolean(),
            "Un statut identique doit être traité comme un no-op.");

        using var invalidStatusRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{mockBaseUrl}/internal/admin/support-requests/{workflowSupportId}/status",
            adminSessionToken);
        invalidStatusRequest.Content = JsonContent.Create(
            new { status = "provisioned" });
        using var invalidStatusResponse = await client.SendAsync(
            invalidStatusRequest);
        Ensure(
            invalidStatusResponse.StatusCode == HttpStatusCode.BadRequest,
            "Un statut support invalide devait être refusé.");

        using var clientStatusRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{mockBaseUrl}/internal/admin/support-requests/{workflowSupportId}/status",
            sessionToken!);
        clientStatusRequest.Content = JsonContent.Create(
            new { status = "resolved" });
        using var clientStatusResponse = await client.SendAsync(
            clientStatusRequest);
        Ensure(
            clientStatusResponse.StatusCode == HttpStatusCode.Forbidden,
            "Un client ne doit pas modifier le statut d'une demande.");

        foreach (var invalidText in new[] { "", new string('x', 2001) })
        {
            using var invalidNoteRequest = CreateSessionRequest(
                HttpMethod.Post,
                $"{mockBaseUrl}/internal/admin/support-requests/{workflowSupportId}/notes",
                adminSessionToken);
            invalidNoteRequest.Content = JsonContent.Create(
                new { text = invalidText });
            using var invalidNoteResponse = await client.SendAsync(
                invalidNoteRequest);
            Ensure(
                invalidNoteResponse.StatusCode == HttpStatusCode.BadRequest,
                "Une note interne vide ou trop longue devait être refusée.");
        }

        const string privateNote = "Note opérationnelle interne V0.11.";
        using var noteRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/admin/support-requests/{workflowSupportId}/notes",
            adminSessionToken);
        noteRequest.Content = JsonContent.Create(new { text = privateNote });
        using var noteResponse = await client.SendAsync(noteRequest);
        Ensure(
            noteResponse.StatusCode == HttpStatusCode.OK,
            "L'ajout d'une note interne mock devait réussir.");

        const string publicMessage =
            "Un retour complémentaire est attendu pour poursuivre.";
        using var messageRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/admin/support-requests/{workflowSupportId}/messages",
            adminSessionToken);
        messageRequest.Content = JsonContent.Create(
            new { text = publicMessage });
        using var messageResponse = await client.SendAsync(messageRequest);
        Ensure(
            messageResponse.StatusCode == HttpStatusCode.OK,
            "L'ajout d'un message public mock devait réussir.");

        foreach (var invalidReply in new[]
        {
            "",
            "ab",
            new string('x', 2001)
        })
        {
            using var invalidReplyRequest = CreateSessionRequest(
                HttpMethod.Post,
                $"{mockBaseUrl}/internal/portal/support-requests/{workflowSupportId}/messages",
                sessionToken!);
            invalidReplyRequest.Content = JsonContent.Create(
                new { text = invalidReply });
            using var invalidReplyResponse = await client.SendAsync(
                invalidReplyRequest);
            Ensure(
                invalidReplyResponse.StatusCode == HttpStatusCode.BadRequest,
                "Une réponse client invalide devait être refusée.");
        }

        const string clientSupportReply =
            "Voici le complément demandé pour cette intervention.";
        using var clientSupportReplyRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/support-requests/{workflowSupportId}/messages",
            sessionToken!);
        clientSupportReplyRequest.Content = JsonContent.Create(
            new { text = clientSupportReply });
        using var clientSupportReplyResponse = await client.SendAsync(
            clientSupportReplyRequest);
        Ensure(
            clientSupportReplyResponse.StatusCode == HttpStatusCode.OK,
            "Le client devait pouvoir répondre à sa demande support.");

        using var foreignSupportReplyRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/support-requests/support-other-customer/messages",
            sessionToken!);
        foreignSupportReplyRequest.Content = JsonContent.Create(
            new { text = "Réponse interdite sur une autre demande." });
        using var foreignSupportReplyResponse = await client.SendAsync(
            foreignSupportReplyRequest);
        Ensure(
            foreignSupportReplyResponse.StatusCode == HttpStatusCode.NotFound,
            "Une demande support étrangère devait rester inaccessible.");

        using var adminPortalReplyRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/support-requests/{workflowSupportId}/messages",
            adminSessionToken);
        adminPortalReplyRequest.Content = JsonContent.Create(
            new { text = "Un admin ne doit pas utiliser la route client." });
        using var adminPortalReplyResponse = await client.SendAsync(
            adminPortalReplyRequest);
        Ensure(
            adminPortalReplyResponse.StatusCode == HttpStatusCode.Forbidden,
            "Un admin ne doit pas utiliser une route de réponse client.");

        using var adminSupportDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/support-requests/{workflowSupportId}",
            adminSessionToken);
        using var adminSupportDetailResponse = await client.SendAsync(
            adminSupportDetailRequest);
        var adminSupportDetailText =
            await adminSupportDetailResponse.Content.ReadAsStringAsync();
        Ensure(
            adminSupportDetailText.Contains(privateNote, StringComparison.Ordinal)
            && adminSupportDetailText.Contains(
                publicMessage,
                StringComparison.Ordinal)
            && adminSupportDetailText.Contains(
                clientSupportReply,
                StringComparison.Ordinal)
            && adminSupportDetailText.Contains(
                "\"authorType\":\"client\"",
                StringComparison.Ordinal),
            "Le détail admin doit distinguer notes internes et messages publics.");

        using var clientSupportDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/portal/support-requests/{workflowSupportId}",
            sessionToken!);
        using var clientSupportDetailResponse = await client.SendAsync(
            clientSupportDetailRequest);
        var clientSupportDetailText =
            await clientSupportDetailResponse.Content.ReadAsStringAsync();
        Ensure(
            clientSupportDetailResponse.StatusCode == HttpStatusCode.OK
            && clientSupportDetailText.Contains(
                publicMessage,
                StringComparison.Ordinal)
            && clientSupportDetailText.Contains(
                clientSupportReply,
                StringComparison.Ordinal)
            && clientSupportDetailText.Contains(
                "\"authorLabel\":\"Vous\"",
                StringComparison.Ordinal)
            && !clientSupportDetailText.Contains(
                privateNote,
                StringComparison.Ordinal)
            && !clientSupportDetailText.Contains(
                "internalNotes",
                StringComparison.OrdinalIgnoreCase),
            "Une note interne ne doit jamais être exposée au client.");

        using var adminServiceListRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/service-requests",
            adminSessionToken);
        using var adminServiceListResponse = await client.SendAsync(
            adminServiceListRequest);
        using var adminServiceListPayload = JsonDocument.Parse(
            await adminServiceListResponse.Content.ReadAsStringAsync());
        var workflowServiceId = adminServiceListPayload.RootElement[0]
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException(
                "Le workflow service mock ne retourne aucun identifiant.");
        using var serviceStatusRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{mockBaseUrl}/internal/admin/service-requests/{workflowServiceId}/status",
            adminSessionToken);
        serviceStatusRequest.Content = JsonContent.Create(
            new { status = "under_review" });
        using var serviceStatusResponse = await client.SendAsync(
            serviceStatusRequest);
        Ensure(
            serviceStatusResponse.StatusCode == HttpStatusCode.OK,
            "Un admin doit pouvoir changer le statut d'une demande de service.");

        using var serviceMessageRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/admin/service-requests/{workflowServiceId}/messages",
            adminSessionToken);
        serviceMessageRequest.Content = JsonContent.Create(
            new { text = "Message public de suivi de service." });
        using var serviceMessageResponse = await client.SendAsync(
            serviceMessageRequest);
        Ensure(
            serviceMessageResponse.StatusCode == HttpStatusCode.OK,
            "L'ajout d'un message public de service mock devait réussir.");

        const string clientServiceReply =
            "Je confirme le périmètre de la demande de service.";
        using var clientServiceReplyRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/service-requests/{workflowServiceId}/messages",
            sessionToken!);
        clientServiceReplyRequest.Content = JsonContent.Create(
            new { text = clientServiceReply });
        using var clientServiceReplyResponse = await client.SendAsync(
            clientServiceReplyRequest);
        Ensure(
            clientServiceReplyResponse.StatusCode == HttpStatusCode.OK,
            "Le client devait pouvoir répondre à sa demande de service.");

        await VerifyCommercialFoundationAsync(
            client,
            mockBaseUrl,
            sessionToken!,
            adminSessionToken,
            MockCustomerReference(),
            workflowServiceId,
            persistent: false);

        using var adminActivityRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/activity",
            adminSessionToken);
        using var adminActivityResponse = await client.SendAsync(
            adminActivityRequest);
        var adminActivityText =
            await adminActivityResponse.Content.ReadAsStringAsync();
        using var adminActivityPayload = JsonDocument.Parse(adminActivityText);
        var recentActivities = adminActivityPayload.RootElement
            .GetProperty("recentActivities")
            .EnumerateArray()
            .ToArray();
        Ensure(
            adminActivityResponse.StatusCode == HttpStatusCode.OK
            && adminActivityPayload.RootElement
                .GetProperty("recentClientReplyCount")
                .GetInt32() >= 2
            && recentActivities.Any(item =>
                item.GetProperty("requestId").GetString() == workflowSupportId
                && item.GetProperty("authorType").GetString() == "client")
            && recentActivities.Any(item =>
                item.GetProperty("requestId").GetString() == workflowServiceId
                && item.GetProperty("authorType").GetString() == "client"),
            "Le centre d'activité mock doit identifier les réponses client.");
        Ensure(
            !adminActivityText.Contains(privateNote, StringComparison.Ordinal)
            && !adminActivityText.Contains(
                clientSupportReply,
                StringComparison.Ordinal)
            && !adminActivityText.Contains(
                clientServiceReply,
                StringComparison.Ordinal),
            "Le centre d'activité ne doit exposer aucun contenu de message.");

        using var clientActivityRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/activity",
            sessionToken!);
        using var clientActivityResponse = await client.SendAsync(
            clientActivityRequest);
        Ensure(
            clientActivityResponse.StatusCode == HttpStatusCode.Forbidden,
            "Un client ne doit pas accéder au centre d'activité admin.");

        foreach (var (resource, expectedId) in new[]
        {
            ("support-requests", workflowSupportId),
            ("service-requests", workflowServiceId)
        })
        {
            using var filteredRequest = CreateSessionRequest(
                HttpMethod.Get,
                $"{mockBaseUrl}/internal/admin/{resource}?attention=client_reply",
                adminSessionToken);
            using var filteredResponse = await client.SendAsync(
                filteredRequest);
            var filteredText =
                await filteredResponse.Content.ReadAsStringAsync();
            Ensure(
                filteredResponse.StatusCode == HttpStatusCode.OK
                && filteredText.Contains(expectedId, StringComparison.Ordinal)
                && filteredText.Contains(
                    "\"hasRecentClientReply\":true",
                    StringComparison.Ordinal)
                && filteredText.Contains(
                    "\"requiresAttention\":true",
                    StringComparison.Ordinal),
                $"Le filtre réponse client {resource} est invalide.");
        }

        using var invalidAttentionRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/support-requests?attention=automatic",
            adminSessionToken);
        using var invalidAttentionResponse = await client.SendAsync(
            invalidAttentionRequest);
        Ensure(
            invalidAttentionResponse.StatusCode == HttpStatusCode.BadRequest,
            "Un filtre d'attention inconnu devait être refusé.");

        using var adminServiceDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/service-requests/{workflowServiceId}",
            adminSessionToken);
        using var adminServiceDetailResponse = await client.SendAsync(
            adminServiceDetailRequest);
        var adminServiceDetailText =
            await adminServiceDetailResponse.Content.ReadAsStringAsync();
        Ensure(
            adminServiceDetailResponse.StatusCode == HttpStatusCode.OK
            && adminServiceDetailText.Contains(
                clientServiceReply,
                StringComparison.Ordinal)
            && adminServiceDetailText.Contains(
                "\"authorType\":\"client\"",
                StringComparison.Ordinal),
            "La réponse client service devait être visible par l'admin.");

        using var clientServiceDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/portal/service-requests/{workflowServiceId}",
            sessionToken!);
        using var clientServiceDetailResponse = await client.SendAsync(
            clientServiceDetailRequest);
        var clientServiceDetailText =
            await clientServiceDetailResponse.Content.ReadAsStringAsync();
        Ensure(
            clientServiceDetailResponse.StatusCode == HttpStatusCode.OK
            && clientServiceDetailText.Contains(
                clientServiceReply,
                StringComparison.Ordinal)
            && clientServiceDetailText.Contains(
                "\"authorLabel\":\"Vous\"",
                StringComparison.Ordinal)
            && !clientServiceDetailText.Contains(
                "internalNotes",
                StringComparison.OrdinalIgnoreCase),
            "La conversation service client devait rester publique uniquement.");

        using var notificationsRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/portal/notifications",
            sessionToken!);
        using var notificationsResponse = await client.SendAsync(
            notificationsRequest);
        using var notificationsPayload = JsonDocument.Parse(
            await notificationsResponse.Content.ReadAsStringAsync());
        Ensure(
            notificationsResponse.StatusCode == HttpStatusCode.OK
            && notificationsPayload.RootElement.GetArrayLength() == 4,
            "Les quatre événements visibles devaient créer quatre notifications.");
        var notificationsText = notificationsPayload.RootElement.GetRawText();
        Ensure(
            !notificationsText.Contains(
                privateNote,
                StringComparison.Ordinal)
            && !notificationsText.Contains(
                publicMessage,
                StringComparison.Ordinal),
            "Une notification ne doit contenir ni note interne ni message complet.");

        var notificationId = notificationsPayload.RootElement[0]
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException(
                "La notification mock ne retourne aucun identifiant.");
        using var readNotificationRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/notifications/{notificationId}/read",
            sessionToken!);
        using var readNotificationResponse = await client.SendAsync(
            readNotificationRequest);
        using var readNotificationPayload = JsonDocument.Parse(
            await readNotificationResponse.Content.ReadAsStringAsync());
        Ensure(
            readNotificationResponse.StatusCode == HttpStatusCode.OK
            && readNotificationPayload.RootElement
                .GetProperty("updatedCount")
                .GetInt32() == 1,
            "Le marquage individuel d'une notification devait réussir.");

        using var foreignNotificationRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/notifications/notification-other-customer/read",
            sessionToken!);
        using var foreignNotificationResponse = await client.SendAsync(
            foreignNotificationRequest);
        Ensure(
            foreignNotificationResponse.StatusCode == HttpStatusCode.NotFound,
            "Une notification absente du client devait rester inaccessible.");

        using var readAllNotificationsRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/notifications/read-all",
            sessionToken!);
        using var readAllNotificationsResponse = await client.SendAsync(
            readAllNotificationsRequest);
        Ensure(
            readAllNotificationsResponse.StatusCode == HttpStatusCode.OK,
            "Le marquage global des notifications devait réussir.");

        using var notificationsAfterReadRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/portal/notifications",
            sessionToken!);
        using var notificationsAfterReadResponse = await client.SendAsync(
            notificationsAfterReadRequest);
        using var notificationsAfterReadPayload = JsonDocument.Parse(
            await notificationsAfterReadResponse.Content.ReadAsStringAsync());
        Ensure(
            notificationsAfterReadPayload.RootElement
                .EnumerateArray()
                .All(item => item.GetProperty("isRead").GetBoolean()),
            "Toutes les notifications mock devaient être marquées comme lues.");

        using var adminPortalRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/portal/services",
            adminSessionToken);
        using var adminPortalResponse = await client.SendAsync(
            adminPortalRequest);
        Ensure(
            adminPortalResponse.StatusCode == HttpStatusCode.Forbidden,
            "Un internal_admin ne doit pas utiliser les vues client.");

        using var servicesRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/portal/services");
        servicesRequest.Headers.Add(correlationHeader, testCorrelationId);
        servicesRequest.Headers.Add(sessionHeader, sessionToken);
        using var servicesResponse = await client.SendAsync(servicesRequest);
        using var servicesPayload = JsonDocument.Parse(
            await servicesResponse.Content.ReadAsStringAsync());

        Ensure(
            servicesResponse.StatusCode == HttpStatusCode.OK,
            "La liste mock des services ne répond pas avec HTTP 200.");
        Ensure(
            servicesResponse.Headers.GetValues(correlationHeader).Single()
                == testCorrelationId,
            "La liste mock des services ne propage pas X-Correlation-Id.");
        Ensure(
            servicesResponse.Headers.GetValues(dataSourceHeader).Single()
                == "mock",
            "Le fallback de développement n'est pas signalé comme mock.");
        Ensure(
            servicesPayload.RootElement.GetArrayLength() == 5,
            "La liste mock des services ne contient pas les cinq services attendus.");
        Ensure(
            servicesPayload.RootElement[0].GetProperty("name").GetString()
                == "Hébergement dossier personnel",
            "Le catalogue client mock n'est pas aligné avec l'activité attendue.");

        using var summaryRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/portal/summary",
            sessionToken!);
        using var summaryResponse = await client.SendAsync(summaryRequest);
        using var summaryPayload = JsonDocument.Parse(
            await summaryResponse.Content.ReadAsStringAsync());
        Ensure(
            summaryResponse.StatusCode == HttpStatusCode.OK,
            "Le résumé portail mock ne répond pas avec HTTP 200.");
        Ensure(
            summaryPayload.RootElement
                .GetProperty("activeServiceCount")
                .GetInt32() == 3,
            "Le résumé portail mock ne contient pas le bon nombre de services actifs.");

        using var catalogRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/portal/service-catalog",
            sessionToken!);
        using var catalogResponse = await client.SendAsync(catalogRequest);
        using var catalogPayload = JsonDocument.Parse(
            await catalogResponse.Content.ReadAsStringAsync());
        Ensure(
            catalogResponse.StatusCode == HttpStatusCode.OK,
            "Le catalogue mock ne répond pas avec HTTP 200.");
        Ensure(
            catalogResponse.Headers.GetValues(dataSourceHeader).Single()
                == "mock",
            "Le catalogue mock n'indique pas sa source.");
        Ensure(
            catalogPayload.RootElement.GetArrayLength() == 8,
            "Le catalogue mock ne contient pas les huit services attendus.");

        using var supportRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/support-requests")
        {
            Content = JsonContent.Create(new
            {
                serviceId = "svc-backup-001",
                priority = "normal",
                subject = "Vérification mock",
                description = "Demande de test sans donnée sensible."
            })
        };
        supportRequest.Headers.Add(correlationHeader, testCorrelationId);
        supportRequest.Headers.Add(sessionHeader, sessionToken);
        using var supportResponse = await client.SendAsync(supportRequest);
        using var supportPayload = JsonDocument.Parse(
            await supportResponse.Content.ReadAsStringAsync());

        Ensure(
            supportResponse.StatusCode == HttpStatusCode.Accepted,
            "La création mock d'une demande support devait retourner HTTP 202.");
        Ensure(
            supportPayload.RootElement.GetProperty("status").GetString()
                == "mock_received",
            "La demande support mock ne renvoie pas le statut attendu.");
        Ensure(
            !supportPayload.RootElement.GetProperty("persisted").GetBoolean(),
            "La demande support mock ne doit pas être persistée.");
        Ensure(
            supportPayload.RootElement.GetProperty("correlation_id").GetString()
                == testCorrelationId,
            "La demande support mock ne propage pas le correlation_id.");

        using var serviceRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/service-requests")
        {
            Content = JsonContent.Create(new
            {
                catalogItemId = "catalog-vpn",
                subject = "Demande de service",
                description = "Qualification mock sans donnée sensible."
            })
        };
        serviceRequest.Headers.Add(correlationHeader, testCorrelationId);
        serviceRequest.Headers.Add(sessionHeader, sessionToken);
        using var serviceResponse = await client.SendAsync(serviceRequest);
        using var servicePayload = JsonDocument.Parse(
            await serviceResponse.Content.ReadAsStringAsync());

        Ensure(
            serviceResponse.StatusCode == HttpStatusCode.Accepted,
            "La création mock d'une demande de service devait retourner HTTP 202.");
        Ensure(
            !servicePayload.RootElement.GetProperty("persisted").GetBoolean(),
            "La demande de service mock ne doit pas être persistée.");
        Ensure(
            servicePayload.RootElement.GetProperty("correlation_id").GetString()
                == testCorrelationId,
            "La demande de service mock ne propage pas le correlation_id.");

        using var invalidServiceRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/service-requests")
        {
            Content = JsonContent.Create(new
            {
                catalogItemId = "catalog-vpn",
                subject = "Description absente"
            })
        };
        invalidServiceRequest.Headers.Add(sessionHeader, sessionToken);
        using var invalidServiceResponse = await client.SendAsync(
            invalidServiceRequest);
        using var invalidServicePayload = JsonDocument.Parse(
            await invalidServiceResponse.Content.ReadAsStringAsync());
        Ensure(
            invalidServiceResponse.StatusCode == HttpStatusCode.BadRequest,
            "Une demande de service incomplète devait retourner HTTP 400.");
        Ensure(
            invalidServicePayload.RootElement.GetProperty("code").GetString()
                == "INVALID_REQUEST",
            "La demande de service incomplète ne retourne pas INVALID_REQUEST.");

        using var inaccessibleServiceRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/support-requests")
        {
            Content = JsonContent.Create(new
            {
                serviceId = "service-owned-by-another-customer",
                priority = "normal",
                subject = "Tentative inter-client",
                description = "Cette demande doit être refusée."
            })
        };
        inaccessibleServiceRequest.Headers.Add(
            sessionHeader,
            sessionToken);
        using var inaccessibleServiceResponse = await client.SendAsync(
            inaccessibleServiceRequest);
        Ensure(
            inaccessibleServiceResponse.StatusCode == HttpStatusCode.Forbidden,
            "Un service hors client devait être refusé avec HTTP 403.");

        using var invalidCatalogRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/service-requests")
        {
            Content = JsonContent.Create(new
            {
                catalogItemId = "catalog-inconnu",
                subject = "Catalogue invalide",
                description = "Cette demande doit être refusée."
            })
        };
        invalidCatalogRequest.Headers.Add(sessionHeader, sessionToken);
        using var invalidCatalogResponse = await client.SendAsync(
            invalidCatalogRequest);
        Ensure(
            invalidCatalogResponse.StatusCode == HttpStatusCode.BadRequest,
            "Un élément de catalogue invalide devait retourner HTTP 400.");

        using var invalidRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/portal/support-requests")
        {
            Content = JsonContent.Create(new { })
        };
        invalidRequest.Headers.Add(correlationHeader, testCorrelationId);
        invalidRequest.Headers.Add(sessionHeader, sessionToken);
        using var invalidResponse = await client.SendAsync(invalidRequest);
        using var invalidPayload = JsonDocument.Parse(
            await invalidResponse.Content.ReadAsStringAsync());

        Ensure(
            invalidResponse.StatusCode == HttpStatusCode.BadRequest,
            "Une demande mock invalide devait retourner HTTP 400.");
        Ensure(
            invalidPayload.RootElement.GetProperty("code").GetString()
                == "INVALID_REQUEST",
            "L'erreur invalide n'est pas structurée avec le code attendu.");
        Ensure(
            invalidPayload.RootElement
                .GetProperty("correlation_id")
                .GetString() == testCorrelationId,
            "L'erreur structurée ne propage pas le correlation_id.");

        await VerifyDisabledActiveDirectoryAdminRoutesAsync(
            client,
            mockBaseUrl,
            adminSessionToken);
        await VerifyDisabledBpceAdminRoutesAsync(
            client,
            mockBaseUrl,
            adminSessionToken);
        /*
        using var adHealthResponse = await client.GetAsync(
            $"{mockBaseUrl}/internal/ad/health");
        var adHealthText = await adHealthResponse.Content.ReadAsStringAsync();
        using var adHealthPayload = JsonDocument.Parse(adHealthText);

        Ensure(
            adStatusResponse.StatusCode == HttpStatusCode.OK,
            "Le diagnostic AD interne ne répond pas avec HTTP 200.");
        Ensure(
            adStatusPayload.RootElement.GetProperty("mode").GetString()
                == "disabled",
            "Le mode AD doit être disabled par défaut dans les tests.");
        Ensure(
            !adHealthText.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !adHealthText.Contains("username", StringComparison.OrdinalIgnoreCase)
            && !adHealthText.Contains("distinguished", StringComparison.OrdinalIgnoreCase),
            "Le diagnostic AD expose une information de configuration interdite.");

        const string passwordLogSentinel =
            "NOT_A_REAL_PASSWORD_LOG_SENTINEL";
        using var adRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/ad/change-password")
        {
            Content = JsonContent.Create(new
            {
                targetDistinguishedName =
                    "CN=demo,OU=TEST_SITE_WEB,DC=home,DC=bzh",
                currentPassword = passwordLogSentinel,
                newPassword = $"{passwordLogSentinel}_NEW"
            })
        };
        adRequest.Headers.Add(correlationHeader, testCorrelationId);
        using var adResponse = await client.SendAsync(adRequest);
        using var adPayload = JsonDocument.Parse(
            await adResponse.Content.ReadAsStringAsync());

        Ensure(
            adResponse.StatusCode == HttpStatusCode.NotImplemented,
            "La route AD disabled devait retourner HTTP 501.");
        Ensure(
            adPayload.RootElement.GetProperty("code").GetString()
                == "AD_INTEGRATION_DISABLED",
            "La route AD disabled ne renvoie pas le code attendu.");
        Ensure(
            adPayload.RootElement.GetProperty("correlation_id").GetString()
                == testCorrelationId,
            "La route AD disabled ne propage pas le correlation_id.");
        */

        using var secondLoginResponse = await client.PostAsJsonAsync(
            $"{mockBaseUrl}/internal/auth/sessions",
            new { email = mockEmail, password = mockPassword });
        using var secondLoginPayload = JsonDocument.Parse(
            await secondLoginResponse.Content.ReadAsStringAsync());
        var secondSessionToken = secondLoginPayload.RootElement
            .GetProperty("sessionToken")
            .GetString()
            ?? throw new InvalidOperationException(
                "La seconde session client est absente.");
        using var revokeOthersRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/auth/sessions/revoke-others",
            sessionToken!);
        using var revokeOthersResponse = await client.SendAsync(
            revokeOthersRequest);
        using var revokeOthersPayload = JsonDocument.Parse(
            await revokeOthersResponse.Content.ReadAsStringAsync());
        Ensure(
            revokeOthersResponse.StatusCode == HttpStatusCode.OK,
            "La révocation des autres sessions devait réussir.");
        Ensure(
            revokeOthersPayload.RootElement
                .GetProperty("revokedCount")
                .GetInt32() >= 1,
            "La seconde session client n'a pas été révoquée.");

        using var revokedOtherRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/auth/session",
            secondSessionToken);
        using var revokedOtherResponse = await client.SendAsync(
            revokedOtherRequest);
        Ensure(
            revokedOtherResponse.StatusCode == HttpStatusCode.Unauthorized,
            "Une autre session révoquée devait être refusée.");

        using var currentSessionRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/auth/session",
            sessionToken!);
        using var currentSessionResponse = await client.SendAsync(
            currentSessionRequest);
        Ensure(
            currentSessionResponse.StatusCode == HttpStatusCode.OK,
            "La session courante ne doit pas être révoquée avec les autres.");

        using var logoutRequest = CreateSessionRequest(
            HttpMethod.Delete,
            $"{mockBaseUrl}/internal/auth/sessions/current",
            sessionToken!);
        using var logoutResponse = await client.SendAsync(logoutRequest);
        Ensure(
            logoutResponse.StatusCode == HttpStatusCode.NoContent,
            "Le logout devait retourner HTTP 204.");

        using var revokedSessionRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/auth/session",
            sessionToken!);
        using var revokedSessionResponse = await client.SendAsync(
            revokedSessionRequest);
        Ensure(
            revokedSessionResponse.StatusCode == HttpStatusCode.Unauthorized,
            "Une session révoquée devait être refusée.");

        await Task.Delay(100);
        Ensure(
            !SnapshotLogs(api.Logs).Contains(
                "NOT_A_REAL_PASSWORD_LOG_SENTINEL",
                StringComparison.Ordinal),
            "Un mot de passe de test a été écrit dans les logs.");
        Ensure(
            !SnapshotLogs(api.Logs).Contains(
                invalidLoginPassword,
                StringComparison.Ordinal)
            && !SnapshotLogs(api.Logs).Contains(
                mockPassword,
                StringComparison.Ordinal)
            && !SnapshotLogs(api.Logs).Contains(
                sessionToken!,
                StringComparison.Ordinal),
            "Un mot de passe ou token de session a été écrit dans les logs.");
        Ensure(
            !SnapshotLogs(api.Logs).Contains(
                mockAdminPassword,
                StringComparison.Ordinal)
            && !SnapshotLogs(api.Logs).Contains(
                adminSessionToken,
                StringComparison.Ordinal),
            "Un mot de passe ou token admin a été écrit dans les logs.");
        Ensure(
            !SnapshotLogs(api.Logs).Contains(privateNote, StringComparison.Ordinal)
            && !SnapshotLogs(api.Logs).Contains(
                publicMessage,
                StringComparison.Ordinal),
            "Le contenu d'une note ou d'un message a été écrit dans les logs.");
    }
    finally
    {
        await api.StopAsync();
    }
}

async Task RunDisabledAccountTestAsync()
{
    var baseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    using var api = StartApi(
        baseUrl,
        startInfo =>
        {
            ConfigureMockAuthentication(startInfo, "disabled", "60");
        });
    using var handler = new HttpClientHandler { UseProxy = false };
    using var client = new HttpClient(handler);

    try
    {
        using var healthResponse = await WaitForHealthAsync(
            client,
            api.Process,
            baseUrl,
            api.Logs);
        Ensure(healthResponse.IsSuccessStatusCode, "Health disabled invalide.");

        using var loginResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/internal/auth/sessions",
            new { email = mockEmail, password = mockPassword });
        using var payload = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());
        Ensure(
            loginResponse.StatusCode == HttpStatusCode.Unauthorized,
            "Un compte désactivé ne doit pas pouvoir se connecter.");
        Ensure(
            payload.RootElement.GetProperty("code").GetString()
                == "INVALID_CREDENTIALS",
            "Un compte désactivé doit recevoir le message générique.");
    }
    finally
    {
        await api.StopAsync();
    }
}

async Task RunExpiredSessionTestAsync()
{
    var baseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    using var api = StartApi(
        baseUrl,
        startInfo =>
        {
            ConfigureMockAuthentication(startInfo, "active", "0");
        });
    using var handler = new HttpClientHandler { UseProxy = false };
    using var client = new HttpClient(handler);

    try
    {
        using var healthResponse = await WaitForHealthAsync(
            client,
            api.Process,
            baseUrl,
            api.Logs);
        Ensure(healthResponse.IsSuccessStatusCode, "Health expiration invalide.");

        using var loginResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/internal/auth/sessions",
            new { email = mockEmail, password = mockPassword });
        using var loginPayload = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());
        Ensure(
            loginResponse.StatusCode == HttpStatusCode.OK,
            "La session d'expiration n'a pas pu être créée.");
        var token = loginPayload.RootElement
            .GetProperty("sessionToken")
            .GetString()
            ?? throw new InvalidOperationException(
                "Token de session d'expiration absent.");

        using var sessionRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{baseUrl}/internal/auth/session",
            token);
        using var sessionResponse = await client.SendAsync(sessionRequest);
        using var sessionPayload = JsonDocument.Parse(
            await sessionResponse.Content.ReadAsStringAsync());
        Ensure(
            sessionResponse.StatusCode == HttpStatusCode.Unauthorized,
            "Une session expirée devait être refusée.");
        Ensure(
            sessionPayload.RootElement.GetProperty("code").GetString()
                == "SESSION_EXPIRED",
            "Une session expirée ne retourne pas SESSION_EXPIRED.");
    }
    finally
    {
        await api.StopAsync();
    }
}

async Task RunLockoutResetTestAsync()
{
    var baseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    using var api = StartApi(
        baseUrl,
        startInfo =>
        {
            ConfigureMockAuthentication(startInfo, "active", "60");
            startInfo.Environment["LOGIN_MAX_FAILURES"] = "3";
            startInfo.Environment["LOGIN_LOCKOUT_MINUTES"] = "10";
        });
    using var handler = new HttpClientHandler { UseProxy = false };
    using var client = new HttpClient(handler);

    try
    {
        using var healthResponse = await WaitForHealthAsync(
            client,
            api.Process,
            baseUrl,
            api.Logs);
        Ensure(healthResponse.IsSuccessStatusCode, "Health lockout invalide.");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var failure = await client.PostAsJsonAsync(
                $"{baseUrl}/internal/auth/sessions",
                new { email = mockEmail, password = "INVALID_BEFORE_SUCCESS" });
            Ensure(
                failure.StatusCode == HttpStatusCode.Unauthorized,
                "Les premiers échecs doivent rester génériques.");
        }

        using var success = await client.PostAsJsonAsync(
            $"{baseUrl}/internal/auth/sessions",
            new { email = mockEmail, password = mockPassword });
        Ensure(
            success.StatusCode == HttpStatusCode.OK,
            "Un login réussi doit remettre le compteur à zéro.");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var failure = await client.PostAsJsonAsync(
                $"{baseUrl}/internal/auth/sessions",
                new { email = mockEmail, password = "INVALID_AFTER_SUCCESS" });
            Ensure(
                failure.StatusCode == HttpStatusCode.Unauthorized,
                "Le compteur n'a pas été remis à zéro après succès.");
        }

        using var lockedResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/internal/auth/sessions",
            new { email = mockEmail, password = "INVALID_LOCKING_ATTEMPT" });
        using var lockedPayload = JsonDocument.Parse(
            await lockedResponse.Content.ReadAsStringAsync());
        Ensure(
            lockedResponse.StatusCode == HttpStatusCode.TooManyRequests,
            "Le compte devait être temporairement verrouillé.");
        Ensure(
            lockedPayload.RootElement.GetProperty("code").GetString()
                == "ACCOUNT_LOCKED",
            "Le verrouillage ne retourne pas ACCOUNT_LOCKED.");

        using var validWhileLockedResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/internal/auth/sessions",
            new { email = mockEmail, password = mockPassword });
        Ensure(
            validWhileLockedResponse.StatusCode
                == HttpStatusCode.TooManyRequests,
            "Un compte verrouillé ne doit pas accepter le bon mot de passe.");
    }
    finally
    {
        await api.StopAsync();
    }
}

async Task RunUnavailableReadinessTestAsync()
{
    var baseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    const string sqlPasswordSentinel =
        "NOT_A_REAL_SQL_PASSWORD_V09";
    using var api = StartApi(
        baseUrl,
        startInfo =>
        {
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
            startInfo.Environment["SQL_PROVIDER"] = "mariadb";
            startInfo.Environment["SQL_HOST"] = "127.0.0.1";
            startInfo.Environment["SQL_PORT"] = "1";
            startInfo.Environment["SQL_DATABASE"] = "unavailable";
            startInfo.Environment["SQL_USERNAME"] = "unavailable";
            startInfo.Environment["SQL_PASSWORD"] = sqlPasswordSentinel;
            startInfo.Environment["AD_INTEGRATION_MODE"] = "disabled";
        });
    using var handler = new HttpClientHandler { UseProxy = false };
    using var client = new HttpClient(handler);

    try
    {
        using var liveResponse = await WaitForEndpointAsync(
            client,
            api.Process,
            $"{baseUrl}/health/live",
            api.Logs);
        Ensure(
            liveResponse.StatusCode == HttpStatusCode.OK,
            "Le health live doit répondre sans MariaDB.");

        using var readyResponse = await client.GetAsync(
            $"{baseUrl}/health/ready");
        var readyBody = await readyResponse.Content.ReadAsStringAsync();
        Ensure(
            readyResponse.StatusCode
                == HttpStatusCode.ServiceUnavailable,
            "Le health ready doit refuser une MariaDB indisponible.");
        Ensure(
            !readyBody.Contains(
                sqlPasswordSentinel,
                StringComparison.Ordinal)
            && !SnapshotLogs(api.Logs).Contains(
                sqlPasswordSentinel,
                StringComparison.Ordinal),
            "La readiness ne doit divulguer aucun mot de passe SQL.");

        using var readyAliasResponse = await client.GetAsync(
            $"{baseUrl}/ready");
        Ensure(
            readyAliasResponse.StatusCode
                == HttpStatusCode.ServiceUnavailable,
            "L'alias /ready doit refuser une MariaDB indisponible.");
    }
    finally
    {
        await api.StopAsync();
    }
}

async Task RunProductionConfigurationValidationTestsAsync()
{
    VerifyRejectedProductionConfiguration(
        "SQL_PASSWORD",
        configuration =>
        {
            configuration.Remove("SQL_PASSWORD");
            configuration["SERVICE_AUTH_TOKEN"] =
                "NOT_A_REAL_SERVICE_AUTH_VALUE_V09";
        });

    VerifyRejectedProductionConfiguration(
        "SERVICE_AUTH_TOKEN",
        configuration =>
        {
            configuration["SQL_PASSWORD"] =
                "NOT_A_REAL_PRODUCTION_SQL_VALUE_V09";
            configuration["SERVICE_AUTH_TOKEN"] =
                "**REPLACE_WITH_SECURE_VALUE**";
        });

    VerifyRejectedProductionConfiguration(
        "RUN_MARIADB_TESTS",
        configuration =>
        {
            configuration["RUN_MARIADB_TESTS"] = "true";
        });

    ValidateRuntimeConfiguration(
        new ConfigurationBuilder().Build(),
        "Development");

    await Task.CompletedTask;
}

async Task RunServiceAuthenticationGuardTestsAsync()
{
    var baseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    const string serviceAuthToken = "NOT_A_REAL_SERVICE_AUTH_GUARD_VALUE_V019";
    using var api = StartApi(
        baseUrl,
        startInfo =>
        {
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Staging";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Staging";
            startInfo.Environment["SQL_PROVIDER"] = "mariadb";
            startInfo.Environment["SQL_HOST"] = "127.0.0.1";
            startInfo.Environment["SQL_PORT"] = "3306";
            startInfo.Environment["SQL_DATABASE"] = "service-auth-guard";
            startInfo.Environment["SQL_USERNAME"] = "service-auth-guard";
            startInfo.Environment["SQL_PASSWORD"] =
                "NOT_A_REAL_SQL_GUARD_VALUE_V019";
            startInfo.Environment["SERVICE_AUTH_TOKEN"] = serviceAuthToken;
            startInfo.Environment["SESSION_COOKIE_SECURE"] = "true";
            startInfo.Environment["AD_INTEGRATION_MODE"] = "disabled";
            foreach (var variable in new[]
            {
                "DEMO_PORTAL_EMAIL",
                "DEMO_PORTAL_PASSWORD",
                "DEMO_PORTAL_STATUS",
                "DEMO_INTERNAL_ADMIN_EMAIL",
                "DEMO_INTERNAL_ADMIN_PASSWORD"
            })
            {
                startInfo.Environment.Remove(variable);
            }
        });
    using var handler = new HttpClientHandler { UseProxy = false };
    using var client = new HttpClient(handler);

    try
    {
        using var healthResponse = await WaitForHealthAsync(
            client,
            api.Process,
            baseUrl,
            api.Logs);
        Ensure(
            healthResponse.IsSuccessStatusCode,
            "Le health check staging doit repondre pour tester X-Service-Auth.");

        using var missingHeaderResponse = await client.GetAsync(
            $"{baseUrl}/internal/admin/ad/status");
        using var missingHeaderPayload = JsonDocument.Parse(
            await missingHeaderResponse.Content.ReadAsStringAsync());
        Ensure(
            missingHeaderResponse.StatusCode == HttpStatusCode.Unauthorized
            && missingHeaderPayload.RootElement.GetProperty("code").GetString()
                == "SERVICE_AUTH_REQUIRED",
            "Les routes /internal/* doivent exiger X-Service-Auth hors Development.");

        using var invalidHeaderRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/internal/admin/ad/status");
        invalidHeaderRequest.Headers.Add(
            serviceAuthHeader,
            "NOT_A_REAL_INVALID_SERVICE_AUTH_V019");
        using var invalidHeaderResponse = await client.SendAsync(
            invalidHeaderRequest);
        using var invalidHeaderPayload = JsonDocument.Parse(
            await invalidHeaderResponse.Content.ReadAsStringAsync());
        Ensure(
            invalidHeaderResponse.StatusCode == HttpStatusCode.Unauthorized
            && invalidHeaderPayload.RootElement.GetProperty("code").GetString()
                == "SERVICE_AUTH_REQUIRED",
            "Un X-Service-Auth invalide doit etre refuse hors Development.");

        using var validHeaderRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/internal/admin/ad/status");
        validHeaderRequest.Headers.Add(serviceAuthHeader, serviceAuthToken);
        using var validHeaderResponse = await client.SendAsync(
            validHeaderRequest);
        var validHeaderText =
            await validHeaderResponse.Content.ReadAsStringAsync();
        Ensure(
            !validHeaderText.Contains(
                "SERVICE_AUTH_REQUIRED",
                StringComparison.Ordinal),
            "Un X-Service-Auth valide ne doit pas etre rejete par le middleware.");

        // V0.19 : le statut BPCE est egalement protege par X-Service-Auth
        using var bpceMissingAuthResponse = await client.GetAsync(
            $"{baseUrl}/internal/admin/bpce/status");
        using var bpceMissingAuthPayload = JsonDocument.Parse(
            await bpceMissingAuthResponse.Content.ReadAsStringAsync());
        Ensure(
            bpceMissingAuthResponse.StatusCode == HttpStatusCode.Unauthorized
            && bpceMissingAuthPayload.RootElement.GetProperty("code").GetString()
                == "SERVICE_AUTH_REQUIRED",
            "L'endpoint BPCE/status doit exiger X-Service-Auth hors Development.");

        using var bpceValidAuthRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/internal/admin/bpce/status");
        bpceValidAuthRequest.Headers.Add(serviceAuthHeader, serviceAuthToken);
        using var bpceValidAuthResponse = await client.SendAsync(bpceValidAuthRequest);
        var bpceValidAuthText =
            await bpceValidAuthResponse.Content.ReadAsStringAsync();
        Ensure(
            !bpceValidAuthText.Contains(
                "SERVICE_AUTH_REQUIRED",
                StringComparison.Ordinal),
            "Un X-Service-Auth valide ne doit pas etre rejete sur l'endpoint BPCE.");
    }
    finally
    {
        await api.StopAsync();
    }
}

void VerifyRejectedProductionConfiguration(
    string expectedVariable,
    Action<Dictionary<string, string?>> configure)
{
    var configuration = CreateProductionConfiguration();
    configure(configuration);

    try
    {
        ValidateRuntimeConfiguration(
            new ConfigurationBuilder()
                .AddInMemoryCollection(configuration)
                .Build(),
            "Production");
        throw new InvalidOperationException(
            "Une configuration Production invalide a été acceptée.");
    }
    catch (TargetInvocationException exception)
        when (TryGetRuntimeConfigurationException(
            exception,
            out var runtimeException))
    {
        var configurationContracts = GetRuntimeConfigurationContracts();
        var variables = configurationContracts.GetVariables(runtimeException);

        Ensure(
            variables.Contains(expectedVariable),
            $"Le refus doit nommer {expectedVariable} sans afficher sa valeur.");
        Ensure(
            !runtimeException.Message.Contains(
                "NOT_A_REAL_PRODUCTION_SQL_VALUE_V09",
                StringComparison.Ordinal)
            && !runtimeException.Message.Contains(
                "NOT_A_REAL_SERVICE_AUTH_VALUE_V09",
                StringComparison.Ordinal)
            && !runtimeException.Message.Contains(
                "**REPLACE_WITH_SECURE_VALUE**",
                StringComparison.Ordinal),
            "Le message de configuration ne doit contenir aucune valeur secrète.");
    }
}

Dictionary<string, string?> CreateProductionConfiguration()
{
    return new Dictionary<string, string?>
    {
        ["SQL_PROVIDER"] = "mariadb",
        ["SQL_HOST"] = "127.0.0.1",
        ["SQL_PORT"] = "3306",
        ["SQL_DATABASE"] = "production-validation",
        ["SQL_USERNAME"] = "production-validation",
        ["SQL_PASSWORD"] = "NOT_A_REAL_PRODUCTION_SQL_VALUE_V09",
        ["SERVICE_AUTH_TOKEN"] = "NOT_A_REAL_SERVICE_AUTH_VALUE_V09",
        ["SESSION_COOKIE_SECURE"] = "true",
        ["AD_INTEGRATION_MODE"] = "disabled"
    };
}

async Task RunMariaDbReadTestsAsync()
{
    var requiredVariables = new[]
    {
        "SQL_PROVIDER",
        "SQL_HOST",
        "SQL_PORT",
        "SQL_DATABASE",
        "SQL_USERNAME",
        "SQL_PASSWORD",
        "DEMO_PORTAL_EMAIL",
        "DEMO_PORTAL_PASSWORD",
        "DEMO_INTERNAL_ADMIN_EMAIL",
        "DEMO_INTERNAL_ADMIN_PASSWORD"
    };
    var missing = requiredVariables
        .Where(name => string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(name)))
        .ToArray();

    Ensure(
        missing.Length == 0,
        "RUN_MARIADB_TESTS=true exige toutes les variables SQL.");

    var mariaDbBaseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    using var api = StartApi(
        mariaDbBaseUrl,
        startInfo =>
        {
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
            startInfo.Environment["AD_INTEGRATION_MODE"] = "disabled";
            startInfo.Environment["LOGIN_MAX_FAILURES"] = "3";
            startInfo.Environment["LOGIN_LOCKOUT_MINUTES"] = "10";
        });
    using var handler = new HttpClientHandler { UseProxy = false };
    using var client = new HttpClient(handler);
    const string isolationCustomerId =
        "90000000-0000-0000-0000-000000000071";
    const string isolationServiceId =
        "90000000-0000-0000-0000-000000000072";
    const string isolationNotificationId =
        "90000000-0000-0000-0000-000000000073";
    const string isolationSupportRequestId =
        "90000000-0000-0000-0000-000000000074";
    string? workflowSupportRequestId = null;
    string? workflowServiceRequestId = null;
    string? adLinkFixtureId = null;

    try
    {
        using var healthResponse = await WaitForHealthAsync(
            client,
            api.Process,
            mariaDbBaseUrl,
            api.Logs);
        Ensure(healthResponse.IsSuccessStatusCode, "Health MariaDB invalide.");

        using var readyResponse = await client.GetAsync(
            $"{mariaDbBaseUrl}/health/ready");
        using var readyPayload = JsonDocument.Parse(
            await readyResponse.Content.ReadAsStringAsync());
        Ensure(
            readyResponse.StatusCode == HttpStatusCode.OK
            && readyPayload.RootElement.GetProperty("checks")
                .GetProperty("mariadb").GetString() == "healthy",
            "La readiness MariaDB conditionnelle est invalide.");

        using var readyAliasResponse = await client.GetAsync(
            $"{mariaDbBaseUrl}/ready");
        using var readyAliasPayload = JsonDocument.Parse(
            await readyAliasResponse.Content.ReadAsStringAsync());
        Ensure(
            readyAliasResponse.StatusCode == HttpStatusCode.OK
            && readyAliasPayload.RootElement.GetProperty("checks")
                .GetProperty("mariadb").GetString() == "healthy",
            "L'alias /ready MariaDB conditionnelle est invalide.");
        await VerifyNotificationMigrationAsync();
        await VerifyCommercialMigrationAsync();

        using var loginResponse = await client.PostAsJsonAsync(
            $"{mariaDbBaseUrl}/internal/auth/sessions",
            new
            {
                email = Environment.GetEnvironmentVariable(
                    "DEMO_PORTAL_EMAIL"),
                password = Environment.GetEnvironmentVariable(
                    "DEMO_PORTAL_PASSWORD")
            });
        using var loginPayload = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());
        Ensure(
            loginResponse.StatusCode == HttpStatusCode.OK,
            "Le login MariaDB conditionnel a échoué.");
        var sessionToken = loginPayload.RootElement
            .GetProperty("sessionToken")
            .GetString()
            ?? throw new InvalidOperationException(
                "Le login MariaDB ne retourne aucun token.");
        await VerifyPersistedSessionHashAsync(sessionToken);
        await PrepareIsolationFixtureAsync(
            isolationCustomerId,
            isolationServiceId,
            isolationSupportRequestId);

        using var clientAdminRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/admin/overview",
            sessionToken);
        using var clientAdminResponse = await client.SendAsync(
            clientAdminRequest);
        Ensure(
            clientAdminResponse.StatusCode == HttpStatusCode.Forbidden,
            "Le client MariaDB ne doit pas accéder à l'administration.");

        using var adminLoginResponse = await client.PostAsJsonAsync(
            $"{mariaDbBaseUrl}/internal/auth/sessions",
            new
            {
                email = Environment.GetEnvironmentVariable(
                    "DEMO_INTERNAL_ADMIN_EMAIL"),
                password = Environment.GetEnvironmentVariable(
                    "DEMO_INTERNAL_ADMIN_PASSWORD")
            });
        using var adminLoginPayload = JsonDocument.Parse(
            await adminLoginResponse.Content.ReadAsStringAsync());
        Ensure(
            adminLoginResponse.StatusCode == HttpStatusCode.OK,
            "Le login admin MariaDB conditionnel a échoué.");
        Ensure(
            adminLoginPayload.RootElement
                .GetProperty("user")
                .GetProperty("role")
                .GetString() == "internal_admin",
            "Le seed admin MariaDB n'a pas le rôle attendu.");
        var adminSessionToken = adminLoginPayload.RootElement
            .GetProperty("sessionToken")
            .GetString()
            ?? throw new InvalidOperationException(
                "Le login admin MariaDB ne retourne aucun token.");
        await VerifyPersistedSessionHashAsync(adminSessionToken);

        using var adminCustomerListRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/admin/customers",
            adminSessionToken);
        using var adminCustomerListResponse = await client.SendAsync(
            adminCustomerListRequest);
        var adminCustomerListText = await adminCustomerListResponse.Content
            .ReadAsStringAsync();
        using var adminCustomerListPayload = JsonDocument.Parse(
            adminCustomerListText);
        Ensure(
            adminCustomerListResponse.StatusCode == HttpStatusCode.OK,
            "La liste clients admin MariaDB devait répondre HTTP 200.");
        Ensure(
            adminCustomerListResponse.Headers.GetValues(dataSourceHeader).Single()
                == "mariadb",
            "La liste clients admin n'utilise pas MariaDB.");
        Ensure(
            adminCustomerListPayload.RootElement.ValueKind == JsonValueKind.Array
            && adminCustomerListPayload.RootElement.GetArrayLength() > 0,
            "Aucun client MariaDB disponible pour tester la fiche admin");
        var mariaDbCustomerReference = adminCustomerListPayload.RootElement
            .EnumerateArray()
            .Select(element => element.TryGetProperty(
                    "customerReference",
                    out var customerReferenceProperty)
                ? customerReferenceProperty.GetString()
                : null)
            .FirstOrDefault(customerReference =>
                !string.IsNullOrWhiteSpace(customerReference))
            ?? throw new InvalidOperationException(
                "Aucun client MariaDB disponible pour tester la fiche admin");

        foreach (var endpoint in new[]
        {
            "/internal/admin/overview",
            "/internal/admin/customers",
            "/internal/admin/support-requests",
            "/internal/admin/service-requests",
            "/internal/admin/sessions",
            "/internal/admin/audit-logs"
        })
        {
            using var request = CreateSessionRequest(
                HttpMethod.Get,
                $"{mariaDbBaseUrl}{endpoint}",
                adminSessionToken);
            using var response = await client.SendAsync(request);
            Ensure(
                response.StatusCode == HttpStatusCode.OK,
                $"La route admin MariaDB {endpoint} devait répondre HTTP 200.");
            Ensure(
                response.Headers.GetValues(dataSourceHeader).Single()
                    == "mariadb",
                $"La route admin {endpoint} n'utilise pas MariaDB.");
        }

        using var adminCustomerDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/admin/customers/{mariaDbCustomerReference}",
            adminSessionToken);
        using var adminCustomerDetailResponse = await client.SendAsync(
            adminCustomerDetailRequest);
        var adminCustomerDetailText =
            await adminCustomerDetailResponse.Content.ReadAsStringAsync();
        Ensure(
            adminCustomerDetailResponse.StatusCode == HttpStatusCode.OK,
            "La fiche client admin MariaDB devait répondre HTTP 200.");
        Ensure(
            adminCustomerDetailResponse.Headers.GetValues(dataSourceHeader).Single()
                == "mariadb",
            "La fiche client admin n'utilise pas MariaDB.");
        Ensure(
            adminCustomerDetailText.Contains(
                "supportRequests",
                StringComparison.Ordinal)
            && adminCustomerDetailText.Contains(
                "commercialDocuments",
                StringComparison.Ordinal),
            "La fiche client admin MariaDB ne contient pas les sections attendues.");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var failure = await client.PostAsJsonAsync(
                $"{mariaDbBaseUrl}/internal/auth/sessions",
                new
                {
                    email = Environment.GetEnvironmentVariable(
                        "DEMO_INTERNAL_ADMIN_EMAIL"),
                    password = "INVALID_MARIADB_ADMIN_PASSWORD"
                });
            Ensure(
                attempt < 2
                    ? failure.StatusCode == HttpStatusCode.Unauthorized
                    : failure.StatusCode == HttpStatusCode.TooManyRequests,
                "Le verrouillage MariaDB admin ne suit pas le seuil configuré.");
        }

        await ResetLoginFailureFixtureAsync(
            Environment.GetEnvironmentVariable(
                "DEMO_INTERNAL_ADMIN_EMAIL")!);
        using var adminLoginAfterReset = await client.PostAsJsonAsync(
            $"{mariaDbBaseUrl}/internal/auth/sessions",
            new
            {
                email = Environment.GetEnvironmentVariable(
                    "DEMO_INTERNAL_ADMIN_EMAIL"),
                password = Environment.GetEnvironmentVariable(
                    "DEMO_INTERNAL_ADMIN_PASSWORD")
            });
        Ensure(
            adminLoginAfterReset.StatusCode == HttpStatusCode.OK,
            "Le reset du verrouillage MariaDB devait restaurer le login.");

        using var summaryRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/portal/summary",
            sessionToken);
        using var summaryResponse = await client.SendAsync(summaryRequest);
        Ensure(
            summaryResponse.StatusCode == HttpStatusCode.OK,
            "Le test MariaDB conditionnel n'a pas pu lire le résumé.");
        Ensure(
            summaryResponse.Headers.GetValues(dataSourceHeader).Single()
                == "mariadb",
            "Le test conditionnel n'utilise pas MariaDB.");

        foreach (var endpoint in new[]
        {
            "/internal/portal/services",
            "/internal/portal/invoices",
            "/internal/portal/service-catalog",
            "/internal/portal/service-requests"
        })
        {
            using var request = CreateSessionRequest(
                HttpMethod.Get,
                $"{mariaDbBaseUrl}{endpoint}",
                sessionToken);
            using var response = await client.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();

            Ensure(
                response.StatusCode == HttpStatusCode.OK,
                $"Le test MariaDB conditionnel a échoué pour {endpoint}. Réponse publique : {responseText}");
            Ensure(
                response.Headers.GetValues(dataSourceHeader).Single()
                    == "mariadb",
                $"Le endpoint {endpoint} n'utilise pas MariaDB.");

            using var payload = JsonDocument.Parse(responseText);
            Ensure(
                payload.RootElement.ValueKind == JsonValueKind.Array,
                $"Le endpoint {endpoint} ne retourne pas une liste JSON.");
        }

        using var catalogForWriteRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/portal/service-catalog",
            sessionToken);
        using var catalogForWriteResponse = await client.SendAsync(
            catalogForWriteRequest);
        using var catalogForWritePayload = JsonDocument.Parse(
            await catalogForWriteResponse.Content.ReadAsStringAsync());
        var catalogItemId = catalogForWritePayload.RootElement[0]
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException(
                "Le catalogue MariaDB ne contient aucun identifiant.");

        using var serviceWriteRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/portal/service-requests")
        {
            Content = JsonContent.Create(new
            {
                catalogItemId,
                subject = "Demande de service conditionnelle",
                description =
                    "Écriture MariaDB opt-in sans donnée sensible."
            })
        };
        serviceWriteRequest.Headers.Add(
            correlationHeader,
            "v0.7-mariadb-service-write");
        serviceWriteRequest.Headers.Add(sessionHeader, sessionToken);
        using var serviceWriteResponse = await client.SendAsync(
            serviceWriteRequest);
        using var serviceWritePayload = JsonDocument.Parse(
            await serviceWriteResponse.Content.ReadAsStringAsync());
        Ensure(
            serviceWriteResponse.StatusCode == HttpStatusCode.Accepted,
            "L'écriture MariaDB d'une demande de service devait retourner HTTP 202.");
        Ensure(
            serviceWritePayload.RootElement.GetProperty("persisted").GetBoolean(),
            "La demande de service MariaDB doit retourner persisted:true.");
        Ensure(
            !string.IsNullOrWhiteSpace(
                serviceWritePayload.RootElement
                    .GetProperty("reference")
                    .GetString()),
            "La demande de service MariaDB ne retourne pas de référence.");
        Ensure(
            serviceWritePayload.RootElement
                .GetProperty("correlation_id")
                .GetString() == "v0.7-mariadb-service-write",
            "La demande de service MariaDB ne propage pas le correlation_id.");
        var workflowServiceReference = serviceWritePayload.RootElement
            .GetProperty("reference")
            .GetString()!;

        using var invalidCatalogRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/portal/service-requests")
        {
            Content = JsonContent.Create(new
            {
                catalogItemId = "catalog-inexistant-v07",
                subject = "Catalogue invalide",
                description = "Cette demande opt-in doit être refusée."
            })
        };
        invalidCatalogRequest.Headers.Add(sessionHeader, sessionToken);
        using var invalidCatalogResponse = await client.SendAsync(
            invalidCatalogRequest);
        Ensure(
            invalidCatalogResponse.StatusCode == HttpStatusCode.BadRequest,
            "Un catalogue MariaDB invalide devait retourner HTTP 400.");

        using var servicesForWriteRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/portal/services",
            sessionToken);
        using var servicesForWriteResponse = await client.SendAsync(
            servicesForWriteRequest);
        using var servicesForWritePayload = JsonDocument.Parse(
            await servicesForWriteResponse.Content.ReadAsStringAsync());
        var serviceId = servicesForWritePayload.RootElement[0]
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException(
                "Les services MariaDB ne contiennent aucun identifiant.");

        using var supportWriteRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/portal/support-requests")
        {
            Content = JsonContent.Create(new
            {
                serviceId,
                priority = "normal",
                subject = "Demande support conditionnelle",
                description =
                    "Écriture MariaDB opt-in sans donnée sensible."
            })
        };
        supportWriteRequest.Headers.Add(
            correlationHeader,
            "v0.7-mariadb-support-write");
        supportWriteRequest.Headers.Add(sessionHeader, sessionToken);
        using var supportWriteResponse = await client.SendAsync(
            supportWriteRequest);
        using var supportWritePayload = JsonDocument.Parse(
            await supportWriteResponse.Content.ReadAsStringAsync());
        Ensure(
            supportWriteResponse.StatusCode == HttpStatusCode.Accepted,
            "L'écriture MariaDB d'une demande support devait retourner HTTP 202.");
        Ensure(
            supportWritePayload.RootElement.GetProperty("persisted").GetBoolean(),
            "La demande support MariaDB doit retourner persisted:true.");
        var workflowSupportReference = supportWritePayload.RootElement
            .GetProperty("reference")
            .GetString()!;

        workflowSupportRequestId = await FindRequestIdAsync(
            client,
            mariaDbBaseUrl,
            "/internal/portal/support-requests",
            sessionToken,
            workflowSupportReference);
        workflowServiceRequestId = await FindRequestIdAsync(
            client,
            mariaDbBaseUrl,
            "/internal/portal/service-requests",
            sessionToken,
            workflowServiceReference);

        await VerifyCommercialFoundationAsync(
            client,
            mariaDbBaseUrl,
            sessionToken,
            adminSessionToken,
            "CLI-DEMO-0060",
            workflowServiceRequestId,
            persistent: true,
            foreignCustomerId: isolationCustomerId);

        adLinkFixtureId = await InsertCustomerAdLinkAsync("CLI-DEMO-0060");
        using var adLinksRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/admin/customers/CLI-DEMO-0060/ad-links",
            adminSessionToken);
        using var adLinksResponse = await client.SendAsync(adLinksRequest);
        using var adLinksPayload = JsonDocument.Parse(
            await adLinksResponse.Content.ReadAsStringAsync());
        Ensure(
            adLinksResponse.StatusCode == HttpStatusCode.OK
            && adLinksPayload.RootElement.EnumerateArray().Any(item =>
                item.GetProperty("id").GetString() == adLinkFixtureId
                && item.GetProperty("customerReference").GetString()
                    == "CLI-DEMO-0060"
                && !string.IsNullOrWhiteSpace(
                    item.GetProperty("objectGuid").GetString())),
            "La lecture MariaDB des liens AD doit rester lisible aprÃ¨s insertion d'un lien.");

        using var refreshedAdminCustomerDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/admin/customers/CLI-DEMO-0060",
            adminSessionToken);
        using var refreshedAdminCustomerDetailResponse = await client.SendAsync(
            refreshedAdminCustomerDetailRequest);
        var refreshedAdminCustomerDetailText =
            await refreshedAdminCustomerDetailResponse.Content.ReadAsStringAsync();
        using var refreshedAdminCustomerDetailPayload = JsonDocument.Parse(
            refreshedAdminCustomerDetailText);
        Ensure(
            refreshedAdminCustomerDetailResponse.StatusCode
                == HttpStatusCode.OK,
            "La fiche client admin CLI-DEMO-0060 doit rester lisible aprÃ¨s ajout d'un document commercial liÃ©.");
        Ensure(
            refreshedAdminCustomerDetailPayload.RootElement
                .GetProperty("commercialDocuments")
                .EnumerateArray()
                .Any(item =>
                    item.GetProperty("serviceRequestId").GetString()
                        == workflowServiceRequestId),
            "La fiche client admin MariaDB doit exposer le serviceRequestId du document commercial liÃ©.");

        using var mariaSupportStatusRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{mariaDbBaseUrl}/internal/admin/support-requests/{workflowSupportRequestId}/status",
            adminSessionToken);
        mariaSupportStatusRequest.Content = JsonContent.Create(
            new { status = "in_progress" });
        using var mariaSupportStatusResponse = await client.SendAsync(
            mariaSupportStatusRequest);
        Ensure(
            mariaSupportStatusResponse.StatusCode == HttpStatusCode.OK,
            "Le changement de statut support MariaDB devait réussir.");

        using var mariaServiceStatusRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{mariaDbBaseUrl}/internal/admin/service-requests/{workflowServiceRequestId}/status",
            adminSessionToken);
        mariaServiceStatusRequest.Content = JsonContent.Create(
            new { status = "under_review" });
        using var mariaServiceStatusResponse = await client.SendAsync(
            mariaServiceStatusRequest);
        Ensure(
            mariaServiceStatusResponse.StatusCode == HttpStatusCode.OK,
            "Le changement de statut service MariaDB devait réussir.");

        const string mariaPrivateNote = "Note interne MariaDB V0.11.";
        using var mariaNoteRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/admin/support-requests/{workflowSupportRequestId}/notes",
            adminSessionToken);
        mariaNoteRequest.Content = JsonContent.Create(
            new { text = mariaPrivateNote });
        using var mariaNoteResponse = await client.SendAsync(mariaNoteRequest);
        Ensure(
            mariaNoteResponse.StatusCode == HttpStatusCode.OK,
            "L'ajout de note interne MariaDB devait réussir.");

        const string mariaPublicMessage =
            "Message public MariaDB de suivi V0.11.";
        using var mariaMessageRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/admin/support-requests/{workflowSupportRequestId}/messages",
            adminSessionToken);
        mariaMessageRequest.Content = JsonContent.Create(
            new { text = mariaPublicMessage });
        using var mariaMessageResponse = await client.SendAsync(
            mariaMessageRequest);
        Ensure(
            mariaMessageResponse.StatusCode == HttpStatusCode.OK,
            "L'ajout de message public MariaDB devait réussir.");

        using var mariaServiceMessageRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/admin/service-requests/{workflowServiceRequestId}/messages",
            adminSessionToken);
        mariaServiceMessageRequest.Content = JsonContent.Create(
            new { text = "Message public MariaDB de service V0.12." });
        using var mariaServiceMessageResponse = await client.SendAsync(
            mariaServiceMessageRequest);
        Ensure(
            mariaServiceMessageResponse.StatusCode == HttpStatusCode.OK,
            "L'ajout de message public service MariaDB devait réussir.");

        const string mariaClientSupportReply =
            "Réponse client MariaDB support V0.13.";
        using var mariaClientSupportReplyRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/portal/support-requests/{workflowSupportRequestId}/messages",
            sessionToken);
        mariaClientSupportReplyRequest.Content = JsonContent.Create(
            new { text = mariaClientSupportReply });
        using var mariaClientSupportReplyResponse = await client.SendAsync(
            mariaClientSupportReplyRequest);
        Ensure(
            mariaClientSupportReplyResponse.StatusCode == HttpStatusCode.OK,
            "La réponse client support MariaDB devait réussir.");

        const string mariaClientServiceReply =
            "Réponse client MariaDB service V0.13.";
        using var mariaClientServiceReplyRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/portal/service-requests/{workflowServiceRequestId}/messages",
            sessionToken);
        mariaClientServiceReplyRequest.Content = JsonContent.Create(
            new { text = mariaClientServiceReply });
        using var mariaClientServiceReplyResponse = await client.SendAsync(
            mariaClientServiceReplyRequest);
        Ensure(
            mariaClientServiceReplyResponse.StatusCode == HttpStatusCode.OK,
            "La réponse client service MariaDB devait réussir.");

        using var mariaAdminActivityRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/admin/activity",
            adminSessionToken);
        using var mariaAdminActivityResponse = await client.SendAsync(
            mariaAdminActivityRequest);
        var mariaAdminActivityText =
            await mariaAdminActivityResponse.Content.ReadAsStringAsync();
        using var mariaAdminActivityPayload = JsonDocument.Parse(
            mariaAdminActivityText);
        var mariaRecentActivities = mariaAdminActivityPayload.RootElement
            .GetProperty("recentActivities")
            .EnumerateArray()
            .ToArray();
        Ensure(
            mariaAdminActivityResponse.StatusCode == HttpStatusCode.OK
            && mariaAdminActivityPayload.RootElement
                .GetProperty("recentClientReplyCount")
                .GetInt32() >= 2
            && mariaRecentActivities.Any(item =>
                item.GetProperty("requestId").GetString()
                    == workflowSupportRequestId
                && item.GetProperty("authorType").GetString() == "client")
            && mariaRecentActivities.Any(item =>
                item.GetProperty("requestId").GetString()
                    == workflowServiceRequestId
                && item.GetProperty("authorType").GetString() == "client"),
            "Le centre d'activité MariaDB doit identifier les réponses client.");
        Ensure(
            !mariaAdminActivityText.Contains(
                mariaPrivateNote,
                StringComparison.Ordinal)
            && !mariaAdminActivityText.Contains(
                mariaClientSupportReply,
                StringComparison.Ordinal)
            && !mariaAdminActivityText.Contains(
                mariaClientServiceReply,
                StringComparison.Ordinal),
            "L'activité MariaDB ne doit exposer aucun contenu sensible.");

        foreach (var (resource, expectedId) in new[]
        {
            ("support-requests", workflowSupportRequestId),
            ("service-requests", workflowServiceRequestId)
        })
        {
            using var mariaFilteredRequest = CreateSessionRequest(
                HttpMethod.Get,
                $"{mariaDbBaseUrl}/internal/admin/{resource}?attention=client_reply",
                adminSessionToken);
            using var mariaFilteredResponse = await client.SendAsync(
                mariaFilteredRequest);
            var mariaFilteredText =
                await mariaFilteredResponse.Content.ReadAsStringAsync();
            Ensure(
                mariaFilteredResponse.StatusCode == HttpStatusCode.OK
                && mariaFilteredText.Contains(
                    expectedId,
                    StringComparison.Ordinal)
                && mariaFilteredText.Contains(
                    "\"hasRecentClientReply\":true",
                    StringComparison.Ordinal),
                $"Le filtre MariaDB réponse client {resource} est invalide.");
        }

        using var mariaForeignReplyRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/portal/support-requests/{isolationSupportRequestId}/messages",
            sessionToken);
        mariaForeignReplyRequest.Content = JsonContent.Create(
            new { text = "Cette réponse doit être refusée." });
        using var mariaForeignReplyResponse = await client.SendAsync(
            mariaForeignReplyRequest);
        Ensure(
            mariaForeignReplyResponse.StatusCode == HttpStatusCode.NotFound,
            "Un client ne doit pas répondre à la demande d'un autre client.");

        using var mariaNotificationsRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/portal/notifications",
            sessionToken);
        using var mariaNotificationsResponse = await client.SendAsync(
            mariaNotificationsRequest);
        using var mariaNotificationsPayload = JsonDocument.Parse(
            await mariaNotificationsResponse.Content.ReadAsStringAsync());
        Ensure(
            mariaNotificationsResponse.StatusCode == HttpStatusCode.OK,
            "La lecture des notifications MariaDB devait réussir.");
        var workflowNotifications = mariaNotificationsPayload.RootElement
            .EnumerateArray()
            .Where(item =>
                item.GetProperty("linkUrl").GetString()?.Contains(
                    workflowSupportRequestId,
                    StringComparison.Ordinal) == true
                || item.GetProperty("linkUrl").GetString()?.Contains(
                    workflowServiceRequestId,
                    StringComparison.Ordinal) == true)
            .ToArray();
        Ensure(
            workflowNotifications.Length == 4,
            "Les événements visibles MariaDB devaient créer quatre notifications.");
        Ensure(
            workflowNotifications.All(item =>
                !item.GetRawText().Contains(
                    mariaPrivateNote,
                    StringComparison.Ordinal)
                && !item.GetRawText().Contains(
                    mariaPublicMessage,
                    StringComparison.Ordinal)),
            "Les notifications ne doivent contenir ni note interne ni message complet.");

        var mariaNotificationId = workflowNotifications[0]
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException(
                "La notification MariaDB ne retourne aucun identifiant.");
        using var mariaReadNotificationRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/portal/notifications/{mariaNotificationId}/read",
            sessionToken);
        using var mariaReadNotificationResponse = await client.SendAsync(
            mariaReadNotificationRequest);
        Ensure(
            mariaReadNotificationResponse.StatusCode == HttpStatusCode.OK,
            "Le marquage individuel MariaDB devait réussir.");

        using var mariaForeignNotificationRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/portal/notifications/{isolationNotificationId}/read",
            sessionToken);
        using var mariaForeignNotificationResponse = await client.SendAsync(
            mariaForeignNotificationRequest);
        Ensure(
            mariaForeignNotificationResponse.StatusCode
                == HttpStatusCode.NotFound,
            "Un client ne doit pas marquer la notification d'un autre client.");

        using var mariaReadAllRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/portal/notifications/read-all",
            sessionToken);
        using var mariaReadAllResponse = await client.SendAsync(
            mariaReadAllRequest);
        Ensure(
            mariaReadAllResponse.StatusCode == HttpStatusCode.OK,
            "Le marquage global MariaDB devait réussir.");

        using var mariaClientDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/portal/support-requests/{workflowSupportRequestId}",
            sessionToken);
        using var mariaClientDetailResponse = await client.SendAsync(
            mariaClientDetailRequest);
        var mariaClientDetailText =
            await mariaClientDetailResponse.Content.ReadAsStringAsync();
        Ensure(
            mariaClientDetailResponse.StatusCode == HttpStatusCode.OK
            && mariaClientDetailText.Contains(
                mariaPublicMessage,
                StringComparison.Ordinal)
            && mariaClientDetailText.Contains(
                mariaClientSupportReply,
                StringComparison.Ordinal)
            && mariaClientDetailText.Contains(
                "\"authorLabel\":\"Vous\"",
                StringComparison.Ordinal)
            && !mariaClientDetailText.Contains(
                mariaPrivateNote,
                StringComparison.Ordinal)
            && !mariaClientDetailText.Contains(
                "internalNotes",
                StringComparison.OrdinalIgnoreCase),
            "La séparation note interne/message public MariaDB est invalide.");

        using var mariaAdminSupportDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/admin/support-requests/{workflowSupportRequestId}",
            adminSessionToken);
        using var mariaAdminSupportDetailResponse = await client.SendAsync(
            mariaAdminSupportDetailRequest);
        var mariaAdminSupportDetailText =
            await mariaAdminSupportDetailResponse.Content.ReadAsStringAsync();
        Ensure(
            mariaAdminSupportDetailResponse.StatusCode == HttpStatusCode.OK
            && mariaAdminSupportDetailText.Contains(
                mariaClientSupportReply,
                StringComparison.Ordinal)
            && mariaAdminSupportDetailText.Contains(
                "\"authorType\":\"client\"",
                StringComparison.Ordinal),
            "La réponse client support MariaDB devait être visible par l'admin.");

        using var mariaClientServiceDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/portal/service-requests/{workflowServiceRequestId}",
            sessionToken);
        using var mariaClientServiceDetailResponse = await client.SendAsync(
            mariaClientServiceDetailRequest);
        var mariaClientServiceDetailText =
            await mariaClientServiceDetailResponse.Content.ReadAsStringAsync();
        Ensure(
            mariaClientServiceDetailResponse.StatusCode == HttpStatusCode.OK
            && mariaClientServiceDetailText.Contains(
                mariaClientServiceReply,
                StringComparison.Ordinal)
            && !mariaClientServiceDetailText.Contains(
                "internalNotes",
                StringComparison.OrdinalIgnoreCase),
            "La conversation service MariaDB devait rester publique.");

        await VerifyWorkflowPersistenceAsync(
            workflowSupportRequestId,
            workflowServiceRequestId);

        using var isolationRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{mariaDbBaseUrl}/internal/portal/support-requests")
        {
            Content = JsonContent.Create(new
            {
                serviceId = isolationServiceId,
                priority = "normal",
                subject = "Test isolation client",
                description = "Cette demande opt-in doit être refusée."
            })
        };
        isolationRequest.Headers.Add(sessionHeader, sessionToken);
        using var isolationResponse = await client.SendAsync(isolationRequest);
        Ensure(
            isolationResponse.StatusCode == HttpStatusCode.Forbidden,
            "Un service MariaDB d'un autre client devait être refusé.");

        using var adStatusRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/admin/ad/status",
            adminSessionToken);
        using var adStatusResponse = await client.SendAsync(adStatusRequest);
        using var adStatusPayload = JsonDocument.Parse(
            await adStatusResponse.Content.ReadAsStringAsync());
        Ensure(
            adStatusResponse.StatusCode == HttpStatusCode.OK,
            "Le diagnostic AD conditionnel ne répond pas avec HTTP 200.");
        Ensure(
            adStatusPayload.RootElement.GetProperty("mode").GetString()
                == "disabled",
            "Le test MariaDB ne doit pas activer Active Directory.");
    }
    finally
    {
        var adminEmail = Environment.GetEnvironmentVariable(
            "DEMO_INTERNAL_ADMIN_EMAIL");
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            await ResetLoginFailureFixtureAsync(adminEmail);
        }
        if (adLinkFixtureId is not null)
        {
            await DeleteCustomerAdLinkAsync(adLinkFixtureId);
        }
        await CleanupIsolationFixtureAsync(
            isolationCustomerId,
            isolationServiceId,
            isolationSupportRequestId);
        await CleanupWorkflowFixtureAsync(
            workflowSupportRequestId,
            workflowServiceRequestId);
        await api.StopAsync();
    }
}

async Task RunMockActiveDirectoryModeTestsAsync()
{
    var mockBaseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    using var api = StartApi(
        mockBaseUrl,
        startInfo =>
        {
            ConfigureMockAuthentication(startInfo, "active", "60");
            startInfo.Environment["AD_INTEGRATION_MODE"] = "mock";
            startInfo.Environment["AD_DOMAIN"] = "home.bzh";
            startInfo.Environment["AD_CLIENTS_OU_DN"] =
                "OU=TEST_SITE_WEB,DC=home,DC=bzh";
            startInfo.Environment["AD_CONNECT_TIMEOUT_MS"] = "3000";
            startInfo.Environment["AD_QUERY_TIMEOUT_MS"] = "5000";
            startInfo.Environment["AD_MAX_RESULTS"] = "25";
            startInfo.Environment.Remove("AD_SERVICE_ACCOUNT_USERNAME");
            startInfo.Environment.Remove("AD_SERVICE_ACCOUNT_PASSWORD");
        });
    using var handler = new HttpClientHandler { UseProxy = false };
    using var client = new HttpClient(handler);

    try
    {
        using var healthResponse = await WaitForHealthAsync(
            client,
            api.Process,
            mockBaseUrl,
            api.Logs);
        Ensure(
            healthResponse.IsSuccessStatusCode,
            "Le health check mock AD ne rÃ©pond pas correctement.");

        var adminSessionToken = await LoginAsAdminAsync(client, mockBaseUrl);
        await VerifyMockActiveDirectoryAdminRoutesAsync(
            client,
            mockBaseUrl,
            adminSessionToken);

        var logs = SnapshotLogs(api.Logs);
        Ensure(
            logs.Contains(
                "admin.customers.ad_users.write",
                StringComparison.Ordinal)
            && logs.Contains(
                "admin.customers.ad_group_members.write",
                StringComparison.Ordinal)
            && logs.Contains(
                "admin.customers.ad_users.move_to_disabled",
                StringComparison.Ordinal),
            "Les actions AD mock doivent Ãªtre journalisÃ©es sans exposer de secrets.");
    }
    finally
    {
        await api.StopAsync();
    }
}

async Task RunMockBpceIssuingTestsAsync()
{
    var mockBaseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    using var api = StartApi(
        mockBaseUrl,
        startInfo =>
        {
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
            startInfo.Environment["BPCE_INTEGRATION_MODE"] = "mock";
            startInfo.Environment["AD_INTEGRATION_MODE"] = "disabled";
            startInfo.Environment["DEMO_PORTAL_EMAIL"] = mockEmail;
            startInfo.Environment["DEMO_PORTAL_PASSWORD"] = mockPassword;
            startInfo.Environment["DEMO_PORTAL_STATUS"] = "active";
            startInfo.Environment["DEMO_INTERNAL_ADMIN_EMAIL"] = mockAdminEmail;
            startInfo.Environment["DEMO_INTERNAL_ADMIN_PASSWORD"] =
                mockAdminPassword;
            startInfo.Environment["SESSION_DURATION_MINUTES"] = "60";
            startInfo.Environment["LOGIN_MAX_FAILURES"] = "5";
            startInfo.Environment["LOGIN_LOCKOUT_MINUTES"] = "10";
            foreach (var variable in new[]
            {
                "SQL_PROVIDER", "SQL_HOST", "SQL_PORT",
                "SQL_DATABASE", "SQL_USERNAME", "SQL_PASSWORD"
            })
            {
                startInfo.Environment.Remove(variable);
            }
        });
    using var handler = new HttpClientHandler { UseProxy = false };
    using var client = new HttpClient(handler);

    try
    {
        using var healthResponse = await WaitForHealthAsync(
            client, api.Process, mockBaseUrl, api.Logs);
        Ensure(
            healthResponse.IsSuccessStatusCode,
            "Le health check BPCE mock doit répondre.");

        var adminSessionToken = await LoginAsAdminAsync(client, mockBaseUrl);

        // V0.20-1 : le mode BPCE mock est bien actif
        using var bpceStatusRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/bpce/status",
            adminSessionToken);
        using var bpceStatusResponse = await client.SendAsync(bpceStatusRequest);
        using var bpceStatusPayload = JsonDocument.Parse(
            await bpceStatusResponse.Content.ReadAsStringAsync());
        Ensure(
            bpceStatusResponse.StatusCode == HttpStatusCode.OK
            && bpceStatusPayload.RootElement.GetProperty("mode").GetString()
                == "mock"
            && bpceStatusPayload.RootElement.GetProperty("status").GetString()
                == "mock",
            "Le statut BPCE doit être mock dans cet environnement de test.");

        // V0.20-2 : création d'un document commercial
        using var createDocRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/admin/commercial-documents",
            adminSessionToken);
        createDocRequest.Content = JsonContent.Create(new
        {
            customerReference = MockCustomerReference(),
            documentType = "quote_draft",
            title = "Facture test V0.20",
            currency = "EUR",
            disclaimer = "Document de test smoke V0.20."
        });
        using var createDocResponse = await client.SendAsync(createDocRequest);
        using var createDocPayload = JsonDocument.Parse(
            await createDocResponse.Content.ReadAsStringAsync());
        Ensure(
            createDocResponse.StatusCode == HttpStatusCode.OK,
            "La création du document commercial doit réussir.");
        var documentId =
            createDocPayload.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException(
                "ID manquant après création du document commercial.");

        // V0.20-3 : ajout d'une ligne (montant non nul)
        using var addLineRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/admin/commercial-documents/{documentId}/lines",
            adminSessionToken);
        addLineRequest.Content = JsonContent.Create(new
        {
            label = "Service test BPCE",
            description = "Ligne de test pour émission BPCE mock.",
            quantity = 1m,
            unitLabel = "unité",
            unitPriceCents = 10000,
            taxRateBasisPoints = 2000,
            sortOrder = 10
        });
        using var addLineResponse = await client.SendAsync(addLineRequest);
        Ensure(
            addLineResponse.StatusCode == HttpStatusCode.OK,
            "L'ajout de ligne doit réussir.");

        // V0.20-4 : partage du document (statut → shared_with_customer)
        using var shareRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/admin/commercial-documents/{documentId}/share",
            adminSessionToken);
        shareRequest.Content = JsonContent.Create(new { });
        using var shareResponse = await client.SendAsync(shareRequest);
        using var sharePayload = JsonDocument.Parse(
            await shareResponse.Content.ReadAsStringAsync());
        Ensure(
            shareResponse.StatusCode == HttpStatusCode.OK
            && sharePayload.RootElement.GetProperty("status").GetString()
                == "shared_with_customer",
            "Le partage doit passer le document en shared_with_customer.");

        // V0.20-5 : émission de la facture en mode BPCE mock
        using var issueRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/admin/commercial-documents/{documentId}/issue",
            adminSessionToken);
        issueRequest.Content = JsonContent.Create(new { sendEmail = false });
        using var issueResponse = await client.SendAsync(issueRequest);
        var issueText = await issueResponse.Content.ReadAsStringAsync();
        using var issuePayload = JsonDocument.Parse(issueText);
        Ensure(
            issueResponse.StatusCode == HttpStatusCode.OK,
            "L'émission BPCE mock doit réussir avec HTTP 200.");
        Ensure(
            issuePayload.RootElement.TryGetProperty("invoice", out var invoiceEl)
            && invoiceEl.ValueKind != JsonValueKind.Null,
            "La réponse d'émission doit contenir les données de la facture.");
        Ensure(
            invoiceEl.TryGetProperty("fiscalNumber", out var fiscalEl)
            && !string.IsNullOrEmpty(fiscalEl.GetString()),
            "La facture mock doit avoir un numéro fiscal non vide.");
        Ensure(
            invoiceEl.TryGetProperty("status", out var invoiceStatusEl)
            && invoiceStatusEl.GetString() == "validated",
            "La facture mock doit avoir le statut validated.");
        Ensure(
            invoiceEl.TryGetProperty("pdfAvailable", out var pdfAvailableEl)
            && pdfAvailableEl.GetBoolean(),
            "Le PDF mock doit être disponible immédiatement après émission.");

        // V0.20-6 : relecture de l'enregistrement de facture
        using var getInvoiceRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/commercial-documents/{documentId}/invoice",
            adminSessionToken);
        using var getInvoiceResponse = await client.SendAsync(getInvoiceRequest);
        using var getInvoicePayload = JsonDocument.Parse(
            await getInvoiceResponse.Content.ReadAsStringAsync());
        Ensure(
            getInvoiceResponse.StatusCode == HttpStatusCode.OK,
            "La lecture de la facture émise doit réussir (GET /invoice).");
        Ensure(
            getInvoicePayload.RootElement.GetProperty("pdfAvailable").GetBoolean(),
            "Le PDF doit être disponible lors de la relecture.");

        // V0.20-7 : une seconde tentative d'émission doit être refusée
        using var issueAgainRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/admin/commercial-documents/{documentId}/issue",
            adminSessionToken);
        issueAgainRequest.Content = JsonContent.Create(new { sendEmail = false });
        using var issueAgainResponse = await client.SendAsync(issueAgainRequest);
        using var issueAgainPayload = JsonDocument.Parse(
            await issueAgainResponse.Content.ReadAsStringAsync());
        Ensure(
            issueAgainResponse.StatusCode == HttpStatusCode.Conflict,
            "Une double émission doit être refusée avec HTTP 409.");
        Ensure(
            issueAgainPayload.RootElement.GetProperty("code").GetString()
                == "INVOICE_ALREADY_ISSUED",
            "Le code de refus doit être INVOICE_ALREADY_ISSUED.");

        // V0.20-8 : récupération du PDF mis en cache
        using var getPdfRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/commercial-documents/{documentId}/invoice/pdf",
            adminSessionToken);
        using var getPdfResponse = await client.SendAsync(getPdfRequest);
        var pdfBytes = await getPdfResponse.Content.ReadAsByteArrayAsync();
        Ensure(
            getPdfResponse.StatusCode == HttpStatusCode.OK,
            "La récupération du PDF mock doit réussir.");
        Ensure(
            getPdfResponse.Content.Headers.ContentType?.MediaType == "application/pdf",
            "Le PDF doit être retourné avec Content-Type application/pdf.");
        Ensure(
            pdfBytes.Length > 0
            && Encoding.ASCII.GetString(
                pdfBytes, 0, Math.Min(5, pdfBytes.Length)) == "%PDF-",
            "Le contenu du PDF mock doit commencer par %PDF-.");

        // V0.20-9 : sans invoice, GET /invoice doit retourner 404
        var logs = SnapshotLogs(api.Logs);
        Ensure(
            logs.Contains("admin.commercial_documents.invoice.read",
                StringComparison.Ordinal),
            "L'audit de lecture de facture doit etre journalise.");
    }
    finally
    {
        await api.StopAsync();
    }
}

async Task RunReadOnlyActiveDirectoryModeTestsAsync()
{
    var mockBaseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    const string adPasswordSentinel = "NOT_A_REAL_AD_SERVICE_PASSWORD_V018";
    using var api = StartApi(
        mockBaseUrl,
        startInfo =>
        {
            ConfigureMockAuthentication(startInfo, "active", "60");
            startInfo.Environment["AD_INTEGRATION_MODE"] = "read_only";
            startInfo.Environment["AD_DOMAIN"] = "home.bzh";
            startInfo.Environment["AD_CLIENTS_OU_DN"] =
                "OU=TEST_SITE_WEB,DC=home,DC=bzh";
            startInfo.Environment["AD_SERVICE_ACCOUNT_USERNAME"] =
                @"HOME\svc_api_portal_ad";
            startInfo.Environment["AD_SERVICE_ACCOUNT_PASSWORD"] =
                adPasswordSentinel;
            startInfo.Environment["AD_CONNECT_TIMEOUT_MS"] = "3000";
            startInfo.Environment["AD_QUERY_TIMEOUT_MS"] = "5000";
            startInfo.Environment["AD_MAX_RESULTS"] = "25";
        });
    using var handler = new HttpClientHandler { UseProxy = false };
    using var client = new HttpClient(handler);

    try
    {
        using var healthResponse = await WaitForHealthAsync(
            client,
            api.Process,
            mockBaseUrl,
            api.Logs);
        Ensure(
            healthResponse.IsSuccessStatusCode,
            "Le health check read_only ne rÃ©pond pas correctement.");

        var adminSessionToken = await LoginAsAdminAsync(client, mockBaseUrl);

        using var statusRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mockBaseUrl}/internal/admin/ad/status",
            adminSessionToken);
        using var statusResponse = await client.SendAsync(statusRequest);
        var statusText = await statusResponse.Content.ReadAsStringAsync();
        using var statusPayload = JsonDocument.Parse(statusText);
        Ensure(
            statusResponse.StatusCode == HttpStatusCode.OK
            && statusPayload.RootElement.GetProperty("mode").GetString()
                == "read_only"
            && statusPayload.RootElement
                .GetProperty("configurationValid")
                .GetBoolean()
            && !statusPayload.RootElement
                .GetProperty("writesEnabled")
                .GetBoolean(),
            "Le statut AD read_only est invalide.");

        using var createUserRequest = CreateSessionRequest(
            HttpMethod.Post,
            $"{mockBaseUrl}/internal/admin/customers/{MockCustomerReference()}/ad/users",
            adminSessionToken);
        createUserRequest.Content = JsonContent.Create(new
        {
            samAccountName = "test.web.0042.readonly",
            displayName = "Read Only User"
        });
        using var createUserResponse = await client.SendAsync(createUserRequest);
        var createUserText =
            await createUserResponse.Content.ReadAsStringAsync();
        using var createUserPayload = JsonDocument.Parse(createUserText);
        Ensure(
            createUserResponse.StatusCode == HttpStatusCode.Forbidden
            && createUserPayload.RootElement.GetProperty("code").GetString()
                == "AD_READ_ONLY",
            "Les Ã©critures AD doivent Ãªtre refusÃ©es en mode read_only.");
        Ensure(
            !statusText.Contains(adPasswordSentinel, StringComparison.Ordinal)
            && !createUserText.Contains(
                adPasswordSentinel,
                StringComparison.Ordinal)
            && !SnapshotLogs(api.Logs).Contains(
                adPasswordSentinel,
                StringComparison.Ordinal),
            "Le secret du compte de service AD ne doit apparaÃ®tre ni dans les rÃ©ponses ni dans les logs.");
    }
    finally
    {
        await api.StopAsync();
    }
}

async Task<string> LoginAsAdminAsync(HttpClient client, string baseUrl)
{
    using var adminLoginResponse = await client.PostAsJsonAsync(
        $"{baseUrl}/internal/auth/sessions",
        new
        {
            email = mockAdminEmail,
            password = mockAdminPassword
        });
    using var adminLoginPayload = JsonDocument.Parse(
        await adminLoginResponse.Content.ReadAsStringAsync());
    Ensure(
        adminLoginResponse.StatusCode == HttpStatusCode.OK,
        "Le login internal_admin requis pour les tests AD doit rÃ©ussir.");

    return adminLoginPayload.RootElement
        .GetProperty("sessionToken")
        .GetString()
        ?? throw new InvalidOperationException(
            "Le login admin AD ne retourne aucun token interne.");
}

async Task VerifyDisabledActiveDirectoryAdminRoutesAsync(
    HttpClient client,
    string baseUrl,
    string adminSessionToken)
{
    using var statusRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/ad/status",
        adminSessionToken);
    using var statusResponse = await client.SendAsync(statusRequest);
    var statusText = await statusResponse.Content.ReadAsStringAsync();
    using var statusPayload = JsonDocument.Parse(statusText);
    Ensure(
        statusResponse.StatusCode == HttpStatusCode.OK
        && statusPayload.RootElement.GetProperty("mode").GetString()
            == "disabled"
        && statusPayload.RootElement.GetProperty("status").GetString()
            == "disabled"
        && !statusPayload.RootElement.GetProperty("readsEnabled").GetBoolean()
        && !statusPayload.RootElement.GetProperty("writesEnabled").GetBoolean(),
        "Le statut AD disabled exposÃ© Ã  l'admin est invalide.");
    Ensure(
        !statusText.Contains("password", StringComparison.OrdinalIgnoreCase)
        && !statusText.Contains("username", StringComparison.OrdinalIgnoreCase),
        "Le statut AD disabled ne doit exposer aucune information sensible.");

    using var searchUsersRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/ad/users?query=0042",
        adminSessionToken);
    using var searchUsersResponse = await client.SendAsync(searchUsersRequest);
    using var searchUsersPayload = JsonDocument.Parse(
        await searchUsersResponse.Content.ReadAsStringAsync());
    Ensure(
        searchUsersResponse.StatusCode == HttpStatusCode.NotImplemented
        && searchUsersPayload.RootElement.GetProperty("code").GetString()
            == "AD_INTEGRATION_DISABLED",
        "La recherche AD doit Ãªtre refusÃ©e en mode disabled.");

    const string logSentinel = "NOT_A_REAL_AD_SECRET_LOG_SENTINEL";
    using var createUserRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/{MockCustomerReference()}/ad/users",
        adminSessionToken);
    createUserRequest.Content = JsonContent.Create(new
    {
        samAccountName = "test.web.0042.disabled",
        displayName = logSentinel
    });
    using var createUserResponse = await client.SendAsync(createUserRequest);
    var createUserText =
        await createUserResponse.Content.ReadAsStringAsync();
    using var createUserPayload = JsonDocument.Parse(createUserText);
    Ensure(
        createUserResponse.StatusCode == HttpStatusCode.NotImplemented
        && createUserPayload.RootElement.GetProperty("code").GetString()
            == "AD_INTEGRATION_DISABLED",
        "Les Ã©critures AD doivent Ãªtre refusÃ©es en mode disabled.");
    Ensure(
        !createUserText.Contains(logSentinel, StringComparison.Ordinal),
        "Une rÃ©ponse AD refusÃ©e ne doit pas rejouer le payload d'entrÃ©e.");

    using var hardDeleteRequest = CreateSessionRequest(
        HttpMethod.Delete,
        $"{baseUrl}/internal/admin/customers/{MockCustomerReference()}/ad/users/test.web.0042.user",
        adminSessionToken);
    using var hardDeleteResponse = await client.SendAsync(hardDeleteRequest);
    Ensure(
        hardDeleteResponse.StatusCode == HttpStatusCode.NotFound
        || hardDeleteResponse.StatusCode == HttpStatusCode.MethodNotAllowed,
        "Aucune suppression dÃ©finitive AD ne doit Ãªtre exposÃ©e.");
}

async Task VerifyDisabledBpceAdminRoutesAsync(
    HttpClient client,
    string baseUrl,
    string adminSessionToken)
{
    using var statusRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/bpce/status",
        adminSessionToken);
    using var statusResponse = await client.SendAsync(statusRequest);
    var statusText = await statusResponse.Content.ReadAsStringAsync();
    using var statusPayload = JsonDocument.Parse(statusText);
    Ensure(
        statusResponse.StatusCode == HttpStatusCode.OK
        && statusPayload.RootElement.GetProperty("mode").GetString()
            == "disabled"
        && statusPayload.RootElement.GetProperty("status").GetString()
            == "disabled"
        && statusPayload.RootElement
            .GetProperty("configurationValid").GetBoolean()
        && !statusPayload.RootElement
            .GetProperty("senderConfigured").GetBoolean(),
        "Le statut BPCE disabled expose a l'admin est invalide.");
    Ensure(
        !statusText.Contains("refresh", StringComparison.OrdinalIgnoreCase)
        && !statusText.Contains("token", StringComparison.OrdinalIgnoreCase)
        && !statusText.Contains("bearer", StringComparison.OrdinalIgnoreCase),
        "Le statut BPCE disabled ne doit exposer aucun secret.");
}

async Task VerifyMockActiveDirectoryAdminRoutesAsync(
    HttpClient client,
    string baseUrl,
    string adminSessionToken)
{
    using var statusRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/ad/status",
        adminSessionToken);
    using var statusResponse = await client.SendAsync(statusRequest);
    using var statusPayload = JsonDocument.Parse(
        await statusResponse.Content.ReadAsStringAsync());
    Ensure(
        statusResponse.StatusCode == HttpStatusCode.OK
        && statusPayload.RootElement.GetProperty("mode").GetString()
            == "mock"
        && statusPayload.RootElement.GetProperty("status").GetString()
            == "mock"
        && statusPayload.RootElement.GetProperty("readsEnabled").GetBoolean()
        && statusPayload.RootElement.GetProperty("writesEnabled").GetBoolean(),
        "Le statut AD mock est invalide.");

    using var searchUsersRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/ad/users?query=0042&customerReference=CLI-DEMO-0042",
        adminSessionToken);
    using var searchUsersResponse = await client.SendAsync(searchUsersRequest);
    using var searchUsersPayload = JsonDocument.Parse(
        await searchUsersResponse.Content.ReadAsStringAsync());
    Ensure(
        searchUsersResponse.StatusCode == HttpStatusCode.OK
        && searchUsersPayload.RootElement.EnumerateArray().Any(item =>
            item.GetProperty("samAccountName").GetString()
                == "test.web.0042.user"
            && item.GetProperty("customerReference").GetString()
                == "CLI-DEMO-0042"),
        "La recherche des utilisateurs AD mock doit retourner les utilisateurs 0042.");

    using var searchGroupsRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/ad/groups?query=PORTAL&customerReference=CLI-DEMO-0042",
        adminSessionToken);
    using var searchGroupsResponse = await client.SendAsync(searchGroupsRequest);
    using var searchGroupsPayload = JsonDocument.Parse(
        await searchGroupsResponse.Content.ReadAsStringAsync());
    Ensure(
        searchGroupsResponse.StatusCode == HttpStatusCode.OK
        && searchGroupsPayload.RootElement.EnumerateArray().Any(item =>
            item.GetProperty("samAccountName").GetString()
                == "KERMARIA_CLI-DEMO-0042_PORTAL_USERS"
            && item.GetProperty("customerReference").GetString()
                == "CLI-DEMO-0042"),
        "La recherche des groupes AD mock doit retourner les groupes 0042.");
    var linkCandidateGroup = searchGroupsPayload.RootElement
        .EnumerateArray()
        .First(item =>
            item.GetProperty("samAccountName").GetString()
                == "KERMARIA_CLI-DEMO-0042_PORTAL_USERS");
    var linkCandidateGroupSam = linkCandidateGroup
        .GetProperty("samAccountName")
        .GetString()
        ?? throw new InvalidOperationException(
            "Le groupe AD mock candidat pour la liaison est introuvable.");
    var linkCandidateGroupDn = linkCandidateGroup
        .GetProperty("distinguishedName")
        .GetString()
        ?? throw new InvalidOperationException(
            "Le DN du groupe AD mock candidat pour la liaison est introuvable.");

    const string createdUserSamAccountName = "test.web.0042.v018";
    using var createUserRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/users",
        adminSessionToken);
    createUserRequest.Content = JsonContent.Create(new
    {
        samAccountName = createdUserSamAccountName,
        displayName = "Test Web 0042 V018",
        givenName = "Test",
        surname = "V018",
        description = "Compte de test V0.20"
    });
    using var createUserResponse = await client.SendAsync(createUserRequest);
    using var createUserPayload = JsonDocument.Parse(
        await createUserResponse.Content.ReadAsStringAsync());
    Ensure(
        createUserResponse.StatusCode == HttpStatusCode.Created
        && createUserPayload.RootElement.GetProperty("code").GetString()
            == "AD_USER_CREATED"
        && createUserPayload.RootElement.GetProperty("changed").GetBoolean(),
        "La crÃ©ation d'un utilisateur AD mock doit rÃ©ussir.");
    var createdUserDn = createUserPayload.RootElement
        .GetProperty("object")
        .GetProperty("distinguishedName")
        .GetString()
        ?? throw new InvalidOperationException(
            "La crÃ©ation AD mock de l'utilisateur ne retourne pas de DN.");
    Ensure(
        createdUserDn.Contains(
            "OU=Users,OU=CLI-DEMO-0042,OU=10_Customers,OU=TEST_SITE_WEB,DC=home,DC=bzh",
            StringComparison.Ordinal),
        "Le DN utilisateur mock doit rester bornÃ© Ã  l'OU autorisÃ©e.");

    const string createdGroupSamAccountName =
        "KERMARIA_CLI-DEMO-0042_V018_USERS";
    using var createGroupRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/groups",
        adminSessionToken);
    createGroupRequest.Content = JsonContent.Create(new
    {
        samAccountName = createdGroupSamAccountName,
        displayName = "Kermaria CLI-DEMO-0042 V018 Users",
        description = "Groupe de test V0.20"
    });
    using var createGroupResponse = await client.SendAsync(createGroupRequest);
    using var createGroupPayload = JsonDocument.Parse(
        await createGroupResponse.Content.ReadAsStringAsync());
    Ensure(
        createGroupResponse.StatusCode == HttpStatusCode.Created
        && createGroupPayload.RootElement.GetProperty("code").GetString()
            == "AD_GROUP_CREATED"
        && createGroupPayload.RootElement.GetProperty("changed").GetBoolean(),
        "La crÃ©ation d'un groupe AD mock doit rÃ©ussir.");
    var createdGroupDn = createGroupPayload.RootElement
        .GetProperty("object")
        .GetProperty("distinguishedName")
        .GetString()
        ?? throw new InvalidOperationException(
            "La crÃ©ation AD mock du groupe ne retourne pas de DN.");

    using var addMemberRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/groups/{createdGroupSamAccountName}/members",
        adminSessionToken);
    addMemberRequest.Content = JsonContent.Create(new
    {
        userSamAccountName = createdUserSamAccountName
    });
    using var addMemberResponse = await client.SendAsync(addMemberRequest);
    using var addMemberPayload = JsonDocument.Parse(
        await addMemberResponse.Content.ReadAsStringAsync());
    Ensure(
        addMemberResponse.StatusCode == HttpStatusCode.OK
        && addMemberPayload.RootElement.GetProperty("code").GetString()
            == "AD_GROUP_MEMBER_ADDED"
        && addMemberPayload.RootElement.GetProperty("changed").GetBoolean(),
        "L'ajout d'un membre AD mock doit rÃ©ussir.");

    using var addMemberAgainRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/groups/{createdGroupSamAccountName}/members",
        adminSessionToken);
    addMemberAgainRequest.Content = JsonContent.Create(new
    {
        userSamAccountName = createdUserSamAccountName
    });
    using var addMemberAgainResponse = await client.SendAsync(
        addMemberAgainRequest);
    using var addMemberAgainPayload = JsonDocument.Parse(
        await addMemberAgainResponse.Content.ReadAsStringAsync());
    Ensure(
        addMemberAgainResponse.StatusCode == HttpStatusCode.OK
        && addMemberAgainPayload.RootElement.GetProperty("code").GetString()
            == "AD_GROUP_MEMBER_ALREADY_PRESENT"
        && !addMemberAgainPayload.RootElement.GetProperty("changed")
            .GetBoolean(),
        "Ajouter un membre deja present doit repondre unchanged.");

    using var removeMemberRequest = CreateSessionRequest(
        HttpMethod.Delete,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/groups/{createdGroupSamAccountName}/members/{createdUserSamAccountName}",
        adminSessionToken);
    using var removeMemberResponse = await client.SendAsync(removeMemberRequest);
    using var removeMemberPayload = JsonDocument.Parse(
        await removeMemberResponse.Content.ReadAsStringAsync());
    Ensure(
        removeMemberResponse.StatusCode == HttpStatusCode.OK
        && removeMemberPayload.RootElement.GetProperty("code").GetString()
            == "AD_GROUP_MEMBER_REMOVED"
        && removeMemberPayload.RootElement.GetProperty("changed").GetBoolean(),
        "Le retrait d'un membre AD mock doit rÃ©ussir.");

    using var removeMemberAgainRequest = CreateSessionRequest(
        HttpMethod.Delete,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/groups/{createdGroupSamAccountName}/members/{createdUserSamAccountName}",
        adminSessionToken);
    using var removeMemberAgainResponse = await client.SendAsync(
        removeMemberAgainRequest);
    using var removeMemberAgainPayload = JsonDocument.Parse(
        await removeMemberAgainResponse.Content.ReadAsStringAsync());
    Ensure(
        removeMemberAgainResponse.StatusCode == HttpStatusCode.OK
        && removeMemberAgainPayload.RootElement.GetProperty("code")
            .GetString() == "AD_GROUP_MEMBER_ALREADY_ABSENT"
        && !removeMemberAgainPayload.RootElement.GetProperty("changed")
            .GetBoolean(),
        "Retirer un membre absent doit repondre unchanged.");

    using var disableUserRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/users/{createdUserSamAccountName}/disable",
        adminSessionToken);
    using var disableUserResponse = await client.SendAsync(disableUserRequest);
    using var disableUserPayload = JsonDocument.Parse(
        await disableUserResponse.Content.ReadAsStringAsync());
    Ensure(
        disableUserResponse.StatusCode == HttpStatusCode.OK
        && disableUserPayload.RootElement.GetProperty("code").GetString()
            == "AD_USER_ALREADY_DISABLED",
        "La dÃ©sactivation AD mock doit rÃ©pondre proprement pour un compte dÃ©jÃ  dÃ©sactivÃ©.");

    using var moveUserRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/users/{createdUserSamAccountName}/move-to-disabled",
        adminSessionToken);
    using var moveUserResponse = await client.SendAsync(moveUserRequest);
    using var moveUserPayload = JsonDocument.Parse(
        await moveUserResponse.Content.ReadAsStringAsync());
    Ensure(
        moveUserResponse.StatusCode == HttpStatusCode.OK
        && moveUserPayload.RootElement.GetProperty("code").GetString()
            == "AD_USER_MOVED_TO_DISABLED"
        && moveUserPayload.RootElement.GetProperty("changed").GetBoolean()
        && moveUserPayload.RootElement
            .GetProperty("object")
            .GetProperty("distinguishedName")
            .GetString()!
            .Contains(
                "OU=Disabled,OU=CLI-DEMO-0042,OU=10_Customers,OU=TEST_SITE_WEB,DC=home,DC=bzh",
                StringComparison.Ordinal),
        "Le dÃ©placement mock vers l'OU Disabled doit rester dans l'OU autorisÃ©e.");

    using var createLinkRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad-links",
        adminSessionToken);
    createLinkRequest.Content = JsonContent.Create(new
    {
        distinguishedName = linkCandidateGroupDn
    });
    using var createLinkResponse = await client.SendAsync(createLinkRequest);
    using var createLinkPayload = JsonDocument.Parse(
        await createLinkResponse.Content.ReadAsStringAsync());
    Ensure(
        createLinkResponse.StatusCode == HttpStatusCode.Created
        && createLinkPayload.RootElement.GetProperty("code").GetString()
            == "AD_LINK_CREATED"
        && createLinkPayload.RootElement.GetProperty("changed").GetBoolean(),
        "La crÃ©ation d'un lien AD client doit rÃ©ussir.");
    var createdLinkId = createLinkPayload.RootElement
        .GetProperty("id")
        .GetString()
        ?? throw new InvalidOperationException(
            "La liaison AD mock ne retourne pas d'identifiant.");

    using var listLinksRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad-links",
        adminSessionToken);
    using var listLinksResponse = await client.SendAsync(listLinksRequest);
    using var listLinksPayload = JsonDocument.Parse(
        await listLinksResponse.Content.ReadAsStringAsync());
    Ensure(
        listLinksResponse.StatusCode == HttpStatusCode.OK
        && listLinksPayload.RootElement.EnumerateArray().Any(item =>
            item.GetProperty("id").GetString() == createdLinkId
            && item.GetProperty("samAccountName").GetString()
                == linkCandidateGroupSam),
        "La liste des liaisons AD doit reflÃ©ter l'objet liÃ©.");

    using var deleteLinkRequest = CreateSessionRequest(
        HttpMethod.Delete,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad-links/{createdLinkId}",
        adminSessionToken);
    using var deleteLinkResponse = await client.SendAsync(deleteLinkRequest);
    using var deleteLinkPayload = JsonDocument.Parse(
        await deleteLinkResponse.Content.ReadAsStringAsync());
    Ensure(
        deleteLinkResponse.StatusCode == HttpStatusCode.OK
        && deleteLinkPayload.RootElement.GetProperty("code").GetString()
            == "AD_LINK_DELETED",
        "La suppression d'un lien AD doit rÃ©ussir.");

    using var invalidDnLinkRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad-links",
        adminSessionToken);
    invalidDnLinkRequest.Content = JsonContent.Create(new
    {
        distinguishedName = "CN=forbidden,CN=Users,DC=home,DC=bzh"
    });
    using var invalidDnLinkResponse = await client.SendAsync(invalidDnLinkRequest);
    using var invalidDnLinkPayload = JsonDocument.Parse(
        await invalidDnLinkResponse.Content.ReadAsStringAsync());
    Ensure(
        invalidDnLinkResponse.StatusCode == HttpStatusCode.Forbidden
        && invalidDnLinkPayload.RootElement.GetProperty("code").GetString()
            == "AD_TARGET_OUTSIDE_ALLOWED_OU",
        "Un DN hors de l'OU autorisÃ©e doit Ãªtre refusÃ©.");

    using var invalidSamCreateUserRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/users",
        adminSessionToken);
    invalidSamCreateUserRequest.Content = JsonContent.Create(new
    {
        samAccountName = "sam invalide",
        displayName = "Invalid Sam"
    });
    using var invalidSamCreateUserResponse = await client.SendAsync(
        invalidSamCreateUserRequest);
    using var invalidSamCreateUserPayload = JsonDocument.Parse(
        await invalidSamCreateUserResponse.Content.ReadAsStringAsync());
    Ensure(
        invalidSamCreateUserResponse.StatusCode == HttpStatusCode.BadRequest
        && invalidSamCreateUserPayload.RootElement.GetProperty("code")
            .GetString() == "INVALID_REQUEST",
        "Un sAMAccountName invalide doit etre refuse.");

    using var invalidUpnCreateUserRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/users",
        adminSessionToken);
    invalidUpnCreateUserRequest.Content = JsonContent.Create(new
    {
        samAccountName = "test.web.0042.invalidupn",
        displayName = "Invalid Upn",
        userPrincipalName = "bad upn@example.invalid"
    });
    using var invalidUpnCreateUserResponse = await client.SendAsync(
        invalidUpnCreateUserRequest);
    using var invalidUpnCreateUserPayload = JsonDocument.Parse(
        await invalidUpnCreateUserResponse.Content.ReadAsStringAsync());
    Ensure(
        invalidUpnCreateUserResponse.StatusCode == HttpStatusCode.BadRequest
        && invalidUpnCreateUserPayload.RootElement.GetProperty("code")
            .GetString() == "INVALID_REQUEST",
        "Un userPrincipalName invalide doit etre refuse.");

    using var missingCustomerRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-9999/ad/users",
        adminSessionToken);
    missingCustomerRequest.Content = JsonContent.Create(new
    {
        samAccountName = "test.web.9999.user",
        displayName = "Missing Customer"
    });
    using var missingCustomerResponse = await client.SendAsync(
        missingCustomerRequest);
    Ensure(
        missingCustomerResponse.StatusCode == HttpStatusCode.NotFound,
        "Un client inexistant doit Ãªtre refusÃ© pour les actions AD.");

    using var crossCustomerRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0100/ad/groups/KERMARIA_CLI-DEMO-0100_PORTAL_USERS/members",
        adminSessionToken);
    crossCustomerRequest.Content = JsonContent.Create(new
    {
        userSamAccountName = "test.web.0042.user"
    });
    using var crossCustomerResponse = await client.SendAsync(
        crossCustomerRequest);
    using var crossCustomerPayload = JsonDocument.Parse(
        await crossCustomerResponse.Content.ReadAsStringAsync());
    Ensure(
        crossCustomerResponse.StatusCode == HttpStatusCode.Forbidden
        && crossCustomerPayload.RootElement.GetProperty("code").GetString()
            == "AD_CROSS_CUSTOMER_FORBIDDEN",
        "L'ajout d'un utilisateur 0042 dans un groupe 0100 doit Ãªtre refusÃ©.");

    using var hardDeleteRequest = CreateSessionRequest(
        HttpMethod.Delete,
        $"{baseUrl}/internal/admin/customers/CLI-DEMO-0042/ad/users/{createdUserSamAccountName}",
        adminSessionToken);
    using var hardDeleteResponse = await client.SendAsync(hardDeleteRequest);
    Ensure(
        hardDeleteResponse.StatusCode == HttpStatusCode.NotFound
        || hardDeleteResponse.StatusCode == HttpStatusCode.MethodNotAllowed,
        "Aucune suppression dÃ©finitive AD ne doit Ãªtre exposÃ©e.");
}

async Task<string> FindRequestIdAsync(
    HttpClient client,
    string baseUrl,
    string path,
    string sessionToken,
    string reference)
{
    using var request = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}{path}",
        sessionToken);
    using var response = await client.SendAsync(request);
    using var payload = JsonDocument.Parse(
        await response.Content.ReadAsStringAsync());
    return payload.RootElement
        .EnumerateArray()
        .First(item =>
            item.GetProperty("reference").GetString() == reference)
        .GetProperty("id")
        .GetString()
        ?? throw new InvalidOperationException(
            "La demande MariaDB créée ne retourne aucun identifiant.");
}

async Task VerifyWorkflowPersistenceAsync(
    string supportRequestId,
    string serviceRequestId)
{
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText =
        """
        SELECT
            (SELECT COUNT(*) FROM request_events
             WHERE request_type = 'support' AND request_id = @support_id)
                AS support_event_count,
            (SELECT COUNT(*) FROM request_events
             WHERE request_type = 'service' AND request_id = @service_id)
                AS service_event_count,
            (SELECT COUNT(*) FROM request_internal_notes
             WHERE request_type = 'support' AND request_id = @support_id)
                AS note_count,
            (SELECT COUNT(*) FROM request_public_messages
             WHERE request_type = 'support' AND request_id = @support_id)
                AS support_message_count,
            (SELECT COUNT(*) FROM request_public_messages
             WHERE request_type = 'service' AND request_id = @service_id)
                AS service_message_count,
            (SELECT COUNT(*) FROM portal_notifications
             WHERE request_type = 'support' AND request_id = @support_id)
                AS support_notification_count,
            (SELECT COUNT(*) FROM portal_notifications
             WHERE request_type = 'service' AND request_id = @service_id)
                AS service_notification_count;
        """;
    AddDbParameter(command, "@support_id", supportRequestId);
    AddDbParameter(command, "@service_id", serviceRequestId);
    await using var reader = await command.ExecuteReaderAsync();
    Ensure(await reader.ReadAsync(), "Le workflow MariaDB est illisible.");
    Ensure(
        Convert.ToInt32(reader["support_event_count"]) >= 3
        && Convert.ToInt32(reader["service_event_count"]) >= 3
        && Convert.ToInt32(reader["note_count"]) == 1
        && Convert.ToInt32(reader["support_message_count"]) == 2
        && Convert.ToInt32(reader["service_message_count"]) == 2
        && Convert.ToInt32(reader["support_notification_count"]) == 2
        && Convert.ToInt32(reader["service_notification_count"]) == 2,
        "Les événements ou notes du workflow MariaDB sont incomplets.");
}

async Task CleanupWorkflowFixtureAsync(
    string? supportRequestId,
    string? serviceRequestId)
{
    if (supportRequestId is null && serviceRequestId is null)
    {
        return;
    }

    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    foreach (var table in new[]
    {
        "portal_notifications",
        "request_internal_notes",
        "request_public_messages",
        "request_events"
    })
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"DELETE FROM {table} WHERE request_id IN (@support_id, @service_id);";
        AddDbParameter(command, "@support_id", supportRequestId ?? string.Empty);
        AddDbParameter(command, "@service_id", serviceRequestId ?? string.Empty);
        await command.ExecuteNonQueryAsync();
    }

    if (supportRequestId is not null)
    {
        await using var supportCommand = connection.CreateCommand();
        supportCommand.Transaction = transaction;
        supportCommand.CommandText =
            "DELETE FROM support_requests WHERE id = @id;";
        AddDbParameter(supportCommand, "@id", supportRequestId);
        await supportCommand.ExecuteNonQueryAsync();
    }

    if (serviceRequestId is not null)
    {
        await using (var commercialLinesCommand = connection.CreateCommand())
        {
            commercialLinesCommand.Transaction = transaction;
            commercialLinesCommand.CommandText =
                """
                DELETE line
                FROM commercial_document_lines line
                INNER JOIN commercial_documents document
                    ON document.id = line.document_id
                WHERE document.service_request_id = @service_id;
                """;
            AddDbParameter(commercialLinesCommand, "@service_id", serviceRequestId);
            await commercialLinesCommand.ExecuteNonQueryAsync();
        }

        await using (var commercialDocumentsCommand = connection.CreateCommand())
        {
            commercialDocumentsCommand.Transaction = transaction;
            commercialDocumentsCommand.CommandText =
                "DELETE FROM commercial_documents WHERE service_request_id = @service_id;";
            AddDbParameter(commercialDocumentsCommand, "@service_id", serviceRequestId);
            await commercialDocumentsCommand.ExecuteNonQueryAsync();
        }

        await using var serviceCommand = connection.CreateCommand();
        serviceCommand.Transaction = transaction;
        serviceCommand.CommandText =
            "DELETE FROM service_requests WHERE id = @id;";
        AddDbParameter(serviceCommand, "@id", serviceRequestId);
        await serviceCommand.ExecuteNonQueryAsync();
    }

    await transaction.CommitAsync();
}
async Task VerifyPersistedSessionHashAsync(string sessionToken)
{
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText =
        """
        SELECT session_token_hash
        FROM portal_sessions
        ORDER BY created_at DESC
        LIMIT 1;
        """;
    var storedHash = Convert.ToString(await command.ExecuteScalarAsync());
    var expectedHash = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(sessionToken)))
        .ToLowerInvariant();

    Ensure(
        !string.IsNullOrWhiteSpace(storedHash),
        "Le hash de session MariaDB est absent.");
    Ensure(
        !string.Equals(storedHash, sessionToken, StringComparison.Ordinal),
        "Le token brut ne doit jamais être stocké dans MariaDB.");
    Ensure(
        string.Equals(storedHash, expectedHash, StringComparison.Ordinal),
        "Le hash de session MariaDB ne correspond pas au token créé.");
}

async Task VerifyNotificationMigrationAsync()
{
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText =
        """
        SELECT COUNT(*)
        FROM schema_migrations
        WHERE migration_id = '005_portal_notifications';
        """;
    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
    Ensure(
        count == 1,
        "La migration 005_portal_notifications doit être appliquée avant les tests opt-in.");
}

async Task VerifyCommercialMigrationAsync()
{
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText =
        """
        SELECT COUNT(*)
        FROM schema_migrations
        WHERE migration_id = '006_commercial_foundation';
        """;
    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
    Ensure(
        count == 1,
        "La migration 006_commercial_foundation doit être appliquée avant les tests opt-in.");
}

async Task VerifyCommercialFoundationAsync(
    HttpClient client,
    string baseUrl,
    string clientSessionToken,
    string adminSessionToken,
    string customerReference,
    string? serviceRequestId,
    bool persistent,
    string? foreignCustomerId = null)
{
    var expectedDataSource = persistent ? "mariadb" : "mock";

    using var clientCatalogRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/portal/catalog",
        clientSessionToken);
    using var clientCatalogResponse = await client.SendAsync(
        clientCatalogRequest);
    using var clientCatalogPayload = JsonDocument.Parse(
        await clientCatalogResponse.Content.ReadAsStringAsync());
    Ensure(
        clientCatalogResponse.StatusCode == HttpStatusCode.OK
        && clientCatalogResponse.Headers.GetValues(dataSourceHeader).Single()
            == expectedDataSource
        && clientCatalogPayload.RootElement.GetArrayLength() >= 1,
        "Le catalogue commercial client doit être accessible.");

    using var adminCatalogRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/catalog",
        adminSessionToken);
    using var adminCatalogResponse = await client.SendAsync(
        adminCatalogRequest);
    using var adminCatalogPayload = JsonDocument.Parse(
        await adminCatalogResponse.Content.ReadAsStringAsync());
    Ensure(
        adminCatalogResponse.StatusCode == HttpStatusCode.OK
        && adminCatalogResponse.Headers.GetValues(dataSourceHeader).Single()
            == expectedDataSource
        && adminCatalogPayload.RootElement.GetArrayLength() >= 1,
        "Le catalogue commercial admin doit être accessible.");

    using var clientDocumentsRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/portal/commercial-documents",
        clientSessionToken);
    using var clientDocumentsResponse = await client.SendAsync(
        clientDocumentsRequest);
    using var initialClientDocumentsPayload = JsonDocument.Parse(
        await clientDocumentsResponse.Content.ReadAsStringAsync());
    Ensure(
        clientDocumentsResponse.StatusCode == HttpStatusCode.OK
        && clientDocumentsResponse.Headers.GetValues(dataSourceHeader).Single()
            == expectedDataSource,
        "La liste client des documents commerciaux doit être accessible.");
    var initialSharedDocumentCount =
        initialClientDocumentsPayload.RootElement.GetArrayLength();

    using var adminDocumentsRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/commercial-documents",
        adminSessionToken);
    using var adminDocumentsResponse = await client.SendAsync(
        adminDocumentsRequest);
    var adminDocumentsBody =
        await adminDocumentsResponse.Content.ReadAsStringAsync();
    var adminDocumentsDataSource =
        adminDocumentsResponse.Headers.TryGetValues(
            dataSourceHeader,
            out var adminDocumentsDataSourceValues)
            ? adminDocumentsDataSourceValues.SingleOrDefault() ?? "<missing>"
            : "<missing>";
    var adminDocumentsDebugExceptionType =
        adminDocumentsResponse.Headers.TryGetValues(
            "X-Debug-Exception-Type",
            out var adminDocumentsDebugExceptionTypeValues)
            ? adminDocumentsDebugExceptionTypeValues.SingleOrDefault()
                ?? "<missing>"
            : "<missing>";
    var adminDocumentsDebugExceptionMessage =
        adminDocumentsResponse.Headers.TryGetValues(
            "X-Debug-Exception-Message",
            out var adminDocumentsDebugExceptionMessageValues)
            ? adminDocumentsDebugExceptionMessageValues.SingleOrDefault()
                ?? "<missing>"
            : "<missing>";
    var adminDocumentsDebugCorrelationId =
        adminDocumentsResponse.Headers.TryGetValues(
            "X-Debug-Correlation-Id",
            out var adminDocumentsDebugCorrelationIdValues)
            ? adminDocumentsDebugCorrelationIdValues.SingleOrDefault()
                ?? "<missing>"
            : "<missing>";
    string adminDocumentsValueKind;
    var adminDocumentsCount = -1;
    try
    {
        using var adminDocumentsPayload = JsonDocument.Parse(adminDocumentsBody);
        adminDocumentsValueKind =
            adminDocumentsPayload.RootElement.ValueKind.ToString();
        if (adminDocumentsPayload.RootElement.ValueKind == JsonValueKind.Array)
        {
            adminDocumentsCount =
                adminDocumentsPayload.RootElement.GetArrayLength();
        }
    }
    catch (JsonException)
    {
        adminDocumentsValueKind = "<invalid-json>";
    }
    if (!(adminDocumentsResponse.StatusCode == HttpStatusCode.OK
        && adminDocumentsDataSource == expectedDataSource
        && adminDocumentsCount >= 1))
    {
        throw new InvalidOperationException(
            "La liste admin des documents commerciaux doit être accessible. "
            + $"Status={adminDocumentsResponse.StatusCode}; "
            + $"DataSource={adminDocumentsDataSource}; "
            + $"ValueKind={adminDocumentsValueKind}; "
            + $"Count={adminDocumentsCount}; "
            + $"Body={adminDocumentsBody.Replace('\r', ' ').Replace('\n', ' ').Trim()}; "
            + $"DebugExceptionType={adminDocumentsDebugExceptionType}; "
            + $"DebugExceptionMessage={adminDocumentsDebugExceptionMessage}; "
            + $"DebugCorrelationId={adminDocumentsDebugCorrelationId}");
    }
    Ensure(
        adminDocumentsResponse.StatusCode == HttpStatusCode.OK
        && adminDocumentsDataSource == expectedDataSource
        && adminDocumentsCount >= 1,
        "La liste admin des documents commerciaux doit être accessible.");

    using var forbiddenCreateRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/commercial-documents",
        clientSessionToken);
    forbiddenCreateRequest.Content = JsonContent.Create(new
    {
        customerReference,
        documentType = "quote_draft",
        title = "Tentative client interdite",
        currency = "EUR",
        serviceRequestId,
        disclaimer = "Document informatif — ne constitue pas une facture officielle.",
        status = "draft"
    });
    using var forbiddenCreateResponse = await client.SendAsync(
        forbiddenCreateRequest);
    Ensure(
        forbiddenCreateResponse.StatusCode == HttpStatusCode.Forbidden,
        "Un client ne doit pas pouvoir créer un document commercial.");

    using var createOfferRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/catalog",
        adminSessionToken);
    createOfferRequest.Content = JsonContent.Create(new
    {
        name = "Offre test V0.15",
        description = "Offre informative créée par les smoke tests.",
        category = "Tests",
        unitLabel = "forfait",
        priceAmountCents = 12345,
        status = "active",
        displayOrder = 999
    });
    using var createOfferResponse = await client.SendAsync(createOfferRequest);
    using var createOfferPayload = JsonDocument.Parse(
        await createOfferResponse.Content.ReadAsStringAsync());
    Ensure(
        createOfferResponse.StatusCode == HttpStatusCode.OK
        && createOfferPayload.RootElement.GetProperty("changed").GetBoolean(),
        "L'admin doit pouvoir créer une offre commerciale.");
    var createdOfferId = createOfferPayload.RootElement
        .GetProperty("id")
        .GetString()
        ?? throw new InvalidOperationException(
            "La création d'offre commerciale ne retourne aucun identifiant.");

    using var updateOfferRequest = CreateSessionRequest(
        HttpMethod.Patch,
        $"{baseUrl}/internal/admin/catalog/{createdOfferId}",
        adminSessionToken);
    updateOfferRequest.Content = JsonContent.Create(new
    {
        name = "Offre test V0.15 modifiée",
        description = "Offre informative modifiée par les smoke tests.",
        category = "Tests",
        unitLabel = "heure",
        priceAmountCents = 13000,
        status = "inactive",
        displayOrder = 1001
    });
    using var updateOfferResponse = await client.SendAsync(updateOfferRequest);
    using var updateOfferPayload = JsonDocument.Parse(
        await updateOfferResponse.Content.ReadAsStringAsync());
    Ensure(
        updateOfferResponse.StatusCode == HttpStatusCode.OK
        && updateOfferPayload.RootElement.GetProperty("status").GetString()
            == "inactive",
        "L'admin doit pouvoir modifier une offre commerciale.");

    using var createDocumentRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/commercial-documents",
        adminSessionToken);
    createDocumentRequest.Content = JsonContent.Create(new
    {
        customerReference,
        documentType = "quote_draft",
        title = "Brouillon commercial V0.15",
        currency = "EUR",
        serviceRequestId,
        disclaimer = "Document informatif — ne constitue pas une facture officielle.",
        status = "draft"
    });
    using var createDocumentResponse = await client.SendAsync(
        createDocumentRequest);
    using var createDocumentPayload = JsonDocument.Parse(
        await createDocumentResponse.Content.ReadAsStringAsync());
    Ensure(
        createDocumentResponse.StatusCode == HttpStatusCode.OK
        && createDocumentPayload.RootElement.GetProperty("status").GetString()
            == "draft",
        "L'admin doit pouvoir créer un document commercial brouillon.");
    var createdDocumentId = createDocumentPayload.RootElement
        .GetProperty("id")
        .GetString()
        ?? throw new InvalidOperationException(
            "La création du document commercial ne retourne aucun identifiant.");

    using var preShareClientDetailRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/portal/commercial-documents/{createdDocumentId}",
        clientSessionToken);
    using var preShareClientDetailResponse = await client.SendAsync(
        preShareClientDetailRequest);
    Ensure(
        preShareClientDetailResponse.StatusCode == HttpStatusCode.NotFound,
        "Un document non partagé ne doit pas être visible côté client.");

    using var invalidLineRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/commercial-documents/{createdDocumentId}/lines",
        adminSessionToken);
    invalidLineRequest.Content = JsonContent.Create(new
    {
        label = "Ligne invalide",
        description = "Cette ligne doit être refusée.",
        quantity = 1,
        unitLabel = "forfait",
        unitPriceCents = -10,
        sortOrder = 10
    });
    using var invalidLineResponse = await client.SendAsync(invalidLineRequest);
    Ensure(
        invalidLineResponse.StatusCode == HttpStatusCode.BadRequest,
        "Les montants invalides doivent être refusés.");

    using var addLineRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/commercial-documents/{createdDocumentId}/lines",
        adminSessionToken);
    addLineRequest.Content = JsonContent.Create(new
    {
        offerId = createdOfferId,
        description = "Ligne informative ajoutée par les smoke tests.",
        quantity = 2,
        taxRateBasisPoints = 2000,
        sortOrder = 10
    });
    using var addLineResponse = await client.SendAsync(addLineRequest);
    using var addLinePayload = JsonDocument.Parse(
        await addLineResponse.Content.ReadAsStringAsync());
    Ensure(
        addLineResponse.StatusCode == HttpStatusCode.OK
        && addLinePayload.RootElement.GetProperty("changed").GetBoolean(),
        "L'admin doit pouvoir ajouter une ligne de document.");
    var createdLineId = addLinePayload.RootElement
        .GetProperty("id")
        .GetString()
        ?? throw new InvalidOperationException(
            "La création de ligne commerciale ne retourne aucun identifiant.");

    using var updateLineRequest = CreateSessionRequest(
        HttpMethod.Patch,
        $"{baseUrl}/internal/admin/commercial-documents/{createdDocumentId}/lines/{createdLineId}",
        adminSessionToken);
    updateLineRequest.Content = JsonContent.Create(new
    {
        offerId = createdOfferId,
        description = "Ligne informative modifiée par les smoke tests.",
        quantity = 3,
        taxRateBasisPoints = 2000,
        sortOrder = 20
    });
    using var updateLineResponse = await client.SendAsync(updateLineRequest);
    using var updateLinePayload = JsonDocument.Parse(
        await updateLineResponse.Content.ReadAsStringAsync());
    Ensure(
        updateLineResponse.StatusCode == HttpStatusCode.OK
        && updateLinePayload.RootElement.GetProperty("changed").GetBoolean(),
        "L'admin doit pouvoir modifier une ligne de document.");

    using var invalidStatusRequest = CreateSessionRequest(
        HttpMethod.Patch,
        $"{baseUrl}/internal/admin/commercial-documents/{createdDocumentId}",
        adminSessionToken);
    invalidStatusRequest.Content = JsonContent.Create(new
    {
        customerReference,
        documentType = "quote_draft",
        title = "Brouillon commercial V0.15",
        currency = "EUR",
        serviceRequestId,
        disclaimer = "Document informatif — ne constitue pas une facture officielle.",
        status = "shared_with_customer"
    });
    using var invalidStatusResponse = await client.SendAsync(
        invalidStatusRequest);
    Ensure(
        invalidStatusResponse.StatusCode == HttpStatusCode.BadRequest,
        "Un statut commercial invalide doit être refusé.");

    using var updateDocumentRequest = CreateSessionRequest(
        HttpMethod.Patch,
        $"{baseUrl}/internal/admin/commercial-documents/{createdDocumentId}",
        adminSessionToken);
    updateDocumentRequest.Content = JsonContent.Create(new
    {
        customerReference,
        documentType = "quote_draft",
        title = "Brouillon commercial V0.15 à vérifier",
        currency = "EUR",
        serviceRequestId,
        disclaimer = "Document informatif — ne constitue pas une facture officielle.",
        status = "pending_review"
    });
    using var updateDocumentResponse = await client.SendAsync(
        updateDocumentRequest);
    using var updateDocumentPayload = JsonDocument.Parse(
        await updateDocumentResponse.Content.ReadAsStringAsync());
    Ensure(
        updateDocumentResponse.StatusCode == HttpStatusCode.OK
        && updateDocumentPayload.RootElement.GetProperty("status").GetString()
            == "pending_review",
        "L'admin doit pouvoir mettre un document commercial en attente de vérification.");

    using var shareRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/commercial-documents/{createdDocumentId}/share",
        adminSessionToken);
    using var shareResponse = await client.SendAsync(shareRequest);
    using var sharePayload = JsonDocument.Parse(
        await shareResponse.Content.ReadAsStringAsync());
    Ensure(
        shareResponse.StatusCode == HttpStatusCode.OK
        && sharePayload.RootElement.GetProperty("status").GetString()
            == "shared_with_customer",
        "L'admin doit pouvoir partager un document commercial au client.");

    using var sharedClientListRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/portal/commercial-documents",
        clientSessionToken);
    using var sharedClientListResponse = await client.SendAsync(
        sharedClientListRequest);
    using var sharedClientListPayload = JsonDocument.Parse(
        await sharedClientListResponse.Content.ReadAsStringAsync());
    Ensure(
        sharedClientListResponse.StatusCode == HttpStatusCode.OK
        && sharedClientListPayload.RootElement.GetArrayLength()
            >= initialSharedDocumentCount + 1
        && sharedClientListPayload.RootElement
            .EnumerateArray()
            .Any(item => item.GetProperty("id").GetString() == createdDocumentId),
        "Le document partagé doit devenir visible côté client.");

    using var clientDetailRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/portal/commercial-documents/{createdDocumentId}",
        clientSessionToken);
    using var clientDetailResponse = await client.SendAsync(clientDetailRequest);
    var clientDetailText = await clientDetailResponse.Content.ReadAsStringAsync();
    using var clientDetailPayload = JsonDocument.Parse(clientDetailText);
    Ensure(
        clientDetailResponse.StatusCode == HttpStatusCode.OK
        && clientDetailPayload.RootElement.GetProperty("status").GetString()
            == "shared_with_customer"
        && clientDetailPayload.RootElement.GetProperty("lines").GetArrayLength()
            == 1
        && clientDetailPayload.RootElement.GetProperty("disclaimer").GetString()
            == "Document informatif — ne constitue pas une facture officielle."
        && clientDetailPayload.RootElement.GetProperty("totalAmountCents")
            .GetInt32() > 0,
        "Le détail client du document partagé doit être cohérent.");
    if (serviceRequestId is not null)
    {
        Ensure(
            clientDetailPayload.RootElement.GetProperty("serviceRequestId")
                .GetString() == serviceRequestId,
            "Le document commercial partagé doit conserver la demande liée.");
    }
    Ensure(
        !clientDetailText.Contains("PayPal", StringComparison.OrdinalIgnoreCase)
        && !clientDetailText.Contains("Stripe", StringComparison.OrdinalIgnoreCase),
        "Aucune fonctionnalité de paiement ne doit être exposée.");

    if (persistent && foreignCustomerId is not null)
    {
        var foreignDocumentId = await InsertForeignCommercialDocumentAsync(
            foreignCustomerId);
        try
        {
            using var foreignDetailRequest = CreateSessionRequest(
                HttpMethod.Get,
                $"{baseUrl}/internal/portal/commercial-documents/{foreignDocumentId}",
                clientSessionToken);
            using var foreignDetailResponse = await client.SendAsync(
                foreignDetailRequest);
            Ensure(
                foreignDetailResponse.StatusCode == HttpStatusCode.NotFound,
                "Un client ne doit jamais voir le document commercial d'un autre client.");
        }
        finally
        {
            await DeleteCommercialDocumentAsync(foreignDocumentId);
        }
    }

    using var cancelRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/commercial-documents/{createdDocumentId}/cancel",
        adminSessionToken);
    using var cancelResponse = await client.SendAsync(cancelRequest);
    using var cancelPayload = JsonDocument.Parse(
        await cancelResponse.Content.ReadAsStringAsync());
    Ensure(
        cancelResponse.StatusCode == HttpStatusCode.OK
        && cancelPayload.RootElement.GetProperty("status").GetString()
            == "cancelled",
        "L'annulation d'un document commercial doit être possible.");

    using var cancelledClientDetailRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/portal/commercial-documents/{createdDocumentId}",
        clientSessionToken);
    using var cancelledClientDetailResponse = await client.SendAsync(
        cancelledClientDetailRequest);
    using var cancelledClientDetailPayload = JsonDocument.Parse(
        await cancelledClientDetailResponse.Content.ReadAsStringAsync());
    Ensure(
        cancelledClientDetailResponse.StatusCode == HttpStatusCode.OK
        && cancelledClientDetailPayload.RootElement.GetProperty("status")
            .GetString() == "cancelled",
        "Un document annulé déjà partagé doit rester lisible côté client.");

    using var adminDetailRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/commercial-documents/{createdDocumentId}",
        adminSessionToken);
    using var adminDetailResponse = await client.SendAsync(adminDetailRequest);
    using var adminDetailPayload = JsonDocument.Parse(
        await adminDetailResponse.Content.ReadAsStringAsync());
    Ensure(
        adminDetailResponse.StatusCode == HttpStatusCode.OK
        && adminDetailPayload.RootElement.GetProperty("status").GetString()
            == "cancelled",
        "Le document annulé doit rester lisible côté admin.");
}

async Task<string> InsertForeignCommercialDocumentAsync(string customerId)
{
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    var adminUserId = await FindInternalAdminUserIdAsync();
    var id = Guid.NewGuid().ToString("D");
    var reference = $"COM-ISO-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
    var now = DateTime.UtcNow;
    command.CommandText =
        """
        INSERT INTO commercial_documents (
            id,
            customer_id,
            service_request_id,
            document_type,
            status,
            title,
            internal_reference,
            currency,
            subtotal_amount_cents,
            tax_amount_cents,
            total_amount_cents,
            disclaimer,
            created_by_user_id,
            created_at,
            updated_at,
            shared_at,
            cancelled_at
        ) VALUES (
            @id,
            @customer_id,
            NULL,
            'quote_draft',
            'shared_with_customer',
            'Document isolation test',
            @reference,
            'EUR',
            1000,
            0,
            1000,
            'Document informatif — ne constitue pas une facture officielle.',
            @created_by_user_id,
            @now,
            @now,
            @now,
            NULL
        );
        """;
    AddDbParameter(command, "@id", id);
    AddDbParameter(command, "@customer_id", customerId);
    AddDbParameter(command, "@created_by_user_id", adminUserId);
    AddDbParameter(command, "@reference", reference);
    AddDbParameter(command, "@now", now);
    await command.ExecuteNonQueryAsync();
    return id;
}

async Task<string> FindInternalAdminUserIdAsync()
{
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText =
        """
        SELECT id
        FROM portal_users
        WHERE email = @email
          AND role = 'internal_admin'
        LIMIT 1;
        """;

    AddDbParameter(
        command,
        "@email",
        Environment.GetEnvironmentVariable("DEMO_INTERNAL_ADMIN_EMAIL")
            ?? string.Empty);

    var value = await command.ExecuteScalarAsync();

    var adminUserId = value switch
    {
        null => null,
        DBNull => null,
        Guid guidValue => guidValue.ToString("D"),
        string stringValue => stringValue,
        _ => value.ToString()
    };

    Ensure(
        !string.IsNullOrWhiteSpace(adminUserId),
        "L'utilisateur admin démo MariaDB requis pour les documents commerciaux est introuvable.");

    return adminUserId!;
}

async Task<string> FindCustomerIdAsync(string customerReference)
{
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText =
        """
        SELECT id
        FROM customers
        WHERE external_reference = @customer_reference
        LIMIT 1;
        """;

    AddDbParameter(command, "@customer_reference", customerReference);

    var value = await command.ExecuteScalarAsync();
    var customerId = value switch
    {
        null => null,
        DBNull => null,
        Guid guidValue => guidValue.ToString("D"),
        string stringValue => stringValue,
        _ => value.ToString()
    };

    Ensure(
        !string.IsNullOrWhiteSpace(customerId),
        $"Le client MariaDB {customerReference} requis pour les liens AD est introuvable.");

    return customerId!;
}

async Task<string> InsertCustomerAdLinkAsync(string customerReference)
{
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();

    var id = Guid.NewGuid().ToString("D");
    var objectGuid = Guid.NewGuid().ToString("D");
    var customerId = await FindCustomerIdAsync(customerReference);
    var adminUserId = await FindInternalAdminUserIdAsync();
    var samAccountName = $"KERMARIA_{customerReference}_AD_LINK_TEST";
    var distinguishedName =
        $"CN={samAccountName},OU=Groups,OU={customerReference},OU=10_Customers,OU=TEST_SITE_WEB,DC=home,DC=bzh";

    command.CommandText =
        """
        INSERT INTO customer_ad_links (
            id,
            customer_id,
            object_guid,
            object_sid,
            object_type,
            sam_account_name,
            user_principal_name,
            display_name,
            distinguished_name,
            linked_at,
            linked_by_user_id
        ) VALUES (
            @id,
            @customer_id,
            @object_guid,
            @object_sid,
            'group',
            @sam_account_name,
            NULL,
            @display_name,
            @distinguished_name,
            @linked_at,
            @linked_by_user_id
        );
        """;

    AddDbParameter(command, "@id", id);
    AddDbParameter(command, "@customer_id", customerId);
    AddDbParameter(command, "@object_guid", objectGuid);
    AddDbParameter(command, "@object_sid", $"S-1-5-21-{Guid.NewGuid():N}");
    AddDbParameter(command, "@sam_account_name", samAccountName);
    AddDbParameter(command, "@display_name", $"AD Link Test {customerReference}");
    AddDbParameter(command, "@distinguished_name", distinguishedName);
    AddDbParameter(command, "@linked_at", DateTime.UtcNow);
    AddDbParameter(command, "@linked_by_user_id", adminUserId);
    await command.ExecuteNonQueryAsync();

    return id;
}

async Task DeleteCustomerAdLinkAsync(string linkId)
{
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText =
        "DELETE FROM customer_ad_links WHERE id = @id;";
    AddDbParameter(command, "@id", linkId);
    await command.ExecuteNonQueryAsync();
}

async Task DeleteCommercialDocumentAsync(string documentId)
{
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    await using (var linesCommand = connection.CreateCommand())
    {
        linesCommand.Transaction = transaction;
        linesCommand.CommandText =
            "DELETE FROM commercial_document_lines WHERE document_id = @document_id;";
        AddDbParameter(linesCommand, "@document_id", documentId);
        await linesCommand.ExecuteNonQueryAsync();
    }

    await using (var documentCommand = connection.CreateCommand())
    {
        documentCommand.Transaction = transaction;
        documentCommand.CommandText =
            "DELETE FROM commercial_documents WHERE id = @document_id;";
        AddDbParameter(documentCommand, "@document_id", documentId);
        await documentCommand.ExecuteNonQueryAsync();
    }

    await transaction.CommitAsync();
}

async Task PrepareIsolationFixtureAsync(
    string customerId,
    string serviceId,
    string supportRequestId)
{
    await CleanupIsolationFixtureAsync(
        customerId,
        serviceId,
        supportRequestId);
    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    await using (var customerCommand = connection.CreateCommand())
    {
        customerCommand.Transaction = transaction;
        customerCommand.CommandText =
            """
            INSERT INTO customers (
                id,
                external_reference,
                display_name,
                status,
                created_at,
                updated_at
            ) VALUES (
                @id,
                'CLI-ISOLATION-V07',
                'Client isolation V0.7',
                'active',
                @now,
                @now
            );
            """;
        AddDbParameter(customerCommand, "@id", customerId);
        AddDbParameter(customerCommand, "@now", DateTime.UtcNow);
        await customerCommand.ExecuteNonQueryAsync();
    }

    await using (var serviceCommand = connection.CreateCommand())
    {
        serviceCommand.Transaction = transaction;
        serviceCommand.CommandText =
            """
            INSERT INTO customer_services (
                id,
                customer_id,
                external_reference,
                service_type,
                name,
                status,
                description,
                scope,
                commercial_terms,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @customer_id,
                'SVC-ISOLATION-V07',
                'support',
                'Service isolation V0.7',
                'active',
                'Donnée fictive de test opt-in.',
                'Test automatisé',
                'Selon devis',
                @now,
                @now
            );
            """;
        AddDbParameter(serviceCommand, "@id", serviceId);
        AddDbParameter(serviceCommand, "@customer_id", customerId);
        AddDbParameter(serviceCommand, "@now", DateTime.UtcNow);
        await serviceCommand.ExecuteNonQueryAsync();
    }

    await using (var notificationCommand = connection.CreateCommand())
    {
        notificationCommand.Transaction = transaction;
        notificationCommand.CommandText =
            """
            INSERT INTO portal_notifications (
                id,
                customer_id,
                request_type,
                request_id,
                notification_type,
                title,
                message,
                link_url,
                read_at,
                created_at
            ) VALUES (
                '90000000-0000-0000-0000-000000000073',
                @customer_id,
                NULL,
                NULL,
                'support_status_changed',
                'Notification isolation',
                'Donnée fictive de test.',
                NULL,
                NULL,
                @now
            );
            """;
        AddDbParameter(
            notificationCommand,
            "@customer_id",
            customerId);
        AddDbParameter(notificationCommand, "@now", DateTime.UtcNow);
        await notificationCommand.ExecuteNonQueryAsync();
    }

    await using (var supportRequestCommand = connection.CreateCommand())
    {
        supportRequestCommand.Transaction = transaction;
        supportRequestCommand.CommandText =
            """
            INSERT INTO support_requests (
                id,
                customer_id,
                created_by_user_id,
                service_id,
                reference,
                subject,
                description,
                priority,
                category,
                status,
                closed_at,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @customer_id,
                NULL,
                @service_id,
                'SUP-ISOLATION-V013',
                'Demande isolation V0.13',
                'Donnée fictive de test opt-in.',
                'normal',
                'support',
                'open',
                NULL,
                @now,
                @now
            );
            """;
        AddDbParameter(supportRequestCommand, "@id", supportRequestId);
        AddDbParameter(
            supportRequestCommand,
            "@customer_id",
            customerId);
        AddDbParameter(supportRequestCommand, "@service_id", serviceId);
        AddDbParameter(supportRequestCommand, "@now", DateTime.UtcNow);
        await supportRequestCommand.ExecuteNonQueryAsync();
    }

    await transaction.CommitAsync();
}

async Task CleanupIsolationFixtureAsync(
    string customerId,
    string serviceId,
    string supportRequestId)
{
    if (!IsMariaDbTestRequested())
    {
        return;
    }

    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    foreach (var table in new[]
    {
        "request_public_messages",
        "request_events"
    })
    {
        await using var requestDataCommand = connection.CreateCommand();
        requestDataCommand.Transaction = transaction;
        requestDataCommand.CommandText =
            $"DELETE FROM {table} WHERE request_type = 'support' AND request_id = @request_id;";
        AddDbParameter(
            requestDataCommand,
            "@request_id",
            supportRequestId);
        await requestDataCommand.ExecuteNonQueryAsync();
    }

    await using (var commercialLinesCommand = connection.CreateCommand())
    {
        commercialLinesCommand.Transaction = transaction;
        commercialLinesCommand.CommandText =
            """
            DELETE line
            FROM commercial_document_lines line
            INNER JOIN commercial_documents document
                ON document.id = line.document_id
            WHERE document.customer_id = @customer_id;
            """;
        AddDbParameter(
            commercialLinesCommand,
            "@customer_id",
            customerId);
        await commercialLinesCommand.ExecuteNonQueryAsync();
    }

    await using (var commercialDocumentsCommand = connection.CreateCommand())
    {
        commercialDocumentsCommand.Transaction = transaction;
        commercialDocumentsCommand.CommandText =
            "DELETE FROM commercial_documents WHERE customer_id = @customer_id;";
        AddDbParameter(
            commercialDocumentsCommand,
            "@customer_id",
            customerId);
        await commercialDocumentsCommand.ExecuteNonQueryAsync();
    }

    await using (var notificationCommand = connection.CreateCommand())
    {
        notificationCommand.Transaction = transaction;
        notificationCommand.CommandText =
            "DELETE FROM portal_notifications WHERE customer_id = @customer_id;";
        AddDbParameter(notificationCommand, "@customer_id", customerId);
        await notificationCommand.ExecuteNonQueryAsync();
    }

    await using (var supportRequestCommand = connection.CreateCommand())
    {
        supportRequestCommand.Transaction = transaction;
        supportRequestCommand.CommandText =
            "DELETE FROM support_requests WHERE id = @id;";
        AddDbParameter(
            supportRequestCommand,
            "@id",
            supportRequestId);
        await supportRequestCommand.ExecuteNonQueryAsync();
    }

    await using (var serviceCommand = connection.CreateCommand())
    {
        serviceCommand.Transaction = transaction;
        serviceCommand.CommandText =
            "DELETE FROM customer_services WHERE id = @id;";
        AddDbParameter(serviceCommand, "@id", serviceId);
        await serviceCommand.ExecuteNonQueryAsync();
    }

    await using (var customerCommand = connection.CreateCommand())
    {
        customerCommand.Transaction = transaction;
        customerCommand.CommandText =
            "DELETE FROM customers WHERE id = @id;";
        AddDbParameter(customerCommand, "@id", customerId);
        await customerCommand.ExecuteNonQueryAsync();
    }

    await transaction.CommitAsync();
}

async Task ResetLoginFailureFixtureAsync(string email)
{
    if (!IsMariaDbTestRequested())
    {
        return;
    }

    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText =
        """
        UPDATE portal_users
        SET failed_login_count = 0,
            last_failed_login_at = NULL,
            locked_until = NULL,
            updated_at = @updated_at
        WHERE LOWER(email) = @email;
        """;
    AddDbParameter(command, "@updated_at", DateTime.UtcNow);
    AddDbParameter(command, "@email", email.Trim().ToLowerInvariant());
    await command.ExecuteNonQueryAsync();
}

DbConnection CreateMariaDbTestConnection()
{
    var builder = new DbConnectionStringBuilder
    {
        ["Server"] = Environment.GetEnvironmentVariable("SQL_HOST"),
        ["Port"] = Environment.GetEnvironmentVariable("SQL_PORT"),
        ["Database"] = Environment.GetEnvironmentVariable("SQL_DATABASE"),
        ["User ID"] = Environment.GetEnvironmentVariable("SQL_USERNAME"),
        ["Password"] = Environment.GetEnvironmentVariable("SQL_PASSWORD"),
        ["Character Set"] = "utf8mb4",
        ["SSL Mode"] = "Preferred"
    };
    var connectorPath = Path.Combine(
        Path.GetDirectoryName(apiAssembly)!,
        "MySqlConnector.dll");
    var connectorAssembly = SmokeTestRuntimeHelpers.LoadAssemblyWithoutLock(
        connectorPath);
    var connectionType = connectorAssembly.GetType(
        "MySqlConnector.MySqlConnection",
        throwOnError: true)!;

    return Activator.CreateInstance(
            connectionType,
            builder.ConnectionString) as DbConnection
        ?? throw new InvalidOperationException(
            "Impossible de créer la connexion MariaDB des tests opt-in.");
}

static void AddDbParameter(
    DbCommand command,
    string name,
    object value)
{
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value;
    command.Parameters.Add(parameter);
}

void VerifyIdentifierMapping()
{
    var apiAssemblyForMapping = SmokeTestRuntimeHelpers.LoadAssemblyWithoutLock(
        apiAssembly);
    var readerType = apiAssemblyForMapping.GetType(
        "Kermaria.ApiInternal.Data.Repositories.MariaDbIdentifierReader",
        throwOnError: true)!;
    var requiredMethod = readerType.GetMethod(
        "ConvertRequiredValue",
        BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Le helper MariaDB d'identifiant requis est introuvable.");
    var nullableMethod = readerType.GetMethod(
        "ConvertNullableValue",
        BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Le helper MariaDB d'identifiant nullable est introuvable.");
    var guid = Guid.NewGuid();

    Ensure(
        InvokeIdentifier(requiredMethod, guid, "test.id")
            == guid.ToString("D"),
        "Le mapping MariaDB Guid vers string est invalide.");
    Ensure(
        InvokeIdentifier(requiredMethod, "catalog-vpn", "test.id")
            == "catalog-vpn",
        "Le mapping MariaDB string est invalide.");
    Ensure(
        InvokeIdentifier(requiredMethod, guid.ToByteArray(), "test.id")
            == guid.ToString("D"),
        "Le mapping MariaDB BINARY(16) vers GUID est invalide.");
    Ensure(
        nullableMethod.Invoke(
            null,
            [DBNull.Value, "test.nullable_id"]) is null,
        "Le mapping MariaDB nullable ne gère pas DBNull.");
}

static string InvokeIdentifier(
    MethodInfo method,
    object value,
    string columnName)
{
    return method.Invoke(null, [value, columnName]) as string
        ?? throw new InvalidOperationException(
            $"Le mapping de l'identifiant {columnName} n'a pas retourné de chaîne.");
}

HttpRequestMessage CreateSessionRequest(
    HttpMethod method,
    string url,
    string sessionToken)
{
    var request = new HttpRequestMessage(method, url);
    request.Headers.Add(sessionHeader, sessionToken);
    return request;
}

static string MockCustomerReference() => "CLI-DEMO-0042";

void ConfigureMockAuthentication(
    ProcessStartInfo startInfo,
    string status,
    string durationMinutes)
{
    startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
    startInfo.Environment["AD_INTEGRATION_MODE"] = "disabled";
    startInfo.Environment["DEMO_PORTAL_EMAIL"] = mockEmail;
    startInfo.Environment["DEMO_PORTAL_PASSWORD"] = mockPassword;
    startInfo.Environment["DEMO_PORTAL_STATUS"] = status;
    startInfo.Environment["DEMO_INTERNAL_ADMIN_EMAIL"] = mockAdminEmail;
    startInfo.Environment["DEMO_INTERNAL_ADMIN_PASSWORD"] =
        mockAdminPassword;
    startInfo.Environment["SESSION_DURATION_MINUTES"] = durationMinutes;
    startInfo.Environment["LOGIN_MAX_FAILURES"] = "5";
    startInfo.Environment["LOGIN_LOCKOUT_MINUTES"] = "10";
    foreach (var variable in new[]
    {
        "SQL_PROVIDER",
        "SQL_HOST",
        "SQL_PORT",
        "SQL_DATABASE",
        "SQL_USERNAME",
        "SQL_PASSWORD"
    })
    {
        startInfo.Environment.Remove(variable);
    }
}

RunningApi StartApi(
    string baseUrl,
    Action<ProcessStartInfo> configure)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = dotnetExecutable,
        WorkingDirectory = Path.GetDirectoryName(apiAssembly)!,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    startInfo.ArgumentList.Add(apiAssembly);
    startInfo.ArgumentList.Add("--urls");
    startInfo.ArgumentList.Add(baseUrl);
    configure(startInfo);
    ApplyChildProcessEnvironmentGuardrails(startInfo);

    var logs = new StringBuilder();
    var process = new Process { StartInfo = startInfo };
    process.OutputDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data is not null)
        {
            lock (logs)
            {
                logs.AppendLine(eventArgs.Data);
            }
        }
    };
    process.ErrorDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data is not null)
        {
            lock (logs)
            {
                logs.AppendLine(eventArgs.Data);
            }
        }
    };

    if (!process.Start())
    {
        throw new InvalidOperationException("Impossible de démarrer API-INTERNAL.");
    }

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return new RunningApi(process, logs);
}

void VerifyChildProcessEnvironmentGuardrails()
{
    var nonDevelopmentStartInfo = new ProcessStartInfo();
    nonDevelopmentStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Staging";
    nonDevelopmentStartInfo.Environment["DOTNET_ENVIRONMENT"] = "Staging";
    nonDevelopmentStartInfo.Environment["RUN_MARIADB_TESTS"] = "true";
    ApplyChildProcessEnvironmentGuardrails(nonDevelopmentStartInfo);
    Ensure(
        !string.Equals(
            nonDevelopmentStartInfo.Environment["RUN_MARIADB_TESTS"],
            "true",
            StringComparison.OrdinalIgnoreCase),
        "Un process enfant non Development ne doit pas heriter de RUN_MARIADB_TESTS=true.");

    var developmentStartInfo = new ProcessStartInfo();
    developmentStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    developmentStartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
    developmentStartInfo.Environment["RUN_MARIADB_TESTS"] = "true";
    ApplyChildProcessEnvironmentGuardrails(developmentStartInfo);
    Ensure(
        string.Equals(
            developmentStartInfo.Environment["RUN_MARIADB_TESTS"],
            "true",
            StringComparison.OrdinalIgnoreCase),
        "Un process enfant Development doit conserver RUN_MARIADB_TESTS=true.");
}

void ApplyChildProcessEnvironmentGuardrails(ProcessStartInfo startInfo)
{
    if (IsDevelopmentEnvironment(startInfo.Environment))
    {
        return;
    }

    startInfo.Environment["RUN_MARIADB_TESTS"] = "false";
}

bool IsDevelopmentEnvironment(
    IDictionary<string, string?> environment)
{
    environment.TryGetValue(
        "ASPNETCORE_ENVIRONMENT",
        out var aspNetEnvironment);
    environment.TryGetValue(
        "DOTNET_ENVIRONMENT",
        out var dotNetEnvironment);
    return string.Equals(
               aspNetEnvironment,
               "Development",
               StringComparison.OrdinalIgnoreCase)
        || string.Equals(
               dotNetEnvironment,
               "Development",
               StringComparison.OrdinalIgnoreCase);
}

void ValidateRuntimeConfiguration(
    IConfiguration configuration,
    string environmentName)
{
    var configurationContracts = GetRuntimeConfigurationContracts();
    configurationContracts.ValidateMethod.Invoke(
        null,
        [configuration, new TestHostEnvironment(environmentName)]);
}

bool TryGetRuntimeConfigurationException(
    TargetInvocationException invocationException,
    out Exception runtimeException)
{
    var configurationContracts = GetRuntimeConfigurationContracts();
    if (invocationException.InnerException is not null
        && configurationContracts.ExceptionType.IsInstanceOfType(
            invocationException.InnerException))
    {
        runtimeException = invocationException.InnerException;
        return true;
    }

    runtimeException = invocationException;
    return false;
}

RuntimeConfigurationContracts GetRuntimeConfigurationContracts()
{
    return runtimeConfiguration
        ?? throw new InvalidOperationException(
            "Les contrats de configuration runtime ne sont pas initialises.");
}

bool IsMariaDbTestRequested()
    => string.Equals(
        Environment.GetEnvironmentVariable("RUN_MARIADB_TESTS"),
        "true",
        StringComparison.OrdinalIgnoreCase);

static async Task<HttpResponseMessage> WaitForHealthAsync(
    HttpClient client,
    Process apiProcess,
    string baseUrl,
    StringBuilder logs)
{
    HttpRequestException? lastException = null;
    for (var attempt = 0; attempt < 40; attempt++)
    {
        if (apiProcess.HasExited)
        {
            throw new InvalidOperationException(
                $"API-INTERNAL s'est arrêtée prématurément. {SnapshotLogs(logs)}");
        }

        try
        {
            return await client.GetAsync($"{baseUrl}/health");
        }
        catch (HttpRequestException exception)
        {
            lastException = exception;
            await Task.Delay(250);
        }
    }

    throw new InvalidOperationException(
        "Le health check de l'API n'a pas répondu dans le délai prévu.");
}

static async Task<HttpResponseMessage> WaitForEndpointAsync(
    HttpClient client,
    Process apiProcess,
    string endpoint,
    StringBuilder logs)
{
    for (var attempt = 0; attempt < 40; attempt++)
    {
        if (apiProcess.HasExited)
        {
            throw new InvalidOperationException(
                $"API-INTERNAL s'est arrêtée prématurément. {SnapshotLogs(logs)}");
        }

        try
        {
            return await client.GetAsync(endpoint);
        }
        catch (HttpRequestException)
        {
            await Task.Delay(250);
        }
    }

    throw new InvalidOperationException(
        "L'endpoint attendu n'a pas répondu dans le délai prévu.");
}

static void Ensure(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string SnapshotLogs(StringBuilder logs)
{
    lock (logs)
    {
        return logs.ToString();
    }
}

sealed class RunningApi : IDisposable
{
    public RunningApi(Process process, StringBuilder logs)
    {
        Process = process;
        Logs = logs;
    }

    public Process Process { get; }
    public StringBuilder Logs { get; }

    public async Task StopAsync()
    {
        if (!Process.HasExited)
        {
            Process.Kill(entireProcessTree: true);
            await Process.WaitForExitAsync();
        }
    }

    public void Dispose()
    {
        Process.Dispose();
    }
}

sealed class TestHostEnvironment : IHostEnvironment
{
    public TestHostEnvironment(string environmentName)
    {
        EnvironmentName = environmentName;
    }

    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "SmokeTests";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } =
        new NullFileProvider();
}

sealed class ApiRuntime : IDisposable
{
    public ApiRuntime(string workingDirectory, string assemblyPath)
    {
        WorkingDirectory = workingDirectory;
        AssemblyPath = assemblyPath;
    }

    public string WorkingDirectory { get; }
    public string AssemblyPath { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkingDirectory))
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

sealed class RuntimeConfigurationContracts
{
    public RuntimeConfigurationContracts(
        Type exceptionType,
        MethodInfo validateMethod,
        PropertyInfo variablesProperty)
    {
        ExceptionType = exceptionType;
        ValidateMethod = validateMethod;
        VariablesProperty = variablesProperty;
    }

    public Type ExceptionType { get; }
    public MethodInfo ValidateMethod { get; }
    public PropertyInfo VariablesProperty { get; }

    public IReadOnlyCollection<string> GetVariables(Exception exception)
    {
        return VariablesProperty.GetValue(exception)
            as IReadOnlyCollection<string>
            ?? throw new InvalidOperationException(
                "La liste des variables invalides est introuvable.");
    }
}

static class SmokeTestRuntimeHelpers
{
    public static string CreateLoopbackBaseUrl()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}";
    }

    public static ApiRuntime CreateIsolatedApiRuntime(string sourceApiAssembly)
    {
        var sourceDirectory = Path.GetDirectoryName(sourceApiAssembly)
            ?? throw new InvalidOperationException(
                "Le repertoire de build API-INTERNAL est introuvable.");
        var runtimeDirectory = Path.Combine(
            Path.GetTempPath(),
            "kermaria-api-internal-smoketests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(runtimeDirectory);
        CopyDirectoryContents(sourceDirectory, runtimeDirectory);

        return new ApiRuntime(
            runtimeDirectory,
            Path.Combine(runtimeDirectory, Path.GetFileName(sourceApiAssembly)));
    }

    public static RuntimeConfigurationContracts LoadRuntimeConfigurationContracts(
        string apiAssemblyPath)
    {
        var loadedAssembly = LoadAssemblyWithoutLock(apiAssemblyPath);
        var exceptionType = loadedAssembly.GetType(
            "Kermaria.ApiInternal.Data.Configuration.RuntimeConfigurationException",
            throwOnError: true)!;
        var validatorType = loadedAssembly.GetType(
            "Kermaria.ApiInternal.Data.Configuration.RuntimeConfigurationValidator",
            throwOnError: true)!;
        var validateMethod = validatorType.GetMethod(
            "Validate",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "Le validateur de configuration runtime est introuvable.");
        var variablesProperty = exceptionType.GetProperty("Variables")
            ?? throw new InvalidOperationException(
                "La liste des variables invalides est introuvable.");

        return new RuntimeConfigurationContracts(
            exceptionType,
            validateMethod,
            variablesProperty);
    }

    public static Assembly LoadAssemblyWithoutLock(string assemblyPath)
    {
        return Assembly.Load(File.ReadAllBytes(assemblyPath));
    }

    private static void CopyDirectoryContents(
        string sourceDirectory,
        string destinationDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(
            sourceDirectory,
            "*",
            SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(
                Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(
            sourceDirectory,
            "*",
            SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(destinationPath)
                ?? destinationDirectory);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }
}
