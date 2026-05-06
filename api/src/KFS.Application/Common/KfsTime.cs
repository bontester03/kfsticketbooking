namespace KFS.Application.Common;

/// All datetimes are persisted in UTC; this helper renders them in the school's local time
/// (Asia/Riyadh, UTC+3, no DST) for emails, reports, and UI output. KFS is in Riyadh —
/// hosting may run in UAE North until Saudi East is GA, but the displayed time always
/// follows the school's wall clock.
public static class KfsTime
{
    public const string IanaId = "Asia/Riyadh";
    // Windows-specific name. NOT "Arabian Standard Time" (which is Asia/Dubai, UTC+4).
    public const string WindowsId = "Arab Standard Time";

    private static readonly TimeZoneInfo Zone = Resolve();

    public static DateTime ToLocal(DateTime utc)
    {
        if (utc.Kind == DateTimeKind.Local)
            utc = utc.ToUniversalTime();
        else if (utc.Kind == DateTimeKind.Unspecified)
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
    }

    public static string FormatLocal(this DateTime utc, string format = "dd MMM yyyy, HH:mm")
        => ToLocal(utc).ToString(format);

    private static TimeZoneInfo Resolve()
    {
        // Linux base images use IANA names; Windows hosts use the legacy Windows names.
        // .NET 8 transparently maps either form on either OS, but resolving both keeps the
        // code defensive against minor host-tzdata gaps (e.g. trimmed Alpine images).
        try { return TimeZoneInfo.FindSystemTimeZoneById(IanaId); }
        catch (TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(WindowsId); }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("KFS-Riyadh", TimeSpan.FromHours(3),
                    "Arab Standard Time", "Arab Standard Time");
            }
        }
    }
}
