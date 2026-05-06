namespace KFS.Application.Common;

/// All datetimes are persisted in UTC; this helper renders them in the venue's local time
/// (Asia/Dubai, UTC+4, no DST) for emails, reports, and UI output.
public static class KfsTime
{
    public const string IanaId = "Asia/Dubai";
    public const string WindowsId = "Arabian Standard Time";

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
                return TimeZoneInfo.CreateCustomTimeZone("KFS-Dubai", TimeSpan.FromHours(4),
                    "Arabian Standard Time", "Arabian Standard Time");
            }
        }
    }
}
