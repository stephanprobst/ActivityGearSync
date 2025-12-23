namespace Strava.Console.Models;

public sealed class ApiCredentials
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
}
