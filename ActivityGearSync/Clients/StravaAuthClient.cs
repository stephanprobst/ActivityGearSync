using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Web;
using ActivityGearSync.Interfaces;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using ActivityGearSync.Storage;

namespace ActivityGearSync.Clients;

public sealed class StravaAuthClient(HttpClient httpClient, TokenStorage tokenStorage) : IStravaAuthClient
{
    private const string AuthorizeUrl = "https://www.strava.com/oauth/authorize";
    private const string TokenUrl = "https://www.strava.com/oauth/token";
    private const string RedirectUri = "http://localhost:5678/callback";
    private const string Scopes = "read,profile:read_all,activity:read_all,activity:write";

    private StravaTokens? _cachedTokens;

    public async Task<StravaTokens?> GetValidTokensAsync()
    {
        if (_cachedTokens is { IsExpired: false })
        {
            return _cachedTokens;
        }

        var tokens = await tokenStorage.LoadTokensAsync();
        if (tokens is null)
        {
            return null;
        }

        if (tokens.IsExpired)
        {
            tokens = await RefreshTokensAsync(tokens.RefreshToken);
            await tokenStorage.SaveTokensAsync(tokens);
        }

        _cachedTokens = tokens;
        return tokens;
    }

    public async Task<StravaTokens> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await tokenStorage.LoadCredentialsAsync()
            ?? throw new InvalidOperationException("API credentials not configured. Please run setup first.");

        string authUrl = BuildAuthorizationUrl(credentials.ClientId);

        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5678/");
        listener.Start();

        OpenBrowser(authUrl);

        string code = await WaitForCallbackAsync(listener, cancellationToken);

        var tokens = await ExchangeCodeForTokensAsync(code, credentials);
        await tokenStorage.SaveTokensAsync(tokens);
        _cachedTokens = tokens;

        return tokens;
    }

    private async Task<StravaTokens> RefreshTokensAsync(string refreshToken)
    {
        var credentials = await tokenStorage.LoadCredentialsAsync()
            ?? throw new InvalidOperationException("API credentials not configured.");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var response = await httpClient.PostAsync(TokenUrl, content);
        response.EnsureSuccessStatusCode();

        var tokens = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.StravaTokens)
            ?? throw new InvalidOperationException("Failed to deserialize token response.");

        return tokens;
    }

    public async Task LogoutAsync()
    {
        await tokenStorage.ClearTokensAsync();
        _cachedTokens = null;
    }

    private static string BuildAuthorizationUrl(string clientId)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = clientId;
        query["response_type"] = "code";
        query["redirect_uri"] = RedirectUri;
        query["scope"] = Scopes;
        query["approval_prompt"] = "auto";
        return $"{AuthorizeUrl}?{query}";
    }

    private static async Task<string> WaitForCallbackAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        var contextTask = listener.GetContextAsync();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        var completedTask = await Task.WhenAny(
            contextTask,
            Task.Delay(Timeout.Infinite, cts.Token)
        );

        if (completedTask != contextTask)
        {
            throw new OperationCanceledException("Authentication timed out.");
        }

        var context = await contextTask;
        string code = context.Request.QueryString["code"]
                      ?? throw new InvalidOperationException("No authorization code received.");

        const string responseHtml = """
                                    <!DOCTYPE html>
                                    <html>
                                    <head><title>Authentication Successful</title></head>
                                    <body style="font-family: sans-serif; text-align: center; padding: 50px;">
                                        <h1>Authentication Successful!</h1>
                                        <p>You can close this window and return to the application.</p>
                                    </body>
                                    </html>
                                    """;

        byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
        context.Response.Close();

        return code;
    }

    private async Task<StravaTokens> ExchangeCodeForTokensAsync(string code, ApiCredentials credentials)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code"
        });

        var response = await httpClient.PostAsync(TokenUrl, content);
        response.EnsureSuccessStatusCode();

        var tokens = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.StravaTokens)
            ?? throw new InvalidOperationException("Failed to deserialize token response.");

        return tokens;
    }

    private static void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
