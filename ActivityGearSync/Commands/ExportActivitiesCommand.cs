using System.Globalization;
using ActivityGearSync.Clients;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using Spectre.Console;

namespace ActivityGearSync.Commands;

public sealed class ExportActivitiesCommand(StravaApiClient apiClient, RateLimiter rateLimiter)
{
    private const string ExportFolder = "exports";

    private sealed record ExportResult(
        StravaActivity Activity,
        string Status,
        string? Error = null);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Export Activities (GPX/TCX)[/]");
        AnsiConsole.WriteLine();

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

        var (after, before) = DateRanges.Calculate(dateRange);

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

        var filtered = activities.Where(a =>
                string.Equals(activityType, ActivityTypes.AllTypes, StringComparison.OrdinalIgnoreCase)
                || ActivityTypes.Matches(a, activityType))
            .ToList();

        if (filtered.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities match your filters.[/]");
            ConsoleHelpers.WaitForKey();
            return;
        }

        int gpsCount = filtered.Count(a => a.HasGps);
        int noGpsCount = filtered.Count - gpsCount;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold yellow]Step 2:[/] Select Activities ({filtered.Count} found, {gpsCount} with GPS)");
        AnsiConsole.WriteLine();

        if (noGpsCount > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Note: {noGpsCount} activities have no GPS data and will be skipped.[/]");
            AnsiConsole.WriteLine();
        }

        DisplayActivitiesTable(filtered);

        var selectedActivities = await ConsoleHelpers.PromptActivitySelectionAsync(
            filtered,
            FormatActivityForSelection,
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

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 3:[/] Choose Export Format");
        AnsiConsole.WriteLine();

        string format = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]export format[/]:")
                .AddChoices(ExportFormats.All), cancellationToken);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Step 4:[/] Confirm Export");
        AnsiConsole.WriteLine();

        string exportPath = Path.Combine(Directory.GetCurrentDirectory(), ExportFolder);
        int exportableCount = selectedActivities.Count(a => a.HasGps);

        var confirmTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        var (shortTermRemaining, dailyRemaining) = rateLimiter.GetRemainingRequests();

        confirmTable.AddRow("Activities selected", $"[bold]{selectedActivities.Count}[/]");
        confirmTable.AddRow("With GPS data", $"[bold]{exportableCount}[/]");
        confirmTable.AddRow("Export format", format);
        confirmTable.AddRow("Output folder", Markup.Escape(exportPath));
        confirmTable.AddRow("Rate limit (15-min)", $"{shortTermRemaining}/{RateLimitThresholds.MaxRequestsPer15Min}");
        confirmTable.AddRow("Rate limit (daily)", $"{dailyRemaining}/{RateLimitThresholds.MaxRequestsPerDay}");

        AnsiConsole.Write(confirmTable);
        AnsiConsole.WriteLine();

        if (exportableCount == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities with GPS data to export.[/]");
            ConsoleHelpers.WaitForKey();
            return;
        }

        if (!await AnsiConsole.ConfirmAsync("Proceed with export?", cancellationToken: cancellationToken))
        {
            AnsiConsole.MarkupLine("[yellow]Export cancelled.[/]");
            ConsoleHelpers.WaitForKey();
            return;
        }

        AnsiConsole.WriteLine();
        FileHelpers.EnsureExportDirectory(exportPath);

        List<ExportResult> results = [];
        bool exportGpx = string.Equals(format, ExportFormats.Gpx, StringComparison.Ordinal)
                         || string.Equals(format, ExportFormats.Both, StringComparison.Ordinal);
        bool exportTcx = string.Equals(format, ExportFormats.Tcx, StringComparison.Ordinal)
                         || string.Equals(format, ExportFormats.Both, StringComparison.Ordinal);

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
                var task = ctx.AddTask($"[green]Exporting {selectedActivities.Count} activities[/]");
                task.MaxValue = selectedActivities.Count;

                foreach (var activity in selectedActivities)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    UpdateProgressWithRateLimitInfo(task, selectedActivities.Count);

                    var result = await ExportActivityAsync(
                        activity, exportPath, exportGpx, exportTcx, cancellationToken);
                    results.Add(result);

                    task.Increment(1);
                }
            });

        DisplaySummary(results, exportPath);
        ConsoleHelpers.WaitForKey();
    }

    private async Task<ExportResult> ExportActivityAsync(
        StravaActivity activity,
        string exportPath,
        bool exportGpx,
        bool exportTcx,
        CancellationToken cancellationToken)
    {
        if (!activity.HasGps)
        {
            return new ExportResult(activity, ExportStatus.NoGps);
        }

        string gpxPath = Path.Combine(exportPath,
            FileHelpers.GenerateExportFileName(activity.StartDateLocal, activity.Id, activity.Name, "gpx"));
        string tcxPath = Path.Combine(exportPath,
            FileHelpers.GenerateExportFileName(activity.StartDateLocal, activity.Id, activity.Name, "tcx"));

        bool gpxExists = exportGpx && File.Exists(gpxPath);
        bool tcxExists = exportTcx && File.Exists(tcxPath);

        bool gpxNeeded = exportGpx && !gpxExists;
        bool tcxNeeded = exportTcx && !tcxExists;

        if (!gpxNeeded && !tcxNeeded)
        {
            return new ExportResult(activity, ExportStatus.Skipped);
        }

        try
        {
            var streams = await apiClient.GetActivityStreamsAsync(activity.Id, cancellationToken);

            if (streams is null || !streams.HasGpsData)
            {
                return new ExportResult(activity, ExportStatus.NoGps);
            }

            if (gpxNeeded)
            {
                GpxExporter.Export(activity, streams, gpxPath);
            }

            if (tcxNeeded)
            {
                TcxExporter.Export(activity, streams, tcxPath);
            }

            return new ExportResult(activity, ExportStatus.Exported);
        }
        catch (Exception ex)
        {
            return new ExportResult(activity, ExportStatus.Failed, ex.Message);
        }
    }

    private static string FormatActivityForSelection(StravaActivity activity)
    {
        string gpsIndicator = activity.HasGps ? "" : " [grey](No GPS)[/]";
        return $"{activity.StartDateLocal:MMM dd} - {Markup.Escape(activity.Name)} ({activity.FormattedDistance}){gpsIndicator}";
    }

    private static void DisplayActivitiesTable(List<StravaActivity> activities)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Date")
            .AddColumn("Name")
            .AddColumn("Type")
            .AddColumn("Distance")
            .AddColumn("GPS");

        foreach (var activity in activities.Take(DisplayLimits.ActivitiesTablePreviewCount))
        {
            string truncatedName = activity.Name.Length > DisplayLimits.ActivityNameMaxLength
                ? Markup.Escape(activity.Name[..DisplayLimits.ActivityNameTruncatedLength]) + "..."
                : Markup.Escape(activity.Name);

            string gpsStatus = activity.HasGps ? "[green]Yes[/]" : "[grey]No[/]";

            table.AddRow(
                activity.StartDateLocal.ToString("MMM dd", CultureInfo.InvariantCulture),
                truncatedName,
                activity.Type,
                activity.FormattedDistance,
                gpsStatus);
        }

        if (activities.Count > DisplayLimits.ActivitiesTablePreviewCount)
        {
            table.AddRow("[grey]...[/]", $"[grey]and {activities.Count - DisplayLimits.ActivitiesTablePreviewCount} more[/]", "", "", "");
        }

        AnsiConsole.Write(table);
    }

    private static void DisplaySummary(List<ExportResult> results, string exportPath)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Export Summary[/]");
        AnsiConsole.WriteLine();

        int exported = results.Count(r => string.Equals(r.Status, ExportStatus.Exported, StringComparison.Ordinal));
        int skipped = results.Count(r => string.Equals(r.Status, ExportStatus.Skipped, StringComparison.Ordinal));
        int noGps = results.Count(r => string.Equals(r.Status, ExportStatus.NoGps, StringComparison.Ordinal));
        int failed = results.Count(r => string.Equals(r.Status, ExportStatus.Failed, StringComparison.Ordinal));

        if (exported > 0)
        {
            AnsiConsole.MarkupLine($"[green]Exported: {exported} activities[/]");
        }

        if (skipped > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Skipped (already exist): {skipped} activities[/]");
        }

        if (noGps > 0)
        {
            AnsiConsole.MarkupLine($"[grey]No GPS data: {noGps} activities[/]");
        }

        if (failed > 0)
        {
            AnsiConsole.MarkupLine($"[red]Failed: {failed} activities[/]");
            var failedResults = results.Where(r => string.Equals(r.Status, ExportStatus.Failed, StringComparison.Ordinal))
                .Take(DisplayLimits.FailedActivitiesPreviewCount);
            foreach (var result in failedResults)
            {
                AnsiConsole.MarkupLine($"  [grey]- {Markup.Escape(result.Activity.Name)}: {Markup.Escape(result.Error ?? "Unknown error")}[/]");
            }

            if (failed > DisplayLimits.FailedActivitiesPreviewCount)
            {
                AnsiConsole.MarkupLine($"  [grey]... and {failed - DisplayLimits.FailedActivitiesPreviewCount} more[/]");
            }
        }

        if (exported > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Files saved to: {Markup.Escape(exportPath)}[/]");
        }
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
            task.Description = $"[green]Exporting {totalActivities} activities[/] [grey]│[/] [{limitColor}]15m: {shortTerm}/{RateLimitThresholds.MaxRequestsPer15Min}[/], [{dailyColor}]daily: {daily}/{RateLimitThresholds.MaxRequestsPerDay}[/]";
        }
        else
        {
            task.Description = $"[green]Exporting {totalActivities} activities[/] [grey]│[/] [dim]15m: {shortTerm}/{RateLimitThresholds.MaxRequestsPer15Min}, daily: {daily}/{RateLimitThresholds.MaxRequestsPerDay}[/]";
        }
    }
}
