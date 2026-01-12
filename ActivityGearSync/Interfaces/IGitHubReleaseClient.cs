using ActivityGearSync.Models;

namespace ActivityGearSync.Interfaces;

public interface IGitHubReleaseClient
{
    Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);

    Task DownloadAssetAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<(long downloaded, long total)>? progress = null,
        CancellationToken cancellationToken = default);
}
