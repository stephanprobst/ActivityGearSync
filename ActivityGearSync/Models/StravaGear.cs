using System.Text.Json.Serialization;

namespace ActivityGearSync.Models;

public sealed class StravaGear
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("primary")]
    public bool Primary { get; init; }

    [JsonPropertyName("distance")]
    public float Distance { get; init; }

    [JsonPropertyName("brand_name")]
    public string? BrandName { get; init; }

    [JsonPropertyName("model_name")]
    public string? ModelName { get; init; }

    public string FormattedDistance => Distance >= 1000
        ? $"{Distance / 1000:F1} km"
        : $"{Distance:F0} m";

    public string DisplayName => string.IsNullOrEmpty(BrandName)
        ? Name
        : $"{Name} ({BrandName})";
}
