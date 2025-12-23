using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Strava.Console.Commands;
using Strava.Console.Infrastructure;
using Strava.Console.Models;
using Strava.Console.Services;

// Setup DI
var services = new ServiceCollection();
ConfigureServices(services);
var serviceProvider = services.BuildServiceProvider();

// Run application
await RunApplicationAsync(serviceProvider);

static void ConfigureServices(IServiceCollection services)
{
    services.AddHttpClient();

    // Infrastructure
    services.AddSingleton<RateLimiter>();

    // Services
    services.AddSingleton<ITokenStorageService, TokenStorageService>();
    services.AddSingleton<IStravaAuthService, StravaAuthService>();
    services.AddSingleton<IStravaApiService, StravaApiService>();

    // Commands
    services.AddTransient<SetupCommand>();
    services.AddTransient<AuthenticateCommand>();
    services.AddTransient<UpdateGearCommand>();
}

static async Task RunApplicationAsync(ServiceProvider serviceProvider)
{
    var tokenStorage = serviceProvider.GetRequiredService<ITokenStorageService>();
    var authService = serviceProvider.GetRequiredService<IStravaAuthService>();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, _) =>
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[yellow]Exiting...[/]");
        cts.Cancel();
        Environment.Exit(0);
    };

    // Check if first run (no credentials)
    if (!tokenStorage.HasCredentials())
    {
        DisplayHeader();
        AnsiConsole.MarkupLine("[yellow]Welcome! It looks like this is your first time using Strava Activity Editor.[/]");
        AnsiConsole.WriteLine();

        var setupCommand = serviceProvider.GetRequiredService<SetupCommand>();
        await setupCommand.ExecuteAsync();
    }

    // Main loop
    while (!cts.Token.IsCancellationRequested)
    {
        DisplayHeader();

        // Show auth status
        var tokens = await authService.GetValidTokensAsync();
        if (tokens?.Athlete is { } athlete)
        {
            AnsiConsole.MarkupLine($"[green]Logged in as:[/] {athlete.FirstName} {athlete.LastName}");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Not authenticated[/]");
        }
        AnsiConsole.WriteLine();

        var isAuthenticated = tokens is not null;
        var choices = BuildMenuChoices(isAuthenticated);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<MenuItem>()
                .Title("What would you like to do?")
                .PageSize(10)
                .UseConverter(item => item.DisplayText)
                .AddChoices(choices));

        try
        {
            await ExecuteMenuChoiceAsync(choice.Choice, serviceProvider, authService, cts.Token);
            if (choice.Choice is MenuChoice.Exit)
            {
                return;
            }
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            AnsiConsole.MarkupLine("Press any key to continue...");
            Console.ReadKey(true);
        }
    }
}

static List<MenuItem> BuildMenuChoices(bool isAuthenticated)
{
    List<MenuItem> choices = [];

    if (isAuthenticated)
    {
        choices.Add(new MenuItem(MenuChoice.UpdateGear, "Update Gear on Activities"));
        choices.Add(new MenuItem(MenuChoice.ViewActivities, "View My Activities"));
        choices.Add(new MenuItem(MenuChoice.ViewGear, "View My Gear"));
    }
    else
    {
        choices.Add(new MenuItem(MenuChoice.Authenticate, "Authenticate with Strava"));
    }

    choices.Add(new MenuItem(MenuChoice.Separator, "---"));

    if (isAuthenticated)
    {
        choices.Add(new MenuItem(MenuChoice.Authenticate, "Re-authenticate"));
        choices.Add(new MenuItem(MenuChoice.Logout, "Logout"));
    }

    choices.Add(new MenuItem(MenuChoice.ReconfigureCredentials, "Reconfigure API Credentials"));
    choices.Add(new MenuItem(MenuChoice.Exit, "Exit"));

    return choices;
}

static async Task ExecuteMenuChoiceAsync(
    MenuChoice choice,
    ServiceProvider serviceProvider,
    IStravaAuthService authService,
    CancellationToken cancellationToken)
{
    switch (choice)
    {
        case MenuChoice.UpdateGear:
            var updateGearCommand = serviceProvider.GetRequiredService<UpdateGearCommand>();
            await updateGearCommand.ExecuteAsync(cancellationToken);
            break;

        case MenuChoice.ViewActivities:
            await ViewActivitiesAsync(serviceProvider, cancellationToken);
            break;

        case MenuChoice.ViewGear:
            await ViewGearAsync(serviceProvider, cancellationToken);
            break;

        case MenuChoice.Authenticate:
            var authCommand = serviceProvider.GetRequiredService<AuthenticateCommand>();
            await authCommand.ExecuteAsync(cancellationToken);
            break;

        case MenuChoice.Logout:
            await authService.LogoutAsync();
            AnsiConsole.MarkupLine("[green]Logged out successfully.[/]");
            AnsiConsole.MarkupLine("Press any key to continue...");
            Console.ReadKey(true);
            break;

        case MenuChoice.ReconfigureCredentials:
            var setupCommand = serviceProvider.GetRequiredService<SetupCommand>();
            await setupCommand.ExecuteAsync();
            break;

        case MenuChoice.Exit:
        case MenuChoice.Separator:
            break;
    }
}

static void DisplayHeader()
{
    AnsiConsole.Clear();

    var rule = new Rule("[orange1]Strava Activity Editor[/]")
    {
        Justification = Justify.Center,
        Style = Style.Parse("orange1")
    };
    AnsiConsole.Write(rule);
    AnsiConsole.WriteLine();
}

static async Task ViewActivitiesAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken)
{
    var apiService = serviceProvider.GetRequiredService<IStravaApiService>();

    AnsiConsole.Clear();
    AnsiConsole.MarkupLine("[bold]My Activities[/]");
    AnsiConsole.WriteLine();

    var dateRange = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Select [green]date range[/]:")
            .AddChoices("Last 7 days", "Last 30 days", "Last 90 days", "This year", "All time"));

    var now = DateTime.Now;
    var (after, _) = dateRange switch
    {
        "Last 7 days" => ((DateTime?)now.AddDays(-7), (DateTime?)null),
        "Last 30 days" => (now.AddDays(-30), null),
        "Last 90 days" => (now.AddDays(-90), null),
        "This year" => (new DateTime(now.Year, 1, 1), null),
        _ => ((DateTime?)null, (DateTime?)null)
    };

    List<StravaActivity>? activities = null;

    await AnsiConsole.Progress()
        .AutoClear(false)
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new SpinnerColumn())
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[green]Fetching activities...[/]");
            task.IsIndeterminate = true;

            activities = await apiService.GetAllActivitiesAsync(
                new Progress<(int fetched, int total)>(p =>
                {
                    task.Description = $"[green]Fetched {p.fetched} activities...[/]";
                }),
                after, null, cancellationToken);

            task.IsIndeterminate = false;
            task.Value = 100;
        });

    activities ??= [];

    if (activities.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No activities found.[/]");
    }
    else
    {
        // Fetch athlete for gear names
        var athlete = await apiService.GetAthleteAsync(cancellationToken);
        var allGear = athlete.AllGear.ToDictionary(g => g.Id, g => g.Name);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Date")
            .AddColumn("Name")
            .AddColumn("Type")
            .AddColumn("Distance")
            .AddColumn("Duration")
            .AddColumn("Gear");

        foreach (var activity in activities.Take(20))
        {
            var gearName = activity.GearId is null
                ? "[grey]None[/]"
                : allGear.GetValueOrDefault(activity.GearId, "[grey]Unknown[/]");

            table.AddRow(
                activity.StartDateLocal.ToString("MMM dd yyyy"),
                activity.Name.Length > 30 ? activity.Name[..27] + "..." : activity.Name,
                activity.Type,
                activity.FormattedDistance,
                activity.FormattedDuration,
                gearName);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);

        if (activities.Count > 20)
        {
            AnsiConsole.MarkupLine($"[grey]Showing 20 of {activities.Count} activities[/]");
        }
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Press any key to continue...");
    Console.ReadKey(true);
}

static async Task ViewGearAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken)
{
    var apiService = serviceProvider.GetRequiredService<IStravaApiService>();

    AnsiConsole.Clear();
    AnsiConsole.MarkupLine("[bold]My Gear[/]");
    AnsiConsole.WriteLine();

    StravaAthlete? athlete = null;

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Fetching gear...", async _ =>
        {
            athlete = await apiService.GetAthleteAsync(cancellationToken);
        });

    if (athlete is null)
    {
        AnsiConsole.MarkupLine("[red]Failed to fetch gear.[/]");
        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(true);
        return;
    }

    // Display Bikes
    if (athlete.Bikes.Count > 0)
    {
        AnsiConsole.MarkupLine("[bold yellow]Bikes[/]");
        var bikesTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Name")
            .AddColumn("Brand")
            .AddColumn("Distance")
            .AddColumn("Primary");

        foreach (var bike in athlete.Bikes)
        {
            bikesTable.AddRow(
                bike.Name,
                bike.BrandName ?? "[grey]-[/]",
                bike.FormattedDistance,
                bike.Primary ? "[green]Yes[/]" : "[grey]No[/]");
        }

        AnsiConsole.Write(bikesTable);
        AnsiConsole.WriteLine();
    }
    else
    {
        AnsiConsole.MarkupLine("[grey]No bikes configured.[/]");
        AnsiConsole.WriteLine();
    }

    // Display Shoes
    if (athlete.Shoes.Count > 0)
    {
        AnsiConsole.MarkupLine("[bold yellow]Shoes[/]");
        var shoesTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Name")
            .AddColumn("Brand")
            .AddColumn("Distance")
            .AddColumn("Primary");

        foreach (var shoe in athlete.Shoes)
        {
            shoesTable.AddRow(
                shoe.Name,
                shoe.BrandName ?? "[grey]-[/]",
                shoe.FormattedDistance,
                shoe.Primary ? "[green]Yes[/]" : "[grey]No[/]");
        }

        AnsiConsole.Write(shoesTable);
        AnsiConsole.WriteLine();
    }
    else
    {
        AnsiConsole.MarkupLine("[grey]No shoes configured.[/]");
        AnsiConsole.WriteLine();
    }

    if (athlete.Bikes.Count == 0 && athlete.Shoes.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]You haven't configured any gear in Strava yet.[/]");
        AnsiConsole.MarkupLine("Add gear at: [link]https://www.strava.com/settings/gear[/]");
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Press any key to continue...");
    Console.ReadKey(true);
}

enum MenuChoice
{
    UpdateGear,
    ViewActivities,
    ViewGear,
    Authenticate,
    Logout,
    ReconfigureCredentials,
    Exit,
    Separator
}

record MenuItem(MenuChoice Choice, string DisplayText);
