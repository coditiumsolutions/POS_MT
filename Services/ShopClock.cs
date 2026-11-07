namespace POS_MT.Services;

/// <summary>
/// Shop-local clock for UI "today" labels. Hosting servers are often UTC/US; tills are Pakistan (UTC+5).
/// </summary>
public static class ShopClock
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Asia/Karachi", "Pakistan Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // UTC+5 fallback if OS timezone data is missing.
        return TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "Pakistan Standard Time", "Pakistan Standard Time");
    }

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateTime Today => Now.Date;

    public static string FormatTopbarDate(DateTime? value = null) =>
        (value ?? Now).ToString("dd MMM, yyyy");
}
