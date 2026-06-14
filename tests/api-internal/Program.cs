using System.Diagnostics;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Data.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

const string correlationHeader = "X-Correlation-Id";
const string dataSourceHeader = "X-Data-Source";
const string sessionHeader = "X-Portal-Session";
const string testCorrelationId = "v0.8-smoke-test";
const string mockBaseUrl = "http://127.0.0.1:5088";
const string mockEmail = "portal.test@example.invalid";
const string mockPassword = "NOT_A_REAL_PASSWORD_V07";
const string mockAdminEmail = "admin.test@example.invalid";
const string mockAdminPassword = "NOT_A_REAL_ADMIN_PASSWORD_V08";

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine(
        "Usage: smoke-tests [dotnet-executable] <api-internal-dll>");
    return 2;
}

var dotnetExecutable = args.Length == 2
    ? Path.GetFullPath(args[0])
    : "dotnet";
var apiAssembly = Path.GetFullPath(args[^1]);

if ((args.Length == 2 && !File.Exists(dotnetExecutable))
    || !File.Exists(apiAssembly))
{
    Console.Error.WriteLine("Le runtime .NET ou l'assembly API est introuvable.");
    return 2;
}

VerifyIdentifierMapping();
await RunMockTestsAsync();
await RunUnavailableReadinessTestAsync();
await RunProductionConfigurationValidationTestsAsync();
await RunDisabledAccountTestAsync();
await RunExpiredSessionTestAsync();
await RunLockoutResetTestAsync();

if (IsMariaDbTestRequested())
{
    await RunMariaDbReadTestsAsync();
}

Console.WriteLine("Smoke tests API-INTERNAL V0.9 réussis.");
return 0;

async Task RunMockTestsAsync()
{
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
            "/internal/admin/customers",
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

        using var adHealthResponse = await client.GetAsync(
            $"{mockBaseUrl}/internal/ad/health");
        var adHealthText = await adHealthResponse.Content.ReadAsStringAsync();
        using var adHealthPayload = JsonDocument.Parse(adHealthText);

        Ensure(
            adHealthResponse.StatusCode == HttpStatusCode.OK,
            "Le diagnostic AD interne ne répond pas avec HTTP 200.");
        Ensure(
            adHealthPayload.RootElement.GetProperty("mode").GetString()
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
            !api.Logs.ToString().Contains(
                passwordLogSentinel,
                StringComparison.Ordinal),
            "Un mot de passe de test a été écrit dans les logs.");
        Ensure(
            !api.Logs.ToString().Contains(
                invalidLoginPassword,
                StringComparison.Ordinal)
            && !api.Logs.ToString().Contains(
                mockPassword,
                StringComparison.Ordinal)
            && !api.Logs.ToString().Contains(
                sessionToken!,
                StringComparison.Ordinal),
            "Un mot de passe ou token de session a été écrit dans les logs.");
        Ensure(
            !api.Logs.ToString().Contains(
                mockAdminPassword,
                StringComparison.Ordinal)
            && !api.Logs.ToString().Contains(
                adminSessionToken,
                StringComparison.Ordinal),
            "Un mot de passe ou token admin a été écrit dans les logs.");
        Ensure(
            !api.Logs.ToString().Contains(privateNote, StringComparison.Ordinal)
            && !api.Logs.ToString().Contains(
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
    const string baseUrl = "http://127.0.0.1:5090";
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
    const string baseUrl = "http://127.0.0.1:5091";
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
    const string baseUrl = "http://127.0.0.1:5092";
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
    const string baseUrl = "http://127.0.0.1:5092";
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
            && !api.Logs.ToString().Contains(
                sqlPasswordSentinel,
                StringComparison.Ordinal),
            "La readiness ne doit divulguer aucun mot de passe SQL.");
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

    RuntimeConfigurationValidator.Validate(
        new ConfigurationBuilder().Build(),
        new TestHostEnvironment("Development"));

    await Task.CompletedTask;
}

void VerifyRejectedProductionConfiguration(
    string expectedVariable,
    Action<Dictionary<string, string?>> configure)
{
    var configuration = CreateProductionConfiguration();
    configure(configuration);

    try
    {
        RuntimeConfigurationValidator.Validate(
            new ConfigurationBuilder()
                .AddInMemoryCollection(configuration)
                .Build(),
            new TestHostEnvironment("Production"));
        throw new InvalidOperationException(
            "Une configuration Production invalide a été acceptée.");
    }
    catch (RuntimeConfigurationException exception)
    {
        Ensure(
            exception.Variables.Contains(expectedVariable),
            $"Le refus doit nommer {expectedVariable} sans afficher sa valeur.");
        Ensure(
            !exception.Message.Contains(
                "NOT_A_REAL_PRODUCTION_SQL_VALUE_V09",
                StringComparison.Ordinal)
            && !exception.Message.Contains(
                "NOT_A_REAL_SERVICE_AUTH_VALUE_V09",
                StringComparison.Ordinal)
            && !exception.Message.Contains(
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

    const string mariaDbBaseUrl = "http://127.0.0.1:5089";
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
    string? workflowSupportRequestId = null;
    string? workflowServiceRequestId = null;

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
        await VerifyNotificationMigrationAsync();

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
            isolationServiceId);

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
            && !mariaClientDetailText.Contains(
                mariaPrivateNote,
                StringComparison.Ordinal)
            && !mariaClientDetailText.Contains(
                "internalNotes",
                StringComparison.OrdinalIgnoreCase),
            "La séparation note interne/message public MariaDB est invalide.");

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

        using var adHealthResponse = await client.GetAsync(
            $"{mariaDbBaseUrl}/internal/ad/health");
        using var adHealthPayload = JsonDocument.Parse(
            await adHealthResponse.Content.ReadAsStringAsync());
        Ensure(
            adHealthResponse.StatusCode == HttpStatusCode.OK,
            "Le diagnostic AD conditionnel ne répond pas avec HTTP 200.");
        Ensure(
            adHealthPayload.RootElement.GetProperty("mode").GetString()
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
        await CleanupIsolationFixtureAsync(
            isolationCustomerId,
            isolationServiceId);
        await CleanupWorkflowFixtureAsync(
            workflowSupportRequestId,
            workflowServiceRequestId);
        await api.StopAsync();
    }
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
                AS message_count,
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
        Convert.ToInt32(reader["support_event_count"]) >= 2
        && Convert.ToInt32(reader["service_event_count"]) >= 2
        && Convert.ToInt32(reader["note_count"]) == 1
        && Convert.ToInt32(reader["message_count"]) == 1
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
        AddDbParameter(
            command,
            "@support_id",
            supportRequestId ?? string.Empty);
        AddDbParameter(
            command,
            "@service_id",
            serviceRequestId ?? string.Empty);
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

async Task PrepareIsolationFixtureAsync(
    string customerId,
    string serviceId)
{
    await CleanupIsolationFixtureAsync(customerId, serviceId);
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

    await transaction.CommitAsync();
}

async Task CleanupIsolationFixtureAsync(
    string customerId,
    string serviceId)
{
    if (!IsMariaDbTestRequested())
    {
        return;
    }

    await using var connection = CreateMariaDbTestConnection();
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    await using (var notificationCommand = connection.CreateCommand())
    {
        notificationCommand.Transaction = transaction;
        notificationCommand.CommandText =
            "DELETE FROM portal_notifications WHERE customer_id = @customer_id;";
        AddDbParameter(notificationCommand, "@customer_id", customerId);
        await notificationCommand.ExecuteNonQueryAsync();
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
    var connectorAssembly = Assembly.LoadFrom(connectorPath);
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
    var apiAssemblyForMapping = Assembly.LoadFrom(apiAssembly);
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

    var logs = new StringBuilder();
    var process = new Process { StartInfo = startInfo };
    process.OutputDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data is not null)
        {
            logs.AppendLine(eventArgs.Data);
        }
    };
    process.ErrorDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data is not null)
        {
            logs.AppendLine(eventArgs.Data);
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
    for (var attempt = 0; attempt < 40; attempt++)
    {
        if (apiProcess.HasExited)
        {
            throw new InvalidOperationException(
                $"API-INTERNAL s'est arrêtée prématurément. {logs}");
        }

        try
        {
            return await client.GetAsync($"{baseUrl}/health");
        }
        catch (HttpRequestException)
        {
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
                $"API-INTERNAL s'est arrêtée prématurément. {logs}");
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
