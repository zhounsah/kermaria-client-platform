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
builder.Services.AddSingleton(subscriptionProvisioningConfiguration);
builder.Services.AddSingleton(downloadStorageConfiguration);
builder.Services.AddSingleton<IPortalPasswordService, PortalPasswordService>();
builder.Services.AddSingleton<ISessionTokenService, SessionTokenService>();
builder.Services.AddSingleton<IDownloadStorageService, DownloadStorageService>();
builder.Services.AddSingleton<MockAuthenticationStore>();
builder.Services.AddSingleton<MockRequestWorkflowStore>();
builder.Services.AddSingleton<MockPortalNotificationStore>();
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
builder.Services.AddScoped<IRequestWorkflowRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbRequestWorkflowRepository(sqlConfiguration)
        : new MockRequestWorkflowRepository(
            serviceProvider.GetRequiredService<MockRequestWorkflowStore>(),
            serviceProvider.GetRequiredService<MockPortalNotificationStore>()));
builder.Services.AddScoped<IPortalNotificationRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbPortalNotificationRepository(sqlConfiguration)
        : new MockPortalNotificationRepository(
            serviceProvider.GetRequiredService<MockPortalNotificationStore>()));
builder.Services.AddSingleton<MockBpceInvoicingRepository>();
builder.Services.AddScoped<IBpceInvoicingRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? (IBpceInvoicingRepository)new MariaDbBpceInvoicingRepository(sqlConfiguration)
        : serviceProvider.GetRequiredService<MockBpceInvoicingRepository>());
builder.Services.AddScoped<IInvoiceIssuingService, InvoiceIssuingService>();
builder.Services.AddScoped<ICommercialRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbCommercialRepository(sqlConfiguration)
        : new MockCommercialRepository(
            serviceProvider.GetRequiredService<MockCommercialStore>()));
builder.Services.AddSingleton<MockManagedContentStore>();
builder.Services.AddScoped<IManagedContentRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbManagedContentRepository(sqlConfiguration)
        : new MockManagedContentRepository(
            serviceProvider.GetRequiredService<MockManagedContentStore>()));
builder.Services.AddScoped<IDownloadRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbDownloadRepository(sqlConfiguration)
        : new MockDownloadRepository(
            serviceProvider.GetRequiredService<MockDownloadStore>()));
builder.Services.AddSingleton<MockPublicPackCatalogStore>();
builder.Services.AddScoped<IPublicPackCatalogRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbPublicPackCatalogRepository(sqlConfiguration)
        : new MockPublicPackCatalogRepository(
            serviceProvider.GetRequiredService<MockPublicPackCatalogStore>()));
builder.Services.AddSingleton<MockSubscriptionStore>();
builder.Services.AddScoped<ISubscriptionRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbSubscriptionRepository(sqlConfiguration)
        : new MockSubscriptionRepository(
            serviceProvider.GetRequiredService<MockSubscriptionStore>()));
builder.Services.AddSingleton<MockSubscriptionProvisioningActionStore>();
builder.Services.AddScoped<ISubscriptionProvisioningActionRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbSubscriptionProvisioningActionRepository(sqlConfiguration)
        : new MockSubscriptionProvisioningActionRepository(
            serviceProvider.GetRequiredService<
                MockSubscriptionProvisioningActionStore>()));
builder.Services.AddSingleton<MockPayPalWebhookStore>();
builder.Services.AddScoped<IPayPalWebhookRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbPayPalWebhookRepository(sqlConfiguration)
        : new MockPayPalWebhookRepository(
            serviceProvider.GetRequiredService<MockPayPalWebhookStore>()));
builder.Services.AddSingleton<MockStripeWebhookStore>();
builder.Services.AddScoped<IStripeWebhookRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbStripeWebhookRepository(sqlConfiguration)
        : new MockStripeWebhookRepository(
            serviceProvider.GetRequiredService<MockStripeWebhookStore>()));
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
builder.Services.AddScoped<IPortalService, PortalService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IRequestWorkflowService, RequestWorkflowService>();
builder.Services.AddScoped<
    IPortalNotificationService,
    PortalNotificationService>();
builder.Services.AddScoped<ICommercialService, CommercialService>();
builder.Services.AddScoped<
    ICommercialOfferTopologyService,
    CommercialOfferTopologyService>();
builder.Services.AddScoped<
    IClientServiceCatalogService,
    ClientServiceCatalogService>();
builder.Services.AddSingleton<MockCartStore>();
builder.Services.AddScoped<ICartRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbCartRepository(sqlConfiguration)
        : new MockCartRepository(
            serviceProvider.GetRequiredService<MockCartStore>()));
builder.Services.AddSingleton<MockRecurringCheckoutStore>();
builder.Services.AddScoped<IRecurringCheckoutRepository>(
    serviceProvider => sqlConfiguration.IsPersistent
        ? new MariaDbRecurringCheckoutRepository(sqlConfiguration)
        : new MockRecurringCheckoutRepository(
            serviceProvider.GetRequiredService<MockRecurringCheckoutStore>()));
builder.Services.AddSingleton<
    IBilledRecurringCheckoutSchemaEnsurer,
    BilledRecurringCheckoutSchemaEnsurer>();
builder.Services.AddSingleton<IDownloadSchemaEnsurer, DownloadSchemaEnsurer>();
builder.Services.AddScoped<ICartProvisioningTrigger, CartProvisioningTrigger>();
builder.Services.AddScoped<
    IBilledSubscriptionPaymentTrigger,
    BilledSubscriptionPaymentTrigger>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IRecurringCheckoutService, RecurringCheckoutService>();
builder.Services.AddScoped<IManagedContentService, ManagedContentService>();
builder.Services.AddScoped<IDownloadService, DownloadService>();
builder.Services.AddScoped<IPublicPackCatalogService, PublicPackCatalogService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IProvisioningService, ProvisioningService>();
builder.Services.AddScoped<
    ISubscriptionProvisioningManager,
    SubscriptionProvisioningManager>();
builder.Services.AddScoped<
    ICustomerActiveDirectoryAdministrationService,
    CustomerActiveDirectoryAdministrationService>();
builder.Services.AddScoped<IPayPalWebhookService, PayPalWebhookService>();
builder.Services.AddScoped<IStripeWebhookService, StripeWebhookService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddHostedService<PayPalPendingCancellationWorker>();
builder.Services.AddHostedService<BillingSubscriptionRenewalWorker>();
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
builder.Services.AddSingleton<IBpceTokenCache, BpceTokenCache>();
builder.Services.AddSingleton<IBpceApiClient, BpceApiClient>();
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
            CartOfferNotEligibleException => (
                StatusCodes.Status400BadRequest,
                "CART_OFFER_NOT_ELIGIBLE",
                "Cette offre ne peut pas être ajoutée au panier (offre récurrente, gratuite ou indisponible)."),
            EmptyCartException => (
                StatusCodes.Status400BadRequest,
                "CART_EMPTY",
                "Votre panier est vide."),
            RecurringOfferNotEligibleException => (
                StatusCodes.Status400BadRequest,
                "RECURRING_CHECKOUT_OFFER_NOT_ELIGIBLE",
                "Cette offre ne peut pas être ajoutée à la sélection d'abonnements."),
            EmptyRecurringCheckoutException => (
                StatusCodes.Status400BadRequest,
                "RECURRING_CHECKOUT_EMPTY",
                "Aucun abonnement n'est actuellement sélectionné."),
            PortalValidationException => (
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                "La demande est incomplète ou invalide."),
            DownloadConflictException conflict => (
                StatusCodes.Status409Conflict,
                conflict.Code,
                conflict.Message),
            PortalDataNotFoundException => (
                StatusCodes.Status404NotFound,
                "PORTAL_DATA_NOT_FOUND",
                "La ressource demandée est introuvable."),
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
            context.Response.Headers["X-Debug-Exception-Type"] =
                exceptionType;
            context.Response.Headers["X-Debug-Exception-Message"] =
                exceptionMessage ?? "<none>";
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
        IAuthenticationService authenticationService,
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

        var link = await linkRepository.FindUserLinkByEmailAsync(
            session.CustomerReference,
            session.Email,
            context.RequestAborted);
        if (link is null)
        {
            await RecordAdAuditAsync(
                context,
                auditService,
                "ad.password_change",
                "refused",
                "AD_NO_LINK_FOR_USER",
                "portal_user",
                session.UserId,
                session.UserId,
                session.CustomerId);
            return Results.Json(
                new ApiError(
                    "AD_NO_LINK_FOR_USER",
                    "Aucun compte Active Directory n'est associe a ce profil.",
                    context.GetCorrelationId()),
                statusCode: StatusCodes.Status404NotFound);
        }

        var result = await adService.ChangeUserPasswordAsync(
            link.CustomerReference,
            link.SamAccountName,
            request.CurrentPassword,
            request.NewPassword,
            context.RequestAborted);

        var failed = result.StatusCode >= 400 || !result.Changed;
        if (failed)
        {
            rateLimiter.RegisterFailure(session.UserId, now);
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
                        : "Le mot de passe ne respecte pas la politique du domaine ou le mot de passe actuel est incorrect.",
                    context.GetCorrelationId()),
                statusCode: locked
                    ? StatusCodes.Status429TooManyRequests
                    : StatusCodes.Status400BadRequest);
        }

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
                "AD_PASSWORD_CHANGED",
                "Le mot de passe Active Directory a ete change.",
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
// V0.27 : catalogue lisible sans session pour alimenter la vitrine publique
// (`/offres`). Toujours protégé par `X-Service-Auth` côté ingress webportal.
app.MapGet(
    "/internal/portal/catalog",
    async (
        HttpContext context,
        ICommercialService service) =>
    {
        return CommercialOk(
            context,
            service,
            await service.GetClientCatalogAsync(context.RequestAborted));
    });

// V0.35 : panier / commande groupée à la carte (offres one-shot). Session
// client requise ; le panier est strictement borné au customer de la session.
app.MapGet(
    "/internal/portal/cart",
    async (
        HttpContext context,
        ICartService cartService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var cart = await cartService.GetCartAsync(
            session.CustomerId,
            context.RequestAborted);
        context.Response.Headers["X-Data-Source"] =
            cartService.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(cart);
    });
app.MapPost(
    "/internal/portal/cart/items",
    async (
        HttpContext context,
        ICartService cartService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var payload = await ReadPayload<CartAddRequest>(context);
        var cart = await cartService.AddItemAsync(
            session.CustomerId,
            payload?.OfferId,
            payload?.Quantity,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "cart.item_added",
                "success",
                TargetType: "commercial_offer",
                TargetReference: payload?.OfferId,
                ActorUserId: session.UserId,
                CustomerId: session.CustomerId),
            context.RequestAborted);
        return Results.Ok(
            new CartMutationResponse(cart, context.GetCorrelationId()));
    });
app.MapPost(
    "/internal/portal/cart/items/remove",
    async (
        HttpContext context,
        ICartService cartService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var payload = await ReadPayload<CartRemoveRequest>(context);
        var cart = await cartService.RemoveItemAsync(
            session.CustomerId,
            payload?.OfferId,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "cart.item_removed",
                "success",
                TargetType: "commercial_offer",
                TargetReference: payload?.OfferId,
                ActorUserId: session.UserId,
                CustomerId: session.CustomerId),
            context.RequestAborted);
        return Results.Ok(
            new CartMutationResponse(cart, context.GetCorrelationId()));
    });
app.MapPost(
    "/internal/portal/cart/confirm",
    async (
        HttpContext context,
        ICartService cartService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var result = await cartService.ConfirmAsync(
            session.CustomerId,
            session.UserId,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "cart.confirmed",
                "success",
                TargetType: "commercial_document",
                TargetReference: result.DocumentId,
                ActorUserId: session.UserId,
                CustomerId: session.CustomerId),
            context.RequestAborted);
        return Results.Ok(result);
    });

app.MapGet(
    "/internal/portal/checkout/summary",
    async (
        HttpContext context,
        IRecurringCheckoutService checkoutService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var summary = await checkoutService.GetSummaryAsync(
            session,
            context.RequestAborted);
        context.Response.Headers["X-Data-Source"] =
            checkoutService.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(summary);
    });
app.MapPost(
    "/internal/portal/checkout/subscriptions/items",
    async (
        HttpContext context,
        IRecurringCheckoutService checkoutService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var payload = await ReadPayload<CheckoutRecurringAddRequest>(context);
        var result = await checkoutService.AddItemAsync(
            session,
            payload?.OfferId,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "checkout.subscription_item_added",
                "success",
                TargetType: "commercial_offer",
                TargetReference: payload?.OfferId,
                ActorUserId: session.UserId,
                CustomerId: session.CustomerId),
            context.RequestAborted);
        return Results.Ok(result);
    });
app.MapPost(
    "/internal/portal/checkout/subscriptions/items/remove",
    async (
        HttpContext context,
        IRecurringCheckoutService checkoutService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var payload = await ReadPayload<CheckoutRecurringAddRequest>(context);
        var result = await checkoutService.RemoveItemAsync(
            session,
            payload?.OfferId,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "checkout.subscription_item_removed",
                "success",
                TargetType: "commercial_offer",
                TargetReference: payload?.OfferId,
                ActorUserId: session.UserId,
                CustomerId: session.CustomerId),
            context.RequestAborted);
        return Results.Ok(result);
    });
app.MapPost(
    "/internal/portal/checkout/subscriptions/confirm",
    async (
        HttpContext context,
        IRecurringCheckoutService checkoutService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var result = await checkoutService.ConfirmAsync(
            session,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "checkout.subscriptions_confirmed",
                "success",
                TargetType: "commercial_document",
                TargetReference: result.DocumentId,
                ActorUserId: session.UserId,
                CustomerId: session.CustomerId),
            context.RequestAborted);
        return Results.Ok(result);
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
            OfferReference: string.IsNullOrWhiteSpace(payload.OfferReference)
                ? null
                : payload.OfferReference.Trim());

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
// hCaptcha, honeypot et rate limit IP sont assurés côté webportal BFF.
app.MapPost(
    "/internal/signup",
    async (
        HttpContext context,
        ISignupService signupService,
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
            var statusCode = result.Code == "SIGNUP_DISABLED"
                ? StatusCodes.Status403Forbidden
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
        ISubscriptionService service,
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
    "/internal/portal/pending-pack-selection",
    async (
        HttpContext context,
        ISignupService signupService,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        context.Response.Headers["X-Data-Source"] =
            signupService.IsPersistent ? "mariadb" : "mock";
        return Results.Ok(
            await signupService.GetPendingPackSelectionAsync(
                session,
                context.RequestAborted));
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
    "/internal/portal/subscriptions",
    async (
        HttpContext context,
        ISubscriptionService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var payload = await ReadPayload<SubscriptionCreatePayload>(context)
            ?? throw new PortalValidationException();
        var rail = payload.Rail switch
        {
            "stripe" => "stripe",
            "paypal" => "paypal",
            _ => throw new PortalValidationException()
        };
        var externalSubscriptionId = rail == "stripe"
            ? payload.StripeSubscriptionId
            : payload.PayPalSubscriptionId;
        if (string.IsNullOrWhiteSpace(payload.OfferId)
            || string.IsNullOrWhiteSpace(externalSubscriptionId))
        {
            throw new PortalValidationException();
        }

        var result = await service.CreatePendingAsync(
            session,
            payload.OfferId.Trim(),
            rail,
            externalSubscriptionId.Trim(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "subscription.created",
                "success",
                TargetType: "subscription",
                TargetReference: result.Id,
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return SubscriptionOk(context, service, result);
    });
app.MapPost(
    "/internal/portal/subscriptions/{id}/return-approved",
    async (
        string id,
        HttpContext context,
        ISubscriptionService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        var session = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        var result = await service.MarkAsPendingActivationAsync(
            session,
            id,
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "subscription.return_approved",
                "success",
                TargetType: "subscription",
                TargetReference: id,
                CustomerId: session.CustomerId,
                ActorUserId: session.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return SubscriptionOk(context, service, result);
    });
app.MapPost(
    "/internal/portal/subscriptions/{id}/cancel",
    async (
        string id,
        HttpContext context,
        ISubscriptionService service,
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
            context.GetCorrelationId(),
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

app.MapPost(
    "/internal/webhooks/paypal",
    async (
        HttpContext context,
        IPayPalWebhookService webhookService) =>
    {
        var payload = await ReadPayload<PayPalWebhookEventPayload>(context)
            ?? throw new PortalValidationException();
        var result = await webhookService.ProcessAsync(
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        return Results.Ok(new
        {
            event_id = result.EventId,
            status = result.Status,
            error_message = result.ErrorMessage,
            correlation_id = context.GetCorrelationId()
        });
    });

app.MapPost(
    "/internal/webhooks/stripe",
    async (
        HttpContext context,
        IStripeWebhookService webhookService) =>
    {
        var payload = await ReadPayload<StripeWebhookEventPayload>(context)
            ?? throw new PortalValidationException();
        var result = await webhookService.ProcessAsync(
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        var response = new
        {
            event_id = result.EventId,
            status = result.Status,
            error_message = result.ErrorMessage,
            correlation_id = context.GetCorrelationId()
        };
        return result.Status == "failed"
            ? Results.Json(
                response,
                statusCode: StatusCodes.Status500InternalServerError)
            : Results.Ok(response);
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
    "/internal/admin/catalog",
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
            "admin.catalog.read");
        return CommercialOk(
            context,
            service,
            await service.GetAdminCatalogAsync(context.RequestAborted));
    });
app.MapPost(
    "/internal/admin/catalog",
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
            "admin.catalog.write");
        var payload = await ReadPayload<CommercialOfferPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.CreateOfferAsync(
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_offer.create",
                "success",
                TargetType: "commercial_offer",
                TargetReference: result.Id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return CommercialOk(context, service, result);
    });
app.MapPatch(
    "/internal/admin/catalog/{id}",
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
            "admin.catalog.write");
        var payload = await ReadPayload<CommercialOfferPayload>(context)
            ?? throw new PortalValidationException();
        var result = await service.UpdateOfferAsync(
            id,
            payload,
            context.GetCorrelationId(),
            context.RequestAborted);
        await auditService.RecordAsync(
            new AuditEvent(
                context.GetCorrelationId(),
                "commercial_offer.update",
                result.Changed ? "success" : "unchanged",
                TargetType: "commercial_offer",
                TargetReference: result.Id,
                ActorUserId: actor.UserId,
                SourceAddress: context.Connection.RemoteIpAddress?.ToString()),
            context.RequestAborted);
        return CommercialOk(context, service, result);
    });
app.MapGet(
    "/internal/admin/subscriptions",
    async (
        HttpContext context,
        ISubscriptionService service,
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
        ISubscriptionService service,
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
        ISubscriptionService service,
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
            context.GetCorrelationId(),
            actor.UserId,
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
        ISubscriptionService service,
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

static IResult DownloadsOk<T>(
    HttpContext context,
    IDownloadService service,
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
    ISubscriptionService service,
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
