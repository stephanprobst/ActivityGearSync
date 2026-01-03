using ActivityGearSync.Models;

namespace ActivityGearSync.Shared;

public sealed class RateLimiter
{
    private readonly Lock _lock = new();

    private static class Limits
    {
        public const int MaxRequestsPer15Min = 100;
        public const int MaxRequestsPerDay = 1000;
        public const int ShortTermSafetyBuffer = 5;
        public const int DailySafetyBuffer = 50;
    }

    public RateLimitInfo? LastServerInfo { get; private set; }

    public int RemainingRequests
    {
        get
        {
            var (shortTerm, _) = GetRemainingRequests();
            return shortTerm;
        }
    }

    public (int ShortTerm, int Daily) GetRemainingRequests()
    {
        lock (_lock)
        {
            if (LastServerInfo is not null)
            {
                return (LastServerInfo.ShortTermRemaining, LastServerInfo.DailyRemaining);
            }

            // No server data yet, assume full quota
            return (Limits.MaxRequestsPer15Min, Limits.MaxRequestsPerDay);
        }
    }

    public (TimeSpan? WaitTime, string? Reason) GetEstimatedWaitTime()
    {
        lock (_lock)
        {
            if (LastServerInfo is null)
            {
                return (null, null);
            }

            // Check daily limit first
            if (LastServerInfo.DailyRemaining <= Limits.DailySafetyBuffer)
            {
                var now = DateTime.UtcNow;
                var midnight = now.Date.AddDays(1);
                return (midnight - now, "daily limit");
            }

            // Check short-term limit
            if (LastServerInfo.ShortTermRemaining <= Limits.ShortTermSafetyBuffer)
            {
                // Calculate time until next 15-min window (0, 15, 30, 45 past the hour)
                var now = DateTime.UtcNow;
                int minutesPastQuarter = now.Minute % 15;
                int secondsToWait = ((15 - minutesPastQuarter) * 60) - now.Second;
                return (TimeSpan.FromSeconds(Math.Max(secondsToWait, 1)), "15-min limit");
            }

            return (null, null);
        }
    }

    public void UpdateFromServer(RateLimitInfo info)
    {
        lock (_lock)
        {
            LastServerInfo = info;
        }
    }

    public async Task WaitIfNeededAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var (waitTime, _) = GetEstimatedWaitTime();

            if (waitTime is null)
            {
                return;
            }

            await Task.Delay(waitTime.Value, cancellationToken);
        }
    }
}
