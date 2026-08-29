using System.Globalization;
using System.Text.Json;
using Kermaria.ApiInternal;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Migration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Infrastructure;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Bpce;
using Kermaria.ApiInternal.Services.Email;
using Kermaria.ApiInternal.Services.Provisioning;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

// External JSON config file (optional). Path overridable via
// KERMARIA_CONFIG_PATH; default C:\ProgramData\Kermaria\api-internal.config.json.
// Contains ALL app config (SQL, secrets, modes, logs, session) in one place
// to avoid polluting Machine environment variables.
// Inserted BEFORE the env variables source so env vars keep the highest
// precedence — enables ad-hoc overrides (e.g. --apply-migrations with a
// different SQL_USERNAME/SQL_PASSWORD) without editing the config file.
var configPath =
    Environment.GetEnvironmentVariable("KERMARIA_CONFIG_PATH")
    ?? @"C:\ProgramData\Kermaria\api-internal.config.json";
var envSourceIndex = builder.Configuration.Sources
    .ToList()
    .FindIndex(s =>
        s is Microsoft.Extensions.Configuration.EnvironmentVariables
            .EnvironmentVariablesConfigurationSource);
var externalConfigSource =
    new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
    {
        Path = configPath,
        Optional = true,
        ReloadOnChange = false,
    };
externalConfigSource.ResolveFileProvider();
if (envSourceIndex >= 0)
{
    builder.Configuration.Sources.Insert(envSourceIndex, externalConfigSource);
}
else
{
    builder.Configuration.Sources.Add(externalConfigSource);
}

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffzzz";
    options.UseUtcTimestamp = false;
});

if (Enum.TryParse<LogLevel>(
        builder.Configuration["LOG_LEVEL"],
        ignoreCase: true,
        out var configuredLogLevel))
{
    builder.Logging.SetMinimumLevel(configuredLogLevel);
}

var logDirectory = builder.Configuration["LOG_FILE_DIRECTORY"]?.Trim();
if (!string.IsNullOrWhiteSpace(logDirectory))
{
    if (!Enum.TryParse<LogLevel>(
            builder.Configuration["LOG_FILE_LEVEL"],
            ignoreCase: true,
            out var fileLogLevel))
    {
        fileLogLevel = configuredLogLevel != default
            ? configuredLogLevel
            : LogLevel.Information;
    }

    if (!int.TryParse(
            builder.Configuration["LOG_FILE_RETENTION_DAYS"],
            out var retentionDays)
        || retentionDays <= 0)
    {
        retentionDays = 30;
    }

    builder.Logging.AddProvider(new FileLoggerProvider(new FileLoggerOptions
    {
        Directory = logDirectory,
        RetentionDays = retentionDays,
        MinimumLevel = fileLogLevel
    }));
}

var isBpceCli = args.Contains(
    "--verify-bpce-sender",
    StringComparer.OrdinalIgnoreCase);

if (!isBpceCli)
{
    RuntimeConfigurationValidator.Validate(
        builder.Configuration,
        builder.Environment);
}

var sqlConfiguration = SqlConfigurationResolver.Resolve(
    builder.Configuration,
    builder.Environment);
var adConfiguration = AdConfigurationResolver.Resolve(builder.Configuration);
var adPasswordConfiguration =
    AdPasswordConfigurationResolver.Resolve(builder.Configuration);
var bpceConfiguration = BpceConfigurationResolver.Resolve(builder.Configuration);
var emailConfiguration = EmailConfigurationResolver.Resolve(builder.Configuration);
var authConfiguration = AuthConfigurationResolver.Resolve(
    builder.Configuration,
    builder.Environment);
var paypalConfiguration = PayPalConfigurationResolver.Resolve(builder.Configuration);
var stripeConfiguration = StripeConfigurationResolver.Resolve(builder.Configuration);
var signupConfiguration = SignupConfigurationResolver.Resolve(builder.Configuration);
var koxoSyncWebhookConfiguration = KoxoSyncWebhookConfigurationResolver.Resolve(
    builder.Configuration);
var subscriptionProvisioningConfiguration =
    SubscriptionProvisioningConfigurationResolver.Resolve(builder.Configuration);
var downloadStorageConfiguration = DownloadStorageConfigurationResolver.Resolve(
    builder.Configuration,
    builder.Environment);

builder.Services.AddSingleton(sqlConfiguration);
builder.Services.AddSingleton(adConfiguration);
builder.Services.AddSingleton(adPasswordConfiguration);
builder.Services.AddSingleton<IAdPasswordRateLimiter, AdPasswordRateLimiter>();
builder.Services.AddSingleton(bpceConfiguration);
builder.Services.AddSingleton(emailConfiguration);
builder.Services.AddSingleton(authConfiguration);
builder.Services.AddSingleton(paypalConfiguration);
builder.Services.AddSingleton(stripeConfiguration);
builder.Services.AddSingleton(signupConfiguration);
builder.Services.AddSingleton(koxoSyncWebhookConfiguration);
builder.Services.AddSingleton(subscriptionProvisioningConfiguration);
builder.Services.AddSingleton(downloadStorageConfiguration);
builder.Services.AddSingleton<IPortalPasswordService, PortalPasswordService>();
builder.Services.AddSingleton<ISessionTokenService, SessionTokenService>();
builder.Services.AddSingleton<IDownloadStorageService, DownloadStorageService>();
builder.Services.AddSingleton<MockAuthenticationStore>();
builder.Services.AddSingleton<MockRequestWorkflowStore>();
builder.Services.AddSingleton<MockPortalNotificationStore>();
builder.Services.AddSingleton<MockBackupStore>();
builder.Services.AddSingleton<MockCommercialStore>();
builder.Services.AddSingleton<MockDownloadStore>();
builder.Services.AddScoped<IPortalRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbPortalRepository(sqlConfiguration)
        : new MockPortalRepository());
builder.Services.AddScoped<IAuthenticationRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbAuthenticationRepository(sqlConfiguration)
        : new MockAuthenticationRepository(
            serviceProvider.GetRequiredService<MockAuthenticationStore>()));
builder.Services.AddScoped<IAdminRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbAdminRepository(sqlConfiguration)
        : new MockAdminRepository(
            serviceProvider.GetRequiredService<MockAuthenticationStore>()));
builder.Services.AddScoped<IDemoAccountRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbDemoAccountRepository(sqlConfiguration)
        : new MockDemoAccountRepository());
builder.Services.AddScoped<IDemoProfileRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbDemoProfileRepository(sqlConfiguration)
        : new MockDemoProfileRepository());
builder.Services.AddScoped<ICommunicationTemplateRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbCommunicationTemplateRepository(sqlConfiguration)
        : new MockCommunicationTemplateRepository());
builder.Services.AddScoped<
    ICommunicationTemplateService,
    CommunicationTemplateService>();
builder.Services.AddScoped<
    IPortalNotificationContentService,
    PortalNotificationContentService>();
builder.Services.AddScoped<IDiagnosticConfigurationRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbDiagnosticConfigurationRepository(sqlConfiguration)
        : new MockDiagnosticConfigurationRepository());
builder.Services.AddScoped<
    IDiagnosticConfigurationService,
    DiagnosticConfigurationService>();
builder.Services.AddScoped<IFiscalPolicyRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbFiscalPolicyRepository(sqlConfiguration)
        : new MockFiscalPolicyRepository());
builder.Services.AddScoped<IFiscalPolicyService, FiscalPolicyService>();
builder.Services.AddScoped<IRequestWorkflowRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbRequestWorkflowRepository(
            sqlConfiguration,
            serviceProvider.GetRequiredService<IPortalNotificationContentService>())
        : new MockRequestWorkflowRepository(
            serviceProvider.GetRequiredService<MockRequestWorkflowStore>(),
            serviceProvider.GetRequiredService<MockPortalNotificationStore>(),
            serviceProvider.GetRequiredService<IPortalNotificationContentService>()));
builder.Services.AddScoped<IPortalNotificationRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbPortalNotificationRepository(sqlConfiguration)
        : new MockPortalNotificationRepository(
            serviceProvider.GetRequiredService<MockPortalNotificationStore>()));
builder.Services.AddScoped<IBackupRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbBackupRepository(
            sqlConfiguration,
            serviceProvider.GetRequiredService<IBackupProtectionService>(),
            serviceProvider.GetRequiredService<
                ILogger<MariaDbBackupRepository>>())
        : new MockBackupRepository(
            serviceProvider.GetRequiredService<MockBackupStore>(),
            serviceProvider.GetRequiredService<IBackupProtectionService>()));
builder.Services.AddSingleton<MockBpceInvoicingRepository>();
builder.Services.AddScoped<IBpceInvoicingRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? (IBpceInvoicingRepository)new MariaDbBpceInvoicingRepository(sqlConfiguration)
        : serviceProvider.GetRequiredService<MockBpceInvoicingRepository>());
builder.Services.AddScoped<IInvoiceIssuingService, InvoiceIssuingService>();
builder.Services.AddScoped<
    ICommercialDocumentStripePaymentService,
    CommercialDocumentStripePaymentService>();
builder.Services.AddScoped<ICommercialDocumentRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbCommercialDocumentRepository(sqlConfiguration)
        : new MockCommercialDocumentRepository(
            serviceProvider.GetRequiredService<MockCommercialStore>()));
builder.Services.AddSingleton<MockManagedContentStore>();
builder.Services.AddSingleton<MockEditorialStore>();
builder.Services.AddScoped<IManagedContentRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbManagedContentRepository(sqlConfiguration)
        : new MockManagedContentRepository(
            serviceProvider.GetRequiredService<MockManagedContentStore>()));
builder.Services.AddScoped<IEditorialRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbEditorialRepository(sqlConfiguration)
        : new MockEditorialRepository(
            serviceProvider.GetRequiredService<MockEditorialStore>()));
builder.Services.AddScoped<IDownloadRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbDownloadRepository(sqlConfiguration)
        : new MockDownloadRepository(
            serviceProvider.GetRequiredService<MockDownloadStore>()));
builder.Services.AddSingleton<MockClientSolutionStore>();
builder.Services.AddScoped<IClientSolutionRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbClientSolutionRepository(sqlConfiguration)
        : new MockClientSolutionRepository(
            serviceProvider.GetRequiredService<MockClientSolutionStore>()));
builder.Services.AddSingleton<MockPublicPackCatalogStore>();
builder.Services.AddScoped<IPublicPackCatalogRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbPublicPackCatalogRepository(sqlConfiguration)
        : new MockPublicPackCatalogRepository(
            serviceProvider.GetRequiredService<MockPublicPackCatalogStore>()));
builder.Services.AddSingleton<MockSubscriptionProvisioningActionStore>();
builder.Services.AddScoped<ISubscriptionProvisioningActionRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbSubscriptionProvisioningActionRepository(sqlConfiguration)
        : new MockSubscriptionProvisioningActionRepository(
            serviceProvider.GetRequiredService<
                MockSubscriptionProvisioningActionStore>()));
builder.Services.AddScoped<IActiveDirectoryLinkRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbActiveDirectoryLinkRepository(sqlConfiguration)
        : new MockActiveDirectoryLinkRepository());
builder.Services.AddSingleton<MockSignupStore>();
builder.Services.AddScoped<ISignupRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbSignupRepository(sqlConfiguration)
        : new MockSignupRepository(
            serviceProvider.GetRequiredService<MockSignupStore>(),
            serviceProvider.GetRequiredService<MockAuthenticationStore>()));
builder.Services.AddScoped<IKoxoRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbKoxoRepository(sqlConfiguration)
        : new MockKoxoRepository());
// Cycle de vie d'identite des utilisateurs additionnels Billing V2 (Phase 4).
// Les magasins mock sont des singletons : l'utilisateur portail cree par
// l'attribution doit rester visible du depot de jetons, qui ecrit son condensat
// dans une requete ulterieure.
builder.Services.AddSingleton<MockPortalUserStore>();
builder.Services.AddSingleton<MockPortalPasswordSetupRepository>(
    serviceProvider => new MockPortalPasswordSetupRepository(
        serviceProvider.GetRequiredService<MockPortalUserStore>())
    {
        // En mock, c'est le magasin en memoire qui tient lieu de transaction
        // pour le secret : il ne le rend visible qu'au moment ou l'unite de
        // travail aboutit. En persistance SQL, le depot MariaDB l'ecrit
        // lui-meme dans sa transaction et ce point d'attache reste nul.
        SealSink = serviceProvider.GetRequiredService<IKoxoPendingPasswordStore>()
            as IKoxoPendingPasswordSealSink
    });
builder.Services.AddScoped<IPortalPasswordSetupRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbPortalPasswordSetupRepository(sqlConfiguration)
        : serviceProvider
            .GetRequiredService<MockPortalPasswordSetupRepository>());
builder.Services.AddSingleton<MockBillingV2AdditionalUserIdentityRepository>(
    serviceProvider => new MockBillingV2AdditionalUserIdentityRepository(
        serviceProvider.GetRequiredService<MockPortalUserStore>(),
        serviceProvider
            .GetRequiredService<MockPortalPasswordSetupRepository>()));
builder.Services.AddScoped<IBillingV2AdditionalUserIdentityRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbBillingV2AdditionalUserIdentityRepository(sqlConfiguration)
        : serviceProvider
            .GetRequiredService<
                MockBillingV2AdditionalUserIdentityRepository>());
builder.Services.AddScoped<BillingV2AdditionalUserIdentityService>();
builder.Services.AddScoped<IBillingV2AdditionalUserIdentityService>(
    serviceProvider => serviceProvider.GetRequiredService<
        BillingV2AdditionalUserIdentityService>());
builder.Services.AddScoped<IBillingV2AdditionalUserIdentityConvergenceService>(
    serviceProvider => serviceProvider.GetRequiredService<
        BillingV2AdditionalUserIdentityService>());
// Lectures ciblees du ciblage de stockage KoXo, distinctes de l'export global :
// l'export porte la politique de population du CSV, pas la designation d'une
// cible de quota.
builder.Services.AddScoped<IBillingV2KoxoTargetingRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbBillingV2KoxoTargetingRepository(sqlConfiguration)
        : new MockBillingV2KoxoTargetingRepository());
// Resolution en lecture seule des cibles de stockage KoXo, consommee par
// BillingV2ProvisioningService avant toute application de quota : le provider
// ne resout jamais lui-meme le titulaire d'une cible.
builder.Services.AddScoped<
    IBillingV2KoxoStorageTargetResolutionService,
    BillingV2KoxoStorageTargetResolutionService>();
builder.Services.AddScoped<IPortalService, PortalService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IBackupProtectionService, BackupProtectionService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<
    IDemoProvisioningService,
    DemoProvisioningService>();
builder.Services.AddSingleton(
    DemoConversionRuntimeConfiguration.Resolve(builder.Configuration));
builder.Services.AddScoped<IDemoConversionService, DemoConversionService>();
builder.Services.AddScoped<IDemoAccountService, DemoAccountService>();
// Le mot de passe est publie par une requete (set-password) et repris par une
// autre (l'export declenche dans la foulee).
//
// En persistance reelle il est retenu en base, chiffre : la version en memoire
// ne survivait ni a un redemarrage de l'API, ni a un export echoue apres
// lecture, et perdait alors le seul secret reversible du systeme. Fail-closed
// si KOXO_PENDING_PASSWORD_KEY manque : rien n'est retenu, et les appelants
// refusent l'operation au lieu de la croire faite.
//
// La variante en memoire reste le mode mock, ou il n'y a aucun KoXo derriere.
builder.Services.AddSingleton<IKoxoPendingPasswordStore>(serviceProvider =>
    sqlConfiguration.IsPersistent
        ? new MariaDbKoxoPendingPasswordStore(
            sqlConfiguration,
            KoxoPendingPasswordProtector.TryCreate(
                builder.Configuration[
                    MariaDbKoxoPendingPasswordStore.KeyVariable]),
            MariaDbKoxoPendingPasswordStore.ResolveLifetime(
                builder.Configuration[
                    MariaDbKoxoPendingPasswordStore.LifetimeVariable]),
            serviceProvider.GetRequiredService<
                ILogger<MariaDbKoxoPendingPasswordStore>>())
        : new KoxoPendingPasswordStore(
            serviceProvider.GetRequiredService<
                ILogger<KoxoPendingPasswordStore>>()));
builder.Services.AddScoped<IKoxoExportService, KoxoExportService>();
builder.Services.AddScoped<IRequestWorkflowService, RequestWorkflowService>();
builder.Services.AddScoped<
    IPortalNotificationService,
    PortalNotificationService>();
builder.Services.AddSingleton<IFiscalPolicy, FiscalPolicy>();
builder.Services.AddScoped<ICommercialService, CommercialService>();
var billingV2RuntimeConfiguration =
    BillingV2RuntimeConfiguration.Resolve(builder.Configuration);
builder.Services.AddSingleton(billingV2RuntimeConfiguration);
if (billingV2RuntimeConfiguration.AdditionalUserMutationsEnabled)
{
    builder.Services.AddHostedService<BillingV2AdditionalUserIdentityConvergenceWorker>();
}
builder.Services.AddSingleton<IBillingV2PricingEngine, BillingV2PricingEngine>();
// Projection commerciale publique : lecture seule du catalogue V2 et devis
// calcule par le moteur ci-dessus. N'ecrit rien et ne cree aucun abonnement.
builder.Services.AddScoped<
    IBillingV2PublicCatalogService,
    BillingV2PublicCatalogService>();
// Administration du catalogue V2 : seule autorite commerciale du produit.
// Ecrit `billing_v2_services`, `_service_tiers`, `_service_prices` (en
// versionnant, jamais en reecrivant), `_offer_presets`, `_preset_items`,
// `_commitment_terms`, `_commitment_payment_options` et
// `_provider_price_mappings`.
builder.Services.AddScoped<
    IBillingV2CatalogAdministrationService,
    BillingV2CatalogAdministrationService>();
builder.Services.AddScoped<
    IBillingV2LaunchReadinessService,
    BillingV2LaunchReadinessService>();
builder.Services.AddScoped<
    IBillingV2AdminReadinessService,
    BillingV2AdminReadinessService>();
builder.Services.AddScoped<
    IBillingV2ConfigurationOverviewService,
    BillingV2ConfigurationOverviewService>();
builder.Services.AddScoped<
    IBillingV2CheckoutReadinessService,
    BillingV2CheckoutReadinessService>();
builder.Services.AddScoped<
    IBillingV2ProviderAgreementService,
    BillingV2ProviderAgreementService>();
builder.Services.AddScoped<IBillingV2DocumentReadinessService>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new BillingV2DocumentReadinessService(
            sqlConfiguration,
            serviceProvider.GetRequiredService<BpceRuntimeConfiguration>(),
            serviceProvider.GetRequiredService<
                ILogger<BillingV2DocumentReadinessService>>())
        : NoOpBillingV2DocumentReadinessService.Instance);
builder.Services.AddScoped<IBillingV2DocumentIssuerService>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new BillingV2DocumentIssuerService(
            sqlConfiguration,
            serviceProvider.GetRequiredService<IInvoiceIssuingService>(),
            serviceProvider.GetRequiredService<
                ILogger<BillingV2DocumentIssuerService>>())
        : NoOpBillingV2DocumentIssuerService.Instance);
builder.Services.AddScoped<
    IBillingV2ProviderCheckoutCommandService,
    BillingV2ProviderCheckoutCommandService>();
builder.Services.AddScoped<IBillingV2ProviderCheckoutExecutor>(
    serviceProvider => billingV2RuntimeConfiguration.ProviderExecutorEnabled
        ? new BillingV2ProviderCheckoutExecutor(
            serviceProvider.GetRequiredService<BillingV2RuntimeConfiguration>(),
            serviceProvider.GetRequiredService<PayPalRuntimeConfiguration>(),
            serviceProvider.GetRequiredService<StripeRuntimeConfiguration>(),
            serviceProvider.GetRequiredService<IHttpClientFactory>())
        : DisabledBillingV2ProviderCheckoutExecutor.Instance);
// Rail Stripe V2 (Phase 2). Fail-closed par defaut : sans flag executor, la
// passerelle est desactivee et aucun appel Stripe ne peut partir.
builder.Services.AddScoped<IBillingV2StripeGateway>(
    serviceProvider => billingV2RuntimeConfiguration.ProviderExecutorEnabled
        ? new BillingV2StripeGateway(
            serviceProvider.GetRequiredService<BillingV2RuntimeConfiguration>(),
            serviceProvider.GetRequiredService<StripeRuntimeConfiguration>(),
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            serviceProvider.GetRequiredService<ILogger<BillingV2StripeGateway>>())
        : DisabledBillingV2StripeGateway.Instance);
builder.Services.AddScoped<
    IBillingV2StripeRailService,
    BillingV2StripeRailService>();
builder.Services.AddSingleton<IBillingV2Clock>(SystemBillingV2Clock.Instance);
builder.Services.AddScoped<
    IBillingV2StripeReconciliationService,
    BillingV2StripeReconciliationService>();
builder.Services.AddScoped<IBillingV2RenewalService>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new BillingV2RenewalService(
            sqlConfiguration,
            serviceProvider.GetRequiredService<IBillingV2Clock>(),
            serviceProvider.GetRequiredService<IBillingV2StripeGateway>(),
            serviceProvider.GetRequiredService<IBillingV2StripeRailService>(),
            serviceProvider.GetRequiredService<
                ILogger<BillingV2RenewalService>>())
        : NoOpBillingV2RenewalService.Instance);
// Phase 3. Declencheur periodique du reconciliateur : OFF par defaut, donc
// pas meme enregistre tant que le drapeau n'est pas pose.
if (billingV2RuntimeConfiguration.ReconciliationWorkerEnabled)
{
    builder.Services.AddHostedService<BillingV2StripeReconciliationWorker>();
}
builder.Services.AddScoped<
    IBillingV2ProviderOutboxDispatcher,
    BillingV2ProviderOutboxDispatcher>();
// Resiliation Billing V2. Meme discipline que le checkout : la demande est
// persistee avec son evenement d'outbox, et c'est le dispatcher qui obtient la
// convergence fournisseur. Sans executeur configure, la resiliation reste due
// et visible en pending_cancellation — jamais close a tort.
builder.Services.AddScoped<IBillingV2ProviderCancellationExecutor>(
    serviceProvider => billingV2RuntimeConfiguration.ProviderExecutorEnabled
        ? new BillingV2ProviderCancellationExecutor(
            serviceProvider.GetRequiredService<BillingV2RuntimeConfiguration>(),
            serviceProvider.GetRequiredService<PayPalRuntimeConfiguration>(),
            serviceProvider.GetRequiredService<StripeRuntimeConfiguration>(),
            serviceProvider.GetRequiredService<IHttpClientFactory>())
        : DisabledBillingV2ProviderCancellationExecutor.Instance);
builder.Services.AddScoped<
    IBillingV2SubscriptionCancellationService,
    BillingV2SubscriptionCancellationService>();
builder.Services.AddScoped<
    IBillingV2CancellationOutboxDispatcher,
    BillingV2CancellationOutboxDispatcher>();
builder.Services.AddScoped<
    IBillingV2ProviderInboundEventService,
    BillingV2ProviderInboundEventService>();
builder.Services.AddScoped<BillingV2FulfillmentDispatcher>();
builder.Services.AddScoped<BillingV2StripeRecurringMutationDispatcher>();
if (billingV2RuntimeConfiguration.StripeRecurringMutationEnabled)
{
    builder.Services.AddHostedService<BillingV2StripeRecurringMutationWorker>();
}
builder.Services.AddScoped<
    IBillingV2AuthoritativeCheckoutService,
    BillingV2AuthoritativeCheckoutService>();
builder.Services.AddScoped<
    IBillingV2SubscriptionChangeService,
    BillingV2SubscriptionChangeService>();
builder.Services.AddScoped<IBillingV2PortalSubscriptionProjection>(
    _ => sqlConfiguration.IsPersistent
        ? new BillingV2PortalSubscriptionProjection(sqlConfiguration)
        : NoOpBillingV2PortalSubscriptionProjection.Instance);
builder.Services.AddScoped<IBillingV2ClientServiceEntitlementProjection>(
    _ => sqlConfiguration.IsPersistent
        ? new BillingV2ClientServiceEntitlementProjection(sqlConfiguration)
        : NoOpBillingV2ClientServiceEntitlementProjection.Instance);
builder.Services.AddScoped<IBillingV2DownloadAccessProjection>(
    _ => sqlConfiguration.IsPersistent
        ? new BillingV2DownloadAccessProjection(sqlConfiguration)
        : NoOpBillingV2DownloadAccessProjection.Instance);
if (billingV2RuntimeConfiguration.ProviderOutboxEnabled)
{
    builder.Services.AddHostedService<BillingV2ProviderOutboxWorker>();
    builder.Services.AddHostedService<BillingV2CancellationOutboxWorker>();
}
// Topologie technique : Billing V2 est la seule source. Enregistree en
// singleton car son instantane est mis en cache pour la duree du processus.
builder.Services.AddSingleton<
    IServiceTopologyService,
    BillingV2ServiceTopologyService>();
builder.Services.AddScoped<
    IBillingV2SubscriptionAdGroupProjection,
    BillingV2SubscriptionAdGroupProjection>();
// Sans point d'entree KoXo configure, le provider reste dormant et refuse tout
// lot non vide : pas de repli silencieux vers une adresse devinee.
var koxoStorageProviderConfiguration =
    BillingV2KoxoStorageProviderConfiguration.Resolve(builder.Configuration);
builder.Services.AddSingleton(koxoStorageProviderConfiguration);
if (koxoStorageProviderConfiguration.Configured)
{
    builder.Services.AddHttpClient(
        BillingV2KoxoStorageProviderConfiguration.HttpClientName,
        client =>
        {
            client.Timeout = koxoStorageProviderConfiguration.Timeout;
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer",
                    koxoStorageProviderConfiguration.BearerToken);
        });
    builder.Services.AddSingleton<
        IBillingV2KoxoStorageProvider,
        HttpBillingV2KoxoStorageProvider>();
}
else
{
    builder.Services.AddSingleton<IBillingV2KoxoStorageProvider>(
        DormantBillingV2KoxoStorageProvider.Instance);
}
builder.Services.AddScoped<
    IBillingV2ProvisioningService,
    BillingV2ProvisioningService>();
builder.Services.AddScoped<
    IClientServiceCatalogService,
    ClientServiceCatalogService>();
builder.Services.AddSingleton<IDownloadSchemaEnsurer, DownloadSchemaEnsurer>();
builder.Services.AddSingleton<
    IClientSolutionSchemaEnsurer,
    ClientSolutionSchemaEnsurer>();
builder.Services.AddScoped<IManagedContentService, ManagedContentService>();
builder.Services.AddScoped<IApplicationSettingsRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbApplicationSettingsRepository(sqlConfiguration)
        : new MockApplicationSettingsRepository());
builder.Services.AddScoped<IApplicationSettingsService, ApplicationSettingsService>();
builder.Services.AddSingleton<IConfigurationStatusService, ConfigurationStatusService>();
builder.Services.AddScoped<IEditorialService, EditorialService>();
builder.Services.AddScoped<IDownloadService, DownloadService>();
builder.Services.AddScoped<IClientSolutionService, ClientSolutionService>();
builder.Services.AddScoped<IPublicPackCatalogService, PublicPackCatalogService>();
builder.Services.AddScoped<IProvisioningService, ProvisioningService>();
builder.Services.AddScoped<
    IBillingV2SubscriptionProvisioningManager,
    BillingV2SubscriptionProvisioningManager>();
builder.Services.AddScoped<
    IBillingV2SubscriptionAdministrationService,
    BillingV2SubscriptionAdministrationService>();
builder.Services.AddScoped<
    ICustomerActiveDirectoryAdministrationService,
    CustomerActiveDirectoryAdministrationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddHostedService<DemoAccountExpirationWorker>();
builder.Services.AddTransient<MariaDbMigrationRunner>();
builder.Services.AddTransient<MariaDbAdminSeeder>();
builder.Services.AddSingleton<OperationalReadinessService>();
builder.Services.AddSingleton<MockAdGroupMembershipStore>();
builder.Services.AddSingleton<IActiveDirectoryService>(serviceProvider =>
    adConfiguration.Mode switch
    {
        AdIntegrationMode.Mock =>
            new MockActiveDirectoryService(
                adConfiguration,
                serviceProvider.GetRequiredService<MockAdGroupMembershipStore>()),
        AdIntegrationMode.ReadOnly or AdIntegrationMode.ControlledWrite =>
            new LdapActiveDirectoryService(
                adConfiguration,
                serviceProvider.GetRequiredService<
                    ILogger<LdapActiveDirectoryService>>()),
        _ => new DisabledActiveDirectoryService(adConfiguration)
    });
builder.Services.AddSingleton<IAdGroupProvisioner>(serviceProvider =>
    adConfiguration.Mode switch
    {
        AdIntegrationMode.Mock => new MockAdGroupProvisioner(
            serviceProvider.GetRequiredService<MockAdGroupMembershipStore>()),
        AdIntegrationMode.ReadOnly or AdIntegrationMode.ControlledWrite =>
            new LdapAdGroupProvisioner(
                adConfiguration,
                serviceProvider.GetRequiredService<
                    ILogger<LdapAdGroupProvisioner>>()),
        _ => new DisabledAdGroupProvisioner()
    });
builder.Services.AddHttpClient(
    BpceTokenCache.HttpClientName,
    client =>
    {
        client.Timeout =
            TimeSpan.FromMilliseconds(bpceConfiguration.RequestTimeoutMs);
    });
builder.Services.AddHttpClient(
    KoxoSyncWebhookTriggerService.HttpClientName,
    client =>
    {
        client.Timeout = koxoSyncWebhookConfiguration.Timeout;
    });
builder.Services.AddHttpClient(
    BillingV2ProviderCheckoutExecutor.HttpClientName,
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });
builder.Services.AddHttpClient(
    BillingV2StripeGateway.HttpClientName,
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });
builder.Services.AddSingleton<IBpceTokenCache, BpceTokenCache>();
builder.Services.AddSingleton<IBpceApiClient, BpceApiClient>();
builder.Services.AddSingleton<IKoxoSyncWebhookTriggerService>(serviceProvider =>
    koxoSyncWebhookConfiguration.Enabled
        ? new KoxoSyncWebhookTriggerService(
            koxoSyncWebhookConfiguration,
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            serviceProvider.GetRequiredService<
                ILogger<KoxoSyncWebhookTriggerService>>())
        : new DisabledKoxoSyncWebhookTriggerService());
builder.Services.AddSingleton<IBpceInvoicingService>(serviceProvider =>
    bpceConfiguration.Mode switch
    {
        BpceIntegrationMode.Mock =>
            new MockBpceInvoicingService(bpceConfiguration),
        BpceIntegrationMode.Live =>
            new LiveBpceInvoicingService(
                bpceConfiguration,
                serviceProvider.GetRequiredService<IBpceTokenCache>(),
                serviceProvider.GetRequiredService<IBpceApiClient>(),
                serviceProvider.GetRequiredService<
                    ILogger<LiveBpceInvoicingService>>()),
        _ => new DisabledBpceInvoicingService(bpceConfiguration)
    });
builder.Services.AddSingleton<MockEmailLogRepository>();
builder.Services.AddScoped<IEmailLogRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbEmailLogRepository(sqlConfiguration)
        : serviceProvider.GetRequiredService<MockEmailLogRepository>());
builder.Services.AddSingleton<IEmailService>(serviceProvider =>
    emailConfiguration.Mode switch
    {
        EmailIntegrationMode.Mock =>
            new MockEmailService(
                emailConfiguration,
                serviceProvider.GetRequiredService<ILogger<MockEmailService>>()),
        EmailIntegrationMode.Live =>
            new LiveEmailService(
                emailConfiguration,
                serviceProvider.GetRequiredService<ILogger<LiveEmailService>>()),
        _ => new DisabledEmailService(emailConfiguration)
    });
builder.Services.AddScoped<IEmailDispatchService, EmailDispatchService>();
builder.Services.AddScoped<ISignupService, SignupService>();

var app = builder.Build();
var exposeDebugExceptionDetails =
    app.Environment.IsDevelopment()
    || string.Equals(
        builder.Configuration["RUN_MARIADB_TESTS"],
        "true",
        StringComparison.OrdinalIgnoreCase);
var diagnosticSecretValues = GetDiagnosticSecretValues(builder.Configuration);

if (!sqlConfiguration.IsPersistent)
{
    app.Logger.LogWarning(
        "MariaDB is not configured; Development mock persistence is active");
}
else
{
    app.Logger.LogInformation("MariaDB persistence is configured");
}

app.Logger.LogInformation(
    "API-INTERNAL started in environment {Environment}; persistence {PersistenceMode}; Active Directory mode {AdMode}; operations_enabled {OperationsEnabled}; BPCE mode {BpceMode}; Email mode {EmailMode}",
    app.Environment.EnvironmentName,
    sqlConfiguration.IsPersistent ? "mariadb" : "mock",
    adConfiguration.ModeName,
    adConfiguration.WritesEnabled,
    bpceConfiguration.ModeName,
    emailConfiguration.ModeName);

if (args.Contains("--seed-demo-data", StringComparer.OrdinalIgnoreCase)
    && !args.Contains("--apply-migrations", StringComparer.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "--seed-demo-data requires --apply-migrations.");
}

if (args.Contains("--apply-migrations", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "MariaDB migrations can only be run by this command in Development.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var migrationRunner =
        scope.ServiceProvider.GetRequiredService<MariaDbMigrationRunner>();
    await migrationRunner.ApplyAsync(
        args.Contains("--seed-demo-data", StringComparer.OrdinalIgnoreCase));
    return;
}

if (args.Contains("--run-demo-expiration", StringComparer.OrdinalIgnoreCase))
{
    // Filet de securite invoque par la tache planifiee Windows (SRV-13) : rejoue
    // le meme balayage que le service de fond (revocation des essais echus +
    // purge), puis quitte. Inerte en persistance mock.
    await using var scope = app.Services.CreateAsyncScope();
    var demoService =
        scope.ServiceProvider.GetRequiredService<IDemoAccountService>();
    if (demoService.IsPersistent)
    {
        var sweep = await demoService.RunExpirationSweepAsync(
            CancellationToken.None);
        app.Logger.LogInformation(
            "Demo expiration (scheduled task): revoked={Revoked} purged={Purged} skipped={Skipped} revokeFailures={Failures}",
            sweep.RevokedCount,
            sweep.PurgedCount,
            sweep.SkippedReferences.Count,
            sweep.RevokeFailures.Count);
    }

    return;
}

if (args.Contains("--seed-admin", StringComparer.OrdinalIgnoreCase))
{
    // Interactive bootstrap of the first internal_admin. Usable outside
    // Development because credentials are prompted on stdin (never in
    // Get-Process, event logs, or the process command line).
    await using var scope = app.Services.CreateAsyncScope();
    var seeder =
        scope.ServiceProvider.GetRequiredService<MariaDbAdminSeeder>();
    var exit = await seeder.RunAsync();
    Environment.Exit(exit);
    return;
}

if (args.Contains("--verify-bpce-sender", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var bpceService =
        scope.ServiceProvider.GetRequiredService<IBpceInvoicingService>();
    var cts = new CancellationTokenSource(
        TimeSpan.FromSeconds(30));

    app.Logger.LogInformation(
        "BPCE sender verification — mode {Mode}",
        bpceService.ModeName);

    var listResult = await bpceService.ListSendersAsync(cts.Token);
    if (listResult.StatusCode >= 400)
    {
        app.Logger.LogError(
            "BPCE sender list failed: [{Code}] {Message}",
            listResult.Code,
            listResult.Message);
        return;
    }

    var senders = listResult.Value ?? Array.Empty<BpceSenderInfo>();
    if (senders.Count == 0)
    {
        app.Logger.LogWarning(
            "BPCE: no sender profiles found. Create one on {Url}",
            $"{bpceConfiguration.BaseUrl}/organisation/senders/");
        return;
    }

    foreach (var sender in senders)
    {
        app.Logger.LogInformation(
            "BPCE sender — id={Id} name={Name} siret={Siret} default={IsDefault} archived={IsArchived}",
            sender.Id,
            sender.Name ?? "(none)",
            sender.Siret ?? "(none)",
            sender.IsDefault,
            sender.IsArchived);
    }

    var configuredSenderId = bpceConfiguration.SenderId;
    if (configuredSenderId is not null)
    {
        var getResult = await bpceService.GetSenderAsync(
            configuredSenderId,
            cts.Token);
        if (getResult.StatusCode >= 400)
        {
            app.Logger.LogWarning(
                "BPCE_SENDER_ID={SenderId} is set but the sender was not found: [{Code}] {Message}",
                configuredSenderId,
                getResult.Code,
                getResult.Message);
        }
        else
        {
            app.Logger.LogInformation(
                "BPCE_SENDER_ID={SenderId} is valid — name={Name} siret={Siret}",
                configuredSenderId,
                getResult.Value?.Name ?? "(none)",
                getResult.Value?.Siret ?? "(none)");
        }
    }
    else
    {
        var defaultSender = senders.FirstOrDefault(s => s.IsDefault)
            ?? senders[0];
        app.Logger.LogInformation(
            "BPCE_SENDER_ID is not set. Suggested value: {Id} ({Name})",
            defaultSender.Id,
            defaultSender.Name ?? "(none)");
    }

    return;
}

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler(exceptionHandler =>
{
    exceptionHandler.Run(async context =>
    {
        var correlationId = context.GetCorrelationId();
        var exception = context.Features
            .Get<IExceptionHandlerFeature>()?
            .Error;
        var (statusCode, code, message) = exception switch
        {
            InvalidCredentialsException => (
                StatusCodes.Status401Unauthorized,
                "INVALID_CREDENTIALS",
                "Identifiants invalides."),
            AccountLockedException => (
                StatusCodes.Status429TooManyRequests,
                "ACCOUNT_LOCKED",
                "Identifiants invalides ou connexion temporairement indisponible."),
            SessionRequiredException => (
                StatusCodes.Status401Unauthorized,
                "SESSION_REQUIRED",
                "Une session valide est requise."),
            SessionExpiredException => (
                StatusCodes.Status401Unauthorized,
                "SESSION_EXPIRED",
                "La session a expiré."),
            SessionRevokedException => (
                StatusCodes.Status401Unauthorized,
                "SESSION_REVOKED",
                "La session n'est plus valide."),
            SessionInvalidException => (
                StatusCodes.Status401Unauthorized,
                "SESSION_INVALID",
                "La session n'est pas valide."),
            PortalAccessDeniedException => (
                StatusCodes.Status403Forbidden,
                "ACCESS_DENIED",
                "L'accès à cette ressource est refusé."),
            PortalValidationException => (
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                "La demande est incomplète ou invalide."),
            DownloadConflictException conflict => (
                StatusCodes.Status409Conflict,
                conflict.Code,
                conflict.Message),
            ClientSolutionConflictException conflict => (
                StatusCodes.Status409Conflict,
                conflict.Code,
                conflict.Message),
            DemoConflictException conflict => (
                StatusCodes.Status409Conflict,
                conflict.Code,
                conflict.Message),
            PortalDataNotFoundException => (
                StatusCodes.Status404NotFound,
                "PORTAL_DATA_NOT_FOUND",
                "La ressource demandée est introuvable."),
            DownloadSchemaUnavailableException => (
                StatusCodes.Status503ServiceUnavailable,
                "DOWNLOADS_SCHEMA_UNAVAILABLE",
                "Le centre de téléchargements n'est pas initialisé en base de données."),
            ClientSolutionSchemaUnavailableException => (
                StatusCodes.Status503ServiceUnavailable,
                "CLIENT_SOLUTIONS_SCHEMA_UNAVAILABLE",
                "Le portail des solutions n'est pas initialisé en base de données."),
            MySqlException => (
                StatusCodes.Status503ServiceUnavailable,
                "SQL_UNAVAILABLE",
                "Le service de données est temporairement indisponible."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR",
                "Une erreur interne est survenue.")
        };

        app.Logger.LogError(
            exception,
            "Controlled request failure code {ErrorCode} status_code {StatusCode} method {Method} path {Path} correlation_id {CorrelationId} exception_type {ExceptionType}",
            code,
            statusCode,
            context.Request.Method,
            context.Request.Path,
            correlationId,
            exception?.GetType().FullName ?? "<none>");

        var auditService =
            context.RequestServices.GetRequiredService<IAuditService>();
        var session = context.Items.TryGetValue(
                "PortalSessionContext",
                out var sessionValue)
            ? sessionValue as PortalSessionContext
            : null;
        await auditService.RecordAsync(
            new AuditEvent(
                correlationId,
                exception is PortalAccessDeniedException
                    ? "security.access_denied"
                    : "request.error",
                "refused",
                code,
                CustomerId: session?.CustomerId,
                ActorUserId: session?.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        context.Response.StatusCode = statusCode;
        var apiError = new ApiError(code, message, correlationId);
        if (exposeDebugExceptionDetails && exception is not null)
        {
            var exceptionType = exception.GetType().FullName ?? "<none>";
            var exceptionMessage = SanitizeDiagnosticValue(
                exception.Message,
                diagnosticSecretValues);
            var stackTrace = SanitizeDiagnosticValue(
                exception.StackTrace,
                diagnosticSecretValues,
                6000);
            // Kestrel refuse les en-tetes non ASCII : un message metier
            // accentue ferait echouer l'ecriture de la reponse et le
            // ExceptionHandlerMiddleware relancerait l'exception d'origine
            // (erreur 500 brute au lieu du code metier attendu).
            context.Response.Headers["X-Debug-Exception-Type"] =
                ToHeaderSafeValue(exceptionType);
            context.Response.Headers["X-Debug-Exception-Message"] =
                ToHeaderSafeValue(exceptionMessage) ?? "<none>";
            context.Response.Headers["X-Debug-Correlation-Id"] =
                correlationId;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    code = apiError.Code,
                    message = apiError.Message,
                    correlation_id = apiError.CorrelationId,
                    debug = new
                    {
                        exception_type = exceptionType,
                        exception_message = exceptionMessage,
                        stack_trace = stackTrace
                    }
                });
            return;
        }

        await context.Response.WriteAsJsonAsync(apiError);
    });
});

app.UseMiddleware<ServiceAuthenticationMiddleware>();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceNames.ApiInternal)));
app.MapGet(
    "/health/live",
    () => Results.Ok(
        new OperationalHealthResponse(
            "healthy",
            "api-internal",
            "live",
            DateTime.UtcNow)));
app.MapGet(
    "/ready",
    async (
        OperationalReadinessService readinessService,
        HttpContext context) =>
    {
        var readiness = await readinessService.CheckAsync(
            context.RequestAborted);
        var payload = new OperationalHealthResponse(
            readiness.IsHealthy ? "healthy" : "unhealthy",
            "api-internal",
            "ready",
            DateTime.UtcNow,
            readiness.Checks);

        return readiness.IsHealthy
            ? Results.Ok(payload)
            : Results.Json(
                payload,
                statusCode: StatusCodes.Status503ServiceUnavailable);
    });
app.MapGet(
    "/health/ready",
    async (
        OperationalReadinessService readinessService,
        HttpContext context) =>
    {
        var readiness = await readinessService.CheckAsync(
            context.RequestAborted);
        var payload = new OperationalHealthResponse(
            readiness.IsHealthy ? "healthy" : "unhealthy",
            "api-internal",
            "ready",
            DateTime.UtcNow,
            readiness.Checks);

        return readiness.IsHealthy
            ? Results.Ok(payload)
            : Results.Json(
                payload,
                statusCode: StatusCodes.Status503ServiceUnavailable);
    });

app.MapPost(
    "/internal/auth/sessions",
    CreatePortalSession);
app.MapGet(
    "/internal/auth/session",
    GetPortalSession);
app.MapDelete(
    "/internal/auth/sessions/current",
    RevokePortalSession);
app.MapPost(
    "/internal/auth/sessions/revoke-others",
    RevokeOtherPortalSessions);

app.MapPost(
    "/internal/profile/password",
    async (
        HttpContext context,
        IActiveDirectoryService adService,
        IActiveDirectoryLinkRepository linkRepository,
        IAuthenticationRepository authenticationRepository,
        IAuthenticationService authenticationService,
        IPortalPasswordService portalPasswordService,
        IAuditService auditService,
        IAdPasswordRateLimiter rateLimiter,
        AdPasswordRuntimeConfiguration adPasswordConfig) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);

        if (!adPasswordConfig.ChangeEnabled)
        {
            await RecordAdAuditAsync(
                context,
                auditService,
                "ad.password_change",
                "refused",
                "AD_PASSWORD_CHANGE_DISABLED",
                "portal_user",
                session.UserId,
                session.UserId,
                session.CustomerId);
            return Results.Json(
                new ApiError(
                    "AD_PASSWORD_CHANGE_DISABLED",
                    "Le changement de mot de passe Active Directory est desactive.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var now = DateTime.UtcNow;
        if (rateLimiter.CheckUser(session.UserId, now)
            == AdPasswordRateLimitDecision.Locked)
        {
            await RecordAdAuditAsync(
                context,
                auditService,
                "ad.password_change",
                "refused",
                "AD_PASSWORD_CHANGE_LOCKED",
                "portal_user",
                session.UserId,
                session.UserId,
                session.CustomerId);
            return Results.Json(
                new ApiError(
                    "AD_PASSWORD_CHANGE_LOCKED",
                    "Trop de tentatives. Reessayez plus tard.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var request = await ReadPayload<ChangeAdPasswordRequest>(context);
        if (request is null
            || string.IsNullOrEmpty(request.CurrentPassword)
            || string.IsNullOrEmpty(request.NewPassword)
            || request.CurrentPassword.Length > 1024
            || request.NewPassword.Length > 1024)
        {
            await RecordAdAuditAsync(
                context,
                auditService,
                "ad.password_change",
                "refused",
                "INVALID_REQUEST",
                "portal_user",
                session.UserId,
                session.UserId,
                session.CustomerId);
            return Results.Json(
                new ApiError(
                    "INVALID_REQUEST",
                    "La requete est invalide.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var credential = await authenticationRepository.FindUserByEmailAsync(
            session.Email,
            context.RequestAborted);
        if (credential is null
            || string.IsNullOrWhiteSpace(credential.PasswordHash)
            || portalPasswordService.Verify(
                credential.Id,
                credential.PasswordHash,
                request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            rateLimiter.RegisterFailure(session.UserId, now);
            await RecordAdAuditAsync(
                context,
                auditService,
                "ad.password_change",
                "refused",
                "INVALID_CURRENT_PASSWORD",
                "portal_user",
                session.UserId,
                session.UserId,
                session.CustomerId);
            return Results.Json(
                new ApiError(
                    "INVALID_CURRENT_PASSWORD",
                    "Le mot de passe actuel est incorrect.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var link = await linkRepository.FindUserLinkByPortalUserIdAsync(
            session.UserId,
            context.RequestAborted);
        if (link is not null)
        {
            var result = await adService.SetUserPasswordAsync(
                link.CustomerReference,
                link.SamAccountName,
                request.NewPassword,
                context.RequestAborted);
            var failed = result.StatusCode >= 400 || !result.Changed;
            if (failed)
            {
                rateLimiter.RegisterFailure(session.UserId, now);
                await linkRepository.UpdateUserPasswordSyncStatusAsync(
                    session.UserId,
                    "failed",
                    now,
                    context.RequestAborted);
                var locked = rateLimiter.CheckUser(session.UserId, now)
                    == AdPasswordRateLimitDecision.Locked;
                await RecordAdAuditAsync(
                    context,
                    auditService,
                    "ad.password_change",
                    "refused",
                    locked ? "AD_PASSWORD_CHANGE_LOCKED" : result.Code,
                    "portal_user",
                    session.UserId,
                    session.UserId,
                    session.CustomerId);

                return Results.Json(
                    new ApiError(
                        locked ? "AD_PASSWORD_CHANGE_LOCKED" : "AD_PASSWORD_CHANGE_FAILED",
                        locked
                            ? "Trop de tentatives. Reessayez plus tard."
                            : "Le mot de passe ne respecte pas la politique du domaine.",
                        context.GetCorrelationId()),
                    statusCode: locked
                        ? StatusCodes.Status429TooManyRequests
                        : StatusCodes.Status400BadRequest);
            }

            await linkRepository.UpdateUserPasswordSyncStatusAsync(
                session.UserId,
                "succeeded",
                now,
                context.RequestAborted);
        }

        await authenticationRepository.UpdatePasswordHashAsync(
            session.UserId,
            portalPasswordService.HashPassword(
                session.UserId,
                request.NewPassword),
            context.RequestAborted);

        rateLimiter.Reset(session.UserId);
        await RecordAdAuditAsync(
            context,
            auditService,
            "ad.password_change",
            "success",
            "AD_PASSWORD_CHANGED",
            "portal_user",
            session.UserId,
            session.UserId,
            session.CustomerId);

        return Results.Json(
            new AdPasswordChangeResponse(
                link is null
                    ? "PORTAL_PASSWORD_CHANGED"
                    : "AD_PASSWORD_CHANGED",
                link is null
                    ? "Le mot de passe du portail a ete change."
                    : "Le mot de passe du portail a ete change et synchronise avec Active Directory.",
                adService.ModeName,
                context.GetCorrelationId()),
            statusCode: StatusCodes.Status200OK);
    });
app.MapGet(
    "/internal/portal/summary",
    async (
        HttpContext context,
        IPortalService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return PortalOk(
            context,
            service,
            await service.GetSummaryAsync(
                session,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/profile",
    async (
        HttpContext context,
        IPortalService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return PortalOk(
            context,
            service,
            await service.GetProfileAsync(
                session,
                context.RequestAborted));
    });
app.MapPost(
    "/internal/portal/profile",
    async (
        HttpContext context,
        IPortalService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);

        var payload = await ReadPayload<ClientProfileUpdate>(context);
        if (payload is null
            || string.IsNullOrWhiteSpace(payload.ContactName))
        {
            await RecordProfileAuditAsync(
                context,
                auditService,
                session,
                "refused",
                "INVALID_REQUEST");
            return Results.Json(
                new ApiError(
                    "INVALID_REQUEST",
                    "Le nom du contact principal est obligatoire.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var profile = await service.UpdateProfileAsync(
            session,
            payload,
            context.RequestAborted);

        await RecordProfileAuditAsync(
            context,
            auditService,
            session,
            "success",
            null);

        return PortalOk(
            context,
            service,
            new ClientProfileUpdateResult(
                "PROFILE_UPDATED",
                "Vos coordonnées ont été enregistrées.",
                profile,
                context.GetCorrelationId()));
    });
app.MapGet(
    "/internal/portal/downloads",
    async (
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return DownloadsOk(
            context,
            service,
            await service.GetPortalDownloadsAsync(
                session,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/downloads/{id}/file",
    async (
        string id,
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var delivery = await service.ResolvePortalDownloadAsync(
            session,
            id,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "download.deliver",
                "success",
                TargetType: "download_resource",
                TargetReference: id,
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (delivery.SourceKind == DownloadSourceKinds.ExternalUrl)
        {
            return Results.Redirect(delivery.ExternalUrl!);
        }

        return Results.File(
            delivery.File!.Stream,
            delivery.File.ContentType,
            delivery.File.FileName);
    });
app.MapGet(
    "/internal/portal/services",
    async (
        HttpContext context,
        IPortalService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return PortalOk(
            context,
            service,
            await service.GetServicesAsync(
                session,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/backups",
    async (
        HttpContext context,
        IBackupService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return BackupOk(
            context,
            service,
            await service.GetClientBackupsAsync(
                session,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/backups/{id}",
    async (
        string id,
        HttpContext context,
        IBackupService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return BackupOk(
            context,
            service,
            await service.GetClientBackupAsync(
                session,
                id,
                context.RequestAborted));
    });
app.MapPost(
    "/internal/portal/backups/{id}/restore-requests",
    async (
        string id,
        HttpContext context,
        IBackupService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var payload = await ReadPayload<BackupRestoreRequestPayload>(context);
        if (payload is null)
        {
            throw new PortalValidationException();
        }

        var result = await service.CreateRestoreRequestAsync(
            session,
            id,
            payload,
            context.GetCorrelationId(),
            context.Connection.RemoteIpAddress?.ToString(),
            context.RequestAborted);
        return BackupOk(context, service, result);
    });
app.MapGet(
    "/internal/portal/invoices",
    async (
        HttpContext context,
        IPortalService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return PortalOk(
            context,
            service,
            await service.GetInvoicesAsync(
                session,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/service-catalog",
    async (
        HttpContext context,
        IPortalService service,
        IAuthenticationService authenticationService) =>
    {
        _ = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return PortalOk(
            context,
            service,
            await service.GetServiceCatalogAsync(context.RequestAborted));
    });
// Conception commerciale V2 : catalogue des formules lisible sans session pour
// alimenter la page publique `/formules`. Lecture seule, toujours protégé par
// `X-Service-Auth` côté ingress webportal.
app.MapGet(
    "/internal/portal/billing-v2/formules",
    async (
        HttpContext context,
        IBillingV2PublicCatalogService service) =>
        Results.Ok(
            await service.GetCatalogAsync(context.RequestAborted)));

// Devis V2 : le navigateur envoie une sélection de codes catalogue, jamais un
// montant. Le total est recalculé ici par BillingV2PricingEngine. Aucune
// écriture, aucune intention créée : ce n'est pas une souscription.
app.MapPost(
    "/internal/portal/billing-v2/formules/devis",
    async (
        HttpContext context,
        IBillingV2PublicCatalogService service) =>
    {
        var payload = await ReadPayload<BillingV2PublicSelectionInput>(context);
        if (payload is null)
        {
            return Results.Json(
                new ApiError(
                    "INVALID_REQUEST",
                    "Le corps de la requete est invalide.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            return Results.Ok(
                await service.QuoteAsync(
                    payload.ToSelection(),
                    context.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Json(
                new ApiError(
                    exception.Message,
                    "La configuration demandee n'est pas disponible.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status400BadRequest);
        }
    });

// V0.27 : réception des messages du formulaire /contact (vitrine publique).
// Anonyme, protégé par `X-Service-Auth`. Rate limit appliqué côté webportal BFF.
app.MapPost(
    "/internal/public/contact-message",
    async (
        HttpContext context,
        IEmailDispatchService emailDispatch) =>
    {
        var payload = await ReadPayload<ContactMessagePayload>(context);
        var correlationId = context.GetCorrelationId();

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.Name)
            || string.IsNullOrWhiteSpace(payload.Email)
            || string.IsNullOrWhiteSpace(payload.Message))
        {
            return Results.Json(
                new ApiError(
                    "INVALID_REQUEST",
                    "Le formulaire de contact est incomplet.",
                    correlationId),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var submission = new ContactFormSubmission(
            VisitorName: payload.Name.Trim(),
            VisitorEmail: payload.Email.Trim(),
            SubjectLine: payload.Subject?.Trim() ?? string.Empty,
            Message: payload.Message,
            FormuleCode: string.IsNullOrWhiteSpace(payload.FormuleCode)
                ? null
                : payload.FormuleCode.Trim());

        var result = await emailDispatch.SendContactFormAsync(
            submission,
            correlationId,
            context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code == "NO_RECIPIENT"
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status502BadGateway;
            return Results.Json(
                new ApiError(result.Code, result.Message, correlationId),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = correlationId
        });
    });

// V0.26 : inscription self-service (anonyme, protégé par X-Service-Auth).
// hCaptcha et honeypot restent assurés côté webportal BFF, qui pose aussi un
// premier limiteur en mémoire. Le kill switch et les limites de débit
// administrables sont, eux, appliqués ici : masquer le parcours côté portail
// ne suffit pas à fermer l'inscription.
app.MapPost(
    "/internal/signup",
    async (
        HttpContext context,
        ISignupService signupService,
        IBillingV2PublicCatalogService billingV2CatalogService,
        IAuditService auditService) =>
    {
        var correlationId = context.GetCorrelationId();
        var payload = await ReadPayload<SignupSubmitPayload>(context);
        if (payload is null)
        {
            return Results.Json(
                new ApiError(
                    "INVALID_REQUEST",
                    "Le corps de la requête est invalide.",
                    correlationId),
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (payload.BillingV2Selection is not null)
        {
            try
            {
                _ = await billingV2CatalogService.QuoteAsync(
                    payload.BillingV2Selection.ToSelection(),
                    context.RequestAborted);
            }
            catch (InvalidOperationException)
            {
                return Results.Json(
                    new ApiError(
                        "INVALID_REQUEST",
                        "La configuration Billing V2 selectionnee est invalide.",
                        correlationId),
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }


        var result = await signupService.SubmitAsync(
            payload, correlationId, context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                correlationId,
                "signup.submit",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "signup",
                SourceAddress:
                    context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code switch
            {
                "SIGNUP_DISABLED" => StatusCodes.Status403Forbidden,
                "RATE_LIMITED" => StatusCodes.Status429TooManyRequests,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Json(
                new ApiError(result.Code, result.Message, correlationId),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = correlationId
        });
    });

app.MapPost(
    "/internal/signup/verify",
    async (
        HttpContext context,
        ISignupService signupService,
        IAuditService auditService) =>
    {
        var correlationId = context.GetCorrelationId();
        var payload = await ReadPayload<SignupVerifyPayload>(context);
        var result = await signupService.VerifyEmailAsync(
            payload?.Token, context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                correlationId,
                result.Succeeded
                    ? "signup.verify_success"
                    : "signup.verify_failed",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "signup",
                SourceAddress:
                    context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code == "TOKEN_EXPIRED"
                ? StatusCodes.Status410Gone
                : StatusCodes.Status400BadRequest;
            return Results.Json(
                new ApiError(result.Code, result.Message, correlationId),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = correlationId
        });
    });

app.MapPost(
    "/internal/signup/set-password",
    async (
        HttpContext context,
        ISignupService signupService,
        IAuditService auditService) =>
    {
        var correlationId = context.GetCorrelationId();
        var payload = await ReadPayload<SignupSetPasswordPayload>(context);
        var result = await signupService.SetPasswordAsync(
            payload?.Token, payload?.Password, context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                correlationId,
                result.Succeeded
                    ? "signup.password_set"
                    : "signup.password_failed",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "signup",
                SourceAddress:
                    context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code == "TOKEN_EXPIRED"
                ? StatusCodes.Status410Gone
                : StatusCodes.Status400BadRequest;
            return Results.Json(
                new ApiError(result.Code, result.Message, correlationId),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = correlationId
        });
    });

// V0.26 : validation non destructive du lien de définition de mot de passe.
// Appelée au chargement de la page /set-password pour afficher directement
// l'état « lien invalide / expiré » sans laisser l'utilisateur remplir un
// formulaire voué à l'échec. NE CONSOMME PAS le jeton : l'anti-rejeu reste
// entièrement porté par le POST /internal/signup/set-password (seul point de
// consommation), on ne trace donc pas d'audit sur cette simple lecture.
app.MapGet(
    "/internal/signup/set-password/validate",
    async (
        HttpContext context,
        ISignupService signupService) =>
    {
        var correlationId = context.GetCorrelationId();
        var token = context.Request.Query["token"].FirstOrDefault();
        var result = await signupService.ValidateSetPasswordTokenAsync(
            token, context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code == "TOKEN_EXPIRED"
                ? StatusCodes.Status410Gone
                : StatusCodes.Status400BadRequest;
            return Results.Json(
                new ApiError(result.Code, result.Message, correlationId),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = correlationId
        });
    });

// Definition du mot de passe d'un utilisateur additionnel Billing V2.
//
// Sans session portail : la personne n'a precisement pas encore de mot de
// passe. L'autorisation est portee par le jeton, dont le `purpose` est
// verifie cote service, et l'acces reste borne par X-Service-Auth comme tout
// `/internal/*`. Volontairement distinct des routes d'inscription : ni la
// meme table, ni les memes regles, et fusionner les deux ferait accepter ici
// un jeton `signup_pending`.
app.MapGet(
    "/internal/billing-v2/additional-users/password-setup/validate",
    async (
        HttpContext context,
        IBillingV2AdditionalUserIdentityService service) =>
    {
        // Lecture stricte : ne consomme pas le jeton. La consommation reste le
        // seul fait du POST, unique point d'anti-rejeu.
        var correlationId = context.GetCorrelationId();
        var result = await service.ValidateInvitationTokenAsync(
            context.Request.Query["token"].FirstOrDefault(),
            context.RequestAborted);

        return result.Succeeded
            ? Results.Ok(new
            {
                code = result.Code,
                message = result.Message,
                correlation_id = correlationId
            })
            : Results.Json(
                new ApiError(result.Code, result.Message, correlationId),
                statusCode: result.Code == PortalPasswordSetupCodes.TokenExpired
                    ? StatusCodes.Status410Gone
                    : StatusCodes.Status400BadRequest);
    });

app.MapPost(
    "/internal/billing-v2/additional-users/password-setup",
    async (
        HttpContext context,
        IBillingV2AdditionalUserIdentityService service,
        IAuditService auditService) =>
    {
        var correlationId = context.GetCorrelationId();
        var payload =
            await ReadPayload<BillingV2AdditionalUserSetPasswordPayload>(
                context);
        var result = await service.SetPasswordAsync(
            payload?.Token,
            payload?.Password,
            context.RequestAborted);

        // Ni jeton ni mot de passe dans l'audit : seul le code de resultat.
        await auditService.RecordAsync(
            new AuditEvent(
                correlationId,
                result.Succeeded
                    ? "billing_v2.additional_user.password_set"
                    : "billing_v2.additional_user.password_failed",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "billing_v2_user_identity",
                SourceAddress:
                    context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        return result.Succeeded
            ? Results.Ok(new
            {
                code = result.Code,
                message = result.Message,
                correlation_id = correlationId
            })
            : Results.Json(
                new ApiError(result.Code, result.Message, correlationId),
                statusCode: ResolveBillingV2AdditionalUserStatusCode(
                    result.Code));
    });

app.MapGet(
    "/internal/portal/commercial-documents",
    async (
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        return CommercialOk(
            context,
            service,
            await service.GetClientDocumentsAsync(
                session,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/commercial-documents/{id}",
    async (
        string id,
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        return CommercialOk(
            context,
            service,
            await service.GetClientDocumentAsync(
                session,
                id,
                context.RequestAborted));
    });
app.MapPost(
    "/internal/portal/commercial-documents/{id}/payment-method",
    async (
        string id,
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var payload = await ReadPayload<PaymentMethodSelectionPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.SelectClientDocumentPaymentMethodAsync(
            session,
            id,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.payment_method_selected",
                result.Changed ? "success" : "unchanged",
                TargetType: "commercial_document",
                TargetReference: id,
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return Results.Ok(result);
    });
app.MapPost(
    "/internal/portal/commercial-documents/{id}/payment-confirm",
    async (
        string id,
        HttpContext context,
        IInvoiceIssuingService issuingService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var confirmPayload = await ReadPayload<PaymentConfirmPayload>(context);
        var paymentMethod = string.Equals(
            confirmPayload?.PaymentMethod,
            "stripe",
            StringComparison.Ordinal)
            ? "stripe"
            : "paypal";
        var result = await issuingService.ConfirmPaymentAsync(
            id,
            context.GetCorrelationId(),
            paymentMethod,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.payment_confirm",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "commercial_document",
                TargetReference: id,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code == "INVOICE_NOT_FOUND"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Results.Json(
                new ApiError(result.Code, result.Message, context.GetCorrelationId()),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            invoice = result.Invoice,
            correlation_id = context.GetCorrelationId()
        });
    });
app.MapGet(
    "/internal/portal/commercial-documents/{id}/invoice",
    async (
        string id,
        HttpContext context,
        ICommercialService commercialService,
        IInvoiceIssuingService issuingService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        await commercialService.GetClientDocumentAsync(
            session,
            id,
            context.RequestAborted);
        var record = await issuingService.GetInvoiceRecordAsync(
            id, context.RequestAborted);
        if (record is null)
        {
            return Results.Ok<BpceIssuedInvoiceInfo?>(null);
        }

        if (record.PdfHash is null)
        {
            var fetched = await issuingService.EnsureInvoicePdfAsync(
                id, context.RequestAborted);
            if (fetched is not null)
            {
                record = await issuingService.GetInvoiceRecordAsync(
                    id, context.RequestAborted) ?? record;
            }
        }

        return Results.Ok<BpceIssuedInvoiceInfo?>(new BpceIssuedInvoiceInfo(
            record.BpceInvoiceId,
            record.FiscalNumber,
            record.Status,
            record.IssueDate,
            record.TotalAmountCents,
            record.Currency,
            record.PdfHash is not null));
    });
app.MapGet(
    "/internal/portal/commercial-documents/{id}/invoice/pdf",
    async (
        string id,
        HttpContext context,
        ICommercialService commercialService,
        IInvoiceIssuingService issuingService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var document = await commercialService.GetClientDocumentAsync(
            session,
            id,
            context.RequestAborted);
        if (document.Status != "issued" && document.Status != "paid")
        {
            return Results.Json(
                new ApiError(
                    "INVOICE_NOT_AVAILABLE",
                    "The invoice is not available for this document.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status404NotFound);
        }

        var record = await issuingService.GetInvoiceRecordAsync(
            id, context.RequestAborted);
        if (record is null)
        {
            return Results.Json(
                new ApiError(
                    "INVOICE_NOT_FOUND",
                    "No issued invoice found for this document.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status404NotFound);
        }

        var pdf = await issuingService.EnsureInvoicePdfAsync(
            id, context.RequestAborted);
        if (pdf is null)
        {
            return Results.Json(
                new ApiError(
                    "PDF_NOT_AVAILABLE",
                    "The invoice PDF is not yet available.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status404NotFound);
        }

        var filename = $"facture-{record.FiscalNumber ?? record.BpceInvoiceId}.pdf";
        return Results.File(pdf, "application/pdf", filename);
    });

app.MapGet(
    "/internal/portal/subscriptions",
    async (
        HttpContext context,
        IBillingV2SubscriptionAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        return SubscriptionOk(
            context,
            service,
            await service.GetClientSubscriptionsAsync(
                session,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/pending-billing-v2-selection",
    async (
        HttpContext context,
        ISignupService signupService,
        IBillingV2SubscriptionAdministrationService subscriptionService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var pendingSelection = await signupService.GetPendingBillingV2SelectionAsync(
            session,
            context.RequestAborted);
        if (pendingSelection is null)
        {
            return Results.Json<PendingBillingV2SelectionSummary?>(null);
        }

        var subscriptions = await subscriptionService.GetClientSubscriptionsAsync(
            session,
            context.RequestAborted);
        var hasOpenBillingV2Subscription = subscriptions.Any(subscription =>
            string.Equals(
                subscription.BillingSystem,
                "billing_v2",
                StringComparison.Ordinal)
            && subscription.Status is not "cancelled"
            && subscription.Status is not "expired");
        if (hasOpenBillingV2Subscription)
        {
            return Results.Json<PendingBillingV2SelectionSummary?>(null);
        }

        context.Response.Headers["X-Data-Source"] =
            signupService.IsPersistent ? "mariadb" : "mock";
        return Results.Json(pendingSelection);
    });

app.MapGet(
    "/internal/portal/content/{key}",
    async (
        string key,
        HttpContext context,
        IManagedContentService service) =>
    {
        return ManagedContentOk(
            context,
            service,
            await service.GetPublicAsync(key, context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/content",
    async (
        HttpContext context,
        IManagedContentService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.catalog.read");
        return ManagedContentOk(
            context,
            service,
            await service.GetAdminListAsync(context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/content/{key}",
    async (
        string key,
        HttpContext context,
        IManagedContentService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.catalog.read");
        return ManagedContentOk(
            context,
            service,
            await service.GetAdminDetailAsync(key, context.RequestAborted));
    });
app.MapPatch(
    "/internal/admin/content/{key}",
    async (
        string key,
        HttpContext context,
        IManagedContentService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.catalog.write");
        var payload = await ReadPayload<ManagedContentPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.UpsertAsync(
            key,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "managed_content.update",
                result.Changed ? "success" : "unchanged",
                TargetType: "managed_content",
                TargetReference: key,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return ManagedContentOk(context, service, result);
    });
app.MapGet(
    "/internal/portal/billing-configuration",
    async (HttpContext context, IApplicationSettingsService service, IAuthenticationService authenticationService, IConfiguration configuration) =>
    {
        await ResolveClientSessionAsync(context, authenticationService, context.RequestServices.GetRequiredService<IAuditService>());
        var fallback = new PortalBillingConfiguration(configuration["BILLING_IBAN"]?.Trim(), configuration["BILLING_BIC"]?.Trim(), configuration["BILLING_PAYPAL_URL"]?.Trim(), configuration["BILLING_TRANSFER_LABEL"]?.Trim() ?? "Zachary HOUNSA-HOUNKPA EI");
        context.Response.Headers["X-Data-Source"] = service.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(await service.GetPortalBillingConfigurationAsync(fallback, context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/settings/status",
    async (HttpContext context, IConfigurationStatusService service, IEditorialRepository editorialRepository, IAuthenticationService authenticationService, IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(context, authenticationService, auditService, "admin.settings.read");
        if (!await editorialRepository.HasAdminPermissionAsync(actor.UserId, "settings.read", context.RequestAborted)) throw new PortalAccessDeniedException();
        return Results.Ok(service.GetSnapshot());
    });
// Textes courts affiches par le portail public (confirmation de formulaire,
// mention RGPD...). Aucun secret, aucune donnee client : lecture anonyme.
// Le parcours public ne lit que la version publiee ; `configuration` vaut
// `null` tant qu'aucune publication n'a eu lieu et le WebPortal retombe alors
// sur la configuration integree a son code.
app.MapGet(
    "/internal/public/diagnostic/configuration",
    async (HttpContext context, IDiagnosticConfigurationService service) =>
        Results.Ok(await service.GetPublishedAsync(context.RequestAborted)));
app.MapGet(
    "/internal/public/system-snippets",
    async (
        HttpContext context,
        ICommunicationTemplateService service) =>
        Results.Ok(new PublicSystemSnippets(
            await service.GetPublicSnippetsAsync(context.RequestAborted))));
app.MapGet(
    "/internal/public/editorial/wiki/home",
    async (
        HttpContext context,
        IEditorialService service) =>
        EditorialOk(
            context,
            service,
            await service.GetPublicWikiHomeAsync(context.RequestAborted)));
app.MapGet(
    "/internal/public/editorial/wiki/search",
    async (
        HttpContext context,
        IEditorialService service) =>
    {
        var query = context.Request.Query["query"].FirstOrDefault() ?? "";
        return EditorialOk(
            context,
            service,
            await service.SearchPublicWikiAsync(query, context.RequestAborted));
    });
app.MapGet(
    "/internal/public/editorial/wiki/articles/{slug}",
    async (
        string slug,
        HttpContext context,
        IEditorialService service) =>
        EditorialOk(
            context,
            service,
            await service.GetPublicBySlugAsync(
                EditorialContentTypes.WikiArticle,
                slug,
                context.RequestAborted)));
app.MapGet(
    "/internal/public/editorial/seo-pages/{slug}",
    async (
        string slug,
        HttpContext context,
        IEditorialService service) =>
        EditorialOk(
            context,
            service,
            await service.GetPublicBySlugAsync(
                EditorialContentTypes.SeoPage,
                slug,
                context.RequestAborted)));
app.MapGet(
    "/internal/public/editorial/faq/{scope}",
    async (
        string scope,
        HttpContext context,
        IEditorialService service) =>
        EditorialOk(
            context,
            service,
            await service.GetPublicFaqAsync(scope, context.RequestAborted)));
app.MapGet(
    "/internal/public/editorial/sitemap",
    async (
        HttpContext context,
        IEditorialService service) =>
        EditorialOk(
            context,
            service,
            await service.GetPublicSitemapAsync(context.RequestAborted)));
app.MapGet(
    "/internal/public/editorial/redirects",
    async (
        HttpContext context,
        IEditorialService service) =>
    {
        var oldPath = context.Request.Query["oldPath"].FirstOrDefault()
            ?? throw new PortalValidationException();
        return EditorialOk(
            context,
            service,
            await service.GetRedirectAsync(oldPath, context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/editorial",
    async (
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.editorial.read");
        var contentType = context.Request.Query["contentType"].FirstOrDefault();
        await service.EnsurePermissionAsync(
            actor,
            EditorialReadPermission(contentType),
            context.RequestAborted);
        return EditorialOk(
            context,
            service,
            await service.GetAdminListAsync(
                contentType,
                context.Request.Query["status"].FirstOrDefault(),
                context.Request.Query["query"].FirstOrDefault(),
                context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/editorial",
    async (
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.editorial.write");
        var payload = await ReadPayload<EditorialContentPayload>(context)
            ?? throw new PortalValidationException();
        await service.EnsurePermissionAsync(
            actor,
            EditorialWritePermission(payload.ContentType),
            context.RequestAborted);
        var result = await service.UpsertContentAsync(
            null,
            payload,
            actor,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "editorial.create",
                result.Changed ? "success" : "unchanged",
                TargetType: "editorial_content",
                TargetReference: result.Id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return EditorialOk(context, service, result);
    });
app.MapGet(
    "/internal/admin/editorial/{id}",
    async (
        string id,
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.editorial.read");
        var content = await service.GetAdminContentAsync(id, context.RequestAborted);
        await service.EnsurePermissionAsync(
            actor,
            EditorialReadPermission(content.ContentType),
            context.RequestAborted);
        return EditorialOk(context, service, content);
    });
app.MapPatch(
    "/internal/admin/editorial/{id}",
    async (
        string id,
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.editorial.write");
        var payload = await ReadPayload<EditorialContentPayload>(context)
            ?? throw new PortalValidationException();
        await service.EnsurePermissionAsync(
            actor,
            EditorialWritePermission(payload.ContentType),
            context.RequestAborted);
        var result = await service.UpsertContentAsync(
            id,
            payload,
            actor,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "editorial.update",
                result.Changed ? "success" : "unchanged",
                TargetType: "editorial_content",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return EditorialOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/editorial/{id}/publish",
    async (
        string id,
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.publish");
        await service.EnsurePermissionAsync(
            actor,
            "content.publish",
            context.RequestAborted);
        var result = await service.PublishAsync(
            id,
            actor,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "editorial.publish",
                "success",
                TargetType: "editorial_content",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return EditorialOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/editorial/{id}/archive",
    async (
        string id,
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.editorial.archive");
        await service.EnsurePermissionAsync(
            actor,
            "content.publish",
            context.RequestAborted);
        var result = await service.ArchiveAsync(
            id,
            actor,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "editorial.archive",
                "success",
                TargetType: "editorial_content",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return EditorialOk(context, service, result);
    });
app.MapGet(
    "/internal/admin/editorial/{id}/revisions",
    async (
        string id,
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.editorial.revisions.read");
        var content = await service.GetAdminContentAsync(id, context.RequestAborted);
        await service.EnsurePermissionAsync(
            actor,
            EditorialReadPermission(content.ContentType),
            context.RequestAborted);
        return EditorialOk(
            context,
            service,
            await service.GetRevisionsAsync(id, context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/editorial/revisions/{revisionId}",
    async (
        string revisionId,
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.editorial.revisions.read");
        return EditorialOk(
            context,
            service,
            await service.GetRevisionAsync(revisionId, context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/editorial/revisions/{revisionId}/restore",
    async (
        string revisionId,
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.editorial.restore");
        await service.EnsurePermissionAsync(
            actor,
            "content.publish",
            context.RequestAborted);
        var result = await service.RestoreRevisionAsync(
            revisionId,
            actor,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "editorial.restore",
                "success",
                TargetType: "editorial_revision",
                TargetReference: revisionId,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return EditorialOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/editorial/categories",
    async (
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.editorial.categories.write");
        var payload = await ReadPayload<EditorialCategoryPayload>(context)
            ?? throw new PortalValidationException();
        await service.EnsurePermissionAsync(
            actor,
            EditorialWritePermission(payload.ContentType),
            context.RequestAborted);
        return EditorialOk(
            context,
            service,
            await service.UpsertCategoryAsync(
                null,
                payload,
                actor,
                context.RequestAborted));
    });
app.MapPatch(
    "/internal/admin/editorial/categories/{id}",
    async (
        string id,
        HttpContext context,
        IEditorialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "content.editorial.categories.write");
        var payload = await ReadPayload<EditorialCategoryPayload>(context)
            ?? throw new PortalValidationException();
        await service.EnsurePermissionAsync(
            actor,
            EditorialWritePermission(payload.ContentType),
            context.RequestAborted);
        return EditorialOk(
            context,
            service,
            await service.UpsertCategoryAsync(
                id,
                payload,
                actor,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/download-categories",
    async (
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.read");
        return DownloadsOk(
            context,
            service,
            await service.GetAdminCategoriesAsync(context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/download-categories",
    async (
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.write");
        var payload = await ReadPayload<DownloadCategoryPayload>(context)
            ?? throw new PortalValidationException();
        DownloadCategoryMutationResponse result;
        try
        {
            result = await service.CreateCategoryAsync(
                payload,
                context.GetCorrelationId(),
                context.RequestAborted);
        }
        catch (DownloadConflictException conflict)
        {
            return DownloadsError(
                context,
                service,
                StatusCodes.Status409Conflict,
                conflict.Code,
                conflict.Message);
        }
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "download_category.create",
                result.Changed ? "success" : "unchanged",
                TargetType: "download_category",
                TargetReference: result.Id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DownloadsOk(context, service, result);
    });
app.MapPatch(
    "/internal/admin/download-categories/{id}",
    async (
        string id,
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.write");
        var payload = await ReadPayload<DownloadCategoryPayload>(context)
            ?? throw new PortalValidationException();
        DownloadCategoryMutationResponse result;
        try
        {
            result = await service.UpdateCategoryAsync(
                id,
                payload,
                context.GetCorrelationId(),
                context.RequestAborted);
        }
        catch (DownloadConflictException conflict)
        {
            return DownloadsError(
                context,
                service,
                StatusCodes.Status409Conflict,
                conflict.Code,
                conflict.Message);
        }
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "download_category.update",
                result.Changed ? "success" : "unchanged",
                TargetType: "download_category",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DownloadsOk(context, service, result);
    });
app.MapDelete(
    "/internal/admin/download-categories/{id}",
    async (
        string id,
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.write");
        DownloadCategoryMutationResponse result;
        try
        {
            result = await service.DeleteCategoryAsync(
                id,
                context.GetCorrelationId(),
                context.RequestAborted);
        }
        catch (DownloadConflictException conflict)
        {
            return DownloadsError(
                context,
                service,
                StatusCodes.Status409Conflict,
                conflict.Code,
                conflict.Message);
        }
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "download_category.delete",
                result.Changed ? "success" : "unchanged",
                TargetType: "download_category",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DownloadsOk(context, service, result);
    });
app.MapGet(
    "/internal/admin/downloads",
    async (
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.read");
        return DownloadsOk(
            context,
            service,
            await service.GetAdminDownloadsAsync(context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/downloads",
    async (
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.write");
        var payload = await ReadPayload<DownloadResourcePayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.CreateResourceAsync(
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "download_resource.create",
                result.Changed ? "success" : "unchanged",
                TargetType: "download_resource",
                TargetReference: result.Id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DownloadsOk(context, service, result);
    });
app.MapGet(
    "/internal/admin/downloads/{id}",
    async (
        string id,
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.read");
        return DownloadsOk(
            context,
            service,
            await service.GetAdminDownloadAsync(
                id,
                context.RequestAborted));
    });
app.MapPatch(
    "/internal/admin/downloads/{id}",
    async (
        string id,
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.write");
        var payload = await ReadPayload<DownloadResourcePayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.UpdateResourceAsync(
            id,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "download_resource.update",
                result.Changed ? "success" : "unchanged",
                TargetType: "download_resource",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DownloadsOk(context, service, result);
    });
app.MapDelete(
    "/internal/admin/downloads/{id}",
    async (
        string id,
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.write");
        var result = await service.DeleteResourceAsync(
            id,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "download_resource.delete",
                result.Changed ? "success" : "unchanged",
                TargetType: "download_resource",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DownloadsOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/downloads/{id}/file",
    async (
        string id,
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.write");
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var file = form.Files.GetFile("file")
            ?? throw new PortalValidationException();
        await using var stream = file.OpenReadStream();
        var result = await service.UploadResourceFileAsync(
            id,
            file.FileName,
            file.ContentType,
            stream,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "download_resource.file.upload",
                result.Changed ? "success" : "unchanged",
                TargetType: "download_resource",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DownloadsOk(context, service, result);
    });
app.MapDelete(
    "/internal/admin/downloads/{id}/file",
    async (
        string id,
        HttpContext context,
        IDownloadService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.downloads.write");
        var result = await service.DeleteResourceFileAsync(
            id,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "download_resource.file.delete",
                result.Changed ? "success" : "unchanged",
                TargetType: "download_resource",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DownloadsOk(context, service, result);
    });
app.MapGet(
    "/internal/portal/client-solutions",
    async (
        HttpContext context,
        IClientSolutionService service) =>
    {
        return ClientSolutionsOk(
            context,
            service,
            await service.GetPublicPortalAsync(context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/client-solutions/{id}/logo",
    async (
        string id,
        HttpContext context,
        IClientSolutionService service) =>
    {
        var logo = await service.GetPublicLogoAsync(id, context.RequestAborted);
        // Un logo est un media administre : on neutralise tout script embarque
        // (cas du SVG) et on empeche la reinterpretation du type MIME.
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        context.Response.Headers["Cache-Control"] = "public, max-age=300";
        return Results.File(
            logo.Bytes,
            logo.ContentType,
            enableRangeProcessing: false);
    });
app.MapGet(
    "/internal/admin/client-solutions",
    async (
        HttpContext context,
        IClientSolutionService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.client_solutions.read");
        return ClientSolutionsOk(
            context,
            service,
            await service.GetAdminPortalAsync(context.RequestAborted));
    });
app.MapPatch(
    "/internal/admin/client-solutions/settings",
    async (
        HttpContext context,
        IClientSolutionService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.client_solutions.write");
        var payload =
            await ReadPayload<ClientSolutionPortalSettingsPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.UpdateSettingsAsync(
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "client_solution_portal.update",
                result.Changed ? "success" : "unchanged",
                TargetType: "client_solution_portal",
                TargetReference: "default",
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return ClientSolutionsOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/client-solutions",
    async (
        HttpContext context,
        IClientSolutionService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.client_solutions.write");
        var payload = await ReadPayload<ClientSolutionPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.CreateSolutionAsync(
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "client_solution.create",
                result.Changed ? "success" : "unchanged",
                TargetType: "client_solution",
                TargetReference: result.Id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return ClientSolutionsOk(context, service, result);
    });
app.MapGet(
    "/internal/admin/client-solutions/{id}",
    async (
        string id,
        HttpContext context,
        IClientSolutionService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.client_solutions.read");
        return ClientSolutionsOk(
            context,
            service,
            await service.GetAdminSolutionAsync(id, context.RequestAborted));
    });
app.MapPatch(
    "/internal/admin/client-solutions/{id}",
    async (
        string id,
        HttpContext context,
        IClientSolutionService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.client_solutions.write");
        var payload = await ReadPayload<ClientSolutionPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.UpdateSolutionAsync(
            id,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "client_solution.update",
                result.Changed ? "success" : "unchanged",
                TargetType: "client_solution",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return ClientSolutionsOk(context, service, result);
    });
app.MapDelete(
    "/internal/admin/client-solutions/{id}",
    async (
        string id,
        HttpContext context,
        IClientSolutionService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.client_solutions.write");
        var result = await service.DeleteSolutionAsync(
            id,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "client_solution.delete",
                result.Changed ? "success" : "unchanged",
                TargetType: "client_solution",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return ClientSolutionsOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/client-solutions/{id}/logo",
    async (
        string id,
        HttpContext context,
        IClientSolutionService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.client_solutions.write");
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var file = form.Files.GetFile("logo")
            ?? throw new PortalValidationException();
        await using var stream = file.OpenReadStream();
        var result = await service.UploadLogoAsync(
            id,
            file.FileName,
            file.ContentType,
            stream,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "client_solution.logo.upload",
                result.Changed ? "success" : "unchanged",
                TargetType: "client_solution",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return ClientSolutionsOk(context, service, result);
    });
app.MapDelete(
    "/internal/admin/client-solutions/{id}/logo",
    async (
        string id,
        HttpContext context,
        IClientSolutionService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.client_solutions.write");
        var result = await service.DeleteLogoAsync(
            id,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "client_solution.logo.delete",
                result.Changed ? "success" : "unchanged",
                TargetType: "client_solution",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return ClientSolutionsOk(context, service, result);
    });
app.MapGet(
    "/internal/portal/public-pack-catalog",
    async (
        HttpContext context,
        IPublicPackCatalogService service) =>
    {
        return PublicPackCatalogOk(
            context,
            service,
            await service.GetAsync(context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/public-pack-catalog",
    async (
        HttpContext context,
        IPublicPackCatalogService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.catalog.read");
        return PublicPackCatalogOk(
            context,
            service,
            await service.GetAsync(context.RequestAborted));
    });
app.MapPatch(
    "/internal/admin/public-pack-catalog",
    async (
        HttpContext context,
        IPublicPackCatalogService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.catalog.write");
        var payload = await ReadPayload<PublicPackCatalogContentPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.UpsertAsync(
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "public_pack_catalog.update",
                result.Changed ? "success" : "unchanged",
                TargetType: "public_pack_catalog",
                TargetReference: "public-pack-catalog",
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return PublicPackCatalogOk(context, service, result);
    });
app.MapPost(
    "/internal/portal/billing-v2/subscriptions/checkout",
    async (
        HttpContext context,
        IBillingV2AuthoritativeCheckoutService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var payload =
            await ReadPayload<BillingV2AuthoritativeCheckoutPayload>(context)
            ?? throw new PortalValidationException();
        // Une demande porte une selection V2 native, sous l'une de ses deux
        // formes : une formule, ou des composants choisis directement. Sans
        // l'une des deux, elle n'a pas d'identite metier.
        var hasSelection =
            !string.IsNullOrWhiteSpace(payload.Selection?.PresetCode)
            || payload.Selection?.Components is { Count: > 0 };
        if (!hasSelection
            || string.IsNullOrWhiteSpace(payload.Provider)
            || string.IsNullOrWhiteSpace(payload.IdempotencyKey)
            || string.IsNullOrWhiteSpace(payload.SuccessUrl)
            || string.IsNullOrWhiteSpace(payload.CancelUrl))
        {
            throw new PortalValidationException();
        }

        BillingV2AuthoritativeCheckoutResult result;
        try
        {
            result = await service.CreateAsync(
                session,
                new BillingV2AuthoritativeCheckoutRequest(
                    payload.Selection!.ToSelection(),
                    payload.Provider.Trim(),
                    payload.IdempotencyKey.Trim(),
                    payload.SuccessUrl.Trim(),
                    payload.CancelUrl.Trim()),
                context.GetCorrelationId(),
                context.RequestAborted);
        }
        catch (InvalidOperationException exception)
            when (exception.Message.StartsWith(
                "BILLING_V2_",
                StringComparison.Ordinal))
        {
            return Results.Json(
                new ApiError(
                    "BILLING_V2_CHECKOUT_NOT_READY",
                    exception.Message,
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status409Conflict);
        }

        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "billing_v2.authoritative_checkout_requested",
                result.Created ? "success" : "unchanged",
                TargetType: "billing_v2_subscription",
                TargetReference: result.SubscriptionId,
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return Results.Ok(new BillingV2AuthoritativeCheckoutResponse(
            result.Created,
            result.SubscriptionId,
            result.Provider,
            result.Environment,
            result.OutboxEventId,
            result.IdempotencyKeyHash,
            result.TotalDueNowCents,
            result.ReasonCode,
            result.ApprovalUrl,
            context.GetCorrelationId()));
    });
app.MapPost(
    "/internal/portal/billing-v2/provider-return",
    async (
        HttpContext context,
        IBillingV2ProviderInboundEventService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var payload = await ReadPayload<BillingV2ProviderReturnPayload>(context)
            ?? throw new PortalValidationException();
        if (string.IsNullOrWhiteSpace(payload.Provider)
            || (string.IsNullOrWhiteSpace(payload.ProviderCheckoutId)
                && string.IsNullOrWhiteSpace(payload.ProviderSubscriptionId)))
        {
            throw new PortalValidationException();
        }

        var environment = payload.Provider.Trim().ToLowerInvariant() == "stripe"
            ? stripeConfiguration.ModeName
            : paypalConfiguration.ModeName;
        var request = BillingV2ProviderInboundEventExtractor.CreateProviderReturn(
            payload.Provider,
            environment,
            payload.ProviderCheckoutId,
            payload.ProviderSubscriptionId,
            payload.RawPayload,
            session.CustomerId);
        var result = await service.ProcessAsync(
            request,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "billing_v2.provider_return_received",
                result.Applied ? "success" : "unchanged",
                ReasonCode: result.ReasonCode,
                TargetType: "billing_v2_subscription",
                TargetReference: result.SubscriptionId,
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return Results.Ok(new
        {
            applied = result.Applied,
            reason_code = result.ReasonCode,
            subscription_id = result.SubscriptionId,
            checkout_session_id = result.CheckoutSessionId,
            correlation_id = context.GetCorrelationId()
        });
    });
app.MapPost(
    "/internal/portal/subscriptions/{id}/cancel",
    async (
        string id,
        HttpContext context,
        IBillingV2SubscriptionAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var result = await service.ClientCancelAsync(
            session,
            id,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "subscription.client_cancel",
                "success",
                TargetType: "subscription",
                TargetReference: result.Id,
                CustomerId: result.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return SubscriptionOk(context, service, result);
    });

app.MapGet(
    "/internal/portal/billing-v2/subscriptions/{subscriptionId}/users",
    async (
        string subscriptionId,
        HttpContext context,
        IBillingV2AdditionalUserIdentityService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        // Le client vient de la session, jamais de la requete : un abonnement
        // d'un autre client renvoie une liste vide, indistinguable d'un
        // abonnement inexistant.
        var slots = await service.ListSlotsAsync(
            session.CustomerId,
            subscriptionId,
            context.RequestAborted);
        return Results.Ok(slots);
    });

app.MapPost(
    "/internal/portal/billing-v2/subscriptions/{subscriptionId}/users/{subscriptionUserId}/assign",
    async (
        string subscriptionId,
        string subscriptionUserId,
        HttpContext context,
        IBillingV2AdditionalUserIdentityService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var payload =
            await ReadPayload<BillingV2AdditionalUserAssignPayload>(context)
            ?? throw new PortalValidationException();

        DateOnly? birthDate = null;
        if (!string.IsNullOrWhiteSpace(payload.BirthDate))
        {
            // Une date illisible est refusee, jamais ignoree : la laisser
            // tomber creerait une identite incomplete sans que personne ne
            // l'ait demande.
            if (!DateOnly.TryParse(
                    payload.BirthDate,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                throw new PortalValidationException();
            }

            birthDate = parsed;
        }

        var result = await service.AssignAsync(
            new BillingV2AdditionalUserAssignment(
                session.CustomerId,
                subscriptionId,
                subscriptionUserId,
                payload.Email ?? string.Empty,
                payload.DisplayName ?? string.Empty,
                payload.PersonalTitle,
                payload.GivenName,
                payload.Surname,
                birthDate,
                payload.Initials,
                payload.Phone,
                session.UserId),
            context.GetCorrelationId(),
            context.RequestAborted);

        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "billing_v2.additional_user.assign",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "billing_v2_subscription_user",
                TargetReference: subscriptionUserId,
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        return BillingV2AdditionalUserResponse(context, result);
    });

app.MapPost(
    "/internal/portal/billing-v2/subscriptions/{subscriptionId}/users/{subscriptionUserId}/resend-invitation",
    async (
        string subscriptionId,
        string subscriptionUserId,
        HttpContext context,
        IBillingV2AdditionalUserIdentityService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var result = await service.ResendInvitationAsync(
            subscriptionUserId,
            subscriptionId,
            session.CustomerId,
            context.GetCorrelationId(),
            context.RequestAborted);

        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "billing_v2.additional_user.resend_invitation",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "billing_v2_subscription_user",
                TargetReference: subscriptionUserId,
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        return BillingV2AdditionalUserResponse(context, result);
    });

app.MapPost(
    "/internal/webhooks/paypal",
    async (
        HttpContext context,
        IBillingV2ProviderInboundEventService billingV2InboundService) =>
    {
        var payload = await ReadPayload<PayPalWebhookEventPayload>(context)
            ?? throw new PortalValidationException();
        // Un evenement que Billing V2 ne sait pas rattacher n'a plus de second
        // destinataire : il est acquitte et ignore. Le repli legacy ecrivait
        // dans un systeme d'abonnement qui n'existe plus.
        var billingV2Request =
            BillingV2ProviderInboundEventExtractor.TryCreatePayPalWebhook(
                payload,
                paypalConfiguration.ModeName);
        if (billingV2Request is null)
        {
            return Results.Ok(new
            {
                event_id = payload.EventId,
                status = "ignored",
                error_message = (string?)null,
                correlation_id = context.GetCorrelationId()
            });
        }

        var billingV2Result = await billingV2InboundService.ProcessAsync(
            billingV2Request,
            context.RequestAborted);
        return Results.Ok(new
        {
            event_id = payload.EventId,
            status = billingV2Result.Applied ? "processed" : "ignored",
            error_message = (string?)null,
            billing_v2 = new
            {
                applied = billingV2Result.Applied,
                reason_code = billingV2Result.ReasonCode,
                subscription_id = billingV2Result.SubscriptionId,
                checkout_session_id = billingV2Result.CheckoutSessionId
            },
            correlation_id = context.GetCorrelationId()
        });
    });

app.MapPost(
    "/internal/webhooks/stripe",
    async (
        HttpContext context,
        IBillingV2ProviderInboundEventService billingV2InboundService,
        ICommercialDocumentStripePaymentService documentPaymentService) =>
    {
        var payload = await ReadPayload<StripeWebhookEventPayload>(context)
            ?? throw new PortalValidationException();
        var billingV2Request =
            BillingV2ProviderInboundEventExtractor.TryCreateStripeWebhook(
                payload,
                stripeConfiguration.ModeName);
        if (billingV2Request is null)
        {
            // Deux rails distincts partagent ce webhook. Billing V2 traite les
            // abonnements ; un reglement ponctuel de document commercial n'a
            // pas d'autre chemin de confirmation, le retour navigateur Stripe
            // n'etant qu'une redirection. Sans cette branche, une facture
            // reglee par carte resterait impayee cote BPCE.
            var documentStatus =
                await documentPaymentService.HandlePaymentIntentSucceededAsync(
                    payload,
                    context.GetCorrelationId(),
                    context.RequestAborted);
            return Results.Ok(new
            {
                event_id = payload.EventId,
                status = documentStatus,
                error_message = (string?)null,
                correlation_id = context.GetCorrelationId()
            });
        }

        var billingV2Result = await billingV2InboundService.ProcessAsync(
            billingV2Request,
            context.RequestAborted);
        return Results.Ok(new
        {
            event_id = payload.EventId,
            status = billingV2Result.Applied ? "processed" : "ignored",
            error_message = (string?)null,
            billing_v2 = new
            {
                applied = billingV2Result.Applied,
                reason_code = billingV2Result.ReasonCode,
                subscription_id = billingV2Result.SubscriptionId,
                checkout_session_id = billingV2Result.CheckoutSessionId
            },
            correlation_id = context.GetCorrelationId()
        });
    });

app.MapPost(
    "/internal/backups/report",
    async (
        HttpContext context,
        IBackupService service,
        IAuditService auditService) =>
    {
        var payload = await ReadPayload<BackupReportPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.IngestReportAsync(
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "backup.report.ingest",
                result.Mapped ? "success" : "refused",
                result.Mapped ? null : "BACKUP_MAPPING_NOT_FOUND",
                "backup_report",
                result.BackupJobId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return BackupOk(context, service, result);
    });

app.MapGet(
    "/internal/portal/support-requests",
    async (
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return WorkflowOk(
            context,
            service,
            await service.GetClientSupportRequestsAsync(
                session,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/service-requests",
    async (
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return WorkflowOk(
            context,
            service,
            await service.GetClientServiceRequestsAsync(
                session,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/support-requests/{id}",
    async (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return WorkflowOk(
            context,
            service,
            await service.GetClientSupportRequestAsync(
                session,
                id,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/portal/service-requests/{id}",
    async (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return WorkflowOk(
            context,
            service,
            await service.GetClientServiceRequestAsync(
                session,
                id,
                context.RequestAborted));
    });
app.MapPost(
    "/internal/portal/support-requests/{id}/messages",
    (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
        AddClientRequestMessage(
            id,
            RequestTypes.Support,
            context,
            service,
            authenticationService,
            auditService));
app.MapPost(
    "/internal/portal/service-requests/{id}/messages",
    (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
        AddClientRequestMessage(
            id,
            RequestTypes.Service,
            context,
            service,
            authenticationService,
            auditService));
app.MapGet(
    "/internal/portal/notifications",
    async (
        HttpContext context,
        IPortalNotificationService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        return NotificationOk(
            context,
            service,
            await service.GetNotificationsAsync(
                session,
                context.RequestAborted));
    });
app.MapPost(
    "/internal/portal/notifications/read-all",
    async (
        HttpContext context,
        IPortalNotificationService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        var result = await service.MarkAllAsReadAsync(
            session,
            context.GetCorrelationId(),
            context.RequestAborted);
        return NotificationOk(context, service, result);
    });
app.MapPost(
    "/internal/portal/notifications/{id}/read",
    async (
        string id,
        HttpContext context,
        IPortalNotificationService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            context.RequestServices.GetRequiredService<IAuditService>());
        var result = await service.MarkAsReadAsync(
            session,
            id,
            context.GetCorrelationId(),
            context.RequestAborted);
        return NotificationOk(context, service, result);
    });
app.MapPost(
    "/internal/portal/support-requests",
    CreateSupportRequest);
app.MapPost(
    "/internal/portal/service-requests",
    CreateServiceRequest);
app.MapGet(
    "/internal/koxo/users",
    async (
        HttpContext context,
        IKoxoExportService service,
        IAuditService auditService) =>
    {
        var correlationId = context.GetCorrelationId();
        var sourceAddress =
            context.Request.Headers["X-Koxo-Source-Address"].FirstOrDefault()
            ?? context.Connection.RemoteIpAddress?.ToString();

        try
        {
            var payload = await service.ExportAsync(
                "api",
                correlationId,
                sourceAddress,
                context.RequestAborted);
            context.Response.Headers["X-Data-Source"] =
                service.IsPersistent ? "mariadb" : "mock";
            await auditService.RecordAsync(
                new AuditEvent(
                    correlationId,
                    "koxo.export.read",
                    "success",
                    TargetType: "koxo_export",
                    SourceAddress: sourceAddress),
                context.RequestAborted);
            return Results.Ok(payload);
        }
        catch (KoxoValidationException exception)
        {
            await auditService.RecordAsync(
                new AuditEvent(
                    correlationId,
                    "koxo.export.read",
                    "refused",
                    "KOXO_EXPORT_VALIDATION_FAILED",
                    "koxo_export",
                    SourceAddress: sourceAddress),
                context.RequestAborted);
            return Results.Json(
                new KoxoValidationFailurePayload(
                    "KOXO_EXPORT_VALIDATION_FAILED",
                    "Un ou plusieurs utilisateurs sont invalides.",
                    exception.InvalidUsers),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    });

app.MapGet(
    "/internal/admin/overview",
    async (
        HttpContext context,
        IAdminService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.overview.read");
        return AdminOk(
            context,
            service,
            await service.GetOverviewAsync(
                adConfiguration.ModeName,
                adConfiguration.WritesEnabled,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/settings",
    async (
        HttpContext context,
        IApplicationSettingsService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(context, authenticationService, auditService, "admin.settings.read");
        if (!await editorialRepository.HasAdminPermissionAsync(actor.UserId, "settings.read", context.RequestAborted)) throw new PortalAccessDeniedException();
        context.Response.Headers["X-Data-Source"] = service.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(await service.GetSnapshotAsync(context.RequestAborted));
    });
app.MapPatch(
    "/internal/admin/settings/{key}",
    async (
        string key,
        HttpContext context,
        IApplicationSettingsService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(context, authenticationService, auditService, "admin.settings.write");
        if (!await editorialRepository.HasAdminPermissionAsync(actor.UserId, "settings.write", context.RequestAborted)) throw new PortalAccessDeniedException();
        var request = await ReadPayload<ApplicationSettingUpdateRequest>(context) ?? throw new PortalValidationException();
        var result = await service.UpdateAsync(key, request, actor.UserId, context.GetCorrelationId(), context.RequestAborted);
        var outcome = result.Code == "SETTINGS_UPDATED" ? "success" : "refused";
        await auditService.RecordAsync(new AuditEvent(context.GetCorrelationId(), "setting_changed", outcome, result.Code, "application_setting", key, ActorUserId: actor.UserId, SourceAddress: context.Connection.RemoteIpAddress?.ToString()), context.RequestAborted);
        return Results.Json(result, statusCode: result.Code == "SETTINGS_VERSION_CONFLICT" ? StatusCodes.Status409Conflict : result.Code == "SETTINGS_UPDATED" ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
    });

// --- Fiscalite (specification, section 14.2) -------------------------------
// Le calcul de la taxe reste dans le code : seule la formulation de la mention
// est administrable, pour un regime deja connu, et jamais retroactivement.
app.MapGet(
    "/internal/admin/settings/fiscal-policy",
    async (
        HttpContext context,
        IFiscalPolicyService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(context, authenticationService, auditService, "admin.settings.read");
        if (!await editorialRepository.HasAdminPermissionAsync(actor.UserId, "settings.read", context.RequestAborted)) throw new PortalAccessDeniedException();
        context.Response.Headers["X-Data-Source"] = service.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(await service.GetAdminViewAsync(context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/settings/fiscal-policy/mentions",
    async (
        HttpContext context,
        IFiscalPolicyService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveBillingWriterAsync(context, editorialRepository, authenticationService, auditService);
        var request = await ReadPayload<FiscalMentionCreateRequest>(context) ?? throw new PortalValidationException();
        var result = await service.AddMentionAsync(request, actor.UserId, context.GetCorrelationId(), context.RequestAborted);
        await RecordFiscalAuditAsync(context, auditService, actor.UserId, "fiscal_mention_scheduled", result.Code, "FISCAL_MENTION_SCHEDULED");
        return Results.Json(result, statusCode: ResolveFiscalStatusCode(result.Code));
    });
app.MapDelete(
    "/internal/admin/settings/fiscal-policy/mentions/{id}",
    async (
        string id,
        HttpContext context,
        IFiscalPolicyService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveBillingWriterAsync(context, editorialRepository, authenticationService, auditService);
        var result = await service.DeleteScheduledMentionAsync(id, context.GetCorrelationId(), context.RequestAborted);
        await RecordFiscalAuditAsync(context, auditService, actor.UserId, "fiscal_mention_cancelled", result.Code, "FISCAL_MENTION_CANCELLED");
        return Results.Json(result, statusCode: ResolveFiscalStatusCode(result.Code));
    });

// --- Resume Billing V2 (specification, sections 14.3 et 14.4) ---------------
// Vue federee : le catalogue reste administre dans `/admin/catalog` et la
// readiness dans `/admin/billing-v2`. Les drapeaux sont en lecture seule, leur
// mutation exigeant une intervention sur la machine puis un redemarrage.
app.MapGet(
    "/internal/admin/settings/billing-v2",
    async (
        HttpContext context,
        IBillingV2ConfigurationOverviewService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(context, authenticationService, auditService, "admin.settings.read");
        if (!await editorialRepository.HasAdminPermissionAsync(actor.UserId, "settings.read", context.RequestAborted)) throw new PortalAccessDeniedException();
        return Results.Ok(await service.GetAsync(context.GetCorrelationId(), context.RequestAborted));
    });

// --- Messages & communications (specification, section 8) ------------------
// Les modeles vivent dans des tables specialisees, jamais dans le registre
// generique. Chaque mutation revalide la whitelist de variables cote API.
app.MapGet(
    "/internal/admin/communications",
    async (
        HttpContext context,
        ICommunicationTemplateService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(context, authenticationService, auditService, "admin.communications.read");
        if (!await editorialRepository.HasAdminPermissionAsync(actor.UserId, "settings.read", context.RequestAborted)) throw new PortalAccessDeniedException();
        context.Response.Headers["X-Data-Source"] = service.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(await service.GetAdminCollectionAsync(context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/communications/{scope}/{key}/revisions",
    async (
        string scope,
        string key,
        HttpContext context,
        ICommunicationTemplateService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(context, authenticationService, auditService, "admin.communications.read");
        if (!await editorialRepository.HasAdminPermissionAsync(actor.UserId, "settings.read", context.RequestAborted)) throw new PortalAccessDeniedException();
        return Results.Ok(new { revisions = await service.GetRevisionsAsync(scope, key, context.RequestAborted) });
    });
app.MapPatch(
    "/internal/admin/communications/email/{key}",
    async (
        string key,
        HttpContext context,
        ICommunicationTemplateService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveTemplateWriterAsync(context, editorialRepository, authenticationService, auditService);
        var request = await ReadPayload<EmailTemplateUpdateRequest>(context) ?? throw new PortalValidationException();
        var result = await service.UpdateEmailTemplateAsync(key, request, actor.UserId, context.GetCorrelationId(), context.RequestAborted);
        await RecordTemplateAuditAsync(context, auditService, actor.UserId, "email_template_changed", "email_template", key, result.Code);
        return Results.Json(result, statusCode: ResolveTemplateStatusCode(result.Code));
    });
app.MapPost(
    "/internal/admin/communications/email/{key}/restore-default",
    async (
        string key,
        HttpContext context,
        ICommunicationTemplateService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveTemplateWriterAsync(context, editorialRepository, authenticationService, auditService);
        var request = await ReadPayload<CommunicationTemplateRestoreRequest>(context) ?? throw new PortalValidationException();
        var result = await service.RestoreEmailTemplateAsync(key, request.ExpectedVersion, actor.UserId, context.GetCorrelationId(), context.RequestAborted);
        await RecordTemplateAuditAsync(context, auditService, actor.UserId, "email_template_restored", "email_template", key, result.Code);
        return Results.Json(result, statusCode: ResolveTemplateStatusCode(result.Code));
    });
app.MapPost(
    "/internal/admin/communications/email/{key}/preview",
    async (
        string key,
        HttpContext context,
        ICommunicationTemplateService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(context, authenticationService, auditService, "admin.communications.preview");
        if (!await editorialRepository.HasAdminPermissionAsync(actor.UserId, "settings.read", context.RequestAborted)) throw new PortalAccessDeniedException();
        var request = await ReadPayload<EmailTemplatePreviewRequest>(context) ?? throw new PortalValidationException();
        var result = service.PreviewEmailTemplate(key, request, context.GetCorrelationId());
        return Results.Json(result, statusCode: result.Code == "TEMPLATE_PREVIEW" ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
    });
app.MapPost(
    "/internal/admin/communications/email/{key}/test",
    async (
        string key,
        HttpContext context,
        ICommunicationTemplateService service,
        IEmailService emailService,
        IEmailLogRepository emailLog,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveTemplateWriterAsync(context, editorialRepository, authenticationService, auditService);
        var request = await ReadPayload<EmailTemplateTestRequest>(context) ?? throw new PortalValidationException();
        var correlationId = context.GetCorrelationId();
        var definition = CommunicationTemplateRegistry.FindEmail(key);
        // L'envoi de test ne vise que l'adresse de l'administrateur connecte :
        // aucune adresse arbitraire ne peut etre atteinte depuis cette route.
        if (definition is null || !definition.TestSendSupported
            || !string.Equals(request.Recipient?.Trim(), actor.Email, StringComparison.OrdinalIgnoreCase))
        {
            await RecordTemplateAuditAsync(context, auditService, actor.UserId, "email_template_test", "email_template", key, "TEMPLATE_TEST_REFUSED");
            return Results.Json(
                new CommunicationTemplateSimpleResponse(
                    "TEMPLATE_TEST_REFUSED",
                    "L'envoi de test n'est possible que vers votre propre adresse, pour un modèle qui le permet.",
                    correlationId),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var sample = definition.Variables.ToDictionary(
            variable => variable.Name,
            variable => (string?)$"[{variable.Name}]",
            StringComparer.Ordinal);
        var (subject, body) = await service.RenderEmailAsync(key, sample, context.RequestAborted);
        var delivery = await emailService.SendAsync(
            new EmailMessage(actor.Email, $"[TEST] {subject}", body, key, null, correlationId),
            context.RequestAborted);
        await emailLog.RecordAsync(key, actor.Email, $"[TEST] {subject}", body, delivery.Status, delivery.ErrorMessage, null, correlationId, delivery.Succeeded, context.RequestAborted);
        await RecordTemplateAuditAsync(context, auditService, actor.UserId, "email_template_test", "email_template", key, delivery.Succeeded ? "TEMPLATE_TEST_SENT" : "TEMPLATE_TEST_FAILED");
        return Results.Json(
            new CommunicationTemplateSimpleResponse(
                delivery.Succeeded ? "TEMPLATE_TEST_SENT" : "TEMPLATE_TEST_FAILED",
                delivery.Succeeded
                    ? $"E-mail de test envoyé à {actor.Email}."
                    : "L'envoi de test a échoué. Consultez le journal des e-mails.",
                correlationId),
            statusCode: delivery.Succeeded ? StatusCodes.Status200OK : StatusCodes.Status502BadGateway);
    });
app.MapPatch(
    "/internal/admin/communications/notification/{key}",
    async (
        string key,
        HttpContext context,
        ICommunicationTemplateService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveTemplateWriterAsync(context, editorialRepository, authenticationService, auditService);
        var request = await ReadPayload<NotificationTemplateUpdateRequest>(context) ?? throw new PortalValidationException();
        var result = await service.UpdateNotificationTemplateAsync(key, request, actor.UserId, context.GetCorrelationId(), context.RequestAborted);
        await RecordTemplateAuditAsync(context, auditService, actor.UserId, "notification_template_changed", "notification_template", key, result.Code);
        return Results.Json(result, statusCode: ResolveTemplateStatusCode(result.Code));
    });
app.MapPost(
    "/internal/admin/communications/notification/{key}/restore-default",
    async (
        string key,
        HttpContext context,
        ICommunicationTemplateService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveTemplateWriterAsync(context, editorialRepository, authenticationService, auditService);
        var request = await ReadPayload<CommunicationTemplateRestoreRequest>(context) ?? throw new PortalValidationException();
        var result = await service.RestoreNotificationTemplateAsync(key, request.ExpectedVersion, actor.UserId, context.GetCorrelationId(), context.RequestAborted);
        await RecordTemplateAuditAsync(context, auditService, actor.UserId, "notification_template_restored", "notification_template", key, result.Code);
        return Results.Json(result, statusCode: ResolveTemplateStatusCode(result.Code));
    });
app.MapPatch(
    "/internal/admin/communications/snippet/{key}",
    async (
        string key,
        HttpContext context,
        ICommunicationTemplateService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveTemplateWriterAsync(context, editorialRepository, authenticationService, auditService);
        var request = await ReadPayload<SystemSnippetUpdateRequest>(context) ?? throw new PortalValidationException();
        var result = await service.UpdateSnippetAsync(key, request, actor.UserId, context.GetCorrelationId(), context.RequestAborted);
        await RecordTemplateAuditAsync(context, auditService, actor.UserId, "system_snippet_changed", "system_snippet", key, result.Code);
        return Results.Json(result, statusCode: ResolveTemplateStatusCode(result.Code));
    });
app.MapPost(
    "/internal/admin/communications/snippet/{key}/restore-default",
    async (
        string key,
        HttpContext context,
        ICommunicationTemplateService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveTemplateWriterAsync(context, editorialRepository, authenticationService, auditService);
        var request = await ReadPayload<CommunicationTemplateRestoreRequest>(context) ?? throw new PortalValidationException();
        var result = await service.RestoreSnippetAsync(key, request.ExpectedVersion, actor.UserId, context.GetCorrelationId(), context.RequestAborted);
        await RecordTemplateAuditAsync(context, auditService, actor.UserId, "system_snippet_restored", "system_snippet", key, result.Code);
        return Results.Json(result, statusCode: ResolveTemplateStatusCode(result.Code));
    });
// --- Diagnostic administrable (specification, section 9) -------------------
// La DSL est validee cote API : le BFF n'est jamais l'autorite. Le brouillon
// et la version publiee sont deux lignes distinctes, si bien qu'une redaction
// en cours ne peut pas atteindre un visiteur.
app.MapGet(
    "/internal/admin/diagnostic/configuration",
    async (
        HttpContext context,
        IDiagnosticConfigurationService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(context, authenticationService, auditService, "admin.diagnostic.read");
        if (!await editorialRepository.HasAdminPermissionAsync(actor.UserId, "settings.read", context.RequestAborted)) throw new PortalAccessDeniedException();
        context.Response.Headers["X-Data-Source"] = service.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(await service.GetAdminViewAsync(context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/diagnostic/revisions",
    async (
        HttpContext context,
        IDiagnosticConfigurationService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(context, authenticationService, auditService, "admin.diagnostic.read");
        if (!await editorialRepository.HasAdminPermissionAsync(actor.UserId, "settings.read", context.RequestAborted)) throw new PortalAccessDeniedException();
        return Results.Ok(new { revisions = await service.GetRevisionsAsync(context.RequestAborted) });
    });
app.MapPost(
    "/internal/admin/diagnostic/validate",
    async (
        HttpContext context,
        IDiagnosticConfigurationService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveDiagnosticWriterAsync(context, editorialRepository, authenticationService, auditService);
        _ = actor;
        var request = await ReadPayload<DiagnosticConfigurationValidateRequest>(context) ?? throw new PortalValidationException();
        var result = service.Validate(request, context.GetCorrelationId());
        return Results.Json(result, statusCode: ResolveDiagnosticStatusCode(result.Code));
    });
app.MapPut(
    "/internal/admin/diagnostic/draft",
    async (
        HttpContext context,
        IDiagnosticConfigurationService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveDiagnosticWriterAsync(context, editorialRepository, authenticationService, auditService);
        var request = await ReadPayload<DiagnosticConfigurationUpdateRequest>(context) ?? throw new PortalValidationException();
        var result = await service.SaveDraftAsync(request, actor.UserId, context.GetCorrelationId(), context.RequestAborted);
        await RecordDiagnosticAuditAsync(context, auditService, actor.UserId, "diagnostic_draft_changed", result.Code, "DIAGNOSTIC_DRAFT_SAVED");
        return Results.Json(result, statusCode: ResolveDiagnosticStatusCode(result.Code));
    });
app.MapPost(
    "/internal/admin/diagnostic/publish",
    async (
        HttpContext context,
        IDiagnosticConfigurationService service,
        IEditorialRepository editorialRepository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveDiagnosticWriterAsync(context, editorialRepository, authenticationService, auditService);
        var request = await ReadPayload<DiagnosticConfigurationPublishRequest>(context) ?? throw new PortalValidationException();
        var result = await service.PublishAsync(request, actor.UserId, context.GetCorrelationId(), context.RequestAborted);
        await RecordDiagnosticAuditAsync(context, auditService, actor.UserId, "diagnostic_published", result.Code, "DIAGNOSTIC_PUBLISHED");
        return Results.Json(result, statusCode: ResolveDiagnosticStatusCode(result.Code));
    });
app.MapGet(
    "/internal/admin/customers",
    async (
        HttpContext context,
        IAdminService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.read");
        return AdminOk(
            context,
            service,
            await service.GetCustomersAsync(context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/customers/{customerReference}",
    async (
        string customerReference,
        HttpContext context,
        IAdminService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.detail.read");
        return AdminOk(
            context,
            service,
            await service.GetCustomerAsync(
                customerReference,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/demo/profiles",
    async (
        HttpContext context,
        IDemoAccountService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.demo.read");
        return DemoOk(
            context,
            service,
            await service.ListProfilesAsync(context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/demo/profiles",
    async (
        HttpContext context,
        IDemoAccountService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.demo.write");
        var payload = await ReadPayload<DemoProfilePayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.UpsertProfileAsync(
            payload,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "demo_profile.upsert",
                "success",
                TargetType: "demo_profile",
                TargetReference: result.Key,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DemoOk(context, service, result);
    });
app.MapDelete(
    "/internal/admin/demo/profiles/{key}",
    async (
        string key,
        HttpContext context,
        IDemoAccountService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.demo.write");
        var deleted = await service.DeleteProfileAsync(
            key,
            context.RequestAborted);
        if (!deleted)
        {
            throw new PortalDataNotFoundException();
        }

        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "demo_profile.delete",
                "success",
                TargetType: "demo_profile",
                TargetReference: key,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DemoOk(context, service, new { deleted = true });
    });
app.MapGet(
    "/internal/admin/demo/content-templates",
    async (
        HttpContext context,
        IDemoAccountService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.demo.read");
        return DemoOk(context, service, service.GetContentTemplates());
    });
app.MapGet(
    "/internal/admin/demo/accounts",
    async (
        HttpContext context,
        IDemoAccountService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.demo.read");
        return DemoOk(
            context,
            service,
            await service.ListAccountsAsync(context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/demo/accounts",
    async (
        HttpContext context,
        IDemoAccountService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.demo.write");
        var payload = await ReadPayload<DemoAccountCreateRequest>(context)
            ?? throw new PortalValidationException();
        var result = await service.CreateAccountAsync(
            payload,
            actor.UserId,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "demo_account.create",
                "success",
                TargetType: "demo_account",
                TargetReference: result.CustomerReference,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DemoOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/demo/accounts/{reference}/convert",
    async (
        string reference,
        HttpContext context,
        IDemoAccountService service,
        IDemoConversionService conversionService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.demo.write");
        var payload = await ReadPayload<DemoConversionRequest>(context)
            ?? new DemoConversionRequest(null);
        var result = await conversionService.ConvertAsync(
            reference,
            payload,
            actor.UserId,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "demo_account.convert",
                result.Converted ? "success" : "partial",
                result.ResultCode,
                TargetType: "demo_account",
                TargetReference: result.CustomerReference,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DemoOk(context, service, result);
    });
app.MapDelete(
    "/internal/admin/demo/accounts/{reference}",
    async (
        string reference,
        HttpContext context,
        IDemoAccountService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.demo.write");
        await service.DeleteAccountAsync(reference, context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "demo_account.delete",
                "success",
                TargetType: "demo_account",
                TargetReference: reference,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        // Corps JSON plutot que 204 : le BFF serialise systematiquement la
        // reponse, et un corps vide y devient un echec alors que la suppression
        // a bien eu lieu.
        return Results.Ok(new { deleted = true });
    });
app.MapPost(
    "/internal/admin/demo/expire",
    async (
        HttpContext context,
        IDemoAccountService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.demo.write");
        var result = await service.RunExpirationSweepAsync(context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "demo_account.expire_sweep",
                "success",
                TargetType: "demo_account",
                TargetReference:
                    $"revoked={result.RevokedCount};purged={result.PurgedCount}",
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return DemoOk(context, service, result);
    });
app.MapGet(
    "/internal/admin/koxo",
    async (
        HttpContext context,
        IKoxoExportService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.koxo.read");
        context.Response.Headers["X-Data-Source"] =
            service.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(await service.GetDashboardAsync(context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/koxo/validate",
    async (
        HttpContext context,
        IKoxoExportService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.koxo.validate");
        context.Response.Headers["X-Data-Source"] =
            service.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(
            await service.ValidateAndRecordAsync(
                context.GetCorrelationId(),
                context.Connection.RemoteIpAddress?.ToString(),
                context.RequestAborted));
    });

// V0.26 : gestion admin des demandes d'inscription self-service.
app.MapGet(
    "/internal/admin/signups",
    async (
        HttpContext context,
        ISignupService signupService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.signups.read");
        var status = context.Request.Query["status"].FirstOrDefault();
        context.Response.Headers["X-Data-Source"] =
            signupService.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(
            await signupService.ListAsync(status, context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/signups/{id}",
    async (
        string id,
        HttpContext context,
        ISignupService signupService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.signups.detail.read");
        var detail = await signupService.GetAsync(id, context.RequestAborted);
        if (detail is null)
        {
            return Results.Json(
                new ApiError(
                    "SIGNUP_NOT_FOUND",
                    "Demande introuvable.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status404NotFound);
        }

        context.Response.Headers["X-Data-Source"] =
            signupService.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(detail);
    });
app.MapPost(
    "/internal/admin/signups/{id}/approve",
    async (
        string id,
        HttpContext context,
        ISignupService signupService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.signups.approve.request");
        var result = await signupService.ApproveAsync(
            id, context.GetCorrelationId(), context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "signup.approved",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "signup",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress:
                    context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code == "SIGNUP_NOT_FOUND"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status409Conflict;
            return Results.Json(
                new ApiError(
                    result.Code, result.Message, context.GetCorrelationId()),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = context.GetCorrelationId()
        });
    });
app.MapPost(
    "/internal/admin/signups/{id}/reject",
    async (
        string id,
        HttpContext context,
        ISignupService signupService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.signups.reject.request");
        var payload = await ReadPayload<SignupRejectPayload>(context);
        var result = await signupService.RejectAsync(
            id, payload?.Reason, context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "signup.rejected",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "signup",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress:
                    context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code == "SIGNUP_NOT_FOUND"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status409Conflict;
            return Results.Json(
                new ApiError(
                    result.Code, result.Message, context.GetCorrelationId()),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = context.GetCorrelationId()
        });
    });
app.MapPost(
    "/internal/admin/signups/{id}/initialize-password",
    async (
        string id,
        HttpContext context,
        ISignupService signupService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.signups.password_initialize.request");
        var payload =
            await ReadPayload<SignupAdminInitializePasswordPayload>(context);
        var result = await signupService.InitializePasswordAsync(
            id,
            payload?.Password,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "signup.password_initialized",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "signup",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress:
                    context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            return Results.Json(
                new ApiError(
                    result.Code,
                    result.Message,
                    context.GetCorrelationId()),
                statusCode: ResolveSignupAdminMutationStatusCode(result.Code));
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = context.GetCorrelationId()
        });
    });
app.MapPost(
    "/internal/admin/signups/{id}/resend-password-email",
    async (
        string id,
        HttpContext context,
        ISignupService signupService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.signups.password_email_resend.request");
        var result = await signupService.ResendPasswordSetupEmailAsync(
            id,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "signup.password_email_resent",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "signup",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress:
                    context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            return Results.Json(
                new ApiError(
                    result.Code,
                    result.Message,
                    context.GetCorrelationId()),
                statusCode: ResolveSignupAdminMutationStatusCode(result.Code));
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = context.GetCorrelationId()
        });
    });
// ---------------------------------------------------------------------------
// Administration du catalogue Billing V2/V2.1
//
// Remplace integralement les anciennes routes `/internal/admin/catalog*`, qui
// administraient `commercial_offers`. Toutes exigent une session
// administrateur interne et sont auditees : un changement de tarif engage le
// montant oppose au prochain client.
// ---------------------------------------------------------------------------
app.MapGet(
    "/internal/admin/billing-v2/catalog",
    async (
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        _ = await ResolveAdminSessionAsync(
            context, authenticationService, auditService, "admin.billing_v2.catalog.read");
        var snapshot = await service.GetCatalogAsync(context.RequestAborted);
        context.Response.Headers["X-Data-Source"] = snapshot.Source;
        return Results.Ok(snapshot);
    });
app.MapGet(
    "/internal/admin/billing-v2/catalog/providers",
    async (
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        _ = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.providers.read");
        return Results.Ok(await service.GetProviderCoverageAsync(
            context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/billing-v2/catalog/services",
    async (
        BillingV2AdminServiceCreatePayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.service.create");
        var result = await service.CreateServiceAsync(
            payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.service.create",
            "billing_v2_service", result.Id ?? payload.Code ?? "unknown", result);
    });
app.MapPatch(
    "/internal/admin/billing-v2/catalog/services/{id}",
    async (
        string id,
        BillingV2AdminServicePayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.service.update");
        var result = await service.UpdateServiceAsync(
            id, payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.service.update",
            "billing_v2_service", id, result);
    });
app.MapPost(
    "/internal/admin/billing-v2/catalog/services/{id}/tiers",
    async (
        string id,
        BillingV2AdminTierCreatePayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.tier.create");
        var result = await service.CreateTierAsync(
            id, payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.tier.create",
            "billing_v2_service_tier", result.Id ?? id, result);
    });
app.MapPatch(
    "/internal/admin/billing-v2/catalog/tiers/{id}",
    async (
        string id,
        BillingV2AdminTierPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.tier.update");
        var result = await service.UpdateTierAsync(
            id, payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.tier.update",
            "billing_v2_service_tier", id, result);
    });
app.MapPost(
    "/internal/admin/billing-v2/catalog/prices",
    async (
        BillingV2AdminPriceRevisionPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.price.publish");
        var result = await service.PublishPriceRevisionAsync(
            payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.price.publish",
            "billing_v2_service_price", result.Id ?? payload.ServiceId ?? "unknown",
            result);
    });
app.MapPost(
    "/internal/admin/billing-v2/catalog/prices/{id}/close",
    async (
        string id,
        BillingV2AdminPriceDeactivationPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.price.close");
        var result = await service.DeactivatePriceAsync(
            id, payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.price.close",
            "billing_v2_service_price", id, result);
    });
app.MapPost(
    "/internal/admin/billing-v2/catalog/presets",
    async (
        BillingV2AdminPresetPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.preset.create");
        var result = await service.CreatePresetAsync(
            payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.preset.create",
            "billing_v2_offer_preset", result.Id ?? payload.Code ?? "unknown", result);
    });
app.MapPatch(
    "/internal/admin/billing-v2/catalog/presets/{id}",
    async (
        string id,
        BillingV2AdminPresetPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.preset.update");
        var result = await service.UpdatePresetAsync(
            id, payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.preset.update",
            "billing_v2_offer_preset", id, result);
    });
app.MapPost(
    "/internal/admin/billing-v2/catalog/presets/{id}/items",
    async (
        string id,
        BillingV2AdminPresetItemPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.preset_item.add");
        var result = await service.AddPresetItemAsync(
            id, payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.preset_item.add",
            "billing_v2_preset_item", result.Id ?? id, result);
    });
app.MapPatch(
    "/internal/admin/billing-v2/catalog/presets/{id}/items/{itemId}",
    async (
        string id,
        string itemId,
        BillingV2AdminPresetItemPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.preset_item.update");
        var result = await service.UpdatePresetItemAsync(
            id, itemId, payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.preset_item.update",
            "billing_v2_preset_item", itemId, result);
    });
app.MapDelete(
    "/internal/admin/billing-v2/catalog/presets/{id}/items/{itemId}",
    async (
        string id,
        string itemId,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.preset_item.remove");
        var result = await service.RemovePresetItemAsync(
            id, itemId, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.preset_item.remove",
            "billing_v2_preset_item", itemId, result);
    });
app.MapPost(
    "/internal/admin/billing-v2/catalog/commitments",
    async (
        BillingV2AdminCommitmentPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.commitment.create");
        var result = await service.CreateCommitmentAsync(
            payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.commitment.create",
            "billing_v2_commitment_term", result.Id ?? payload.Code ?? "unknown",
            result);
    });
app.MapPatch(
    "/internal/admin/billing-v2/catalog/commitments/{id}",
    async (
        string id,
        BillingV2AdminCommitmentPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.commitment.update");
        var result = await service.UpdateCommitmentAsync(
            id, payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.commitment.update",
            "billing_v2_commitment_term", id, result);
    });
app.MapPost(
    "/internal/admin/billing-v2/catalog/commitments/{id}/payment-options",
    async (
        string id,
        BillingV2AdminCommitmentPaymentOptionPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.payment_option.upsert");
        var result = await service.UpsertCommitmentPaymentOptionAsync(
            id, payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor, "billing_v2.catalog.payment_option.upsert",
            "billing_v2_commitment_term", id, result);
    });
app.MapPost(
    "/internal/admin/billing-v2/catalog/prices/{id}/provider-mapping",
    async (
        string id,
        BillingV2AdminProviderMappingPayload payload,
        HttpContext context,
        IBillingV2CatalogAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.catalog.provider_mapping.upsert");
        var result = await service.UpsertProviderMappingAsync(
            id, payload, actor.UserId, context.RequestAborted);
        return await CatalogMutationResultAsync(
            context, auditService, actor,
            "billing_v2.catalog.provider_mapping.upsert",
            "billing_v2_service_price", id, result);
    });
app.MapGet(
    "/internal/admin/billing-v2/readiness",
    async (
        HttpContext context,
        IBillingV2AdminReadinessService service,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolvePortalSessionAsync(
            context,
            authenticationService);
        if (session.UserRole != PortalRoles.InternalAdmin)
        {
            throw new PortalAccessDeniedException();
        }

        var snapshot = await service.CheckAsync(
            context.GetCorrelationId(),
            context.RequestAborted);
        return Results.Ok(snapshot);
    });
app.MapPost(
    "/internal/admin/billing-v2/provisioning-readiness/{customerId}/review",
    async (
        string customerId,
        HttpContext context,
        IBillingV2ProvisioningService provisioningService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.billing_v2.provisioning_readiness.review");
        var result = await provisioningService.ReviewClientReadinessAsync(
            customerId,
            actor.UserId,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "billing_v2.provisioning_readiness.review",
                result.Ready ? "success" : "refused",
                ReasonCode: result.ReasonCode,
                TargetType: "customer",
                TargetReference: customerId,
                CustomerId: customerId,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return Results.Ok(result);
    });
app.MapPost(
    "/internal/admin/billing-v2/subscriptions/{id}/provisioning/reconcile",
    async (
        string id,
        HttpContext context,
        IBillingV2ProvisioningService provisioningService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context, authenticationService, auditService,
            "admin.billing_v2.provisioning.reconcile");
        var result = await provisioningService.TryReconcileActivatedSubscriptionAsync(
            id, context.RequestAborted);
        var resultCode = result?.ResultCode
            ?? "BILLING_V2_PROVISIONING_NOT_EXECUTED";
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "billing_v2.provisioning.reconcile",
                result?.Succeeded == true ? "success" : "refused",
                ReasonCode: resultCode,
                TargetType: "billing_v2_subscription",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return Results.Ok(new
        {
            executed = result is not null,
            succeeded = result?.Succeeded ?? false,
            changed = result?.Changed ?? false,
            resultCode,
            operations = result?.Operations ?? Array.Empty<ProvisioningOperationResult>()
        });
    });
app.MapGet(
    "/internal/admin/billing-v2/subscriptions",
    async (
        HttpContext context,
        IBillingV2PortalSubscriptionProjection projection,
        IBillingV2SubscriptionAdministrationService subscriptionService,
        IAuthenticationService authenticationService) =>
    {
        var session = await ResolvePortalSessionAsync(
            context,
            authenticationService);
        if (session.UserRole != PortalRoles.InternalAdmin)
        {
            throw new PortalAccessDeniedException();
        }

        context.Response.Headers["X-Data-Source"] =
            subscriptionService.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(await projection.GetAdminSubscriptionsAsync(
            context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/ad/status",
    async (
        HttpContext context,
        IActiveDirectoryService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.ad.status.read");
        var status = await service.GetStatusAsync(context.RequestAborted);
        await RecordAdAuditAsync(
            context,
            auditService,
            "admin.ad.status.read",
            "success",
            status.Status,
            "active_directory",
            status.Mode,
            actor.UserId,
            null);
        return Results.Ok(status);
    });
app.MapGet(
    "/internal/admin/bpce/status",
    async (
        HttpContext context,
        IBpceInvoicingService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.bpce.status.read");
        var status = await service.GetStatusAsync(context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "admin.bpce.status.read",
                "success",
                TargetType: "bpce_invoicing",
                TargetReference: status.Mode,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return Results.Ok(status);
    });
app.MapGet(
    "/internal/admin/ad/users",
    async (
        HttpContext context,
        IActiveDirectoryService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.ad.users.search");
        var result = await service.SearchUsersAsync(
            context.Request.Query["query"].FirstOrDefault(),
            context.Request.Query["customerReference"].FirstOrDefault(),
            context.RequestAborted);
        return await CompleteAdQueryAsync(
            context,
            auditService,
            "admin.ad.users.search",
            actor.UserId,
            null,
            result);
    });
app.MapGet(
    "/internal/admin/ad/groups",
    async (
        HttpContext context,
        IActiveDirectoryService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.ad.groups.search");
        var result = await service.SearchGroupsAsync(
            context.Request.Query["query"].FirstOrDefault(),
            context.Request.Query["customerReference"].FirstOrDefault(),
            context.RequestAborted);
        return await CompleteAdQueryAsync(
            context,
            auditService,
            "admin.ad.groups.search",
            actor.UserId,
            null,
            result);
    });
app.MapGet(
    "/internal/admin/customers/{customerReference}/ad-links",
    async (
        string customerReference,
        HttpContext context,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_links.read");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var links = await repository.GetCustomerLinksAsync(
            customer.CustomerReference,
            context.RequestAborted);
        await RecordAdAuditAsync(
            context,
            auditService,
            "admin.customers.ad_links.read",
            "success",
            "AD_LINKS_FOUND",
            "customer_ad_link",
            customer.CustomerReference,
            actor.UserId,
            customer.CustomerId);
        return Results.Ok(links);
    });
app.MapGet(
    "/internal/admin/customers/{customerReference}/active-directory",
    async (
        string customerReference,
        HttpContext context,
        ICustomerActiveDirectoryAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.active_directory.read");
        var workspace = await service.GetWorkspaceAsync(
            customerReference,
            context.Request.Query["subscriptionId"].FirstOrDefault(),
            context.RequestAborted);
        await RecordAdAuditAsync(
            context,
            auditService,
            "admin.customers.active_directory.read",
            "success",
            workspace.LastResultCode ?? workspace.ProvisioningStatus,
            "customer_active_directory",
            workspace.CustomerReference,
            actor.UserId,
            null);
        return Results.Ok(workspace);
    });
app.MapPost(
    "/internal/admin/customers/{customerReference}/active-directory/services/{technicalServiceReference}",
    async (
        string customerReference,
        string technicalServiceReference,
        HttpContext context,
        CustomerAdProvisioningMutationRequest? request,
        ICustomerActiveDirectoryAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.active_directory.service.write");
        var result = await service.ApplyServiceActionAsync(
            customerReference,
            technicalServiceReference,
            request,
            context.GetCorrelationId(),
            actor.UserId,
            context.RequestAborted);
        var statusCode = MapCustomerAdProvisioningStatusCode(result.Code);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                ResolveCustomerAdProvisioningAuditAction(
                    request?.Operation,
                    request?.IsOverride ?? false,
                    "service"),
                statusCode >= 400
                    ? "refused"
                    : result.Changed
                        ? "success"
                        : "unchanged",
                ReasonCode: result.Code,
                TargetType: "customer_ad_service",
                TargetReference: technicalServiceReference,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return Results.Json(result, statusCode: statusCode);
    });
app.MapPost(
    "/internal/admin/customers/{customerReference}/active-directory/groups/{groupSamAccountName}",
    async (
        string customerReference,
        string groupSamAccountName,
        HttpContext context,
        CustomerAdProvisioningMutationRequest? request,
        ICustomerActiveDirectoryAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.active_directory.group.write");
        var result = await service.ApplyGroupActionAsync(
            customerReference,
            groupSamAccountName,
            request,
            context.GetCorrelationId(),
            actor.UserId,
            context.RequestAborted);
        var statusCode = MapCustomerAdProvisioningStatusCode(result.Code);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                ResolveCustomerAdProvisioningAuditAction(
                    request?.Operation,
                    request?.IsOverride ?? false,
                    "group"),
                statusCode >= 400
                    ? "refused"
                    : result.Changed
                        ? "success"
                        : "unchanged",
                ReasonCode: result.Code,
                TargetType: "customer_ad_group",
                TargetReference: groupSamAccountName,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return Results.Json(result, statusCode: statusCode);
    });
app.MapPost(
    "/internal/admin/customers/{customerReference}/ad-links",
    async (
        string customerReference,
        HttpContext context,
        IActiveDirectoryService service,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_links.write");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var request = await ReadPayload<CreateCustomerAdLinkRequest>(context);
        var result = await service.ResolveObjectForLinkAsync(
            customer.CustomerReference,
            request?.DistinguishedName,
            context.RequestAborted);
        if (result.StatusCode >= 400 || result.Value is null)
        {
            return await CompleteAdMutationAsync(
                context,
                auditService,
                "admin.customers.ad_links.write",
                actor.UserId,
                customer.CustomerId,
                service.ModeName,
                result);
        }

        var linkResult = await repository.UpsertCustomerLinkAsync(
            customer.CustomerReference,
            actor.UserId,
            result.Value,
            context.RequestAborted);
        await RecordAdAuditAsync(
            context,
            auditService,
            "admin.customers.ad_links.write",
            linkResult.Changed ? "success" : "unchanged",
            linkResult.Changed
                ? "AD_LINK_CREATED"
                : "AD_LINK_ALREADY_PRESENT",
            "customer_ad_link",
            customer.CustomerReference,
            actor.UserId,
            customer.CustomerId);
        return Results.Json(
            new AdLinkMutationResponse(
                linkResult.Id,
                linkResult.Changed
                    ? "AD_LINK_CREATED"
                    : "AD_LINK_ALREADY_PRESENT",
                linkResult.Changed
                    ? "Active Directory object linked to the customer."
                    : "Active Directory object was already linked to the customer.",
                linkResult.Changed,
                context.GetCorrelationId(),
                result.Value),
            statusCode: linkResult.Changed
                ? StatusCodes.Status201Created
                : StatusCodes.Status200OK);
    });
app.MapDelete(
    "/internal/admin/customers/{customerReference}/ad-links/{linkId}",
    async (
        string customerReference,
        string linkId,
        HttpContext context,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_links.delete");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var deleted = await repository.DeleteCustomerLinkAsync(
            customer.CustomerReference,
            NormalizeGuidIdentifier(linkId),
            context.RequestAborted);
        await RecordAdAuditAsync(
            context,
            auditService,
            "admin.customers.ad_links.delete",
            deleted ? "success" : "refused",
            deleted ? "AD_LINK_DELETED" : "AD_LINK_NOT_FOUND",
            "customer_ad_link",
            customer.CustomerReference,
            actor.UserId,
            customer.CustomerId);
        return deleted
            ? Results.Ok(new AdLinkMutationResponse(
                NormalizeGuidIdentifier(linkId),
                "AD_LINK_DELETED",
                "Active Directory link removed from the customer.",
                true,
                context.GetCorrelationId()))
            : Results.Json(
                new ApiError(
                    "AD_LINK_NOT_FOUND",
                    "The requested Active Directory link was not found.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status404NotFound);
    });
app.MapPost(
    "/internal/admin/customers/{customerReference}/ad/users",
    async (
        string customerReference,
        HttpContext context,
        IActiveDirectoryService service,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_users.write");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var request = await ReadPayload<CreateAdUserRequest>(context);
        var result = await service.CreateUserAsync(
            customer.CustomerReference,
            request,
            context.RequestAborted);
        if (result.StatusCode < 400 && result.Value is not null)
        {
            await repository.UpsertCustomerLinkAsync(
                customer.CustomerReference,
                actor.UserId,
                result.Value,
                context.RequestAborted);
        }

        return await CompleteAdMutationAsync(
            context,
            auditService,
            "admin.customers.ad_users.write",
            actor.UserId,
            customer.CustomerId,
            service.ModeName,
            result);
    });
app.MapPost(
    "/internal/admin/customers/{customerReference}/ad/groups",
    async (
        string customerReference,
        HttpContext context,
        IActiveDirectoryService service,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_groups.write");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var request = await ReadPayload<CreateAdGroupRequest>(context);
        var result = await service.CreateGroupAsync(
            customer.CustomerReference,
            request,
            context.RequestAborted);
        if (result.StatusCode < 400 && result.Value is not null)
        {
            await repository.UpsertCustomerLinkAsync(
                customer.CustomerReference,
                actor.UserId,
                result.Value,
                context.RequestAborted);
        }

        return await CompleteAdMutationAsync(
            context,
            auditService,
            "admin.customers.ad_groups.write",
            actor.UserId,
            customer.CustomerId,
            service.ModeName,
            result);
    });
app.MapPost(
    "/internal/admin/customers/{customerReference}/ad/groups/{groupSamAccountName}/members",
    async (
        string customerReference,
        string groupSamAccountName,
        HttpContext context,
        IActiveDirectoryService service,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_group_members.write");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var request = await ReadPayload<AdGroupMemberRequest>(context);
        var result = await service.AddGroupMemberAsync(
            customer.CustomerReference,
            NormalizeSamIdentifier(groupSamAccountName),
            request?.UserSamAccountName,
            context.RequestAborted);
        return await CompleteAdMutationAsync(
            context,
            auditService,
            "admin.customers.ad_group_members.write",
            actor.UserId,
            customer.CustomerId,
            service.ModeName,
            result);
    });
app.MapDelete(
    "/internal/admin/customers/{customerReference}/ad/groups/{groupSamAccountName}/members/{userSamAccountName}",
    async (
        string customerReference,
        string groupSamAccountName,
        string userSamAccountName,
        HttpContext context,
        IActiveDirectoryService service,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_group_members.delete");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var result = await service.RemoveGroupMemberAsync(
            customer.CustomerReference,
            NormalizeSamIdentifier(groupSamAccountName),
            NormalizeSamIdentifier(userSamAccountName),
            context.RequestAborted);
        return await CompleteAdMutationAsync(
            context,
            auditService,
            "admin.customers.ad_group_members.delete",
            actor.UserId,
            customer.CustomerId,
            service.ModeName,
            result);
    });
app.MapPost(
    "/internal/admin/customers/{customerReference}/ad/users/{samAccountName}/disable",
    async (
        string customerReference,
        string samAccountName,
        HttpContext context,
        IActiveDirectoryService service,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_users.disable");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var result = await service.DisableUserAsync(
            customer.CustomerReference,
            NormalizeSamIdentifier(samAccountName),
            context.RequestAborted);
        return await CompleteAdMutationAsync(
            context,
            auditService,
            "admin.customers.ad_users.disable",
            actor.UserId,
            customer.CustomerId,
            service.ModeName,
            result);
    });
app.MapPost(
    "/internal/admin/customers/{customerReference}/ad/users/{samAccountName}/move-to-disabled",
    async (
        string customerReference,
        string samAccountName,
        HttpContext context,
        IActiveDirectoryService service,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_users.move_to_disabled");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var result = await service.MoveUserToDisabledAsync(
            customer.CustomerReference,
            NormalizeSamIdentifier(samAccountName),
            context.RequestAborted);
        return await CompleteAdMutationAsync(
            context,
            auditService,
            "admin.customers.ad_users.move_to_disabled",
            actor.UserId,
            customer.CustomerId,
            service.ModeName,
            result);
    });
app.MapGet(
    "/internal/admin/customers/{customerReference}/ad/users/{samAccountName}/groups",
    async (
        string customerReference,
        string samAccountName,
        HttpContext context,
        IActiveDirectoryService service,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_users.groups_read");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var result = await service.GetUserEffectiveGroupsAsync(
            customer.CustomerReference,
            NormalizeSamIdentifier(samAccountName),
            context.RequestAborted);
        return await CompleteAdQueryAsync(
            context,
            auditService,
            "admin.customers.ad_users.groups_read",
            actor.UserId,
            customer.CustomerId,
            result);
    });
app.MapPost(
    "/internal/admin/customers/{customerReference}/ad/users/{samAccountName}/rename",
    async (
        string customerReference,
        string samAccountName,
        HttpContext context,
        IActiveDirectoryService service,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_users.rename");
        var customer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var request = await ReadPayload<RenameAdUserRequest>(context);
        var result = await service.RenameUserAsync(
            customer.CustomerReference,
            NormalizeSamIdentifier(samAccountName),
            request,
            context.RequestAborted);
        if (result.StatusCode < 400 && result.Changed && result.Value is not null)
        {
            await repository.RefreshCustomerLinkAsync(
                customer.CustomerReference,
                result.Value,
                context.RequestAborted);
        }

        return await CompleteAdMutationAsync(
            context,
            auditService,
            "admin.customers.ad_users.rename",
            actor.UserId,
            customer.CustomerId,
            service.ModeName,
            result);
    });
app.MapPost(
    "/internal/admin/customers/{customerReference}/ad/users/{samAccountName}/move",
    async (
        string customerReference,
        string samAccountName,
        HttpContext context,
        IActiveDirectoryService service,
        IActiveDirectoryLinkRepository repository,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.customers.ad_users.move");
        var sourceCustomer = await ResolveAdCustomerContextAsync(
            repository,
            customerReference,
            context.RequestAborted);
        var request = await ReadPayload<MoveAdUserRequest>(context);
        // Validate target customer exists locally before talking to AD.
        // The AD scope check (NormalizeMoveContainer + customer reference
        // format) still runs inside the service, but a missing customer
        // here means our DB cannot persist the link, so refuse early.
        if (!string.IsNullOrWhiteSpace(request?.TargetCustomerReference)
            && !request.TargetCustomerReference.Equals(
                sourceCustomer.CustomerReference,
                StringComparison.OrdinalIgnoreCase))
        {
            _ = await ResolveAdCustomerContextAsync(
                repository,
                request.TargetCustomerReference,
                context.RequestAborted);
        }

        var result = await service.MoveUserAsync(
            sourceCustomer.CustomerReference,
            NormalizeSamIdentifier(samAccountName),
            request,
            context.RequestAborted);
        if (result.StatusCode < 400 && result.Changed && result.Value is not null)
        {
            await repository.RefreshCustomerLinkAsync(
                result.Value.CustomerReference,
                result.Value,
                context.RequestAborted);
        }

        return await CompleteAdMutationAsync(
            context,
            auditService,
            "admin.customers.ad_users.move",
            actor.UserId,
            sourceCustomer.CustomerId,
            service.ModeName,
            result);
    });
app.MapGet(
    "/internal/admin/support-requests",
    async (
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.support_requests.read");
        return WorkflowOk(
            context,
            service,
            await service.GetAdminSupportRequestsAsync(
                ReadAdminRequestListQuery(context),
                context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/activity",
    async (
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.activity.read");
        return WorkflowOk(
            context,
            service,
            await service.GetAdminActivityAsync(context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/backups/integrations",
    async (
        HttpContext context,
        IBackupService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.backups.integrations.read");
        return BackupOk(
            context,
            service,
            await service.GetAdminIntegrationsAsync(context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/backups/integrations",
    async (
        HttpContext context,
        IBackupService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.backups.integrations.write");
        var payload = await ReadPayload<BackupIntegrationPayload>(context)
            ?? throw new PortalValidationException();
        return BackupOk(
            context,
            service,
            await service.UpsertAdminIntegrationAsync(
                payload,
                context.GetCorrelationId(),
                context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/service-requests",
    async (
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.service_requests.read");
        return WorkflowOk(
            context,
            service,
            await service.GetAdminServiceRequestsAsync(
                ReadAdminRequestListQuery(context),
                context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/support-requests/{id}",
    async (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.support_request.read");
        return WorkflowOk(
            context,
            service,
            await service.GetAdminSupportRequestAsync(
                id,
                context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/service-requests/{id}",
    async (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.service_request.read");
        return WorkflowOk(
            context,
            service,
            await service.GetAdminServiceRequestAsync(
                id,
                context.RequestAborted));
    });
app.MapPatch(
    "/internal/admin/support-requests/{id}/status",
    (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
        UpdateRequestStatus(
            id,
            RequestTypes.Support,
            context,
            service,
            authenticationService,
            auditService));
app.MapPatch(
    "/internal/admin/service-requests/{id}/status",
    (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
        UpdateRequestStatus(
            id,
            RequestTypes.Service,
            context,
            service,
            authenticationService,
            auditService));
app.MapPost(
    "/internal/admin/support-requests/{id}/notes",
    (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
        AddRequestText(
            id,
            RequestTypes.Support,
            isPublic: false,
            context,
            service,
            authenticationService,
            auditService));
app.MapPost(
    "/internal/admin/service-requests/{id}/notes",
    (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
        AddRequestText(
            id,
            RequestTypes.Service,
            isPublic: false,
            context,
            service,
            authenticationService,
            auditService));
app.MapPost(
    "/internal/admin/support-requests/{id}/messages",
    (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
        AddRequestText(
            id,
            RequestTypes.Support,
            isPublic: true,
            context,
            service,
            authenticationService,
            auditService));
app.MapPost(
    "/internal/admin/service-requests/{id}/messages",
    (
        string id,
        HttpContext context,
        IRequestWorkflowService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
        AddRequestText(
            id,
            RequestTypes.Service,
            isPublic: true,
            context,
            service,
            authenticationService,
            auditService));
app.MapGet(
    "/internal/admin/sessions",
    async (
        HttpContext context,
        IAdminService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.sessions.read");
        return AdminOk(
            context,
            service,
            await service.GetSessionsAsync(context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/audit-logs",
    async (
        HttpContext context,
        IAdminService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.audit_logs.read");
        return AdminOk(
            context,
            service,
            await service.GetAuditLogsAsync(context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/subscriptions",
    async (
        HttpContext context,
        IBillingV2SubscriptionAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.subscriptions.read");
        return SubscriptionOk(
            context,
            service,
            await service.GetAdminSubscriptionsAsync(context.RequestAborted));
    });
app.MapGet(
    "/internal/admin/subscriptions/{id}",
    async (
        string id,
        HttpContext context,
        IBillingV2SubscriptionAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.subscriptions.read");
        return SubscriptionOk(
            context,
            service,
            await service.GetAdminSubscriptionDetailAsync(
                id,
                context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/subscriptions/{id}/cancel",
    async (
        string id,
        HttpContext context,
        IBillingV2SubscriptionAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.subscriptions.cancel");
        var result = await service.AdminCancelAsync(
            id,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "subscription.admin_cancel",
                "success",
                TargetType: "subscription",
                TargetReference: result.Id,
                CustomerId: result.CustomerId,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return SubscriptionOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/subscriptions/{id}/provisioning/reconcile",
    async (
        string id,
        HttpContext context,
        SubscriptionProvisioningReconcileRequest? request,
        IBillingV2SubscriptionAdministrationService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.subscriptions.provisioning.reconcile");
        var result = await service.ReconcileProvisioningAsync(
            id,
            request?.TargetUserSamAccountNames?.Count == 1
                ? "subscription.provisioning.manual_reconcile_user"
                : "subscription.provisioning.manual_reconcile",
            context.GetCorrelationId(),
            actor.UserId,
            request?.TargetUserSamAccountNames,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "subscription.provisioning_reconcile",
                result.Status == "failed" ? "refused" : "success",
                ReasonCode: result.LastResultCode,
                TargetType: "subscription",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return SubscriptionOk(context, service, result);
    });
app.MapGet(
    "/internal/admin/commercial-documents",
    async (
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.read");
        return CommercialOk(
            context,
            service,
            await service.GetAdminDocumentsAsync(context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/commercial-documents",
    async (
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.write");
        var payload = await ReadPayload<CommercialDocumentPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.CreateDocumentAsync(
            actor,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.create",
                "success",
                TargetType: "commercial_document",
                TargetReference: result.InternalReference,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return CommercialOk(context, service, result);
    });
app.MapGet(
    "/internal/admin/commercial-documents/{id}",
    async (
        string id,
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.read");
        return CommercialOk(
            context,
            service,
            await service.GetAdminDocumentAsync(id, context.RequestAborted));
    });
app.MapPatch(
    "/internal/admin/commercial-documents/{id}",
    async (
        string id,
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.write");
        var payload = await ReadPayload<CommercialDocumentPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.UpdateDocumentAsync(
            actor,
            id,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.update",
                result.Changed ? "success" : "unchanged",
                TargetType: "commercial_document",
                TargetReference: result.InternalReference,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return CommercialOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/commercial-documents/{id}/lines",
    async (
        string id,
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.write");
        var payload = await ReadPayload<CommercialDocumentLinePayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.AddLineAsync(
            actor,
            id,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.line.create",
                "success",
                TargetType: "commercial_document",
                TargetReference: result.DocumentId,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return CommercialOk(context, service, result);
    });
app.MapPatch(
    "/internal/admin/commercial-documents/{id}/lines/{lineId}",
    async (
        string id,
        string lineId,
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.write");
        var payload = await ReadPayload<CommercialDocumentLinePayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.UpdateLineAsync(
            actor,
            id,
            lineId,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.line.update",
                result.Changed ? "success" : "unchanged",
                TargetType: "commercial_document",
                TargetReference: result.DocumentId,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return CommercialOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/commercial-documents/{id}/share",
    async (
        string id,
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.write");
        var result = await service.ShareDocumentAsync(
            actor,
            id,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.share",
                result.Changed ? "success" : "unchanged",
                TargetType: "commercial_document",
                TargetReference: result.InternalReference,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return CommercialOk(context, service, result);
    });
app.MapPost(
    "/internal/admin/commercial-documents/{id}/cancel",
    async (
        string id,
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.write");
        var result = await service.CancelDocumentAsync(
            actor,
            id,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.cancel",
                result.Changed ? "success" : "unchanged",
                TargetType: "commercial_document",
                TargetReference: result.InternalReference,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return CommercialOk(context, service, result);
    });

app.MapPost(
    "/internal/admin/commercial-documents/{id}/issue",
    async (
        string id,
        HttpContext context,
        IInvoiceIssuingService issuingService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.issue");
        var request = await ReadPayload<BpceIssueInvoiceRequest>(context);
        var result = await issuingService.IssueInvoiceAsync(
            id,
            request?.SendEmail ?? false,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.issue",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "commercial_document",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code == "DOCUMENT_NOT_FOUND"
                ? StatusCodes.Status404NotFound
                : result.Code == "INVOICE_ALREADY_ISSUED"
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;
            return Results.Json(
                new ApiError(result.Code, result.Message, context.GetCorrelationId()),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            invoice = result.Invoice,
            correlation_id = context.GetCorrelationId()
        });
    });

app.MapGet(
    "/internal/admin/commercial-documents/{id}/invoice",
    async (
        string id,
        HttpContext context,
        IInvoiceIssuingService issuingService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.invoice.read");
        var record = await issuingService.GetInvoiceRecordAsync(
            id, context.RequestAborted);
        if (record is null)
        {
            return Results.Json(
                new ApiError(
                    "INVOICE_NOT_FOUND",
                    "No issued invoice found for this document.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status404NotFound);
        }

        if (record.PdfHash is null)
        {
            var fetched = await issuingService.EnsureInvoicePdfAsync(
                id, context.RequestAborted);
            if (fetched is not null)
            {
                record = await issuingService.GetInvoiceRecordAsync(
                    id, context.RequestAborted) ?? record;
            }
        }

        return Results.Ok(new BpceIssuedInvoiceInfo(
            record.BpceInvoiceId,
            record.FiscalNumber,
            record.Status,
            record.IssueDate,
            record.TotalAmountCents,
            record.Currency,
            record.PdfHash is not null));
    });

app.MapGet(
    "/internal/admin/commercial-documents/{id}/invoice/pdf",
    async (
        string id,
        HttpContext context,
        IInvoiceIssuingService issuingService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.invoice.pdf");
        var record = await issuingService.GetInvoiceRecordAsync(
            id, context.RequestAborted);
        if (record is null)
        {
            return Results.Json(
                new ApiError(
                    "INVOICE_NOT_FOUND",
                    "No issued invoice found for this document.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status404NotFound);
        }

        var pdf = await issuingService.EnsureInvoicePdfAsync(
            id, context.RequestAborted);
        if (pdf is null)
        {
            return Results.Json(
                new ApiError(
                    "PDF_NOT_AVAILABLE",
                    "The invoice PDF is not yet available.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status404NotFound);
        }

        var filename = $"facture-{record.FiscalNumber ?? record.BpceInvoiceId}.pdf";
        return Results.File(pdf, "application/pdf", filename);
    });

app.MapPost(
    "/internal/admin/commercial-documents/{id}/send-reminder",
    async (
        string id,
        HttpContext context,
        IEmailDispatchService emailDispatch,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.send_reminder");
        var result = await emailDispatch.SendPaymentReminderAsync(
            id,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.send_reminder",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "commercial_document",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code == "DOCUMENT_NOT_FOUND"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Results.Json(
                new ApiError(result.Code, result.Message, context.GetCorrelationId()),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = context.GetCorrelationId()
        });
    });

app.MapGet(
    "/internal/admin/email-log",
    async (
        HttpContext context,
        IEmailLogRepository emailLog,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.email_log.read");
        var limit = int.TryParse(
            context.Request.Query["limit"].ToString(), out var parsed)
            ? parsed
            : 100;
        var entries = await emailLog.ListRecentAsync(
            limit, context.RequestAborted);
        context.Response.Headers["X-Data-Source"] =
            emailLog.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(entries);
    });

app.MapPost(
    "/internal/admin/commercial-documents/{id}/mark-as-paid",
    async (
        string id,
        HttpContext context,
        IInvoiceIssuingService issuingService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var actor = await ResolveAdminSessionAsync(
            context,
            authenticationService,
            auditService,
            "admin.commercial_documents.mark_as_paid");
        var result = await issuingService.ConfirmPaymentAsync(
            id,
            context.GetCorrelationId(),
            "manual",
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_document.mark_as_paid",
                result.Succeeded ? "success" : "refused",
                ReasonCode: result.Code,
                TargetType: "commercial_document",
                TargetReference: id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);

        if (!result.Succeeded)
        {
            var statusCode = result.Code == "INVOICE_NOT_FOUND"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Results.Json(
                new ApiError(result.Code, result.Message, context.GetCorrelationId()),
                statusCode: statusCode);
        }

        return Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            invoice = result.Invoice,
            correlation_id = context.GetCorrelationId()
        });
    });

// Les mentions fiscales sont projetees de maniere synchrone dans les lignes de
// document : elles doivent donc etre chargees avant de servir la premiere
// requete. En cas d'echec, le service repart sur les mentions integrees au code.
using (var fiscalScope = app.Services.CreateScope())
{
    await fiscalScope.ServiceProvider
        .GetRequiredService<IFiscalPolicyService>()
        .RefreshAsync(CancellationToken.None);
}

app.MapFallback((HttpContext context) =>
    Results.Json(
        new ApiError(
            "ROUTE_NOT_FOUND",
            "La ressource demandée est introuvable.",
            context.GetCorrelationId()),
        statusCode: StatusCodes.Status404NotFound));

app.Run();

static IResult PortalOk<T>(
    HttpContext context,
    IPortalService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult AdminOk<T>(
    HttpContext context,
    IAdminService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult WorkflowOk<T>(
    HttpContext context,
    IRequestWorkflowService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult BackupOk<T>(
    HttpContext context,
    IBackupService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult DemoOk<T>(
    HttpContext context,
    IDemoAccountService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult NotificationOk<T>(
    HttpContext context,
    IPortalNotificationService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult CommercialOk<T>(
    HttpContext context,
    ICommercialService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult ManagedContentOk<T>(
    HttpContext context,
    IManagedContentService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult EditorialOk<T>(
    HttpContext context,
    IEditorialService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static string EditorialReadPermission(string? contentType)
    => NormalizeEditorialPermissionType(contentType) switch
    {
        EditorialContentTypes.WikiArticle => "content.wiki.read",
        EditorialContentTypes.SeoPage => "content.seo.read",
        EditorialContentTypes.Faq => "content.faq.read",
        _ => "content.wiki.read"
    };

static string EditorialWritePermission(string? contentType)
    => NormalizeEditorialPermissionType(contentType) switch
    {
        EditorialContentTypes.WikiArticle => "content.wiki.write",
        EditorialContentTypes.SeoPage => "content.seo.write",
        EditorialContentTypes.Faq => "content.faq.write",
        _ => "content.wiki.write"
    };

static string? NormalizeEditorialPermissionType(string? contentType)
{
    if (string.IsNullOrWhiteSpace(contentType))
    {
        return null;
    }

    var normalized = contentType.Trim().ToLowerInvariant();
    return EditorialContentTypes.IsKnown(normalized) ? normalized : null;
}

static IResult DownloadsOk<T>(
    HttpContext context,
    IDownloadService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult ClientSolutionsOk<T>(
    HttpContext context,
    IClientSolutionService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult DownloadsError(
    HttpContext context,
    IDownloadService service,
    int statusCode,
    string code,
    string message)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Json(
        new ApiError(code, message, context.GetCorrelationId()),
        statusCode: statusCode);
}

static IResult PublicPackCatalogOk<T>(
    HttpContext context,
    IPublicPackCatalogService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static IResult SubscriptionOk<T>(
    HttpContext context,
    IBillingV2SubscriptionAdministrationService service,
    T data)
{
    context.Response.Headers["X-Data-Source"] =
        service.IsPersistent ? "mariadb" : "mock";
    return Results.Ok(data);
}

static AdminRequestListQuery ReadAdminRequestListQuery(HttpContext context)
    => new(
        context.Request.Query["status"].FirstOrDefault(),
        context.Request.Query["priority"].FirstOrDefault(),
        context.Request.Query["order"].FirstOrDefault() ?? "newest",
        context.Request.Query["attention"].FirstOrDefault());

static async Task<IResult> UpdateRequestStatus(
    string requestId,
    string requestType,
    HttpContext context,
    IRequestWorkflowService service,
    IAuthenticationService authenticationService,
    IAuditService auditService)
{
    var actor = await ResolveAdminSessionAsync(
        context,
        authenticationService,
        auditService,
        $"admin.{requestType}_request.status.write");
    var payload = await ReadPayload<RequestStatusPayload>(context)
        ?? throw new PortalValidationException();
    var result = await service.UpdateStatusAsync(
        actor,
        requestType,
        requestId,
        payload,
        context.GetCorrelationId(),
        context.RequestAborted);

    await auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            $"{requestType}_request.status.change",
            result.Changed ? "success" : "unchanged",
            TargetType: $"{requestType}_request",
            TargetReference: result.Reference,
            ActorUserId: actor.UserId,
            SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);

    return Results.Ok(result);
}

static async Task<IResult> AddRequestText(
    string requestId,
    string requestType,
    bool isPublic,
    HttpContext context,
    IRequestWorkflowService service,
    IAuthenticationService authenticationService,
    IAuditService auditService)
{
    var action = isPublic ? "public_message" : "internal_note";
    var actor = await ResolveAdminSessionAsync(
        context,
        authenticationService,
        auditService,
        $"admin.{requestType}_request.{action}.write");
    var payload = await ReadPayload<RequestTextPayload>(context)
        ?? throw new PortalValidationException();
    var result = isPublic
        ? await service.AddPublicMessageAsync(
            actor,
            requestType,
            requestId,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted)
        : await service.AddInternalNoteAsync(
            actor,
            requestType,
            requestId,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);

    await auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            $"{requestType}_request.{action}.add",
            "success",
            TargetType: $"{requestType}_request",
            TargetReference: result.Reference,
            ActorUserId: actor.UserId,
            SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);

    return Results.Ok(result);
}

static async Task<IResult> AddClientRequestMessage(
    string requestId,
    string requestType,
    HttpContext context,
    IRequestWorkflowService service,
    IAuthenticationService authenticationService,
    IAuditService auditService)
{
    var actor = await ResolveClientSessionAsync(
        context,
        authenticationService,
        auditService);
    var payload = await ReadPayload<RequestTextPayload>(context)
        ?? throw new PortalValidationException();
    var result = await service.AddClientPublicMessageAsync(
        actor,
        requestType,
        requestId,
        payload,
        context.GetCorrelationId(),
        context.RequestAborted);

    await auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            $"{requestType}_request.client_reply.add",
            "success",
            CustomerId: actor.CustomerId,
            TargetType: $"{requestType}_request",
            TargetReference: result.Reference,
            ActorUserId: actor.UserId,
            SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);

    return Results.Ok(result);
}

static async Task<IResult> CreateSupportRequest(
    HttpContext context,
    IPortalService service,
    IAuthenticationService authenticationService)
{
    var session = await ResolveClientSessionAsync(
        context,
        authenticationService,
        context.RequestServices.GetRequiredService<IAuditService>());
    var payload = await ReadPayload<SupportRequestPayload>(context)
        ?? throw new PortalValidationException();
    var result = await service.CreateSupportRequestAsync(
        session,
        payload,
        context.GetCorrelationId(),
        context.Connection.RemoteIpAddress?.ToString(),
        context.RequestAborted);

    return Results.Json(
        new MockSubmissionResponse(
            result.Reference,
            result.Status,
            result.Persisted,
            result.Message,
            result.CorrelationId),
        statusCode: StatusCodes.Status202Accepted);
}

static async Task<IResult> CreateServiceRequest(
    HttpContext context,
    IPortalService service,
    IAuthenticationService authenticationService)
{
    var session = await ResolveClientSessionAsync(
        context,
        authenticationService,
        context.RequestServices.GetRequiredService<IAuditService>());
    var payload = await ReadPayload<ServiceRequestPayload>(context)
        ?? throw new PortalValidationException();
    var result = await service.CreateServiceRequestAsync(
        session,
        payload,
        context.GetCorrelationId(),
        context.Connection.RemoteIpAddress?.ToString(),
        context.RequestAborted);

    return Results.Json(
        new MockSubmissionResponse(
            result.Reference,
            result.Status,
            result.Persisted,
            result.Message,
            result.CorrelationId),
        statusCode: StatusCodes.Status202Accepted);
}

static async Task<IResult> CreatePortalSession(
    HttpContext context,
    IAuthenticationService authenticationService)
{
    var request = await ReadPayload<LoginRequest>(context)
        ?? throw new InvalidCredentialsException();
    var result = await authenticationService.CreateSessionAsync(
        request,
        context.GetCorrelationId(),
        context.Connection.RemoteIpAddress?.ToString(),
        context.Request.Headers["User-Agent"].ToString(),
        context.RequestAborted);

    return Results.Ok(new InternalSessionCreatedResponse(
        result.SessionToken,
        result.User,
        ToUtcIso(result.ExpiresAtUtc)));
}

static async Task<IResult> GetPortalSession(
    HttpContext context,
    IAuthenticationService authenticationService)
{
    var session = await ResolvePortalSessionAsync(
        context,
        authenticationService);
    return Results.Ok(new InternalSessionResponse(
        ToPublicUser(session),
        ToUtcIso(session.ExpiresAtUtc)));
}

static async Task<IResult> RevokePortalSession(
    HttpContext context,
    IAuthenticationService authenticationService)
{
    await authenticationService.RevokeSessionAsync(
        GetPortalSessionToken(context),
        context.GetCorrelationId(),
        context.Connection.RemoteIpAddress?.ToString(),
        context.RequestAborted);
    return Results.NoContent();
}

static async Task<IResult> RevokeOtherPortalSessions(
    HttpContext context,
    IAuthenticationService authenticationService)
{
    var revokedCount = await authenticationService.RevokeOtherSessionsAsync(
        GetPortalSessionToken(context),
        context.GetCorrelationId(),
        context.Connection.RemoteIpAddress?.ToString(),
        context.RequestAborted);
    return Results.Ok(new RevokeOtherSessionsResponse(revokedCount));
}

static async Task<PortalSessionContext> ResolvePortalSessionAsync(
    HttpContext context,
    IAuthenticationService authenticationService)
{
    var session = await authenticationService.ResolveSessionAsync(
        GetPortalSessionToken(context),
        context.GetCorrelationId(),
        context.Connection.RemoteIpAddress?.ToString(),
        context.RequestAborted);
    context.Items["PortalSessionContext"] = session;
    return session;
}

static async Task<PortalSessionContext> ResolveClientSessionAsync(
    HttpContext context,
    IAuthenticationService authenticationService,
    IAuditService auditService)
{
    var session = await ResolvePortalSessionAsync(
        context,
        authenticationService);
    if (session.UserRole == PortalRoles.ClientUser)
    {
        return session;
    }

    await auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            "portal.access",
            "refused",
            "role_insufficient",
            CustomerId: session.CustomerId,
            ActorUserId: session.UserId,
            SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);
    throw new PortalAccessDeniedException();
}

// Journalise une mutation de catalogue et traduit son code metier en statut
// HTTP. Un refus fonctionnel (code deja pris, recouvrement de fenetres) est un
// conflit, pas une erreur serveur : le client doit pouvoir le distinguer d'une
// panne pour reproposer une saisie.
static async Task<IResult> CatalogMutationResultAsync(
    HttpContext context,
    IAuditService auditService,
    PortalSessionContext actor,
    string action,
    string targetType,
    string targetReference,
    BillingV2AdminCatalogMutationResponse result)
{
    var refused = result.Code.EndsWith("_TAKEN", StringComparison.Ordinal)
        || result.Code.EndsWith("_OVERLAP", StringComparison.Ordinal)
        || result.Code.EndsWith("_NOT_CLOSABLE", StringComparison.Ordinal);

    await auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            action,
            refused ? "refused" : "success",
            ReasonCode: result.Code,
            TargetType: targetType,
            TargetReference: targetReference,
            ActorUserId: actor.UserId,
            SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);

    var body = new
    {
        code = result.Code,
        message = result.Message,
        id = result.Id,
        correlation_id = context.GetCorrelationId()
    };

    return refused
        ? Results.Json(body, statusCode: StatusCodes.Status409Conflict)
        : Results.Ok(body);
}

static async Task<PortalSessionContext> ResolveAdminSessionAsync(
    HttpContext context,
    IAuthenticationService authenticationService,
    IAuditService auditService,
    string action)
{
    var session = await ResolvePortalSessionAsync(
        context,
        authenticationService);
    if (session.UserRole != PortalRoles.InternalAdmin)
    {
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "admin.access",
                "refused",
                "role_insufficient",
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        throw new PortalAccessDeniedException();
    }

    await auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            action,
            "success",
            TargetType: "admin_view",
            ActorUserId: session.UserId,
            SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);
    return session;
}

// Toute mutation de gabarit exige la permission dediee `settings.templates.write`
// (specification, section 20) : la permission generique `settings.write` ne suffit
// pas, un contenu envoye a des clients n'ayant pas le meme niveau de risque.
static async Task<PortalSessionContext> ResolveTemplateWriterAsync(
    HttpContext context,
    IEditorialRepository editorialRepository,
    IAuthenticationService authenticationService,
    IAuditService auditService)
{
    var actor = await ResolveAdminSessionAsync(
        context,
        authenticationService,
        auditService,
        "admin.communications.write");
    if (!await editorialRepository.HasAdminPermissionAsync(
            actor.UserId,
            "settings.templates.write",
            context.RequestAborted))
    {
        throw new PortalAccessDeniedException();
    }

    return actor;
}

static Task RecordTemplateAuditAsync(
    HttpContext context,
    IAuditService auditService,
    string actorUserId,
    string action,
    string targetType,
    string targetId,
    string code)
{
    var outcome = code is "TEMPLATE_UPDATED" or "TEMPLATE_TEST_SENT"
        ? "success"
        : "refused";
    return auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            action,
            outcome,
            code,
            targetType,
            targetId,
            ActorUserId: actorUserId,
            SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);
}

// La fiscalite n'est pas un reglage comme un autre : une mention erronee sur une
// facture engage l'entreprise. La permission est donc distincte de
// `settings.write`.
static async Task<PortalSessionContext> ResolveBillingWriterAsync(
    HttpContext context,
    IEditorialRepository editorialRepository,
    IAuthenticationService authenticationService,
    IAuditService auditService)
{
    var actor = await ResolveAdminSessionAsync(
        context,
        authenticationService,
        auditService,
        "admin.billing.settings.write");
    if (!await editorialRepository.HasAdminPermissionAsync(
            actor.UserId,
            "settings.billing.write",
            context.RequestAborted))
    {
        throw new PortalAccessDeniedException();
    }

    return actor;
}

static Task RecordFiscalAuditAsync(
    HttpContext context,
    IAuditService auditService,
    string actorUserId,
    string action,
    string code,
    string successCode)
    => auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            action,
            code == successCode ? "success" : "refused",
            code,
            "fiscal_policy",
            action,
            ActorUserId: actorUserId,
            SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);

static int ResolveFiscalStatusCode(string code)
    => code switch
    {
        "FISCAL_MENTION_SCHEDULED" or "FISCAL_MENTION_CANCELLED"
            => StatusCodes.Status200OK,
        "FISCAL_VERSION_CONFLICT" or "FISCAL_EFFECTIVE_DATE_TAKEN"
            => StatusCodes.Status409Conflict,
        "FISCAL_MENTION_NOT_CANCELLABLE" => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status400BadRequest
    };

// Toute mutation du diagnostic exige `settings.diagnostic.write` : un parcours
// public mal configure oriente de vrais clients vers une mauvaise formule.
static async Task<PortalSessionContext> ResolveDiagnosticWriterAsync(
    HttpContext context,
    IEditorialRepository editorialRepository,
    IAuthenticationService authenticationService,
    IAuditService auditService)
{
    var actor = await ResolveAdminSessionAsync(
        context,
        authenticationService,
        auditService,
        "admin.diagnostic.write");
    if (!await editorialRepository.HasAdminPermissionAsync(
            actor.UserId,
            "settings.diagnostic.write",
            context.RequestAborted))
    {
        throw new PortalAccessDeniedException();
    }

    return actor;
}

static Task RecordDiagnosticAuditAsync(
    HttpContext context,
    IAuditService auditService,
    string actorUserId,
    string action,
    string code,
    string successCode)
    => auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            action,
            code == successCode ? "success" : "refused",
            code,
            "diagnostic_configuration",
            action == "diagnostic_published" ? "published" : "draft",
            ActorUserId: actorUserId,
            SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);

static int ResolveDiagnosticStatusCode(string code)
    => code switch
    {
        "DIAGNOSTIC_VALID" or "DIAGNOSTIC_DRAFT_SAVED" or "DIAGNOSTIC_PUBLISHED"
            => StatusCodes.Status200OK,
        "DIAGNOSTIC_VERSION_CONFLICT" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };

static int ResolveTemplateStatusCode(string code)
    => code switch
    {
        "TEMPLATE_UPDATED" => StatusCodes.Status200OK,
        "TEMPLATE_VERSION_CONFLICT" => StatusCodes.Status409Conflict,
        "TEMPLATE_UNKNOWN_KEY" => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status400BadRequest,
    };

static string? GetPortalSessionToken(HttpContext context)
    => context.Request.Headers[
        AuthenticationHeaders.PortalSession].FirstOrDefault();

static AuthenticatedPortalUser ToPublicUser(PortalSessionContext session)
    => new(
        session.DisplayName,
        session.Email,
        session.UserRole == PortalRoles.ClientUser
            ? session.CustomerReference
            : null,
        session.UserStatus,
        session.UserRole,
        ToNullableUtcIso(session.LastLoginAtUtc));

static string ToUtcIso(DateTime value)
    => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");

static string? ToNullableUtcIso(DateTime? value)
    => value is null ? null : ToUtcIso(value.Value);

static async Task<AdCustomerContext> ResolveAdCustomerContextAsync(
    IActiveDirectoryLinkRepository repository,
    string customerReference,
    CancellationToken cancellationToken)
{
    var normalizedCustomerReference =
        ActiveDirectoryInputValidator.NormalizeCustomerReference(
            customerReference)
        ?? throw new PortalValidationException();
    return await repository.GetCustomerContextAsync(
            normalizedCustomerReference,
            cancellationToken)
        ?? throw new PortalDataNotFoundException();
}

static string NormalizeSamIdentifier(string value)
{
    return ActiveDirectoryInputValidator.NormalizeSamAccountName(value)
        ?? throw new PortalValidationException();
}

static string NormalizeGuidIdentifier(string value)
{
    var normalized = value.Trim();
    return Guid.TryParse(normalized, out var parsed)
        ? parsed.ToString("D")
        : throw new PortalValidationException();
}

static async Task<IResult> CompleteAdQueryAsync<T>(
    HttpContext context,
    IAuditService auditService,
    string action,
    string actorUserId,
    string? customerId,
    AdServiceResult<T> result)
{
    await RecordAdAuditAsync(
        context,
        auditService,
        action,
        result.StatusCode < 400 ? "success" : "refused",
        result.Code,
        "active_directory",
        null,
        actorUserId,
        customerId);

    if (result.StatusCode >= 400)
    {
        return Results.Json(
            new ApiError(
                result.Code,
                result.Message,
                context.GetCorrelationId()),
            statusCode: result.StatusCode);
    }

    return Results.Ok(result.Value);
}

static async Task<IResult> CompleteAdMutationAsync(
    HttpContext context,
    IAuditService auditService,
    string action,
    string actorUserId,
    string? customerId,
    string mode,
    AdServiceResult<AdDirectoryObjectSummary> result)
{
    await RecordAdAuditAsync(
        context,
        auditService,
        action,
        result.StatusCode >= 400
            ? "refused"
            : result.Changed
                ? "success"
                : "unchanged",
        result.Code,
        "active_directory",
        result.Value?.SamAccountName,
        actorUserId,
        customerId);

    if (result.StatusCode >= 400)
    {
        return Results.Json(
            new ApiError(
                result.Code,
                result.Message,
                context.GetCorrelationId()),
            statusCode: result.StatusCode);
    }

    return Results.Json(
        new AdMutationResponse(
            result.Code,
            result.Message,
            mode,
            result.Changed,
            context.GetCorrelationId(),
            result.Value),
        statusCode: result.StatusCode);
}

// Journal d'audit de la correction de coordonnees : jamais de valeur de champ,
// uniquement les identifiants techniques et l'issue de l'operation.
static async Task RecordProfileAuditAsync(
    HttpContext context,
    IAuditService auditService,
    PortalSessionContext session,
    string outcome,
    string? reasonCode)
{
    await auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            "portal.profile_update",
            outcome,
            reasonCode,
            "portal_user",
            session.UserId,
            session.CustomerId,
            session.UserId,
            context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);
}

static async Task RecordAdAuditAsync(
    HttpContext context,
    IAuditService auditService,
    string action,
    string outcome,
    string? reasonCode,
    string? targetType,
    string? targetReference,
    string? actorUserId,
    string? customerId)
{
    await auditService.RecordAsync(
        new AuditEvent(
            context.GetCorrelationId(),
            action,
            outcome,
            reasonCode,
            targetType,
            targetReference,
            customerId,
            actorUserId,
            context.Connection.RemoteIpAddress?.ToString()),
        context.RequestAborted);
}

static IResult BillingV2AdditionalUserResponse(
    HttpContext context,
    BillingV2AdditionalUserOperationResult result)
{
    var correlationId = context.GetCorrelationId();
    // La reponse ne porte ni identifiant d'utilisateur portail, ni statut de
    // cycle de vie : le navigateur relit la liste, qui est la seule projection
    // produit. Renvoyer ces valeurs ici les ferait exister cote client sans
    // qu'aucun ecran n'en ait besoin.
    return result.Succeeded
        ? Results.Ok(new
        {
            code = result.Code,
            message = result.Message,
            correlation_id = correlationId
        })
        : Results.Json(
            new ApiError(result.Code, result.Message, correlationId),
            statusCode: ResolveBillingV2AdditionalUserStatusCode(result.Code));
}

/// <summary>
/// Traduit un refus du cycle de vie en code HTTP.
/// </summary>
/// <remarks>
/// Confirmer l'existence d'une place qu'on n'a pas le droit de voir est deja
/// une fuite. Le service rabat donc lui-meme les refus d'appartenance sur
/// « introuvable », code et message compris — le statut HTTP ne suffirait pas,
/// le corps de la reponse est affiche a l'ecran. Les codes d'appartenance
/// restent listes ici en second rideau, pour qu'un futur appelant qui les
/// laisserait passer ne reponde pas 400.
/// </remarks>
static int ResolveBillingV2AdditionalUserStatusCode(string code)
    => code switch
    {
        BillingV2AdditionalUserRejectionCodes.SlotNotFound
            or BillingV2AdditionalUserRejectionCodes.SlotSubscriptionMismatch
            or BillingV2AdditionalUserRejectionCodes.SlotCustomerMismatch
            or BillingV2AdditionalUserMaterializationCodes.LifecycleMissing =>
                StatusCodes.Status404NotFound,
        BillingV2AdditionalUserRejectionCodes.SlotAlreadyAssigned
            or BillingV2AdditionalUserRejectionCodes.LifecycleAlreadyExists
            or BillingV2AdditionalUserRejectionCodes.EmailAlreadyUsed
            or BillingV2AdditionalUserRejectionCodes.SlotIsPrimary
            or BillingV2AdditionalUserRejectionCodes.SlotNotActive
            or BillingV2AdditionalUserRejectionCodes.SubscriptionNotProvisionable
            or BillingV2AdditionalUserRejectionCodes.SlotEntitlementMissing
            or BillingV2AdditionalUserRejectionCodes.SlotScopeIncoherent
            or BillingV2AdditionalUserRejectionCodes.CustomerNotFound
            or "INVALID_STATE" => StatusCodes.Status409Conflict,
        PortalPasswordSetupCodes.TokenExpired => StatusCodes.Status410Gone,
        BillingV2AdditionalUserMaterializationCodes.ProvisioningDisabled
            or BillingV2AdditionalUserMaterializationCodes
                .PasswordHandoffUnavailable =>
                StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest
    };

static async Task<T?> ReadPayload<T>(HttpContext context)
{
    try
    {
        return await context.Request.ReadFromJsonAsync<T>();
    }
    catch (JsonException)
    {
        return default;
    }
    catch (NotSupportedException)
    {
        return default;
    }
}

static int ResolveSignupAdminMutationStatusCode(string code)
    => code switch
    {
        "SIGNUP_NOT_FOUND" => StatusCodes.Status404NotFound,
        "INVALID_PASSWORD" or "INVALID_REQUEST" => StatusCodes.Status400BadRequest,
        _ when code.StartsWith("EMAIL_", StringComparison.Ordinal)
            => StatusCodes.Status502BadGateway,
        _ => StatusCodes.Status409Conflict
    };

static string[] GetDiagnosticSecretValues(IConfiguration configuration)
    => new[]
        {
            configuration["SERVICE_AUTH_TOKEN"],
            configuration["SQL_PASSWORD"],
            configuration["DEMO_PORTAL_PASSWORD"],
            configuration["DEMO_INTERNAL_ADMIN_PASSWORD"],
            configuration.GetConnectionString("DefaultConnection"),
            configuration.GetConnectionString("MariaDb")
        }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal)
        .ToArray()!;

static string? SanitizeDiagnosticValue(
    string? value,
    IReadOnlyCollection<string> secretValues,
    int maxLength = 512)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    var sanitized = value;
    foreach (var secretValue in secretValues)
    {
        sanitized = sanitized.Replace(
            secretValue,
            "<redacted>",
            StringComparison.Ordinal);
    }

    sanitized = sanitized
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal)
        .Trim();

    return sanitized.Length <= maxLength
        ? sanitized
        : sanitized[..maxLength] + "...";
}

static string? ToHeaderSafeValue(string? value)
{
    if (string.IsNullOrEmpty(value))
    {
        return value;
    }

    var builder = new System.Text.StringBuilder(value.Length);
    foreach (var character in value)
    {
        builder.Append(
            character is >= ' ' and <= '~' ? character : '?');
    }

    return builder.ToString();
}

static int MapCustomerAdProvisioningStatusCode(string code)
{
    return code switch
    {
        "INVALID_REQUEST" => StatusCodes.Status400BadRequest,
        "PROVISIONING_NO_TARGET_USERS" => StatusCodes.Status400BadRequest,
        "PROVISIONING_SERVICE_NOT_CONFIGURED" =>
            StatusCodes.Status400BadRequest,
        "PROVISIONING_GROUP_NOT_CONFIGURED" =>
            StatusCodes.Status400BadRequest,
        "PROVISIONING_OVERRIDE_REQUIRED" =>
            StatusCodes.Status403Forbidden,
        "AD_TARGET_OUTSIDE_ALLOWED_ROOTS" =>
            StatusCodes.Status403Forbidden,
        "AD_GROUP_SCOPE_INCOMPATIBLE" =>
            StatusCodes.Status409Conflict,
        "AD_READ_ONLY" => StatusCodes.Status403Forbidden,
        "AD_CONFIGURATION_INVALID" =>
            StatusCodes.Status503ServiceUnavailable,
        "AD_UNAVAILABLE" => StatusCodes.Status503ServiceUnavailable,
        "AD_ACCESS_DENIED" => StatusCodes.Status403Forbidden,
        "PROVISIONING_SERVICE_NOT_FOUND" =>
            StatusCodes.Status404NotFound,
        _ => code.StartsWith(
                 "CUSTOMER_AD_",
                 StringComparison.Ordinal)
             || code.StartsWith(
                 "AD_GROUP_MEMBER_",
                 StringComparison.Ordinal)
            ? StatusCodes.Status200OK
            : StatusCodes.Status200OK
    };
}

static string ResolveCustomerAdProvisioningAuditAction(
    string? operation,
    bool isOverride,
    string targetKind)
{
    var normalizedOperation = operation?.Trim().ToLowerInvariant();
    var normalizedTargetKind = targetKind.Trim().ToLowerInvariant();
    return (normalizedTargetKind, normalizedOperation, isOverride) switch
    {
        ("service", "activate", true) =>
            "subscription.provisioning.override_service_activate",
        ("service", "remove", true) =>
            "subscription.provisioning.override_service_remove",
        ("service", "activate", false) =>
            "subscription.provisioning.manual_service_activate",
        ("service", "remove", false) =>
            "subscription.provisioning.manual_service_remove",
        ("group", "activate", true) =>
            "subscription.provisioning.override_group_activate",
        ("group", "remove", true) =>
            "subscription.provisioning.override_group_remove",
        ("group", "activate", false) =>
            "subscription.provisioning.manual_group_activate",
        ("group", "remove", false) =>
            "subscription.provisioning.manual_group_remove",
        _ => "subscription.provisioning.manual_action"
    };
}

public partial class Program
{
}
