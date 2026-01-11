using System.Text.Json.Serialization;

namespace ActivityGearSync.Models;

public sealed class StravaActivity
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("sport_type")]
    public required string SportType { get; init; }

    [JsonPropertyName("start_date_local")]
    public DateTime StartDateLocal { get; init; }

    [JsonPropertyName("distance")]
    public float Distance { get; init; }

    [JsonPropertyName("moving_time")]
    public int MovingTime { get; init; }

    [JsonPropertyName("gear_id")]
    public string? GearId { get; init; }

    [JsonPropertyName("commute")]
    public bool Commute { get; init; }

    [JsonPropertyName("trainer")]
    public bool Trainer { get; init; }

    [JsonPropertyName("private")]
    public bool Private { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("start_latlng")]
    public double[]? StartLatLng { get; init; }

    public bool HasGps => StartLatLng is { Length: 2 };

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
