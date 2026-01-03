using Spectre.Console;

namespace ActivityGearSync.Shared;

public static class ConsoleHelpers
{
    public static void WaitForKey()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(intercept: true);
    }

    public static string FormatWaitTime(TimeSpan waitTime)
    {
        if (waitTime.TotalHours >= 1)
        {
            return $"{waitTime.Hours}h {waitTime.Minutes}m";
        }

        if (waitTime.TotalMinutes >= 1)
        {
            return $"{waitTime.Minutes}m {waitTime.Seconds}s";
        }

        return $"{waitTime.Seconds}s";
    }
}
