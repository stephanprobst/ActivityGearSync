using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using ActivityGearSync.Tests.Fixtures;

namespace ActivityGearSync.Tests.Shared;

public class GearFilterLogicTests
{
    private static List<StravaGear> CreateTestGear()
    {
        return
        [
            new StravaGear { Id = "b1", Name = "Road Bike", Primary = true },
            new StravaGear { Id = "b2", Name = "Mountain Bike", Primary = false },
            new StravaGear { Id = "s1", Name = "Running Shoes", Primary = true }
        ];
    }

    [Test]
    public async Task MatchesGearFilter_AllActivities_AlwaysReturnsTrue()
    {
        var activity = TestActivityFactory.CreateActivity(gearId: "b1");
        var gear = CreateTestGear();

        bool result = GearFilterLogic.MatchesGearFilter(
            activity,
            GearFilterLogic.GearFilters.AllActivities,
            gear);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MatchesGearFilter_NoGearAssigned_MatchesNullGearId()
    {
        var activity = TestActivityFactory.CreateActivity(gearId: null);
        var gear = CreateTestGear();

        bool result = GearFilterLogic.MatchesGearFilter(
            activity,
            GearFilterLogic.GearFilters.NoGearAssigned,
            gear);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MatchesGearFilter_NoGearAssigned_DoesNotMatchWithGear()
    {
        var activity = TestActivityFactory.CreateActivity(gearId: "b1");
        var gear = CreateTestGear();

        bool result = GearFilterLogic.MatchesGearFilter(
            activity,
            GearFilterLogic.GearFilters.NoGearAssigned,
            gear);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task MatchesGearFilter_SpecificGear_MatchesByGearId()
    {
        var activity = TestActivityFactory.CreateActivity(gearId: "b1");
        var gear = CreateTestGear();

        bool result = GearFilterLogic.MatchesGearFilter(
            activity,
            "Road Bike",
            gear);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MatchesGearFilter_SpecificGear_DoesNotMatchDifferentGear()
    {
        var activity = TestActivityFactory.CreateActivity(gearId: "b2");
        var gear = CreateTestGear();

        bool result = GearFilterLogic.MatchesGearFilter(
            activity,
            "Road Bike",
            gear);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task MatchesGearFilter_SpecificGear_IsCaseInsensitive()
    {
        var activity = TestActivityFactory.CreateActivity(gearId: "b1");
        var gear = CreateTestGear();

        bool result = GearFilterLogic.MatchesGearFilter(
            activity,
            "ROAD BIKE",
            gear);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MatchesGearFilter_WithNameTransform_AppliesTransform()
    {
        var activity = TestActivityFactory.CreateActivity(gearId: "b1");
        var gear = CreateTestGear();

        bool result = GearFilterLogic.MatchesGearFilter(
            activity,
            "ROAD_BIKE",
            gear,
            name => name.Replace(" ", "_").ToUpperInvariant());

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task FilterActivities_ByTypeAndGear_ReturnsFilteredList()
    {
        var activities = new List<StravaActivity>
        {
            TestActivityFactory.CreateActivity(id: 1, type: "Run", gearId: "s1"),
            TestActivityFactory.CreateActivity(id: 2, type: "Run", gearId: null),
            TestActivityFactory.CreateActivity(id: 3, type: "Ride", gearId: "b1"),
            TestActivityFactory.CreateActivity(id: 4, type: "Ride", gearId: "b2")
        };
        var gear = CreateTestGear();

        var result = GearFilterLogic.FilterActivities(
            activities,
            ActivityTypes.AllTypes,
            "Road Bike",
            gear);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Id).IsEqualTo(3);
    }

    [Test]
    public async Task FilterActivities_ByTypeOnly_ReturnsMatchingType()
    {
        var activities = new List<StravaActivity>
        {
            TestActivityFactory.CreateActivity(id: 1, type: "Run", gearId: "s1"),
            TestActivityFactory.CreateActivity(id: 2, type: "Run", gearId: null),
            TestActivityFactory.CreateActivity(id: 3, type: "Ride", gearId: "b1")
        };
        var gear = CreateTestGear();

        var result = GearFilterLogic.FilterActivities(
            activities,
            ActivityTypes.Run,
            GearFilterLogic.GearFilters.AllActivities,
            gear);

        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task FilterActivities_NoGearAssigned_ReturnsActivitiesWithoutGear()
    {
        var activities = new List<StravaActivity>
        {
            TestActivityFactory.CreateActivity(id: 1, type: "Run", gearId: "s1"),
            TestActivityFactory.CreateActivity(id: 2, type: "Run", gearId: null),
            TestActivityFactory.CreateActivity(id: 3, type: "Ride", gearId: null)
        };
        var gear = CreateTestGear();

        var result = GearFilterLogic.FilterActivities(
            activities,
            ActivityTypes.AllTypes,
            GearFilterLogic.GearFilters.NoGearAssigned,
            gear);

        await Assert.That(result.Count).IsEqualTo(2);
    }
}
