using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Spectre.Console;
using ActivityGearSync.Commands;
using ActivityGearSync.Clients;
using ActivityGearSync.Shared;
using ActivityGearSync.Storage;

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

    // GitHub client for updates
    services.AddHttpClient<GitHubReleaseClient>();

    // Services
    services.AddSingleton<TokenStorage>();
    services.AddSingleton<StravaAuthClient>();

    // Commands
    services.AddTransient<SetupCommand>();
    services.AddTransient<AuthenticateCommand>();
    services.AddTransient<UpdateGearCommand>();
    services.AddTransient<UpdateSportTypeCommand>();
    services.AddTransient<UpdateActivityFlagsCommand>();
    services.AddTransient<UpdateActivityTextCommand>();
    services.AddTransient<UpdateCommand>();
    services.AddTransient<ViewActivitiesCommand>();
    services.AddTransient<ViewGearCommand>();
}

static async Task RunApplicationAsync(ServiceProvider serviceProvider)
{
    var tokenStorage = serviceProvider.GetRequiredService<TokenStorage>();
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
        choices.Add(new MenuItem(MenuChoice.UpdateFlags, "Update Activity Flags"));
        choices.Add(new MenuItem(MenuChoice.UpdateText, "Update Activity Name/Description"));
        choices.Add(new MenuItem(MenuChoice.ViewActivities, "View My Activities"));
        choices.Add(new MenuItem(MenuChoice.ViewGear, "View My Gear"));
    }
    else
    {
        choices.Add(new MenuItem(MenuChoice.Authenticate, "Authenticate with Strava"));
    }

    choices.Add(new MenuItem(MenuChoice.CheckUpdates, "Check for Updates"));
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

        case MenuChoice.UpdateFlags:
            var updateFlagsCommand = serviceProvider.GetRequiredService<UpdateActivityFlagsCommand>();
            await updateFlagsCommand.ExecuteAsync(cancellationToken);
            break;

        case MenuChoice.UpdateText:
            var updateTextCommand = serviceProvider.GetRequiredService<UpdateActivityTextCommand>();
            await updateTextCommand.ExecuteAsync(cancellationToken);
            break;

        case MenuChoice.ViewActivities:
            var viewActivitiesCommand = serviceProvider.GetRequiredService<ViewActivitiesCommand>();
            await viewActivitiesCommand.ExecuteAsync(cancellationToken);
            break;

        case MenuChoice.ViewGear:
            var viewGearCommand = serviceProvider.GetRequiredService<ViewGearCommand>();
            await viewGearCommand.ExecuteAsync(cancellationToken);
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

        case MenuChoice.CheckUpdates:
            var updateCommand = serviceProvider.GetRequiredService<UpdateCommand>();
            await updateCommand.ExecuteAsync(cancellationToken);
            break;

        case MenuChoice.Exit:
        case MenuChoice.Separator:
            break;
    }
}

static void DisplayHeader()
{
    AnsiConsole.Clear();

    var rule = new Rule($"[orange1]Activity Gear Sync[/] [grey]{AppVersion.Version}[/]")
    {
        Justification = Justify.Center,
        Style = Style.Parse("orange1")
    };
    AnsiConsole.Write(rule);
    AnsiConsole.WriteLine();
}

internal enum MenuChoice
{
    UpdateGear,
    UpdateSportType,
    UpdateFlags,
    UpdateText,
    ViewActivities,
    ViewGear,
    Authenticate,
    Logout,
    ReconfigureCredentials,
    CheckUpdates,
    Exit,
    Separator
}

internal record MenuItem(MenuChoice Choice, string DisplayText);
