using Strava.Console.Models;

namespace Strava.Console.Services;

public interface IStravaApiService
{
    Task<StravaAthlete> GetAthleteAsync(CancellationToken cancellationToken = default);

    Task<List<StravaActivity>> GetActivitiesAsync(
        int page = 1,
        int perPage = 100,
        DateTime? after = null,
        DateTime? before = null,
        CancellationToken cancellationToken = default);

    Task<List<StravaActivity>> GetAllActivitiesAsync(
        IProgress<(int fetched, int total)>? progress = null,
        DateTime? after = null,
        DateTime? before = null,
        CancellationToken cancellationToken = default);

    Task<StravaActivity> UpdateActivityGearAsync(
        long activityId,
        string? gearId,
        CancellationToken cancellationToken = default);
}
