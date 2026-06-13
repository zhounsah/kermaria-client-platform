namespace Kermaria.ApiInternal.Infrastructure;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = CorrelationIdProvider.Resolve(
            context.Request.Headers[HeaderName].FirstOrDefault());

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlation_id"] = correlationId
        }))
        {
            _logger.LogInformation(
                "Handling {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await _next(context);
        }
    }
}
