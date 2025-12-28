using System.Security.Cryptography;
using System.Text;

namespace ActivityGearSync.Infrastructure;

public static class EncryptionHelper
{
    private static readonly string KeyFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ActivityGearSync",
        ".key"
    );

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        byte[] key = GetOrCreateKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to encrypted data
        byte[] result = new byte[aes.IV.Length + encryptedBytes.Length];
        aes.IV.CopyTo(result, 0);
        encryptedBytes.CopyTo(result, aes.IV.Length);

        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            return string.Empty;
        }

        byte[] key = GetOrCreateKey();
        byte[] fullCipher = Convert.FromBase64String(encryptedText);

        using var aes = Aes.Create();
        aes.Key = key;

        // Extract IV from beginning of cipher text
        byte[] iv = new byte[aes.BlockSize / 8];
        byte[] cipherBytes = new byte[fullCipher.Length - iv.Length];
        Array.Copy(fullCipher, 0, iv, 0, iv.Length);
        Array.Copy(fullCipher, iv.Length, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] GetOrCreateKey()
    {
        string directory = Path.GetDirectoryName(KeyFilePath)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(KeyFilePath))
        {
            return Convert.FromBase64String(File.ReadAllText(KeyFilePath));
        }

        // Generate new 256-bit key
        byte[] key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllText(KeyFilePath, Convert.ToBase64String(key));

        // Try to hide the key file
        try
        {
            File.SetAttributes(KeyFilePath, FileAttributes.Hidden);
        }
        catch
        {
            // Ignore on platforms that don't support hidden attribute
        }

        return key;
    }
}
