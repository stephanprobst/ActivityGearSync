using System.Text.Json.Serialization;
using ActivityGearSync.Models;

namespace ActivityGearSync.Shared;

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
[JsonSerializable(typeof(SportTypeUpdateRequest))]
[JsonSerializable(typeof(ActivityFlagsUpdateRequest))]
[JsonSerializable(typeof(ActivityTextUpdateRequest))]
[JsonSerializable(typeof(List<StravaActivity>))]
[JsonSerializable(typeof(List<StravaGear>))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubAsset))]
[JsonSerializable(typeof(List<GitHubAsset>))]
public partial class AppJsonContext : JsonSerializerContext;
