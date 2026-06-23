using System.Text.Json;
using Kermaria.ApiInternal;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Migration;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Infrastructure;
using Kermaria.ApiInternal.Services;
using Kermaria.ApiInternal.Data.Repositories;
using Kermaria.ApiInternal.Services.ActiveDirectory;
using Kermaria.ApiInternal.Services.Bpce;
using Microsoft.AspNetCore.Diagnostics;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
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
var bpceConfiguration = BpceConfigurationResolver.Resolve(builder.Configuration);
var authConfiguration = AuthConfigurationResolver.Resolve(
    builder.Configuration,
    builder.Environment);

builder.Services.AddSingleton(sqlConfiguration);
builder.Services.AddSingleton(adConfiguration);
builder.Services.AddSingleton(bpceConfiguration);
builder.Services.AddSingleton(authConfiguration);
builder.Services.AddSingleton<IPortalPasswordService, PortalPasswordService>();
builder.Services.AddSingleton<ISessionTokenService, SessionTokenService>();
builder.Services.AddSingleton<MockAuthenticationStore>();
builder.Services.AddSingleton<MockRequestWorkflowStore>();
builder.Services.AddSingleton<MockPortalNotificationStore>();
builder.Services.AddSingleton<MockCommercialStore>();
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
builder.Services.AddScoped<IActiveDirectoryLinkRepository>(
    _ => sqlConfiguration.IsPersistent
        ? new MariaDbActiveDirectoryLinkRepository(sqlConfiguration)
        : new MockActiveDirectoryLinkRepository());
builder.Services.AddScoped<IPortalService, PortalService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IRequestWorkflowService, RequestWorkflowService>();
builder.Services.AddScoped<
    IPortalNotificationService,
    PortalNotificationService>();
builder.Services.AddScoped<ICommercialService, CommercialService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddTransient<MariaDbMigrationRunner>();
builder.Services.AddSingleton<OperationalReadinessService>();
builder.Services.AddSingleton<IActiveDirectoryService>(serviceProvider =>
    adConfiguration.Mode switch
    {
        AdIntegrationMode.Mock =>
            new MockActiveDirectoryService(adConfiguration),
        AdIntegrationMode.ReadOnly or AdIntegrationMode.ControlledWrite =>
            new LdapActiveDirectoryService(
                adConfiguration,
                serviceProvider.GetRequiredService<
                    ILogger<LdapActiveDirectoryService>>()),
        _ => new DisabledActiveDirectoryService(adConfiguration)
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
    "API-INTERNAL started in environment {Environment}; persistence {PersistenceMode}; Active Directory mode {AdMode}; operations_enabled {OperationsEnabled}; BPCE mode {BpceMode}",
    app.Environment.EnvironmentName,
    sqlConfiguration.IsPersistent ? "mariadb" : "mock",
    adConfiguration.ModeName,
    adConfiguration.WritesEnabled,
    bpceConfiguration.ModeName);

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
app.MapGet(
    "/internal/portal/catalog",
    async (
        HttpContext context,
        ICommercialService service,
        IAuthenticationService authenticationService,
        IAuditService auditService) =>
    {
        _ = await ResolveClientSessionAsync(
            context,
            authenticationService,
            auditService);
        return CommercialOk(
            context,
            service,
            await service.GetClientCatalogAsync(context.RequestAborted));
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

        var pdf = await issuingService.GetCachedInvoicePdfAsync(
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

public partial class Program
{
}
