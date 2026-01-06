using ActivityGearSync.Models;
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

    public static async Task<List<StravaActivity>?> PromptActivitySelectionAsync(
        List<StravaActivity> filtered,
        Func<StravaActivity, string> displayConverter,
        CancellationToken cancellationToken)
    {
        string selectionMode = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("How would you like to select activities?")
                .AddChoices(
                    $"{SelectionModes.SelectAll} ({filtered.Count} activities)",
                    SelectionModes.SelectIndividually,
                    SelectionModes.Cancel), cancellationToken);

        if (string.Equals(selectionMode, SelectionModes.Cancel, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]Selection cancelled.[/]");
            WaitForKey();
            return null;
        }

        List<StravaActivity> selectedActivities;

        if (selectionMode.StartsWith(SelectionModes.SelectAll, StringComparison.OrdinalIgnoreCase))
        {
            selectedActivities = filtered;
            AnsiConsole.MarkupLine($"[green]Selected all {filtered.Count} activities.[/]");
        }
        else
        {
            selectedActivities = await AnsiConsole.PromptAsync(
                new MultiSelectionPrompt<StravaActivity>()
                    .Title("Select activities to update:")
                    .PageSize(DisplayLimits.SelectionPageSize)
                    .MoreChoicesText("[grey](Move up and down to see more activities)[/]")
                    .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]")
                    .UseConverter(displayConverter)
                    .AddChoices(filtered), cancellationToken);
        }

        return selectedActivities;
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
