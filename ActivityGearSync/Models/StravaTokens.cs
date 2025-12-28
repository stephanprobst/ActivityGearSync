using System.Text.Json.Serialization;

namespace ActivityGearSync.Models;

public sealed class StravaTokens
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; }

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; init; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";

    [JsonPropertyName("athlete")]
    public StravaTokenAthlete? Athlete { get; init; }

    public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt - 300; // 5 min buffer
}

public sealed class StravaTokenAthlete
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("firstname")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastname")]
    public string? LastName { get; init; }
}
