using System.Text.Json.Serialization;

namespace ActivityGearSync.Models;

public sealed class StravaAthlete
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("firstname")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("lastname")]
    public string LastName { get; init; } = string.Empty;

    [JsonPropertyName("bikes")]
    public List<StravaGear> Bikes { get; init; } = [];

    [JsonPropertyName("shoes")]
    public List<StravaGear> Shoes { get; init; } = [];

    public string FullName => $"{FirstName} {LastName}".Trim();

    public IEnumerable<StravaGear> AllGear => Bikes.Concat(Shoes);
}
