using ActivityGearSync.Models;

namespace ActivityGearSync.Shared;

public static class GearFilterLogic
{
    public static class GearFilters
    {
        public const string AllActivities = "All activities";
        public const string NoGearAssigned = "No gear assigned";
        public const string RemoveGear = "(Remove gear)";
    }

    public static bool MatchesGearFilter(
        StravaActivity activity,
        string filter,
        List<StravaGear> allGear,
        Func<string, string>? nameTransform = null)
    {
        if (string.Equals(filter, GearFilters.AllActivities, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(filter, GearFilters.NoGearAssigned, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrEmpty(activity.GearId);
        }

        nameTransform ??= name => name;
        var gear = allGear.FirstOrDefault(g =>
            string.Equals(nameTransform(g.Name), filter, StringComparison.OrdinalIgnoreCase));

        return gear is not null && string.Equals(activity.GearId, gear.Id, StringComparison.OrdinalIgnoreCase);
    }

    public static List<StravaActivity> FilterActivities(
        List<StravaActivity> activities,
        string activityType,
        string gearFilter,
        List<StravaGear> allGear,
        Func<string, string>? nameTransform = null)
    {
        return
        [
            .. activities.Where(a =>
                (string.Equals(activityType, ActivityTypes.AllTypes, StringComparison.OrdinalIgnoreCase)
                    || ActivityTypes.Matches(a, activityType))
                && MatchesGearFilter(a, gearFilter, allGear, nameTransform))
        ];
    }
}
