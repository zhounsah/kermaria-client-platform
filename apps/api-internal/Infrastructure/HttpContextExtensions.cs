namespace Kermaria.ApiInternal.Infrastructure;

public static class HttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext context)
    {
        return context.Items[CorrelationIdMiddleware.ItemKey] as string
            ?? CorrelationIdProvider.Resolve(null);
    }
}
