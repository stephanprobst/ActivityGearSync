using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Spectre.Console;
using ActivityGearSync.Commands;
using ActivityGearSync.Infrastructure;
using ActivityGearSync.Models;
using ActivityGearSync.Services;

// Setup DI
var services = new ServiceCollection();
ConfigureServices(services);
var serviceProvider = services.BuildServiceProvider();

// Run application
await RunApplicationAsync(serviceProvider);

static void ConfigureServices(IServiceCollection services)
{
    // Infrastructure
    services.AddSingleton<RateLimiter>();
    services.AddTransient<RateLimitHandler>();

    // Configure HttpClient for Strava API with resilience
    services.AddHttpClient<StravaApiClient>()
        .AddHttpMessageHandler<RateLimitHandler>()
        .AddResilienceHandler("StravaApi", builder =>
        {
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests ||
                    args.Outcome.Result?.StatusCode >= HttpStatusCode.InternalServerError ||
                    args.Outcome.Exception is HttpRequestException),
                DelayGenerator = args =>
                {
                    // Use Retry-After header if available for 429 responses
                    if (args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var retryAfter = RateLimitHeaderParser.ParseRetryAfter(args.Outcome.Result.Headers);
                        if (retryAfter.HasValue)
                        {
                            return ValueTask.FromResult<TimeSpan?>(retryAfter.Value);
                        }
                    }

                    return ValueTask.FromResult<TimeSpan?>(null); // Use default backoff
                }
            });
        });

    // Default HttpClient for other services
    services.AddHttpClient();

    // Services
    services.AddSingleton<TokenStorageService>();
    services.AddSingleton<StravaAuthClient>();

    // Commands
    services.AddTransient<SetupCommand>();
    services.AddTransient<AuthenticateCommand>();
    services.AddTransient<UpdateGearCommand>();
    services.AddTransient<UpdateSportTypeCommand>();
}

static async Task RunApplicationAsync(ServiceProvider serviceProvider)
{
    var tokenStorage = serviceProvider.GetRequiredService<TokenStorageService>();
    var authService = serviceProvider.GetRequiredService<StravaAuthClient>();

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
        AnsiConsole.MarkupLine("[yellow]Welcome! It looks like this is your first time using Activity Gear Sync.[/]");
        AnsiConsole.WriteLine();

        var setupCommand = serviceProvider.GetRequiredService<SetupCommand>();
        await setupCommand.ExecuteAsync(cts.Token);
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

        bool isAuthenticated = tokens is not null;
        var choices = BuildMenuChoices(isAuthenticated);

        var choice = await AnsiConsole.PromptAsync(new SelectionPrompt<MenuItem>()
                .Title("What would you like to do?")
                .PageSize(10)
                .UseConverter(item => item.DisplayText)
                .AddChoices(choices), cts.Token);

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
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            AnsiConsole.MarkupLine("Press any key to continue...");
            Console.ReadKey(intercept: true);
        }
    }
}

static List<MenuItem> BuildMenuChoices(bool isAuthenticated)
{
    List<MenuItem> choices = [];

    if (isAuthenticated)
    {
        choices.Add(new MenuItem(MenuChoice.UpdateGear, "Update Gear on Activities"));
        choices.Add(new MenuItem(MenuChoice.UpdateSportType, "Update Activity Type"));
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
    StravaAuthClient authClient,
    CancellationToken cancellationToken)
{
    switch (choice)
    {
        case MenuChoice.UpdateGear:
            var updateGearCommand = serviceProvider.GetRequiredService<UpdateGearCommand>();
            await updateGearCommand.ExecuteAsync(cancellationToken);
            break;

        case MenuChoice.UpdateSportType:
            var updateSportTypeCommand = serviceProvider.GetRequiredService<UpdateSportTypeCommand>();
            await updateSportTypeCommand.ExecuteAsync(cancellationToken);
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
            await authClient.LogoutAsync();
            AnsiConsole.MarkupLine("[green]Logged out successfully.[/]");
            AnsiConsole.MarkupLine("Press any key to continue...");
            Console.ReadKey(intercept: true);
            break;

        case MenuChoice.ReconfigureCredentials:
            var setupCommand = serviceProvider.GetRequiredService<SetupCommand>();
            await setupCommand.ExecuteAsync(cancellationToken);
            break;

        case MenuChoice.Exit:
        case MenuChoice.Separator:
            break;
    }
}

static void DisplayHeader()
{
    AnsiConsole.Clear();

    var rule = new Rule($"[orange1]Activity Gear Sync[/] [grey]v{ThisAssembly.AssemblyInformationalVersion}[/]")
    {
        Justification = Justify.Center,
        Style = Style.Parse("orange1")
    };
    AnsiConsole.Write(rule);
    AnsiConsole.WriteLine();
}

static async Task ViewActivitiesAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken)
{
    var apiService = serviceProvider.GetRequiredService<StravaApiClient>();

    AnsiConsole.Clear();
    AnsiConsole.MarkupLine("[bold]My Activities[/]");
    AnsiConsole.WriteLine();

    string dateRange = await AnsiConsole.PromptAsync(new SelectionPrompt<string>()
            .Title("Select [green]date range[/]:")
            .AddChoices("Last 7 days", "Last 30 days", "Last 90 days", "This year", "All time"), cancellationToken);

    var now = DateTime.Now;
    var (after, _) = dateRange switch
    {
        "Last 7 days" => (now.AddDays(-7), null),
        "Last 30 days" => (now.AddDays(-30), null),
        "Last 90 days" => (now.AddDays(-90), null),
        "This year" => (new DateTime(now.Year, 1, 1), null),
        _ => ((DateTime?)null, (DateTime?)null)
    };

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

            activities = [.. await apiService.GetAllActivitiesAsync(
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
    }
    else
    {
        // Fetch athlete for gear names
        var athlete = await apiService.GetAthleteAsync(cancellationToken);
        var allGear = athlete.AllGear.ToDictionary(g => g.Id, g => g.Name, StringComparer.Ordinal);

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
            string gearName = activity.GearId is null
                ? "[grey]None[/]"
                : allGear.GetValueOrDefault(activity.GearId, "[grey]Unknown[/]");

            table.AddRow(
                activity.StartDateLocal.ToString("MMM dd yyyy", CultureInfo.InvariantCulture),
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
    Console.ReadKey(intercept: true);
}

static async Task ViewGearAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken)
{
    var apiService = serviceProvider.GetRequiredService<StravaApiClient>();

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
        Console.ReadKey(intercept: true);
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

    AnsiConsole.MarkupLine("[grey]Note: Strava does not support adding gear via external tools.[/]");
    AnsiConsole.MarkupLine("[grey]Gear must be managed through the Strava website.[/]");
    AnsiConsole.MarkupLine("[link]https://www.strava.com/settings/gear[/]");
    AnsiConsole.WriteLine();

    if (await AnsiConsole.ConfirmAsync("Would you like to open the Strava gear settings page?", defaultValue: false, cancellationToken))
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.strava.com/settings/gear",
                UseShellExecute = true
            });
            AnsiConsole.MarkupLine("[green]Browser opened.[/]");
        }
        catch
        {
            AnsiConsole.MarkupLine("[yellow]Could not open browser. Please navigate to:[/]");
            AnsiConsole.MarkupLine("[link]https://www.strava.com/settings/gear[/]");
        }
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Press any key to continue...");
    Console.ReadKey(intercept: true);
}

internal enum MenuChoice
{
    UpdateGear,
    UpdateSportType,
    ViewActivities,
    ViewGear,
    Authenticate,
    Logout,
    ReconfigureCredentials,
    Exit,
    Separator
}

internal record MenuItem(MenuChoice Choice, string DisplayText);
