using System.Xml.Linq;
using ActivityGearSync.Shared;
using ActivityGearSync.Tests.Fixtures;

namespace ActivityGearSync.Tests.Shared;

public class TcxExporterTests
{
    private static readonly XNamespace TcxNs = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";

    [Test]
    public async Task Export_ValidActivity_CreatesValidTcxFile()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(name: "Morning Run");
        var streams = TestActivityFactory.CreateStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tcx");

        try
        {
            // Act
            TcxExporter.Export(activity, streams, tempFile);

            // Assert
            await Assert.That(File.Exists(tempFile)).IsTrue();

            var doc = XDocument.Load(tempFile);
            var root = doc.Root!;

            await Assert.That(root.Name.LocalName).IsEqualTo("TrainingCenterDatabase");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task Export_RunActivity_SetsSportToRunning()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: "Run");
        var streams = TestActivityFactory.CreateStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tcx");

        try
        {
            // Act
            TcxExporter.Export(activity, streams, tempFile);

            // Assert
            var doc = XDocument.Load(tempFile);
            var activityElement = doc.Descendants(TcxNs + "Activity").First();

            await Assert.That(activityElement.Attribute("Sport")?.Value).IsEqualTo("Running");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task Export_RideActivity_SetsSportToBiking()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: "Ride");
        var streams = TestActivityFactory.CreateStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tcx");

        try
        {
            // Act
            TcxExporter.Export(activity, streams, tempFile);

            // Assert
            var doc = XDocument.Load(tempFile);
            var activityElement = doc.Descendants(TcxNs + "Activity").First();

            await Assert.That(activityElement.Attribute("Sport")?.Value).IsEqualTo("Biking");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task Export_ValidActivity_ContainsLapWithTotalTime()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(movingTime: 1800);
        var streams = TestActivityFactory.CreateStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tcx");

        try
        {
            // Act
            TcxExporter.Export(activity, streams, tempFile);

            // Assert
            var doc = XDocument.Load(tempFile);
            var totalTime = doc.Descendants(TcxNs + "TotalTimeSeconds").First();

            await Assert.That(totalTime.Value).IsEqualTo("1800");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task Export_ValidActivity_ContainsTrackpoints()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity();
        var streams = TestActivityFactory.CreateStreams(pointCount: 5);
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tcx");

        try
        {
            // Act
            TcxExporter.Export(activity, streams, tempFile);

            // Assert
            var doc = XDocument.Load(tempFile);
            var trackpoints = doc.Descendants(TcxNs + "Trackpoint").ToList();

            await Assert.That(trackpoints.Count).IsEqualTo(5);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task Export_ValidActivity_IncludesNotesWithActivityName()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(name: "My Test Activity");
        var streams = TestActivityFactory.CreateStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tcx");

        try
        {
            // Act
            TcxExporter.Export(activity, streams, tempFile);

            // Assert
            var doc = XDocument.Load(tempFile);
            var notes = doc.Descendants(TcxNs + "Notes").First();

            await Assert.That(notes.Value).IsEqualTo("My Test Activity");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task Export_NoGpsData_ThrowsInvalidOperationException()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity();
        var streams = TestActivityFactory.CreateEmptyStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tcx");

        // Act & Assert
        await Assert.That(() => TcxExporter.Export(activity, streams, tempFile))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("Run", "Running")]
    [Arguments("TrailRun", "Running")]
    [Arguments("VirtualRun", "Running")]
    [Arguments("Ride", "Biking")]
    [Arguments("MountainBikeRide", "Biking")]
    [Arguments("Walk", "Other")]
    [Arguments("Yoga", "Other")]
    public async Task Export_ActivityType_MapsCorrectly(string activityType, string expectedSport)
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: activityType);
        var streams = TestActivityFactory.CreateStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tcx");

        try
        {
            // Act
            TcxExporter.Export(activity, streams, tempFile);

            // Assert
            var doc = XDocument.Load(tempFile);
            string? sport = doc.Descendants(TcxNs + "Activity").First().Attribute("Sport")?.Value;

            await Assert.That(sport).IsEqualTo(expectedSport);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
