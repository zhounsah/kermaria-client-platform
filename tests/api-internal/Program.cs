using System.Diagnostics;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Email;
using Kermaria.ApiInternal.Services.Provisioning;
using Kermaria.ApiInternal.SmokeTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    if (arguments.Length == 1
        && string.Equals(arguments[0], "--billing-v2-change-integration", StringComparison.Ordinal))
    {
        try { await BillingV2SubscriptionChangeIntegrationTests.RunAsync(); Console.WriteLine("Integration M vers L Billing V2 reussie."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine("Integration M vers L Billing V2 en echec."); Console.Error.WriteLine(exception); return 1; }
    }

    if (arguments.Length == 1
        && string.Equals(arguments[0], "--billing-v2-change-one-time-refusal", StringComparison.Ordinal))
    {
        try { await BillingV2SubscriptionChangeIntegrationTests.RunSubscriptionChangeOneTimeRefusalAsync(); Console.WriteLine("Refus des frais one-time de changement Billing V2 reussi."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine("Refus des frais one-time de changement Billing V2 en echec."); Console.Error.WriteLine(exception); return 1; }
    }

    if (arguments.Length == 1
        && string.Equals(arguments[0], "--billing-v2-downgrade-integration", StringComparison.Ordinal))
    {
        try { await BillingV2SubscriptionChangeIntegrationTests.RunDeferredDowngradeAsync(); Console.WriteLine("Integration L vers S differee Billing V2 reussie."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine("Integration L vers S differee Billing V2 en echec."); Console.Error.WriteLine(exception); return 1; }
    }

    if (arguments.Length == 1
        && string.Equals(arguments[0], "--billing-v2-change-crash-concurrency", StringComparison.Ordinal))
    {
        try { await BillingV2SubscriptionChangeIntegrationTests.RunCrashConcurrencyAsync(); Console.WriteLine("Tests crash et concurrence Billing V2 reussis."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine("Tests crash et concurrence Billing V2 en echec."); Console.Error.WriteLine(exception); return 1; }
    }

    if (arguments.Length == 1
        && string.Equals(arguments[0], "--billing-v2-stripe-indeterminate", StringComparison.Ordinal))
    {
        try { await BillingV2SubscriptionChangeIntegrationTests.RunStripeIndeterminateAsync(); Console.WriteLine("Tests Stripe indetermine Billing V2 reussis."); return 0; }
        catch (Exception exception) { Console.Error.WriteLine("Tests Stripe indetermine Billing V2 en echec."); Console.Error.WriteLine(exception); return 1; }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-pricing",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2PricingTests.RunAsync();
            Console.WriteLine("Tests pricing Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Tests pricing Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-public-catalog",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2PublicCatalogTests.RunAsync();
            Console.WriteLine("Tests catalogue public Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests catalogue public Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-provisioning-scope",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2ProvisioningScopeTests.RunAsync();
            Console.WriteLine(
                "Tests isolation par utilisateur du provisioning Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests isolation par utilisateur du provisioning Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-provisioning-semantics",
            StringComparison.Ordinal))
    {
        try
        {
            BillingV2ProvisioningSemanticsTests.Run();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests semantique des regles de provisioning Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-koxo-storage-target",
            StringComparison.Ordinal))
    {
        try
        {
            BillingV2KoxoStorageTargetTests.Run();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests cibles de stockage KoXo Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-koxo-storage-resolution",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2KoxoStorageResolutionServiceTests.RunAsync();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests alimentation des cibles de stockage KoXo Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-koxo-storage-provider",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2KoxoStorageProviderTests.RunAsync();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests provider de stockage KoXo Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-additional-user-identity",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2AdditionalUserIdentityTests.RunAsync();
            Console.WriteLine(
                "Tests cycle de vie des utilisateurs additionnels Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests cycle de vie des utilisateurs additionnels Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    // Volontairement hors de la suite par defaut : elle exige une MariaDB
    // JETABLE portant les migrations 001 a 065. Sans base, elle echoue en le
    // disant plutot que de simuler un succes.
    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-additional-user-identity-schema",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2AdditionalUserIdentitySchemaTests.RunAsync();
            Console.WriteLine(
                "Tests schema du cycle de vie des utilisateurs additionnels "
                + "Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests schema du cycle de vie des utilisateurs additionnels "
                + "Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-new-subscription",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2NewSubscriptionTests.RunAsync();
            Console.WriteLine("Tests nouveaux abonnements Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests nouveaux abonnements Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-hardening",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2HardeningTests.RunAsync();
            Console.WriteLine("Tests hardening Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Tests hardening Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--commercial-document-stripe-payment",
            StringComparison.Ordinal))
    {
        try
        {
            await CommercialDocumentStripePaymentTests.RunAsync();
            Console.WriteLine(
                "Tests reglement Stripe des documents commerciaux reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests reglement Stripe des documents commerciaux en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-renewal",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2RenewalTests.RunAsync();
            Console.WriteLine("Tests renouvellement Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests renouvellement Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-cancellation",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2CancellationTests.RunAsync();
            Console.WriteLine("Tests resiliation Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Tests resiliation Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-stripe-rail",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2StripeRailTests.RunAsync();
            Console.WriteLine("Tests rail Stripe Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Tests rail Stripe Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-financial-core",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2FinancialCoreTests.RunAsync();
            Console.WriteLine("Tests coeur financier Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests coeur financier Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    // Exige une MariaDB JETABLE via BILLING_V2_TEST_MARIADB_CONNECTION.
    // Volontairement hors de la suite par defaut : sans base, la suite echoue
    // explicitement au lieu de passer en silence.
    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-financial-core-schema",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2FinancialCoreSchemaTests.RunAsync();
            Console.WriteLine(
                "Tests schema coeur financier Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests schema coeur financier Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    // Exige elle aussi une MariaDB JETABLE portant les migrations 001 a 063.
    // Couvre les quatre correctifs issus de la validation reelle : catalogue
    // sans palier, ancre d'idempotence, bornes du contrat comptant et fin de
    // terme.
    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--billing-v2-native-checkout-schema",
            StringComparison.Ordinal))
    {
        try
        {
            await BillingV2NativeCheckoutSchemaTests.RunAsync();
            Console.WriteLine(
                "Tests schema checkout natif Billing V2 reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests schema checkout natif Billing V2 en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    // Regles d'adoption d'une identite AD, sur persistance mock : qui a le
    // droit de reprendre un objet annuaire deja connu.
    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--ad-link-adoption",
            StringComparison.Ordinal))
    {
        try
        {
            await ActiveDirectoryLinkAdoptionTests.RunAsync();
            Console.WriteLine(
                "Tests regles d'adoption des liens Active Directory reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests regles d'adoption des liens Active Directory en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    // Exige elle aussi une MariaDB JETABLE. Le defaut couvert est l'omission
    // d'une colonne dans un UPDATE : la persistance mock reecrit
    // l'enregistrement entier, donc cette classe de bug lui est invisible.
    if (arguments.Length == 1
        && string.Equals(
            arguments[0],
            "--ad-link-repository-schema",
            StringComparison.Ordinal))
    {
        try
        {
            await ActiveDirectoryLinkRepositorySchemaTests.RunAsync();
            Console.WriteLine(
                "Tests adoption des liens Active Directory reussis.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Tests adoption des liens Active Directory en echec.");
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

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
        VerifyBackupProtectionService();
        VerifyActiveDirectoryPathScope();
        VerifyChildProcessEnvironmentGuardrails();
        await VerifySignupStoresPriceFreeBillingV2SelectionAsync();
        await VerifyCommunicationTemplatesAsync();
        await VerifyDiagnosticConfigurationAsync();
        await RunMockTestsAsync();
        await RunMockActiveDirectoryModeTestsAsync();
        await RunMockBpceIssuingTestsAsync();
        await RunReadOnlyActiveDirectoryModeTestsAsync();
        await RunUnavailableReadinessTestAsync();
        await RunProductionConfigurationValidationTestsAsync();
        await RunServiceAuthenticationGuardTestsAsync();
        await RunKoxoExportHttpTestsAsync();
        await RunKoxoExportServiceTestsAsync();
        await RunKoxoPendingPasswordTestsAsync();
        await RunKoxoSyncWebhookTriggerServiceTestsAsync();
        await RunSignupKoxoWebhookTriggerTestsAsync();
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
            "OU=Clients,DC=clients,DC=home,DC=bzh")
        ?? throw new InvalidOperationException(
            "Le scope Active Directory ne peut pas etre instancie.");
    var extractCustomerReference = scopeType.GetMethod(
            "ExtractCustomerReference")
        ?? throw new InvalidOperationException(
            "La methode ExtractCustomerReference est introuvable.");

    var userCustomerReference = extractCustomerReference.Invoke(
        scope,
        ["CN=test2,OU=Users,OU=CLI-DEMO-0060,OU=Clients,DC=clients,DC=home,DC=bzh"]) as string;
    var groupCustomerReference = extractCustomerReference.Invoke(
        scope,
        ["CN=testgroupe1,OU=Groups,OU=CLI-DEMO-0060,OU=Clients,DC=clients,DC=home,DC=bzh"]) as string;
    var disabledCustomerReference = extractCustomerReference.Invoke(
        scope,
        ["CN=test3,OU=Disabled,OU=CLI-DEMO-0060,OU=Clients,DC=clients,DC=home,DC=bzh"]) as string;

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
        "Le scope AD doit extraire la reference client reelle directement sous OU=Clients.");
}

void VerifyBackupProtectionService()
{
    var service = new BackupProtectionService();
    var now = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);

    Ensure(
        service.ComputeProtectionStatus(
            now,
            now.AddHours(-2),
            "success",
            now.AddMinutes(-10),
            1440,
            2160,
            180) == BackupProtectionStatuses.Protected,
        "Une sauvegarde reussie recente doit etre protegee.");
    Ensure(
        service.ComputeProtectionStatus(
            now,
            now.AddHours(-2),
            "warning",
            now.AddMinutes(-10),
            1440,
            2160,
            180) == BackupProtectionStatuses.Warning,
        "Un dernier job en Warning doit afficher Attention.");
    Ensure(
        service.ComputeProtectionStatus(
            now,
            now.AddHours(-40),
            "success",
            now.AddMinutes(-10),
            1440,
            2160,
            180) == BackupProtectionStatuses.Critical,
        "Une derniere reussite trop ancienne doit etre critique.");
    Ensure(
        service.ComputeProtectionStatus(
            now,
            null,
            "unknown",
            now.AddMinutes(-10),
            1440,
            2160,
            180) == BackupProtectionStatuses.Unknown,
        "Des donnees Veeam manquantes doivent rester inconnues.");
    Ensure(
        service.ComputeProtectionStatus(
            now,
            now.AddHours(-2),
            "success",
            now.AddHours(-4),
            1440,
            2160,
            180) == BackupProtectionStatuses.Unknown,
        "Un collecteur silencieux ne doit pas laisser un etat protege.");
    Ensure(
        BackupProtectionService.SanitizePublicMessage(
            @"\\192.168.100.201\KoXoDATA$ repository01")
        .Contains("avertissement technique", StringComparison.Ordinal),
        "Les messages publics doivent masquer les details techniques Veeam.");
}

async Task VerifySignupStoresPriceFreeBillingV2SelectionAsync()
{
    // Le configurateur legacy calculait un prix au signup, ce qui obligeait a
    // verifier que le montant venu du navigateur etait bien ignore. Billing V2
    // rend l'invariant structurel : la selection ne transporte que des codes
    // catalogue, et le montant est etabli plus tard par le pricing engine.
    // Ce test garde les deux moities de cet invariant.
    var amountLikeProperty = typeof(BillingV2PublicSelection)
        .GetProperties()
        .FirstOrDefault(property =>
            property.Name.Contains("Cents", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Price", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Amount", StringComparison.OrdinalIgnoreCase));
    Ensure(
        amountLikeProperty is null,
        "La selection publique Billing V2 ne doit porter aucun montant : "
        + "le navigateur ne peut pas devenir une autorite tarifaire "
        + $"(propriete fautive : {amountLikeProperty?.Name}).");

    var authStore = CreateMockAuthenticationStore();
    var signupStore = new MockSignupStore();
    var disabledAdConfiguration = CreateDisabledAdConfiguration();
    var adMembershipStore = new MockAdGroupMembershipStore();
    var signupService = new SignupService(
        new MockSignupRepository(signupStore, authStore),
        new TestEmailDispatchService(),
        new PortalPasswordService(),
        new MockActiveDirectoryService(disabledAdConfiguration, adMembershipStore),
        new MockActiveDirectoryLinkRepository(),
        new MockAdGroupProvisioner(adMembershipStore),
        NewPendingPasswordStore(),
        new RecordingKoxoSyncWebhookTriggerService(),
        new SignupRuntimeConfiguration(true, 3, 10, 24, 24, false),
        NewApplicationSettingsService(),
        CreateMockEmailConfiguration(),
        disabledAdConfiguration,
        LoggerFactory.Create(_ => { }).CreateLogger<SignupService>());

    var selectionInput = new BillingV2PublicSelectionInput
    {
        PresetCode = "pack-dossier-securise",
        PaymentMode = BillingV2PaymentModes.Monthly,
        StoragePersonalTierCode = "STORAGE-PERSONAL-8",
        BackupPersonal = true,
        AdditionalUsers = 0
    };

    var result = await signupService.SubmitAsync(
        new SignupSubmitPayload(
            "Societe Selection",
            "Alice Martin",
            "billing-v2-selection@example.invalid",
            "0102030405",
            "Test de selection Billing V2.",
            new SignupCustomerData(
                "professional",
                "Societe Selection",
                "billing-selection@example.invalid",
                "0102030405",
                "1 rue du Test",
                null,
                "75001",
                "Paris",
                "FR"),
            new SignupUserData(
                "madame",
                "Alice",
                "Martin",
                "1990-01-02",
                null,
                "Alice Martin",
                "billing-v2-selection@example.invalid",
                "0102030405",
                true),
            "127.0.0.1",
            "api-smoke-test",
            selectionInput),
        "billing-v2-selection-test",
        CancellationToken.None);

    Ensure(
        result.Succeeded,
        "Le signup portant une selection Billing V2 valide doit etre accepte.");

    var storedSignup = signupStore.Rows.Values.Single();
    var storedSelection = storedSignup.BillingV2Selection;
    Ensure(
        storedSelection is not null
        && storedSelection.PresetCode == "pack-dossier-securise"
        && storedSelection.StoragePersonalTierCode == "STORAGE-PERSONAL-8"
        && storedSelection.BackupPersonal
        && storedSelection.PaymentMode == BillingV2PaymentModes.Monthly,
        "Le signup doit conserver la selection Billing V2 telle qu'elle a ete faite.");

    // Une formule sans engagement explicite retombe sur FLEX cote serveur :
    // le navigateur ne choisit pas non plus la remise d'engagement.
    Ensure(
        storedSelection?.CommitmentCode == "FLEX",
        "Une formule sans engagement explicite doit etre normalisee en FLEX par le serveur.");
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

        await VerifyDownloadsAsync(
            client,
            mockBaseUrl,
            sessionToken!,
            adminSessionToken);

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
        await VerifyManagedContentAsync(
            client,
            mockBaseUrl,
            sessionToken!,
            adminSessionToken,
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
        Ensure(
            servicesPayload.RootElement
                .EnumerateArray()
                .Select(service =>
                    $"{service.GetProperty("id").GetString()}|{service.GetProperty("status").GetString()}")
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(new[]
                {
                    "svc-personal-hosting-001|active",
                    "svc-backup-001|active",
                    "svc-vpn-001|pending",
                    "svc-rds-001|suspended",
                    "svc-support-001|active"
                }),
            "Le catalogue client mock ne contient pas les couples id/statut attendus.");

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

    VerifyRejectedProductionConfiguration(
        "STRIPE_SECRET_KEY",
        configuration =>
        {
            configuration["STRIPE_MODE"] = "live";
            configuration["STRIPE_SECRET_KEY"] = "sk_test_never_use";
            configuration["STRIPE_PUBLISHABLE_KEY"] = "pk_live_fake";
            configuration["STRIPE_WEBHOOK_SECRET"] = "whsec_not_a_real_secret";
        });

    VerifyRejectedProductionConfiguration(
        "STRIPE_PUBLISHABLE_KEY",
        configuration =>
        {
            configuration["STRIPE_MODE"] = "test";
            configuration["STRIPE_SECRET_KEY"] = "sk_test_fake";
            configuration["STRIPE_PUBLISHABLE_KEY"] = "pk_live_never_use";
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
        var mariaDbClientCustomerReference = loginPayload.RootElement
            .GetProperty("user")
            .GetProperty("customerReference")
            .GetString()
            ?? throw new InvalidOperationException(
                "Le login MariaDB ne retourne aucune reference client.");
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
        Ensure(
            servicesForWritePayload.RootElement
                .EnumerateArray()
                .All(service => service.GetProperty("id").GetString()
                    != "svc-personal-hosting-001"),
            "Le catalogue MariaDB ne doit pas contenir le service mock d'hébergement personnel.");
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
            mariaDbClientCustomerReference,
            workflowServiceRequestId,
            persistent: true,
            foreignCustomerId: isolationCustomerId);
        await VerifyManagedContentAsync(
            client,
            mariaDbBaseUrl,
            sessionToken,
            adminSessionToken,
            persistent: true);

        adLinkFixtureId = await InsertCustomerAdLinkAsync(mariaDbClientCustomerReference);
        using var adLinksRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/admin/customers/{mariaDbClientCustomerReference}/ad-links",
            adminSessionToken);
        using var adLinksResponse = await client.SendAsync(adLinksRequest);
        using var adLinksPayload = JsonDocument.Parse(
            await adLinksResponse.Content.ReadAsStringAsync());
        Ensure(
            adLinksResponse.StatusCode == HttpStatusCode.OK
            && adLinksPayload.RootElement.EnumerateArray().Any(item =>
                item.GetProperty("id").GetString() == adLinkFixtureId
                && item.GetProperty("customerReference").GetString()
                    == mariaDbClientCustomerReference
                && !string.IsNullOrWhiteSpace(
                    item.GetProperty("objectGuid").GetString())),
            "La lecture MariaDB des liens AD doit rester lisible aprÃ¨s insertion d'un lien.");

        using var refreshedAdminCustomerDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{mariaDbBaseUrl}/internal/admin/customers/{mariaDbClientCustomerReference}",
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
            "La fiche client admin MariaDB doit rester lisible aprÃ¨s ajout d'un document commercial liÃ©.");
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

async Task VerifyDownloadsAsync(
    HttpClient client,
    string baseUrl,
    string clientSessionToken,
    string adminSessionToken)
{
    using var unauthenticatedResponse = await client.GetAsync(
        $"{baseUrl}/internal/portal/downloads");
    Ensure(
        unauthenticatedResponse.StatusCode == HttpStatusCode.Unauthorized,
        "Les téléchargements portail doivent refuser l'absence de session.");

    var categories = await GetJsonArrayAsync(
        CreateSessionRequest(
            HttpMethod.Get,
            $"{baseUrl}/internal/admin/download-categories",
            adminSessionToken),
        HttpStatusCode.OK,
        "La liste admin des catégories de téléchargement doit répondre HTTP 200.");
    Ensure(
        categories.GetArrayLength() >= 5,
        "Les catégories de téléchargement par défaut doivent être semées.");

    var defaultCategoryId = categories.EnumerateArray()
            .FirstOrDefault(category =>
                category.GetProperty("slug").GetString() == "logiciels")
            .GetProperty("id")
            .GetString()
        ?? throw new InvalidOperationException(
            "La catégorie par défaut 'logiciels' est introuvable.");

    var hiddenDownloadId = await CreateDownloadAsync(new
    {
        categoryId = defaultCategoryId,
        title = "RDP suspendu",
        shortDescription =
            "Fichier réservé à un service suspendu pour le test d'autorisation.",
        resourceType = "rdp",
        sourceKind = "external_url",
        visibilityMode = "targeted",
        status = "active",
        externalUrl = "https://downloads.example.invalid/rdp-hidden.rdp",
        versionLabel = "v1",
        installationInstructions = "À utiliser uniquement après réactivation.",
        displayOrder = 10,
        visibilityRules = new[]
        {
            new
            {
                targetType = "service_type",
                targetValue = "rds"
            }
        }
    });
    var unauthorizedServiceDownloadId = await CreateDownloadAsync(new
    {
        categoryId = defaultCategoryId,
        title = "Outil de supervision",
        shortDescription =
            "Outil réservé à un service de supervision absent des droits du client.",
        resourceType = "software",
        sourceKind = "external_url",
        visibilityMode = "targeted",
        status = "active",
        externalUrl = "https://downloads.example.invalid/monitoring-tool.exe",
        versionLabel = "v1",
        installationInstructions = "Installer uniquement pour un service supervisé.",
        displayOrder = 15,
        visibilityRules = new[]
        {
            new
            {
                targetType = "service_type",
                targetValue = "monitoring"
            }
        }
    });
    var externalDownloadId = await CreateDownloadAsync(new
    {
        categoryId = defaultCategoryId,
        title = "Guide support distant",
        shortDescription =
            "Documentation visible pour les services support actifs.",
        resourceType = "document",
        sourceKind = "external_url",
        visibilityMode = "targeted",
        status = "active",
        externalUrl = "https://downloads.example.invalid/guide-support.pdf",
        versionLabel = "2026.07",
        installationInstructions = "Ouvrir le guide avant la première connexion.",
        displayOrder = 20,
        visibilityRules = new[]
        {
            new
            {
                targetType = "service_type",
                targetValue = "support"
            }
        }
    });
    var internalDownloadPayload = new
    {
        categoryId = defaultCategoryId,
        title = "Script support sécurisé",
        shortDescription =
            "Script interne visible pour les services support actifs.",
        resourceType = "script",
        sourceKind = "internal_file",
        visibilityMode = "targeted",
        status = "inactive",
        externalUrl = (string?)null,
        versionLabel = "2.0.0",
        installationInstructions = "Exécuter le script avec les droits habituels.",
        displayOrder = 30,
        visibilityRules = new[]
        {
            new
            {
                targetType = "service_type",
                targetValue = "support"
            }
        }
    };
    var internalDownloadId = await CreateDownloadAsync(internalDownloadPayload);

    var internalDetailBeforeUpload = await GetJsonObjectAsync(
        CreateSessionRequest(
            HttpMethod.Get,
            $"{baseUrl}/internal/admin/downloads/{internalDownloadId}",
            adminSessionToken),
        HttpStatusCode.OK,
        "La fiche admin du téléchargement interne doit être lisible.");
    Ensure(
        internalDetailBeforeUpload.GetProperty("sourceKind").GetString()
            == "internal_file"
        && internalDetailBeforeUpload.GetProperty("status").GetString()
            == "inactive",
        "Le téléchargement interne doit démarrer inactif avant upload.");

    await UploadDownloadFileAsync(
        client,
        baseUrl,
        adminSessionToken,
        internalDownloadId,
        "support-script-v1.ps1",
        "Write-Output 'v1'");
    await UploadDownloadFileAsync(
        client,
        baseUrl,
        adminSessionToken,
        internalDownloadId,
        "support-script-v2.ps1",
        "Write-Output 'v2'");

    using (var activateRequest = CreateSessionRequest(
               HttpMethod.Patch,
               $"{baseUrl}/internal/admin/downloads/{internalDownloadId}",
               adminSessionToken))
    {
        activateRequest.Content = JsonContent.Create(new
        {
            categoryId = defaultCategoryId,
            title = "Script support sécurisé",
            shortDescription =
                "Script interne visible pour les services support actifs.",
            resourceType = "script",
            sourceKind = "internal_file",
            visibilityMode = "targeted",
            status = "active",
            externalUrl = (string?)null,
            versionLabel = "2.0.1",
            installationInstructions =
                "Exécuter le script avec les droits habituels.",
            displayOrder = 30,
            visibilityRules = new[]
            {
                new
                {
                    targetType = "service_type",
                    targetValue = "support"
                }
            }
        });
        using var activateResponse = await client.SendAsync(activateRequest);
        using var activatePayload = JsonDocument.Parse(
            await activateResponse.Content.ReadAsStringAsync());
            Ensure(
            activateResponse.StatusCode == HttpStatusCode.OK,
            "L'activation du téléchargement interne après upload doit réussir.");
            Ensure(
            activatePayload.RootElement.GetProperty("changed").GetBoolean(),
            "L'activation du téléchargement interne doit être marquée changed.");
    }

    var adminDownloads = await GetJsonArrayAsync(
        CreateSessionRequest(
            HttpMethod.Get,
            $"{baseUrl}/internal/admin/downloads",
            adminSessionToken),
        HttpStatusCode.OK,
        "La liste admin des téléchargements doit répondre HTTP 200.");
    Ensure(
        adminDownloads.EnumerateArray().Any(item =>
            item.GetProperty("id").GetString() == hiddenDownloadId)
        && adminDownloads.EnumerateArray().Any(item =>
            item.GetProperty("id").GetString() == unauthorizedServiceDownloadId)
        && adminDownloads.EnumerateArray().Any(item =>
            item.GetProperty("id").GetString() == externalDownloadId)
        && adminDownloads.EnumerateArray().Any(item =>
            item.GetProperty("id").GetString() == internalDownloadId),
        "La liste admin doit exposer les téléchargements créés.");

    using (var categoryDeleteRequest = CreateSessionRequest(
               HttpMethod.Delete,
               $"{baseUrl}/internal/admin/download-categories/{defaultCategoryId}",
               adminSessionToken))
    {
        using var categoryDeleteResponse = await client.SendAsync(
            categoryDeleteRequest);
        var categoryDeleteText = await categoryDeleteResponse.Content
            .ReadAsStringAsync();
            Ensure(
            categoryDeleteResponse.StatusCode == HttpStatusCode.Conflict,
            "Une catégorie utilisée doit refuser la suppression.");
            Ensure(
            categoryDeleteText.TrimStart().StartsWith("{", StringComparison.Ordinal),
            $"Le refus de suppression de catÃ©gorie doit retourner du JSON. ReÃ§u: {categoryDeleteText}");
        using var categoryDeletePayload = JsonDocument.Parse(categoryDeleteText);
        Ensure(
            categoryDeletePayload.RootElement.GetProperty("code").GetString()
                == "DOWNLOAD_CATEGORY_NOT_EMPTY",
            "Le refus de suppression de catégorie doit exposer le code attendu.");
    }

    var portalDownloads = await GetJsonArrayAsync(
        CreateSessionRequest(
            HttpMethod.Get,
            $"{baseUrl}/internal/portal/downloads",
            clientSessionToken),
        HttpStatusCode.OK,
        "La liste portail des téléchargements doit répondre HTTP 200.");
    var visibleDownloadIds = portalDownloads.EnumerateArray()
        .SelectMany(category => category.GetProperty("items").EnumerateArray())
        .Select(item => item.GetProperty("id").GetString())
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .ToHashSet(StringComparer.Ordinal);
    Ensure(
        visibleDownloadIds.Contains(externalDownloadId)
        && visibleDownloadIds.Contains(internalDownloadId)
        && !visibleDownloadIds.Contains(hiddenDownloadId)
        && !visibleDownloadIds.Contains(unauthorizedServiceDownloadId),
        "Le portail doit filtrer les téléchargements selon les droits actifs.");

    using (var hiddenFileRequest = CreateSessionRequest(
               HttpMethod.Get,
               $"{baseUrl}/internal/portal/downloads/{hiddenDownloadId}/file",
               clientSessionToken))
    {
        using var hiddenFileResponse = await client.SendAsync(hiddenFileRequest);
        Ensure(
            hiddenFileResponse.StatusCode == HttpStatusCode.NotFound,
            "Un téléchargement non autorisé doit répondre HTTP 404.");
    }

    using (var unauthorizedServiceFileRequest = CreateSessionRequest(
               HttpMethod.Get,
               $"{baseUrl}/internal/portal/downloads/{unauthorizedServiceDownloadId}/file",
               clientSessionToken))
    {
        using var unauthorizedServiceFileResponse = await client.SendAsync(
            unauthorizedServiceFileRequest);
        Ensure(
            unauthorizedServiceFileResponse.StatusCode == HttpStatusCode.NotFound,
            "Un téléchargement ciblé sur un service absent des droits du client doit répondre HTTP 404.");
    }

    using (var redirectHandler = new HttpClientHandler
           {
               UseProxy = false,
               AllowAutoRedirect = false
           })
    using (var redirectClient = new HttpClient(redirectHandler))
    using (var externalFileRequest = CreateSessionRequest(
               HttpMethod.Get,
               $"{baseUrl}/internal/portal/downloads/{externalDownloadId}/file",
               clientSessionToken))
    {
        using var externalFileResponse = await redirectClient.SendAsync(
            externalFileRequest);
        Ensure(
            externalFileResponse.StatusCode == HttpStatusCode.Redirect,
            "Un téléchargement externe visible doit répondre par redirection.");
        Ensure(
            string.Equals(
                externalFileResponse.Headers.Location?.ToString(),
                "https://downloads.example.invalid/guide-support.pdf",
                StringComparison.Ordinal),
            "La redirection externe doit conserver l'URL configurée.");
    }

    using (var internalFileRequest = CreateSessionRequest(
               HttpMethod.Get,
               $"{baseUrl}/internal/portal/downloads/{internalDownloadId}/file",
               clientSessionToken))
    {
        using var internalFileResponse = await client.SendAsync(internalFileRequest);
        Ensure(
            internalFileResponse.StatusCode == HttpStatusCode.OK,
            "Le téléchargement interne visible doit répondre HTTP 200.");
        var internalFileBody = await internalFileResponse.Content.ReadAsStringAsync();
        Ensure(
            internalFileBody.Contains("v2", StringComparison.Ordinal),
            "Le binaire servi doit être le dernier fichier uploadé.");
    }

    using (var deleteFileRequest = CreateSessionRequest(
               HttpMethod.Delete,
               $"{baseUrl}/internal/admin/downloads/{internalDownloadId}/file",
               adminSessionToken))
    {
        using var deleteFileResponse = await client.SendAsync(deleteFileRequest);
        Ensure(
            deleteFileResponse.StatusCode == HttpStatusCode.OK,
            "La suppression du fichier interne doit répondre HTTP 200.");
    }

    var internalDetailAfterDelete = await GetJsonObjectAsync(
        CreateSessionRequest(
            HttpMethod.Get,
            $"{baseUrl}/internal/admin/downloads/{internalDownloadId}",
            adminSessionToken),
        HttpStatusCode.OK,
        "La fiche admin du téléchargement interne doit rester lisible après suppression du fichier.");
    Ensure(
        internalDetailAfterDelete.GetProperty("status").GetString()
            == "inactive"
        && internalDetailAfterDelete.GetProperty("fileOriginalName").ValueKind
            == JsonValueKind.Null,
        "La suppression du fichier doit désactiver la ressource interne.");

    foreach (var resourceId in new[]
    {
        hiddenDownloadId,
        unauthorizedServiceDownloadId,
        externalDownloadId,
        internalDownloadId
    })
    {
        using var deleteRequest = CreateSessionRequest(
            HttpMethod.Delete,
            $"{baseUrl}/internal/admin/downloads/{resourceId}",
            adminSessionToken);
        using var deleteResponse = await client.SendAsync(deleteRequest);
        Ensure(
            deleteResponse.StatusCode == HttpStatusCode.OK,
            $"La suppression admin du téléchargement {resourceId} doit réussir.");
    }

    async Task<string> CreateDownloadAsync(object payload)
    {
        using var request = CreateSessionRequest(
            HttpMethod.Post,
            $"{baseUrl}/internal/admin/downloads",
            adminSessionToken);
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Ensure(
            response.StatusCode == HttpStatusCode.OK,
            "La création admin d'un téléchargement doit répondre HTTP 200.");
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException(
                "La création admin d'un téléchargement ne retourne aucun identifiant.");
    }

    async Task UploadDownloadFileAsync(
        HttpClient httpClient,
        string currentBaseUrl,
        string currentAdminSessionToken,
        string resourceId,
        string fileName,
        string fileContent)
    {
        using var request = CreateSessionRequest(
            HttpMethod.Post,
            $"{currentBaseUrl}/internal/admin/downloads/{resourceId}/file",
            currentAdminSessionToken);
        using var multipart = new MultipartFormDataContent();
        using var fileBytes = new ByteArrayContent(
            Encoding.UTF8.GetBytes(fileContent));
        fileBytes.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(fileBytes, "file", fileName);
        request.Content = multipart;
        using var response = await httpClient.SendAsync(request);
        using var payload = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Ensure(
            response.StatusCode == HttpStatusCode.OK,
            "L'upload du fichier de téléchargement doit répondre HTTP 200.");
        Ensure(
            payload.RootElement.GetProperty("changed").GetBoolean(),
            "L'upload du fichier de téléchargement doit être marqué changed.");
    }

    async Task<JsonElement> GetJsonArrayAsync(
        HttpRequestMessage request,
        HttpStatusCode expectedStatusCode,
        string failureMessage)
    {
        using (request)
        {
            using var response = await client.SendAsync(request);
            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());
            Ensure(response.StatusCode == expectedStatusCode, failureMessage);
        Ensure(
            document.RootElement.ValueKind == JsonValueKind.Array,
            "Le payload JSON attendu doit être un tableau.");
            return document.RootElement.Clone();
        }
    }

    async Task<JsonElement> GetJsonObjectAsync(
        HttpRequestMessage request,
        HttpStatusCode expectedStatusCode,
        string failureMessage)
    {
        using (request)
        {
            using var response = await client.SendAsync(request);
            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());
            Ensure(response.StatusCode == expectedStatusCode, failureMessage);
        Ensure(
            document.RootElement.ValueKind == JsonValueKind.Object,
            "Le payload JSON attendu doit être un objet.");
            return document.RootElement.Clone();
        }
    }
}

async Task RunKoxoExportHttpTestsAsync()
{
    var baseUrl = SmokeTestRuntimeHelpers.CreateLoopbackBaseUrl();
    const string serviceAuthToken = "NOT_A_REAL_KOXO_SERVICE_AUTH_VALUE_V040";
    using var api = StartApi(
        baseUrl,
        startInfo =>
        {
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Staging";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Staging";
            startInfo.Environment["SQL_PROVIDER"] = "mariadb";
            startInfo.Environment["SQL_HOST"] = "127.0.0.1";
            startInfo.Environment["SQL_PORT"] = "3306";
            startInfo.Environment["SQL_DATABASE"] = "koxo-auth-guard";
            startInfo.Environment["SQL_USERNAME"] = "koxo-auth-guard";
            startInfo.Environment["SQL_PASSWORD"] =
                "NOT_A_REAL_SQL_KOXO_AUTH_VALUE_V040";
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
            "Le health check staging doit repondre pour tester l'export KoXo.");

        using var missingHeaderResponse = await client.GetAsync(
            $"{baseUrl}/internal/koxo/users");
        using var missingHeaderPayload = JsonDocument.Parse(
            await missingHeaderResponse.Content.ReadAsStringAsync());
        Ensure(
            missingHeaderResponse.StatusCode == HttpStatusCode.Unauthorized
            && missingHeaderPayload.RootElement.GetProperty("code").GetString()
                == "SERVICE_AUTH_REQUIRED",
            "L'export KoXo prive doit exiger X-Service-Auth hors Development.");

        using var invalidHeaderRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/internal/koxo/users");
        invalidHeaderRequest.Headers.Add(
            serviceAuthHeader,
            "NOT_A_REAL_INVALID_KOXO_SERVICE_AUTH_V040");
        using var invalidHeaderResponse = await client.SendAsync(
            invalidHeaderRequest);
        using var invalidHeaderPayload = JsonDocument.Parse(
            await invalidHeaderResponse.Content.ReadAsStringAsync());
        Ensure(
            invalidHeaderResponse.StatusCode == HttpStatusCode.Unauthorized
            && invalidHeaderPayload.RootElement.GetProperty("code").GetString()
                == "SERVICE_AUTH_REQUIRED",
            "Un X-Service-Auth invalide doit etre refuse sur /internal/koxo/users.");

        using var validHeaderRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/internal/koxo/users");
        validHeaderRequest.Headers.Add(serviceAuthHeader, serviceAuthToken);
        validHeaderRequest.Headers.Add(correlationHeader, "v0.40-koxo-http");
        using var validHeaderResponse = await client.SendAsync(validHeaderRequest);
        var validHeaderText = await validHeaderResponse.Content.ReadAsStringAsync();
        Ensure(
            !validHeaderText.Contains(
                "SERVICE_AUTH_REQUIRED",
                StringComparison.Ordinal)
            && !validHeaderText.Contains(
                "password",
                StringComparison.OrdinalIgnoreCase),
            "Un X-Service-Auth valide ne doit pas etre rejete par le garde KoXo ni exposer de mot de passe.");
    }
    finally
    {
        await api.StopAsync();
    }
}

async Task RunKoxoExportServiceTestsAsync()
{
    var sortableRepository = new InMemoryKoxoRepository(
    [
        new KoxoExportCandidate(
            "portal-user-2",
            "CLI-Z",
            "CLI-000002",
            "madame",
            "Beta",
            "Anne",
            "1988-03-12",
            "anne.beta@example.invalid"),
        new KoxoExportCandidate(
            "portal-user-1",
            "CLI-A",
            "CLI-000010",
            "monsieur",
            "Alpha",
            "Zed",
            "1981-01-07",
            "zed.alpha@example.invalid"),
        new KoxoExportCandidate(
            "portal-user-3",
            "CLI-A",
            "CLI-000001",
            "madame",
            "Aardvark",
            "Zoe",
            "1992-10-02",
            "zoe.aardvark@example.invalid")
    ]);
    var sortableService = new KoxoExportService(sortableRepository, NewPendingPasswordStore());
    var sortablePayload = await sortableService.ExportAsync(
        "api",
        "v0.40-koxo-sort",
        "127.0.0.1",
        CancellationToken.None);

    Ensure(
        sortablePayload.SchemaVersion == 2
        && sortablePayload.UserCount == 3
        && sortablePayload.Users.Count == 3
        && DateTimeOffset.TryParse(sortablePayload.GeneratedAt, out _),
        "Le payload KoXo valide doit exposer schemaVersion=2, un generatedAt ISO et un userCount exact.");
    EnsureSequenceEqual(
        sortablePayload.Users.Select(user => user.IdentifiantUnique).ToArray(),
        ["CLI-000001", "CLI-000010", "CLI-000002"],
        "Le tri KoXo doit etre deterministe par groupe secondaire puis identifiant unique.");
    Ensure(
        !JsonSerializer.Serialize(sortablePayload)
            .Contains("password", StringComparison.OrdinalIgnoreCase),
        "Le payload KoXo ne doit jamais exposer de mot de passe.");

    var secondPayload = await sortableService.ExportAsync(
        "api",
        "v0.40-koxo-sort-repeat",
        "127.0.0.1",
        CancellationToken.None);
    EnsureSequenceEqual(
        secondPayload.Users.Select(user => user.IdentifiantUnique).ToArray(),
        sortablePayload.Users.Select(user => user.IdentifiantUnique).ToArray(),
        "Un export KoXo repete ne doit pas recalculer les identifiants uniques.");

    var splitRepository = new InMemoryKoxoRepository(
    [
        new KoxoExportCandidate(
            "portal-user-paying",
            "CLI-000001",
            "CLI-000001",
            "monsieur",
            "Payant",
            "Paul",
            "1980-01-02",
            "paul.payant@example.invalid"),
        new KoxoExportCandidate(
            "portal-user-trial",
            "DEMO-abcdef0123456789abcdef",
            "CLI-000002",
            "madame",
            "Essai",
            "Emma",
            "1990-05-06",
            "emma.essai@example.invalid",
            IsDemo: true,
            KoxoGroupReference: "CLI-000042"),
        new KoxoExportCandidate(
            "portal-user-legacy-trial",
            "DEMO-fedcba9876543210fedcba",
            "CLI-000003",
            "madame",
            "Ancienne",
            "Alice",
            "1975-11-30",
            "alice.ancienne@example.invalid",
            IsDemo: true)
    ]);
    var splitService = new KoxoExportService(splitRepository, NewPendingPasswordStore());
    var splitPayload = await splitService.ExportAsync(
        "api",
        "v1.1-koxo-split",
        "127.0.0.1",
        CancellationToken.None);

    var payingUser = splitPayload.Users.Single(user =>
        user.IdentifiantUnique == "CLI-000001");
    var trialUser = splitPayload.Users.Single(user =>
        user.IdentifiantUnique == "CLI-000002");
    var legacyTrialUser = splitPayload.Users.Single(user =>
        user.IdentifiantUnique == "CLI-000003");

    Ensure(
        payingUser.GroupePrimaire == "CLIENTS"
        && trialUser.GroupePrimaire == "CLIENTS DÉMO"
        && legacyTrialUser.GroupePrimaire == "CLIENTS DÉMO",
        "L'export KoXo doit aiguiller chaque identite vers le groupe primaire de son profil.");

    Ensure(
        splitPayload.Users.All(user =>
            user.GroupePrimaire is "CLIENTS" or "CLIENTS DÉMO"),
        "Aucune identite ne doit porter un groupe primaire inconnu : elle n'atteindrait aucun CSV, donc passerait pour orpheline et serait desactivee.");

    // Le prefixe n'est pas decoratif : KoXo ne cree un groupe secondaire que
    // s'il est nouveau pour sa propre base. Avec le meme nom des deux cotes de
    // la separation, l'identite migree perd son groupe DEFINITIVEMENT.
    Ensure(
        trialUser.GroupeSecondaire == "DEMO-CLI-000042"
        && payingUser.GroupeSecondaire == "CLI-000001"
        && trialUser.GroupeSecondaire != payingUser.GroupeSecondaire,
        "Le groupe secondaire d'un essai doit deriver du code reserve avec un prefixe qui le distingue de l'OU definitive.");

    Ensure(
        legacyTrialUser.GroupeSecondaire == "DEMO-CLI-DEMO",
        "Un essai cree avant la reservation systematique d'un code doit retomber sur l'OU commune, prefixee — l'exclure le ferait passer pour orphelin.");

    var invalidRepository = new InMemoryKoxoRepository(
    [
        new KoxoExportCandidate(
            "portal-user-4",
            "CLI-B",
            "CLI-000111",
            "madame",
            "Valide",
            "Alice",
            "1990-04-15",
            "alice.valide@example.invalid"),
        new KoxoExportCandidate(
            "portal-user-5",
            "CLI-B",
            "CLI-000111",
            "monsieur",
            "Doublon",
            "Bob",
            "1989-06-20",
            "bob.doublon@example.invalid"),
        new KoxoExportCandidate(
            "portal-user-6",
            "CLI-C",
            "CLI-000222",
            "autre",
            "SansDate",
            "Charlie",
            null,
            "charlie.sansdate@example.invalid")
    ]);
    var invalidService = new KoxoExportService(invalidRepository, NewPendingPasswordStore());
    KoxoValidationException? validationException = null;
    try
    {
        await invalidService.ExportAsync(
            "api",
            "v0.40-koxo-invalid",
            "127.0.0.1",
            CancellationToken.None);
    }
    catch (KoxoValidationException exception)
    {
        validationException = exception;
    }

    Ensure(
        validationException is not null
        && validationException.InvalidUsers.Count == 3
        && validationException.InvalidUsers.Count(user =>
            user.Fields.Contains("identifiantUnique", StringComparer.Ordinal)) == 2
        && validationException.InvalidUsers.Any(user =>
            user.Fields.Contains("civilite", StringComparer.Ordinal)
            && user.Fields.Contains("dateNaissance", StringComparer.Ordinal)),
        "Les doublons et invalidites KoXo doivent bloquer globalement l'export avec une erreur structuree.");

    var invalidDashboard = await invalidService.ValidateAndRecordAsync(
        "v0.40-koxo-dashboard",
        "127.0.0.1",
        CancellationToken.None);
    Ensure(
        invalidDashboard.InvalidUserCount == 3
        && invalidDashboard.ExportableUserCount == 0
        && invalidDashboard.LastRun is not null
        && string.Equals(
            invalidDashboard.LastRun.Status,
            "validation_failed",
            StringComparison.Ordinal)
        && invalidDashboard.Preview is null,
        "La validation KoXo admin doit enregistrer un audit persistant et refuser toute reponse partielle.");

    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DEMO_PORTAL_EMAIL"] = mockEmail,
            ["DEMO_PORTAL_PASSWORD"] = mockPassword,
            ["DEMO_PORTAL_STATUS"] = "active",
            ["DEMO_INTERNAL_ADMIN_EMAIL"] = mockAdminEmail,
            ["DEMO_INTERNAL_ADMIN_PASSWORD"] = mockAdminPassword
        })
        .Build();
    var authStore = new MockAuthenticationStore(
        configuration,
        new PortalPasswordService());
    var signupStore = new MockSignupStore();
    var signupRepository = new MockSignupRepository(signupStore, authStore);
    const string signupId = "signup-v040-koxo";
    await signupRepository.InsertPendingAsync(
        new SignupInsert(
            signupId,
            "Client test KoXo",
            "Alice Stable",
            "alice.stable@example.invalid",
            "0102030405",
            "Validation KoXo",
            new SignupCustomerData(
                "professionnel",
                "Client test KoXo",
                "alice.stable@example.invalid",
                "0102030405",
                "1 rue de la Stabilite",
                null,
                "29000",
                "Quimper",
                "France"),
            new SignupUserData(
                "madame",
                "Alice",
                "Stable",
                "1991-09-14",
                "AS",
                "Alice Stable",
                "alice.stable@example.invalid",
                "0102030405",
                true),
            "verification-hash",
            DateTime.UtcNow.AddHours(4),
            "127.0.0.1",
            "SmokeTests"),
        CancellationToken.None);
    await signupRepository.MarkEmailVerifiedAsync(signupId, CancellationToken.None);
    var approval = await signupRepository.ApproveAsync(
        new SignupApprovalRequest(
            signupId,
            "customer-v040-koxo",
            "CLI-DEMO-0042",
            new SignupCustomerData(
                "professionnel",
                "Client test KoXo",
                "alice.stable@example.invalid",
                "0102030405",
                "1 rue de la Stabilite",
                null,
                "29000",
                "Quimper",
                "France"),
            new SignupUserData(
                "madame",
                "Alice",
                "Stable",
                "1991-09-14",
                "AS",
                "Alice Stable",
                "alice.stable@example.invalid",
                "0102030405",
                true),
            "portal-user-v040-koxo",
            "password-hash",
            DateTime.UtcNow.AddHours(24)),
        CancellationToken.None);
    Ensure(
        approval is not null
        && string.Equals(
            approval.KoxoUniqueIdentifier,
            signupStore.Rows[signupId].ApprovedUserKoxoUniqueIdentifier,
            StringComparison.Ordinal)
        && approval.KoxoUniqueIdentifier.StartsWith(
            "CLI-",
            StringComparison.Ordinal),
        "L'identifiant unique KoXo doit etre attribue une fois a l'approbation et conserve en persistance.");
}

async Task RunKoxoSyncWebhookTriggerServiceTestsAsync()
{
    var captured = new CapturedRequestHandler((request, body) =>
    {
        var payload = JsonDocument.Parse(body);
        Ensure(
            request.Method == HttpMethod.Post,
            "Le webhook KoXo doit utiliser POST.");
        Ensure(
            request.RequestUri?.AbsoluteUri
                == "https://srv-21.example.invalid/internal/koxo/sync",
            "Le webhook KoXo doit viser l'URL configuree.");
        Ensure(
            request.Headers.Authorization?.Scheme == "Bearer"
            && request.Headers.Authorization.Parameter
                == "NOT_A_REAL_KOXO_SYNC_WEBHOOK_TOKEN_V041",
            "Le webhook KoXo doit porter un bearer token dedie.");
        Ensure(
            payload.RootElement.GetProperty("trigger").GetString()
                == "password_set"
            && payload.RootElement.GetProperty("portalUserId").GetString()
                == "portal-user-v041-koxo",
            "Le payload du webhook KoXo doit contenir le contexte du compte.");
        Ensure(
            !body.Contains("NOT_A_REAL_PASSWORD", StringComparison.Ordinal),
            "Le webhook KoXo ne doit jamais exposer le mot de passe.");
    });
    using var client = new HttpClient(captured);
    var loggerFactory = LoggerFactory.Create(_ => { });
    var service = new KoxoSyncWebhookTriggerService(
        new KoxoSyncWebhookRuntimeConfiguration(
            new Uri("https://srv-21.example.invalid/internal/koxo/sync"),
            "NOT_A_REAL_KOXO_SYNC_WEBHOOK_TOKEN_V041",
            TimeSpan.FromSeconds(5),
            false),
        new SingleHttpClientFactory(client),
        loggerFactory.CreateLogger<KoxoSyncWebhookTriggerService>());

    await service.TriggerAsync(
        new KoxoSyncWebhookTriggerRequest(
            "signup-v041-koxo",
            "portal-user-v041-koxo",
            "CLI-DEMO-0041",
            "password_set",
            "corr-v041-koxo",
            DateTime.UtcNow.ToString("O")),
        CancellationToken.None);

    Ensure(
        captured.RequestCount == 1,
        "Le service webhook KoXo doit emettre exactement une requete.");
}

async Task RunSignupKoxoWebhookTriggerTestsAsync()
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DEMO_PORTAL_EMAIL"] = mockEmail,
            ["DEMO_PORTAL_PASSWORD"] = mockPassword,
            ["DEMO_PORTAL_STATUS"] = "active",
            ["DEMO_INTERNAL_ADMIN_EMAIL"] = mockAdminEmail,
            ["DEMO_INTERNAL_ADMIN_PASSWORD"] = mockAdminPassword
        })
        .Build();
    var authStore = new MockAuthenticationStore(
        configuration,
        new PortalPasswordService());
    var signupStore = new MockSignupStore();
    var signupRepository = new MockSignupRepository(signupStore, authStore);
    var trigger = new RecordingKoxoSyncWebhookTriggerService();
    const string signupId = "signup-v041-koxo-trigger";
    const string userId = "portal-user-v041-koxo-trigger";
    const string customerId = "customer-v041-koxo-trigger";
    const string customerReference = "CLI-DEMO-0042";
    const string passwordToken = "token-v041-koxo-trigger";

    await signupRepository.InsertPendingAsync(
        new SignupInsert(
            signupId,
            "Client trigger KoXo",
            "Alice Trigger",
            "alice.trigger@example.invalid",
            "0102030405",
            "Trigger KoXo",
            new SignupCustomerData(
                "professionnel",
                "Client trigger KoXo",
                "alice.trigger@example.invalid",
                "0102030405",
                "1 rue du Trigger",
                null,
                "29000",
                "Quimper",
                "France"),
            new SignupUserData(
                "madame",
                "Alice",
                "Trigger",
                "1990-01-02",
                "AT",
                "Alice Trigger",
                "alice.trigger@example.invalid",
                "0102030405",
                true),
            "verification-hash-trigger",
            DateTime.UtcNow.AddHours(4),
            "127.0.0.1",
            "SmokeTests"),
        CancellationToken.None);
    await signupRepository.MarkEmailVerifiedAsync(signupId, CancellationToken.None);
    await signupRepository.ApproveAsync(
        new SignupApprovalRequest(
            signupId,
            customerId,
            customerReference,
            new SignupCustomerData(
                "professionnel",
                "Client trigger KoXo",
                "alice.trigger@example.invalid",
                "0102030405",
                "1 rue du Trigger",
                null,
                "29000",
                "Quimper",
                "France"),
            new SignupUserData(
                "madame",
                "Alice",
                "Trigger",
                "1990-01-02",
                "AT",
                "Alice Trigger",
                "alice.trigger@example.invalid",
                "0102030405",
                true),
            userId,
            HashTokenForTests(passwordToken),
            DateTime.UtcNow.AddHours(24)),
        CancellationToken.None);

    var adConfiguration = new AdRuntimeConfiguration(
        AdIntegrationMode.Mock,
        "clients.home.bzh",
        "OU=Clients,DC=clients,DC=home,DC=bzh",
        "OU=Clients,DC=clients,DC=home,DC=bzh",
        ["OU=Clients,DC=clients,DC=home,DC=bzh"],
        false,
        null,
        null,
        3000,
        5000,
        25,
        true);
    var adMembershipStore = new MockAdGroupMembershipStore();
    var service = new SignupService(
        signupRepository,
        new TestEmailDispatchService(),
        new PortalPasswordService(),
        new MockActiveDirectoryService(
            adConfiguration,
            adMembershipStore),
        new MockActiveDirectoryLinkRepository(),
        new MockAdGroupProvisioner(adMembershipStore),
        NewPendingPasswordStore(),
        trigger,
        new SignupRuntimeConfiguration(true, 3, 1, 24, 24, false),
        NewApplicationSettingsService(),
        new EmailRuntimeConfiguration(
            EmailIntegrationMode.Mock,
            "smtp.example.invalid",
            25,
            false,
            null,
            null,
            "noreply@example.invalid",
            "Kermaria",
            "https://portal.example.invalid",
            "contact@example.invalid",
            10000,
            false,
            Array.Empty<string>(),
            true),
        adConfiguration,
        LoggerFactory.Create(_ => { }).CreateLogger<SignupService>());

    var result = await service.SetPasswordAsync(
        passwordToken,
        "NOT_A_REAL_PASSWORD_V041",
        CancellationToken.None);

    Ensure(
        result.Succeeded
        && result.Code == "PASSWORD_SET",
        "Le mot de passe doit rester defini meme si le webhook KoXo est un effet de bord.");
    Ensure(
        trigger.Requests.Count == 1
        && trigger.Requests[0].SignupId == signupId
        && trigger.Requests[0].PortalUserId == userId
        && trigger.Requests[0].CustomerReference == customerReference,
        "Le set-password doit declencher exactement une notification KoXo pour SRV-21.");
}

// Communications administrables (specification, section 8). Les smoke tests
// tournent en persistance mock : ils valident les regles de service (whitelist
// fermee, bornes, concurrence, repli code) et non le SQL, qui reste couvert par
// `validate:mariadb`.
static async Task VerifyCommunicationTemplatesAsync()
{
    var repository = new MockCommunicationTemplateRepository();
    var service = new CommunicationTemplateService(
        repository,
        LoggerFactory.Create(_ => { }).CreateLogger<CommunicationTemplateService>());
    CommunicationTemplateService.Invalidate();
    const string actor = "00000000-0000-0000-0000-0000000000aa";
    const string correlation = "smoke-communications";
    var token = CancellationToken.None;

    var collection = await service.GetAdminCollectionAsync(token);
    Ensure(
        collection.EmailTemplates.Count == 7,
        "Les sept modeles e-mail du cadrage doivent etre exposes.");
    Ensure(
        collection.EmailTemplates.All(item => !item.Customized && item.Source == "code"),
        "Sans ligne enregistree, chaque modele doit se declarer par defaut.");
    Ensure(
        collection.Snippets.Count > 0 && collection.NotificationTemplates.Count > 0,
        "Notifications et textes systeme doivent aussi etre exposes.");

    var unknownKey = await service.UpdateEmailTemplateAsync(
        "modele_inexistant",
        new EmailTemplateUpdateRequest("Objet", "Corps", true, 0),
        actor,
        correlation,
        token);
    Ensure(
        unknownKey.Code == "TEMPLATE_UNKNOWN_KEY",
        "Une cle hors registre doit etre refusee, jamais creee.");

    var unknownVariable = await service.UpdateEmailTemplateAsync(
        "signup_verification",
        new EmailTemplateUpdateRequest(
            "Objet",
            "Bonjour {{contactName}}, jeton {{secretToken}}.",
            true,
            0),
        actor,
        correlation,
        token);
    Ensure(
        unknownVariable.Code == "TEMPLATE_UNKNOWN_VARIABLE",
        "Une variable hors whitelist doit faire echouer la sauvegarde.");

    var tooLong = await service.UpdateSnippetAsync(
        "contact_form_confirmation",
        new SystemSnippetUpdateRequest(new string('x', 5000), 0),
        actor,
        correlation,
        token);
    Ensure(
        tooLong.Code == "TEMPLATE_TOO_LONG",
        "Un texte systeme au-dela de sa borne doit etre refuse.");

    var saved = await service.UpdateEmailTemplateAsync(
        "signup_verification",
        new EmailTemplateUpdateRequest(
            "Objet administre",
            "Bonjour {{contactName}}, lien : {{verificationUrl}}",
            true,
            0),
        actor,
        correlation,
        token);
    Ensure(
        saved.Code == "TEMPLATE_UPDATED"
        && saved.Template is { Version: 1, Customized: true, Source: "database" },
        "Un enregistrement valide doit produire la version 1 en base.");

    var stale = await service.UpdateEmailTemplateAsync(
        "signup_verification",
        new EmailTemplateUpdateRequest("Autre objet", "Bonjour {{contactName}}", true, 0),
        actor,
        correlation,
        token);
    Ensure(
        stale.Code == "TEMPLATE_VERSION_CONFLICT",
        "Une version attendue perimee doit etre refusee, pas ecrasee.");

    var rendered = await service.RenderEmailAsync(
        "signup_verification",
        EmailTemplates.SignupVerificationVariables("Alice", "https://portail.invalid/v/1"),
        token);
    Ensure(
        rendered.Subject == "Objet administre"
        && rendered.Body.Contains("Alice", StringComparison.Ordinal)
        && rendered.Body.Contains("https://portail.invalid/v/1", StringComparison.Ordinal),
        "Le runtime doit utiliser le modele administre et substituer ses variables.");

    var disabled = await service.UpdateEmailTemplateAsync(
        "signup_verification",
        new EmailTemplateUpdateRequest(
            "Objet administre",
            "Bonjour {{contactName}}, lien : {{verificationUrl}}",
            false,
            1),
        actor,
        correlation,
        token);
    Ensure(disabled.Code == "TEMPLATE_UPDATED", "La desactivation doit etre acceptee.");
    var fallback = await service.RenderEmailAsync(
        "signup_verification",
        EmailTemplates.SignupVerificationVariables("Alice", "https://portail.invalid/v/1"),
        token);
    var (defaultSubject, _) = EmailTemplates.Default("signup_verification");
    Ensure(
        fallback.Subject == defaultSubject,
        "Un modele desactive doit retomber sur le gabarit integre au code.");

    var restored = await service.RestoreEmailTemplateAsync(
        "signup_verification",
        expectedVersion: 2,
        actor,
        correlation,
        token);
    Ensure(
        restored.Code == "TEMPLATE_UPDATED"
        && restored.Template is { Customized: false },
        "La restauration doit reecrire le gabarit de code sans casser l'historique.");

    var revisions = await service.GetRevisionsAsync("email", "signup_verification", token);
    Ensure(
        revisions.Count == 3 && revisions[0].Outcome == "restored",
        "Chaque mutation acceptee doit laisser une revision horodatee.");

    var preview = service.PreviewEmailTemplate(
        "signup_verification",
        new EmailTemplatePreviewRequest("Objet {{contactName}}", "Corps {{verificationUrl}}"),
        correlation);
    Ensure(
        preview.Code == "TEMPLATE_PREVIEW"
        && preview.Subject == "Objet [contactName]"
        && preview.Body == "Corps [verificationUrl]",
        "L'apercu doit substituer des valeurs d'exemple sans envoyer d'e-mail.");

    var snippets = await service.GetPublicSnippetsAsync(token);
    Ensure(
        snippets.ContainsKey("contact_form_confirmation"),
        "Les textes systeme publics doivent rester exposes meme sans personnalisation.");

    CommunicationTemplateService.Invalidate();
}

static async Task VerifyDiagnosticConfigurationAsync()
{
    var repository = new MockDiagnosticConfigurationRepository();
    var service = new DiagnosticConfigurationService(
        repository,
        LoggerFactory.Create(_ => { }).CreateLogger<DiagnosticConfigurationService>());
    DiagnosticConfigurationService.Invalidate();
    const string actor = "00000000-0000-0000-0000-0000000000bb";
    const string correlation = "smoke-diagnostic";
    var token = CancellationToken.None;

    var initial = await service.GetAdminViewAsync(token);
    Ensure(
        initial.Draft.Source == "code" && initial.Published.Source == "code",
        "Sans ligne enregistree, le diagnostic doit se declarer integre au code.");
    Ensure(
        initial.Draft.Configuration is null && initial.Published.Configuration is null,
        "L'API ne duplique pas la configuration par defaut du WebPortal.");

    // Une configuration incomplete est refusee : le registre ferme exige les
    // huit contextes du parcours public.
    var incomplete = service.Validate(
        new DiagnosticConfigurationValidateRequest(
            ParseJson("""{"schemaVersion":1,"contexts":[]}""")),
        correlation);
    Ensure(
        incomplete.Code == "DIAGNOSTIC_INVALID" && incomplete.Errors.Count > 0,
        "Une configuration sans contexte doit etre refusee avec des erreurs explicites.");

    var valid = BuildSmokeDiagnosticConfiguration();
    Ensure(
        service.Validate(new DiagnosticConfigurationValidateRequest(valid), correlation)
            .Code == "DIAGNOSTIC_VALID",
        "La configuration de reference doit passer le registre ferme.");

    // Un operateur hors DSL doit etre refuse : aucune expression arbitraire ne
    // peut atteindre la base.
    var forged = ParseJson(
        JsonSerializer.Serialize(valid).Replace("\"answered\"", "\"eval\""));
    Ensure(
        service.Validate(new DiagnosticConfigurationValidateRequest(forged), correlation)
            .Code == "DIAGNOSTIC_INVALID",
        "Un operateur inconnu doit etre refuse.");

    var saved = await service.SaveDraftAsync(
        new DiagnosticConfigurationUpdateRequest(valid, 0),
        actor,
        correlation,
        token);
    Ensure(
        saved.Code == "DIAGNOSTIC_DRAFT_SAVED" && saved.View!.Draft.Version == 1,
        "Le premier enregistrement doit produire la version 1 du brouillon.");
    Ensure(
        saved.View!.Published.Source == "code",
        "Enregistrer un brouillon ne doit jamais modifier la version publiee.");
    Ensure(
        saved.View!.DraftDiffers,
        "Un brouillon non publie doit etre signale comme different.");

    var stale = await service.SaveDraftAsync(
        new DiagnosticConfigurationUpdateRequest(valid, 0),
        actor,
        correlation,
        token);
    Ensure(
        stale.Code == "DIAGNOSTIC_VERSION_CONFLICT",
        "Une version attendue perimee doit etre refusee, jamais ecrasee.");

    var wrongDraft = await service.PublishAsync(
        new DiagnosticConfigurationPublishRequest(99, 0),
        actor,
        correlation,
        token);
    Ensure(
        wrongDraft.Code == "DIAGNOSTIC_VERSION_CONFLICT",
        "Publier un brouillon deplace doit etre refuse.");

    var published = await service.PublishAsync(
        new DiagnosticConfigurationPublishRequest(1, 0),
        actor,
        correlation,
        token);
    Ensure(
        published.Code == "DIAGNOSTIC_PUBLISHED"
        && published.View!.Published.Version == 1
        && published.View!.Published.Source == "database",
        "La publication doit produire la version 1 publiee.");
    Ensure(
        !published.View!.DraftDiffers,
        "Apres publication, brouillon et version publiee doivent coincider.");

    var publicView = await service.GetPublishedAsync(token);
    Ensure(
        publicView.Source == "database" && publicView.Configuration is not null,
        "Le parcours public doit voir la version publiee.");

    var revisions = await service.GetRevisionsAsync(token);
    Ensure(
        revisions.Count == 2
        && revisions[0].Outcome == "published"
        && revisions[1].Outcome == "draft_saved",
        "L'historique doit tracer l'enregistrement puis la publication.");

    DiagnosticConfigurationService.Invalidate();
}

/// <summary>
/// Configuration minimale mais complete : les huit contextes fermes, chacun
/// avec une question, une regle inconditionnelle finale et, pour `backup`, une
/// correspondance Billing V2 reelle.
/// </summary>
static JsonElement BuildSmokeDiagnosticConfiguration()
{
    var contexts = DiagnosticConfigurationRegistry.ContextIds
        .Select(id => new
        {
            id,
            label = $"Contexte {id}",
            eyebrow = "Diagnostic IT",
            title = $"Titre du contexte {id}",
            intro = "Introduction suffisamment longue pour la validation.",
            contactSubject = $"Sujet {id}",
            formulaEligible = id == "backup",
            questions = new object[]
            {
                new
                {
                    id = "structure",
                    legend = "Quelle est votre structure ?",
                    summaryLabel = "Structure",
                    mode = "single",
                    hint = (string?)null,
                    when = (object?)null,
                    options = new object[]
                    {
                        new { value = "individual", label = "Particulier", exclusive = false },
                        new { value = "business", label = "Entreprise", exclusive = false },
                    },
                },
            },
            guidance = new object[]
            {
                new
                {
                    id = "DIA-SMOKE-000",
                    when = Array.Empty<object>(),
                    title = "Orientation par defaut",
                    body = "Texte de repli affiche lorsque aucune autre regle ne tient.",
                    points = Array.Empty<string>(),
                },
            },
            billingMapping = id == "backup"
                ? new
                {
                    requireAll = new object[]
                    {
                        new
                        {
                            questionId = "structure",
                            @operator = "answered",
                            values = Array.Empty<string>(),
                        },
                    },
                    usersQuestionId = (string?)null,
                    structureQuestionId = "structure",
                    storageQuestionId = (string?)null,
                    restoreTestQuestionId = (string?)null,
                    needsRemoteFilesWhen = (object?)null,
                    needsVpnWhen = (object?)null,
                    needsWindowsDesktopWhen = (object?)null,
                    individualDataKind = "personal_documents",
                    organisationDataKind = "business_documents",
                }
                : null,
        })
        .ToArray();

    return ParseJson(JsonSerializer.Serialize(new { schemaVersion = 1, contexts }));
}

static JsonElement ParseJson(string payload)
{
    using var document = JsonDocument.Parse(payload);
    return document.RootElement.Clone();
}

static KoxoPendingPasswordStore NewPendingPasswordStore()
    => new(LoggerFactory.Create(_ => { }).CreateLogger<KoxoPendingPasswordStore>());

// Registre de parametres en memoire : les smoke tests n'ont pas de MariaDB, donc
// le service retombe sur les valeurs par defaut du registre ferme.
static IApplicationSettingsService NewApplicationSettingsService()
    => new ApplicationSettingsService(new MockApplicationSettingsRepository());

static AdRuntimeConfiguration CreateDisabledAdConfiguration()
    => new(
        AdIntegrationMode.Disabled,
        null,
        null,
        null,
        [],
        false,
        null,
        null,
        3000,
        5000,
        25,
        true);

static EmailRuntimeConfiguration CreateMockEmailConfiguration()
    => new(
        EmailIntegrationMode.Mock,
        "smtp.example.invalid",
        25,
        false,
        null,
        null,
        "noreply@example.invalid",
        "Kermaria",
        "https://portal.example.invalid",
        "contact@example.invalid",
        10000,
        false,
        [],
        true);

static MockAuthenticationStore CreateMockAuthenticationStore()
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DEMO_PORTAL_EMAIL"] = mockEmail,
            ["DEMO_PORTAL_PASSWORD"] = mockPassword,
            ["DEMO_PORTAL_STATUS"] = "active",
            ["DEMO_INTERNAL_ADMIN_EMAIL"] = mockAdminEmail,
            ["DEMO_INTERNAL_ADMIN_PASSWORD"] = mockAdminPassword
        })
        .Build();
    return new MockAuthenticationStore(
        configuration,
        new PortalPasswordService());
}

async Task RunKoxoPendingPasswordTestsAsync()
{
    var repository = new InMemoryKoxoRepository(
    [
        new KoxoExportCandidate(
            "portal-user-1",
            "CLI-A",
            "CLI-000001",
            "madame",
            "Aardvark",
            "Zoe",
            "1992-10-02",
            "zoe.aardvark@example.invalid")
    ]);
    var store = NewPendingPasswordStore();
    var service = new KoxoExportService(repository, store);

    await store.PublishAsync(
        "portal-user-1",
        "NOT_A_REAL_PASSWORD_V041",
        CancellationToken.None);

    // Le tableau de bord rejoue la preparation a la demande. S'il consommait
    // l'entree, le mot de passe disparaitrait avant d'atteindre KoXo — et
    // serait au passage affiche a l'administrateur dans l'apercu.
    var dashboard = await service.GetDashboardAsync(CancellationToken.None);
    Ensure(
        dashboard.Preview is not null
        && dashboard.Preview.Users.All(user => user.MotDePasse is null),
        "L'apercu admin ne doit jamais porter de mot de passe.");

    var exported = await service.ExportAsync(
        "api",
        "v0.41-koxo-password",
        "127.0.0.1",
        CancellationToken.None);
    Ensure(
        exported.Users.Single().MotDePasse == "NOT_A_REAL_PASSWORD_V041",
        "L'export reel doit publier le mot de passe en attente.");

    // Relecture NON destructive : tant que l'identite annuaire n'est pas
    // confirmee, un second instantane doit reporter le meme mot de passe.
    // L'ancien comportement a usage unique perdait le seul secret reversible
    // du systeme des que l'export echouait apres lecture, ou que l'API
    // redemarrait entre les deux — sans aucune erreur visible.
    var again = await service.ExportAsync(
        "api",
        "v0.41-koxo-password-2",
        "127.0.0.1",
        CancellationToken.None);
    Ensure(
        again.Users.Single().MotDePasse == "NOT_A_REAL_PASSWORD_V041",
        "Un second export doit republier le mot de passe tant qu'il n'est "
        + "pas acquitte.");

    // L'acquittement n'intervient qu'apres preuve du lien annuaire.
    await store.AcknowledgeAsync("portal-user-1", CancellationToken.None);
    var acknowledged = await service.ExportAsync(
        "api",
        "v0.41-koxo-password-3",
        "127.0.0.1",
        CancellationToken.None);
    Ensure(
        acknowledged.Users.Single().MotDePasse is null,
        "Apres acquittement, le mot de passe ne doit plus etre republie.");

    Ensure(
        await store.PeekAsync("portal-user-inconnu", CancellationToken.None)
            is null,
        "Un identifiant sans mot de passe en attente doit rendre null.");
}

static string HashTokenForTests(string token)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
    return Convert.ToHexString(bytes).ToLowerInvariant();
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
            startInfo.Environment["AD_DOMAIN"] = "clients.home.bzh";
            startInfo.Environment["AD_CLIENTS_OU_DN"] =
                "OU=Clients,DC=clients,DC=home,DC=bzh";
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
            startInfo.Environment["AD_DOMAIN"] = "clients.home.bzh";
            startInfo.Environment["AD_CLIENTS_OU_DN"] =
                "OU=Clients,DC=clients,DC=home,DC=bzh";
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
            "OU=Users,OU=CLI-DEMO-0042,OU=Clients,DC=clients,DC=home,DC=bzh",
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
                "OU=Disabled,OU=CLI-DEMO-0042,OU=Clients,DC=clients,DC=home,DC=bzh",
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

async Task VerifyManagedContentAsync(
    HttpClient client,
    string baseUrl,
    string clientSessionToken,
    string adminSessionToken,
    bool persistent)
{
    var expectedDataSource = persistent ? "mariadb" : "mock";
    const string legalKey = "legal:cgv";
    const string packSheetKey = "pack-sheet:pack-dossier-securise";
    const string storefrontKey = "storefront:vpn-entreprise";
    var encodedLegalKey = Uri.EscapeDataString(legalKey);
    var encodedPackSheetKey = Uri.EscapeDataString(packSheetKey);
    var encodedStorefrontKey = Uri.EscapeDataString(storefrontKey);

    using var publicLegalRequest = new HttpRequestMessage(
        HttpMethod.Get,
        $"{baseUrl}/internal/portal/content/{encodedLegalKey}");
    publicLegalRequest.Headers.Add(correlationHeader, "managed-content-public");
    using var publicLegalResponse = await client.SendAsync(publicLegalRequest);
    using var publicLegalPayload = JsonDocument.Parse(
        await publicLegalResponse.Content.ReadAsStringAsync());
    Ensure(
        publicLegalResponse.StatusCode == HttpStatusCode.OK
        && publicLegalResponse.Headers.GetValues(dataSourceHeader).Single()
            == expectedDataSource
        && publicLegalPayload.RootElement.GetProperty("key").GetString()
            == legalKey
        && publicLegalPayload.RootElement
            .GetProperty("versionLabel")
            .GetString()
            ?.StartsWith("Version du :", StringComparison.Ordinal) == true
        && publicLegalPayload.RootElement
            .GetProperty("bodyMarkdown")
            .GetString()
            ?.Contains("Les présentes Conditions Générales de Vente", StringComparison.Ordinal) == true,
        "Le contenu public des CGV doit être seedé et lisible en UTF-8.");

    using var publicPackSheetRequest = new HttpRequestMessage(
        HttpMethod.Get,
        $"{baseUrl}/internal/portal/content/{encodedPackSheetKey}");
    publicPackSheetRequest.Headers.Add(correlationHeader, "managed-content-pack");
    using var publicPackSheetResponse = await client.SendAsync(
        publicPackSheetRequest);
    using var publicPackSheetPayload = JsonDocument.Parse(
        await publicPackSheetResponse.Content.ReadAsStringAsync());
    Ensure(
        publicPackSheetResponse.StatusCode == HttpStatusCode.OK
        && publicPackSheetPayload.RootElement.GetProperty("key").GetString()
            == packSheetKey
        && publicPackSheetPayload.RootElement
            .GetProperty("bodyMarkdown")
            .GetString()
            ?.Contains("## Présentation", StringComparison.Ordinal) == true,
        "Une fiche technique pack doit être disponible côté public.");

    using var publicStorefrontRequest = new HttpRequestMessage(
        HttpMethod.Get,
        $"{baseUrl}/internal/portal/content/{encodedStorefrontKey}");
    publicStorefrontRequest.Headers.Add(correlationHeader, "managed-content-storefront");
    using var publicStorefrontResponse = await client.SendAsync(publicStorefrontRequest);
    using var publicStorefrontPayload = JsonDocument.Parse(
        await publicStorefrontResponse.Content.ReadAsStringAsync());
    var publicStorefrontBody = publicStorefrontPayload.RootElement
        .GetProperty("bodyMarkdown").GetString()
        ?? throw new InvalidOperationException("Le body storefront est absent.");
    using var publicStorefrontDocument = JsonDocument.Parse(publicStorefrontBody);
    Ensure(
        publicStorefrontResponse.StatusCode == HttpStatusCode.OK
        && publicStorefrontPayload.RootElement.GetProperty("key").GetString() == storefrontKey
        && publicStorefrontPayload.RootElement.GetProperty("contentType").GetString() == "storefront_page"
        && publicStorefrontDocument.RootElement.GetProperty("title").GetString()
            ?.Contains("Accès VPN sécurisé", StringComparison.Ordinal) == true,
        "La landing storefront SEO doit être seedée et lisible côté public.");

    using var clientForbiddenAdminListRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/content",
        clientSessionToken);
    using var clientForbiddenAdminListResponse = await client.SendAsync(
        clientForbiddenAdminListRequest);
    Ensure(
        clientForbiddenAdminListResponse.StatusCode == HttpStatusCode.Forbidden,
        "Un client ne doit pas accéder à la liste admin des contenus.");

    using var adminListRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/content",
        adminSessionToken);
    using var adminListResponse = await client.SendAsync(adminListRequest);
    using var adminListPayload = JsonDocument.Parse(
        await adminListResponse.Content.ReadAsStringAsync());
    var adminEntries = adminListPayload.RootElement.EnumerateArray().ToArray();
    Ensure(
        adminListResponse.StatusCode == HttpStatusCode.OK
        && adminListResponse.Headers.GetValues(dataSourceHeader).Single()
            == expectedDataSource
        && adminEntries.Length >= 6
        && adminEntries.Any(item =>
            item.GetProperty("key").GetString() == legalKey
            && item.GetProperty("contentType").GetString() == "legal")
        && adminEntries.Any(item =>
            item.GetProperty("key").GetString() == packSheetKey
            && item.GetProperty("contentType").GetString() == "pack_sheet")
        && adminEntries.Any(item =>
            item.GetProperty("key").GetString() == storefrontKey
            && item.GetProperty("contentType").GetString() == "storefront_page"),
        "La liste admin des contenus doit exposer les contenus légaux, les fiches packs et les pages storefront.");

    using var adminDetailRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/content/{encodedLegalKey}",
        adminSessionToken);
    using var adminDetailResponse = await client.SendAsync(adminDetailRequest);
    using var adminDetailPayload = JsonDocument.Parse(
        await adminDetailResponse.Content.ReadAsStringAsync());
    Ensure(
        adminDetailResponse.StatusCode == HttpStatusCode.OK
        && adminDetailResponse.Headers.GetValues(dataSourceHeader).Single()
            == expectedDataSource
        && adminDetailPayload.RootElement.GetProperty("key").GetString()
            == legalKey
        && adminDetailPayload.RootElement
            .GetProperty("bodyMarkdown")
            .GetString()
            ?.Contains("TVA non applicable", StringComparison.Ordinal) == true,
        "Le détail admin des CGV doit rester accessible.");

    using var originalPackDetailRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/content/{encodedPackSheetKey}",
        adminSessionToken);
    using var originalPackDetailResponse = await client.SendAsync(
        originalPackDetailRequest);
    using var originalPackDetailPayload = JsonDocument.Parse(
        await originalPackDetailResponse.Content.ReadAsStringAsync());
    Ensure(
        originalPackDetailResponse.StatusCode == HttpStatusCode.OK,
        "La fiche technique admin initiale doit être lisible.");

    var originalBodyMarkdown = originalPackDetailPayload.RootElement
        .GetProperty("bodyMarkdown")
        .GetString()
        ?? throw new InvalidOperationException(
            "Le bodyMarkdown initial de la fiche pack est absent.");
    var originalVersionLabel = originalPackDetailPayload.RootElement
        .GetProperty("versionLabel")
        .ValueKind == JsonValueKind.Null
            ? null
            : originalPackDetailPayload.RootElement
                .GetProperty("versionLabel")
                .GetString();

    var updatedVersionLabel = $"Smoke test {DateTime.UtcNow:yyyyMMddHHmmssfff}";
    const string updatedBodyMarkdown =
        "## Présentation\n\nContenu de test administrable avec accents : accès, sécurité, pré-requis.\n\n## Support\n\nPoint de contrôle smoke test.";

    try
    {
        using var updateRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{baseUrl}/internal/admin/content/{encodedPackSheetKey}",
            adminSessionToken);
        updateRequest.Content = JsonContent.Create(new
        {
            bodyMarkdown = updatedBodyMarkdown,
            versionLabel = updatedVersionLabel
        });
        using var updateResponse = await client.SendAsync(updateRequest);
        using var updatePayload = JsonDocument.Parse(
            await updateResponse.Content.ReadAsStringAsync());
        Ensure(
            updateResponse.StatusCode == HttpStatusCode.OK
            && updateResponse.Headers.GetValues(dataSourceHeader).Single()
                == expectedDataSource
            && updatePayload.RootElement.GetProperty("key").GetString()
                == packSheetKey
            && updatePayload.RootElement.GetProperty("changed").GetBoolean()
            && updatePayload.RootElement
                .GetProperty("correlation_id")
                .GetString() is { Length: > 0 },
            "La mise à jour admin d'une fiche pack doit être persistée.");

        using var refreshedPackDetailRequest = CreateSessionRequest(
            HttpMethod.Get,
            $"{baseUrl}/internal/admin/content/{encodedPackSheetKey}",
            adminSessionToken);
        using var refreshedPackDetailResponse = await client.SendAsync(
            refreshedPackDetailRequest);
        using var refreshedPackDetailPayload = JsonDocument.Parse(
            await refreshedPackDetailResponse.Content.ReadAsStringAsync());
        Ensure(
            refreshedPackDetailResponse.StatusCode == HttpStatusCode.OK
            && refreshedPackDetailPayload.RootElement
                .GetProperty("bodyMarkdown")
                .GetString() == updatedBodyMarkdown
            && refreshedPackDetailPayload.RootElement
                .GetProperty("versionLabel")
                .GetString() == updatedVersionLabel,
            "La fiche pack mise à jour doit être relisible côté admin.");
    }
    finally
    {
        using var restoreRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{baseUrl}/internal/admin/content/{encodedPackSheetKey}",
            adminSessionToken);
        restoreRequest.Content = JsonContent.Create(new
        {
            bodyMarkdown = originalBodyMarkdown,
            versionLabel = originalVersionLabel
        });
        using var restoreResponse = await client.SendAsync(restoreRequest);
        Ensure(
            restoreResponse.StatusCode == HttpStatusCode.OK,
            "La restauration de la fiche pack après smoke test doit réussir.");
    }

    using var originalStorefrontRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/content/{encodedStorefrontKey}",
        adminSessionToken);
    using var originalStorefrontResponse = await client.SendAsync(originalStorefrontRequest);
    using var originalStorefrontPayload = JsonDocument.Parse(
        await originalStorefrontResponse.Content.ReadAsStringAsync());
    var originalStorefrontBody = originalStorefrontPayload.RootElement
        .GetProperty("bodyMarkdown").GetString()
        ?? throw new InvalidOperationException("Le seed storefront est absent.");

    const string updatedStorefrontBody = """
        {"seoTitle":"VPN entreprise test | Zachary IT","seoDescription":"Une description de test suffisamment détaillée pour valider la persistance du contenu storefront administrable.","title":"Accès VPN administrable","lead":"Contenu de test pour valider la mise à jour structurée d’une page storefront depuis le back-office.","ctaLabel":"Demander un audit","ctaHref":"/diagnostic","sections":[{"heading":"Accès privé","bodyMarkdown":"Un accès VPN cadré pour les usages autorisés."}],"faq":[{"question":"Le VPN est-il administrable ?","answer":"Oui, cette question est un test de persistance."},{"question":"Les prix sont-ils inclus ?","answer":"Non, le contenu ne porte aucun montant."}],"relatedLinks":[{"label":"Diagnostic","href":"/diagnostic"}]}
        """;
    try
    {
        using var updateStorefrontRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{baseUrl}/internal/admin/content/{encodedStorefrontKey}",
            adminSessionToken);
        updateStorefrontRequest.Content = JsonContent.Create(new
        {
            bodyMarkdown = updatedStorefrontBody,
            versionLabel = (string?)null
        });
        using var updateStorefrontResponse = await client.SendAsync(updateStorefrontRequest);
        Ensure(
            updateStorefrontResponse.StatusCode == HttpStatusCode.OK,
            "La mise à jour admin d’une page storefront structurée doit réussir.");

        var allowedEditorialQuantities = new[]
        {
            "24 heures",
            "30 jours",
            "2 utilisateurs"
        };
        foreach (var allowedPhrase in allowedEditorialQuantities)
        {
            using var allowedQuantityRequest = CreateSessionRequest(
                HttpMethod.Patch,
                $"{baseUrl}/internal/admin/content/{encodedStorefrontKey}",
                adminSessionToken);
            allowedQuantityRequest.Content = JsonContent.Create(new
            {
                bodyMarkdown = updatedStorefrontBody.Replace("aucun montant", allowedPhrase),
                versionLabel = (string?)null
            });
            using var allowedQuantityResponse = await client.SendAsync(allowedQuantityRequest);
            Ensure(
                allowedQuantityResponse.StatusCode == HttpStatusCode.OK,
                $"Une quantité éditoriale non tarifaire doit rester autorisée : {allowedPhrase}.");
        }

        var forbiddenPricePhrases = new[]
        {
            "25 €",
            "25€",
            "25 euro",
            "25 euros",
            "25 EUR",
            "25EUR",
            "25 EUR / mois",
            "25 eur/mois",
            "29 EUR",
            "29EUR/mois",
            "29 euros HT"
        };
        foreach (var forbiddenPricePhrase in forbiddenPricePhrases)
        {
            using var invalidPriceRequest = CreateSessionRequest(
                HttpMethod.Patch,
                $"{baseUrl}/internal/admin/content/{encodedStorefrontKey}",
                adminSessionToken);
            invalidPriceRequest.Content = JsonContent.Create(new
            {
                bodyMarkdown = updatedStorefrontBody.Replace("aucun montant", forbiddenPricePhrase),
                versionLabel = (string?)null
            });
            using var invalidPriceResponse = await client.SendAsync(invalidPriceRequest);
            Ensure(
                invalidPriceResponse.StatusCode == HttpStatusCode.BadRequest,
                $"Un montant CMS doit être refusé quelle que soit sa notation : {forbiddenPricePhrase}.");
        }
    }
    finally
    {
        using var restoreStorefrontRequest = CreateSessionRequest(
            HttpMethod.Patch,
            $"{baseUrl}/internal/admin/content/{encodedStorefrontKey}",
            adminSessionToken);
        restoreStorefrontRequest.Content = JsonContent.Create(new
        {
            bodyMarkdown = originalStorefrontBody,
            versionLabel = (string?)null
        });
        using var restoreStorefrontResponse = await client.SendAsync(restoreStorefrontRequest);
        Ensure(
            restoreStorefrontResponse.StatusCode == HttpStatusCode.OK,
            "La restauration de la page storefront après smoke test doit réussir.");
    }
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

    // Le catalogue commercial legacy n'existe plus : l'autorite est Billing
    // V2, et les documents commerciaux ne s'y rattachent plus. Ce qui reste a
    // verifier ici, c'est que l'ancienne surface est bien morte.
    using var legacyClientCatalogRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/portal/catalog",
        clientSessionToken);
    using var legacyClientCatalogResponse = await client.SendAsync(
        legacyClientCatalogRequest);
    Ensure(
        legacyClientCatalogResponse.StatusCode == HttpStatusCode.NotFound,
        "L'ancienne route catalogue client ne doit plus exister.");

    using var legacyAdminCatalogRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/catalog",
        adminSessionToken);
    using var legacyAdminCatalogResponse = await client.SendAsync(
        legacyAdminCatalogRequest);
    Ensure(
        legacyAdminCatalogResponse.StatusCode == HttpStatusCode.NotFound,
        "L'ancienne route catalogue admin ne doit plus exister.");

    using var adminCatalogRequest = CreateSessionRequest(
        HttpMethod.Get,
        $"{baseUrl}/internal/admin/billing-v2/catalog",
        adminSessionToken);
    using var adminCatalogResponse = await client.SendAsync(
        adminCatalogRequest);
    using var adminCatalogPayload = JsonDocument.Parse(
        await adminCatalogResponse.Content.ReadAsStringAsync());
    var adminCatalogRoot = adminCatalogPayload.RootElement;
    var adminCatalogSource = adminCatalogRoot.GetProperty("source").GetString();
    var adminCatalogEditable = adminCatalogRoot.GetProperty("editable").GetBoolean();
    Ensure(
        adminCatalogResponse.StatusCode == HttpStatusCode.OK,
        "L'administration du catalogue Billing V2 doit répondre à l'admin.");
    if (persistent)
    {
        Ensure(
            adminCatalogSource == "mariadb"
            && adminCatalogEditable
            && adminCatalogRoot.GetProperty("services").GetArrayLength() >= 1,
            "En persistance réelle, le catalogue Billing V2 doit être lisible et éditable.");
    }
    else
    {
        // Pas de repli fictif : editer un catalogue absent donnerait
        // l'illusion d'un enregistrement.
        Ensure(
            adminCatalogSource == "unavailable"
            && !adminCatalogEditable
            && adminCatalogRoot.GetProperty("services").GetArrayLength() == 0,
            "Sans persistance, l'administration Billing V2 doit se déclarer non éditable plutôt que d'inventer un catalogue.");
    }

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

    // Les offres commerciales legacy ne sont plus creables : le tarif se
    // publie en versions immuables sur billing_v2_service_prices.
    using var legacyCreateOfferRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/catalog",
        adminSessionToken);
    legacyCreateOfferRequest.Content = JsonContent.Create(new
    {
        name = "Offre legacy interdite",
        priceAmountCents = 12345
    });
    using var legacyCreateOfferResponse = await client.SendAsync(
        legacyCreateOfferRequest);
    Ensure(
        legacyCreateOfferResponse.StatusCode == HttpStatusCode.NotFound,
        "La création d'offre commerciale legacy ne doit plus être possible.");

    using var clientPricePublishRequest = CreateSessionRequest(
        HttpMethod.Post,
        $"{baseUrl}/internal/admin/billing-v2/catalog/prices",
        clientSessionToken);
    clientPricePublishRequest.Content = JsonContent.Create(new
    {
        serviceId = "service-inexistant",
        amountCents = 1000,
        currency = "EUR",
        billingCadence = "monthly"
    });
    using var clientPricePublishResponse = await client.SendAsync(
        clientPricePublishRequest);
    Ensure(
        clientPricePublishResponse.StatusCode == HttpStatusCode.Forbidden,
        "Un client ne doit pas pouvoir publier une révision de tarif Billing V2.");

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
    // Une ligne de document est un instantane autonome : elle porte son propre
    // libelle et son propre prix, et ne pointe vers aucune entree de catalogue.
    // Une revision tarifaire ne doit pas reecrire un devis deja emis.
    addLineRequest.Content = JsonContent.Create(new
    {
        label = "Prestation informative",
        description = "Ligne informative ajoutée par les smoke tests.",
        quantity = 2,
        unitLabel = "forfait",
        unitPriceCents = 12345,
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
        label = "Prestation informative révisée",
        description = "Ligne informative modifiée par les smoke tests.",
        quantity = 3,
        unitLabel = "forfait",
        unitPriceCents = 13000,
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
        $"CN={samAccountName},OU=Groups,OU={customerReference},OU=Clients,DC=clients,DC=home,DC=bzh";

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

static void EnsureSequenceEqual(
    IReadOnlyList<string> actual,
    IReadOnlyList<string> expected,
    string message)
{
    Ensure(
        actual.Count == expected.Count
        && actual.SequenceEqual(expected, StringComparer.Ordinal),
        message);
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
    startInfo.Environment.TryGetValue(
        "DOWNLOAD_STORAGE_ROOT",
        out var downloadStorageRootValue);
    if (string.IsNullOrWhiteSpace(downloadStorageRootValue))
    {
        var downloadStorageRoot = Path.Combine(
            Path.GetTempPath(),
            "kermaria-api-internal-download-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(downloadStorageRoot);
        startInfo.Environment["DOWNLOAD_STORAGE_ROOT"] = downloadStorageRoot;
    }

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

sealed class InMemoryKoxoRepository : IKoxoRepository
{
    private readonly List<KoxoExportCandidate> _candidates;
    private readonly List<KoxoRunSummary> _runs = [];

    public InMemoryKoxoRepository(IEnumerable<KoxoExportCandidate> candidates)
    {
        _candidates = candidates.ToList();
    }

    public bool IsPersistent => false;

    public Task<IReadOnlyList<KoxoExportCandidate>> ListExportCandidatesAsync(
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<KoxoExportCandidate>>(_candidates);

    public Task InsertRunAsync(
        KoxoRunInsert run,
        CancellationToken cancellationToken)
    {
        _runs.Insert(0, new KoxoRunSummary(
            DateTime.UtcNow.ToString("O"),
            run.Source,
            run.Status,
            run.SchemaVersion,
            run.UserCount,
            run.InvalidUserCount,
            run.CorrelationId,
            run.SourceAddress,
            run.SummaryMessage,
            run.GeneratedAtUtc?.ToString("O")));
        return Task.CompletedTask;
    }

    public Task<KoxoRunSummary?> GetLatestRunAsync(CancellationToken cancellationToken)
        => Task.FromResult(_runs.FirstOrDefault());

    public Task<KoxoRunSummary?> GetLatestRunBySourceAsync(
        string source,
        CancellationToken cancellationToken)
        => Task.FromResult(
            _runs.FirstOrDefault(run =>
                string.Equals(run.Source, source, StringComparison.Ordinal)));
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

sealed class SingleHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public SingleHttpClientFactory(HttpClient client)
    {
        _client = client;
    }

    public HttpClient CreateClient(string name) => _client;
}

sealed class CapturedRequestHandler : HttpMessageHandler
{
    private readonly Action<HttpRequestMessage, string> _assertion;

    public CapturedRequestHandler(Action<HttpRequestMessage, string> assertion)
    {
        _assertion = assertion;
    }

    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        _assertion(request, body);
        return new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = JsonContent.Create(new { status = "queued" })
        };
    }
}

sealed class RecordingKoxoSyncWebhookTriggerService : IKoxoSyncWebhookTriggerService
{
    public List<KoxoSyncWebhookTriggerRequest> Requests { get; } = [];

    public Task TriggerAsync(
        KoxoSyncWebhookTriggerRequest request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.CompletedTask;
    }
}

sealed class TestEmailDispatchService : IEmailDispatchService
{
    public Task<EmailDispatchResult> SendInvoiceIssuedAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));

    public Task<EmailDispatchResult> SendPaymentReminderAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));

    public Task<EmailDispatchResult> SendPaymentConfirmedAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));

    public Task<EmailDispatchResult> SendContactFormAsync(
        ContactFormSubmission submission,
        string correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));

    public Task<EmailDispatchResult> SendSignupVerificationAsync(
        string email,
        string contactName,
        string verificationUrl,
        string correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));

    public Task<EmailDispatchResult> SendAccountApprovedAsync(
        string email,
        string contactName,
        string setPasswordUrl,
        string correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));

    public Task<EmailDispatchResult> SendAccountRejectedAsync(
        string email,
        string contactName,
        string? reason,
        string correlationId,
        CancellationToken cancellationToken)
        => Task.FromResult(new EmailDispatchResult(true, "noop", string.Empty));
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
