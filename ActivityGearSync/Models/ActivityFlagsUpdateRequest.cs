using System.Text.Json.Serialization;

namespace ActivityGearSync.Models;

public sealed class ActivityFlagsUpdateRequest
{
    [JsonPropertyName("commute")]
    public bool? Commute { get; init; }

    [JsonPropertyName("trainer")]
    public bool? Trainer { get; init; }

    [JsonPropertyName("hide_from_home")]
    public bool? HideFromHome { get; init; }
}
