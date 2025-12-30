using System.Globalization;
using Spectre.Console;
using ActivityGearSync.Infrastructure;
using ActivityGearSync.Models;
using ActivityGearSync.Services;

namespace ActivityGearSync.Commands;

public sealed class UpdateGearCommand(StravaApiClient apiClient, RateLimiter rateLimiter)
{
    // Activity type filter options
    private static class ActivityTypes
    {
        public const string AllTypes = "All Types";
        public const string Run = "Run";
        public const string Ride = "Ride";
        public const string Walk = "Walk";
        public const string Hike = "Hike";
        public const string Swim = "Swim";
        public const string Other = "Other";

        public static readonly string[] All = [AllTypes, Run, Ride, Walk, Hike, Swim, Other];
    }

    // Date range filter options
    private static class DateRanges
    {
        public const string Last7Days = "Last 7 days";
        public const string Last30Days = "Last 30 days";
        public const string Last90Days = "Last 90 days";
        public const string ThisYear = "This year";
        public const string AllTime = "All time";

        public static readonly string[] All = [Last7Days, Last30Days, Last90Days, ThisYear, AllTime];

        public const int Days7 = 7;
        public const int Days30 = 30;
        public const int Days90 = 90;
    }

    // Gear filter options
    private static class GearFilters
    {
        public const string AllActivities = "All activities";
        public const string NoGearAssigned = "No gear assigned";
        public const string RemoveGear = "(Remove gear)";
    }

    // UI and display constants
    private static class DisplayLimits
    {
        public const int SelectionPageSize = 15;
        public const int ActivitiesTablePreviewCount = 10;
        public const int FailedActivitiesPreviewCount = 5;
        public const int ActivityNameMaxLength = 25;
        public const int ActivityNameTruncatedLength = 22;
    }

    // Selection mode options
    private static class SelectionModes
    {
        public const string SelectAll = "Select all";
        public const string SelectIndividually = "Select individually...";
        public const string Cancel = "Unselect all & Cancel";
    }

    // Rate limiting constants
    private static class RateLimitThresholds
    {
        public const int LowRemainingWarning = 10;
        public const int MaxRequestsPer15Min = 100;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Update Gear on Activities[/]");
        AnsiConsole.WriteLine();

        // Step 1: Fetch athlete to get gear list
        StravaAthlete athlete;
        try
        {
            athlete = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Fetching your gear...", async _ =>
                    await apiClient.GetAthleteAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to fetch athlete data: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
            return;
        }

        var allGear = athlete.AllGear.ToList();
        if (allGear.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]You don't have any gear configured in Strava.[/]");
            AnsiConsole.MarkupLine("Please add gear in Strava settings first.");
            WaitForKey();
            return;
        }

        // Step 2: Get filter options
        AnsiConsole.MarkupLine("[bold yellow]Step 1:[/] Filter Activities");
        AnsiConsole.WriteLine();

        string activityType = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]activity type[/]:")
                .AddChoices(ActivityTypes.All), cancellationToken);

        string dateRange = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]date range[/]:")
                .AddChoices(DateRanges.All), cancellationToken);

        string gearFilter = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Filter by [green]current gear[/]:")
                .AddChoices([GearFilters.AllActivities, GearFilters.NoGearAssigned, .. allGear.Select(g => Markup.Escape(g.Name))]), cancellationToken);

        // Calculate date filter
        var (after, before) = CalculateDateRange(dateRange);

        // Step 3: Fetch activities
        AnsiConsole.WriteLine();
        List<StravaActivity> activities = [];

        await AnsiConsole.Progress()
            .AutoClear(enabled: false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Fetching activities...[/]");
                task.IsIndeterminate = true;

                var result = await apiClient.GetAllActivitiesAsync(
                    new Progress<(int fetched, int total)>(p =>
                    {
                        task.Description = $"[green]Fetched {p.fetched} activities...[/]";
                    }),
                    after, before, cancellationToken);

                activities.AddRange(result);
                task.IsIndeterminate = false;
                task.Value = 100;
            });

        // Apply filters
        var filtered = activities.Where(a =>
                (string.Equals(activityType, ActivityTypes.AllTypes, StringComparison.OrdinalIgnoreCase)
                 || MatchesActivityType(a, activityType))
                && MatchesGearFilter(a, gearFilter, allGear))
            .ToList();

        if (filtered.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities match your filters.[/]");
            WaitForKey();
            return;
        }

        // Step 4: Display activities and select
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold yellow]Step 2:[/] Select Activities ({filtered.Count} found)");
        AnsiConsole.WriteLine();

        DisplayActivitiesTable(filtered, allGear);

        // Selection mode menu
        string selectionMode = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("How would you like to select activities?")
                .AddChoices(
                    $"{SelectionModes.SelectAll} ({filtered.Count} activities)",
                    SelectionModes.SelectIndividually,
                    SelectionModes.Cancel), cancellationToken);

        List<StravaActivity> selectedActivities;

        if (selectionMode.StartsWith(SelectionModes.SelectAll, StringComparison.OrdinalIgnoreCase))
        {
            selectedActivities = filtered;
            AnsiConsole.MarkupLine($"[green]Selected all {filtered.Count} activities.[/]");
        }
        else if (string.Equals(selectionMode, SelectionModes.Cancel, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]Selection cancelled.[/]");
            WaitForKey();
            return;
        }
        else
        {
            selectedActivities = await AnsiConsole.PromptAsync(
                new MultiSelectionPrompt<StravaActivity>()
                    .Title("Select activities to update:")
                    .PageSize(DisplayLimits.SelectionPageSize)
                    .MoreChoicesText("[grey](Move up and down to see more activities)[/]")
                    .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]")
                    .UseConverter(a => $"{a.StartDateLocal:MMM dd} - {Markup.Escape(a.Name)} ({a.FormattedDistance})")
                    .AddChoices(filtered), cancellationToken);

            if (selectedActivities.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No activities selected.[/]");
                WaitForKey();
                return;
            }
        }

        // Step 5: Select target gear
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 3:[/] Choose New Gear");
        AnsiConsole.WriteLine();

        List<string> gearChoices = [GearFilters.RemoveGear, .. allGear.Select(g => $"{Markup.Escape(g.Name)} ({g.FormattedDistance})")];

        string selectedGearName = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]gear to assign[/]:")
                .AddChoices(gearChoices), cancellationToken);

        var targetGear = string.Equals(selectedGearName, GearFilters.RemoveGear, StringComparison.OrdinalIgnoreCase) ? null
            : allGear.FirstOrDefault(g => selectedGearName.StartsWith(Markup.Escape(g.Name), StringComparison.OrdinalIgnoreCase));

        // Step 6: Confirm
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 4:[/] Confirm Update");
        AnsiConsole.WriteLine();

        var confirmTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        confirmTable.AddRow("Activities to update", $"[bold]{selectedActivities.Count}[/]");
        confirmTable.AddRow("New gear", targetGear?.Name ?? "[grey]None (remove gear)[/]");
        confirmTable.AddRow("Rate limit remaining", $"{rateLimiter.RemainingRequests}/{RateLimitThresholds.MaxRequestsPer15Min}");

        AnsiConsole.Write(confirmTable);
        AnsiConsole.WriteLine();

        if (!await AnsiConsole.ConfirmAsync("Proceed with update?", cancellationToken: cancellationToken))
        {
            AnsiConsole.MarkupLine("[yellow]Update cancelled.[/]");
            WaitForKey();
            return;
        }

        // Step 7: Execute updates
        AnsiConsole.WriteLine();
        int successCount = 0;
        List<(StravaActivity Activity, string Error)> failedActivities = [];

        await AnsiConsole.Progress()
            .AutoClear(enabled: false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[green]Updating {selectedActivities.Count} activities[/]");
                task.MaxValue = selectedActivities.Count;

                foreach (var activity in selectedActivities)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        await apiClient.UpdateActivityGearAsync(activity.Id, targetGear?.Id, cancellationToken);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failedActivities.Add((activity, ex.Message));
                    }

                    task.Increment(1);

                    int remaining = rateLimiter.RemainingRequests;
                    if (remaining < RateLimitThresholds.LowRemainingWarning)
                    {
                        task.Description = $"[yellow]Rate limit low ({remaining}/{RateLimitThresholds.MaxRequestsPer15Min}). Slowing down...[/]";
                    }
                }
            });

        // Step 8: Summary
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Update Summary[/]");
        AnsiConsole.WriteLine();

        if (successCount > 0)
        {
            AnsiConsole.MarkupLine($"[green]Successfully updated {successCount} activities.[/]");
        }

        if (failedActivities.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]Failed to update {failedActivities.Count} activities:[/]");
            foreach ((var activity, string error) in failedActivities.Take(DisplayLimits.FailedActivitiesPreviewCount))
            {
                AnsiConsole.MarkupLine($"  [grey]- {Markup.Escape(activity.Name)}: {Markup.Escape(error)}[/]");
            }

            if (failedActivities.Count > DisplayLimits.FailedActivitiesPreviewCount)
            {
                AnsiConsole.MarkupLine($"  [grey]... and {failedActivities.Count - DisplayLimits.FailedActivitiesPreviewCount} more[/]");
            }
        }

        WaitForKey();
    }

    private static void DisplayActivitiesTable(List<StravaActivity> activities, List<StravaGear> allGear)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Date")
            .AddColumn("Name")
            .AddColumn("Type")
            .AddColumn("Distance")
            .AddColumn("Current Gear");

        foreach (var activity in activities.Take(DisplayLimits.ActivitiesTablePreviewCount))
        {
            string gearName = activity.GearId switch
            {
                null => "[grey]None[/]",
                _ => allGear.FirstOrDefault(g => string.Equals(g.Id, activity.GearId, StringComparison.OrdinalIgnoreCase))?.Name is { } name
                    ? Markup.Escape(name)
                    : "[grey]Unknown[/]"
            };

            string truncatedName = activity.Name.Length > DisplayLimits.ActivityNameMaxLength
                ? Markup.Escape(activity.Name[..DisplayLimits.ActivityNameTruncatedLength]) + "..."
                : Markup.Escape(activity.Name);

            table.AddRow(
                activity.StartDateLocal.ToString("MMM dd", CultureInfo.InvariantCulture),
                truncatedName,
                activity.Type,
                activity.FormattedDistance,
                gearName);
        }

        if (activities.Count > DisplayLimits.ActivitiesTablePreviewCount)
        {
            table.AddRow("[grey]...[/]", $"[grey]and {activities.Count - DisplayLimits.ActivitiesTablePreviewCount} more[/]", "", "", "");
        }

        AnsiConsole.Write(table);
    }

    private static (DateTime? after, DateTime? before) CalculateDateRange(string dateRange)
    {
        var now = DateTime.Now;
        return dateRange switch
        {
            DateRanges.Last7Days => (now.AddDays(-DateRanges.Days7), null),
            DateRanges.Last30Days => (now.AddDays(-DateRanges.Days30), null),
            DateRanges.Last90Days => (now.AddDays(-DateRanges.Days90), null),
            DateRanges.ThisYear => (new DateTime(now.Year, 1, 1), null),
            _ => (null, null)
        };
    }

    private static bool MatchesActivityType(StravaActivity activity, string filter)
    {
        return filter switch
        {
            ActivityTypes.Run => activity.Type is "Run" or "TrailRun" or "VirtualRun",
            ActivityTypes.Ride => activity.Type is "Ride" or "MountainBikeRide" or "GravelRide" or "EBikeRide" or "VirtualRide",
            ActivityTypes.Walk => activity.Type is "Walk",
            ActivityTypes.Hike => activity.Type is "Hike",
            ActivityTypes.Swim => activity.Type is "Swim" or "OpenWaterSwim",
            ActivityTypes.Other => activity.Type is not ("Run" or "TrailRun" or "VirtualRun" or "Ride" or "MountainBikeRide" or "GravelRide" or "EBikeRide" or "VirtualRide" or "Walk" or "Hike" or "Swim" or "OpenWaterSwim"),
            _ => true
        };
    }

    private static bool MatchesGearFilter(StravaActivity activity, string filter, List<StravaGear> allGear)
    {
        if (string.Equals(filter, GearFilters.AllActivities, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(filter, GearFilters.NoGearAssigned, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrEmpty(activity.GearId);
        }

        var gear = allGear.FirstOrDefault(g => string.Equals(Markup.Escape(g.Name), filter, StringComparison.OrdinalIgnoreCase));
        return gear != null && string.Equals(activity.GearId, gear.Id, StringComparison.OrdinalIgnoreCase);
    }

    private static void WaitForKey()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Press any key to continue...");
        System.Console.ReadKey(intercept: true);
    }
}
