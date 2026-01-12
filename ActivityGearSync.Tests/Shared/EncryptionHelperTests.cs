using ActivityGearSync.Shared;

namespace ActivityGearSync.Tests.Shared;

public class EncryptionHelperTests
{
    [Test]
    public async Task EncryptDecrypt_RoundTrip_ReturnsOriginalText()
    {
        // Arrange
        const string originalText = "This is a secret message!";

        // Act
        string encrypted = EncryptionHelper.Encrypt(originalText);
        string decrypted = EncryptionHelper.Decrypt(encrypted);

        // Assert
        await Assert.That(decrypted).IsEqualTo(originalText);
    }

    [Test]
    public async Task Encrypt_EmptyString_ReturnsEmptyString()
    {
        // Act
        string encrypted = EncryptionHelper.Encrypt(string.Empty);

        // Assert
        await Assert.That(encrypted).IsEmpty();
    }

    [Test]
    public async Task Decrypt_EmptyString_ReturnsEmptyString()
    {
        // Act
        string decrypted = EncryptionHelper.Decrypt(string.Empty);

        // Assert
        await Assert.That(decrypted).IsEmpty();
    }

    [Test]
    public async Task Encrypt_ProducesDifferentOutputForSameInput()
    {
        // Due to random IV, encrypting the same text twice should produce different ciphertexts
        const string text = "Same input text";

        // Act
        string encrypted1 = EncryptionHelper.Encrypt(text);
        string encrypted2 = EncryptionHelper.Encrypt(text);

        // Assert
        await Assert.That(encrypted1).IsNotEqualTo(encrypted2);

        // But both should decrypt to the same value
        await Assert.That(EncryptionHelper.Decrypt(encrypted1)).IsEqualTo(text);
        await Assert.That(EncryptionHelper.Decrypt(encrypted2)).IsEqualTo(text);
    }

    [Test]
    public async Task EncryptDecrypt_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        const string text = "Special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?`~";

        // Act
        string encrypted = EncryptionHelper.Encrypt(text);
        string decrypted = EncryptionHelper.Decrypt(encrypted);

        // Assert
        await Assert.That(decrypted).IsEqualTo(text);
    }

    [Test]
    public async Task EncryptDecrypt_UnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        const string text = "Unicode: 中文 日本語 한국어 😀";

        // Act
        string encrypted = EncryptionHelper.Encrypt(text);
        string decrypted = EncryptionHelper.Decrypt(encrypted);

        // Assert
        await Assert.That(decrypted).IsEqualTo(text);
    }

    [Test]
    public async Task EncryptDecrypt_LongText_HandlesCorrectly()
    {
        // Arrange
        string text = new('A', 10000);

        // Act
        string encrypted = EncryptionHelper.Encrypt(text);
        string decrypted = EncryptionHelper.Decrypt(encrypted);

        // Assert
        await Assert.That(decrypted).IsEqualTo(text);
    }

    [Test]
    public async Task Decrypt_InvalidBase64_ThrowsException()
    {
        // Arrange
        const string invalidCiphertext = "not-valid-base64!!!";

        // Act & Assert
        await Assert.That(() => EncryptionHelper.Decrypt(invalidCiphertext))
            .Throws<FormatException>();
    }
}
