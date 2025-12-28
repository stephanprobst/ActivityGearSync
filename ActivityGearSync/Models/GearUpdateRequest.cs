using System.Text.Json.Serialization;

namespace ActivityGearSync.Models;

public sealed class GearUpdateRequest
{
    [JsonPropertyName("gear_id")]
    public required string GearId { get; init; }
}
