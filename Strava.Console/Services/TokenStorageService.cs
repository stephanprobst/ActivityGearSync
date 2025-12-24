using System.Text.Json;
using Strava.Console.Infrastructure;
using Strava.Console.Models;

namespace Strava.Console.Services;

public sealed class TokenStorageService
{
    private readonly string _storagePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StravaConsole"
    );

    private readonly string _tokensFilePath;
    private readonly string _credentialsFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public TokenStorageService()
    {
        _tokensFilePath = Path.Combine(_storagePath, "tokens.json");
        _credentialsFilePath = Path.Combine(_storagePath, "credentials.json");
    }

    public bool HasStoredTokens() => File.Exists(_tokensFilePath);

    public bool HasCredentials() => File.Exists(_credentialsFilePath);

    public async Task<StravaTokens?> LoadTokensAsync()
    {
        if (!File.Exists(_tokensFilePath))
        {
            return null;
        }

        try
        {
            string encryptedJson = await File.ReadAllTextAsync(_tokensFilePath);
            string json = EncryptionHelper.Decrypt(encryptedJson);
            return JsonSerializer.Deserialize<StravaTokens>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveTokensAsync(StravaTokens tokens)
    {
        EnsureDirectoryExists();
        string json = JsonSerializer.Serialize(tokens, JsonOptions);
        string encryptedJson = EncryptionHelper.Encrypt(json);
        await File.WriteAllTextAsync(_tokensFilePath, encryptedJson);
    }

    public Task ClearTokensAsync()
    {
        if (File.Exists(_tokensFilePath))
        {
            File.Delete(_tokensFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<ApiCredentials?> LoadCredentialsAsync()
    {
        if (!File.Exists(_credentialsFilePath))
        {
            return null;
        }

        try
        {
            string encryptedJson = await File.ReadAllTextAsync(_credentialsFilePath);
            string json = EncryptionHelper.Decrypt(encryptedJson);
            return JsonSerializer.Deserialize<ApiCredentials>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveCredentialsAsync(ApiCredentials credentials)
    {
        EnsureDirectoryExists();
        string json = JsonSerializer.Serialize(credentials, JsonOptions);
        string encryptedJson = EncryptionHelper.Encrypt(json);
        await File.WriteAllTextAsync(_credentialsFilePath, encryptedJson);
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }
}
