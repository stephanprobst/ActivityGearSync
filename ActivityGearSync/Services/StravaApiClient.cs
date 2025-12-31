using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using ActivityGearSync.Infrastructure;
using ActivityGearSync.Models;

namespace ActivityGearSync.Services;

public sealed class StravaApiClient(
    HttpClient httpClient,
    StravaAuthClient authClient,
    RateLimiter rateLimiter)
{
    private const string BaseUrl = "https://www.strava.com/api/v3";

    private static class RetryPolicy
    {
        public const int MaxRetries = 3;
        public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMinutes(1);
    }

    public async Task<StravaAthlete> GetAthleteAsync(CancellationToken cancellationToken = default)
    {
        await rateLimiter.WaitIfNeededAsync(cancellationToken);
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
        await rateLimiter.WaitIfNeededAsync(cancellationToken);

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
        await rateLimiter.WaitIfNeededAsync(cancellationToken);

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, $"/activities/{activityId}");
        var gearUpdate = new GearUpdateRequest { GearId = gearId ?? "none" };
        request.Content = JsonContent.Create(gearUpdate, AppJsonContext.Default.GearUpdateRequest);

        return await SendAndProcessAsync(request, AppJsonContext.Default.StravaActivity, cancellationToken);
    }

    private async Task<T> SendAndProcessAsync<T>(
        HttpRequestMessage request,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        int attempt = 0;

        while (attempt <= RetryPolicy.MaxRetries)
        {
            try
            {
                // Clone request if retrying (HttpRequestMessage can only be sent once)
                var requestToSend = attempt == 0 ? request : await CloneRequestAsync(request);
                var response = await httpClient.SendAsync(requestToSend, cancellationToken);

                // Parse rate limit headers from every response
                var rateLimitInfo = RateLimitHeaderParser.Parse(response.Headers);
                if (rateLimitInfo is not null)
                {
                    rateLimiter.UpdateFromServer(rateLimitInfo);
                }

                // Handle 429 Too Many Requests
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    attempt++;

                    if (attempt > RetryPolicy.MaxRetries)
                    {
                        var retryAfter = RateLimitHeaderParser.ParseRetryAfter(response.Headers);
                        throw new RateLimitExceededException(
                            "Rate limit exceeded after maximum retries",
                            rateLimitInfo,
                            retryAfter);
                    }

                    // Wait before retry
                    var delay = RateLimitHeaderParser.ParseRetryAfter(response.Headers)
                        ?? RetryPolicy.DefaultRetryDelay;

                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                // Throw for other errors
                response.EnsureSuccessStatusCode();

                // Deserialize response
                return await response.Content.ReadFromJsonAsync(jsonTypeInfo, cancellationToken)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            catch (HttpRequestException) when (attempt < RetryPolicy.MaxRetries)
            {
                attempt++;
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }

        throw new InvalidOperationException("Request failed after maximum retries.");
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(HttpMethod method, string endpoint)
    {
        var tokens = await authClient.GetValidTokensAsync()
            ?? throw new InvalidOperationException("Not authenticated. Please authenticate first.");

        var request = new HttpRequestMessage(method, $"{BaseUrl}{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return request;
    }

    private async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        // Copy headers
        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content if present
        if (original.Content is not null)
        {
            string content = await original.Content.ReadAsStringAsync();
            clone.Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
        }

        // Re-authorize the request
        var tokens = await authClient.GetValidTokensAsync()
            ?? throw new InvalidOperationException("Not authenticated. Please authenticate first.");
        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return clone;
    }
}
