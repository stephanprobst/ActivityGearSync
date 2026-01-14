using ActivityGearSync.Models;

namespace ActivityGearSync.Interfaces;

public interface IStravaApiClient
{
    Task<StravaAthlete> GetAthleteAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<StravaActivity>> GetAllActivitiesAsync(
        IProgress<(int fetched, int total)>? progress = null,
        DateTime? after = null,
        DateTime? before = null,
        CancellationToken cancellationToken = default);

    Task<StravaActivity> UpdateActivityGearAsync(
        long activityId,
        string? gearId,
        CancellationToken cancellationToken = default);

    Task<StravaActivity> UpdateActivitySportTypeAsync(
        long activityId,
        string sportType,
        CancellationToken cancellationToken = default);

    Task<StravaActivity> UpdateActivityFlagsAsync(
        long activityId,
        ActivityFlagsUpdateRequest flags,
        CancellationToken cancellationToken = default);

    Task<StravaActivity> UpdateActivityTextAsync(
        long activityId,
        string? name,
        string? description,
        CancellationToken cancellationToken = default);

    Task<ActivityStreams?> GetActivityStreamsAsync(
        long activityId,
        CancellationToken cancellationToken = default);
}
