using System.Diagnostics;
using ActivityGearSync.Clients;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using Spectre.Console;

namespace ActivityGearSync.Commands;

public sealed class ViewGearCommand(StravaApiClient apiClient)
{
    private static class Columns
    {
        public const string Name = "Name";
        public const string Brand = "Brand";
        public const string Distance = "Distance";
        public const string Primary = "Primary";
    }

    private static class Urls
    {
        public const string StravaGearSettings = "https://www.strava.com/settings/gear";
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]My Gear[/]");
        AnsiConsole.WriteLine();

        StravaAthlete? athlete = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Fetching gear...", async _ =>
                athlete = await apiClient.GetAthleteAsync(cancellationToken));

        if (athlete is null)
        {
            AnsiConsole.MarkupLine("[red]Failed to fetch gear.[/]");
            ConsoleHelpers.WaitForKey();
            return;
        }

        DisplayBikes(athlete);
        AnsiConsole.WriteLine();
        DisplayShoes(athlete);
        AnsiConsole.WriteLine();

        DisplayStravaGearNote();
        AnsiConsole.WriteLine();

        await PromptToOpenStravaAsync(cancellationToken);

        ConsoleHelpers.WaitForKey();
    }

    private static void DisplayBikes(StravaAthlete athlete)
    {
        if (athlete.Bikes.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]Bikes[/]");
            var table = CreateGearTable();

            foreach (var bike in athlete.Bikes)
            {
                AddGearRow(table, bike);
            }

            AnsiConsole.Write(table);
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]No bikes configured.[/]");
        }
    }

    private static void DisplayShoes(StravaAthlete athlete)
    {
        if (athlete.Shoes.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]Shoes[/]");
            var table = CreateGearTable();

            foreach (var shoe in athlete.Shoes)
            {
                AddGearRow(table, shoe);
            }

            AnsiConsole.Write(table);
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]No shoes configured.[/]");
        }
    }

    private static Table CreateGearTable()
    {
        return new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(Columns.Name)
            .AddColumn(Columns.Brand)
            .AddColumn(Columns.Distance)
            .AddColumn(Columns.Primary);
    }

    private static void AddGearRow(Table table, StravaGear gear)
    {
        table.AddRow(
            gear.Name,
            gear.BrandName ?? "[grey]-[/]",
            gear.FormattedDistance,
            gear.Primary ? "[green]Yes[/]" : "[grey]No[/]");
    }

    private static void DisplayStravaGearNote()
    {
        AnsiConsole.MarkupLine("[grey]Note: Strava does not support adding gear via external tools.[/]");
        AnsiConsole.MarkupLine("[grey]Gear must be managed through the Strava website.[/]");
        AnsiConsole.MarkupLine($"[link]{Urls.StravaGearSettings}[/]");
    }

    private static async Task PromptToOpenStravaAsync(CancellationToken cancellationToken)
    {
        if (!await AnsiConsole.ConfirmAsync(
            "Would you like to open the Strava gear settings page?",
            defaultValue: false,
            cancellationToken))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Urls.StravaGearSettings,
                UseShellExecute = true
            });
            AnsiConsole.MarkupLine("[green]Browser opened.[/]");
        }
        catch
        {
            AnsiConsole.MarkupLine("[yellow]Could not open browser. Please navigate to:[/]");
            AnsiConsole.MarkupLine($"[link]{Urls.StravaGearSettings}[/]");
        }
    }
}
