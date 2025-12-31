using System.Globalization;
using Spectre.Console;
using ActivityGearSync.Infrastructure;
using ActivityGearSync.Models;
using ActivityGearSync.Services;

namespace ActivityGearSync.Commands;

public sealed class UpdateSportTypeCommand(StravaApiClient apiClient, RateLimiter rateLimiter)
{
    // Activity type filter options (high-level categories)
    private static class ActivityCategories
    {
        public const string AllTypes = "All Types";
        public const string Run = "Run";
        public const string Ride = "Ride";
        public const string Walk = "Walk";
        public const string Swim = "Swim";
        public const string Winter = "Winter Sports";
        public const string Water = "Water Sports";
        public const string Workout = "Workout";
        public const string Racquet = "Racquet Sports";
        public const string Other = "Other";

        public static readonly string[] All = [AllTypes, Run, Ride, Walk, Swim, Winter, Water, Workout, Racquet, Other];
    }

    // Sport types grouped by category (for same-category conversion)
    private static class SportTypes
    {
        public static readonly string[] Ride = ["Ride", "GravelRide", "MountainBikeRide", "EBikeRide", "EMountainBikeRide", "VirtualRide", "Velomobile", "Handcycle"];
        public static readonly string[] Run = ["Run", "TrailRun", "VirtualRun"];
        public static readonly string[] Walk = ["Walk", "Hike"];
        public static readonly string[] Swim = ["Swim"];
        public static readonly string[] Winter = ["AlpineSki", "BackcountrySki", "NordicSki", "Snowboard", "Snowshoe"];
        public static readonly string[] Water = ["Canoeing", "Kayaking", "Rowing", "VirtualRow", "Sail", "StandUpPaddling", "Surfing", "Kitesurf", "Windsurf"];
        public static readonly string[] Workout = ["Crossfit", "Elliptical", "StairStepper", "WeightTraining", "Workout", "Yoga", "Pilates", "HighIntensityIntervalTraining"];
        public static readonly string[] Racquet = ["Badminton", "Pickleball", "Racquetball", "Squash", "TableTennis", "Tennis"];
        public static readonly string[] Other = ["Golf", "IceSkate", "InlineSkate", "RockClimbing", "RollerSki", "Skateboard", "Soccer", "Wheelchair"];

        public static string[] GetSportTypesForCategory(string category)
        {
            return category switch
            {
                ActivityCategories.Ride => Ride,
                ActivityCategories.Run => Run,
                ActivityCategories.Walk => Walk,
                ActivityCategories.Swim => Swim,
                ActivityCategories.Winter => Winter,
                ActivityCategories.Water => Water,
                ActivityCategories.Workout => Workout,
                ActivityCategories.Racquet => Racquet,
                ActivityCategories.Other => Other,
                _ => []
            };
        }

        public static string? GetCategoryForSportType(string sportType)
        {
            if (Ride.Contains(sportType, StringComparer.OrdinalIgnoreCase))
            {
                return ActivityCategories.Ride;
            }

            if (Run.Contains(sportType, StringComparer.OrdinalIgnoreCase))
            {
                return ActivityCategories.Run;
            }

            if (Walk.Contains(sportType, StringComparer.OrdinalIgnoreCase))
            {
                return ActivityCategories.Walk;
            }

            if (Swim.Contains(sportType, StringComparer.OrdinalIgnoreCase))
            {
                return ActivityCategories.Swim;
            }

            if (Winter.Contains(sportType, StringComparer.OrdinalIgnoreCase))
            {
                return ActivityCategories.Winter;
            }

            if (Water.Contains(sportType, StringComparer.OrdinalIgnoreCase))
            {
                return ActivityCategories.Water;
            }

            if (Workout.Contains(sportType, StringComparer.OrdinalIgnoreCase))
            {
                return ActivityCategories.Workout;
            }

            if (Racquet.Contains(sportType, StringComparer.OrdinalIgnoreCase))
            {
                return ActivityCategories.Racquet;
            }

            if (Other.Contains(sportType, StringComparer.OrdinalIgnoreCase))
            {
                return ActivityCategories.Other;
            }

            return null;
        }
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

    // Sport type filter options
    private static class SportTypeFilters
    {
        public const string AllSportTypes = "All sport types";
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
        public const int LowShortTermWarning = 10;
        public const int LowDailyWarning = 100;
        public const int MaxRequestsPer15Min = 100;
        public const int MaxRequestsPerDay = 1000;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Update Activity Type[/]");
        AnsiConsole.WriteLine();

        // Step 1: Get filter options
        AnsiConsole.MarkupLine("[bold yellow]Step 1:[/] Filter Activities");
        AnsiConsole.WriteLine();

        string activityCategory = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]activity category[/]:")
                .AddChoices(ActivityCategories.All), cancellationToken);

        string dateRange = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]date range[/]:")
                .AddChoices(DateRanges.All), cancellationToken);

        // Build sport type filter choices based on selected category
        List<string> sportTypeFilterChoices = [SportTypeFilters.AllSportTypes];
        if (!string.Equals(activityCategory, ActivityCategories.AllTypes, StringComparison.OrdinalIgnoreCase))
        {
            sportTypeFilterChoices.AddRange(SportTypes.GetSportTypesForCategory(activityCategory));
        }

        string sportTypeFilter = SportTypeFilters.AllSportTypes;
        if (sportTypeFilterChoices.Count > 1)
        {
            sportTypeFilter = await AnsiConsole.PromptAsync(
                new SelectionPrompt<string>()
                    .Title("Filter by [green]current sport type[/]:")
                    .AddChoices(sportTypeFilterChoices), cancellationToken);
        }

        // Calculate date filter
        var (after, before) = CalculateDateRange(dateRange);

        // Step 2: Fetch activities
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
                MatchesActivityCategory(a, activityCategory)
                && MatchesSportTypeFilter(a, sportTypeFilter))
            .ToList();

        if (filtered.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities match your filters.[/]");
            WaitForKey();
            return;
        }

        // Step 3: Display activities and select
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold yellow]Step 2:[/] Select Activities ({filtered.Count} found)");
        AnsiConsole.WriteLine();

        DisplayActivitiesTable(filtered);

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
                    .UseConverter(a => $"{a.StartDateLocal:MMM dd} - {Markup.Escape(a.Name)} ({a.SportType})")
                    .AddChoices(filtered), cancellationToken);

            if (selectedActivities.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No activities selected.[/]");
                WaitForKey();
                return;
            }
        }

        // Step 4: Determine valid target sport types based on selected activities
        // All selected activities must be in the same category for conversion
        var categories = selectedActivities
            .Select(a => SportTypes.GetCategoryForSportType(a.SportType))
            .Where(c => c is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (categories.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Selected activities have unknown sport types that cannot be converted.[/]");
            WaitForKey();
            return;
        }

        if (categories.Count > 1)
        {
            AnsiConsole.MarkupLine("[yellow]Selected activities span multiple categories. Please select activities from the same category.[/]");
            AnsiConsole.MarkupLine($"[grey]Categories found: {string.Join(", ", categories)}[/]");
            WaitForKey();
            return;
        }

        string targetCategory = categories[0]!;
        string[] availableSportTypes = SportTypes.GetSportTypesForCategory(targetCategory);

        // Step 5: Select target sport type
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 3:[/] Choose New Sport Type");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"[grey]Category: {targetCategory}[/]");
        AnsiConsole.WriteLine();

        string targetSportType = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]new sport type[/]:")
                .AddChoices(availableSportTypes), cancellationToken);

        // Step 6: Confirm
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 4:[/] Confirm Update");
        AnsiConsole.WriteLine();

        var confirmTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        (int shortTermRemaining, int dailyRemaining) = rateLimiter.GetRemainingRequests();

        confirmTable.AddRow("Activities to update", $"[bold]{selectedActivities.Count}[/]");
        confirmTable.AddRow("New sport type", $"[bold]{targetSportType}[/]");
        confirmTable.AddRow("Rate limit (15-min)", $"{shortTermRemaining}/{RateLimitThresholds.MaxRequestsPer15Min}");
        confirmTable.AddRow("Rate limit (daily)", $"{dailyRemaining}/{RateLimitThresholds.MaxRequestsPerDay}");

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
                        await apiClient.UpdateActivitySportTypeAsync(activity.Id, targetSportType, cancellationToken);
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
            AnsiConsole.MarkupLine($"[green]Successfully updated {successCount} activities to {targetSportType}.[/]");
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

    private static void DisplayActivitiesTable(List<StravaActivity> activities)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Date")
            .AddColumn("Name")
            .AddColumn("Sport Type")
            .AddColumn("Distance");

        foreach (var activity in activities.Take(DisplayLimits.ActivitiesTablePreviewCount))
        {
            string truncatedName = activity.Name.Length > DisplayLimits.ActivityNameMaxLength
                ? Markup.Escape(activity.Name[..DisplayLimits.ActivityNameTruncatedLength]) + "..."
                : Markup.Escape(activity.Name);

            table.AddRow(
                activity.StartDateLocal.ToString("MMM dd", CultureInfo.InvariantCulture),
                truncatedName,
                activity.SportType,
                activity.FormattedDistance);
        }

        if (activities.Count > DisplayLimits.ActivitiesTablePreviewCount)
        {
            table.AddRow("[grey]...[/]", $"[grey]and {activities.Count - DisplayLimits.ActivitiesTablePreviewCount} more[/]", "", "");
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

    private static bool MatchesActivityCategory(StravaActivity activity, string category)
    {
        if (string.Equals(category, ActivityCategories.AllTypes, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string? activityCategory = SportTypes.GetCategoryForSportType(activity.SportType);
        return string.Equals(activityCategory, category, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSportTypeFilter(StravaActivity activity, string filter)
    {
        if (string.Equals(filter, SportTypeFilters.AllSportTypes, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(activity.SportType, filter, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateProgressWithRateLimitInfo(ProgressTask task, int totalActivities)
    {
        (int shortTerm, int daily) = rateLimiter.GetRemainingRequests();
        (var waitTime, string? waitReason) = rateLimiter.GetEstimatedWaitTime();

        // If we need to wait, show the wait time
        if (waitTime.HasValue && waitReason is not null)
        {
            string waitTimeFormatted = FormatWaitTime(waitTime.Value);
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

    private static string FormatWaitTime(TimeSpan waitTime)
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

    private static void WaitForKey()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(intercept: true);
    }
}
