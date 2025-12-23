using Strava.Console.Models;

namespace Strava.Console.Services;

public interface ITokenStorageService
{
    Task<StravaTokens?> LoadTokensAsync();
    Task SaveTokensAsync(StravaTokens tokens);
    Task ClearTokensAsync();
    bool HasStoredTokens();

    Task<ApiCredentials?> LoadCredentialsAsync();
    Task SaveCredentialsAsync(ApiCredentials credentials);
    bool HasCredentials();
}
