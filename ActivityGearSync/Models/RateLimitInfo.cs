namespace ActivityGearSync.Models;

public sealed record RateLimitInfo(
    int ShortTermLimit,
    int ShortTermUsage,
    int DailyLimit,
    int DailyUsage,
    DateTime Timestamp)
{
    public int ShortTermRemaining => Math.Max(0, ShortTermLimit - ShortTermUsage);
    public int DailyRemaining => Math.Max(0, DailyLimit - DailyUsage);
    public bool IsShortTermExhausted => ShortTermRemaining <= 0;
    public bool IsDailyExhausted => DailyRemaining <= 0;
}
