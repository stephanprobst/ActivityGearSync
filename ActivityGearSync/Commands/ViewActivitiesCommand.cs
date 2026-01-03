using System.Globalization;
using ActivityGearSync.Clients;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using Spectre.Console;

namespace ActivityGearSync.Commands;

public sealed class ViewActivitiesCommand(StravaApiClient apiClient)
{
    private static class Columns
    {
        public const string Date = "Date";
        public const string Name = "Name";
        public const string Type = "Type";
        public const string Distance = "Distance";
        public const string Duration = "Duration";
        public const string Gear = "Gear";
    }

    private static class Limits
    {
        public const int MaxDisplayCount = 20;
        public const int NameMaxLength = 30;
        public const int NameTruncatedLength = 27;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]My Activities[/]");
        AnsiConsole.WriteLine();

        string dateRange = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select [green]date range[/]:")
                .AddChoices(DateRanges.All),
            cancellationToken);

        var (after, _) = DateRanges.Calculate(dateRange);

        List<StravaActivity>? activities = null;

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

                activities = [.. await apiClient.GetAllActivitiesAsync(
                    new Progress<(int fetched, int total)>(p =>
                    {
                        task.Description = $"[green]Fetched {p.fetched} activities...[/]";
                    }),
                    after, before: null, cancellationToken)];

                task.IsIndeterminate = false;
                task.Value = 100;
            });

        activities ??= [];

        if (activities.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities found.[/]");
            ConsoleHelpers.WaitForKey();
            return;
        }

        var athlete = await apiClient.GetAthleteAsync(cancellationToken);
        var allGear = athlete.AllGear.ToDictionary(g => g.Id, g => g.Name, StringComparer.Ordinal);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(Columns.Date)
            .AddColumn(Columns.Name)
            .AddColumn(Columns.Type)
            .AddColumn(Columns.Distance)
            .AddColumn(Columns.Duration)
            .AddColumn(Columns.Gear);

        foreach (var activity in activities.Take(Limits.MaxDisplayCount))
        {
            string gearName = activity.GearId is null
                ? "[grey]None[/]"
                : allGear.GetValueOrDefault(activity.GearId, "[grey]Unknown[/]");

            string displayName = activity.Name.Length > Limits.NameMaxLength
                ? activity.Name[..Limits.NameTruncatedLength] + "..."
                : activity.Name;

            table.AddRow(
                activity.StartDateLocal.ToString("MMM dd yyyy", CultureInfo.InvariantCulture),
                displayName,
                activity.Type,
                activity.FormattedDistance,
                activity.FormattedDuration,
                gearName);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);

        if (activities.Count > Limits.MaxDisplayCount)
        {
            AnsiConsole.MarkupLine($"[grey]Showing {Limits.MaxDisplayCount} of {activities.Count} activities[/]");
        }

        ConsoleHelpers.WaitForKey();
    }
}
