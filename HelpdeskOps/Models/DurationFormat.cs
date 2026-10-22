namespace HelpdeskOps.Models;

public static class DurationFormat
{
    // 1590 minutes -> "1 gün 2 saat", 95 -> "1 saat 35 dk", 20 -> "20 dk"
    public static string FromMinutes(double minutes)
    {
        var span = TimeSpan.FromMinutes(Math.Round(minutes));
        if (span.TotalDays >= 1)
            return span.Hours > 0 ? $"{(int)span.TotalDays} gün {span.Hours} saat" : $"{(int)span.TotalDays} gün";
        if (span.TotalHours >= 1)
            return span.Minutes > 0 ? $"{(int)span.TotalHours} saat {span.Minutes} dk" : $"{(int)span.TotalHours} saat";
        return $"{span.Minutes} dk";
    }
}
