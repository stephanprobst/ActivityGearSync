using System.Text.Json.Serialization;

namespace ActivityGearSync.Models;

public sealed class ActivityTextUpdateRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
