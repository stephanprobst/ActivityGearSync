using System.Text.Json.Serialization;

namespace ActivityGearSync.Models;

public sealed class SportTypeUpdateRequest
{
    [JsonPropertyName("sport_type")]
    public required string SportType { get; init; }
}
