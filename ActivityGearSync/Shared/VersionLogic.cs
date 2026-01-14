using System.Globalization;

namespace ActivityGearSync.Shared;

public static class VersionLogic
{
    public static bool IsDevBuild(string version)
    {
        return !Version.TryParse(version, out _);
    }

    public static bool IsNewerVersion(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVer) &&
            Version.TryParse(current, out var currentVer))
        {
            return latestVer > currentVer;
        }

        return !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetRuntimeIdentifier()
    {
        return (OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()) switch
        {
            (true, _) => "win-x64",
            (_, true) => "osx-arm64",
            _ => "linux-x64"
        };
    }

    public static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int order = 0;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", size, sizes[order]);
    }
}
