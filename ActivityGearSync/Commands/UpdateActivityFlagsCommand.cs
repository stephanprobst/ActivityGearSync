using System.Globalization;
using ActivityGearSync.Interfaces;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using Spectre.Console;

namespace ActivityGearSync.Commands;

public sealed class UpdateActivityFlagsCommand(IStravaApiClient apiClient, RateLimiter rateLimiter)
{
    private static class FlagTypes
    {
        public const string Commute = "Commute";
        public const string Trainer = "Trainer";
        public const string Privacy = "Privacy (hide from home)";

        public static readonly string[] All = [Commute, Trainer, Privacy];
    }

    private static class FlagValueFilters
    {
        public const string ShowAll = "Show all";
        public const string OnlyTrue = "Only where flag is true";
        public const string OnlyFalse = "Only where flag is false";

        public static readonly string[] All = [ShowAll, OnlyTrue, OnlyFalse];
    }

    private static class TargetValues
    {
        public const string SetTrue = "Set to true";
        public const string SetFalse = "Set to false";

        public static readonly string[] All = [SetTrue, SetFalse];
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Update Activity Flags[/]");
        AnsiConsole.WriteLine();

        // Step 1: Select flag type
        AnsiConsole.MarkupLine("[bold yellow]Step 1:[/] Select Flag to Update");
        AnsiConsole.WriteLine();

        string flagType = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Which flag do you want to update?")
                .AddChoices(FlagTypes.All), cancellationToken);

        // Step 2: Get filter options
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 2:[/] Filter Activities");
        AnsiConsole.WriteLine();

        string activityType = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]activity type[/]:")
                .AddChoices(ActivityTypes.All), cancellationToken);

        string dateRange = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]date range[/]:")
                .AddChoices(DateRanges.All), cancellationToken);

        string flagValueFilter = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title($"Filter by current [green]{GetFlagDisplayName(flagType)}[/] value:")
                .AddChoices(FlagValueFilters.All), cancellationToken);

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
                        task.Description = $"[green]Fetched {p.fetched} activities...[/]"),
                    after, before, cancellationToken);

                activities.AddRange(result);
                task.IsIndeterminate = false;
                task.Value = 100;
            });

        // Apply filters
        var filtered = activities.Where(a =>
                (string.Equals(activityType, ActivityTypes.AllTypes, StringComparison.OrdinalIgnoreCase)
                 || ActivityTypes.Matches(a, activityType))
                && MatchesFlagValueFilter(a, flagType, flagValueFilter))
            .ToList();

        if (filtered.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities match your filters.[/]");
            ConsoleHelpers.WaitForKey();
            return;
        }

        // Step 4: Display activities and select
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold yellow]Step 3:[/] Select Activities ({filtered.Count} found)");
        AnsiConsole.WriteLine();

        DisplayActivitiesTable(filtered, flagType);

        var selectedActivities = await ConsoleHelpers.PromptActivitySelectionAsync(
            filtered,
            a => $"{a.StartDateLocal:MMM dd} - {Markup.Escape(a.Name)} ({a.FormattedDistance})",
            cancellationToken);

        if (selectedActivities is null or { Count: 0 })
        {
            if (selectedActivities is { Count: 0 })
            {
                AnsiConsole.MarkupLine("[yellow]No activities selected.[/]");
                ConsoleHelpers.WaitForKey();
            }

            return;
        }

        // Step 5: Select target value
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold yellow]Step 4:[/] Set {GetFlagDisplayName(flagType)} Value");
        AnsiConsole.WriteLine();

        string targetValueChoice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title($"Set [green]{GetFlagDisplayName(flagType)}[/] to:")
                .AddChoices(TargetValues.All), cancellationToken);

        bool targetValue = string.Equals(targetValueChoice, TargetValues.SetTrue, StringComparison.OrdinalIgnoreCase);

        // Step 6: Confirm
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 5:[/] Confirm Update");
        AnsiConsole.WriteLine();

        var confirmTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        var (shortTermRemaining, dailyRemaining) = rateLimiter.GetRemainingRequests();

        confirmTable.AddRow("Activities to update", $"[bold]{selectedActivities.Count}[/]");
        confirmTable.AddRow("Flag", GetFlagDisplayName(flagType));
        confirmTable.AddRow("New value", targetValue ? "[green]true[/]" : "[grey]false[/]");
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

                    UpdateProgressWithRateLimitInfo(task, selectedActivities.Count);

                    try
                    {
                        var flagsRequest = CreateFlagsRequest(flagType, targetValue);
                        await apiClient.UpdateActivityFlagsAsync(activity.Id, flagsRequest, cancellationToken);
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

    private static string GetFlagDisplayName(string flagType)
    {
        return flagType switch
        {
            FlagTypes.Commute => "Commute",
            FlagTypes.Trainer => "Trainer",
            FlagTypes.Privacy => "Privacy",
            _ => flagType
        };
    }

    private static bool GetFlagValue(StravaActivity activity, string flagType)
    {
        return flagType switch
        {
            FlagTypes.Commute => activity.Commute,
            FlagTypes.Trainer => activity.Trainer,
            FlagTypes.Privacy => activity.Private,
            _ => false
        };
    }

    private static ActivityFlagsUpdateRequest CreateFlagsRequest(string flagType, bool value)
    {
        return flagType switch
        {
            FlagTypes.Commute => new ActivityFlagsUpdateRequest { Commute = value },
            FlagTypes.Trainer => new ActivityFlagsUpdateRequest { Trainer = value },
            FlagTypes.Privacy => new ActivityFlagsUpdateRequest { HideFromHome = value },
            _ => throw new ArgumentException($"Unknown flag type: {flagType}", nameof(flagType))
        };
    }

    private static bool MatchesFlagValueFilter(StravaActivity activity, string flagType, string filter)
    {
        if (string.Equals(filter, FlagValueFilters.ShowAll, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        bool currentValue = GetFlagValue(activity, flagType);

        return filter switch
        {
            FlagValueFilters.OnlyTrue => currentValue,
            FlagValueFilters.OnlyFalse => !currentValue,
            _ => true
        };
    }

    private static void DisplayActivitiesTable(List<StravaActivity> activities, string flagType)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Date")
            .AddColumn("Name")
            .AddColumn("Type")
            .AddColumn("Distance")
            .AddColumn(GetFlagDisplayName(flagType));

        foreach (var activity in activities.Take(DisplayLimits.ActivitiesTablePreviewCount))
        {
            bool flagValue = GetFlagValue(activity, flagType);
            string flagDisplay = flagValue ? "[green]true[/]" : "[grey]false[/]";

            string truncatedName = activity.Name.Length > DisplayLimits.ActivityNameMaxLength
                ? Markup.Escape(activity.Name[..DisplayLimits.ActivityNameTruncatedLength]) + "..."
                : Markup.Escape(activity.Name);

            table.AddRow(
                activity.StartDateLocal.ToString("MMM dd", CultureInfo.InvariantCulture),
                truncatedName,
                activity.Type,
                activity.FormattedDistance,
                flagDisplay);
        }

        if (activities.Count > DisplayLimits.ActivitiesTablePreviewCount)
        {
            table.AddRow("[grey]...[/]", $"[grey]and {activities.Count - DisplayLimits.ActivitiesTablePreviewCount} more[/]", "", "", "");
        }

        AnsiConsole.Write(table);
    }

    private void UpdateProgressWithRateLimitInfo(ProgressTask task, int totalActivities)
    {
        var (shortTerm, daily) = rateLimiter.GetRemainingRequests();
        var (waitTime, waitReason) = rateLimiter.GetEstimatedWaitTime();

        if (waitTime.HasValue && waitReason is not null)
        {
            string waitTimeFormatted = ConsoleHelpers.FormatWaitTime(waitTime.Value);
            task.Description = $"[yellow]Waiting {waitTimeFormatted} ({waitReason})[/] [grey]│[/] [dim]15m: {shortTerm}/{RateLimitThresholds.MaxRequestsPer15Min}, daily: {daily}/{RateLimitThresholds.MaxRequestsPerDay}[/]";
        }
        else if (shortTerm < RateLimitThresholds.LowShortTermWarning || daily < RateLimitThresholds.LowDailyWarning)
        {
            string limitColor = shortTerm < RateLimitThresholds.LowShortTermWarning ? "yellow" : "green";
            string dailyColor = daily < RateLimitThresholds.LowDailyWarning ? "yellow" : "green";
            task.Description = $"[green]Updating {totalActivities} activities[/] [grey]│[/] [{limitColor}]15m: {shortTerm}/{RateLimitThresholds.MaxRequestsPer15Min}[/], [{dailyColor}]daily: {daily}/{RateLimitThresholds.MaxRequestsPerDay}[/]";
        }
        else
        {
            task.Description = $"[green]Updating {totalActivities} activities[/] [grey]│[/] [dim]15m: {shortTerm}/{RateLimitThresholds.MaxRequestsPer15Min}, daily: {daily}/{RateLimitThresholds.MaxRequestsPerDay}[/]";
        }
    }
}
