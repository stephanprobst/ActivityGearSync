using Spectre.Console;
using Strava.Console.Infrastructure;
using Strava.Console.Models;
using Strava.Console.Services;

namespace Strava.Console.Commands;

public sealed class SetupCommand(ITokenStorageService tokenStorage)
{
    public async Task ExecuteAsync()
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

        AnsiConsole.MarkupLine("[bold yellow]Step 1:[/] Register Your API Application");
        AnsiConsole.WriteLine();

        // Generate icon if it doesn't exist
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.png");
        IconGenerator.EnsureIconExists(iconPath);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Field")
            .AddColumn("Value");

        table.AddRow("Application Name", "[green]Activity Editor[/] (or any name, cannot contain 'Strava')");
        table.AddRow("Category", "Any category");
        table.AddRow("Website", "[cyan]http://localhost[/]");
        table.AddRow("Authorization Callback Domain", "[cyan]localhost[/]");
        table.AddRow("Application Icon", $"[cyan]{iconPath}[/]");

        AnsiConsole.MarkupLine("1. Open [link=https://www.strava.com/settings/api]https://www.strava.com/settings/api[/] in your browser");
        AnsiConsole.MarkupLine("2. Fill in the following:");
        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("3. After saving, you'll see your [bold]Client ID[/] and [bold]Client Secret[/]");
        AnsiConsole.WriteLine();

        if (AnsiConsole.Confirm("Would you like to open the Strava API settings page now?"))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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

        AnsiConsole.MarkupLine("[bold yellow]Step 2:[/] Enter Your API Credentials");
        AnsiConsole.WriteLine();

        var clientId = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter your [bold]Client ID[/]:")
                .Validate(id => !string.IsNullOrWhiteSpace(id)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Client ID is required")));

        var clientSecret = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter your [bold]Client Secret[/]:")
                .Secret()
                .Validate(secret => !string.IsNullOrWhiteSpace(secret)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Client Secret is required")));

        var credentials = new ApiCredentials
        {
            ClientId = clientId.Trim(),
            ClientSecret = clientSecret.Trim()
        };

        await AnsiConsole.Status()
            .StartAsync("Saving credentials...", async _ =>
            {
                await tokenStorage.SaveCredentialsAsync(credentials);
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Credentials saved successfully![/]");
        AnsiConsole.MarkupLine("You can now authenticate with Strava.");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("Press any key to continue...");
        System.Console.ReadKey(true);
    }
}
