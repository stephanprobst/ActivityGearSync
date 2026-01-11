using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActivityGearSync.Models;

/// <summary>
/// Response from Strava streams API with key_by_type=true.
/// Keys are stream types (time, latlng, altitude, heartrate, cadence, watts, temp).
/// </summary>
public sealed class ActivityStreams
{
    [JsonPropertyName("time")]
    public StreamData? Time { get; init; }

    [JsonPropertyName("latlng")]
    public StreamData? LatLng { get; init; }

    [JsonPropertyName("altitude")]
    public StreamData? Altitude { get; init; }

    [JsonPropertyName("heartrate")]
    public StreamData? HeartRate { get; init; }

    [JsonPropertyName("cadence")]
    public StreamData? Cadence { get; init; }

    [JsonPropertyName("watts")]
    public StreamData? Watts { get; init; }

    [JsonPropertyName("temp")]
    public StreamData? Temperature { get; init; }

    public bool HasGpsData => LatLng?.Data is { Count: > 0 };
}

public sealed class StreamData
{
    [JsonPropertyName("data")]
    public required List<JsonElement> Data { get; init; }

    [JsonPropertyName("series_type")]
    public string? SeriesType { get; init; }

    [JsonPropertyName("original_size")]
    public int OriginalSize { get; init; }

    [JsonPropertyName("resolution")]
    public string? Resolution { get; init; }
}
