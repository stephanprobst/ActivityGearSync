using System.Globalization;
using ActivityGearSync.Clients;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using Spectre.Console;

namespace ActivityGearSync.Commands;

public sealed class UpdateGearCommand(StravaApiClient apiClient, RateLimiter rateLimiter)
{
    private static class GearFilters
    {
        public const string AllActivities = "All activities";
        public const string NoGearAssigned = "No gear assigned";
        public const string RemoveGear = "(Remove gear)";
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
            ConsoleHelpers.WaitForKey();
            return;
        }

        var allGear = athlete.AllGear.ToList();
        if (allGear.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]You don't have any gear configured in Strava.[/]");
            AnsiConsole.MarkupLine("Please add gear in Strava settings first.");
            ConsoleHelpers.WaitForKey();
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
        var (after, before) = DateRanges.Calculate(dateRange);

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
                 || ActivityTypes.Matches(a, activityType))
                && MatchesGearFilter(a, gearFilter, allGear))
            .ToList();

        if (filtered.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities match your filters.[/]");
            ConsoleHelpers.WaitForKey();
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
            ConsoleHelpers.WaitForKey();
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
                ConsoleHelpers.WaitForKey();
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

        var (shortTermRemaining, dailyRemaining) = rateLimiter.GetRemainingRequests();

        confirmTable.AddRow("Activities to update", $"[bold]{selectedActivities.Count}[/]");
        confirmTable.AddRow("New gear", targetGear?.Name ?? "[grey]None (remove gear)[/]");
        confirmTable.AddRow("Rate limit (15-min)", $"{shortTermRemaining}/{RateLimitThresholds.MaxRequestsPer15Min}");
        confirmTable.AddRow("Rate limit (daily)", $"{dailyRemaining}/{RateLimitThresholds.MaxRequestsPerDay}");

        AnsiConsole.Write(confirmTable);
        AnsiConsole.WriteLine();

        if (!await AnsiConsole.ConfirmAsync("Proceed with update?", cancellationToken: cancellationToken))
        {
            AnsiConsole.MarkupLine("[yellow]Update cancelled.[/]");
            ConsoleHelpers.WaitForKey();
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
                new ElapsedTimeColumn(),
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

                    // Update progress description with rate limit info before the request
                    UpdateProgressWithRateLimitInfo(task, selectedActivities.Count);

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

        ConsoleHelpers.WaitForKey();
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

    private void UpdateProgressWithRateLimitInfo(ProgressTask task, int totalActivities)
    {
        var (shortTerm, daily) = rateLimiter.GetRemainingRequests();
        var (waitTime, waitReason) = rateLimiter.GetEstimatedWaitTime();

        // If we need to wait, show the wait time
        if (waitTime.HasValue && waitReason is not null)
        {
            string waitTimeFormatted = ConsoleHelpers.FormatWaitTime(waitTime.Value);
            task.Description = $"[yellow]Waiting {waitTimeFormatted} ({waitReason})[/] [grey]│[/] [dim]15m: {shortTerm}/{RateLimitThresholds.MaxRequestsPer15Min}, daily: {daily}/{RateLimitThresholds.MaxRequestsPerDay}[/]";
        }
        // If limits are low, show warning
        else if (shortTerm < RateLimitThresholds.LowShortTermWarning || daily < RateLimitThresholds.LowDailyWarning)
        {
            string limitColor = shortTerm < RateLimitThresholds.LowShortTermWarning ? "yellow" : "green";
            string dailyColor = daily < RateLimitThresholds.LowDailyWarning ? "yellow" : "green";
            task.Description = $"[green]Updating {totalActivities} activities[/] [grey]│[/] [{limitColor}]15m: {shortTerm}/{RateLimitThresholds.MaxRequestsPer15Min}[/], [{dailyColor}]daily: {daily}/{RateLimitThresholds.MaxRequestsPerDay}[/]";
        }
        // Normal operation - show rate limit info
        else
        {
            task.Description = $"[green]Updating {totalActivities} activities[/] [grey]│[/] [dim]15m: {shortTerm}/{RateLimitThresholds.MaxRequestsPer15Min}, daily: {daily}/{RateLimitThresholds.MaxRequestsPerDay}[/]";
        }
    }
}
