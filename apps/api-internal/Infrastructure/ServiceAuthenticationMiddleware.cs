using System.Security.Cryptography;
using System.Text;
using Kermaria.ApiInternal.Contracts;

namespace Kermaria.ApiInternal.Infrastructure;

public sealed class ServiceAuthenticationMiddleware
{
    public const string HeaderName = "X-Service-Auth";

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private readonly byte[]? _expectedTokenHash;
    private readonly ILogger<ServiceAuthenticationMiddleware> _logger;

    public ServiceAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<ServiceAuthenticationMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;

        var token = configuration["SERVICE_AUTH_TOKEN"];
        _expectedTokenHash = string.IsNullOrWhiteSpace(token)
            ? null
            : SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_environment.IsProduction()
            || !context.Request.Path.StartsWithSegments(
                "/internal",
                StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var providedToken = context.Request.Headers[HeaderName]
            .FirstOrDefault();
        var providedTokenHash = string.IsNullOrWhiteSpace(providedToken)
            ? null
            : SHA256.HashData(Encoding.UTF8.GetBytes(providedToken));
        var authenticated = _expectedTokenHash is not null
            && providedTokenHash is not null
            && CryptographicOperations.FixedTimeEquals(
                _expectedTokenHash,
                providedTokenHash);

        if (authenticated)
        {
            await _next(context);
            return;
        }

        var correlationId = context.GetCorrelationId();
        _logger.LogWarning(
            "Service authentication refused correlation_id {CorrelationId}",
            correlationId);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(
            new ApiError(
                "SERVICE_AUTH_REQUIRED",
                "L'identité du service appelant n'est pas valide.",
                correlationId));
    }
}
