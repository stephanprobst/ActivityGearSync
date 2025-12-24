using Spectre.Console;
using Strava.Console.Infrastructure;
using Strava.Console.Models;
using Strava.Console.Services;

namespace Strava.Console.Commands;

public sealed class UpdateGearCommand(StravaApiClient apiClient, RateLimiter rateLimiter)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
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
            AnsiConsole.MarkupLine($"[red]Failed to fetch athlete data: {ex.Message}[/]");
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

        string activityType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select [green]activity type[/]:")
                .AddChoices("All Types", "Run", "Ride", "Walk", "Hike", "Swim", "Other"));

        string dateRange = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select [green]date range[/]:")
                .AddChoices("Last 7 days", "Last 30 days", "Last 90 days", "This year", "All time"));

        string gearFilter = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Filter by [green]current gear[/]:")
                .AddChoices(["All activities", "No gear assigned", .. allGear.Select(g => g.Name)]));

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
                (string.Equals(activityType, "All Types", StringComparison.OrdinalIgnoreCase)
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

        var selectedActivities = AnsiConsole.Prompt(
            new MultiSelectionPrompt<StravaActivity>()
                .Title("Select activities to update:")
                .PageSize(15)
                .MoreChoicesText("[grey](Move up and down to see more activities)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]")
                .UseConverter(a => $"{a.StartDateLocal:MMM dd} - {a.Name} ({a.FormattedDistance})")
                .AddChoices(filtered));

        if (selectedActivities.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities selected.[/]");
            WaitForKey();
            return;
        }

        // Step 5: Select target gear
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 3:[/] Choose New Gear");
        AnsiConsole.WriteLine();

        const string removeGearChoice = "[Remove gear]";
        List<string> gearChoices = [removeGearChoice, .. allGear.Select(g => $"{g.Name} ({g.FormattedDistance})")];

        string selectedGearName = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select [green]gear to assign[/]:")
                .AddChoices(gearChoices));

        var targetGear = string.Equals(selectedGearName, removeGearChoice, StringComparison.OrdinalIgnoreCase) ? null
            : allGear.FirstOrDefault(g => selectedGearName.StartsWith(g.Name));

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
        confirmTable.AddRow("Rate limit remaining", $"{rateLimiter.RemainingRequests}/100");

        AnsiConsole.Write(confirmTable);
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("Proceed with update?"))
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
                    if (remaining < 10)
                    {
                        task.Description = $"[yellow]Rate limit low ({remaining}/100). Slowing down...[/]";
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
            foreach ((var activity, string error) in failedActivities.Take(5))
            {
                AnsiConsole.MarkupLine($"  [grey]- {activity.Name}: {error}[/]");
            }

            if (failedActivities.Count > 5)
            {
                AnsiConsole.MarkupLine($"  [grey]... and {failedActivities.Count - 5} more[/]");
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

        foreach (var activity in activities.Take(10))
        {
            string gearName = activity.GearId is null
                ? "[grey]None[/]"
                : allGear.FirstOrDefault(g => string.Equals(g.Id, activity.GearId, StringComparison.OrdinalIgnoreCase))?.Name ?? "[grey]Unknown[/]";

            table.AddRow(
                activity.StartDateLocal.ToString("MMM dd"),
                activity.Name.Length > 25 ? activity.Name[..22] + "..." : activity.Name,
                activity.Type,
                activity.FormattedDistance,
                gearName);
        }

        if (activities.Count > 10)
        {
            table.AddRow("[grey]...[/]", $"[grey]and {activities.Count - 10} more[/]", "", "", "");
        }

        AnsiConsole.Write(table);
    }

    private static (DateTime? after, DateTime? before) CalculateDateRange(string dateRange)
    {
        var now = DateTime.Now;
        return dateRange switch
        {
            "Last 7 days" => (now.AddDays(-7), null),
            "Last 30 days" => (now.AddDays(-30), null),
            "Last 90 days" => (now.AddDays(-90), null),
            "This year" => (new DateTime(now.Year, 1, 1), null),
            _ => (null, null)
        };
    }

    private static bool MatchesActivityType(StravaActivity activity, string filter)
    {
        return filter switch
        {
            "Run" => activity.Type is "Run" or "TrailRun" or "VirtualRun",
            "Ride" => activity.Type is "Ride" or "MountainBikeRide" or "GravelRide" or "EBikeRide" or "VirtualRide",
            "Walk" => activity.Type is "Walk",
            "Hike" => activity.Type is "Hike",
            "Swim" => activity.Type is "Swim" or "OpenWaterSwim",
            "Other" => activity.Type is not ("Run" or "TrailRun" or "VirtualRun" or "Ride" or "MountainBikeRide" or "GravelRide" or "EBikeRide" or "VirtualRide" or "Walk" or "Hike" or "Swim" or "OpenWaterSwim"),
            _ => true
        };
    }

    private static bool MatchesGearFilter(StravaActivity activity, string filter, List<StravaGear> allGear)
    {
        if (string.Equals(filter, "All activities", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(filter, "No gear assigned", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrEmpty(activity.GearId);
        }

        var gear = allGear.FirstOrDefault(g => string.Equals(g.Name, filter, StringComparison.OrdinalIgnoreCase));
        return gear != null && string.Equals(activity.GearId, gear.Id, StringComparison.OrdinalIgnoreCase);
    }

    private static void WaitForKey()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Press any key to continue...");
        System.Console.ReadKey(intercept: true);
    }
}
