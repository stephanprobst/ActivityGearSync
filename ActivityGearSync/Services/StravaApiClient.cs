using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using ActivityGearSync.Infrastructure;
using ActivityGearSync.Models;

namespace ActivityGearSync.Services;

public sealed class StravaApiClient(HttpClient httpClient, StravaAuthClient authClient)
{
    private const string BaseUrl = "https://www.strava.com/api/v3";

    public async Task<StravaAthlete> GetAthleteAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "/athlete");
        return await SendAndProcessAsync(request, AppJsonContext.Default.StravaAthlete, cancellationToken);
    }

    private async Task<List<StravaActivity>> GetActivitiesAsync(
        int page = 1,
        int perPage = 100,
        DateTime? after = null,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        string url = $"/athlete/activities?page={page}&per_page={perPage}";
        if (after.HasValue)
        {
            url += $"&after={new DateTimeOffset(after.Value).ToUnixTimeSeconds()}";
        }

        if (before.HasValue)
        {
            url += $"&before={new DateTimeOffset(before.Value).ToUnixTimeSeconds()}";
        }

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);
        return await SendAndProcessAsync(request, AppJsonContext.Default.ListStravaActivity, cancellationToken)
            ?? [];
    }

    public async Task<IEnumerable<StravaActivity>> GetAllActivitiesAsync(
        IProgress<(int fetched, int total)>? progress = null,
        DateTime? after = null,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        List<StravaActivity> allActivities = [];
        int page = 1;
        const int perPage = 100;

        while (!cancellationToken.IsCancellationRequested)
        {
            var activities = await GetActivitiesAsync(page, perPage, after, before, cancellationToken);

            if (activities.Count == 0)
            {
                break;
            }

            allActivities.AddRange(activities);
            progress?.Report((allActivities.Count, -1));

            if (activities.Count < perPage)
            {
                break;
            }

            page++;
        }

        return allActivities;
    }

    public async Task<StravaActivity> UpdateActivityGearAsync(
        long activityId,
        string? gearId,
        CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"/activities/{activityId}");
        var gearUpdate = new GearUpdateRequest { GearId = gearId ?? "none" };
        request.Content = JsonContent.Create(gearUpdate, AppJsonContext.Default.GearUpdateRequest);

        return await SendAndProcessAsync(request, AppJsonContext.Default.StravaActivity, cancellationToken);
    }

    public async Task<StravaActivity> UpdateActivitySportTypeAsync(
        long activityId,
        string sportType,
        CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"/activities/{activityId}");
        var sportTypeUpdate = new SportTypeUpdateRequest { SportType = sportType };
        request.Content = JsonContent.Create(sportTypeUpdate, AppJsonContext.Default.SportTypeUpdateRequest);

        return await SendAndProcessAsync(request, AppJsonContext.Default.StravaActivity, cancellationToken);
    }

    public async Task<StravaActivity> UpdateActivityFlagsAsync(
        long activityId,
        ActivityFlagsUpdateRequest flags,
        CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"/activities/{activityId}");
        request.Content = JsonContent.Create(flags, AppJsonContext.Default.ActivityFlagsUpdateRequest);

        return await SendAndProcessAsync(request, AppJsonContext.Default.StravaActivity, cancellationToken);
    }

    private async Task<T> SendAndProcessAsync<T>(
        HttpRequestMessage request,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        // Polly handles retries via HttpClient pipeline
        // RateLimitHandler handles rate limit tracking
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(jsonTypeInfo, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize response.");
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(HttpMethod method, string endpoint)
    {
        var tokens = await authClient.GetValidTokensAsync()
            ?? throw new InvalidOperationException("Not authenticated. Please authenticate first.");

        var request = new HttpRequestMessage(method, $"{BaseUrl}{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return request;
    }
}
