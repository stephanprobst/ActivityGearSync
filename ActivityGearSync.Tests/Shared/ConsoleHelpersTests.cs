using ActivityGearSync.Shared;

namespace ActivityGearSync.Tests.Shared;

public class ConsoleHelpersTests
{
    [Test]
    public async Task FormatWaitTime_LessThanMinute_ReturnsSeconds()
    {
        // Arrange
        var waitTime = TimeSpan.FromSeconds(45);

        // Act
        string formatted = ConsoleHelpers.FormatWaitTime(waitTime);

        // Assert
        await Assert.That(formatted).IsEqualTo("45s");
    }

    [Test]
    public async Task FormatWaitTime_LessThanHour_ReturnsMinutesAndSeconds()
    {
        // Arrange
        var waitTime = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30));

        // Act
        string formatted = ConsoleHelpers.FormatWaitTime(waitTime);

        // Assert
        await Assert.That(formatted).IsEqualTo("5m 30s");
    }

    [Test]
    public async Task FormatWaitTime_OverHour_ReturnsHoursAndMinutes()
    {
        // Arrange
        var waitTime = TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(15));

        // Act
        string formatted = ConsoleHelpers.FormatWaitTime(waitTime);

        // Assert
        await Assert.That(formatted).IsEqualTo("2h 15m");
    }

    [Test]
    public async Task FormatWaitTime_ExactlyOneMinute_ReturnsMinutesAndSeconds()
    {
        // Arrange
        var waitTime = TimeSpan.FromMinutes(1);

        // Act
        string formatted = ConsoleHelpers.FormatWaitTime(waitTime);

        // Assert
        await Assert.That(formatted).IsEqualTo("1m 0s");
    }

    [Test]
    public async Task FormatWaitTime_ExactlyOneHour_ReturnsHoursAndMinutes()
    {
        // Arrange
        var waitTime = TimeSpan.FromHours(1);

        // Act
        string formatted = ConsoleHelpers.FormatWaitTime(waitTime);

        // Assert
        await Assert.That(formatted).IsEqualTo("1h 0m");
    }

    [Test]
    public async Task FormatWaitTime_ZeroSeconds_ReturnsZeroSeconds()
    {
        // Arrange
        var waitTime = TimeSpan.Zero;

        // Act
        string formatted = ConsoleHelpers.FormatWaitTime(waitTime);

        // Assert
        await Assert.That(formatted).IsEqualTo("0s");
    }

    [Test]
    public async Task FormatWaitTime_LargeValue_ReturnsHoursAndMinutes()
    {
        // Arrange
        var waitTime = TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59));

        // Act
        string formatted = ConsoleHelpers.FormatWaitTime(waitTime);

        // Assert
        await Assert.That(formatted).IsEqualTo("23h 59m");
    }
}
