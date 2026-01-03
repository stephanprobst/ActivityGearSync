using ActivityGearSync.Models;

namespace ActivityGearSync.Infrastructure;

public static class ActivityTypes
{
    public const string AllTypes = "All Types";
    public const string Run = "Run";
    public const string Ride = "Ride";
    public const string Walk = "Walk";
    public const string Hike = "Hike";
    public const string Swim = "Swim";
    public const string Other = "Other";

    public static readonly string[] All = [AllTypes, Run, Ride, Walk, Hike, Swim, Other];

    public static bool Matches(StravaActivity activity, string filter)
    {
        return filter switch
        {
            Run => activity.Type is "Run" or "TrailRun" or "VirtualRun",
            Ride => activity.Type is "Ride" or "MountainBikeRide" or "GravelRide" or "EBikeRide" or "VirtualRide",
            Walk => activity.Type is "Walk",
            Hike => activity.Type is "Hike",
            Swim => activity.Type is "Swim" or "OpenWaterSwim",
            Other => activity.Type is not ("Run" or "TrailRun" or "VirtualRun" or "Ride" or "MountainBikeRide" or "GravelRide" or "EBikeRide" or "VirtualRide" or "Walk" or "Hike" or "Swim" or "OpenWaterSwim"),
            _ => true
        };
    }
}

public static class DateRanges
{
    public const string Last7Days = "Last 7 days";
    public const string Last30Days = "Last 30 days";
    public const string Last90Days = "Last 90 days";
    public const string ThisYear = "This year";
    public const string AllTime = "All time";

    public static readonly string[] All = [Last7Days, Last30Days, Last90Days, ThisYear, AllTime];

    public const int Days7 = 7;
    public const int Days30 = 30;
    public const int Days90 = 90;

    public static (DateTime? After, DateTime? Before) Calculate(string dateRange)
    {
        var now = DateTime.Now;
        return dateRange switch
        {
            Last7Days => (now.AddDays(-Days7), null),
            Last30Days => (now.AddDays(-Days30), null),
            Last90Days => (now.AddDays(-Days90), null),
            ThisYear => (new DateTime(now.Year, 1, 1), null),
            _ => (null, null)
        };
    }
}

public static class DisplayLimits
{
    public const int SelectionPageSize = 15;
    public const int ActivitiesTablePreviewCount = 10;
    public const int FailedActivitiesPreviewCount = 5;
    public const int ActivityNameMaxLength = 25;
    public const int ActivityNameTruncatedLength = 22;
    public const int TextMaxDisplayLength = 30;
    public const int TextTruncatedLength = 27;
}

public static class SelectionModes
{
    public const string SelectAll = "Select all";
    public const string SelectIndividually = "Select individually...";
    public const string Cancel = "Unselect all & Cancel";
}

public static class RateLimitThresholds
{
    public const int LowShortTermWarning = 10;
    public const int LowDailyWarning = 100;
    public const int MaxRequestsPer15Min = 100;
    public const int MaxRequestsPerDay = 1000;
}
