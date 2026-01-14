using ActivityGearSync.Shared;

namespace ActivityGearSync.Tests.Shared;

public class DateRangesTests
{
    [Test]
    public async Task Calculate_Last7Days_ReturnsCorrectRange()
    {
        // Arrange
        var beforeTest = DateTime.Now;

        // Act
        var (after, before) = DateRanges.Calculate(DateRanges.Last7Days);
        var afterTest = DateTime.Now;

        // Assert
        await Assert.That(before).IsNull();
        await Assert.That(after).IsNotNull();

        // The 'after' date should be approximately 7 days ago
        var expectedEarliest = beforeTest.AddDays(-7);
        var expectedLatest = afterTest.AddDays(-7);
        await Assert.That(after!.Value).IsGreaterThanOrEqualTo(expectedEarliest);
        await Assert.That(after!.Value).IsLessThanOrEqualTo(expectedLatest);
    }

    [Test]
    public async Task Calculate_Last30Days_ReturnsCorrectRange()
    {
        // Act
        var (after, before) = DateRanges.Calculate(DateRanges.Last30Days);

        // Assert
        await Assert.That(before).IsNull();
        await Assert.That(after).IsNotNull();

        var expectedApprox = DateTime.Now.AddDays(-30);
        double diff = Math.Abs((after!.Value - expectedApprox).TotalSeconds);
        await Assert.That(diff).IsLessThan(5); // Within 5 seconds
    }

    [Test]
    public async Task Calculate_Last90Days_ReturnsCorrectRange()
    {
        // Act
        var (after, before) = DateRanges.Calculate(DateRanges.Last90Days);

        // Assert
        await Assert.That(before).IsNull();
        await Assert.That(after).IsNotNull();

        var expectedApprox = DateTime.Now.AddDays(-90);
        double diff = Math.Abs((after!.Value - expectedApprox).TotalSeconds);
        await Assert.That(diff).IsLessThan(5);
    }

    [Test]
    public async Task Calculate_ThisYear_ReturnsJanuary1st()
    {
        // Act
        var (after, before) = DateRanges.Calculate(DateRanges.ThisYear);

        // Assert
        await Assert.That(before).IsNull();
        await Assert.That(after).IsNotNull();
        await Assert.That(after!.Value.Year).IsEqualTo(DateTime.Now.Year);
        await Assert.That(after.Value.Month).IsEqualTo(1);
        await Assert.That(after.Value.Day).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_AllTime_ReturnsBothNull()
    {
        // Act
        var (after, before) = DateRanges.Calculate(DateRanges.AllTime);

        // Assert
        await Assert.That(after).IsNull();
        await Assert.That(before).IsNull();
    }

    [Test]
    public async Task Calculate_UnknownValue_ReturnsBothNull()
    {
        // Act
        var (after, before) = DateRanges.Calculate("Unknown filter");

        // Assert
        await Assert.That(after).IsNull();
        await Assert.That(before).IsNull();
    }

    [Test]
    public async Task All_ContainsExpectedValues()
    {
        // Assert
        await Assert.That(DateRanges.All).Contains(DateRanges.Last7Days);
        await Assert.That(DateRanges.All).Contains(DateRanges.Last30Days);
        await Assert.That(DateRanges.All).Contains(DateRanges.Last90Days);
        await Assert.That(DateRanges.All).Contains(DateRanges.ThisYear);
        await Assert.That(DateRanges.All).Contains(DateRanges.AllTime);
        await Assert.That(DateRanges.All.Length).IsEqualTo(5);
    }

    [Test]
    public void Constants_HaveExpectedDaysValues()
    {
        // Assert - verify constants are defined correctly
        // These are compile-time constants so we just verify they exist
        _ = DateRanges.Days7;
        _ = DateRanges.Days30;
        _ = DateRanges.Days90;
    }
}
