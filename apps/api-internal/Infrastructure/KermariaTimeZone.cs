namespace Kermaria.ApiInternal.Infrastructure;

public static class KermariaTimeZone
{
    public const string IanaId = "Europe/Paris";
    private const string WindowsId = "Romance Standard Time";

    private static readonly TimeZoneInfo _tz = ResolveTimeZone();

    public static TimeZoneInfo TimeZone => _tz;

    public static DateTime Now
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);

    public static DateTime ToLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            _tz);

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsId);
        }
    }
}
