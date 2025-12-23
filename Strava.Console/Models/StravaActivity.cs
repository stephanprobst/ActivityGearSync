using System.Text.Json.Serialization;

namespace Strava.Console.Models;

public sealed class StravaActivity
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("sport_type")]
    public string SportType { get; init; } = string.Empty;

    [JsonPropertyName("start_date_local")]
    public DateTime StartDateLocal { get; init; }

    [JsonPropertyName("distance")]
    public float Distance { get; init; }

    [JsonPropertyName("moving_time")]
    public int MovingTime { get; init; }

    [JsonPropertyName("gear_id")]
    public string? GearId { get; init; }

    public string FormattedDistance => Distance >= 1000
        ? $"{Distance / 1000:F1} km"
        : $"{Distance:F0} m";

    public string FormattedDuration
    {
        get
        {
            var ts = TimeSpan.FromSeconds(MovingTime);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }
    }
}
