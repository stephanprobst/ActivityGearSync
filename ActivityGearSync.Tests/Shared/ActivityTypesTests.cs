using ActivityGearSync.Shared;
using ActivityGearSync.Tests.Fixtures;

namespace ActivityGearSync.Tests.Shared;

public class ActivityTypesTests
{
    [Test]
    [Arguments("Run", true)]
    [Arguments("TrailRun", true)]
    [Arguments("VirtualRun", true)]
    [Arguments("Walk", false)]
    [Arguments("Ride", false)]
    public async Task Matches_RunFilter_MatchesRunTypes(string activityType, bool expected)
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: activityType);

        // Act
        bool matches = ActivityTypes.Matches(activity, ActivityTypes.Run);

        // Assert
        await Assert.That(matches).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Ride", true)]
    [Arguments("MountainBikeRide", true)]
    [Arguments("GravelRide", true)]
    [Arguments("EBikeRide", true)]
    [Arguments("VirtualRide", true)]
    [Arguments("Run", false)]
    public async Task Matches_RideFilter_MatchesRideTypes(string activityType, bool expected)
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: activityType);

        // Act
        bool matches = ActivityTypes.Matches(activity, ActivityTypes.Ride);

        // Assert
        await Assert.That(matches).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Walk", true)]
    [Arguments("Run", false)]
    public async Task Matches_WalkFilter_MatchesWalkOnly(string activityType, bool expected)
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: activityType);

        // Act
        bool matches = ActivityTypes.Matches(activity, ActivityTypes.Walk);

        // Assert
        await Assert.That(matches).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Hike", true)]
    [Arguments("Walk", false)]
    [Arguments("Run", false)]
    public async Task Matches_HikeFilter_MatchesHikeOnly(string activityType, bool expected)
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: activityType);

        // Act
        bool matches = ActivityTypes.Matches(activity, ActivityTypes.Hike);

        // Assert
        await Assert.That(matches).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Swim", true)]
    [Arguments("OpenWaterSwim", true)]
    [Arguments("Run", false)]
    public async Task Matches_SwimFilter_MatchesSwimTypes(string activityType, bool expected)
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: activityType);

        // Act
        bool matches = ActivityTypes.Matches(activity, ActivityTypes.Swim);

        // Assert
        await Assert.That(matches).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Yoga", true)]
    [Arguments("WeightTraining", true)]
    [Arguments("Run", false)]
    [Arguments("Ride", false)]
    [Arguments("Walk", false)]
    [Arguments("Hike", false)]
    [Arguments("Swim", false)]
    public async Task Matches_OtherFilter_MatchesUnknownTypes(string activityType, bool expected)
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: activityType);

        // Act
        bool matches = ActivityTypes.Matches(activity, ActivityTypes.Other);

        // Assert
        await Assert.That(matches).IsEqualTo(expected);
    }

    [Test]
    public async Task Matches_AllTypesFilter_MatchesEverything()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: "AnyRandomType");

        // Act
        bool matches = ActivityTypes.Matches(activity, ActivityTypes.AllTypes);

        // Assert
        await Assert.That(matches).IsTrue();
    }

    [Test]
    public async Task All_ContainsExpectedValues()
    {
        // Assert
        await Assert.That(ActivityTypes.All).Contains(ActivityTypes.AllTypes);
        await Assert.That(ActivityTypes.All).Contains(ActivityTypes.Run);
        await Assert.That(ActivityTypes.All).Contains(ActivityTypes.Ride);
        await Assert.That(ActivityTypes.All).Contains(ActivityTypes.Walk);
        await Assert.That(ActivityTypes.All).Contains(ActivityTypes.Hike);
        await Assert.That(ActivityTypes.All).Contains(ActivityTypes.Swim);
        await Assert.That(ActivityTypes.All).Contains(ActivityTypes.Other);
        await Assert.That(ActivityTypes.All.Length).IsEqualTo(7);
    }
}
