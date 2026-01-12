using System.Diagnostics;
using ActivityGearSync.Interfaces;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using Spectre.Console;

namespace ActivityGearSync.Commands;

public sealed class UpdateCommand(IGitHubReleaseClient releaseClient)
{
    private static class Messages
    {
        public const string Title = "Check for Updates";
        public const string Checking = "Checking for updates...";
        public const string NoReleases = "No releases available yet.";
        public const string UpToDate = "You're already running the latest version.";
        public const string DevBuild = "You're running a dev build.";
        public const string UpdateAvailable = "A new version is available!";
        public const string Downloading = "Downloading update...";
        public const string Applying = "Applying update...";
        public const string RestartRequired = "Update complete! Please restart the application.";
        public const string PressAnyKey = "Press any key to continue...";
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[bold]{Messages.Title}[/]");
        AnsiConsole.WriteLine();

        const string currentVersion = AppVersion.Version;
        AnsiConsole.MarkupLine($"Current version: [cyan]{Markup.Escape(currentVersion)}[/]");
        AnsiConsole.WriteLine();

        GitHubRelease? release;

        try
        {
            release = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(Messages.Checking, async _ =>
                    await releaseClient.GetLatestReleaseAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to check for updates: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
            return;
        }

        if (release is null)
        {
            AnsiConsole.MarkupLine($"[yellow]{Messages.NoReleases}[/]");
            WaitForKey();
            return;
        }

        string latestVersion = release.TagName.TrimStart('v');
        bool isDevBuild = VersionLogic.IsDevBuild(currentVersion);
        bool updateAvailable = isDevBuild || VersionLogic.IsNewerVersion(latestVersion, currentVersion);

        if (!updateAvailable)
        {
            AnsiConsole.MarkupLine($"[green]{Messages.UpToDate}[/]");
            WaitForKey();
            return;
        }

        // Show update info
        AnsiConsole.MarkupLine(isDevBuild
            ? $"[yellow]{Messages.DevBuild}[/]"
            : $"[green]{Messages.UpdateAvailable}[/]");

        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Version")
            .AddColumn("Value");

        table.AddRow("Current", Markup.Escape(currentVersion));
        table.AddRow("Latest", $"[green]{Markup.Escape(latestVersion)}[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Find asset for current platform
        string rid = VersionLogic.GetRuntimeIdentifier();
        var asset = release.Assets.FirstOrDefault(a =>
            a.Name.Contains(rid, StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            AnsiConsole.MarkupLine($"[red]No release available for your platform ({rid}).[/]");
            WaitForKey();
            return;
        }

        AnsiConsole.MarkupLine($"Download size: [cyan]{VersionLogic.FormatFileSize(asset.Size)}[/]");
        AnsiConsole.WriteLine();

        if (!await AnsiConsole.ConfirmAsync("Do you want to download and install this update?",
            defaultValue: true, cancellationToken: cancellationToken))
        {
            AnsiConsole.MarkupLine("[yellow]Update cancelled.[/]");
            WaitForKey();
            return;
        }

        AnsiConsole.WriteLine();

        // Download the update
        string? tempPath = null;

        try
        {
            tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            await AnsiConsole.Progress()
                .AutoClear(enabled: false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new TransferSpeedColumn(),
                    new RemainingTimeColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask(Messages.Downloading, maxValue: asset.Size);

                    await releaseClient.DownloadAssetAsync(
                        asset.BrowserDownloadUrl,
                        tempPath,
                        new Progress<(long downloaded, long total)>(p => task.Value = p.downloaded),
                        cancellationToken);

                    task.Value = asset.Size;
                });

            AnsiConsole.MarkupLine("[green]Download complete.[/]");
            AnsiConsole.WriteLine();

            // Apply the update
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(Messages.Applying, async _ => await ApplyUpdateAsync(tempPath, cancellationToken));

            AnsiConsole.MarkupLine($"[green]{Messages.RestartRequired}[/]");
            AnsiConsole.WriteLine();

            if (await AnsiConsole.ConfirmAsync("Close the application now?", defaultValue: true, cancellationToken: cancellationToken))
            {
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Update failed: {Markup.Escape(ex.Message)}[/]");

            // Clean up temp file on error
            if (tempPath is not null && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            WaitForKey();
        }
    }

    private static async Task ApplyUpdateAsync(string downloadedPath, CancellationToken cancellationToken)
    {
        string? currentExePath = Environment.ProcessPath;

        if (string.IsNullOrEmpty(currentExePath))
        {
            throw new InvalidOperationException("Could not determine current executable path.");
        }

        if (OperatingSystem.IsWindows())
        {
            await ApplyWindowsUpdateAsync(downloadedPath, currentExePath, cancellationToken);
        }
        else
        {
            await ApplyUnixUpdateAsync(downloadedPath, currentExePath, cancellationToken);
        }
    }

    private static Task ApplyWindowsUpdateAsync(string downloadedPath, string currentExePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string oldPath = currentExePath + ".old";

        // Remove any existing .old file
        if (File.Exists(oldPath))
        {
            File.Delete(oldPath);
        }

        // Rename current exe to .old
        File.Move(currentExePath, oldPath);

        // Move downloaded file to current exe path
        File.Move(downloadedPath, currentExePath);

        return Task.CompletedTask;
    }

    private static async Task ApplyUnixUpdateAsync(string downloadedPath, string currentExePath, CancellationToken cancellationToken)
    {
        // Replace the current executable
        File.Move(downloadedPath, currentExePath, overwrite: true);

        // Make it executable
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"+x \"{currentExePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is not null)
        {
            await process.WaitForExitAsync(cancellationToken);
        }
    }

    private static void WaitForKey()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(Messages.PressAnyKey);
        Console.ReadKey(intercept: true);
    }
}
