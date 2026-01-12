using System.Net;
using System.Net.Http.Json;
using ActivityGearSync.Interfaces;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;

namespace ActivityGearSync.Clients;

public sealed class GitHubReleaseClient(HttpClient httpClient) : IGitHubReleaseClient
{
    private static class GitHub
    {
        public const string Owner = "stephanprobst";
        public const string Repo = "ActivityGearSync";
        public const string ApiBaseUrl = "https://api.github.com";
        public const string UserAgent = "ActivityGearSync";
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        const string url = $"{GitHub.ApiBaseUrl}/repos/{GitHub.Owner}/{GitHub.Repo}/releases/latest";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", GitHub.UserAgent);
        request.Headers.Add("Accept", "application/vnd.github+json");

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            AppJsonContext.Default.GitHubRelease, cancellationToken);
    }

    public async Task DownloadAssetAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<(long downloaded, long total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        request.Headers.Add("User-Agent", GitHub.UserAgent);
        request.Headers.Add("Accept", "application/octet-stream");

        var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1;
        long downloadedBytes = 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(destinationPath);

        byte[] buffer = new byte[8192];

        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;
            progress?.Report((downloadedBytes, totalBytes));
        }
    }
}
