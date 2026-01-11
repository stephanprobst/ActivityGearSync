namespace ActivityGearSync.Shared;

public static class FileHelpers
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static string SanitizeFileName(string fileName)
    {
        string sanitized = fileName;
        foreach (char c in InvalidFileNameChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        sanitized = sanitized.Replace(':', '-').Replace('/', '-').Replace('\\', '-');
        sanitized = sanitized.Trim();

        if (sanitized.Length > 100)
        {
            sanitized = sanitized[..100];
        }

        return sanitized;
    }

    public static string GenerateExportFileName(DateTime activityDate, long activityId, string activityName, string extension)
    {
        string dateStr = activityDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        string safeName = SanitizeFileName(activityName);
        return $"{dateStr}_{activityId}_{safeName}.{extension}";
    }

    public static void EnsureExportDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
