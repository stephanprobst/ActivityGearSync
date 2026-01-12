using ActivityGearSync.Models;

namespace ActivityGearSync.Interfaces;

public interface IStravaAuthClient
{
    Task<StravaTokens?> GetValidTokensAsync();

    Task<StravaTokens> AuthenticateAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync();
}
