namespace HelpdeskOps;

// The app serves an office in Turkey, so dates follow Turkey time whatever time zone the hosting server uses
public static class TurkeyTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateTime Today => Now.Date;

    // Stored values come back from the database without a kind, but they are always UTC
    public static DateTime ToTurkeyTime(this DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime ToUtc(DateTime turkeyTime) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(turkeyTime, DateTimeKind.Unspecified), Zone);
}
