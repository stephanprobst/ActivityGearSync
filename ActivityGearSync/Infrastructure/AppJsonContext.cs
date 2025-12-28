using System.Text.Json;
using System.Text.Json.Serialization;
using ActivityGearSync.Models;

namespace ActivityGearSync.Infrastructure;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StravaTokens))]
[JsonSerializable(typeof(StravaTokenAthlete))]
[JsonSerializable(typeof(StravaActivity))]
[JsonSerializable(typeof(StravaAthlete))]
[JsonSerializable(typeof(StravaGear))]
[JsonSerializable(typeof(ApiCredentials))]
[JsonSerializable(typeof(GearUpdateRequest))]
[JsonSerializable(typeof(List<StravaActivity>))]
[JsonSerializable(typeof(List<StravaGear>))]
public partial class AppJsonContext : JsonSerializerContext;
