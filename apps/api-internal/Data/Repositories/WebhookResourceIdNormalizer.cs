namespace Kermaria.ApiInternal.Data.Repositories;

internal static class WebhookResourceIdNormalizer
{
    public const int MaximumLength = 255;

    public static string? Normalize(string? resourceId)
    {
        var trimmed = resourceId?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaximumLength
            ? trimmed
            : trimmed[..MaximumLength];
    }
}
