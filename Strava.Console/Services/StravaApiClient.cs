using System.Net.Http.Headers;
using System.Net.Http.Json;
using Strava.Console.Infrastructure;
using Strava.Console.Models;

namespace Strava.Console.Services;

public sealed class StravaApiClient(
    HttpClient httpClient,
    StravaAuthClient authClient,
    RateLimiter rateLimiter)
{
    private const string BaseUrl = "https://www.strava.com/api/v3";

    public async Task<StravaAthlete> GetAthleteAsync(CancellationToken cancellationToken = default)
    {
        await rateLimiter.WaitIfNeededAsync(cancellationToken);
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "/athlete");
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StravaAthlete>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize athlete response.");
    }

    private async Task<List<StravaActivity>> GetActivitiesAsync(
        int page = 1,
        int perPage = 100,
        DateTime? after = null,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        await rateLimiter.WaitIfNeededAsync(cancellationToken);

        var url = $"/athlete/activities?page={page}&per_page={perPage}";
        if (after.HasValue)
        {
            url += $"&after={new DateTimeOffset(after.Value).ToUnixTimeSeconds()}";
        }

        if (before.HasValue)
        {
            url += $"&before={new DateTimeOffset(before.Value).ToUnixTimeSeconds()}";
        }

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<StravaActivity>>(cancellationToken: cancellationToken)
            ?? [];
    }

    public async Task<IEnumerable<StravaActivity>> GetAllActivitiesAsync(
        IProgress<(int fetched, int total)>? progress = null,
        DateTime? after = null,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        List<StravaActivity> allActivities = [];
        var page = 1;
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
        await rateLimiter.WaitIfNeededAsync(cancellationToken);

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"/activities/{activityId}");
        request.Content = JsonContent.Create(new { gear_id = gearId ?? "none" });

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<StravaActivity>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize activity response.");
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
