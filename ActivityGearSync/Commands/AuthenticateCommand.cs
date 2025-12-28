using Spectre.Console;
using ActivityGearSync.Services;

namespace ActivityGearSync.Commands;

public sealed class AuthenticateCommand(StravaAuthClient authClient)
{
    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Authenticate with Strava[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("This will open your browser to authorize the application.");
        AnsiConsole.MarkupLine("After authorizing, you'll be redirected back automatically.");
        AnsiConsole.WriteLine();

        if (!await AnsiConsole.ConfirmAsync("Proceed with authentication?", cancellationToken: cancellationToken))
        {
            return false;
        }

        try
        {
            var tokens = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Waiting for authorization...", async ctx =>
                {
                    ctx.Status("Opening browser for authorization...");
                    return await authClient.AuthenticateAsync(cancellationToken);
                });

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]Authentication successful![/]");

            if (tokens.Athlete is { } athlete)
            {
                AnsiConsole.MarkupLine($"Welcome, [bold]{athlete.FirstName} {athlete.LastName}[/]!");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Press any key to continue...");
            System.Console.ReadKey(intercept: true);
            return true;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Authentication was cancelled or timed out.[/]");
            AnsiConsole.MarkupLine("Press any key to continue...");
            System.Console.ReadKey(intercept: true);
            return false;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Authentication failed: {ex.Message}[/]");
            AnsiConsole.MarkupLine("Press any key to continue...");
            System.Console.ReadKey(intercept: true);
            return false;
        }
    }
}
