using Strava.Console.Models;

namespace Strava.Console.Services;

public interface IStravaAuthService
{
    Task<StravaTokens?> GetValidTokensAsync();
    Task<StravaTokens> AuthenticateAsync(CancellationToken cancellationToken = default);
    Task<StravaTokens> RefreshTokensAsync(string refreshToken);
    Task LogoutAsync();
    bool IsAuthenticated { get; }
}
