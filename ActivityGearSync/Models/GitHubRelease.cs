using System.Text.Json.Serialization;

namespace ActivityGearSync.Models;

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public required string TagName { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<GitHubAsset> Assets { get; init; }
}

public sealed class GitHubAsset
{
    public required string Name { get; init; }

    [JsonPropertyName("browser_download_url")]
    public required string BrowserDownloadUrl { get; init; }

    public required long Size { get; init; }
}
