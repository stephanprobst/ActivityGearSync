using System.Globalization;
using ActivityGearSync.Clients;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using Spectre.Console;

namespace ActivityGearSync.Commands;

public sealed class UpdateActivityTextCommand(StravaApiClient apiClient, RateLimiter rateLimiter)
{
    private static class FieldTypes
    {
        public const string Name = "Activity name";
        public const string Description = "Activity description";

        public static readonly string[] All = [Name, Description];
    }

    private static class Operations
    {
        public const string Set = "Set new value";
        public const string AddPrefix = "Add prefix";
        public const string AddSuffix = "Add suffix";
        public const string FindReplace = "Find & Replace";

        public static readonly string[] All = [Set, AddPrefix, AddSuffix, FindReplace];
    }

    private static class NameFilters
    {
        public const string NoFilter = "No filter";
        public const string Contains = "Name contains...";

        public static readonly string[] All = [NoFilter, Contains];
    }

    private sealed class OperationParams
    {
        public string? NewValue { get; init; }
        public string? Prefix { get; init; }
        public string? Suffix { get; init; }
        public string? FindText { get; init; }
        public string? ReplaceText { get; init; }
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Update Activity Name/Description[/]");
        AnsiConsole.WriteLine();

        // Step 1: Choose field to edit
        AnsiConsole.MarkupLine("[bold yellow]Step 1:[/] Choose Field");
        AnsiConsole.WriteLine();

        string fieldType = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("What do you want to [green]edit[/]?")
                .AddChoices(FieldTypes.All), cancellationToken);

        bool isEditingName = string.Equals(fieldType, FieldTypes.Name, StringComparison.Ordinal);

        // Step 2: Choose operation
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 2:[/] Choose Operation");
        AnsiConsole.WriteLine();

        string operation = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]operation[/]:")
                .AddChoices(Operations.All), cancellationToken);

        // Step 3: Get operation parameters
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 3:[/] Enter Values");
        AnsiConsole.WriteLine();

        var operationParams = await GetOperationParamsAsync(operation, isEditingName, cancellationToken);
        if (operationParams is null)
        {
            return;
        }

        // Step 4: Filter activities
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 4:[/] Filter Activities");
        AnsiConsole.WriteLine();

        string activityType = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]activity type[/]:")
                .AddChoices(ActivityTypes.All), cancellationToken);

        string dateRange = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]date range[/]:")
                .AddChoices(DateRanges.All), cancellationToken);

        string nameFilter = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Filter by [green]name[/]:")
                .AddChoices(NameFilters.All), cancellationToken);

        string? nameContains = null;
        if (string.Equals(nameFilter, NameFilters.Contains, StringComparison.Ordinal))
        {
            nameContains = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter text to search for in activity names:")
                    .PromptStyle("green"), cancellationToken);
        }

        var (after, before) = DateRanges.Calculate(dateRange);

        // Step 5: Fetch activities
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
                    new Progress<(int fetched, int total)>(p => task.Description = $"[green]Fetched {p.fetched} activities...[/]"),
                    after, before, cancellationToken);

                activities.AddRange(result);
                task.IsIndeterminate = false;
                task.Value = 100;
            });

        // Apply filters
        var filtered = activities.Where(a =>
                (string.Equals(activityType, ActivityTypes.AllTypes, StringComparison.OrdinalIgnoreCase)
                 || ActivityTypes.Matches(a, activityType))
                && MatchesNameFilter(a, nameContains))
            .ToList();

        if (filtered.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities match your filters.[/]");
            ConsoleHelpers.WaitForKey();
            return;
        }

        // Step 6: Display activities and select
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold yellow]Step 5:[/] Select Activities ({filtered.Count} found)");
        AnsiConsole.WriteLine();

        DisplayActivitiesTable(filtered, isEditingName);

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
            ConsoleHelpers.WaitForKey();
            return;
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
                    .UseConverter(a => $"{a.StartDateLocal:MMM dd} - {Markup.Escape(a.Name)} ({a.FormattedDistance})")
                    .AddChoices(filtered), cancellationToken);
        }

        if (selectedActivities.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities selected.[/]");
            ConsoleHelpers.WaitForKey();
            return;
        }

        // Step 7: Preview changes
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 6:[/] Preview Changes");
        AnsiConsole.WriteLine();

        DisplayPreviewTable(selectedActivities, isEditingName, operation, operationParams);

        // Step 8: Confirm
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 7:[/] Confirm Update");
        AnsiConsole.WriteLine();

        var confirmTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        var (shortTermRemaining, dailyRemaining) = rateLimiter.GetRemainingRequests();

        confirmTable.AddRow("Activities to update", $"[bold]{selectedActivities.Count}[/]");
        confirmTable.AddRow("Field", isEditingName ? "Name" : "Description");
        confirmTable.AddRow("Operation", operation);
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

        // Step 9: Execute updates
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
                        string? newName = null;
                        string? newDescription = null;

                        if (isEditingName)
                        {
                            newName = ApplyOperation(activity.Name, operation, operationParams);
                            if (string.IsNullOrWhiteSpace(newName))
                            {
                                failedActivities.Add((activity, "Name cannot be empty"));
                                task.Increment(1);
                                continue;
                            }
                        }
                        else
                        {
                            newDescription = ApplyOperation(activity.Description ?? "", operation, operationParams);
                        }

                        await apiClient.UpdateActivityTextAsync(activity.Id, newName, newDescription, cancellationToken);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failedActivities.Add((activity, ex.Message));
                    }

                    task.Increment(1);
                }
            });

        // Step 10: Summary
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

    private static async Task<OperationParams?> GetOperationParamsAsync(
        string operation,
        bool isEditingName,
        CancellationToken cancellationToken)
    {
        string fieldName = isEditingName ? "name" : "description";

        return operation switch
        {
            Operations.Set => new OperationParams
            {
                NewValue = await GetSetValueAsync(fieldName, isEditingName, cancellationToken)
            },
            Operations.AddPrefix => new OperationParams
            {
                Prefix = await AnsiConsole.PromptAsync(
                    new TextPrompt<string>($"Enter prefix to add to {fieldName}:")
                        .PromptStyle("green"), cancellationToken)
            },
            Operations.AddSuffix => new OperationParams
            {
                Suffix = await AnsiConsole.PromptAsync(
                    new TextPrompt<string>($"Enter suffix to add to {fieldName}:")
                        .PromptStyle("green"), cancellationToken)
            },
            Operations.FindReplace => new OperationParams
            {
                FindText = await AnsiConsole.PromptAsync(
                    new TextPrompt<string>("Enter text to find:")
                        .PromptStyle("green"), cancellationToken),
                ReplaceText = await AnsiConsole.PromptAsync(
                    new TextPrompt<string>("Enter replacement text:")
                        .PromptStyle("green")
                        .AllowEmpty(), cancellationToken)
            },
            _ => null
        };
    }

    private static async Task<string> GetSetValueAsync(
        string fieldName,
        bool isEditingName,
        CancellationToken cancellationToken)
    {
        var prompt = new TextPrompt<string>($"Enter new {fieldName}:")
            .PromptStyle("green");

        if (!isEditingName)
        {
            prompt.AllowEmpty();
        }

        return await AnsiConsole.PromptAsync(prompt, cancellationToken);
    }

    private static string ApplyOperation(string original, string operation, OperationParams operationParams)
    {
        return operation switch
        {
            Operations.Set => operationParams.NewValue ?? "",
            Operations.AddPrefix => (operationParams.Prefix ?? "") + original,
            Operations.AddSuffix => original + (operationParams.Suffix ?? ""),
            Operations.FindReplace => original.Replace(
                operationParams.FindText ?? "",
                operationParams.ReplaceText ?? "",
                StringComparison.OrdinalIgnoreCase),
            _ => original
        };
    }

    private static void DisplayActivitiesTable(List<StravaActivity> activities, bool showName)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Date")
            .AddColumn(showName ? "Name" : "Description")
            .AddColumn("Type")
            .AddColumn("Distance");

        foreach (var activity in activities.Take(DisplayLimits.ActivitiesTablePreviewCount))
        {
            string text = showName ? activity.Name : (activity.Description ?? "[grey]<empty>[/]");
            string truncatedText = text.Length > DisplayLimits.TextMaxDisplayLength
                ? Markup.Escape(text[..DisplayLimits.TextTruncatedLength]) + "..."
                : Markup.Escape(text);

            table.AddRow(
                activity.StartDateLocal.ToString("MMM dd", CultureInfo.InvariantCulture),
                truncatedText,
                activity.Type,
                activity.FormattedDistance);
        }

        if (activities.Count > DisplayLimits.ActivitiesTablePreviewCount)
        {
            table.AddRow("[grey]...[/]", $"[grey]and {activities.Count - DisplayLimits.ActivitiesTablePreviewCount} more[/]", "", "");
        }

        AnsiConsole.Write(table);
    }

    private static void DisplayPreviewTable(
        List<StravaActivity> activities,
        bool isEditingName,
        string operation,
        OperationParams operationParams)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Date")
            .AddColumn(isEditingName ? "Current Name" : "Current Description")
            .AddColumn(isEditingName ? "New Name" : "New Description");

        foreach (var activity in activities.Take(DisplayLimits.ActivitiesTablePreviewCount))
        {
            string current = isEditingName ? activity.Name : (activity.Description ?? "");
            string newValue = ApplyOperation(current, operation, operationParams);

            string currentDisplay = string.IsNullOrEmpty(current)
                ? "[grey]<empty>[/]"
                : TruncateText(current);

            string newDisplay = string.IsNullOrEmpty(newValue)
                ? "[grey]<empty>[/]"
                : TruncateText(newValue);

            bool hasChange = !string.Equals(current, newValue, StringComparison.Ordinal);
            string changeIndicator = hasChange ? "[green]" : "[grey]";

            table.AddRow(
                activity.StartDateLocal.ToString("MMM dd", CultureInfo.InvariantCulture),
                currentDisplay,
                $"{changeIndicator}{newDisplay}[/]");
        }

        if (activities.Count > DisplayLimits.ActivitiesTablePreviewCount)
        {
            table.AddRow("[grey]...[/]", $"[grey]and {activities.Count - DisplayLimits.ActivitiesTablePreviewCount} more[/]", "");
        }

        AnsiConsole.Write(table);
    }

    private static string TruncateText(string text)
    {
        return text.Length > DisplayLimits.TextMaxDisplayLength
            ? Markup.Escape(text[..DisplayLimits.TextTruncatedLength]) + "..."
            : Markup.Escape(text);
    }

    private static bool MatchesNameFilter(StravaActivity activity, string? nameContains)
    {
        if (string.IsNullOrEmpty(nameContains))
        {
            return true;
        }

        return activity.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase);
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
