using System.Diagnostics;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using ActivityGearSync.Storage;
using Spectre.Console;

namespace ActivityGearSync.Commands;

public sealed class SetupCommand(TokenStorage tokenStorage)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();

        var panel = new Panel(
            new Markup("[bold]First-Time Setup[/]\n\nTo use this application, you need to register an API application."))
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1),
            Header = new PanelHeader("[orange1] Activity Editor [/]")
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // Generate icon if it doesn't exist
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.png");
        IconGenerator.EnsureIconExists(iconPath);

        AnsiConsole.MarkupLine("[bold yellow]Step 1:[/] Create Your API Application");
        AnsiConsole.WriteLine();

        var createTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Field")
            .AddColumn("Value");

        createTable.AddRow("Application Name", "[green]Activity Editor[/] (or any name, cannot contain 'Strava')");
        createTable.AddRow("Category", "Any category");
        createTable.AddRow("Website", "[cyan]http://localhost[/]");
        createTable.AddRow("Authorization Callback Domain", "[cyan]localhost[/]");

        AnsiConsole.MarkupLine("1. Open [link=https://www.strava.com/settings/api]https://www.strava.com/settings/api[/] in your browser");
        AnsiConsole.MarkupLine("2. Fill in the following and click [bold]Create[/]:");
        AnsiConsole.WriteLine();
        AnsiConsole.Write(createTable);
        AnsiConsole.WriteLine();

        if (await AnsiConsole.ConfirmAsync("Would you like to open the Strava API settings page now?", cancellationToken: cancellationToken))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.strava.com/settings/api",
                    UseShellExecute = true
                });
                AnsiConsole.MarkupLine("[green]Browser opened. Please register your application and return here.[/]");
            }
            catch
            {
                AnsiConsole.MarkupLine("[yellow]Could not open browser. Please manually navigate to:[/]");
                AnsiConsole.MarkupLine("[link]https://www.strava.com/settings/api[/]");
            }

            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[bold yellow]Step 2:[/] Upload Application Icon");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("After creating the application, you must upload an icon before you can see your credentials.");
        AnsiConsole.MarkupLine("A default icon has been created for you.");
        AnsiConsole.MarkupLine($"Upload the icon from: [cyan]{iconPath}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Press any key once you've uploaded the icon...");
        Console.ReadKey(intercept: true);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold yellow]Step 3:[/] Enter Your API Credentials");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("After uploading the icon, you'll see your [bold]Client ID[/] and [bold]Client Secret[/].");
        AnsiConsole.WriteLine();

        string clientId = await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Enter your [bold]Client ID[/]:")
                .Validate(id => !string.IsNullOrWhiteSpace(id)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Client ID is required")), cancellationToken);

        string clientSecret = await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Enter your [bold]Client Secret[/]:")
                .Secret()
                .Validate(secret => !string.IsNullOrWhiteSpace(secret)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Client Secret is required")), cancellationToken);

        var credentials = new ApiCredentials
        {
            ClientId = clientId.Trim(),
            ClientSecret = clientSecret.Trim()
        };

        await AnsiConsole.Status()
            .StartAsync("Saving credentials...", async _ =>
                await tokenStorage.SaveCredentialsAsync(credentials));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Credentials saved successfully![/]");
        AnsiConsole.MarkupLine("You can now authenticate with Strava.");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("Press any key to continue...");
        Console.ReadKey(intercept: true);
    }
}
