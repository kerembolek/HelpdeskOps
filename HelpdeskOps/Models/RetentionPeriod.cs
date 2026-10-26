namespace HelpdeskOps.Models;

public static class RetentionPeriod
{
    // 730 -> "2 yıl", 180 -> "6 ay", 45 -> "45 gün"
    public static string Format(int days) => days switch
    {
        _ when days % 365 == 0 => $"{days / 365} yıl",
        _ when days % 30 == 0 => $"{days / 30} ay",
        _ => $"{days} gün"
    };
}
