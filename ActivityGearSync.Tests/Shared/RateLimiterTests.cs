using ActivityGearSync.Shared;
using ActivityGearSync.Tests.Fixtures;

namespace ActivityGearSync.Tests.Shared;

public class RateLimiterTests
{
    [Test]
    public async Task GetRemainingRequests_NoServerInfo_ReturnsFullQuota()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Act
        var (shortTerm, daily) = rateLimiter.GetRemainingRequests();

        // Assert
        await Assert.That(shortTerm).IsEqualTo(100);
        await Assert.That(daily).IsEqualTo(1000);
    }

    [Test]
    public async Task UpdateFromServer_UpdatesRemainingRequests()
    {
        // Arrange
        var rateLimiter = new RateLimiter();
        var info = TestActivityFactory.CreateRateLimitInfo(
            shortTermLimit: 100,
            shortTermUsage: 50,
            dailyLimit: 1000,
            dailyUsage: 200);

        // Act
        rateLimiter.UpdateFromServer(info);
        var (shortTerm, daily) = rateLimiter.GetRemainingRequests();

        // Assert
        await Assert.That(shortTerm).IsEqualTo(50);
        await Assert.That(daily).IsEqualTo(800);
    }

    [Test]
    public async Task GetEstimatedWaitTime_NoServerInfo_ReturnsNull()
    {
        // Arrange
        var rateLimiter = new RateLimiter();

        // Act
        var (waitTime, reason) = rateLimiter.GetEstimatedWaitTime();

        // Assert
        await Assert.That(waitTime).IsNull();
        await Assert.That(reason).IsNull();
    }

    [Test]
    public async Task GetEstimatedWaitTime_ShortTermExhausted_ReturnsWaitTime()
    {
        // Arrange
        var rateLimiter = new RateLimiter();
        var info = TestActivityFactory.CreateRateLimitInfo(
            shortTermUsage: 96); // Remaining is 4, below safety buffer of 5
        rateLimiter.UpdateFromServer(info);

        // Act
        var (waitTime, reason) = rateLimiter.GetEstimatedWaitTime();

        // Assert
        await Assert.That(waitTime).IsNotNull();
        await Assert.That(reason).IsEqualTo("15-min limit");
    }

    [Test]
    public async Task GetEstimatedWaitTime_DailyExhausted_ReturnsWaitUntilMidnight()
    {
        // Arrange
        var rateLimiter = new RateLimiter();
        var info = TestActivityFactory.CreateRateLimitInfo(
            shortTermUsage: 10,
            dailyUsage: 951); // Remaining is 49, below safety buffer of 50
        rateLimiter.UpdateFromServer(info);

        // Act
        var (waitTime, reason) = rateLimiter.GetEstimatedWaitTime();

        // Assert
        await Assert.That(waitTime).IsNotNull();
        await Assert.That(reason).IsEqualTo("daily limit");
    }

    [Test]
    public async Task RemainingRequests_Property_ReturnsShortTermValue()
    {
        // Arrange
        var rateLimiter = new RateLimiter();
        var info = TestActivityFactory.CreateRateLimitInfo(
            shortTermUsage: 75,
            dailyUsage: 500);
        rateLimiter.UpdateFromServer(info);

        // Act
        int remaining = rateLimiter.RemainingRequests;

        // Assert
        await Assert.That(remaining).IsEqualTo(25);
    }

    [Test]
    public async Task LastServerInfo_AfterUpdate_ReturnsLatestInfo()
    {
        // Arrange
        var rateLimiter = new RateLimiter();
        var info = TestActivityFactory.CreateRateLimitInfo(shortTermUsage: 30);

        // Act
        rateLimiter.UpdateFromServer(info);

        // Assert
        await Assert.That(rateLimiter.LastServerInfo).IsNotNull();
        await Assert.That(rateLimiter.LastServerInfo!.ShortTermUsage).IsEqualTo(30);
    }
}
